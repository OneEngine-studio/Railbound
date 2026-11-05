using TMPro;
using UnityEngine;
using JUTPS;

namespace Railbound.UI
{
    /// <summary>
    /// Extras for the vitals card next to <see cref="SegmentedHealthBarUI"/>: a numeric health readout and a
    /// pulsing low-health warning stripe. Reads health through the segmented bar, so it follows the same
    /// player reference (including the network player after the HUD references are rebound).
    /// </summary>
    [DisallowMultipleComponent]
    public class HudVitalsReadout : MonoBehaviour
    {
        [SerializeField] private SegmentedHealthBarUI healthBar;
        [SerializeField] private TMP_Text healthValue;
        [SerializeField] private CanvasGroup lowHealthWarning;

        [Header("Style")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color criticalColor = new Color(1f, 0.32f, 0.28f, 1f);
        [SerializeField, Range(0.05f, 0.6f)] private float criticalFraction = 2f / 7f;
        [SerializeField] private float pulseSpeed = 7f;

        private int _lastShown = int.MinValue;

        void Awake()
        {
            if (healthBar == null) healthBar = GetComponent<SegmentedHealthBarUI>();
        }

        void Update()
        {
            var h = healthBar != null ? healthBar.Health : null;
            if (h == null) return;

            int value = Mathf.Max(0, Mathf.CeilToInt(h.Health));
            float norm = Mathf.Clamp01(h.Health / Mathf.Max(1f, h.MaxHealth));
            bool critical = norm <= criticalFraction && value > 0;

            if (healthValue != null)
            {
                if (value != _lastShown) { healthValue.text = value.ToString(); _lastShown = value; }
                healthValue.color = critical ? criticalColor : normalColor;
            }

            if (lowHealthWarning != null)
            {
                float target = critical ? 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * pulseSpeed) * 0.5f + 0.225f : 0f;
                lowHealthWarning.alpha = Mathf.MoveTowards(lowHealthWarning.alpha, target, Time.unscaledDeltaTime * 4f);
            }
        }
    }
}
