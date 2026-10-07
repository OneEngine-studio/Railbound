using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Railbound.UI.Anim
{
    /// <summary>
    /// Experience level card (INEXPERT / ACCOMPLISHED / MASTER):
    /// - Idle: dimmed (when unselected), stable, selected card breathes slowly.
    /// - Hover: lifts 5-10px, scales 1.00 -> 1.02, border (ring) brightens, artwork
    ///   zooms 1.04 inside its mask, description (baked into the art) becomes clearer.
    /// - Selected: strong gold ring + glow, checkmark pops in (Back Out), scale 1.03,
    ///   ambient breathing; the controller dims the other cards (deselected).
    /// - Deselected: glow/checkmark fade, scale returns to normal.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelCardAnimator : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image art;                    // letterboxed artwork (inside ArtClip)
        [SerializeField] private GameObject frame;             // art-bounds container (holds strips)
        [SerializeField] private Image[] frameStrips;          // the 4 gold selection strips
        [SerializeField] private Button button;
        [SerializeField] private RectTransform cardRect;

        private readonly object _scaleToken = new object();
        private readonly object _posToken = new object();
        private readonly object _artToken = new object();
        private readonly object _ringToken = new object();
        private readonly object _checkToken = new object();
        private readonly object _ambientToken = new object();

        private Image _ring;
        private RectTransform _checkRoot;
        private Image _checkImage;
        private Color _dimColor = new Color(0.45f, 0.45f, 0.5f, 1f);
        private static readonly Color Gold = new Color(1f, 0.75f, 0.2f, 1f);

        private Vector2 _basePos;
        private Vector3 _baseScale = Vector3.one;
        private bool _baseCaptured;
        private bool _selected;
        private bool _hovered;
        private bool _initialized;

        /// <summary>Called by the onboarding controller right after the card is built.</summary>
        public void Initialize(Image artwork, GameObject frameObject, Color dimColor)
        {
            art = artwork;
            frame = frameObject;
            _dimColor = dimColor;
            // Capture the strips before the animator adds ring/checkmark children to the frame.
            frameStrips = frame != null ? frame.GetComponentsInChildren<Image>(true) : new Image[0];
            if (frame != null) frame.SetActive(true); // container stays alive; strips toggle instead
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

            EnsureRing();
            EnsureCheckmark();
        }

        private void EnsureRing()
        {
            if (_ring != null || frame == null) return;
            var existing = frame.transform.Find("HoverRing");
            if (existing != null) { _ring = existing.GetComponent<Image>(); return; }

            var sprite = Resources.Load<Sprite>("UI/Ring_Square");
            if (sprite == null) return;

            var ringObject = new GameObject("HoverRing", typeof(RectTransform), typeof(Image));
            ringObject.transform.SetParent(frame.transform, false);
            ringObject.transform.SetSiblingIndex(0);
            _ring = ringObject.GetComponent<Image>();
            _ring.sprite = sprite;
            _ring.type = Image.Type.Sliced;
            _ring.color = new Color(Gold.r, Gold.g, Gold.b, 0f);
            _ring.raycastTarget = false;
            var rect = (RectTransform)ringObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(-3f, -3f);
            rect.offsetMax = new Vector2(3f, 3f);
        }

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

            var sprite = Resources.Load<Sprite>("UI/Checkmark");
            var backingSprite = Resources.Load<Sprite>("UI/Onboarding_Chamfer");
            if (sprite == null) return;

            var root = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(frame.transform, false);
            _checkRoot = (RectTransform)root.transform;
            _checkRoot.anchorMin = new Vector2(1f, 1f);
            _checkRoot.anchorMax = new Vector2(1f, 1f);
            _checkRoot.pivot = new Vector2(1f, 1f);
            _checkRoot.anchoredPosition = new Vector2(-12f, -12f);
            _checkRoot.sizeDelta = new Vector2(46f, 46f);

            var backing = root.GetComponent<Image>();
            backing.sprite = backingSprite;
            backing.type = backingSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            backing.color = new Color(0.04f, 0.05f, 0.09f, 0.95f);
            backing.raycastTarget = false;

            var glyphObject = new GameObject("Glyph", typeof(RectTransform), typeof(Image));
            glyphObject.transform.SetParent(root.transform, false);
            var glyphRect = (RectTransform)glyphObject.transform;
            glyphRect.anchorMin = Vector2.zero;
            glyphRect.anchorMax = Vector2.one;
            glyphRect.offsetMin = new Vector2(7f, 7f);
            glyphRect.offsetMax = new Vector2(-7f, -7f);
            _checkImage = glyphObject.GetComponent<Image>();
            _checkImage.sprite = sprite;
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
            Refresh(UIAnim.Hover, UIAnim.HoverEase);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            EnsureVisuals();
            _hovered = false;
            Refresh(UIAnim.Hover, UIAnim.HoverEase);
        }

        /// <summary>Selected / deselected - called by the onboarding controller.</summary>
        public void SetSelected(bool selected)
        {
            EnsureVisuals();
            var wasSelected = _selected;
            _selected = selected;

            if (frameStrips != null)
                for (var i = 0; i < frameStrips.Length; i++)
                    if (frameStrips[i] != null)
                        frameStrips[i].gameObject.SetActive(selected);

            if (_checkRoot != null)
            {
                _checkRoot.gameObject.SetActive(selected);
                if (selected && !wasSelected)
                {
                    // Checkmark: scale from 0 to 1 with a small overshoot (Back Out).
                    _checkRoot.localScale = Vector3.zero;
                    UITween.Kill(_checkToken);
                    UITween.ScaleTo(_checkRoot, Vector3.one, UIAnim.Popup, UIAnim.PopEase, null, _checkToken);
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
                var scale = _selected
                    ? (_hovered ? UIAnim.CardSelectedScale + 0.015f : UIAnim.CardSelectedScale)
                    : (_hovered ? UIAnim.CardHoverScale : 1f);
                UITween.ScaleTo(cardRect, _baseScale * scale, duration, ease, null, _scaleToken);

                var pos = _basePos + (_hovered ? new Vector2(0f, UIAnim.CardHoverLift) : Vector2.zero);
                UITween.MoveAnchoredTo(cardRect, pos, duration, ease, null, _posToken);
            }

            // artwork: brightness (dim <-> full) + slight zoom on hover -------
            if (art != null)
            {
                var brightness = _selected ? 1f : (_hovered ? 0.6f : 0f);
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

            // border / glow --------------------------------------------------
            if (_ring != null)
            {
                var alpha = _selected
                    ? (_hovered ? UIAnim.RingHoverSelected : UIAnim.RingSelected)
                    : (_hovered ? UIAnim.RingHover : 0f);
                var color = new Color(Gold.r, Gold.g, Gold.b, alpha);
                var fromRing = _ring.color;
                UITween.Kill(_ringToken);
                UITween.To(0f, 1f, duration, ease,
                    p => { if (_ring != null) _ring.color = Color.LerpUnclamped(fromRing, color, p); },
                    null, 0f, _ringToken);
            }

            // ambient breathing on the selected card (slow + subtle) ------------
            UITween.Kill(_ambientToken);
            if (_selected && Application.isPlaying && _ring != null)
            {
                var ring = _ring;
                UITween.Loop(UIAnim.RingAmbientMin, UIAnim.RingSelected, UIAnim.AmbientCycle, Ease.InOutCubic,
                    a => { if (ring != null && ring.color.a > 0.01f) ring.color = new Color(Gold.r, Gold.g, Gold.b, a); },
                    yoyo: true, target: _ambientToken);
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
            UITween.Kill(_ringToken);
            UITween.Kill(_checkToken);
            UITween.Kill(_ambientToken);
            _hovered = false;
        }
    }
}
