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
                MakeMaterial("BRHouse_Concrete", new Color(0.55f, 0.55f, 0.52f)),
                MakeMaterial("BRHouse_Rust", new Color(0.50f, 0.28f, 0.22f)),
                MakeMaterial("BRHouse_Sand", new Color(0.66f, 0.58f, 0.42f))
            };
            Material roofMat = MakeMaterial("BRHouse_Roof", new Color(0.22f, 0.24f, 0.26f));
            Material floorMat = MakeMaterial("BRHouse_Floor", new Color(0.32f, 0.30f, 0.28f));
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
                            wallMats[rng.Next(wallMats.Length)], roofMat, floorMat, crateMat, rng, out int pts))
                        continue;

                    houseCenters.Add(p);
                    housesBuilt++;
                    lootPoints += pts;
                }

                coverBuilt += ScatterCover(terrain, villageRoot.transform, center, 62f, 16, crateMat, sandbagMat, rng);
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
            System.Random rng, out int lootPointCount)
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

            // Front wall (z+) has the door; back wall (z-) has a window; side walls are solid
            float doorW = 1.7f, doorH = 2.4f;
            float seg = (w - doorW) * 0.5f;
            Box(house.transform, "FrontWall_L", new Vector3(-(doorW * 0.5f + seg * 0.5f), floorTop + hh * 0.5f, d * 0.5f - t * 0.5f), new Vector3(seg, hh, t), wallMat);
            Box(house.transform, "FrontWall_R", new Vector3((doorW * 0.5f + seg * 0.5f), floorTop + hh * 0.5f, d * 0.5f - t * 0.5f), new Vector3(seg, hh, t), wallMat);
            Box(house.transform, "FrontWall_Lintel", new Vector3(0f, floorTop + doorH + (hh - doorH) * 0.5f, d * 0.5f - t * 0.5f), new Vector3(doorW, hh - doorH, t), wallMat);

            float winW = 1.6f, winBottom = 1.0f, winTop = 2.1f;
            float wseg = (w - winW) * 0.5f;
            Box(house.transform, "BackWall_L", new Vector3(-(winW * 0.5f + wseg * 0.5f), floorTop + hh * 0.5f, -d * 0.5f + t * 0.5f), new Vector3(wseg, hh, t), wallMat);
            Box(house.transform, "BackWall_R", new Vector3((winW * 0.5f + wseg * 0.5f), floorTop + hh * 0.5f, -d * 0.5f + t * 0.5f), new Vector3(wseg, hh, t), wallMat);
            Box(house.transform, "BackWall_Sill", new Vector3(0f, floorTop + winBottom * 0.5f, -d * 0.5f + t * 0.5f), new Vector3(winW, winBottom, t), wallMat);
            Box(house.transform, "BackWall_Head", new Vector3(0f, floorTop + winTop + (hh - winTop) * 0.5f, -d * 0.5f + t * 0.5f), new Vector3(winW, hh - winTop, t), wallMat);

            Box(house.transform, "SideWall_L", new Vector3(-w * 0.5f + t * 0.5f, floorTop + hh * 0.5f, 0f), new Vector3(t, hh, d - 2f * t), wallMat);
            Box(house.transform, "SideWall_R", new Vector3(w * 0.5f - t * 0.5f, floorTop + hh * 0.5f, 0f), new Vector3(t, hh, d - 2f * t), wallMat);

            // Roof slab with a small overhang
            Box(house.transform, "Roof", new Vector3(0f, floorTop + hh + 0.15f, 0f), new Vector3(w + 0.8f, 0.3f, d + 0.8f), roofMat);

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

        // ------------------------------------------------------------------ helpers

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
