using System.Collections.Generic;
using UnityEngine;

namespace Railbound.Maps
{
    public partial class MapBuilder
    {
        /// <summary>Builds the map's terrain from the layout: heights, ground paint, trees, bushes and grass.</summary>
        public void BuildTerrain(MapLayout layout)
        {
            float S = Definition.TerrainSize;
            float H = layout.MaxHeight;
            int res = S <= 640f ? 513 : 1025;
            var td = new TerrainData { name = Definition.Id + "_terrain", heightmapResolution = res };
            td.size = new Vector3(S, H, S);
            float x0 = -S * 0.5f, z0 = -S * 0.5f;

            var heights = new float[res, res];
            float step = S / (res - 1);
            for (int zi = 0; zi < res; zi++)
                for (int xi = 0; xi < res; xi++)
                    heights[zi, xi] = Mathf.Clamp01(layout.Height(x0 + xi * step, z0 + zi * step) / H);
            td.SetHeights(0, 0, heights);

            // ground paint
            TerrainLayer[] layers = Layers(out int[] surfaceToLayer);
            td.terrainLayers = layers;
            int ar = S <= 640f ? 512 : 1024;
            td.alphamapResolution = ar;
            var alpha = new float[ar, ar, Mathf.Max(1, layers.Length)];
            var w = new float[8];
            for (int zi = 0; zi < ar; zi++)
            {
                for (int xi = 0; xi < ar; xi++)
                {
                    float u = (xi + 0.5f) / ar, v = (zi + 0.5f) / ar;
                    float wx = x0 + u * S, wz = z0 + v * S;
                    float h = td.GetInterpolatedHeight(u, v);
                    float slope = td.GetSteepness(u, v);
                    System.Array.Clear(w, 0, w.Length);
                    layout.Paint(wx, wz, h, slope, w);
                    float sum = 0f;
                    for (int k = 0; k < 8; k++) sum += w[k];
                    if (sum <= 0f) { w[(int)Surface.Grass] = 1f; sum = 1f; }
                    for (int k = 0; k < 8; k++)
                        if (w[k] > 0f && layers.Length > 0) alpha[zi, xi, surfaceToLayer[k]] += w[k] / sum;
                }
            }
            if (layers.Length > 0) td.SetAlphamaps(0, 0, alpha);

            // trees and bushes (terrain instances: cheap to draw, with the prefab's colliders)
            var protos = new List<TreePrototype>();
            int treeStart = protos.Count;
            var treePrefabs = Map2Trees.Count > 0 ? Map2Trees : new List<GameObject>(Kit != null ? Kit.conifers : new GameObject[0]);
            foreach (var p in treePrefabs) if (p != null) protos.Add(new TreePrototype { prefab = p });
            int treeCount = protos.Count - treeStart;
            int bushStart = protos.Count;
            var bushPrefabs = Map2Bushes.Count > 0 ? Map2Bushes : new List<GameObject>(Kit != null ? Kit.bushes : new GameObject[0]);
            foreach (var p in bushPrefabs) if (p != null) protos.Add(new TreePrototype { prefab = p });
            int bushCount = protos.Count - bushStart;
            td.treePrototypes = protos.ToArray();

            var instances = new List<TreeInstance>();
            const float cell = 5f;
            int cells = Mathf.FloorToInt(S / cell);
            for (int cz = 0; cz < cells; cz++)
            {
                for (int cx = 0; cx < cells; cx++)
                {
                    float wx = x0 + (cx + (float)Rng.NextDouble()) * cell;
                    float wz = z0 + (cz + (float)Rng.NextDouble()) * cell;
                    float u = (wx - x0) / S, v = (wz - z0) / S;
                    float h = td.GetInterpolatedHeight(u, v);
                    float slope = td.GetSteepness(u, v);
                    double roll = Rng.NextDouble();
                    if (treeCount > 0)
                    {
                        float d = layout.TreeDensity(wx, wz, h, slope) * cell * cell / 100f;
                        if (roll < d) { instances.Add(Tree(treeStart + Rng.Next(treeCount), u, v, 0.8f, 1.35f)); continue; }
                    }
                    if (bushCount > 0)
                    {
                        float d = layout.BushDensity(wx, wz, h, slope) * cell * cell / 100f;
                        if (roll < d) instances.Add(Tree(bushStart + Rng.Next(bushCount), u, v, 0.4f, 0.8f));
                    }
                }
            }
            if (instances.Count > 0) td.SetTreeInstances(instances.ToArray(), true);

            // grass (MAP-2's own grass when the scene has it)
            if (Map2Grass.Count > 0 && !Headless)
            {
                int dr = S <= 640f ? 512 : 1024;
                td.SetDetailResolution(dr, 32);
                if (Map2Terrain != null && Map2Terrain.terrainData != null) td.SetDetailScatterMode(Map2Terrain.terrainData.detailScatterMode);
                td.detailPrototypes = Map2Grass.ToArray();
                bool coverage = td.detailScatterMode == DetailScatterMode.CoverageMode;
                int n = Map2Grass.Count;
                var maps = new int[n][,];
                for (int i = 0; i < n; i++) maps[i] = new int[dr, dr];
                for (int zi = 0; zi < dr; zi++)
                {
                    for (int xi = 0; xi < dr; xi++)
                    {
                        float u = (xi + 0.5f) / dr, v = (zi + 0.5f) / dr;
                        float wx = x0 + u * S, wz = z0 + v * S;
                        float g = layout.GrassDensity(wx, wz, td.GetInterpolatedHeight(u, v), td.GetSteepness(u, v));
                        if (g <= 0.01f) continue;
                        // patches: each kind of grass has its own places
                        int kind = Mathf.Clamp(Mathf.FloorToInt(Mathf.PerlinNoise(wx * 0.021f + 31.7f, wz * 0.021f + 7.3f) * n * 1.3f), 0, n - 1);
                        // MAP-2's grass is tall: keep it a light cover that does not hide the ground, the markers or a crouching player
                        maps[kind][zi, xi] = coverage ? Mathf.RoundToInt(Mathf.Clamp01(g) * 110f) : Mathf.RoundToInt(Mathf.Clamp01(g) * 3f);
                    }
                }
                for (int i = 0; i < n; i++) td.SetDetailLayer(0, 0, i, maps[i]);
            }

            var go = Terrain.CreateTerrainGameObject(td);
            go.name = "Terrain " + Definition.Name;
            go.transform.SetParent(Root, false);
            go.transform.position = new Vector3(x0, 0f, z0);
            Terrain = go.GetComponent<Terrain>();
            Material mat = Map2TerrainMaterial != null ? Map2TerrainMaterial : (Kit != null ? Kit.terrainMaterial : null);
            if (mat != null) Terrain.materialTemplate = mat;
            if (Map2Terrain != null)
            {
                Terrain.heightmapPixelError = Map2Terrain.heightmapPixelError;
                Terrain.basemapDistance = Map2Terrain.basemapDistance;
                Terrain.detailObjectDistance = Mathf.Min(Map2Terrain.detailObjectDistance, 120f);
                Terrain.detailObjectDensity = Map2Terrain.detailObjectDensity;
                Terrain.treeDistance = Map2Terrain.treeDistance;
                Terrain.treeBillboardDistance = Map2Terrain.treeBillboardDistance;
                Terrain.treeCrossFadeLength = Map2Terrain.treeCrossFadeLength;
                Terrain.treeMaximumFullLODCount = Map2Terrain.treeMaximumFullLODCount;
                Terrain.drawInstanced = Map2Terrain.drawInstanced;
                Terrain.shadowCastingMode = Map2Terrain.shadowCastingMode;
            }
            else
            {
                Terrain.drawInstanced = true;
                Terrain.treeDistance = 1200f;
                Terrain.treeBillboardDistance = 120f;
            }
        }

