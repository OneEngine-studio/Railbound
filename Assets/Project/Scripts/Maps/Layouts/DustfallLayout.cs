using System.Collections.Generic;
using UnityEngine;

namespace Railbound.Maps.Layouts
{
    /// <summary>
    /// DUSTFALL (Extraction, 20 players, 1000 x 1000 m). Dry canyon and mesa country. Four loot areas: a copper mine (north) with its
    /// pit and buildings, a ghost town (west) along one main street, a wrecked train (east) derailed in a canyon, and a stepped
    /// quarry (south). Three extraction pads with beacon masts stand at the edges: squads have to cross open ground to get out.
    /// </summary>
    public class DustfallLayout : LayoutBase
    {
        public override float MaxHeight => 200f;
        private const float Base = 30f;

        private static readonly Vector2 Mine = new Vector2(0f, 360f), Town = new Vector2(-360f, 0f), Wreck = new Vector2(360f, 30f), Quarry = new Vector2(0f, -350f);
        private static readonly Vector2[] Pads = { new Vector2(-430f, 410f), new Vector2(430f, 420f), new Vector2(40f, -462f) };

        public override void Plan()
        {
            Pad(Mine.x, Mine.y, 75f, 55f, Base + 4f, 30f);
            Pad(Town.x, Town.y, 80f, 30f, Base + 1f, 30f);
            Pad(Wreck.x, Wreck.y, 26f, 110f, Base - 4f, 34f);   // the canyon floor where the train left the rails
            Pad(Quarry.x, Quarry.y, 62f, 62f, Base + 2f, 25f);
            foreach (var p in Pads) RoundPad(p.x, p.y, 12f, Base + 2f, 22f);
        }

        private float Raw(float x, float z)
        {
            float o = Outside(x, z);
            float m = MapNoise.Perlin(x, z, 0.0042f, 21f) * 0.8f + MapNoise.Perlin(x, z, 0.013f, 22f) * 0.2f;
            float h = Base + MapNoise.Fbm(x, z, 0.006f, 3, 23f) * 5f;
            // mesas: two terraces with steep sides
            h += MapNoise.Smooth(0.58f, 0.63f, m) * 34f + MapNoise.Smooth(0.7f, 0.74f, m) * 24f;
            // canyons
            float c = MapNoise.Ridged(x, z, 0.0026f, 2, 24f);
            h -= MapNoise.Smooth(0.72f, 0.92f, c) * 16f;
            // the rim beyond the play area
            h += MapNoise.Smooth(0f, 200f, o) * 60f + MapNoise.Fbm(x, z, 0.003f, 4, 25f) * 20f * MapNoise.Smooth(0f, 160f, o);
            return h;
        }

        public override float Height(float x, float z)
        {
            float h = ApplyPads(x, z, Raw(x, z));
            // the quarry pit: three steps down
            float qd = MapNoise.RectDistance(x, z, Quarry.x, Quarry.y, 0f, 0f);
            float qx = Mathf.Max(Mathf.Abs(x - Quarry.x), Mathf.Abs(z - Quarry.y));
            if (qx < 40f) h -= 5f * (qx < 40f ? 1f : 0f) + 5f * (qx < 28f ? 1f : 0f) + 5f * (qx < 16f ? 1f : 0f);
            // the mine pit: a round hole, terraced
            float md = Vector2.Distance(new Vector2(x, z), new Vector2(Mine.x + 30f, Mine.y + 25f));
            if (md < 26f) h -= 4f + MapNoise.Smooth(0f, 1f, (26f - md) / 26f) * 10f;
            return h;
        }

        public override void Paint(float x, float z, float h, float slope, float[] w)
        {
            float rock = MapNoise.Smooth(26f, 38f, slope);
            float n = MapNoise.Perlin(x, z, 0.02f, 26f);
            float low = 1f - MapNoise.Smooth(Base - 8f, Base - 2f, h);
            float built = PadWeight(x, z);
            w[(int)Surface.Rock] += rock;
            float free = 1f - rock;
            w[(int)Surface.Gravel] += free * (low * 0.6f + built * 0.3f);
            w[(int)Surface.Dirt] += free * (0.25f + 0.35f * n + built * 0.4f);
            w[(int)Surface.Sand] += free * (1f - low * 0.6f) * (0.9f - 0.3f * n);
            // the town's main street
            if (Mathf.Abs(z - Town.y) < 5f && Mathf.Abs(x - Town.x) < 70f) w[(int)Surface.Dirt] += 3f * free;
        }

