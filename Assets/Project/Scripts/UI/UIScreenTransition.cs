using System;
using System.Collections;
using UnityEngine;
#if DOTWEEN
using DG.Tweening;
#endif

namespace Railbound.UI
{
    /// <summary>
    /// Smooth screen fade and settle transitions driven by CanvasGroup.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class UIScreenTransition : MonoBehaviour
    {
        [Header("Animation Timings")]
        [SerializeField] private float fadeInDuration = 0.25f;
        [SerializeField] private float fadeOutDuration = 0.18f;
        [SerializeField] private float enterOffsetY = 20f;
        [SerializeField] private bool playOnAwake = false;

        private CanvasGroup _canvasGroup;
        private RectTransform _rectTransform;
        private Vector2 _defaultAnchoredPos;
        private Coroutine _activeRoutine;

        private void Awake()
        {
            EnsureComponents();
            if (playOnAwake)
                FadeIn();
        }

        private void EnsureComponents()
        {
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            if (_rectTransform == null)
            {
                _rectTransform = GetComponent<RectTransform>();
                if (_rectTransform != null) _defaultAnchoredPos = _rectTransform.anchoredPosition;
            }
        }

        public void FadeIn(Action onComplete = null)
        {
            EnsureComponents();
            gameObject.SetActive(true);
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;

#if DOTWEEN
            _canvasGroup.DOKill();
            _rectTransform.DOKill();

            _canvasGroup.alpha = 0f;
            _rectTransform.anchoredPosition = _defaultAnchoredPos + new Vector2(0f, enterOffsetY);

            _canvasGroup.DOFade(1f, fadeInDuration).SetUpdate(true);
            _rectTransform.DOAnchorPos(_defaultAnchoredPos, fadeInDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .OnComplete(() => onComplete?.Invoke());
#else
            if (_activeRoutine != null) StopCoroutine(_activeRoutine);
            _activeRoutine = StartCoroutine(FadeRoutine(0f, 1f, enterOffsetY, 0f, fadeInDuration, onComplete));
#endif
        }

        public void FadeOut(Action onComplete = null)
        {
            EnsureComponents();
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

#if DOTWEEN
            _canvasGroup.DOKill();
            _rectTransform.DOKill();

            _canvasGroup.DOFade(0f, fadeOutDuration)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    gameObject.SetActive(false);
                    onComplete?.Invoke();
                });
#else
            if (_activeRoutine != null) StopCoroutine(_activeRoutine);
            _activeRoutine = StartCoroutine(FadeRoutine(_canvasGroup.alpha, 0f, 0f, 0f, fadeOutDuration, () =>
            {
                gameObject.SetActive(false);
                onComplete?.Invoke();
            }));
#endif
        }

        public void SetInstant(bool visible)
        {
            EnsureComponents();
            if (_activeRoutine != null) StopCoroutine(_activeRoutine);
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
            _rectTransform.anchoredPosition = _defaultAnchoredPos;
            gameObject.SetActive(visible);
        }

        private IEnumerator FadeRoutine(float startAlpha, float endAlpha, float startOffsetY, float endOffsetY, float duration, Action onComplete)
        {
            float t = 0f;
            Vector2 startPos = _defaultAnchoredPos + new Vector2(0f, startOffsetY);
            Vector2 endPos = _defaultAnchoredPos + new Vector2(0f, endOffsetY);

            _canvasGroup.alpha = startAlpha;
            _rectTransform.anchoredPosition = startPos;

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(t / duration);
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, progress);
                _rectTransform.anchoredPosition = Vector2.Lerp(startPos, endPos, progress);
                yield return null;
            }

            _canvasGroup.alpha = endAlpha;
            _rectTransform.anchoredPosition = endPos;
            onComplete?.Invoke();
        }
    }
}