        private TreeInstance Tree(int prototype, float u, float v, float min, float max)
        {
            float s = Range(min, max);
            return new TreeInstance
            {
                prototypeIndex = prototype,
                position = new Vector3(u, 0f, v),
                widthScale = s * Range(0.9f, 1.1f),
                heightScale = s,
                rotation = Range(0f, Mathf.PI * 2f),
                color = Color.white,
                lightmapColor = Color.white,
            };
        }

        /// <summary>The terrain layers (the kit's, else MAP-2's) and where each Surface paints.</summary>
        private TerrainLayer[] Layers(out int[] surfaceToLayer)
        {
            surfaceToLayer = new int[8];
            if (Kit != null && Kit.terrainLayers.Length >= 8 && System.Array.TrueForAll(Kit.terrainLayers, l => l != null))
            {
                for (int i = 0; i < 8; i++) surfaceToLayer[i] = i;
                return Kit.terrainLayers;
            }
            var src = Map2Layers ?? new TerrainLayer[0];
            if (src.Length == 0) return src;
            string[][] keys =
            {
                new[] { "grass_a", "grass" }, new[] { "soil", "heather" }, new[] { "cliff" }, new[] { "black_sand_a", "sand" },
                new[] { "pebbles" }, new[] { "black_sand_rocks", "sand" }, new[] { "pebbles", "tidal" }, new[] { "black_sand_rocks", "pebbles" },
            };
            for (int s = 0; s < 8; s++)
            {
                surfaceToLayer[s] = 0;
                bool found = false;
                foreach (var k in keys[s])
                {
                    for (int i = 0; i < src.Length && !found; i++)
                        if (src[i] != null && src[i].name.ToLowerInvariant().Contains(k)) { surfaceToLayer[s] = i; found = true; }
                    if (found) break;
                }
            }
            return src;
        }
    }

