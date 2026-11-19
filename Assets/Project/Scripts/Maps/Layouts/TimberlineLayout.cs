using System.Collections.Generic;
using UnityEngine;

namespace Railbound.Maps.Layouts
{
    /// <summary>
    /// TIMBERLINE (Tactical / zero build, 20 players, 900 x 900 m). Forested highland: two long ridges run west to east with a river
    /// valley between them; gullies cut down the slopes; rock outcrops crown the crests. Pine stands alternate with open meadows.
    /// The only buildings are low stone ruins, two lookout cabins on the ridges and log bridges over the river: fights are decided
    /// by high ground and natural cover.
    /// </summary>
    public class TimberlineLayout : LayoutBase
    {
        public override float MaxHeight => 230f;
        private const float Valley = 24f;
        private readonly List<Vector2> clearings = new List<Vector2>();   // ruins and cabins (no trees on them)

        private static float RiverZ(float x) => Mathf.Sin(x * 0.0072f) * 38f + Mathf.Sin(x * 0.019f + 1.3f) * 9f;

        public override void Plan()
        {
            // the ruins and cabins sit on flattened ground
            var spots = new[] { new Vector2(-300f, 95f), new Vector2(-120f, -110f), new Vector2(60f, 120f), new Vector2(250f, -95f), new Vector2(330f, 140f), new Vector2(-260f, -230f) };
            foreach (var s in spots) clearings.Add(s);
            clearings.Add(new Vector2(-160f, 228f));   // north lookout
            clearings.Add(new Vector2(180f, -226f));   // south lookout
        }

        private float Raw(float x, float z)
        {
            float o = Outside(x, z);
            float n = MapNoise.Fbm(x, z, 0.0035f, 4, 1f);
            float ridgeN = 230f + MapNoise.Fbm(x, 0f, 0.004f, 2, 2f) * 40f;
            float ridgeS = -225f + MapNoise.Fbm(x, 0f, 0.004f, 2, 3f) * 40f;
            float rN = 78f * Mathf.Exp(-Mathf.Pow((z - ridgeN) / 105f, 2f));
            float rS = 70f * Mathf.Exp(-Mathf.Pow((z - ridgeS) / 110f, 2f));
            float h = Valley + rN + rS + n * 14f;
            // gullies down the slopes
            float gully = MapNoise.Ridged(x, z, 0.0085f, 3, 4f);
            float slopeBand = MapNoise.Smooth(35f, 80f, Mathf.Abs(z)) * (1f - MapNoise.Smooth(200f, 260f, Mathf.Abs(z)));
            h -= gully * gully * 16f * slopeBand;
            // crests get a little rougher
            h += MapNoise.Ridged(x, z, 0.02f, 2, 6f) * 5f * MapNoise.Smooth(60f, 95f, h);
            // the river channel
            float d = Mathf.Abs(z - RiverZ(x));
            h = Mathf.Lerp(h, Valley - 4.5f, 1f - MapNoise.Smooth(6f, 26f, d));
            // mountains beyond the play area
            h += MapNoise.Smooth(0f, 220f, o) * 70f + MapNoise.Fbm(x, z, 0.003f, 4, 9f) * 25f * MapNoise.Smooth(0f, 180f, o);
            return h;
        }

        public override float Height(float x, float z)
        {
            float h = Raw(x, z);
            foreach (var c in clearings)
            {
                float d = Vector2.Distance(new Vector2(x, z), c);
                if (d > 26f) continue;
                h = Mathf.Lerp(h, Raw(c.x, c.y), 1f - MapNoise.Smooth(9f, 26f, d));
            }
            return h;
        }

