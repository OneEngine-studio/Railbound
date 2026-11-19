using UnityEngine;

namespace Railbound.Maps.Layouts
{
    /// <summary>
    /// FOUNDRY (Free-for-all, 8 players, 76 x 76 m). A furnace hall with a gallery in the middle, four entrances; around it a ring of
    /// yards and small rooms joined by doors, pipe racks and cover, inside a tall brick wall. 16 spawns spread around the ring.
    /// </summary>
    public class FoundryLayout : LayoutBase
    {
        private const float Floor = 20f;
        public override float MaxHeight => 80f;

        private static readonly Vector2[] Rooms =
        {
            new Vector2(-26f, -26f), new Vector2(26f, -26f), new Vector2(26f, 26f), new Vector2(-26f, 26f),
            new Vector2(0f, -28.5f), new Vector2(28.5f, 0f), new Vector2(0f, 28.5f), new Vector2(-28.5f, 0f),
        };

        public override void Plan() { Pad(0f, 0f, HalfX + 2f, HalfZ + 2f, Floor, 6f); }

        public override float Height(float x, float z) => ApplyPads(x, z, ArenaGround(x, z, Floor, 26f, 11f));

        public override void Paint(float x, float z, float h, float slope, float[] w)
        {
            float inside = PadWeight(x, z);
            float n = MapNoise.Perlin(x, z, 0.09f, 2f);
            w[(int)Surface.Concrete] += inside * (0.55f + 0.45f * n);
            w[(int)Surface.Gravel] += inside * 0.35f * (1f - n);
            w[(int)Surface.Dirt] += inside * 0.15f;
            float outW = 1f - inside;
            float rock = MapNoise.Smooth(26f, 40f, slope);
            w[(int)Surface.Rock] += outW * rock;
            w[(int)Surface.Dirt] += outW * (1f - rock) * 0.5f;
            w[(int)Surface.Grass] += outW * (1f - rock) * 0.5f;
        }

        public override float TreeDensity(float x, float z, float h, float slope) => Outside(x, z) > 12f && slope < 30f ? 0.9f : 0f;
        public override float BushDensity(float x, float z, float h, float slope) => Outside(x, z) > 5f ? 0.8f : 0f;
        public override float GrassDensity(float x, float z, float h, float slope) => Outside(x, z) > 3f ? 0.5f : 0f;

