using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Railbound.UI.Anim
{
    /// <summary>
    /// Interactive Operator Dossier Card for the Nickname / Callsign Registration step:
    /// - Tactical operative portrait clipped in chamfered frame.
    /// - Real-time callsign synchronization with glowing codename badge.
    /// - Hover: lifts 6px, scales 1.015, gold border glow illuminates, artwork zooms 1.04x.
    /// - Click: pulses badge and triggers interactive tactile feedback.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteractiveDossierAnimator : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        [SerializeField] private RectTransform cardRect;
        [SerializeField] private Image artwork;
        [SerializeField] private Image chamferBorder;
        [SerializeField] private TMP_Text callsignText;
        [SerializeField] private Image statusDot;

        private readonly object _scaleToken = new object();
        private readonly object _posToken = new object();
        private readonly object _artToken = new object();
        private readonly object _borderToken = new object();
        private readonly object _badgeToken = new object();
        private readonly object _pulseToken = new object();

        private static readonly Color SlateBorder = new Color(0.24f, 0.32f, 0.44f, 0.55f);
        private static readonly Color GoldBorder = new Color(1f, 0.75f, 0.2f, 0.95f);
        private static readonly Color GoldText = new Color(1f, 0.85f, 0.35f, 1f);
        private static readonly Color UnassignedColor = new Color(0.5f, 0.55f, 0.65f, 0.7f);

        private Vector2 _basePos;
        private Vector3 _baseScale = Vector3.one;
        private bool _baseCaptured;
        private bool _hovered;
        private bool _pressed;
        private Action _onClickAction;

        public void Initialize(Image art, Image border, TMP_Text codenameLabel, Image liveDot, Action onClick)
        {
            artwork = art;
            chamferBorder = border;
            callsignText = codenameLabel;
            statusDot = liveDot;
            _onClickAction = onClick;
            cardRect = (RectTransform)transform;

            CaptureBase();
            StartAmbientPulse();
        }

        private void CaptureBase()
        {
            if (_baseCaptured || cardRect == null) return;
            _baseCaptured = true;
            _basePos = Vector2.zero; // Anchored rect base position is always stable zero offset
            _baseScale = cardRect.localScale == Vector3.zero ? Vector3.one : cardRect.localScale;
        }

        public void SetCallsign(string callsign)
        {
            if (callsignText == null) return;

            var hasName = !string.IsNullOrWhiteSpace(callsign);
            callsignText.text = hasName ? callsign.ToUpperInvariant() : "[UNASSIGNED]";
            callsignText.color = hasName ? GoldText : UnassignedColor;

            // Punch pop on callsign update
            var textRect = callsignText.rectTransform;
            if (textRect != null)
            {
                UITween.Kill(_badgeToken);
                textRect.localScale = Vector3.one * 1.12f;
                UITween.ScaleTo(textRect, Vector3.one, 0.25f, Ease.OutCubic, null, _badgeToken);
            }
        }

        private void StartAmbientPulse()
        {
            if (statusDot == null) return;
            var c = statusDot.color;
            UITween.Loop(0.4f, 1f, 1.8f, Ease.InOutQuad,
                a => { if (statusDot != null) statusDot.color = new Color(c.r, c.g, c.b, a); },
                yoyo: true, target: _pulseToken);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            UISfx.PlayHover();
            Refresh(UIAnim.Hover, UIAnim.HoverEase);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            _pressed = false;
            Refresh(UIAnim.Hover, UIAnim.HoverEase);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _pressed = true;
            Refresh(UIAnim.PressDown, Ease.OutQuad);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pressed = false;
            Refresh(UIAnim.PressRelease, Ease.OutCubic);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            UISfx.PlayClick();
            _onClickAction?.Invoke();
        }

        private void Refresh(float duration, Ease ease)
        {
            CaptureBase();

            // Card scale and lift
            if (cardRect != null)
            {
                var targetScale = _pressed
                    ? _baseScale * 0.98f
                    : (_hovered ? _baseScale * 1.015f : _baseScale);
                UITween.ScaleTo(cardRect, targetScale, duration, ease, null, _scaleToken);

                var targetPos = _basePos + (_hovered && !_pressed ? new Vector2(0f, 6f) : Vector2.zero);
                UITween.MoveAnchoredTo(cardRect, targetPos, duration, ease, null, _posToken);
            }

            // Artwork zoom and brightness
            if (artwork != null)
            {
                var targetColor = _hovered ? Color.white : new Color(0.88f, 0.88f, 0.92f, 1f);
                var fromColor = artwork.color;
                UITween.Kill(_artToken);
                UITween.To(0f, 1f, duration, ease,
                    p => { if (artwork != null) artwork.color = Color.LerpUnclamped(fromColor, targetColor, p); },
                    null, 0f, _artToken);

                var artRect = (RectTransform)artwork.transform;
                var targetZoom = _hovered ? Vector3.one * 1.04f : Vector3.one;
                UITween.ScaleTo(artRect, targetZoom, duration, ease, null, _artToken);
            }

            // Chamfered border glow
            if (chamferBorder != null)
            {
                var targetBorder = _hovered ? GoldBorder : SlateBorder;
                var fromBorder = chamferBorder.color;
                UITween.Kill(_borderToken);
                UITween.To(0f, 1f, duration, ease,
                    p => { if (chamferBorder != null) chamferBorder.color = Color.LerpUnclamped(fromBorder, targetBorder, p); },
                    null, 0f, _borderToken);
            }
        }

        private void OnDisable()
        {
            UITween.Kill(_scaleToken);
            UITween.Kill(_posToken);
            UITween.Kill(_artToken);
            UITween.Kill(_borderToken);
            UITween.Kill(_badgeToken);
            UITween.Kill(_pulseToken);
            _hovered = false;
            _pressed = false;
        }
    }
}
