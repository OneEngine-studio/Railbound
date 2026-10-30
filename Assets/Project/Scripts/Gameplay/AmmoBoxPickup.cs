using System.Collections;
using UnityEngine;
using JUTPS;
using JUTPS.WeaponSystem;
using JUTPS.ItemSystem;

namespace Railbound.Gameplay
{
    /// <summary>
    /// Tactical Battle Royale Ammo Box with realistic physics.
    /// Automatically opens its lid when the player approaches,
    /// plays mechanical opening audio, animates bullet cartridges, and grants ammo.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(BoxCollider))]
    public class AmmoBoxPickup : MonoBehaviour
    {
        [Header("Ammo Settings")]
        [Tooltip("Amount of bullets granted to the player.")]
        public int ammoAmount = 32;

        [Header("Lid Opening Animation")]
        [Tooltip("Transform of the hinged lid to rotate open.")]
        public Transform lidTransform;
        public float openDuration = 0.35f;
        [Tooltip("Relative pitch angle to rotate the lid backwards around its hinge.")]
        public float openAngleOffset = -110f;

        [Header("Visual & Effects")]
        public GameObject openParticlePrefab;
        public GameObject bulletModelPrefab;
        public Light ambientLight;

        [Header("Persistence / Battle Royale Behavior")]
        [Tooltip("If true, the empty open ammo box stays permanently in the map after looting like in PUBG / Apex / Warzone.")]
        public bool keepOpenBoxInWorld = true;
        [Tooltip("If keepOpenBoxInWorld is false, delay before the open box despawns.")]
        public float despawnDelay = 60f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip openLatchClip;
        [SerializeField] private AudioClip pickupChimeClip;

