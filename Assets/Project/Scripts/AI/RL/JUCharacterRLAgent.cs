using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Policies;
using JUTPS;
using JUTPS.CharacterBrain;
using JUTPS.AI;
using Project.AI.Sensory;
using Project.AI.Skills;
using Project.AI.Learning;

namespace Project.AI.RL
{
    /// <summary>
    /// Reinforcement Learning Agent for JU TPS Characters using Unity ML-Agents (PPO / MA-POCA),
    /// Barracuda/Sentis tactical utility evaluation, 3D Sound Sensory perception,
    /// Kinematic Predictive Tracking (lead aiming), and Post-Match Evolutionary Learning.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(JUCharacterBrain))]
    [RequireComponent(typeof(BehaviorParameters))]
    [RequireComponent(typeof(DecisionRequester))]
    [AddComponentMenu("Project/AI/RL/JU Character RL Agent")]
    public class JUCharacterRLAgent : Agent, IAISoundListener
    {
        public const int VECTOR_OBSERVATION_SIZE = 24;
        public const int CONTINUOUS_ACTION_COUNT = 4;
        public static readonly int[] DISCRETE_BRANCH_SIZES = new int[] { 2, 2, 3, 3, 2, 2 };

        public enum CombatRole
        {
            RangedTactical = 0,
            MeleeRusher = 1
        }

        [Header("RL Agent Identity & Role")]
        public CombatRole Role = CombatRole.RangedTactical;
        public int TeamId = 1;
        public string[] TargetTags = new string[] { "Player" };
        public LayerMask VisibilityLayerMask = ~0;

        [Header("Dynamic Weights & Evolved Profile")]
        public AIProfileAsset ProfileTemplate;
        public string CustomProfileId;
        [SerializeField] private AIProfileData runtimeProfile;

        [Header("Predictive Kinematic Tracking")]
        [SerializeField] private AIPredictiveTracker predictiveTracker = new AIPredictiveTracker();

        [Header("Match Performance Tracker")]
        [SerializeField] private AIMatchTracker matchTracker;

        [Header("Combat & Sensor Parameters")]
        public float DetectionRadius = 120f;
        public float OptimalCombatRange = 14f;
        public float AimUpOffset = 1.05f;
        public float LookTargetSpeed = 8f;
        public float SafeZoneRadius = 250f;
        public Vector3 SafeZoneCenter = Vector3.zero;
        public WaypointPath PatrolWaypointPath;

        [Header("Recurrent Memory State (POMDP)")]
        [SerializeField] private Transform currentTarget;
        [SerializeField] private JUCharacterBrain currentTargetBrain;
        [SerializeField] private JUHealth currentTargetHealth;
        [SerializeField] private Vector3 lastKnownTargetPos;
        [SerializeField] private Vector3 lastKnownTargetVel;
        [SerializeField] private float timeSinceTargetSeen = 0f;
        [SerializeField] private bool hasTargetLineOfSight;

        [Header("Acoustic Threat Memory")]
        [SerializeField] private Vector3 threatLocation;
        [SerializeField] private Vector3 threatDirection;
        [SerializeField] private float threatMemoryTimer;
        [SerializeField] private bool hasAcousticThreat;
        private float soundReactionCooldown;
        private float suppressingFireTimer;

        [Header("Live Telemetry (Read-Only)")]
        public float CurrentEpisodeReward;
        public int CompletedEpisodesCount;
        public float LastDamageDealt;
        public float LastDamageTaken;

        private JUCharacterBrain characterBrain;
        private JUCharacterController characterController;
        private JUHealth selfHealth;
        private Rigidbody selfRb;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;

        private float prevSelfHealth;
        private float prevTargetHealth;
        private float strafePhase;
        private float targetScanTimer;
        private float equipCheckTimer;
        private Vector3 smoothedAimPosition;

        private Vector3[] cachedNavPath = new Vector3[0];
        private int currentNavCornerIndex;
        private int currentWaypointIndex;
        private float navPathRefreshTimer;
        private Vector3 roamDestination;
        private float roamRefreshTimer;
        private bool hasEvolvedThisDeath;

        // Cached actions from the latest ML-Agents step so JUCharacterController
        // receives smooth per-frame locomotion, rotation, and item inputs in Update().
        private float cachedMoveX;
        private float cachedMoveZ;
        private float cachedAimYawOffset;
        private float cachedAimPitchOffset;
        private bool cachedFireAction;
        private bool cachedAimAction;
        private int cachedStanceAction;
        private int cachedMobilityAction;
        private bool cachedReloadAction;
        private bool cachedSwitchAction;
        private bool hasReceivedAction;

        public JUCharacterBrain CharacterBrain => characterBrain;
        public JUHealth SelfHealth => selfHealth;
        public Transform CurrentTarget => currentTarget;
        public bool HasLineOfSight => hasTargetLineOfSight;
        public AIProfileData RuntimeProfile => runtimeProfile;
        public AIPredictiveTracker PredictiveTracker => predictiveTracker;
        public AIMatchTracker MatchTracker => matchTracker;

        // IAISoundListener implementation
        Vector3 IAISoundListener.ListenerPosition => transform.position;
        GameObject IAISoundListener.ListenerGameObject => gameObject;
        float IAISoundListener.HearingSensitivityMultiplier => runtimeProfile != null ? runtimeProfile.HearingSensitivity : 1.0f;

        private void Awake()
        {
            // Remove any duplicate base Unity.MLAgents.Agent components that Unity's RequireComponent
            // may have auto-added before JUCharacterRLAgent, so DecisionRequester always binds to this Agent.
            Agent[] allAgents = GetComponents<Agent>();
            for (int i = 0; i < allAgents.Length; i++)
            {
                if (allAgents[i] != null && allAgents[i] != this && allAgents[i].GetType() == typeof(Agent))
                {
                    allAgents[i].enabled = false;
                    Destroy(allAgents[i]);
                }
            }

            ConfigureCharacterAsAI();
            EnsureMatchTrackerAttached();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            AISoundSensorySystem.RegisterListener(this);
        }

        protected override void OnDisable()
        {
            AISoundSensorySystem.UnregisterListener(this);
            base.OnDisable();
        }

