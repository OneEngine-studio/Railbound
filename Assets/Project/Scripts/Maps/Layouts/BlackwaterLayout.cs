using UnityEngine;

namespace Railbound.Maps.Layouts
{
    /// <summary>
    /// BLACKWATER (Search &amp; Destroy, 6 v 6, 150 x 96 m). Attackers spawn west, defenders east. Site A (north) is a fuel depot with
    /// tanks and pipe racks; site B (south) is inside a warehouse with two entrances and a catwalk. A rail cut below ground level runs
    /// through the middle with two bridges over it. Defenders reach either site about ten seconds before the attackers.
    /// </summary>
    public class BlackwaterLayout : LayoutBase
    {
        private const float Floor = 20f, CutDepth = 3.6f;
        private static readonly Vector2 SiteA = new Vector2(26f, 27f), SiteB = new Vector2(24f, -27f);
        public override float MaxHeight => 80f;

        public override void Plan()
        {
            Pad(0f, 0f, HalfX + 2f, HalfZ + 2f, Floor, 6f);
            Pad(0f, 0f, 50f, 4.5f, Floor - CutDepth, 7.5f);   // the rail cut (later pad wins)
        }

        public override float Height(float x, float z) => ApplyPads(x, z, ArenaGround(x, z, Floor, 24f, 31f));

        public override void Paint(float x, float z, float h, float slope, float[] w)
        {
            float inside = MapNoise.RectDistance(x, z, 0f, 0f, HalfX + 2f, HalfZ + 2f) <= 0f ? 1f : 1f - MapNoise.Smooth(0f, 6f, MapNoise.RectDistance(x, z, 0f, 0f, HalfX + 2f, HalfZ + 2f));
            float cut = 1f - MapNoise.Smooth(4f, 10f, Mathf.Abs(z)) * 1f;
            cut *= x > -58f && x < 58f ? 1f : 0f;
            float n = MapNoise.Perlin(x, z, 0.07f, 12f);
            float east = MapNoise.Smooth(-10f, 20f, x);
            w[(int)Surface.Gravel] += inside * cut;
            w[(int)Surface.Asphalt] += inside * (1f - cut) * east * (0.7f + 0.3f * n);
            w[(int)Surface.Dirt] += inside * (1f - cut) * (1f - east) * (0.6f + 0.4f * n);
            w[(int)Surface.Concrete] += inside * (1f - cut) * 0.25f * (1f - n);
            float outW = 1f - inside;
            float rock = MapNoise.Smooth(26f, 40f, slope);
            w[(int)Surface.Rock] += outW * rock + inside * MapNoise.Smooth(30f, 45f, slope);
            w[(int)Surface.Grass] += outW * (1f - rock);
        }

        public override float TreeDensity(float x, float z, float h, float slope) => Outside(x, z) > 12f && slope < 32f ? 1.4f : 0f;
        public override float BushDensity(float x, float z, float h, float slope) => Outside(x, z) > 4f ? 1f : 0f;
        public override float GrassDensity(float x, float z, float h, float slope) => Outside(x, z) > 2f ? 0.7f : 0f;

