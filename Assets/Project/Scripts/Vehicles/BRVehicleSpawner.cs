using System.Collections;
using System.Collections.Generic;
using JUTPS;
using Railbound.BR;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Railbound.Vehicles
{
    /// <summary>
    /// Places the match's vehicles: the server (or an offline map) picks a new random layout every match and spawns the network
    /// vehicles from <c>Resources/Vehicles</c>; clients receive them with their positions from the server.
    ///
    /// The map is split into as many slices (by angle) as there are vehicles, so they end up all over the map rather than in one
    /// corner. A spot must be open, flat Terrain under the whole footprint (never a roof, a train, a building or water), on the
    /// NavMesh, with nothing standing there, at least <see cref="MinSpacing"/> from other vehicles and a little away from the
    /// player spawn points.
    /// Command line: <c>-vehicles n</c> (0 = none), <c>-vehicle-seed n</c> (a fixed layout for tests),
    /// <c>-vehicle-near-spawn</c> (test: one vehicle next to every player spawn).
    /// </summary>
    public class BRVehicleSpawner : MonoBehaviour
    {
        public static BRVehicleSpawner Instance { get; private set; }

        public int Count = 10;
        public float MinSpacing = 55f;
        public float MinDistanceFromPlayerSpawn = 8f;
        public float MaxSlopeDegrees = 12f;
        public float MaxFootprintStep = 0.45f;

        public int Spawned { get; private set; }
        public readonly List<RailboundVehicle> Vehicles = new List<RailboundVehicle>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryInstall();
        }

        private static void OnSceneLoaded(Scene s, LoadSceneMode m) { TryInstall(); }

        private static void TryInstall()
        {
            if (Instance != null) return;
            var setup = BRMatchSetup.Instance != null ? BRMatchSetup.Instance : FindFirstObjectByType<BRMatchSetup>();
            if (setup == null) return;
            setup.gameObject.AddComponent<BRVehicleSpawner>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private static bool OfflineMatch
        {
            get
            {
                foreach (string a in System.Environment.GetCommandLineArgs())
                    if (string.Equals(a, "-offline", System.StringComparison.OrdinalIgnoreCase) || string.Equals(a, "-solo", System.StringComparison.OrdinalIgnoreCase)) return true;
                return NetworkManager.Singleton == null;
            }
        }

        private IEnumerator Start()
        {
            // a map built for a mode says how many vehicles it has (none on the arenas); MAP-2 keeps the scene's count
            var map = Railbound.Maps.MapLoader.Active;
            int n = IntArg("-vehicles", map != null && !map.IsMap2 ? map.Vehicles : Count);
            if (n <= 0) yield break;

            // wait for the map layout (player spawns) and, in a network session, for the server to run
            float waited = 0f;
            while (true)
            {
                var nm = NetworkManager.Singleton;
                bool net = nm != null && nm.IsListening;
                if (net && !nm.IsServer) yield break;   // a client: the server's vehicles arrive over the network
                var setup = BRMatchSetup.Instance;
                bool layout = setup != null && setup.PlayerSpawnsReady > 0;
                if ((net || OfflineMatch) && (layout || waited > 10f) && Terrain.activeTerrain != null) break;
                if (waited > 60f) { Debug.LogWarning("[Vehicles] No server and no offline match after 60 s: no vehicles."); yield break; }
                waited += 0.25f;
                yield return new WaitForSecondsRealtime(0.25f);
            }
            yield return null;   // loot and bots are placed in BRMatchSetup.Start; let their colliders settle in

            var catalog = LoadCatalog();
            if (catalog.Count == 0) { Debug.LogWarning("[Vehicles] Resources/Vehicles holds no vehicle prefabs."); yield break; }
            SpawnLayout(n, catalog);
        }

        public static List<RailboundVehicle> LoadCatalog()
        {
            var list = new List<RailboundVehicle>();
            foreach (var go in Resources.LoadAll<GameObject>("Vehicles"))   // LoadAll does not filter prefabs by component type
                if (go != null && go.TryGetComponent(out RailboundVehicle v)) list.Add(v);
            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return list;
        }

        private void SpawnLayout(int count, List<RailboundVehicle> catalog)
        {
            int seed = IntArg("-vehicle-seed", 0);
            var rng = new System.Random(seed != 0 ? seed : System.Environment.TickCount ^ GetInstanceID());
            var setup = BRMatchSetup.Instance;
            Vector3 center = setup != null ? setup.MapCenter : Vector3.zero;
            float radius = setup != null ? setup.MapRadius : 250f;

            var playerSpawns = new List<Vector3>();
            if (setup != null)
                for (int i = 0; i < setup.PlayerSpawnsReady; i++)
                    if (setup.TryGetPlayerSpawn(i, out Vector3 p, out _)) playerSpawns.Add(p);

            var taken = new List<Vector3>();
            var rejects = new Dictionary<string, int>();
            float startAngle = (float)rng.NextDouble() * Mathf.PI * 2f;

            // test aid: a vehicle beside every player spawn
            if (HasArg("-vehicle-near-spawn"))
            {
                for (int i = 0; i < playerSpawns.Count; i++)
                {
                    var prefab = PickWeighted(catalog, rng);
                    for (int attempt = 0; attempt < 40; attempt++)
                    {
                        float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = 9f + (float)rng.NextDouble() * 6f;
                        Vector3 xz = playerSpawns[i] + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                        float yaw = (float)rng.NextDouble() * 360f;
                        if (!TryFootprint(prefab, xz, yaw, null, 0f, rejects, out Vector3 spot)) continue;
                        Place(prefab, spot, yaw);
                        taken.Add(spot);
                        break;
                    }
                }
            }

            for (int i = 0; i < count; i++)
            {
                var prefab = PickWeighted(catalog, rng);
                bool placed = false;
                for (int attempt = 0; attempt < 80 && !placed; attempt++)
                {
                    // this vehicle's slice of the map; after many failures anywhere
                    float a = attempt < 50
                        ? startAngle + (i + (float)rng.NextDouble()) / count * Mathf.PI * 2f
                        : (float)rng.NextDouble() * Mathf.PI * 2f;
                    float r = radius * Mathf.Sqrt(Mathf.Lerp(0.03f, 0.92f, (float)rng.NextDouble()));
                    Vector3 xz = new Vector3(center.x + Mathf.Cos(a) * r, 0f, center.z + Mathf.Sin(a) * r);
                    float yaw = (float)rng.NextDouble() * 360f;
                    if (!TryFootprint(prefab, xz, yaw, taken, MinSpacing * (attempt < 60 ? 1f : 0.5f), rejects, out Vector3 spot)) continue;
                    if (TooCloseTo(playerSpawns, spot, MinDistanceFromPlayerSpawn)) { Count1(rejects, "playerSpawn"); continue; }
                    Place(prefab, spot, yaw);
                    taken.Add(spot);
                    placed = true;
                }
            }

            var sb = new System.Text.StringBuilder();
            foreach (var kv in rejects) sb.Append(kv.Key).Append('=').Append(kv.Value).Append(' ');
            Debug.Log($"[Vehicles] Spawned {Spawned}/{count} vehicles (map radius {radius:F0} m). Rejected spots: {sb}");
        }

        private void Place(RailboundVehicle prefab, Vector3 spot, float yaw)
        {
            var go = Instantiate(prefab.gameObject, spot, Quaternion.Euler(0f, yaw, 0f));
            go.name = prefab.name + "_" + Spawned;
            var no = go.GetComponent<NetworkObject>();
            var nm = NetworkManager.Singleton;
            if (no != null && nm != null && nm.IsListening && nm.IsServer) no.Spawn(true);
            var v = go.GetComponent<RailboundVehicle>();
            Vehicles.Add(v);
            Spawned++;
            Debug.Log($"[Vehicles] {go.name} at {spot:F1} yaw {yaw:F0}.");
        }

        private static RailboundVehicle PickWeighted(List<RailboundVehicle> catalog, System.Random rng)
        {
            float total = 0f;
            foreach (var v in catalog) total += Mathf.Max(0.01f, v.SpawnWeight);
            float roll = (float)rng.NextDouble() * total;
            foreach (var v in catalog)
            {
                roll -= Mathf.Max(0.01f, v.SpawnWeight);
                if (roll <= 0f) return v;
            }
            return catalog[catalog.Count - 1];
        }

        /// <summary>The whole footprint of <paramref name="prefab"/> at (xz, yaw) on open, flat, clear Terrain.</summary>
        private bool TryFootprint(RailboundVehicle prefab, Vector3 xz, float yaw, List<Vector3> taken, float spacing, Dictionary<string, int> rejects, out Vector3 spot)
        {
            spot = default;
            if (taken != null)
                foreach (var t in taken)
                {
                    Vector3 d = t - xz; d.y = 0f;
                    if (d.sqrMagnitude < spacing * spacing) { Count1(rejects, "spacing"); return false; }
                }

            var b = prefab.LocalBounds;
            if (b.size.sqrMagnitude < 0.1f) b = new Bounds(new Vector3(0f, 0.8f, 0f), new Vector3(2f, 1.6f, 4.4f));
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
            float hx = b.extents.x + 0.3f, hz = b.extents.z + 0.3f;
            var samples = new[]
            {
                Vector3.zero, new Vector3(-hx, 0f, -hz), new Vector3(hx, 0f, -hz), new Vector3(-hx, 0f, hz), new Vector3(hx, 0f, hz),
                new Vector3(0f, 0f, hz), new Vector3(0f, 0f, -hz)
            };
            float minY = float.MaxValue, maxY = float.MinValue;
            RaycastHit centerHit = default;
            for (int i = 0; i < samples.Length; i++)
            {
                Vector3 p = xz + rot * new Vector3(samples[i].x, 0f, samples[i].z + b.center.z);
                if (!TerrainHit(p, out RaycastHit hit, out string why)) { Count1(rejects, why); return false; }
                if (Vector3.Angle(hit.normal, Vector3.up) > MaxSlopeDegrees) { Count1(rejects, "slope"); return false; }
                minY = Mathf.Min(minY, hit.point.y); maxY = Mathf.Max(maxY, hit.point.y);
                if (i == 0) centerHit = hit;
            }
            if (maxY - minY > MaxFootprintStep) { Count1(rejects, "uneven"); return false; }
            if (!NavMesh.SamplePosition(centerHit.point, out NavMeshHit nav, 2.5f, NavMesh.AllAreas)) { Count1(rejects, "noNavMesh"); return false; }

            Vector3 boxCenter = centerHit.point + rot * new Vector3(0f, 0f, b.center.z) + Vector3.up * (b.size.y * 0.5f + 0.35f);
            var overlaps = Physics.OverlapBox(boxCenter, new Vector3(b.extents.x + 0.4f, b.extents.y, b.extents.z + 0.4f), rot, ~0, QueryTriggerInteraction.Ignore);
            foreach (var o in overlaps)
            {
                if (o is TerrainCollider || Ignorable(o)) continue;
                Count1(rejects, "blocked");
                return false;
            }

            // the vehicle's origin is not always at the bottom of its wheels (the monster truck's is at the axles)
            spot = new Vector3(centerHit.point.x, maxY + 0.15f - Mathf.Min(0f, b.min.y), centerHit.point.z);
            return true;
        }

        /// <summary>The first real surface straight down must be the Terrain.</summary>
        private static bool TerrainHit(Vector3 xz, out RaycastHit hit, out string why)
        {
            hit = default;
            var hits = Physics.RaycastAll(new Vector3(xz.x, 600f, xz.z), Vector3.down, 1500f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, c) => a.distance.CompareTo(c.distance));
            foreach (var h in hits)
            {
                if (Ignorable(h.collider)) continue;
                if (!(h.collider is TerrainCollider)) { why = "notTerrain"; return false; }
                hit = h;
                why = null;
                return true;
            }
            why = "noHit";
            return false;
        }

        private static bool Ignorable(Collider c)
        {
            if (c == null) return true;
            if (c.name.Contains("StormRing") || c.GetComponentInParent<BRZoneManager>() != null) return true;
            if (c.GetComponentInParent<JUTPS.WeaponSystem.Weapon>() != null) return true;
            if (c.GetComponentInParent<Railbound.Gameplay.AmmoBoxPickup>() != null) return true;
            if (c.GetComponentInParent<Railbound.Gameplay.MedkitPickup>() != null) return true;
            return false;
        }

        private static bool TooCloseTo(List<Vector3> points, Vector3 p, float min)
        {
            foreach (var q in points) { Vector3 d = q - p; d.y = 0f; if (d.sqrMagnitude < min * min) return true; }
            return false;
        }

        private static void Count1(Dictionary<string, int> d, string key) { d.TryGetValue(key, out int n); d[key] = n + 1; }

        private static bool HasArg(string name)
        {
            foreach (string a in System.Environment.GetCommandLineArgs()) if (string.Equals(a, name, System.StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static int IntArg(string name, int fallback)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, System.StringComparison.OrdinalIgnoreCase) && int.TryParse(args[i + 1], out int v)) return v;
            return fallback;
        }
    }
}
