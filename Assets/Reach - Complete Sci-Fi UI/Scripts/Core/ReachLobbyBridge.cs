using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Michsky.UI.Reach;
using Railbound.Networking;

namespace Michsky.UI.Reach
{
    /// <summary>
    /// Bridges the Reach UI Main Menu / Multiplayer Canvas with Railbound's GameLobby systems.
    /// Handles:
    /// 1. Direct navigation to the Multiplayer lobby screen.
    /// 2. User profile name & level synchronization from AuthenticationService/PlayerProfileService.
    /// 3. Updating Lobby Player cards (Slot 0 for local player, Squad slots, empty slots).
    /// 4. Hooking the Reach UI PLAY button to LobbyMatchmakingController.Deploy().
    /// 5. Hooking Searching Games animated status banner.
    /// 6. Handling Go Back / Exit modal actions.
    /// </summary>
    [DisallowMultipleComponent]
    public class ReachLobbyBridge : MonoBehaviour
    {
        public static ReachLobbyBridge Instance { get; private set; }

        [Header("Reach Core Components")]
        [SerializeField] private PanelManager mainPanelManager;
        [SerializeField] private MenuManager menuManager;

        [Header("Multiplayer Panel References")]
        [SerializeField] private GameObject multiplayerPanel;
        [SerializeField] private ButtonManager playButton;
        [SerializeField] private ButtonManager stopSearchingButton;
        [SerializeField] private ButtonManager goBackHomeButton;
        [SerializeField] private ModeSelector modeSelector;
        [SerializeField] private TextMeshProUGUI lobbyHeaderCount;

        [Header("Top Panels")]
        [SerializeField] private TextMeshProUGUI profileNameText;
        [SerializeField] private UIPopup searchingGamesPopup;
        [SerializeField] private TextMeshProUGUI searchingStatusText;
        [SerializeField] private TextMeshProUGUI searchingTimerText;

        [Header("Lobby Players")]
        [SerializeField] private LobbyPlayer[] lobbySlots;

        [Header("Exit / Modal")]
        [SerializeField] private ModalWindowManager exitModalWindow;

        [Header("Character Preview")]
        [Tooltip("If true, shows the 3D stage character. If false, shows the 2D Reach UI silhouette.")]
        [SerializeField] private bool use3DCharacterStage = false;
        [SerializeField] private GameObject placeholderModel2D;
        [SerializeField] private GameObject lobby3DStage;

        private float searchStartTime;
        private bool isSearching;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            StartCoroutine(InitializeLobbyView());
        }

        private IEnumerator InitializeLobbyView()
        {
            // Give one frame for Reach UI managers to wake up
            yield return null;

            // 1. Force Open Multiplayer Panel
            if (mainPanelManager != null)
            {
                mainPanelManager.OpenPanel("Multiplayer");
            }

            // 2. Setup Player Profile & Name
            SyncPlayerProfile();

            // 3. Setup Lobby Slots (Matching the tactical squad visual)
            SetupLobbySlots();

            // 4. Setup Play / Search Handlers
            SetupButtons();

            // 5. Setup Character Preview Mode
            UpdateCharacterPreviewMode();
        }

        private void Update()
        {
            if (isSearching)
            {
                float elapsed = Time.time - searchStartTime;
                int minutes = Mathf.FloorToInt(elapsed / 60f);
                int seconds = Mathf.FloorToInt(elapsed % 60f);
                if (searchingTimerText != null)
                {
                    searchingTimerText.text = $"{minutes:00}:{seconds:00}";
                }
            }
        }

