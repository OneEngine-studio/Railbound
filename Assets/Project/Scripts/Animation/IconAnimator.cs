using UnityEngine;
using UnityEngine.UI;

namespace Railbound.UI.Anim
{
    /// <summary>
    /// Reusable icon animations: scale, rotation, fade, slide, bounce, pulse, glow,
    /// color transition, shake, spin and flip. All methods are safe to call from
    /// edit mode (they settle instantly) and are no-ops for loops outside Play Mode.
    /// </summary>
    public static class IconAnimator
    {
        /// <summary>Smooth scale pulse (warning icons, badges). Slow + subtle by default.</summary>
        public static void Pulse(RectTransform rt, float amount = 1.12f, float cycle = UIAnim.AmbientCycle, object target = null)
        {
            if (rt == null) return;
            var from = rt.localScale;
            var to = from * amount;
            UITween.Loop(1f, 1f, cycle, Ease.InOutCubic,
                p => { if (rt != null) rt.localScale = Vector3.LerpUnclamped(from, to, p); },
                yoyo: true, target: target ?? rt);
        }

        /// <summary>Glow pulse: alpha oscillates between minAlpha and maxAlpha.</summary>
        public static void GlowPulse(Image image, float minAlpha = 0.35f, float maxAlpha = 0.9f, float cycle = UIAnim.AmbientCycle, object target = null)
        {
            if (image == null) return;
            var color = image.color;
            UITween.Loop(minAlpha, maxAlpha, cycle, Ease.InOutCubic,
                a => { if (image != null) image.color = new Color(color.r, color.g, color.b, a); },
                yoyo: true, target: target ?? image);
        }

        /// <summary>Horizontal shake with decay (errors, locked/unavailable feedback).</summary>
        public static void Shake(RectTransform rt, float distance = 4f, float duration = UIAnim.ErrorShake, object target = null)
        {
            if (rt == null) return;
            if (!Application.isPlaying) return;
            var basePos = rt.anchoredPosition;
            var token = target ?? rt;
            UITween.Kill(token);
            UITween.To(0f, 1f, duration, Ease.Linear,
                p =>
                {
                    if (rt == null) return;
                    var falloff = 1f - p;
                    var offset = Mathf.Sin(p * Mathf.PI * 6f) * distance * falloff;
                    rt.anchoredPosition = basePos + new Vector2(offset, 0f);
                },
                () => { if (rt != null) rt.anchoredPosition = basePos; },
                0f, token);
        }

        /// <summary>Continuous rotation - Linear, the only place Linear easing is allowed.</summary>
        public static void Spin(RectTransform rt, float degreesPerCycle = 360f, float cycle = UIAnim.SpinnerCycle, object target = null)
        {
            if (rt == null) return;
            var baseRot = rt.localEulerAngles.z;
            UITween.Loop(0f, degreesPerCycle, cycle, UIAnim.LoopEase,
                v => { if (rt != null) rt.localEulerAngles = new Vector3(0f, 0f, baseRot + v); },
                yoyo: false, target: target ?? rt);
        }

        public static void StopSpin(RectTransform rt, object target = null)
        {
            if (rt == null) return;
            UITween.Kill(target ?? rt);
            rt.localEulerAngles = new Vector3(0f, rt.localEulerAngles.y, 0f);
        }

        /// <summary>Jump up and settle back (bounce feel).</summary>
        public static void Bounce(RectTransform rt, float height = 8f, float duration = 0.4f, object target = null)
        {
            if (rt == null) return;
            var token = target ?? rt;
            var basePos = rt.anchoredPosition;
            var peak = basePos + new Vector2(0f, height);
            UITween.Kill(token);
            UITween.To(0f, 1f, duration * 0.4f, Ease.OutCubic,
                p => { if (rt != null) rt.anchoredPosition = Vector2.LerpUnclamped(basePos, peak, p); },
                () =>
                {
                    UITween.To(0f, 1f, duration * 0.6f, Ease.InCubic,
                        p => { if (rt != null) rt.anchoredPosition = Vector2.LerpUnclamped(peak, basePos, p); },
                        null, 0f, token);
                }, 0f, token);
        }

