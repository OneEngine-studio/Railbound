using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace JUTPS.FX
{
    [AddComponentMenu("JU TPS/FX/Slow Motion")]
    public class JUSlowmotion : MonoBehaviour
    {
        public static JUSlowmotion Instance;

        [Header("Slowmotion Settings")]
        public bool EnableSlowmotion = true;

        float SlowDownFactor = 0.05f;
        float SlowDownLenght = 1f;

        protected virtual void Start()
        {
            Instance = this;
            Time.fixedDeltaTime = 0.015f;
        }

        protected virtual void Update()
        {
            if (!EnableSlowmotion)
                return;

            if (SlowDownLenght <= 0f)
                SlowDownLenght = 1f;

            Time.timeScale += (1f / SlowDownLenght) * Time.unscaledDeltaTime;
            Time.timeScale = Mathf.Clamp(Time.timeScale, 0f, 1f);
            Time.fixedDeltaTime = Mathf.Clamp(Time.fixedDeltaTime, 0.01f, 0.333f);
        }

        /// <summary>
        /// Do a slowmotion effect
        /// </summary>
        /// <param name="timescale">time scale at the start of slow motion</param>
        /// <param name="duration">duration of slowmotion</param>
        public static void DoSlowMotion(float timescale = 0.1f, float duration = 2f)
        {
            if (Instance == null)
                return;

            if (Instance.EnableSlowmotion == false)
            {
                Debug.LogWarning("Called Slow Motion effect but it is not enabled");
                return;
            }

            timescale = Mathf.Clamp01(timescale);
            duration = Mathf.Max(0.01f, duration);

            Instance.SlowDownFactor = timescale;
            Instance.SlowDownLenght = duration;
            Time.timeScale = timescale;
            Time.fixedDeltaTime = Time.timeScale * .01f;

            Instance.CancelInvoke(nameof(DisableSlowmotion));
            Instance.Invoke(nameof(DisableSlowmotion), 0.4f * duration);
        }

        /// <summary>
        /// Do a slowmotion effect
        /// </summary>
        public static void DoSlowMotion()
        {
            DoSlowMotion(0.1f, 2f);
        }

        /// <summary>
        /// Disable slowmotion effect and reset fixed time step to 0.015f
        /// </summary>
        public void DisableSlowmotion()
        {
            SlowDownFactor = 1f;
            SlowDownLenght = 1f;
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.015f;
        }
    }
}