        public override float BushDensity(float x, float z, float h, float slope) => slope < 20f && PadWeight(x, z) < 0.2f ? 0.18f * MapNoise.Perlin(x, z, 0.03f, 27f) : 0f;
        public override float TreeDensity(float x, float z, float h, float slope) => 0f;
        public override float GrassDensity(float x, float z, float h, float slope) => 0f;

        public override void Build()
        {
            var land = B.Group("Dustfall");
            BuildMine(B.Group("Copper mine (N)", land));
            BuildTown(B.Group("Ghost town (W)", land));
            BuildWreck(B.Group("Wrecked train (E)", land));
            BuildQuarry(B.Group("Quarry (S)", land));
            for (int i = 0; i < Pads.Length; i++) B.ExtractionPad(Pads[i].x, Pads[i].y, (i + 1).ToString());
            B.Anchor(MapAnchor.AnchorKind.LootArea, B.OnGround(Mine.x, Mine.y), 0f, 0, "Copper mine", 70f);
            B.Anchor(MapAnchor.AnchorKind.LootArea, B.OnGround(Town.x, Town.y), 0f, 0, "Ghost town", 70f);
            B.Anchor(MapAnchor.AnchorKind.LootArea, B.OnGround(Wreck.x, Wreck.y), 0f, 0, "Wrecked train", 80f);
            B.Anchor(MapAnchor.AnchorKind.LootArea, B.OnGround(Quarry.x, Quarry.y), 0f, 0, "Quarry", 60f);

            // shacks and rocks in the open country
            var open = B.Group("Open country", land);
            var keep = new List<Vector2> { Mine, Town, Wreck, Quarry };
            keep.AddRange(Pads);
            int shacks = 0;
            for (int tries = 0; tries < 400 && shacks < 14; tries++)
            {
                float x = B.Range(-450f, 450f), z = B.Range(-450f, 450f);
                bool near = false;
                foreach (var k in keep) if (Vector2.Distance(new Vector2(x, z), k) < 110f) { near = true; break; }
                if (near) continue;
                // only on fairly flat ground
                float h0 = B.Ground(x - 5f, z - 5f), h1 = B.Ground(x + 5f, z + 5f), h2 = B.Ground(x - 5f, z + 5f), h3 = B.Ground(x + 5f, z - 5f);
                if (Mathf.Max(Mathf.Max(h0, h1), Mathf.Max(h2, h3)) - Mathf.Min(Mathf.Min(h0, h1), Mathf.Min(h2, h3)) > 2.2f) continue;
                B.Building(x, z, B.Range(0f, 360f), new BuildingSpec { Width = B.Range(5f, 7f), Depth = B.Range(5f, 6.5f), Floors = 1, Wall = "wood", Trim = "wood_dark", Floor = "wood", Roof = "metal_rust", Gable = B.Chance(0.5f) }, open, "Shack");
                keep.Add(new Vector2(x, z));
                shacks++;
            }
            for (int i = 0; i < 160; i++)
            {
                float x = B.Range(-470f, 470f), z = B.Range(-470f, 470f);
                bool near = false;
                foreach (var k in keep) if (Vector2.Distance(new Vector2(x, z), k) < 40f) { near = true; break; }
                if (near) continue;
                B.Rock(B.OnGround(x, z), B.Range(0.7f, 2f), B.Range(0f, 360f), open);
            }

            View("overview", -330f, -330f, 170f, 0f, 0f, 30f);
            View("mine", -40f, 300f, 2f, Mine.x + 30f, Mine.y + 25f, -4f);
            View("ghost town", Town.x + 70f, Town.y + 3f, 1.8f, Town.x - 20f, Town.y);
            View("wreck", Wreck.x - 30f, Wreck.y - 70f, 3f, Wreck.x, Wreck.y);
            View("quarry", Quarry.x - 55f, Quarry.y + 55f, 6f, Quarry.x, Quarry.y, -10f);
            View("extraction", Pads[0].x + 30f, Pads[0].y - 30f, 2f, Pads[0].x, Pads[0].y, 6f);
        }

