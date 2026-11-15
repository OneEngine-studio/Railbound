using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using JUTPS.InventorySystem;

namespace Railbound.BR
{
    /// <summary>
    /// Keeps the loot of a networked match identical on every peer. The loot itself stays plain local objects (weapons,
    /// ammo boxes, medkits): <see cref="BRMatchSetup"/> registers each one in placement order, which gives it an id that is
    /// the same on every peer, and this component replicates what happens to them:
    ///
    ///  * Layout: the server publishes where every item was placed; a client whose own placement differs (physics can
    ///    differ slightly between machines) moves its items onto the server's positions.
    ///  * Pickups: a peer that sees an item taken (picked up by a player or a bot, or destroyed) reports the id; the server
    ///    adds it to <c>taken</c> and every other peer hides that item. A late joiner receives the whole list.
    ///  * Drops: loot dropped by a dying bot is made on the server only; the server announces it so the clients create
    ///    the same pickup under the same id.
    /// Offline (no network session) it does nothing.
    /// </summary>
    public class BRLootSync : NetworkBehaviour
    {
        public static BRLootSync Instance { get; private set; }

        [Tooltip("Pickups a dying bot drops (the same prefabs as BattleRoyaleLootDropper), so clients can create them on announcement.")]
        [SerializeField] private GameObject medkitPrefab;
        [SerializeField] private GameObject ammoBoxPrefab;

        private const int DynamicIdBase = 100000;

        private readonly NetworkList<int> taken = new NetworkList<int>(null, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkList<Vector3> layout = new NetworkList<Vector3>(null, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private static readonly List<GameObject> layoutItems = new List<GameObject>();
        private static bool layoutComplete;

        private readonly Dictionary<int, GameObject> dynamicItems = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, byte> dynamicKinds = new Dictionary<int, byte>();
        private readonly HashSet<int> seenTaken = new HashSet<int>();      // ids this peer already knows are gone
        private int nextDynamicId = DynamicIdBase;
        private bool layoutPublished, layoutApplied;
        private float nextScan;

        private void Awake()
        {
            Instance = this;
        }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        // ------------------------------------------------------------------ registration (called by BRMatchSetup)
        public static void BeginLayout()
        {
            layoutItems.Clear();
            layoutComplete = false;
            if (Instance != null) { Instance.layoutApplied = false; Instance.seenTaken.Clear(); }
        }

        /// <summary>Registers one placed loot item; the id is the order of calls, equal on every peer.</summary>
        public static int Register(GameObject item)
        {
            if (item == null) return -1;
            layoutItems.Add(item);
            return layoutItems.Count - 1;
        }

        public static void EndLayout()
        {
            layoutComplete = true;
        }

        public static int RegisteredCount => layoutItems.Count;
        public static int TakenCount => Instance != null && Instance.IsSpawned ? Instance.taken.Count : 0;

        // ------------------------------------------------------------------ network
        public override void OnNetworkSpawn()
        {
            if (!IsServer)
            {
                taken.OnListChanged += HandleTakenChanged;
            }
            else if (NetworkManager != null && NetworkManager.SceneManager != null)
            {
                NetworkManager.SceneManager.OnSynchronizeComplete += SendExistingDrops;
            }
        }

        public override void OnNetworkDespawn()
        {
            taken.OnListChanged -= HandleTakenChanged;
            if (NetworkManager != null && NetworkManager.SceneManager != null)
                NetworkManager.SceneManager.OnSynchronizeComplete -= SendExistingDrops;
        }

        // a client that joins mid-match has not seen the drops made before: send the ones still lying around
        private void SendExistingDrops(ulong clientId)
        {
            if (!IsServer || clientId == NetworkManager.ServerClientId) return;
            var target = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } };
            int sent = 0;
            foreach (var kv in dynamicItems)
            {
                var go = kv.Value;
                if (go == null || !go.activeInHierarchy || seenTaken.Contains(kv.Key)) continue;
                if (!dynamicKinds.TryGetValue(kv.Key, out byte kind)) continue;
                DropClientRpc(kv.Key, kind, go.transform.position, go.transform.rotation, Vector3.zero, Vector3.zero, target);
                sent++;
            }
            if (sent > 0) Debug.Log($"[BRLootSync] Sent {sent} existing drop(s) to late-joining client {clientId}.");
        }

        private void Update()
        {
            if (!IsSpawned) return;

            if (IsServer && layoutComplete && !layoutPublished)
            {
                layoutPublished = true;
                layout.Clear();
                for (int i = 0; i < layoutItems.Count; i++)
                    layout.Add(layoutItems[i] != null ? layoutItems[i].transform.position : Vector3.zero);
            }

            if (!IsServer && layoutComplete && !layoutApplied && layout.Count > 0)
            {
                layoutApplied = true;
                ApplyServerLayout();
                for (int i = 0; i < taken.Count; i++) HideRemote(taken[i]);
            }

            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + 0.2f;
            ScanForTaken();
        }

