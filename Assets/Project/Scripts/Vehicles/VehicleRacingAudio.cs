using System.Collections;
using UnityEngine;
using JUTPS.VehicleSystem;

namespace Project.Vehicles
{
    /// <summary>
    /// Advanced racing engine audio system featuring simulated multi-gear transmission,
    /// dynamic RPM revving, throttle responsiveness, engine braking deceleration whine,
    /// and lateral drift tire screech.
    /// </summary>
    [RequireComponent(typeof(Vehicle))]
    [AddComponentMenu("Project/Vehicles/Vehicle Racing Audio")]
    public class VehicleRacingAudio : MonoBehaviour
    {
        [Header("Audio Clips")]
        [Tooltip("Ignition start clip.")]
        public AudioClip StartEngineClip;

        [Tooltip("Primary looping engine sound clip.")]
        public AudioClip EngineLoopClip;

        [Tooltip("Optional tire screech clip (if unassigned, a procedural tire skid sound is generated).")]
        public AudioClip TireSkidClip;

        [Header("Pitch Tuning (Combustion Harmonics)")]
        [Range(0.4f, 1.2f)] public float IdlePitch = 0.75f;
        [Range(1.5f, 3.0f)] public float RedlinePitch = 2.2f;
        [Range(0.4f, 1.5f)] public float ReversePitch = 1.05f;

        [Header("Volume Tuning")]
        [Range(0f, 1f)] public float IdleVolume = 0.5f;
        [Range(0f, 1f)] public float CruisingVolume = 0.85f;
        [Range(0f, 1f)] public float MaxAccelerationVolume = 1.0f;

        [Header("RPM & Gear Dynamics")]
        [Tooltip("RPM response speed when accelerating.")]
        public float AccelerationLerpSpeed = 5.5f;

        [Tooltip("RPM drop speed when off throttle (engine braking).")]
        public float DecelerationLerpSpeed = 2.8f;

        [Tooltip("Number of simulated forward gears.")]
        public int NumberOfGears = 5;

        [Tooltip("Max speed in m/s at the top of the highest gear.")]
        public float TopSpeed = 48f; // ~173 km/h

        [Header("Tire Screech & Drift")]
        [Tooltip("Minimum sideways slip velocity (m/s) before tire screech triggers.")]
        public float MinDriftSpeed = 3.2f;

        [Range(0f, 1f)] public float MaxSkidVolume = 0.75f;

        // Audio Sources
        private AudioSource _startSource;
        private AudioSource _engineSource;
        private AudioSource _skidSource;

        private Vehicle _vehicle;
        private Rigidbody _rb;
        private float _currentRPM = 1000f; // 1000 to 7500
        private int _currentGear = 1;
        private bool _isEngineStarted = false;
        private bool _isStarting = false;

        private void Awake()
        {
            _vehicle = GetComponent<Vehicle>();
            _rb = GetComponent<Rigidbody>();

            // Disable legacy engine sound to prevent audio conflicts
            JUVehicleEngineSound legacySound = GetComponent<JUVehicleEngineSound>();
            if (legacySound != null)
            {
                legacySound.enabled = false;
            }

            SetupAudioSources();
            EnsureTireSkidClip();
        }

        private void SetupAudioSources()
        {
            // Primary Engine Loop Source
            _engineSource = gameObject.AddComponent<AudioSource>();
            _engineSource.clip = EngineLoopClip;
            _engineSource.loop = true;
            _engineSource.playOnAwake = false;
            _engineSource.spatialBlend = 1f; // 3D Audio
            _engineSource.minDistance = 3f;
            _engineSource.maxDistance = 65f;
            _engineSource.rolloffMode = AudioRolloffMode.Linear;
            _engineSource.volume = 0f;

            // Ignition Start Source
            _startSource = gameObject.AddComponent<AudioSource>();
            _startSource.clip = StartEngineClip;
            _startSource.loop = false;
            _startSource.playOnAwake = false;
            _startSource.spatialBlend = 1f;
            _startSource.minDistance = 3f;
            _startSource.maxDistance = 45f;
            _startSource.volume = 0.9f;

            // Skid / Drift Source
            _skidSource = gameObject.AddComponent<AudioSource>();
            _skidSource.loop = true;
            _skidSource.playOnAwake = false;
            _skidSource.spatialBlend = 1f;
            _skidSource.minDistance = 2f;
            _skidSource.maxDistance = 50f;
            _skidSource.volume = 0f;
        }

