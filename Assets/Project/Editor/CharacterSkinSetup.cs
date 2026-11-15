using Project.Character;
using UnityEditor;
using UnityEngine;

namespace Railbound.Editor
{
    /// <summary>Creates / refreshes the skin catalog asset (Assets/Project/Resources/CharacterSkinCatalog.asset) from the baked skins.</summary>
    public static class CharacterSkinSetup
    {
        private const string CatalogPath = "Assets/Project/Resources/CharacterSkinCatalog.asset";

        private static string Skin(string id) { string p = CharacterSkinBaker.OutputDir + "/Skin_" + id + ".prefab"; return System.IO.File.Exists(p) ? p : null; }

        private static GameObject Load(string path) { return AssetDatabase.LoadAssetAtPath<GameObject>(path); }

        [MenuItem("Tools/Railbound/Characters/Create or Refresh Skin Catalog")]
        public static string CreateOrRefreshCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CharacterSkinCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CharacterSkinCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            void Set(string id, string name, string role, string prefabPath, int price, bool owned, bool bots, string note)
            {
                var e = catalog.Find(id);
                if (e == null) { e = new CharacterSkinCatalog.Entry { id = id }; catalog.entries.Add(e); }
                e.displayName = name; e.roleCategory = role; e.priceCoins = price; e.ownedByDefault = owned; e.botEligible = bots; e.note = note;
                e.prefab = prefabPath != null ? Load(prefabPath) : null;
            }

            Set("player1", "MIL-R5 GHOST", "ASSAULT / SPEC-OPS", CharacterSkinBaker.MilPrefabPath, 0, true, true, "The starting model.");
            Set("striker", "STRIKER-14", "ASSAULT", Skin("striker"), 3000, false, true, "");
            Set("cammy", "CAMMY", "RECON", Skin("cammy"), 3500, false, true, "");
            Set("tactical", "TACTICAL", "SUPPORT", Skin("tactical"), 3500, false, true, "");
            Set("swat", "S.W.A.T. OPERATOR", "BREACHER / TACTICAL", Skin("swat"), 3000, false, true, "");

            // the glb characters (no skeleton) are gone
            catalog.entries.RemoveAll(e => e != null && (e.id == "federation" || e.id == "ghost" || e.id == "modern_soldier"));
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            int usable = 0; foreach (var e in catalog.entries) if (e.Usable) usable++;
            return "catalog: " + catalog.entries.Count + " entries, " + usable + " usable";
        }

        /// <summary>Deletes a baked skin: its prefab and its meshes.</summary>
        public static string DeleteBakedSkin(string id)
        {
            int n = 0;
            if (AssetDatabase.DeleteAsset(CharacterSkinBaker.OutputDir + "/Skin_" + id + ".prefab")) n++;
            foreach (var guid in AssetDatabase.FindAssets(id + "_ t:Mesh", new[] { CharacterSkinBaker.OutputDir + "/Meshes" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileName(path).StartsWith(id + "_") && AssetDatabase.DeleteAsset(path)) n++;
            }
            return "deleted " + n + " assets of skin " + id;
        }

        public static string AddSkinNetToPlayerPrefab()
        {
            const string pp = "Assets/SandBox/Networking/Prefabs/TPS Character.prefab";
            var root = PrefabUtility.LoadPrefabContents(pp);
            string r = "already there";
            bool changed = false;
            if (root.GetComponent<PlayerSkinNet>() == null) { root.AddComponent<PlayerSkinNet>(); changed = true; }
            if (root.GetComponent<Railbound.BR.PlayerCombatNet>() == null) { root.AddComponent<Railbound.BR.PlayerCombatNet>(); changed = true; }
            if (changed) { PrefabUtility.SaveAsPrefabAsset(root, pp); r = "added"; }
            PrefabUtility.UnloadPrefabContents(root);
            return "PlayerSkinNet on player prefab: " + r;
        }
    }
}
