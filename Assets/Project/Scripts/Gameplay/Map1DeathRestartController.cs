using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using JUTPS;

namespace Railbound.Gameplay
{
    /// <summary>
    /// Manages player death detection and automatic scene restart for MAP-2.
    /// Supports both local offline player (TPS Character) and networked player (JUNetworkPlayer).
    /// Shows the DeathScreenUI (ELIMINATED + countdown) before cleanly reloading the map.
    /// </summary>
    public class Map1DeathRestartController : MonoBehaviour
    {
        public static Map1DeathRestartController Instance { get; private set; }

        /// <summary>
        /// The physics step the whole game runs at. The camera / slow-motion scripts and the Project Settings use the same
        /// value, so jump arcs, gravity and step-up feel identical on every spawn.
        /// </summary>
        public const float GameFixedTimestep = 0.015f;

        [Header("Restart Configuration")]
        [Tooltip("Time in seconds to wait after player death before restarting MAP-2.")]
        [SerializeField] private float restartDelay = 3.0f;

        [Tooltip("Slow down time slightly upon death for dramatic tactical feedback.")]
        [SerializeField] private bool enableDeathSlowMotion = true;

        [SerializeField] private float deathTimeScale = 0.45f;

        [Header("Status (Read Only)")]
        [SerializeField] private GameObject activePlayer;
        [SerializeField] private JUHealth activeHealth;
        [SerializeField] private bool isRestarting = false;
        [SerializeField] private float restartTimer = 0f;

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
            Time.fixedDeltaTime = GameFixedTimestep;
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
            Time.fixedDeltaTime = GameFixedTimestep;
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

            // Battle Royale: the match coordinator owns elimination (placement screen, spectating, results)
            if (Railbound.BR.BRMatchCoordinator.Instance != null)
            {
                DeathAudio.Play(activePlayer != null ? activePlayer.transform.position + Vector3.up : Vector3.zero);
                return;
            }

            restartTimer = restartDelay;

            Debug.LogWarning($"[Map1DeathRestartController] Local player killed! Initiating MAP-2 restart countdown ({restartDelay:F1}s)...");

            // elimination sound: 3D at the body, local player only, closes slowly after the respawn
            DeathAudio.Play(activePlayer != null ? activePlayer.transform.position + Vector3.up : Vector3.zero);
            Railbound.UI.DeathScreenUI.Show(restartDelay, () => restartTimer, SceneManager.GetActiveScene().name);
            StartCoroutine(DeathRestartCoroutine());
        }

        private IEnumerator DeathRestartCoroutine()
        {
            if (enableDeathSlowMotion)
            {
                Time.timeScale = deathTimeScale;
                Time.fixedDeltaTime = GameFixedTimestep * Time.timeScale;
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
            Time.fixedDeltaTime = GameFixedTimestep;

            // Cleanly shut down Netcode client session if active to avoid socket leaks
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
            {
                Debug.Log("[Map1DeathRestartController] Disconnecting Netcode client prior to restarting MAP-2...");
                NetworkManager.Singleton.Shutdown();
            }

            // Restore cursor state
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            string sceneName = SceneManager.GetActiveScene().name;
            if (string.IsNullOrEmpty(sceneName)) sceneName = "MAP-2";

            Debug.Log($"[Map1DeathRestartController] Restarting scene '{sceneName}' now...");
            SceneManager.LoadScene(sceneName);
        }
    }
}