        private void EnsureTireSkidClip()
        {
            if (TireSkidClip != null)
            {
                _skidSource.clip = TireSkidClip;
                return;
            }

            // Procedural generation of a 1.0s looping friction tire screech
            int sampleRate = 44100;
            int length = sampleRate; // 1 sec
            float[] samples = new float[length];
            System.Random rand = new System.Random(42);

            float phase1 = 0f;
            float phase2 = 0f;
            for (int i = 0; i < length; i++)
            {
                // Modulated dual sine tones (approx 1900 Hz & 2350 Hz) blended with band-shaped noise
                phase1 += 2f * Mathf.PI * 1920f / sampleRate;
                phase2 += 2f * Mathf.PI * 2380f / sampleRate;
                float tone = Mathf.Sin(phase1) * 0.4f + Mathf.Sin(phase2) * 0.3f;
                float noise = ((float)rand.NextDouble() * 2f - 1f) * 0.3f;
                samples[i] = (tone + noise) * 0.6f;
            }

            TireSkidClip = AudioClip.Create("ProceduralTireScreech", length, 1, sampleRate, false);
            TireSkidClip.SetData(samples, 0);
            _skidSource.clip = TireSkidClip;
        }

        private void Start()
        {
            if (_vehicle.IsOn)
            {
                StartCoroutine(StartEngineSequence());
            }
        }

        private void Update()
        {
            if (_vehicle == null) return;

            if (_vehicle.IsOn)
            {
                if (!_isEngineStarted && !_isStarting)
                {
                    StartCoroutine(StartEngineSequence());
                }

                UpdateEngineAudio();
                UpdateTireSkidAudio();
            }
            else
            {
                if (_isEngineStarted || _isStarting)
                {
                    StopEngine();
                }
            }
        }

        private IEnumerator StartEngineSequence()
        {
            _isStarting = true;

            if (_startSource != null && StartEngineClip != null)
            {
                _startSource.Play();
            }

            yield return new WaitForSeconds(0.45f);

            if (_engineSource != null && EngineLoopClip != null)
            {
                if (!_engineSource.isPlaying)
                {
                    _engineSource.Play();
                }
            }

            _isEngineStarted = true;
            _isStarting = false;
        }

        private void StopEngine()
        {
            _isEngineStarted = false;
            _isStarting = false;

            if (_engineSource != null && _engineSource.isPlaying)
            {
                _engineSource.pitch = Mathf.MoveTowards(_engineSource.pitch, 0.3f, 1.2f * Time.deltaTime);
                _engineSource.volume = Mathf.MoveTowards(_engineSource.volume, 0f, 1.5f * Time.deltaTime);
                if (_engineSource.volume <= 0.01f)
                {
                    _engineSource.Stop();
                }
            }

            if (_skidSource != null && _skidSource.isPlaying)
            {
                _skidSource.Stop();
            }
        }

