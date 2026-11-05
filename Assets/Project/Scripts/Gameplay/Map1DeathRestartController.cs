using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using JUTPS;

namespace Railbound.Gameplay
{
    /// <summary>
    /// Manages player death detection and automatic scene restart for MAP-1.
    /// Supports both local offline player (TPS Character) and networked player (JUNetworkPlayer).
    /// Displays a tactical "KILLED IN ACTION" HUD overlay with countdown before cleanly reloading MAP-1.
    /// </summary>
    public class Map1DeathRestartController : MonoBehaviour
    {
        public static Map1DeathRestartController Instance { get; private set; }

        [Header("Restart Configuration")]
        [Tooltip("Time in seconds to wait after player death before restarting MAP-1.")]
        [SerializeField] private float restartDelay = 3.0f;

        [Tooltip("Slow down time slightly upon death for dramatic tactical feedback.")]
        [SerializeField] private bool enableDeathSlowMotion = true;

        [SerializeField] private float deathTimeScale = 0.45f;

        [Header("Status (Read Only)")]
        [SerializeField] private GameObject activePlayer;
        [SerializeField] private JUHealth activeHealth;
        [SerializeField] private bool isRestarting = false;
        [SerializeField] private float restartTimer = 0f;

        // UI Styles
        private GUIStyle _fullscreenVignetteStyle;
        private GUIStyle _bannerBoxStyle;
        private GUIStyle _subHeaderStyle;
        private GUIStyle _deathHeaderStyle;
        private GUIStyle _countdownStyle;
        private GUIStyle _detailStyle;
        private Texture2D _vignetteTex;
        private Texture2D _bannerTex;
        private bool _stylesInitialized = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // Dedicated Server / Headless Guard: Never run death restart controller on dedicated servers
            bool isHeadless = SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null;
            string[] args = Environment.GetCommandLineArgs();
            bool isServer = isHeadless || System.Array.Exists(args, a => string.Equals(a, "-server", StringComparison.OrdinalIgnoreCase) ||
                                                                        string.Equals(a, "-dedicated", StringComparison.OrdinalIgnoreCase));
            if (isServer)
            {
                Debug.Log("[Map1DeathRestartController] Headless/Dedicated server detected. Disabling DeathRestartController.");
                enabled = false;
                return;
            }

            // Ensure normal time scale upon scene load
            Time.timeScale = 1.0f;
            Time.fixedDeltaTime = 0.02f;
        }

        private void Start()
        {
            FindAndRegisterActivePlayer();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnsubscribeFromHealth();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            isRestarting = false;
            restartTimer = 0f;
            Time.timeScale = 1.0f;
            Time.fixedDeltaTime = 0.02f;
            FindAndRegisterActivePlayer();
        }

        private void Update()
        {
            if (isRestarting) return;

            // Continuously verify and track the active local player (handles dynamic Netcode spawning)
            if (activePlayer == null || !activePlayer.activeInHierarchy || activeHealth == null)
            {
                FindAndRegisterActivePlayer();
            }

            // Dual safety check: Poll IsDead and Health <= 0 in addition to event listener
            if (activeHealth != null)
            {
                if (activeHealth.IsDead || activeHealth.Health <= 0f)
                {
                    TriggerPlayerDeath();
                }
            }
        }

        /// <summary>
        /// Explicitly register the local player GameObject (called by JUNetworkPlayer or offline player).
        /// </summary>
        public void RegisterPlayer(GameObject playerGo)
        {
            if (playerGo == null) return;

            UnsubscribeFromHealth();

            activePlayer = playerGo;
            activeHealth = playerGo.GetComponent<JUHealth>();

            if (activeHealth != null)
            {
                activeHealth.OnDeath.RemoveListener(OnPlayerDied);
                activeHealth.OnDeath.AddListener(OnPlayerDied);
                Debug.Log($"[Map1DeathRestartController] Registered active player '{playerGo.name}' with JUHealth (Health: {activeHealth.Health}/{activeHealth.MaxHealth}).");
            }
        }

        private void FindAndRegisterActivePlayer()
        {
            // 1. Check if Netcode local player is spawned and active
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
            {
                var localClient = NetworkManager.Singleton.LocalClient;
                if (localClient != null && localClient.PlayerObject != null && localClient.PlayerObject.gameObject.activeInHierarchy)
                {
                    RegisterPlayer(localClient.PlayerObject.gameObject);
                    return;
                }
            }

            // 2. Check for active scene player tagged "Player"
            GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            if (taggedPlayer != null && taggedPlayer.activeInHierarchy)
            {
                RegisterPlayer(taggedPlayer);
                return;
            }

            // 3. Fallback: Search for "TPS Character" by name
            GameObject sceneChar = GameObject.Find("TPS Character");
            if (sceneChar != null && sceneChar.activeInHierarchy)
            {
                RegisterPlayer(sceneChar);
            }
        }

