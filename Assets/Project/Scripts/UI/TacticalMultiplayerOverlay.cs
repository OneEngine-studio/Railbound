using System;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Railbound.UI
{
    /// <summary>
    /// Lightweight Tactical Multiplayer Network & Ping Indicator.
    /// Connects automatically to the dedicated server and displays only a clean, minimal
    /// ping readout in the top-right corner of the screen without intrusive UI windows.
    /// </summary>
    public class TacticalMultiplayerOverlay : MonoBehaviour
    {
        [Header("Dedicated Server Configuration")]
        [SerializeField] private string serverAddress = ServerConfig.GameServerAddress;
        [SerializeField] private ushort serverPort = 7777;
        [SerializeField] private bool autoConnectClientOnStart = true;

        private bool _isConnecting = false;
        private GUIStyle _pingBoxStyle;
        private GUIStyle _pingLabelStyle;
        private bool _stylesInitialized = false;

        private void OnEnable()
        {
            StartCoroutine(HookConnectionLogs());
        }

        private void OnDisable()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            nm.OnClientConnectedCallback -= LogClientConnected;
            nm.OnClientDisconnectCallback -= LogClientDisconnected;
        }

        private System.Collections.IEnumerator HookConnectionLogs()
        {
            while (NetworkManager.Singleton == null) yield return null;
            var nm = NetworkManager.Singleton;
            nm.OnClientConnectedCallback -= LogClientConnected;
            nm.OnClientDisconnectCallback -= LogClientDisconnected;
            nm.OnClientConnectedCallback += LogClientConnected;
            nm.OnClientDisconnectCallback += LogClientDisconnected;
        }

        private static void LogClientConnected(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            if (nm.IsServer)
                Debug.Log($"[Net] Client {clientId} connected. Players online: {nm.ConnectedClientsIds.Count}");
            else
                Debug.Log($"[Net] Connected to server as client {clientId}.");
        }

        private static void LogClientDisconnected(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            // ConnectedClientsIds is server-only; clients would throw NotServerException here.
            if (nm.IsServer)
                Debug.Log($"[Net] Client {clientId} disconnected. Players online: {nm.ConnectedClientsIds.Count}");
            else
                Debug.Log($"[Net] Disconnected from server (client {clientId}).");
        }

        private System.Collections.IEnumerator Start()
        {
            ApplyCommandLineArgs();

            string[] args = Environment.GetCommandLineArgs();
            bool isHeadless = SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null;
            bool isServer = isHeadless || args.Any(a => string.Equals(a, "-server", StringComparison.OrdinalIgnoreCase) ||
                                                        string.Equals(a, "-dedicated", StringComparison.OrdinalIgnoreCase));
            bool isHost = args.Any(a => string.Equals(a, "-host", StringComparison.OrdinalIgnoreCase));
            bool isOffline = args.Any(a => string.Equals(a, "-offline", StringComparison.OrdinalIgnoreCase) ||
                                           string.Equals(a, "-solo", StringComparison.OrdinalIgnoreCase));

            // The scene's offline "TPS Character" is only a placeholder for single-player. On a dedicated server it would count
            // as a human: the match would start (and run its bots) on an empty server, long before the first real player.
            if (isServer && !isHost) DeactivateOfflineScenePlayer();

            if (isServer || isHost || isOffline) yield break;

            if (autoConnectClientOnStart)
            {
                DeactivateOfflineScenePlayer();
                yield return new WaitForSeconds(0.3f);

                yield return ConnectAutomatically();
            }
        }

        private void DeactivateOfflineScenePlayer()
        {
            var offlineChar = GameObject.Find("TPS Character");
            if (offlineChar != null && offlineChar.GetComponent<NetworkObject>() == null)
            {
                Debug.Log("[TacticalMultiplayerOverlay] Deactivating offline scene player for multiplayer session.");
                offlineChar.SetActive(false);
            }
        }

        private void ApplyCommandLineArgs()
        {
            if (NetworkManager.Singleton == null) return;

            string[] args = Environment.GetCommandLineArgs();

            // Custom port override via -port <num>
            int portIndex = Array.FindIndex(args, a => string.Equals(a, "-port", StringComparison.OrdinalIgnoreCase));
            if (portIndex >= 0 && portIndex < args.Length - 1 && ushort.TryParse(args[portIndex + 1], out ushort parsedPort))
            {
                serverPort = parsedPort;
            }

            // Custom IP override via -ip <address>
            int ipIndex = Array.FindIndex(args, a => string.Equals(a, "-ip", StringComparison.OrdinalIgnoreCase));
            if (ipIndex >= 0 && ipIndex < args.Length - 1)
            {
                serverAddress = args[ipIndex + 1].Trim();
            }

            UpdateTransportConfig();

            // Headless / Dedicated Server flag
            bool isHeadless = SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null;
            bool isServer = args.Any(a => string.Equals(a, "-server", StringComparison.OrdinalIgnoreCase) ||
                                          string.Equals(a, "-dedicated", StringComparison.OrdinalIgnoreCase));
            bool isHost = args.Any(a => string.Equals(a, "-host", StringComparison.OrdinalIgnoreCase));
            bool isClient = args.Any(a => string.Equals(a, "-client", StringComparison.OrdinalIgnoreCase));

            if (isHeadless || isServer)
            {
                Debug.Log($"[TacticalMultiplayerOverlay] Auto-starting Dedicated Server on port {serverPort}...");
                NetworkManager.Singleton.StartServer();
            }
            else if (isHost)
            {
                Debug.Log($"[TacticalMultiplayerOverlay] Auto-starting Host on port {serverPort}...");
                NetworkManager.Singleton.StartHost();
            }
            else if (isClient)
            {
                if (HasCommandLineEndpoint()) { Debug.Log($"[TacticalMultiplayerOverlay] Auto-starting Client connecting to {serverAddress}:{serverPort}..."); StartClient(); }
                else StartCoroutine(ConnectAutomatically());
            }
        }

        [SerializeField, Tooltip("Old IMGUI ping badge. The HUD now shows ping in its own network chip (HudNetworkChip).")] private bool showLegacyPingBadge = false;

        private void OnGUI()
        {
            if (!showLegacyPingBadge) return;
            // Do not render UI in headless Linux servers (Docker)
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;

            var nm = NetworkManager.Singleton;
            if (nm == null) return;

            InitStyles();

            float screenW = Screen.width;
            const float badgeH = 26f;

            if (nm.IsClient)
            {
                ulong rtt = nm.NetworkConfig.NetworkTransport != null 
                    ? nm.NetworkConfig.NetworkTransport.GetCurrentRtt(NetworkManager.ServerClientId) 
                    : 0;

                // Color code: Green < 100ms, Yellow < 200ms, Orange >= 200ms
                Color pingColor = rtt < 100 ? new Color(0.22f, 0.95f, 0.45f) :
                                  rtt < 200 ? new Color(0.98f, 0.85f, 0.20f) :
                                              new Color(1.0f, 0.45f, 0.20f);

                string pingText = $"● {rtt} ms";
                float badgeW = 95f;
                Rect badgeRect = new Rect(screenW - badgeW - 16f, 14f, badgeW, badgeH);

                GUI.Box(badgeRect, GUIContent.none, _pingBoxStyle);

                Color prevColor = GUI.color;
                GUI.color = pingColor;
                GUI.Label(badgeRect, pingText, _pingLabelStyle);
                GUI.color = prevColor;
            }
            else if (_isConnecting)
            {
                float badgeW = 125f;
                Rect badgeRect = new Rect(screenW - badgeW - 16f, 14f, badgeW, badgeH);

                GUI.Box(badgeRect, GUIContent.none, _pingBoxStyle);

                Color prevColor = GUI.color;
                GUI.color = new Color(0.2f, 0.8f, 1.0f);
                GUI.Label(badgeRect, "● CONNECTING...", _pingLabelStyle);
                GUI.color = prevColor;
            }
            else if (nm.IsHost)
            {
                float badgeW = 110f;
                Rect badgeRect = new Rect(screenW - badgeW - 16f, 14f, badgeW, badgeH);

                GUI.Box(badgeRect, GUIContent.none, _pingBoxStyle);

                Color prevColor = GUI.color;
                GUI.color = new Color(0.2f, 0.95f, 0.45f);
                GUI.Label(badgeRect, $"● HOST:{serverPort}", _pingLabelStyle);
                GUI.color = prevColor;
            }
        }

        private bool _autoConnecting;

        /// <summary>
        /// Connects without any -ip / -port: when the lobby has not already allocated a server, this asks Nakama (RPC railbound_allocate,
        /// which allocates a Ready server through Agones) for the current game server address and port. Everybody who starts with the same
        /// match id (default: the current half hour, or "-match &lt;id&gt;") gets the same server, so friends on other networks land together.
        /// If the allocation fails it falls back to the configured address.
        /// </summary>
        private System.Collections.IEnumerator ConnectAutomatically()
        {
            var nm = NetworkManager.Singleton;
            if (_autoConnecting || nm == null || nm.IsClient || nm.IsServer) yield break;
            _autoConnecting = true;
            _isConnecting = true;

            var server = Railbound.Networking.GameServerConnection.Instance;
            if (!HasCommandLineEndpoint() && !(server != null && server.HasEndpoint))
            {
                var task = AllocateFromNakamaAsync();
                while (!task.IsCompleted) yield return null;
                if (task.Status == System.Threading.Tasks.TaskStatus.RanToCompletion && task.Result.HasValue)
                {
                    serverAddress = task.Result.Value.Host;
                    serverPort = (ushort)task.Result.Value.Port;
                    Debug.Log($"[TacticalMultiplayerOverlay] Nakama allocated {task.Result.Value.ServerName}: {serverAddress}:{serverPort}");
                }
                else
                {
                    Debug.LogWarning($"[TacticalMultiplayerOverlay] No server allocated (Nakama unreachable or none ready); trying {serverAddress}:{serverPort}.");
                }
            }

            _isConnecting = false;
            _autoConnecting = false;
            if (nm != null && !nm.IsClient && !nm.IsServer)
            {
                Debug.Log($"[TacticalMultiplayerOverlay] Auto-connecting client to {serverAddress}:{serverPort}...");
                StartClient();
            }
        }

        private static async System.Threading.Tasks.Task<ServerAllocationClient.Allocation?> AllocateFromNakamaAsync()
        {
            try
            {
                var client = new Nakama.Client(ServerConfig.NakamaScheme, ServerConfig.NakamaHost, ServerConfig.NakamaPort,
                    ServerConfig.NakamaServerKey, Nakama.UnityWebRequestAdapter.Instance);
                string deviceId = PlayerPrefs.GetString("nakama.device.id", "");
                if (string.IsNullOrEmpty(deviceId)) deviceId = SystemInfo.deviceUniqueIdentifier;
                if (string.IsNullOrEmpty(deviceId) || deviceId == SystemInfo.unsupportedIdentifier) deviceId = Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString("nakama.device.id", deviceId);
                var session = await client.AuthenticateDeviceAsync(deviceId);

                string matchId = null;
                string[] args = Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length - 1; i++) if (string.Equals(args[i], "-match", StringComparison.OrdinalIgnoreCase)) matchId = args[i + 1];
                if (string.IsNullOrEmpty(matchId)) matchId = "open-" + DateTime.UtcNow.ToString("yyyyMMdd-HH") + (DateTime.UtcNow.Minute < 30 ? "a" : "b");

                return await ServerAllocationClient.AllocateAsync(client, session, matchId, 1, 90f, null,
                    s => Debug.Log("[TacticalMultiplayerOverlay] " + s));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[TacticalMultiplayerOverlay] Allocation failed: " + ex.Message);
                return null;
            }
        }

        public void StartClient()
        {
            UpdateTransportConfig();
            if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
            {
                _isConnecting = true;
                DeactivateOfflineScenePlayer();
                Debug.Log($"[TacticalMultiplayerOverlay] Connecting Client -> {serverAddress}:{serverPort}...");
                NetworkManager.Singleton.StartClient();
            }
        }

        public void StartHost()
        {
            UpdateTransportConfig();
            if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
            {
                Debug.Log($"[TacticalMultiplayerOverlay] Starting Host on port {serverPort}...");
                NetworkManager.Singleton.StartHost();
            }
        }

        public void StartDedicatedServer()
        {
            UpdateTransportConfig();
            if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
            {
                Debug.Log($"[TacticalMultiplayerOverlay] Starting Server on port {serverPort}...");
                NetworkManager.Singleton.StartServer();
            }
        }

        public void Disconnect()
        {
            if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer))
            {
                Debug.Log("[TacticalMultiplayerOverlay] Shutting down Netcode session...");
                _isConnecting = false;
                NetworkManager.Singleton.Shutdown();
            }
        }

        private void UpdateTransportConfig()
        {
            if (NetworkManager.Singleton == null) return;
            Railbound.Networking.ConnectionApprovalGate.Configure(NetworkManager.Singleton);   // client and server must agree on connection approval
            if (!NetworkManager.Singleton.IsListening)
            {
                var link = Railbound.Networking.GameServerConnection.Instance;
                if (link != null) NetworkManager.Singleton.NetworkConfig.ConnectionData = Railbound.Networking.ConnectionApprovalGate.BuildClientPayload(link.MatchId, link.JoinToken);
            }

            // the lobby asked Nakama for this match's game server (Agones); a command-line -ip / -port still wins
            var server = Railbound.Networking.GameServerConnection.Instance;
            if (server != null && server.HasEndpoint && !HasCommandLineEndpoint())
            {
                serverAddress = server.Host;
                serverPort = server.Port;
            }

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null)
            {
                transport.SetConnectionData(serverAddress, serverPort, "0.0.0.0");
                // Precaution: the default send queue is only ~96 KB per connection, about two seconds of this match's replicated state
                // (19 bots). A client that stalls (alt-tab, Wi-Fi hiccup) should not overflow it and lose messages.
                transport.MaxSendQueueSize = 4 * 1024 * 1024;
                transport.MaxPacketQueueSize = Mathf.Max(transport.MaxPacketQueueSize, 512);
            }
        }

        private static bool HasCommandLineEndpoint()
        {
            string[] args = Environment.GetCommandLineArgs();
            return Array.Exists(args, a => string.Equals(a, "-ip", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "-port", StringComparison.OrdinalIgnoreCase));
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            Texture2D boxTex = new Texture2D(1, 1);
            boxTex.SetPixel(0, 0, new Color(0.04f, 0.08f, 0.12f, 0.85f));
            boxTex.Apply();

            _pingBoxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = boxTex },
                padding = new RectOffset(4, 4, 2, 2)
            };

            _pingLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _stylesInitialized = true;
        }
    }
}
