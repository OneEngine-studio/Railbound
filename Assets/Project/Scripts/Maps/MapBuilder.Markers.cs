using System.Collections.Generic;
using UnityEngine;

namespace Railbound.Maps
{
    public partial class MapBuilder
    {
        // 5 x 7 block letters for objective signs (rows from the top; '#' is a block)
        private static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
        {
            ['A'] = new[] { ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
            ['B'] = new[] { "####.", "#...#", "#...#", "####.", "#...#", "#...#", "####." },
            ['C'] = new[] { ".####", "#....", "#....", "#....", "#....", "#....", ".####" },
            ['E'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#####" },
            ['X'] = new[] { "#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#" },
            ['1'] = new[] { "..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###." },
            ['2'] = new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####" },
            ['3'] = new[] { "####.", "....#", "....#", ".###.", "....#", "....#", "####." },
        };

        /// <summary>
        /// Block letters (one mesh) standing upright at <paramref name="origin"/> facing <paramref name="yaw"/>, or lying flat on the
        /// ground when <paramref name="flat"/>. <paramref name="cell"/> is the size of one block.
        /// </summary>
        public GameObject BlockText(string text, Vector3 origin, float yaw, float cell, string mat, bool flat = false, Transform parent = null)
        {
            var boxes = new List<(Vector3, Vector3, Quaternion)>();
            float advance = 6f * cell;
            float width = text.Length * advance - cell;
            for (int i = 0; i < text.Length; i++)
            {
                if (!Glyphs.TryGetValue(char.ToUpperInvariant(text[i]), out var rows)) continue;
                for (int r = 0; r < 7; r++)
                    for (int c = 0; c < 5; c++)
                    {
                        if (rows[r][c] != '#') continue;
                        float lx = -width * 0.5f + i * advance + (c + 0.5f) * cell;
                        float ly = (6 - r + 0.5f) * cell;
                        Vector3 local = flat ? new Vector3(lx, 0f, ly) : new Vector3(lx, ly, 0f);
                        Vector3 size = flat ? new Vector3(cell * 0.96f, 0.04f, cell * 0.96f) : new Vector3(cell * 0.96f, cell * 0.96f, cell * 0.4f);
                        boxes.Add((local, size, Quaternion.identity));
                    }
            }
            var g = Group("Sign " + text, parent);
            g.position = origin;
            g.rotation = Quaternion.Euler(0f, yaw, 0f);
            return Combined(g, "Letters", mat, boxes, false);
        }

        /// <summary>A capture zone: a glowing ring on the ground, a lettered sign on a post, and the zone marker.</summary>
        public MapAnchor CapturePad(float x, float z, float radius, string label, float signYaw = 0f)
        {
            var g = Group("Zone " + label);
            int n = Mathf.Max(16, Mathf.RoundToInt(radius * 3.2f));
            var ring = new List<(Vector3, Vector3, Quaternion)>();
            for (int i = 0; i < n; i++)
            {
                if (i % 2 == 1) continue;   // dashed
                float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n;
                Vector3 p0 = new Vector3(x + Mathf.Cos(a0) * radius, 0f, z + Mathf.Sin(a0) * radius);
                Vector3 p1 = new Vector3(x + Mathf.Cos(a1) * radius, 0f, z + Mathf.Sin(a1) * radius);
                Vector3 mid = (p0 + p1) * 0.5f;
                mid.y = Ground(mid.x, mid.z) + 0.03f;
                Vector3 d = p1 - p0;
                ring.Add((mid, new Vector3(0.35f, 0.06f, d.magnitude), Quaternion.LookRotation(d.normalized, Vector3.up)));
            }
            Combined(g, "Ring", "glow_amber", ring, false);
            Vector3 post = OnGround(x + Mathf.Sin(signYaw * Mathf.Deg2Rad) * (radius + 1.2f), z + Mathf.Cos(signYaw * Mathf.Deg2Rad) * (radius + 1.2f));
            Box(g, post + Vector3.up * 2.2f, new Vector3(0.2f, 4.4f, 0.2f), "metal", null, true, "Sign post");
            Box(g, post + Vector3.up * 4.4f, new Vector3(2.4f, 2.8f, 0.12f), Quaternion.Euler(0f, signYaw + 180f, 0f), "trim", null, false, "Sign board");
            BlockText(label, post + Vector3.up * 3.3f + Quaternion.Euler(0f, signYaw + 180f, 0f) * Vector3.forward * 0.08f, signYaw + 180f, 0.3f, "glow_amber", false, g);
            BlockText(label, OnGround(x, z, 0.02f) - new Vector3(0f, 0f, 1.6f), 0f, 0.7f, "glow_amber", true, g);
            return Anchor(MapAnchor.AnchorKind.CaptureZone, OnGround(x, z), 0f, 0, label, radius);
        }

        /// <summary>A bomb site: a painted square with corner marks, its letter on the ground, crates for cover, and the site marker.</summary>
        public MapAnchor BombSite(float x, float z, float half, string label, float yaw = 0f, Transform parent = null)
        {
            var g = Group("Site " + label, parent);
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            var lines = new List<(Vector3, Vector3, Quaternion)>();
            for (int s = 0; s < 4; s++)
            {
                Quaternion side = q * Quaternion.Euler(0f, s * 90f, 0f);
                Vector3 c = new Vector3(x, 0f, z) + side * new Vector3(0f, 0f, half);
                c.y = Ground(c.x, c.z) + 0.03f;
                lines.Add((c, new Vector3(half * 2f, 0.05f, 0.25f), side));
            }
            Combined(g, "Outline", "hazard", lines, false);
            BlockText(label, OnGround(x, z, 0.03f) - q * new Vector3(0f, 0f, 2f), yaw, 0.6f, "hazard", true, g);
            CrateStack(OnGround(x + half * 0.55f, z + half * 0.4f), yaw, g);
            CrateStack(OnGround(x - half * 0.5f, z - half * 0.5f), yaw + 90f, g);
            return Anchor(MapAnchor.AnchorKind.BombSite, OnGround(x, z), yaw, 0, label, half);
        }

        /// <summary>An extraction pad: a concrete pad with hazard edges, a beacon mast with a glowing top, and the marker.</summary>
        public MapAnchor ExtractionPad(float x, float z, string label)
        {
            var g = Group("Extraction " + label);
            float y = Ground(x, z);
            float lo = Mathf.Min(Mathf.Min(Ground(x - 7, z - 7), Ground(x + 7, z - 7)), Mathf.Min(Ground(x - 7, z + 7), Ground(x + 7, z + 7)));
            float top = Mathf.Max(y, Mathf.Max(Ground(x - 7, z), Ground(x + 7, z))) + 0.3f;
            Box(g, new Vector3(x, (top + lo - 0.5f) * 0.5f, z), new Vector3(14f, top - lo + 0.5f, 14f), "concrete", null, true, "Pad");
            var stripes = new List<(Vector3, Vector3, Quaternion)>();
            for (int s = 0; s < 4; s++)
            {
                Quaternion side = Quaternion.Euler(0f, s * 90f, 0f);
                Vector3 c = new Vector3(x, top + 0.02f, z) + side * new Vector3(0f, 0f, 6.4f);
                stripes.Add((c, new Vector3(13.6f, 0.04f, 0.6f), side));
            }
            Combined(g, "Stripes", "hazard", stripes, false);
            BlockText("EX", new Vector3(x, top + 0.03f, z - 1.8f), 0f, 0.55f, "hazard", true, g);
            Vector3 mast = new Vector3(x + 5.2f, top, z + 5.2f);
            Box(g, mast + Vector3.up * 8f, new Vector3(0.35f, 16f, 0.35f), "metal", null, true, "Beacon mast");
            Box(g, mast + Vector3.up * 16.4f, new Vector3(1.1f, 0.9f, 1.1f), "glow_amber", null, false, "Beacon");
            return Anchor(MapAnchor.AnchorKind.Extraction, new Vector3(x, top, z), 0f, 0, label, 7f);
        }
    }
}