        public override void Paint(float x, float z, float h, float slope, float[] w)
        {
            float d = Mathf.Abs(z - RiverZ(x));
            float bed = 1f - MapNoise.Smooth(5f, 11f, d);
            float bank = (1f - MapNoise.Smooth(10f, 22f, d)) * (1f - bed);
            float rock = Mathf.Max(MapNoise.Smooth(30f, 44f, slope), MapNoise.Smooth(108f, 125f, h) * 0.7f);
            float forest = Forest(x, z);
            float n = MapNoise.Perlin(x, z, 0.03f, 5f);
            w[(int)Surface.Rock] += rock;
            float free = 1f - rock;
            w[(int)Surface.Gravel] += bed * free;
            w[(int)Surface.Sand] += bank * free * 0.7f;
            float rest = free * (1f - bed) * (1f - bank * 0.7f);
            w[(int)Surface.Dirt] += rest * (forest * 0.55f + MapNoise.Smooth(18f, 30f, slope) * 0.4f + n * 0.1f);
            w[(int)Surface.Grass] += rest * (1f - forest * 0.45f);
        }

        private static float Forest(float x, float z) => MapNoise.Smooth(0.46f, 0.6f, MapNoise.Perlin(x, z, 0.0065f, 11f) * 0.75f + MapNoise.Perlin(x, z, 0.02f, 12f) * 0.25f);

        private bool NearClearing(float x, float z, float r)
        {
            foreach (var c in clearings) if (Vector2.Distance(new Vector2(x, z), c) < r) return true;
            return false;
        }

        public override float TreeDensity(float x, float z, float h, float slope)
        {
            if (slope > 36f || Mathf.Abs(z - RiverZ(x)) < 14f || NearClearing(x, z, 24f)) return 0f;
            float o = Outside(x, z);
            float f = Forest(x, z);
            if (o > 0f) f = Mathf.Max(f, MapNoise.Smooth(0f, 60f, o) * 0.8f);
            float sparse = 0.05f * MapNoise.Perlin(x, z, 0.05f, 13f);
            return f * 2.6f + sparse;
        }

        public override float BushDensity(float x, float z, float h, float slope)
        {
            if (slope > 38f || Mathf.Abs(z - RiverZ(x)) < 9f || NearClearing(x, z, 14f)) return 0f;
            return 0.35f + Forest(x, z) * 0.6f;
        }

        public override float GrassDensity(float x, float z, float h, float slope)
        {
            if (Mathf.Abs(z - RiverZ(x)) < 9f || slope > 34f) return 0f;
            return 0.85f - Forest(x, z) * 0.45f;
        }

