using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// Modular competitive rankings and seasonal leaderboard view.
    /// Displays competitive brackets, global operative rankings, and RP tiers.
    /// </summary>
    public class RankingsView : MonoBehaviour
    {
        [Header("Transition")]
        [SerializeField] private UIScreenTransition transition;

        [Header("Navigation & Actions")]
        [SerializeField] private Button backButton;

        [Header("Season & Standing Readouts")]
        [SerializeField] private TextMeshProUGUI seasonTitleText;
        [SerializeField] private TextMeshProUGUI playerTierText;
        [SerializeField] private TextMeshProUGUI playerRankPointsText;
        [SerializeField] private TextMeshProUGUI playerRankPositionText;

        public event Action OnBackClicked;

        private void Awake()
        {
            if (backButton != null)
                backButton.onClick.AddListener(() => OnBackClicked?.Invoke());
        }

        public void Show()
        {
            if (transition != null) transition.FadeIn();
            else gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (transition != null) transition.FadeOut();
            else gameObject.SetActive(false);
        }
    }
}