        private void ApplyServerLayout()
        {
            int n = Mathf.Min(layoutItems.Count, layout.Count);
            int moved = 0;
            for (int i = 0; i < n; i++)
            {
                var go = layoutItems[i];
                if (go == null) continue;
                if ((go.transform.position - layout[i]).sqrMagnitude > 0.25f * 0.25f)
                {
                    go.transform.position = layout[i];
                    var rb = go.GetComponent<Rigidbody>();
                    if (rb != null) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                    moved++;
                }
            }
            Debug.Log($"[BRLootSync] Loot layout: {layoutItems.Count} local items, {layout.Count} on the server; moved {moved} onto the server's positions" +
                      (layoutItems.Count != layout.Count ? " (COUNTS DIFFER: placement diverged)" : "."));
        }

        private static bool IsGone(GameObject go)
        {
            if (go == null) return true;
            if (!go.activeInHierarchy) return true;
            return go.GetComponentInParent<JUInventory>() != null;   // a weapon now carried by somebody
        }

        private void ScanForTaken()
        {
            if (!layoutComplete && dynamicItems.Count == 0) return;

            for (int i = 0; i < layoutItems.Count; i++)
            {
                if (seenTaken.Contains(i)) continue;
                if (IsGone(layoutItems[i])) Report(i);
            }
            if (dynamicItems.Count == 0) return;

            List<int> gone = null;
            foreach (var kv in dynamicItems)
            {
                if (seenTaken.Contains(kv.Key)) continue;
                if (IsGone(kv.Value)) { if (gone == null) gone = new List<int>(); gone.Add(kv.Key); }
            }
            if (gone != null) foreach (var id in gone) Report(id);
        }

        private void Report(int id)
        {
            seenTaken.Add(id);
            if (IsServer) AddTaken(id);
            else ReportTakenServerRpc(id);
        }

        private void AddTaken(int id)
        {
            for (int i = 0; i < taken.Count; i++) if (taken[i] == id) return;
            taken.Add(id);
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReportTakenServerRpc(int id)
        {
            AddTaken(id);
        }

        private void HandleTakenChanged(NetworkListEvent<int> change)
        {
            if (change.Type == NetworkListEvent<int>.EventType.Add) HideRemote(change.Value);
            else if (change.Type == NetworkListEvent<int>.EventType.Full) for (int i = 0; i < taken.Count; i++) HideRemote(taken[i]);
        }

        private void HideRemote(int id)
        {
            if (!seenTaken.Add(id)) return;       // already known (this peer reported it itself, or hid it before)
            GameObject go = null;
            if (id >= DynamicIdBase) dynamicItems.TryGetValue(id, out go);
            else if (id >= 0 && id < layoutItems.Count) go = layoutItems[id];
            if (go != null) go.SetActive(false);
        }

        // ------------------------------------------------------------------ drops from dying bots
        public enum DropKind : byte { Medkit = 0, AmmoBox = 1 }

        /// <summary>Server only: a bot dropped <paramref name="item"/>; make the same pickup appear on every client.</summary>
        public static void ServerAnnounceDrop(DropKind kind, GameObject item, Vector3 velocity, Vector3 torque)
        {
            var s = Instance;
            if (s == null || !s.IsSpawned || !s.IsServer || item == null) return;
            int id = s.nextDynamicId++;
            s.dynamicItems[id] = item;
            s.dynamicKinds[id] = (byte)kind;
            Debug.Log($"[BRLootSync] Drop {id} ({kind}) announced at {item.transform.position}.");
            s.DropClientRpc(id, (byte)kind, item.transform.position, item.transform.rotation, velocity, torque);
        }

        [ClientRpc]
        private void DropClientRpc(int id, byte kind, Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 torque, ClientRpcParams rpcParams = default)
        {
            if (IsServer) return;                      // the server made the real one
            if (dynamicItems.TryGetValue(id, out var already) && already != null) return;   // already have it
            Debug.Log($"[BRLootSync] Drop {id} created at {position}.");
            var prefab = kind == (byte)DropKind.Medkit ? medkitPrefab : ammoBoxPrefab;
            if (prefab == null) return;

            var go = Instantiate(prefab, position, rotation);
            dynamicItems[id] = go;
            var med = go.GetComponent<Railbound.Gameplay.MedkitPickup>();
            var ammo = go.GetComponent<Railbound.Gameplay.AmmoBoxPickup>();
            if (med != null) med.SpawnWithImpulse(velocity, torque);
            else if (ammo != null) ammo.SpawnWithImpulse(velocity, torque);
            else
            {
                var rb = go.GetComponent<Rigidbody>();
                if (rb != null) { rb.velocity = velocity; rb.angularVelocity = torque; }
            }
            nextDynamicId = Mathf.Max(nextDynamicId, id + 1);
            // a pickup that is already known to be gone (late message): hide at once
            for (int i = 0; i < taken.Count; i++) if (taken[i] == id) { seenTaken.Add(id); go.SetActive(false); break; }
        }
    }
}
