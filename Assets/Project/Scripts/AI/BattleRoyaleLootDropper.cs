// BattleRoyaleLootDropper.cs
using UnityEngine;
using JUTPS;
using Railbound.Gameplay;

namespace Railbound.AI
{
    [RequireComponent(typeof(JUHealth))]
    public class BattleRoyaleLootDropper : MonoBehaviour
    {
        // existing code...

        public void DropLoot()
        {
            if (medkitPrefab == null && ammoBoxPrefab == null)
            {
                Debug.LogWarning($"{nameof(BattleRoyaleLootDropper)}: No loot prefabs assigned.");
                return;
            }

            ammoDropChance = Mathf.Clamp01(ammoDropChance);
            Vector3 center = transform.position + Vector3.up * 0.8f;

            if (dropMode == DropMode.RandomSingleItem)
            {
                bool dropAmmo = Random.value < ammoDropChance;

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
                    DropSingleMedkit(center + (-transform.right * 0.35f));

                if (ammoBoxPrefab != null)
                    DropSingleAmmoBox(center + (transform.right * 0.35f));
            }
        }

        private void DropSingleMedkit(Vector3 pos)
        {
            if (medkitPrefab == null)
                return;

            Quaternion spawnRot = Quaternion.Euler(Random.Range(-10f, 10f), Random.Range(0f, 360f), Random.Range(-10f, 10f));
            var medkitObj = Instantiate(medkitPrefab, pos, spawnRot);

            var medkitPickup = medkitObj.GetComponent<MedkitPickup>();
            Vector3 medkitVel = Vector3.up * popUpForce + (transform.forward * spreadForce) + (Random.insideUnitSphere * 0.25f);
            Vector3 medkitTorque = new Vector3(Random.Range(-1f, 1f), Random.Range(-4f, 4f), Random.Range(-1f, 1f));

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

            Quaternion spawnRot = Quaternion.Euler(Random.Range(-8f, 8f), Random.Range(0f, 360f), Random.Range(-8f, 8f));
            var ammoObj = Instantiate(ammoBoxPrefab, pos, spawnRot);

            var ammoPickup = ammoObj.GetComponent<AmmoBoxPickup>();
            Vector3 ammoVel = Vector3.up * popUpForce + (transform.forward * spreadForce) + (Random.insideUnitSphere * 0.25f);
            Vector3 ammoTorque = new Vector3(Random.Range(-1f, 1f), Random.Range(-4f, 4f), Random.Range(-1f, 1f));

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
