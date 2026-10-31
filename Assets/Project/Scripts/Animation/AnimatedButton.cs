using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace Railbound.UI.Anim
{
    public enum ButtonAnimState
    {
        Idle, HoverIn, HoverOut, PressDown, PressRelease, Click,
        Selected, Deselected, Disabled, Locked, Loading, Success, Error, Focused
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class AnimatedButton : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
        ISelectHandler, IDeselectHandler
    {
        public enum HoverStyle { Full, Light }

        [SerializeField] private Button button;
        [SerializeField] private RectTransform icon;
        [SerializeField] private HoverStyle hoverStyle = HoverStyle.Full;
        [SerializeField] private Color glowColor = new Color(1f, 0.75f, 0.2f, 1f);
        [SerializeField] private bool rotateIconOnHover;
        [SerializeField] private float iconNudge = UIAnim.IconNudge;

        public ButtonAnimState State { get; private set; } = ButtonAnimState.Idle;

        private readonly object _scaleToken = new object();
        private readonly object _colorToken = new object();
        private readonly object _glowToken = new object();
        private readonly object _rotToken = new object();
        private readonly object _fxToken = new object();

        private RectTransform _rt;
        private Image _glow;
        private Image _spinner;
        private TMP_Text _label;
        private Graphic[] _group;
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

        public void SetIcon(RectTransform iconRect, bool rotateOnHover) { icon = iconRect; rotateIconOnHover = rotateOnHover; }
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
            _label = GetComponentInChildren<TMP_Text>(true);

            if (button != null)
            {
                button.transition = Selectable.Transition.None;
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

        private void EnsureGlow()
        {
            var existing = transform.Find("HoverGlow");
            if (existing != null && existing.gameObject.activeSelf)
                existing.gameObject.SetActive(false);
            _glow = null;
        }

        private void EnsureSpinner()
        {
            if (_spinner != null) return;

            var existing = transform.Find("LoadingSpinner");
            if (existing != null) _spinner = existing.GetComponent<Image>();
            if (_spinner != null) return;

            var sprite = Resources.Load<Sprite>("UI/Icon_Spinner");
            if (sprite == null) return;

            var spinnerObj = new GameObject("LoadingSpinner", typeof(RectTransform), typeof(Image));
            spinnerObj.transform.SetParent(transform, false);
            spinnerObj.transform.SetAsLastSibling();
            
            _spinner = spinnerObj.GetComponent<Image>();
            _spinner.sprite = sprite;
            _spinner.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);
            _spinner.raycastTarget = false;

            var sRect = (RectTransform)spinnerObj.transform;
            sRect.anchorMin = sRect.anchorMax = sRect.pivot = new Vector2(0.5f, 0.5f);
            sRect.anchoredPosition = Vector2.zero;
            sRect.sizeDelta = new Vector2(28f, 28f);
        }

        private void CacheGraphics()
        {
            var all = GetComponentsInChildren<Graphic>(true);
            var list = new System.Collections.Generic.List<Graphic>(all.Length);
            foreach (var graphic in all)
            {
                if (graphic == null || (_glow != null && graphic.transform == _glow.transform) || (_spinner != null && graphic.transform == _spinner.transform))
                    continue;
                list.Add(graphic);
            }
            _group = list.ToArray();
            _groupColors = new Color[_group.Length];
            for (var i = 0; i < _group.Length; i++) _groupColors[i] = _group[i].color;
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
            UITween.Kill(_scaleToken); UITween.Kill(_colorToken);
            UITween.Kill(_glowToken); UITween.Kill(_rotToken); UITween.Kill(_fxToken);
            
            _hovered = _pressed = false;
            _brightness = 1f; _flashAmount = 0f;
            
            if (_rt != null) _rt.localScale = _baseScale;
            ApplyVisuals();
            
            if (_glow != null) _glow.color = ColorWithAlpha(0f);
            if (_spinner != null) { _spinner.gameObject.SetActive(false); }
            
            State = RestState();
        }

        public void OnPointerEnter(PointerEventData eventData) => HoverIn();
        public void OnPointerExit(PointerEventData eventData) => HoverOut();
        public void OnPointerDown(PointerEventData eventData)
        {
            EnsureInitialized();
            if (!CanInteract()) return;
            _pressed = true; State = ButtonAnimState.PressDown;
            Refresh(UIAnim.PressDown, Ease.OutQuad, includeScale: true);
        }
        public void OnPointerUp(PointerEventData eventData)
        {
            EnsureInitialized();
            if (!_pressed) return;
            _pressed = false; State = ButtonAnimState.PressRelease;
            Refresh(UIAnim.PressRelease, Ease.OutCubic, includeScale: true);
        }
        public void OnSelect(BaseEventData eventData) { EnsureInitialized(); _focused = true; if (!_hovered && !_pressed) State = ButtonAnimState.Focused; Refresh(UIAnim.StateChange, UIAnim.HoverEase); }
        public void OnDeselect(BaseEventData eventData) { EnsureInitialized(); _focused = false; State = RestState(); Refresh(UIAnim.StateChange, UIAnim.HoverEase); }

        public void HoverIn()
        {
            EnsureInitialized();
            if (!CanInteract()) { if (_locked) IconAnimator.Shake(icon != null ? icon : _rt, 4f, UIAnim.ErrorShake, _fxToken); return; }
            _hovered = true; State = ButtonAnimState.HoverIn; UISfx.PlayHover();
            Refresh(UIAnim.Hover, UIAnim.HoverEase, includeScale: true, iconIn: true);
        }

        public void HoverOut() { EnsureInitialized(); _hovered = false; State = RestState(); Refresh(UIAnim.Hover, UIAnim.HoverEase, includeScale: true, iconIn: false); }

        public void SetSelected(bool selected) { EnsureInitialized(); if (_selected == selected) return; _selected = selected; State = selected ? ButtonAnimState.Selected : ButtonAnimState.Deselected; Refresh(UIAnim.StateChange, UIAnim.HoverEase); if (!selected) State = RestState(); }
        public void SetLocked(bool locked) { EnsureInitialized(); if (_locked == locked) return; _locked = locked; State = locked ? ButtonAnimState.Locked : ButtonAnimState.Idle; Refresh(UIAnim.StateChange, UIAnim.HoverEase, includeScale: true); }

        public void SetLoading(bool loading)
        {
            EnsureInitialized();
            if (_loading == loading) return;
            _loading = loading; State = loading ? ButtonAnimState.Loading : ButtonAnimState.Idle;
            Refresh(UIAnim.StateChange, UIAnim.HoverEase, includeScale: true);
            UITween.Kill(_rotToken);

            if (_label != null) _label.gameObject.SetActive(!loading);
            if (icon != null) icon.gameObject.SetActive(!loading);

            if (loading)
            {
                EnsureSpinner();
                if (_spinner != null)
                {
                    _spinner.gameObject.SetActive(true);
                    var sRect = (RectTransform)_spinner.transform;
                    var isLightBackground = _label != null && (_label.color.r < 0.25f && _label.color.g < 0.25f && _label.color.b < 0.25f);
                    _spinner.color = isLightBackground ? new Color(0.12f, 0.09f, 0.04f, 1f) : new Color(1f, 0.78f, 0.2f, 1f);
                    
                    UITween.Loop(0f, -360f, 0.85f, Ease.Linear, v => { if (sRect != null) sRect.localEulerAngles = new Vector3(0f, 0f, v); }, false, _rotToken);
                }
            }
            else if (_spinner != null) _spinner.gameObject.SetActive(false);
        }

        public void SetSuccess() { EnsureInitialized(); State = ButtonAnimState.Success; UISfx.PlayClick(); FlashGroup(new Color(0.35f, 1.6f, 0.7f, 1f), UIAnim.Success, () => State = RestState()); }
        public void SetError() { EnsureInitialized(); State = ButtonAnimState.Error; IconAnimator.Shake(_rt, 5f, UIAnim.ErrorShake, _fxToken); FlashGroup(new Color(1.8f, 0.35f, 0.4f, 1f), UIAnim.ErrorShake, () => State = RestState()); }

        private void FlashGroup(Color color, float totalDuration, System.Action onDone)
        {
            EnsureInitialized(); _flashColor = color; UITween.Kill(_colorToken);
            UITween.To(0f, 1f, 0.07f, Ease.OutQuad, v => { _flashAmount = v; ApplyVisuals(); }, () => {
                UITween.To(1f, 0f, totalDuration, Ease.OutCubic, v => { _flashAmount = v; ApplyVisuals(); }, () => { _flashAmount = 0f; ApplyVisuals(); onDone?.Invoke(); }, 0f, _colorToken);
            }, 0f, _colorToken);
        }

        private void PlayClickAnimation()
        {
            EnsureInitialized(); if (!CanInteract()) return;
            State = ButtonAnimState.Click; UISfx.PlayClick();
            FlashGroup(new Color(1.22f, 1.22f, 1.22f, 1f), 0.10f, () => State = RestState());

            if (_rt != null && !_pressed && Mathf.Abs(_rt.localScale.x - _baseScale.x) < 0.005f)
            {
                var punchScale = _baseScale * UIAnim.PressScale; var restScale = _hovered ? _baseScale * HoverScaleValue : _baseScale;
                UITween.Kill(_scaleToken);
                UITween.To(0f, 1f, UIAnim.ClickDown, Ease.OutQuad, p => { if (_rt != null) _rt.localScale = Vector3.LerpUnclamped(_baseScale, punchScale, p); }, () => {
                    UITween.To(0f, 1f, UIAnim.ClickUp, Ease.OutCubic, p => { if (_rt != null) _rt.localScale = Vector3.LerpUnclamped(punchScale, restScale, p); }, null, 0f, _scaleToken);
                }, 0f, _scaleToken);
            }
        }

        private void Update() { if (button == null || button.interactable == _lastInteractable) return; _lastInteractable = button.interactable; Refresh(UIAnim.StateChange, UIAnim.HoverEase, includeScale: true); }

        private void Refresh(float duration, Ease ease, bool includeScale = false, bool? iconIn = null)
        {
            EnsureInitialized();
            if (_flashAmount > 0f) _flashAmount = 0f;
            if (Mathf.Approximately(_brightness, 1f)) CacheGraphics();

            float brightness = _loading ? UIAnim.BrightnessLoading : _locked ? UIAnim.BrightnessLocked : (button != null && !button.interactable) ? UIAnim.BrightnessDisabled : _pressed ? UIAnim.BrightnessPressed : _hovered ? HoverBrightness : 1f;
            TweenBrightness(brightness, duration, ease);

            if (includeScale && _rt != null && !_loading)
            {
                Vector3 target = (_locked || (button != null && !button.interactable)) ? _baseScale : _pressed ? _baseScale * UIAnim.PressScale : _hovered ? _baseScale * HoverScaleValue : _baseScale;
                UITween.ScaleTo(_rt, target, duration, ease, null, _scaleToken);
            }

            if (icon != null)
            {
                var inside = iconIn ?? _hovered; var allowed = CanInteract();
                UITween.MoveAnchoredTo(icon, _iconBasePos + (allowed && inside ? new Vector2(iconNudge, 0f) : Vector2.zero), duration, ease, null, _fxToken);
                if (rotateIconOnHover) UITween.RotateZTo(icon, allowed && inside ? _iconBaseRot + 15f : _iconBaseRot, duration, ease, null, _rotToken);
            }
        }

        private void TweenBrightness(float to, float duration, Ease ease) { var from = _brightness; UITween.Kill(_colorToken); UITween.To(from, to, duration, ease, v => { _brightness = v; ApplyVisuals(); }, null, 0f, _colorToken); }
        private Color ColorWithAlpha(float alpha) => new Color(glowColor.r, glowColor.g, glowColor.b, alpha);
        private ButtonAnimState RestState() => _loading ? ButtonAnimState.Loading : _locked ? ButtonAnimState.Locked : (button != null && !button.interactable) ? ButtonAnimState.Disabled : _hovered ? ButtonAnimState.HoverIn : _selected ? ButtonAnimState.Selected : _focused ? ButtonAnimState.Focused : ButtonAnimState.Idle;
        private bool CanInteract() => button != null && button.interactable && !_locked && !_loading;
    }
}
