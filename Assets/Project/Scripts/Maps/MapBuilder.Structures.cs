using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Railbound.Maps
{
    /// <summary>A gap in a wall: a door or a window, measured along the wall from its start.</summary>
    public struct Opening
    {
        public float At, Width, Bottom, Top;
        public static Opening Door(float at, float width = 1.4f, float height = 2.35f) => new Opening { At = at, Width = width, Bottom = 0f, Top = height };
        public static Opening Window(float at, float width = 1.3f, float sill = 1.0f, float top = 2.15f) => new Opening { At = at, Width = width, Bottom = sill, Top = top };
    }

    /// <summary>What a house or block looks like.</summary>
    public class BuildingSpec
    {
        public float Width = 8f, Depth = 8f;
        public int Floors = 1;
        public float FloorHeight = 3.2f;
        public string Wall = "plaster_white";
        public Color? Tint;
        public string Trim = "concrete_dark";
        public string Floor = "concrete";
        public string Roof = "roof";
        public bool Gable;
        public bool BackDoor = true;
        public bool SideDoor;
        public float WindowEvery = 3.2f;
        public bool Interior = true;
        public int LootPerFloor = 1;
        public bool Parapet = true;
    }

    public partial class MapBuilder
    {
        public const float WallThickness = 0.25f;

        // ------------------------------------------------------------------ walls
        /// <summary>A wall from <paramref name="a"/> to <paramref name="b"/> (local to <paramref name="p"/>, y ignored) with doors and windows cut out.</summary>
        public void Wall(Transform p, Vector3 a, Vector3 b, float y0, float height, float thickness, string mat, Color? tint, params Opening[] openings)
        {
            Vector3 d = b - a; d.y = 0f;
            float len = d.magnitude;
            if (len < 0.05f) return;
            d /= len;
            Quaternion rot = Quaternion.LookRotation(d, Vector3.up);
            var ops = new List<Opening>(openings);
            ops.Sort((x, y) => x.At.CompareTo(y.At));
            float cursor = 0f;
            void Piece(float s, float e, float bottom, float top)
            {
                if (e - s < 0.02f || top - bottom < 0.02f) return;
                Vector3 c = a + d * ((s + e) * 0.5f);
                c.y = y0 + (bottom + top) * 0.5f;
                Box(p, c, new Vector3(thickness, top - bottom, e - s), rot, mat, tint);
            }
            foreach (var o in ops)
            {
                float s = Mathf.Clamp(o.At - o.Width * 0.5f, cursor, len), e = Mathf.Clamp(o.At + o.Width * 0.5f, s, len);
                Piece(cursor, s, 0f, height);
                if (o.Bottom > 0.02f) Piece(s, e, 0f, o.Bottom);
                if (o.Top < height - 0.02f) Piece(s, e, o.Top, height);
                cursor = e;
            }
            Piece(cursor, len, 0f, height);
        }

        /// <summary>Evenly spaced windows along a wall of <paramref name="len"/> metres, leaving room for a door at <paramref name="doorAt"/> (or none: NaN).</summary>
        public static List<Opening> Windows(float len, float every, float doorAt, float doorWidth = 1.4f, float sill = 1.0f, float top = 2.15f)
        {
            var list = new List<Opening>();
            int n = Mathf.Max(0, Mathf.FloorToInt((len - 1.2f) / every));
            for (int i = 0; i < n; i++)
            {
                float at = (len - (n - 1) * every) * 0.5f + i * every;
                if (!float.IsNaN(doorAt) && Mathf.Abs(at - doorAt) < (doorWidth + 1.3f) * 0.5f + 0.4f) continue;
                list.Add(Opening.Window(at, 1.2f, sill, top));
            }
            return list;
        }

        // ------------------------------------------------------------------ buildings
        /// <summary>
        /// A house or block standing on the ground at <paramref name="x"/>, <paramref name="z"/>: walls with a front door, windows, a back
        /// door, floors joined by an inside ramp-stair, a flat roof with a parapet (or a gable roof), a plinth down to the lowest corner,
        /// crates inside and loot spots. Returns the building's root (its local -Z side is the front).
        /// </summary>
        public Transform Building(float x, float z, float yaw, BuildingSpec s, Transform parent = null, string name = "Building")
        {
            float hw = s.Width * 0.5f, hd = s.Depth * 0.5f;
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            float lo = float.MaxValue, hi = float.MinValue;
            for (int i = 0; i < 9; i++)
            {
                Vector3 c = q * new Vector3((i % 3 - 1) * hw, 0f, (i / 3 - 1) * hd);
                float g = Ground(x + c.x, z + c.z);
                lo = Mathf.Min(lo, g); hi = Mathf.Max(hi, g);
            }
            float baseY = hi + 0.18f;
            var g0 = Group(name, parent);
            g0.position = new Vector3(x, baseY, z);
            g0.rotation = q;

            // plinth from the lowest corner up to the floor
            float plinth = baseY - lo + 0.6f;
            Box(g0, new Vector3(0f, -plinth * 0.5f, 0f), new Vector3(s.Width + 0.3f, plinth, s.Depth + 0.3f), s.Trim, null, true, "Plinth");

            float t = WallThickness;
            bool stairs = s.Floors > 1;
            float stairW = 1.25f, stairRun = Mathf.Min(5.4f, s.Depth - 2.2f);
            float sx0 = hw - t - stairW, sx1 = hw - t;           // stair strip along the +X wall
            float sz0 = -hd + t + 0.9f, sz1 = sz0 + stairRun;

            for (int f = 0; f < s.Floors; f++)
            {
                float y = f * s.FloorHeight;
                // floor slab (with the stair hole above the ground floor)
                if (f == 0) Box(g0, new Vector3(0f, -0.08f, 0f), new Vector3(s.Width - 0.02f, 0.16f, s.Depth - 0.02f), s.Floor, null, true, "Floor");
                else if (stairs)
                {
                    SlabWithHole(g0, y, hw, hd, sx0, sx1, sz0 - 0.1f, sz1, s.Floor);
                }
                float fh = s.FloorHeight;
                bool ground = f == 0;
                float doorAt = s.Width * 0.5f + (stairs ? -0.8f : 0f);
                var front = Windows(s.Width, s.WindowEvery, ground ? doorAt : float.NaN);
                if (ground) front.Add(Opening.Door(doorAt));
                Wall(g0, new Vector3(-hw, 0, -hd + t * 0.5f), new Vector3(hw, 0, -hd + t * 0.5f), y, fh, t, s.Wall, s.Tint, front.ToArray());
                float backAt = s.Width * 0.3f;
                var back = Windows(s.Width, s.WindowEvery, ground && s.BackDoor ? backAt : float.NaN);
                if (ground && s.BackDoor) back.Add(Opening.Door(backAt));
                Wall(g0, new Vector3(-hw, 0, hd - t * 0.5f), new Vector3(hw, 0, hd - t * 0.5f), y, fh, t, s.Wall, s.Tint, back.ToArray());
                float sideLen = s.Depth - 2f * t;
                float sideDoorAt = sideLen * 0.5f;
                var left = Windows(sideLen, s.WindowEvery, ground && s.SideDoor ? sideDoorAt : float.NaN);
                if (ground && s.SideDoor) left.Add(Opening.Door(sideDoorAt));
                Wall(g0, new Vector3(-hw + t * 0.5f, 0, -hd + t), new Vector3(-hw + t * 0.5f, 0, hd - t), y, fh, t, s.Wall, s.Tint, left.ToArray());
                // the stair wall keeps its windows above the stair only on the top floor
                var right = stairs && f < s.Floors - 1 ? new List<Opening>() : Windows(sideLen, s.WindowEvery, float.NaN);
                Wall(g0, new Vector3(hw - t * 0.5f, 0, -hd + t), new Vector3(hw - t * 0.5f, 0, hd - t), y, fh, t, s.Wall, s.Tint, right.ToArray());

                // corner pillars and a band at the floor line
                for (int c = 0; c < 4; c++)
                {
                    float cx = (c % 2 == 0 ? -1 : 1) * (hw + 0.02f), cz = (c < 2 ? -1 : 1) * (hd + 0.02f);
                    Box(g0, new Vector3(cx, y + fh * 0.5f, cz), new Vector3(0.42f, fh, 0.42f), s.Trim, null, false, "Pillar");
                }
                if (f > 0) Box(g0, new Vector3(0f, y, 0f), new Vector3(s.Width + 0.12f, 0.22f, s.Depth + 0.12f), s.Trim, null, false, "Band");

                if (stairs && f < s.Floors - 1)
                    Stairs(g0, new Vector3((sx0 + sx1) * 0.5f, y, sz0), 0f, fh, stairRun, stairW, s.Trim);

                if (s.Interior)
                {
                    int crates = Rng.Next(1, 3);
                    for (int k = 0; k < crates; k++)
                    {
                        float cx = Range(-hw + 1.2f, (stairs ? sx0 : hw) - 1.2f), cz = Range(-hd + 2.2f, hd - 1.2f);
                        if (cx > hw - 1.2f || cz < -hd + 1.6f) continue;
                        float size = Range(0.8f, 1.2f);
                        Box(g0, new Vector3(cx, y + size * 0.5f, cz), new Vector3(size * 1.2f, size, size), Quaternion.Euler(0f, Range(-20f, 20f), 0f), "wood", null, true, "Crate");
                    }
                    for (int k = 0; k < s.LootPerFloor; k++)
                    {
                        float lx = Range(-hw + 1f, (stairs ? sx0 : hw) - 1f), lz = Range(-hd + 1f, hd - 1f);
                        Loot(g0.TransformPoint(new Vector3(lx, y + 0.15f, lz)));
                    }
                }
            }

            float top = s.Floors * s.FloorHeight;
            if (s.Gable) GableRoof(g0, top, s.Width, s.Depth, s.Roof);
            else
            {
                Box(g0, new Vector3(0f, top + 0.12f, 0f), new Vector3(s.Width + 0.4f, 0.24f, s.Depth + 0.4f), s.Roof, null, true, "Roof");
                if (s.Parapet)
                {
                    float ph = 0.55f, pt = 0.2f;
                    Box(g0, new Vector3(0f, top + 0.24f + ph * 0.5f, -hd - 0.1f), new Vector3(s.Width + 0.4f, ph, pt), s.Trim, null, true, "Parapet");
                    Box(g0, new Vector3(0f, top + 0.24f + ph * 0.5f, hd + 0.1f), new Vector3(s.Width + 0.4f, ph, pt), s.Trim, null, true, "Parapet");
                    Box(g0, new Vector3(-hw - 0.1f, top + 0.24f + ph * 0.5f, 0f), new Vector3(pt, ph, s.Depth + 0.4f), s.Trim, null, true, "Parapet");
                    Box(g0, new Vector3(hw + 0.1f, top + 0.24f + ph * 0.5f, 0f), new Vector3(pt, ph, s.Depth + 0.4f), s.Trim, null, true, "Parapet");
                }
            }
            return g0;
        }

        private void SlabWithHole(Transform p, float y, float hw, float hd, float hx0, float hx1, float hz0, float hz1, string mat)
        {
            const float th = 0.22f;
            float cy = y - th * 0.5f;
            // left of the hole, right of the hole, in front of it, behind it
            Box(p, new Vector3((-hw + hx0) * 0.5f, cy, 0f), new Vector3(hx0 + hw, th, hd * 2f), mat, null, true, "Slab");
            if (hw - hx1 > 0.05f) Box(p, new Vector3((hx1 + hw) * 0.5f, cy, 0f), new Vector3(hw - hx1, th, hd * 2f), mat, null, true, "Slab");
            Box(p, new Vector3((hx0 + hx1) * 0.5f, cy, (-hd + hz0) * 0.5f), new Vector3(hx1 - hx0, th, hz0 + hd), mat, null, true, "Slab");
            Box(p, new Vector3((hx0 + hx1) * 0.5f, cy, (hz1 + hd) * 0.5f), new Vector3(hx1 - hx0, th, hd - hz1), mat, null, true, "Slab");
            // a rail along the open side of the hole
            Box(p, new Vector3(hx0 - 0.04f, y + 0.5f, (hz0 + hz1) * 0.5f), new Vector3(0.06f, 1f, hz1 - hz0), "metal", null, true, "Rail");
        }

        private void GableRoof(Transform p, float top, float w, float d, string mat)
        {
            const float pitch = 27f;
            float hs = w * 0.5f + 0.35f;
            float rise = hs * Mathf.Tan(pitch * Mathf.Deg2Rad);
            float len = hs / Mathf.Cos(pitch * Mathf.Deg2Rad);
            Box(p, new Vector3(-hs * 0.5f, top + rise * 0.5f + 0.1f, 0f), new Vector3(len, 0.2f, d + 0.6f), Quaternion.Euler(0f, 0f, pitch), mat, null, true, "Roof");
            Box(p, new Vector3(hs * 0.5f, top + rise * 0.5f + 0.1f, 0f), new Vector3(len, 0.2f, d + 0.6f), Quaternion.Euler(0f, 0f, -pitch), mat, null, true, "Roof");
            // the gable ends, stepped
            int steps = 5;
            for (int i = 0; i < steps; i++)
            {
                float f0 = (float)i / steps, f1 = (float)(i + 1) / steps;
                float wi = (w - 0.1f) * (1f - (f0 + f1) * 0.5f);
                float yy = top + rise * (f0 + f1) * 0.5f;
                float hh = rise / steps;
                Box(p, new Vector3(0f, yy, -d * 0.5f + 0.12f), new Vector3(wi, hh, 0.22f), "plaster_white", null, false, "Gable");
                Box(p, new Vector3(0f, yy, d * 0.5f - 0.12f), new Vector3(wi, hh, 0.22f), "plaster_white", null, false, "Gable");
            }
            // ceiling (walkable attic floor keeps players from falling into the roof space)
            Box(p, new Vector3(0f, top + 0.08f, 0f), new Vector3(w, 0.16f, d), "concrete_dark", null, true, "Ceiling");
        }

        /// <summary>
        /// A straight flight of stairs (local to <paramref name="p"/>) starting at <paramref name="bottom"/>, climbing <paramref name="rise"/>
        /// over <paramref name="run"/> metres in the direction <paramref name="yaw"/> (0: local +Z). Walking surface is a smooth ramp;
        /// the steps are drawn on it.
        /// </summary>
        public Transform Stairs(Transform p, Vector3 bottom, float yaw, float rise, float run, float width, string mat)
        {
            var g = Group("Stairs", p);
            g.localPosition = bottom;
            g.localRotation = Quaternion.Euler(0f, yaw, 0f);
            float len = Mathf.Sqrt(rise * rise + run * run);
            float angle = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            var ramp = new GameObject("Ramp");
            ramp.transform.SetParent(g, false);
            ramp.transform.localPosition = new Vector3(0f, rise * 0.5f - 0.06f, run * 0.5f);
            ramp.transform.localRotation = Quaternion.Euler(-angle, 0f, 0f);
            ramp.AddComponent<BoxCollider>().size = new Vector3(width, 0.12f, len + 0.2f);
            int n = Mathf.Max(2, Mathf.CeilToInt(rise / 0.19f));
            var parts = new List<(Vector3, Vector3, Quaternion)>();
            for (int i = 0; i < n; i++)
            {
                float h = rise * (i + 1) / n, z0 = run * i / n, z1 = run * (i + 1) / n;
                parts.Add((new Vector3(0f, h * 0.5f, (z0 + z1) * 0.5f), new Vector3(width, h, z1 - z0), Quaternion.identity));
            }
            Combined(g, "Steps", mat, parts, false);
            return g;
        }

        // ------------------------------------------------------------------ halls and platforms
        /// <summary>
        /// A big hall (engine shed, warehouse, furnace hall): thick walls with wide doors at both ends and side doors, columns, roof
        /// trusses and a roof with a skylight strip. Returns its root (length along local Z).
        /// </summary>
        public Transform Hall(float x, float z, float yaw, float width, float length, float height, string wall, string roof,
                              float endDoorWidth, float endDoorHeight, int sideDoors, Transform parent = null, string name = "Hall", Color? tint = null)
        {
            float hw = width * 0.5f, hl = length * 0.5f;
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            float lo = float.MaxValue, hi = float.MinValue;
            for (int i = 0; i < 9; i++)
            {
                Vector3 c = q * new Vector3((i % 3 - 1) * hw, 0f, (i / 3 - 1) * hl);
                float gnd = Ground(x + c.x, z + c.z);
                lo = Mathf.Min(lo, gnd); hi = Mathf.Max(hi, gnd);
            }
            var g = Group(name, parent);
            g.position = new Vector3(x, hi + 0.1f, z);
            g.rotation = q;
            float plinth = hi - lo + 0.7f;
            Box(g, new Vector3(0f, -plinth * 0.5f, 0f), new Vector3(width + 0.4f, plinth, length + 0.4f), "concrete_dark", null, true, "Plinth");
            Box(g, new Vector3(0f, -0.05f, 0f), new Vector3(width - 0.1f, 0.1f, length - 0.1f), "concrete", null, true, "Floor");
            const float t = 0.4f;
            Wall(g, new Vector3(-hw, 0, -hl + t * 0.5f), new Vector3(hw, 0, -hl + t * 0.5f), 0f, height, t, wall, tint, Opening.Door(hw, endDoorWidth, endDoorHeight));
            Wall(g, new Vector3(-hw, 0, hl - t * 0.5f), new Vector3(hw, 0, hl - t * 0.5f), 0f, height, t, wall, tint, Opening.Door(hw, endDoorWidth, endDoorHeight));
            var side = new List<Opening>();
            float sl = length - 2f * t;
            for (int i = 0; i < sideDoors; i++) side.Add(Opening.Door(sl * (i + 1) / (sideDoors + 1), 2.2f, 2.8f));
            for (int i = 0; i <= sideDoors; i++)
            {
                float at = sl * (i + 0.5f) / (sideDoors + 1);
                side.Add(Opening.Window(at, 2.4f, height - 2.2f, height - 0.8f));
            }
            Wall(g, new Vector3(-hw + t * 0.5f, 0, -hl + t), new Vector3(-hw + t * 0.5f, 0, hl - t), 0f, height, t, wall, tint, side.ToArray());
            Wall(g, new Vector3(hw - t * 0.5f, 0, -hl + t), new Vector3(hw - t * 0.5f, 0, hl - t), 0f, height, t, wall, tint, side.ToArray());
            // columns and trusses every 6 m
            int bays = Mathf.Max(2, Mathf.RoundToInt(length / 6f));
            for (int i = 1; i < bays; i++)
            {
                float zz = -hl + length * i / bays;
                Box(g, new Vector3(-hw + 0.55f, height * 0.5f, zz), new Vector3(0.45f, height, 0.45f), "metal", null, true, "Column");
                Box(g, new Vector3(hw - 0.55f, height * 0.5f, zz), new Vector3(0.45f, height, 0.45f), "metal", null, true, "Column");
                Box(g, new Vector3(0f, height - 0.35f, zz), new Vector3(width - 0.4f, 0.3f, 0.25f), "metal", null, false, "Truss");
            }
            // lamps under the roof: a hall has almost no daylight inside
            for (int i = 0; i < bays; i++)
                InteriorLight(g, new Vector3(0f, height - 1.1f, -hl + length * (i + 0.5f) / bays), Mathf.Max(width, 12f) * 1.1f, 9000f);

            // roof with a skylight strip down the middle (the strip is open: daylight falls in)
            float strip = Mathf.Min(2.4f, width * 0.2f);
            float side2 = (width + 0.6f - strip) * 0.5f;
            Box(g, new Vector3(-(strip + side2) * 0.5f, height + 0.15f, 0f), new Vector3(side2, 0.3f, length + 0.6f), roof, null, true, "Roof");
            Box(g, new Vector3((strip + side2) * 0.5f, height + 0.15f, 0f), new Vector3(side2, 0.3f, length + 0.6f), roof, null, true, "Roof");
            for (int i = 0; i <= bays; i++)
                Box(g, new Vector3(0f, height + 0.15f, -hl + length * i / bays), new Vector3(strip, 0.2f, 0.15f), "metal", null, true, "Skylight bar");
            return g;
        }

        /// <summary>A warm lamp hanging inside a building (local to <paramref name="p"/>): a glowing fitting and a shadowless point light.</summary>
        public void InteriorLight(Transform p, Vector3 local, float range, float lumen)
        {
            Box(p, local + Vector3.up * 0.15f, new Vector3(0.9f, 0.12f, 0.4f), "glow_white", null, false, "Lamp");
            if (Headless) return;
            var go = new GameObject("Light");
            go.transform.SetParent(p, false);
            go.transform.localPosition = local;
            var hd = UnityEngine.Rendering.HighDefinition.GameObjectExtension.AddHDLight(go, UnityEngine.Rendering.HighDefinition.HDLightTypeAndShape.Point);
            hd.SetIntensity(lumen, UnityEngine.Rendering.HighDefinition.LightUnit.Lumen);
            hd.SetRange(range);
            hd.EnableShadows(false);
            hd.color = new Color(1f, 0.88f, 0.72f);
        }

        /// <summary>A raised platform (station platform, loading dock) with ramp-stairs at both ends. Top at ground + height.</summary>
        public Transform Platform(float x, float z, float yaw, float length, float width, float height, string mat, Transform parent = null)
        {
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            float lo = float.MaxValue, hi = float.MinValue;
            for (int i = -1; i <= 1; i++)
            {
                Vector3 c = q * new Vector3(0f, 0f, i * length * 0.5f);
                float gnd = Ground(x + c.x, z + c.z);
                lo = Mathf.Min(lo, gnd); hi = Mathf.Max(hi, gnd);
            }
            var g = Group("Platform", parent);
            g.position = new Vector3(x, hi, z);
            g.rotation = q;
            float depth = height + (hi - lo) + 0.4f;
            Box(g, new Vector3(0f, height - depth * 0.5f, 0f), new Vector3(width, depth, length), mat, null, true, "Deck");
            Box(g, new Vector3(width * 0.5f - 0.2f, height + 0.01f, 0f), new Vector3(0.35f, 0.02f, length), "hazard", null, false, "Edge");
            float run = height / Mathf.Tan(28f * Mathf.Deg2Rad);
            Stairs(g, new Vector3(-width * 0.25f, 0f, length * 0.5f + run), 180f, height, run, Mathf.Min(2.4f, width * 0.45f), mat);
            Stairs(g, new Vector3(-width * 0.25f, 0f, -length * 0.5f - run), 0f, height, run, Mathf.Min(2.4f, width * 0.45f), mat);
            return g;
        }

        /// <summary>A walkway at <paramref name="height"/> above <paramref name="a"/>..<paramref name="b"/> (world), with railings and posts.</summary>
        public Transform Catwalk(Vector3 a, Vector3 b, float y, float width, Transform parent = null, bool railLeft = true, bool railRight = true)
        {
            Vector3 d = b - a; d.y = 0f;
            float len = d.magnitude;
            var g = Group("Catwalk", parent);
            g.position = new Vector3((a.x + b.x) * 0.5f, y, (a.z + b.z) * 0.5f);
            g.rotation = Quaternion.LookRotation(d / len, Vector3.up);
            Box(g, new Vector3(0f, -0.08f, 0f), new Vector3(width, 0.16f, len), "metal", null, true, "Deck");
            if (railLeft) Railing(g, new Vector3(-width * 0.5f, 0f, -len * 0.5f), new Vector3(-width * 0.5f, 0f, len * 0.5f), 0f);
            if (railRight) Railing(g, new Vector3(width * 0.5f, 0f, -len * 0.5f), new Vector3(width * 0.5f, 0f, len * 0.5f), 0f);
            int posts = Mathf.Max(2, Mathf.CeilToInt(len / 5f) + 1);
            for (int i = 0; i < posts; i++)
            {
                float zz = -len * 0.5f + len * i / (posts - 1);
                Vector3 wp = g.TransformPoint(new Vector3(0f, 0f, zz));
                float gy = Ground(wp.x, wp.z);
                float hh = y - gy;
                if (hh > 0.3f) Box(g, new Vector3(0f, -hh * 0.5f - 0.16f, zz), new Vector3(0.3f, hh, 0.3f), "metal", null, true, "Post");
            }
            return g;
        }

        /// <summary>A 1 m railing on top of <paramref name="y"/> from <paramref name="a"/> to <paramref name="b"/> (local), solid to walk against.</summary>
        public void Railing(Transform p, Vector3 a, Vector3 b, float y)
        {
            Vector3 d = b - a; d.y = 0f;
            float len = d.magnitude;
            if (len < 0.1f) return;
            Quaternion r = Quaternion.LookRotation(d / len, Vector3.up);
            Vector3 mid = (a + b) * 0.5f;
            Box(p, new Vector3(mid.x, y + 1.0f, mid.z), new Vector3(0.06f, 0.06f, len), r, "metal", null, false, "Rail");
            Box(p, new Vector3(mid.x, y + 0.5f, mid.z), new Vector3(0.04f, 0.04f, len), r, "metal", null, false, "Rail");
            var block = new GameObject("RailBlock");
            block.transform.SetParent(p, false);
            block.transform.localPosition = new Vector3(mid.x, y + 0.55f, mid.z);
            block.transform.localRotation = r;
            block.AddComponent<BoxCollider>().size = new Vector3(0.08f, 1.1f, len);
            int n = Mathf.Max(2, Mathf.CeilToInt(len / 1.6f) + 1);
            var posts = new List<(Vector3, Vector3, Quaternion)>();
            for (int i = 0; i < n; i++)
            {
                Vector3 pp = Vector3.Lerp(a, b, (float)i / (n - 1));
                posts.Add((new Vector3(pp.x, y + 0.5f, pp.z), new Vector3(0.05f, 1.0f, 0.05f), Quaternion.identity));
            }
            Combined(p, "Posts", "metal", posts, false);
        }

        // ------------------------------------------------------------------ cover
        public GameObject Crate(Vector3 groundPoint, float size, float yaw, Transform parent = null, string mat = "wood")
            => Box(parent, groundPoint + Vector3.up * (size * 0.5f), new Vector3(size, size, size), Quaternion.Euler(0f, yaw, 0f), mat, null, true, "Crate");

        /// <summary>A stack of crates (world ground point).</summary>
        public void CrateStack(Vector3 g, float yaw, Transform parent = null)
        {
            float s = 1.2f;
            Crate(g, s, yaw, parent);
            if (Chance(0.7f)) Crate(g + Quaternion.Euler(0f, yaw, 0f) * new Vector3(s + 0.05f, 0f, 0f), s, yaw + Range(-6f, 6f), parent);
            if (Chance(0.5f)) Crate(g + Vector3.up * s, s * 0.9f, yaw + Range(-15f, 15f), parent);
        }

        /// <summary>A waist-high sandbag wall along the ground from a to b (world).</summary>
        public void Sandbags(Vector3 a, Vector3 b, Transform parent = null)
        {
            Vector3 d = b - a; d.y = 0f;
            float len = d.magnitude;
            if (len < 0.3f) return;
            Vector3 dir = d / len;
            int n = Mathf.CeilToInt(len / 0.75f);
            var g = Group("Sandbags", parent);
            Quaternion r = Quaternion.LookRotation(dir, Vector3.up);
            var bags = new List<(Vector3, Vector3, Quaternion)>();
            for (int row = 0; row < 3; row++)
            {
                for (int i = 0; i < n; i++)
                {
                    float off = (i + (row % 2) * 0.5f) * len / n;
                    if (off > len) continue;
                    Vector3 p = a + dir * off;
                    float gy = Ground(p.x, p.z);
                    bags.Add((new Vector3(p.x, gy + 0.15f + row * 0.28f, p.z), new Vector3(0.5f, 0.3f, 0.78f), r * Quaternion.Euler(0f, Range(-5f, 5f), 0f)));
                }
            }
            Combined(g, "Bags", "sandbag", bags, false);
            float mid = Ground((a.x + b.x) * 0.5f, (a.z + b.z) * 0.5f);
            var col = new GameObject("Cover");
            col.transform.SetParent(g, false);
            col.transform.position = new Vector3((a.x + b.x) * 0.5f, mid + 0.45f, (a.z + b.z) * 0.5f);
            col.transform.rotation = r;
            col.AddComponent<BoxCollider>().size = new Vector3(0.55f, 0.95f, len);
        }

        /// <summary>A concrete road barrier (world ground point).</summary>
        public void Barrier(Vector3 g, float yaw, Transform parent = null)
        {
            Quaternion r = Quaternion.Euler(0f, yaw, 0f);
            Box(parent, g + Vector3.up * 0.25f, new Vector3(0.65f, 0.5f, 2.4f), r, "concrete", null, true, "Barrier");
            Box(parent, g + Vector3.up * 0.7f, new Vector3(0.3f, 0.45f, 2.4f), r, "concrete", null, true, "Barrier");
        }

        /// <summary>A shipping container (the kit's model, or a box of the same size) standing on the ground.</summary>
        public GameObject Container(Vector3 g, float yaw, Transform parent = null, int color = -1, float stackY = 0f)
        {
            GameObject prefab = Kit != null && Kit.containers.Length > 0 ? Kit.containers[color >= 0 ? color % Kit.containers.Length : Rng.Next(Kit.containers.Length)] : null;
            Vector3 at = g + Vector3.up * stackY;
            if (prefab != null)
            {
                var b = PropSize(prefab);
                var go = Prop(prefab, at - Vector3.up * b.min.y, yaw, parent);
                if (go != null) return go;
            }
            Color[] tints = { new Color(0.2f, 0.35f, 0.6f), new Color(0.25f, 0.45f, 0.3f), new Color(0.65f, 0.22f, 0.18f), new Color(0.75f, 0.55f, 0.2f) };
            return Box(parent, at + Vector3.up * 1.3f, new Vector3(2.44f, 2.6f, 12.2f), Quaternion.Euler(0f, yaw, 0f), "metal", tints[(color < 0 ? Rng.Next(4) : color) % 4], true, "Container");
        }

        /// <summary>Height of a container (for stacking).</summary>
        public float ContainerHeight
        {
            get
            {
                if (Kit != null && Kit.containers.Length > 0) { var b = PropSize(Kit.containers[0]); if (b.size.y > 0.5f) return b.size.y; }
                return 2.6f;
            }
        }

        /// <summary>A wagon or locomotive on a track at <paramref name="railTop"/> height.</summary>
        public GameObject RollingStock(GameObject[] pool, Vector3 railPoint, float yaw, Transform parent = null, int index = -1)
        {
            GameObject prefab = pool != null && pool.Length > 0 ? pool[index >= 0 ? index % pool.Length : Rng.Next(pool.Length)] : null;
            if (prefab != null)
            {
                var b = PropSize(prefab);
                var go = Prop(prefab, railPoint - Vector3.up * b.min.y, yaw, parent);
                if (go != null) return go;
            }
            return Box(parent, railPoint + Vector3.up * 1.8f, new Vector3(3f, 3.2f, 14f), Quaternion.Euler(0f, yaw, 0f), "metal_rust", null, true, "Wagon");
        }

        /// <summary>A railway track along the ground from a to b (world): ballast bed, sleepers and two rails. Returns the rail-top height at the middle.</summary>
        public float Track(Vector3 a, Vector3 b, Transform parent = null, bool ballast = true)
        {
            Vector3 d = b - a; d.y = 0f;
            float len = d.magnitude;
            if (len < 1f) return Ground(a.x, a.z);
            Vector3 dir = d / len;
            Quaternion r = Quaternion.LookRotation(dir, Vector3.up);
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            var g = Group("Track", parent);
            var sleepers = new List<(Vector3, Vector3, Quaternion)>();
            var rails = new List<(Vector3, Vector3, Quaternion)>();
            var beds = new List<(Vector3, Vector3, Quaternion)>();
            int segs = Mathf.CeilToInt(len / 10f);
            for (int s = 0; s < segs; s++)
            {
                float t0 = len * s / segs, t1 = len * (s + 1) / segs;
                Vector3 p0 = a + dir * t0, p1 = a + dir * t1;
                float y0 = Ground(p0.x, p0.z), y1 = Ground(p1.x, p1.z);
                Vector3 mid = (p0 + p1) * 0.5f;
                float ym = (y0 + y1) * 0.5f;
                float pitch = -Mathf.Atan2(y1 - y0, t1 - t0) * Mathf.Rad2Deg;
                Quaternion rr = r * Quaternion.Euler(pitch, 0f, 0f);
                if (ballast) beds.Add((new Vector3(mid.x, ym + 0.1f, mid.z), new Vector3(3.4f, 0.4f, t1 - t0 + 0.05f), rr));
                for (int k = -1; k <= 1; k += 2)
                    rails.Add((new Vector3(mid.x, ym + 0.42f, mid.z) + side * (0.72f * k), new Vector3(0.08f, 0.15f, t1 - t0 + 0.02f), rr));
            }
            int n = Mathf.FloorToInt(len / 0.65f);
            for (int i = 0; i <= n; i++)
            {
                Vector3 p = a + dir * (i * 0.65f);
                float y = Ground(p.x, p.z);
                sleepers.Add((new Vector3(p.x, y + 0.3f, p.z), new Vector3(2.6f, 0.16f, 0.26f), r));
            }
            if (ballast) Combined(g, "Ballast", "gravel", beds, true);
            Combined(g, "Sleepers", "wood_dark", sleepers, false);
            Combined(g, "Rails", "metal", rails, false);
            Vector3 c = (a + b) * 0.5f;
            return Ground(c.x, c.z) + 0.5f;
        }

        /// <summary>A lamp post (pole and glowing head) standing on the ground.</summary>
        public void Lamp(Vector3 g, float yaw, Transform parent = null, float height = 6f)
        {
            Quaternion r = Quaternion.Euler(0f, yaw, 0f);
            Box(parent, g + Vector3.up * (height * 0.5f), new Vector3(0.18f, height, 0.18f), "metal", null, true, "Lamp");
            Box(parent, g + Vector3.up * height + r * new Vector3(0f, 0f, 0.7f), new Vector3(0.12f, 0.12f, 1.4f), r, "metal", null, false, "Arm");
            Box(parent, g + Vector3.up * (height - 0.12f) + r * new Vector3(0f, 0f, 1.35f), new Vector3(0.4f, 0.12f, 0.55f), r, "glow_white", null, false, "Light");
        }

        /// <summary>A cylinder (tank, silo, pipe) standing at a world ground point; <paramref name="horizontal"/> lays it along yaw.</summary>
        public GameObject Cylinder(Vector3 center, float radius, float height, string mat, float yaw = 0f, bool horizontal = false, Transform parent = null, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Cylinder";
            go.transform.SetParent(parent != null ? parent : Root, false);
            go.transform.position = center;
            go.transform.rotation = horizontal ? Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(90f, 0f, 0f) : Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            go.GetComponent<MeshRenderer>().sharedMaterial = Mat(mat);
            var cap = go.GetComponent<CapsuleCollider>();
            if (cap != null) Kill(cap);
            if (collider) go.AddComponent<MeshCollider>().sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            Boxes++;
            return go;
        }

        // ------------------------------------------------------------------ combined meshes
        /// <summary>Many boxes of one material as one mesh (sleepers, steps, sandbags): one draw call, optional mesh collider.</summary>
        public GameObject Combined(Transform parent, string name, string mat, List<(Vector3 c, Vector3 s, Quaternion r)> boxes, bool collider)
        {
            if (boxes.Count == 0) return null;
            float tile = Tile(mat);
            var combine = new CombineInstance[boxes.Count];
            for (int i = 0; i < boxes.Count; i++)
            {
                var (c, s, r) = boxes[i];
                combine[i] = new CombineInstance { mesh = BoxMesh(s, tile), transform = Matrix4x4.TRS(c, r, Vector3.one) };
            }
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(combine, true, true);
            mesh.RecalculateBounds();
            // the boxes are in the parent's own space (a group made at the root has none: there they are world positions)
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : Root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Mat(mat);
            if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            Boxes += boxes.Count;
            return go;
        }
    }
}