        private void EnsureMatchTrackerAttached()
        {
            if (matchTracker == null)
            {
                matchTracker = GetComponent<AIMatchTracker>();
                if (matchTracker == null)
                {
                    matchTracker = gameObject.AddComponent<AIMatchTracker>();
                }
            }
        }

        private void ConfigureCharacterAsAI()
        {
            if (characterBrain == null) characterBrain = GetComponent<JUCharacterBrain>();
            if (characterController == null) characterController = GetComponent<JUCharacterController>();
            if (selfHealth == null) selfHealth = GetComponent<JUHealth>();
            if (selfRb == null) selfRb = GetComponent<Rigidbody>();

            if (characterBrain != null)
            {
                characterBrain.IsArtificialIntelligence = true;
                characterBrain.MyPivotCamera = null;
            }

            if (characterController != null)
            {
                characterController.IsArtificialIntelligence = true;
                characterController.UseDefaultControllerInput = false;
                characterController.MyPivotCamera = null;
            }

            // Copy WaypointPath from legacy brains and keep them disabled so they don't conflict
            var legacyPatrol = GetComponent<PatrolAI>();
            if (legacyPatrol != null)
            {
                if (PatrolWaypointPath == null && legacyPatrol.WaypointPath != null)
                {
                    PatrolWaypointPath = legacyPatrol.WaypointPath;
                }
                if (legacyPatrol.enabled) legacyPatrol.enabled = false;
            }

            var legacyZombie = GetComponent<ZombieAI>();
            if (legacyZombie != null)
            {
                if (PatrolWaypointPath == null && legacyZombie.WaypointPath != null)
                {
                    PatrolWaypointPath = legacyZombie.WaypointPath;
                }
                if (legacyZombie.enabled) legacyZombie.enabled = false;
            }

            var legacyRandomDest = GetComponent<AIRandomDestination>();
            if (legacyRandomDest != null && legacyRandomDest.enabled)
            {
                legacyRandomDest.enabled = false;
            }
        }

        public override void Initialize()
        {
            ConfigureCharacterAsAI();
            EnsureMatchTrackerAttached();

            // Load evolved profile or create from template
            string profileId = !string.IsNullOrEmpty(CustomProfileId)
                ? CustomProfileId
                : (Role == CombatRole.RangedTactical ? "RangedTactical_Evolving" : "MeleeRusher_Evolving");

            runtimeProfile = AIProfileEvolutionSystem.LoadOrCreateProfile(profileId, ProfileTemplate);
            if (runtimeProfile != null)
            {
                OptimalCombatRange = runtimeProfile.OptimalRange;
                if (matchTracker != null)
                {
                    matchTracker.Initialize(runtimeProfile.ProfileId, runtimeProfile.TotalMatchesPlayed + 1);
                }
            }

            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
            smoothedAimPosition = transform.position + transform.forward * 10f + Vector3.up * AimUpOffset;
            strafePhase = Random.Range(0f, Mathf.PI * 2f);
            roamDestination = transform.position + transform.forward * 15f;
            hasEvolvedThisDeath = false;

            AcquireBestTarget();
            UpdateLineOfSightMemory();
            CacheHealthBaselines();
            EnsureWeaponEquippedIfNeeded();
        }

        public override void OnEpisodeBegin()
        {
            ConfigureCharacterAsAI();

            CurrentEpisodeReward = 0f;
            LastDamageDealt = 0f;
            LastDamageTaken = 0f;
            timeSinceTargetSeen = 0f;
            strafePhase = Random.Range(0f, Mathf.PI * 2f);
            hasEvolvedThisDeath = false;

            predictiveTracker.Reset();

            AcquireBestTarget();
            UpdateLineOfSightMemory();
            CacheHealthBaselines();
            EnsureWeaponEquippedIfNeeded();

            if (currentTarget != null)
            {
                lastKnownTargetPos = currentTarget.position;
            }
            else
            {
                lastKnownTargetPos = transform.position + transform.forward * 15f;
            }
        }

        private void CacheHealthBaselines()
        {
            prevSelfHealth = selfHealth != null ? selfHealth.Health : 100f;
            prevTargetHealth = currentTargetHealth != null ? currentTargetHealth.Health : 100f;
        }

