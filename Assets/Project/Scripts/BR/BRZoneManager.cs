using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using JUTPS;

namespace Railbound.BR
{
    /// <summary>
    /// Manages the Battle Royale Safe Zone / Shrinking Storm Ring.
    /// Handles Netcode state synchronization for multiplayer, with automatic fallback for offline/editor play testing.
    /// </summary>
    public class BRZoneManager : NetworkBehaviour
    {
        public static BRZoneManager Instance { get; private set; }

        [System.Serializable]
        public struct ZonePhase
        {
            public string PhaseName;
            public float WaitDuration;
            public float ShrinkDuration;
            public float TargetRadius;
            public float DamagePerSecond;

            public ZonePhase(string name, float wait, float shrink, float targetR, float dps)
            {
                PhaseName = name;
                WaitDuration = wait;
                ShrinkDuration = shrink;
                TargetRadius = targetR;
                DamagePerSecond = dps;
            }
        }

        [Header("Zone Configuration")]
        [SerializeField] private float initialRadius = 300f;
        [SerializeField] private Vector3 initialCenter = new Vector3(0f, 0f, 15f);
        [SerializeField] private float wallHeight = 100f;
        [SerializeField] private Material stormWallMaterial;

        [Header("Phases")]
        [SerializeField] private ZonePhase[] phases = new ZonePhase[]
        {
            new ZonePhase("Phase 1", 60f, 45f, 200f, 1.5f),
            new ZonePhase("Phase 2", 45f, 35f, 120f, 3.0f),
            new ZonePhase("Phase 3", 30f, 25f, 60f, 6.0f),
            new ZonePhase("Phase 4", 20f, 20f, 25f, 10.0f),
            new ZonePhase("Phase 5 - Final", 15f, 15f, 0f, 15.0f)
        };

        [Header("State (Runtime)")]
        public NetworkVariable<Vector3> NetCenter = new NetworkVariable<Vector3>(
            Vector3.zero,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public NetworkVariable<float> NetRadius = new NetworkVariable<float>(
            300f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public NetworkVariable<int> NetCurrentPhase = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public NetworkVariable<bool> NetIsShrinking = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public NetworkVariable<float> NetTimeRemaining = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        // Offline / Local fallback values
        private Vector3 localCenter;
        private float localRadius;
        private int localPhaseIndex = 0;
        private bool localIsShrinking = false;
        private float localTimeRemaining = 0f;

        private Transform ringVisualTransform;
        private float damageTimer = 0f;

        public Vector3 CurrentCenter => IsNetworkedActive ? NetCenter.Value : localCenter;
        public float CurrentRadius => IsNetworkedActive ? NetRadius.Value : localRadius;
        public int CurrentPhaseIndex => IsNetworkedActive ? NetCurrentPhase.Value : localPhaseIndex;
        public bool IsShrinking => IsNetworkedActive ? NetIsShrinking.Value : localIsShrinking;
        public float TimeRemaining => IsNetworkedActive ? NetTimeRemaining.Value : localTimeRemaining;

        private bool IsNetworkedActive => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        private bool IsAuthority => !IsNetworkedActive || IsServer;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            localCenter = initialCenter;
            localRadius = initialRadius;

            SetupVisualBarrier();
        }

        private void Start()
        {
            if (IsAuthority)
            {
                if (IsNetworkedActive)
                {
                    NetCenter.Value = initialCenter;
                    NetRadius.Value = initialRadius;
                    NetCurrentPhase.Value = 0;
                    NetIsShrinking.Value = false;
                }
                StartCoroutine(ZoneRoutine());
            }
        }

        private void Update()
        {
            UpdateVisualBarrier();

            if (IsAuthority)
            {
                damageTimer += Time.deltaTime;
                if (damageTimer >= 1.0f)
                {
                    damageTimer = 0f;
                    ApplyStormDamage();
                }
            }
        }

        private void SetupVisualBarrier()
        {
            if (ringVisualTransform != null) return;

            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = "StormRingVisual";
            cylinder.transform.SetParent(transform);

            // Remove physical collider so bullets and players pass through
            var col = cylinder.GetComponent<Collider>();
            if (col != null) Destroy(col);

            if (stormWallMaterial != null)
            {
                var rend = cylinder.GetComponent<MeshRenderer>();
                if (rend != null) rend.sharedMaterial = stormWallMaterial;
            }
            else
            {
                var loadedMat = Resources.Load<Material>("Materials/M_StormRing");
                if (loadedMat == null) loadedMat = LoadStormMaterialDirect();
                if (loadedMat != null)
                {
                    var rend = cylinder.GetComponent<MeshRenderer>();
                    if (rend != null) rend.sharedMaterial = loadedMat;
                }
            }

            ringVisualTransform = cylinder.transform;
            UpdateVisualBarrier();
        }

        private Material LoadStormMaterialDirect()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Project/Materials/M_StormRing.mat");
#else
            return null;
#endif
        }

