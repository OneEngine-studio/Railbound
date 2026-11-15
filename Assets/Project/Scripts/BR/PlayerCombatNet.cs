using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using JUTPS.InventorySystem;
using JUTPS.WeaponSystem;

namespace Railbound.BR
{
    /// <summary>
    /// Makes a player's gunfire and reloading visible and audible to everybody else, and audible to the bots.
    ///
    /// A player fires on its own machine (the bullet and the damage are local to the owner, the damage is forwarded by BRNetHealth).
    /// The owner reports each shot to the server; the server lets the bots hear it (the AI sound sensor only ran on the shooter's machine
    /// before, so bots never reacted to a player's shots) and sends it on to every other client, which plays the muzzle flash, the gunshot,
    /// the tracer and the impact, and the reload sound on its copy of the player. Shots use unreliable delivery: a lost packet is one
    /// missing flash, never a stall.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerCombatNet : NetworkBehaviour
    {
        private JUInventory inventory;

        // test counters: what this peer sent as the shooter, relayed as the server, or received as a bystander (PLAYER shots only)
        public static int SentShots, SentBullets, SentReloads, RelayedShots, ReceivedShots, ReceivedBullets, ReceivedReloads;

        private void Awake()
        {
            inventory = GetComponent<JUInventory>();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) return;
            Weapon.OnAnyShot += OwnerShot;
            Weapon.OnAnyBulletSpawned += OwnerBullet;
            Weapon.OnAnyReload += OwnerReload;
        }

        public override void OnNetworkDespawn()
        {
            Weapon.OnAnyShot -= OwnerShot;
            Weapon.OnAnyBulletSpawned -= OwnerBullet;
            Weapon.OnAnyReload -= OwnerReload;
        }

        private bool Mine(Weapon w) { return w != null && IsSpawned && IsOwner && w.transform.IsChildOf(transform); }

        private void OwnerShot(Weapon w) { if (Mine(w)) { SentShots++; FireServerRpc(); } }
        private void OwnerBullet(Weapon w, Vector3 start, Vector3 end, Quaternion rot, Vector3 normal) { if (Mine(w)) { SentBullets++; BulletServerRpc(start, end, rot, normal); } }
        private void OwnerReload(Weapon w) { if (Mine(w)) { SentReloads++; ReloadServerRpc(); } }

        // everybody except the shooter (it already played its own shot) and except nobody else: a dedicated server has no screen
        private ClientRpcParams ToOthers()
        {
            var ids = new List<ulong>();
            foreach (var id in NetworkManager.ConnectedClientsIds) if (id != OwnerClientId) ids.Add(id);
            return new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = ids } };
        }

        // ------------------------------------------------------------------ shot
        [ServerRpc(Delivery = RpcDelivery.Unreliable)]
        private void FireServerRpc()
        {
            RelayedShots++;
            var w = RemoteWeaponFx.Held(inventory);
            Vector3 at = w != null && w.Shoot_Position != null ? w.Shoot_Position.position : transform.position;
            // the bots hear it (this normally runs only inside Weapon.Shot on the shooter's machine)
            Project.AI.Sensory.AISoundSensorySystem.RegisterGunshot(at, gameObject);
            FireClientRpc(ToOthers());
        }

        [ClientRpc(Delivery = RpcDelivery.Unreliable)]
        private void FireClientRpc(ClientRpcParams rpcParams = default)
        {
            if (IsOwner) return;
            ReceivedShots++;
            RemoteWeaponFx.Shot(RemoteWeaponFx.Held(inventory));
        }

        // ------------------------------------------------------------------ tracer and impact
        [ServerRpc(Delivery = RpcDelivery.Unreliable)]
        private void BulletServerRpc(Vector3 start, Vector3 end, Quaternion rotation, Vector3 normal)
        {
            BulletClientRpc(start, end, rotation, normal, ToOthers());
        }

        [ClientRpc(Delivery = RpcDelivery.Unreliable)]
        private void BulletClientRpc(Vector3 start, Vector3 end, Quaternion rotation, Vector3 normal, ClientRpcParams rpcParams = default)
        {
            if (IsOwner) return;
            ReceivedBullets++;
            RemoteWeaponFx.Bullet(RemoteWeaponFx.Held(inventory), start, end, rotation, normal);
        }

        // ------------------------------------------------------------------ reload
        [ServerRpc]
        private void ReloadServerRpc()
        {
            ReloadClientRpc(ToOthers());
        }

        [ClientRpc]
        private void ReloadClientRpc(ClientRpcParams rpcParams = default)
        {
            if (IsOwner) return;
            ReceivedReloads++;
            RemoteWeaponFx.Reload(RemoteWeaponFx.Held(inventory));
        }
    }
}
