using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Character
{
    /// <summary>
    /// The playable character models. Each entry points at a "skin" prefab: a copy of the MIL-R5 soldier hierarchy (same skeleton, same
    /// animator) with the model's meshes already moved onto that skeleton, made by the editor tool <c>CharacterSkinBaker</c>. Because every
    /// skin shares the MIL-R5 skeleton, the existing retargeter, hand and leg IK and ragdoll drive all of them unchanged.
    /// The asset lives in a Resources folder so it exists in every build (client, server and lobby).
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterSkinCatalog", menuName = "Railbound/Character Skin Catalog")]
    public class CharacterSkinCatalog : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [Tooltip("Stable id (saved in profiles and sent over the network). Never change it once players own the skin.")]
            public string id;
            public string displayName;
            public string roleCategory;
            [Tooltip("Skin prefab. Empty = not usable yet (for example the model has no skeleton): the entry is ignored everywhere.")]
            public GameObject prefab;
            [Tooltip("Coins to unlock it in the lobby store. 0 together with Owned By Default = the starting skin.")]
            public int priceCoins = 2500;
            public bool ownedByDefault;
            [Tooltip("May an AI bot wear it?")]
            public bool botEligible = true;
            [TextArea(1, 3)] public string note;

            public bool Usable => prefab != null;
        }

        public const string ResourcePath = "CharacterSkinCatalog";
        public const string DefaultSkinId = "player1";

        public List<Entry> entries = new List<Entry>();

        private static CharacterSkinCatalog cached;
        public static CharacterSkinCatalog Instance
        {
            get
            {
                if (cached == null) cached = Resources.Load<CharacterSkinCatalog>(ResourcePath);
                return cached;
            }
        }

        public Entry Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < entries.Count; i++) if (entries[i] != null && entries[i].id == id) return entries[i];
            return null;
        }

        /// <summary>The prefab of a skin, or the default skin's when the id is unknown or unusable.</summary>
        public GameObject PrefabOrDefault(string id)
        {
            var e = Find(id);
            if (e != null && e.Usable) return e.prefab;
            var d = Find(DefaultSkinId);
            return d != null ? d.prefab : null;
        }

        /// <summary>A random usable, bot-eligible skin id (the default skin if none).</summary>
        public string RandomBotSkin(System.Random rng)
        {
            var pool = new List<string>();
            foreach (var e in entries) if (e != null && e.Usable && e.botEligible) pool.Add(e.id);
            if (pool.Count == 0) return DefaultSkinId;
            return pool[rng.Next(pool.Count)];
        }
    }
}
