using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Railbound.UI.Anim
{
    /// <summary>The full interactive lifecycle of a button.</summary>
    public enum ButtonAnimState
    {
        Idle, HoverIn, HoverOut, PressDown, PressRelease, Click,
        Selected, Deselected, Disabled, Locked, Loading, Success, Error, Focused
    }

    /// <summary>
    /// Drives every button animation state: hover (scale 1.00 -> 1.04, brightness, border/glow,
    /// icon nudge 2-4px), press/click (1.04 -> 0.96 -> 1.00 + flash + sound), selected, disabled,
    /// locked (icon shake), loading (Linear spinner ring), success, error and focus.
    /// Brightness applies to the whole visual group (fill, children, labels) so icon-only
    /// buttons respond too. The Button's transition is forced to None - tweens own all visuals.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class AnimatedButton : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
        ISelectHandler, IDeselectHandler
    {
        public enum RingStyle { None, Square, Chamfer }

        [SerializeField] private Button button;
        [SerializeField] private RectTransform icon;            // optional: nudge/rotate target
        [SerializeField] private RingStyle ringStyle = RingStyle.Square;
        [SerializeField] private Color ringColor = new Color(1f, 0.75f, 0.2f, 1f);
        [SerializeField] private bool rotateIconOnHover;
        [SerializeField] private float iconNudge = UIAnim.IconNudge;

        public ButtonAnimState State { get; private set; } = ButtonAnimState.Idle;

        // tween channels - killing one never disturbs the others
        private readonly object _scaleToken = new object();
        private readonly object _colorToken = new object();
        private readonly object _ringToken = new object();
        private readonly object _rotToken = new object();
        private readonly object _fxToken = new object();

        private RectTransform _rt;
        private Image _ring;
        private Graphic[] _group;              // every graphic under the button (ring excluded)
        private Color[] _groupColors;
        private Vector3 _baseScale;
        private Vector2 _iconBasePos;
        private float _iconBaseRot;
        private float _brightness = 1f;
        private float _flashAmount;
        private Color _flashColor = Color.white;
        private bool _hovered, _pressed, _selected, _focused, _locked, _loading;
        private bool _lastInteractable = true;
        private bool _initialized;

        // ------------------------------------------------------------------ setup

        /// <summary>Editor tooling hook: assign the icon and hover rotation before Play Mode.</summary>
        public void SetIcon(RectTransform iconRect, bool rotateOnHover)
        {
            icon = iconRect;
            rotateIconOnHover = rotateOnHover;
        }

        public void SetRingStyle(RingStyle style) => ringStyle = style;
        public void SetRingColor(Color color) => ringColor = color;

        private void Awake() => EnsureInitialized();

        private void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            if (button == null) button = GetComponent<Button>();
            _rt = (RectTransform)transform;
            _baseScale = _rt.localScale;
            if (button != null)
            {
                button.transition = Selectable.Transition.None; // this component owns visuals now
                _lastInteractable = button.interactable;
                button.onClick.AddListener(PlayClickAnimation);
            }
            if (icon != null)
            {
                _iconBasePos = icon.anchoredPosition;
                _iconBaseRot = icon.localEulerAngles.z;
            }

            EnsureRing();
            CacheGraphics();
        }

        private void EnsureRing()
        {
            if (_ring != null || ringStyle == RingStyle.None) return;

            var existing = transform.Find("HoverRing");
            if (existing != null) _ring = existing.GetComponent<Image>();
            if (_ring != null) return;

            var sprite = Resources.Load<Sprite>(ringStyle == RingStyle.Chamfer ? "UI/Ring_Chamfer" : "UI/Ring_Square");
            if (sprite == null) return;

            var ringObject = new GameObject("HoverRing", typeof(RectTransform), typeof(Image));
            ringObject.transform.SetParent(transform, false);
            ringObject.transform.SetSiblingIndex(0); // behind labels, above the button fill
            _ring = ringObject.GetComponent<Image>();
            _ring.sprite = sprite;
            _ring.type = Image.Type.Sliced;
            _ring.color = new Color(ringColor.r, ringColor.g, ringColor.b, 0f);
            _ring.raycastTarget = false;
            var rect = (RectTransform)ringObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(-3f, -3f);
            rect.offsetMax = new Vector2(3f, 3f);
        }

        /// <summary>Brightness/flash targets: every graphic under the button except the ring.</summary>
        private void CacheGraphics()
        {
            var all = GetComponentsInChildren<Graphic>(true);
            var list = new System.Collections.Generic.List<Graphic>(all.Length);
            for (var i = 0; i < all.Length; i++)
            {
                var graphic = all[i];
                if (graphic == null) continue;
                if (_ring != null && graphic.transform == _ring.transform) continue;
                list.Add(graphic);
            }
            _group = list.ToArray();
            _groupColors = new Color[_group.Length];
            for (var i = 0; i < _group.Length; i++)
                _groupColors[i] = _group[i].color;
        }

        private void ApplyVisuals()
        {
            if (_group == null) return;
            for (var i = 0; i < _group.Length; i++)
            {
                var graphic = _group[i];
                if (graphic == null) continue;
                var c = _groupColors[i];
                graphic.color = _flashAmount > 0f
                    ? Color.Lerp(new Color(c.r * _brightness, c.g * _brightness, c.b * _brightness, c.a), _flashColor, _flashAmount)
                    : new Color(c.r * _brightness, c.g * _brightness, c.b * _brightness, c.a);
            }
        }

        private void OnDisable()
        {
            UITween.Kill(_scaleToken);
            UITween.Kill(_colorToken);
            UITween.Kill(_ringToken);
            UITween.Kill(_rotToken);
            UITween.Kill(_fxToken);
            _hovered = false;
            _pressed = false;
            _brightness = 1f;
            _flashAmount = 0f;
            if (_rt != null) _rt.localScale = _baseScale;
            ApplyVisuals();
            State = RestState();
        }

        // ------------------------------------------------------------------ pointer states

        public void OnPointerEnter(PointerEventData eventData) => HoverIn();

        public void OnPointerExit(PointerEventData eventData) => HoverOut();

        public void OnPointerDown(PointerEventData eventData)
        {
            EnsureInitialized();
            if (!CanInteract()) return;
            _pressed = true;
            State = ButtonAnimState.PressDown;
            Refresh(UIAnim.PressDown, Ease.OutQuad, includeScale: true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            EnsureInitialized();
            if (!_pressed) return;
            _pressed = false;
            State = ButtonAnimState.PressRelease;
            Refresh(UIAnim.PressRelease, Ease.OutCubic, includeScale: true);
        }

        public void OnSelect(BaseEventData eventData)
        {
            EnsureInitialized();
            _focused = true;
            if (!_hovered && !_pressed) State = ButtonAnimState.Focused;
            Refresh(UIAnim.StateChange, UIAnim.HoverEase);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            EnsureInitialized();
            _focused = false;
            State = RestState();
            Refresh(UIAnim.StateChange, UIAnim.HoverEase);
        }

        /// <summary>Hover in: Ease Out, 0.12-0.18s.</summary>
        public void HoverIn()
        {
            EnsureInitialized();
            if (!CanInteract())
            {
                // Locked/unavailable: small shake on the icon (or the whole button).
                if (_locked)
                    IconAnimator.Shake(icon != null ? icon : _rt, 4f, UIAnim.ErrorShake, _fxToken);
                return;
            }

            _hovered = true;
            State = ButtonAnimState.HoverIn;
            UISfx.PlayHover();
            Refresh(UIAnim.Hover, UIAnim.HoverEase, includeScale: true, iconIn: true);
        }

        /// <summary>Hover out: Ease Out back to base.</summary>
        public void HoverOut()
        {
            EnsureInitialized();
            _hovered = false;
            State = RestState();
            Refresh(UIAnim.Hover, UIAnim.HoverEase, includeScale: true, iconIn: false);
        }

        // ------------------------------------------------------------------ persistent states

        public void SetSelected(bool selected)
        {
            EnsureInitialized();
            if (_selected == selected) return;
            _selected = selected;
            State = selected ? ButtonAnimState.Selected : ButtonAnimState.Deselected;
            Refresh(UIAnim.StateChange, UIAnim.HoverEase);
            if (!selected) State = RestState();
        }

        public void SetLocked(bool locked)
        {
            EnsureInitialized();
            if (_locked == locked) return;
            _locked = locked;
            State = locked ? ButtonAnimState.Locked : ButtonAnimState.Idle;
            Refresh(UIAnim.StateChange, UIAnim.HoverEase, includeScale: true);
        }

        public void SetLoading(bool loading)
        {
            EnsureInitialized();
            if (_loading == loading) return;
            _loading = loading;
            State = loading ? ButtonAnimState.Loading : ButtonAnimState.Idle;
            Refresh(UIAnim.StateChange, UIAnim.HoverEase, includeScale: true);

            UITween.Kill(_rotToken);
            if (loading)
            {
                if (_ring != null)
                {
                    var ringRect = (RectTransform)_ring.transform;
                    ringRect.localEulerAngles = Vector3.zero;
                    // Loading indicator: the one place Linear easing is allowed (continuous loop).
                    UITween.Loop(0f, -360f, UIAnim.SpinnerCycle, UIAnim.LoopEase,
                        v => { if (ringRect != null) ringRect.localEulerAngles = new Vector3(0f, 0f, v); },
                        yoyo: false, target: _rotToken);
                }
            }
            else if (_ring != null)
            {
                ((RectTransform)_ring.transform).localEulerAngles = Vector3.zero;
            }
        }

        /// <summary>Success flash: bright green pulse over the whole button, then settles.</summary>
        public void SetSuccess()
        {
            EnsureInitialized();
            State = ButtonAnimState.Success;
            UISfx.PlayClick();
            FlashGroup(new Color(0.35f, 1.6f, 0.7f, 1f), UIAnim.Success,
                () => State = RestState());
        }

        /// <summary>Error flash: red tint + shake, then settles.</summary>
        public void SetError()
        {
            EnsureInitialized();
            State = ButtonAnimState.Error;
            IconAnimator.Shake(_rt, 5f, UIAnim.ErrorShake, _fxToken);
            FlashGroup(new Color(1.8f, 0.35f, 0.4f, 1f), UIAnim.ErrorShake,
                () => State = RestState());
        }

        private void FlashGroup(Color color, float totalDuration, System.Action onDone)
        {
            EnsureInitialized();
            _flashColor = color;
            UITween.Kill(_colorToken);
            UITween.To(0f, 1f, 0.07f, Ease.OutQuad,
                v => { _flashAmount = v; ApplyVisuals(); },
                () =>
                {
                    UITween.To(1f, 0f, totalDuration, Ease.OutCubic,
                        v => { _flashAmount = v; ApplyVisuals(); },
                        () => { _flashAmount = 0f; ApplyVisuals(); onDone?.Invoke(); },
                        0f, _colorToken);
                }, 0f, _colorToken);
        }

        // ------------------------------------------------------------------ click

        private void PlayClickAnimation()
        {
            EnsureInitialized();
            if (!CanInteract()) return;
            State = ButtonAnimState.Click;
            UISfx.PlayClick();

            // 1.04 -> 0.96 -> 1.00 within 0.08-0.15s, plus a short ring flash.
            var pressScale = _baseScale * UIAnim.PressScale;
            var restScale = _hovered ? _baseScale * UIAnim.HoverScale : _baseScale;
            var start = _rt != null ? _rt.localScale : _baseScale;
            UITween.Kill(_scaleToken);
            if (_rt != null)
            {
                UITween.To(0f, 1f, UIAnim.ClickDown, Ease.OutQuad,
                    p => { if (_rt != null) _rt.localScale = Vector3.LerpUnclamped(start, pressScale, p); },
                    () =>
                    {
                        var mid = pressScale;
                        UITween.To(0f, 1f, UIAnim.ClickUp, Ease.OutCubic,
                            p => { if (_rt != null) _rt.localScale = Vector3.LerpUnclamped(mid, restScale, p); },
                            () => State = RestState(), 0f, _scaleToken);
                    }, 0f, _scaleToken);
            }

            if (_ring != null)
            {
                var flash = RingColorAt(UIAnim.RingFlash);
                var rest = RingColorAt(RestRingAlpha());
                UITween.Kill(_ringToken);
                UITween.To(0f, 1f, UIAnim.Flash * 0.4f, Ease.OutQuad,
                    p => { if (_ring != null) _ring.color = Color.LerpUnclamped(_ring.color, flash, p); },
                    () =>
                    {
                        if (_ring == null) return;
                        UITween.To(0f, 1f, UIAnim.Flash, Ease.OutCubic,
                            p => { if (_ring != null) _ring.color = Color.LerpUnclamped(flash, rest, p); },
                            null, 0f, _ringToken);
                    }, 0f, _ringToken);
            }

            if (rotateIconOnHover && icon != null)
                IconAnimator.Bounce(icon, 4f, 0.22f, _fxToken);
        }

        // ------------------------------------------------------------------ visual refresh

        private void Update()
        {
            if (button == null) return;
            if (button.interactable == _lastInteractable) return;
            _lastInteractable = button.interactable;
            Refresh(UIAnim.StateChange, UIAnim.HoverEase, includeScale: true);
        }

        private void Refresh(float duration, Ease ease, bool includeScale = false, bool? iconIn = null)
        {
            EnsureInitialized();

            // Colors are only re-cached from a neutral (untweened, unflashed) state so
            // external recolors (e.g. SetActiveTab) are picked up without drift.
            if (_flashAmount > 0f) _flashAmount = 0f;
            if (Mathf.Approximately(_brightness, 1f)) CacheGraphics();

            // brightness (whole visual group) -----------------------------
            float brightness;
            if (_loading) brightness = UIAnim.BrightnessLoading;
            else if (_locked) brightness = UIAnim.BrightnessLocked;
            else if (button != null && !button.interactable) brightness = UIAnim.BrightnessDisabled;
            else if (_pressed) brightness = UIAnim.BrightnessPressed;
            else if (_hovered) brightness = UIAnim.BrightnessHover;
            else brightness = 1f;
            TweenBrightness(brightness, duration, ease);

            // scale -------------------------------------------------------
            if (includeScale && _rt != null && !_loading)
            {
                Vector3 target;
                if (_locked || (button != null && !button.interactable)) target = _baseScale;
                else if (_pressed) target = _baseScale * UIAnim.PressScale;
                else if (_hovered) target = _baseScale * UIAnim.HoverScale;
                else target = _baseScale;
                UITween.ScaleTo(_rt, target, duration, ease, null, _scaleToken);
            }

            // border / glow ------------------------------------------------
            EnsureRing();
            if (_ring != null)
                RingTo(RestRingAlpha(), duration, ease);

            // icon nudge / rotation ----------------------------------------
            if (icon != null)
            {
                var inside = iconIn ?? _hovered;
                var allowed = CanInteract();
                var pos = _iconBasePos + (allowed && inside ? new Vector2(iconNudge, 0f) : Vector2.zero);
                UITween.MoveAnchoredTo(icon, pos, duration, ease, null, _fxToken);
                if (rotateIconOnHover)
                    UITween.RotateZTo(icon, allowed && inside ? _iconBaseRot + 15f : _iconBaseRot, duration, ease, null, _rotToken);
            }
        }

        private void TweenBrightness(float to, float duration, Ease ease)
        {
            var from = _brightness;
            UITween.Kill(_colorToken);
            UITween.To(from, to, duration, ease,
                v => { _brightness = v; ApplyVisuals(); },
                null, 0f, _colorToken);
        }

        private void RingTo(float alpha, float duration, Ease ease)
        {
            var from = _ring.color;
            var to = RingColorAt(alpha);
            UITween.Kill(_ringToken);
            UITween.To(0f, 1f, duration, ease,
                p => { if (_ring != null) _ring.color = Color.LerpUnclamped(from, to, p); },
                null, 0f, _ringToken);
        }

        private Color RingColorAt(float alpha) => new Color(ringColor.r, ringColor.g, ringColor.b, alpha);

        private float RestRingAlpha()
        {
            if (_loading) return UIAnim.RingSelected;
            if (_locked || (button != null && !button.interactable)) return 0f;
            if (_selected && _hovered) return UIAnim.RingHoverSelected;
            if (_selected) return UIAnim.RingSelected;
            if (_focused && !_hovered) return 0.4f;
            if (_hovered) return UIAnim.RingHover;
            return 0f;
        }

        private ButtonAnimState RestState()
        {
            if (_loading) return ButtonAnimState.Loading;
            if (_locked) return ButtonAnimState.Locked;
            if (button != null && !button.interactable) return ButtonAnimState.Disabled;
            if (_hovered) return ButtonAnimState.HoverIn;
            if (_selected) return ButtonAnimState.Selected;
            if (_focused) return ButtonAnimState.Focused;
            return ButtonAnimState.Idle;
        }

        private bool CanInteract() => button != null && button.interactable && !_locked && !_loading;
    }
}