        private void UnsubscribeFromHealth()
        {
            if (activeHealth != null)
            {
                activeHealth.OnDeath.RemoveListener(OnPlayerDied);
            }
        }

        private void OnPlayerDied()
        {
            TriggerPlayerDeath();
        }

        /// <summary>
        /// Triggers the death restart sequence.
        /// </summary>
        public void TriggerPlayerDeath()
        {
            if (isRestarting) return;
            isRestarting = true;
            restartTimer = restartDelay;

            Debug.LogWarning($"[Map1DeathRestartController] Local player killed! Initiating MAP-1 restart countdown ({restartDelay:F1}s)...");

            StartCoroutine(DeathRestartCoroutine());
        }

        private IEnumerator DeathRestartCoroutine()
        {
            if (enableDeathSlowMotion)
            {
                Time.timeScale = deathTimeScale;
                Time.fixedDeltaTime = 0.02f * Time.timeScale;
            }

            // Count down in unscaled real time so slow-mo does not delay the actual countdown
            while (restartTimer > 0f)
            {
                restartTimer -= Time.unscaledDeltaTime;
                yield return null;
            }

            restartTimer = 0f;

            // Restore normal time scale before scene change
            Time.timeScale = 1.0f;
            Time.fixedDeltaTime = 0.02f;

            // Cleanly shut down Netcode client session if active to avoid socket leaks
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
            {
                Debug.Log("[Map1DeathRestartController] Disconnecting Netcode client prior to restarting MAP-1...");
                NetworkManager.Singleton.Shutdown();
            }

            // Restore cursor state
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            string sceneName = SceneManager.GetActiveScene().name;
            if (string.IsNullOrEmpty(sceneName)) sceneName = "MAP-1";

            Debug.Log($"[Map1DeathRestartController] Restarting scene '{sceneName}' now...");
            SceneManager.LoadScene(sceneName);
        }

        private void OnGUI()
        {
            if (!isRestarting) return;

            // Skip rendering in headless dedicated server builds
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;

            InitStyles();

            float screenW = Screen.width;
            float screenH = Screen.height;

            // 1. Semi-transparent dark red tactical vignette overlay
            if (_vignetteTex != null)
            {
                GUI.DrawTexture(new Rect(0, 0, screenW, screenH), _vignetteTex, ScaleMode.StretchToFill);
            }

            // 2. Central Tactical Banner
            float bannerW = Mathf.Min(680, screenW - 40);
            float bannerH = 220;
            float bannerX = (screenW - bannerW) * 0.5f;
            float bannerY = (screenH - bannerH) * 0.38f;

            Rect bannerRect = new Rect(bannerX, bannerY, bannerW, bannerH);
            GUI.Box(bannerRect, GUIContent.none, _bannerBoxStyle);

            GUILayout.BeginArea(new Rect(bannerX + 20, bannerY + 16, bannerW - 40, bannerH - 32));
            GUILayout.BeginVertical();

            GUILayout.Label("── MISSION FAILED ──", _subHeaderStyle);
            GUILayout.Space(2);
            GUILayout.Label("KILLED IN ACTION", _deathHeaderStyle);
            GUILayout.Space(8);

            string countdownStr = $"RESTARTING OPERATION IN {Mathf.Max(0f, restartTimer):0.0}s";
            GUILayout.Label(countdownStr, _countdownStyle);
            GUILayout.Space(6);

            GUILayout.Label($"Disconnecting from server ({ServerConfig.GameServerAddress}) & reloading MAP-1...", _detailStyle);

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            // Red/Dark tactical vignette texture
            _vignetteTex = new Texture2D(1, 1);
            _vignetteTex.SetPixel(0, 0, new Color(0.25f, 0.02f, 0.02f, 0.65f));
            _vignetteTex.Apply();

            // Dark military banner background texture
            _bannerTex = new Texture2D(1, 1);
            _bannerTex.SetPixel(0, 0, new Color(0.04f, 0.05f, 0.07f, 0.95f));
            _bannerTex.Apply();

            _bannerBoxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _bannerTex },
                padding = new RectOffset(20, 20, 20, 20)
            };

            _subHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1.0f, 0.35f, 0.35f) }
            };

            _deathHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1.0f, 0.12f, 0.12f) }
            };

            _countdownStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.0f, 0.95f, 1.0f) }
            };

            _detailStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.7f, 0.75f, 0.85f) }
            };

            _stylesInitialized = true;
        }
    }
}

