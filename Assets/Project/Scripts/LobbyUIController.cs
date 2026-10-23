using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Railbound.UI.Anim;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Railbound.UI
{
    /// <summary>
    /// Battle-royale multiplayer lobby controller.
    /// Wires every UI element of the Lobby scene (see LobbyUIHierarchy.md for the
    /// recommended RectTransform layout) and exposes data-driven refresh methods.
    /// Interactive elements animate through Railbound.UI.Anim (AnimatedButton,
    /// IconAnimator, Notifications) and RailboundButtonEffects.
    ///
    /// Scene: Assets/Project/GameLobby.unity
    /// Canvas: 1920x1080 reference, Scale With Screen Size, Match Width Or Height (0.5)
    /// </summary>
    [DisallowMultipleComponent]
    public class LobbyUIController : MonoBehaviour
    {
        // ------------------------------------------------------------------  data models

        [Serializable]
        public class QuestEntry
        {
            public string title;
            public int current;
            public int target;
            public string rewardLabel;   // e.g. "500 XP"
        }

        [Serializable]
        public class ChallengerEntry
        {
            public string name;
            public Sprite portrait;
        }

        // ------------------------------------------------------------------  1. Top navigation bar

        [Header("1. Top Navigation Bar")]
        [SerializeField] private RectTransform topBar;                 // Top-Stretch
        [SerializeField] private Image logoBadge;                     // "BASIN" badge
        [SerializeField] private TextMeshProUGUI logoText;

        [SerializeField] private RectTransform navTabsContainer;       // HorizontalLayoutGroup
        [SerializeField] private NavTab[] navTabs;                   // Lobby, Seasons, Challengers, Equipment, Shop

        [Serializable]
        public class NavTab
        {
            public string label;
            public Button button;
            public TextMeshProUGUI labelText;
            public Image activeIndicator;   // red bottom border, visible only when active
            public Image hoverGlow;
        }

        [SerializeField] private RectTransform utilityIcons;           // HorizontalLayoutGroup (right)
        [SerializeField] private TextMeshProUGUI friendsCountText;    // "5"
        [SerializeField] private Button friendsButton;
        [SerializeField] private Button notificationButton;
        [SerializeField] private Button mailButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button powerButton;

        [SerializeField] private Image currencyIcon;                  // green gemstone
        [SerializeField] private TextMeshProUGUI currencyText;        // "2350"

        // ------------------------------------------------------------------  2. Center viewport & party

        [Header("2. Center Character Viewport & Party Slots")]
        [SerializeField] private RectTransform characterViewport;      // center-background 3D viewport
        [SerializeField] private RectTransform playerTag;              // floating tag above model
        [SerializeField] private Image levelBadge;                    // "32"
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private TextMeshProUGUI usernameText;        // "wtxinc"
        [SerializeField] private TextMeshProUGUI statsSubtitleText;   // "Show statistics"

        [SerializeField] private Button partySlotLeft;                // "+" left of model
        [SerializeField] private Button partySlotRight;               // "+" right of model
        [SerializeField] private Image partySlotLeftFill;
        [SerializeField] private Image partySlotRightFill;

        // ------------------------------------------------------------------  3. Left panel - season & quests

        [Header("3. Left Panel - Season Progress & Quests")]
        [SerializeField] private RectTransform leftPanel;              // Left-Stretch
        [SerializeField] private TextMeshProUGUI seasonTitleText;     // "SEASON 2"
        [SerializeField] private TextMeshProUGUI weekSubtitleText;     // "WEEK 3" (red highlight block)
        [SerializeField] private TextMeshProUGUI xpCounterText;       // "600 / 1000"
        [SerializeField] private Image xpFillImage;                   // progress bar fill
        [SerializeField] private Button checkAwardsButton;

        [SerializeField] private RectTransform questScrollView;        // ScrollRect viewport
        [SerializeField] private RectTransform questContent;           // VerticalLayoutGroup content
        [SerializeField] private GameObject questItemPrefab;          // row: title, fraction, reward badge

        [SerializeField] private List<QuestEntry> quests = new List<QuestEntry>
        {
            new QuestEntry { title = "Play the following number of games", current = 2,  target = 10,   rewardLabel = "500 XP" },
            new QuestEntry { title = "Deal 1500 damage",                 current = 314, target = 1500, rewardLabel = "250 XP" },
            new QuestEntry { title = "Kill or assist in killing 5 enemies", current = 3, target = 5,   rewardLabel = "250 XP" },
            new QuestEntry { title = "Revive 15 team members",           current = 0,  target = 15,   rewardLabel = "750 XP" },
            new QuestEntry { title = "Shotgun eliminations",             current = 2,  target = 10,   rewardLabel = "500 XP" },
            new QuestEntry { title = "Sniper rifle eliminations",        current = 0,  target = 10,   rewardLabel = "500 XP" },
        };

        // ------------------------------------------------------------------  4. Bottom-right controls

        [Header("4. Bottom-Right Controls & Matchmaking")]
        [SerializeField] private RectTransform bottomRightPanel;      // Bottom-Right anchor
        [SerializeField] private RectTransform challengerCard;        // "New Challenger" preview
        [SerializeField] private TextMeshProUGUI challengerTitleText; // "New Challenger"
        [SerializeField] private TextMeshProUGUI challengerNameText;  // "Luna"
        [SerializeField] private Image challengerPortrait;

        [SerializeField] private Button readyButton;                  // big gold READY
        [SerializeField] private Image readyGlow;
        [SerializeField] private TextMeshProUGUI readyText;

        [SerializeField] private TextMeshProUGUI gameModeText;        // "Game Mode : Solo"
        [SerializeField] private Button gameModeSettingsButton;       // small gear icon

        // ------------------------------------------------------------------  5. Cyberpunk palette

        [Header("5. Battle Royale Palette")]
        [SerializeField] private Color goldAccent  = new Color(1.00f, 0.75f, 0.20f);  // READY / active tab / progress
        [SerializeField] private Color neonRed    = new Color(1.00f, 0.10f, 0.15f);  // WEEK 3 / alerts
        [SerializeField] private Color neonGreen  = new Color(0.10f, 1.00f, 0.45f);  // currency gem
        [SerializeField] private Color neonCyan   = new Color(0.00f, 0.90f, 1.00f);  // secondary glow
        [SerializeField] private Color darkPanel  = new Color(0.04f, 0.05f, 0.09f, 0.85f);
        [SerializeField] private Color darkPanelSolid = new Color(0.04f, 0.05f, 0.09f, 1f);

        // ------------------------------------------------------------------  6. Tactical Audio Clips

        [Header("Tactical UI Audio (Sci-Fi Pack)")]
        [SerializeField] private AudioClip selectTabSfx;
        [SerializeField] private AudioClip deploySfx;
        [SerializeField] private AudioClip openModalSfx;
        [SerializeField] private AudioClip hoverSfx;
        [SerializeField] private AudioClip closeModalSfx;

        // ------------------------------------------------------------------  7. Events

        [Header("7. Events")]
        [SerializeField] private UnityEvent onReadyClicked;
        [SerializeField] private UnityEvent onCheckAwardsClicked;
        [SerializeField] private UnityEvent onPartyInviteClicked;      // param: -1 left, +1 right
        [SerializeField] private UnityEvent<string> onNavTabClicked;

        // ------------------------------------------------------------------  Extra Left Nav & Chat Refs

        private Button _strikePassButton;
        private Button _mallButton;
        private Button _eventButton;
        private Button _katanaPromoButton;
        private Button _surveyBannerButton;
        private Button _playerTagButton;
        private Button _goldCurrencyButton;
        private Button _challengerCardButton;

        private GameObject _chatDrawer;
        private GameObject ChatDrawerObject
        {
            get
            {
                if (_chatDrawer == null)
                {
                    var bottomNav = transform.Find("Panel_BottomLeftNav");
                    if (bottomNav != null)
                    {
                        var drawerTr = bottomNav.Find("Panel_GlobalChatDrawer");
                        if (drawerTr != null) _chatDrawer = drawerTr.gameObject;
                    }
                }
                return _chatDrawer;
            }
        }
        private Button _chatTickerButton;
        private Button _chatCloseButton;

        // ------------------------------------------------------------------  runtime state

        private int _activeTabIndex = 0;
        private int _currency = 1250;
        private int _friendsOnline = 5;
        private int _level = 12;
        private string _username = "Spectre_07";
        private int _xpCurrent = 1450;
        private int _xpTarget = 2000;
        private ChallengerEntry _challenger = new ChallengerEntry { name = "APEX EXPRESS" };

        public int ActiveTabIndex => _activeTabIndex;
        public string Username => _username;
        public int Currency => _currency;

        // ------------------------------------------------------------------  lifecycle

        private void Awake()
        {
            EnsureAudioClips();
            AutoDiscoverReferences();
            WireEvents();
            ConfigureMicroInteractionsAndAudio();
            ApplyPalette();
            LoadPersistedPlayerState();
        }

        private void Start()
        {
            RefreshAll();
            EnsureUIManagerAvailable();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                HandleEscapeKey();
            }
        }

        // ------------------------------------------------------------------  Audio & Discovery

        private void EnsureAudioClips()
        {
#if UNITY_EDITOR
            if (selectTabSfx == null)
                selectTabSfx = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Free UI Click Sound Effects Pack/AUDIO/Sci-Fi/SFX_UI_Click_Designed_Scifi_Thin_Select_1.wav");
            if (deploySfx == null)
                deploySfx = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Free UI Click Sound Effects Pack/AUDIO/Sci-Fi/SFX_UI_Click_Designed_Scifi_Flangy_Thick_Generic_1.wav");
            if (openModalSfx == null)
                openModalSfx = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Free UI Click Sound Effects Pack/AUDIO/Sci-Fi/SFX_UI_Click_Designed_Scifi_Movement_Open_1.wav");
            if (hoverSfx == null)
                hoverSfx = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Free UI Click Sound Effects Pack/AUDIO/Sci-Fi/SFX_UI_Click_Designed_Scifi_Digital_Generic_1.wav");
            if (closeModalSfx == null)
                closeModalSfx = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Free UI Click Sound Effects Pack/AUDIO/Sci-Fi/SFX_UI_Click_Designed_Scifi_Thick_Negative_Close_1.wav");
#endif
        }

        private void AutoDiscoverReferences()
        {
            // Auto discover Left Column buttons
            if (leftPanel != null)
            {
                _strikePassButton = leftPanel.Find("Button_StrikePass")?.GetComponent<Button>();
                _mallButton = leftPanel.Find("Button_Mall")?.GetComponent<Button>();
                _eventButton = leftPanel.Find("Button_Event")?.GetComponent<Button>();
                _katanaPromoButton = leftPanel.Find("Button_KatanaPromo")?.GetComponent<Button>();
                _surveyBannerButton = leftPanel.Find("Button_SurveyBanner")?.GetComponent<Button>();
            }

            // Top Bar
            if (playerTag != null)
            {
                _playerTagButton = playerTag.GetComponent<Button>();
                if (_playerTagButton == null)
                {
                    _playerTagButton = playerTag.gameObject.AddComponent<Button>();
                    _playerTagButton.transition = Selectable.Transition.None;
                }
            }

            if (topBar != null)
            {
                var goldPanel = topBar.Find("Panel_GoldCurrency");
                if (goldPanel != null)
                {
                    _goldCurrencyButton = goldPanel.GetComponent<Button>();
                    if (_goldCurrencyButton == null)
                    {
                        _goldCurrencyButton = goldPanel.gameObject.AddComponent<Button>();
                        _goldCurrencyButton.transition = Selectable.Transition.None;
                    }
                }
            }

            // Bottom Right Map Card
            if (challengerCard != null)
            {
                _challengerCardButton = challengerCard.GetComponent<Button>();
                if (_challengerCardButton == null)
                {
                    _challengerCardButton = challengerCard.gameObject.AddComponent<Button>();
                    _challengerCardButton.transition = Selectable.Transition.None;
                }
            }

            // Chat Drawer & Ticker
            var bottomNav = transform.Find("Panel_BottomLeftNav");
            if (bottomNav != null)
            {
                var drawerTr = bottomNav.Find("Panel_GlobalChatDrawer");
                if (drawerTr != null)
                {
                    _chatDrawer = drawerTr.gameObject;
                    _chatCloseButton = drawerTr.Find("Header_Bar/Button_Close")?.GetComponent<Button>();
                }
                _chatTickerButton = bottomNav.Find("Panel_ChatTicker")?.GetComponent<Button>();
            }

            // Ensure GraphicRaycaster
            var raycaster = GetComponent<GraphicRaycaster>();
            if (raycaster == null) gameObject.AddComponent<GraphicRaycaster>();
            raycaster.enabled = true;
        }

        private void ConfigureMicroInteractionsAndAudio()
        {
            // Wire all Button components in this Canvas with RailboundButtonEffects
            var allButtons = GetComponentsInChildren<Button>(true);
            foreach (var btn in allButtons)
            {
                if (btn == null) continue;

                var effects = btn.GetComponent<RailboundButtonEffects>();
                if (effects == null)
                {
                    effects = btn.gameObject.AddComponent<RailboundButtonEffects>();
                }

                // Assign specific tactical audio clips
                if (btn == readyButton)
                {
                    effects.SetAudioClips(deploySfx, hoverSfx);
                }
                else if (IsTabButton(btn))
                {
                    effects.SetAudioClips(selectTabSfx, hoverSfx);
                }
                else if (btn == settingsButton || btn == _strikePassButton || btn == _mallButton || btn == _eventButton ||
                         btn == _katanaPromoButton || btn == _surveyBannerButton || btn == _playerTagButton ||
                         btn == _goldCurrencyButton || btn == _challengerCardButton || btn == gameModeSettingsButton)
                {
                    effects.SetAudioClips(openModalSfx, hoverSfx);
                }
                else if (btn == _chatCloseButton || btn == powerButton)
                {
                    effects.SetAudioClips(closeModalSfx, hoverSfx);
                }
                else
                {
                    effects.SetAudioClips(selectTabSfx, hoverSfx);
                }
            }
        }

        private bool IsTabButton(Button btn)
        {
            if (navTabs == null) return false;
            foreach (var tab in navTabs)
            {
                if (tab != null && tab.button == btn) return true;
            }
            return false;
        }

        private void EnsureUIManagerAvailable()
        {
            if (UIManager.Instance == null)
            {
                Debug.Log("[LobbyUI] UIManager auto-bootstrapped via UIManager.Instance");
            }
        }

        private void LoadPersistedPlayerState()
        {
            string savedName = PlayerPrefs.GetString("PlayerName", string.Empty);
            if (!string.IsNullOrEmpty(savedName))
            {
                _username = savedName;
            }
        }

        // ------------------------------------------------------------------  wiring

        private void WireEvents()
        {
            if (readyButton != null)
                readyButton.onClick.AddListener(HandleReadyClicked);
            if (checkAwardsButton != null)
                checkAwardsButton.onClick.AddListener(HandleTaskClicked);
            if (partySlotLeft != null)
                partySlotLeft.onClick.AddListener(() => HandlePartyInvite(-1));
            if (partySlotRight != null)
                partySlotRight.onClick.AddListener(() => HandlePartyInvite(1));
            if (gameModeSettingsButton != null)
                gameModeSettingsButton.onClick.AddListener(HandleGameModeSettings);
            if (powerButton != null)
                powerButton.onClick.AddListener(HandlePower);
            if (settingsButton != null)
                settingsButton.onClick.AddListener(HandleSettings);
            if (mailButton != null)
                mailButton.onClick.AddListener(HandleMail);
            if (notificationButton != null)
                notificationButton.onClick.AddListener(HandleNotifications);
            if (friendsButton != null)
                friendsButton.onClick.AddListener(HandleFriends);

            // Left Navigation
            if (_strikePassButton != null)
                _strikePassButton.onClick.AddListener(HandleStrikePassClicked);
            if (_mallButton != null)
                _mallButton.onClick.AddListener(HandleMallClicked);
            if (_eventButton != null)
                _eventButton.onClick.AddListener(HandleEventClicked);
            if (_katanaPromoButton != null)
                _katanaPromoButton.onClick.AddListener(HandleKatanaPromoClicked);
            if (_surveyBannerButton != null)
                _surveyBannerButton.onClick.AddListener(HandleSurveyBannerClicked);

            // Top Bar
            if (_playerTagButton != null)
                _playerTagButton.onClick.AddListener(HandlePlayerTagClicked);
            if (_goldCurrencyButton != null)
                _goldCurrencyButton.onClick.AddListener(HandleGoldCurrencyClicked);

            // Challenger / Map Card
            if (_challengerCardButton != null)
                _challengerCardButton.onClick.AddListener(HandleGameModeSettings);

            // Chat Ticker & Drawer
            if (_chatTickerButton != null)
                _chatTickerButton.onClick.AddListener(ToggleGlobalChat);
            if (_chatCloseButton != null)
                _chatCloseButton.onClick.AddListener(CloseGlobalChat);

            // Bottom Navigation Tabs
            for (int i = 0; i < navTabs.Length; i++)
            {
                int index = i; // capture for closure
                if (navTabs[i].button != null)
                    navTabs[i].button.onClick.AddListener(() => SetActiveTab(index));
            }
        }

        // ------------------------------------------------------------------  refresh

        public void RefreshAll()
        {
            RefreshTopBar();
            RefreshPlayerTag();
            RefreshSeasonPanel();
            RefreshQuests();
            RefreshChallengerCard();
            RefreshReadyButton();
        }

        public void RefreshTopBar()
        {
            if (logoText != null) logoText.text = "RAILBOUND // v0.4.2";
            if (friendsCountText != null) friendsCountText.text = _friendsOnline.ToString();
            if (currencyText != null) currencyText.text = _currency.ToString("N0");
            SetActiveTabVisuals(_activeTabIndex);
        }

        public void RefreshPlayerTag()
        {
            if (levelText != null) levelText.text = $"Lv.{Mathf.Max(1, _level)}";
            if (usernameText != null) usernameText.text = _username;
            if (statsSubtitleText != null) statsSubtitleText.text = "SQUAD BOUNTY";
        }

        public void RefreshSeasonPanel()
        {
            if (seasonTitleText != null) seasonTitleText.text = "RAIL PASS";
            if (weekSubtitleText != null) weekSubtitleText.text = "GOLD II";
            if (xpCounterText != null) xpCounterText.text = $"{_xpCurrent:N0} / {_xpTarget:N0} RP";
            if (xpFillImage != null) xpFillImage.fillAmount = Mathf.Clamp01((float)_xpCurrent / Mathf.Max(1, _xpTarget));
        }

        public void RefreshQuests()
        {
            if (questContent == null || questItemPrefab == null) return;

            // clear existing rows
            for (int i = questContent.childCount - 1; i >= 0; i--)
                Destroy(questContent.GetChild(i).gameObject);

            foreach (var quest in quests)
            {
                GameObject row = Instantiate(questItemPrefab, questContent);
                var texts = row.GetComponentsInChildren<TextMeshProUGUI>();
                if (texts.Length >= 3)
                {
                    texts[0].text = quest.title;
                    texts[1].text = $"{quest.current} / {quest.target}";
                    texts[2].text = quest.rewardLabel;
                }
                var fill = row.GetComponentInChildren<Image>(true);
                if (fill != null && fill.type == Image.Type.Filled)
                    fill.fillAmount = Mathf.Clamp01((float)quest.current / Mathf.Max(1, quest.target));
            }
        }

        public void RefreshChallengerCard()
        {
            if (challengerTitleText != null) challengerTitleText.text = "SEASON 01";
            if (challengerNameText != null) challengerNameText.text = _challenger.name;
            if (challengerPortrait != null && _challenger.portrait != null)
                challengerPortrait.sprite = _challenger.portrait;
        }

        public void RefreshReadyButton()
        {
            if (readyText != null) readyText.text = "DEPLOY";
            if (gameModeText != null) gameModeText.text = "CONVOY RAID";
        }

        // ------------------------------------------------------------------  interactions

        public void SetActiveTab(int index)
        {
            if (navTabs == null || navTabs.Length == 0) return;
            _activeTabIndex = Mathf.Clamp(index, 0, navTabs.Length - 1);
            SetActiveTabVisuals(_activeTabIndex);

            string tabName = navTabs[_activeTabIndex].label;
            onNavTabClicked?.Invoke(tabName);

            // Handle sub-screen switching based on tab
            switch (_activeTabIndex)
            {
                case 0: // OPERATORS
                    if (UIManager.Instance != null && UIManager.Instance.CurrentScreen != ScreenType.None)
                    {
                        UIManager.Instance.CloseCurrentScreen();
                    }
                    break;
                case 1: // ARSENAL (LoadoutView)
                    OpenArsenal();
                    break;
                case 2: // LOCKER
                    ShowLockerOverview();
                    break;
                case 3: // RANKINGS
                    ShowLeaderboardOverview();
                    break;
                case 4: // CAREER
                    ShowCareerOverview();
                    break;
            }
        }

        private void SetActiveTabVisuals(int activeIndex)
        {
            if (navTabs == null || navTabs.Length == 0) return;
            for (int i = 0; i < navTabs.Length; i++)
            {
                bool active = i == activeIndex;
                if (navTabs[i].activeIndicator != null)
                    navTabs[i].activeIndicator.gameObject.SetActive(active);
                if (navTabs[i].hoverGlow != null)
                    navTabs[i].hoverGlow.gameObject.SetActive(false);
                if (navTabs[i].labelText != null)
                    navTabs[i].labelText.color = active ? Color.white : new Color(0.84f, 0.88f, 0.92f, 0.88f);
                if (navTabs[i].button != null)
                {
                    var bg = navTabs[i].button.GetComponent<Image>();
                    if (bg != null)
                        bg.color = active ? new Color(0.14f, 0.16f, 0.21f, 0.72f) : new Color(0.08f, 0.10f, 0.14f, 0.45f);
                    var animated = navTabs[i].button.GetComponent<AnimatedButton>();
                    if (animated != null)
                        animated.SetSelected(active);
                }
            }
        }

        public void OpenArsenal()
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.OpenScreen(ScreenType.Loadout);
            }
            else
            {
                Debug.Log("[LobbyUI] Open Arsenal (Loadout)");
            }
        }

        public void OpenMatchmaking()
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.OpenScreen(ScreenType.Matchmaking);
            }
            else
            {
                Debug.Log("[LobbyUI] Open Matchmaking Screen");
            }
        }

        private void HandleReadyClicked()
        {
            Debug.Log("[LobbyUI] DEPLOY clicked - initiating match and scene transition to MAP-1");
            if (readyText != null) readyText.text = "DEPLOYING...";
            onReadyClicked?.Invoke();

            if (UIManager.Instance != null)
            {
                UIManager.Instance.OpenScreen(ScreenType.Matchmaking);
            }

            var matchmaker = FindFirstObjectByType<Railbound.Networking.LobbyMatchmakingController>();
            if (matchmaker == null)
            {
                var go = new GameObject("LobbyMatchmakingController");
                matchmaker = go.AddComponent<Railbound.Networking.LobbyMatchmakingController>();
            }

            matchmaker.Deploy();
        }

        private void HandleTaskClicked()
        {
            if (questScrollView != null)
            {
                bool next = !questScrollView.gameObject.activeSelf;
                questScrollView.gameObject.SetActive(next);
            }
            onCheckAwardsClicked?.Invoke();
        }

        private void HandlePartyInvite(int side)
        {
            Debug.Log($"[LobbyUI] Party invite slot {(side < 0 ? "left" : "right")}");
            onPartyInviteClicked?.Invoke();
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowConfirmation("SQUAD INVITATION", $"Dispatch combat invite to operative slot {(side < 0 ? "#1" : "#2")}?", null);
            }
        }

        private void HandleGameModeSettings()
        {
            OpenMatchmaking();
        }

        private void HandlePower()
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowConfirmation("CONFIRM EXIT", "Terminate session and exit Railbound?", () =>
                {
#if UNITY_EDITOR
                    EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                });
            }
            else
            {
                Debug.Log("[LobbyUI] Power / exit");
            }
        }

        private void HandleSettings()
        {
            if (UIManager.Instance != null)
                UIManager.Instance.ShowSettings();
            else
                Debug.Log("[LobbyUI] Settings");
        }

        private void HandleMail()
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowConfirmation("TACTICAL COMMS // INBOX", "Inbox: 2 Unread Transmissions\n\n[HQ Forward Ops] Convoy Bounty Claim Approved (+500 RP)\n[Sector Relay] Iron Canyon weapon caches restocked for Season 01.", null);
            }
            else
            {
                Debug.Log("[LobbyUI] Mail");
            }
        }

        private void HandleNotifications()
        {
            Debug.Log("[LobbyUI] Notifications");
            var canvas = topBar != null ? topBar.GetComponentInParent<Canvas>() : GetComponentInParent<Canvas>();
            Notifications.ShowCountdown(canvas, "CONVOY RAID ACTIVE", 10);
        }

        private void HandleFriends()
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowConfirmation("SQUAD COMS // OPERATIVES", $"{_friendsOnline} operatives currently online in Sector 4.\n\n[● LIVE] Vanguard_01 (Convoy Raid)\n[● LIVE] IronClad (Hangar)\n[● LIVE] Ghost_Sniper (Iron Canyon)\n\nBroadcast squad invite to all active operatives?", null);
            }
            else
            {
                Debug.Log("[LobbyUI] Friends");
            }
        }

        private void HandlePlayerTagClicked()
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowConfirmation("OPERATOR DOSSIER // PROFILE",
                    $"CALLSIGN: {_username}\nLEVEL: Lv.{_level}\nRANK TIER: GOLD II\nRP PROGRESS: {_xpCurrent} / {_xpTarget} RP\nSQUAD STATUS: ACTIVE READY\n\nSEASON 01 COMBAT RATING: 1,420 ELO", null);
            }
        }

        private void HandleGoldCurrencyClicked()
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowConfirmation("DEPOT STORE // CURRENCY",
                    $"Current Balance: {_currency:N0} Credits\n\nAcquire +500 Tactical Credits for 500 Gold Coins?",
                    () =>
                    {
                        SetCurrency(_currency + 500);
                    });
            }
        }

        private void HandleStrikePassClicked()
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowLevelUp(_level, "MAG-PULSE RIFLE MK.II", "RAIL PASS // TIER " + _level);
            }
        }

        private void HandleMallClicked()
        {
            OpenArsenal();
        }

        private void HandleEventClicked()
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowConfirmation("SEASON 01 // ACTIVE EVENTS",
                    "CONVOY INVASION // LIVE EVENT\n\n- Boost: Double RP earned in Iron Canyon operations\n- Special Objective: Intercept armored train car\n- Event Window: 03d 14h remaining", null);
            }
        }

        private void HandleKatanaPromoClicked()
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowConfirmation("TACTICAL CONTRACT // BOUNTY",
                    "HIGH-VALUE TARGET: CONVOY ALPHA\n\n- Destroy escort vehicles: (2/5)\n- Recover titanium crate: (0/1)\n- Contract Reward: 750 RP + Prototype Blueprint", null);
            }
        }

        private void HandleSurveyBannerClicked()
        {
            OpenMatchmaking();
        }

        private void ShowLockerOverview()
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowConfirmation("OPERATOR LOCKER",
                    $"TACTICAL VAULT & CUSTOMIZATION\n\nActive Callsign: {_username}\nEquipped Operator: UNIT 01 // RANGER\nActive Camo: Ash Shadow\nAccessory: Tactical Balaclava\nCustomization slot unlocks at Level 15.", null);
            }
        }

        private void ShowLeaderboardOverview()
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowConfirmation("GLOBAL LEADERBOARD // TOP OPERATIVES",
                    "1. [APEX] Titan_V - 4,820 RP\n2. [RAID] Vector_09 - 4,650 RP\n3. [KNT] Shadow_01 - 4,410 RP\n4. [VNG] Spectre_99 - 4,120 RP\n...\nYour Rank: #142 (Gold II - 1,450 RP)", null);
            }
        }

        private void ShowCareerOverview()
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowConfirmation("OPERATOR CAREER RECORD",
                    $"Callsign: {_username}\nMatches Deployed: 48\nVictories: 14\nWin Rate: 29.2%\nK/D Ratio: 2.84\nLongest Convoy Streak: 5 Wins", null);
            }
        }

        public void ToggleGlobalChat()
        {
            var chatCtrl = GetComponentInChildren<LobbyGlobalChatController>(true);
            if (chatCtrl != null)
            {
                chatCtrl.ToggleChatDrawer();
            }
            else if (ChatDrawerObject != null)
            {
                bool active = !ChatDrawerObject.activeSelf;
                ChatDrawerObject.SetActive(active);
            }
        }

        public void CloseGlobalChat()
        {
            var chatCtrl = GetComponentInChildren<LobbyGlobalChatController>(true);
            if (chatCtrl != null)
            {
                chatCtrl.CloseChatDrawer();
            }
            if (ChatDrawerObject != null)
            {
                ChatDrawerObject.SetActive(false);
            }
        }

        public void HandleEscapeKey()
        {
            // 1. Close chat drawer if open
            if (ChatDrawerObject != null && ChatDrawerObject.activeSelf)
            {
                CloseGlobalChat();
                return;
            }

            // 2. Delegate to UIManager for modals and sub-screens
            if (UIManager.Instance != null)
            {
                if (UIManager.Instance.IsAnyModalOpen)
                {
                    UIManager.Instance.HandleEscape();
                    return;
                }

                if (UIManager.Instance.CurrentScreen == ScreenType.Loadout ||
                    UIManager.Instance.CurrentScreen == ScreenType.Matchmaking)
                {
                    UIManager.Instance.GoBack();
                    SetActiveTab(0); // return to Operators tab
                    return;
                }
            }

            // 3. Return to Operators tab if another tab was active
            if (_activeTabIndex != 0)
            {
                SetActiveTab(0);
                return;
            }

            // 4. In main lobby, show exit confirmation
            HandlePower();
        }

        // ------------------------------------------------------------------  palette

        private void ApplyPalette()
        {
            if (readyGlow != null) readyGlow.color = new Color(goldAccent.r, goldAccent.g, goldAccent.b, 0.35f);
            if (xpFillImage != null) xpFillImage.color = goldAccent;
        }

        // ------------------------------------------------------------------  public API for backend binding

        public void SetCurrency(int value) { _currency = value; RefreshTopBar(); }
        public void SetFriendsOnline(int count) { _friendsOnline = count; RefreshTopBar(); }
        public void SetPlayerInfo(int level, string username) { if (level > 0) _level = level; if (!string.IsNullOrEmpty(username)) _username = username; RefreshPlayerTag(); }
        public void SetXp(int current, int target) { _xpCurrent = current; _xpTarget = target; RefreshSeasonPanel(); }
        public void SetQuests(IList<QuestEntry> entries) { quests.Clear(); quests.AddRange(entries); RefreshQuests(); }
        public void SetChallenger(ChallengerEntry entry) { _challenger = entry; RefreshChallengerCard(); }
    }
}
