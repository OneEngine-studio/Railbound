using Unity.Netcode;
using UnityEngine;
using JUTPS;
using JUTPS.PhysicsScripts;

namespace Railbound.BR
{
    /// <summary>
    /// Server-authoritative health for a networked character (a human player or an AI bot).
    ///
    /// Before this, every peer kept its own <see cref="JUHealth"/>: the storm and the bots hurt the SERVER's copy of a player
    /// while the player's own client never lost a point, and a hit on a bot only hurt the shooter's private copy of it.
    ///
    ///  * Server: whatever happens to its copy of the body (storm, bot bullets, forwarded client hits) is published in
    ///    <c>netHealth</c>.
    ///  * Every other peer: applies the published value to its copy (so the owner of a player really loses health and dies,
    ///    and a bot dies on every screen). Damage that a client causes locally to ANY networked body (its own bullets, fall
    ///    damage) is forwarded to the server, which applies it for real. Healing by the owner of a player is reported too.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(JUHealth))]
    public class BRNetHealth : NetworkBehaviour
    {
        private const float MaxDamagePerReport = 250f;

        private readonly NetworkVariable<float> netHealth = new NetworkVariable<float>(
            -1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private JUHealth health;
        private float lastServerValue = -1f;
        private float nextHealReport;

        private void Awake()
        {
            health = GetComponent<JUHealth>();
        }

        public override void OnNetworkSpawn()
        {
            if (health == null) health = GetComponent<JUHealth>();
            JUHealth.OnAnyDamaged += HandleAnyDamaged;

            if (IsServer)
            {
                netHealth.Value = health.Health;
            }
            else
            {
                netHealth.OnValueChanged += HandleServerHealth;
                ApplyServerValue(netHealth.Value);
                // another player's copy on this machine: nobody switches its ragdoll on when it dies (bots do it in BRNetBot)
                if (!IsOwner && GetComponent<BRNetBot>() == null) { health.OnDeath.RemoveListener(RagdollRemotePlayer); health.OnDeath.AddListener(RagdollRemotePlayer); if (health.IsDead) RagdollRemotePlayer(); }
            }
        }

        private void RagdollRemotePlayer()
        {
            var ragdoll = GetComponent<AdvancedRagdollController>();
            if (ragdoll != null) ragdoll.State = AdvancedRagdollController.RagdollState.Ragdolled;
        }

        public override void OnNetworkDespawn()
        {
            if (health != null) health.OnDeath.RemoveListener(RagdollRemotePlayer);
            JUHealth.OnAnyDamaged -= HandleAnyDamaged;
            netHealth.OnValueChanged -= HandleServerHealth;
        }

        private void HandleServerHealth(float previous, float current)
        {
            ApplyServerValue(current);
        }

        private void ApplyServerValue(float value)
        {
            if (value < 0f || health == null) return;
            lastServerValue = value;
            if (Mathf.Abs(health.Health - value) > 0.01f)
            {
                health.Health = Mathf.Clamp(value, 0f, health.MaxHealth);
                health.CheckHealthState();
            }
        }

        // damage dealt on a non-server peer (JUHealth.DoDamage runs where the bullet is): the server must apply it
        private void HandleAnyDamaged(JUHealth h, float damage, Vector3 hitPosition)
        {
            if (h != health || !IsSpawned || IsServer || damage <= 0f) return;

            // who is shooting: bullets set JUHealth.PendingAttacker while they apply damage (the local player's character)
            ulong attackerId = 0;
            var attacker = JUHealth.PendingAttacker;
            if (attacker != null && attacker.TryGetComponent(out NetworkObject attackerObject) && attackerObject.IsSpawned)
                attackerId = attackerObject.NetworkObjectId;

            ReportDamageServerRpc(Mathf.Min(damage, MaxDamagePerReport), attackerId);
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReportDamageServerRpc(float damage, ulong attackerId, ServerRpcParams rpcParams = default)
        {
            if (health == null || health.IsDead) return;

            // the kill is credited to the shooter, but only to a character that the reporting client really owns
            GameObject attacker = null;
            if (attackerId != 0 && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(attackerId, out var attackerObject)
                && attackerObject.OwnerClientId == rpcParams.Receive.SenderClientId)
                attacker = attackerObject.gameObject;

            JUHealth.PendingAttacker = attacker;
            health.DoDamage(Mathf.Clamp(damage, 0f, MaxDamagePerReport));
            JUHealth.PendingAttacker = null;
        }

        [ServerRpc]
        private void ReportHealServerRpc(float newHealth)
        {
            if (health == null || health.IsDead) return;
            if (newHealth > health.Health) health.Health = Mathf.Min(newHealth, health.MaxHealth);
        }

        private void Update()
        {
            if (!IsSpawned || health == null) return;

            if (IsServer)
            {
                if (Mathf.Abs(health.Health - netHealth.Value) > 0.01f) netHealth.Value = health.Health;
                return;
            }

            // the owner heals itself locally (medkit): tell the server, or the next published value would undo it
            if (IsOwner && lastServerValue >= 0f && !health.IsDead && health.Health > lastServerValue + 0.5f && Time.unscaledTime >= nextHealReport)
            {
                nextHealReport = Time.unscaledTime + 0.25f;
                ReportHealServerRpc(health.Health);
            }
        }
    }
}
