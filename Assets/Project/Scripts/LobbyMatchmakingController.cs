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

        [Header("UI Feedback")]
        [Tooltip("Optional text field to display matchmaking / deployment status.")]
        [SerializeField] private TextMeshProUGUI statusText;

        public string CurrentMatchId { get; private set; }
        public bool IsDeploying { get; private set; }

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
                playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SandBox/Networking/Prefabs/TPS Character.prefab");
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
            UpdateStatus("CREATING NAKAMA MATCH...");

            try
            {
                // 1. Create Nakama Match
                await CreateNakamaMatchAsync();

                // 2. Prepare Netcode NetworkManager & Start Host
                UpdateStatus("STARTING NETCODE HOST...");
                EnsureNetcodeHost();

                // 3. Load Target Gameplay Scene (MAP-1)
                UpdateStatus("LOADING MAP-1...");
                LoadGameplayScene();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LobbyMatchmaker] Deployment failed: {ex.Message}\n{ex.StackTrace}");
                UpdateStatus("DEPLOY FAILED");
                IsDeploying = false;
            }
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
                string scheme = "http";
                string host = "161.118.255.172";
                int port = 7350;
                string serverKey = "defaultkey";

                var client = new Client(scheme, host, port, serverKey, UnityWebRequestAdapter.Instance);
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

        private void EnsureNetcodeHost()
        {
            var netMgr = NetworkManager.Singleton;
            if (netMgr == null)
            {
                Debug.Log("[LobbyMatchmaker] NetworkManager singleton not found; instantiating runtime NetworkManager...");
                var netMgrGo = new GameObject("NetworkManager");
                DontDestroyOnLoad(netMgrGo);

                netMgr = netMgrGo.AddComponent<NetworkManager>();
                var transport = netMgrGo.AddComponent<UnityTransport>();
                transport.SetConnectionData("127.0.0.1", netcodePort, "0.0.0.0");
                netMgrGo.AddComponent<NetworkHotkeyManager>();
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
                }
            }
            else
            {
                Debug.Log("[LobbyMatchmaker] Netcode is already running (Host/Server).");
            }
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
                SceneManager.LoadScene(targetGameplayScene);
            }
        }

        private void UpdateStatus(string message)
        {
            Debug.Log($"[LobbyMatchmaker] {message}");
            if (statusText != null)
            {
                statusText.text = message;
            }
        }
    }
}
