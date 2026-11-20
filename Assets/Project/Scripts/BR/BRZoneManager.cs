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
                    _instance = FindFirstObjectByType<BRZoneManager>();
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
        [Tooltip("Total height of the storm wall. It starts at Wall Bottom and must clear the tallest terrain inside the play area.")]
        [SerializeField] private float wallHeight = 320f;
        [SerializeField] private float wallBottom = -60f;
        [SerializeField] private Material stormWallMaterial;

        [Header("Play area")]
        [Tooltip("Take the circle's center and size from BRMatchSetup (Map Center / Map Radius), so spawns, loot and storm share ONE play area.")]
        [SerializeField] private bool followMatchSetup = true;
        [Tooltip("The first ring is this much wider than the area where spawns and loot are placed.")]
        [SerializeField] private float outerMargin = 12f;
        [Tooltip("The phase target radii below were designed for a ring of this size; they are scaled to the real play area.")]
        [SerializeField] private float referenceRadius = 550f;

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

        [Header("Match Flow (dedicated server)")]
        [Tooltip("Seconds the final zone state is shown before a new round starts.")]
        [SerializeField] private float roundRestartDelay = 25f;

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

        // phases with the target radii scaled to the real play area (the serialized ones are only a template)
        private ZonePhase[] activePhases;

        /// <summary>Radius of the opening ring (the whole play area).</summary>
        public float InitialRadius => initialRadius;
        public Vector3 InitialCenter => initialCenter;

        /// <summary>
        /// Zone for AI code: the circle bots must stay in. While a ring is shrinking this is the ring it is shrinking TO,
        /// so bots start walking before the storm reaches them. Returns false when there is no zone in the scene.
        /// </summary>
        public static bool TryGetSafeCircle(out Vector3 center, out float radius, out bool shrinking)
        {
            var z = _instance;
            if (z == null) { center = Vector3.zero; radius = 0f; shrinking = false; return false; }
            shrinking = z.IsShrinking;
            center = shrinking ? z.TargetCenter : z.CurrentCenter;
            radius = shrinking ? z.TargetRadius : z.CurrentRadius;
            return true;
        }

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

        /// <summary>Name of the active zone phase (e.g. "Phase 2 - Sector Choke"); used by the HUD objective card.</summary>
        public string CurrentPhaseName => (phases != null && CurrentPhaseIndex >= 0 && CurrentPhaseIndex < phases.Length) ? phases[CurrentPhaseIndex].PhaseName : string.Empty;
        public int PhaseCount => phases != null ? phases.Length : 0;

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

            ResolvePlayArea();

            localStartRadius = initialRadius;
            localTargetRadius = activePhases.Length > 0 ? activePhases[0].TargetRadius : initialRadius;
            localStartCenter = initialCenter;
            localTargetCenter = initialCenter;
            localPhaseDuration = activePhases.Length > 0 ? activePhases[0].WaitDuration : 120f;
            localTimeRemaining = localPhaseDuration;

            interpolatedRadius = initialRadius;
            interpolatedCenter = initialCenter;

            SetupVisualBarrier();

            // the match state machine (alive count, killfeed, winner) lives next to the storm
            BRMatchCoordinator.EnsureExists(gameObject);
        }

        /// <summary>Center/size of the opening ring from the match setup, and phase radii scaled to it.</summary>
        private void ResolvePlayArea()
        {
            if (followMatchSetup)
            {
                var setup = FindFirstObjectByType<BRMatchSetup>();
                if (setup != null && setup.MapRadius > 1f)
                {
                    initialCenter = new Vector3(setup.MapCenter.x, 0f, setup.MapCenter.z);
                    initialRadius = setup.MapRadius + Mathf.Max(0f, outerMargin);
                }
            }

            float scale = referenceRadius > 1f ? initialRadius / referenceRadius : 1f;
            activePhases = new ZonePhase[phases.Length];
            for (int i = 0; i < phases.Length; i++)
            {
                var ph = phases[i];
                ph.TargetRadius = ph.TargetRadius * scale;
                activePhases[i] = ph;
            }
        }

        private void Start()
        {
            if (IsAuthority)
            {
                // ZoneRoutine resets the synced state itself and, on a networked server, waits for the first player
                StartCoroutine(ZoneRoutine());
            }
        }

        private void Update()
        {
            UpdateInterpolatedZone();
            UpdateVisualBarrier();
            MonitorLocalPlayerSafeZone();
            ClientNotifyTick();

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
                }
                else if (Time.time - lastOutsideWarningTime > 12.0f)
                {
                    lastOutsideWarningTime = Time.time;
                }
            }
            else
            {
                if (!wasLocalPlayerInsideSafeZone)
                {
                    wasLocalPlayerInsideSafeZone = true;
                }
            }
        }

        private Material ringMaterial;
        private static readonly int UnlitColorId = Shader.PropertyToID("_UnlitColor");
        private static readonly int UseVertexColorId = Shader.PropertyToID("_UseVertexColor");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private void SetupVisualBarrier()
        {
            if (ringVisualTransform != null) return;
            // a headless server has nothing to draw
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;

            var go = new GameObject("StormRingVisual");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = BuildRingMesh(160);
            var rend = go.AddComponent<MeshRenderer>();
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
            rend.allowOcclusionWhenDynamic = false;

            Material src = stormWallMaterial;
            if (src == null) src = Resources.Load<Material>("Materials/M_StormRing");
            if (src == null) src = LoadStormMaterialDirect();
            if (src != null)
            {
                // own instance: the wall is tinted and pulsed per frame, the shared asset stays untouched
                ringMaterial = new Material(src) { name = "StormRing (runtime)" };
                if (ringMaterial.HasProperty(UseVertexColorId)) ringMaterial.SetFloat(UseVertexColorId, 1f);   // the alpha ramp comes from the mesh
                rend.sharedMaterial = ringMaterial;
            }

            ringVisualTransform = go.transform;
            UpdateVisualBarrier();
        }

        /// <summary>
        /// Open tube of radius 1 and height 1 (scaled at runtime). Vertex alpha: a bright band at the ground that fades
        /// towards the sky, so the border reads from far away without hiding the sky or the terrain behind it.
        /// Both faces are drawn (inside and outside); the tube has no collider.
        /// </summary>
        private static Mesh BuildRingMesh(int segments)
        {
            float[] ys = { 0f, 0.02f, 0.45f, 1f };
            float[] alphas = { 0.85f, 0.6f, 0.25f, 0.05f };
            int rings = ys.Length;
            var verts = new Vector3[(segments + 1) * rings];
            var cols = new Color[verts.Length];
            var uvs = new Vector2[verts.Length];
            for (int r = 0; r < rings; r++)
            {
                for (int i = 0; i <= segments; i++)
                {
                    float ang = (float)i / segments * Mathf.PI * 2f;
                    int idx = r * (segments + 1) + i;
                    verts[idx] = new Vector3(Mathf.Cos(ang), ys[r], Mathf.Sin(ang));
                    cols[idx] = new Color(1f, 1f, 1f, alphas[r]);
                    uvs[idx] = new Vector2((float)i / segments, ys[r]);
                }
            }
            var tris = new List<int>();
            for (int r = 0; r < rings - 1; r++)
            {
                for (int i = 0; i < segments; i++)
                {
                    int a = r * (segments + 1) + i, b = a + 1, c = a + (segments + 1), d = c + 1;
                    tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(b); tris.Add(c); tris.Add(d);   // one winding
                    tris.Add(a); tris.Add(b); tris.Add(c); tris.Add(b); tris.Add(d); tris.Add(c);   // and the other
                }
            }
            var mesh = new Mesh { name = "StormRingTube" };
            mesh.vertices = verts;
            mesh.colors = cols;
            mesh.uv = uvs;
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.bounds = new Bounds(new Vector3(0f, 0.5f, 0f), new Vector3(2f, 1f, 2f));
            return mesh;
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

            float radius = Mathf.Max(0.5f, CurrentRadius);
            Vector3 center = CurrentCenter;

            ringVisualTransform.position = new Vector3(center.x, wallBottom, center.z);
            ringVisualTransform.localScale = new Vector3(radius, wallHeight, radius);

            if (ringMaterial != null)
            {
                // slow breathing, and redder while the ring is closing in
                float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 1.6f);
                Color calm = new Color(0.06f, 0.34f, 0.95f, 1f);      // HDR: the wall is additive, so it glows against sky and ground alike
                Color closing = new Color(0.95f, 0.22f, 0.06f, 1f);
                Color c = Color.Lerp(calm, closing, IsShrinking ? 1f : 0f);
                c.a = pulse;
                if (ringMaterial.HasProperty(UnlitColorId)) ringMaterial.SetColor(UnlitColorId, c);
                if (ringMaterial.HasProperty(BaseColorId)) ringMaterial.SetColor(BaseColorId, c);
            }
        }

        /// <summary>True on the networked server only; offline play and clients never wait for players.</summary>
        private bool IsDedicatedAuthority => IsNetworkedActive && IsServer;

        private int ConnectedPlayerCount()
        {
            var nm = NetworkManager.Singleton;
            return nm != null && nm.IsServer ? nm.ConnectedClientsList.Count : 0;
        }

        /// <summary>
        /// Match loop. On a networked server the zone does NOT run until a player is connected (otherwise the whole
        /// match burns down while the server sits empty and every late joiner lands in a finished, zero-radius zone).
        /// A round restarts when everybody leaves and again a short while after the final collapse.
        /// </summary>
        private static bool LaunchedAsServer()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return true;
            foreach (string a in System.Environment.GetCommandLineArgs())
            {
                if (string.Equals(a, "-server", System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(a, "-dedicated", System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(a, "-host", System.StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private IEnumerator ZoneRoutine()
        {
            // a server process may still be bringing the network up when this component starts: wait for it,
            // otherwise the "wait for a player" gate below would be skipped and the round would burn down empty
            if (LaunchedAsServer())
            {
                float waited = 0f;
                while (!IsNetworkedActive && waited < 30f)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            while (true)
            {
                ResetZoneToInitial();

                // hold phase 1 until somebody is actually on the server
                while (IsDedicatedAuthority && ConnectedPlayerCount() == 0) yield return null;

                // and until the match coordinator has finished the warmup countdown
                while (BRMatchCoordinator.Instance != null && !BRMatchCoordinator.Instance.ZoneMayRun) yield return null;

                yield return StartCoroutine(RunRound());

                // offline / listen-less play: a single round, the final state stays on screen
                if (!IsDedicatedAuthority) yield break;

                // round finished (or everyone left): short breather, then start fresh
                float hold = roundRestartDelay;
                while (hold > 0f && ConnectedPlayerCount() > 0)
                {
                    hold -= Time.deltaTime;
                    yield return null;
                }
            }
        }

        private bool RoundAborted()
        {
            // clients never drive the zone; a server with nobody connected abandons the round
            if (IsNetworkedActive && !IsServer) return true;
            return IsDedicatedAuthority && ConnectedPlayerCount() == 0;
        }

        private IEnumerator RunRound()
        {
            for (int p = 0; p < activePhases.Length; p++)
            {
                ZonePhase phase = activePhases[p];

                // 1. Wait phase
                SetPhaseWait(p, phase.WaitDuration);
                float timer = phase.WaitDuration;

                // Reset per-phase notification flags
                notified30sWarning = false;
                notified10sWarning = false;
                notifiedShrinkStart = false;


                while (timer > 0f)
                {
                    if (RoundAborted()) yield break;
                    timer -= Time.deltaTime;
                    UpdateTimeRemaining(timer);

                    // Tactical threshold notifications
                    if (timer <= 30.5f && timer > 28.5f && !notified30sWarning)
                    {
                        notified30sWarning = true;
                    }
                    else if (timer <= 10.5f && timer > 8.5f && !notified10sWarning)
                    {
                        notified10sWarning = true;
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

                if (p == activePhases.Length - 1)
                {
                }
                else
                {
                }

                timer = phase.ShrinkDuration;
                while (timer > 0f)
                {
                    if (RoundAborted()) yield break;
                    timer -= Time.deltaTime;
                    UpdateTimeRemaining(timer);
                    yield return null;
                }

                // Finalize ring at exact target before starting next wait phase
                SetPhaseFinalized(phase.TargetRadius, targetC);
            }
        }

        /// <summary>Puts the zone (local + synced values) back to its opening state: phase 1, full radius, waiting.</summary>
        private void ResetZoneToInitial()
        {
            localStartRadius = initialRadius;
            localTargetRadius = activePhases.Length > 0 ? activePhases[0].TargetRadius : initialRadius;
            localStartCenter = initialCenter;
            localTargetCenter = initialCenter;
            localIsShrinking = false;
            localPhaseIndex = 0;
            localPhaseDuration = activePhases.Length > 0 ? activePhases[0].WaitDuration : 120f;
            localTimeRemaining = localPhaseDuration;

            if (IsNetworkedActive && IsServer)
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
        }

        // ---- client side: the routine only runs on the authority, so mirror its announcements from the synced state ----
        private int clientPhaseSeen = -1;
        private bool clientShrinkSeen;

        private void ClientNotifyTick()
        {
            if (!IsNetworkedActive || IsServer) return;

            int p = NetCurrentPhase.Value;
            bool shrinking = NetIsShrinking.Value;
            float t = NetTimeRemaining.Value;

            if (p != clientPhaseSeen)
            {
                bool firstSight = clientPhaseSeen < 0;
                clientPhaseSeen = p;
                clientShrinkSeen = shrinking;
                notified30sWarning = notified10sWarning = false;
            }

            if (shrinking)
            {
                if (!clientShrinkSeen)
                {
                    clientShrinkSeen = true;
                }
            }
            else
            {
                clientShrinkSeen = false;
                if (t <= 30.5f && t > 28.5f && !notified30sWarning)
                {
                    notified30sWarning = true;
                }
                else if (t <= 10.5f && t > 8.5f && !notified10sWarning)
                {
                    notified10sWarning = true;
                }
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

        private readonly List<JUHealth> stormTargets = new List<JUHealth>();
        private float stormScanTimer;

        private void ApplyStormDamage()
        {
            int pIndex = Mathf.Clamp(CurrentPhaseIndex, 0, activePhases.Length - 1);
            float dps = activePhases[pIndex].DamagePerSecond;
            float radius = CurrentRadius;
            float radiusSq = radius * radius;
            Vector3 center = CurrentCenter;

            // the list of bodies is refreshed every few seconds (a scene-wide search on every tick is wasteful)
            stormScanTimer -= 1f;
            if (stormScanTimer <= 0f || stormTargets.Count == 0)
            {
                stormScanTimer = 3f;
                stormTargets.Clear();
                stormTargets.AddRange(FindObjectsByType<JUHealth>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
            }

            for (int i = stormTargets.Count - 1; i >= 0; i--)
            {
                var h = stormTargets[i];
                if (h == null) { stormTargets.RemoveAt(i); continue; }
                if (h.IsDead) continue;

                Vector3 pos = h.transform.position;
                float dx = pos.x - center.x, dz = pos.z - center.z;
                float distSq = dx * dx + dz * dz;
                if (distSq <= radiusSq) continue;

                // the further out, the harder it burns: a body far outside the play area cannot linger
                float outside = Mathf.Sqrt(distSq) - radius;
                float mult = 1f + Mathf.Min(4f, outside / 40f);
                JUHealth.PendingAttacker = null;   // storm damage is nobody's kill
                h.DoDamage(dps * mult);
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
