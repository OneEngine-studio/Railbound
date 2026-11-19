using System;
using System.Collections.Generic;
using Railbound.BR;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Railbound.Maps
{
    /// <summary>
    /// Builds the chosen map when the match scene (MAP-2) loads, before any of its Start methods run. MAP-2's own ground (terrain,
    /// rocks, water, NavMesh, reflection probes) is switched off and the map's layout is built in its place; everything else in the
    /// scene (networking, player, camera, match setup, loot, zone, bots, HUD, menus, sky) is reused and pointed at the new play area.
    /// For MAP-2 itself nothing happens.
    /// </summary>
    public static class MapLoader
    {
        public const string MatchSceneName = "MAP-2";

        /// <summary>The map built in the current match scene (MAP-2 when no other map was built).</summary>
        public static MapDefinition Active { get; private set; }
        public static MapBuilder LastBuild { get; private set; }
        public static float LastBuildSeconds { get; private set; }

        /// <summary>Raised after a map was built (test aids, HUD).</summary>
        public static event Action<MapDefinition> Built;

        // MAP-2 objects that belong to its ground and would fight the new map
        private static readonly string[] Map2Ground = { "Terrain", "Rocks", "Water", "Navigation", "Reflections" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Hook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        // back in the lobby: the next match chooses its map again
        private static void OnSceneUnloaded(Scene scene)
        {
            if (scene.name != MatchSceneName) return;
            Active = null;
            LastBuild = null;
            MapSelection.Set(null);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Active = null; LastBuild = null; }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != MatchSceneName) return;
            var def = MapSelection.Current;
            Active = def;
            if (def == null || def.IsMap2) { Active = MapCatalog.Map2; return; }
            try { Build(scene, def); }
            catch (Exception e)
            {
                Debug.LogError("[Map] Building " + def.Name + " failed: " + e);
            }
        }

        /// <summary>
        /// Builds <paramref name="def"/> into <paramref name="scene"/>. The editor preview (<paramref name="preview"/>) builds into an empty
        /// scene and leaves the match objects, the light and the NavMesh alone.
        /// </summary>
        public static MapBuilder Build(Scene scene, MapDefinition def, bool preview = false)
        {
            float started = Time.realtimeSinceStartup;
            var kit = MapKit.Load();
            if (kit == null) Debug.LogWarning("[Map] The map kit is missing (Railbound > Maps > Bake Map Kit): building with plain colours.");

            var rootGo = new GameObject("[Map] " + def.Name);
            SceneManager.MoveGameObjectToScene(rootGo, scene);
            var b = new MapBuilder(def, kit, rootGo.transform);

            NavMeshSurface map2Nav = null;
            var off = new List<GameObject>();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (Array.IndexOf(Map2Ground, root.name) < 0) continue;
                if (root.name == "Terrain") Harvest(root, b);
                if (root.name == "Rocks") HarvestRocks(root, b);
                if (root.name == "Navigation") map2Nav = root.GetComponentInChildren<NavMeshSurface>(true);
                off.Add(root);
            }
            int agentType = map2Nav != null ? map2Nav.agentTypeID : 0;
            foreach (var go in off) go.SetActive(false);
            FreezeSceneItems(scene);

            var layout = def.Layout();
            layout.Bind(b);
            layout.Plan();
            b.BuildTerrain(layout);
            layout.Build();
            b.Boundary();

            float navSeconds = 0f;
            if (!preview)
            {
                ConfigureMatch(def, b);
                MapBuilder.ApplyLook(def);
                float navStarted = Time.realtimeSinceStartup;
                BakeNavMesh(b, agentType);
                navSeconds = Time.realtimeSinceStartup - navStarted;
                if (!b.Headless) StaticBatchingUtility.Combine(rootGo);
            }

            LastBuild = b;
            LastBuildSeconds = Time.realtimeSinceStartup - started;
            Debug.Log($"[Map] Built {def.Name} ({def.Id}) for {def.ModeId}: {b.Boxes} boxes, {b.Props} props, {b.Anchors.Count} markers, " +
                      $"terrain {def.TerrainSize:0} m, play area {def.PlayArea.x:0} x {def.PlayArea.y:0} m, navmesh {navSeconds:0.0} s, total {LastBuildSeconds:0.0} s");
            Built?.Invoke(def);
            return b;
        }

        // ------------------------------------------------------------------ MAP-2's assets
        private static void Harvest(GameObject terrainRoot, MapBuilder b)
        {
            var terrains = terrainRoot.GetComponentsInChildren<Terrain>(true);
            if (terrains.Length == 0) return;
            var t = terrains[0];
            b.Map2Terrain = t;
            b.Map2TerrainMaterial = t.materialTemplate;
            var data = t.terrainData;
            if (data == null) return;
            b.Map2Layers = data.terrainLayers;
            var seen = new HashSet<GameObject>();
            foreach (var tp in data.treePrototypes)
            {
                if (tp.prefab == null || !seen.Add(tp.prefab)) continue;
                string n = tp.prefab.name.ToLowerInvariant();
                if (n.Contains("rock")) continue;   // rocks come from MAP-2's rock objects (with colliders)
                if (n.Contains("pine") || n.Contains("conifer") || n.Contains("cypress") || n.Contains("tree")) b.Map2Trees.Add(tp.prefab);
                else if (n.Contains("bush") || n.Contains("shrub") || n.Contains("fern")) b.Map2Bushes.Add(tp.prefab);
            }
            foreach (var dp in data.detailPrototypes)
                if (dp != null && (dp.prototype != null || dp.prototypeTexture != null)) b.Map2Grass.Add(dp);
        }

        private static void HarvestRocks(GameObject rocksRoot, MapBuilder b)
        {
            var names = new HashSet<string>();
            foreach (Transform child in rocksRoot.GetComponentsInChildren<Transform>(true))
            {
                if (child == rocksRoot.transform || child.parent != rocksRoot.transform && child.parent.parent != rocksRoot.transform) continue;
                if (child.GetComponentInChildren<MeshRenderer>(true) == null) continue;
                string key = child.name.Split(' ', '(')[0];
                if (!names.Add(key)) continue;   // one template per rock model
                b.Map2Rocks.Add(child.gameObject);
                if (b.Map2Rocks.Count >= 12) break;
            }
            // the same order on every peer (the layout picks rocks with the seeded generator)
            b.Map2Rocks.Sort((x, y) => string.CompareOrdinal(x.name, y.name));
        }

        /// <summary>Items MAP-2 placed on its own ground would fall forever once that ground is off: hold them still.</summary>
        private static void FreezeSceneItems(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name != "Pickable Items") continue;
                foreach (var rb in root.GetComponentsInChildren<Rigidbody>(true)) rb.isKinematic = true;
            }
        }

        // ------------------------------------------------------------------ match
        private static void ConfigureMatch(MapDefinition def, MapBuilder b)
        {
            float radius = def.PlayRadius;
            var setup = UnityEngine.Object.FindFirstObjectByType<BRMatchSetup>(FindObjectsInactive.Include);
            if (setup != null)
            {
                setup.MapCenter = Vector3.zero;
                setup.MapRadius = radius * (def.DesignedSpawns ? 0.92f : 0.88f);
                float area = def.PlayArea.x * def.PlayArea.y;
                float share = Mathf.Clamp(area / (1600f * 1600f), 0f, 1f);
                setup.MinSpawnSpacing = Mathf.Clamp(Mathf.Sqrt(area / Mathf.Max(1, def.Players)) * 0.45f, 6f, 35f);
                setup.MinLootSpacing = Mathf.Clamp(setup.MinSpawnSpacing * 0.4f, 3f, 14f);
                setup.StarterLootDistance = def.DesignedSpawns ? new Vector2(2f, 6f) : new Vector2(6f, 18f);
                setup.ScatteredWeapons = Mathf.Max(def.DesignedSpawns ? 10 : 18, Mathf.RoundToInt(24 * Mathf.Sqrt(share) * 2f));
                setup.AmmoBoxes = Mathf.Max(def.DesignedSpawns ? 12 : 20, Mathf.RoundToInt(30 * Mathf.Sqrt(share) * 2f));
                setup.Medkits = Mathf.Max(def.DesignedSpawns ? 6 : 10, Mathf.RoundToInt(12 * Mathf.Sqrt(share) * 2f));
                setup.LootPointShare = 0.6f;
                setup.MaxSlopeDegrees = 30f;
                setup.DesignedSpawns.Clear();
                if (def.DesignedSpawns)
                    foreach (var a in b.Anchors)
                        if (a.Kind == MapAnchor.AnchorKind.Spawn || a.Kind == MapAnchor.AnchorKind.TeamSpawn)
                            setup.DesignedSpawns.Add(new Pose(a.transform.position, a.transform.rotation));
            }

            var coordinator = UnityEngine.Object.FindFirstObjectByType<BRMatchCoordinator>(FindObjectsInactive.Include);
            if (coordinator != null)
            {
                coordinator.Capacity = def.Players;
                coordinator.EarlyStartHumans = Mathf.Max(1, def.Players / 2);
            }

            var vehicles = UnityEngine.Object.FindFirstObjectByType<Railbound.Vehicles.BRVehicleSpawner>(FindObjectsInactive.Include);
            if (vehicles != null) vehicles.Count = def.Vehicles;

            var bounds = UnityEngine.Object.FindFirstObjectByType<Arikan.MiniMapBounds>(FindObjectsInactive.Include);
            if (bounds != null && bounds.topRight != null && bounds.bottomLeft != null)
            {
                float h = def.TerrainSize * 0.5f;
                bounds.topRight.position = new Vector3(h, 0f, h);
                bounds.bottomLeft.position = new Vector3(-h, 0f, -h);
            }
        }

        private static void BakeNavMesh(MapBuilder b, int agentType)
        {
            var surface = b.Root.gameObject.AddComponent<NavMeshSurface>();
            surface.agentTypeID = agentType;
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~0;
            surface.minRegionArea = 4f;
            if (b.Definition.TerrainSize > 700f)
            {
                surface.overrideVoxelSize = true;
                surface.voxelSize = 0.3f;
                surface.overrideTileSize = true;
                surface.tileSize = 128;
            }
            // the invisible edge walls are not ground
            var boundary = b.Root.Find("Boundary");
            if (boundary != null)
            {
                var mod = boundary.gameObject.AddComponent<NavMeshModifier>();
                mod.ignoreFromBuild = true;
            }
            surface.BuildNavMesh();
        }
    }
}
