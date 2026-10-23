using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// Post-match victory/defeat summary screen with stats, XP bar, and podium.
    /// </summary>
    public class GameOverView : MonoBehaviour
    {
        [Header("Transition")]
        [SerializeField] private UIScreenTransition transition;

        [Header("Result Banner")]
        [SerializeField] private TMP_Text resultTitleText;
        [SerializeField] private Image resultBannerGlow;
        [SerializeField] private CanvasGroup bannerCanvasGroup;

        [Header("Match Statistics")]
        [SerializeField] private TMP_Text killsText;
        [SerializeField] private TMP_Text deathsText;
        [SerializeField] private TMP_Text assistsText;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text headshotPctText;

        [Header("XP & Progression")]
        [SerializeField] private TMP_Text xpGainedText;
        [SerializeField] private Slider xpProgressBar;
        [SerializeField] private TMP_Text levelText;

        [Header("MVP Showcase")]
        [SerializeField] private Transform mvpPodiumContainer;

        [Header("Action Buttons")]
        [SerializeField] private Button playAgainButton;
        [SerializeField] private Button returnToLobbyButton;

        public event Action OnPlayAgainClicked;
        public event Action OnReturnToLobbyClicked;

        private void Awake()
        {
            if (playAgainButton != null) playAgainButton.onClick.AddListener(() => OnPlayAgainClicked?.Invoke());
            if (returnToLobbyButton != null) returnToLobbyButton.onClick.AddListener(() => OnReturnToLobbyClicked?.Invoke());
        }

        public void Show(bool isVictory)
        {
            if (resultTitleText != null)
            {
                resultTitleText.text = isVictory ? "VICTORY" : "DEFEAT";
                resultTitleText.color = isVictory ? new Color(1f, 0.48f, 0f) : new Color(0.9f, 0.2f, 0.2f);
            }

            if (resultBannerGlow != null)
            {
                resultBannerGlow.color = isVictory ? new Color(1f, 0.48f, 0f, 0.5f) : new Color(0.9f, 0.2f, 0.2f, 0.5f);
            }

            if (transition != null) transition.FadeIn();
            else gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (transition != null) transition.FadeOut();
            else gameObject.SetActive(false);
        }

        public void SetStats(int kills, int deaths, int assists, int score, float headshotPct, int xpGained, float currentLevelPct, int level)
        {
            if (killsText != null) killsText.text = kills.ToString();
            if (deathsText != null) deathsText.text = deaths.ToString();
            if (assistsText != null) assistsText.text = assists.ToString();
            if (scoreText != null) scoreText.text = score.ToString();
            if (headshotPctText != null) headshotPctText.text = $"{Mathf.RoundToInt(headshotPct)}%";
            if (xpGainedText != null) xpGainedText.text = $"+{xpGained} XP";
            if (xpProgressBar != null) xpProgressBar.value = Mathf.Clamp01(currentLevelPct);
            if (levelText != null) levelText.text = $"LEVEL {level}";
        }
    }
}
