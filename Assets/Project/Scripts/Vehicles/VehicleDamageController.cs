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
        public float MinDamageSpeed = 6.0f; // ~21.6 km/h

        [Tooltip("Damage dealt per m/s of impact velocity above MinDamageSpeed.")]
        public float DamagePerSpeed = 8.0f;

        [Tooltip("Speed threshold (m/s) at which crash is considered heavy.")]
        public float HeavyCrashSpeed = 13.0f;

        [Header("Driver Concussion Damage")]
        [Tooltip("Impact speed (m/s) above which the driver takes concussion injury.")]
        public float DriverDamageSpeedThreshold = 16.0f; // ~58 km/h

        [Tooltip("Damage multiplier applied to the driver on high-speed crash.")]
        public float DriverDamageMultiplier = 1.5f;

        [Header("Pedestrian / Character Ramming")]
        [Tooltip("Minimum vehicle speed (m/s) required to damage pedestrians on impact.")]
        public float MinRammingVehicleSpeed = 7.5f;

        [Tooltip("Damage inflicted on pedestrians/characters hit by this moving vehicle.")]
        public float RammingDamageMultiplier = 4.0f;

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
        private float _spawnTime;
        private float _lastCollisionTime = -10f;
        private float _lastPedestrianHitTime = -10f;

        private const float SPAWN_GRACE_PERIOD = 2.0f;
        private const float COLLISION_COOLDOWN = 0.35f;

        private void Awake()
        {
            _vehicle = GetComponent<Vehicle>();
            _health = GetComponent<JUHealth>();
            _rb = GetComponent<Rigidbody>();
            _spawnTime = Time.time;

            SetupImpactAudio();
            InitializeVisualFX();

            if (_health != null)
            {
                _health.OnDeath.AddListener(OnVehicleDestroyed);
            }
        }

        private void Start()
        {
            _spawnTime = Time.time;
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
                SanitizeVisualFXObject(_smokeInstance);
                _smokeInstance.SetActive(false);
            }

            // Pre-instantiate Fire FX (strip DeadZone tag and trigger colliders so it is purely visual!)
            if (FireFXPrefab != null)
            {
                _fireInstance = Instantiate(FireFXPrefab, worldPos, transform.rotation, transform);
                _fireInstance.transform.localPosition = EngineHoodOffset;
                SanitizeVisualFXObject(_fireInstance);
                _fireInstance.SetActive(false);
            }
        }

        private static void SanitizeVisualFXObject(GameObject fxObj)
        {
            if (fxObj == null) return;

            foreach (Transform t in fxObj.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.tag = "Untagged";
            }

            foreach (Collider col in fxObj.GetComponentsInChildren<Collider>(true))
            {
                col.enabled = false;
                Destroy(col);
            }
        }

        private void Update()
        {
            if (_health == null || _isDestroyed) return;

            float healthPercent = _health.Health / Mathf.Max(_health.MaxHealth, 1f);

            // Smoke Stage (<= 50% HP)
            if (_smokeInstance != null)
            {
                bool shouldSmoke = healthPercent <= 0.50f && healthPercent > 0f;
                if (_smokeInstance.activeSelf != shouldSmoke)
                {
                    _smokeInstance.SetActive(shouldSmoke);
                }
            }

            // Fire Stage (<= 25% HP)
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

            // Ignore collisions during initial scene physics settling
            if (Time.time - _spawnTime < SPAWN_GRACE_PERIOD) return;

            // Ignore own child colliders
            if (collision.transform.IsChildOf(transform)) return;

            // Check if the collided object is a character / pedestrian / ragdoll bone
            JUCharacterController character = collision.gameObject.GetComponentInParent<JUCharacterController>();
            AdvancedRagdollController ragdoll = collision.gameObject.GetComponentInParent<AdvancedRagdollController>();
            bool isCharacterOrRagdoll = (character != null) || (ragdoll != null) || (collision.gameObject.layer == 15);

            if (isCharacterOrRagdoll)
            {
                // Characters touching a stopped/slow vehicle never damage the vehicle;
                // only check if this moving vehicle is ramming a pedestrian at high speed.
                CheckPedestrianCollision(collision, character, ragdoll);
                return;
            }

            // Ignore small items / pickups
            if (collision.gameObject.layer == LayerMask.NameToLayer("Item")) return;

            // Cooldown between collision damage events
            if (Time.time - _lastCollisionTime < COLLISION_COOLDOWN) return;

            // Ignore flat vertical ground / suspension bottoming-out contacts (only crash into walls, obstacles, or steep impacts)
            if (collision.contactCount > 0)
            {
                Vector3 normal = collision.GetContact(0).normal;
                // If contact normal is pointing mostly straight up (ground under vehicle), ignore unless impact is extreme
                if (normal.y > 0.72f) return;
            }

            // Ensure either this vehicle or the other colliding body is actually moving fast
            float ownSpeed = _rb != null ? _rb.velocity.magnitude : 0f;
            float otherSpeed = collision.rigidbody != null ? collision.rigidbody.velocity.magnitude : 0f;
            if (ownSpeed < MinDamageSpeed * 0.5f && otherSpeed < MinDamageSpeed * 0.5f) return;

            float impactSpeed = collision.relativeVelocity.magnitude;
            if (impactSpeed < MinDamageSpeed) return;

            _lastCollisionTime = Time.time;

            // 1. Play Metal Impact Audio
            PlayImpactAudio(impactSpeed);

            // 2. Calculate and apply vehicle collision damage (clamped so a single bump cannot insta-kill a full-HP car)
            float rawDamage = (impactSpeed - MinDamageSpeed) * DamagePerSpeed;
            float maxSingleHitDamage = _health.MaxHealth * 0.45f;
            float damage = Mathf.Min(rawDamage, maxSingleHitDamage);

            Vector3 contactPoint = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
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

        private void CheckPedestrianCollision(Collision collision, JUCharacterController character, AdvancedRagdollController rag)
        {
            // NEVER damage characters if the vehicle is parked, turned off, or moving slowly!
            if (_vehicle == null || !_vehicle.IsOn || _rb == null) return;

            float vehicleSpeed = _rb.velocity.magnitude;
            if (vehicleSpeed < MinRammingVehicleSpeed) return;

            if (Time.time - _lastPedestrianHitTime < 0.5f) return;

            // Never damage the driver or someone currently entering/exiting this vehicle
            DriveVehicles driveAbility = character != null
                ? character.GetComponent<DriveVehicles>()
                : collision.gameObject.GetComponentInParent<DriveVehicles>();

            if (driveAbility != null)
            {
                if (driveAbility.CurrentVehicle == _vehicle || driveAbility.IsDriving || driveAbility.IsCharacterEntering || driveAbility.IsCharacterExiting)
                {
                    return;
                }
            }

            // Ensure the vehicle is actually moving toward the character
            Vector3 targetPos = character != null ? character.transform.position : collision.transform.position;
            Vector3 dirToTarget = (targetPos - transform.position).normalized;
            if (Vector3.Dot(_rb.velocity.normalized, dirToTarget) < 0.3f) return;

            JUHealth targetHealth = character != null
                ? character.CharacterHealth
                : collision.gameObject.GetComponentInParent<JUHealth>();

            if (targetHealth != null && targetHealth != _health)
            {
                _lastPedestrianHitTime = Time.time;

                float pedDamage = Mathf.Clamp((vehicleSpeed - MinRammingVehicleSpeed) * RammingDamageMultiplier, 10f, 85f);
                Vector3 contactPoint = collision.contactCount > 0 ? collision.GetContact(0).point : targetPos;
                targetHealth.DoDamage(pedDamage, contactPoint);

                PlayImpactAudio(vehicleSpeed);

                // Ragdoll pedestrian with controlled velocity change (avoid extreme impulses)
                if (rag != null)
                {
                    rag.SetActiveRagdoll(true);
                    if (rag.Hips != null)
                    {
                        Rigidbody hipsRb = rag.Hips.GetComponent<Rigidbody>();
                        if (hipsRb != null)
                        {
                            Vector3 knockbackVel = _rb.velocity.normalized * Mathf.Min(vehicleSpeed * 0.8f, 12f) + Vector3.up * 3.5f;
                            hipsRb.AddForce(knockbackVel, ForceMode.VelocityChange);
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
                        float driverDamage = Mathf.Clamp((impactSpeed - DriverDamageSpeedThreshold) * DriverDamageMultiplier, 5f, 45f);
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

            // Eject driver safely before explosion
            DriveVehicles[] drivers = FindObjectsOfType<DriveVehicles>();
            foreach (DriveVehicles d in drivers)
            {
                if (d != null && d.CurrentVehicle == _vehicle)
                {
                    d.ExitVehicle();

                    // Place driver slightly clear of the vehicle blast
                    d.transform.position = transform.position - transform.right * 3.2f + Vector3.up * 0.5f;
                }
            }

            // Spawn Explosion FX only if vehicle doesn't already have an active Explosion Particle child
            Transform existingExplosion = transform.Find("Explosion Particle");
            if (existingExplosion != null)
            {
                existingExplosion.gameObject.SetActive(true);
            }
            else if (ExplosionFXPrefab != null)
            {
                Vector3 explosionPos = transform.TransformPoint(EngineHoodOffset);
                GameObject explosion = Instantiate(ExplosionFXPrefab, explosionPos, Quaternion.identity);
                Destroy(explosion, 6f);
            }

            // Apply controlled shockwave jolt to the wrecked vehicle
            if (_rb != null)
            {
                _rb.AddForce(Vector3.up * (_rb.mass * 3.5f), ForceMode.Impulse);
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
