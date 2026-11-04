using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using JUTPS;
using JUTPS.InventorySystem;
using JUTPS.WeaponSystem;
using Project.AI.RL;
using Railbound.Gameplay;

namespace Railbound.BR
{
    /// <summary>
    /// Randomizes a Battle Royale match when the map starts:
    ///  - every bot and every player slot gets a random spawn point on open TERRAIN
    ///    (never on a train, roof, platform or inside a building),
    ///  - every spawn gets a starter weapon nearby so nobody has to wander forever,
    ///  - weapons, ammo boxes and medkits are scattered across the whole map.
    /// Everything comes from one seed, so the same seed produces the same layout on every peer.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    [AddComponentMenu("Railbound/BR/Match Setup (Random Spawns & Loot)")]
    public class BRMatchSetup : MonoBehaviour
    {
        public static BRMatchSetup Instance { get; private set; }

        [Header("Seed")]
        [Tooltip("0 = pick a new random seed every match. Any other value reproduces the same layout.")]
        public int Seed = 0;
        public int ActiveSeed { get; private set; }

        [Header("Playable Area")]
        public Vector3 MapCenter = new Vector3(0f, 0f, 15f);
        public float MapRadius = 300f;

        [Header("Terrain Sampling")]
        [Tooltip("Only spots whose top surface is the Terrain are used, so trains, roofs and platforms are rejected.")]
        public float RayStartHeight = 400f;
        public float MaxSlopeDegrees = 28f;
        public float ClearanceRadius = 0.9f;
        [Tooltip("Max horizontal gap between the terrain hit and the NavMesh.")]
        public float NavMeshSnapDistance = 2.5f;
        public int MaxAttemptsPerPoint = 60;

        [Header("Spawns")]
        public bool RandomizeBotSpawns = true;
        public bool RandomizePlayerSpawn = true;
        [Tooltip("How many player spawn slots to prepare (one per real player in the match).")]
        public int PlayerSlots = 10;
        public float MinSpawnSpacing = 35f;

        [Header("Starter Loot (one weapon near every spawn)")]
        public bool PlaceStarterLoot = true;
        public Vector2 StarterLootDistance = new Vector2(12f, 30f);

        [Header("Scattered Loot")]
        public bool RandomizeLoot = true;
        [Tooltip("Total ground weapons scattered over the map (existing ones are moved, extras are cloned).")]
        public int ScatteredWeapons = 24;
        public int AmmoBoxes = 30;
        public int Medkits = 12;
        public float MinLootSpacing = 14f;
        [Range(0f, 1f)]
        [Tooltip("Share of weapons, ammo and medkits placed on BRLootPoints (inside buildings) instead of open terrain.")]
        public float LootPointShare = 0.5f;
        public GameObject AmmoBoxPrefab;
        public GameObject MedkitPrefab;

        [Header("Weapon Spawn Weights (by item name)")]
        public List<WeaponWeight> WeaponWeights = new List<WeaponWeight>
        {
            new WeaponWeight { ItemName = "P226", Weight = 3f },
            new WeaponWeight { ItemName = "P226 Dual", Weight = 2f },
            new WeaponWeight { ItemName = "UMP", Weight = 3f },
            new WeaponWeight { ItemName = "Benelli M4", Weight = 2f },
            new WeaponWeight { ItemName = "M82", Weight = 1f }
        };

        [System.Serializable]
        public struct WeaponWeight
        {
            public string ItemName;
            public float Weight;
        }

        [Header("Result (Read-Only)")]
        public int BotsPlaced;
        public int PlayerSpawnsReady;
        public int WeaponsPlaced;
        public int AmmoPlaced;
        public int MedkitsPlaced;

        private readonly List<Vector3> usedSpawnPoints = new List<Vector3>();
        private readonly List<Vector3> usedLootPoints = new List<Vector3>();
        private readonly List<Vector3> playerSpawns = new List<Vector3>();
        private readonly List<Weapon> weaponTemplates = new List<Weapon>();
        private System.Random rng;
        private Transform lootRoot;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            Generate();
        }

        [ContextMenu("Regenerate Match Layout")]
        public void Generate()
        {
            ActiveSeed = Seed != 0 ? Seed : Random.Range(1, int.MaxValue);
            rng = new System.Random(ActiveSeed);

            usedSpawnPoints.Clear();
            usedLootPoints.Clear();
            playerSpawns.Clear();
            weaponTemplates.Clear();
            BotsPlaced = PlayerSpawnsReady = WeaponsPlaced = AmmoPlaced = MedkitsPlaced = 0;

            // Player slots first so players always get a spot, then bots.
            if (RandomizePlayerSpawn) PreparePlayerSpawns();
            if (RandomizeBotSpawns) PlaceBots();
            if (RandomizeLoot) ScatterLoot();

            Debug.Log($"[BRMatchSetup] seed={ActiveSeed} bots={BotsPlaced} playerSpawns={PlayerSpawnsReady} " +
                      $"weapons={WeaponsPlaced} ammo={AmmoPlaced} medkits={MedkitsPlaced} " +
                      $"lootPoints={LootPointsUsed}/{LootPointsAvailable}");
        }

