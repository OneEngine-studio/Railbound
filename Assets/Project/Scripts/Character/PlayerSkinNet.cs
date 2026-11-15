using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Project.Character
{
    /// <summary>
    /// The model a human player wears. The owner publishes the id of the skin it equipped in the lobby; every peer puts that model on its copy
    /// of the character. Offline (single player) the equipped skin is applied locally.
    /// Which skins a player may wear is checked in the lobby store (owned or bought); the server does not check it yet.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerSkinNet : NetworkBehaviour
    {
        private readonly NetworkVariable<FixedString32Bytes> skinId = new NetworkVariable<FixedString32Bytes>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private string applied;

        private void Awake()
        {
            // the player prefab already carries the default (MIL-R5) model
            if (transform.Find(CharacterSkinApplier.SkinChildName) != null) applied = CharacterSkinCatalog.DefaultSkinId;
        }

        public override void OnNetworkSpawn()
        {
            skinId.OnValueChanged += HandleChanged;
            if (IsOwner) skinId.Value = new FixedString32Bytes(CharacterSkins.EquippedSkinId);
            Apply(skinId.Value.ToString());
        }

        public override void OnNetworkDespawn()
        {
            skinId.OnValueChanged -= HandleChanged;
        }

        private void HandleChanged(FixedString32Bytes previous, FixedString32Bytes current)
        {
            Apply(current.ToString());
        }

        private System.Collections.IEnumerator Start()
        {
            // single player: no network session will spawn this object
            yield return new WaitForSeconds(1.5f);
            if (IsSpawned) yield break;
            var nm = NetworkManager.Singleton;
            if (nm != null && (nm.IsListening || nm.IsConnectedClient)) yield break;
            Apply(CharacterSkins.EquippedSkinId);
        }

        private void Apply(string id)
        {
            if (string.IsNullOrEmpty(id) || id == applied) return;
            var catalog = CharacterSkinCatalog.Instance;
            if (catalog == null) return;
            var prefab = catalog.PrefabOrDefault(id);
            if (prefab == null) return;
            if (CharacterSkinApplier.Apply(gameObject, prefab)) applied = id;
        }
    }
}
