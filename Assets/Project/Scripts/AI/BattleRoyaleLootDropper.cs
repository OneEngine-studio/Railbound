using UnityEngine;
using JUTPS;
using Railbound.Gameplay;

namespace Railbound.AI
{
    /// <summary>
    /// Spawns realistic physical loot drops when an enemy combatant is killed.
    /// </summary>
    [RequireComponent(typeof(JUHealth))]
    public class BattleRoyaleLootDropper : MonoBehaviour
    {
        public enum DropMode
        {
            RandomSingleItem, // Drops either 1 Medkit OR 1 Ammo Box randomly
            BothItems         // Drops both 1 Medkit and 1 Ammo Box
        }

        [Header("Drop Strategy")]
        public DropMode dropMode = DropMode.RandomSingleItem;

        [Range(0f, 1f)]
        [Tooltip("Chance to drop an Ammo Box when using RandomSingleItem.")]
        public float ammoDropChance = 0.5f;

        [Header("Prefabs to Drop")]
        public GameObject medkitPrefab;
        public GameObject ammoBoxPrefab;

        [Header("Drop Physics")]
        public float popUpForce = 3.6f;
        public float spreadForce = 1.4f;
        public float randomTorque = 5f;

        private JUHealth _health;
        private bool _hasDropped = false;

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
            Vector3 center = transform.position + Vector3.up * 0.8f;

            if (dropMode == DropMode.RandomSingleItem)
            {
                bool dropAmmo = Random.value < ammoDropChance;

                // Fallbacks if one prefab happens to be unassigned
                if (dropAmmo && ammoBoxPrefab == null && medkitPrefab != null) dropAmmo = false;
                if (!dropAmmo && medkitPrefab == null && ammoBoxPrefab != null) dropAmmo = true;

                if (dropAmmo && ammoBoxPrefab != null)
                {
                    DropItem(ammoBoxPrefab, center + transform.forward * 0.35f, true);
                }
                else if (!dropAmmo && medkitPrefab != null)
                {
                    DropItem(medkitPrefab, center + transform.forward * 0.35f, false);
                }
            }
            else
            {
                if (medkitPrefab != null) DropItem(medkitPrefab, center + (-transform.right * 0.35f), false);
                if (ammoBoxPrefab != null) DropItem(ammoBoxPrefab, center + (transform.right * 0.35f), true);
            }
        }

        private void DropItem(GameObject prefab, Vector3 pos, bool isAmmo)
        {
            float angleRange = isAmmo ? 8f : 10f;
            Quaternion spawnRot = Quaternion.Euler(Random.Range(-angleRange, angleRange), Random.Range(0f, 360f), Random.Range(-angleRange, angleRange));
            
            var itemObj = Instantiate(prefab, pos, spawnRot);

            Vector3 vel = Vector3.up * popUpForce + (transform.forward * spreadForce) + (Random.insideUnitSphere * 0.25f);
            Vector3 torque = new Vector3(Random.Range(-1f, 1f), Random.Range(-4f, 4f), Random.Range(-1f, 1f));

            // Try to use specific pickup components first
            if (isAmmo)
            {
                var ammoPickup = itemObj.GetComponent<AmmoBoxPickup>();
                if (ammoPickup != null) { ammoPickup.SpawnWithImpulse(vel, torque); return; }
            }
            else
            {
                var medkitPickup = itemObj.GetComponent<MedkitPickup>();
                if (medkitPickup != null) { medkitPickup.SpawnWithImpulse(vel, torque); return; }
            }

            // Fallback to standard Rigidbody
            var rb = itemObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = vel;
                rb.angularVelocity = torque;
            }
        }
    }
}
