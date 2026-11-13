using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nakama;
using Railbound.UI;
using UnityEngine;

namespace Railbound.Networking
{
    /// <summary>
    /// The lobby party on Nakama's party feature: create a party, share its code, join with a code, leave. The roster and each
    /// member's name / level / operator / ready state are mirrored into <see cref="LobbyParty"/>, which the squad panel and the 3D
    /// stage already read from.
    ///
    /// It is a persistent singleton, so the party survives the scene changes into a match and back to the lobby.
    /// A party is created with open = true (anyone who has the code can join, no approval) and hidden = true (it is not listed),
    /// so the party id is the invite code.
    ///
    /// Nakama calls its events from its own thread: they are queued and handled in Update, on the main thread.
    /// </summary>
    public sealed class NakamaPartyService : MonoBehaviour
    {
        private const long OpProfile = 1;   // a member's name, level, operator and ready state
        private const long OpDeploy = 2;    // the leader started the match

        [Serializable]
        private class ProfileMessage { public string n; public string l; public int o; public bool r; }

        private sealed class Remote
        {
            public string UserId, Username;
            public ProfileMessage Profile;
        }

        // ---------------- singleton ----------------
        private static NakamaPartyService _instance;
        private static bool _quitting;

        public static NakamaPartyService Instance
        {
            get
            {
                if (_instance == null && !_quitting)
                {
                    var go = new GameObject("NakamaPartyService");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<NakamaPartyService>();
                }
                return _instance;
            }
        }

        /// <summary>The service if it exists; never creates one.</summary>
        public static NakamaPartyService InstanceIfExists => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _instance = null; _quitting = false; }

        // ---------------- state ----------------
        /// <summary>The party code to share (the Nakama party id); empty when not in a party.</summary>
        public string PartyId { get; private set; } = "";
        public bool InParty => !string.IsNullOrEmpty(PartyId);
        public bool IsLeader { get; private set; }
        public bool Busy { get; private set; }

        /// <summary>
        /// True once the server rejected a party request in this session. The Nakama server must be 3.28 or newer: older servers do
        /// not understand the party fields of the current SDK ("hidden" / "label"), call the message malformed and close the socket.
        /// No more party requests are sent then (each one would drop the connection again).
        /// </summary>
        public bool Unsupported { get; private set; }

        /// <summary>Everybody in the party including the local player (0 when not in a party).</summary>
        public int MemberCount => InParty ? _members.Count : 0;

        /// <summary>User ids of all members including the local player, sorted (the same list on every client).</summary>
        public IReadOnlyList<string> MemberUserIds => _members.Keys.OrderBy(x => x, StringComparer.Ordinal).ToList();

        /// <summary>Raised when the roster, the leader or the state changed.</summary>
        public event Action Changed;
        /// <summary>A short, user-facing line ("PARTY CODE COPIED", "PARTY NOT FOUND OR FULL").</summary>
        public event Action<string> Notice;
        /// <summary>The party leader started the match: every member starts their deployment too.</summary>
        public event Action DeployRequested;

        private ISocket _socket;
        private readonly Dictionary<string, Remote> _members = new Dictionary<string, Remote>();
        private string _leaderId = "";
        private string _lastSentProfile = "";
        private readonly ConcurrentQueue<Action> _inbox = new ConcurrentQueue<Action>();

        private static string LocalUserId => AuthenticationService.Instance != null ? AuthenticationService.Instance.CurrentUserId : "";

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnApplicationQuit() { _quitting = true; }

        private void OnDestroy()
        {
            Detach();
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            while (_inbox.TryDequeue(out var action))
            {
                try { action(); } catch (Exception ex) { Debug.LogWarning("[Party] " + ex.Message); }
            }

            // keep our own profile in the party up to date (the lobby changes the name / operator after the profile loaded)
            if (InParty && !Busy) BroadcastProfileIfChanged();
        }

