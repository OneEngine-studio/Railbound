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
        public float popUpForce = 3.6f;
        public float spreadForce = 1.4f;
        public float randomTorque = 5f;

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
                    DropSingleAmmoBox(center + transform.forward * 0.35f);
                }
                else if (!dropAmmo && medkitPrefab != null)
                {
                    DropSingleMedkit(center + transform.forward * 0.35f);
                }
            }
            else
            {
                if (medkitPrefab != null)
                    DropSingleMedkit(center - transform.right * 0.35f);

                if (ammoBoxPrefab != null)
                    DropSingleAmmoBox(center + transform.right * 0.35f);
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
            var medkitPickup = medkitObj.GetComponent<MedkitPickup>();

            Vector3 medkitVel =
                Vector3.up * popUpForce +
                transform.forward * spreadForce +
                Random.insideUnitSphere * 0.25f;

            Vector3 medkitTorque = new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(-4f, 4f),
                Random.Range(-1f, 1f));

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
            var ammoPickup = ammoObj.GetComponent<AmmoBoxPickup>();

            Vector3 ammoVel =
                Vector3.up * popUpForce +
                transform.forward * spreadForce +
                Random.insideUnitSphere * 0.25f;

            Vector3 ammoTorque = new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(-4f, 4f),
                Random.Range(-1f, 1f));

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
        }
    }
}
