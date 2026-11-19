using UnityEngine;

namespace Railbound.Maps.Layouts
{
    /// <summary>Shared pieces of the layouts: spawn groups, viewpoints, perimeter walls and the hills around small arenas.</summary>
    public abstract class LayoutBase : MapLayout
    {
        protected float HalfX => Def.PlayArea.x * 0.5f;
        protected float HalfZ => Def.PlayArea.y * 0.5f;

        /// <summary>Distance outside the play rectangle (0 inside).</summary>
        protected float Outside(float x, float z) => MapNoise.RectDistance(x, z, 0f, 0f, HalfX, HalfZ);

        /// <summary>
        /// Ground of a small arena: flat at <paramref name="floor"/> inside the play area, rising into rough hills beyond it (the view, and
        /// a natural edge behind the boundary walls).
        /// </summary>
        protected float ArenaGround(float x, float z, float floor, float hillHeight, float seed)
        {
            float o = Outside(x, z);
            float rise = MapNoise.Smooth(4f, 60f, o) * hillHeight;
            float rough = MapNoise.Fbm(x, z, 0.012f, 4, seed) * (2f + MapNoise.Smooth(0f, 50f, o) * 12f);
            float inner = MapNoise.Fbm(x, z, 0.05f, 2, seed + 9f) * 0.12f * (1f - MapNoise.Smooth(0f, 3f, o));
            return floor + rise + rough * MapNoise.Smooth(0f, 8f, o) + inner;
        }

        /// <summary>Paint for an arena's surroundings: grass, dirt on the lower slopes and rock where it is steep.</summary>
        protected static void NaturalPaint(float slope, float[] w, float dirt = 0.3f)
        {
            float rock = MapNoise.Smooth(28f, 42f, slope);
            w[(int)Surface.Rock] += rock;
            w[(int)Surface.Grass] += (1f - rock) * (1f - dirt);
            w[(int)Surface.Dirt] += (1f - rock) * dirt;
        }

        /// <summary>A team's spawn group: <paramref name="count"/> spots in a row across <paramref name="facingYaw"/>, low cover in front.</summary>
        protected void TeamSpawns(int team, Vector3 center, float facingYaw, int count, float spacing, bool cover = true)
        {
            Quaternion q = Quaternion.Euler(0f, facingYaw, 0f);
            Vector3 across = q * Vector3.right;
            for (int i = 0; i < count; i++)
            {
                float off = (i - (count - 1) * 0.5f) * spacing;
                Vector3 p = center + across * off;
                B.Anchor(MapAnchor.AnchorKind.TeamSpawn, B.OnGround(p.x, p.z), facingYaw, team);
            }
            // a team-coloured strip on the ground marks the spawn
            Vector3 g = B.OnGround(center.x, center.z, 0.03f);
            B.Box(null, g, new Vector3(count * spacing + 2f, 0.03f, 0.25f), q, team == 1 ? "team_a" : "team_b", null, false, "Spawn strip");
            if (!cover) return;
            Vector3 front = center + q * Vector3.forward * 4.5f;
            for (int i = -1; i <= 1; i += 2)
            {
                Vector3 p = front + across * (i * count * spacing * 0.3f);
                B.Barrier(B.OnGround(p.x, p.z), facingYaw + 90f);
            }
        }

        protected void Spawn(float x, float z, float yaw) => B.Anchor(MapAnchor.AnchorKind.Spawn, B.OnGround(x, z), yaw);

        /// <summary>A camera viewpoint for the map test: eye at <paramref name="eye"/> (height above the ground), looking at a point.</summary>
        protected void View(string label, float x, float z, float height, float tx, float tz, float targetHeight = 1.5f)
        {
            Vector3 eye = B.OnGround(x, z, height), target = B.OnGround(tx, tz, targetHeight);
            var a = B.Anchor(MapAnchor.AnchorKind.Viewpoint, eye, 0f, 0, label);
            a.transform.rotation = Quaternion.LookRotation(target - eye, Vector3.up);
        }

        /// <summary>A wall around the play rectangle, <paramref name="height"/> tall, with posts every few metres.</summary>
        protected void PerimeterWall(float height, string mat, float inset = 0.5f, Color? tint = null)
        {
            var g = B.Group("Perimeter");
            float hx = HalfX - inset, hz = HalfZ - inset;
            Vector2[] c = { new Vector2(-hx, -hz), new Vector2(hx, -hz), new Vector2(hx, hz), new Vector2(-hx, hz) };
            for (int i = 0; i < 4; i++)
            {
                Vector2 a = c[i], b = c[(i + 1) % 4];
                float len = Vector2.Distance(a, b);
                int segs = Mathf.CeilToInt(len / 6f);
                for (int s = 0; s < segs; s++)
                {
                    Vector2 p0 = Vector2.Lerp(a, b, (float)s / segs), p1 = Vector2.Lerp(a, b, (float)(s + 1) / segs);
                    float gy = Mathf.Min(B.Ground(p0.x, p0.y), B.Ground(p1.x, p1.y));
                    B.Segment(g, new Vector3(p0.x, 0, p0.y), new Vector3(p1.x, 0, p1.y), gy - 0.5f, height + 0.5f, 0.3f, mat, tint);
                    Vector3 post = B.OnGround(p0.x, p0.y);
                    B.Box(g, post + Vector3.up * (height * 0.5f + 0.1f), new Vector3(0.5f, height + 0.2f, 0.5f), "concrete_dark", null, false, "Post");
                }
            }
        }

        /// <summary>Scatters <paramref name="count"/> pieces of cover (crates, barriers, sandbags) in a rectangle, away from a list of keep-out circles.</summary>
        protected void ScatterCover(Rect area, int count, Transform parent, params Vector3[] keepOut)
        {
            int placed = 0;
            for (int tries = 0; tries < count * 6 && placed < count; tries++)
            {
                float x = B.Range(area.xMin, area.xMax), z = B.Range(area.yMin, area.yMax);
                bool blocked = false;
                foreach (var k in keepOut) if (new Vector2(x - k.x, z - k.z).magnitude < k.y) { blocked = true; break; }
                if (blocked) continue;
                Vector3 g = B.OnGround(x, z);
                float yaw = B.Range(0f, 360f);
                int kind = B.Range(0, 3);
                if (kind == 0) B.CrateStack(g, yaw, parent);
                else if (kind == 1) B.Barrier(g, yaw, parent);
                else
                {
                    Vector3 d = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * B.Range(1.6f, 3f);
                    B.Sandbags(g - d, g + d, parent);
                }
                placed++;
            }
        }
    }
}
