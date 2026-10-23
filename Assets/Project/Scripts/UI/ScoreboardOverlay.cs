using TMPro;
using UnityEngine;

namespace Railbound.UI
{
    /// <summary>
    /// Tactical in-game scoreboard overlay (held via TAB).
    /// Displays team rosters, K/D/A, Ping, and real-time score counters.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ScoreboardOverlay : MonoBehaviour
    {
        [Header("Canvas Group")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Match Info")]
        [SerializeField] private TMP_Text matchModeText;
        [SerializeField] private TMP_Text mapNameText;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text alphaScoreText;
        [SerializeField] private TMP_Text bravoScoreText;

        [Header("Team Roster Containers")]
        [SerializeField] private Transform alphaRosterContainer;
        [SerializeField] private Transform bravoRosterContainer;

        private void Awake()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            SetVisible(false);
        }

        public void SetVisible(bool visible)
        {
            if (canvasGroup == null) return;
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        public void Show() => SetVisible(true);
        public void Hide() => SetVisible(false);
    }
}