        private void EnsureWeaponEquippedIfNeeded()
        {
            if (Role != CombatRole.RangedTactical || characterBrain == null) return;

            if (characterBrain.WeaponInUseRightHand == null &&
                characterBrain.HoldableItemInUseRightHand == null &&
                !characterBrain.IsWeaponSwitching &&
                characterBrain.Inventory != null &&
                characterBrain.Inventory.HoldableItensRightHand != null &&
                characterBrain.Inventory.HoldableItensRightHand.Length > 0)
            {
                for (int i = 0; i < characterBrain.Inventory.HoldableItensRightHand.Length; i++)
                {
                    var item = characterBrain.Inventory.HoldableItensRightHand[i];
                    if (item != null && item.Unlocked)
                    {
                        characterBrain.SwitchToItem(item.ItemSwitchID);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// IAISoundListener implementation:
        /// Reacts to 3D audio stimuli (gunfire, explosions) emitted by players or enemies.
        /// Calculates directional vector, assigns threat location, and triggers tactical investigation or cover.
        /// </summary>
        public void OnSoundHeard(SoundStimulus stimulus, Vector3 perceivedDirection, float perceivedDistance)
        {
            if (stimulus.Instigator == gameObject || (selfHealth != null && selfHealth.IsDead))
            {
                return;
            }

            // Only react to non-allied tags (e.g. Player)
            bool isEnemy = false;
            if (stimulus.Instigator != null)
            {
                foreach (string tag in TargetTags)
                {
                    if (stimulus.Instigator.CompareTag(tag))
                    {
                        isEnemy = true;
                        break;
                    }
                }
            }
            else
            {
                isEnemy = true;
            }

            if (!isEnemy) return;

            // Reaction time cooldown based on evolved profile
            if (soundReactionCooldown > 0f) return;
            soundReactionCooldown = runtimeProfile != null ? runtimeProfile.ReactionTime : 0.25f;

            if (matchTracker != null)
            {
                matchTracker.RecordSoundHeard(soundReactionCooldown);
            }

            // Calculate perceived threat location:
            // SoundTriangulation skill yields exact coordinates; otherwise add small realistic distance error
            Vector3 soundPos = stimulus.Position;
            if (runtimeProfile != null && !runtimeProfile.HasSkill(AISkill.SoundTriangulation))
            {
                float noise = (1f / Mathf.Max(0.5f, runtimeProfile.HearingSensitivity)) * (perceivedDistance * 0.08f);
                soundPos += Random.insideUnitSphere * noise;
                soundPos.y = stimulus.Position.y;
            }

            threatLocation = soundPos;
            threatDirection = perceivedDirection;
            threatMemoryTimer = 14f;
            hasAcousticThreat = true;

            // Decision: Evaluate Aggression vs CoverPriority
            float coverPrio = runtimeProfile != null ? runtimeProfile.CoverPriority : 0.5f;
            float aggression = runtimeProfile != null ? runtimeProfile.Aggression : 0.5f;

            if (coverPrio > aggression || (selfHealth != null && selfHealth.Health < 40f))
            {
                // Take defensive cover relative to threat vector
                if (matchTracker != null) matchTracker.RecordCoverEntered();
            }
            else if (runtimeProfile != null && runtimeProfile.HasSkill(AISkill.SuppressingFire) && Role == CombatRole.RangedTactical)
            {
                // Trigger suppressing fire burst towards sound origin
                suppressingFireTimer = 1.6f;
            }
            else if (matchTracker != null)
            {
                matchTracker.RecordThreatInvestigated();
            }
        }

        private void Update()
        {
            if (characterBrain == null)
            {
                ConfigureCharacterAsAI();
                if (characterBrain == null) return;
            }

            if (selfHealth != null && selfHealth.IsDead)
            {
                characterBrain._Move(0f, 0f, false);
                TriggerEvolutionOnDeath();
                return;
            }

            // Timers
            soundReactionCooldown -= Time.deltaTime;
            suppressingFireTimer -= Time.deltaTime;
            if (hasAcousticThreat)
            {
                threatMemoryTimer -= Time.deltaTime;
                if (threatMemoryTimer <= 0f)
                {
                    hasAcousticThreat = false;
                }
            }

            // Guarantee AI input flags stay locked so player input never overrides this agent
            characterBrain.IsArtificialIntelligence = true;
            if (characterController != null && characterController.UseDefaultControllerInput)
            {
                characterController.UseDefaultControllerInput = false;
            }

            // Periodically ensure ranged bot has its gun drawn
            equipCheckTimer -= Time.deltaTime;
            if (equipCheckTimer <= 0f)
            {
                equipCheckTimer = 1.0f;
                EnsureWeaponEquippedIfNeeded();
            }

            targetScanTimer -= Time.deltaTime;
            if (targetScanTimer <= 0f)
            {
                targetScanTimer = 0.2f;
                AcquireBestTarget();
                UpdateLineOfSightMemory();
            }
            else if (currentTarget != null && hasTargetLineOfSight)
            {
                lastKnownTargetPos = currentTarget.position;
            }

            // Update predictive kinematic tracker with current target position
            if (currentTarget != null)
            {
                predictiveTracker.UpdateTargetKinematics(currentTarget.position, Time.deltaTime);
            }

            // If DecisionRequester hasn't triggered yet or got bound to a removed component, request a decision
            if (!hasReceivedAction)
            {
                RequestDecision();
            }

            // Apply the current RL policy action every Update() frame so JUCharacterController
            // smoothly moves, rotates, aims, and fires/attacks at full frame rate.
            ApplyCachedActionsToCharacter();

            EvaluateStepRewards();
        }

        private void TriggerEvolutionOnDeath()
        {
            if (hasEvolvedThisDeath) return;
            hasEvolvedThisDeath = true;

            if (matchTracker != null && runtimeProfile != null)
            {
                var metrics = matchTracker.EndMatch();
                runtimeProfile = AIProfileEvolutionSystem.EvolveProfile(runtimeProfile, metrics);
            }
        }

        /// <summary>
        /// Collects 24 normalized vector observations representing self status, weapon state,
        /// target relative kinematics, line-of-sight memory, sound threat awareness, and safe-zone awareness.
        /// </summary>
        public override void CollectObservations(VectorSensor sensor)
        {
            if (characterBrain == null)
            {
                for (int i = 0; i < VECTOR_OBSERVATION_SIZE; i++) sensor.AddObservation(0f);
                return;
            }

            // 1. Self Health & State (5 floats)
            float normHealth = selfHealth != null && selfHealth.MaxHealth > 0f
                ? Mathf.Clamp01(selfHealth.Health / selfHealth.MaxHealth)
                : 1f;
            sensor.AddObservation(normHealth);
            sensor.AddObservation(characterBrain.IsGrounded ? 1f : 0f);
            sensor.AddObservation(characterBrain.IsCrouched ? 1f : 0f);
            sensor.AddObservation(characterBrain.IsRolling ? 1f : 0f);
            sensor.AddObservation(characterBrain.FiringMode ? 1f : 0f);

            // 2. Local Velocity (3 floats)
            Vector3 localVel = selfRb != null
                ? transform.InverseTransformDirection(selfRb.velocity) / 10f
                : Vector3.zero;
            sensor.AddObservation(Mathf.Clamp(localVel.x, -1f, 1f));
            sensor.AddObservation(Mathf.Clamp(localVel.y, -1f, 1f));
            sensor.AddObservation(Mathf.Clamp(localVel.z, -1f, 1f));

            // 3. Weapon & Ammo Status (4 floats)
            bool hasRangedWeapon = characterBrain.WeaponInUseRightHand != null;
            float normAmmoInMag = 0f;
            float hasReserveAmmo = 0f;
            float isReloading = characterBrain.IsReloading ? 1f : 0f;
            if (hasRangedWeapon)
            {
                var w = characterBrain.WeaponInUseRightHand;
                normAmmoInMag = w.BulletsPerMagazine > 0
                    ? Mathf.Clamp01((float)w.BulletsAmounts / w.BulletsPerMagazine)
                    : 1f;
                hasReserveAmmo = w.TotalBullets > 0 ? 1f : 0f;
            }
            sensor.AddObservation(hasRangedWeapon ? 1f : 0f);
            sensor.AddObservation(normAmmoInMag);
            sensor.AddObservation(hasReserveAmmo);
            sensor.AddObservation(isReloading);

            // 4. Target Relative Position, Distance, Bearing & Memory (9 floats)
            Vector3 targetPos = currentTarget != null
                ? currentTarget.position
                : (hasAcousticThreat ? threatLocation : lastKnownTargetPos);

            Vector3 toTargetWorld = targetPos - transform.position;
            float distToTarget = toTargetWorld.magnitude;
            Vector3 localDirToTarget = distToTarget > 0.01f
                ? transform.InverseTransformDirection(toTargetWorld.normalized)
                : Vector3.forward;

            sensor.AddObservation(localDirToTarget.x);
            sensor.AddObservation(localDirToTarget.y);
            sensor.AddObservation(localDirToTarget.z);
            sensor.AddObservation(Mathf.Clamp01(distToTarget / Mathf.Max(1f, DetectionRadius)));
            sensor.AddObservation(hasTargetLineOfSight ? 1f : 0f);
            sensor.AddObservation(Mathf.Clamp01(timeSinceTargetSeen / 15f));

            float targetNormHealth = currentTargetHealth != null && currentTargetHealth.MaxHealth > 0f
                ? Mathf.Clamp01(currentTargetHealth.Health / currentTargetHealth.MaxHealth)
                : 0f;
            sensor.AddObservation(targetNormHealth);

            Vector3 localTargetVel = transform.InverseTransformDirection(predictiveTracker.SmoothedVelocity) / 10f;
            sensor.AddObservation(Mathf.Clamp(localTargetVel.x, -1f, 1f));
            sensor.AddObservation(Mathf.Clamp(localTargetVel.z, -1f, 1f));

            // 5. Safe Zone & Acoustic Threat Awareness (3 floats)
            Vector3 toSafeCenter = SafeZoneCenter - transform.position;
            toSafeCenter.y = 0f;
            float distToCenter = toSafeCenter.magnitude;
            Vector3 localSafeDir = distToCenter > 0.01f
                ? transform.InverseTransformDirection(toSafeCenter.normalized)
                : Vector3.forward;
            sensor.AddObservation(localSafeDir.x);
            sensor.AddObservation(localSafeDir.z);
            sensor.AddObservation(hasAcousticThreat ? 1f : 0f);
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            if (characterBrain == null || (selfHealth != null && selfHealth.IsDead))
            {
                return;
            }

            var cont = actions.ContinuousActions;
            var disc = actions.DiscreteActions;

            cachedMoveX = Mathf.Clamp(cont[0], -1f, 1f);
            cachedMoveZ = Mathf.Clamp(cont[1], -1f, 1f);
            cachedAimYawOffset = Mathf.Clamp(cont[2], -1f, 1f) * 30f;
            cachedAimPitchOffset = Mathf.Clamp(cont[3], -1f, 1f) * 12f;

            cachedFireAction = disc[0] == 1;
            cachedAimAction = disc[1] == 1;
            cachedStanceAction = disc[2];   // 0=Stand, 1=Crouch, 2=Prone
            cachedMobilityAction = disc[3]; // 0=Walk, 1=Sprint, 2=Roll
            cachedReloadAction = disc[4] == 1;
            cachedSwitchAction = disc[5] == 1;
            hasReceivedAction = true;

            ApplyCachedActionsToCharacter();
        }

        private void ApplyCachedActionsToCharacter()
        {
            if (characterBrain == null || (selfHealth != null && selfHealth.IsDead))
            {
                return;
            }

            bool isRunning = cachedMobilityAction == 1;

            // 1. Apply World-Space Movement to JUCharacterBrain
            characterBrain._Move(cachedMoveX, cachedMoveZ, isRunning);

            // 2. Apply Stance & Evasive Roll
            if (cachedStanceAction == 1 && !characterBrain.IsCrouched)
            {
                characterBrain._Crouch();
                if (matchTracker != null) matchTracker.RecordCoverEntered();
            }
            else if (cachedStanceAction == 0 && characterBrain.IsCrouched)
            {
                characterBrain._GetUp();
            }

            if (cachedMobilityAction == 2 && !characterBrain.IsRolling && characterBrain.IsGrounded)
            {
                characterBrain._Roll();
            }

            // 3. Compute Target Aim Position with Predictive Kinematic Lead Aiming
            Vector3 rawTargetPoint;
            if (currentTarget != null)
            {
                if (currentTargetBrain != null && currentTargetBrain.HumanoidSpine != null)
                {
                    rawTargetPoint = currentTargetBrain.HumanoidSpine.position;
                }
                else
                {
                    rawTargetPoint = currentTarget.position + Vector3.up * AimUpOffset;
                }

                // Apply Predictive Lead Aiming using AIPredictiveTracker and evolved AimAccuracy
                float bulletSpeed = 120f;
                if (characterBrain.WeaponInUseRightHand != null && characterBrain.WeaponInUseRightHand.BulletPrefab != null)
                {
                    var b = characterBrain.WeaponInUseRightHand.BulletPrefab.GetComponent<JUTPS.WeaponSystem.Bullet>();
                    if (b != null && b.BulletVelocity > 10f) bulletSpeed = b.BulletVelocity;
                }

                float accuracy = runtimeProfile != null ? runtimeProfile.AimAccuracy : 0.65f;
                bool useAcceleration = runtimeProfile != null && runtimeProfile.HasSkill(AISkill.PredictiveLeadShot);

                Vector3 shooterPos = transform.position + Vector3.up * AimUpOffset;
                rawTargetPoint = predictiveTracker.CalculateLeadAimPoint(shooterPos, bulletSpeed, accuracy, useAcceleration);
            }
            else if (hasAcousticThreat)
            {
                // Aim towards acoustic gunshot source
                rawTargetPoint = threatLocation + Vector3.up * AimUpOffset;
            }
            else
            {
                rawTargetPoint = lastKnownTargetPos + Vector3.up * AimUpOffset;
            }

            Vector3 dirToTarget = rawTargetPoint - transform.position;
            if (dirToTarget.sqrMagnitude < 0.05f)
            {
                dirToTarget = transform.forward * 5f;
            }

            Quaternion aimOffsetRot = Quaternion.Euler(cachedAimPitchOffset, cachedAimYawOffset, 0f);
            Vector3 desiredAimPoint = transform.position + (aimOffsetRot * dirToTarget);
            smoothedAimPosition = Vector3.Lerp(smoothedAimPosition, desiredAimPoint, Mathf.Clamp01(LookTargetSpeed * Time.deltaTime));
            characterBrain.LookAtPosition = smoothedAimPosition;

            // 4. Role-Specific Combat & Orientation Execution
            if (Role == CombatRole.MeleeRusher)
            {
                characterBrain.FiringMode = false;
                characterBrain.FiringModeIK = false;

                if (currentTarget != null)
                {
                    float distToTarget = Vector3.Distance(transform.position, currentTarget.position);
                    if (distToTarget <= 3.2f)
                    {
                        characterBrain.DoLookAt(characterBrain.LookAtPosition, LookTargetSpeed);
                    }
                }

                if (cachedFireAction)
                {
                    characterBrain.DefaultUseOfAllItems(true, true, false, false, false, true);
                    if (matchTracker != null) matchTracker.RecordShotFired();
                }
                else
                {
                    characterBrain.DefaultUseOfAllItems(false, false, false, false, false, false);
                }
            }
            else
            {
                bool shouldUseFireMode = (cachedAimAction || cachedFireAction) && !characterBrain.IsReloading;
                characterBrain.FiringMode = shouldUseFireMode;
                characterBrain.FiringModeIK = shouldUseFireMode;

                if (shouldUseFireMode)
                {
                    characterBrain.DoLookAt(characterBrain.LookAtPosition, LookTargetSpeed);
                }

                if (cachedReloadAction && characterBrain.WeaponInUseRightHand != null &&
                    characterBrain.WeaponInUseRightHand.BulletsAmounts < characterBrain.WeaponInUseRightHand.BulletsPerMagazine &&
                    characterBrain.WeaponInUseRightHand.TotalBullets > 0)
                {
                    characterBrain.DefaultUseOfAllItems(false, false, true, false, false, false);
                }
                else if (cachedFireAction && (hasTargetLineOfSight || suppressingFireTimer > 0f))
                {
                    characterBrain.DefaultUseOfAllItems(true, true, false, false, cachedSwitchAction, true);
                    if (matchTracker != null) matchTracker.RecordShotFired();

                    Vector3 toTargetNorm = (rawTargetPoint - transform.position).normalized;
                    Vector3 aimDirNorm = (smoothedAimPosition - transform.position).normalized;
                    if (Vector3.Dot(toTargetNorm, aimDirNorm) < 0.8f)
                    {
                        ProvideReward(-0.002f * Time.deltaTime);
                    }
                }
                else
                {
                    characterBrain.DefaultUseOfAllItems(false, false, false, false, cachedSwitchAction, false);
                }
            }
        }

        /// <summary>
        /// Embedded Tactical Policy: Combines NavMesh navigation + local raycast steering,
        /// dynamic profile weights (Aggression, CoverPriority, FlankAwareness),
        /// unlocked skills (FlankScouting, SuppressingFire, TacticalRetreat), and acoustic threat investigation.
        /// </summary>
        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var cont = actionsOut.ContinuousActions;
            var disc = actionsOut.DiscreteActions;

            cont[0] = 0f;
            cont[1] = 0f;
            cont[2] = 0f;
            cont[3] = 0f;
            disc[0] = 0;
            disc[1] = 0;
            disc[2] = 0;
            disc[3] = 0;
            disc[4] = 0;
            disc[5] = 0;

            if (characterBrain == null || (selfHealth != null && selfHealth.IsDead))
            {
                return;
            }

            // Dynamic weights from evolved profile
            float aggressionWeight = runtimeProfile != null ? runtimeProfile.Aggression : 0.6f;
            float coverWeight = runtimeProfile != null ? runtimeProfile.CoverPriority : 0.55f;
            float aggressiveness = SentisTacticalEvaluator.EvaluateAggressiveness(this) * (0.5f + aggressionWeight * 0.5f);

            if (currentTarget == null || (currentTargetHealth != null && currentTargetHealth.IsDead))
            {
                AcquireBestTarget();
            }

            // Check if ranged weapon needs reload
            bool needsReload = false;
            if (Role == CombatRole.RangedTactical && characterBrain.WeaponInUseRightHand != null)
            {
                var w = characterBrain.WeaponInUseRightHand;
                needsReload = w.BulletsAmounts <= 0 && w.TotalBullets > 0;
                if (needsReload)
                {
                    disc[4] = 1; // Reload
                }
            }

            if (currentTarget != null)
            {
                Vector3 targetPos = currentTarget.position;
                Vector3 toTarget = targetPos - transform.position;
                float dist = toTarget.magnitude;

                if (Role == CombatRole.MeleeRusher)
                {
                    // Melee Rusher: sprint toward player using NavMesh + obstacle avoidance and strike within 2.4m
                    Vector3 moveDir = ComputeSteeredDirectionTo(targetPos);
                    if (dist <= 1.6f)
                    {
                        cont[0] = moveDir.x * 0.25f;
                        cont[1] = moveDir.z * 0.25f;
                        disc[3] = 0;
                    }
                    else
                    {
                        cont[0] = Mathf.Clamp(moveDir.x, -1f, 1f);
                        cont[1] = Mathf.Clamp(moveDir.z, -1f, 1f);
                        disc[3] = dist > 3.0f ? 1 : 0; // Sprint when closing gap
                    }

                    if (dist <= 2.4f)
                    {
                        disc[0] = 1; // Melee strike
                    }
                }
                else
                {
                    // Ranged Tactical Bot
                    strafePhase += Time.deltaTime * 2.2f;
                    Vector3 flatToTarget = new Vector3(toTarget.x, 0f, toTarget.z);
                    Vector3 dirToTargetFlat = flatToTarget.sqrMagnitude > 0.01f ? flatToTarget.normalized : transform.forward;
                    Vector3 rightPerp = Vector3.Cross(Vector3.up, dirToTargetFlat);
                    float strafeSign = Mathf.Sin(strafePhase) >= 0f ? 1f : -1f;

                    if (dist <= DetectionRadius && (hasTargetLineOfSight || dist <= OptimalCombatRange * 2.5f))
                    {
                        disc[1] = 1; // Aim Mode ON

                        Vector3 desiredWorldMove;
                        bool shouldTakeCover = needsReload || (coverWeight > 0.6f && aggressiveness < 0.42f) || (selfHealth != null && selfHealth.Health < 35f);

                        if (shouldTakeCover)
                        {
                            // Seek cover or retreat while strafing & crouching
                            Vector3 coverDir = FindCoverDirection(targetPos, dirToTargetFlat, rightPerp * strafeSign);
                            desiredWorldMove = SteerAroundObstacles(coverDir);
                            disc[2] = 1; // Crouch in cover / while reloading

                            // TacticalRetreat skill grants sprint velocity when breaking contact
                            bool hasTacticalRetreat = runtimeProfile != null && runtimeProfile.HasSkill(AISkill.TacticalRetreat);
                            disc[3] = hasTacticalRetreat ? 1 : 0;
                        }
                        else if (dist > OptimalCombatRange * 1.2f)
                        {
                            // Advance toward optimal firing range with lateral weave
                            Vector3 pathDir = ComputeSteeredDirectionTo(targetPos);
                            desiredWorldMove = SteerAroundObstacles((pathDir * 0.85f + rightPerp * strafeSign * 0.35f).normalized);
                            disc[3] = dist > OptimalCombatRange * 1.6f ? 1 : 0;
                        }
                        else if (dist < OptimalCombatRange * 0.55f)
                        {
                            // Kiting / backing away from close threat while firing
                            desiredWorldMove = SteerAroundObstacles((-dirToTargetFlat * 0.8f + rightPerp * strafeSign * 0.5f).normalized);
                            disc[3] = 0;
                        }
                        else
                        {
                            // Optimal combat distance: tactical lateral strafing
                            desiredWorldMove = SteerAroundObstacles(rightPerp * strafeSign * 0.85f);
                            disc[2] = aggressiveness < 0.52f ? 1 : 0;
                        }

                        cont[0] = Mathf.Clamp(desiredWorldMove.x, -1f, 1f);
                        cont[1] = Mathf.Clamp(desiredWorldMove.z, -1f, 1f);

                        // Fire bursts when target is in line of sight or close combat
                        if (!needsReload && (hasTargetLineOfSight || dist <= OptimalCombatRange * 1.5f) && dist <= OptimalCombatRange * 2.8f)
                        {
                            bool burstWindow = Mathf.Sin(Time.time * 6.0f + GetInstanceID() * 0.1f) > -0.35f;
                            disc[0] = burstWindow ? 1 : 0;
                        }
                    }
                    else if (hasAcousticThreat)
                    {
                        // Tactical acoustic response: Flank or investigate gunshot origin
                        Vector3 acousticMove;
                        if (runtimeProfile != null && runtimeProfile.HasSkill(AISkill.FlankScouting))
                        {
                            // Approach threat from a perpendicular flank angle
                            Vector3 flankOffset = Vector3.Cross(Vector3.up, threatDirection) * 8f;
                            acousticMove = ComputeSteeredDirectionTo(threatLocation + flankOffset);
                        }
                        else
                        {
                            acousticMove = ComputeSteeredDirectionTo(threatLocation);
                        }

                        cont[0] = Mathf.Clamp(acousticMove.x, -1f, 1f);
                        cont[1] = Mathf.Clamp(acousticMove.z, -1f, 1f);
                        disc[3] = 1; // Sprint to investigate threat

                        // Suppressing fire skill: lay down bursts towards threat location
                        if (suppressingFireTimer > 0f && runtimeProfile != null && runtimeProfile.HasSkill(AISkill.SuppressingFire))
                        {
                            disc[0] = 1;
                            disc[1] = 1;
                        }
                    }
                    else if (PatrolWaypointPath != null && PatrolWaypointPath.WaypointPathPositions != null && PatrolWaypointPath.WaypointPathPositions.Length > 0 && dist > DetectionRadius)
                    {
                        // Follow assigned WaypointPath when player is outside detection radius
                        Vector3 wpDir = GetWaypointPatrolDirection();
                        cont[0] = Mathf.Clamp(wpDir.x, -1f, 1f);
                        cont[1] = Mathf.Clamp(wpDir.z, -1f, 1f);
                        disc[3] = 0;
                    }
                    else
                    {
                        // Pursue player across the map so the bot actively hunts targets
                        Vector3 huntDir = ComputeSteeredDirectionTo(targetPos);
                        cont[0] = Mathf.Clamp(huntDir.x, -1f, 1f);
                        cont[1] = Mathf.Clamp(huntDir.z, -1f, 1f);
                        disc[3] = 1;
                    }
                }
            }
            else if (hasAcousticThreat)
            {
                // Investigate acoustic threat location
                Vector3 toThreat = ComputeSteeredDirectionTo(threatLocation);
                cont[0] = Mathf.Clamp(toThreat.x, -1f, 1f);
                cont[1] = Mathf.Clamp(toThreat.z, -1f, 1f);
                disc[3] = 1;

                if (suppressingFireTimer > 0f && runtimeProfile != null && runtimeProfile.HasSkill(AISkill.SuppressingFire))
                {
                    disc[0] = 1;
                    disc[1] = 1;
                }
            }
            else
            {
                // No target or sound cue: follow WaypointPath or roam
                Vector3 patrolDir = GetWaypointPatrolDirection();
                cont[0] = Mathf.Clamp(patrolDir.x, -1f, 1f);
                cont[1] = Mathf.Clamp(patrolDir.z, -1f, 1f);
                disc[3] = 0;
            }
        }

        private Vector3 FindCoverDirection(Vector3 threatPos, Vector3 dirToThreatFlat, Vector3 fallbackStrafe)
        {
            Collider[] nearby = Physics.OverlapSphere(transform.position, 12f, VisibilityLayerMask, QueryTriggerInteraction.Ignore);
            float bestScore = float.MaxValue;
            Vector3 bestCoverSpot = Vector3.zero;
            bool foundCover = false;

            for (int i = 0; i < nearby.Length; i++)
            {
                Collider col = nearby[i];
                if (col == null || col.transform == transform || col.transform.IsChildOf(transform)) continue;
                if (currentTarget != null && (col.transform == currentTarget || col.transform.IsChildOf(currentTarget))) continue;
                if (col.bounds.size.y < 1.0f || col.bounds.size.x > 40f) continue;

                Vector3 awayFromThreat = (col.bounds.center - threatPos);
                awayFromThreat.y = 0f;
                if (awayFromThreat.sqrMagnitude < 0.1f) continue;
                awayFromThreat.Normalize();

                float radius = Mathf.Max(col.bounds.extents.x, col.bounds.extents.z) + 1.1f;
                Vector3 candidateSpot = col.bounds.center + awayFromThreat * radius;
                candidateSpot.y = transform.position.y;

                float distToSpot = Vector3.Distance(transform.position, candidateSpot);
                if (distToSpot < bestScore && distToSpot > 0.5f)
                {
                    bestScore = distToSpot;
                    bestCoverSpot = candidateSpot;
                    foundCover = true;
                }
            }

            if (foundCover)
            {
                Vector3 toCover = bestCoverSpot - transform.position;
                toCover.y = 0f;
                if (toCover.sqrMagnitude > 0.05f)
                {
                    return toCover.normalized;
                }
            }

            return (-dirToThreatFlat * 0.65f + fallbackStrafe * 0.75f).normalized;
        }

        private Vector3 GetWaypointPatrolDirection()
        {
            if (PatrolWaypointPath != null &&
                PatrolWaypointPath.WaypointPathPositions != null &&
                PatrolWaypointPath.WaypointPathPositions.Length > 0)
            {
                Vector3[] wps = PatrolWaypointPath.WaypointPathPositions;
                if (currentWaypointIndex >= wps.Length || currentWaypointIndex < 0)
                {
                    currentWaypointIndex = 0;
                }

                Vector3 targetWp = wps[currentWaypointIndex];
                float distToWp = Vector2.Distance(
                    new Vector2(transform.position.x, transform.position.z),
                    new Vector2(targetWp.x, targetWp.z));

                if (distToWp < 2.0f)
                {
                    currentWaypointIndex = (currentWaypointIndex + 1) % wps.Length;
                    targetWp = wps[currentWaypointIndex];
                }

                return ComputeSteeredDirectionTo(targetWp);
            }

            // Fallback roaming if no WaypointPath is assigned
            roamRefreshTimer -= Time.deltaTime;
            float distToRoam = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.z),
                new Vector2(roamDestination.x, roamDestination.z));
            if (roamRefreshTimer <= 0f || distToRoam < 2.0f)
            {
                roamRefreshTimer = Random.Range(4f, 8f);
                Vector2 randCircle = Random.insideUnitCircle * 20f;
                roamDestination = spawnPosition + new Vector3(randCircle.x, 0f, randCircle.y);
            }

            return ComputeSteeredDirectionTo(roamDestination);
        }

        private Vector3 ComputeSteeredDirectionTo(Vector3 destination)
        {
            navPathRefreshTimer -= Time.deltaTime;
            if (navPathRefreshTimer <= 0f || cachedNavPath == null || cachedNavPath.Length == 0)
            {
                navPathRefreshTimer = 0.5f;
                Vector3 startPos = transform.position;
                if (NavMesh.SamplePosition(startPos, out NavMeshHit startHit, 6f, NavMesh.AllAreas) &&
                    NavMesh.SamplePosition(destination, out NavMeshHit destHit, 10f, NavMesh.AllAreas))
                {
                    NavMeshPath path = new NavMeshPath();
                    if (NavMesh.CalculatePath(startHit.position, destHit.position, NavMesh.AllAreas, path) &&
                        path.corners != null && path.corners.Length > 0)
                    {
                        cachedNavPath = path.corners;
                        currentNavCornerIndex = cachedNavPath.Length > 1 ? 1 : 0;
                    }
                    else
                    {
                        cachedNavPath = new Vector3[0];
                    }
                }
                else
                {
                    cachedNavPath = new Vector3[0];
                }
            }

            Vector3 desiredDir = Vector3.zero;
            if (cachedNavPath != null && cachedNavPath.Length > 0)
            {
                if (currentNavCornerIndex >= cachedNavPath.Length)
                {
                    currentNavCornerIndex = cachedNavPath.Length - 1;
                }

                Vector3 corner = cachedNavPath[currentNavCornerIndex];
                float flatDist = Vector2.Distance(
                    new Vector2(transform.position.x, transform.position.z),
                    new Vector2(corner.x, corner.z));

                if (flatDist < 1.6f && currentNavCornerIndex < cachedNavPath.Length - 1)
                {
                    currentNavCornerIndex++;
                    corner = cachedNavPath[currentNavCornerIndex];
                }

                Vector3 dir = corner - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                {
                    desiredDir = dir.normalized;
                }
            }

            if (desiredDir.sqrMagnitude < 0.01f)
            {
                Vector3 direct = destination - transform.position;
                direct.y = 0f;
                desiredDir = direct.sqrMagnitude > 0.01f ? direct.normalized : transform.forward;
            }

            return SteerAroundObstacles(desiredDir);
        }

        private Vector3 SteerAroundObstacles(Vector3 desiredWorldDir)
        {
            desiredWorldDir.y = 0f;
            if (desiredWorldDir.sqrMagnitude < 0.001f) return Vector3.zero;
            desiredWorldDir.Normalize();

            Vector3 origin = transform.position + Vector3.up * 0.9f;
            if (!IsDirectionBlocked(origin, desiredWorldDir, 2.2f))
            {
                return desiredWorldDir;
            }

            // Fan out left/right angles to steer smoothly around walls, trees, or vehicles
            float[] testAngles = new float[] { 30f, -30f, 60f, -60f, 95f, -95f, 135f, -135f };
            for (int i = 0; i < testAngles.Length; i++)
            {
                Vector3 candidate = Quaternion.Euler(0f, testAngles[i], 0f) * desiredWorldDir;
                if (!IsDirectionBlocked(origin, candidate, 2.0f))
                {
                    return candidate.normalized;
                }
            }

            return -desiredWorldDir;
        }

        private bool IsDirectionBlocked(Vector3 origin, Vector3 dir, float checkDist)
        {
            RaycastHit[] hits = Physics.RaycastAll(origin, dir, checkDist, VisibilityLayerMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                Transform ht = hits[i].transform;
                if (ht == null || ht == transform || ht.IsChildOf(transform)) continue;
                if (currentTarget != null && (ht == currentTarget || ht.IsChildOf(currentTarget))) continue;
                return true;
            }
            return false;
        }

        private void AcquireBestTarget()
        {
            float bestDist = float.MaxValue;
            Transform bestTransform = null;
            JUHealth bestHealth = null;
            JUCharacterBrain bestBrain = null;

            foreach (string tag in TargetTags)
            {
                if (string.IsNullOrEmpty(tag)) continue;
                GameObject[] candidates = GameObject.FindGameObjectsWithTag(tag);
                for (int i = 0; i < candidates.Length; i++)
                {
                    GameObject candidate = candidates[i];
                    if (candidate == null || candidate == gameObject) continue;

                    JUHealth h = candidate.GetComponent<JUHealth>();
                    if (h != null && h.IsDead) continue;

                    float d = Vector3.Distance(transform.position, candidate.transform.position);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        bestTransform = candidate.transform;
                        bestHealth = h;
                        bestBrain = candidate.GetComponent<JUCharacterBrain>();
                    }
                }
            }

            if (bestTransform != currentTarget)
            {
                currentTarget = bestTransform;
                currentTargetHealth = bestHealth;
                currentTargetBrain = bestBrain;
                prevTargetHealth = currentTargetHealth != null ? currentTargetHealth.Health : 100f;
            }
        }