        // ------------------------------------------------------------------ spawns

        private void PreparePlayerSpawns()
        {
            for (int i = 0; i < Mathf.Max(1, PlayerSlots); i++)
            {
                if (!TryFindPoint(usedSpawnPoints, MinSpawnSpacing, out Vector3 p)) break;
                usedSpawnPoints.Add(p);
                playerSpawns.Add(p);
                if (PlaceStarterLoot) PlaceStarterWeaponNear(p);
            }
            PlayerSpawnsReady = playerSpawns.Count;

            // Offline / editor play: move the placed player character to the first slot.
            if (playerSpawns.Count > 0 && FindOfflinePlayer() is JUCharacterController offline)
            {
                Quaternion rot = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                TeleportBody(offline.transform, playerSpawns[0], rot);
            }
        }

        /// <summary>Spawn position for a player slot (clientId modulo slot count). False if no layout exists yet.</summary>
        public bool TryGetPlayerSpawn(int slot, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (playerSpawns.Count == 0) return false;

            int index = Mathf.Abs(slot) % playerSpawns.Count;
            position = playerSpawns[index];
            rotation = Quaternion.Euler(0f, (index * 137.5f) % 360f, 0f);
            return true;
        }

        private void PlaceBots()
        {
            var agents = FindObjectsOfType<JUCharacterRLAgent>(true);
            for (int i = 0; i < agents.Length; i++)
            {
                var agent = agents[i];
                if (agent == null) continue;

                if (!TryFindPoint(usedSpawnPoints, MinSpawnSpacing * 0.7f, out Vector3 p))
                {
                    Debug.LogWarning($"[BRMatchSetup] No free terrain spot for bot '{agent.name}'. Leaving it where it is.");
                    continue;
                }

                usedSpawnPoints.Add(p);
                agent.TeleportTo(p, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
                BotsPlaced++;

                if (PlaceStarterLoot) PlaceStarterWeaponNear(p);
            }
        }

        private JUCharacterController FindOfflinePlayer()
        {
            if (JUGameManager.PlayerController != null && JUGameManager.PlayerController.GetComponent<JUCharacterRLAgent>() == null)
            {
                return JUGameManager.PlayerController;
            }

            var all = FindObjectsOfType<JUCharacterController>();
            for (int i = 0; i < all.Length; i++)
            {
                var c = all[i];
                if (c.GetComponent<JUCharacterRLAgent>() == null && c.CompareTag("Player")) return c;
            }
            return null;
        }

        private static void TeleportBody(Transform body, Vector3 position, Quaternion rotation)
        {
            body.SetPositionAndRotation(position, rotation);
            var rb = body.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            Physics.SyncTransforms();
        }

        // ------------------------------------------------------------------ loot

        private void EnsureLootRoot()
        {
            if (lootRoot != null) return;
            lootRoot = new GameObject("[BR] Random Loot").transform;
        }

        private void CollectWeaponTemplates()
        {
            weaponTemplates.Clear();
            var all = FindObjectsOfType<Weapon>(true);
            for (int i = 0; i < all.Length; i++)
            {
                var w = all[i];
                if (w == null || w.GetComponentInParent<JUInventory>() != null) continue;
                weaponTemplates.Add(w);
            }
        }

        private readonly List<Transform> lootPointSpots = new List<Transform>();
        private readonly HashSet<int> usedLootPointSpots = new HashSet<int>();
        public int LootPointsAvailable { get; private set; }
        public int LootPointsUsed { get; private set; }

        /// <summary>
        /// Picks a spot for one loot item: with probability LootPointShare a free BRLootPoint (inside a building),
        /// otherwise open terrain.
        /// </summary>
        private bool TryGetLootSpot(float terrainSpacing, out Vector3 spot)
        {
            if (lootPointSpots.Count > usedLootPointSpots.Count && rng.NextDouble() < LootPointShare)
            {
                for (int attempt = 0; attempt < 12; attempt++)
                {
                    int index = rng.Next(lootPointSpots.Count);
                    if (lootPointSpots[index] == null || !usedLootPointSpots.Add(index)) continue;
                    spot = lootPointSpots[index].position;
                    LootPointsUsed++;
                    return true;
                }
            }
            return TryFindPoint(usedLootPoints, terrainSpacing, out spot);
        }

        private void ScatterLoot()
        {
            EnsureLootRoot();
            if (weaponTemplates.Count == 0) CollectWeaponTemplates();

            lootPointSpots.Clear();
            usedLootPointSpots.Clear();
            LootPointsUsed = 0;
            foreach (var lp in FindObjectsOfType<BRLootPoint>()) lootPointSpots.Add(lp.transform);
            LootPointsAvailable = lootPointSpots.Count;

            // Existing ground weapons are moved first, then extras are cloned until ScatteredWeapons is reached.
            int placed = 0;
            var originals = new List<Weapon>(weaponTemplates);
            for (int i = 0; i < originals.Count && placed < ScatteredWeapons; i++)
            {
                if (!TryGetLootSpot(MinLootSpacing, out Vector3 p)) break;
                usedLootPoints.Add(p);
                MoveLoot(originals[i].transform, p);
                WeaponsPlaced++;
                placed++;
            }

            int guard = 0;
            while (placed < ScatteredWeapons && guard++ < ScatteredWeapons * 4)
            {
                var template = PickWeaponTemplate();
                if (template == null) break;
                if (!TryGetLootSpot(MinLootSpacing, out Vector3 p)) break;
                usedLootPoints.Add(p);
                CloneLoot(template.gameObject, p);
                WeaponsPlaced++;
                placed++;
            }

            AmmoPlaced = ScatterPrefab(AmmoBoxPrefab, AmmoBoxes);
            MedkitsPlaced = ScatterPrefab(MedkitPrefab, Medkits);
        }

        private int ScatterPrefab(GameObject prefab, int count)
        {
            if (prefab == null || count <= 0) return 0;

            int placed = 0;
            for (int i = 0; i < count; i++)
            {
                if (!TryGetLootSpot(MinLootSpacing * 0.6f, out Vector3 p)) break;
                usedLootPoints.Add(p);

                var go = Instantiate(prefab, p + Vector3.up * 0.4f, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), lootRoot);
                var ammo = go.GetComponent<AmmoBoxPickup>();
                if (ammo != null) ammo.SpawnWithImpulse(Vector3.zero, Vector3.zero);
                var med = go.GetComponent<MedkitPickup>();
                if (med != null) med.SpawnWithImpulse(Vector3.zero, Vector3.zero);
                placed++;
            }
            return placed;
        }

