using System;
using System.Collections.Generic;
using JUTPS;
using Project.AI.RL;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Railbound.BR
{
    public enum BRMatchState
    {
        WaitingForPlayers = 0,
        Warmup = 1,
        ActiveBattle = 2,
        FinalShowdown = 3,
        MatchEnded = 4
    }

    /// <summary>What the killfeed, the HUD and the results screen need to know about one elimination.</summary>
    public struct BRKillInfo
    {
        public string Killer;          // gamertag, or THE STORM / ENVIRONMENT
        public string Victim;
        public ulong VictimClientId;   // ulong.MaxValue for bots / offline players
        public ulong KillerClientId;
        public int Placement;          // place the victim finished in
        public int AliveAfter;
        public bool KillerIsLocal;
        public bool VictimIsLocal;
    }

    /// <summary>
    /// The match itself: who is in it, who is still alive, who killed whom and who won.
    ///
    /// It runs on the server (or in offline play) and mirrors its state to every client:
    ///   WaitingForPlayers -> Warmup (countdown, bots held, backfilled to <see cref="Capacity"/>)
    ///   -> ActiveBattle (storm starts, bots released) -> FinalShowdown (few left) -> MatchEnded (one left).
    /// It is added at runtime next to the <see cref="BRZoneManager"/>, so a Battle Royale scene needs no extra setup.
    /// Same pattern as the zone manager: networked state when a session is running, plain local state otherwise.
    /// </summary>
    public class BRMatchCoordinator : NetworkBehaviour
    {
        public static BRMatchCoordinator Instance { get; private set; }

        /// <summary>Set by the Agones lifecycle when Nakama allocated this server for a lobby of N players (0 = unknown).</summary>
        public static int ExpectedHumans;

        [Header("Match")]
        [Tooltip("Total combatants: real players plus bots that fill the empty slots.")]
        public int Capacity = 20;
        [Tooltip("Seconds between the first player being ready and the storm starting.")]
        public float WarmupSeconds = 10f;
        [Tooltip("Enter the FinalShowdown state when this many (or fewer) are alive.")]
        public int FinalShowdownAlive = 4;
        [Tooltip("Seconds the results stay up before everybody goes back to the lobby.")]
        public float ReturnToLobbyDelay = 25f;
        public string LobbySceneName = "GameLobby";
        [Tooltip("Dedicated server only: after the first player arrives, wait this long for more before the warmup starts (a lobby of Capacity/2 real players starts it at once).")]
        public float LobbyWindowSeconds = 30f;
        [Tooltip("Real players that start the warmup immediately, without waiting for the whole window.")]
        public int EarlyStartHumans = 10;

        // ---------------------------------------------------------------- replicated state
        private readonly NetworkVariable<int> netState = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> netAlive = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> netTotal = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> netWarmup = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<FixedString64Bytes> netWinner = new NetworkVariable<FixedString64Bytes>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private BRMatchState localState = BRMatchState.WaitingForPlayers;
        private int localAlive, localTotal;
        private float localWarmup;
        private string localWinner = "";

        // set when this machine was a client of a game server and the connection is gone: the match is frozen at the state it had
        private bool wasNetworkClient, lostServer;
        private BRMatchState frozenState = BRMatchState.WaitingForPlayers;

        /// <summary>True once the connection to the game server is gone (the HUD shows a banner and the player is sent to the lobby).</summary>
        public bool ServerConnectionLost => lostServer;
        /// <summary>True when the connection ended after the match was already over (the server closed), false for a real loss.</summary>
        public bool ServerConnectionLostAfterMatch { get; private set; }

        public BRMatchState State => lostServer
            ? frozenState
            : ((NetworkActive && IsSpawned && (IsServer || localState != BRMatchState.MatchEnded)) ? (BRMatchState)netState.Value : localState);
        public int AliveCount => (!Headless && (rosterBuilt || localTotal > 0)) ? localAlive : (NetworkActive && IsSpawned ? netAlive.Value : localAlive);
        public int TotalCount => (!Headless && (rosterBuilt || localTotal > 0)) ? localTotal : (NetworkActive && IsSpawned ? netTotal.Value : localTotal);
        public float WarmupRemaining => NetworkActive && IsSpawned ? netWarmup.Value : localWarmup;
        public string WinnerName => (!Headless && !string.IsNullOrEmpty(localWinner)) ? localWinner : (NetworkActive && IsSpawned ? netWinner.Value.ToString() : localWinner);
        public bool IsMatchLive => State == BRMatchState.ActiveBattle || State == BRMatchState.FinalShowdown;
        public bool ZoneMayRun => State >= BRMatchState.ActiveBattle;
        public float MatchSeconds { get; private set; }

        // ---------------------------------------------------------------- events (fire on every peer that has a screen)
        public static event Action<BRMatchState> StateChanged;
        public static event Action<BRKillInfo> KillOccurred;
        public static event Action<BRKillInfo> LocalEliminated;
        public static event Action<string, bool> MatchEnded;   // winner name, is the winner the local player

        // ---------------------------------------------------------------- participants (authority side)
        private readonly List<BRParticipant> participants = new List<BRParticipant>();
        private readonly HashSet<string> usedNames = new HashSet<string>();
        private System.Random rng;
        private float nextScan, stableHumanTime;
        private bool rosterBuilt, botsHeld, endHandled;
        private BRMatchState lastReportedState = (BRMatchState)(-1);
        private readonly List<Behaviour> heldBotBrains = new List<Behaviour>();

        private static bool NetworkActive => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        private bool IsAuthority => !NetworkActive || NetworkManager.Singleton.IsServer;

        /// <summary>Adds the coordinator (and, on machines with a screen, the HUD) next to <paramref name="host"/> if missing.</summary>
        public static BRMatchCoordinator EnsureExists(GameObject host)
        {
            if (Instance != null) return Instance;
            var existing = host.GetComponent<BRMatchCoordinator>();
            return existing != null ? existing : host.AddComponent<BRMatchCoordinator>();
        }

        private static bool Headless
        {
            get
            {
                if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return true;
                string[] args = Environment.GetCommandLineArgs();
                return Array.Exists(args, a => string.Equals(a, "-server", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "-dedicated", StringComparison.OrdinalIgnoreCase));
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            int seed = BRMatchSetup.Instance != null && BRMatchSetup.Instance.ActiveSeed != 0 ? BRMatchSetup.Instance.ActiveSeed : 20260104;
            rng = new System.Random(seed);
            if (!Headless && GetComponent<BRMatchHUD>() == null) gameObject.AddComponent<BRMatchHUD>();
        }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        // ================================================================= update loop
        private void Update()
        {
            WatchServerConnection();
            if (lostServer) { ReportStateChange(); return; }   // never carry on as a local match without the server

            if (IsAuthority) AuthorityTick();
            else ClientTick();
            ReportStateChange();
        }

        /// <summary>
        /// A client that was playing on a dedicated server must not turn into the authority of a new local match when the
        /// connection ends. The server closes after the match, crashes or the network drops; <c>IsAuthority</c> is
        /// "!NetworkActive || IsServer", so without this the client restarted warm-up, bots and zone locally and stayed in MAP-2
        /// instead of going back to the lobby.
        /// </summary>
        private void WatchServerConnection()
        {
            if (lostServer) return;
            var nm = NetworkManager.Singleton;
            bool asClient = nm != null && nm.IsListening && nm.IsClient && !nm.IsServer;
            if (asClient)
            {
                wasNetworkClient = true;
                frozenState = State;   // remember where the match was
                return;
            }
            if (!wasNetworkClient) return;   // offline / host / server: nothing to guard

            lostServer = true;
            bool matchWasOver = frozenState == BRMatchState.MatchEnded;
            Debug.LogWarning($"[BRMatch] The game server connection ended (match state was {frozenState}); returning to the lobby.");
            ServerConnectionLostAfterMatch = matchWasOver;
            // the match HUD shows a banner now; the lobby repeats the reason once the player is back there
            Railbound.UI.LobbyNotice.Set(matchWasOver ? "THE MATCH SERVER HAS CLOSED" : "CONNECTION TO THE MATCH WAS LOST",
                matchWasOver ? Railbound.UI.LobbyNotice.Kind.Info : Railbound.UI.LobbyNotice.Kind.Problem);
            StartCoroutine(LeaveAfterConnectionLost());
        }

        private System.Collections.IEnumerator LeaveAfterConnectionLost()
        {
            // a moment to read the message (and to let a running results screen show), then back to the lobby
            yield return new WaitForSecondsRealtime(3f);
            LeaveMatch();
        }

        private void ClientTick()
        {
            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + (State == BRMatchState.WaitingForPlayers || State == BRMatchState.Warmup ? 0.4f : 1.0f);
                ScanForCombatants();
            }

            if (State >= BRMatchState.Warmup && !rosterBuilt && participants.Count > 0)
            {
                BuildBotRoster();
            }

            if (State < BRMatchState.ActiveBattle && !botsHeld && participants.Count > 0)
            {
                HoldBots(true);
            }
            else if (State >= BRMatchState.ActiveBattle && botsHeld)
            {
                HoldBots(false);
            }

            PollEliminations();

            if (State == BRMatchState.ActiveBattle || State == BRMatchState.FinalShowdown)
            {
                MatchSeconds += Time.deltaTime;
                if (rosterBuilt && localAlive <= 1 && !endHandled) EndMatch();
            }
        }

        private void AuthorityTick()
        {
            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + (State == BRMatchState.WaitingForPlayers || State == BRMatchState.Warmup ? 0.4f : 2f);
                ScanForCombatants();
            }

            PollEliminations();

            switch (State)
            {
                case BRMatchState.WaitingForPlayers:
                {
                    int humans = CountHumans();
                    stableHumanTime = humans >= 1 ? stableHumanTime + Time.unscaledDeltaTime : 0f;
                    // a dedicated server collects a lobby first; a host or offline game has nobody else to wait for
                    bool dedicated = NetworkActive && NetworkManager.Singleton.IsServer && !NetworkManager.Singleton.IsClient;
                    float needed = dedicated ? LobbyWindowSeconds : 1.5f;
                    int fullLobby = ExpectedHumans > 0 ? ExpectedHumans : EarlyStartHumans;
                    if (dedicated && humans >= fullLobby) needed = 1.5f;
                    if (stableHumanTime >= needed) BeginWarmup();
                    break;
                }

                case BRMatchState.Warmup:
                    SetWarmup(Mathf.Max(0f, WarmupRemaining - Time.deltaTime));
                    if (WarmupRemaining <= 0f) BeginBattle();
                    break;

                case BRMatchState.ActiveBattle:
                case BRMatchState.FinalShowdown:
                    MatchSeconds += Time.deltaTime;
                    if (State == BRMatchState.ActiveBattle && localAlive <= FinalShowdownAlive && localAlive > 1) SetState(BRMatchState.FinalShowdown);
                    if (localAlive <= 1 && !endHandled) EndMatch();
                    break;
            }
        }

        // ================================================================= state changes (authority)
        private void SetState(BRMatchState s)
        {
            localState = s;
            if (NetworkActive && IsSpawned && IsServer) netState.Value = (int)s;
        }

        private void SetWarmup(float v)
        {
            localWarmup = v;
            if (NetworkActive && IsSpawned && IsServer) netWarmup.Value = v;
        }

        private void SetCounts(int alive, int total)
        {
            localAlive = alive; localTotal = total;
            if (NetworkActive && IsSpawned && IsServer) { netAlive.Value = alive; netTotal.Value = total; }
        }

        private void BeginWarmup()
        {
            HoldBots(true);
            BuildBotRoster();
            SetWarmup(WarmupSeconds);
            SetState(BRMatchState.Warmup);
        }

        private void BeginBattle()
        {
            HoldBots(false);
            MatchSeconds = 0f;
            SetState(BRMatchState.ActiveBattle);
            BRFeedNotificationManager.Instance?.ShowInfo("THE MATCH HAS BEGUN - " + localAlive + " COMBATANTS DEPLOYED");
        }

        private void EndMatch()
        {
            endHandled = true;
            BRParticipant winner = null;
            foreach (var p in participants) if (p != null && p.IsAlive) { winner = p; break; }
            if (winner == null)
            {
                // everybody died at once: the last one eliminated wins
                float latest = -1f;
                foreach (var p in participants) if (p != null && p.EliminatedAtTime > latest) { latest = p.EliminatedAtTime; winner = p; }
            }

            string name = winner != null ? winner.DisplayName : "NOBODY";
            if (winner != null) { winner.Placement = 1; winner.Eliminated = false; }
            localWinner = name;
            if (NetworkActive && IsSpawned && IsServer) netWinner.Value = new FixedString64Bytes(name);
            SetState(BRMatchState.MatchEnded);

            ulong winnerClient = winner != null ? winner.ClientId : ulong.MaxValue;
            if (NetworkActive && IsSpawned && IsServer) MatchEndedClientRpc(name, winnerClient);
            else RaiseMatchEnded(name, winnerClient);
        }

        // ================================================================= participants
        private void ScanForCombatants()
        {
            var found = FindObjectsByType<JUCharacterController>(FindObjectsSortMode.None);
            for (int i = 0; i < found.Length; i++)
            {
                var c = found[i];
                if (c == null || !c.gameObject.activeInHierarchy) continue;
                bool isPlayerTag = c.CompareTag("Player"), isEnemyTag = c.CompareTag("Enemy");
                if (!isPlayerTag && !isEnemyTag) continue;
                var health = c.GetComponent<JUHealth>();
                if (health == null || health.IsDead) continue;

                var p = c.GetComponent<BRParticipant>();
                if (p == null)
                {
                    bool isBot = c.GetComponent<JUCharacterRLAgent>() != null || isEnemyTag;
                    // Once the bot roster is built, ignore any stray extra bots, but always register human players and initial scene bots
                    if (isBot && rosterBuilt && State >= BRMatchState.ActiveBattle) continue;
                    p = c.gameObject.AddComponent<BRParticipant>();
                    p.Controller = c; p.Health = health;
                    p.IsBot = isBot;
                    var no = c.GetComponent<NetworkObject>();
                    if (!p.IsBot && no != null && no.IsSpawned) p.ClientId = no.OwnerClientId;
                    p.DisplayName = p.IsBot ? BRIdentityPool.Take(usedNames, rng) : HumanName(c, p.ClientId);
                    participants.Add(p);
                    if (State < BRMatchState.ActiveBattle && p.IsBot) HoldBot(c);
                }
                else if (!p.IsBot && p.ClientId == ulong.MaxValue && c.TryGetComponent(out NetworkObject no) && no.IsSpawned)
                {
                    p.ClientId = no.OwnerClientId;
                }
            }

            // destroyed bots stay listed on purpose: their elimination was already counted
            RecountAlive();
        }

        private string HumanName(JUCharacterController c, ulong clientId)
        {
            string saved = null;
            try { var auth = AuthenticationService.Instance; if (auth != null && auth.IsAuthenticated && auth.Session != null) saved = auth.Session.Username; }
            catch (Exception) { /* offline editor play: no account */ }
            bool local = !NetworkActive || (c.TryGetComponent(out NetworkObject no) && no.IsOwner);
            if (local && !string.IsNullOrWhiteSpace(saved)) return saved;
            return clientId != ulong.MaxValue ? "Player" + (clientId + 1) : (local ? "YOU" : BRIdentityPool.Take(usedNames, rng));
        }

        private int CountHumans()
        {
            int n = 0;
            foreach (var p in participants) if (p != null && !p.IsBot && p.IsAlive) n++;
            return n;
        }

        private void RecountAlive()
        {
            int alive = 0;
            foreach (var p in participants) if (p != null && p.IsAlive) alive++;
            SetCounts(alive, participants.Count);
        }

        public BRParticipant LocalParticipant
        {
            get
            {
                foreach (var p in participants)
                {
                    if (p == null || p.IsBot) continue;
                    if (!NetworkActive) return p;
                    if (p.TryGetComponent(out NetworkObject no) && no.IsOwner) return p;
                }
                return null;
            }
        }

        public IReadOnlyList<BRParticipant> Participants => participants;

        public BRParticipant FindByName(string displayName)
        {
            foreach (var p in participants) if (p != null && p.DisplayName == displayName) return p;
            return null;
        }

        // ================================================================= bots
        private void BuildBotRoster()
        {
            if (rosterBuilt) return;
            rosterBuilt = true;

            var bots = new List<BRParticipant>();
            foreach (var p in participants) if (p != null && p.IsBot) bots.Add(p);
            int humans = CountHumans();
            int wanted = Mathf.Max(0, Capacity - humans);

            // too many: retire the surplus so the match is exactly Capacity strong
            for (int i = bots.Count - 1; i >= 0 && bots.Count > wanted; i--)
            {
                participants.Remove(bots[i]);
                bots[i].gameObject.SetActive(false);
                bots.RemoveAt(i);
            }

            // too few: clone existing combatants and drop them on free ground
            var setup = BRMatchSetup.Instance;
            int guard = 0;
            while (bots.Count < wanted && bots.Count > 0 && guard++ < 64)
            {
                var template = bots[rng.Next(bots.Count)];
                var clone = Instantiate(template.gameObject, template.transform.position, template.transform.rotation);
                clone.name = "BR_Combatant_Backfill_" + guard;
                var cp = clone.GetComponent<BRParticipant>();
                if (cp != null) DestroyImmediate(cp);      // the clone gets its own identity below
                // the template's brains are held (disabled): the clone's copies must be held too and released with the others
                var heldTypes = new HashSet<Type>();
                foreach (var hb in heldBotBrains) if (hb != null && hb.gameObject == template.gameObject) heldTypes.Add(hb.GetType());
                foreach (var t in heldTypes) if (clone.GetComponent(t) is Behaviour cb && !heldBotBrains.Contains(cb)) heldBotBrains.Add(cb);
                var agent = clone.GetComponent<JUCharacterRLAgent>();
                if (setup != null && agent != null) setup.TryPlaceBot(agent);
                // register right away so the loop counts it
                var newP = clone.AddComponent<BRParticipant>();
                newP.Controller = clone.GetComponent<JUCharacterController>();
                newP.Health = clone.GetComponent<JUHealth>();
                newP.IsBot = true;
                newP.DisplayName = BRIdentityPool.Take(usedNames, rng);
                participants.Add(newP);
                bots.Add(newP);
            }

            RecountAlive();
            Debug.Log("[BRMatchCoordinator] Roster ready: humans=" + humans + " bots=" + bots.Count + " total=" + localTotal + " (capacity " + Capacity + ")");
        }

        private static readonly string[] BotBrainTypes = { "PatrolAI", "JUCharacterRLAgent", "DecisionRequester" };

        private void HoldBots(bool hold)
        {
            botsHeld = hold;
            if (hold)
            {
                foreach (var p in participants) if (p != null && p.IsBot) HoldBot(p.Controller);
                return;
            }
            for (int i = 0; i < heldBotBrains.Count; i++) if (heldBotBrains[i] != null) heldBotBrains[i].enabled = true;
            heldBotBrains.Clear();
        }

        private void HoldBot(JUCharacterController c)
        {
            if (c == null) return;
            foreach (var b in c.GetComponents<Behaviour>())
            {
                if (b == null || !b.enabled) continue;
                string t = b.GetType().Name;
                if (Array.IndexOf(BotBrainTypes, t) < 0) continue;
                b.enabled = false;
                heldBotBrains.Add(b);
            }
        }

        // ================================================================= eliminations
        private void PollEliminations()
        {
            if (participants.Count == 0) return;
            for (int i = 0; i < participants.Count; i++)
            {
                var p = participants[i];
                if (p == null) continue;                    // destroyed after death: already counted when it died
                if (p.Eliminated) continue;
                if (p.Health == null) continue;
                if (p.Health.IsDead || p.Health.Health <= 0f) Eliminate(p);
            }
            RecountAlive();
        }

        private void Eliminate(BRParticipant victim)
        {
            if (victim.Eliminated) return;

            int aliveBefore = 0;
            foreach (var p in participants) if (p != null && p.IsAlive) aliveBefore++;
            // a participant already flagged dead by Health is no longer "alive" in IsAlive: make sure the victim itself is counted
            if (!victim.IsAlive) aliveBefore++;

            victim.Eliminated = true;
            victim.EliminatedAtTime = Time.time;
            victim.Placement = Mathf.Max(2, aliveBefore);

            // kill credit: whoever damaged the victim last, if recently
            BRParticipant killer = null;
            var h = victim.Health;
            if (h != null && h.LastAttacker != null && Time.time - h.LastAttackTime < 12f)
            {
                killer = h.LastAttacker.GetComponentInParent<BRParticipant>();
                if (killer == victim) killer = null;
            }
            string killerName;
            if (killer != null) { killer.Kills++; killerName = killer.DisplayName; }
            else
            {
                var zone = BRZoneManager.Instance;
                bool outside = zone != null && !zone.IsPositionInSafeZone(victim.transform.position);
                killerName = outside ? "THE STORM" : "ENVIRONMENT";
            }

            RecountAlive();
            var info = new BRKillInfo
            {
                Killer = killerName,
                Victim = victim.DisplayName,
                VictimClientId = victim.ClientId,
                KillerClientId = killer != null ? killer.ClientId : ulong.MaxValue,
                Placement = victim.Placement,
                AliveAfter = localAlive
            };

            if (NetworkActive && IsSpawned && IsServer) KillClientRpc(info.Killer, info.Victim, info.VictimClientId, info.KillerClientId, info.Placement, info.AliveAfter);
            else RaiseKill(info);
        }

        // ================================================================= networking
        [ClientRpc]
        private void KillClientRpc(string killer, string victim, ulong victimClient, ulong killerClient, int placement, int aliveAfter)
        {
            // When running as a client, local PollEliminations() tracks the scene's bots and local player directly;
            // ignore server-only bot-vs-bot kills so the client's killfeed and placement match its own scene.
            if (!IsServer && !Headless && victimClient == ulong.MaxValue && killerClient == ulong.MaxValue) return;
            RaiseKill(new BRKillInfo { Killer = killer, Victim = victim, VictimClientId = victimClient, KillerClientId = killerClient, Placement = placement, AliveAfter = aliveAfter });
        }

        [ClientRpc]
        private void MatchEndedClientRpc(string winner, ulong winnerClient)
        {
            // The server decides when the match is over. (Bots are not replicated: this client simulates its own copy, so its
            // own alive count can still be above one when the server's match has ended. Ignoring the result here meant the HUD
            // never showed the results or the return countdown, and the server then shut down under the player.)
            RaiseMatchEnded(winner, winnerClient);
        }

        private void RaiseKill(BRKillInfo info)
        {
            ulong me = NetworkActive ? NetworkManager.Singleton.LocalClientId : ulong.MaxValue;
            var lp = LocalParticipant;
            info.VictimIsLocal = (NetworkActive && info.VictimClientId != ulong.MaxValue && info.VictimClientId == me)
                              || (lp != null && lp.DisplayName == info.Victim);
            info.KillerIsLocal = (NetworkActive && info.KillerClientId != ulong.MaxValue && info.KillerClientId == me)
                              || (lp != null && lp.DisplayName == info.Killer);
            KillOccurred?.Invoke(info);
            if (info.VictimIsLocal) LocalEliminated?.Invoke(info);
        }

        private void RaiseMatchEnded(string winner, ulong winnerClient)
        {
            ulong me = NetworkActive ? NetworkManager.Singleton.LocalClientId : ulong.MaxValue;
            var lp = LocalParticipant;
            bool local = (NetworkActive && winnerClient != ulong.MaxValue && winnerClient == me)
                      || (lp != null && lp.DisplayName == winner);
            MatchEnded?.Invoke(winner, local);
        }

        private void ReportStateChange()
        {
            var s = State;
            if (s == lastReportedState) return;
            lastReportedState = s;
            StateChanged?.Invoke(s);
        }

        // ================================================================= leaving
        /// <summary>Back to the lobby: stop the Netcode session if one is running, restore time and cursor, load the lobby scene.</summary>
        public void LeaveMatch()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            // leave the game server through the connection singleton so it forgets the address and does not report a lost connection
            if (Railbound.Networking.GameServerConnection.Instance != null) Railbound.Networking.GameServerConnection.Instance.Clear();
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) NetworkManager.Singleton.Shutdown();
            Railbound.UI.SceneTransition.Go(LobbySceneName, "RETURNING TO LOBBY");
        }
    }
}
