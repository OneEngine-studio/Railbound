using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Michsky.UI.Reach;
using Railbound.Networking;

namespace Railbound.UI
{
    /// <summary>
    /// Modern UI Toolkit Controller for the RAILBOUND Yellow Line HUD Lobby.
    /// Manages VisualElements defined in RailboundLobby.uxml and RailboundLobby.uss:
    /// - Live profile sync (AuthenticationService & PlayerProfileService)
    /// - Operator switcher & floating 3D tag
    /// - 6-tab navigation menu with indicator states
    /// - Tactical deploy button & Fill Squad toggle
    /// - Squad roster & readiness states
    /// - Emote action bar & audio sound feedback
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public class RailboundLobbyUIDocController : MonoBehaviour
    {
        public static RailboundLobbyUIDocController Instance { get; private set; }

        [Header("Audio SFX")]
        [SerializeField] private AudioClip clickSound;
        [SerializeField] private AudioClip hoverSound;
        [SerializeField] private AudioClip deploySound;
        private AudioSource audioSource;

        private UIDocument uiDocument;
        private VisualElement root;

        // Header Elements
        private Label headerPlayerName;
        private Label headerPlayerLevel;
        private VisualElement headerXpFill;
        private Label headerCoinsText;
        private Label headerStarsText;
        private Label headerOpName;
        private Label headerOpRole;
        private Label floatingOpName;
        private Button btnPrevOp;
        private Button btnNextOp;
        private Button btnSettings;

        // Nav Tab Elements
        private Button[] navTabs;
        private VisualElement[] navIndicators;
        private int currentNavIndex = 0;

        // Deploy & Mode Elements
        private Button btnDeploy;
        private Label deployLabel;
        private Button btnFillSquadToggle;
        private VisualElement fillSquadKnob;
        private bool isFillSquadOn = true;
        private bool isDeploying = false;

        // Squad Elements
        private Label squadCountText;
        private Label squadNick0;
        private Label squadLevel0;
        private Button btnInvitePlayer;

        // Emotes
        private Button btnEmoteSalute;
        private Button btnEmoteGreet;
        private Button btnEmoteFlex;
        private Button btnEmoteVictory;

        // Season Promo
        private VisualElement seasonPromo;

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
            audioSource.spatialBlend = 0f;
        }

        private void OnEnable()
        {
            uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null) return;
            root = uiDocument.rootVisualElement;
            if (root == null) return;

