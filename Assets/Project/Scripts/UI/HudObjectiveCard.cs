using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Railbound.BR;

namespace Railbound.UI
{
    /// <summary>
    /// Top-left objective card: current zone phase, what the player should do (return to the zone / hold / zone closing),
    /// the countdown and the distance to the safe zone edge. Reads everything from <see cref="BRZoneManager"/> and is
    /// hidden while there is no zone (lobby, offline test scenes without a zone manager).
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    [DisallowMultipleComponent]
    public class HudObjectiveCard : MonoBehaviour
    {
        [SerializeField] private TMP_Text phaseText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text timerLabel;
        [SerializeField] private TMP_Text timerValue;
        [SerializeField] private TMP_Text distanceText;
        [SerializeField] private Image progressFill;

        [Header("Style")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color warningColor = new Color(1f, 0.72f, 0.2f, 1f);
        [SerializeField] private Color dangerColor = new Color(1f, 0.32f, 0.28f, 1f);
        [SerializeField] private float fadeSpeed = 4f;

        private CanvasGroup _group;
        private Transform _player;

        void Awake() { _group = GetComponent<CanvasGroup>(); _group.alpha = 0f; }

        void Update()
        {
            var zm = BRZoneManager.Instance;
            bool visible = zm != null && zm.PhaseCount > 0;
            _group.alpha = Mathf.MoveTowards(_group.alpha, visible ? 1f : 0f, Time.unscaledDeltaTime * fadeSpeed);
            if (!visible) return;

            if (_player == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) _player = p.transform;
            }

            string phase = zm.CurrentPhaseName.ToUpperInvariant();
            if (phaseText != null) phaseText.text = phase;

            float edge = 0f; bool inside = true;
            if (_player != null)
            {
                Vector3 c = zm.CurrentCenter;
                float d = Vector2.Distance(new Vector2(_player.position.x, _player.position.z), new Vector2(c.x, c.z));
                edge = d - zm.CurrentRadius;
                inside = edge <= 0f;
            }

            Color accent; string status;
            if (!inside) { status = "RETURN TO THE SAFE ZONE"; accent = dangerColor; }
            else if (zm.IsShrinking) { status = "ZONE CLOSING IN"; accent = warningColor; }
            else { status = "HOLD THE SAFE ZONE"; accent = normalColor; }

            if (statusText != null) { statusText.text = status; statusText.color = accent; }

            float t = Mathf.Max(0f, zm.TimeRemaining);
            if (timerLabel != null) timerLabel.text = zm.IsShrinking ? "CLOSES IN" : "NEXT ZONE IN";
            if (timerValue != null) timerValue.text = $"{(int)(t / 60f):00}:{(int)(t % 60f):00}";

            if (distanceText != null)
                distanceText.text = inside ? "INSIDE" : $"{Mathf.CeilToInt(edge)} m";

            if (progressFill != null)
            {
                float dur = Mathf.Max(0.01f, zm.PhaseDuration);
                progressFill.fillAmount = Mathf.Clamp01(1f - t / dur);
                progressFill.color = accent;
            }
        }
    }
}