        public override void Build()
        {
            var land = B.Group("Timberline");

            // ---------------- rock outcrops on the crests and steep ground (natural cover)
            var rocks = B.Group("Outcrops", land);
            int clusters = 0;
            for (int tries = 0; tries < 2000 && clusters < 150; tries++)
            {
                float x = B.Range(-430f, 430f), z = B.Range(-430f, 430f);
                float h = B.Ground(x, z);
                bool crest = h > 85f || (h > 45f && B.Chance(0.25f));
                if (!crest || NearClearing(x, z, 20f) || Mathf.Abs(z - RiverZ(x)) < 12f) continue;
                int n = B.Range(2, 6);
                for (int i = 0; i < n; i++)
                {
                    float px = x + B.Range(-5f, 5f), pz = z + B.Range(-5f, 5f);
                    B.Rock(B.OnGround(px, pz), B.Range(0.7f, 1.8f), B.Range(0f, 360f), rocks);
                }
                clusters++;
            }
            // boulders in the river
            for (int i = 0; i < 50; i++)
            {
                float x = B.Range(-440f, 440f);
                B.Rock(B.OnGround(x, RiverZ(x) + B.Range(-6f, 6f)), B.Range(0.5f, 1.1f), B.Range(0f, 360f), rocks);
            }

            // ---------------- stone ruins
            for (int i = 0; i < 6; i++) Ruin(clearings[i], B.Group("Ruin", land));

            // ---------------- lookout cabins on the ridges
            for (int i = 6; i < 8; i++)
            {
                var c = clearings[i];
                B.Building(c.x, c.y, i == 6 ? 180f : 0f, new BuildingSpec
                {
                    Width = 6f, Depth = 7f, Floors = 2, FloorHeight = 3.4f, Wall = "wood_dark", Trim = "wood", Floor = "wood", Roof = "roof",
                    WindowEvery = 1.7f, SideDoor = true, LootPerFloor = 2,
                }, land, "Lookout");
            }

            // ---------------- log bridges over the river
            foreach (float bx in new[] { -170f, 40f, 235f })
            {
                float rz = RiverZ(bx);
                float slope = (RiverZ(bx + 1f) - RiverZ(bx - 1f)) * 0.5f;
                float yaw = Mathf.Atan2(-slope, 1f) * Mathf.Rad2Deg;   // across the river
                var g = B.Group("Log bridge", land);
                float y = Mathf.Max(B.Ground(bx, rz - 17f), B.Ground(bx, rz + 17f)) + 0.2f;
                g.position = new Vector3(bx, y, rz);
                g.rotation = Quaternion.Euler(0f, yaw, 0f);
                B.Box(g, new Vector3(0f, -0.2f, 0f), new Vector3(3.4f, 0.4f, 38f), "wood", null, true, "Deck");
                B.Railing(g, new Vector3(-1.7f, 0f, -19f), new Vector3(-1.7f, 0f, 19f), 0f);
                B.Railing(g, new Vector3(1.7f, 0f, -19f), new Vector3(1.7f, 0f, 19f), 0f);
                for (int s = -1; s <= 1; s += 2) B.Box(g, new Vector3(0f, -3f, s * 7f), new Vector3(3f, 5.6f, 0.9f), "wood_dark", null, true, "Trestle");
            }

            View("overview", -520f, -520f, 200f, 0f, 0f, 40f);
            View("valley", -300f, RiverZ(-300f) + 30f, 2f, -100f, RiverZ(-100f));
            View("north ridge", -160f, 210f, 2f, 0f, 120f, 40f);
            View("forest", 120f, -60f, 1.8f, 180f, -110f, 20f);
            View("ruin", clearings[2].x + 14f, clearings[2].y - 10f, 1.8f, clearings[2].x, clearings[2].y);
            View("bridge", 40f + 25f, RiverZ(40f) - 20f, 2f, 40f, RiverZ(40f));
        }

        /// <summary>A stone ruin: the broken walls of a farmhouse with a doorway, a fallen corner and a chimney stack.</summary>
        private void Ruin(Vector2 c, Transform g)
        {
            float w = B.Range(9f, 12f), d = B.Range(7f, 9f), yaw = B.Range(0f, 360f);
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            Vector3 Corner(float lx, float lz) { Vector3 p = new Vector3(c.x, 0f, c.y) + q * new Vector3(lx, 0f, lz); return p; }
            Vector3[] k = { Corner(-w / 2, -d / 2), Corner(w / 2, -d / 2), Corner(w / 2, d / 2), Corner(-w / 2, d / 2) };
            for (int s = 0; s < 4; s++)
            {
                Vector3 a = k[s], b = k[(s + 1) % 4];
                int pieces = 4;
                for (int i = 0; i < pieces; i++)
                {
                    if ((s == 0 && i == 1) || B.Chance(0.18f)) continue;   // the doorway, and fallen pieces
                    Vector3 p0 = Vector3.Lerp(a, b, (float)i / pieces), p1 = Vector3.Lerp(a, b, (float)(i + 1) / pieces);
                    float gy = Mathf.Min(B.Ground(p0.x, p0.z), B.Ground(p1.x, p1.z));
                    B.Segment(g, p0, p1, gy - 0.4f, B.Range(0.9f, 2.6f) + 0.4f, 0.55f, "rock");
                }
            }
            Vector3 ch = Corner(w / 2 - 0.8f, d / 2 - 0.8f);
            B.Box(g, B.OnGround(ch.x, ch.z, 2.6f), new Vector3(1.3f, 5.2f, 1.3f), "rock", null, true, "Chimney");
            B.Loot(B.OnGround(c.x, c.y, 0.2f));
            B.Rock(B.OnGround(c.x + q.x * 3f, c.y - 2f), 0.6f, B.Range(0f, 360f), g);
        }
    }
}
