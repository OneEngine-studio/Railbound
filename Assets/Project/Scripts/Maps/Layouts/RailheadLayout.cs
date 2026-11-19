using System.Collections.Generic;
using UnityEngine;

namespace Railbound.Maps.Layouts
{
    /// <summary>
    /// RAILHEAD (Resurgence, 20 players, 520 x 520 m). A compact rail junction town on a plateau: a four-track rail yard with a
    /// station in the middle, and four districts around it, each with its own look:
    ///   north-west market (two-floor houses round a market square), north-east workshops (halls, sheds, containers),
    ///   south-west terraces (rows of gabled houses), south-east water-tower hill.
    /// A dry river bed runs along the south edge; hills rise beyond the play area. Short distances between buildings keep respawning
    /// players close to the fight.
    /// </summary>
    public class RailheadLayout : LayoutBase
    {
        private const float Plateau = 30f;
        private static readonly float[] Tracks = { -13.5f, -4.5f, 4.5f, 13.5f };
        private static readonly Vector2 Hill = new Vector2(150f, -150f);
        private const float RiverZ = -236f;
        public override float MaxHeight => 110f;

        private readonly List<Vector2> roadsA = new List<Vector2>(), roadsB = new List<Vector2>();

        public override void Plan()
        {
            Pad(0f, 0f, 262f, 30f, Plateau, 18f);              // the yard
            Pad(0f, 40f, 70f, 14f, Plateau + 0.4f, 10f);       // station square
            Pad(-130f, 150f, 22f, 22f, Plateau + 0.6f, 14f);   // market square
            AddRoad(new Vector2(0f, 30f), new Vector2(0f, 250f));
            AddRoad(new Vector2(0f, -30f), new Vector2(0f, -222f));
            AddRoad(new Vector2(-255f, 62f), new Vector2(255f, 62f));
            AddRoad(new Vector2(-255f, -62f), new Vector2(255f, -62f));
            for (int i = 0; i < 3; i++)
            {
                float z = 110f + i * 50f;
                AddRoad(new Vector2(-240f, z), new Vector2(-30f, z));
                AddRoad(new Vector2(30f, z), new Vector2(240f, z));
                AddRoad(new Vector2(-240f, -z), new Vector2(-30f, -z));
            }
            AddRoad(new Vector2(-130f, 62f), new Vector2(-130f, 240f));
            AddRoad(new Vector2(130f, 62f), new Vector2(130f, 240f));
            AddRoad(new Vector2(-130f, -62f), new Vector2(-130f, -225f));
        }

        private void AddRoad(Vector2 a, Vector2 b) { roadsA.Add(a); roadsB.Add(b); }

        private float RoadDistance(float x, float z)
        {
            float d = float.MaxValue;
            for (int i = 0; i < roadsA.Count; i++) d = Mathf.Min(d, MapNoise.SegmentDistance(x, z, roadsA[i], roadsB[i]));
            return d;
        }

        public override float Height(float x, float z)
        {
            float o = Outside(x, z);
            float h = Plateau + MapNoise.Fbm(x, z, 0.006f, 3, 2f) * 2.2f;
            // water-tower hill
            float hd = Vector2.Distance(new Vector2(x, z), Hill);
            h += 14f * Mathf.Exp(-(hd * hd) / (2f * 55f * 55f));
            // the dry river bed along the south edge, and its bank
            float rz = RiverZ + Mathf.Sin(x * 0.012f) * 8f;
            float bed = 1f - MapNoise.Smooth(7f, 24f, Mathf.Abs(z - rz));
            h -= bed * 7f;
            // hills beyond the play area
            h += MapNoise.Smooth(10f, 140f, o) * 42f + MapNoise.Fbm(x, z, 0.004f, 4, 5f) * 18f * MapNoise.Smooth(0f, 120f, o);
            h = ApplyPads(x, z, h);
            // roads are graded flat across
            float rd = RoadDistance(x, z);
            if (rd < 9f) h = Mathf.Lerp(h, Plateau + MapNoise.Fbm(x, z, 0.006f, 3, 2f) * 2.2f + (hd < 120f ? 14f * Mathf.Exp(-(hd * hd) / (2f * 55f * 55f)) : 0f), (1f - MapNoise.Smooth(4f, 9f, rd)) * (1f - bed));
            return h;
        }

