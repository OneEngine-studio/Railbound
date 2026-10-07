using System;
using System.Collections.Generic;
using UnityEngine;

namespace Railbound.UI.Anim
{
    /// <summary>
    /// Menu open/close animation:
    ///   Open  - fade in + panel scales 0.95 -> 1.00 + slides into position (Ease Out).
    ///   Close - fade out + scale 1.00 -> 0.97 (Ease In Out).
    /// In edit mode everything settles instantly so scene previews stay correct.
    /// </summary>
    public static class MenuAnimator
    {
        private static readonly Dictionary<RectTransform, Vector3> BaseScales = new Dictionary<RectTransform, Vector3>();
        private static readonly Dictionary<RectTransform, Vector2> BasePositions = new Dictionary<RectTransform, Vector2>();

        public static void Open(CanvasGroup group, RectTransform panel = null, Action onDone = null)
        {
            if (group == null) { onDone?.Invoke(); return; }

            if (!Application.isPlaying)
            {
                group.alpha = 1f;
                group.interactable = true;
                group.blocksRaycasts = true;
                onDone?.Invoke();
                return;
            }

            group.interactable = true;
            group.blocksRaycasts = true;
            UITween.Kill(group);
            UITween.FadeTo(group, 1f, UIAnim.MenuOpen, UIAnim.HoverEase, null, group);

            if (panel != null)
            {
                RememberBase(panel);
                var basePos = BasePositions[panel];
                var baseScale = BaseScales[panel];
                UITween.Kill(panel);
                panel.anchoredPosition = basePos + new Vector2(0f, 24f);
                panel.localScale = baseScale * 0.95f;
                UITween.MoveAnchoredTo(panel, basePos, UIAnim.MenuOpen, UIAnim.HoverEase, null, panel);
                UITween.ScaleTo(panel, baseScale, UIAnim.MenuOpen, UIAnim.HoverEase, onDone, panel);
            }
            else
            {
                onDone?.Invoke();
            }
        }

        public static void Close(CanvasGroup group, RectTransform panel = null, Action onDone = null)
        {
            if (group == null) { onDone?.Invoke(); return; }

            if (!Application.isPlaying)
            {
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;
                onDone?.Invoke();
                return;
            }

            group.interactable = false;
            group.blocksRaycasts = false;
            UITween.Kill(group);
            UITween.FadeTo(group, 0f, UIAnim.MenuClose, UIAnim.TransitionEase, null, group);

            if (panel != null)
            {
                RememberBase(panel);
                var baseScale = BaseScales[panel];
                UITween.Kill(panel);
                UITween.ScaleTo(panel, baseScale * 0.97f, UIAnim.MenuClose, UIAnim.TransitionEase, onDone, panel);
            }
            else
            {
                onDone?.Invoke();
            }
        }

        /// <summary>Quick pop when content swaps inside an already-open menu (step changes).</summary>
        public static void SwapStep(RectTransform panel)
        {
            if (panel == null || !Application.isPlaying) return;
            RememberBase(panel);
            var baseScale = BaseScales[panel];
            UITween.Kill(panel);
            UITween.To(0f, 1f, 0.22f, UIAnim.PopEase,
                p => { if (panel != null) panel.localScale = baseScale * Mathf.LerpUnclamped(0.97f, 1f, p); },
                null, 0f, panel);
        }

        private static void RememberBase(RectTransform panel)
        {
            if (!BaseScales.ContainsKey(panel))
            {
                BaseScales[panel] = panel.localScale;
                BasePositions[panel] = panel.anchoredPosition;
            }
        }
    }
}
