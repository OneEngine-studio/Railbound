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

        // a copy of a body on this machine (not the server's, not the owner's): how it moves and where this machine last shot it
        private const float ResyncSeconds = 1.2f;        // a local hit the server did not confirm is undone after this
        private Vector3 lastPos, velocity;
        private bool hasLastPos, corpse;
        private float lastLocalDamageAt = -99f, lastShotAt = -99f;
        private Vector3 lastShotPoint, lastShotDir;

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
                health.OnTakeDamage -= ServerDamaged;
                health.OnTakeDamage += ServerDamaged;
                // the dead character's weapon becomes a pickup beside the body, the same on every peer
                health.OnDeath.RemoveListener(ServerDeath);
                health.OnDeath.AddListener(ServerDeath);
            }
            else
            {
                netHealth.OnValueChanged += HandleServerHealth;
                ApplyServerValue(netHealth.Value);
                // another player's or a bot's copy on this machine: its character controller does not run the death, so do it here
                if (!IsOwner) { health.OnDeath.RemoveListener(HandleCopyDeath); health.OnDeath.AddListener(HandleCopyDeath); if (health.IsDead) HandleCopyDeath(); }
            }
            // on every peer the weapon in the dead hand is hidden (the pickup beside the body is the one to take)
            health.OnDeath.RemoveListener(HideDeadHandWeapon);
            health.OnDeath.AddListener(HideDeadHandWeapon);
        }

        private void HandleCopyDeath()
        {
            if (corpse) return;
            corpse = true;
            bool shot = Time.time - lastShotAt < 1.5f;   // this machine fired the killing shot (or one just before it)
            CorpseRagdoll.Begin(gameObject, velocity, shot, lastShotPoint, lastShotDir);
        }

        private void ServerDeath() { WeaponDrop.ServerDrop(GetComponent<JUTPS.JUCharacterController>()); }
        private void HideDeadHandWeapon() { WeaponDrop.HideHeld(gameObject); }

        // ------------------------------------------------------------------ vehicle hits (knockdown)
        /// <summary>Who runs this body's physics: the server for a bot, the owner for a player.</summary>
        private bool SimulatesBody => GetComponent<BRNetBot>() != null ? IsServer : IsOwner;
        private const float MaxImpactSpeed = 30f;
        private bool knockedDown;

        /// <summary>
        /// A vehicle hit this body (called on the driver's machine, before the damage is reported, so the throw is in place when a death
        /// arrives). The server passes it to every peer: the one that simulates the body throws it for real and lets it get up, the
        /// others throw their copies and wait for it.
        /// </summary>
        public void RequestImpact(Vector3 velocity, Vector3 point)
        {
            if (!IsSpawned) return;
            velocity = Vector3.ClampMagnitude(velocity, MaxImpactSpeed);
            if (IsServer) { ImpactClientRpc(velocity, point); return; }
            // the driver sees the hit at once: its copy is thrown now, not a round trip later (meanwhile the car, which no longer collides
            // with the body, would drive through it); the echo from the server is then skipped here
            if (!SimulatesBody && health != null)
            {
                CorpseRagdoll.Knockdown(gameObject, velocity, point, false);
                knockedDown = true;
                predictedImpactAt = Time.time;
                Debug.Log($"[Impact] {name} thrown at {velocity.magnitude:0.0} m/s (this driver's copy, at once).");
            }
            ImpactServerRpc(velocity, point);
        }

        private float predictedImpactAt = -99f;

        [ServerRpc(RequireOwnership = false)]
        private void ImpactServerRpc(Vector3 velocity, Vector3 point)
        {
            if (health == null) return;
            ImpactClientRpc(Vector3.ClampMagnitude(velocity, MaxImpactSpeed), point);
        }

        [ClientRpc]
        private void ImpactClientRpc(Vector3 velocity, Vector3 point)
        {
            if (health == null) return;
            bool sim = SimulatesBody;
            if (!sim && Time.time - predictedImpactAt < 2f) { predictedImpactAt = -99f; return; }   // this machine threw it already
            Debug.Log($"[Impact] {name} thrown at {velocity.magnitude:0.0} m/s ({(sim ? "simulating" : "copy")}{(health.IsDead ? ", dead" : "")}).");
            if (!sim) { CorpseRagdoll.Knockdown(gameObject, velocity, point, false); knockedDown = true; return; }
            CorpseRagdoll.Knockdown(gameObject, velocity, point, true);
            if (!health.IsDead) { knockedDown = true; StopCoroutine(nameof(WatchGetUp)); StartCoroutine(nameof(WatchGetUp)); }
        }

        // the simulating peer: once JU has the body standing again (and it is still alive), every copy gets up too
        private System.Collections.IEnumerator WatchGetUp()
        {
            var ragdoll = GetComponent<AdvancedRagdollController>();
            float t = 0f;
            yield return new WaitForSeconds(0.3f);
            while (ragdoll != null && ragdoll.State != AdvancedRagdollController.RagdollState.Animated && t < 12f) { t += Time.deltaTime; yield return null; }
            if (health == null || health.IsDead) yield break;
            if (ragdoll != null && ragdoll.State != AdvancedRagdollController.RagdollState.Animated) ragdoll.State = AdvancedRagdollController.RagdollState.WaitStablePosition;
            knockedDown = false;
            if (IsServer) RecoveredClientRpc(); else RecoveredServerRpc();
        }

        [ServerRpc]
        private void RecoveredServerRpc() { RecoveredClientRpc(); }

        [ClientRpc]
        private void RecoveredClientRpc()
        {
            if (SimulatesBody || health == null || health.IsDead) return;
            knockedDown = false;
            CorpseRagdoll.Recover(gameObject);
        }

        public override void OnNetworkDespawn()
        {
            if (health != null) { health.OnDeath.RemoveListener(HandleCopyDeath); health.OnDeath.RemoveListener(ServerDeath); health.OnDeath.RemoveListener(HideDeadHandWeapon); }
            JUHealth.OnAnyDamaged -= HandleAnyDamaged;
            netHealth.OnValueChanged -= HandleServerHealth;
            if (health != null) health.OnTakeDamage -= ServerDamaged;
        }

        // ------------------------------------------------------------------ hit feedback
        // the server applies every hit; the player who was hit only sees his health drop. Tell him who hit him and from where,
        // so his camera jolts and the hit arc points at the shooter (Railbound.Gameplay.HitFeedback).
        private ClientRpcParams ownerOnly;

        private void ServerDamaged(float damage, Vector3 hitPosition)
        {
            if (!IsSpawned || damage <= 0f || GetComponent<BRNetBot>() != null) return;
            bool known = health.LastAttacker != null && health.LastAttacker != gameObject && Time.time - health.LastAttackTime < 0.05f;
            Vector3 from = known ? health.LastAttacker.transform.position + Vector3.up * 1.4f : hitPosition;
            if (OwnerClientId == NetworkManager.ServerClientId)
            {
                if (IsOwner) Railbound.Gameplay.HitFeedback.Report(from, known, damage);   // a host's own player
                return;
            }
            if (ownerOnly.Send.TargetClientIds == null || ownerOnly.Send.TargetClientIds.Count == 0 || ownerOnly.Send.TargetClientIds[0] != OwnerClientId)
                ownerOnly = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } } };
            HitClientRpc(from, known, damage, ownerOnly);
        }

        [ClientRpc]
        private void HitClientRpc(Vector3 from, bool known, float damage, ClientRpcParams rpcParams = default)
        {
            if (!IsOwner) return;
            Railbound.Gameplay.HitFeedback.Report(from, known, damage);
        }

        private void HandleServerHealth(float previous, float current)
        {
            ApplyServerValue(current);
        }

        private void ApplyServerValue(float value)
        {
            if (value < 0f || health == null) return;
            lastServerValue = value;
            // the server brought the body back (a new round): it is not a corpse any more
            if (corpse && value > 0f) { corpse = false; CorpseRagdoll.End(gameObject); }
            // a server 0 always ends a copy that is not dead yet (it may hold 0.01 from a local hit waiting for this)
            if (Mathf.Abs(health.Health - value) > 0.01f || (value <= 0f && !health.IsDead))
            {
                health.Health = Mathf.Clamp(value, 0f, health.MaxHealth);
                health.CheckHealthState();
            }
        }

        // damage dealt on a non-server peer (JUHealth.DoDamage runs where the bullet is): the server must apply it
        private void HandleAnyDamaged(JUHealth h, float damage, Vector3 hitPosition)
        {
            if (h != health || !IsSpawned || IsServer || damage <= 0f) return;

            // only the server decides that a body dies: a copy that died here first and was then corrected by the server used to
            // die, come back and die again (the corpse stood up and fell). The hit still shows; the server's value ends it.
            if (health.Health <= 0f && lastServerValue > 0f) health.Health = 0.01f;
            lastLocalDamageAt = Time.time;
            if (!IsOwner)
            {
                var shooter = JUHealth.PendingAttacker;
                Vector3 from = shooter != null ? shooter.transform.position + Vector3.up * 1.4f : (Camera.main != null ? Camera.main.transform.position : hitPosition - transform.forward);
                lastShotAt = Time.time; lastShotPoint = hitPosition; lastShotDir = hitPosition - from; lastShotDir.y *= 0.35f;
            }

            // who is shooting: bullets set JUHealth.PendingAttacker while they apply damage (the local player's character)
            ulong attackerId = 0;
            var attacker = JUHealth.PendingAttacker;
            if (attacker != null && attacker.TryGetComponent(out NetworkObject attackerObject) && attackerObject.IsSpawned)
                attackerId = attackerObject.NetworkObjectId;

            // and with what: the weapon in the shooter's hand (weapon mastery is paid for the weapon that got the kill)
            string weapon = attacker != null ? Railbound.Gameplay.WeaponIds.HeldBy(attacker.GetComponent<JUTPS.JUCharacterController>()) : "";
            ReportDamageServerRpc(Mathf.Min(damage, MaxDamagePerReport), attackerId, new Unity.Collections.FixedString32Bytes(weapon));
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReportDamageServerRpc(float damage, ulong attackerId, Unity.Collections.FixedString32Bytes weapon, ServerRpcParams rpcParams = default)
        {
            if (health == null || health.IsDead) return;

            // the kill is credited to the shooter, but only to a character that the reporting client really owns
            GameObject attacker = null;
            if (attackerId != 0 && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(attackerId, out var attackerObject)
                && attackerObject.OwnerClientId == rpcParams.Receive.SenderClientId)
                attacker = attackerObject.gameObject;

            JUHealth.PendingAttacker = attacker;
            // only a known weapon id is kept (Nakama ignores anything else anyway)
            string weaponId = Railbound.Gameplay.WeaponIds.IsKnown(weapon.ToString()) ? weapon.ToString() : "";
            JUHealth.PendingAttackWeapon = attacker != null ? weaponId : "";
            health.DoDamage(Mathf.Clamp(damage, 0f, MaxDamagePerReport));
            JUHealth.PendingAttacker = null;
            JUHealth.PendingAttackWeapon = "";
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

            // a copy's speed, for a death that keeps the body's momentum
            if (!IsOwner && !corpse)
            {
                Vector3 p = transform.position;
                if (hasLastPos && Time.deltaTime > 0f) velocity = Vector3.Lerp(velocity, (p - lastPos) / Time.deltaTime, Mathf.Clamp01(Time.deltaTime * 10f));
                lastPos = p; hasLastPos = true;
            }

            // a local hit the server never confirmed (it missed there, or the report was refused): back to the server's value
            if (lastServerValue >= 0f && !health.IsDead && Time.time - lastLocalDamageAt > ResyncSeconds && Mathf.Abs(health.Health - lastServerValue) > 0.5f
                && !(IsOwner && health.Health > lastServerValue))
            {
                health.Health = Mathf.Clamp(lastServerValue, 0f, health.MaxHealth);
                health.CheckHealthState();
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