        public override void Paint(float x, float z, float h, float slope, float[] w)
        {
            float o = Outside(x, z);
            float rd = RoadDistance(x, z);
            float road = 1f - MapNoise.Smooth(3.4f, 4.6f, rd);
            float yard = Mathf.Abs(z) < 26f && Mathf.Abs(x) < 258f ? 1f : 0f;
            float rz = RiverZ + Mathf.Sin(x * 0.012f) * 8f;
            float bed = 1f - MapNoise.Smooth(6f, 14f, Mathf.Abs(z - rz));
            float n = MapNoise.Perlin(x, z, 0.04f, 7f);
            float town = o <= 0f ? 1f - MapNoise.Smooth(0f, 1f, Mathf.Max(0f, Mathf.Abs(x) - 245f) / 10f) : 0f;
            float rock = MapNoise.Smooth(26f, 40f, slope);
            w[(int)Surface.Rock] += rock;
            float free = 1f - rock;
            w[(int)Surface.Road] += road * free * (1f - yard);
            w[(int)Surface.Gravel] += (yard * 0.9f + bed) * free * (1f - road * (1f - yard));
            w[(int)Surface.Sand] += bed * 0.5f * free;
            float rest = free * (1f - road) * (1f - yard) * (1f - bed);
            w[(int)Surface.Dirt] += rest * (town * 0.35f + 0.15f) * (0.6f + 0.4f * n);
            w[(int)Surface.Grass] += rest * (1f - town * 0.25f);
            if (Mathf.Abs(x + 130f) < 22f && Mathf.Abs(z - 150f) < 22f) w[(int)Surface.Concrete] += 2f * free;   // market square
            if (Mathf.Abs(z - 40f) < 13f && Mathf.Abs(x) < 70f) w[(int)Surface.Concrete] += 2f * free;          // station square
        }

        public override float TreeDensity(float x, float z, float h, float slope)
        {
            float o = Outside(x, z);
            if (slope > 34f) return 0f;
            if (o > 8f) return 2.4f * MapNoise.Smooth(0.35f, 0.6f, MapNoise.Perlin(x, z, 0.012f, 3f));
            if (RoadDistance(x, z) < 7f || Mathf.Abs(z) < 34f) return 0f;
            float hd = Vector2.Distance(new Vector2(x, z), Hill);
            float hill = hd < 95f ? 0.9f : 0f;
            float river = Mathf.Abs(z - RiverZ) < 30f && Mathf.Abs(z - RiverZ) > 10f ? 0.8f : 0f;
            float park = Mathf.Abs(x + 130f) < 22f && Mathf.Abs(z - 150f) < 22f ? 0f : 0.05f;
            return Mathf.Max(hill, Mathf.Max(river, park));
        }

        public override float BushDensity(float x, float z, float h, float slope) => Outside(x, z) > 4f ? 0.5f : (RoadDistance(x, z) > 8f && Mathf.Abs(z) > 34f ? 0.08f : 0f);
        public override float GrassDensity(float x, float z, float h, float slope) => Outside(x, z) > 4f ? 0.6f : (RoadDistance(x, z) > 7f && Mathf.Abs(z) > 30f ? 0.25f * MapNoise.Smooth(0.4f, 0.75f, MapNoise.Perlin(x, z, 0.04f, 7f)) : 0f);

        // ------------------------------------------------------------------ build
        private readonly List<Vector3> taken = new List<Vector3>();   // x, radius, z of things placed (buildings avoid each other)

        private bool Free(float x, float z, float r)
        {
            foreach (var t in taken) if (new Vector2(x - t.x, z - t.z).magnitude < r + t.y) return false;
            return RoadDistance(x, z) > r * 0.7f + 4.5f;
        }

