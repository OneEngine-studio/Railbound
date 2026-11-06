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
    }

    /// <summary>
    /// The lobby party: the local player plus up to <see cref="MaxMembers"/> - 1 friends (3 players in total).
    /// UI (squad panel) and the 3D stage (one model per member) both read from here, so wiring the real friend / party
    /// backend only means calling <see cref="TryAddMember"/> / <see cref="RemoveMember"/> / <see cref="SetReady"/>.
    /// Until that backend exists, <see cref="seedDemoSquad"/> fills the two friend slots with sample players so the
    /// layout can be seen; switch it off for a real build.
    /// </summary>
    [DisallowMultipleComponent]
    public class LobbyParty : MonoBehaviour
    {
        public const int MaxMembers = 3;

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

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(this); return; }
            _instance = this;
            EnsureLocal();
            if (seedDemoSquad)
            {
                TryAddMember(new PartyMember { id = "demo_vex", displayName = "VEX_TACTICAL", levelText = "LVL. 48", operatorIndex = 2, ready = true });
                TryAddMember(new PartyMember { id = "demo_noob", displayName = "NOOB_SHENKY", levelText = "LVL. 3", operatorIndex = 3, ready = false });
            }
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
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
            Changed?.Invoke();
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
