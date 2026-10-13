using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Railbound.UI.Anim
{
    /// <summary>
    /// Experience level card (INEXPERT / ACCOMPLISHED / MASTER):
    /// - Idle: dimmed (when unselected), stable, selected card breathes slowly.
    /// - Hover: lifts 5-10px, scales 1.00 -> 1.02, soft gold glow halo fades in, artwork
    ///   zooms 1.04 inside its mask, description (baked into the art) becomes clearer.
    /// - Selected: gold frame strips + glow halo, checkmark pops in (Back Out), scale 1.03,
    ///   ambient breathing; the controller dims the other cards (deselected).
    /// - Deselected: glow/checkmark fade, scale returns to normal.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelCardAnimator : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private Image art;                    // letterboxed artwork (inside ArtClip)
        [SerializeField] private GameObject frame;             // art-bounds container (holds strips)
        [SerializeField] private Image[] frameStrips;          // legacy references
        [SerializeField] private Button button;
        [SerializeField] private RectTransform cardRect;

        private readonly object _scaleToken = new object();
        private readonly object _posToken = new object();
        private readonly object _artToken = new object();
        private readonly object _borderToken = new object();
        private readonly object _checkToken = new object();
        private readonly object _ambientToken = new object();

        private Image _chamferBorder;      // 9-sliced tactical chamfer frame hugging card artwork
        private RectTransform _checkRoot;
        private Image _checkImage;
        private Color _dimColor = new Color(0.45f, 0.45f, 0.5f, 1f);
        private static readonly Color Gold = new Color(1f, 0.75f, 0.2f, 1f);
        private static readonly Color SlateBorder = new Color(0.24f, 0.32f, 0.44f, 0.5f);
        private static readonly Color HoverBorder = new Color(0.38f, 0.48f, 0.62f, 0.55f);
        private static readonly Color HoverGold = new Color(1f, 0.88f, 0.35f, 1f);

        private Vector2 _basePos;
        private Vector3 _baseScale = Vector3.one;
        private bool _baseCaptured;
        private bool _selected;
        private bool _hovered;
        private bool _pressed;
        private bool _initialized;

        /// <summary>Called by the onboarding controller right after the card is built.</summary>
        public void Initialize(Image artwork, GameObject frameObject, Color dimColor)
        {
            art = artwork;
            frame = frameObject;
            _dimColor = dimColor;
            frameStrips = frame != null ? frame.GetComponentsInChildren<Image>(true) : new Image[0];
            if (frame != null) frame.SetActive(true);
            _initialized = false;
            EnsureVisuals();
        }

        private void EnsureVisuals()
        {
            if (_initialized) return;
            _initialized = true;

            if (cardRect == null) cardRect = (RectTransform)transform;
            if (button == null) button = GetComponent<Button>();
            if (button != null) button.transition = Selectable.Transition.None;
            if (art == null)
            {
                var artTransform = transform.Find("ArtClip/Art");
                if (artTransform != null) art = artTransform.GetComponent<Image>();
            }
            if (frame == null)
            {
                var frameTransform = transform.Find("SelectionFrame");
                if (frameTransform != null) frame = frameTransform.gameObject;
            }

            EnsureBorder();
            EnsureCheckmark();
        }

        /// <summary>Ensures a single continuous 9-sliced chamfered frame hugging the rounded card art.</summary>
        private void EnsureBorder()
        {
            if (frame == null) return;

            // Hide legacy 4-strip images so only the sleek 9-sliced chamfer frame is visible
            for (var i = 0; i < frame.transform.childCount; i++)
            {
                var child = frame.transform.GetChild(i);
                if (child.name == "Top" || child.name == "Bottom" || child.name == "Left" || child.name == "Right")
                    child.gameObject.SetActive(false);
            }

            var existing = frame.transform.Find("ChamferBorder");
            if (existing != null)
            {
                _chamferBorder = existing.GetComponent<Image>();
                return;
            }

            var borderObj = new GameObject("ChamferBorder", typeof(RectTransform), typeof(Image));
            borderObj.transform.SetParent(frame.transform, false);
            borderObj.transform.SetAsFirstSibling();
            var bRt = (RectTransform)borderObj.transform;
            bRt.anchorMin = Vector2.zero;
            bRt.anchorMax = Vector2.one;
            bRt.offsetMin = Vector2.zero;
            bRt.offsetMax = Vector2.zero;

            _chamferBorder = borderObj.GetComponent<Image>();
            var ringSprite = Resources.Load<Sprite>("UI/Ring_Chamfer");
            if (ringSprite != null)
            {
                _chamferBorder.sprite = ringSprite;
                _chamferBorder.type = Image.Type.Sliced;
            }
            _chamferBorder.color = _selected ? Gold : SlateBorder;
            _chamferBorder.raycastTarget = false;
        }

        /// <summary>Military hexagonal badge checkmark mirroring tactical rank insignia.</summary>
        private void EnsureCheckmark()
        {
            if (_checkRoot != null || frame == null) return;
            var existing = frame.transform.Find("Checkmark");
            if (existing != null)
            {
                _checkRoot = (RectTransform)existing;
                var img = existing.Find("Glyph");
                if (img != null) _checkImage = img.GetComponent<Image>();
                return;
            }

            var hexSprite = Resources.Load<Sprite>("UI/Checkmark_Hex") ?? Resources.Load<Sprite>("UI/Checkmark");
            var backingSprite = Resources.Load<Sprite>("UI/Onboarding_Chamfer");

            var root = new GameObject("Checkmark", typeof(RectTransform));
            root.transform.SetParent(frame.transform, false);
            _checkRoot = (RectTransform)root.transform;
            _checkRoot.anchorMin = new Vector2(1f, 1f);
            _checkRoot.anchorMax = new Vector2(1f, 1f);
            _checkRoot.pivot = new Vector2(1f, 1f);
            _checkRoot.anchoredPosition = new Vector2(-12f, -12f);
            _checkRoot.sizeDelta = new Vector2(48f, 48f);

            // Dark chamfered backing
            var backingObj = new GameObject("Backing", typeof(RectTransform), typeof(Image));
            backingObj.transform.SetParent(root.transform, false);
            var bRt = (RectTransform)backingObj.transform;
            bRt.anchorMin = Vector2.zero;
            bRt.anchorMax = Vector2.one;
            bRt.offsetMin = Vector2.zero;
            bRt.offsetMax = Vector2.zero;
            var backing = backingObj.GetComponent<Image>();
            backing.sprite = backingSprite;
            backing.type = backingSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            backing.color = new Color(0.04f, 0.05f, 0.09f, 0.95f);
            backing.raycastTarget = false;

            // Tactical hexagonal badge glyph
            var glyphObject = new GameObject("Glyph", typeof(RectTransform), typeof(Image));
            glyphObject.transform.SetParent(root.transform, false);
            var glyphRect = (RectTransform)glyphObject.transform;
            glyphRect.anchorMin = Vector2.zero;
            glyphRect.anchorMax = Vector2.one;
            glyphRect.offsetMin = new Vector2(5f, 5f);
            glyphRect.offsetMax = new Vector2(-5f, -5f);
            _checkImage = glyphObject.GetComponent<Image>();
            _checkImage.sprite = hexSprite;
            _checkImage.color = Gold;
            _checkImage.raycastTarget = false;

            root.SetActive(false);
        }

        // ------------------------------------------------------------------ interaction

        public void OnPointerEnter(PointerEventData eventData)
        {
            EnsureVisuals();
            if (button != null && !button.interactable) return;
            _hovered = true;
            UISfx.PlayHover();
            Refresh(UIAnim.Hover, UIAnim.HoverEase);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            EnsureVisuals();
            _hovered = false;
            _pressed = false;
            Refresh(UIAnim.Hover, UIAnim.HoverEase);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            EnsureVisuals();
            if (button != null && !button.interactable) return;
            _pressed = true;
            Refresh(UIAnim.PressDown, Ease.OutQuad);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            EnsureVisuals();
            _pressed = false;
            Refresh(UIAnim.PressRelease, Ease.OutCubic);
        }

        /// <summary>Selected / deselected - called by the onboarding controller.</summary>
        public void SetSelected(bool selected)
        {
            EnsureVisuals();
            var wasSelected = _selected;
            _selected = selected;

            if (_checkRoot != null)
            {
                _checkRoot.gameObject.SetActive(selected);
                if (selected && !wasSelected)
                {
                    _checkRoot.localScale = Vector3.zero;
                    UITween.Kill(_checkToken);
                    UITween.ScaleTo(_checkRoot, Vector3.one, UIAnim.Popup, UIAnim.PopEase, null, _checkToken);
                    UISfx.PlayClick();
                }
            }

            Refresh(UIAnim.StateChange, UIAnim.HoverEase, expandSelected: selected && !wasSelected);
        }

        private void Refresh(float duration, Ease ease, bool expandSelected = false)
        {
            EnsureVisuals();
            CaptureBase();

            // scale + lift ------------------------------------------------
            if (cardRect != null)
            {
                var scale = _pressed
                    ? _baseScale * 0.98f
                    : (_selected
                        ? (_hovered ? _baseScale * (UIAnim.CardSelectedScale + 0.012f) : _baseScale * UIAnim.CardSelectedScale)
                        : (_hovered ? _baseScale * UIAnim.CardHoverScale : _baseScale));
                UITween.ScaleTo(cardRect, scale, duration, ease, null, _scaleToken);

                var pos = _basePos + (_hovered && !_pressed ? new Vector2(0f, UIAnim.CardHoverLift) : Vector2.zero);
                UITween.MoveAnchoredTo(cardRect, pos, duration, ease, null, _posToken);
            }

            // artwork: brightness + slight zoom on hover ------------------
            if (art != null)
            {
                var brightness = _selected ? 1f : (_hovered ? 0.75f : 0f);
                var to = Color.LerpUnclamped(_dimColor, Color.white, brightness);
                var from = art.color;
                UITween.Kill(_artToken);
                UITween.To(0f, 1f, duration, ease,
                    p => { if (art != null) art.color = Color.LerpUnclamped(from, to, p); },
                    null, 0f, _artToken);

                var artRect = (RectTransform)art.transform;
                var zoom = _hovered ? Vector3.one * UIAnim.CardArtZoom : Vector3.one;
                UITween.ScaleTo(artRect, zoom, duration, ease, null, _artToken);
            }

            // chamfered tactical border color -----------------------------
            if (_chamferBorder != null)
            {
                var targetBorderColor = _selected
                    ? (_hovered ? HoverGold : Gold)
                    : (_hovered ? HoverBorder : SlateBorder);
                var fromColor = _chamferBorder.color;
                UITween.Kill(_borderToken);
                UITween.To(0f, 1f, duration, ease,
                    p => { if (_chamferBorder != null) _chamferBorder.color = Color.LerpUnclamped(fromColor, targetBorderColor, p); },
                    null, 0f, _borderToken);
            }
        }

        private void CaptureBase()
        {
            if (_baseCaptured || cardRect == null) return;
            _baseCaptured = true;
            _basePos = cardRect.anchoredPosition;
            _baseScale = cardRect.localScale;
        }

        private void OnDisable()
        {
            UITween.Kill(_scaleToken);
            UITween.Kill(_posToken);
            UITween.Kill(_artToken);
            UITween.Kill(_borderToken);
            UITween.Kill(_checkToken);
            UITween.Kill(_ambientToken);
            _hovered = false;
            _pressed = false;
        }
    }
}