        // ---------------- actions ----------------
        /// <summary>Creates a party (you are the leader) unless you are already in one.</summary>
        public async Task CreateAsync()
        {
            if (InParty || Busy) return;
            if (Unsupported) { Say(UnsupportedLine); return; }
            Busy = true;
            try
            {
                var socket = await GetSocketAsync();
                if (socket == null) { Say("NOT CONNECTED TO THE SERVER"); return; }

                var party = await socket.CreatePartyAsync(true, true, LobbyParty.MaxMembers);
                HandleParty(party);
                Say("PARTY CREATED - SHARE THE CODE");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Party] Could not create a party: " + ex.GetType().Name + ": " + ex.Message);
                if (LooksLikeRejectedByServer(ex)) { Unsupported = true; Say(UnsupportedLine); }
                else Say("COULD NOT CREATE A PARTY - TRY AGAIN");
            }
            finally { Busy = false; Changed?.Invoke(); }
        }

        /// <summary>Joins the party with this code (leaves the current one first).</summary>
        public async Task JoinAsync(string code)
        {
            code = (code ?? "").Trim();
            if (code.Length == 0) { Say("ENTER A PARTY CODE"); return; }
            if (Busy) return;
            if (Unsupported) { Say(UnsupportedLine); return; }
            if (InParty && code == PartyId) { Say("YOU ARE ALREADY IN THIS PARTY"); return; }

            Busy = true;
            try
            {
                var socket = await GetSocketAsync();
                if (socket == null) { Say("NOT CONNECTED TO THE SERVER"); return; }

                if (InParty) await LeaveInternalAsync(socket);

                await socket.JoinPartyAsync(code);
                // the party details (members, leader) arrive as the ReceivedParty event; remember the code meanwhile
                PartyId = code;
                Say("JOINED THE PARTY");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Party] Could not join '" + code + "': " + ex.GetType().Name + ": " + ex.Message);
                ResetState();
                if (LooksLikeRejectedByServer(ex)) { Unsupported = true; Say(UnsupportedLine); }
                else Say("PARTY NOT FOUND, FULL OR CLOSED");
            }
            finally { Busy = false; Changed?.Invoke(); }
        }

        public async Task LeaveAsync()
        {
            if (!InParty || Busy) return;
            Busy = true;
            try
            {
                var socket = await GetSocketAsync();
                if (socket != null) await LeaveInternalAsync(socket);
                Say("YOU LEFT THE PARTY");
            }
            catch (Exception ex) { Debug.LogWarning("[Party] Leave failed: " + ex.Message); ResetState(); }
            finally { Busy = false; Changed?.Invoke(); }
        }

        /// <summary>The leader tells the other members to start their deployment as well.</summary>
        public async Task BroadcastDeployAsync()
        {
            if (!InParty || !IsLeader || _socket == null) return;
            try { await _socket.SendPartyDataAsync(PartyId, OpDeploy, "go"); }
            catch (Exception ex) { Debug.LogWarning("[Party] Could not tell the party to deploy: " + ex.Message); }
        }

        private const string UnsupportedLine = "PARTIES ARE NOT AVAILABLE ON THIS SERVER YET";

        // An old server answers a party request it cannot parse by closing the socket: the pending call is cancelled at once
        // (a normal refusal, for example "party full", is an ApiResponse / socket error with a message instead).
        private bool LooksLikeRejectedByServer(Exception ex)
        {
            return ex is TaskCanceledException || ex is OperationCanceledException || (_socket != null && !_socket.IsConnected);
        }

        /// <summary>Copies the party code to the clipboard.</summary>
        public void CopyCode()
        {
            if (!InParty) return;
            GUIUtility.systemCopyBuffer = PartyId;
            Say("PARTY CODE COPIED");
        }

        /// <summary>Pushes the current roster into the lobby's party model (call when the lobby appears).</summary>
        public void SyncToLobby() { ApplyRosterToLobby(); }

        // ---------------- socket and events ----------------
        private async Task<ISocket> GetSocketAsync()
        {
            var auth = AuthenticationService.Instance;
            if (auth == null || !auth.IsAuthenticated) return null;
            var socket = await auth.EnsureSocketAsync();
            if (socket == null || !socket.IsConnected) return null;
            Attach(socket);
            return socket;
        }

        private void Attach(ISocket socket)
        {
            if (ReferenceEquals(_socket, socket)) return;
            // a new socket means the old one closed: the party it belonged to is gone
            Detach();
            if (InParty) ResetState();
            _socket = socket;
            socket.ReceivedParty += OnParty;
            socket.ReceivedPartyPresence += OnPresence;
            socket.ReceivedPartyData += OnData;
            socket.ReceivedPartyLeader += OnLeader;
            socket.ReceivedPartyClose += OnClose;
        }

        private void Detach()
        {
            if (_socket == null) return;
            _socket.ReceivedParty -= OnParty;
            _socket.ReceivedPartyPresence -= OnPresence;
            _socket.ReceivedPartyData -= OnData;
            _socket.ReceivedPartyLeader -= OnLeader;
            _socket.ReceivedPartyClose -= OnClose;
            _socket = null;
        }

        private async Task LeaveInternalAsync(ISocket socket)
        {
            string id = PartyId;
            ResetState();
            if (!string.IsNullOrEmpty(id)) await socket.LeavePartyAsync(id);
        }

        private void OnParty(IParty party) { _inbox.Enqueue(() => HandleParty(party)); }
        private void OnPresence(IPartyPresenceEvent e) { _inbox.Enqueue(() => HandlePresence(e)); }
        private void OnData(IPartyData d) { _inbox.Enqueue(() => HandleData(d)); }
        private void OnLeader(IPartyLeader l) { _inbox.Enqueue(() => HandleLeader(l)); }
        private void OnClose(IPartyClose c) { _inbox.Enqueue(() => HandleClose(c)); }

        private void HandleParty(IParty party)
        {
            if (party == null) return;
            PartyId = party.Id;
            _members.Clear();
            if (party.Presences != null) foreach (var p in party.Presences) AddMember(p);
            if (party.Self != null) AddMember(party.Self);
            _leaderId = party.Leader != null ? party.Leader.UserId : "";
            IsLeader = _leaderId == LocalUserId;
            _lastSentProfile = "";   // a new roster: everybody needs our profile again
            RosterChanged();
            BroadcastProfileIfChanged();
        }

        private void HandlePresence(IPartyPresenceEvent e)
        {
            if (e == null || e.PartyId != PartyId) return;
            bool someoneJoined = false;
            if (e.Joins != null) foreach (var p in e.Joins) { AddMember(p); someoneJoined = true; }
            if (e.Leaves != null) foreach (var p in e.Leaves) _members.Remove(p.UserId);
            if (someoneJoined) _lastSentProfile = "";   // the newcomer has not seen our profile
            RosterChanged();
            BroadcastProfileIfChanged();
        }

        private void HandleLeader(IPartyLeader l)
        {
            if (l == null || l.PartyId != PartyId || l.Presence == null) return;
            _leaderId = l.Presence.UserId;
            IsLeader = _leaderId == LocalUserId;
            RosterChanged();
        }

        private void HandleClose(IPartyClose c)
        {
            if (c == null || c.PartyId != PartyId) return;
            ResetState();
            Say("THE PARTY WAS CLOSED");
            Changed?.Invoke();
        }

        private void HandleData(IPartyData d)
        {
            if (d == null || d.PartyId != PartyId || d.Presence == null) return;
            if (d.Presence.UserId == LocalUserId) return;

            if (d.OpCode == OpProfile)
            {
                try
                {
                    var msg = JsonUtility.FromJson<ProfileMessage>(System.Text.Encoding.UTF8.GetString(d.Data));
                    if (msg != null && _members.TryGetValue(d.Presence.UserId, out var r)) { r.Profile = msg; RosterChanged(); }
                }
                catch (Exception ex) { Debug.LogWarning("[Party] Bad profile message: " + ex.Message); }
            }
            else if (d.OpCode == OpDeploy && d.Presence.UserId == _leaderId)
            {
                DeployRequested?.Invoke();
            }
        }

        // ---------------- roster ----------------
        private void AddMember(IUserPresence p)
        {
            if (p == null || string.IsNullOrEmpty(p.UserId)) return;
            if (_members.TryGetValue(p.UserId, out var existing)) { existing.Username = p.Username; return; }
            _members[p.UserId] = new Remote { UserId = p.UserId, Username = p.Username };
        }

        private void RosterChanged()
        {
            ApplyRosterToLobby();
            Changed?.Invoke();
        }

        private void ApplyRosterToLobby()
        {
            var lobby = LobbyParty.InstanceIfExists;
            if (lobby == null) return;

            var others = new List<PartyMember>();
            foreach (var kv in _members.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                if (kv.Key == LocalUserId) continue;
                var r = kv.Value;
                var p = r.Profile;
                others.Add(new PartyMember
                {
                    id = r.UserId,
                    displayName = !string.IsNullOrWhiteSpace(p != null ? p.n : null) ? p.n : (r.Username ?? "PLAYER").ToUpperInvariant(),
                    levelText = p != null && !string.IsNullOrEmpty(p.l) ? p.l : "",
                    operatorIndex = p != null ? p.o : 0,
                    ready = p == null || p.r,
                    isLeader = r.UserId == _leaderId,
                });
            }
            lobby.ReplaceRemoteMembers(InParty ? others : new List<PartyMember>(), InParty && IsLeader);
        }

        private void BroadcastProfileIfChanged()
        {
            var lobby = LobbyParty.InstanceIfExists;
            if (!InParty || _socket == null || lobby == null || lobby.Local == null) return;

            var l = lobby.Local;
            var msg = new ProfileMessage { n = l.displayName, l = l.levelText, o = l.operatorIndex, r = l.ready };
            string json = JsonUtility.ToJson(msg);
            if (json == _lastSentProfile) return;
            _lastSentProfile = json;
            _ = SendProfileAsync(json);
        }

        private async Task SendProfileAsync(string json)
        {
            try { await _socket.SendPartyDataAsync(PartyId, OpProfile, json); }
            catch (Exception ex) { _lastSentProfile = ""; Debug.LogWarning("[Party] Could not send the profile: " + ex.Message); }
        }

        private void ResetState()
        {
            PartyId = "";
            IsLeader = false;
            _leaderId = "";
            _members.Clear();
            _lastSentProfile = "";
            ApplyRosterToLobby();
        }

        private void Say(string line)
        {
            Debug.Log("[Party] " + line);
            Notice?.Invoke(line);
        }
    }
}