        private void UpdateLineOfSightMemory()
        {
            if (currentTarget == null)
            {
                hasTargetLineOfSight = false;
                timeSinceTargetSeen += 0.2f;
                return;
            }

            Vector3 eyePos = transform.position + Vector3.up * (AimUpOffset + 0.35f);
            Vector3 targetChest = (currentTargetBrain != null && currentTargetBrain.HumanoidSpine != null)
                ? currentTargetBrain.HumanoidSpine.position
                : currentTarget.position + Vector3.up * AimUpOffset;

            Vector3 toTarget = targetChest - eyePos;
            float dist = toTarget.magnitude;

            if (dist > DetectionRadius)
            {
                hasTargetLineOfSight = false;
                timeSinceTargetSeen += 0.2f;
                return;
            }

            if (dist < 2.5f)
            {
                hasTargetLineOfSight = true;
                timeSinceTargetSeen = 0f;
                lastKnownTargetPos = currentTarget.position;
                Rigidbody targetRb = currentTarget.GetComponent<Rigidbody>();
                lastKnownTargetVel = targetRb != null ? targetRb.velocity : Vector3.zero;
                return;
            }

            // RaycastAll and ignore our own colliders (capsule, ragdoll bones, equipped weapons)
            RaycastHit[] hits = Physics.RaycastAll(eyePos, toTarget.normalized, dist, VisibilityLayerMask, QueryTriggerInteraction.Ignore);
            float closestHitDist = float.MaxValue;
            Transform closestHitTransform = null;

            for (int i = 0; i < hits.Length; i++)
            {
                Transform ht = hits[i].transform;
                if (ht == null || ht == transform || ht.IsChildOf(transform)) continue;

                if (hits[i].distance < closestHitDist)
                {
                    closestHitDist = hits[i].distance;
                    closestHitTransform = ht;
                }
            }

            if (closestHitTransform == null ||
                closestHitTransform == currentTarget ||
                closestHitTransform.IsChildOf(currentTarget) ||
                closestHitDist >= dist - 0.6f)
            {
                hasTargetLineOfSight = true;
                timeSinceTargetSeen = 0f;
                lastKnownTargetPos = currentTarget.position;
                Rigidbody targetRb = currentTarget.GetComponent<Rigidbody>();
                lastKnownTargetVel = targetRb != null ? targetRb.velocity : Vector3.zero;
                return;
            }

            hasTargetLineOfSight = false;
            timeSinceTargetSeen += 0.2f;
        }

