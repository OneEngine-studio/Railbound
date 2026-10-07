using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Railbound.UI.Anim
{
    /// <summary>Easing curves used by every UI animation (see UIAnim for which curve goes where).</summary>
    public enum Ease
    {
        Linear,      // continuous loops only (spinners, progress)
        OutQuad,
        OutCubic,    // default for hover / pops of position
        OutQuint,
        InQuad,
        InCubic,
        InOutQuad,
        InOutCubic,  // default for screen/menu transitions
        BackOut,     // pop effects (overshoot)
        BackInOut,
        ElasticOut,  // use sparingly
        OutExpo
    }

    public static class Easing
    {
        public static float Evaluate(Ease ease, float t)
        {
            t = Mathf.Clamp01(t);
            switch (ease)
            {
                case Ease.Linear: return t;
                case Ease.OutQuad: return 1f - (1f - t) * (1f - t);
                case Ease.OutCubic: { float u = 1f - t; return 1f - u * u * u; }
                case Ease.OutQuint: { float u = 1f - t; return 1f - u * u * u * u * u; }
                case Ease.InQuad: return t * t;
                case Ease.InCubic: return t * t * t;
                case Ease.InOutQuad:
                    return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) * 0.5f;
                case Ease.InOutCubic:
                    return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
                case Ease.BackOut:
                {
                    const float c1 = 1.70158f, c3 = c1 + 1f;
                    float u = t - 1f;
                    return 1f + c3 * u * u * u + c1 * u * u;
                }
                case Ease.BackInOut:
                {
                    const float c2 = 1.70158f * 1.525f;
                    return t < 0.5f
                        ? (Mathf.Pow(2f * t, 2f) * ((c2 + 1f) * 2f * t - c2)) * 0.5f
                        : (Mathf.Pow(2f * t - 2f, 2f) * ((c2 + 1f) * (t * 2f - 2f) + c2) + 2f) * 0.5f;
                }
                case Ease.ElasticOut:
                {
                    if (t <= 0f) return 0f;
                    if (t >= 1f) return 1f;
                    const float c4 = (2f * Mathf.PI) / 3f;
                    return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
                }
                case Ease.OutExpo: return t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);
                default: return t;
            }
        }
    }

    /// <summary>Handle for a running tween. Cancel() stops it without completing.</summary>
    public sealed class Tween
    {
        internal float From, To, Duration, Delay, Elapsed;
        internal Ease Ease = Ease.OutCubic;
        internal Action<float> Setter;
        internal Action OnComplete;
        internal int Cycles = 1;        // -1 = infinite loop
        internal bool Yoyo;
        internal int CycleIndex;
        internal object Target;
        internal bool Killed;
        internal bool Finished;

        public bool IsDone => Killed || Finished;

        public void Cancel() => Killed = true;
    }

    /// <summary>
    /// Minimal tween engine for uGUI. Runs on unscaled time (menus keep animating while the
    /// game is paused). Outside of Play Mode every tween applies its final value instantly,
    /// so edit-mode previews always show the settled state.
    /// </summary>
    public static class UITween
    {
        private static readonly List<Tween> Active = new List<Tween>();
        private static TweenRunner _runner;

        // ------------------------------------------------------------------ public API

        /// <summary>Core tween: from -> to over duration seconds.</summary>
        public static Tween To(
            float from, float to, float duration, Ease ease, Action<float> setter,
            Action onComplete = null, float delay = 0f, object target = null)
        {
            if (!Application.isPlaying)
            {
                setter?.Invoke(to);
                onComplete?.Invoke();
                return new Tween { Finished = true };
            }

            var tween = new Tween
            {
                From = from,
                To = to,
                Duration = Mathf.Max(0.0001f, duration),
                Delay = Mathf.Max(0f, delay),
                Ease = ease,
                Setter = setter,
                OnComplete = onComplete,
                Cycles = 1,
                Target = target
            };
            Active.Add(tween);
            var unused = Runner; // ensure the driver exists
            return tween;
        }

        /// <summary>
        /// Continuous loop (cycles = -1). Use for spinners (Linear) and breathing/pulse effects
        /// (yoyo + InOutCubic). No-op outside of Play Mode.
        /// </summary>
        public static Tween Loop(
            float from, float to, float cycleDuration, Ease ease, Action<float> setter,
            bool yoyo = true, float delay = 0f, object target = null)
        {
            if (!Application.isPlaying)
                return new Tween { Finished = true };

            var tween = new Tween
            {
                From = from,
                To = to,
                Duration = Mathf.Max(0.0001f, cycleDuration),
                Delay = Mathf.Max(0f, delay),
                Ease = ease,
                Setter = setter,
                Cycles = -1,
                Yoyo = yoyo,
                Target = target
            };
            Active.Add(tween);
            var unused = Runner;
            return tween;
        }

        public static Tween ScaleTo(RectTransform rt, Vector3 to, float duration, Ease ease, Action onComplete = null, object target = null)
        {
            if (rt == null) { onComplete?.Invoke(); return new Tween { Finished = true }; }
            var from = rt.localScale;
            return To(0f, 1f, duration, ease, p => { if (rt != null) rt.localScale = Vector3.LerpUnclamped(from, to, p); }, onComplete, 0f, target ?? rt);
        }

        public static Tween MoveAnchoredTo(RectTransform rt, Vector2 to, float duration, Ease ease, Action onComplete = null, object target = null)
        {
            if (rt == null) { onComplete?.Invoke(); return new Tween { Finished = true }; }
            var from = rt.anchoredPosition;
            return To(0f, 1f, duration, ease, p => { if (rt != null) rt.anchoredPosition = Vector2.LerpUnclamped(from, to, p); }, onComplete, 0f, target ?? rt);
        }

        public static Tween FadeTo(CanvasGroup group, float to, float duration, Ease ease, Action onComplete = null, object target = null)
        {
            if (group == null) { onComplete?.Invoke(); return new Tween { Finished = true }; }
            var from = group.alpha;
            return To(from, to, duration, ease, v => { if (group != null) group.alpha = v; }, onComplete, 0f, target ?? group);
        }

        public static Tween ColorTo(Graphic graphic, Color to, float duration, Ease ease, Action onComplete = null, object target = null)
        {
            if (graphic == null) { onComplete?.Invoke(); return new Tween { Finished = true }; }
            var from = graphic.color;
            return To(0f, 1f, duration, ease, p => { if (graphic != null) graphic.color = Color.LerpUnclamped(from, to, p); }, onComplete, 0f, target ?? graphic);
        }

        public static Tween RotateZTo(RectTransform rt, float z, float duration, Ease ease, Action onComplete = null, object target = null)
        {
            if (rt == null) { onComplete?.Invoke(); return new Tween { Finished = true }; }
            var from = rt.localEulerAngles.z;
            return To(from, z, duration, ease, v => { if (rt != null) rt.localEulerAngles = new Vector3(0f, 0f, v); }, onComplete, 0f, target ?? rt);
        }

        /// <summary>Cancels every tween registered against this target (usually a component's channel token).</summary>
        public static void Kill(object target)
        {
            if (target == null) return;
            for (var i = 0; i < Active.Count; i++)
                if (ReferenceEquals(Active[i].Target, target))
                    Active[i].Killed = true;
        }

        public static void KillAll()
        {
            for (var i = 0; i < Active.Count; i++)
                Active[i].Killed = true;
        }

        // ------------------------------------------------------------------ driver

        private static TweenRunner Runner
        {
            get
            {
                if (_runner == null)
                {
                    var go = new GameObject("__UITweenRunner__");
                    go.hideFlags = HideFlags.HideInHierarchy;
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    _runner = go.AddComponent<TweenRunner>();
                }
                return _runner;
            }
        }

        internal static void Step(float delta)
        {
            for (var i = Active.Count - 1; i >= 0; i--)
            {
                var tween = Active[i];
                if (tween.Killed)
                {
                    Active.RemoveAt(i);
                    continue;
                }

                if (tween.Delay > 0f)
                {
                    tween.Delay -= delta;
                    if (tween.Delay > 0f) continue;
                }

                tween.Elapsed += delta;
                var t = Mathf.Clamp01(tween.Elapsed / tween.Duration);
                var eased = Easing.Evaluate(tween.Ease, tween.Yoyo && (tween.CycleIndex & 1) == 1 ? 1f - t : t);
                tween.Setter?.Invoke(tween.From + (tween.To - tween.From) * eased);

                if (t < 1f) continue;

                tween.CycleIndex++;
                if (tween.Cycles > 0)
                {
                    tween.Cycles--;
                    if (tween.Cycles > 0)
                    {
                        tween.Elapsed -= tween.Duration;
                        continue;
                    }

                    tween.Finished = true;
                    tween.Setter?.Invoke(tween.To); // exact end value
                    Active.RemoveAt(i);
                    try { tween.OnComplete?.Invoke(); }
                    catch (Exception exception) { Debug.LogException(exception); }
                }
                else
                {
                    tween.Elapsed -= tween.Duration; // infinite loop
                }
            }
        }

        private sealed class TweenRunner : MonoBehaviour
        {
            private void Update() => Step(Time.unscaledDeltaTime);
            private void OnDestroy() { if (_runner == this) _runner = null; }
        }
    }
}
