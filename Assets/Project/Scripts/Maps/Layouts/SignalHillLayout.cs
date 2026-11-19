using UnityEngine;

namespace Railbound.Maps.Layouts
{
    /// <summary>
    /// SIGNAL HILL (Control, 6 v 6, 160 x 110 m). Three capture zones on a line west to east: A at a relay hut, B under a radio mast
    /// on a low hill in the middle, C at a pump house. Team 1 spawns north, team 2 south, the same distance from all three zones.
    /// A broken trench line and rock outcrops give cover between the zones.
    /// </summary>
    public class SignalHillLayout : LayoutBase
    {
        private const float Floor = 20f;
        private static readonly Vector2 A = new Vector2(-55f, 0f), Bz = new Vector2(0f, 0f), C = new Vector2(55f, 0f);
        public override float MaxHeight => 90f;

        public override void Plan()
        {
            RoundPad(A.x, A.y, 9f, Floor + 0.3f, 6f);
            RoundPad(C.x, C.y, 9f, Floor + 0.2f, 6f);
            Pad(0f, 47f, 18f, 5f, Floor + 0.5f, 6f);
            Pad(0f, -47f, 18f, 5f, Floor + 0.5f, 6f);
        }

        public override float Height(float x, float z)
        {
            float o = Outside(x, z);
            float hill = 6.5f * Mathf.Exp(-(x * x + z * z) / (2f * 24f * 24f));
            float roll = MapNoise.Fbm(x, z, 0.02f, 3, 6f) * 1.6f;
            float edge = MapNoise.Smooth(2f, 55f, o) * 26f + MapNoise.Fbm(x, z, 0.01f, 4, 8f) * 10f * MapNoise.Smooth(0f, 40f, o);
            float h = Floor + hill + roll + edge;
            h = ApplyPads(x, z, h);
            // the top of the hill is flattened for zone B
            float top = Floor + 6.5f;
            float w = 1f - MapNoise.Smooth(7f, 12f, new Vector2(x, z).magnitude);
            return Mathf.Lerp(h, top, w);
        }

        public override void Paint(float x, float z, float h, float slope, float[] w)
        {
            float o = Outside(x, z);
            float path = Mathf.Min(MapNoise.SegmentDistance(x, z, new Vector2(0f, 47f), A), MapNoise.SegmentDistance(x, z, new Vector2(0f, 47f), C));
            path = Mathf.Min(path, Mathf.Min(MapNoise.SegmentDistance(x, z, new Vector2(0f, -47f), A), MapNoise.SegmentDistance(x, z, new Vector2(0f, -47f), C)));
            path = Mathf.Min(path, MapNoise.SegmentDistance(x, z, new Vector2(0f, 47f), new Vector2(0f, -47f)));
            float road = 1f - MapNoise.Smooth(1.2f, 2.6f, path + MapNoise.Perlin(x, z, 0.2f, 3f) * 0.8f);
            float zone = Mathf.Max(1f - MapNoise.Smooth(6f, 10f, Vector2.Distance(new Vector2(x, z), A)), 1f - MapNoise.Smooth(6f, 10f, Vector2.Distance(new Vector2(x, z), C)));
            zone = Mathf.Max(zone, 1f - MapNoise.Smooth(5f, 9f, new Vector2(x, z).magnitude));
            float rock = MapNoise.Smooth(24f, 38f, slope);
            float n = MapNoise.Perlin(x, z, 0.05f, 9f);
            w[(int)Surface.Rock] += rock;
            w[(int)Surface.Gravel] += zone * (1f - rock);
            w[(int)Surface.Dirt] += Mathf.Max(road, 0.25f * n) * (1f - rock) * (1f - zone);
            w[(int)Surface.Grass] += (1f - road) * (1f - rock) * (1f - zone) * (0.8f + 0.2f * n) * (o > 0f ? 1f : 1f);
        }