        private void EvaluateStepRewards()
        {
            // 1. Self Damage / Death Penalty
            if (selfHealth != null)
            {
                float currentHp = selfHealth.Health;
                if (currentHp < prevSelfHealth)
                {
                    float dmgTaken = prevSelfHealth - currentHp;
                    LastDamageTaken += dmgTaken;
                    ProvideReward(-0.03f * dmgTaken);
                }
                prevSelfHealth = currentHp;

                if (selfHealth.IsDead)
                {
                    ProvideReward(-3.0f);
                    CompletedEpisodesCount++;
                    TriggerEvolutionOnDeath();
                    EndEpisode();
                    return;
                }
            }

            // 2. Enemy Damage Dealt / Kill Reward
            if (currentTargetHealth != null)
            {
                float targetHp = currentTargetHealth.Health;
                if (targetHp < prevTargetHealth)
                {
                    float dmgDealt = prevTargetHealth - targetHp;
                    LastDamageDealt += dmgDealt;
                    ProvideReward(0.05f * dmgDealt);
                    if (matchTracker != null) matchTracker.RecordShotHit(dmgDealt);
                }
                prevTargetHealth = targetHp;

                if (currentTargetHealth.IsDead)
                {
                    ProvideReward(2.0f);
                    if (matchTracker != null) matchTracker.RecordKill();
                    AcquireBestTarget();
                }
            }

            // 3. Tactical Spacing & Engagement Shaping
            if (currentTarget != null && hasTargetLineOfSight)
            {
                float dist = Vector3.Distance(transform.position, currentTarget.position);
                if (Role == CombatRole.RangedTactical && dist >= 6f && dist <= OptimalCombatRange * 1.4f)
                {
                    ProvideReward(0.002f * Time.deltaTime);
                }
                else if (Role == CombatRole.MeleeRusher && dist <= 3.5f)
                {
                    ProvideReward(0.003f * Time.deltaTime);
                }
            }

            // 4. Safe Zone Boundary Check
            float distFromCenter = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.z),
                new Vector2(SafeZoneCenter.x, SafeZoneCenter.z));
            if (distFromCenter > SafeZoneRadius)
            {
                ProvideReward(-0.05f * Time.deltaTime);
            }
            else
            {
                ProvideReward(0.0005f * Time.deltaTime);
            }
        }

        public void ProvideReward(float delta)
        {
            AddReward(delta);
            CurrentEpisodeReward += delta;
        }

        public void ResetAgentToSpawn()
        {
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            if (selfRb != null)
            {
                selfRb.velocity = Vector3.zero;
                selfRb.angularVelocity = Vector3.zero;
            }
            if (selfHealth != null)
            {
                selfHealth.Health = selfHealth.MaxHealth;
                selfHealth.IsDead = false;
            }
            if (characterBrain != null)
            {
                characterBrain.IsDead = false;
            }
        }
    }
}
