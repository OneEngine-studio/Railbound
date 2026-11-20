using UnityEngine;
using JUTPS;
using Railbound.Gameplay;

namespace Railbound.AI
{
    /// <summary>
    /// Spawns physical loot drops when an enemy combatant is killed.
    /// In RandomSingleItem mode, each defeated enemy drops one item:
    /// either an Ammo Box or a Medkit, chosen randomly.
    /// </summary>
    [RequireComponent(typeof(JUHealth))]
    public class BattleRoyaleLootDropper : MonoBehaviour
    {
        public enum DropMode
        {
            RandomSingleItem,
            BothItems
        }

        [Header("Drop Strategy")]
        [Tooltip("RandomSingleItem drops one randomly selected item. BothItems drops both.")]
        public DropMode dropMode = DropMode.RandomSingleItem;

        [Range(0f, 1f)]
        [Tooltip("Chance to drop an Ammo Box in RandomSingleItem mode.")]
        public float ammoDropChance = 0.5f;

        [Header("Prefabs to Drop")]
        [Tooltip("Medkit pickup prefab with physics.")]
        public GameObject medkitPrefab;

        [Tooltip("Ammo Box pickup prefab with physics.")]
        public GameObject ammoBoxPrefab;

        [Header("Drop Physics")]
        public float popUpForce = 2.2f;
        public float spreadForce = 0.8f;
        public float randomTorque = 1.5f;

        /// <summary>
        /// A drop must not be thrown around by bodies: it used to appear inside the falling corpse, which shoved it out at speed (a crate
        /// flying through the body into the camera). It ignores collisions with every character body and ragdoll bone near it, on every
        /// machine (the clients' copies call this too).
        /// </summary>
        public static void IgnoreNearbyBodies(GameObject item)
        {
            if (item == null) return;
            var own = item.GetComponentsInChildren<Collider>(true);
            if (own.Length == 0) return;
            foreach (var c in Physics.OverlapSphere(item.transform.position, 3f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (c == null || c.transform.IsChildOf(item.transform)) continue;
                if (c.GetComponentInParent<JUTPS.JUCharacterController>() == null && c.GetComponentInParent<JUTPS.PhysicsScripts.AdvancedRagdollController>() == null) continue;
                foreach (var o in own) if (o != null) Physics.IgnoreCollision(o, c, true);
            }
        }

        // beside the body, at chest height, on the given side (never inside the ragdoll)
        private Vector3 Beside(Vector3 side)
        {
            side.y = 0f;
            if (side.sqrMagnitude < 0.001f) side = Vector3.right;
            return transform.position + side.normalized * 0.9f + Vector3.up * 0.8f;
        }

        private JUHealth _health;
        private bool _hasDropped;

        private void Awake()
        {
            _health = GetComponent<JUHealth>();
            if (_health != null)
            {
                _health.OnDeath.AddListener(HandleDeath);
            }
        }

        private void OnDestroy()
        {
            if (_health != null)
            {
                _health.OnDeath.RemoveListener(HandleDeath);
            }
        }

        private void HandleDeath()
        {
            if (_hasDropped) return;

            _hasDropped = true;
            DropLoot();
        }

        public void DropLoot()
        {
            if (medkitPrefab == null && ammoBoxPrefab == null)
            {
                Debug.LogWarning($"{nameof(BattleRoyaleLootDropper)}: No loot prefabs assigned.", this);
                return;
            }

            Vector3 center = transform.position + Vector3.up * 0.8f;

            if (dropMode == DropMode.RandomSingleItem)
            {
                bool dropAmmo = Random.value < Mathf.Clamp01(ammoDropChance);

                // Fall back to the available prefab if the selected one is missing.
                if (dropAmmo && ammoBoxPrefab == null)
                    dropAmmo = false;

                if (!dropAmmo && medkitPrefab == null)
                    dropAmmo = true;

                if (dropAmmo && ammoBoxPrefab != null)
                {
                    DropSingleAmmoBox(Beside(transform.right));
                }
                else if (!dropAmmo && medkitPrefab != null)
                {
                    DropSingleMedkit(Beside(transform.right));
                }
            }
            else
            {
                if (medkitPrefab != null)
                    DropSingleMedkit(Beside(-transform.right));

                if (ammoBoxPrefab != null)
                    DropSingleAmmoBox(Beside(transform.right));
            }
        }

        private void DropSingleMedkit(Vector3 pos)
        {
            if (medkitPrefab == null)
                return;

            Quaternion spawnRot = Quaternion.Euler(
                Random.Range(-10f, 10f),
                Random.Range(0f, 360f),
                Random.Range(-10f, 10f));

            var medkitObj = Instantiate(medkitPrefab, pos, spawnRot);
            IgnoreNearbyBodies(medkitObj);
            var medkitPickup = medkitObj.GetComponent<MedkitPickup>();

            Vector3 out1 = pos - transform.position; out1.y = 0f;
            Vector3 medkitVel =
                Vector3.up * Mathf.Min(popUpForce, 2.2f) +
                out1.normalized * Mathf.Min(spreadForce, 0.8f) +
                Random.insideUnitSphere * 0.15f;

            Vector3 medkitTorque = new Vector3(
                Random.Range(-0.3f, 0.3f),
                Random.Range(-Mathf.Min(randomTorque, 1.5f), Mathf.Min(randomTorque, 1.5f)),
                Random.Range(-0.3f, 0.3f));

            if (medkitPickup != null)
            {
                medkitPickup.SpawnWithImpulse(medkitVel, medkitTorque);
            }
            else
            {
                var rb = medkitObj.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.velocity = medkitVel;
                    rb.angularVelocity = medkitTorque;
                }
            }
            Railbound.BR.BRLootSync.ServerAnnounceDrop(Railbound.BR.BRLootSync.DropKind.Medkit, medkitObj, medkitVel, medkitTorque);
        }

        private void DropSingleAmmoBox(Vector3 pos)
        {
            if (ammoBoxPrefab == null)
                return;

            Quaternion spawnRot = Quaternion.Euler(
                Random.Range(-8f, 8f),
                Random.Range(0f, 360f),
                Random.Range(-8f, 8f));

            var ammoObj = Instantiate(ammoBoxPrefab, pos, spawnRot);
            IgnoreNearbyBodies(ammoObj);
            var ammoPickup = ammoObj.GetComponent<AmmoBoxPickup>();

            Vector3 out2 = pos - transform.position; out2.y = 0f;
            Vector3 ammoVel =
                Vector3.up * Mathf.Min(popUpForce, 2.2f) +
                out2.normalized * Mathf.Min(spreadForce, 0.8f) +
                Random.insideUnitSphere * 0.15f;

            Vector3 ammoTorque = new Vector3(
                Random.Range(-0.3f, 0.3f),
                Random.Range(-Mathf.Min(randomTorque, 1.5f), Mathf.Min(randomTorque, 1.5f)),
                Random.Range(-0.3f, 0.3f));

            if (ammoPickup != null)
            {
                ammoPickup.SpawnWithImpulse(ammoVel, ammoTorque);
            }
            else
            {
                var rb = ammoObj.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.velocity = ammoVel;
                    rb.angularVelocity = ammoTorque;
                }
            }
            Railbound.BR.BRLootSync.ServerAnnounceDrop(Railbound.BR.BRLootSync.DropKind.AmmoBox, ammoObj, ammoVel, ammoTorque);
        }
    }
}
