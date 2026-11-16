using System.Collections.Generic;
using Project.Character;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using JUTPS;
using JUTPS.InventorySystem;
using JUTPS.PhysicsScripts;

namespace Railbound.BR
{
    /// <summary>
    /// A bot of a networked match. The SERVER runs the bot (AI, physics, weapons, pickups); every client only shows it:
    /// position and animation come from NetworkTransform / NetworkAnimator, health from <see cref="BRNetHealth"/>, the weapon
    /// in the hand from <c>rightHandItem</c> here. Without this each client ran its own private copy of every bot.
    /// The bots are in-scene NetworkObjects, so they exist on every peer from the scene itself and are linked when the
    /// client connects.
    /// </summary>
    [DisallowMultipleComponent]
    public class BRNetBot : NetworkBehaviour
    {
        // brains and helpers that must run only where the bot is simulated (the server)
        private static readonly string[] ServerOnlyBehaviours =
        {
            "PatrolAI", "JUCharacterRLAgent", "DecisionRequester", "BehaviorParameters", "RayPerceptionSensorComponent3D",
            "AIMatchTracker", "BattleRoyaleLootDropper", "AutoDestroy"
        };

        // aim state of the bot (firing mode, reloading, look point), published by the server; clients pose their copy of the bot with it
        private readonly NetworkVariable<PuppetPose> pose = new NetworkVariable<PuppetPose>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private float nextPosePublish;
        private JUCharacterController puppet, controller;

        /// <summary>Muzzle flashes/gunshots this peer has shown for bots (test counter).</summary>
        public static int ShotEffectsShown;
        public static int BulletVisualsShown;

        private readonly NetworkVariable<bool> retired = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // the model this bot wears (id from the CharacterSkinCatalog): the server draws it at random, every peer shows it
        private readonly NetworkVariable<FixedString32Bytes> skinId = new NetworkVariable<FixedString32Bytes>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private string appliedSkin;

