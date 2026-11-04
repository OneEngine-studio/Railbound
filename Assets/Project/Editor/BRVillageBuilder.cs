#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Railbound.BR;

namespace Railbound.Editor
{
    /// <summary>
    /// Builds simple "dummy" houses from cubes and groups them into villages on flat terrain, away from the
    /// existing points of interest. Every house has a door, a window, a floor, a roof, interior cover and
    /// loot points, and everything has real colliders so bots and bullets treat it as solid.
    /// The tool is idempotent: it removes its previous output and rebuilds from the seed.
    /// </summary>
    public static class BRVillageBuilder
    {
        public const string RootName = "[BR] Village POIs";

        private const float WallHeight = 3.2f;
        private const float WallThickness = 0.3f;

        private static readonly string[] ExistingPoiRoots =
        {
            "[BR] Central Train Depot", "[BR] North Radar Station", "[BR] NW Quarry & Derailed Train"
        };

        [MenuItem("Railbound/BR/Build Village POIs")]
        public static void BuildMenu()
        {
            Debug.Log("[BRVillageBuilder] " + Build(20260101, 5, 6));
        }

        public static string Build(int seed, int villageCount, int housesPerVillage)
        {
            var terrain = Terrain.activeTerrain;
            if (terrain == null) return "No active terrain.";

            var rng = new System.Random(seed);

            var old = GameObject.Find(RootName);
            if (old != null) Undo.DestroyObjectImmediate(old);

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Build villages");

            Material[] wallMats =
            {
                MakeStoneMaterial("BRRuin_MossyStone_A", new Color(1f, 1f, 1f)),
                MakeStoneMaterial("BRRuin_MossyStone_B", new Color(0.82f, 0.92f, 0.78f)),
                MakeStoneMaterial("BRRuin_MossyStone_C", new Color(0.92f, 0.84f, 0.76f))
            };
            Material roofMat = MakeStoneMaterial("BRRuin_Roof", new Color(0.45f, 0.5f, 0.42f));
            Material floorMat = MakeStoneMaterial("BRRuin_Floor", new Color(0.6f, 0.62f, 0.58f));
            Material crateMat = MakeMaterial("BRHouse_Crate", new Color(0.42f, 0.33f, 0.20f));
            Material sandbagMat = MakeMaterial("BRHouse_Sandbag", new Color(0.50f, 0.46f, 0.34f));

            var poiCenters = ExistingPoiRoots.Select(FindRootCenter).Where(c => c.HasValue).Select(c => c.Value).ToList();

            var villageCenters = new List<Vector3>();
            int housesBuilt = 0, coverBuilt = 0, lootPoints = 0;

            for (int v = 0; v < villageCount; v++)
            {
                if (!TryFindVillageCenter(terrain, rng, poiCenters, villageCenters, out Vector3 center)) break;
                villageCenters.Add(center);

                var villageRoot = new GameObject("Village " + (v + 1));
                villageRoot.transform.SetParent(root.transform, false);
                villageRoot.transform.position = center;

                var houseCenters = new List<Vector3>();
                int target = Mathf.Max(3, housesPerVillage + rng.Next(-1, 2));

                for (int attempt = 0; attempt < 80 && houseCenters.Count < target; attempt++)
                {
                    float r = 8f + 50f * Mathf.Sqrt((float)rng.NextDouble());
                    float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                    Vector3 p = center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);

                    if (houseCenters.Any(h => Flat(h, p) < 17f)) continue;

                    float yaw = Mathf.Atan2(center.x - p.x, center.z - p.z) * Mathf.Rad2Deg + (float)(rng.NextDouble() * 40.0 - 20.0);
                    float w = Pick(rng, 8f, 11f), d = Pick(rng, 6f, 8f);

                    if (!TryBuildHouse(terrain, villageRoot.transform, "House " + (houseCenters.Count + 1), p, yaw, w, d,
                            wallMats[rng.Next(wallMats.Length)], roofMat, floorMat, crateMat, rng, true, out int pts))
                        continue;

                    houseCenters.Add(p);
                    housesBuilt++;
                    lootPoints += pts;
                }

                coverBuilt += ScatterCover(terrain, villageRoot.transform, center, 62f, 16, crateMat, sandbagMat, rng);
                coverBuilt += BuildColumns(terrain, villageRoot.transform, center, 48f, wallMats[0], rng);
            }