        public override void Build()
        {
            var site = B.Group("Blackwater");

            // ---------------- the rail cut, its wagons and the two bridges
            float rail = B.Track(new Vector3(-50f, 0, 0f), new Vector3(50f, 0, 0f), site);
            if (B.Kit != null)
            {
                B.RollingStock(B.Kit.wagons, new Vector3(-31f, rail, 0f), 90f, site, 1);
                B.RollingStock(B.Kit.wagons, new Vector3(6f, rail, 0f), 90f, site, 2);
            }
            Bridge(-14f, site);
            Bridge(36f, site);

            // ---------------- site A: the fuel depot (north)
            var depot = B.Group("Fuel depot", site);
            B.Cylinder(B.OnGround(17f, 36f, 3.5f), 3.4f, 7f, "metal", 0f, false, depot);
            B.Cylinder(B.OnGround(30f, 38f, 3.5f), 3.4f, 7f, "metal", 0f, false, depot);
            B.Cylinder(B.OnGround(40f, 25f, 3.5f), 3.4f, 7f, "metal_rust", 0f, false, depot);
            for (int k = 0; k < 4; k++)
                B.Box(depot, B.OnGround(14f + k * 8f, 31f, 1.7f), new Vector3(0.3f, 3.4f, 0.3f), "metal", null, true, "Rack post");
            B.Cylinder(B.OnGround(26f, 31f, 3.1f), 0.38f, 28f, "metal_rust", 90f, true, depot);
            B.Cylinder(B.OnGround(26f, 31.6f, 3.6f), 0.28f, 28f, "metal", 90f, true, depot);
            B.Building(15f, 21f, 180f, new BuildingSpec { Width = 6f, Depth = 5f, Floors = 1, Wall = "concrete", Tint = new Color(0.8f, 0.8f, 0.76f), Trim = "concrete_dark", Roof = "roof", SideDoor = true }, depot, "Control hut");
            B.BombSite(SiteA.x, SiteA.y, 5.5f, "A", 0f, depot);
            B.Loot(B.OnGround(33f, 30f, 0.2f));

            // ---------------- site B: the warehouse (south), with a catwalk along its south wall
            var wh = B.Hall(SiteB.x, SiteB.y, 90f, 18f, 26f, 8f, "metal_rust", "roof", 5f, 4f, 2, site, "Warehouse");
            float wy = B.Ground(SiteB.x, SiteB.y) + 0.1f;
            float cy = wy + 3.6f;
            B.Stairs(site, new Vector3(35.5f, wy, SiteB.y - 6.9f), -90f, 3.6f, 6.2f, 1.4f, "metal");
            B.Catwalk(new Vector3(29.3f, 0, SiteB.y - 6.9f), new Vector3(12.5f, 0, SiteB.y - 6.9f), cy, 1.8f, site);
            B.Container(B.OnGround(19f, SiteB.y + 5f), 90f, site, 3);
            B.CrateStack(B.OnGround(30f, SiteB.y + 5f), 0f, site);
            B.BombSite(SiteB.x - 1f, SiteB.y - 1f, 4.2f, "B", 90f, site);
            B.Loot(new Vector3(20f, cy + 0.2f, SiteB.y - 6.9f));

            // ---------------- attacker side (west): a yard of containers and a ruined wall
            for (int s = -1; s <= 1; s += 2)
            {
                B.Container(B.OnGround(-46f, s * 24f), 0f, site, s < 0 ? 0 : 2);
                B.Container(B.OnGround(-36f, s * 33f), 90f, site, 1);
                B.Container(B.OnGround(-36f, s * 33f), 90f, site, 3, B.ContainerHeight);
                Ruin(new Vector2(-22f, s * 26f), s, site);
                B.CrateStack(B.OnGround(-58f, s * 16f), 0f, site);
            }

            // ---------------- defender side (east): offices
            B.Building(60f, 22f, 90f, new BuildingSpec { Width = 10f, Depth = 8f, Floors = 2, Wall = "concrete", Tint = new Color(0.85f, 0.82f, 0.76f), Trim = "concrete_dark", Roof = "roof", SideDoor = true }, site, "Office north");
            B.Building(60f, -21f, 90f, new BuildingSpec { Width = 10f, Depth = 8f, Floors = 2, Wall = "concrete", Tint = new Color(0.78f, 0.8f, 0.84f), Trim = "concrete_dark", Roof = "roof", SideDoor = true }, site, "Office south");
            B.Barrier(B.OnGround(48f, 10f), 0f, site);
            B.Barrier(B.OnGround(48f, -10f), 0f, site);

            PerimeterWall(3f, "concrete", 0.4f, new Color(0.7f, 0.68f, 0.64f));
            for (int k = -2; k <= 2; k++) B.Lamp(B.OnGround(k * 24f, 8.5f), 180f, site, 6f);

            // ---------------- spawns
            TeamSpawns(1, new Vector3(-68f, 0f, 0f), 90f, 6, 3f);   // attackers
            TeamSpawns(2, new Vector3(67f, 0f, 0f), -90f, 6, 3f);   // defenders

            View("overview", -72f, -45f, 26f, 10f, 0f);
            View("site A", 10f, 18f, 1.7f, 28f, 32f);
            View("site B", 12f, -24f, 1.7f, 26f, -28f);
            View("rail cut", -45f, 2f, 1.7f, 10f, 0f, -1f);
            View("attackers", -66f, 6f, 1.7f, 0f, 20f);
        }

        /// <summary>A bridge over the rail cut at <paramref name="x"/>: a deck at ground level with railings.</summary>
        private void Bridge(float x, Transform parent)
        {
            float half = 11f;
            var g = B.Group("Bridge", parent);
            g.position = new Vector3(x, Floor, 0f);
            B.Box(g, new Vector3(0f, -0.2f, 0f), new Vector3(4.4f, 0.4f, half * 2f), "concrete", null, true, "Deck");
            B.Railing(g, new Vector3(-2.2f, 0f, -half), new Vector3(-2.2f, 0f, half), 0f);
            B.Railing(g, new Vector3(2.2f, 0f, -half), new Vector3(2.2f, 0f, half), 0f);
            for (int s = -1; s <= 1; s += 2)
                B.Box(g, new Vector3(0f, -CutDepth * 0.5f, s * 3.2f), new Vector3(4f, CutDepth, 0.8f), "concrete_dark", null, true, "Pier");
        }

        /// <summary>A ruined brick wall: broken segments of uneven height with gaps.</summary>
        private void Ruin(Vector2 at, int side, Transform parent)
        {
            for (int i = 0; i < 5; i++)
            {
                if (i == 2) continue;   // a gap to run through
                float z0 = at.y - 8f + i * 3.4f;
                float h = B.Range(1.1f, 2.8f);
                B.Segment(parent, new Vector3(at.x, 0, z0), new Vector3(at.x, 0, z0 + 2.8f), B.Ground(at.x, z0) - 0.4f, h + 0.4f, 0.45f, "brick");
            }
        }
    }
}