        // item in the right hand (JUInventory switch id, -1 = bare hands)
        private readonly NetworkVariable<int> rightHandItem = new NetworkVariable<int>(
            -1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private JUInventory inventory;
        private Rigidbody body;

        private void Awake()
        {
            inventory = GetComponent<JUInventory>();
            controller = GetComponent<JUCharacterController>();
            body = GetComponent<Rigidbody>();
        }

        public override void OnNetworkSpawn()
        {
            retired.OnValueChanged += HandleRetired;
            if (retired.Value) gameObject.SetActive(false);

            skinId.OnValueChanged += HandleSkinChanged;
            if (IsServer && skinId.Value.IsEmpty)
            {
                var catalog = CharacterSkinCatalog.Instance;
                skinId.Value = new FixedString32Bytes(catalog != null ? catalog.RandomBotSkin(new System.Random(System.Guid.NewGuid().GetHashCode())) : CharacterSkinCatalog.DefaultSkinId);
            }
            ApplySkin(skinId.Value.ToString());

            if (IsServer)
            {
                JUTPS.WeaponSystem.Weapon.OnAnyShot += HandleServerShot;
                JUTPS.WeaponSystem.Weapon.OnAnyBulletSpawned += HandleServerBullet;
                JUTPS.WeaponSystem.Weapon.OnAnyReload += HandleServerReload;
            }
            else
            {
                MakeClientProxy();
                rightHandItem.OnValueChanged += HandleItemChanged;
                ApplyItem(rightHandItem.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            JUTPS.WeaponSystem.Weapon.OnAnyShot -= HandleServerShot;
            JUTPS.WeaponSystem.Weapon.OnAnyBulletSpawned -= HandleServerBullet;
            JUTPS.WeaponSystem.Weapon.OnAnyReload -= HandleServerReload;
            retired.OnValueChanged -= HandleRetired;
            skinId.OnValueChanged -= HandleSkinChanged;
            rightHandItem.OnValueChanged -= HandleItemChanged;
            var hp = GetComponent<JUHealth>();
            if (hp != null) hp.OnDeath.RemoveListener(HandleProxyDeath);
        }

        /// <summary>The weapon in the bot's right hand on this peer (the controller's own field is not kept up to date on a client).</summary>
        public JUTPS.WeaponSystem.Weapon HeldWeapon
        {
            get
            {
                if (inventory == null || inventory.HoldableItensRightHand == null) return null;
                int id = IsSpawned && !IsServer ? rightHandItem.Value : inventory.CurrentRightHandItemID;
                if (id < 0) return null;
                foreach (var item in inventory.HoldableItensRightHand)
                    if (item != null && item.ItemSwitchID == id) return item as JUTPS.WeaponSystem.Weapon;
                return null;
            }
        }

        // On a client the character controller (which normally ragdolls the body on death) is switched off, so do it here.
        private void HandleProxyDeath()
        {
            var ragdoll = GetComponent<AdvancedRagdollController>();
            if (ragdoll != null) ragdoll.State = AdvancedRagdollController.RagdollState.Ragdolled;
        }

        private void MakeClientProxy()
        {
            var hp = GetComponent<JUHealth>();
            if (hp != null) { hp.OnDeath.RemoveListener(HandleProxyDeath); hp.OnDeath.AddListener(HandleProxyDeath); if (hp.IsDead) HandleProxyDeath(); }
            foreach (var b in GetComponents<Behaviour>())
            {
                if (b == null) continue;
                if (System.Array.IndexOf(ServerOnlyBehaviours, b.GetType().Name) >= 0) b.enabled = false;
            }
            // the controller stays on as a puppet (no AI, input, movement or physics) so the hands, spine and head are posed
            if (PuppetPoseSync.MakePuppet(controller)) puppet = controller;
            else if (controller != null) controller.enabled = false;
            if (body != null)
            {
                body.isKinematic = true;
                body.interpolation = RigidbodyInterpolation.None;   // NetworkTransform interpolates
            }
        }

        private void HandleSkinChanged(FixedString32Bytes previous, FixedString32Bytes current) { ApplySkin(current.ToString()); }

        private void ApplySkin(string id)
        {
            if (string.IsNullOrEmpty(id) || id == appliedSkin || CharacterSkinApplier.NoVisuals) return;
            var catalog = CharacterSkinCatalog.Instance;
            var prefab = catalog != null ? catalog.PrefabOrDefault(id) : null;
            if (prefab != null && CharacterSkinApplier.Apply(gameObject, prefab)) appliedSkin = id;
        }

        // single player (no network session spawns this bot): draw a random model locally
        private System.Collections.IEnumerator Start()
        {
            yield return new WaitForSeconds(2f);
            if (IsSpawned) yield break;
            var nm = NetworkManager.Singleton;
            if (nm != null && (nm.IsListening || nm.IsConnectedClient)) yield break;
            var catalog = CharacterSkinCatalog.Instance;
            if (catalog != null) ApplySkin(catalog.RandomBotSkin(new System.Random(GetInstanceID())));
        }

        private void HandleRetired(bool previous, bool current)
        {
            if (current) gameObject.SetActive(false);
        }

        // ---- dormant: an empty server does not simulate its bots at all (they cost about half the server's CPU while idle)
        private readonly List<Behaviour> dormantOff = new List<Behaviour>();
        private bool dormant, rbWasKinematic;

        /// <summary>
        /// Switches every script on the bot off (and freezes its body) until woken. Network components, health and the
        /// animator stay, so the bot keeps its place and still replicates to a client that joins meanwhile.
        /// </summary>
        public void SetDormant(bool value)
        {
            if (value == dormant) return;
            dormant = value;
            if (value)
            {
                dormantOff.Clear();
                foreach (var b in GetComponentsInChildren<Behaviour>(false))
                {
                    if (b == null || !b.enabled) continue;
                    if (b is NetworkBehaviour || b is Animator || b is JUHealth || b is BRParticipant) continue;
                    b.enabled = false;
                    dormantOff.Add(b);
                }
                if (body != null) { rbWasKinematic = body.isKinematic; body.isKinematic = true; }
            }
            else
            {
                foreach (var b in dormantOff) if (b != null) b.enabled = true;
                dormantOff.Clear();
                if (body != null) body.isKinematic = rbWasKinematic;
            }
        }

        /// <summary>Server only: this bot is not part of the match (more humans joined, so fewer bots are needed).</summary>
        public void Retire()
        {
            if (!IsServer || !IsSpawned) { gameObject.SetActive(false); return; }
            retired.Value = true;
            gameObject.SetActive(false);
        }

        // ---- shot effects: the bot's bullets exist on the server only; clients get the muzzle flash and the gunshot
        private void HandleServerShot(JUTPS.WeaponSystem.Weapon weapon)
        {
            if (weapon == null || !IsSpawned || !weapon.transform.IsChildOf(transform)) return;
            ShotClientRpc();
        }

        // the tracer and the impact: the bot's real bullet is on the server, clients get a visual copy that flies the same line
        private void HandleServerBullet(JUTPS.WeaponSystem.Weapon weapon, Vector3 start, Vector3 end, Quaternion rotation, Vector3 normal)
        {
            if (weapon == null || !IsSpawned || !weapon.transform.IsChildOf(transform)) return;
            BulletClientRpc(start, end, rotation, normal);
        }

        [ClientRpc(Delivery = RpcDelivery.Unreliable)]
        private void BulletClientRpc(Vector3 start, Vector3 end, Quaternion rotation, Vector3 normal)
        {
            if (IsServer) return;
            var w = HeldWeapon;
            if (w == null) return;
            BulletVisualsShown++;
            RemoteWeaponFx.Bullet(w, start, end, rotation, normal);
        }

        private void HandleServerReload(JUTPS.WeaponSystem.Weapon weapon)
        {
            if (weapon == null || !IsSpawned || !weapon.transform.IsChildOf(transform)) return;
            ReloadClientRpc();
        }

        [ClientRpc]
        private void ReloadClientRpc()
        {
            if (IsServer) return;
            RemoteWeaponFx.Reload(HeldWeapon);
        }

        [ClientRpc(Delivery = RpcDelivery.Unreliable)]
        private void ShotClientRpc()
        {
            if (IsServer) return;
            var w = HeldWeapon;
            if (w == null) return;
            ShotEffectsShown++;
            RemoteWeaponFx.Shot(w);   // same muzzle flash and gunshot as a remote player's shot
        }

        private void HandleItemChanged(int previous, int current) => ApplyItem(current);

        private void ApplyItem(int id)
        {
            if (inventory == null || inventory.HoldableItensRightHand == null) return;
            if (id >= 0)
            {
                foreach (var item in inventory.HoldableItensRightHand)
                {
                    if (item != null && item.ItemSwitchID == id)
                    {
                        item.Unlocked = true;
                        if (item.ItemQuantity <= 0) item.ItemQuantity = 1;
                        break;
                    }
                }
            }
            PuppetPoseSync.SwitchItem(controller, inventory, id);
        }

        private void Update()
        {
            if (puppet != null && IsSpawned && !IsServer) puppet.PuppetUpdate(pose.Value.Firing, pose.Value.Reloading, pose.Value.Look);
            if (IsServer && IsSpawned) PuppetPoseSync.Publish(controller, pose, ref nextPosePublish);
            if (!IsServer || !IsSpawned || inventory == null) return;
            if (rightHandItem.Value != inventory.CurrentRightHandItemID) rightHandItem.Value = inventory.CurrentRightHandItemID;
        }
    }
}
