using System.IO;
using JUTPS;
using JUTPS.WeaponSystem;
using Railbound.UI;
using UnityEditor;
using UnityEngine;

namespace Railbound.EditorTools
{
    /// <summary>
    /// Menu "Railbound / Bake Weapon Catalog": reads the real stats of the game's weapon prefabs and writes them to
    /// Assets/Project/Resources/WeaponCatalog.asset, which the lobby's WEAPONS menu shows. Run it again after changing a weapon prefab
    /// (or after adding a weapon to the list below), so the menu always matches the game.
    /// </summary>
    public static class WeaponCatalogBaker
    {
        private const string Folder = "Assets/all_Projects/Demos/Demo Prefabs/Items/";
        private const string OutPath = "Assets/Project/Resources/" + WeaponCatalogAsset.ResourceName + ".asset";

        private struct Source { public string id, name, path, blurb; public WeaponCategory category; }

        // the weapons that can appear in a match (see BRMatchSetup.WeaponWeights) and the melee weapon every character carries
        private static readonly Source[] Sources =
        {
            new Source { id = "p226", name = "P226", path = "Weapons/Guns/P226.prefab", category = WeaponCategory.Pistol,
                blurb = "A semi-automatic sidearm. Light, quick to draw and dependable at close and medium range." },
            new Source { id = "p226dual", name = "P226 DUAL", path = "Weapons/Guns/P226 Dual.prefab", category = WeaponCategory.Pistol,
                blurb = "Two pistols firing together with automatic fire. A big magazine and a lot of kick." },
            new Source { id = "ump", name = "UMP", path = "Weapons/Guns/UMP.prefab", category = WeaponCategory.Smg,
                blurb = "A fast-firing submachine gun with a large magazine. Controllable and strong in close fights." },
            new Source { id = "benelli", name = "BENELLI M4", path = "Weapons/Guns/Benelli M4.prefab", category = WeaponCategory.Shotgun,
                blurb = "A pump-action shotgun that fires a spread of pellets. Devastating up close, slow to cycle." },
            new Source { id = "m82", name = "M82", path = "Weapons/Guns/SNIPER M82.prefab", category = WeaponCategory.Sniper,
                blurb = "A bolt-action sniper rifle with a scope. Slow and heavy, and it hits hard from far away." },
            new Source { id = "bat", name = "BASEBALL BAT", path = "Melee Weapons/BaseballBat.prefab", category = WeaponCategory.Melee,
                blurb = "A heavy melee weapon for when the ammunition runs out." },
        };

        [MenuItem("Railbound/Bake Weapon Catalog")]
        public static void Bake()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(OutPath));
            var asset = AssetDatabase.LoadAssetAtPath<WeaponCatalogAsset>(OutPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<WeaponCatalogAsset>();
                AssetDatabase.CreateAsset(asset, OutPath);
            }
            asset.weapons.Clear();

            foreach (var s in Sources)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + s.path);
                if (prefab == null) { Debug.LogWarning("[WeaponCatalog] Missing prefab " + Folder + s.path); continue; }

                var e = new WeaponCatalogAsset.Entry
                {
                    id = s.id, displayName = s.name, category = s.category, blurb = s.blurb, prefab = prefab,
                };

                var gun = prefab.GetComponent<Weapon>();
                var melee = prefab.GetComponent<MeleeWeapon>();
                if (gun != null)
                {
                    float perBullet = 0f;
                    if (gun.BulletPrefab != null)
                    {
                        var bullet = gun.BulletPrefab.GetComponent<Bullet>();
                        if (bullet != null) perBullet = bullet.BulletDamage;
                    }
                    bool shotgun = gun.FireMode == Weapon.WeaponFireMode.Shotgun;
                    e.pellets = shotgun ? Mathf.Max(1, gun.NumberOfShotgunBulletsPerShot) : 1;
                    e.damage = perBullet * e.pellets;
                    e.roundsPerMinute = gun.Fire_Rate > 0f ? 60f / gun.Fire_Rate : 0f;
                    e.magazine = gun.BulletsPerMagazine;
                    e.reserve = gun.TotalBullets;
                    e.recoil = gun.RecoilForce;
                    e.spreadPerShot = gun.LossOfAccuracyPerShot;
                    e.aimFov = gun.CameraFOV;
                    switch (gun.FireMode)
                    {
                        case Weapon.WeaponFireMode.Auto: e.fireMode = "AUTO"; break;
                        case Weapon.WeaponFireMode.SemiAuto: e.fireMode = "SEMI-AUTO"; break;
                        case Weapon.WeaponFireMode.BoltAction: e.fireMode = "BOLT-ACTION"; break;
                        default: e.fireMode = "PUMP"; break;
                    }
                    e.aimMode = gun.AimMode == Weapon.WeaponAimMode.Scope ? "SCOPE" : (gun.AimMode == Weapon.WeaponAimMode.CameraApproach ? "IRON SIGHTS" : "NONE");
                }
                else if (melee != null)
                {
                    e.isMelee = true;
                    var damager = prefab.GetComponentInChildren<Damager>(true);
                    e.damage = damager != null ? damager.Damage : 0f;
                    e.fireMode = "MELEE";
                    e.aimMode = "NONE";
                }
                else
                {
                    Debug.LogWarning("[WeaponCatalog] " + s.name + " has neither a Weapon nor a MeleeWeapon component; skipped.");
                    continue;
                }
                asset.weapons.Add(e);
                Debug.Log($"[WeaponCatalog] {e.displayName}: damage {e.damage} x{e.pellets}, {e.roundsPerMinute:0} rpm, mag {e.magazine}/{e.reserve}, recoil {e.recoil}, spread {e.spreadPerShot}, {e.fireMode}");
            }

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Debug.Log("[WeaponCatalog] Baked " + asset.weapons.Count + " weapons to " + OutPath);
        }
    }
}
