using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Nakama;
using TMPro;

namespace Railbound.Networking
{
    /// <summary>
    /// Bridges the Lobby Deploy button with Nakama match creation and Unity Netcode for GameObjects.
    /// When the user clicks DEPLOY:
    /// 1. Connects/ensures the Nakama socket and creates a real-time Nakama match.
    /// 2. Starts Unity Netcode as Host (spawning the local player).
    /// 3. Seamlessly transitions the scene to MAP-1 with synchronized Netcode scene management.
    /// </summary>
    public class LobbyMatchmakingController : MonoBehaviour
    {
        public static LobbyMatchmakingController Instance { get; private set; }

        [Header("Scene Configuration")]
        [Tooltip("The target gameplay scene to load upon match deployment.")]
        [SerializeField] private string targetGameplayScene = "MAP-1";

        [Header("Netcode Configuration")]
        [Tooltip("Default transport port for Netcode.")]
        [SerializeField] private ushort netcodePort = 7777;
        [Tooltip("Prefab for player character (from Assets/SandBox/Networking/Prefabs/TPS Character.prefab).")]
        [SerializeField] private GameObject playerPrefab;

        [Header("Matchmaking")]
        [Tooltip("How long to wait for the Nakama backend before continuing with a local match.")]
        [SerializeField] private float nakamaTimeoutSeconds = 8f;

        [Header("UI Feedback")]
        [Tooltip("Optional text field to display matchmaking / deployment status.")]
        [SerializeField] private TextMeshProUGUI statusText;

        public string CurrentMatchId { get; private set; }
        public bool IsDeploying { get; private set; }

        /// <summary>Raised with every deployment status line (e.g. "CREATING NAKAMA MATCH...", "LOADING MAP-1...", "DEPLOY FAILED", "CANCELLED").</summary>
        public event Action<string> StatusChanged;

        /// <summary>True while a deployment can still be cancelled (before the gameplay scene starts loading).</summary>
        public bool CanCancel => IsDeploying && !_sceneLoadStarted;

        private bool _cancelRequested;
        private bool _sceneLoadStarted;
        private bool _startedHostHere;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            LoadPlayerPrefabIfNull();
        }

        private void LoadPlayerPrefabIfNull()
        {
            if (playerPrefab == null)
            {
#if UNITY_EDITOR
                playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/SandBox/Networking/Prefabs/TPS Character.prefab");
#else
                playerPrefab = Resources.Load<GameObject>("Prefabs/TPS Character");
#endif
            }
        }