            // Everything built here is static scenery.
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;

            EditorUtility.SetDirty(root);
            return $"villages={villageCenters.Count} houses={housesBuilt} coverPieces={coverBuilt} lootPoints={lootPoints}";
        }

        // ------------------------------------------------------------------ village placement

        private static bool TryFindVillageCenter(Terrain terrain, System.Random rng, List<Vector3> poiCenters,
            List<Vector3> villages, out Vector3 center)
        {
            for (int attempt = 0; attempt < 600; attempt++)
            {
                float r = 270f * Mathf.Sqrt((float)rng.NextDouble());
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                Vector3 p = new Vector3(Mathf.Cos(a) * r, 0f, 15f + Mathf.Sin(a) * r);

                if (poiCenters.Any(c => Flat(c, p) < 110f)) continue;
                if (villages.Any(c => Flat(c, p) < 130f)) continue;

                // Flat enough: height spread over a ring of probes must be small
                float h0 = Height(terrain, p);
                float min = h0, max = h0;
                for (int i = 0; i < 8; i++)
                {
                    float ang = i * Mathf.PI / 4f;
                    float hp = Height(terrain, p + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * 35f);
                    min = Mathf.Min(min, hp);
                    max = Mathf.Max(max, hp);
                }
                if (max - min > 6f) continue;

                if (!UnityEngine.AI.NavMesh.SamplePosition(new Vector3(p.x, h0, p.z), out var nav, 4f, UnityEngine.AI.NavMesh.AllAreas)) continue;

                center = new Vector3(p.x, h0, p.z);
                return true;
            }

            center = Vector3.zero;
            return false;
        }

