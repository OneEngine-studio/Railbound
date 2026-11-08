using System;
using System.Linq;
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
    /// 3. Seamlessly transitions the scene to MAP-2 with synchronized Netcode scene management.
    /// </summary>
    public class LobbyMatchmakingController : MonoBehaviour
    {
        public static LobbyMatchmakingController Instance { get; private set; }

        [Header("Scene Configuration")]
        [Tooltip("The target gameplay scene to load upon match deployment.")]
        [SerializeField] private string targetGameplayScene = "MAP-2";

        [Header("Netcode Configuration")]
        [Tooltip("Default transport port for Netcode.")]
        [SerializeField] private ushort netcodePort = 7777;
        [Tooltip("Seconds the client may take to connect to the allocated game server before the deployment fails.")]
        [SerializeField] private float serverConnectTimeoutSeconds = 20f;
        [Tooltip("Prefab for player character (from Assets/SandBox/Networking/Prefabs/TPS Character.prefab).")]
        [SerializeField] private GameObject playerPrefab;

        [Header("Matchmaking")]
        [Tooltip("How long to wait for the Nakama backend before continuing with a local match.")]
        [SerializeField] private float nakamaTimeoutSeconds = 8f;

        [Header("Queue (looking for other players before the match starts)")]
        [Tooltip("The soft-launch window: how long DEPLOY searches for other players. When it runs out the match starts anyway and bots fill the empty slots.")]
        [SerializeField] private float queueSeconds = 30f;
        [SerializeField] private int queueMinPlayers = 2;
        [Tooltip("A lobby of this many real players starts the match immediately.")]
        [SerializeField] private int queueMaxPlayers = 10;

        [Header("Game server (Agones, allocated through Nakama)")]
        [Tooltip("How long to wait for Nakama to hand out a game server for the match.")]
        [SerializeField] private float allocationTimeoutSeconds = 45f;
        [Tooltip("If no server can be allocated (Nakama module missing, fleet empty), connect to the default server address instead of failing.")]
        [SerializeField] private bool legacyServerFallback = true;

        [Header("UI Feedback")]
        [Tooltip("Optional text field to display matchmaking / deployment status.")]
        [SerializeField] private TextMeshProUGUI statusText;

        public string CurrentMatchId { get; private set; }
        public bool IsDeploying { get; private set; }

        /// <summary>Raised with every deployment status line (e.g. "CREATING NAKAMA MATCH...", "LOADING MAP-2...", "DEPLOY FAILED", "CANCELLED").</summary>
        public event Action<string> StatusChanged;

        /// <summary>True while a deployment can still be cancelled (before the gameplay scene starts loading).</summary>
        public bool CanCancel => IsDeploying && !_sceneLoadStarted;

        private string _queueMatchId = "";
        private int _queuePlayers = 1;
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
        /// Initiates the full deployment flow: creates Nakama match, starts Netcode host, and loads MAP-2.
        /// </summary>
        public async void Deploy()
        {
            if (IsDeploying)
            {
                Debug.LogWarning("[LobbyMatchmaker] Deployment is already in progress.");
                return;
            }

            IsDeploying = true;
            _queueMatchId = Guid.NewGuid().ToString("N");   // a solo match; a matchmaker lobby replaces it with a shared id
            _queuePlayers = 1;
            MatchConnection.Clear();
            _cancelRequested = false;
            _sceneLoadStarted = false;
            _startedHostHere = false;

            try
            {
                // 0. Queue: look for other players for up to queueSeconds (skipped when Nakama is not reachable)
                await QueueForPlayersAsync();
                if (_cancelRequested) { AbortDeploy(); return; }

                // 0b. Game server: Nakama allocates one Agones server per match and everybody of the match gets the same one
                if (!await AllocateServerAsync())
                {
                    if (_cancelRequested) { AbortDeploy(); return; }
                    throw new InvalidOperationException("No game server available.");
                }

                UpdateStatus("CREATING NAKAMA MATCH...");

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

                // 2b. A client of the allocated server: wait until the server accepted us (it then loads the map on this client)
                if (MatchConnection.HasAllocation && NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
                {
                    if (!await WaitForServerConnectionAsync())
                    {
                        if (_cancelRequested) { AbortDeploy(); return; }
                        throw new InvalidOperationException($"Could not connect to the game server {MatchConnection.Host}:{MatchConnection.Port}.");
                    }
                }

                // 3. Load Target Gameplay Scene (MAP-2)
                _sceneLoadStarted = true;
                UpdateStatus("LOADING MAP-2...");
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

        /// <summary>
        /// Puts the player into the Nakama matchmaker and waits until a lobby forms, the soft-launch window runs out, or the player cancels.
        /// A full or partial lobby and a timeout all lead to the same next step; the match coordinator on the game server fills the
        /// remaining slots with bots. Without a reachable Nakama backend there is nobody to wait for, so this returns at once.
        /// </summary>
        private async Task QueueForPlayersAsync()
        {
            ISocket socket = null;
            try
            {
                if (AuthenticationService.Instance != null && AuthenticationService.Instance.IsAuthenticated)
                    socket = await AuthenticationService.Instance.EnsureSocketAsync();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyMatchmaker] Queue unavailable ({ex.Message}); starting without waiting for other players.");
            }
            if (socket == null || !socket.IsConnected)
            {
                Debug.Log("[LobbyMatchmaker] No Nakama socket: skipping the player queue (bots will fill the match).");
                return;
            }

            var matched = new TaskCompletionSource<IMatchmakerMatched>();
            Action<IMatchmakerMatched> onMatched = m => matched.TrySetResult(m);
            socket.ReceivedMatchmakerMatched += onMatched;
            IMatchmakerTicket ticket = null;
            try
            {
                ticket = await socket.AddMatchmakerAsync("*", queueMinPlayers, queueMaxPlayers);
                float start = Time.realtimeSinceStartup;
                while (!matched.Task.IsCompleted && !_cancelRequested)
                {
                    float left = queueSeconds - (Time.realtimeSinceStartup - start);
                    if (left <= 0f) break;
                    UpdateStatus($"SEARCHING FOR PLAYERS  {Mathf.CeilToInt(left)}s");
                    await Task.Delay(250);
                }

                if (matched.Task.IsCompleted)
                {
                    // every member of a matchmaker lobby derives the same match id from the member list
                    var ids = matched.Task.Result.Users.Select(u => u.Presence.UserId).OrderBy(x => x, StringComparer.Ordinal).ToArray();
                    using (var sha = System.Security.Cryptography.SHA1.Create())
                    {
                        byte[] hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(string.Join("|", ids)));
                        _queueMatchId = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant().Substring(0, 24);
                    }
                    _queuePlayers = ids.Length;
                    UpdateStatus("LOBBY FOUND");
                    Debug.Log($"[LobbyMatchmaker] Matchmaker formed a lobby of {matched.Task.Result.Users.Count()} players.");
                    await Task.Delay(600);
                }
                else if (!_cancelRequested)
                {
                    UpdateStatus("NO FULL LOBBY - BOTS WILL FILL THE MATCH");
                    Debug.Log("[LobbyMatchmaker] Queue window elapsed; continuing with bot backfill.");
                    await Task.Delay(900);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyMatchmaker] Matchmaker queue failed ({ex.Message}); continuing without it.");
            }
            finally
            {
                socket.ReceivedMatchmakerMatched -= onMatched;
                if (ticket != null && !matched.Task.IsCompleted)
                {
                    try { await socket.RemoveMatchmakerAsync(ticket); }
                    catch (Exception) { /* the ticket expires on its own */ }
                }
            }
        }

        /// <summary>
        /// Gets the dedicated server of this match from Nakama and stores it in <see cref="MatchConnection"/>.
        /// Returns false when that failed and the legacy default server is not allowed (or the player cancelled).
        /// </summary>
        private async Task<bool> AllocateServerAsync()
        {
            var auth = AuthenticationService.Instance;
            if (auth == null || !auth.IsAuthenticated)
            {
                Debug.Log("[LobbyMatchmaker] Not signed in: using the default server address.");
                return true;
            }

            UpdateStatus("REQUESTING GAME SERVER...");
            var allocation = await ServerAllocationClient.AllocateAsync(_queueMatchId, _queuePlayers, allocationTimeoutSeconds, () => _cancelRequested, UpdateStatus);
            if (_cancelRequested) return false;

            if (allocation.HasValue)
            {
                MatchConnection.Set(_queueMatchId, allocation.Value.Host, allocation.Value.Port, allocation.Value.ServerName);
                UpdateStatus("GAME SERVER READY");
                await Task.Delay(500);
                return true;
            }

            if (legacyServerFallback)
            {
                Debug.LogWarning("[LobbyMatchmaker] No allocated server; falling back to the default server address.");
                return true;
            }
            return false;
        }

        /// <summary>Waits until the Netcode client is connected to the allocated game server. On failure it shuts the client down and forgets the allocation.</summary>
        private async Task<bool> WaitForServerConnectionAsync()
        {
            var nm = NetworkManager.Singleton;
            string server = MatchConnection.Host + ":" + MatchConnection.Port;
            float waited = 0f;
            while (nm != null && nm.IsListening && !nm.IsConnectedClient && !_cancelRequested && waited < serverConnectTimeoutSeconds)
            {
                UpdateStatus($"CONNECTING TO GAME SERVER  {server}  {Mathf.CeilToInt(serverConnectTimeoutSeconds - waited)}s");
                await Task.Delay(250);
                waited += 0.25f;
            }

            if (nm != null && nm.IsConnectedClient)
            {
                UpdateStatus("CONNECTED TO GAME SERVER");
                Debug.Log($"[LobbyMatchmaker] Connected to the game server {server} as client {nm.LocalClientId}.");
                return true;
            }

            Debug.LogWarning($"[LobbyMatchmaker] Connection to {server} failed or timed out after {waited:0}s.");
            if (nm != null && nm.IsListening) nm.Shutdown();
            MatchConnection.Clear();
            return false;
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
            if (MatchConnection.HasAllocation)
            {
                // Nakama allocated a dedicated game server for this match. Never host a second, local copy of the match here:
                // connect to the allocated server as a client. The NetworkManager that lives on since the login scene is the one
                // that does it (the one in the gameplay scene is a duplicate and gets destroyed), and the server's Netcode scene
                // management then loads the map on this client.
                var clientMgr = NetworkManager.Singleton;
                if (clientMgr == null)
                {
                    Debug.Log("[LobbyMatchmaker] No NetworkManager in the lobby; handing off to the gameplay scene to connect.");
                    return true;
                }
                if (clientMgr.IsListening)
                {
                    Debug.Log("[LobbyMatchmaker] Netcode is already running; not starting a second client.");
                    return true;
                }

                var clientTransport = clientMgr.GetComponent<UnityTransport>();
                if (clientTransport == null)
                {
                    Debug.LogError("[LobbyMatchmaker] The NetworkManager has no UnityTransport; cannot connect to the game server.");
                    return false;
                }

                LoadPlayerPrefabIfNull();
                if (playerPrefab != null) clientMgr.NetworkConfig.PlayerPrefab = playerPrefab;
                clientMgr.NetworkConfig.EnableSceneManagement = true;
                clientTransport.SetConnectionData(MatchConnection.Host, MatchConnection.Port);

                if (!clientMgr.StartClient())
                {
                    Debug.LogError($"[LobbyMatchmaker] Netcode client failed to start for {MatchConnection.Host}:{MatchConnection.Port}.");
                    return false;
                }
                Debug.Log($"[LobbyMatchmaker] Netcode client started -> dedicated server {MatchConnection.Host}:{MatchConnection.Port} (not hosting).");
                return true;
            }

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
            if (netMgr != null && netMgr.IsClient && !netMgr.IsServer && netMgr.IsConnectedClient)
            {
                // Connected to a dedicated server with scene management: the server tells this client which scene to load.
                // Loading it here as well would load the map twice and break the scene synchronisation.
                Debug.Log($"[LobbyMatchmaker] Connected to the game server; waiting for it to load '{targetGameplayScene}' on this client.");
                return;
            }
            if (netMgr != null && netMgr.IsListening && netMgr.IsServer)
            {
                Debug.Log($"[LobbyMatchmaker] Loading scene '{targetGameplayScene}' via Netcode SceneManager.");
                netMgr.SceneManager.LoadScene(targetGameplayScene, LoadSceneMode.Single);
            }
            else
            {
                Debug.Log($"[LobbyMatchmaker] Loading scene '{targetGameplayScene}' through the loading screen.");
                // The loading screen loads the map and then stays up until the Netcode client is connected to the allocated server.
                // Without the LoadingScreen scene in the build it falls back to a plain async load.
                Railbound.UI.LoadingScreenController.Load(targetGameplayScene);
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
