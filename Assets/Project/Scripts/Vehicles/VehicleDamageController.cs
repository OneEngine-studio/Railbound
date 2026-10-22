using UnityEngine;
using JUTPS;
using JUTPS.VehicleSystem;
using JUTPS.ActionScripts;
using JUTPS.PhysicsScripts;

namespace Project.Vehicles
{
    /// <summary>
    /// Comprehensive vehicle collision damage and destruction controller.
    /// Manages collision impact detection, impact audio SFX, health deduction,
    /// progressive damage visual effects (smoke, fire, explosion), driver concussion,
    /// and emergency driver ejection upon destruction.
    /// </summary>
    [RequireComponent(typeof(Vehicle))]
    [RequireComponent(typeof(JUHealth))]
    [AddComponentMenu("Project/Vehicles/Vehicle Damage Controller")]
    public class VehicleDamageController : MonoBehaviour
    {
        [Header("Collision Damage Tuning")]
        [Tooltip("Minimum impact velocity (m/s) before damage is incurred (avoids damage on minor nudges).")]
        public float MinDamageSpeed = 4.0f; // ~14.4 km/h

        [Tooltip("Damage dealt per m/s of impact velocity above MinDamageSpeed.")]
        public float DamagePerSpeed = 16f;

        [Tooltip("Speed threshold (m/s) at which crash is considered heavy.")]
        public float HeavyCrashSpeed = 11.5f;

        [Header("Driver Concussion Damage")]
        [Tooltip("Impact speed (m/s) above which the driver takes concussion injury.")]
        public float DriverDamageSpeedThreshold = 14.0f; // ~50 km/h

        [Tooltip("Damage multiplier applied to the driver on high-speed crash.")]
        public float DriverDamageMultiplier = 2.2f;

        [Header("Pedestrian / Character Ramming")]
        [Tooltip("Damage inflicted on pedestrians/characters hit by this vehicle.")]
        public float RammingDamageMultiplier = 12f;

        [Header("Engine Offset & Visual FX")]
        [Tooltip("Local position of the engine / hood where smoke and fire originate.")]
        public Vector3 EngineHoodOffset = new Vector3(0.07f, 0.85f, 1.5f);

        [Tooltip("Prefab for light/medium engine damage smoke (< 50% HP).")]
        public GameObject SmokeFXPrefab;

        [Tooltip("Prefab for critical engine fire (< 25% HP).")]
        public GameObject FireFXPrefab;

        [Tooltip("Prefab for lethal explosion (<= 0% HP).")]
        public GameObject ExplosionFXPrefab;

        [Header("Impact Audio Clips")]
        [Tooltip("Light to medium metal impact sound.")]
        public AudioClip LightImpactClip;

        [Tooltip("Heavy metal collision crash sound.")]
        public AudioClip HeavyImpactClip;

        private Vehicle _vehicle;
        private JUHealth _health;
        private Rigidbody _rb;
        private AudioSource _impactAudioSource;

        private GameObject _smokeInstance;
        private GameObject _fireInstance;
        private bool _isDestroyed = false;

        private void Awake()
        {
            _vehicle = GetComponent<Vehicle>();
            _health = GetComponent<JUHealth>();
            _rb = GetComponent<Rigidbody>();

            SetupImpactAudio();
            InitializeVisualFX();

            if (_health != null)
            {
                _health.OnDeath.AddListener(OnVehicleDestroyed);
            }
        }

        private void SetupImpactAudio()
        {
            _impactAudioSource = gameObject.AddComponent<AudioSource>();
            _impactAudioSource.playOnAwake = false;
            _impactAudioSource.loop = false;
            _impactAudioSource.spatialBlend = 1.0f;
            _impactAudioSource.minDistance = 3f;
            _impactAudioSource.maxDistance = 55f;
            _impactAudioSource.rolloffMode = AudioRolloffMode.Linear;
        }

        private void InitializeVisualFX()
        {
            Vector3 worldPos = transform.TransformPoint(EngineHoodOffset);

            // Pre-instantiate Smoke FX
            if (SmokeFXPrefab != null)
            {
                _smokeInstance = Instantiate(SmokeFXPrefab, worldPos, transform.rotation, transform);
                _smokeInstance.transform.localPosition = EngineHoodOffset;
                _smokeInstance.SetActive(false);
            }

            // Pre-instantiate Fire FX
            if (FireFXPrefab != null)
            {
                _fireInstance = Instantiate(FireFXPrefab, worldPos, transform.rotation, transform);
                _fireInstance.transform.localPosition = EngineHoodOffset;
                _fireInstance.SetActive(false);
            }
        }

        private void Update()
        {
            if (_health == null || _isDestroyed) return;

            float healthPercent = _health.Health / Mathf.Max(_health.MaxHealth, 1f);

            // Smoke Stage (< 50% HP)
            if (_smokeInstance != null)
            {
                bool shouldSmoke = healthPercent <= 0.50f && healthPercent > 0f;
                if (_smokeInstance.activeSelf != shouldSmoke)
                {
                    _smokeInstance.SetActive(shouldSmoke);
                }
            }

            // Fire Stage (< 25% HP)
            if (_fireInstance != null)
            {
                bool shouldFire = healthPercent <= 0.25f && healthPercent > 0f;
                if (_fireInstance.activeSelf != shouldFire)
                {
                    _fireInstance.SetActive(shouldFire);
                }
            }

            // Check if dead
            if (_health.Health <= 0f && !_isDestroyed)
            {
                OnVehicleDestroyed();
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_isDestroyed) return;

            float impactSpeed = collision.relativeVelocity.magnitude;

            // Check if we hit a pedestrian / character
            CheckPedestrianCollision(collision, impactSpeed);

            // Ignore light bumps below damage threshold
            if (impactSpeed < MinDamageSpeed) return;

            // 1. Play Metal Impact Audio
            PlayImpactAudio(impactSpeed);

            // 2. Calculate and apply vehicle collision damage
            float damage = (impactSpeed - MinDamageSpeed) * DamagePerSpeed;
            Vector3 contactPoint = collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position;
            _health.DoDamage(damage, contactPoint);

            // 3. High-speed crash driver concussion injury
            if (impactSpeed >= DriverDamageSpeedThreshold)
            {
                ApplyDriverCrashDamage(impactSpeed);
            }
        }