        /// <summary>Slide a fixed distance in a direction and come back (arrows: 4-8px).</summary>
        public static void SlideAndBack(RectTransform rt, Vector2 direction, float distance = UIAnim.ArrowSlide, float duration = 0.18f, object target = null)
        {
            if (rt == null) return;
            var token = target ?? rt;
            var basePos = rt.anchoredPosition;
            var outPos = basePos + direction.normalized * distance;
            UITween.Kill(token);
            UITween.To(0f, 1f, duration * 0.5f, Ease.OutCubic,
                p => { if (rt != null) rt.anchoredPosition = Vector2.LerpUnclamped(basePos, outPos, p); },
                () =>
                {
                    UITween.To(0f, 1f, duration * 0.5f, Ease.InOutCubic,
                        p => { if (rt != null) rt.anchoredPosition = Vector2.LerpUnclamped(outPos, basePos, p); },
                        null, 0f, token);
                }, 0f, token);
        }

        /// <summary>Fade a graphic to an alpha.</summary>
        public static void Fade(Graphic graphic, float toAlpha, float duration = UIAnim.Hover, object target = null)
        {
            if (graphic == null) return;
            var from = graphic.color;
            var to = new Color(from.r, from.g, from.b, toAlpha);
            UITween.To(0f, 1f, duration, UIAnim.HoverEase,
                p => { if (graphic != null) graphic.color = Color.LerpUnclamped(from, to, p); },
                null, 0f, target ?? graphic);
        }

        /// <summary>Color transition.</summary>
        public static void ColorTo(Graphic graphic, Color to, float duration = UIAnim.Hover, object target = null)
        {
            if (graphic == null) return;
            UITween.ColorTo(graphic, to, duration, UIAnim.TransitionEase, null, target ?? graphic);
        }

        /// <summary>Rotate to an absolute Z angle.</summary>
        public static void RotateTo(RectTransform rt, float z, float duration = UIAnim.Hover, object target = null)
        {
            if (rt == null) return;
            UITween.RotateZTo(rt, z, duration, UIAnim.HoverEase, null, target ?? rt);
        }

        /// <summary>Scale pop from zero with Back Out overshoot (checkmarks, icon pops).</summary>
        public static void PopIn(RectTransform rt, float duration = UIAnim.Popup, object target = null)
        {
            if (rt == null) return;
            var final = rt.localScale;
            if (Mathf.Approximately(final.x, 0f)) final = Vector3.one;
            UITween.ScaleTo(rt, final, duration, UIAnim.PopEase, null, target ?? rt);
        }

        /// <summary>180 degree flip around an axis.</summary>
        public static void Flip(RectTransform rt, bool horizontal = true, float duration = UIAnim.Hover, object target = null)
        {
            if (rt == null) return;
            var from = rt.localEulerAngles;
            var to = horizontal
                ? new Vector3(from.x, from.y + 180f, from.z)
                : new Vector3(from.x + 180f, from.y, from.z);
            UITween.To(0f, 1f, duration, UIAnim.TransitionEase,
                p => { if (rt != null) rt.localEulerAngles = Vector3.LerpUnclamped(from, to, p); },
                null, 0f, target ?? rt);
        }
    }

    /// <summary>
    /// Attach to any object that should pulse continuously at runtime
    /// (notification badges, warning icons). Keeps edit-mode previews static.
    /// </summary>
    public sealed class AmbientPulse : MonoBehaviour
    {
        [SerializeField] private float amount = 1.10f;
        [SerializeField] private float cycle = 1.2f;

        private void Start()
        {
            IconAnimator.Pulse((RectTransform)transform, amount, cycle, this);
        }

        private void OnDisable() => UITween.Kill(this);
    }
}
