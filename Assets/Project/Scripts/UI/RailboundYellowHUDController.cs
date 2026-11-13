using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using TMPro;
using Michsky.UI.Reach;
using Railbound.Networking;

namespace Railbound.UI
{
    /// <summary>
    /// Master controller for the RAILBOUND Yellow Line HUD Lobby UI.
    /// Implements AAA Warzone-inspired tactical military shooter lobby aesthetics:
    /// - Tactical Yellow (#FFD000) accents, thin chamfered borders, dark steel surfaces.
    /// - Header with profile sync, currencies, season indicator, and operator switcher.
    /// - 6-item Left Navigation Menu (Play, Operators, Loadout, Battle Pass, Challenges, Store).
    /// - Central 3D character integration with floating tactical HUD tag.
    /// - Lower-left Railbound Royale game mode panel with Deploy action.
    /// - Right-side tactical squad roster with readiness status and invite slot.
    /// - Lower-right season promotion panel.
    /// - Bottom footer with keyboard shortcuts and telemetry.
    /// </summary>
    [DisallowMultipleComponent]
    public class RailboundYellowHUDController : MonoBehaviour
    {
        public static RailboundYellowHUDController Instance { get; private set; }

        [Header("Colors & Theme")]
        public static readonly Color YellowAccent = new Color(0.97f, 0.975f, 1.0f, 1.0f);     // accent is now white (name kept for the existing references)
        public static readonly Color YellowBright = new Color(1.0f, 1.0f, 1.0f, 1.0f);
        public static readonly Color DarkBg = new Color(0.043f, 0.075f, 0.110f, 0.95f);      // #0B131C
        public static readonly Color DarkSteel = new Color(0.095f, 0.133f, 0.176f, 0.90f);   // #18222D
        public static readonly Color TextPrimary = new Color(0.94f, 0.95f, 0.96f, 1.0f);     // #F0F3F5
        public static readonly Color TextSecondary = new Color(0.576f, 0.635f, 0.686f, 1.0f);// #93A2AF
        public static readonly Color RedWarning = new Color(1.0f, 0.27f, 0.27f, 1.0f);       // #FF4545
        public static readonly Color GreenSuccess = new Color(0.2f, 0.85f, 0.3f, 1.0f);

        [Header("Top Header Elements")]
        [SerializeField] private TextMeshProUGUI headerPlayerName;
        [SerializeField] private TextMeshProUGUI headerPlayerLevel;
        [SerializeField] private Image headerPlayerXpFill;
        [SerializeField] private TextMeshProUGUI headerCoinsText;
        [SerializeField] private TextMeshProUGUI headerStarsText;
        [SerializeField] private TextMeshProUGUI headerOperatorName;
        [SerializeField] private TextMeshProUGUI headerOperatorRole;
        [SerializeField] private Button headerPrevOperatorBtn;
        [SerializeField] private Button headerNextOperatorBtn;
        [SerializeField] private Button headerSettingsBtn;

        [Header("Floating Operator Tag (Center 3D Stage)")]
        [SerializeField] private RectTransform floatingOperatorTag;
        [SerializeField] private TextMeshProUGUI floatingOperatorName;

        [Header("Left Navigation Menu")]
        [SerializeField] private Button[] navButtons; // 0=Play, 1=Operators, 2=Loadout, 3=BattlePass, 4=Challenges, 5=Store
        [SerializeField] private Image[] navSelectedIndicators;
        [SerializeField] private TextMeshProUGUI[] navLabels;
        [SerializeField] private int currentNavIndex = 0;

        [Header("Lower-Left Game Mode Panel")]
        [SerializeField] private TextMeshProUGUI modeTitleText;
        [SerializeField] private TextMeshProUGUI modeSubtitleText;
        [SerializeField] private TextMeshProUGUI modeMapText;
        [SerializeField] private Button fillSquadToggleBtn;
        [SerializeField] private Image fillSquadToggleKnob;
        [SerializeField] private Button deployButton;
        [SerializeField] private TextMeshProUGUI deployButtonLabel;
        [SerializeField] private Image deployButtonBg;
        private bool isFillSquadOn = true;
        private bool isDeploying = false;

        [Header("Right-Side Squad Panel")]
        [SerializeField] private TextMeshProUGUI squadCountText;
        [SerializeField] private SquadRowUI[] squadRows; // 0=Local, 1=Squadmate1, 2=Squadmate2
        [SerializeField] private Button invitePlayerBtn;

        [Serializable]
        public class SquadRowUI
        {
            public GameObject root;
            public Image avatarImage;
            public TextMeshProUGUI nameText;
            public TextMeshProUGUI levelText;
            public Image readyBadgeBg;
            public TextMeshProUGUI readyBadgeText;
            public GameObject leaderCrown;
        }

        [Header("Promotion & Footer")]
        [SerializeField] private Button seasonPromoBtn;
        [SerializeField] private TextMeshProUGUI footerPingText;

        [Header("Panel Roots")]
        [SerializeField] private GameObject customizationPanelRoot;

        [Header("Audio SFX")]
        [SerializeField] private AudioClip clickSound;
        [SerializeField] private AudioClip hoverSound;
        [SerializeField] private AudioClip deploySound;
        private AudioSource audioSource;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D UI sound
        }

        public void PlayClickSound()
        {
            if (audioSource != null && clickSound != null)
                audioSource.PlayOneShot(clickSound, 0.75f);
        }

        public void PlayHoverSound()
        {
            if (audioSource != null && hoverSound != null)
                audioSource.PlayOneShot(hoverSound, 0.5f);
        }

        public void PlayDeploySound()
        {
            if (audioSource != null && deploySound != null)
                audioSource.PlayOneShot(deploySound, 0.95f);
            else PlayClickSound();
        }