        private void PlaceStarterWeaponNear(Vector3 spawn)
        {
            EnsureLootRoot();
            if (weaponTemplates.Count == 0) CollectWeaponTemplates();

            var template = PickWeaponTemplate();
            if (template == null) return;

            for (int attempt = 0; attempt < 20; attempt++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float dist = Mathf.Lerp(StarterLootDistance.x, StarterLootDistance.y, (float)rng.NextDouble());
                Vector3 guess = spawn + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * dist;
                if (TrySnapToTerrain(guess, out Vector3 p))
                {
                    usedLootPoints.Add(p);
                    CloneLoot(template.gameObject, p);
                    WeaponsPlaced++;
                    return;
                }
            }
        }

        private Weapon PickWeaponTemplate()
        {
            if (weaponTemplates.Count == 0) return null;

            float total = 0f;
            for (int i = 0; i < weaponTemplates.Count; i++) total += WeightFor(weaponTemplates[i]);
            if (total <= 0f) return weaponTemplates[rng.Next(weaponTemplates.Count)];

            float roll = (float)rng.NextDouble() * total;
            for (int i = 0; i < weaponTemplates.Count; i++)
            {
                roll -= WeightFor(weaponTemplates[i]);
                if (roll <= 0f) return weaponTemplates[i];
            }
            return weaponTemplates[weaponTemplates.Count - 1];
        }

        private float WeightFor(Weapon w)
        {
            if (w == null) return 0f;
            for (int i = 0; i < WeaponWeights.Count; i++)
            {
                if (WeaponWeights[i].ItemName == w.ItemName) return Mathf.Max(0f, WeaponWeights[i].Weight);
            }
            return 1f;
        }