        public override void Build()
        {
            var town = B.Group("Railhead");
            BuildYard(town);
            BuildMarket(B.Group("Market (NW)", town));
            BuildWorkshops(B.Group("Workshops (NE)", town));
            BuildTerraces(B.Group("Terraces (SW)", town));
            BuildHill(B.Group("Water-tower hill (SE)", town));
            BuildRiver(B.Group("River", town));

            // street furniture along the main roads
            for (int i = -5; i <= 5; i++)
            {
                if (i == 0) continue;
                B.Lamp(B.OnGround(i * 42f, 67f), 180f, town);
                B.Lamp(B.OnGround(i * 42f, -67f), 0f, town);
            }

            View("overview", -230f, -240f, 110f, 0f, 0f);
            View("yard", -80f, -30f, 3f, 40f, 0f);
            View("station", 30f, 70f, 2f, 0f, 38f, 4f);
            View("market", -100f, 125f, 1.8f, -130f, 155f);
            View("workshops", 100f, 90f, 1.8f, 150f, 150f);
            View("terraces", -60f, -112f, 1.8f, -160f, -100f);
            View("water tower", 100f, -95f, 1.8f, Hill.x, Hill.y, 12f);
        }

        private void BuildYard(Transform town)
        {
            var yard = B.Group("Rail yard", town);
            var rails = new float[Tracks.Length];
            for (int i = 0; i < Tracks.Length; i++) rails[i] = B.Track(new Vector3(-258f, 0, Tracks[i]), new Vector3(258f, 0, Tracks[i]), yard);
            // strings of wagons with gaps every so often to cross the yard
            if (B.Kit != null && B.Kit.wagons.Length > 0)
            {
                float len = Mathf.Max(10f, B.PropSize(B.Kit.wagons[0]).size.z + 0.6f);
                for (int t = 0; t < Tracks.Length; t++)
                {
                    float x = -240f + B.Range(0f, 30f);
                    while (x < 240f)
                    {
                        int run = B.Range(2, 6);
                        for (int k = 0; k < run && x < 240f; k++, x += len)
                        {
                            if (Mathf.Abs(x) < 62f && t == 3) continue;   // the station track stays clear by the platform
                            B.RollingStock(B.Kit.wagons, new Vector3(x, rails[t], Tracks[t]), 90f, yard);
                        }
                        x += B.Range(18f, 45f);
                    }
                }
                if (B.Kit.locomotives.Length > 0)
                {
                    B.RollingStock(B.Kit.locomotives, new Vector3(-20f, rails[3], Tracks[3]), 90f, yard, 1);
                    B.RollingStock(B.Kit.locomotives, new Vector3(110f, rails[0], Tracks[0]), -90f, yard, 2);
                }
            }
            // station platform and building
            B.Platform(0f, 22.5f, 90f, 120f, 6.5f, 1.05f, "concrete", yard);
            for (int k = -2; k <= 2; k++)
            {
                Vector3 g = B.OnGround(k * 22f, 22.5f, 1.05f);
                B.Box(yard, g + Vector3.up * 1.6f, new Vector3(0.25f, 3.2f, 0.25f), "metal", null, true, "Canopy post");
                B.Box(yard, g + Vector3.up * 3.3f, new Vector3(18f, 0.2f, 5f), "roof", null, true, "Canopy");
            }
            B.Building(0f, 37f, 0f, new BuildingSpec { Width = 30f, Depth = 11f, Floors = 2, Wall = "plaster_ochre", Trim = "concrete_dark", Roof = "roof_red", Gable = true, SideDoor = true, LootPerFloor = 3 }, yard, "Station");
            taken.Add(new Vector3(0f, 18f, 37f));
            // container stacks south of the yard
            for (int i = 0; i < 9; i++)
            {
                float x = -200f + i * 50f + B.Range(-8f, 8f), z = -36f + B.Range(-3f, 3f);
                if (Mathf.Abs(x) < 15f) continue;
                B.Container(B.OnGround(x, z), 90f, yard);
                if (B.Chance(0.6f)) B.Container(B.OnGround(x, z), 90f, yard, -1, B.ContainerHeight);
                B.Container(B.OnGround(x + B.Range(-2f, 2f), z - 4f), 90f, yard);
                B.CrateStack(B.OnGround(x + 8f, z + 3f), 0f, yard);
                B.Loot(B.OnGround(x + 7f, z - 1f, 0.2f));
                taken.Add(new Vector3(x, 9f, z - 2f));
            }
            // signal gantries
            foreach (float gx in new[] { -150f, 150f })
            {
                for (int s = -1; s <= 1; s += 2) B.Box(yard, B.OnGround(gx, s * 18.5f, 3.5f), new Vector3(0.4f, 7f, 0.4f), "metal", null, true, "Gantry leg");
                B.Box(yard, B.OnGround(gx, 0f, 7f), new Vector3(0.5f, 0.5f, 37.4f), "metal", null, false, "Gantry");
            }
        }