        /// <summary>
        /// Initiates the full deployment flow: creates Nakama match, starts Netcode host, and loads MAP-1.
        /// </summary>
        public async void Deploy()
        {
            if (IsDeploying)
            {
                Debug.LogWarning("[LobbyMatchmaker] Deployment is already in progress.");
                return;
            }

            IsDeploying = true;
            _cancelRequested = false;
            _sceneLoadStarted = false;
            _startedHostHere = false;
            UpdateStatus("CREATING NAKAMA MATCH...");

            try
            {
                // 1. Create Nakama Match (bounded wait: an unreachable backend must not freeze the lobby for a minute,
                //    and the player can cancel while we wait; on timeout we continue with a local match like on any Nakama error)
                var nakamaTask = CreateNakamaMatchAsync();
                float waited = 0f;
                while (!nakamaTask.IsCompleted && !_cancelRequested && waited < nakamaTimeoutSeconds)
                {
                    await Task.Delay(100);
                    waited += 0.1f;
                }
                if (_cancelRequested) { AbortDeploy(); return; }
                if (!nakamaTask.IsCompleted)
                {
                    Debug.LogWarning($"[LobbyMatchmaker] Nakama did not answer within {nakamaTimeoutSeconds:0}s; continuing with a local match.");
                    CurrentMatchId = $"local_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
                }

                // 2. Prepare Netcode NetworkManager & Start Host
                UpdateStatus("STARTING NETCODE HOST...");
                bool alreadyRunning = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
                if (!EnsureNetcodeHost())
                {
                    throw new InvalidOperationException("Failed to start Unity Netcode Host.");
                }
                _startedHostHere = !alreadyRunning;
                if (_cancelRequested) { AbortDeploy(); return; }

                // 3. Load Target Gameplay Scene (MAP-1)
                _sceneLoadStarted = true;
                UpdateStatus("LOADING MAP-1...");
                await Task.Delay(450);   // let the lobby's loading overlay fade in before the scene load takes over
                LoadGameplayScene();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LobbyMatchmaker] Deployment failed: {ex.Message}\n{ex.StackTrace}");
                UpdateStatus("DEPLOY FAILED");
                IsDeploying = false;
            }
        }

        /// <summary>Cancels a deployment that has not started loading the gameplay scene yet.</summary>
        public void CancelDeploy()
        {
            if (!CanCancel) return;
            _cancelRequested = true;
            // the awaited steps are short; Deploy() notices the flag right after the current step finishes
        }

        private void AbortDeploy()
        {
            if (_startedHostHere && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }
            _startedHostHere = false;
            _cancelRequested = false;
            IsDeploying = false;
            UpdateStatus("CANCELLED");
        }

        private async Task CreateNakamaMatchAsync()
        {
            try
            {
                // Prefer project AuthenticationService singleton if present
                if (AuthenticationService.Instance != null && AuthenticationService.Instance.IsAuthenticated)
                {
                    var socket = await AuthenticationService.Instance.EnsureSocketAsync();
                    if (socket != null && socket.IsConnected)
                    {
                        string matchName = $"convoy_raid_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
                        var match = await socket.CreateMatchAsync(matchName);
                        CurrentMatchId = match.Id;
                        Debug.Log($"[LobbyMatchmaker] Nakama match successfully created! Match ID: {CurrentMatchId}");
                        return;
                    }
                }

                // Fallback: Standalone device authentication on Nakama server
                Debug.Log("[LobbyMatchmaker] AuthenticationService not active or socket offline; attempting direct Nakama socket...");
                var client = new Client(ServerConfig.NakamaScheme, ServerConfig.NakamaHost, ServerConfig.NakamaPort,
                    ServerConfig.NakamaServerKey, UnityWebRequestAdapter.Instance);
                string deviceId = SystemInfo.deviceUniqueIdentifier;
                if (string.IsNullOrEmpty(deviceId) || deviceId == SystemInfo.unsupportedIdentifier)
                    deviceId = Guid.NewGuid().ToString("N");

                var session = await client.AuthenticateDeviceAsync(deviceId);
                var socketFallback = client.NewSocket();
                await socketFallback.ConnectAsync(session, true);
                var directMatch = await socketFallback.CreateMatchAsync();
                CurrentMatchId = directMatch.Id;
                Debug.Log($"[LobbyMatchmaker] Nakama match created via direct socket! Match ID: {CurrentMatchId}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyMatchmaker] Nakama match creation notice: {ex.Message}. Proceeding to local Netcode match.");
                CurrentMatchId = $"local_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
            }
        }

        private bool EnsureNetcodeHost()
        {
            var netMgr = NetworkManager.Singleton;
            if (netMgr == null)
            {
                // The lobby scene has no NetworkManager. A runtime-created one has no NetworkConfig (it threw here) and would
                // not know the gameplay scene's prefabs, so do not host from the lobby: hand off to the gameplay scene, whose
                // own NetworkManager / TacticalMultiplayerOverlay connects the player to the game server.
                Debug.Log("[LobbyMatchmaker] No NetworkManager in the lobby; handing off to the gameplay scene to connect.");
                return true;
            }

            LoadPlayerPrefabIfNull();
            if (playerPrefab != null)
            {
                netMgr.NetworkConfig.PlayerPrefab = playerPrefab;
            }

            netMgr.NetworkConfig.EnableSceneManagement = true;

            if (!netMgr.IsListening)
            {
                bool hostStarted = netMgr.StartHost();
                if (hostStarted)
                {
                    Debug.Log("[LobbyMatchmaker] Unity Netcode Host successfully started!");
                }
                else
                {
                    Debug.LogError("[LobbyMatchmaker] Failed to start Unity Netcode Host.");
                    return false;
                }
            }
            else
            {
                Debug.Log("[LobbyMatchmaker] Netcode is already running (Host/Server).");
            }

            return true;
        }

        private void LoadGameplayScene()
        {
            var netMgr = NetworkManager.Singleton;
            if (netMgr != null && netMgr.IsListening && netMgr.IsServer)
            {
                Debug.Log($"[LobbyMatchmaker] Loading scene '{targetGameplayScene}' via Netcode SceneManager.");
                netMgr.SceneManager.LoadScene(targetGameplayScene, LoadSceneMode.Single);
            }
            else
            {
                Debug.Log($"[LobbyMatchmaker] Loading scene '{targetGameplayScene}' via Unity SceneManager.");
                SceneManager.LoadSceneAsync(targetGameplayScene);   // async: the loading overlay keeps animating
            }
        }

        private void UpdateStatus(string message)
        {
            Debug.Log($"[LobbyMatchmaker] {message}");
            if (statusText != null)
            {
                statusText.text = message;
            }
            StatusChanged?.Invoke(message);
        }
    }
}
