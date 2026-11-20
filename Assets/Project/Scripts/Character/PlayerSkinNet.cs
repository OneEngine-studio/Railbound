using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Project.Character
{
    /// <summary>
    /// The model a human player wears. The owner publishes the id of the skin it equipped in the lobby; every peer puts that model on its copy
    /// of the character. Offline (single player) the equipped skin is applied locally.
    /// Which skins a player may wear is checked in the lobby store (owned or bought on the server); the game server does not check it in a match yet
    /// (cosmetic only, see docs/server-authoritative-economy.md, known gaps).
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerSkinNet : NetworkBehaviour
    {
        private readonly NetworkVariable<FixedString32Bytes> skinId = new NetworkVariable<FixedString32Bytes>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        // the operator's look on that model (tints, cap, headset), see OperatorLookApplier; empty = the model as authored
        private readonly NetworkVariable<FixedString64Bytes> lookCode = new NetworkVariable<FixedString64Bytes>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private string applied;
        private string appliedLook = "";

        private void Awake()
        {
            // the player prefab already carries the default (MIL-R5) model
            if (transform.Find(CharacterSkinApplier.SkinChildName) != null) applied = CharacterSkinCatalog.DefaultSkinId;
        }

        public override void OnNetworkSpawn()
        {
            skinId.OnValueChanged += HandleChanged;
            lookCode.OnValueChanged += HandleLookChanged;
            if (IsOwner)
            {
                skinId.Value = new FixedString32Bytes(CharacterSkins.EquippedSkinId);
                lookCode.Value = new FixedString64Bytes(CharacterSkins.EquippedLookCode);
            }
            Apply(skinId.Value.ToString());
            ApplyLook(lookCode.Value.ToString());
        }

        public override void OnNetworkDespawn()
        {
            skinId.OnValueChanged -= HandleChanged;
            lookCode.OnValueChanged -= HandleLookChanged;
        }

        private void HandleChanged(FixedString32Bytes previous, FixedString32Bytes current)
        {
            Apply(current.ToString());
            ApplyLook(lookCode.Value.ToString(), true);   // a new model starts untinted
        }

        private void HandleLookChanged(FixedString64Bytes previous, FixedString64Bytes current)
        {
            ApplyLook(current.ToString());
        }

        private void ApplyLook(string code, bool force = false)
        {
            if (CharacterSkinApplier.NoVisuals) return;
            if (!force && code == appliedLook) return;
            if (OperatorLookApplier.TryDecode(code, out var look)) OperatorLookApplier.ApplyToCharacter(gameObject, look);
            appliedLook = code;
        }

        private System.Collections.IEnumerator Start()
        {
            // single player: no network session will spawn this object
            yield return new WaitForSeconds(1.5f);
            if (IsSpawned) yield break;
            var nm = NetworkManager.Singleton;
            if (nm != null && (nm.IsListening || nm.IsConnectedClient)) yield break;
            Apply(CharacterSkins.EquippedSkinId);
            ApplyLook(CharacterSkins.EquippedLookCode, true);
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
