using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Railbound.Maps
{
    /// <summary>
    /// The toolbox a <see cref="MapLayout"/> builds with: terrain, textured boxes (world-scaled UVs, real box colliders), static copies of
    /// prop prefabs, rocks and the gameplay markers. Everything is drawn from one seeded random generator, so the server and every
    /// client build exactly the same map.
    /// </summary>
    public partial class MapBuilder
    {
        public readonly MapDefinition Definition;
        public readonly MapKit Kit;
        public readonly Transform Root;
        public readonly System.Random Rng;
        public readonly bool Headless;          // dedicated server: colliders only matter, but renderers are cheap and kept
        public Terrain Terrain { get; private set; }

        public readonly List<MapAnchor> Anchors = new List<MapAnchor>();
        public int Boxes { get; private set; }
        public int Props { get; private set; }

        // what MAP-2 brought (its terrain's trees, grass and rocks look right in this HDRP setup); empty when MAP-2 is not loaded
        internal readonly List<GameObject> Map2Trees = new List<GameObject>();
        internal readonly List<GameObject> Map2Bushes = new List<GameObject>();
        internal readonly List<GameObject> Map2Rocks = new List<GameObject>();
        internal readonly List<DetailPrototype> Map2Grass = new List<DetailPrototype>();
        internal Material Map2TerrainMaterial;
        internal TerrainLayer[] Map2Layers;
        internal Terrain Map2Terrain;

        public MapBuilder(MapDefinition def, MapKit kit, Transform root)
        {
            Definition = def;
            Kit = kit;
            Root = root;
            Rng = new System.Random(def.Seed);
            Headless = Application.isBatchMode && SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null;
        }

        /// <summary>Removes an object now in the editor preview, at the end of the frame in play.</summary>
        internal static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
        }

        // ------------------------------------------------------------------ random
        public float Range(float a, float b) => a + (float)Rng.NextDouble() * (b - a);
        public int Range(int a, int bExclusive) => Rng.Next(a, bExclusive);
        public bool Chance(float p) => Rng.NextDouble() < p;
        public T Pick<T>(IList<T> list) => list == null || list.Count == 0 ? default : list[Rng.Next(list.Count)];

        // ------------------------------------------------------------------ ground
        /// <summary>The ground height at a world point (the map's terrain).</summary>
        public float Ground(float x, float z) => Terrain != null ? Terrain.SampleHeight(new Vector3(x, 0f, z)) + Terrain.transform.position.y : 0f;
        public Vector3 OnGround(float x, float z, float lift = 0f) => new Vector3(x, Ground(x, z) + lift, z);

        // ------------------------------------------------------------------ groups
        public Transform Group(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : Root, false);
            return go.transform;
        }

        // ------------------------------------------------------------------ materials
        private readonly Dictionary<string, Material> tinted = new Dictionary<string, Material>();
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>A kit material, optionally tinted (one shared instance per material and tint).</summary>
        public Material Mat(string id, Color? tint = null)
        {
            string key = tint.HasValue ? id + "#" + ColorUtility.ToHtmlStringRGB(tint.Value) : id;
            if (tinted.TryGetValue(key, out var cached)) return cached;
            var nm = Kit != null ? Kit.Find(id) : null;
            Material m;
            if (nm != null && nm.material != null)
            {
                if (tint.HasValue)
                {
                    m = new Material(nm.material) { name = nm.material.name + "_" + key };
                    Color c = nm.material.HasProperty(BaseColorId) ? nm.material.GetColor(BaseColorId) : Color.white;
                    m.SetColor(BaseColorId, c * tint.Value);
                }
                else m = nm.material;
            }
            else m = Fallback(id, tint ?? Color.white);
            tinted[key] = m;
            return m;
        }

        private Material fallbackBase;
        private Material Fallback(string id, Color tint)
        {
            if (fallbackBase == null)
            {
                var shader = Shader.Find("HDRP/Lit");
                if (shader == null) return null;
                fallbackBase = new Material(shader) { name = "MapFallback" };
            }
            var m = new Material(fallbackBase) { name = "MapFallback_" + id };
            m.SetColor(BaseColorId, tint);
            return m;
        }

        public float Tile(string id)
        {
            var nm = Kit != null ? Kit.Find(id) : null;
            return nm != null && nm.tileMeters > 0.01f ? nm.tileMeters : 2f;
        }

        // ------------------------------------------------------------------ boxes
        private readonly Dictionary<long, Mesh> boxMeshes = new Dictionary<long, Mesh>();

        /// <summary>
        /// A solid textured box: <paramref name="center"/> and <paramref name="size"/> in world metres (or local to
        /// <paramref name="parent"/>), turned by <paramref name="rotation"/>. The texture keeps its real size on every face.
        /// </summary>
        public GameObject Box(Transform parent, Vector3 center, Vector3 size, Quaternion rotation, string mat, Color? tint = null, bool collider = true, string name = null)
        {
            size = new Vector3(Mathf.Max(0.01f, Mathf.Abs(size.x)), Mathf.Max(0.01f, Mathf.Abs(size.y)), Mathf.Max(0.01f, Mathf.Abs(size.z)));
            var go = new GameObject(name ?? mat);
            go.transform.SetParent(parent != null ? parent : Root, false);
            go.transform.localPosition = center;
            go.transform.localRotation = rotation;
            go.AddComponent<MeshFilter>().sharedMesh = BoxMesh(size, Tile(mat));
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = Mat(mat, tint);
            if (collider) go.AddComponent<BoxCollider>().size = size;
            Boxes++;
            return go;
        }

        public GameObject Box(Transform parent, Vector3 center, Vector3 size, string mat, Color? tint = null, bool collider = true, string name = null)
            => Box(parent, center, size, Quaternion.identity, mat, tint, collider, name);

        /// <summary>A box between two points on the ground plane (a wall, a kerb, a rail), <paramref name="height"/> tall from <paramref name="baseY"/>.</summary>
        public GameObject Segment(Transform parent, Vector3 a, Vector3 b, float baseY, float height, float thickness, string mat, Color? tint = null, bool collider = true)
        {
            Vector3 d = b - a; d.y = 0f;
            float len = d.magnitude;
            if (len < 0.01f) return null;
            Vector3 mid = (a + b) * 0.5f;
            mid.y = baseY + height * 0.5f;
            return Box(parent, mid, new Vector3(thickness, height, len), Quaternion.LookRotation(d / len, Vector3.up), mat, tint, collider);
        }

        private Mesh BoxMesh(Vector3 s, float tile)
        {
            long key = ((long)Mathf.RoundToInt(s.x * 50f) * 73856093L) ^ ((long)Mathf.RoundToInt(s.y * 50f) * 19349663L) ^ ((long)Mathf.RoundToInt(s.z * 50f) * 83492791L) ^ ((long)Mathf.RoundToInt(tile * 10f) << 48);
            if (boxMeshes.TryGetValue(key, out var mesh)) return mesh;
            mesh = new Mesh { name = "MapBox" };
            Vector3 h = s * 0.5f;
            var v = new List<Vector3>(24); var n = new List<Vector3>(24); var uv = new List<Vector2>(24); var t = new List<int>(36);
            void Face(Vector3 normal, Vector3 right, Vector3 up, float w, float hgt)
            {
                Vector3 c = Vector3.Scale(normal, h);
                Vector3 rx = right * (w * 0.5f), uy = up * (hgt * 0.5f);
                int i = v.Count;
                v.Add(c - rx - uy); v.Add(c + rx - uy); v.Add(c + rx + uy); v.Add(c - rx + uy);
                for (int k = 0; k < 4; k++) n.Add(normal);
                float u = w / tile, vv = hgt / tile;
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(u, 0)); uv.Add(new Vector2(u, vv)); uv.Add(new Vector2(0, vv));
                t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2);
            }
            Face(Vector3.forward, Vector3.left, Vector3.up, s.x, s.y);
            Face(Vector3.back, Vector3.right, Vector3.up, s.x, s.y);
            Face(Vector3.right, Vector3.forward, Vector3.up, s.z, s.y);
            Face(Vector3.left, Vector3.back, Vector3.up, s.z, s.y);
            Face(Vector3.up, Vector3.right, Vector3.forward, s.x, s.z);
            Face(Vector3.down, Vector3.right, Vector3.back, s.x, s.z);
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            boxMeshes[key] = mesh;
            return mesh;
        }

        // ------------------------------------------------------------------ props
        private readonly Dictionary<GameObject, List<(Mesh mesh, Material[] mats, Matrix4x4 m)>> propParts = new Dictionary<GameObject, List<(Mesh, Material[], Matrix4x4)>>();
        private readonly Dictionary<GameObject, Bounds> propBounds = new Dictionary<GameObject, Bounds>();

        /// <summary>
        /// A static copy of a prop prefab (only its meshes: no scripts, no physics), standing at <paramref name="position"/> turned by
        /// <paramref name="yaw"/>, with one box collider around it. Returns null when the prefab is missing.
        /// </summary>
        public GameObject Prop(GameObject prefab, Vector3 position, float yaw, Transform parent = null, float scale = 1f, bool collider = true)
        {
            if (prefab == null) return null;
            if (!propParts.TryGetValue(prefab, out var parts)) parts = ReadProp(prefab);
            if (parts.Count == 0) return null;
            var go = new GameObject(prefab.name);
            go.transform.SetParent(parent != null ? parent : Root, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * scale;
            foreach (var (mesh, mats, m) in parts)
            {
                var part = new GameObject(mesh.name);
                part.transform.SetParent(go.transform, false);
                part.transform.localPosition = m.GetColumn(3);
                part.transform.localRotation = m.rotation;
                part.transform.localScale = m.lossyScale;
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                part.AddComponent<MeshRenderer>().sharedMaterials = mats;
            }
            if (collider && propBounds.TryGetValue(prefab, out var b))
            {
                var bc = go.AddComponent<BoxCollider>();
                bc.center = b.center;
                bc.size = b.size;
            }
            Props++;
            return go;
        }

        /// <summary>The size of a prop (its meshes' bounds in its own space), or zero.</summary>
        public Bounds PropSize(GameObject prefab)
        {
            if (prefab == null) return new Bounds();
            if (!propParts.ContainsKey(prefab)) ReadProp(prefab);
            return propBounds.TryGetValue(prefab, out var b) ? b : new Bounds();
        }

        private List<(Mesh, Material[], Matrix4x4)> ReadProp(GameObject prefab)
        {
            var parts = new List<(Mesh, Material[], Matrix4x4)>();
            Matrix4x4 toRoot = prefab.transform.worldToLocalMatrix;
            var skip = new HashSet<Renderer>();
            foreach (var lod in prefab.GetComponentsInChildren<LODGroup>(true))
            {
                var lods = lod.GetLODs();
                for (int i = 1; i < lods.Length; i++) foreach (var r in lods[i].renderers) if (r != null) skip.Add(r);
            }
            bool any = false;
            Bounds bounds = new Bounds();
            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (mf.sharedMesh == null || mr == null || !mr.enabled || skip.Contains(mr)) continue;
                if (!mf.gameObject.activeSelf) continue;
                Matrix4x4 m = toRoot * mf.transform.localToWorldMatrix;
                parts.Add((mf.sharedMesh, mr.sharedMaterials, m));
                Bounds mb = mf.sharedMesh.bounds;
                foreach (var corner in Corners(mb))
                {
                    Vector3 p = m.MultiplyPoint3x4(corner);
                    if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; } else bounds.Encapsulate(p);
                }
            }
            propParts[prefab] = parts;
            propBounds[prefab] = bounds;
            return parts;
        }

        private static IEnumerable<Vector3> Corners(Bounds b)
        {
            Vector3 mn = b.min, mx = b.max;
            for (int i = 0; i < 8; i++) yield return new Vector3((i & 1) == 0 ? mn.x : mx.x, (i & 2) == 0 ? mn.y : mx.y, (i & 4) == 0 ? mn.z : mx.z);
        }

        // ------------------------------------------------------------------ rocks
        /// <summary>A rock (MAP-2's rock models when the scene has them, otherwise the forest pack's), sunk a little into the ground.</summary>
        public GameObject Rock(Vector3 groundPoint, float scale, float yaw, Transform parent = null)
        {
            GameObject template = Map2Rocks.Count > 0 ? Pick(Map2Rocks) : (Kit != null ? Pick(Kit.rocks) : null);
            if (template == null)
            {
                float s = scale * 2.2f;
                return Box(parent, groundPoint + Vector3.up * (s * 0.3f), new Vector3(s, s * 0.8f, s * 1.1f), Quaternion.Euler(Range(-8f, 8f), yaw, Range(-8f, 8f)), "rock");
            }
            var go = Object.Instantiate(template, parent != null ? parent : Root);
            go.name = template.name;
            go.SetActive(true);
            go.transform.position = groundPoint + Vector3.down * (0.15f * scale);
            go.transform.rotation = Quaternion.Euler(Range(-6f, 6f), yaw, Range(-6f, 6f));
            go.transform.localScale = template.transform.lossyScale * scale;
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) Kill(mb);
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) Kill(rb);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Kill(c);
            // one box around the rock's first model (a mesh collider needs a readable mesh, which a build may not keep)
            Bounds local = new Bounds();
            bool any = false;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                Matrix4x4 m = go.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                foreach (var corner in Corners(mf.sharedMesh.bounds))
                {
                    Vector3 p = m.MultiplyPoint3x4(corner);
                    if (!any) { local = new Bounds(p, Vector3.zero); any = true; } else local.Encapsulate(p);
                }
                if (mf.GetComponentInParent<LODGroup>() != null) break;   // LOD0 is enough
            }
            if (any)
            {
                var bc = go.AddComponent<BoxCollider>();
                bc.center = local.center;
                bc.size = local.size * 0.85f;   // rocks are rounder than their box
            }
            Props++;
            return go;
        }

        // ------------------------------------------------------------------ gameplay markers
        public MapAnchor Anchor(MapAnchor.AnchorKind kind, Vector3 position, float yaw = 0f, int team = 0, string label = null, float radius = 0f)
        {
            var go = new GameObject("[Anchor] " + kind + (label != null ? " " + label : "") + (team > 0 ? " T" + team : ""));
            go.transform.SetParent(Root, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var a = go.AddComponent<MapAnchor>();
            a.Kind = kind; a.Team = team; a.Label = label; a.Radius = radius;
            Anchors.Add(a);
            return a;
        }

        /// <summary>A loot spot (the match setup puts part of the loot on these).</summary>
        public void Loot(Vector3 position)
        {
            var go = new GameObject("[Loot]");
            go.transform.SetParent(Root, false);
            go.transform.position = position;
            go.AddComponent<Railbound.BR.BRLootPoint>();
        }

        /// <summary>Invisible walls 40 m tall around the play area, so nobody walks off the map.</summary>
        public void Boundary(float margin = 1f)
        {
            var g = Group("Boundary");
            float hx = Definition.PlayArea.x * 0.5f + margin, hz = Definition.PlayArea.y * 0.5f + margin;
            const float H = 60f, T = 2f;
            void Wall(Vector3 c, Vector3 s)
            {
                var go = new GameObject("Edge");
                go.transform.SetParent(g, false);
                go.transform.position = c;
                go.AddComponent<BoxCollider>().size = s;
            }
            float y = 40f;
            Wall(new Vector3(0f, y, hz + T * 0.5f), new Vector3(hx * 2f + T * 2f, H * 2f, T));
            Wall(new Vector3(0f, y, -hz - T * 0.5f), new Vector3(hx * 2f + T * 2f, H * 2f, T));
            Wall(new Vector3(hx + T * 0.5f, y, 0f), new Vector3(T, H * 2f, hz * 2f));
            Wall(new Vector3(-hx - T * 0.5f, y, 0f), new Vector3(T, H * 2f, hz * 2f));
        }

        // ------------------------------------------------------------------ light
        /// <summary>Sun direction and colour for this map (the scene's own sun, sky and exposure are kept).</summary>
        public static void ApplyLook(MapDefinition def)
        {
            Light sun = null;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional && l.isActiveAndEnabled && (sun == null || l.intensity > sun.intensity)) sun = l;
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(def.SunElevation, def.SunAzimuth, 0f);
                sun.color = def.SunTint;
            }
            if (def.FogDistance > 0f)
            {
                foreach (var v in Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None))
                {
                    if (v.sharedProfile == null || !v.sharedProfile.Has<Fog>()) continue;
                    if (v.profile.TryGet<Fog>(out var fog))
                    {
                        fog.meanFreePath.overrideState = true;
                        fog.meanFreePath.value = def.FogDistance;
                    }
                }
            }
        }
    }
}
