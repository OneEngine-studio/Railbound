using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// Tactical matchmaking and searching overlay screen.
    /// Features animated radar scanning circle, live elapsed timer, ping/region readout, and squad rosters.
    /// </summary>
    public class MatchmakingView : MonoBehaviour
    {
        [Header("Transition")]
        [SerializeField] private UIScreenTransition transition;

        [Header("Header / Profile")]
        [SerializeField] private TMP_Text playerNameText;
        [SerializeField] private TMP_Text playerLevelText;
        [SerializeField] private Image playerAvatarImage;
        [SerializeField] private TMP_Text pingText;
        [SerializeField] private TMP_Text regionText;
        [SerializeField] private Button backButton;

        [Header("Radar Hub & Search Status")]
        [SerializeField] private RectTransform radarReticle;
        [SerializeField] private TMP_Text searchStatusText;
        [SerializeField] private TMP_Text elapsedTimeText;
        [SerializeField] private Button cancelMatchmakingButton;

        [Header("Team Rosters")]
        [SerializeField] private Transform teamAlphaRosterContainer;
        [SerializeField] private Transform teamBravoRosterContainer;
        [SerializeField] private TMP_Text teamAlphaCountText;
        [SerializeField] private TMP_Text teamBravoCountText;

        [Header("Map & Mode Selection")]
        [SerializeField] private Image mapThumbnailImage;
        [SerializeField] private TMP_Text mapNameText;
        [SerializeField] private TMP_Dropdown modeDropdown;
        [SerializeField] private TMP_Text countdownTimerText;

        [Header("Action Button")]
        [SerializeField] private Button readyButton;
        [SerializeField] private TMP_Text readyButtonText;
        [SerializeField] private Image readyPulseAccent;

        public event Action OnBackClicked;
        public event Action OnReadyClicked;
        public event Action<int> OnModeChanged;

        private bool _isReady = false;
        private float _elapsedSearchTime = 0f;
        private bool _isSearching = true;

        private void Awake()
        {
            if (backButton != null) backButton.onClick.AddListener(() => OnBackClicked?.Invoke());
            if (cancelMatchmakingButton != null) cancelMatchmakingButton.onClick.AddListener(() => OnBackClicked?.Invoke());
            if (readyButton != null) readyButton.onClick.AddListener(ToggleReady);
            if (modeDropdown != null) modeDropdown.onValueChanged.AddListener(mode => OnModeChanged?.Invoke(mode));
        }

        private void Update()
        {
            if (radarReticle != null && gameObject.activeInHierarchy)
            {
                radarReticle.Rotate(0f, 0f, -60f * Time.deltaTime);
            }

            if (_isSearching && gameObject.activeInHierarchy)
            {
                _elapsedSearchTime += Time.deltaTime;
                if (elapsedTimeText != null)
                {
                    int mins = (int)(_elapsedSearchTime / 60f);
                    int secs = (int)(_elapsedSearchTime % 60f);
                    elapsedTimeText.text = $"{mins:00}:{secs:00}";
                }
            }
        }

        public void Show()
        {
            _elapsedSearchTime = 0f;
            _isSearching = true;
            if (searchStatusText != null) searchStatusText.text = "SEARCHING FOR OPERATORS...";
            if (transition != null) transition.FadeIn();
            else gameObject.SetActive(true);
        }

        public void Hide()
        {
            _isSearching = false;
            if (transition != null) transition.FadeOut();
            else gameObject.SetActive(false);
        }

        public void ToggleReady()
        {
            _isReady = !_isReady;
            if (readyButtonText != null)
                readyButtonText.text = _isReady ? "CANCEL READY" : "DEPLOY / READY";
            OnReadyClicked?.Invoke();
        }

        public void SetPing(int pingMs, string region)
        {
            if (pingText != null) pingText.text = $"{pingMs} ms";
            if (regionText != null) regionText.text = region;
        }

        public void SetCountdown(int seconds)
        {
            if (countdownTimerText != null)
                countdownTimerText.text = seconds > 0 ? $"STARTING IN {seconds}s" : "MATCH READY";
        }
    }
}
