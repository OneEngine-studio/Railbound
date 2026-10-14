using System;
using UnityEngine;
using UnityEngine.UI;

namespace Railbound.UI.Anim
{
    public enum ScreenTransitionType
    {
        Fade,
        Crossfade,
        SlideLeft,
        SlideRight,
        Zoom,
        Blur            // dark-overlay + scale approximation of a blur (no post FX required)
    }

    /// <summary>
    /// Screen-to-screen transitions. Directional moves should follow navigation:
    /// forward (main -> play) slides the current screen left and brings the next in from the
    /// right; backward reverses it. Duration 0.30-0.50s, Ease In Out.
    /// </summary>
    public static class ScreenTransition
    {
        /// <summary>Transition from one screen to another. Screens get CanvasGroups on demand.</summary>
        public static void Play(
            RectTransform from, RectTransform to,
            ScreenTransitionType type, bool forward = true,
            float duration = UIAnim.Screen, Action onDone = null)
        {
            if (!Application.isPlaying)
            {
                if (from != null) SetVisible(from, false);
                if (to != null) SetVisible(to, true);
                onDone?.Invoke();
                return;
            }

            duration = Mathf.Clamp(duration, 0.30f, 0.50f);
            var fromGroup = GetGroup(from, true);
            var toGroup = GetGroup(to, true);

            if (to != null)
            {
                SetVisible(to, true);
                to.SetAsLastSibling();
            }

            switch (type)
            {
                case ScreenTransitionType.Fade:
                    if (from != null) UITween.FadeTo(fromGroup, 0f, duration * 0.5f, UIAnim.TransitionEase, null, fromGroup);
                    if (to != null)
                    {
                        toGroup.alpha = 0f;
                        UITween.FadeTo(toGroup, 1f, duration, UIAnim.TransitionEase, () => { SetVisible(from, false); onDone?.Invoke(); }, toGroup);
                    }
                    else onDone?.Invoke();
                    break;

                case ScreenTransitionType.Crossfade:
                    if (from != null) UITween.FadeTo(fromGroup, 0f, duration, UIAnim.TransitionEase, null, fromGroup);
                    if (to != null)
                    {
                        toGroup.alpha = 0f;
                        UITween.FadeTo(toGroup, 1f, duration, UIAnim.TransitionEase, () => { SetVisible(from, false); onDone?.Invoke(); }, toGroup);
                    }
                    else onDone?.Invoke();
                    break;

                case ScreenTransitionType.SlideLeft:
                    Slide(from, to, to != null ? -1f : 0f, forward, duration, fromGroup, toGroup, onDone);
                    break;

                case ScreenTransitionType.SlideRight:
                    Slide(from, to, to != null ? 1f : 0f, forward, duration, fromGroup, toGroup, onDone);
                    break;

                case ScreenTransitionType.Zoom:
                    ZoomTransition(from, to, duration, fromGroup, toGroup, onDone);
                    break;

                case ScreenTransitionType.Blur:
                    BlurTransition(from, to, duration, fromGroup, toGroup, onDone);
                    break;

                default:
                    onDone?.Invoke();
                    break;
            }
        }

        /// <summary>Convenience: direction is derived from navigation (forward/back).</summary>
        public static void Directional(RectTransform from, RectTransform to, bool forward, float duration = UIAnim.Screen, Action onDone = null)
        {
            Play(from, to, forward ? ScreenTransitionType.SlideLeft : ScreenTransitionType.SlideRight, forward, duration, onDone);
        }

        private static void Slide(
            RectTransform from, RectTransform to, float outDirection, bool forward,
            float duration, CanvasGroup fromGroup, CanvasGroup toGroup, Action onDone)
        {
            // forward: current exits toward -X (left), next enters from +X (right)
            var sign = forward ? -1f : 1f;
            if (from != null && to != null) outDirection = sign;

            if (from != null)
            {
                var fromStart = from.anchoredPosition;
                var fromEnd = fromStart + new Vector2(outDirection * from.rect.width, 0f);
                UITween.MoveAnchoredTo(from, fromEnd, duration, UIAnim.TransitionEase,
                    () => SetVisible(from, false), from);
            }

            if (to != null)
            {
                var toEnd = to.anchoredPosition;
                var toStart = toEnd - new Vector2(outDirection * to.rect.width, 0f);
                to.anchoredPosition = toStart;
                toGroup.alpha = 1f;
                UITween.MoveAnchoredTo(to, toEnd, duration, UIAnim.TransitionEase,
                    onDone, to);
            }
            else onDone?.Invoke();
        }

        private static void ZoomTransition(
            RectTransform from, RectTransform to, float duration,
            CanvasGroup fromGroup, CanvasGroup toGroup, Action onDone)
        {
            if (from != null)
            {
                var fromScale = from.localScale;
                UITween.ScaleTo(from, fromScale * 1.08f, duration, UIAnim.TransitionEase, null, from);
                UITween.FadeTo(fromGroup, 0f, duration, UIAnim.TransitionEase, () => SetVisible(from, false), fromGroup);
            }

            if (to != null)
            {
                var toScale = to.localScale;
                to.localScale = toScale * 0.92f;
                toGroup.alpha = 0f;
                UITween.ScaleTo(to, toScale, duration, UIAnim.TransitionEase, null, to);
                UITween.FadeTo(toGroup, 1f, duration, UIAnim.TransitionEase, onDone, toGroup);
            }
            else onDone?.Invoke();
        }

        private static void BlurTransition(
            RectTransform from, RectTransform to, float duration,
            CanvasGroup fromGroup, CanvasGroup toGroup, Action onDone)
        {
            // Post-process blur substitute: outgoing screen softens out (fade + slight zoom),
            // incoming resolves with a small zoom-in. Swap in a blur material here if the
            // project gains a blur shader.
            if (from != null)
            {
                var fromScale = from.localScale;
                UITween.ScaleTo(from, fromScale * 1.03f, duration, UIAnim.TransitionEase, null, from);
                UITween.FadeTo(fromGroup, 0f, duration * 0.6f, UIAnim.TransitionEase, () => SetVisible(from, false), fromGroup);
            }

            if (to != null)
            {
                var toScale = to.localScale;
                to.localScale = toScale * 1.02f;
                toGroup.alpha = 0f;
                UITween.ScaleTo(to, toScale, duration, UIAnim.TransitionEase, null, to);
                UITween.FadeTo(toGroup, 1f, duration * 0.7f, UIAnim.TransitionEase, onDone, toGroup);
            }
            else onDone?.Invoke();
        }

        private static CanvasGroup GetGroup(RectTransform rect, bool createIfNeeded)
        {
            if (rect == null) return null;
            var group = rect.GetComponent<CanvasGroup>();
            if (group == null && createIfNeeded) group = rect.gameObject.AddComponent<CanvasGroup>();
            return group;
        }

        private static void SetVisible(RectTransform rect, bool visible)
        {
            if (rect == null) return;
            rect.gameObject.SetActive(visible);
        }
    }
}