        private void BuildMine(Transform g)
        {
            B.Hall(Mine.x - 35f, Mine.y - 10f, 0f, 16f, 26f, 9f, "metal_rust", "roof", 6f, 5f, 2, g, "Crusher hall");
            B.Hall(Mine.x - 5f, Mine.y - 30f, 90f, 14f, 20f, 7f, "brick", "roof", 5f, 4f, 1, g, "Workshop");
            B.Building(Mine.x - 45f, Mine.y + 30f, 180f, new BuildingSpec { Width = 14f, Depth = 9f, Floors = 2, Wall = "brick", Trim = "concrete_dark", Roof = "roof", SideDoor = true, LootPerFloor = 2 }, g, "Mine office");
            // the headframe over the shaft at the pit's edge
            Vector3 hf = B.OnGround(Mine.x + 4f, Mine.y + 25f);
            for (int i = 0; i < 4; i++)
            {
                float lx = i % 2 == 0 ? -2.5f : 2.5f, lz = i < 2 ? -2.5f : 2.5f;
                B.Box(g, hf + new Vector3(lx, 9f, lz), new Vector3(0.4f, 18f, 0.4f), "metal_rust", null, true, "Headframe leg");
            }
            B.Box(g, hf + new Vector3(0f, 18.2f, 0f), new Vector3(6f, 0.6f, 6f), "metal_rust", null, false, "Headframe top");
            B.Cylinder(hf + new Vector3(0f, 19.4f, 0f), 2f, 0.5f, "metal", 0f, true, g, false);
            // conveyor from the pit to the crusher hall
            B.Catwalk(B.OnGround(Mine.x + 2f, Mine.y + 18f), B.OnGround(Mine.x - 26f, Mine.y - 2f), B.Ground(Mine.x - 12f, Mine.y + 8f) + 6f, 1.6f, g);
            for (int i = 0; i < 6; i++)
            {
                Vector3 c = B.OnGround(Mine.x - 20f + i * 9f, Mine.y - 45f);
                B.Container(c, 90f, g);
                if (i % 2 == 0) B.Container(c, 90f, g, -1, B.ContainerHeight);
            }
            for (int i = 0; i < 6; i++) B.Loot(B.OnGround(Mine.x - 35f + B.Range(-5f, 5f), Mine.y - 10f + B.Range(-10f, 10f), 0.25f));
            ScatterCover(new Rect(Mine.x - 70f, Mine.y - 50f, 140f, 100f), 14, g,
                new Vector3(Mine.x - 35f, 18f, Mine.y - 10f), new Vector3(Mine.x - 5f, 14f, Mine.y - 30f), new Vector3(Mine.x - 45f, 11f, Mine.y + 30f), new Vector3(Mine.x + 30f, 30f, Mine.y + 25f), new Vector3(Mine.x, 8f, Mine.y - 45f));
        }

        private void BuildTown(Transform g)
        {
            float street = Town.y;
            for (int i = 0; i < 6; i++)
            {
                float x = Town.x - 55f + i * 21f;
                bool saloon = i == 2;
                B.Building(x, street + 13f, 0f, new BuildingSpec
                {
                    Width = saloon ? 16f : B.Range(9f, 12f), Depth = saloon ? 12f : B.Range(8f, 10f), Floors = saloon || B.Chance(0.4f) ? 2 : 1,
                    Wall = "wood", Tint = new Color(B.Range(0.75f, 1f), B.Range(0.68f, 0.9f), B.Range(0.6f, 0.8f)), Trim = "wood_dark", Floor = "wood",
                    Roof = "roof", Gable = !saloon && B.Chance(0.6f), SideDoor = true, LootPerFloor = saloon ? 3 : 1,
                }, g, saloon ? "Saloon" : "Storefront");
                B.Building(x + 4f, street - 13f, 180f, new BuildingSpec
                {
                    Width = B.Range(9f, 12f), Depth = B.Range(8f, 10f), Floors = B.Chance(0.35f) ? 2 : 1,
                    Wall = "wood", Tint = new Color(B.Range(0.7f, 1f), B.Range(0.65f, 0.88f), B.Range(0.55f, 0.78f)), Trim = "wood_dark", Floor = "wood",
                    Roof = "roof", Gable = B.Chance(0.6f), SideDoor = true,
                }, g, "Storefront");
                // boardwalk posts and barrels along the street
                B.Box(g, B.OnGround(x + 6f, street + 6.5f, 0.45f), new Vector3(0.7f, 0.9f, 0.7f), "wood_dark", null, true, "Barrel");
                if (B.Chance(0.6f)) B.CrateStack(B.OnGround(x - 3f, street - 6.5f), B.Range(0f, 40f), g);
            }
            // a water tower and a corral at the end of the street
            Vector3 wt = B.OnGround(Town.x + 78f, Town.y + 18f);
            for (int i = 0; i < 4; i++)
                B.Box(g, wt + new Vector3(i % 2 == 0 ? -2f : 2f, 5f, i < 2 ? -2f : 2f), new Vector3(0.35f, 10f, 0.35f), "wood_dark", null, true, "Leg");
            B.Cylinder(wt + new Vector3(0f, 12.5f, 0f), 3f, 5f, "wood", 0f, false, g);
            for (int i = 0; i < 4; i++)
            {
                Quaternion q = Quaternion.Euler(0f, i * 90f, 0f);
                Vector3 a = new Vector3(Town.x - 80f, 0f, Town.y - 20f) + q * new Vector3(-8f, 0f, 8f), b = new Vector3(Town.x - 80f, 0f, Town.y - 20f) + q * new Vector3(8f, 0f, 8f);
                B.Segment(g, a, b, B.Ground(a.x, a.z), 1.1f, 0.15f, "wood_dark");
            }
        }

