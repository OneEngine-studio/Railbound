using UnityEngine;

namespace Railbound.Maps.Layouts
{
    /// <summary>
    /// DEPOT 9 (Team Deathmatch, 6 v 6, 120 x 76 m). Mirrored west / east: team 1 spawns west, team 2 east.
    /// Centre lane: an engine shed with a parked locomotive and an inside gallery. North lane: container stacks with gaps.
    /// South lane: a platform beside a second track with a footbridge over it. Three cross-routes link the lanes.
    /// </summary>
    public class Depot9Layout : LayoutBase
    {
        private const float Floor = 20f;
        public override float MaxHeight => 80f;

        public override void Plan() { Pad(0f, 0f, HalfX + 2f, HalfZ + 2f, Floor, 6f); }

        public override float Height(float x, float z) => ApplyPads(x, z, ArenaGround(x, z, Floor, 22f, 3f));

        public override void Paint(float x, float z, float h, float slope, float[] w)
        {
            float inside = PadWeight(x, z);
            if (inside > 0.01f)
            {
                bool lane = Mathf.Abs(z) < 9f || Mathf.Abs(z + 28f) < 3f;   // tracks: gravel
                float tiles = MapNoise.Perlin(x, z, 0.08f, 5f);
                if (lane) w[(int)Surface.Gravel] += inside;
                else if (z > 9f) w[(int)Surface.Concrete] += inside * (0.6f + tiles * 0.4f);
                else w[(int)Surface.Asphalt] += inside;
                w[(int)Surface.Dirt] += inside * 0.15f * tiles;
            }
            NaturalPaint(slope, w, 0.35f);
            for (int i = 0; i < 8; i++) if (inside > 0.01f && i != (int)Surface.Gravel && i != (int)Surface.Concrete && i != (int)Surface.Asphalt && i != (int)Surface.Dirt) w[i] *= 1f - inside;
        }

        public override float TreeDensity(float x, float z, float h, float slope) => Outside(x, z) > 14f && slope < 30f ? 1.6f * MapNoise.Perlin(x, z, 0.03f, 1f) : 0f;
        public override float BushDensity(float x, float z, float h, float slope) => Outside(x, z) > 6f ? 1.2f : 0f;
        public override float GrassDensity(float x, float z, float h, float slope) => Outside(x, z) > 3f ? 0.7f : 0f;

