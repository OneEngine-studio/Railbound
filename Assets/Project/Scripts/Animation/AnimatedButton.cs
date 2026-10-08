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
    /// Drives every button animation state:
    /// - Full style: soft gold glow halo fades in behind the button (no hard outline), scale
    ///   1.00 -> 1.04, brightness x1.15, icon nudge/rotation.
    /// - Light style (small icon tiles): no glow, scale 1.00 -> 1.03, brightness x1.12, icon nudge.
    /// - Press/click: 1.04 -> 0.96 -> 1.00 + glow flash + sound. Loading spins a Linear ring.
    /// The Button's transition is forced to None - tweens own all visuals.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class AnimatedButton : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
        ISelectHandler, IDeselectHandler
    {
        public enum HoverStyle
        {
            Full,   // glow halo + scale 1.04 + brightness 1.15 (READY, CHECK AWARDS, tabs, cards' cousins)
            Light   // no glow, scale 1.03 + brightness 1.12 (small icon tiles)
        }

        [SerializeField] private Button button;
        [SerializeField] private RectTransform icon;            // optional: nudge/rotate target
        [SerializeField] private HoverStyle hoverStyle = HoverStyle.Full;
        [SerializeField] private Color glowColor = new Color(1f, 0.75f, 0.2f, 1f);
        [SerializeField] private bool rotateIconOnHover;
        [SerializeField] private float iconNudge = UIAnim.IconNudge;

        public ButtonAnimState State { get; private set; } = ButtonAnimState.Idle;

        // tween channels - killing one never disturbs the others
        private readonly object _scaleToken = new object();
        private readonly object _colorToken = new object();
        private readonly object _glowToken = new object();
        private readonly object _ringToken = new object();
        private readonly object _rotToken = new object();
        private readonly object _fxToken = new object();

        private RectTransform _rt;
        private Image _glow;               // soft halo (Full style only)
        private Image _ring;               // static button border outline (NEVER rotates)
        private Image _spinner;            // small 22x22 rotating spinner icon inside button
        private Graphic[] _group;          // every graphic under the button (glow + ring + spinner excluded)
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

        private float HoverScaleValue => hoverStyle == HoverStyle.Light ? UIAnim.HoverScaleLight : UIAnim.HoverScale;
        private float HoverBrightness => hoverStyle == HoverStyle.Light ? UIAnim.BrightnessHoverLight : UIAnim.BrightnessHover;

        // ------------------------------------------------------------------ setup

        /// <summary>Editor tooling hook: assign the icon and hover rotation before Play Mode.</summary>
        public void SetIcon(RectTransform iconRect, bool rotateOnHover)
        {
            icon = iconRect;
            rotateIconOnHover = rotateOnHover;
        }

        /// <summary>Full = soft glow + 1.04 scale; Light = no glow, 1.03 scale (icon tiles).</summary>
        public void SetHoverStyle(HoverStyle style) => hoverStyle = style;
        public void SetGlowColor(Color color) => glowColor = color;

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

            EnsureGlow();
            CacheGraphics();
        }

        /// <summary>Soft radial halo behind the button - the hover/selection highlight.</summary>
        private void EnsureGlow()
        {
            if (_glow != null || hoverStyle != HoverStyle.Full) return;

            var existing = transform.Find("HoverGlow");
            if (existing != null) _glow = existing.GetComponent<Image>();
            if (_glow != null) return;

            var sprite = Resources.Load<Sprite>("UI/Glow_Square");
            if (sprite == null) return;

            var glowObject = new GameObject("HoverGlow", typeof(RectTransform), typeof(Image));
            glowObject.transform.SetParent(transform, false);
            glowObject.transform.SetSiblingIndex(0); // first child: above the fill, below labels
            _glow = glowObject.GetComponent<Image>();
            _glow.sprite = sprite;
            _glow.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);
            _glow.raycastTarget = false;
            var rect = (RectTransform)glowObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            var outset = UIAnim.GlowOutset;
            rect.offsetMin = new Vector2(-outset, -outset);
            rect.offsetMax = new Vector2(outset, outset);
        }

        /// <summary>Static button border outline - strictly pinned to button bounds, NEVER rotated.</summary>
        private void EnsureRing()
        {
            if (_ring != null) return;

            var existing = transform.Find("HoverRing");
            if (existing != null) _ring = existing.GetComponent<Image>();
            if (_ring != null)
            {
                ((RectTransform)_ring.transform).localEulerAngles = Vector3.zero;
                return;
            }

            var sprite = Resources.Load<Sprite>("UI/Ring_Square") ?? Resources.Load<Sprite>("UI/Ring_Chamfer");
            if (sprite == null) return;

            var ringObject = new GameObject("HoverRing", typeof(RectTransform), typeof(Image));
            ringObject.transform.SetParent(transform, false);
            _ring = ringObject.GetComponent<Image>();
            _ring.sprite = sprite;
            _ring.type = Image.Type.Sliced;
            _ring.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);
            _ring.raycastTarget = false;
            var rect = (RectTransform)ringObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(-2f, -2f);
            rect.offsetMax = new Vector2(2f, 2f);
            rect.localEulerAngles = Vector3.zero;
        }

        /// <summary>Small 22x22 rotating spinner icon inside the button during loading.</summary>
        private void EnsureSpinner()
        {
            if (_spinner != null) return;

            var existing = transform.Find("LoadingSpinner");
            if (existing != null) _spinner = existing.GetComponent<Image>();
            if (_spinner != null) return;

            var sprite = Resources.Load<Sprite>("UI/Ring_Chamfer") ?? Resources.Load<Sprite>("UI/Ring_Square");
            if (sprite == null) return;

            var spinnerObj = new GameObject("LoadingSpinner", typeof(RectTransform), typeof(Image));
            spinnerObj.transform.SetParent(transform, false);
            spinnerObj.transform.SetAsLastSibling();
            _spinner = spinnerObj.GetComponent<Image>();
            _spinner.sprite = sprite;
            _spinner.type = Image.Type.Simple;
            _spinner.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);
            _spinner.raycastTarget = false;

            var sRect = (RectTransform)spinnerObj.transform;
            sRect.anchorMin = new Vector2(0.5f, 0.5f);
            sRect.anchorMax = new Vector2(0.5f, 0.5f);
            sRect.pivot = new Vector2(0.5f, 0.5f);
            sRect.sizeDelta = new Vector2(22f, 22f);
            sRect.localEulerAngles = Vector3.zero;
            UpdateSpinnerPosition();
        }

        private void UpdateSpinnerPosition()
        {
            if (_spinner == null) return;
            var sRect = (RectTransform)_spinner.transform;
            var label = GetComponentInChildren<TMPro.TMP_Text>(true);
            if (label != null && !string.IsNullOrEmpty(label.text))
            {
                var textWidth = label.preferredWidth;
                sRect.anchoredPosition = new Vector2(-textWidth * 0.5f - 20f, 0f);
            }
            else
            {
                sRect.anchoredPosition = Vector2.zero;
            }
        }

        /// <summary>Brightness/flash targets: every graphic under the button except glow/ring/spinner.</summary>
        private void CacheGraphics()
        {
            var all = GetComponentsInChildren<Graphic>(true);
            var list = new System.Collections.Generic.List<Graphic>(all.Length);
            for (var i = 0; i < all.Length; i++)
            {
                var graphic = all[i];
                if (graphic == null) continue;
                var t = graphic.transform;
                if (_glow != null && t == _glow.transform) continue;
                if (_ring != null && t == _ring.transform) continue;
                if (_spinner != null && t == _spinner.transform) continue;
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
            UITween.Kill(_glowToken);
            UITween.Kill(_ringToken);
            UITween.Kill(_rotToken);
            UITween.Kill(_fxToken);
            _hovered = false;
            _pressed = false;
            _brightness = 1f;
            _flashAmount = 0f;
            if (_rt != null) _rt.localScale = _baseScale;
            ApplyVisuals();
            if (_glow != null) _glow.color = ColorWithAlpha(0f);
            if (_ring != null) ((RectTransform)_ring.transform).localEulerAngles = Vector3.zero;
            if (_spinner != null)
            {
                _spinner.gameObject.SetActive(false);
                ((RectTransform)_spinner.transform).localEulerAngles = Vector3.zero;
            }
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

            // Border ALWAYS stays static around the button - NEVER rotated!
            if (_ring != null)
            {
                ((RectTransform)_ring.transform).localEulerAngles = Vector3.zero;
            }

            if (loading)
            {
                EnsureRing();
                EnsureSpinner();
                if (_spinner != null)
                {
                    UpdateSpinnerPosition();
                    _spinner.gameObject.SetActive(true);
                    _spinner.color = new Color(glowColor.r, glowColor.g, glowColor.b, 1f);
                    var sRect = (RectTransform)_spinner.transform;
                    sRect.localEulerAngles = Vector3.zero;
                    // Rotate ONLY the small 22x22 spinner icon!
                    UITween.Loop(0f, -360f, UIAnim.SpinnerCycle, UIAnim.LoopEase,
                        v => { if (sRect != null) sRect.localEulerAngles = new Vector3(0f, 0f, v); },
                        yoyo: false, target: _rotToken);
                }
            }
            else
            {
                if (_spinner != null)
                {
                    _spinner.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);
                    _spinner.gameObject.SetActive(false);
                }
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

            // 1.04 -> 0.96 -> 1.00 within 0.08-0.15s, plus a short glow flash.
            var pressScale = _baseScale * UIAnim.PressScale;
            var restScale = _hovered ? _baseScale * HoverScaleValue : _baseScale;
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

            if (_glow != null)
            {
                var rest = RestGlowAlpha();
                UITween.Kill(_glowToken);
                UITween.To(rest, UIAnim.GlowFlash, UIAnim.Flash * 0.4f, Ease.OutQuad,
                    v => SetGlowAlpha(v),
                    () =>
                    {
                        UITween.To(UIAnim.GlowFlash, rest, UIAnim.Flash, Ease.OutCubic,
                            v => SetGlowAlpha(v), null, 0f, _glowToken);
                    }, 0f, _glowToken);
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
            // external recolors (e.g. SetActiveTab gold labels) are picked up without drift.
            if (_flashAmount > 0f) _flashAmount = 0f;
            if (Mathf.Approximately(_brightness, 1f)) CacheGraphics();

            // brightness (whole visual group) -----------------------------
            float brightness;
            if (_loading) brightness = UIAnim.BrightnessLoading;
            else if (_locked) brightness = UIAnim.BrightnessLocked;
            else if (button != null && !button.interactable) brightness = UIAnim.BrightnessDisabled;
            else if (_pressed) brightness = UIAnim.BrightnessPressed;
            else if (_hovered) brightness = HoverBrightness;
            else brightness = 1f;
            TweenBrightness(brightness, duration, ease);

            // scale -------------------------------------------------------
            if (includeScale && _rt != null && !_loading)
            {
                Vector3 target;
                if (_locked || (button != null && !button.interactable)) target = _baseScale;
                else if (_pressed) target = _baseScale * UIAnim.PressScale;
                else if (_hovered) target = _baseScale * HoverScaleValue;
                else target = _baseScale;
                UITween.ScaleTo(_rt, target, duration, ease, null, _scaleToken);
            }

            // glow halo (border/glow appears) ------------------------------
            EnsureGlow();
            if (_glow != null)
                GlowTo(RestGlowAlpha(), duration, ease);

            // loading spinner ring ------------------------------------------
            if (_loading)
            {
                EnsureRing();
                RingTo(UIAnim.RingSpinner, duration, ease);
            }
            else if (_ring != null)
            {
                RingTo(0f, duration, ease);
            }

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

        private void GlowTo(float alpha, float duration, Ease ease)
        {
            if (_glow == null) return;
            var from = _glow.color.a;
            UITween.Kill(_glowToken);
            UITween.To(from, alpha, duration, ease,
                v => SetGlowAlpha(v), null, 0f, _glowToken);
        }

        private void SetGlowAlpha(float alpha)
        {
            if (_glow != null) _glow.color = ColorWithAlpha(alpha);
        }

        private Color ColorWithAlpha(float alpha) => new Color(glowColor.r, glowColor.g, glowColor.b, alpha);

        private void RingTo(float alpha, float duration, Ease ease)
        {
            if (_ring == null) return;
            var from = _ring.color;
            var to = new Color(glowColor.r, glowColor.g, glowColor.b, alpha);
            UITween.Kill(_ringToken);
            UITween.To(0f, 1f, duration, ease,
                p => { if (_ring != null) _ring.color = Color.LerpUnclamped(from, to, p); },
                null, 0f, _ringToken);
        }

        private float RestGlowAlpha()
        {
            if (_loading) return UIAnim.GlowLoading;
            if (_locked || (button != null && !button.interactable)) return 0f;
            if (hoverStyle == HoverStyle.Light) return 0f;   // tiles: brightness + nudge only
            if (_selected && _hovered) return UIAnim.GlowHoverSelected;
            if (_selected) return UIAnim.GlowSelected;
            if (_focused && !_hovered) return UIAnim.GlowFocused;
            if (_hovered) return UIAnim.GlowHover;
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
