using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Railbound.UI.Anim;

namespace Railbound.UI
{
    /// <summary>
    /// Battle-royale multiplayer lobby controller.
    /// Wires every UI element of the Lobby scene (see LobbyUIHierarchy.md for the
    /// recommended RectTransform layout) and exposes data-driven refresh methods.
    /// Interactive elements animate through Railbound.UI.Anim (AnimatedButton,
    /// IconAnimator, Notifications).
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

        // ------------------------------------------------------------------  6. Events

        [Header("6. Events")]
        [SerializeField] private UnityEvent onReadyClicked;
        [SerializeField] private UnityEvent onCheckAwardsClicked;
        [SerializeField] private UnityEvent onPartyInviteClicked;      // param: -1 left, +1 right
        [SerializeField] private UnityEvent<string> onNavTabClicked;

        // ------------------------------------------------------------------  runtime state

        private int _activeTabIndex;
        private int _currency = 1250;
        private int _friendsOnline = 5;
        private int _level = 12;
        private string _username = "EstheAda5813";
        private int _xpCurrent = 1450;
        private int _xpTarget = 2000;
        private ChallengerEntry _challenger = new ChallengerEntry { name = "APEX EXPRESS" };

        // ------------------------------------------------------------------  lifecycle

        private void Awake()
        {
            WireEvents();
            ApplyPalette();
        }

        private void Start()
        {
            RefreshAll();
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
            SetActiveTab(_activeTabIndex);
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
            for (int i = 0; i < navTabs.Length; i++)
            {
                bool active = i == _activeTabIndex;
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
                        animated.SetSelected(false);
                }
            }
            onNavTabClicked?.Invoke(navTabs[_activeTabIndex].label);
        }

        private void HandleReadyClicked()
        {
            // TODO: call match-orchestrator ticket API via game-api BFF
            Debug.Log("[LobbyUI] PLAY clicked - request match ticket");
            onReadyClicked?.Invoke();
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
            // TODO: open friend picker / send invite through Nakama socket
            Debug.Log($"[LobbyUI] Party invite slot {(side < 0 ? "left" : "right")}");
            onPartyInviteClicked?.Invoke();
        }

        private void HandleGameModeSettings() => Debug.Log("[LobbyUI] Game mode settings");
        private void HandlePower() => Debug.Log("[LobbyUI] Power / exit");
        private void HandleSettings() => Debug.Log("[LobbyUI] Settings");
        private void HandleMail() => Debug.Log("[LobbyUI] Mail");
        private void HandleNotifications()
        {
            Debug.Log("[LobbyUI] Notifications");
            // Demo toast for the spec example: slides in, warning icon pulses,
            // progress bar depletes, auto-dismisses when the countdown hits zero.
            var canvas = topBar != null ? topBar.GetComponentInParent<Canvas>() : GetComponentInParent<Canvas>();
            Notifications.ShowCountdown(canvas, "TRAIN CAR DESTROYING", 10);
        }
        private void HandleFriends() => Debug.Log("[LobbyUI] Friends");

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
