using UnityEngine;

namespace Project.Character
{
    /// <summary>The skin the local player equipped in the lobby (saved on the device; the lobby writes it, the match reads it).</summary>
    public static class CharacterSkins
    {
        private const string EquippedKey = "railbound.equipped.skin";

        /// <summary>Id of the skin the local player wears in a match. Always a usable skin of the catalog.</summary>
        public static string EquippedSkinId
        {
            get
            {
                string id = PlayerPrefs.GetString(EquippedKey, CharacterSkinCatalog.DefaultSkinId);
                // command line "-skin <id>" (test builds): wear this skin whatever the profile says
                string[] args = System.Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length - 1; i++) if (string.Equals(args[i], "-skin", System.StringComparison.OrdinalIgnoreCase)) id = args[i + 1];
                var catalog = CharacterSkinCatalog.Instance;
                var e = catalog != null ? catalog.Find(id) : null;
                return e != null && e.Usable ? id : CharacterSkinCatalog.DefaultSkinId;
            }
        }

        public static void SetEquipped(string skinId)
        {
            PlayerPrefs.SetString(EquippedKey, string.IsNullOrEmpty(skinId) ? CharacterSkinCatalog.DefaultSkinId : skinId);
            PlayerPrefs.Save();
        }
    }
}