        private void PlayImpactAudio(float impactSpeed)
        {
            if (_impactAudioSource == null) return;

            AudioClip clip = (impactSpeed >= HeavyCrashSpeed && HeavyImpactClip != null) ? HeavyImpactClip : LightImpactClip;
            if (clip != null)
            {
                float volume = Mathf.Clamp01(0.4f + (impactSpeed / 25f));
                _impactAudioSource.pitch = Random.Range(0.88f, 1.12f);
                _impactAudioSource.PlayOneShot(clip, volume);
            }
        }

        private void CheckPedestrianCollision(Collision collision, float impactSpeed)
        {
            // Ignore own colliders
            if (collision.transform.IsChildOf(transform)) return;

            JUHealth targetHealth = collision.gameObject.GetComponentInParent<JUHealth>();
            if (targetHealth != null && targetHealth != _health)
            {
                // Hit a character / player / enemy
                if (impactSpeed > 3.0f)
                {
                    float pedDamage = impactSpeed * RammingDamageMultiplier;
                    Vector3 contactPoint = collision.contacts.Length > 0 ? collision.contacts[0].point : collision.transform.position;
                    targetHealth.DoDamage(pedDamage, contactPoint);

                    // Ragdoll pedestrian if possible
                    AdvancedRagdollController rag = collision.gameObject.GetComponentInParent<AdvancedRagdollController>();
                    if (rag != null)
                    {
                        rag.SetActiveRagdoll(true);
                        if (rag.Hips != null)
                        {
                            Rigidbody hipsRb = rag.Hips.GetComponent<Rigidbody>();
                            if (hipsRb != null)
                            {
                                hipsRb.AddForce(transform.forward * (impactSpeed * 350f) + Vector3.up * 200f, ForceMode.Impulse);
                            }
                        }
                    }
                }
            }
        }

        private void ApplyDriverCrashDamage(float impactSpeed)
        {
            DriveVehicles[] drivers = FindObjectsOfType<DriveVehicles>();
            foreach (DriveVehicles d in drivers)
            {
                if (d != null && d.CurrentVehicle == _vehicle)
                {
                    JUHealth driverHealth = d.GetComponent<JUHealth>();
                    if (driverHealth != null)
                    {
                        float driverDamage = (impactSpeed - DriverDamageSpeedThreshold) * DriverDamageMultiplier;
                        driverHealth.DoDamage(driverDamage, d.transform.position);
                    }
                    break;
                }
            }
        }

        public void OnVehicleDestroyed()
        {
            if (_isDestroyed) return;
            _isDestroyed = true;

            // Turn off engine & controls
            if (_vehicle != null)
            {
                _vehicle.ControlsEnabled = false;
                _vehicle.IsOn = false;
                _vehicle.enabled = false;
            }

            // Eject and protect driver
            DriveVehicles[] drivers = FindObjectsOfType<DriveVehicles>();
            foreach (DriveVehicles d in drivers)
            {
                if (d != null && d.CurrentVehicle == _vehicle)
                {
                    d.ExitVehicle();

                    // Toss driver clear of wreckage
                    AdvancedRagdollController rag = d.GetComponent<AdvancedRagdollController>();
                    if (rag != null)
                    {
                        rag.SetActiveRagdoll(true);
                        if (rag.Hips != null)
                        {
                            Rigidbody hipsRb = rag.Hips.GetComponent<Rigidbody>();
                            if (hipsRb != null)
                            {
                                hipsRb.AddForce(Vector3.up * 8f + transform.forward * 4f, ForceMode.VelocityChange);
                            }
                        }
                    }
                }
            }

            // Spawn Explosion FX
            Vector3 explosionPos = transform.TransformPoint(EngineHoodOffset);
            if (ExplosionFXPrefab != null)
            {
                GameObject explosion = Instantiate(ExplosionFXPrefab, explosionPos, Quaternion.identity);
                Destroy(explosion, 6f);
            }

            // Apply explosive shockwave torque to the wrecked vehicle
            if (_rb != null)
            {
                _rb.AddForce(Vector3.up * 4500f, ForceMode.Impulse);
                _rb.AddTorque(Random.insideUnitSphere * 2500f, ForceMode.Impulse);
            }

            // Play heavy crash sound
            if (_impactAudioSource != null && HeavyImpactClip != null)
            {
                _impactAudioSource.pitch = 0.85f;
                _impactAudioSource.PlayOneShot(HeavyImpactClip, 1.0f);
            }
        }

        private void OnDestroy()
        {
            if (_health != null)
            {
                _health.OnDeath.RemoveListener(OnVehicleDestroyed);
            }
        }
    }
}
