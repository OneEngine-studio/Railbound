using System.Collections.Generic;
using JUTPS;
using JUTPS.InventorySystem;
using JUTPS.ItemSystem;
using JUTPS.WeaponSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Railbound.BR
{
    /// <summary>
    /// A dead character's weapon becomes a pickup on the ground beside the body.
    ///
    /// Before, the weapon stayed in the ragdoll's hand and was only collectable through JU's corpse loot, which needs the player to stand
    /// within a metre of the hand: often under the body, inside a bush or across a slope, so it "sometimes" could not be taken, and the bot
    /// itself used to be destroyed a few seconds after it died. Now the server drops a weapon of the same kind as a world pickup beside the
    /// body and announces it (<see cref="BRLootSync"/>), so every player sees the same pickup and taking it hides it everywhere; the
    /// weapon in the dead hand is hidden on every peer.
    ///
    /// The pickups are copies of the world's own weapons of that kind (one that was taken and hidden still serves as the model). Nothing is
    /// created ahead of time: the match setup's own search for world weapons must not find extra copies.
    /// </summary>
    public static class WeaponDrop
    {
        /// <summary>A pickup of the weapon called <paramref name="itemName"/>, or null when the world has none of that kind.</summary>
        public static GameObject Spawn(string itemName, Vector3 position, Quaternion rotation)
        {
            var proto = Prototype(itemName);
            if (proto == null) return null;
            var go = Object.Instantiate(proto, position, rotation);
            SceneManager.MoveGameObjectToScene(go, proto.scene);
            go.SetActive(true);
            var rb = go.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            Railbound.AI.BattleRoyaleLootDropper.IgnoreNearbyBodies(go);
            return go;
        }

        // a world weapon of that kind (not one in somebody's hands); an inactive one (taken and hidden) is as good
        private static GameObject Prototype(string itemName)
        {
            if (string.IsNullOrEmpty(itemName)) return null;
            GameObject inactive = null;
            foreach (var w in Object.FindObjectsByType<Weapon>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (w == null || w.ItemName != itemName || w.GetComponentInParent<JUInventory>() != null) continue;
                if (w.gameObject.activeInHierarchy) return w.gameObject;
                if (inactive == null) inactive = w.gameObject;
            }
            return inactive;
        }

        /// <summary>Server: drops the weapon <paramref name="c"/> held as a pickup beside its body and tells every client.</summary>
        public static void ServerDrop(JUCharacterController c)
        {
            if (c == null) return;
            if (!PuppetPoseSync.TryGetHeld(c, out var weapon, out _, out _) || weapon == null) return;
            Vector3 at = GroundBeside(c.transform);
            var rot = Quaternion.Euler(0f, c.transform.eulerAngles.y + 70f, 90f);   // lying on its side
            var go = Spawn(weapon.ItemName, at, rot);
            if (go == null) { Debug.Log($"[WeaponDrop] {c.name}: no world copy of {weapon.ItemName} to drop."); return; }
            BRLootSync.ServerAnnounceWeaponDrop(go, weapon.ItemName);
            Debug.Log($"[WeaponDrop] {c.name} dropped {weapon.ItemName} at {at:F1}.");
        }

        /// <summary>Hides the weapons in a dead character's hands (they can no longer be shot, seen or looted from the hand).</summary>
        public static void HideHeld(GameObject character)
        {
            if (character == null) return;
            var inv = character.GetComponent<JUInventory>();
            if (inv == null) return;
            Hide(inv.HoldableItensRightHand);
            Hide(inv.HoldableItensLeftHand);
            inv.EnablePickup = false;
        }

        private static void Hide(JUHoldableItem[] items)
        {
            if (items == null) return;
            foreach (var it in items)
            {
                if (it == null || !it.gameObject.activeInHierarchy) continue;
                foreach (var r in it.GetComponentsInChildren<Renderer>()) r.enabled = false;
                foreach (var col in it.GetComponentsInChildren<Collider>()) col.enabled = false;
            }
        }

        // a spot on the ground beside the body, clear of it
        private static Vector3 GroundBeside(Transform body)
        {
            Vector3 from = body.position + body.right * 0.8f + Vector3.up * 1.5f;
            int mask = LayerMask.GetMask("Default", "Terrain", "Walls");
            if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, 4f, mask == 0 ? ~0 : mask, QueryTriggerInteraction.Ignore)) return hit.point + Vector3.up * 0.08f;
            return body.position + body.right * 0.8f + Vector3.up * 0.1f;
        }
    }
}