        public override void Build()
        {
            var yard = B.Group("Depot 9");

            // ---------------- centre lane: track through the engine shed
            float rail = B.Track(new Vector3(-HalfX, 0, 0f), new Vector3(HalfX, 0, 0f), yard);
            var shed = B.Hall(0f, 0f, 90f, 18f, 44f, 9f, "brick", "roof", 7.5f, 6.5f, 2, yard, "Engine shed");
            if (B.Kit != null) B.RollingStock(B.Kit.locomotives, new Vector3(-3f, rail, 0f), 90f, yard, 0);
            // gallery along the north wall inside the shed, with stairs at both ends
            float gy = B.Ground(0f, 0f) + 4.2f;
            B.Catwalk(new Vector3(-14f, 0, 6.6f), new Vector3(14f, 0, 6.6f), gy, 2.2f, yard);
            B.Stairs(yard, B.OnGround(-21f, 6.6f), 90f, 4.2f, 7f, 1.6f, "metal");
            B.Stairs(yard, B.OnGround(21f, 6.6f), -90f, 4.2f, 7f, 1.6f, "metal");
            // workbenches and parts inside
            for (int s = -1; s <= 1; s += 2)
            {
                B.Box(yard, B.OnGround(s * 9f, -5.5f, 0.45f), new Vector3(3f, 0.9f, 1.2f), "metal_green", null, true, "Bench");
                B.CrateStack(B.OnGround(s * 16f, -6f), 0f, yard);
                B.Loot(B.OnGround(s * 6f, -6f, 0.2f));
                B.Loot(B.OnGround(s * 10f, 6.6f, 4.4f));
            }

            // wagons on the centre track outside the shed (mid cover), mirrored
            if (B.Kit != null)
            {
                B.RollingStock(B.Kit.wagons, new Vector3(-33f, rail, 0f), 90f, yard, 0);
                B.RollingStock(B.Kit.wagons, new Vector3(33f, rail, 0f), -90f, yard, 0);
            }

            // ---------------- north lane: containers (mirrored), with gaps
            var north = B.Group("North lane", yard);
            void ContainerPair(float x, float z, float yaw, bool stack, int color)
            {
                B.Container(B.OnGround(x, z), yaw, north, color);
                if (stack) B.Container(B.OnGround(x, z), yaw, north, color + 1, B.ContainerHeight);
            }
            for (int s = -1; s <= 1; s += 2)
            {
                ContainerPair(s * 14f, 22f, 90f, true, 0);
                ContainerPair(s * 27f, 30f, 0f, false, 2);
                ContainerPair(s * 27f, 15f, 90f, false, 1);
                ContainerPair(s * 42f, 24f, 0f, true, 3);
                B.CrateStack(B.OnGround(s * 6f, 15f), 0f, north);
                B.CrateStack(B.OnGround(s * 36f, 33f), 45f, north);
                B.Loot(B.OnGround(s * 20f, 26f, 0.2f));
            }
            ContainerPair(0f, 31f, 90f, false, 2);   // the middle chokepoint

            // office blocks in the north corners (two floors, a view down the lane)
            for (int s = -1; s <= 1; s += 2)
                B.Building(s * 51f, 30f, s < 0 ? -90f : 90f, new BuildingSpec { Width = 9f, Depth = 7.5f, Floors = 2, Wall = "concrete", Tint = new Color(0.82f, 0.8f, 0.74f), Trim = "concrete_dark", Roof = "roof", SideDoor = true }, yard, "Office");

            // ---------------- south lane: platform, second track and a footbridge
            var south = B.Group("South lane", yard);
            float rail2 = B.Track(new Vector3(-HalfX, 0, -28f), new Vector3(HalfX, 0, -28f), south);
            B.Platform(0f, -21.5f, 90f, 44f, 6f, 1.1f, "concrete", south);
            for (int s = -1; s <= 1; s += 2)
            {
                if (B.Kit != null) B.RollingStock(B.Kit.wagons, new Vector3(s * 18f, rail2, -28f), 90f, south, 1);
                B.Barrier(B.OnGround(s * 30f, -17f), 90f, south);
                B.Barrier(B.OnGround(s * 40f, -24f), 0f, south);
                B.Building(s * 49f, -27f, s < 0 ? -90f : 90f, new BuildingSpec { Width = 7f, Depth = 7f, Floors = 1, Wall = "metal_green", Trim = "concrete_dark", Roof = "roof", SideDoor = true }, south, "Pump shed");
                // platform shelters
                for (int k = 0; k < 2; k++)
                {
                    float px = s * (6f + k * 10f);
                    Vector3 g = B.OnGround(px, -21.5f, 1.1f);
                    B.Box(south, g + Vector3.up * 1.4f, new Vector3(0.2f, 2.8f, 0.2f), "metal", null, true, "Shelter post");
                    B.Box(south, g + Vector3.up * 2.9f, new Vector3(4f, 0.15f, 4.5f), "roof", null, true, "Shelter roof");
                }
            }
            // footbridge over the platform and the south track at x = 0: a lookout reached by stairs from the lane
            float fy = B.Ground(0f, -28f) + 5.6f;
            B.Catwalk(new Vector3(0f, 0, -34f), new Vector3(0f, 0, -19f), fy, 2.4f, south);
            B.Stairs(south, B.OnGround(0f, -10f), 180f, fy - B.Ground(0f, -10f), 9f, 2f, "metal");
            B.Loot(new Vector3(0f, fy + 0.2f, -30f));

            // ---------------- cross-routes (the gaps between lanes stay open) and edge
            PerimeterWall(3.2f, "concrete", 0.4f, new Color(0.78f, 0.76f, 0.72f));
            for (int s = -1; s <= 1; s += 2)
                for (int k = 0; k < 2; k++) B.Lamp(B.OnGround(s * (12f + k * 18f), 10.5f), 180f, yard);

            // ---------------- spawns: west team 1, east team 2, behind cover, out of each other's sight
            TeamSpawns(1, new Vector3(-55f, 0f, -2f), 90f, 6, 3.2f);
            TeamSpawns(2, new Vector3(55f, 0f, 2f), -90f, 6, 3.2f);
            for (int s = -1; s <= 1; s += 2)
            {
                B.Container(B.OnGround(s * 46f, -12f), 0f, yard, 1);
                B.Container(B.OnGround(s * 46f, 12f), 0f, yard, 0);
            }

            View("overview", -70f, -50f, 28f, 0f, 0f);
            View("shed", -18f, -4f, 1.7f, 10f, 2f);
            View("north lane", -50f, 20f, 1.7f, 10f, 25f);
            View("south lane", 50f, -18f, 1.7f, -10f, -24f);
            View("spawn west", -56f, -2f, 1.7f, 0f, 0f);
        }
    }
}
