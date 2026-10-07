using System;
using UnityEngine;

namespace Railbound.UI.Anim
{
    /// <summary>
    /// Popup/dialog animation:
    ///   Open  - overlay fades in, dialog scales 0.85 -> 1.00 with Back Out
    ///           (natural overshoot through ~1.05) over 0.20-0.35s.
    ///   Close - scale 1.00 -> 0.95 + fade out.
    /// </summary>
    public static class PopupAnimator
    {
        public static void Open(RectTransform dialog, CanvasGroup overlay = null, Action onDone = null)
        {
            if (overlay != null)
            {
                overlay.interactable = true;
                overlay.blocksRaycasts = true;
                if (Application.isPlaying)
                    UITween.FadeTo(overlay, 1f, UIAnim.Popup, UIAnim.HoverEase, null, overlay);
                else
                    overlay.alpha = 1f;
            }

            if (dialog == null) { onDone?.Invoke(); return; }

            if (!Application.isPlaying)
            {
                dialog.localScale = Vector3.one;
                onDone?.Invoke();
                return;
            }

            UITween.Kill(dialog);
            dialog.localScale = Vector3.one * 0.85f;
            // 0.85 -> 1.05 (Back Out) -> 1.00, total ~0.28s (popup window 0.20-0.35)
            UITween.ScaleTo(dialog, Vector3.one * 1.05f, 0.18f, UIAnim.PopEase, () =>
            {
                if (dialog != null)
                    UITween.ScaleTo(dialog, Vector3.one, 0.10f, UIAnim.TransitionEase, onDone, dialog);
            }, dialog);
        }

        public static void Close(RectTransform dialog, CanvasGroup overlay = null, Action onDone = null)
        {
            if (overlay != null)
            {
                overlay.interactable = false;
                overlay.blocksRaycasts = false;
            }

            if (dialog == null)
            {
                if (overlay != null && !Application.isPlaying) overlay.alpha = 0f;
                onDone?.Invoke();
                return;
            }

            if (!Application.isPlaying)
            {
                if (overlay != null) overlay.alpha = 0f;
                dialog.localScale = Vector3.one;
                onDone?.Invoke();
                return;
            }

            UITween.Kill(dialog);
            UITween.ScaleTo(dialog, Vector3.one * 0.95f, UIAnim.MenuClose, UIAnim.TransitionEase, null, dialog);
            if (overlay != null)
                UITween.FadeTo(overlay, 0f, UIAnim.MenuClose, UIAnim.TransitionEase, onDone, overlay);
            else
                onDone?.Invoke();
        }
    }
}