    /// <summary>Small noise and shaping helpers for the layouts' height functions.</summary>
    public static class MapNoise
    {
        public static float Perlin(float x, float z, float scale, float seed) => Mathf.PerlinNoise(x * scale + seed * 13.37f + 1000f, z * scale - seed * 7.91f + 1000f);

        /// <summary>Fractal noise from -1 to 1.</summary>
        public static float Fbm(float x, float z, float scale, int octaves, float seed)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += (Perlin(x, z, scale, seed + i * 3.1f) * 2f - 1f) * amp;
                norm += amp;
                amp *= 0.5f;
                scale *= 2.03f;
            }
            return sum / norm;
        }

        /// <summary>Ridged noise from 0 to 1: sharp crests.</summary>
        public static float Ridged(float x, float z, float scale, int octaves, float seed)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                float n = 1f - Mathf.Abs(Perlin(x, z, scale, seed + i * 5.7f) * 2f - 1f);
                sum += n * n * amp;
                norm += amp;
                amp *= 0.5f;
                scale *= 2.1f;
            }
            return sum / norm;
        }

        public static float Smooth(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Distance from a point to a rectangle centred at (cx, cz), half sizes (hx, hz), turned by yaw degrees (0 inside).</summary>
        public static float RectDistance(float x, float z, float cx, float cz, float hx, float hz, float yaw = 0f)
        {
            float dx = x - cx, dz = z - cz;
            if (yaw != 0f)
            {
                float r = -yaw * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
                float rx = dx * c - dz * s, rz = dx * s + dz * c;
                dx = rx; dz = rz;
            }
            float ox = Mathf.Max(Mathf.Abs(dx) - hx, 0f), oz = Mathf.Max(Mathf.Abs(dz) - hz, 0f);
            return Mathf.Sqrt(ox * ox + oz * oz);
        }

        /// <summary>Distance from a point to the segment a-b on the ground plane.</summary>
        public static float SegmentDistance(float x, float z, Vector2 a, Vector2 b)
        {
            Vector2 p = new Vector2(x, z), ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }
    }

    /// <summary>A flat area a layout presses into its terrain (a town square, an arena floor, a pad), with a soft edge.</summary>
    public struct FlatPad
    {
        public float X, Z, HalfX, HalfZ, Yaw, Height, Falloff;
        public bool Round;

        /// <summary>How much of the pad's height wins at a point (1 inside, 0 beyond the falloff).</summary>
        public float Weight(float x, float z)
        {
            float d = Round ? Mathf.Max(0f, new Vector2(x - X, z - Z).magnitude - HalfX) : MapNoise.RectDistance(x, z, X, Z, HalfX, HalfZ, Yaw);
            return 1f - MapNoise.Smooth(0f, Mathf.Max(0.01f, Falloff), d);
        }
    }
}
