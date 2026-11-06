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
    /// - Bottom-center emote action bar.
    /// - Bottom footer with keyboard shortcuts and telemetry.
    /// </summary>
    [DisallowMultipleComponent]
    public class RailboundYellowHUDController : MonoBehaviour
    {
        public static RailboundYellowHUDController Instance { get; private set; }

        [Header("Colors & Theme")]
        public static readonly Color YellowAccent = new Color(1.0f, 0.816f, 0.0f, 1.0f);     // #FFD000
        public static readonly Color YellowBright = new Color(1.0f, 0.894f, 0.36f, 1.0f);    // #FFE45C
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

        [Header("Emote Action Bar")]
        [SerializeField] private Button btnEmoteSalute;
        [SerializeField] private Button btnEmoteWave;
        [SerializeField] private Button btnEmoteFlex;
        [SerializeField] private Button btnEmoteVictory;

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
            BindEmotes();
            BindPromoAndFooter();

            SyncProfileData();
            UpdateOperatorDisplay();
            SetupDeployStates();

            // Hook operator change
            if (LobbyCharacterPreviewManager.Instance != null)
            {
                LobbyCharacterPreviewManager.Instance.OnOperatorChanged += (op) => UpdateOperatorDisplay();
            }
        }

        private void Update()
        {
            // Keyboard shortcuts
            if (Input.GetKeyDown(KeyCode.Space))
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
                    OnSettingsOrBackClicked();
                });
        }

        public async void SyncProfileData()
        {
            string username = "OPERATOR";
            string levelStr = "LVL. 69";
            float xpProgress = 0.65f;

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
                        levelStr = $"LVL. {profile.experienceLevel}";
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"[YellowHUD] Profile fallback: {ex.Message}");
            }

            if (headerPlayerName != null) headerPlayerName.text = username.ToUpper();
            if (headerPlayerLevel != null) headerPlayerLevel.text = levelStr;
            if (headerPlayerXpFill != null) headerPlayerXpFill.fillAmount = xpProgress;

            if (headerCoinsText != null) headerCoinsText.text = "2,450";
            if (headerStarsText != null) headerStarsText.text = "850";

            // Also update local player squad row
            if (squadRows != null && squadRows.Length > 0 && squadRows[0].nameText != null)
            {
                squadRows[0].nameText.text = username.ToUpper();
                if (squadRows[0].levelText != null) squadRows[0].levelText.text = levelStr;
            }
        }

        private void UpdateOperatorDisplay()
        {
            var op = LobbyCharacterPreviewManager.Instance?.CurrentOperator;
            string opName = op != null ? op.displayName : "MIL-R5 GHOST";
            string opRole = op != null ? $"// {op.roleCategory.ToUpper()}" : "// ASSAULT / SPEC-OPS";

            if (headerOperatorName != null) headerOperatorName.text = opName;
            if (headerOperatorRole != null) headerOperatorRole.text = opRole;
            if (floatingOperatorName != null) floatingOperatorName.text = opName;
        }

        // ----------------------------------------------------
        // 2. LEFT NAVIGATION MENU
        // ----------------------------------------------------
        private void BindNavigation()
        {
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

            SetCustomizationPanelVisible(index == 1 || index == 2);

            switch (index)
            {
                case 0: // PLAY
                    Debug.Log("[YellowHUD] Nav: PLAY (Main Lobby)");
                    break;
                case 1: // OPERATORS
                    Debug.Log("[YellowHUD] Nav: OPERATORS");
                    break;
                case 2: // LOADOUT
                    Debug.Log("[YellowHUD] Nav: LOADOUT - Toggling loadout skills");
                    LobbyCustomizationUI.Instance?.SendMessage("ToggleSkillsCard", SendMessageOptions.DontRequireReceiver);
                    break;
                case 3: // BATTLE PASS
                    Debug.Log("[YellowHUD] Nav: BATTLE PASS");
                    ShowTacticalNotice("BATTLE PASS // SEASON 01 - TIER 24 / 100");
                    break;
                case 4: // CHALLENGES
                    Debug.Log("[YellowHUD] Nav: CHALLENGES");
                    ShowTacticalNotice("TACTICAL OPS // DAILY ASSIGNMENTS SYNCHRONIZED");
                    break;
                case 5: // STORE
                    Debug.Log("[YellowHUD] Nav: ARSENAL STORE");
                    ShowTacticalNotice("TACTICAL ARSENAL // SUPPLY SHIPMENT EN ROUTE");
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
                    navLabels[i].color = isSelected ? YellowAccent : TextPrimary;
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
        }

        private static CanvasGroup EnsureGroup(Transform t)
        {
            if (t == null) return null;
            var g = t.GetComponent<CanvasGroup>();
            if (g == null) g = t.gameObject.AddComponent<CanvasGroup>();
            return g;
        }

        private void OnDeployClicked()
        {
            if (deployState == DeployState.Deploying) { CancelDeploy(); return; }
            if (deployState == DeployState.Loading) return;

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
            }
            else if (mm == null)
            {
                SetDeployState(DeployState.Idle, "");
            }
        }

        private void OnMatchmakingStatus(string message)
        {
            string m = (message ?? "").ToUpperInvariant();
            if (m.Contains("FAILED")) SetDeployState(DeployState.Failed, "DEPLOY FAILED - CHECK YOUR CONNECTION");
            else if (m.Contains("CANCEL") && !m.Contains("CANCELLING")) SetDeployState(DeployState.Idle, "");
            else if (m.Contains("LOADING")) SetDeployState(DeployState.Loading, "LOADING MAP");
            else if (m.Contains("STARTING")) { deployStage = "CONNECTING"; RefreshDeployVisuals(); }
            else if (m.Contains("CREATING")) { deployStage = "FINDING MATCH"; RefreshDeployVisuals(); }
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
            if (deployState == DeployState.Deploying || deployState == DeployState.Loading)
            {
                int secs = Mathf.FloorToInt(Time.unscaledTime - deployStateStart);
                string line = $"{deployStage}   {secs / 60}:{secs % 60:00}";
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

            if (squadCountText != null)
            {
                squadCountText.text = "3 / 4 PLAYERS";
            }
        }

        private void OnInviteClicked()
        {
            PlayClickSound();
            Debug.Log("[YellowHUD] Invite Player Clicked!");
            ShowTacticalNotice("PARTY INVITATION LINK COPIED TO CLIPBOARD");
        }

        // ----------------------------------------------------
        // 5. EMOTE BAR
        // ----------------------------------------------------
        private void BindEmotes()
        {
            if (btnEmoteSalute != null) btnEmoteSalute.onClick.AddListener(() => TriggerEmote(1));
            if (btnEmoteWave != null) btnEmoteWave.onClick.AddListener(() => TriggerEmote(2));
            if (btnEmoteFlex != null) btnEmoteFlex.onClick.AddListener(() => TriggerEmote(3));
            if (btnEmoteVictory != null) btnEmoteVictory.onClick.AddListener(() => TriggerEmote(4));
        }

        private void TriggerEmote(int id)
        {
            PlayClickSound();
            Debug.Log($"[YellowHUD] Triggering Emote #{id}");
            LobbyCharacterPreviewManager.Instance?.PlayEmote(id);
        }

        // ----------------------------------------------------
        // 6. PROMO & FOOTER
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