        private void Start()
        {
            BindHeader();
            BindNavigation();
            BindGameModeAndDeploy();
            BindSquad();
            BindPromoAndFooter();

            SyncProfileData();
            UpdateOperatorDisplay();
            SetupDeployStates();

            // squad shown on the stage: nameplates over the models, daily challenges in the left column
            var plateFont = deployButtonLabel != null ? deployButtonLabel.font : null;
            LobbyChallengesPanel.Create(transform, plateFont);
            LobbySquadNameplates.Create(transform, plateFont);
            partyPanel = LobbyPartyPanel.Create(transform, plateFont);
            modePanel = LobbyGameModePanel.Create(transform, plateFont);
            SetupModeCard();

            // the full-screen menus of the top navigation; they go back to PLAY when they close
            Action backToPlay = () => { currentNavIndex = 0; RefreshNavTabsVisual(); };
            settingsScreen = LobbyReachSettings.Find();
            if (settingsScreen != null) settingsScreen.SignOutRequested = OnSettingsOrBackClicked;
            else Debug.LogWarning("[YellowHUD] The settings menu is missing from the lobby scene: run Railbound > Import MAP-2 Settings Menu into Lobby.");
            levelUpPopup = LobbyLevelUpPopup.Create(plateFont);
            PlayerProgression.Changed -= OnProgressChanged; PlayerProgression.Changed += OnProgressChanged;
            ApplyProgress(true);
            menuScreens[2] = LobbyMenuScreen.Create<LobbyWeaponsScreen>(plateFont, gameObject, SelectNavTab, backToPlay);
            menuScreens[3] = LobbyMenuScreen.Create<LobbyBattlePassScreen>(plateFont, gameObject, SelectNavTab, backToPlay);
            menuScreens[4] = LobbyMenuScreen.Create<LobbyChallengesScreen>(plateFont, gameObject, SelectNavTab, backToPlay);
            menuScreens[5] = LobbyMenuScreen.Create<LobbyStoreScreen>(plateFont, gameObject, SelectNavTab, backToPlay);
            customizer = LobbyCharacterCustomizer.Create(plateFont, gameObject, OnCustomizerClosed);
            LobbyParty.Instance.SelectionChanged += UpdateOperatorDisplay;

            // Hook operator change
            if (LobbyCharacterPreviewManager.Instance != null)
            {
                LobbyCharacterPreviewManager.Instance.OnOperatorChanged += (op) => UpdateOperatorDisplay();
            }
        }

        private LobbyCharacterCustomizer customizer;
        private LobbyPartyPanel partyPanel;
        private LobbyGameModePanel modePanel;
        private readonly LobbyMenuScreen[] menuScreens = new LobbyMenuScreen[6];   // by nav index; null where the tab has no full-screen menu

        private LobbyReachSettings settingsScreen;   // the settings menu copied from MAP-2's pause menu (see LobbySettingsImporter)
        private LobbyLevelUpPopup levelUpPopup;
        private float shownFill = -1f;                  // the XP bar as drawn (it glides to the real value)
        private float shownCoins = -1f, targetCoins;    // the coin counter as drawn (it counts up)
        private bool levelUpRunning;

        private bool AnyMenuBusy()
        {
            foreach (var s in menuScreens) if (s != null && s.BusyThisFrame) return true;
            if (settingsScreen != null && settingsScreen.BusyThisFrame) return true;
            return levelUpPopup != null && levelUpPopup.IsOpen;
        }

        private void OpenSettings()
        {
            if (settingsScreen == null) return;
            if (deployState != DeployState.Idle && deployState != DeployState.Failed) return;
            for (int i = 0; i < menuScreens.Length; i++) if (menuScreens[i] != null) menuScreens[i].CloseQuietly();
            settingsScreen.Open();
        }

        private void OnProgressChanged() { ApplyProgress(false); }

        // level, XP bar, coins and gems in the header, from the real progress data
        private void ApplyProgress(bool instant)
        {
            var d = PlayerProgression.Current;
            if (headerPlayerLevel != null) headerPlayerLevel.text = PlayerProgression.LevelText;
            targetCoins = d.coins;
            if (instant || shownCoins < 0f) shownCoins = targetCoins;
            if (headerCoinsText != null) headerCoinsText.text = Mathf.RoundToInt(shownCoins).ToString("N0");
            if (headerStarsText != null) headerStarsText.text = d.gems.ToString("N0");
            // a Filled image without a sprite ignores fillAmount (it just draws full), so the bar needs a plain white sprite
            if (headerPlayerXpFill != null && headerPlayerXpFill.sprite == null)
            {
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                var cols = new Color32[16]; for (int i = 0; i < cols.Length; i++) cols[i] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(cols); tex.Apply();
                headerPlayerXpFill.sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
                headerPlayerXpFill.type = Image.Type.Filled;
                headerPlayerXpFill.fillMethod = Image.FillMethod.Horizontal;
                headerPlayerXpFill.fillOrigin = 0;
            }
            float fill = PlayerProgression.XpFill;
            if (instant || shownFill < 0f) shownFill = fill;
            if (headerPlayerXpFill != null) headerPlayerXpFill.fillAmount = shownFill;
            LobbyParty.Instance.SetLocal(headerPlayerName != null ? headerPlayerName.text : "OPERATOR", PlayerProgression.LevelText,
                LobbyCharacterPreviewManager.Instance != null ? LobbyCharacterPreviewManager.Instance.CurrentOperatorIndex : 0);
        }

        // the bar glides and the coins count up; a level-up that happened in the last match is shown once the lobby is quiet
        private void TickProgress()
        {
            float dt = Time.unscaledDeltaTime;
            if (headerPlayerXpFill != null && shownFill >= 0f)
            {
                float target = PlayerProgression.XpFill;
                if (!Mathf.Approximately(shownFill, target))
                {
                    shownFill = Mathf.MoveTowards(shownFill, target, dt * 0.6f);
                    headerPlayerXpFill.fillAmount = shownFill;
                }
            }
            if (headerCoinsText != null && shownCoins >= 0f && !Mathf.Approximately(shownCoins, targetCoins))
            {
                float diff = targetCoins - shownCoins;
                shownCoins = Mathf.MoveTowards(shownCoins, targetCoins, Mathf.Max(8f, Mathf.Abs(diff) * 2.5f) * dt);
                headerCoinsText.text = Mathf.RoundToInt(shownCoins).ToString("N0");
            }

            if (!levelUpRunning && PlayerProgression.Pending.Count > 0 && levelUpPopup != null && !levelUpPopup.IsOpen
                && (deployState == DeployState.Idle || deployState == DeployState.Failed)
                && !(customizer != null && customizer.BusyThisFrame) && !(partyPanel != null && partyPanel.IsOpen) && !(modePanel != null && modePanel.IsOpen))
                StartCoroutine(ShowLevelUps());
        }

        private IEnumerator ShowLevelUps()
        {
            levelUpRunning = true;
            yield return new WaitForSecondsRealtime(0.9f);   // let the lobby appear first
            // several levels gained since the last time are shown as one climb
            int from = -1, to = 0, bonus = 0;
            while (PlayerProgression.Pending.Count > 0)
            {
                var p = PlayerProgression.Pending.Dequeue();
                if (from < 0) from = p.from;
                to = p.to; bonus += p.bonusCoins;
            }
            if (from >= 0)
            {
                if (settingsScreen != null) settingsScreen.Close();
                foreach (var m in menuScreens) if (m != null) m.CloseQuietly();
                bool closed = false;
                levelUpPopup.Show(from, to, bonus, () => closed = true, PlayDeploySound);
                while (!closed) yield return null;
            }
            levelUpRunning = false;
        }

