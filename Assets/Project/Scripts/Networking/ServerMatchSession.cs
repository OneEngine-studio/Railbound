using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Railbound.BR;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Networking;

namespace Railbound.Networking
{
    /// <summary>What a client sends when it connects to a game server (NetworkConfig.ConnectionData, JSON, about 150 bytes).</summary>
    [Serializable]
    public sealed class JoinPayload
    {
        public int v = 1;
        public string m;   // match id the client was allocated
        public string u;   // the client's Nakama user id
        public string t;   // join ticket: the random token Nakama handed out with railbound_allocate
    }

    /// <summary>
    /// Connection approval, both halves. Netcode needs the same <c>ConnectionApproval</c> flag on the client and the server (it is part of the
    /// config hash), so <see cref="Configure"/> is called wherever a NetworkManager is about to start.
    ///
    ///   client  -> <see cref="BuildClientPayload"/>: match id + Nakama user id + join ticket
    ///   server  -> <see cref="ServerMatchSession.Approve"/>: match id must be the one this server was allocated for, and Nakama must confirm
    ///              the (match, user, ticket) triple (RPC railbound_validate_join), so the user id cannot be made up.
    /// </summary>
    public static class ConnectionApprovalGate
    {
        public static void Configure(NetworkManager nm)
        {
            if (nm == null || nm.IsListening) return;
            nm.NetworkConfig.ConnectionApproval = true;
            if (nm.ConnectionApprovalCallback == null) nm.ConnectionApprovalCallback = ServerMatchSession.Approve;   // only a server or host ever calls it
        }

        public static byte[] BuildClientPayload(string matchId, string joinToken)
        {
            string user = "";
            var auth = AuthenticationService.Instance;
            if (auth != null && auth.IsAuthenticated && auth.Session != null) user = auth.Session.UserId ?? "";
            // local test clients (MAP-2 only, no Nakama session, started with -ip/-port) name the match and a user on the command line.
            // Only a server started with -insecure-join accepts them: a real server checks the join ticket with Nakama.
            if (string.IsNullOrEmpty(matchId)) matchId = CommandLineValue("-match");
            if (string.IsNullOrEmpty(user)) user = CommandLineValue("-test-user");
            if (string.IsNullOrEmpty(joinToken)) joinToken = CommandLineValue("-join-token");
            return Encoding.UTF8.GetBytes(JsonUtility.ToJson(new JoinPayload { m = matchId ?? "", u = user ?? "", t = joinToken ?? "" }));
        }

