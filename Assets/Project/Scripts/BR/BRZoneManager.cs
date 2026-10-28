using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using JUTPS;

namespace Railbound.BR
{
    /// <summary>
    /// Manages the Battle Royale Safe Zone / Shrinking Storm Ring with realistic tactical pacing.
    /// Implements continuous client-side smooth interpolation synchronized with server authoritative state.
    /// Automatically triggers Reach UI Feed Notifications (Yellow warnings, Red critical alerts, Cyan info)
    /// and tactical notification audio clips.
    /// </summary>
    public class BRZoneManager : NetworkBehaviour
    {
        private static BRZoneManager _instance;
        public static BRZoneManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<BRZoneManager>();
                }
                return _instance;
            }
            private set => _instance = value;
        }

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
        [SerializeField] private float initialRadius = 550f;
        [SerializeField] private Vector3 initialCenter = new Vector3(0f, 0f, 15f);
        [SerializeField] private float wallHeight = 120f;
        [SerializeField] private Material stormWallMaterial;

        [Header("Realistic Battle Royale Phases (Paced like Warzone / Apex)")]
        [SerializeField] private ZonePhase[] phases = new ZonePhase[]
        {
            new ZonePhase("Phase 1 - Infiltration", 120f, 90f, 380f, 1.0f),
            new ZonePhase("Phase 2 - Sector Choke", 90f, 75f, 240f, 2.0f),
            new ZonePhase("Phase 3 - Tactical Closure", 75f, 60f, 140f, 4.0f),
            new ZonePhase("Phase 4 - Enclosure", 60f, 50f, 70f, 7.0f),
            new ZonePhase("Phase 5 - Endgame", 45f, 40f, 25f, 10.0f),
            new ZonePhase("Phase 6 - Final Collapse", 30f, 30f, 0f, 18.0f)
        };

        [Header("Network Sync State")]
        public NetworkVariable<int> NetCurrentPhase = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<bool> NetIsShrinking = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<float> NetTimeRemaining = new NetworkVariable<float>(
            120f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<float> NetPhaseDuration = new NetworkVariable<float>(
            120f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<float> NetStartRadius = new NetworkVariable<float>(
            550f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<float> NetTargetRadius = new NetworkVariable<float>(
            380f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<Vector3> NetStartCenter = new NetworkVariable<Vector3>(
            new Vector3(0f, 0f, 15f), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<Vector3> NetTargetCenter = new NetworkVariable<Vector3>(
            new Vector3(0f, 0f, 15f), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // Offline / Local fallback values
        private int localPhaseIndex = 0;
        private bool localIsShrinking = false;
        private float localTimeRemaining = 120f;
        private float localPhaseDuration = 120f;
        private float localStartRadius = 550f;
        private float localTargetRadius = 380f;
        private Vector3 localStartCenter = new Vector3(0f, 0f, 15f);
        private Vector3 localTargetCenter = new Vector3(0f, 0f, 15f);

        // Smooth continuous runtime state (interpolated locally every frame)
        private float interpolatedRadius;
        private Vector3 interpolatedCenter;

        private Transform ringVisualTransform;
        private float damageTimer = 0f;

        // Notification debouncers
        private int lastNotifiedPhase = -1;
        private bool notified30sWarning = false;
        private bool notified10sWarning = false;
        private bool notifiedShrinkStart = false;
        private bool wasLocalPlayerInsideSafeZone = true;
        private float lastOutsideWarningTime = -99f;
        private Transform localPlayerTransform;

        public Vector3 CurrentCenter => interpolatedCenter;
        public float CurrentRadius => interpolatedRadius;
        public int CurrentPhaseIndex => IsNetworkedActive ? NetCurrentPhase.Value : localPhaseIndex;
        public bool IsShrinking => IsNetworkedActive ? NetIsShrinking.Value : localIsShrinking;
        public float TimeRemaining => IsNetworkedActive ? NetTimeRemaining.Value : localTimeRemaining;
        public float PhaseDuration => IsNetworkedActive ? NetPhaseDuration.Value : localPhaseDuration;
        public float StartRadius => IsNetworkedActive ? NetStartRadius.Value : localStartRadius;
        public float TargetRadius => IsNetworkedActive ? NetTargetRadius.Value : localTargetRadius;
        public Vector3 StartCenter => IsNetworkedActive ? NetStartCenter.Value : localStartCenter;
        public Vector3 TargetCenter => IsNetworkedActive ? NetTargetCenter.Value : localTargetCenter;

        private bool IsNetworkedActive => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        private bool IsAuthority => !IsNetworkedActive || IsServer;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            localStartRadius = initialRadius;
            localTargetRadius = phases.Length > 0 ? phases[0].TargetRadius : initialRadius;
            localStartCenter = initialCenter;
            localTargetCenter = initialCenter;
            localPhaseDuration = phases.Length > 0 ? phases[0].WaitDuration : 120f;
            localTimeRemaining = localPhaseDuration;

            interpolatedRadius = initialRadius;
            interpolatedCenter = initialCenter;

            SetupVisualBarrier();
        }

        private void Start()
        {
            if (IsAuthority)
            {
                if (IsNetworkedActive)
                {
                    NetCurrentPhase.Value = 0;
                    NetIsShrinking.Value = false;
                    NetPhaseDuration.Value = localPhaseDuration;
                    NetTimeRemaining.Value = localPhaseDuration;
                    NetStartRadius.Value = localStartRadius;
                    NetTargetRadius.Value = localTargetRadius;
                    NetStartCenter.Value = localStartCenter;
                    NetTargetCenter.Value = localTargetCenter;
                }
                StartCoroutine(ZoneRoutine());
            }
        }

        private void Update()
        {
            UpdateInterpolatedZone();
            UpdateVisualBarrier();
            MonitorLocalPlayerSafeZone();

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

        private void UpdateInterpolatedZone()
        {
            bool shrinking = IsShrinking;
            float duration = PhaseDuration;
            float remaining = TimeRemaining;

            if (shrinking && duration > 0.01f)
            {
                float elapsed = Mathf.Clamp(duration - remaining, 0f, duration);
                float t = Mathf.Clamp01(elapsed / duration);
                // Silky smooth SmoothStep curve for natural military ring closure
                float smoothT = t * t * (3f - 2f * t);

                interpolatedRadius = Mathf.Lerp(StartRadius, TargetRadius, smoothT);
                interpolatedCenter = Vector3.Lerp(StartCenter, TargetCenter, smoothT);
            }
            else
            {
                interpolatedRadius = StartRadius;
                interpolatedCenter = StartCenter;
            }
        }

        private void MonitorLocalPlayerSafeZone()
        {
            if (localPlayerTransform == null)
            {
                var playerObj = GameObject.Find("TPS Character");
                if (playerObj != null) localPlayerTransform = playerObj.transform;
                if (localPlayerTransform == null) return;
            }

            bool isInside = IsPositionInSafeZone(localPlayerTransform.position);

            if (!isInside)
            {
                if (wasLocalPlayerInsideSafeZone)
                {
                    wasLocalPlayerInsideSafeZone = false;
                    lastOutsideWarningTime = Time.time;
                    BRFeedNotificationManager.Instance?.ShowCritical("CRITICAL: YOU ARE OUTSIDE THE SAFE ZONE - TAKE SHELTER!");
                }
                else if (Time.time - lastOutsideWarningTime > 12.0f)
                {
                    lastOutsideWarningTime = Time.time;
                    BRFeedNotificationManager.Instance?.ShowCritical("CRITICAL: TAKING STORM DAMAGE - RETURN TO SAFE ZONE!");
                }
            }
            else
            {
                if (!wasLocalPlayerInsideSafeZone)
                {
                    wasLocalPlayerInsideSafeZone = true;
                    BRFeedNotificationManager.Instance?.ShowInfo("SAFE: RE-ENTERED SAFE ZONE");
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

            ringVisualTransform.position = new Vector3(center.x, wallHeight * 0.5f, center.z);
            ringVisualTransform.localScale = new Vector3(radius * 2f, wallHeight * 0.5f, radius * 2f);
        }

        private IEnumerator ZoneRoutine()
        {
            for (int p = 0; p < phases.Length; p++)
            {
                ZonePhase phase = phases[p];

                // 1. Wait phase
                SetPhaseWait(p, phase.WaitDuration);
                float timer = phase.WaitDuration;

                // Reset per-phase notification flags
                notified30sWarning = false;
                notified10sWarning = false;
                notifiedShrinkStart = false;

                // Notify new phase
                BRFeedNotificationManager.Instance?.ShowInfo($"NEW SAFE ZONE LOCATED ({phase.PhaseName})");

                while (timer > 0f)
                {
                    timer -= Time.deltaTime;
                    UpdateTimeRemaining(timer);

                    // Tactical threshold notifications
                    if (timer <= 30.5f && timer > 28.5f && !notified30sWarning)
                    {
                        notified30sWarning = true;
                        BRFeedNotificationManager.Instance?.ShowWarning("WARNING: SAFE ZONE COLLAPSE IN 30 SECONDS");
                    }
                    else if (timer <= 10.5f && timer > 8.5f && !notified10sWarning)
                    {
                        notified10sWarning = true;
                        BRFeedNotificationManager.Instance?.ShowWarning("WARNING: SAFE ZONE COLLAPSE IN 10 SECONDS - MOVE NOW!");
                    }

                    yield return null;
                }

                // Calculate next center (smooth shift towards a random offset inside current radius)
                Vector3 currentC = StartCenter;
                float currentR = StartRadius;
                float maxShift = (currentR - phase.TargetRadius) * 0.35f;
                Vector2 randomOffset = Random.insideUnitCircle * Mathf.Max(0f, maxShift);
                Vector3 targetC = currentC + new Vector3(randomOffset.x, 0f, randomOffset.y);

                // 2. Shrink phase
                SetPhaseShrink(p, phase.ShrinkDuration, currentR, phase.TargetRadius, currentC, targetC);

                if (p == phases.Length - 1)
                {
                    BRFeedNotificationManager.Instance?.ShowCritical("CRITICAL: FINAL COLLAPSE INITIATED - ELIMINATE ALL HOSTILES!");
                }
                else
                {
                    BRFeedNotificationManager.Instance?.ShowWarning("RESTRICTED AREA: SAFE ZONE IS NOW SHRINKING");
                }

                timer = phase.ShrinkDuration;
                while (timer > 0f)
                {
                    timer -= Time.deltaTime;
                    UpdateTimeRemaining(timer);
                    yield return null;
                }

                // Finalize ring at exact target before starting next wait phase
                SetPhaseFinalized(phase.TargetRadius, targetC);
            }
        }

        private void SetPhaseWait(int phaseIndex, float duration)
        {
            localPhaseIndex = phaseIndex;
            localIsShrinking = false;
            localPhaseDuration = duration;
            localTimeRemaining = duration;

            if (IsNetworkedActive && IsServer)
            {
                NetCurrentPhase.Value = phaseIndex;
                NetIsShrinking.Value = false;
                NetPhaseDuration.Value = duration;
                NetTimeRemaining.Value = duration;
            }
        }

        private void SetPhaseShrink(int phaseIndex, float duration, float startR, float targetR, Vector3 startC, Vector3 targetC)
        {
            localPhaseIndex = phaseIndex;
            localIsShrinking = true;
            localPhaseDuration = duration;
            localTimeRemaining = duration;
            localStartRadius = startR;
            localTargetRadius = targetR;
            localStartCenter = startC;
            localTargetCenter = targetC;

            if (IsNetworkedActive && IsServer)
            {
                NetCurrentPhase.Value = phaseIndex;
                NetIsShrinking.Value = true;
                NetPhaseDuration.Value = duration;
                NetTimeRemaining.Value = duration;
                NetStartRadius.Value = startR;
                NetTargetRadius.Value = targetR;
                NetStartCenter.Value = startC;
                NetTargetCenter.Value = targetC;
            }
        }

        private void SetPhaseFinalized(float finalR, Vector3 finalC)
        {
            localStartRadius = finalR;
            localTargetRadius = finalR;
            localStartCenter = finalC;
            localTargetCenter = finalC;
            localIsShrinking = false;

            if (IsNetworkedActive && IsServer)
            {
                NetStartRadius.Value = finalR;
                NetTargetRadius.Value = finalR;
                NetStartCenter.Value = finalC;
                NetTargetCenter.Value = finalC;
                NetIsShrinking.Value = false;
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