        private Rigidbody _rb;
        private bool _isOpening = false;
        private bool _isCollected = false;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            if (_rb != null)
            {
                // Bias center of mass downwards so box lands upright on terrain
                _rb.centerOfMass = new Vector3(0f, -0.15f, -0.02f);
            }

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.spatialBlend = 0.5f;
                    audioSource.playOnAwake = false;
                }
            }

            if (openLatchClip == null) openLatchClip = CreateLatchOpenClip();
            if (pickupChimeClip == null) pickupChimeClip = CreateAmmoChimeClip();
        }

        /// <summary>
        /// Launches the ammo box with physical impulse when dropped by an enemy.
        /// </summary>
        public void SpawnWithImpulse(Vector3 velocity, Vector3 angularVelocity)
        {
            if (_rb == null) _rb = GetComponent<Rigidbody>();
            _rb.isKinematic = false;
            _rb.useGravity = true;
            _rb.velocity = velocity;
            _rb.angularVelocity = angularVelocity;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_isOpening || _isCollected) return;

            if (other.CompareTag("Player") || other.GetComponentInParent<JUCharacterController>() != null)
            {
                var pl = other.GetComponent<JUCharacterController>() ?? other.GetComponentInParent<JUCharacterController>();
                if (pl != null)
                {
                    // Disable all trigger colliders immediately so it cannot be triggered again
                    foreach (var c in GetComponents<Collider>())
                    {
                        if (c.isTrigger) c.enabled = false;
                    }
                    StartCoroutine(OpenAndCollectRoutine(pl));
                }
            }
        }

        private IEnumerator OpenAndCollectRoutine(JUCharacterController pl)
        {
            _isOpening = true;

            // 1. Play mechanical lid unlatch sound
            if (audioSource != null && openLatchClip != null)
            {
                audioSource.PlayOneShot(openLatchClip, 0.95f);
            }

            // 2. Animate lid opening smoothly
            if (lidTransform != null)
            {
                Quaternion startRot = lidTransform.localRotation;
                Quaternion targetRot = startRot * Quaternion.Euler(openAngleOffset, 0f, 0f);
                float elapsed = 0f;

                while (elapsed < openDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.SmoothStep(0f, 1f, elapsed / openDuration);
                    lidTransform.localRotation = Quaternion.Slerp(startRot, targetRot, t);
                    yield return null;
                }
                lidTransform.localRotation = targetRot;
            }
            else
            {
                yield return new WaitForSeconds(0.15f);
            }

            // 3. Spawn floating bullet rising effect
            if (bulletModelPrefab != null)
            {
                for (int i = 0; i < 3; i++)
                {
                    Vector3 offset = new Vector3((i - 1) * 0.08f, 0.15f, 0f);
                    var bullet = Instantiate(bulletModelPrefab, transform.position + offset, Quaternion.identity);
                    StartCoroutine(AnimateRisingBullet(bullet, pl.transform.position + Vector3.up * 1.2f));
                }
            }

            // 4. Play ammo collect chime
            if (audioSource != null && pickupChimeClip != null)
            {
                audioSource.PlayOneShot(pickupChimeClip, 0.9f);
            }

            // 5. Grant ammo to player weapons
            bool granted = GrantAmmoToPlayer(pl);

            // 6. Show HUD notification
            ShowHUDNotification($"<color=#FFD54F>+{ammoAmount} AMMO COLLECTED</color>");

            _isCollected = true;

            // 7. Visibly empty the ammo contents inside the box so players see it was looted
            var contents = transform.Find("AmmoContents");
            if (contents != null)
            {
                StartCoroutine(EmptyAmmoContentsRoutine(contents));
            }

            // 8. Make the physical box kinematic so it stays firmly in place on the terrain
            if (_rb != null)
            {
                _rb.velocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                _rb.isKinematic = true;
            }

            // 9. Realistic Battle Royale behavior:
            // The open, empty ammo crate stays in the world as a looted prop!
            if (!keepOpenBoxInWorld)
            {
                yield return new WaitForSeconds(despawnDelay);
                Destroy(gameObject);
            }
        }

        private IEnumerator EmptyAmmoContentsRoutine(Transform contents)
        {
            // Give player a brief moment to see the open box before ammo is scooped up
            yield return new WaitForSeconds(0.4f);

            Vector3 startScale = contents.localScale;
            float duration = 0.35f;
            float elapsed = 0f;

            while (elapsed < duration && contents != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                contents.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
                yield return null;
            }

            if (contents != null)
            {
                contents.gameObject.SetActive(false);
            }
        }

        private IEnumerator AnimateRisingBullet(GameObject bullet, Vector3 targetPos)
        {
            if (bullet == null) yield break;
            Vector3 start = bullet.transform.position;
            float duration = 0.45f;
            float elapsed = 0f;

            while (elapsed < duration && bullet != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                bullet.transform.position = Vector3.Lerp(start, targetPos, t * t);
                bullet.transform.localScale = Vector3.Lerp(bullet.transform.localScale, Vector3.zero, t);
                yield return null;
            }

            if (bullet != null) Destroy(bullet);
        }

        private bool GrantAmmoToPlayer(JUCharacterController pl)
        {
            bool anyGranted = false;

            // Grant to actively held weapons
            if (pl.WeaponInUseRightHand != null)
            {
                pl.WeaponInUseRightHand.TotalBullets += ammoAmount;
                anyGranted = true;
            }
            if (pl.WeaponInUseLeftHand != null)
            {
                pl.WeaponInUseLeftHand.TotalBullets += ammoAmount;
                anyGranted = true;
            }

            // Also check all inventory / holster weapons so ammo isn't wasted if fists equipped
            var allWeapons = pl.GetComponentsInChildren<Weapon>(true);
            foreach (var w in allWeapons)
            {
                if (w != pl.WeaponInUseRightHand && w != pl.WeaponInUseLeftHand)
                {
                    w.TotalBullets += ammoAmount;
                    anyGranted = true;
                }
            }

            return anyGranted;
        }

        private void ShowHUDNotification(string message)
        {
            var msgPanel = GameObject.Find("OnScreenWarnings/InteractMessage/Text");
            if (msgPanel != null)
            {
                var textComp = msgPanel.GetComponent<UnityEngine.UI.Text>();
                if (textComp != null)
                {
                    textComp.text = message;
                    var parent = msgPanel.transform.parent.gameObject;
                    parent.SetActive(true);
                    StartCoroutine(HideMessageAfterDelay(parent, 2.5f));
                }
            }
        }

        private IEnumerator HideMessageAfterDelay(GameObject obj, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (obj != null) obj.SetActive(false);
        }

        #region Procedural Audio Clips
        private static AudioClip CreateLatchOpenClip()
        {
            int sampleRate = 44100;
            float duration = 0.28f;
            int numSamples = (int)(sampleRate * duration);
            float[] samples = new float[numSamples];

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 22f);

                // Sharp metallic latch snap + click
                float wave = Mathf.Sin(2f * Mathf.PI * 1800f * t) * 0.5f
                           + Mathf.Sin(2f * Mathf.PI * 620f * t) * 0.4f
                           + (Random.value * 2f - 1f) * 0.25f;

                samples[i] = wave * env * 0.5f;
            }

            var clip = AudioClip.Create("AmmoBoxLatchOpen", numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateAmmoChimeClip()
        {
            int sampleRate = 44100;
            float duration = 0.35f;
            int numSamples = (int)(sampleRate * duration);
            float[] samples = new float[numSamples];

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 10f);

                // Two crisp brass metallic clicks (bullets loading into mag)
                float freq = t < 0.12f ? 1480f : 2200f;
                float wave = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.6f
                           + (Random.value * 2f - 1f) * 0.15f;

                samples[i] = wave * env * 0.45f;
            }

            var clip = AudioClip.Create("AmmoChimeProcedural", numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
        #endregion
    }
}