        private static string CommandLineValue(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        // makes sure every NetworkManager that appears in any scene is configured before it starts (the server's one lives in the map scene)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoConfigure()
        {
            Configure(NetworkManager.Singleton);
            var go = new GameObject("[ConnectionApprovalWatcher]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<Watcher>();
        }

        private sealed class Watcher : MonoBehaviour
        {
            private void Update() { Configure(NetworkManager.Singleton); }
        }
    }

    /// <summary>
    /// Server side of the economy. On a dedicated server it
    ///   1. approves or rejects every connecting client (see <see cref="ConnectionApprovalGate"/>) and remembers which Nakama user is which client,
    ///   2. is told by the match coordinator who was eliminated, by whom, and how the match ended (the coordinator is the authority on that),
    ///   3. reports each human player's result (kills, placement, time survived) to Nakama over the HTTP-key RPC railbound_report_match.
    ///      XP and coins are computed by Nakama, never by the game or the client.
    ///
    /// Settings (environment of the game server pod, or command line):
    ///   RAILBOUND_NAKAMA_URL        base URL of Nakama (default: ServerConfig's public address; inside the cluster http://nakama.apps.svc.cluster.local:7350)
    ///   RAILBOUND_NAKAMA_HTTP_KEY   Nakama's runtime.http_key. Without it every client is refused and nothing is reported.
    ///   -insecure-join              local testing only: accept a well-formed payload without asking Nakama (no rewards are reported)
    ///   -match &lt;id&gt;                local testing only: the match id of a server that was not started by Agones
    /// </summary>
    public sealed class ServerMatchSession : MonoBehaviour
    {
        private const string ValidateRpc = "railbound_validate_join";
        private const string ReportRpc = "railbound_report_match";
        private const float MatchIdWaitSeconds = 6f;     // the Agones lifecycle polls the allocation every 2 s, a client may be faster
        private const float RpcTimeoutSeconds = 5f;      // netcode drops a pending approval after 10 s

        private sealed class Entry
        {
            public string userId;
            public ulong clientId;
            public bool connected;
            public bool final;        // placement is decided
            public bool noReward;     // left before the battle began
            public int placement, kills, total;
            public int team = -1, slot;   // the squad from Nakama's ticket check (-1 = a team of one)
            public float survived;
            public readonly Dictionary<string, int> weaponKills = new Dictionary<string, int>();   // weapon id -> kills (weapon mastery)
        }

        [Serializable] private sealed class ValidateRequest { public string matchId, userId, token; }
        [Serializable] private sealed class ValidateReply { public bool valid; public string reason; public int team = -1; public int slot; }
        [Serializable] private sealed class WeaponKill { public string weapon; public int kills; }
        [Serializable] private sealed class ReportPlayer { public string userId; public int placement, totalPlayers, kills, survivedSeconds; public List<WeaponKill> weaponKills = new List<WeaponKill>(); }
        [Serializable] private sealed class ReportRequest { public string matchId; public List<ReportPlayer> players = new List<ReportPlayer>(); }

        private static ServerMatchSession instance;

        private readonly Dictionary<string, Entry> byUser = new Dictionary<string, Entry>();
        private readonly Dictionary<ulong, Entry> byClient = new Dictionary<ulong, Entry>();
        private string lockedMatchId = "";
        private int approvedTeam = -1, approvedSlot;   // what the last ticket check said (approvals run one after the other)

        /// <summary>The squad of a connected client, as Nakama told it with the ticket (false: no team, a team of one).</summary>
        public static bool TryGetTeam(ulong clientId, out int team, out int slot)
        {
            team = -1; slot = 0;
            if (instance == null || !instance.byClient.TryGetValue(clientId, out var e) || e.team < 0) return false;
            team = e.team; slot = e.slot;
            return true;
        }

        /// <summary>A squad member who fell earlier gets the squad's place now that the squad is out.</summary>
        public static void NoteSquadPlacement(ulong clientId, int placement, int totalPlayers)
        {
            if (instance == null || !instance.byClient.TryGetValue(clientId, out var e)) return;
            e.final = true; e.placement = Mathf.Max(2, placement); e.total = Mathf.Max(totalPlayers, e.placement);
        }
        private bool subscribed;
        private enum ReportState { None, Sending, Done, Failed }
        private ReportState report = ReportState.None;

        /// <summary>The match this server pays rewards for ("" until the first player was approved on a server not started by Agones).</summary>
        public static string MatchId => instance != null ? instance.CurrentMatchId() : "";

        // ---------------------------------------------------------------- settings
        private static string ArgValue(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        private static bool HasArg(string name) { return Array.Exists(Environment.GetCommandLineArgs(), a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase)); }

        private static bool Insecure => HasArg("-insecure-join");
        private static string HttpKey => Environment.GetEnvironmentVariable("RAILBOUND_NAKAMA_HTTP_KEY") ?? "";

        private static string NakamaUrl
        {
            get
            {
                string url = Environment.GetEnvironmentVariable("RAILBOUND_NAKAMA_URL");
                if (string.IsNullOrWhiteSpace(url))
                    url = ServerConfig.NakamaScheme + "://" + ServerConfig.NakamaHost + ((ServerConfig.NakamaScheme == "https" && ServerConfig.NakamaPort == 443) || (ServerConfig.NakamaScheme == "http" && ServerConfig.NakamaPort == 80) ? "" : ":" + ServerConfig.NakamaPort);
                return url.TrimEnd('/');
            }
        }

        private string CurrentMatchId()
        {
            if (AgonesServerLifecycle.InAgones) return AgonesServerLifecycle.MatchId;
            string arg = ArgValue("-match");
            return !string.IsNullOrEmpty(arg) ? arg : lockedMatchId;
        }

        private static ServerMatchSession Ensure()
        {
            if (instance != null) return instance;
            var go = new GameObject("[ServerMatchSession]");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<ServerMatchSession>();
            return instance;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { instance = null; }

        private void Subscribe()
        {
            var nm = NetworkManager.Singleton;
            if (subscribed || nm == null) return;
            nm.OnClientDisconnectCallback += OnClientDisconnected;
            subscribed = true;
        }

        private void OnDestroy()
        {
            var nm = NetworkManager.Singleton;
            if (subscribed && nm != null) nm.OnClientDisconnectCallback -= OnClientDisconnected;
            if (instance == this) instance = null;
        }

        // ================================================================= approval
        /// <summary>NetworkManager.ConnectionApprovalCallback.</summary>
        public static void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.CreatePlayerObject = true;
            if (request.ClientNetworkId == NetworkManager.ServerClientId) { response.Approved = true; return; }   // the host itself (offline testing)

            JoinPayload payload = null;
            try { if (request.Payload != null && request.Payload.Length > 0 && request.Payload.Length < 1024) payload = JsonUtility.FromJson<JoinPayload>(Encoding.UTF8.GetString(request.Payload)); }
            catch (Exception) { payload = null; }

            if (payload == null || string.IsNullOrEmpty(payload.m) || payload.m.Length > 100 || string.IsNullOrEmpty(payload.u) || payload.u.Length > 64)
            {
                Reject(response, "NO VALID MATCH ID AND PLAYER ID");
                return;
            }
            if (!Insecure && (string.IsNullOrEmpty(payload.t) || payload.t.Length > 64))
            {
                Reject(response, "NO JOIN TICKET");
                return;
            }

            response.Approved = false;
            response.Pending = true;   // decided in VerifyRoutine
            var session = Ensure();
            session.Subscribe();
            session.StartCoroutine(session.VerifyRoutine(request.ClientNetworkId, payload, response));
        }

        private static void Reject(NetworkManager.ConnectionApprovalResponse response, string reason)
        {
            response.Approved = false;
            response.Reason = reason;
            response.Pending = false;
            Debug.LogWarning("[ServerMatch] Connection refused: " + reason);
        }

        private IEnumerator VerifyRoutine(ulong clientId, JoinPayload p, NetworkManager.ConnectionApprovalResponse response)
        {
            string reject = null;

            // 1. the match id must be the one this server was allocated for (Agones tells the lifecycle a moment after the allocation)
            float waited = 0f;
            string expected = CurrentMatchId();
            while (string.IsNullOrEmpty(expected) && AgonesServerLifecycle.InAgones && waited < MatchIdWaitSeconds)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
                expected = CurrentMatchId();
            }
            if (string.IsNullOrEmpty(expected) && AgonesServerLifecycle.InAgones) reject = "THIS SERVER HAS NO MATCH YET";
            else if (!string.IsNullOrEmpty(expected) && expected != p.m) reject = "WRONG MATCH";

            // 2. Nakama must know this player's ticket for this match
            if (reject == null && !Insecure)
            {
                if (string.IsNullOrEmpty(HttpKey)) reject = "SERVER IS NOT CONNECTED TO THE ACCOUNT SERVICE";
                else
                {
                    bool? valid = null;
                    yield return CallRpc(ValidateRpc, JsonUtility.ToJson(new ValidateRequest { matchId = p.m, userId = p.u, token = p.t }), (ok, body) =>
                    {
                        if (!ok) { valid = null; return; }
                        try
                        {
                            var r = JsonUtility.FromJson<ValidateReply>(body);
                            valid = r != null && r.valid;
                            if (r != null && r.valid) { approvedTeam = r.team; approvedSlot = r.slot; }
                        }
                        catch (Exception) { valid = null; }
                    });
                    if (valid == null) reject = "ACCOUNT SERVICE UNREACHABLE, TRY AGAIN";
                    else if (!valid.Value) reject = "JOIN TICKET REJECTED";
                }
            }

            // 3. one connection per player
            if (reject == null && byUser.TryGetValue(p.u, out var known) && known.connected) reject = "ALREADY CONNECTED";
            if (reject == null && report != ReportState.None) reject = "THE MATCH IS OVER";
            // left the battle or was eliminated: the place is final, so no second life (a reconnect during the warm-up is fine)
            if (reject == null && byUser.TryGetValue(p.u, out var prior) && prior.final) reject = OutOfMatchReason;

            if (reject != null) { Reject(response, reject); yield break; }

            if (string.IsNullOrEmpty(lockedMatchId)) lockedMatchId = p.m;
            if (!byUser.TryGetValue(p.u, out var entry)) { entry = new Entry { userId = p.u }; byUser[p.u] = entry; }
            entry.clientId = clientId; entry.connected = true;
            if (approvedTeam >= 0) { entry.team = approvedTeam; entry.slot = approvedSlot; }
            approvedTeam = -1; approvedSlot = 0;
            byClient[clientId] = entry;
            Debug.Log($"[ServerMatch] Approved client {clientId} as user {p.u} for match {p.m}{(Insecure ? " (INSECURE, not checked by Nakama)" : "")}.");

            response.Approved = true;
            response.CreatePlayerObject = true;
            response.Pending = false;   // last: Netcode reads the other fields once this is false
        }

        /// <summary>The refusal a client gets when it tries to come back into a match it is already out of.</summary>
        public const string OutOfMatchReason = "YOU ARE OUT OF THIS MATCH";

        private void OnClientDisconnected(ulong clientId)
        {
            if (!byClient.TryGetValue(clientId, out var e)) return;
            byClient.Remove(clientId);
            e.connected = false;
            if (e.final || report != ReportState.None) return;

            var c = BRMatchCoordinator.Instance;
            if (c == null || c.State < BRMatchState.ActiveBattle) { e.noReward = true; return; }   // left the lobby or the warm-up: nothing to pay
            if (c.State == BRMatchState.MatchEnded) return;                                        // NoteMatchEnd already decided everybody

            // left a running match: counts as eliminated now (the coordinator's alive count may or may not include them yet)
            e.final = true;
            e.placement = Mathf.Max(2, c.AliveCount);
            e.total = Mathf.Max(e.placement, c.TotalCount);
            e.survived = c.MatchSeconds;
        }

        // ================================================================= results (called by BRMatchCoordinator on the server)
        /// <summary>A combatant was eliminated. The victim's place and survival time are final; the killer's kill count is updated.</summary>
        public static void NoteElimination(ulong victimClient, int placement, int totalPlayers, float survivedSeconds, ulong killerClient, int killerKills, string killerWeapon = "", bool final = true)
        {
            if (instance == null) return;
            if (instance.byClient.TryGetValue(victimClient, out var v) && !v.final)
            {
                // a squad member who falls while the squad fights on: the place is the squad's, decided later
                v.survived = survivedSeconds;
                if (final) { v.final = true; v.placement = Mathf.Max(2, placement); v.total = Mathf.Max(totalPlayers, v.placement); }
            }
            if (killerClient != ulong.MaxValue && instance.byClient.TryGetValue(killerClient, out var k))
            {
                k.kills = Mathf.Max(k.kills, killerKills);
                if (!string.IsNullOrEmpty(killerWeapon))
                {
                    k.weaponKills.TryGetValue(killerWeapon, out int n);
                    k.weaponKills[killerWeapon] = n + 1;
                }
            }
        }

        /// <summary>The match is over: decide everybody who is still undecided (the winner) and report the results to Nakama.</summary>
        public static void NoteMatchEnd(IReadOnlyList<BRParticipant> participants, int totalPlayers, float matchSeconds)
        {
            if (instance == null) return;
            instance.FinishMatch(participants, totalPlayers, matchSeconds);
        }

        private void FinishMatch(IReadOnlyList<BRParticipant> participants, int totalPlayers, float matchSeconds)
        {
            if (participants != null)
            {
                foreach (var p in participants)
                {
                    if (p == null || p.IsBot || p.ClientId == ulong.MaxValue) continue;
                    if (!byClient.TryGetValue(p.ClientId, out var e)) continue;
                    e.kills = Mathf.Max(e.kills, p.Kills);
                    if (e.final) continue;
                    e.final = true;
                    e.placement = p.Placement > 0 ? p.Placement : 1;
                    e.total = Mathf.Max(totalPlayers, e.placement);
                    e.survived = matchSeconds;
                }
            }
            StartReport();
        }

        // ================================================================= reporting
        private void StartReport()
        {
            if (report != ReportState.None) return;
            report = ReportState.Sending;
            StartCoroutine(ReportRoutine());
        }

        private IEnumerator ReportRoutine()
        {
            string matchId = CurrentMatchId();
            var request = new ReportRequest { matchId = matchId };
            foreach (var e in byUser.Values)
            {
                if (e.noReward || !e.final) continue;
                var rp = new ReportPlayer { userId = e.userId, placement = e.placement, totalPlayers = e.total, kills = e.kills, survivedSeconds = Mathf.RoundToInt(e.survived) };
                foreach (var wk in e.weaponKills) rp.weaponKills.Add(new WeaponKill { weapon = wk.Key, kills = wk.Value });
                request.players.Add(rp);
            }
            if (request.players.Count == 0 || string.IsNullOrEmpty(matchId) || Insecure || string.IsNullOrEmpty(HttpKey))
            {
                if (request.players.Count > 0) Debug.LogWarning("[ServerMatch] Results are NOT reported (" + (Insecure ? "-insecure-join" : "no Nakama HTTP key") + ").");
                report = ReportState.Done;
                yield break;
            }

            string body = JsonUtility.ToJson(request);
            for (int attempt = 1; attempt <= 4; attempt++)
            {
                bool ok = false; string reply = null;
                yield return CallRpc(ReportRpc, body, (success, text) => { ok = success; reply = text; });
                if (ok)
                {
                    Debug.Log($"[ServerMatch] Reported {request.players.Count} player result(s) of match {matchId}: {reply}");
                    report = ReportState.Done;
                    yield break;
                }
                Debug.LogWarning($"[ServerMatch] Reporting the match failed (attempt {attempt}/4); retrying.");
                yield return new WaitForSecondsRealtime(2f * attempt);
            }
            report = ReportState.Failed;
            Debug.LogError("[ServerMatch] The match results could not be reported to Nakama: " + body);
        }

        /// <summary>Run by the Agones lifecycle before the server exits: sends what is still unsent (everybody left) and waits for it.</summary>
        public static IEnumerator FlushBeforeShutdown()
        {
            if (instance == null) yield break;
            var s = instance;
            if (s.report == ReportState.None)
            {
                // the match never ended (everybody left): players still connected are decided by the state of the match, the others already are
                var c = BRMatchCoordinator.Instance;
                bool started = c != null && c.State >= BRMatchState.ActiveBattle;
                foreach (var e in s.byUser.Values)
                {
                    if (e.final || e.noReward) continue;
                    if (!started) { e.noReward = true; continue; }
                    e.final = true; e.placement = Mathf.Max(2, c.AliveCount); e.total = Mathf.Max(e.placement, c.TotalCount); e.survived = c.MatchSeconds;
                }
                s.StartReport();
            }
            float waited = 0f;
            while (s != null && s.report == ReportState.Sending && waited < 25f) { waited += Time.unscaledDeltaTime; yield return null; }
        }

        // ================================================================= Nakama HTTP
        private IEnumerator CallRpc(string rpc, string jsonBody, Action<bool, string> done)
        {
            string url = NakamaUrl + "/v2/rpc/" + rpc + "?http_key=" + Uri.EscapeDataString(HttpKey) + "&unwrap";
            using (var req = new UnityWebRequest(url, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = Mathf.CeilToInt(RpcTimeoutSeconds);
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[ServerMatch] Nakama RPC {rpc} failed: {req.responseCode} {req.error} {req.downloadHandler?.text}");
                    done(false, null);
                    yield break;
                }
                done(true, req.downloadHandler.text);
            }
        }
    }
}
