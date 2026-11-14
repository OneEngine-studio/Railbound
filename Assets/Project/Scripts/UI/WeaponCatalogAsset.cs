using System;
using System.Collections.Generic;
using UnityEngine;

namespace Railbound.UI
{
    public enum WeaponCategory { Pistol, Smg, Shotgun, Sniper, Melee }

    /// <summary>
    /// The weapons of the game as the WEAPONS menu shows them: name, category, a short description, the prefab used for the 3D preview and the
    /// real stats read from that prefab by the editor tool "Railbound / Bake Weapon Catalog" (nothing here is typed in by hand, so the menu
    /// cannot disagree with the game). Loaded from Resources/WeaponCatalog.
    /// </summary>
    [CreateAssetMenu(menuName = "Railbound/Weapon Catalog", fileName = "WeaponCatalog")]
    public class WeaponCatalogAsset : ScriptableObject
    {
        public const string ResourceName = "WeaponCatalog";

        [Serializable]
        public class Entry
        {
            public string id;
            public string displayName;
            public WeaponCategory category;
            [TextArea(2, 4)] public string blurb;
            public GameObject prefab;          // used for the 3D preview only

            // stats baked from the prefab
            public bool isMelee;
            public float damage;               // per shot (all pellets of a shotgun shot together), or per melee hit
            public int pellets = 1;
            public float roundsPerMinute;
            public int magazine;
            public int reserve;
            public float recoil;               // Weapon.RecoilForce (higher = kicks more)
            public float spreadPerShot;        // Weapon.LossOfAccuracyPerShot (higher = loses accuracy faster)
            public float aimFov;               // camera FOV while aiming (lower = more zoom)
            public string fireMode;            // AUTO, SEMI-AUTO, BOLT-ACTION, PUMP, MELEE
            public string aimMode;             // IRON SIGHTS, SCOPE, NONE
        }

        public List<Entry> weapons = new List<Entry>();

        private static WeaponCatalogAsset cached;
        public static WeaponCatalogAsset Load()
        {
            if (cached == null) cached = Resources.Load<WeaponCatalogAsset>(ResourceName);
            return cached;
        }

        public static string CategoryName(WeaponCategory c)
        {
            switch (c)
            {
                case WeaponCategory.Pistol: return "PISTOLS";
                case WeaponCategory.Smg: return "SMGS";
                case WeaponCategory.Shotgun: return "SHOTGUNS";
                case WeaponCategory.Sniper: return "SNIPERS";
                default: return "MELEE";
            }
        }
    }
}