        // the selected tab's label must stay readable (dark on the white block) whatever hover colours other scripts apply
        private void LateUpdate() { RefreshNavTabsVisual(); }

        private void OnCustomizerClosed()
        {
            currentNavIndex = 0;
            RefreshNavTabsVisual();
        }

        private void Update()
        {
            TickProgress();

            // the customization screen owns the keyboard while it is open (and on the frame its Esc closed it)
            if (customizer != null && customizer.BusyThisFrame)
            {
                TickDeployState();
                return;
            }

            // a full-screen menu (Battle Pass, Challenges, Store) owns the keyboard while it is open and on the frame its Esc closed it
            if (AnyMenuBusy())
            {
                TickDeployState();
                return;
            }

            // the party window owns the keyboard while it is open: Space / Esc / F1 must not deploy, go back or reopen it
            // (typing a party code has Space and letters in it)
            if (partyPanel != null && partyPanel.IsOpen)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) partyPanel.Hide();
                TickDeployState();
                return;
            }

            // the mode screen owns the keyboard while it is open (Space must not deploy, Esc must only close it)
            if (modePanel != null && modePanel.IsOpen)
            {
                TickDeployState();
                return;
            }

            // Keyboard shortcuts
            if (Input.GetKeyDown(KeyCode.M))
            {
                OpenModePanel();
            }
            else if (Input.GetKeyDown(KeyCode.Space))
            {
                if (deployState == DeployState.Idle || deployState == DeployState.Failed) OnDeployClicked();
            }
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (deployState == DeployState.Deploying) CancelDeploy();
                else if (deployState == DeployState.Idle || deployState == DeployState.Failed) OnSettingsOrBackClicked();
            }
            else if (Input.GetKeyDown(KeyCode.F1))
            {
                OnInviteClicked();
            }

            TickDeployState();
        }

        private Coroutine pingCoroutine;

        // The Reach panel manager toggles the Multiplayer panel's active state right after load, which stops coroutines,
        // so (re)start the server ping every time this component is enabled.
        private void OnEnable()
        {
            if (footerPingText != null)
            {
                if (pingCoroutine != null) StopCoroutine(pingCoroutine);
                pingCoroutine = StartCoroutine(PingRoutine());
            }
        }

        private void OnDestroy()
        {
            if (hookedMatchmaker != null) hookedMatchmaker.StatusChanged -= OnMatchmakingStatus;
            GameModeCatalog.Changed -= ApplyModeToCard;
            PlayerProgression.Changed -= OnProgressChanged;
            var partyService = NakamaPartyService.InstanceIfExists;
            if (partyService != null) { partyService.DeployRequested -= OnPartyDeployRequested; partyService.CancelRequested -= OnPartyCancelRequested; }
            var party = LobbyParty.InstanceIfExists;
            if (party != null) { party.Changed -= RefreshSquad; party.SelectionChanged -= UpdateOperatorDisplay; }
        }

        // ----------------------------------------------------
        // 1. TOP HEADER & PROFILE SYNC
        // ----------------------------------------------------
        private void BindHeader()
        {
            if (headerPrevOperatorBtn != null)
                headerPrevOperatorBtn.onClick.AddListener(() =>
                {
                    PlayClickSound();
                    LobbyCharacterPreviewManager.Instance?.PreviousOperator();
                });

            if (headerNextOperatorBtn != null)
                headerNextOperatorBtn.onClick.AddListener(() =>
                {
                    PlayClickSound();
                    LobbyCharacterPreviewManager.Instance?.NextOperator();
                });

            if (headerSettingsBtn != null)
                headerSettingsBtn.onClick.AddListener(() =>
                {
                    PlayClickSound();
                    OpenSettings();
                });
        }

        public async void SyncProfileData()
        {
            string username = "OPERATOR";

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
                    }
                    await PlayerProgression.LoadAsync();   // level, XP and coins (cloud copy merged with this device's)
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"[YellowHUD] Profile fallback: {ex.Message}");
            }

            string levelStr = PlayerProgression.LevelText;
            if (headerPlayerName != null) headerPlayerName.text = username.ToUpper();
            if (headerPlayerLevel != null) headerPlayerLevel.text = levelStr;
            ApplyProgress(true);

            LobbyParty.Instance.SetLocal(username.ToUpper(), levelStr, LobbyCharacterPreviewManager.Instance != null ? LobbyCharacterPreviewManager.Instance.CurrentOperatorIndex : 0);


            // Also update local player squad row
            if (squadRows != null && squadRows.Length > 0 && squadRows[0].nameText != null)
            {
                squadRows[0].nameText.text = username.ToUpper();
                if (squadRows[0].levelText != null) squadRows[0].levelText.text = levelStr;
            }
        }

        private void UpdateOperatorDisplay()
        {
            // the plate shows whoever is in the spotlight; only the local player's operator can be cycled
            var preview = LobbyCharacterPreviewManager.Instance;
            var selected = LobbyParty.Instance.Selected;
            bool localSelected = selected == null || selected.isLocal;
            var op = (!localSelected && preview != null) ? preview.GetOperator(selected.operatorIndex) : preview?.CurrentOperator;
            string opName = op != null ? op.displayName : "MIL-R5 GHOST";
            string opRole = op != null ? $"// {op.roleCategory.ToUpper()}" : "// ASSAULT / SPEC-OPS";

            if (headerOperatorName != null) headerOperatorName.text = opName;
            if (headerOperatorRole != null) headerOperatorRole.text = opRole;
            if (floatingOperatorName != null) floatingOperatorName.text = opName;
            if (headerPrevOperatorBtn != null) headerPrevOperatorBtn.gameObject.SetActive(localSelected);
            if (headerNextOperatorBtn != null) headerNextOperatorBtn.gameObject.SetActive(localSelected);
            if (preview != null)
                LobbyParty.Instance.SetLocal(null, null, preview.CurrentOperatorIndex);
        }

        // ----------------------------------------------------
        // 2. LEFT NAVIGATION MENU
        // ----------------------------------------------------
        private void BindNavigation()
        {
            // the LOADOUT tab became WEAPONS
            if (navLabels != null && navLabels.Length > 2 && navLabels[2] != null) navLabels[2].text = "WEAPONS";
            if (navButtons == null) return;
            for (int i = 0; i < navButtons.Length; i++)
            {
                int index = i;
                if (navButtons[i] != null)
                {
                    navButtons[i].onClick.AddListener(() => SelectNavTab(index));
                }
            }
            RefreshNavTabsVisual();
        }

        public void SelectNavTab(int index)
        {
            PlayClickSound();
            currentNavIndex = index;
            RefreshNavTabsVisual();

            if (settingsScreen != null) settingsScreen.Close();
            // leaving a full-screen menu for another tab closes it first
            for (int i = 0; i < menuScreens.Length; i++)
                if (menuScreens[i] != null && i != index) menuScreens[i].CloseQuietly();

            SetCustomizationPanelVisible(false);   // the old skills card is gone: skills show on the operator screens

            switch (index)
            {
                case 0: // PLAY
                    Debug.Log("[YellowHUD] Nav: PLAY (Main Lobby)");
                    break;
                case 1: // OPERATORS: the customization screen
                    Debug.Log("[YellowHUD] Nav: OPERATORS");
                    if (customizer != null) customizer.Open();
                    break;
                case 2: // WEAPONS (was LOADOUT)
                    Debug.Log("[YellowHUD] Nav: WEAPONS");
                    if (menuScreens[2] != null) menuScreens[2].Open();
                    break;
                case 3: // BATTLE PASS
                    Debug.Log("[YellowHUD] Nav: BATTLE PASS");
                    if (menuScreens[3] != null) menuScreens[3].Open();
                    break;
                case 4: // CHALLENGES
                    Debug.Log("[YellowHUD] Nav: CHALLENGES");
                    if (menuScreens[4] != null) menuScreens[4].Open();
                    break;
                case 5: // STORE
                    Debug.Log("[YellowHUD] Nav: STORE");
                    if (menuScreens[5] != null) menuScreens[5].Open();
                    break;
            }
        }

        private void SetCustomizationPanelVisible(bool visible)
        {
            if (customizationPanelRoot == null)
            {
                foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                {
                    if (t != null && t.name == "Customization_UI_Root" && t.gameObject.scene.IsValid())
                    {
                        customizationPanelRoot = t.gameObject;
                        break;
                    }
                }
            }

            if (customizationPanelRoot != null && customizationPanelRoot.activeSelf != visible)
            {
                customizationPanelRoot.SetActive(visible);
            }
        }

        private void RefreshNavTabsVisual()
        {
            if (navButtons == null) return;
            for (int i = 0; i < navButtons.Length; i++)
            {
                bool isSelected = (i == currentNavIndex);
                if (navSelectedIndicators != null && i < navSelectedIndicators.Length && navSelectedIndicators[i] != null)
                {
                    navSelectedIndicators[i].gameObject.SetActive(isSelected);
                }
                if (navLabels != null && i < navLabels.Length && navLabels[i] != null)
                {
                    // MW look: the selected tab is a white block (Indicator_Selected) with dark text, the rest stay soft white
                    navLabels[i].color = isSelected ? new Color(0.05f, 0.055f, 0.065f, 1f) : new Color(0.96f, 0.96f, 0.97f, 0.78f);
                    navLabels[i].fontStyle = isSelected ? FontStyles.Bold : FontStyles.Normal;
                }
            }
        }

        // ----------------------------------------------------
        // 3. LOWER-LEFT GAME MODE & DEPLOY
        // ----------------------------------------------------
        private void BindGameModeAndDeploy()
        {
            if (fillSquadToggleBtn != null)
            {
                fillSquadToggleBtn.onClick.AddListener(ToggleFillSquad);
                UpdateFillSquadVisual();
            }

            if (deployButton != null)
            {
                deployButton.onClick.AddListener(OnDeployClicked);
            }
        }

        // ----------------------------------------------------
        // 3a. MATCH MODE: the card shows the chosen mode; clicking it (or key M) opens the mode screen
        // ----------------------------------------------------
        private Button modeCardButton;
        private TextMeshProUGUI modeTagPlayers, modeTagMap;

        private void SetupModeCard()
        {
            Transform card = modeTitleText != null ? modeTitleText.transform.parent : null;
            if (card != null)
            {
                var tp = card.Find("Tag_30P"); if (tp != null) modeTagPlayers = tp.GetComponentInChildren<TextMeshProUGUI>(true);
                var tm = card.Find("Tag_Map"); if (tm != null) modeTagMap = tm.GetComponentInChildren<TextMeshProUGUI>(true);

                var hit = card.GetComponent<Image>();
                if (hit == null) { hit = card.gameObject.AddComponent<Image>(); hit.color = new Color(0f, 0f, 0f, 0f); }
                hit.raycastTarget = true;
                modeCardButton = card.GetComponent<Button>();
                if (modeCardButton == null) modeCardButton = card.gameObject.AddComponent<Button>();
                modeCardButton.targetGraphic = hit;
                modeCardButton.transition = Selectable.Transition.None;
                modeCardButton.onClick.RemoveAllListeners();
                modeCardButton.onClick.AddListener(OpenModePanel);

                // a chip on the banner tells the player the mode can be changed (and which key does it)
                var chipGo = new GameObject("Chip_ChangeMode", typeof(RectTransform), typeof(Image), typeof(Button));
                chipGo.layer = card.gameObject.layer;
                chipGo.transform.SetParent(card, false);
                var cr = (RectTransform)chipGo.transform;
                cr.anchorMin = cr.anchorMax = new Vector2(1f, 1f); cr.pivot = new Vector2(1f, 1f);
                cr.anchoredPosition = new Vector2(-30f, -30f); cr.sizeDelta = new Vector2(176f, 30f);
                var chipImg = chipGo.GetComponent<Image>(); chipImg.color = new Color(0f, 0f, 0f, 0.78f);
                var chipBtn = chipGo.GetComponent<Button>(); chipBtn.targetGraphic = chipImg;
                var cb = chipBtn.colors; cb.normalColor = Color.white; cb.highlightedColor = new Color(0.78f, 0.78f, 0.78f, 1f); cb.pressedColor = new Color(0.55f, 0.55f, 0.55f, 1f); cb.fadeDuration = 0.08f; chipBtn.colors = cb;
                chipBtn.onClick.AddListener(OpenModePanel);
                var chipTextGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                chipTextGo.layer = card.gameObject.layer;
                chipTextGo.transform.SetParent(chipGo.transform, false);
                var ctr = (RectTransform)chipTextGo.transform; ctr.anchorMin = Vector2.zero; ctr.anchorMax = Vector2.one; ctr.offsetMin = Vector2.zero; ctr.offsetMax = Vector2.zero;
                var chipText = chipTextGo.GetComponent<TextMeshProUGUI>();
                chipText.font = modeSubtitleText != null ? modeSubtitleText.font : TMP_Settings.defaultFontAsset;
                chipText.text = "M   CHANGE MODE"; chipText.fontSize = 14; chipText.fontStyle = FontStyles.Bold; chipText.characterSpacing = 4f;
                chipText.alignment = TextAlignmentOptions.Center; chipText.color = Color.white; chipText.raycastTarget = false;
            }

            GameModeCatalog.Changed -= ApplyModeToCard;
            GameModeCatalog.Changed += ApplyModeToCard;
            ApplyModeToCard();
        }

        private void ApplyModeToCard()
        {
            var mode = GameModeCatalog.Selected;
            var team = GameModeCatalog.SelectedTeamSize;
            if (modeTitleText != null) modeTitleText.text = mode.CardTitle;
            if (modeTagPlayers != null) modeTagPlayers.text = mode.Players + "P";
            if (modeTagMap != null) modeTagMap.text = mode.Map;

            // the idle subtitle is the line under the title: team size or format and the player count
            idleSubtitle = GameModeCatalog.Summary(mode, team);
            if (deployState == DeployState.Idle) RefreshDeployVisuals();
        }

        private void OpenModePanel()
        {
            if (modePanel == null) return;
            if (deployState != DeployState.Idle && deployState != DeployState.Failed) return;   // the mode cannot change while searching

            var party = NakamaPartyService.InstanceIfExists;
            if (party != null && party.InParty && party.MemberCount > 1 && !party.IsLeader)
            {
                noticeText = "ONLY THE PARTY LEADER CHOOSES THE MODE";
                noticeUntil = Time.unscaledTime + 4f;
                return;
            }
            PlayClickSound();
            modePanel.Show();
        }

        private void ToggleFillSquad()
        {
            PlayClickSound();
            isFillSquadOn = !isFillSquadOn;
            UpdateFillSquadVisual();
            Debug.Log($"[YellowHUD] Fill Squad: {(isFillSquadOn ? "ON" : "OFF")}");
        }

        private void UpdateFillSquadVisual()
        {
            if (fillSquadToggleKnob != null)
            {
                var rt = fillSquadToggleKnob.rectTransform;
                rt.anchoredPosition = isFillSquadOn ? new Vector2(10f, 0f) : new Vector2(-10f, 0f);
                fillSquadToggleKnob.color = isFillSquadOn ? YellowAccent : TextSecondary;
            }
        }

        // ----------------------------------------------------
        // 3b. DEPLOY / MATCHMAKING STATES
        //     Idle -> Deploying (cancellable) -> Loading (overlay) | Failed (retry)
        // ----------------------------------------------------
        private enum DeployState { Idle, Deploying, Loading, Failed }
        private DeployState deployState = DeployState.Idle;
        private float deployStateStart;
        private string deployStage = "";

        private Sprite deployIdleSprite;
        private Color deployIdleColor = Color.white;
        private Color deployIdleLabelColor = Color.white;
        private string idleSubtitle = "";
        private Color idleSubtitleColor = Color.white;
        private CanvasGroup lockTags, lockMap, lockFill;
        private TextMeshProUGUI keyHintText;
        private Transform keyHintRoot, playTriangle;
        private LobbyMatchmakingController hookedMatchmaker;

        private CanvasGroup loadingOverlay;
        private RectTransform loadingBar;
        private TextMeshProUGUI loadingSub;
        private float loadingTarget;

        private static readonly Color Dim = new Color(0.96f, 0.96f, 0.97f, 0.62f);

        private void SetupDeployStates()
        {
            if (deployButtonBg != null) { deployIdleSprite = deployButtonBg.sprite; deployIdleColor = deployButtonBg.color; }
            if (deployButtonLabel != null) deployIdleLabelColor = deployButtonLabel.color;
            if (modeSubtitleText != null) { idleSubtitle = modeSubtitleText.text; idleSubtitleColor = modeSubtitleText.color; }
            if (deployButton != null)
            {
                keyHintRoot = deployButton.transform.Find("KeyHint");
                if (keyHintRoot != null) keyHintText = keyHintRoot.GetComponentInChildren<TextMeshProUGUI>(true);
                playTriangle = deployButton.transform.Find("PlayTriangle");
            }
            Transform card = modeTitleText != null ? modeTitleText.transform.parent : null;
            if (card != null)
            {
                lockTags = EnsureGroup(card.Find("Tag_30P"));
                lockMap = EnsureGroup(card.Find("Tag_Map"));
                lockFill = EnsureGroup(card.Find("FillSquad_Row"));
            }

            var mm = LobbyMatchmakingController.Instance;
            if (mm != null)
            {
                hookedMatchmaker = mm;
                mm.StatusChanged += OnMatchmakingStatus;
            }
            RefreshDeployVisuals();

            // the match or the loading screen sent the player back with a reason: say it (a problem offers RETRY)
            if (LobbyNotice.TryTake(out string notice, out LobbyNotice.Kind noticeKind))
            {
                if (noticeKind == LobbyNotice.Kind.Problem) SetDeployState(DeployState.Failed, notice);
                else { noticeText = notice; noticeUntil = Time.unscaledTime + 8f; }
            }

            // "PLAY AGAIN" on the results screen asked for the next deployment to start by itself
            if (LobbyAutoDeploy.TryConsume()) StartCoroutine(AutoDeployRoutine());

            // the party leader starts / cancels the search for everybody
            var partyService = NakamaPartyService.Instance;
            if (partyService != null)
            {
                partyService.DeployRequested -= OnPartyDeployRequested; partyService.DeployRequested += OnPartyDeployRequested;
                partyService.CancelRequested -= OnPartyCancelRequested; partyService.CancelRequested += OnPartyCancelRequested;
            }

            SetupPartyChip();
        }

        private void OnPartyDeployRequested()
        {
            if (deployState != DeployState.Idle && deployState != DeployState.Failed) return;
            deployFromParty = true;
            OnDeployClicked();
        }

        private void OnPartyCancelRequested()
        {
            if (deployState == DeployState.Deploying) CancelDeploy();
        }

        // The "F1 SOCIAL" chip in the footer opens the party window with the mouse too and shows the size of the party.
        private TextMeshProUGUI partyChipLabel;

        private void SetupPartyChip()
        {
            Transform chip = transform.Find("Bottom_Footer/Keybinds/KB_Social");
            if (chip == null) return;

            var action = chip.Find("Action");
            if (action != null) partyChipLabel = action.GetComponent<TextMeshProUGUI>();

            var hit = chip.GetComponent<Image>();
            if (hit == null) { hit = chip.gameObject.AddComponent<Image>(); hit.color = new Color(0f, 0f, 0f, 0f); }
            hit.raycastTarget = true;
            var btn = chip.GetComponent<Button>();
            if (btn == null) btn = chip.gameObject.AddComponent<Button>();
            btn.targetGraphic = hit;
            btn.transition = Selectable.Transition.None;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(OnInviteClicked);
            RefreshPartyChip();
        }

        private void RefreshPartyChip()
        {
            if (partyChipLabel == null) return;
            var lobbyParty = LobbyParty.InstanceIfExists;
            int n = lobbyParty != null ? lobbyParty.Count : 1;
            partyChipLabel.text = n > 1 ? "PARTY " + n + "/" + LobbyParty.MaxMembers : "SOCIAL";
        }

        private IEnumerator AutoDeployRoutine()
        {
            yield return new WaitForSecondsRealtime(1.0f);   // let the lobby appear first
            if (deployState == DeployState.Idle || deployState == DeployState.Failed) OnDeployClicked();
        }

        private string noticeText;      // an information line for the idle subtitle, shown for a few seconds
        private float noticeUntil;

        private static CanvasGroup EnsureGroup(Transform t)
        {
            if (t == null) return null;
            var g = t.GetComponent<CanvasGroup>();
            if (g == null) g = t.gameObject.AddComponent<CanvasGroup>();
            return g;
        }

        private bool deployFromParty;   // the deployment was started by the party leader's signal, not by this player

        private void OnDeployClicked()
        {
            if (deployState == DeployState.Deploying) { CancelDeploy(); return; }
            if (deployState == DeployState.Loading) return;

            // In a party only the leader starts the match; the members follow on the leader's signal.
            var party = NakamaPartyService.InstanceIfExists;
            bool inGroup = party != null && party.InParty && party.MemberCount > 1;
            if (inGroup && !party.IsLeader && !deployFromParty)
            {
                noticeText = "ONLY THE PARTY LEADER CAN START THE MATCH";
                noticeUntil = Time.unscaledTime + 4f;
                return;
            }
            deployFromParty = false;
            if (inGroup && party.IsLeader) _ = party.BroadcastDeployAsync();

            PlayDeploySound();
            Debug.Log("[YellowHUD] DEPLOY CLICKED! Initiating matchmaking...");
            SetDeployState(DeployState.Deploying, "FINDING MATCH");

            var bridge = ReachLobbyBridge.Instance;
            if (bridge != null)
            {
                bridge.HandlePlayClicked();
            }
            else if (LobbyMatchmakingController.Instance != null)
            {
                LobbyMatchmakingController.Instance.Deploy();
            }
            else
            {
                SetDeployState(DeployState.Failed, "MATCHMAKER UNAVAILABLE");
            }
        }

        private void CancelDeploy()
        {
            var mm = LobbyMatchmakingController.Instance;
            PlayClickSound();
            if (mm != null && mm.CanCancel)
            {
                deployStage = "CANCELLING";
                mm.CancelDeploy();
                // the leader's cancel stops the search of the whole party
                var party = NakamaPartyService.InstanceIfExists;
                if (party != null && party.InParty && party.IsLeader && party.MemberCount > 1) _ = party.BroadcastCancelAsync();
            }
            else if (mm == null)
            {
                SetDeployState(DeployState.Idle, "");
            }
        }

        private void OnMatchmakingStatus(string message)
        {
            string m = (message ?? "").ToUpperInvariant();
            if (m.Contains("FAILED")) SetDeployState(DeployState.Failed, FailureText());
            else if (m.Contains("CANCEL") && !m.Contains("CANCELLING")) SetDeployState(DeployState.Idle, "");
            else if (m.Contains("LOADING")) SetDeployState(DeployState.Loading, "LOADING MAP");
            else
            {
                string stage = StageFor(m);
                if (stage != null) { deployStage = stage; RefreshDeployVisuals(); }
            }
        }

        /// <summary>The plain-words stage the player sees for a backend status line (null = keep the current one).</summary>
        private static string StageFor(string status)
        {
            if (status.Contains("SEARCHING FOR PLAYERS")) return "SEARCHING FOR PLAYERS";
            if (status.Contains("LOBBY FOUND")) return "LOBBY FOUND";
            if (status.Contains("NO FULL LOBBY")) return "FILLING THE MATCH WITH BOTS";
            if (status.Contains("REQUESTING GAME SERVER") || status.Contains("PREPARING A MATCH SERVER")) return "PREPARING A MATCH SERVER";
            if (status.Contains("GAME SERVER READY")) return "MATCH SERVER READY";
            if (status.Contains("CREATING")) return "SETTING UP THE MATCH";
            if (status.Contains("WARMING UP")) return "WARMING UP THE SERVER";
            if (status.Contains("CONNECTED TO GAME SERVER")) return "CONNECTED";
            if (status.Contains("PREPARING NETWORK") || status.Contains("CONNECTING") || status.Contains("STARTING")) return "CONNECTING";
            return null;
        }

        /// <summary>One calm sentence for the failure, by cause; never an exception text.</summary>
        private string FailureText()
        {
            var mm = hookedMatchmaker != null ? hookedMatchmaker : LobbyMatchmakingController.Instance;
            switch (mm != null ? mm.LastFailure : LobbyMatchmakingController.DeployFailure.Other)
            {
                case LobbyMatchmakingController.DeployFailure.ServersBusy: return "ALL MATCH SERVERS ARE BUSY - TRY AGAIN IN A MOMENT";
                case LobbyMatchmakingController.DeployFailure.ConnectionFailed: return "COULD NOT REACH THE MATCH SERVER - PLEASE TRY AGAIN";
                default: return "COULD NOT START THE MATCH - PLEASE TRY AGAIN";
            }
        }

        private void SetDeployState(DeployState state, string stage)
        {
            if (state != deployState && (deployState == DeployState.Idle || deployState == DeployState.Failed))
                deployStateStart = Time.unscaledTime;
            deployState = state;
            deployStage = stage;
            isDeploying = state == DeployState.Deploying || state == DeployState.Loading;
            RefreshDeployVisuals();
            loadingTarget = state == DeployState.Loading ? 1f : 0f;
            if (state == DeployState.Loading) EnsureLoadingOverlay();
        }

        private void RefreshDeployVisuals()
        {
            bool busy = deployState == DeployState.Deploying || deployState == DeployState.Loading;

            if (deployButtonBg != null)
            {
                if (busy)
                {
                    var chip = Resources.Load<Sprite>("HUD/Hud_Chip");
                    if (chip != null) deployButtonBg.sprite = chip;
                    deployButtonBg.color = new Color(1f, 1f, 1f, 0.92f);
                }
                else
                {
                    deployButtonBg.sprite = deployIdleSprite;
                    deployButtonBg.color = deployState == DeployState.Failed ? new Color(1f, 0.55f, 0.52f, 1f) : deployIdleColor;
                }
            }

            if (deployButtonLabel != null)
            {
                switch (deployState)
                {
                    case DeployState.Idle: deployButtonLabel.text = "DEPLOY"; deployButtonLabel.color = deployIdleLabelColor; break;
                    case DeployState.Deploying: deployButtonLabel.text = "CANCEL"; deployButtonLabel.color = TextPrimary; break;
                    case DeployState.Loading: deployButtonLabel.text = "LOADING..."; deployButtonLabel.color = Dim; break;
                    case DeployState.Failed: deployButtonLabel.text = "RETRY"; deployButtonLabel.color = deployIdleLabelColor; break;
                }
            }

            if (playTriangle != null) playTriangle.gameObject.SetActive(!busy);
            if (keyHintRoot != null) keyHintRoot.gameObject.SetActive(deployState != DeployState.Loading);
            if (keyHintText != null) keyHintText.text = deployState == DeployState.Deploying ? "ESC" : "SPACE";
            if (deployButton != null) deployButton.interactable = deployState != DeployState.Loading;

            float a = busy ? 0.4f : 1f;
            foreach (var g in new[] { lockTags, lockMap, lockFill })
                if (g != null) { g.alpha = a; g.interactable = !busy; g.blocksRaycasts = !busy; }

            if (modeSubtitleText != null)
            {
                if (deployState == DeployState.Idle) { modeSubtitleText.text = idleSubtitle; modeSubtitleText.color = idleSubtitleColor; }
                else if (deployState == DeployState.Failed) { modeSubtitleText.text = deployStage; modeSubtitleText.color = RedWarning; }
            }
        }

        private void TickDeployState()
        {
            if (deployState == DeployState.Idle && !string.IsNullOrEmpty(noticeText) && modeSubtitleText != null)
            {
                if (Time.unscaledTime < noticeUntil) { modeSubtitleText.text = noticeText; modeSubtitleText.color = YellowAccent; }
                else { noticeText = null; RefreshDeployVisuals(); }
            }

            if (deployState == DeployState.Deploying || deployState == DeployState.Loading)
            {
                int secs = Mathf.FloorToInt(Time.unscaledTime - deployStateStart);
                string line = $"{deployStage}   {secs / 60}:{secs % 60:00}";
                // while searching, show how long the whole search window is (after it the match starts with bots)
                if (deployStage == "SEARCHING FOR PLAYERS" && hookedMatchmaker != null)
                {
                    int window = Mathf.RoundToInt(hookedMatchmaker.QueueSeconds);
                    line += $" / {window / 60}:{window % 60:00}";
                }
                if (modeSubtitleText != null) { modeSubtitleText.text = line; modeSubtitleText.color = YellowAccent; }
                if (loadingSub != null) loadingSub.text = line;
            }

            if (loadingOverlay != null)
            {
                loadingOverlay.alpha = Mathf.MoveTowards(loadingOverlay.alpha, loadingTarget, Time.unscaledDeltaTime * 4f);
                loadingOverlay.blocksRaycasts = loadingOverlay.alpha > 0.01f;
                if (loadingBar != null)
                {
                    float k = (Mathf.Sin(Time.unscaledTime * 2.4f) + 1f) * 0.5f;
                    loadingBar.anchoredPosition = new Vector2(Mathf.Lerp(-170f, 170f, k), 0f);
                }
            }
        }

        private void EnsureLoadingOverlay()
        {
            if (loadingOverlay != null) return;
            var canvas = GetComponentInParent<Canvas>();
            Transform parent = canvas != null ? canvas.rootCanvas.transform : transform;
            int layer = parent.gameObject.layer;

            var go = new GameObject("Deploy_Loading_Overlay", typeof(RectTransform), typeof(CanvasGroup));
            go.layer = layer; go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var cv = go.AddComponent<Canvas>(); cv.overrideSorting = true; cv.sortingOrder = 900;
            go.AddComponent<GraphicRaycaster>();
            loadingOverlay = go.GetComponent<CanvasGroup>(); loadingOverlay.alpha = 0f;

            var dim = NewUi("Dim", go.transform, layer); Stretch(dim);
            var img = dim.gameObject.AddComponent<Image>(); img.color = new Color(0.02f, 0.025f, 0.032f, 0.9f);

            TMP_FontAsset font = deployButtonLabel != null ? deployButtonLabel.font : null;
            var title = NewText("Title", go.transform, layer, "DEPLOYING", 56, TextPrimary, font);
            title.rectTransform.anchoredPosition = new Vector2(0f, 46f);
            loadingSub = NewText("Stage", go.transform, layer, "LOADING MAP", 26, Dim, font);
            loadingSub.rectTransform.anchoredPosition = new Vector2(0f, -8f);

            var track = NewUi("BarTrack", go.transform, layer);
            track.anchorMin = track.anchorMax = new Vector2(0.5f, 0.5f); track.sizeDelta = new Vector2(520f, 4f); track.anchoredPosition = new Vector2(0f, -60f);
            var ti = track.gameObject.AddComponent<Image>(); ti.color = new Color(1f, 1f, 1f, 0.14f);
            loadingBar = NewUi("Bar", track, layer);
            loadingBar.anchorMin = loadingBar.anchorMax = new Vector2(0.5f, 0.5f); loadingBar.sizeDelta = new Vector2(180f, 4f);
            var bi = loadingBar.gameObject.AddComponent<Image>(); bi.color = YellowAccent;
        }

        private static RectTransform NewUi(string name, Transform parent, int layer)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = layer; go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        }

        private static TextMeshProUGUI NewText(string name, Transform parent, int layer, string text, float size, Color col, TMP_FontAsset font)
        {
            var r = NewUi(name, parent, layer);
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.sizeDelta = new Vector2(900f, size + 16f);
            var t = r.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.fontStyle = FontStyles.Bold; t.alignment = TextAlignmentOptions.Center; t.color = col;
            t.characterSpacing = 4f; t.enableWordWrapping = false; t.raycastTarget = false;
            if (font != null) t.font = font;
            return t;
        }

        // Real backend ping for the footer chip (Nakama health endpoint).
        private IEnumerator PingRoutine()
        {
            var wait = new WaitForSecondsRealtime(10f);
            while (true)
            {
                string url = $"{ServerConfig.NakamaScheme}://{ServerConfig.NakamaHost}:{ServerConfig.NakamaPort}/healthcheck";
                float t0 = Time.realtimeSinceStartup;
                using (var req = UnityWebRequest.Get(url))
                {
                    req.timeout = 4;
                    var op = req.SendWebRequest();
                    // UnityWebRequest.timeout does not cover a connect to an unreachable host, so enforce our own limit
                    float waited = 0f;
                    while (!op.isDone && waited < 4f) { waited += Time.unscaledDeltaTime; yield return null; }
                    if (!op.isDone) req.Abort();
                    bool ok = op.isDone && req.result == UnityWebRequest.Result.Success;
                    int ms = Mathf.RoundToInt((Time.realtimeSinceStartup - t0) * 1000f);
                    if (footerPingText != null)
                    {
                        footerPingText.text = ok
                            ? $"SERVER ONLINE   |   PING {ms} MS   |   v{Application.version}"
                            : $"SERVER OFFLINE   |   PING --   |   v{Application.version}";
                        footerPingText.color = ok ? Dim : new Color(1f, 0.45f, 0.42f, 0.9f);
                    }
                }
                yield return wait;
            }
        }

        // ----------------------------------------------------
        // 4. RIGHT-SIDE SQUAD PANEL
        // ----------------------------------------------------
        private void BindSquad()
        {
            if (invitePlayerBtn != null)
            {
                invitePlayerBtn.onClick.AddListener(OnInviteClicked);
            }

            CacheBadgeVisuals();
            var party = LobbyParty.Instance;
            party.Changed += RefreshSquad;
            RefreshSquad();
        }

        // ---- squad panel is driven by LobbyParty (local player + up to 2 friends = 3 players) ----
        private Sprite badgeReadySprite, badgeNotReadySprite, iconReadySprite, iconNotReadySprite;
        private Color badgeReadyTextColor = Color.black, badgeNotReadyTextColor = new Color(1f, 0.38f, 0.36f, 1f);

        private void CacheBadgeVisuals()
        {
            if (squadRows == null) return;
            if (squadRows.Length > 1 && squadRows[1] != null && squadRows[1].readyBadgeBg != null)
            {
                badgeReadySprite = squadRows[1].readyBadgeBg.sprite;
                if (squadRows[1].readyBadgeText != null) badgeReadyTextColor = squadRows[1].readyBadgeText.color;
                var ic = squadRows[1].readyBadgeBg.transform.Find("Icon");
                if (ic != null && ic.GetComponent<Image>() != null) iconReadySprite = ic.GetComponent<Image>().sprite;
            }
            if (squadRows.Length > 2 && squadRows[2] != null && squadRows[2].readyBadgeBg != null)
            {
                badgeNotReadySprite = squadRows[2].readyBadgeBg.sprite;
                if (squadRows[2].readyBadgeText != null) badgeNotReadyTextColor = squadRows[2].readyBadgeText.color;
                var ic = squadRows[2].readyBadgeBg.transform.Find("Icon");
                if (ic != null && ic.GetComponent<Image>() != null) iconNotReadySprite = ic.GetComponent<Image>().sprite;
            }
        }

        private void RefreshSquad()
        {
            var party = LobbyParty.Instance;
            var members = party.Members;

            if (squadRows != null)
            {
                for (int i = 0; i < squadRows.Length; i++)
                {
                    var row = squadRows[i];
                    if (row == null || row.root == null) continue;
                    bool has = i < members.Count && i < LobbyParty.MaxMembers;

                    var cg = row.root.GetComponent<CanvasGroup>();
                    if (cg == null) cg = row.root.AddComponent<CanvasGroup>();
                    cg.alpha = has ? 1f : 0.35f;

                    if (row.nameText != null)
                    {
                        row.nameText.richText = true;
                        row.nameText.text = has ? members[i].displayName + (members[i].isLeader && members.Count > 1 ? "  <size=65%><color=#B0B0B0>LEADER</color></size>" : "") : "OPEN SLOT";
                    }
                    if (row.levelText != null) row.levelText.text = has ? members[i].levelText : "";

                    if (row.readyBadgeBg != null)
                    {
                        row.readyBadgeBg.gameObject.SetActive(has);
                        if (has)
                        {
                            bool ready = members[i].ready;
                            var sp = ready ? badgeReadySprite : badgeNotReadySprite;
                            if (sp != null) row.readyBadgeBg.sprite = sp;
                            if (row.readyBadgeText != null)
                            {
                                row.readyBadgeText.text = ready ? "READY" : "NOT READY";
                                row.readyBadgeText.color = ready ? badgeReadyTextColor : badgeNotReadyTextColor;
                            }
                            var ic = row.readyBadgeBg.transform.Find("Icon");
                            var icImg = ic != null ? ic.GetComponent<Image>() : null;
                            var isp = ready ? iconReadySprite : iconNotReadySprite;
                            if (icImg != null && isp != null) icImg.sprite = isp;
                        }
                    }
                }
            }

            RefreshPartyChip();
            if (squadCountText != null) squadCountText.text = $"{members.Count} / {LobbyParty.MaxMembers} PLAYERS";
            if (invitePlayerBtn != null) invitePlayerBtn.gameObject.SetActive(!party.IsFull);

            // the panel is only as tall as it needs to be: no empty invite row once the party is full
            var panel = squadCountText != null ? squadCountText.transform.parent as RectTransform : null;
            if (panel != null) panel.sizeDelta = new Vector2(panel.sizeDelta.x, party.IsFull ? 306f : 372f);
        }

        private void OnInviteClicked()
        {
            PlayClickSound();
            // the party window: your party code (copy it for a friend) and a field to join a friend's party
            if (partyPanel != null) partyPanel.Show();
        }

        // ----------------------------------------------------
        // 5. PROMO & FOOTER
        // ----------------------------------------------------
        private void BindPromoAndFooter()
        {
            if (seasonPromoBtn != null)
            {
                seasonPromoBtn.onClick.AddListener(() =>
                {
                    SelectNavTab(3); // Open Battle Pass
                });
            }

            if (footerPingText != null)
            {
                footerPingText.text = "CHECKING SERVER...";
            }
        }

        private void OnSettingsOrBackClicked()
        {
            PlayClickSound();
            Debug.Log("[YellowHUD] Settings / Back clicked");
            var bridge = ReachLobbyBridge.Instance;
            if (bridge != null)
            {
                bridge.HandleGoBackClicked();
            }
        }

        private void ShowTacticalNotice(string message)
        {
            Debug.Log($"[YellowHUD Notice] {message}");
        }
    }
}