        private static Vector3? FindRootCenter(string rootName)
        {
            var go = GameObject.Find(rootName);
            if (go == null) return null;
            var rends = go.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0) return go.transform.position;
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b.center;
        }

        // ------------------------------------------------------------------ houses

        private static bool TryBuildHouse(Terrain terrain, Transform parent, string name, Vector3 position, float yaw,
            float w, float d, Material wallMat, Material roofMat, Material floorMat, Material crateMat,
            System.Random rng, bool ruined, out int lootPointCount)
        {
            lootPointCount = 0;
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);

            // Ground check over the footprint: must be nearly flat so doors line up with the ground
            float minH = float.MaxValue, maxH = float.MinValue;
            for (int ix = -1; ix <= 1; ix++)
            {
                for (int iz = -1; iz <= 1; iz++)
                {
                    Vector3 probe = position + rot * new Vector3(ix * w * 0.5f, 0f, iz * d * 0.5f);
                    float h = Height(terrain, probe);
                    minH = Mathf.Min(minH, h);
                    maxH = Mathf.Max(maxH, h);
                }
            }
            if (maxH - minH > 0.6f) return false;

            float baseY = maxH;

            // Free space check: nothing solid (other than terrain, loot and the storm) inside the footprint
            Vector3 boxCenter = new Vector3(position.x, baseY + 2f, position.z);
            var hits = Physics.OverlapBox(boxCenter, new Vector3(w * 0.5f + 1.5f, 2.2f, d * 0.5f + 1.5f), rot, ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (h is TerrainCollider) continue;
                if (h.GetComponentInParent<UnityEngine.AI.NavMeshAgent>() != null) continue;
                return false;
            }

            var house = new GameObject(name);
            house.transform.SetParent(parent, false);
            house.transform.SetPositionAndRotation(new Vector3(position.x, baseY, position.z), rot);

            float t = WallThickness;
            float floorTop = 0.1f;
            float hh = WallHeight;

            // Floor / foundation: thick slab buried in the ground so slopes never show a gap
            Box(house.transform, "Floor", new Vector3(0f, floorTop - 0.5f, 0f), new Vector3(w, 1f, d), floorMat);

            // Front wall (z+) has the door; back wall (z-) has a window; side walls are solid.
            // Ruined houses get randomly crumbled wall heights, a broken lintel and usually no roof.
            float doorW = 1.7f, doorH = 2.4f;
            float seg = (w - doorW) * 0.5f;
            Wall(house.transform, "FrontWall_L", -(doorW * 0.5f + seg * 0.5f), d * 0.5f - t * 0.5f, seg, t, floorTop, hh, wallMat, ruined, rng);
            Wall(house.transform, "FrontWall_R", (doorW * 0.5f + seg * 0.5f), d * 0.5f - t * 0.5f, seg, t, floorTop, hh, wallMat, ruined, rng);
            if (!ruined || rng.NextDouble() < 0.3)
                Box(house.transform, "FrontWall_Lintel", new Vector3(0f, floorTop + doorH + (hh - doorH) * 0.5f, d * 0.5f - t * 0.5f), new Vector3(doorW, hh - doorH, t), wallMat);

            float winW = 1.6f, winBottom = 1.0f, winTop = 2.1f;
            float wseg = (w - winW) * 0.5f;
            Wall(house.transform, "BackWall_L", -(winW * 0.5f + wseg * 0.5f), -d * 0.5f + t * 0.5f, wseg, t, floorTop, hh, wallMat, ruined, rng);
            Wall(house.transform, "BackWall_R", (winW * 0.5f + wseg * 0.5f), -d * 0.5f + t * 0.5f, wseg, t, floorTop, hh, wallMat, ruined, rng);
            Box(house.transform, "BackWall_Sill", new Vector3(0f, floorTop + winBottom * 0.5f, -d * 0.5f + t * 0.5f), new Vector3(winW, winBottom, t), wallMat);
            if (!ruined)
                Box(house.transform, "BackWall_Head", new Vector3(0f, floorTop + winTop + (hh - winTop) * 0.5f, -d * 0.5f + t * 0.5f), new Vector3(winW, hh - winTop, t), wallMat);

            Wall(house.transform, "SideWall_L", -w * 0.5f + t * 0.5f, 0f, t, d - 2f * t, floorTop, hh, wallMat, ruined, rng);
            Wall(house.transform, "SideWall_R", w * 0.5f - t * 0.5f, 0f, t, d - 2f * t, floorTop, hh, wallMat, ruined, rng);

            // Roof slab with a small overhang (most ruined houses have lost theirs)
            if (!ruined || rng.NextDouble() < 0.25)
                Box(house.transform, "Roof", new Vector3(0f, floorTop + hh + 0.15f, 0f), new Vector3(w + 0.8f, 0.3f, d + 0.8f), roofMat);

            // Rubble blocks around the walls
            if (ruined)
            {
                int blocks = rng.Next(4, 9);
                for (int i = 0; i < blocks; i++)
                {
                    float rx = ((float)rng.NextDouble() - 0.5f) * (w + 4f);
                    float rz = ((float)rng.NextDouble() - 0.5f) * (d + 4f);
                    float s2 = Pick(rng, 0.4f, 1.0f);
                    var rub = Box(house.transform, "Rubble", new Vector3(rx, floorTop + s2 * 0.35f, rz), new Vector3(s2 * 1.3f, s2 * 0.7f, s2), wallMat);
                    rub.transform.localRotation = Quaternion.Euler(Pick(rng, -12f, 12f), (float)rng.NextDouble() * 360f, Pick(rng, -12f, 12f));
                }
            }

            // Interior cover: a couple of crates along the back wall
            int crates = rng.Next(1, 3);
            for (int i = 0; i < crates; i++)
            {
                float cx = ((float)rng.NextDouble() - 0.5f) * (w - 3f);
                Box(house.transform, "Crate " + (i + 1), new Vector3(cx, floorTop + 0.5f, -d * 0.5f + 1.2f), new Vector3(1.0f, 1.0f, 1.0f), crateMat);
            }

            // Loot points on the floor inside
            lootPointCount = rng.Next(1, 3);
            for (int i = 0; i < lootPointCount; i++)
            {
                var lp = new GameObject("LootPoint " + (i + 1));
                lp.transform.SetParent(house.transform, false);
                lp.transform.localPosition = new Vector3(((float)rng.NextDouble() - 0.5f) * (w - 3.2f), floorTop + 0.05f,
                    ((float)rng.NextDouble() - 0.5f) * (d - 3.2f) + 0.4f);
                lp.AddComponent<BRLootPoint>();
            }

            return true;
        }

        // ------------------------------------------------------------------ outdoor cover

        private static int ScatterCover(Terrain terrain, Transform parent, Vector3 center, float radius, int count,
            Material crateMat, Material sandbagMat, System.Random rng)
        {
            var cover = new GameObject("Cover");
            cover.transform.SetParent(parent, false);

            int built = 0;
            for (int attempt = 0; attempt < count * 8 && built < count; attempt++)
            {
                float r = 6f + radius * Mathf.Sqrt((float)rng.NextDouble());
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                Vector3 p = center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                float h = Height(terrain, p);
                bool sandbag = rng.NextDouble() < 0.55;

                Vector3 size = sandbag ? new Vector3(2.6f, 0.9f, 0.7f) : new Vector3(1.3f, 1.3f, 1.3f);
                float yaw = (float)rng.NextDouble() * 360f;
                Quaternion rot = Quaternion.Euler(0f, yaw, 0f);

                bool blocked = false;
                foreach (var hit in Physics.OverlapBox(new Vector3(p.x, h + size.y * 0.5f + 0.2f, p.z), size * 0.5f + Vector3.one * 0.4f, rot, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit is TerrainCollider) continue;
                    blocked = true;
                    break;
                }
                if (blocked) continue;

                Box(cover.transform, sandbag ? "Sandbags" : "Crate", new Vector3(p.x, h + size.y * 0.5f - 0.05f, p.z), size,
                    sandbag ? sandbagMat : crateMat, rot, worldSpace: true);
                built++;
            }
            return built;
        }

        // ------------------------------------------------------------------ ancient columns

        /// <summary>Standing, broken and fallen stone columns: ancient ruins scattered around a village.</summary>
        private static int BuildColumns(Terrain terrain, Transform parent, Vector3 center, float radius, Material stone, System.Random rng)
        {
            var columns = new GameObject("Ancient Columns");
            columns.transform.SetParent(parent, false);

            int count = rng.Next(4, 8), built = 0;
            for (int attempt = 0; attempt < count * 10 && built < count; attempt++)
            {
                float r = 10f + radius * Mathf.Sqrt((float)rng.NextDouble());
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                Vector3 p = center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                float h = Height(terrain, p);

                float diameter = Pick(rng, 1.0f, 1.5f);
                float length = Pick(rng, 2.5f, 5.5f);
                int kind = rng.Next(3); // 0 standing, 1 broken stump, 2 fallen
                Quaternion rot;
                Vector3 pos;
                if (kind == 0)
                {
                    rot = Quaternion.Euler(Pick(rng, -4f, 4f), (float)rng.NextDouble() * 360f, Pick(rng, -4f, 4f));
                    pos = new Vector3(p.x, h + length * 0.5f - 0.1f, p.z);
                }
                else if (kind == 1)
                {
                    length *= 0.4f;
                    rot = Quaternion.Euler(Pick(rng, -8f, 8f), (float)rng.NextDouble() * 360f, Pick(rng, -8f, 8f));
                    pos = new Vector3(p.x, h + length * 0.5f - 0.1f, p.z);
                }
                else
                {
                    rot = Quaternion.Euler(86f, (float)rng.NextDouble() * 360f, 0f);
                    pos = new Vector3(p.x, h + diameter * 0.5f - 0.05f, p.z);
                }

                bool blocked = false;
                foreach (var hit in Physics.OverlapSphere(pos, Mathf.Max(length * 0.5f, 1.5f), ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit is TerrainCollider) continue;
                    blocked = true;
                    break;
                }
                if (blocked) continue;

                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = kind == 0 ? "Column" : kind == 1 ? "Broken Column" : "Fallen Column";
                go.transform.SetParent(columns.transform, false);
                go.transform.SetPositionAndRotation(pos, rot);
                go.transform.localScale = new Vector3(diameter, length * 0.5f, diameter); // cylinder height is 2 units
                go.GetComponent<MeshRenderer>().sharedMaterial = stone;
                built++;
            }
            return built;
        }

        // ------------------------------------------------------------------ helpers

        private static void Wall(Transform parent, string name, float x, float z, float sx, float sz, float floorTop,
            float fullHeight, Material mat, bool ruined, System.Random rng)
        {
            float h = ruined ? Pick(rng, 1.0f, fullHeight) : fullHeight;
            Box(parent, name, new Vector3(x, floorTop + h * 0.5f, z), new Vector3(sx, h, sz), mat);
        }

        private static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat,
            Quaternion? worldRot = null, bool worldSpace = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            if (worldSpace)
            {
                go.transform.position = pos;
                go.transform.rotation = worldRot ?? Quaternion.identity;
            }
            else
            {
                go.transform.localPosition = pos;
            }
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private static float Height(Terrain terrain, Vector3 p)
        {
            return terrain.SampleHeight(p) + terrain.transform.position.y;
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static float Pick(System.Random rng, float min, float max)
        {
            return min + (float)rng.NextDouble() * (max - min);
        }

        /// <summary>
        /// Mossy rock material built from the terrain demo's Cliff_Mossy_E PBR layer (colour, normal, mask),
        /// using triplanar mapping so the texture does not stretch on scaled cubes.
        /// </summary>
        private static Material MakeStoneMaterial(string name, Color tint)
        {
            const string folder = "Assets/Project/Materials/BRVillage";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Project/Materials")) AssetDatabase.CreateFolder("Assets/Project", "Materials");
                AssetDatabase.CreateFolder("Assets/Project/Materials", "BRVillage");
            }

            string path = folder + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("HDRP/Lit") ?? Shader.Find("Standard")) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }

            TerrainLayer layer = null;
            foreach (var guid in AssetDatabase.FindAssets("Cliff_Mossy_E t:TerrainLayer"))
            {
                layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(AssetDatabase.GUIDToAssetPath(guid));
                if (layer != null) break;
            }

            if (layer != null)
            {
                if (mat.HasProperty("_BaseColorMap")) mat.SetTexture("_BaseColorMap", layer.diffuseTexture);
                if (mat.HasProperty("_NormalMap")) mat.SetTexture("_NormalMap", layer.normalMapTexture);
                if (mat.HasProperty("_MaskMap")) mat.SetTexture("_MaskMap", layer.maskMapTexture);
                if (mat.HasProperty("_NormalScale")) mat.SetFloat("_NormalScale", 1.2f);
                float tile = 1f / Mathf.Max(0.5f, layer.tileSize.x);
                if (mat.HasProperty("_BaseColorMap")) mat.SetTextureScale("_BaseColorMap", new Vector2(tile, tile));
                // Triplanar mapping in world space (UV set 5 = Triplanar in HDRP Lit)
                if (mat.HasProperty("_UVBase")) mat.SetFloat("_UVBase", 5f);
                if (mat.HasProperty("_TexWorldScale")) mat.SetFloat("_TexWorldScale", 1f);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.35f);
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);

            UnityEditor.Rendering.HighDefinition.HDShaderUtils.ResetMaterialKeywords(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material MakeMaterial(string name, Color color)
        {
            const string folder = "Assets/Project/Materials/BRVillage";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Project/Materials")) AssetDatabase.CreateFolder("Assets/Project", "Materials");
                AssetDatabase.CreateFolder("Assets/Project/Materials", "BRVillage");
            }

            string path = folder + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            Shader shader = Shader.Find("HDRP/Lit") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader) { name = name };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.15f);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
#endif