        private void UpdateEngineAudio()
        {
            if (!_isEngineStarted || _engineSource == null || !_engineSource.isPlaying) return;

            float forwardSpeed = _vehicle.ForwardSpeed;
            float absSpeed = Mathf.Abs(forwardSpeed);
            float throttle = Mathf.Abs(_vehicle.FinalThrottle);
            bool isAccelerating = throttle > 0.05f;
            bool isReversing = forwardSpeed < -0.5f;

            float targetRPM;

            if (isReversing)
            {
                // Reverse Gear
                float revFraction = Mathf.Clamp01(absSpeed / 12f);
                targetRPM = Mathf.Lerp(1200f, 5500f, revFraction);
            }
            else
            {
                // Calculate simulated gear and RPM
                float speedPerGear = TopSpeed / NumberOfGears;
                _currentGear = Mathf.Clamp(Mathf.FloorToInt(absSpeed / speedPerGear) + 1, 1, NumberOfGears);

                float gearMinSpeed = (_currentGear - 1) * speedPerGear;
                float gearMaxSpeed = _currentGear * speedPerGear;
                float gearFraction = Mathf.Clamp01((absSpeed - gearMinSpeed) / (gearMaxSpeed - gearMinSpeed + 0.01f));

                // High-RPM redline per gear
                float baseGearRPM = Mathf.Lerp(1600f, 7200f, gearFraction);

                // Idle purr when vehicle is stopped
                if (absSpeed < 1.0f && !isAccelerating)
                {
                    targetRPM = 1000f;
                }
                else
                {
                    targetRPM = baseGearRPM;
                    if (isAccelerating)
                    {
                        targetRPM += 500f * throttle; // Snappy rev surge on throttle press
                    }
                }
            }

            // Smooth RPM transition
            float lerpRate = isAccelerating ? AccelerationLerpSpeed : DecelerationLerpSpeed;
            _currentRPM = Mathf.Lerp(_currentRPM, targetRPM, lerpRate * Time.deltaTime);

            // Map RPM to Pitch
            float normalizedRPM = Mathf.Clamp01((_currentRPM - 1000f) / 6200f);
            float targetPitch = Mathf.Lerp(IdlePitch, RedlinePitch, normalizedRPM);

            if (isReversing)
            {
                targetPitch = Mathf.Lerp(IdlePitch, ReversePitch, normalizedRPM);
            }

            // Map Volume: quiet rumble at idle, full throttle roar on acceleration
            float targetVolume;
            if (absSpeed < 1.0f && !isAccelerating)
            {
                targetVolume = IdleVolume;
            }
            else if (isAccelerating)
            {
                targetVolume = Mathf.Lerp(CruisingVolume, MaxAccelerationVolume, throttle);
            }
            else
            {
                // Engine braking / coasting volume
                targetVolume = Mathf.Lerp(IdleVolume, CruisingVolume, normalizedRPM * 0.8f);
            }

            _engineSource.pitch = Mathf.Lerp(_engineSource.pitch, targetPitch, 8f * Time.deltaTime);
            _engineSource.volume = Mathf.Lerp(_engineSource.volume, targetVolume, 6f * Time.deltaTime);
        }

        private void UpdateTireSkidAudio()
        {
            if (_skidSource == null) return;

            if (!_vehicle.IsGrounded)
            {
                _skidSource.volume = Mathf.MoveTowards(_skidSource.volume, 0f, 4f * Time.deltaTime);
                return;
            }

            // Calculate sideways slip velocity
            float lateralSpeed = Vector3.Dot(_vehicle.Velocity, transform.right);
            float absLateral = Mathf.Abs(lateralSpeed);

            // Also check heavy braking at high speed
            bool isBrakingHard = _vehicle.FinalBrake > 0.6f && Mathf.Abs(_vehicle.ForwardSpeed) > 5.5f;

            float skidIntensity = 0f;
            if (absLateral > MinDriftSpeed)
            {
                skidIntensity = Mathf.Clamp01((absLateral - MinDriftSpeed) / 6f);
            }
            if (isBrakingHard)
            {
                skidIntensity = Mathf.Max(skidIntensity, 0.65f);
            }

            if (skidIntensity > 0.05f)
            {
                if (!_skidSource.isPlaying)
                {
                    _skidSource.Play();
                }

                _skidSource.volume = Mathf.Lerp(_skidSource.volume, skidIntensity * MaxSkidVolume, 12f * Time.deltaTime);
                _skidSource.pitch = Mathf.Lerp(0.95f, 1.25f, skidIntensity);
            }
            else
            {
                _skidSource.volume = Mathf.MoveTowards(_skidSource.volume, 0f, 6f * Time.deltaTime);
                if (_skidSource.volume <= 0.01f && _skidSource.isPlaying)
                {
                    _skidSource.Stop();
                }
            }
        }

        private void OnDisable()
        {
            if (_engineSource != null) _engineSource.Stop();
            if (_skidSource != null) _skidSource.Stop();
            if (_startSource != null) _startSource.Stop();
            _isEngineStarted = false;
            _isStarting = false;
        }
    }
}