        private void BuildWreck(Transform g)
        {
            // the line runs north-south down the canyon; the train left it on a curve
            float rail = B.Track(new Vector3(Wreck.x + 8f, 0, Wreck.y - 120f), new Vector3(Wreck.x + 8f, 0, Wreck.y + 130f), g);
            if (B.Kit == null) return;
            Vector3 c = new Vector3(Wreck.x, 0f, Wreck.y);
            var loco = B.RollingStock(B.Kit.locomotives, B.OnGround(c.x - 6f, c.z + 40f), 20f, g, 0);
            if (loco != null) { loco.transform.rotation = Quaternion.Euler(0f, 20f, 78f); loco.transform.position += Vector3.up * 0.6f; }
            float[] yaws = { 35f, -25f, 70f, 10f, -55f, 15f };
            for (int i = 0; i < yaws.Length; i++)
            {
                Vector3 p = B.OnGround(c.x + B.Range(-14f, 10f), c.z + 22f - i * 15f);
                var w = B.RollingStock(B.Kit.wagons, p, yaws[i], g);
                if (w != null && i % 3 == 1) w.transform.rotation = Quaternion.Euler(0f, yaws[i], -12f);
                B.Loot(p + Quaternion.Euler(0f, yaws[i], 0f) * new Vector3(2.8f, 0.2f, 0f));
            }
            // spilled containers
            for (int i = 0; i < 5; i++) B.Container(B.OnGround(c.x + B.Range(-18f, 14f), c.z + B.Range(-70f, 40f)), B.Range(0f, 360f), g);
            B.Building(c.x + 22f, c.z - 70f, -90f, new BuildingSpec { Width = 8f, Depth = 6f, Floors = 1, Wall = "brick", Roof = "roof", SideDoor = true, LootPerFloor = 2 }, g, "Signal hut");
        }

        private void BuildQuarry(Transform g)
        {
            // cut blocks on the steps, sheds and a crane on the rim
            for (int i = 0; i < 26; i++)
            {
                float a = B.Range(0f, Mathf.PI * 2f), r = B.Range(18f, 46f);
                Vector3 p = B.OnGround(Quarry.x + Mathf.Cos(a) * r, Quarry.y + Mathf.Sin(a) * r);
                float s = B.Range(1.2f, 2.6f);
                B.Box(g, p + Vector3.up * (s * 0.45f), new Vector3(s * 1.5f, s, s), Quaternion.Euler(0f, B.Range(0f, 90f), 0f), "rock", null, true, "Block");
            }
            B.Hall(Quarry.x + 52f, Quarry.y + 10f, 0f, 12f, 18f, 6.5f, "metal_rust", "roof", 4f, 4f, 1, g, "Quarry shed");
            B.Hall(Quarry.x - 50f, Quarry.y - 20f, 90f, 10f, 16f, 6f, "metal_green", "roof", 4f, 3.6f, 1, g, "Store");
            Vector3 cr = B.OnGround(Quarry.x - 44f, Quarry.y + 44f);
            B.Box(g, cr + new Vector3(0f, 11f, 0f), new Vector3(1.2f, 22f, 1.2f), "hazard", null, true, "Crane mast");
            B.Box(g, cr + new Vector3(10f, 21.5f, -10f), new Vector3(1f, 1f, 30f), Quaternion.Euler(0f, 45f, 0f), "hazard", null, false, "Crane jib");
            for (int i = 0; i < 4; i++) B.Loot(B.OnGround(Quarry.x + 52f + B.Range(-4f, 4f), Quarry.y + 10f + B.Range(-6f, 6f), 0.25f));
            B.Loot(B.OnGround(Quarry.x, Quarry.y, 0.2f));
            // ramps down the steps
            for (int k = 0; k < 3; k++)
            {
                float edge = 40f - k * 12f;
                // from the lower step up to the rim of the next one (outward, +x)
                B.Stairs(g, B.OnGround(Quarry.x + edge - 9.5f, Quarry.y + 4f), 90f, 5f, 9f, 3f, "rock");
            }
        }
    }
}