        public override void Build()
        {
            var site = B.Group("Foundry");

            // ---------------- the furnace hall and its gallery
            B.Hall(0f, 0f, 0f, 22f, 22f, 10f, "brick", "roof", 4f, 3.6f, 1, site, "Furnace hall");
            float fy = B.Ground(0f, 0f) + 0.1f;
            B.Cylinder(new Vector3(0f, fy + 3.5f, 1.5f), 3f, 7f, "metal_rust", 0f, false, site);
            B.Cylinder(new Vector3(0f, fy + 9.2f, 1.5f), 1.1f, 4.6f, "metal", 0f, false, site, false);   // flue into the roof
            B.Box(site, new Vector3(0f, fy + 0.6f, -2.4f), new Vector3(3.6f, 1.2f, 1.4f), "concrete_dark", null, true, "Pour channel");
            float gy = fy + 4.2f;
            for (int s = -1; s <= 1; s += 2)
            {
                B.Catwalk(new Vector3(s * 8.6f, 0, -2.2f), new Vector3(s * 8.6f, 0, 8.4f), gy, 2f, site);
                B.Stairs(site, new Vector3(s * 8.6f, fy, -9.6f), 0f, 4.2f, 7.2f, 1.5f, "metal");
                B.Loot(new Vector3(s * 8.6f, gy + 0.2f, 6f));
                B.Loot(B.OnGround(s * 5f, -7f, 0.25f));
                B.CrateStack(B.OnGround(s * 6.5f, -4f), 0f, site);
            }
            // the cross gallery along the north wall joins both sides (open on its south edge, where the side galleries meet it)
            B.Catwalk(new Vector3(-9.6f, 0, 9.4f), new Vector3(9.6f, 0, 9.4f), gy, 2f, site, true, false);

            // ---------------- the ring of rooms (doors on three sides: no dead ends)
            foreach (var r in Rooms)
            {
                float yaw = Mathf.Atan2(-r.x, -r.y) * Mathf.Rad2Deg;   // front faces the hall
                B.Building(r.x, r.y, yaw + 180f, new BuildingSpec
                {
                    Width = 6f, Depth = 6f, Floors = 1, Wall = "brick", Trim = "concrete_dark", Roof = "roof", SideDoor = true, BackDoor = true,
                    WindowEvery = 2.6f, LootPerFloor = 1,
                }, site, "Room");
            }

            // pipe racks between the rooms: cover you can shoot under
            for (int q = 0; q < 4; q++)
            {
                float a = q * 90f + 45f;
                Quaternion rot = Quaternion.Euler(0f, a, 0f);
                Vector3 c = rot * new Vector3(0f, 0f, 19f);
                Vector3 along = rot * Vector3.right;
                for (int k = -1; k <= 1; k += 2)
                {
                    Vector3 post = c + along * (k * 4.5f);
                    B.Box(site, B.OnGround(post.x, post.z, 1.6f), new Vector3(0.3f, 3.2f, 0.3f), "metal", null, true, "Rack post");
                }
                Vector3 g = B.OnGround(c.x, c.z);
                B.Cylinder(g + Vector3.up * 2.6f, 0.4f, 10f, "metal_rust", a + 90f, true, site);
                B.Cylinder(g + Vector3.up * 3.3f + rot * Vector3.forward * 0.5f, 0.3f, 10f, "metal", a + 90f, true, site);
            }

            // 16 spawns around the ring, kept clear of the rooms (placed first: cover keeps away from them)
            var spawns = new System.Collections.Generic.List<Vector2>();
            for (int i = 0; i < 16; i++)
            {
                float a = (i * 22.5f + 11.25f) * Mathf.Deg2Rad;
                Vector2 p = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * 31f;
                foreach (var r in Rooms) if (Vector2.Distance(p, r) < 6.5f) { p = p.normalized * 19.5f; break; }
                spawns.Add(p);
                Spawn(p.x, p.y, Mathf.Atan2(-p.x, -p.y) * Mathf.Rad2Deg);
            }

            // cover in the yards
            var keep = new System.Collections.Generic.List<Vector3> { new Vector3(0f, 15f, 0f) };
            foreach (var r in Rooms) keep.Add(new Vector3(r.x, 6.5f, r.y));
            foreach (var sp in spawns) keep.Add(new Vector3(sp.x, 2.8f, sp.y));
            for (int q = 0; q < 4; q++) { Vector3 c = Quaternion.Euler(0f, q * 90f + 45f, 0f) * new Vector3(0f, 0f, 19f); keep.Add(new Vector3(c.x, 6f, c.z)); }
            ScatterCover(new Rect(-35f, -35f, 70f, 70f), 22, site, keep.ToArray());

            // ---------------- the wall, corner towers and the chimney
            PerimeterWall(5f, "brick", 0.4f);
            for (int c = 0; c < 4; c++)
            {
                float cx = (c % 2 == 0 ? -1 : 1) * (HalfX - 1.8f), cz = (c < 2 ? -1 : 1) * (HalfZ - 1.8f);
                B.Box(site, B.OnGround(cx, cz, 4f), new Vector3(3.2f, 8f, 3.2f), "brick", null, true, "Tower");
            }
            B.Cylinder(B.OnGround(-31f, 32f, 16f), 2.2f, 32f, "brick", 0f, false, site);
            B.Lamp(B.OnGround(-14f, -14f), 45f, site, 5f);
            B.Lamp(B.OnGround(14f, -14f), -45f, site, 5f);
            B.Lamp(B.OnGround(14f, 14f), -135f, site, 5f);
            B.Lamp(B.OnGround(-14f, 14f), 135f, site, 5f);

            View("overview", -33f, -33f, 28f, 4f, 4f);
            View("hall", -9f, -9f, 1.7f, 4f, 6f);
            View("gallery", 8.6f, 8f, 5.9f, -6f, -6f);
            View("ring", 33f, -20f, 1.7f, -33f, -20f);
        }
    }
}
