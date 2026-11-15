using UnityEngine;
using JUTPS.InventorySystem;
using JUTPS.WeaponSystem;

namespace Railbound.BR
{
    /// <summary>
    /// What a peer shows when ANOTHER character fires or reloads: muzzle flash, gunshot, the tracer and its impact, the reload sound.
    /// The real shot (bullet, damage, ammo) happens on the shooter's machine (a player) or on the server (a bot); these are only the
    /// visible and audible parts, played on every other peer. Nothing here deals damage.
    /// </summary>
    public static class RemoteWeaponFx
    {
        // test counters (BRNetDebug prints them)
        public static int ShotsShown, BulletsShown, ReloadsShown;
        public static event System.Action<Weapon> ShotShownEvent;   // test aid: BRNetDebug takes a screenshot

        /// <summary>The weapon in the right hand of a character, from the item id the network keeps in sync (the controller's own field is not).</summary>
        public static Weapon Held(JUInventory inventory)
        {
            if (inventory == null || inventory.HoldableItensRightHand == null) return null;
            int id = inventory.CurrentRightHandItemID;
            if (id < 0) return null;
            foreach (var item in inventory.HoldableItensRightHand)
                if (item != null && item.ItemSwitchID == id) return item as Weapon;
            return null;
        }

        public static void Shot(Weapon w)
        {
            if (w == null || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            ShotsShown++;
            ShotShownEvent?.Invoke(w);
            Vector3 muzzle = w.Shoot_Position != null ? w.Shoot_Position.position : w.transform.position;
            if (w.MuzzleFlashParticlePrefab != null)
            {
                var flash = Object.Instantiate(w.MuzzleFlashParticlePrefab, muzzle, w.Shoot_Position != null ? w.Shoot_Position.rotation : w.transform.rotation, w.transform);
                Object.Destroy(flash, 2f);
                if (ShotsShown <= 3)
                {
                    var ps = flash.GetComponentInChildren<ParticleSystem>();
                    Debug.Log("[RemoteWeaponFx] shot " + ShotsShown + ": weapon=" + w.ItemName + " active=" + w.gameObject.activeInHierarchy + " flash=" + flash.name + " activeSelf=" + flash.activeInHierarchy
                        + " ps=" + (ps != null ? ps.isPlaying + "/dur=" + ps.main.duration.ToString("F2") + "/life=" + ps.main.startLifetime.constant.ToString("F2") + "/size=" + ps.main.startSize.constant.ToString("F2") : "none")
                        + " renderers=" + flash.GetComponentsInChildren<Renderer>().Length + " muzzle=" + muzzle.ToString("F1"));
                }
            }
            if (w.ShootAudio != null)
                Railbound.Audio.WeaponShotAudio.PlayShot(w.ShootAudio, muzzle, Random.Range(0.92f, 1.08f), w.ItemName);
        }

        public static void Bullet(Weapon w, Vector3 start, Vector3 end, Quaternion rotation, Vector3 normal)
        {
            if (w == null || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            BulletsShown++;
            BRBulletVisual.Spawn(w.BulletPrefab, start, end, rotation, normal);
        }

        public static void Reload(Weapon w)
        {
            if (w == null || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            ReloadsShown++;
            w.PlayReloadAudio();
        }
    }
}
