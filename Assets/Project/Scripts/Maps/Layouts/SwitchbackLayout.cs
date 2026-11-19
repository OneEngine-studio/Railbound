using UnityEngine;

namespace Railbound.Maps.Layouts
{
    /// <summary>
    /// SWITCHBACK (Gun Game, 8 players, 60 x 60 m). Two tracks cross near the middle (east-west and north-south); a two-floor signal
    /// box overlooks the crossing; wagons, a tool shed, containers and low walls fill the four quarters. 12 spawns around the edge,
    /// so every weapon from pistol to sniper has a use within seconds of a spawn.
    /// </summary>
    public class SwitchbackLayout : LayoutBase
    {
        private const float Floor = 20f;
        private const float TrackZ = -6f, TrackX = 6f;
        public override float MaxHeight => 80f;

        private static readonly Vector2[] Spawns =
        {
            new Vector2(-26f, -26f), new Vector2(-4f, -27f), new Vector2(26f, -27f), new Vector2(27f, -11f),
            new Vector2(27f, 14f), new Vector2(26f, 27f), new Vector2(2f, 27f), new Vector2(-14f, 27f),
            new Vector2(-27f, 24f), new Vector2(-27f, 4f), new Vector2(-27f, -12f), new Vector2(-6f, -17f),
        };

        public override void Plan() { Pad(0f, 0f, HalfX + 2f, HalfZ + 2f, Floor, 6f); }

        public override float Height(float x, float z) => ApplyPads(x, z, ArenaGround(x, z, Floor, 20f, 21f));

        public override void Paint(float x, float z, float h, float slope, float[] w)
        {
            float inside = PadWeight(x, z);
            bool track = Mathf.Abs(z - TrackZ) < 3.2f || Mathf.Abs(x - TrackX) < 3.2f;
            float n = MapNoise.Perlin(x, z, 0.11f, 4f);
            if (track) w[(int)Surface.Gravel] += inside;
            else
            {
                w[(int)Surface.Dirt] += inside * (0.4f + 0.4f * n);
                w[(int)Surface.Gravel] += inside * 0.3f * (1f - n);
                w[(int)Surface.Grass] += inside * 0.25f * MapNoise.Smooth(0.55f, 0.8f, n);
            }
            float outW = 1f - inside;
            float rock = MapNoise.Smooth(26f, 40f, slope);
            w[(int)Surface.Rock] += outW * rock;
            w[(int)Surface.Grass] += outW * (1f - rock);
        }

        public override float TreeDensity(float x, float z, float h, float slope) => Outside(x, z) > 10f && slope < 32f ? 1.8f : 0f;
        public override float BushDensity(float x, float z, float h, float slope) => Outside(x, z) > 4f ? 1.2f : 0f;
        public override float GrassDensity(float x, float z, float h, float slope) => Outside(x, z) > 2f ? 0.8f : 0f;

        public override void Build()
        {
            var yard = B.Group("Switchback");

            // ---------------- the two tracks and their crossing
            float railA = B.Track(new Vector3(-HalfX, 0, TrackZ), new Vector3(HalfX, 0, TrackZ), yard);
            float railB = B.Track(new Vector3(TrackX, 0, -HalfZ), new Vector3(TrackX, 0, HalfZ), yard);
            if (B.Kit != null)
            {
                B.RollingStock(B.Kit.wagons, new Vector3(-19f, railA, TrackZ), 90f, yard, 2);
                B.RollingStock(B.Kit.wagons, new Vector3(21f, railA, TrackZ), 90f, yard, 3);
                B.RollingStock(B.Kit.wagons, new Vector3(TrackX, railB, 19f), 0f, yard, 1);
                B.RollingStock(B.Kit.wagons, new Vector3(TrackX, railB, -20f), 0f, yard, 0);
            }

            // ---------------- north-west: the signal box overlooking the crossing
            B.Building(-11f, 11f, -45f, new BuildingSpec
            {
                Width = 6.5f, Depth = 7.5f, Floors = 2, Wall = "brick", Trim = "wood_dark", Roof = "roof_red", Gable = true,
                WindowEvery = 1.9f, SideDoor = true, LootPerFloor = 1,
            }, yard, "Signal box");
            B.CrateStack(B.OnGround(-20f, 3f), 20f, yard);
            B.Sandbags(B.OnGround(-22f, 16f), B.OnGround(-16f, 20f), yard);

            // ---------------- north-east: containers and a lamp
            B.Container(B.OnGround(18f, 6f), 90f, yard, 2);
            B.Container(B.OnGround(19f, 13f), 90f, yard, 0);
            B.Container(B.OnGround(19f, 13f), 90f, yard, 1, B.ContainerHeight);
            B.CrateStack(B.OnGround(13f, 22f), 0f, yard);
            B.Loot(B.OnGround(16f, 9.5f, 0.2f));

            // ---------------- south-east: the tool shed
            B.Hall(19f, -18f, 0f, 8f, 10f, 4.6f, "metal_rust", "roof", 3f, 3f, 1, yard, "Tool shed");
            B.CrateStack(B.OnGround(19f, -16f), 0f, yard);
            B.Loot(B.OnGround(21f, -20f, 0.25f));

            // ---------------- south-west: low walls and sandbags
            B.Segment(yard, B.OnGround(-22f, -14f), B.OnGround(-12f, -14f), B.Ground(-17f, -14f), 1.2f, 0.35f, "concrete");
            B.Segment(yard, B.OnGround(-16f, -22f), B.OnGround(-16f, -28f), B.Ground(-16f, -25f), 1.2f, 0.35f, "concrete");
            B.Sandbags(B.OnGround(-8f, -24f), B.OnGround(-2f, -21f), yard);
            B.CrateStack(B.OnGround(-21f, -21f), 30f, yard);
            B.Loot(B.OnGround(-19f, -18f, 0.2f));

            // ---------------- the crossing itself: points levers and a relay cabinet (low cover)
            B.Box(yard, B.OnGround(0f, -11f, 0.6f), new Vector3(1.2f, 1.2f, 0.7f), "metal_green", null, true, "Relay cabinet");
            B.Box(yard, B.OnGround(11f, -1f, 0.6f), new Vector3(0.7f, 1.2f, 1.2f), "metal_green", null, true, "Relay cabinet");
            B.Barrier(B.OnGround(-2f, 2f), 45f, yard);
            B.Barrier(B.OnGround(13f, -13f), 45f, yard);

            PerimeterWall(2.6f, "concrete", 0.4f, new Color(0.8f, 0.78f, 0.74f));
            B.Lamp(B.OnGround(0f, 0f), 135f, yard, 7f);

            foreach (var p in Spawns) Spawn(p.x, p.y, Mathf.Atan2(-p.x, -p.y) * Mathf.Rad2Deg);

            View("overview", -28f, -28f, 18f, 4f, 2f);
            View("crossing", 0f, -22f, 1.7f, 6f, 0f);
            View("signal box", -6f, 6f, 4.8f, 20f, -14f);
            View("sheds", 27f, -2f, 1.7f, 16f, -20f);
        }
    }
}
