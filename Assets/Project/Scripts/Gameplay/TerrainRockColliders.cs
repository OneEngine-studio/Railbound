using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Railbound.Gameplay
{
    /// <summary>
    /// Makes the rocks that a Terrain paints as "trees" solid. MAP-2 paints about 7,600 rocks that way (Rock_C_01, Rock_B_01,
    /// Rock_C_02, ...) and their prototypes have no collider (or only one on a child, which the TerrainCollider does not use),
    /// so players, bots, loot and bullets went straight through them. The map assets are not in git, so the fix lives here:
    /// when a scene loads, every rock instance of a prototype without a root collider gets a static MeshCollider with the
    /// prototype's own mesh. The same code runs on clients and on the dedicated server, so both see the same solid rocks.
    /// </summary>
    public static class TerrainRockColliders
    {
        private const string RootName = "[TerrainRockColliders]";
        private const int MaxColliderVertices = 3000;   // above this, use the next LOD (the collision shape does not need the detail)

        public static int LastBuiltCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Build(SceneManager.GetActiveScene());   // the first scene loaded before this hook existed
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { Build(scene); }

        /// <summary>Adds the colliders for every terrain of the scene (once per scene; safe to call again).</summary>
        public static int Build(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return 0;
            foreach (var root in scene.GetRootGameObjects()) if (root.name == RootName) return 0;   // already built

            float started = Time.realtimeSinceStartup;
            GameObject holder = null;
            int count = 0, terrains = 0;
            var shapes = new Dictionary<GameObject, Shape>();

            foreach (var terrain in Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))
            {
                if (terrain.gameObject.scene != scene || !terrain.isActiveAndEnabled) continue;
                var tc = terrain.GetComponent<TerrainCollider>();
                var data = terrain.terrainData;
                if (tc == null || !tc.enabled || data == null) continue;
                terrains++;

                var prototypes = data.treePrototypes;
                var perPrototype = new Shape[prototypes.Length];
                bool any = false;
                for (int i = 0; i < prototypes.Length; i++)
                {
                    var prefab = prototypes[i].prefab;
                    if (!NeedsCollider(prefab)) continue;
                    if (!shapes.TryGetValue(prefab, out var shape)) { shape = Shape.From(prefab); shapes[prefab] = shape; }
                    perPrototype[i] = shape;
                    any |= shape != null;
                }
                if (!any) continue;

                Vector3 origin = terrain.transform.position, size = data.size;
                foreach (var tree in data.treeInstances)
                {
                    if (tree.prototypeIndex < 0 || tree.prototypeIndex >= perPrototype.Length) continue;
                    var shape = perPrototype[tree.prototypeIndex];
                    if (shape == null) continue;
                    if (holder == null)
                    {
                        holder = new GameObject(RootName);
                        SceneManager.MoveGameObjectToScene(holder, scene);
                    }
                    var go = new GameObject(shape.Name);
                    go.layer = shape.Layer;
                    var t = go.transform;
                    t.SetParent(holder.transform, false);
                    // TreeInstance: normalised position on the terrain, rotation in radians around Y, width/height scale on top of the prefab's scale
                    t.SetPositionAndRotation(origin + Vector3.Scale(tree.position, size), Quaternion.Euler(0f, tree.rotation * Mathf.Rad2Deg, 0f));
                    t.localScale = Vector3.Scale(shape.RootScale, new Vector3(tree.widthScale, tree.heightScale, tree.widthScale));
                    var child = new GameObject("shape");
                    child.layer = shape.Layer;
                    child.transform.SetParent(t, false);
                    child.transform.localPosition = shape.LocalPosition;
                    child.transform.localRotation = shape.LocalRotation;
                    child.transform.localScale = shape.LocalScale;
                    child.AddComponent<MeshCollider>().sharedMesh = shape.Mesh;
                    count++;
                }
            }

            LastBuiltCount = count;
            if (count > 0)
                Debug.Log($"[TerrainRockColliders] {count} rock collider(s) on {terrains} terrain(s) of '{scene.name}' in {(Time.realtimeSinceStartup - started) * 1000f:F0} ms.");
            return count;
        }

        /// <summary>A rock prototype without a collider on its root (the TerrainCollider builds tree colliders from the root only).</summary>
        private static bool NeedsCollider(GameObject prefab)
        {
            if (prefab == null) return false;
            if (prefab.name.IndexOf("rock", System.StringComparison.OrdinalIgnoreCase) < 0) return false;   // bushes and foliage stay walk-through
            return prefab.GetComponent<Collider>() == null;
        }

        private sealed class Shape
        {
            public string Name;
            public int Layer;
            public Mesh Mesh;
            public Vector3 RootScale;
            public Vector3 LocalPosition;   // mesh object relative to the prefab root (root scale excluded)
            public Quaternion LocalRotation;
            public Vector3 LocalScale;

            public static Shape From(GameObject prefab)
            {
                MeshFilter mf = null;
                var lod = prefab.GetComponent<LODGroup>();
                if (lod != null)
                {
                    var lods = lod.GetLODs();
                    for (int i = 0; i < lods.Length && mf == null; i++)
                    {
                        foreach (var r in lods[i].renderers)
                        {
                            var f = r != null ? r.GetComponent<MeshFilter>() : null;
                            if (f == null || f.sharedMesh == null) continue;
                            mf = f;
                            break;
                        }
                        // the most detailed LOD can be needlessly heavy for collision: take the next one if it exists
                        if (mf != null && mf.sharedMesh.vertexCount > MaxColliderVertices && i + 1 < lods.Length)
                        {
                            foreach (var r in lods[i + 1].renderers)
                            {
                                var f = r != null ? r.GetComponent<MeshFilter>() : null;
                                if (f != null && f.sharedMesh != null) { mf = f; break; }
                            }
                        }
                    }
                }
                if (mf == null)
                {
                    foreach (var f in prefab.GetComponentsInChildren<MeshFilter>(true))
                        if (f.sharedMesh != null) { mf = f; break; }
                }
                if (mf == null || mf.sharedMesh == null) return null;
                if (!mf.sharedMesh.isReadable)
                {
                    Debug.LogWarning($"[TerrainRockColliders] '{prefab.name}': mesh '{mf.sharedMesh.name}' is not readable; its rocks stay without collider.");
                    return null;
                }

                // pose of the mesh object below the prefab root, without the root's own scale (that is applied per instance)
                var root = prefab.transform;
                Matrix4x4 rootNoScale = Matrix4x4.TRS(root.position, root.rotation, Vector3.one);
                Matrix4x4 rel = rootNoScale.inverse * mf.transform.localToWorldMatrix;
                Vector3 rs = root.localScale;
                // undo the root scale that localToWorld already contains, so it is applied once, per instance
                rel = Matrix4x4.Scale(new Vector3(1f / rs.x, 1f / rs.y, 1f / rs.z)) * rel;

                return new Shape
                {
                    Name = prefab.name + " (collider)",
                    Layer = prefab.layer,
                    Mesh = mf.sharedMesh,
                    RootScale = rs,
                    LocalPosition = rel.GetColumn(3),
                    LocalRotation = rel.rotation,
                    LocalScale = rel.lossyScale,
                };
            }
        }
    }
}
