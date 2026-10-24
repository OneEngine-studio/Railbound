using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// Modular tactical career & lifetime statistics view.
    /// Displays operative dossier, match history, win rates, and accolades.
    /// </summary>
    public class CareerStatsView : MonoBehaviour
    {
        [Header("Transition")]
        [SerializeField] private UIScreenTransition transition;

        [Header("Navigation & Actions")]
        [SerializeField] private Button backButton;

        [Header("Profile & Career Readouts")]
        [SerializeField] private TextMeshProUGUI callsignText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private TextMeshProUGUI winRateText;
        [SerializeField] private TextMeshProUGUI kdRatioText;
        [SerializeField] private TextMeshProUGUI matchesDeployedText;
        [SerializeField] private TextMeshProUGUI streakText;

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
