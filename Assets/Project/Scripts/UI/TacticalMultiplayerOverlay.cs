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

            if (isServer || isHost || isOffline) yield break;

            if (autoConnectClientOnStart)
            {
                DeactivateOfflineScenePlayer();
                yield return new WaitForSeconds(0.3f);

                if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
                {
                    Debug.Log($"[TacticalMultiplayerOverlay] Direct Public Join: Auto-connecting client to {serverAddress}:{serverPort}...");
                    StartClient();
                }
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
                Debug.Log($"[TacticalMultiplayerOverlay] Auto-starting Client connecting to {serverAddress}:{serverPort}...");
                StartClient();
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

            // the lobby asked Nakama for this match's game server (Agones); a command-line -ip / -port still wins
            if (MatchConnection.HasAllocation && !HasCommandLineEndpoint())
            {
                serverAddress = MatchConnection.Host;
                serverPort = MatchConnection.Port;
            }

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null)
            {
                transport.SetConnectionData(serverAddress, serverPort, "0.0.0.0");
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
