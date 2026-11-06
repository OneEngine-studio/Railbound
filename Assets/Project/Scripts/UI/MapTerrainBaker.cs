using System;
using System.Collections;
using UnityEngine;

namespace Railbound.UI
{
    /// <summary>
    /// Builds the clean "automap" base image for the minimap and the full map straight from the terrain heightmap:
    /// soft hill shading, contour lines, a low-water tint and a faint 100 m / 200 m grid. The result is blitted into a
    /// RenderTexture (with GPU-generated mip-maps, so the zoomed-out full map stays crisp) that both map views show
    /// through a RawImage. It runs in small row batches over a few seconds so loading never hitches.
    /// </summary>
    public static class MapTerrainBaker
    {
        public struct Settings
        {
            public int textureSize;       // output resolution (square)
            public float waterLevel;      // metres; terrain below this is tinted as water
            public float minorContour;    // metres between thin contour lines
            public float majorContour;    // metres between strong contour lines
            public float gridMinor;       // metres between faint grid lines
            public float gridMajor;       // metres between stronger grid lines
            public int rowsPerFrame;

            public static Settings Default => new Settings
            {
                textureSize = 1536,
                waterLevel = -999f,
                minorContour = 10f,
                majorContour = 50f,
                gridMinor = 100f,
                gridMajor = 200f,
                rowsPerFrame = 14
            };
        }

        public static RenderTexture CreateTarget(int size)
        {
            var rt = new RenderTexture(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = "Railbound_MapRT",
                useMipMap = true,
                autoGenerateMips = true,
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 4
            };
            rt.Create();
            // dark placeholder until the bake finishes
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(false, true, new Color(0.055f, 0.06f, 0.065f, 1f));
            RenderTexture.active = prev;
            return rt;
        }

        /// <summary>Coroutine: bakes <paramref name="terrain"/> into <paramref name="target"/> (square, covers the terrain footprint).</summary>
        public static IEnumerator Bake(Terrain terrain, RenderTexture target, Settings s, Action onDone)
        {
            if (terrain == null || terrain.terrainData == null || target == null) yield break;

            TerrainData td = terrain.terrainData;
            int res = td.heightmapResolution;
            float[,] h2 = td.GetHeights(0, 0, res, res);
            var h = new float[res * res];
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                    h[y * res + x] = h2[y, x];
            h2 = null;

            float sizeX = td.size.x, sizeY = td.size.y;
            float baseY = terrain.transform.position.y;
            int n = Mathf.Max(64, s.textureSize);
            float texelM = sizeX / n;                       // metres per output texel
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color32[n * n];

            // light from the north-west, slightly above the horizon
            Vector3 light = new Vector3(-0.55f, 0.75f, 0.45f).normalized;
            float flatShade = light.y;

            int rows = Mathf.Max(1, s.rowsPerFrame);
            for (int y0 = 0; y0 < n; y0 += rows)
            {
                int y1 = Mathf.Min(n, y0 + rows);
                for (int y = y0; y < y1; y++)
                {
                    float v = (y + 0.5f) / n;
                    float wz = v * sizeX;
                    for (int x = 0; x < n; x++)
                    {
                        float u = (x + 0.5f) / n;
                        float wx = u * sizeX;

                        float hm = Sample(h, res, u, v) * sizeY + baseY;
                        float hxp = Sample(h, res, u + 1f / n, v) * sizeY;
                        float hxm = Sample(h, res, u - 1f / n, v) * sizeY;
                        float hzp = Sample(h, res, u, v + 1f / n) * sizeY;
                        float hzm = Sample(h, res, u, v - 1f / n) * sizeY;
                        float dhdx = (hxp - hxm) / (2f * texelM);
                        float dhdz = (hzp - hzm) / (2f * texelM);
                        float slope = Mathf.Sqrt(dhdx * dhdx + dhdz * dhdz);

                        // hill shading
                        float inv = 1f / Mathf.Sqrt(dhdx * dhdx + 1f + dhdz * dhdz);
                        float shade = (-dhdx * light.x + 1f * light.y + -dhdz * light.z) * inv;
                        float s01 = Mathf.Clamp((shade - flatShade) * 1.7f, -0.45f, 0.45f);

                        // elevation ramp: valley floor dark, ridges lighter
                        float e = Mathf.Clamp01((hm - 8f) / 150f);
                        e = e * e * (3f - 2f * e);
                        float g = Mathf.Lerp(0.27f, 0.58f, e) + s01 * 0.30f;
                        Color c = new Color(g * 0.93f, g * 1.0f, g * 0.97f, 1f);

                        // low ground / water
                        if (s.waterLevel > -100f && hm < s.waterLevel)
                        {
                            float depth = Mathf.Clamp01((s.waterLevel - hm) / 6f);
                            Color w = Color.Lerp(new Color(0.20f, 0.33f, 0.43f), new Color(0.12f, 0.24f, 0.34f), depth);
                            c = Color.Lerp(c, w, 0.88f);
                        }

                        // contours (only where the ground actually slopes, so flat valley stays clean)
                        c = Contour(c, hm, slope, texelM, s.minorContour, 0.10f, 0.012f);
                        c = Contour(c, hm, slope, texelM, s.majorContour, 0.24f, 0.012f);

                        // grid
                        float gridA = Mathf.Max(GridLine(wx, wz, s.gridMinor, texelM) * 0.11f,
                                                GridLine(wx, wz, s.gridMajor, texelM) * 0.26f);
                        if (gridA > 0f) c = Color.Lerp(c, Color.white, gridA);

                        px[y * n + x] = c;
                    }
                }
                yield return null;
            }

            tex.SetPixels32(px);
            tex.Apply(false, false);
            Graphics.Blit(tex, target);
            UnityEngine.Object.Destroy(tex);
            onDone?.Invoke();
        }

        private static Color Contour(Color c, float hm, float slope, float texelM, float interval, float strength, float minSlope)
        {
            if (interval <= 0.01f || slope < minSlope) return c;
            float f = hm / interval;
            float distH = Mathf.Abs(f - Mathf.Round(f)) * interval;     // metres of height to the nearest line
            float distPx = distH / (slope * texelM);                     // ... converted to screen texels
            float a = 1f - Mathf.SmoothStep(0.35f, 1.15f, distPx);
            a *= strength * Mathf.Clamp01(slope * 5f);
            return a > 0f ? Color.Lerp(c, Color.white, a) : c;
        }

        private static float GridLine(float wx, float wz, float spacing, float texelM)
        {
            if (spacing <= 0.01f) return 0f;
            float dx = Mathf.Abs(wx - Mathf.Round(wx / spacing) * spacing) / texelM;
            float dz = Mathf.Abs(wz - Mathf.Round(wz / spacing) * spacing) / texelM;
            float d = Mathf.Min(dx, dz);
            return 1f - Mathf.SmoothStep(0.4f, 1.3f, d);
        }

        private static float Sample(float[] h, int res, float u, float v)
        {
            float fx = Mathf.Clamp01(u) * (res - 1);
            float fy = Mathf.Clamp01(v) * (res - 1);
            int x0 = (int)fx, y0 = (int)fy;
            int x1 = Mathf.Min(x0 + 1, res - 1), y1 = Mathf.Min(y0 + 1, res - 1);
            float tx = fx - x0, ty = fy - y0;
            float a = h[y0 * res + x0], b = h[y0 * res + x1], c = h[y1 * res + x0], d = h[y1 * res + x1];
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }
    }
}