        private void MoveLoot(Transform item, Vector3 groundPoint)
        {
            // Some weapons start parented to the train or a building; detach them so they stay on the ground.
            item.SetParent(lootRoot, true);
            item.SetPositionAndRotation(groundPoint + Vector3.up * 0.4f, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
            var rb = item.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            item.gameObject.SetActive(true);
            Physics.SyncTransforms();
        }

        private void CloneLoot(GameObject template, Vector3 groundPoint)
        {
            var clone = Instantiate(template, groundPoint + Vector3.up * 0.4f,
                Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), lootRoot);
            clone.name = template.name + " (random loot)";
            clone.SetActive(true);
            var rb = clone.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        // ------------------------------------------------------------------ terrain sampling

        private bool TryFindPoint(List<Vector3> taken, float minSpacing, out Vector3 point)
        {
            float minSq = minSpacing * minSpacing;

            for (int attempt = 0; attempt < MaxAttemptsPerPoint; attempt++)
            {
                // Uniform random point in the playable circle
                float r = MapRadius * Mathf.Sqrt((float)rng.NextDouble());
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                Vector3 guess = new Vector3(MapCenter.x + Mathf.Cos(a) * r, 0f, MapCenter.z + Mathf.Sin(a) * r);

                if (!TrySnapToTerrain(guess, out Vector3 p)) continue;

                bool tooClose = false;
                for (int i = 0; i < taken.Count; i++)
                {
                    Vector3 d = taken[i] - p;
                    d.y = 0f;
                    if (d.sqrMagnitude < minSq)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose) continue;

                point = p;
                return true;
            }

            point = Vector3.zero;
            Debug.LogWarning($"[BRMatchSetup] Could not find a free terrain point after {MaxAttemptsPerPoint} tries ({taken.Count} already taken). Rejections: {RejectSummary()}");
            rejectCounts.Clear();
            return false;
        }

        /// <summary>
        /// Accepts a spot only when the first thing hit from above is the Terrain (so no trains, roofs or platforms),
        /// the ground is flat enough, it sits on the NavMesh, and nothing solid is standing on it.
        /// </summary>
        /// <summary>Colliders that are not part of the ground: the storm wall, and loose loot lying around.</summary>
        private static bool IsIgnorableCollider(Collider c)
        {
            if (c == null) return true;
            if (c.name.Contains("StormRing") || c.GetComponentInParent<BRZoneManager>() != null) return true;
            if (c.GetComponentInParent<Weapon>() != null) return true;
            if (c.GetComponentInParent<AmmoBoxPickup>() != null) return true;
            if (c.GetComponentInParent<MedkitPickup>() != null) return true;
            return false;
        }

        private readonly Dictionary<string, int> rejectCounts = new Dictionary<string, int>();

        private bool Reject(string reason)
        {
            rejectCounts.TryGetValue(reason, out int n);
            rejectCounts[reason] = n + 1;
            return false;
        }

        private string RejectSummary()
        {
            var parts = new List<string>();
            foreach (var kv in rejectCounts) parts.Add(kv.Key + "=" + kv.Value);
            return string.Join(", ", parts);
        }

        private bool TrySnapToTerrain(Vector3 xz, out Vector3 point)
        {
            point = Vector3.zero;

            Vector3 origin = new Vector3(xz.x, RayStartHeight, xz.z);
            var hits = Physics.RaycastAll(origin, Vector3.down, RayStartHeight * 3f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            // The first real surface from above decides: it must be the Terrain.
            RaycastHit hit = default;
            bool found = false;
            for (int i = 0; i < hits.Length; i++)
            {
                if (IsIgnorableCollider(hits[i].collider)) continue;
                hit = hits[i];
                found = true;
                break;
            }
            if (!found) return Reject("noHit");

            if (!(hit.collider is TerrainCollider)) return Reject("notTerrain:" + hit.collider.name);
            if (Vector3.Angle(hit.normal, Vector3.up) > MaxSlopeDegrees) return Reject("slope");

            if (!NavMesh.SamplePosition(hit.point, out NavMeshHit nav, NavMeshSnapDistance, NavMesh.AllAreas)) return Reject("noNavMesh");
            Vector3 flat = nav.position - hit.point;
            flat.y = 0f;
            if (flat.magnitude > NavMeshSnapDistance) return Reject("navTooFar");

            Collider[] overlaps = Physics.OverlapSphere(nav.position + Vector3.up * (ClearanceRadius + 0.2f),
                ClearanceRadius, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < overlaps.Length; i++)
            {
                if (overlaps[i] is TerrainCollider) continue;
                if (IsIgnorableCollider(overlaps[i])) continue;
                if (overlaps[i].GetComponentInParent<Rigidbody>() != null && overlaps[i].GetComponentInParent<JUCharacterController>() == null)
                {
                    // Loose props do not block a spot, solid map geometry does.
                    if (!overlaps[i].GetComponentInParent<Rigidbody>().isKinematic) continue;
                }
                return Reject("blocked:" + overlaps[i].name);
            }

            point = nav.position;
            return true;
        }
    }
}