            QueryElements();
            BindEvents();
            SyncProfileData();
            UpdateOperatorDisplay();

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
                OnDeployClicked();
            }
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
                OnSettingsOrBackClicked();
            }
            else if (Input.GetKeyDown(KeyCode.F1))
            {
                OnInviteClicked();
            }
        }

        private void QueryElements()
        {
            // Header
            headerPlayerName = root.Q<Label>("HeaderPlayerName");
            headerPlayerLevel = root.Q<Label>("HeaderPlayerLevel");
            headerXpFill = root.Q<VisualElement>("HeaderXpFill");
            headerCoinsText = root.Q<Label>("HeaderCoinsText");
            headerStarsText = root.Q<Label>("HeaderStarsText");
            headerOpName = root.Q<Label>("HeaderOpName");
            headerOpRole = root.Q<Label>("HeaderOpRole");
            floatingOpName = root.Q<Label>("FloatingOpName");
            btnPrevOp = root.Q<Button>("BtnPrevOp");
            btnNextOp = root.Q<Button>("BtnNextOp");
            btnSettings = root.Q<Button>("BtnSettings");

            // Nav Tabs
            navTabs = new Button[]
            {
                root.Q<Button>("NavPlay"),
                root.Q<Button>("NavOperators"),
                root.Q<Button>("NavLoadout"),
                root.Q<Button>("NavBattlePass"),
                root.Q<Button>("NavChallenges"),
                root.Q<Button>("NavStore")
            };

            navIndicators = new VisualElement[]
            {
                root.Q<VisualElement>("IndPlay"),
                root.Q<VisualElement>("IndOperators"),
                root.Q<VisualElement>("IndLoadout"),
                root.Q<VisualElement>("IndBattlePass"),
                root.Q<VisualElement>("IndChallenges"),
                root.Q<VisualElement>("IndStore")
            };

            // Game Mode & Deploy
            btnDeploy = root.Q<Button>("BtnDeploy");
            deployLabel = root.Q<Label>("DeployLabel");
            btnFillSquadToggle = root.Q<Button>("BtnFillSquadToggle");
            fillSquadKnob = root.Q<VisualElement>("FillSquadKnob");

            // Squad
            squadCountText = root.Q<Label>("SquadCountText");
            squadNick0 = root.Q<Label>("SquadNick0");
            squadLevel0 = root.Q<Label>("SquadLevel0");
            btnInvitePlayer = root.Q<Button>("BtnInvitePlayer");

            // Emotes
            btnEmoteSalute = root.Q<Button>("BtnEmoteSalute");
            btnEmoteGreet = root.Q<Button>("BtnEmoteGreet");
            btnEmoteFlex = root.Q<Button>("BtnEmoteFlex");
            btnEmoteVictory = root.Q<Button>("BtnEmoteVictory");

            // Promo
            seasonPromo = root.Q<VisualElement>("SeasonPromo");
        }

        private void BindEvents()
        {
            // Operator cycle
            if (btnPrevOp != null)
            {
                btnPrevOp.clicked += () =>
                {
                    PlayClickSound();
                    LobbyCharacterPreviewManager.Instance?.PreviousOperator();
                };
                AddHoverAudio(btnPrevOp);
            }

            if (btnNextOp != null)
            {
                btnNextOp.clicked += () =>
                {
                    PlayClickSound();
                    LobbyCharacterPreviewManager.Instance?.NextOperator();
                };
                AddHoverAudio(btnNextOp);
            }

            if (btnSettings != null)
            {
                btnSettings.clicked += () =>
                {
                    PlayClickSound();
                    OnSettingsOrBackClicked();
                };
                AddHoverAudio(btnSettings);
            }

            // Nav tabs
            for (int i = 0; i < navTabs.Length; i++)
            {
                int index = i;
                if (navTabs[i] != null)
                {
                    navTabs[i].clicked += () => SelectNavTab(index);
                    AddHoverAudio(navTabs[i]);
                }
            }

            // Fill Squad
            if (btnFillSquadToggle != null)
            {
                btnFillSquadToggle.clicked += ToggleFillSquad;
                AddHoverAudio(btnFillSquadToggle);
            }

            // Deploy
            if (btnDeploy != null)
            {
                btnDeploy.clicked += OnDeployClicked;
                AddHoverAudio(btnDeploy);
            }

            // Invite
            if (btnInvitePlayer != null)
            {
                btnInvitePlayer.clicked += OnInviteClicked;
                AddHoverAudio(btnInvitePlayer);
            }

            // Emotes
            if (btnEmoteSalute != null) { btnEmoteSalute.clicked += () => TriggerEmote(1); AddHoverAudio(btnEmoteSalute); }
            if (btnEmoteGreet != null) { btnEmoteGreet.clicked += () => TriggerEmote(2); AddHoverAudio(btnEmoteGreet); }
            if (btnEmoteFlex != null) { btnEmoteFlex.clicked += () => TriggerEmote(3); AddHoverAudio(btnEmoteFlex); }
            if (btnEmoteVictory != null) { btnEmoteVictory.clicked += () => TriggerEmote(4); AddHoverAudio(btnEmoteVictory); }

            // Promo click
            if (seasonPromo != null)
            {
                seasonPromo.RegisterCallback<ClickEvent>(evt =>
                {
                    SelectNavTab(3); // Open Battle Pass
                });
            }
        }

        private void AddHoverAudio(VisualElement elem)
        {
            if (elem == null) return;
            elem.RegisterCallback<PointerEnterEvent>(evt => PlayHoverSound());
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
                Debug.Log($"[UI Toolkit HUD] Profile fallback: {ex.Message}");
            }

            if (headerPlayerName != null) headerPlayerName.text = username.ToUpper();
            if (headerPlayerLevel != null) headerPlayerLevel.text = levelStr;
            if (headerXpFill != null) headerXpFill.style.width = Length.Percent(xpProgress * 100f);

            if (headerCoinsText != null) headerCoinsText.text = "2,450";
            if (headerStarsText != null) headerStarsText.text = "850";

            if (squadNick0 != null) squadNick0.text = username.ToUpper();
            if (squadLevel0 != null) squadLevel0.text = levelStr;
        }

        private void UpdateOperatorDisplay()
        {
            var op = LobbyCharacterPreviewManager.Instance?.CurrentOperator;
            string opName = op != null ? op.displayName : "MIL-R5 GHOST";
            string opRole = op != null ? $"// {op.roleCategory.ToUpper()}" : "// ASSAULT / SPEC-OPS";

            if (headerOpName != null) headerOpName.text = opName;
            if (headerOpRole != null) headerOpRole.text = opRole;
            if (floatingOpName != null) floatingOpName.text = opName;
        }

        public void SelectNavTab(int index)
        {
            PlayClickSound();
            currentNavIndex = index;

            for (int i = 0; i < navTabs.Length; i++)
            {
                bool isSelected = (i == currentNavIndex);
                if (navTabs[i] != null)
                {
                    if (isSelected) navTabs[i].AddToClassList("nav-tab-selected");
                    else navTabs[i].RemoveFromClassList("nav-tab-selected");
                }

                if (navIndicators != null && i < navIndicators.Length && navIndicators[i] != null)
                {
                    navIndicators[i].style.display = isSelected ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }

            switch (index)
            {
                case 0: Debug.Log("[UI Toolkit HUD] Nav: PLAY (Main Lobby)"); break;
                case 1:
                    Debug.Log("[UI Toolkit HUD] Nav: OPERATORS - Cycling");
                    LobbyCharacterPreviewManager.Instance?.NextOperator();
                    break;
                case 2:
                    Debug.Log("[UI Toolkit HUD] Nav: LOADOUT");
                    LobbyCustomizationUI.Instance?.SendMessage("ToggleSkillsCard", SendMessageOptions.DontRequireReceiver);
                    break;
                case 3: Debug.Log("[UI Toolkit HUD] Nav: BATTLE PASS // SEASON 01"); break;
                case 4: Debug.Log("[UI Toolkit HUD] Nav: CHALLENGES // DAILY OPS"); break;
                case 5: Debug.Log("[UI Toolkit HUD] Nav: ARSENAL STORE"); break;
            }
        }

        private void ToggleFillSquad()
        {
            PlayClickSound();
            isFillSquadOn = !isFillSquadOn;
            if (fillSquadKnob != null)
            {
                if (isFillSquadOn)
                {
                    fillSquadKnob.RemoveFromClassList("toggle-knob-off");
                    fillSquadKnob.AddToClassList("toggle-knob");
                }
                else
                {
                    fillSquadKnob.RemoveFromClassList("toggle-knob");
                    fillSquadKnob.AddToClassList("toggle-knob-off");
                }
            }
            Debug.Log($"[UI Toolkit HUD] Fill Squad: {(isFillSquadOn ? "ON" : "OFF")}");
        }

        private void OnDeployClicked()
        {
            if (isDeploying) return;
            isDeploying = true;
            PlayDeploySound();
            Debug.Log("[UI Toolkit HUD] DEPLOY CLICKED! Initiating matchmaking...");

            if (deployLabel != null) deployLabel.text = "SEARCHING...";

            var bridge = ReachLobbyBridge.Instance;
            if (bridge != null)
            {
                bridge.SendMessage("HandlePlayClicked", SendMessageOptions.DontRequireReceiver);
            }
            else if (LobbyMatchmakingController.Instance != null)
            {
                LobbyMatchmakingController.Instance.Deploy();
            }
        }

        private void OnInviteClicked()
        {
            PlayClickSound();
            Debug.Log("[UI Toolkit HUD] Invite Player Clicked!");
        }

        private void TriggerEmote(int id)
        {
            PlayClickSound();
            Debug.Log($"[UI Toolkit HUD] Triggering Emote #{id}");
            LobbyCharacterPreviewManager.Instance?.PlayEmote(id);
        }

        private void OnSettingsOrBackClicked()
        {
            PlayClickSound();
            Debug.Log("[UI Toolkit HUD] Settings / Back clicked");
            var bridge = ReachLobbyBridge.Instance;
            if (bridge != null)
            {
                bridge.SendMessage("HandleGoBackClicked", SendMessageOptions.DontRequireReceiver);
            }
        }
    }
}