        private void BuildMarket(Transform g)
        {
            // stalls round the square
            for (int i = 0; i < 10; i++)
            {
                float a = i * 36f * Mathf.Deg2Rad;
                Vector3 p = B.OnGround(-130f + Mathf.Sin(a) * 14f, 150f + Mathf.Cos(a) * 14f);
                float yaw = i * 36f;
                Quaternion q = Quaternion.Euler(0f, yaw, 0f);
                B.Box(g, p + Vector3.up * 0.45f, new Vector3(2.6f, 0.9f, 1.2f), q, "wood", null, true, "Stall");
                B.Box(g, p + Vector3.up * 2.4f + q * new Vector3(0f, 0f, -0.4f), new Vector3(3f, 0.08f, 2.2f), q * Quaternion.Euler(10f, 0f, 0f), i % 2 == 0 ? "plaster_red" : "plaster_blue", null, false, "Awning");
                B.Box(g, p + Vector3.up * 1.2f + q * new Vector3(1.3f, 0f, -1.2f), new Vector3(0.1f, 2.4f, 0.1f), "wood_dark", null, false, "Pole");
            }
            B.Loot(B.OnGround(-130f, 150f, 0.2f));
            taken.Add(new Vector3(-130f, 24f, 150f));
            string[] walls = { "plaster_white", "plaster_ochre", "plaster_red", "plaster_blue", "plaster_white" };
            Rows(g, -240f, -38f, 72f, 240f, 15f, 2, walls, false, 0.65f);
        }

        private void BuildWorkshops(Transform g)
        {
            var spots = new[] { new Vector2(70f, 90f), new Vector2(190f, 92f), new Vector2(75f, 190f), new Vector2(200f, 188f), new Vector2(160f, 140f), new Vector2(95f, 140f) };
            for (int i = 0; i < spots.Length; i++)
            {
                var s = spots[i];
                float yaw = (i % 2) * 90f;
                B.Hall(s.x, s.y, yaw, 18f, 28f, 8f, i % 3 == 0 ? "metal_rust" : "brick", "roof", 6f, 5f, 2, g, "Workshop");
                B.Loot(B.OnGround(s.x + 3f, s.y + 2f, 0.25f));
                B.Loot(B.OnGround(s.x - 4f, s.y - 6f, 0.25f));
                B.CrateStack(B.OnGround(s.x - 3f, s.y + 5f), 0f, g);
                taken.Add(new Vector3(s.x, 17f, s.y));
                // a yard of containers next to it
                Vector3 c = B.OnGround(s.x + (yaw == 0f ? 15f : 0f), s.y + (yaw == 0f ? 0f : 15f));
                if (Free(c.x, c.z, 7f))
                {
                    B.Container(c, yaw, g);
                    if (B.Chance(0.5f)) B.Container(c, yaw, g, -1, B.ContainerHeight);
                    taken.Add(new Vector3(c.x, 7f, c.z));
                }
            }
            Rows(g, 40f, 240f, 72f, 240f, 14f, 1, new[] { "concrete", "metal_green", "brick" }, false, 0.5f);
        }

        private void BuildTerraces(Transform g)
        {
            Rows(g, -240f, -38f, -205f, -72f, 9.5f, 2, new[] { "brick", "plaster_white", "plaster_ochre" }, true, 0.75f);
        }

        private void BuildHill(Transform g)
        {
            // the water tower on the crest
            Vector3 top = B.OnGround(Hill.x, Hill.y);
            for (int i = 0; i < 4; i++)
            {
                float lx = (i % 2 == 0 ? -3f : 3f), lz = (i < 2 ? -3f : 3f);
                B.Box(g, top + new Vector3(lx, 7f, lz), new Vector3(0.45f, 14f, 0.45f), "metal", null, true, "Leg");
            }
            B.Box(g, top + new Vector3(0f, 14.1f, 0f), new Vector3(8f, 0.3f, 8f), "metal", null, true, "Deck");
            B.Cylinder(top + new Vector3(0f, 17.5f, 0f), 4.6f, 6.5f, "metal_rust", 0f, false, g);
            taken.Add(new Vector3(Hill.x, 9f, Hill.y));
            Scatter(g, 40f, 240f, -205f, -72f, 18, new[] { "plaster_white", "wood", "plaster_ochre" });
        }

