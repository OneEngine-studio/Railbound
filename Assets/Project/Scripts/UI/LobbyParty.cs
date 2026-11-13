using System;
using System.Collections.Generic;
using UnityEngine;

namespace Railbound.UI
{
    /// <summary>One person standing in the lobby party.</summary>
    [Serializable]
    public class PartyMember
    {
        public string id;
        public string displayName;
        public string levelText;
        public int operatorIndex;     // which operator model this player shows
        public bool ready;
        public bool isLocal;
        public bool isLeader;         // the party leader starts the match
    }

    /// <summary>
    /// The lobby party: the local player plus up to <see cref="MaxMembers"/> - 1 friends (4 players in total).
    /// UI (squad panel) and the 3D stage (one model per member) both read from here, so wiring the real friend / party
    /// backend only means calling <see cref="TryAddMember"/> / <see cref="RemoveMember"/> / <see cref="SetReady"/>.
    /// Until that backend exists, <see cref="seedDemoSquad"/> fills the three friend slots with sample players so the
    /// layout can be seen; switch it off for a real build.
    /// </summary>
    [DisallowMultipleComponent]
    public class LobbyParty : MonoBehaviour
    {
        public const int MaxMembers = 4;

        private static LobbyParty _instance;
        public static LobbyParty Instance
        {
            get
            {
                if (_instance == null) _instance = FindObjectOfType<LobbyParty>(true);
                if (_instance == null) _instance = new GameObject("LobbyParty").AddComponent<LobbyParty>();
                return _instance;
            }
        }

        /// <summary>The party if it exists; never creates one (use from OnDestroy / teardown code).</summary>
        public static LobbyParty InstanceIfExists => _instance;

        [Tooltip("Fill the friend slots with sample squadmates (placeholder until the real party backend is connected).")]
        [SerializeField] private bool seedDemoSquad = true;

        private readonly List<PartyMember> members = new List<PartyMember>();
        public IReadOnlyList<PartyMember> Members => members;
        public int Count => members.Count;
        public bool IsFull => members.Count >= MaxMembers;
        public PartyMember Local => members.Count > 0 ? members[0] : null;

        /// <summary>Raised whenever the roster or a member's state changes.</summary>
        public event Action Changed;

        /// <summary>Raised when a different member is put in the spotlight.</summary>
        public event Action SelectionChanged;

        private int selectedIndex;
        /// <summary>Index (into <see cref="Members"/>) of the member in the spotlight. 0 is the local player.</summary>
        public int SelectedIndex => Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, members.Count - 1));
        public PartyMember Selected => members.Count > 0 ? members[SelectedIndex] : null;

        public void Select(int index)
        {
            if (members.Count == 0) return;
            index = Mathf.Clamp(index, 0, members.Count - 1);
            if (index == selectedIndex) return;
            selectedIndex = index;
            SelectionChanged?.Invoke();
        }

        /// <summary>Moves the spotlight to the next (+1) or previous (-1) member, wrapping around.</summary>
        public void SelectOffset(int delta)
        {
            if (members.Count < 2) return;
            Select(((SelectedIndex + delta) % members.Count + members.Count) % members.Count);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(this); return; }
            _instance = this;
            EnsureLocal();

            // a signed-in player must never see made-up squadmates: the real party (Nakama) fills the slots
            bool signedIn = AuthenticationService.Instance != null && AuthenticationService.Instance.IsAuthenticated;
            if (seedDemoSquad && !signedIn)
            {
                TryAddMember(new PartyMember { id = "demo_vex", displayName = "VEX_TACTICAL", levelText = "LVL. 48", operatorIndex = 2, ready = true });
                TryAddMember(new PartyMember { id = "demo_noob", displayName = "NOOB_SHENKY", levelText = "LVL. 3", operatorIndex = 3, ready = false });
                TryAddMember(new PartyMember { id = "demo_rider", displayName = "GHOST_RIDER", levelText = "LVL. 27", operatorIndex = 1, ready = true });
            }
        }

        private void Start()
        {
            // the party outlives the lobby scene (it persists into a match and back): show it again
            var service = Railbound.Networking.NakamaPartyService.InstanceIfExists;
            if (service != null) service.SyncToLobby();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>
        /// Replaces everybody except the local player by <paramref name="remote"/> (the real party roster) and marks whether the
        /// local player leads it. Used by the party service; an empty list means the player is alone.
        /// </summary>
        public void ReplaceRemoteMembers(List<PartyMember> remote, bool localIsLeader)
        {
            EnsureLocal();
            bool changed = false;

            for (int i = members.Count - 1; i >= 1; i--) { members.RemoveAt(i); changed = true; }

            int room = MaxMembers - 1;
            foreach (var m in remote)
            {
                if (room-- <= 0) break;
                m.isLocal = false;
                if (string.IsNullOrEmpty(m.id)) m.id = Guid.NewGuid().ToString("N");
                members.Add(m);
                changed = true;
            }

            if (members[0].isLeader != localIsLeader) { members[0].isLeader = localIsLeader; changed = true; }

            if (selectedIndex >= members.Count) { selectedIndex = 0; SelectionChanged?.Invoke(); }
            if (changed) Changed?.Invoke();
        }

        private void EnsureLocal()
        {
            if (members.Count == 0 || !members[0].isLocal)
                members.Insert(0, new PartyMember { id = "local", displayName = "OPERATOR", levelText = "LVL. 1", operatorIndex = 0, ready = true, isLocal = true });
        }

        /// <summary>Sets the local player's name / level (from the profile) and operator.</summary>
        public void SetLocal(string displayName, string levelText, int operatorIndex)
        {
            EnsureLocal();
            var l = members[0];
            if (!string.IsNullOrEmpty(displayName)) l.displayName = displayName;
            if (!string.IsNullOrEmpty(levelText)) l.levelText = levelText;
            l.operatorIndex = operatorIndex;
            Changed?.Invoke();
        }

        public bool TryAddMember(PartyMember m)
        {
            if (m == null || IsFull) return false;
            if (string.IsNullOrEmpty(m.id)) m.id = Guid.NewGuid().ToString("N");
            if (members.Exists(x => x.id == m.id)) return false;
            m.isLocal = false;
            members.Add(m);
            Changed?.Invoke();
            return true;
        }

        public bool RemoveMember(string id)
        {
            int i = members.FindIndex(x => x.id == id && !x.isLocal);
            if (i < 0) return false;
            members.RemoveAt(i);
            bool selectionMoved = selectedIndex >= members.Count || selectedIndex == i;
            if (selectionMoved) selectedIndex = 0;
            Changed?.Invoke();
            if (selectionMoved) SelectionChanged?.Invoke();
            return true;
        }

        public void SetReady(string id, bool ready)
        {
            var m = members.Find(x => x.id == id);
            if (m == null || m.ready == ready) return;
            m.ready = ready;
            Changed?.Invoke();
        }
    }
}