        public async void SyncPlayerProfile()
        {
            string username = "OPERATOR";
            string levelText = "LVL. 1";

            if (AuthenticationService.Instance != null && AuthenticationService.Instance.Session != null)
            {
                if (!string.IsNullOrEmpty(AuthenticationService.Instance.Session.Username))
                {
                    username = AuthenticationService.Instance.Session.Username;
                }
            }

            try
            {
                if (AuthenticationService.Instance != null && AuthenticationService.Instance.IsAuthenticated)
                {
                    var profile = await PlayerProfileService.LoadAsync();
                    if (profile != null && !string.IsNullOrEmpty(profile.username))
                    {
                        username = profile.username;
                        levelText = profile.experienceLevel.ToString().ToUpper();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"[ReachLobbyBridge] Profile load fallback: {ex.Message}");
            }

            if (profileNameText != null)
            {
                profileNameText.text = username;
            }

            // Update Slot 0 (Local Player)
            if (lobbySlots != null && lobbySlots.Length > 0 && lobbySlots[0] != null)
            {
                lobbySlots[0].SetPlayerName(username);
                lobbySlots[0].SetAdditionalText(levelText);
                lobbySlots[0].SetState(LobbyPlayer.ItemState.Ready);
            }
        }

        private void SetupLobbySlots()
        {
            if (lobbySlots == null || lobbySlots.Length < 4) return;

            // Slot 0: Local Player (Ready)
            if (lobbySlots[0] != null)
            {
                lobbySlots[0].SetState(LobbyPlayer.ItemState.Ready);
            }

            // Slot 1: Squad member 1 (Ready)
            if (lobbySlots[1] != null)
            {
                lobbySlots[1].SetPlayerName("G3T_R3KT");
                lobbySlots[1].SetAdditionalText("LVL. 28");
                lobbySlots[1].SetState(LobbyPlayer.ItemState.Ready);
            }

            // Slot 2: Squad member 2 (Not Ready)
            if (lobbySlots[2] != null)
            {
                lobbySlots[2].SetPlayerName("NOOB_SHENKY");
                lobbySlots[2].SetAdditionalText("LVL. 3");
                lobbySlots[2].SetState(LobbyPlayer.ItemState.NotReady);
            }

            // Slot 3: Empty slot
            if (lobbySlots[3] != null)
            {
                lobbySlots[3].SetState(LobbyPlayer.ItemState.Empty);
            }

            if (lobbyHeaderCount != null)
            {
                lobbyHeaderCount.text = "LOBBY | 3/4";
            }
        }

        private void SetupButtons()
        {
            if (playButton != null)
            {
                playButton.onClick.RemoveListener(HandlePlayClicked);
                playButton.onClick.AddListener(HandlePlayClicked);
            }

            if (stopSearchingButton != null)
            {
                stopSearchingButton.onClick.RemoveListener(HandleStopSearchingClicked);
                stopSearchingButton.onClick.AddListener(HandleStopSearchingClicked);
            }

            if (goBackHomeButton != null)
            {
                goBackHomeButton.onClick.RemoveListener(HandleGoBackClicked);
                goBackHomeButton.onClick.AddListener(HandleGoBackClicked);
            }
        }

        public void HandlePlayClicked()
        {
            Debug.Log("[ReachLobbyBridge] PLAY clicked! Initiating matchmaking deployment...");
            isSearching = true;
            searchStartTime = Time.time;

            if (searchingGamesPopup != null)
            {
                searchingGamesPopup.PlayIn();
            }

            if (searchingStatusText != null)
            {
                searchingStatusText.text = "SEARCHING FOR CONVOY RAID...";
            }

            // Trigger Nakama match creation & Netcode deployment
            if (LobbyMatchmakingController.Instance != null)
            {
                LobbyMatchmakingController.Instance.Deploy();
            }
            else
            {
                Debug.LogWarning("[ReachLobbyBridge] LobbyMatchmakingController singleton not found; searching standalone.");
            }
        }

        public void HandleStopSearchingClicked()
        {
            Debug.Log("[ReachLobbyBridge] Matchmaking cancelled by player.");
            isSearching = false;

            if (searchingGamesPopup != null)
            {
                searchingGamesPopup.PlayOut();
            }
        }

        public void HandleGoBackClicked()
        {
            if (exitModalWindow != null)
            {
                exitModalWindow.OpenWindow();
            }
            else
            {
                Debug.Log("[ReachLobbyBridge] Go Back clicked. Returning to LoginMenu.");
                SceneManager.LoadScene("LoginMenu");
            }
        }

        public void UpdateDeploymentStatus(string status)
        {
            if (searchingStatusText != null)
            {
                searchingStatusText.text = status;
            }
        }

        private void UpdateCharacterPreviewMode()
        {
            if (lobby3DStage == null)
            {
                lobby3DStage = GameObject.Find("LobbyCharacterStage");
            }

            if (use3DCharacterStage)
            {
                if (placeholderModel2D != null) placeholderModel2D.SetActive(false);
                if (lobby3DStage != null) lobby3DStage.SetActive(true);
            }
            else
            {
                if (placeholderModel2D != null) placeholderModel2D.SetActive(true);
                if (lobby3DStage != null) lobby3DStage.SetActive(false);
            }
        }
    }
}