        private void BuildRiver(Transform g)
        {
            foreach (float bx in new[] { -130f, 0f, 130f })
            {
                float rz = RiverZ + Mathf.Sin(bx * 0.012f) * 8f;
                var b = B.Group("Bridge", g);
                b.position = new Vector3(bx, Plateau, rz);
                B.Box(b, new Vector3(0f, -0.25f, 0f), new Vector3(7f, 0.5f, 40f), "concrete", null, true, "Deck");
                B.Railing(b, new Vector3(-3.5f, 0f, -20f), new Vector3(-3.5f, 0f, 20f), 0f);
                B.Railing(b, new Vector3(3.5f, 0f, -20f), new Vector3(3.5f, 0f, 20f), 0f);
                for (int s = -1; s <= 1; s += 2) B.Box(b, new Vector3(0f, -4f, s * 6f), new Vector3(6f, 7.5f, 1.4f), "concrete_dark", null, true, "Pier");
            }
            for (int i = 0; i < 40; i++)
            {
                float x = B.Range(-250f, 250f);
                float rz = RiverZ + Mathf.Sin(x * 0.012f) * 8f + B.Range(-9f, 9f);
                B.Rock(B.OnGround(x, rz), B.Range(0.5f, 1.3f), B.Range(0f, 360f), g);
            }
        }

        /// <summary>Houses in rows along the district's streets (one row on each side of every street), with yards between.</summary>
        private void Rows(Transform g, float x0, float x1, float z0, float z1, float width, int floors, string[] walls, bool gable, float fill)
        {
            for (int i = 0; i < roadsA.Count; i++)
            {
                Vector2 a = roadsA[i], b = roadsB[i];
                Vector2 d = b - a;
                float len = d.magnitude;
                Vector2 dir = d / len;
                Vector2 n = new Vector2(-dir.y, dir.x);
                for (int side = -1; side <= 1; side += 2)
                {
                    float t = B.Range(4f, 12f);
                    while (t < len - 6f)
                    {
                        float depth = B.Range(7.5f, 9.5f), w = width * B.Range(0.85f, 1.15f);
                        Vector2 c = a + dir * (t + w * 0.5f) + n * side * (9f + depth * 0.5f);
                        t += w + B.Range(2.5f, 8f);
                        if (c.x < x0 || c.x > x1 || c.y < z0 || c.y > z1) continue;
                        if (!B.Chance(fill)) continue;
                        float r = Mathf.Max(w, depth) * 0.62f;
                        if (!Free(c.x, c.y, r)) continue;
                        // the front faces the street
                        Vector2 face = -n * side;
                        float yaw = Mathf.Atan2(-face.x, -face.y) * Mathf.Rad2Deg;
                        var spec = new BuildingSpec
                        {
                            Width = w, Depth = depth, Floors = floors > 1 && B.Chance(0.7f) ? floors : 1, Wall = B.Pick(walls),
                            Trim = "concrete_dark", Roof = gable ? "roof_red" : "roof", Gable = gable, SideDoor = B.Chance(0.4f),
                        };
                        B.Building(c.x, c.y, yaw, spec, g, "House");
                        taken.Add(new Vector3(c.x, r, c.y));
                    }
                }
            }
        }

        /// <summary>Loose houses on open ground (the hill).</summary>
        private void Scatter(Transform g, float x0, float x1, float z0, float z1, int count, string[] walls)
        {
            int placed = 0;
            for (int tries = 0; tries < count * 10 && placed < count; tries++)
            {
                float x = B.Range(x0, x1), z = B.Range(z0, z1);
                float w = B.Range(7f, 10f), d = B.Range(7.5f, 9f);
                float r = Mathf.Max(w, d) * 0.62f;
                if (!Free(x, z, r)) continue;
                B.Building(x, z, B.Range(0f, 360f), new BuildingSpec { Width = w, Depth = d, Floors = B.Chance(0.4f) ? 2 : 1, Wall = B.Pick(walls), Gable = B.Chance(0.6f), Roof = "roof_red" }, g, "House");
                taken.Add(new Vector3(x, r, z));
                placed++;
            }
        }
    }
}
