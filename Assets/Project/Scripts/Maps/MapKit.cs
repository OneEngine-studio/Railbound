using System;
using System.Collections.Generic;
using UnityEngine;

namespace Railbound.Maps
{
    /// <summary>The ground types a map can paint on its terrain (the order of <see cref="MapKit.terrainLayers"/>).</summary>
    public enum Surface { Grass, Dirt, Rock, Sand, Gravel, Asphalt, Concrete, Road }

    /// <summary>
    /// The building blocks every map is made from: surface materials and terrain layers baked from the textures in git, and the prop
    /// prefabs (containers, wagons, locomotives, rails, rocks, bushes, conifers). Written by <c>Railbound &gt; Maps &gt; Bake Map Kit</c>
    /// to <c>Assets/Project/Resources/Maps/MapKit.asset</c>, so the maps never depend on the gitignored MAP-2 folder.
    /// </summary>
    public class MapKit : ScriptableObject
    {
        public const string ResourcePath = "Maps/MapKit";

        [Serializable]
        public class NamedMaterial
        {
            public string id;
            public Material material;
            [Tooltip("Metres of wall or floor one repeat of the texture covers.")]
            public float tileMeters = 2f;
        }

        public List<NamedMaterial> materials = new List<NamedMaterial>();
        [Tooltip("One layer per Surface, in the order of the enum.")]
        public TerrainLayer[] terrainLayers = Array.Empty<TerrainLayer>();
        public Material terrainMaterial;

        [Header("Props (shown as static meshes: their scripts and physics are not copied)")]
        public GameObject[] containers = Array.Empty<GameObject>();
        public GameObject[] wagons = Array.Empty<GameObject>();
        public GameObject[] locomotives = Array.Empty<GameObject>();
        public GameObject rail20;
        public GameObject rail100;
        public GameObject[] rocks = Array.Empty<GameObject>();
        public GameObject[] bushes = Array.Empty<GameObject>();
        public GameObject[] conifers = Array.Empty<GameObject>();

        private Dictionary<string, NamedMaterial> byId;

        public NamedMaterial Find(string id)
        {
            if (byId == null || byId.Count != materials.Count)
            {
                byId = new Dictionary<string, NamedMaterial>();
                foreach (var m in materials) if (m != null && !string.IsNullOrEmpty(m.id)) byId[m.id] = m;
            }
            return byId.TryGetValue(id, out var nm) ? nm : null;
        }

        public TerrainLayer Layer(Surface s)
        {
            int i = (int)s;
            return i < terrainLayers.Length ? terrainLayers[i] : null;
        }

        private static MapKit cached;

        /// <summary>The baked kit, or null when it has not been baked (maps then build with plain colours).</summary>
        public static MapKit Load()
        {
            if (cached == null) cached = Resources.Load<MapKit>(ResourcePath);
            return cached;
        }
    }
}