        public override float TreeDensity(float x, float z, float h, float slope)
        {
            float o = Outside(x, z);
            if (o > 6f) return slope < 32f ? 2f * MapNoise.Perlin(x, z, 0.03f, 2f) : 0f;
            // a few trees in the corners of the play area
            return Mathf.Abs(x) > 64f && Mathf.Abs(z) > 30f ? 0.5f : 0f;
        }

        public override float BushDensity(float x, float z, float h, float slope) => Outside(x, z) > 2f ? 1f : 0.12f;
        public override float GrassDensity(float x, float z, float h, float slope) => Outside(x, z) > 2f ? 0.6f : 0.3f * MapNoise.Smooth(0.35f, 0.7f, MapNoise.Perlin(x, z, 0.05f, 9f));

        public override void Build()
        {
            var site = B.Group("Signal Hill");

            // ---------------- A: relay hut with its antenna
            B.Building(A.x - 4f, A.y, 90f, new BuildingSpec { Width = 6f, Depth = 5.2f, Floors = 1, Wall = "concrete", Tint = new Color(0.74f, 0.78f, 0.72f), Trim = "concrete_dark", Roof = "roof", SideDoor = true }, site, "Relay hut");
            Mast(new Vector2(A.x - 8.5f, A.y + 4.5f), 14f, 0.6f, site);
            B.CrateStack(B.OnGround(A.x + 5f, A.y - 4f), 15f, site);
            B.Barrier(B.OnGround(A.x + 6f, A.y + 5f), 0f, site);
            B.CapturePad(A.x, A.y, 8f, "A", 90f);

            // ---------------- B: the radio mast on the hill, low walls around it
            Mast(Bz, 24f, 1.2f, site);
            for (int i = 0; i < 4; i++)
            {
                float a = (i * 90f + 45f) * Mathf.Deg2Rad;
                Vector2 c = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * 6.5f;
                Vector2 t = new Vector2(Mathf.Cos(a), -Mathf.Sin(a)) * 1.6f;
                B.Segment(site, new Vector3(c.x - t.x, 0, c.y - t.y), new Vector3(c.x + t.x, 0, c.y + t.y), B.Ground(c.x, c.y) - 0.3f, 1.45f, 0.4f, "concrete");
            }
            B.Box(site, B.OnGround(0f, 0f, 0.6f), new Vector3(2.4f, 1.2f, 1.6f), "metal_green", null, true, "Equipment");
            B.Loot(B.OnGround(2.5f, 2.5f, 0.2f));
            B.CapturePad(Bz.x, Bz.y, 9f, "B", 0f);

            // ---------------- C: the pump house and its tanks
            B.Hall(C.x + 4f, C.y, 0f, 9f, 11f, 5f, "concrete", "roof", 3f, 2.8f, 1, site, "Pump house");
            B.Cylinder(B.OnGround(C.x + 12.5f, C.y + 8f, 2.5f), 2.4f, 5f, "metal_rust", 0f, false, site);
            B.Cylinder(B.OnGround(C.x + 12.5f, C.y - 8f, 2.5f), 2.4f, 5f, "metal_rust", 0f, false, site);
            B.Cylinder(B.OnGround(C.x - 1f, C.y + 6f, 0.5f), 0.35f, 8f, "metal", 90f, true, site);
            B.Loot(B.OnGround(C.x + 4f, C.y, 0.25f));
            B.CapturePad(C.x, C.y, 8f, "C", -90f);

            // ---------------- between the zones: a broken trench line and rock outcrops
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * 28f;
                B.Sandbags(B.OnGround(x, 7f), B.OnGround(x + s * 1.5f, 19f), site);
                B.Sandbags(B.OnGround(x, -7f), B.OnGround(x + s * 1.5f, -19f), site);
                B.Sandbags(B.OnGround(x - s * 3f, 28f), B.OnGround(x + s * 3f, 33f), site);
                B.Sandbags(B.OnGround(x - s * 3f, -28f), B.OnGround(x + s * 3f, -33f), site);
                foreach (var p in new[] { new Vector2(s * 44f, 22f), new Vector2(s * 44f, -22f), new Vector2(s * 16f, 30f), new Vector2(s * 16f, -30f), new Vector2(s * 66f, 34f), new Vector2(s * 66f, -34f) })
                    Outcrop(p, 3, site);
            }

