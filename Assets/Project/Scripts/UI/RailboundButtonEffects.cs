using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if DOTWEEN
using DG.Tweening;
#endif

namespace Railbound.UI
{
    /// <summary>
    /// Tactical micro-interaction component for Railbound buttons.
    /// Provides smooth hover scale (1.04x), punch click (0.97x), and glowing border (#FF7A00).
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class RailboundButtonEffects : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Scale Micro-interactions")]
        [SerializeField] private float hoverScale = 1.04f;
        [SerializeField] private float clickScale = 0.97f;
        [SerializeField] private float hoverDuration = 0.12f;
        [SerializeField] private float clickDuration = 0.08f;

        [Header("Tactical Glow / Border")]
        [SerializeField] private Graphic glowBorderGraphic;
        [SerializeField] private Outline glowOutline;
        [SerializeField] private Color normalGlowColor = new Color(1f, 0.48f, 0f, 0f);
        [SerializeField] private Color hoverGlowColor = new Color(1f, 0.48f, 0f, 0.95f);
        [SerializeField] private Color clickGlowColor = new Color(1f, 0.72f, 0.1f, 1f);

        [Header("Audio")]
        [SerializeField] private AudioClip hoverSfx;
        [SerializeField] private AudioClip clickSfx;
        [SerializeField, Range(0f, 1f)] private float audioVolume = 0.4f;

        private Button _button;
        private Vector3 _originalScale = Vector3.one;
        private AudioSource _audioSource;
        private Coroutine _scaleRoutine;
        private Coroutine _colorRoutine;
        private Coroutine _outlineRoutine;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _originalScale = transform.localScale;
            if (_originalScale == Vector3.zero || _originalScale.x < 0.01f)
                _originalScale = Vector3.one;

            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.playOnAwake = false;
                _audioSource.spatialBlend = 0f;
            }

            SetupGlowReferences();

#if UNITY_EDITOR
            if (clickSfx == null)
                clickSfx = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Free UI Click Sound Effects Pack/AUDIO/Sci-Fi/SFX_UI_Click_Designed_Scifi_Thin_Select_1.wav");
            if (hoverSfx == null)
                hoverSfx = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Free UI Click Sound Effects Pack/AUDIO/Sci-Fi/SFX_UI_Click_Designed_Scifi_Digital_Generic_1.wav");
#endif
        }

        private void SetupGlowReferences()
        {
            if (glowBorderGraphic == null)
            {
                glowBorderGraphic = transform.Find("Glow")?.GetComponent<Graphic>()
                                 ?? transform.Find("Border")?.GetComponent<Graphic>()
                                 ?? transform.Find("GuestAccent")?.GetComponent<Graphic>()
                                 ?? transform.Find("Accent")?.GetComponent<Graphic>();
            }

            if (glowOutline == null)
            {
                glowOutline = GetComponent<Outline>();
                if (glowOutline == null && glowBorderGraphic == null)
                {
                    glowOutline = gameObject.AddComponent<Outline>();
                    glowOutline.effectDistance = new Vector2(2f, -2f);
                }
            }

            if (glowBorderGraphic != null)
                glowBorderGraphic.color = normalGlowColor;

            if (glowOutline != null)
                glowOutline.effectColor = normalGlowColor;
        }

        public void SetAudioClips(AudioClip click, AudioClip hover)
        {
            clickSfx = click;
            hoverSfx = hover;
        }

        private void OnDisable()
        {
            transform.localScale = _originalScale;
            if (glowBorderGraphic != null) glowBorderGraphic.color = normalGlowColor;
            if (glowOutline != null) glowOutline.effectColor = normalGlowColor;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_button != null && !_button.interactable) return;
            PlaySound(hoverSfx);
            Vector3 baseScale = _originalScale == Vector3.zero ? Vector3.one : _originalScale;
            AnimateScale(baseScale * hoverScale, hoverDuration);
            AnimateGlow(hoverGlowColor, hoverDuration);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_button != null && !_button.interactable) return;
            Vector3 baseScale = _originalScale == Vector3.zero ? Vector3.one : _originalScale;
            AnimateScale(baseScale, hoverDuration);
            AnimateGlow(normalGlowColor, hoverDuration);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_button != null && !_button.interactable) return;
            PlaySound(clickSfx);
            Vector3 baseScale = _originalScale == Vector3.zero ? Vector3.one : _originalScale;
            AnimateScale(baseScale * clickScale, clickDuration);
            AnimateGlow(clickGlowColor, clickDuration);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_button != null && !_button.interactable) return;
            Vector3 baseScale = _originalScale == Vector3.zero ? Vector3.one : _originalScale;
            AnimateScale(baseScale * hoverScale, clickDuration);
            AnimateGlow(hoverGlowColor, clickDuration);
        }

        private void PlaySound(AudioClip clip)
        {
            if (clip != null && _audioSource != null)
                _audioSource.PlayOneShot(clip, audioVolume);
        }

        private void AnimateScale(Vector3 targetScale, float duration)
        {
#if DOTWEEN
            transform.DOKill();
            transform.DOScale(targetScale, duration).SetEase(Ease.OutQuad).SetUpdate(true);
#endif
            if (_scaleRoutine != null) StopCoroutine(_scaleRoutine);
            _scaleRoutine = StartCoroutine(ScaleRoutine(targetScale, duration));
        }

        private void AnimateGlow(Color targetColor, float duration)
        {
            if (glowBorderGraphic != null)
            {
#if DOTWEEN
                glowBorderGraphic.DOKill();
                glowBorderGraphic.DOColor(targetColor, duration).SetUpdate(true);
#endif
                if (_colorRoutine != null) StopCoroutine(_colorRoutine);
                _colorRoutine = StartCoroutine(ColorRoutine(glowBorderGraphic, targetColor, duration));
            }

            if (glowOutline != null)
            {
#if DOTWEEN
                glowOutline.DOKill();
                glowOutline.DOColor(targetColor, duration).SetUpdate(true);
#endif
                if (_outlineRoutine != null) StopCoroutine(_outlineRoutine);
                _outlineRoutine = StartCoroutine(OutlineRoutine(glowOutline, targetColor, duration));
            }
        }

        private IEnumerator ScaleRoutine(Vector3 target, float duration)
        {
            Vector3 start = transform.localScale;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(t / duration);
                float ease = 1f - (1f - progress) * (1f - progress);
                transform.localScale = Vector3.LerpUnclamped(start, target, ease);
                yield return null;
            }
            transform.localScale = target;
            _scaleRoutine = null;
        }

        private IEnumerator ColorRoutine(Graphic graphic, Color target, float duration)
        {
            if (graphic == null) yield break;
            Color start = graphic.color;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                graphic.color = Color.Lerp(start, target, Mathf.Clamp01(t / duration));
                yield return null;
            }
            graphic.color = target;
            _colorRoutine = null;
        }

        private IEnumerator OutlineRoutine(Outline outline, Color target, float duration)
        {
            if (outline == null) yield break;
            Color start = outline.effectColor;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                outline.effectColor = Color.Lerp(start, target, Mathf.Clamp01(t / duration));
                yield return null;
            }
            outline.effectColor = target;
            _outlineRoutine = null;
        }
    }
}
