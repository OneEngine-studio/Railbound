using System.Collections.Generic;
using System.IO;
using System.Linq;
using Railbound.Maps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Railbound.EditorTools
{
    /// <summary>
    /// Menu "Railbound / Maps / Bake Map Kit": builds the maps' surface materials and terrain layers from the ground textures in git
    /// (NWH ground set, JU floor tiles), finds the prop prefabs (WSM train pack, forest pack) and writes them all to
    /// Assets/Project/Resources/Maps/MapKit.asset. Run it again after changing the list below. The batch build calls
    /// <see cref="BakeBatch"/> so a build never ships an out-of-date kit.
    /// "Railbound / Maps / Preview / ..." builds one map into a new empty scene to look at it (do not save that scene).
    /// </summary>
    public static class MapKitBaker
    {
        private const string Folder = "Assets/Project/Maps";
        private const string KitPath = "Assets/Project/Resources/" + MapKit.ResourcePath + ".asset";
        private const string Ground = "Assets/NWH/Common/Textures/Ground/";

        private struct Surf
        {
            public string id, albedo, normal;
            public Color tint;
            public float tile, metallic, smooth;
            public Color glow;   // a = 0: no glow
        }

        private static Surf S(string id, string albedo, string normal, Color tint, float tile, float metallic = 0f, float smooth = 0.25f)
            => new Surf { id = id, albedo = albedo, normal = normal, tint = tint, tile = tile, metallic = metallic, smooth = smooth };

        private static Surf Glow(string id, Color c) => new Surf { id = id, tint = c * 0.6f, tile = 1f, smooth = 0.5f, glow = c };

        private static readonly Surf[] Surfaces =
        {
            S("concrete", "ConcreteAlbedo.png", "ConcreteNormal.png", Color.white, 3f),
            S("concrete_dark", "ConcreteAlbedo.png", "ConcreteNormal.png", new Color(0.52f, 0.52f, 0.52f), 3f),
            S("asphalt", "AsphaltAlbedo.png", "AsphaltNormal.png", Color.white, 4f),
            S("road", "RoadAlbedo.png", "RoadNormal.png", Color.white, 6f),
            S("gravel", "RoadGravel.png", "RoadGravelNormal.png", Color.white, 3f),
            S("dirt", "DirtAlbedo.png", "DirtNormal.png", Color.white, 3f),
            S("rock", "CliffAlbedo.png", "CliffNormal.png", Color.white, 5f),
            S("rock_red", "CliffAlbedo.png", "CliffNormal.png", new Color(0.86f, 0.58f, 0.44f), 5f),
            S("sand", "SandAlbedo.png", "SandNormal.png", Color.white, 4f),
            S("grass", "GrassAlbedo.png", "GrassNormal.png", Color.white, 3f),
            S("tiles", "Assets/all_Projects/Demos/Textures/Third Party Textures/FloorTiles/Tiles074_1K_Color.png", null, Color.white, 1.5f, 0f, 0.4f),
            S("plaster_white", "ConcreteAlbedo.png", "ConcreteNormal.png", new Color(0.95f, 0.93f, 0.88f), 4f),
            S("plaster_ochre", "ConcreteAlbedo.png", "ConcreteNormal.png", new Color(0.9f, 0.72f, 0.46f), 4f),
            S("plaster_red", "ConcreteAlbedo.png", "ConcreteNormal.png", new Color(0.74f, 0.38f, 0.31f), 4f),
            S("plaster_blue", "ConcreteAlbedo.png", "ConcreteNormal.png", new Color(0.5f, 0.6f, 0.68f), 4f),
            S("brick", "CliffAlbedo.png", "CliffNormal.png", new Color(0.58f, 0.4f, 0.34f), 3f),
            S("wood", "DirtAlbedo.png", "DirtNormal.png", new Color(0.62f, 0.45f, 0.3f), 1.6f, 0f, 0.2f),
            S("wood_dark", "DirtAlbedo.png", "DirtNormal.png", new Color(0.36f, 0.25f, 0.17f), 1.6f, 0f, 0.2f),
            S("metal", "ConcreteAlbedo.png", "ConcreteNormal.png", new Color(0.4f, 0.42f, 0.45f), 2f, 0.7f, 0.45f),
            S("metal_rust", "DirtAlbedo.png", "DirtNormal.png", new Color(0.55f, 0.4f, 0.3f), 2f, 0.45f, 0.3f),
            S("metal_green", "ConcreteAlbedo.png", "ConcreteNormal.png", new Color(0.3f, 0.42f, 0.33f), 2f, 0.5f, 0.4f),
            S("roof", "AsphaltAlbedo.png", "AsphaltNormal.png", new Color(0.42f, 0.4f, 0.39f), 2f),
            S("roof_red", "AsphaltAlbedo.png", "AsphaltNormal.png", new Color(0.62f, 0.3f, 0.22f), 2f),
            S("sandbag", "SandAlbedo.png", "SandNormal.png", new Color(0.78f, 0.7f, 0.52f), 1f),
            S("trim", null, null, new Color(0.07f, 0.07f, 0.08f), 1f, 0f, 0.35f),
            S("hazard", null, null, new Color(0.95f, 0.72f, 0.08f), 1f, 0f, 0.3f),
            S("glass", null, null, new Color(0.16f, 0.2f, 0.24f), 1f, 0.2f, 0.92f),
            S("team_a", null, null, new Color(0.16f, 0.45f, 0.95f), 1f),
            S("team_b", null, null, new Color(0.95f, 0.45f, 0.12f), 1f),
            Glow("glow_white", new Color(1f, 0.95f, 0.85f)),
            Glow("glow_amber", new Color(1f, 0.68f, 0.18f)),
            Glow("glow_a", new Color(0.25f, 0.55f, 1f)),
            Glow("glow_b", new Color(1f, 0.2f, 0.12f)),
        };

        // one terrain layer per Surface (the order of the enum)
        private static readonly (string albedo, string normal, float tile)[] TerrainSurfaces =
        {
            ("GrassAlbedo.png", "GrassNormal.png", 5f),
            ("DirtAlbedo.png", "DirtNormal.png", 5f),
            ("CliffAlbedo.png", "CliffNormal.png", 16f),
            ("SandAlbedo.png", "SandNormal.png", 9f),
            ("RoadGravel.png", "RoadGravelNormal.png", 5f),
            ("AsphaltAlbedo.png", "AsphaltNormal.png", 6f),
            ("ConcreteAlbedo.png", "ConcreteNormal.png", 5f),
            ("RoadAlbedo.png", "RoadNormal.png", 8f),
        };

        [MenuItem("Railbound/Maps/Bake Map Kit")]
        public static void BakeMenu() { Debug.Log("[MapKitBaker] " + Bake()); }

        /// <summary>Batch entry: <c>-executeMethod Railbound.EditorTools.MapKitBaker.BakeBatch</c>.</summary>
        public static void BakeBatch()
        {
            string result = Bake();
            Debug.Log("[MapKitBaker] " + result);
            EditorApplication.Exit(result.StartsWith("OK") ? 0 : 1);
        }

        public static string Bake()
        {
            Directory.CreateDirectory(Folder + "/Materials");
            Directory.CreateDirectory(Folder + "/TerrainLayers");
            Directory.CreateDirectory(Path.GetDirectoryName(KitPath));
            var lit = Shader.Find("HDRP/Lit");
            var terrainLit = Shader.Find("HDRP/TerrainLit");
            if (lit == null || terrainLit == null) return "FAILED: HDRP shaders not found";

            var kit = AssetDatabase.LoadAssetAtPath<MapKit>(KitPath);
            if (kit == null) { kit = ScriptableObject.CreateInstance<MapKit>(); AssetDatabase.CreateAsset(kit, KitPath); }

            kit.materials.Clear();
            int missing = 0;
            foreach (var s in Surfaces)
            {
                var m = MaterialAsset(s.id, lit);
                Texture2D albedo = Tex(s.albedo), normal = Tex(s.normal);
                if (s.albedo != null && albedo == null) { missing++; Debug.LogWarning("[MapKitBaker] texture missing: " + s.albedo); }
                m.SetColor("_BaseColor", s.tint);
                m.SetTexture("_BaseColorMap", albedo);
                m.SetTexture("_NormalMap", normal);
                m.SetFloat("_NormalScale", 1f);
                m.SetFloat("_Metallic", s.metallic);
                m.SetFloat("_Smoothness", s.smooth);
                if (s.glow.a > 0f)
                {
                    HDMaterial.SetUseEmissiveIntensity(m, true);
                    HDMaterial.SetEmissiveColor(m, s.glow);
                    HDMaterial.SetEmissiveIntensity(m, 11f, UnityEditor.Rendering.HighDefinition.EmissiveIntensityUnit.EV100);
                }
                HDMaterial.ValidateMaterial(m);
                EditorUtility.SetDirty(m);
                kit.materials.Add(new MapKit.NamedMaterial { id = s.id, material = m, tileMeters = s.tile });
            }

            kit.terrainLayers = new TerrainLayer[TerrainSurfaces.Length];
            for (int i = 0; i < TerrainSurfaces.Length; i++)
            {
                var (a, n, tile) = TerrainSurfaces[i];
                string path = $"{Folder}/TerrainLayers/{(Surface)i}.terrainlayer";
                var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
                if (layer == null) { layer = new TerrainLayer(); AssetDatabase.CreateAsset(layer, path); }
                layer.diffuseTexture = Tex(a);
                layer.normalMapTexture = Tex(n);
                layer.normalScale = 1f;
                layer.tileSize = new Vector2(tile, tile);
                layer.smoothness = (Surface)i == Surface.Asphalt || (Surface)i == Surface.Road ? 0.18f : 0.06f;
                layer.smoothnessSource = TerrainLayerSmoothnessSource.Constant;   // not the textures' alpha (opaque: mirror-smooth ground)
                layer.metallic = 0f;
                // the sand texture is near white: a warm, darker sand reads as desert ground instead of snow
                layer.diffuseRemapMax = (Surface)i == Surface.Sand ? new Vector4(0.82f, 0.66f, 0.48f, 1f) : Vector4.one;
                EditorUtility.SetDirty(layer);
                kit.terrainLayers[i] = layer;
            }

            var tm = MaterialAsset("terrain", terrainLit);
            HDMaterial.ValidateMaterial(tm);
            EditorUtility.SetDirty(tm);
            kit.terrainMaterial = tm;

            kit.containers = Prefabs("Assets/WSM Game Studio", "Container_Blue_12_19", "Container_Green_12_19", "Container_Red_12_19");
            kit.wagons = Prefabs("Assets/WSM Game Studio", "PlatformWagon_Red_13_4_BlueContainer", "PlatformWagon_Black_13_4_GreenContainer", "PlatformWagon_Red_13_4_RedContainer", "PlatformWagon_Black_13_4", "PlatformWagon_Red_13_4");
            kit.locomotives = Prefabs("Assets/WSM Game Studio", "Locomotive_01_Blue", "Locomotive_01_Orange", "Locomotive_01_White");
            kit.rocks = Prefabs("Assets/Forest Scene", "Rock1A", "Rock1B", "Rock2", "Rock3", "Rock4A", "Rock5A", "Rock6A");
            kit.bushes = Prefabs("Assets/Forest Scene", "P_Bush01", "P_Bush02", "P_Bush03", "P_Bush04", "P_Bush05");
            kit.conifers = Prefabs("Assets/Forest Scene", "PF Conifer Medium BOTD", "PF Conifer Small BOTD", "PF Conifer Tall BOTD");
            var r20 = Prefabs("Assets/WSM Game Studio", "Rails_20m"); kit.rail20 = r20.Length > 0 ? r20[0] : null;
            var r100 = Prefabs("Assets/WSM Game Studio", "Rails_100m"); kit.rail100 = r100.Length > 0 ? r100[0] : null;

            EditorUtility.SetDirty(kit);
            AssetDatabase.SaveAssets();
            return $"OK: {kit.materials.Count} materials ({missing} missing textures), {kit.terrainLayers.Length} terrain layers, " +
                   $"{kit.containers.Length} containers, {kit.wagons.Length} wagons, {kit.locomotives.Length} locomotives, {kit.rocks.Length} rocks, " +
                   $"{kit.bushes.Length} bushes, {kit.conifers.Length} conifers -> {KitPath}";
        }

        private static Material MaterialAsset(string id, Shader shader)
        {
            string path = $"{Folder}/Materials/Map_{id}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader) { name = "Map_" + id }; AssetDatabase.CreateAsset(m, path); }
            else if (m.shader != shader) m.shader = shader;
            return m;
        }

        private static Texture2D Tex(string file)
        {
            if (string.IsNullOrEmpty(file)) return null;
            string path = file.StartsWith("Assets/") ? file : Ground + file;
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static GameObject[] Prefabs(string folder, params string[] names)
        {
            var list = new List<GameObject>();
            foreach (var n in names)
            {
                var guid = AssetDatabase.FindAssets(n + " t:Prefab", new[] { folder })
                    .FirstOrDefault(g => Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g)) == n);
                if (guid == null) { Debug.LogWarning("[MapKitBaker] prefab missing: " + n); continue; }
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (go != null) list.Add(go);
            }
            return list.ToArray();
        }

        // ------------------------------------------------------------------ preview
        [MenuItem("Railbound/Maps/Preview/Railhead (Resurgence)")] private static void P1() => Preview("railhead");
        [MenuItem("Railbound/Maps/Preview/Timberline (Tactical)")] private static void P2() => Preview("timberline");
        [MenuItem("Railbound/Maps/Preview/Depot 9 (Team Deathmatch)")] private static void P3() => Preview("depot9");
        [MenuItem("Railbound/Maps/Preview/Foundry (Free-for-all)")] private static void P4() => Preview("foundry");
        [MenuItem("Railbound/Maps/Preview/Switchback (Gun Game)")] private static void P5() => Preview("switchback");
        [MenuItem("Railbound/Maps/Preview/Signal Hill (Control)")] private static void P6() => Preview("signalhill");
        [MenuItem("Railbound/Maps/Preview/Blackwater (Search & Destroy)")] private static void P7() => Preview("blackwater");
        [MenuItem("Railbound/Maps/Preview/Dustfall (Extraction)")] private static void P8() => Preview("dustfall");

        private static void Preview(string id)
        {
            var def = MapCatalog.Find(id);
            if (def == null || def.IsMap2) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var b = MapLoader.Build(scene, def, preview: true);
            Debug.Log($"[MapKitBaker] Preview of {def.Name}: {b.Boxes} boxes, {b.Props} props. This scene is only a preview: do not save it.");
        }
    }
}