            // ---------------- spawns: north team 1, south team 2, each behind a shelter
            TeamSpawns(1, new Vector3(0f, 0f, 48f), 180f, 6, 3.4f);
            TeamSpawns(2, new Vector3(0f, 0f, -48f), 0f, 6, 3.4f);
            B.Building(0f, 53f, 180f, new BuildingSpec { Width = 10f, Depth = 4.2f, Floors = 1, Wall = "wood", Trim = "wood_dark", Roof = "roof", Gable = true, BackDoor = false, Interior = false }, site, "North shelter");
            B.Building(0f, -53f, 0f, new BuildingSpec { Width = 10f, Depth = 4.2f, Floors = 1, Wall = "wood", Trim = "wood_dark", Roof = "roof", Gable = true, BackDoor = false, Interior = false }, site, "South shelter");

            PerimeterWall(2f, "wood_dark", 0.4f);

            View("overview", -90f, -70f, 34f, 0f, 0f);
            View("zone A", -40f, -12f, 1.7f, -55f, 0f);
            View("zone B", 12f, 14f, 1.7f, 0f, 0f, 5f);
            View("zone C", 40f, 12f, 1.7f, 58f, 0f);
            View("north spawn", 0f, 46f, 1.7f, 0f, 0f, 4f);
        }

        /// <summary>A lattice mast: four legs, cross braces every few metres and a red light on top.</summary>
        private void Mast(Vector2 at, float height, float half, Transform parent)
        {
            Vector3 g = B.OnGround(at.x, at.y);
            var parts = new System.Collections.Generic.List<(Vector3, Vector3, Quaternion)>();
            for (int i = 0; i < 4; i++)
            {
                float lx = (i % 2 == 0 ? -1 : 1) * half, lz = (i < 2 ? -1 : 1) * half;
                parts.Add((g + new Vector3(lx, height * 0.5f, lz), new Vector3(0.12f, height, 0.12f), Quaternion.identity));
            }
            for (float y = 2.5f; y < height; y += 2.5f)
            {
                parts.Add((g + new Vector3(0f, y, -half), new Vector3(half * 2f, 0.08f, 0.08f), Quaternion.identity));
                parts.Add((g + new Vector3(0f, y, half), new Vector3(half * 2f, 0.08f, 0.08f), Quaternion.identity));
                parts.Add((g + new Vector3(-half, y, 0f), new Vector3(0.08f, 0.08f, half * 2f), Quaternion.identity));
                parts.Add((g + new Vector3(half, y, 0f), new Vector3(0.08f, 0.08f, half * 2f), Quaternion.identity));
            }
            B.Combined(B.Group("Mast", parent), "Lattice", "metal", parts, false);
            var col = new GameObject("Mast collider");
            col.transform.SetParent(parent, false);
            col.transform.position = g + Vector3.up * (height * 0.5f);
            col.AddComponent<BoxCollider>().size = new Vector3(half * 2f + 0.15f, height, half * 2f + 0.15f);
            B.Box(parent, g + Vector3.up * (height + 0.3f), new Vector3(0.5f, 0.5f, 0.5f), "glow_b", null, false, "Mast light");
        }

        private void Outcrop(Vector2 at, int count, Transform parent)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 p = at + new Vector2(B.Range(-3f, 3f), B.Range(-3f, 3f));
                B.Rock(B.OnGround(p.x, p.y), B.Range(0.7f, 1.4f), B.Range(0f, 360f), parent);
            }
        }
    }
}
