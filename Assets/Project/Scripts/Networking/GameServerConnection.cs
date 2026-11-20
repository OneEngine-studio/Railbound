using System;
using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Railbound.Networking
{
    public enum GameServerState
    {
        /// <summary>No game server chosen (or the connection was cleared).</summary>
        Idle,
        /// <summary>An address is known (allocated by Nakama/Agones or the default one) but the client has not connected yet.</summary>
        Allocated,
        Connecting,
        Connected,
        /// <summary>The last attempt failed or the server dropped us; <see cref="GameServerConnection.LastError"/> says why.</summary>
        Failed,
    }

    /// <summary>
    /// The one place that knows where the dedicated game server (Netcode for GameObjects, run by Agones) is and that owns the
    /// client connection to it. It is a persistent singleton, so the lobby, the loading screen, the match overlay and any other
    /// client service read the same address and the same connection state instead of keeping their own copies.
    ///
    /// Address: Nakama's allocation RPC hands out the UDP port (Agones range 7000-8000). The host comes from
    /// <see cref="ServerConfig.GameServerAddress"/> while <see cref="ServerConfig.UseConfiguredGameServerHost"/> is on, so the client
    /// never depends on a stale public address stored in the Nakama deployment. Without an allocation
    /// <see cref="UseDefaultEndpoint"/> uses ServerConfig's default host and port.
    /// </summary>
    public sealed class GameServerConnection : MonoBehaviour
    {
        private static GameServerConnection _instance;
        private static bool _quitting;

        public static GameServerConnection Instance
        {
            get
            {
                if (_instance == null && !_quitting)
                {
                    var go = new GameObject("GameServerConnection");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<GameServerConnection>();
                }
                return _instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            _quitting = false;
        }

        [Tooltip("Seconds one connection attempt may take. A server that was just handed out can need a moment before it answers, so several short attempts beat one long wait.")]
        [SerializeField] private float attemptTimeoutSeconds = 8f;
        [Tooltip("Seconds the first attempt may take to reach a server that was just handed out: it may still be starting its match and does not answer while it loads.")]
        [SerializeField] private float firstAttemptTimeoutSeconds = 20f;
        [SerializeField] private int maxAttempts = 4;
        [SerializeField] private float retryDelaySeconds = 1.5f;
        [Tooltip("Seconds an attempt may wait once the server answered and is deciding on the join (it checks the join ticket with Nakama).")]
        [SerializeField] private float approvalWaitSeconds = 40f;

        public string MatchId { get; private set; } = "";
        public string Host { get; private set; } = "";
        public ushort Port { get; private set; }
        public string ServerName { get; private set; } = "";
        /// <summary>Join ticket from Nakama's allocation; sent to the game server in the connection payload (connection approval).</summary>
        public string JoinToken { get; private set; } = "";
        public GameServerState State { get; private set; } = GameServerState.Idle;
        public string LastError { get; private set; } = "";

        /// <summary>True once an address was chosen, from an allocation or the default.</summary>
        public bool HasEndpoint => !string.IsNullOrEmpty(Host) && Port != 0;
        public string Endpoint => Host + ":" + Port;
        /// <summary>Total time all attempts together may take.</summary>
        public float ConnectTimeoutSeconds => approvalWaitSeconds + Mathf.Max(0f, firstAttemptTimeoutSeconds - attemptTimeoutSeconds) + (attemptTimeoutSeconds + retryDelaySeconds) * Mathf.Max(1, maxAttempts);
        /// <summary>Number of the running connection attempt (1-based) and how many there are.</summary>
        public int Attempt { get; private set; }
        public int MaxAttempts => Mathf.Max(1, maxAttempts);

        /// <summary>Raised on every state change, on the main thread.</summary>
        public event Action<GameServerState> StateChanged;

        private Coroutine _routine;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnApplicationQuit() { _quitting = true; }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        // ---------------- address ----------------

        /// <summary>Stores the server Nakama allocated for the match.</summary>
        public void SetAllocation(string matchId, string host, int port, string serverName, string joinToken = "")
        {
            string resolved = ResolveHost(host);
            if (!string.Equals(resolved, host, StringComparison.OrdinalIgnoreCase))
                Debug.Log($"[GameServer] Nakama reported host '{host}'; using the configured game server host {resolved} with the allocated port {port}.");
            SetEndpoint(matchId, resolved, port, serverName);
            JoinToken = joinToken ?? "";
        }

        /// <summary>Uses the default game server of <see cref="ServerConfig"/> (no allocation available).</summary>
        public void UseDefaultEndpoint(string matchId)
        {
            SetEndpoint(matchId, ServerConfig.GameServerAddress, ServerConfig.GameServerPort, "default");
        }

        private static string ResolveHost(string allocatedHost)
        {
            bool configured = !string.IsNullOrWhiteSpace(ServerConfig.GameServerAddress);
            // a client in local test mode (-test-nakama, loopback only) plays on a game server on this machine: keep 127.0.0.1
            if (AuthenticationService.TestBackend && allocatedHost == "127.0.0.1") return allocatedHost;
            if (configured && (ServerConfig.UseConfiguredGameServerHost || string.IsNullOrWhiteSpace(allocatedHost) || IsPrivateAddress(allocatedHost)))
                return ServerConfig.GameServerAddress;
            return allocatedHost ?? "";
        }

        private static bool IsPrivateAddress(string host)
        {
            if (!System.Net.IPAddress.TryParse(host, out var ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return false;
            var b = ip.GetAddressBytes();
            return b[0] == 10 || b[0] == 127 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168);
        }

        private void SetEndpoint(string matchId, string host, int port, string serverName)
        {
            if (State == GameServerState.Connecting || State == GameServerState.Connected) Disconnect();
            MatchId = matchId ?? "";
            Host = host ?? "";
            Port = (ushort)Mathf.Clamp(port, 0, ushort.MaxValue);
            ServerName = serverName ?? "";
            JoinToken = "";   // SetAllocation stores the ticket of this endpoint right after
            LastError = "";
            SetState(HasEndpoint ? GameServerState.Allocated : GameServerState.Idle);
            Debug.Log($"[GameServer] Endpoint set: {Endpoint} ({ServerName}).");
        }

        /// <summary>Forgets the endpoint; also leaves a running connection.</summary>
        public void Clear()
        {
            if (State == GameServerState.Connecting || State == GameServerState.Connected) Disconnect();
            MatchId = Host = ServerName = LastError = JoinToken = "";
            Port = 0;
            SetState(GameServerState.Idle);
        }

        /// <summary>Writes the endpoint into the NetworkManager's transport.</summary>
        public bool ApplyToTransport()
        {
            if (!HasEndpoint) return false;
            var nm = NetworkManager.Singleton;
            var transport = nm != null ? nm.GetComponent<UnityTransport>() : null;
            if (transport == null) return false;
            transport.SetConnectionData(Host, Port, "0.0.0.0");
            return true;
        }

        // ---------------- connection ----------------

        /// <summary>
        /// Connects the persistent NetworkManager to the game server as a client (never as a host). Progress is reported through
        /// <see cref="State"/> / <see cref="StateChanged"/>; the attempt keeps running across scene loads.
        /// </summary>
        public bool Connect()
        {
            if (State == GameServerState.Connecting || State == GameServerState.Connected) return true;
            if (!HasEndpoint) return Fail("NO GAME SERVER ADDRESS");

            var nm = NetworkManager.Singleton;
            if (nm == null) return Fail("NETWORK UNAVAILABLE");
            if (nm.IsServer) return Fail("THIS INSTANCE IS A SERVER/HOST AND CANNOT JOIN ANOTHER SERVER");
            if (nm.GetComponent<UnityTransport>() == null) return Fail("NETWORK TRANSPORT MISSING");

            if (_routine != null) StopCoroutine(_routine);
            LastError = "";
            SetState(GameServerState.Connecting);
            _routine = StartCoroutine(ConnectRoutine(nm));
            return true;
        }

        /// <summary>Leaves the game server and keeps the endpoint.</summary>
        public void Disconnect()
        {
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening && !nm.IsServer) nm.Shutdown();
            SetState(HasEndpoint ? GameServerState.Allocated : GameServerState.Idle);
        }

        private IEnumerator ConnectRoutine(NetworkManager nm)
        {
            // A server that was just allocated (or a node under load) may need a few seconds before it answers, and one UDP
            // packet can get lost, so the client makes several short attempts instead of one long blind wait.
            int attempts = MaxAttempts;
            string failure = "COULD NOT REACH THE GAME SERVER";
            bool connected = false;
            bool finalRefusal = false;

            for (Attempt = 1; Attempt <= attempts && !connected; Attempt++)
            {
                // the previous attempt may have finished its handshake after its timeout (a slow server): keep that connection
                if (nm != null && nm.IsConnectedClient && !nm.IsServer) { connected = true; break; }
                // an earlier session (a client that was started after login, the previous attempt, a previous match) has to be gone first
                if (nm != null && nm.IsListening) nm.Shutdown();
                float guard = 0f;
                while (nm != null && (nm.IsListening || nm.ShutdownInProgress) && guard < 5f)
                {
                    guard += Mathf.Min(Time.unscaledDeltaTime, 0.25f);
                    yield return null;
                }
                if (nm == null) { Fail("NETWORK UNAVAILABLE"); yield break; }

                nm.NetworkConfig.EnableSceneManagement = true;   // the server loads the map on this client
                ConnectionApprovalGate.Configure(nm);            // same flag as the server (part of the netcode config hash)
                nm.NetworkConfig.ConnectionData = ConnectionApprovalGate.BuildClientPayload(MatchId, JoinToken);   // match id, Nakama user id, join ticket
                if (!ApplyToTransport()) { Fail("NETWORK TRANSPORT MISSING"); yield break; }
                // once the transport is connected the server is deciding on the join: that gets its own, longer limit
                bool transportUp = false;
                var transport = nm.NetworkConfig.NetworkTransport;
                NetworkTransport.TransportEventDelegate onTransport = (e, id, payload, at) => { if (e == NetworkEvent.Connect) transportUp = true; };
                if (transport != null) transport.OnTransportEvent += onTransport;
                if (!nm.StartClient()) { if (transport != null) transport.OnTransportEvent -= onTransport; Fail("FAILED TO START NETWORK CLIENT"); yield break; }
                Debug.Log($"[GameServer] Netcode client started -> {Endpoint} (attempt {Attempt}/{attempts})");

                float elapsed = 0f;
                bool waitLogged = false;
                float reachLimit = Attempt <= 1 ? Mathf.Max(attemptTimeoutSeconds, firstAttemptTimeoutSeconds) : attemptTimeoutSeconds;
                while (elapsed < (transportUp ? reachLimit + approvalWaitSeconds : reachLimit))
                {
                    if (transportUp && !waitLogged) { waitLogged = true; Debug.Log("[GameServer] Reached the server; waiting for it to accept the join."); }
                    if (nm.IsConnectedClient) { connected = true; break; }
                    // a frame can take seconds while the map loads; that must not use up the attempt, so one frame counts at most 0.25 s
                    elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.25f);
                    if (!nm.IsListening && elapsed > 0.5f)
                    {
                        string reason = nm.DisconnectReason;
                        failure = string.IsNullOrEmpty(reason) ? "CONNECTION TO THE GAME SERVER FAILED" : "THE GAME SERVER REFUSED THE CONNECTION: " + reason;
                        if (IsFinalRefusal(reason)) finalRefusal = true;
                        break;
                    }
                    yield return null;
                }
                if (transport != null) transport.OnTransportEvent -= onTransport;

                if (!connected)
                {
                    Debug.LogWarning($"[GameServer] Attempt {Attempt}/{attempts} to {Endpoint} failed: {failure}");
                    if (finalRefusal)
                    {
                        // retrying cannot change the answer; a match the player is out of is not offered again
                        if (failure.Contains(ServerMatchSession.OutOfMatchReason) || failure.Contains("THE MATCH IS OVER")) MatchmakingQueueClient.NoteLeftMatch(MatchId);
                        break;
                    }
                    if (Attempt < attempts)
                    {
                        // a handshake that is still running can complete during the pause, so watch for it instead of sleeping blind
                        float wait = 0f;
                        while (wait < retryDelaySeconds && !(nm != null && nm.IsConnectedClient))
                        {
                            wait += Mathf.Min(Time.unscaledDeltaTime, 0.25f);
                            yield return null;
                        }
                    }
                }
            }

            if (!connected)
            {
                if (nm != null && nm.IsListening) nm.Shutdown();
                Fail(failure);
                yield break;
            }

            SetState(GameServerState.Connected);
            Debug.Log($"[GameServer] Connected to {Endpoint} as client {nm.LocalClientId}.");

            // after this point only a lost connection matters
            while (nm != null && nm.IsConnectedClient) yield return null;
            _routine = null;
            if (State == GameServerState.Connected)
            {
                string reason = nm != null ? nm.DisconnectReason : "";
                Fail(string.IsNullOrEmpty(reason) ? "DISCONNECTED FROM THE GAME SERVER" : "DISCONNECTED: " + reason);
            }
        }

        /// <summary>Refusals a retry cannot change.</summary>
        private static bool IsFinalRefusal(string reason)
        {
            if (string.IsNullOrEmpty(reason)) return false;
            return reason.Contains(ServerMatchSession.OutOfMatchReason) || reason.Contains("THE MATCH IS OVER")
                || reason.Contains("WRONG MATCH") || reason.Contains("JOIN TICKET REJECTED") || reason.Contains("ALREADY CONNECTED");
        }

        private bool Fail(string reason)
        {
            _routine = null;
            LastError = reason;
            Debug.LogWarning($"[GameServer] {reason} ({(HasEndpoint ? Endpoint : "no address")}).");
            SetState(GameServerState.Failed);
            return false;
        }

        private void SetState(GameServerState state)
        {
            if (State == state) return;
            State = state;
            StateChanged?.Invoke(state);
        }
    }
}