        private void UpdateVisualBarrier()
        {
            if (ringVisualTransform == null) return;

            float radius = CurrentRadius;
            Vector3 center = CurrentCenter;

            // Cylinder default primitive has diameter=1, height=2
            ringVisualTransform.position = new Vector3(center.x, wallHeight * 0.5f, center.z);
            ringVisualTransform.localScale = new Vector3(radius * 2f, wallHeight * 0.5f, radius * 2f);
        }

        private IEnumerator ZoneRoutine()
        {
            for (int p = 0; p < phases.Length; p++)
            {
                ZonePhase phase = phases[p];

                // 1. Wait phase
                SetPhaseState(p, false, phase.WaitDuration);
                float timer = phase.WaitDuration;
                while (timer > 0f)
                {
                    timer -= Time.deltaTime;
                    UpdateTimeRemaining(timer);
                    yield return null;
                }

                // Calculate next center (shrink slightly towards a random offset inside the current radius)
                Vector3 startCenter = CurrentCenter;
                float maxShift = (CurrentRadius - phase.TargetRadius) * 0.4f;
                Vector2 randomOffset = Random.insideUnitCircle * Mathf.Max(0f, maxShift);
                Vector3 targetCenter = startCenter + new Vector3(randomOffset.x, 0f, randomOffset.y);

                float startRadius = CurrentRadius;
                float targetRadius = phase.TargetRadius;

                // 2. Shrink phase
                SetPhaseState(p, true, phase.ShrinkDuration);
                timer = phase.ShrinkDuration;
                float elapsed = 0f;

                while (elapsed < phase.ShrinkDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / phase.ShrinkDuration);
                    // Smoothstep interpolation
                    float smoothT = t * t * (3f - 2f * t);

                    float currentR = Mathf.Lerp(startRadius, targetRadius, smoothT);
                    Vector3 currentC = Vector3.Lerp(startCenter, targetCenter, smoothT);

                    SetRadiusAndCenter(currentR, currentC);
                    UpdateTimeRemaining(phase.ShrinkDuration - elapsed);

                    yield return null;
                }

                SetRadiusAndCenter(targetRadius, targetCenter);
            }
        }

        private void SetPhaseState(int phaseIndex, bool isShrinking, float duration)
        {
            localPhaseIndex = phaseIndex;
            localIsShrinking = isShrinking;
            localTimeRemaining = duration;

            if (IsNetworkedActive && IsServer)
            {
                NetCurrentPhase.Value = phaseIndex;
                NetIsShrinking.Value = isShrinking;
                NetTimeRemaining.Value = duration;
            }
        }

        private void SetRadiusAndCenter(float radius, Vector3 center)
        {
            localRadius = radius;
            localCenter = center;

            if (IsNetworkedActive && IsServer)
            {
                NetRadius.Value = radius;
                NetCenter.Value = center;
            }
        }

        private void UpdateTimeRemaining(float time)
        {
            localTimeRemaining = Mathf.Max(0f, time);
            if (IsNetworkedActive && IsServer)
            {
                NetTimeRemaining.Value = localTimeRemaining;
            }
        }

        private void ApplyStormDamage()
        {
            int pIndex = Mathf.Clamp(CurrentPhaseIndex, 0, phases.Length - 1);
            float dps = phases[pIndex].DamagePerSecond;
            float radiusSq = CurrentRadius * CurrentRadius;
            Vector3 center = CurrentCenter;

            var allHealths = FindObjectsByType<JUHealth>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var h in allHealths)
            {
                if (h == null || h.IsDead) continue;

                Vector3 pos = h.transform.position;
                float distSq = (pos.x - center.x) * (pos.x - center.x) + (pos.z - center.z) * (pos.z - center.z);

                if (distSq > radiusSq)
                {
                    h.DoDamage(dps);
                }
            }
        }

        /// <summary>
        /// Public utility check: returns true if the position is safely inside the circle.
        /// </summary>
        public bool IsPositionInSafeZone(Vector3 position)
        {
            Vector3 center = CurrentCenter;
            float distSq = (position.x - center.x) * (position.x - center.x) + (position.z - center.z) * (position.z - center.z);
            return distSq <= (CurrentRadius * CurrentRadius);
        }
    }
}
