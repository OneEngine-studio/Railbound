using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Policies;
using JUTPS;
using JUTPS.CharacterBrain;
using JUTPS.AI;

namespace Project.AI.RL
{
    /// <summary>
    /// Reinforcement Learning Agent for JU TPS Characters using Unity ML-Agents (PPO / MA-POCA).
    /// Replaces static FSM logic (PatrolAI / ZombieAI) with continuous + discrete policy control,
    /// 3D RayPerception sensors, recurrent target memory, and dense combat reward shaping.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(JUCharacterBrain))]
    [RequireComponent(typeof(BehaviorParameters))]
    [RequireComponent(typeof(DecisionRequester))]
    [AddComponentMenu("Project/AI/RL/JU Character RL Agent")]
    public class JUCharacterRLAgent : Agent
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

        [Header("Combat & Sensor Parameters")]
        public float DetectionRadius = 65f;
        public float OptimalCombatRange = 14f;
        public float AimUpOffset = 1.05f;
        public float SafeZoneRadius = 250f;
        public Vector3 SafeZoneCenter = Vector3.zero;

        [Header("Recurrent Memory State (POMDP)")]
        [SerializeField] private Transform currentTarget;
        [SerializeField] private JUHealth currentTargetHealth;
        [SerializeField] private Vector3 lastKnownTargetPos;
        [SerializeField] private Vector3 lastKnownTargetVel;
        [SerializeField] private float timeSinceTargetSeen = 99f;
        [SerializeField] private bool hasTargetLineOfSight;

        [Header("Live Telemetry (Read-Only)")]
        public float CurrentEpisodeReward;
        public int CompletedEpisodesCount;
        public float LastDamageDealt;
        public float LastDamageTaken;

        private JUCharacterBrain characterBrain;
        private JUHealth selfHealth;
        private Rigidbody selfRb;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;

        private float prevSelfHealth;
        private float prevTargetHealth;
        private float strafePhase;
        private float targetScanTimer;
        private Vector3 smoothedAimPosition;
        private Vector3 currentWaypointTarget;
        private Vector3[] cachedNavPath = new Vector3[0];
        private int currentNavCornerIndex;
        private float navPathRefreshTimer;

        public JUCharacterBrain CharacterBrain => characterBrain;
        public JUHealth SelfHealth => selfHealth;
        public Transform CurrentTarget => currentTarget;
        public bool HasLineOfSight => hasTargetLineOfSight;

        public override void Initialize()
        {
            characterBrain = GetComponent<JUCharacterBrain>();
            selfHealth = GetComponent<JUHealth>();
            selfRb = GetComponent<Rigidbody>();

            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
            smoothedAimPosition = transform.position + transform.forward * 10f + Vector3.up * AimUpOffset;
            strafePhase = Random.Range(0f, Mathf.PI * 2f);

            if (characterBrain != null)
            {
                characterBrain.IsArtificialIntelligence = true;
            }

            // Disable legacy scripted AI brains so they do not override RL actions
            var legacyPatrol = GetComponent<PatrolAI>();
            if (legacyPatrol != null && legacyPatrol.enabled)
            {
                legacyPatrol.enabled = false;
            }

            var legacyZombie = GetComponent<ZombieAI>();
            if (legacyZombie != null && legacyZombie.enabled)
            {
                legacyZombie.enabled = false;
            }

            AcquireBestTarget();
            CacheHealthBaselines();
        }

        public override void OnEpisodeBegin()
        {
            CurrentEpisodeReward = 0f;
            LastDamageDealt = 0f;
            LastDamageTaken = 0f;
            timeSinceTargetSeen = 99f;
            strafePhase = Random.Range(0f, Mathf.PI * 2f);

            AcquireBestTarget();
            CacheHealthBaselines();

            if (currentTarget != null)
            {
                lastKnownTargetPos = currentTarget.position;
            }
            else
            {
                lastKnownTargetPos = transform.position + transform.forward * 10f;
            }
        }

        private void CacheHealthBaselines()
        {
            prevSelfHealth = selfHealth != null ? selfHealth.Health : 100f;
            prevTargetHealth = currentTargetHealth != null ? currentTargetHealth.Health : 100f;
        }

        private void Update()
        {
            if (characterBrain == null || (selfHealth != null && selfHealth.IsDead))
            {
                return;
            }

            targetScanTimer -= Time.deltaTime;
            if (targetScanTimer <= 0f)
            {
                targetScanTimer = 0.25f;
                AcquireBestTarget();
                UpdateLineOfSightMemory();
            }
            else if (currentTarget != null && hasTargetLineOfSight)
            {
                lastKnownTargetPos = currentTarget.position;
            }

            EvaluateStepRewards();
        }

        /// <summary>
        /// Collects 24 normalized vector observations representing self status, weapon state,
        /// target relative kinematics, line-of-sight memory, and safe-zone awareness.
        /// Combined with RayPerceptionSensorComponent3D for 3D obstacle/enemy vision.
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
            Vector3 targetPos = currentTarget != null && hasTargetLineOfSight
                ? currentTarget.position
                : lastKnownTargetPos;
            Vector3 toTargetWorld = targetPos - transform.position;
            float distToTarget = toTargetWorld.magnitude;
            Vector3 localDirToTarget = distToTarget > 0.01f
                ? transform.InverseTransformDirection(toTargetWorld.normalized)
                : Vector3.forward;

            sensor.AddObservation(localDirToTarget.x);
            sensor.AddObservation(localDirToTarget.y);
            sensor.AddObservation(localDirToTarget.z);
            sensor.AddObservation(Mathf.Clamp01(distToTarget / DetectionRadius));
            sensor.AddObservation(hasTargetLineOfSight ? 1f : 0f);
            sensor.AddObservation(Mathf.Clamp01(timeSinceTargetSeen / 15f));

            float targetNormHealth = currentTargetHealth != null && currentTargetHealth.MaxHealth > 0f
                ? Mathf.Clamp01(currentTargetHealth.Health / currentTargetHealth.MaxHealth)
                : 0f;
            sensor.AddObservation(targetNormHealth);

            Vector3 localTargetVel = transform.InverseTransformDirection(lastKnownTargetVel) / 10f;
            sensor.AddObservation(Mathf.Clamp(localTargetVel.x, -1f, 1f));
            sensor.AddObservation(Mathf.Clamp(localTargetVel.z, -1f, 1f));

            // 5. Safe Zone / Tactical Arena Awareness (3 floats)
            Vector3 toSafeCenter = SafeZoneCenter - transform.position;
            toSafeCenter.y = 0f;
            float distToCenter = toSafeCenter.magnitude;
            Vector3 localSafeDir = distToCenter > 0.01f
                ? transform.InverseTransformDirection(toSafeCenter.normalized)
                : Vector3.forward;
            sensor.AddObservation(localSafeDir.x);
            sensor.AddObservation(localSafeDir.z);
            sensor.AddObservation(Mathf.Clamp01(distToCenter / Mathf.Max(1f, SafeZoneRadius)));
        }

        /// <summary>
        /// Executes continuous locomotion/aiming actions and discrete combat actions on JUCharacterBrain.
        /// Continuous[4]: [0]=MoveX, [1]=MoveZ, [2]=AimYawOffset, [3]=AimPitchOffset
        /// Discrete[6]: [0]=Fire(2), [1]=AimMode(2), [2]=Stance(3), [3]=Mobility(3), [4]=Reload(2), [5]=SwitchWeapon(2)
        /// </summary>
        public override void OnActionReceived(ActionBuffers actions)
        {
            if (characterBrain == null || (selfHealth != null && selfHealth.IsDead))
            {
                return;
            }

            var cont = actions.ContinuousActions;
            var disc = actions.DiscreteActions;

            float moveX = Mathf.Clamp(cont[0], -1f, 1f);
            float moveZ = Mathf.Clamp(cont[1], -1f, 1f);
            float aimYawOffset = Mathf.Clamp(cont[2], -1f, 1f) * 35f;
            float aimPitchOffset = Mathf.Clamp(cont[3], -1f, 1f) * 15f;

            bool fireAction = disc[0] == 1;
            bool aimAction = disc[1] == 1;
            int stanceAction = disc[2];   // 0=Stand, 1=Crouch, 2=Prone
            int mobilityAction = disc[3]; // 0=Walk, 1=Sprint, 2=Roll
            bool reloadAction = disc[4] == 1;
            bool switchAction = disc[5] == 1;

            bool isSprinting = mobilityAction == 1 && !aimAction;

            // 1. Apply Movement
            characterBrain._Move(moveX, moveZ, isSprinting);

            // 2. Apply Stance & Evasive Roll
            if (stanceAction == 1 && !characterBrain.IsCrouched)
            {
                characterBrain._Crouch();
            }
            else if (stanceAction == 0 && characterBrain.IsCrouched)
            {
                characterBrain._GetUp();
            }

            if (mobilityAction == 2 && !characterBrain.IsRolling && characterBrain.IsGrounded)
            {
                characterBrain._Roll();
            }

            // 3. Apply Aiming & LookAt Position
            Vector3 baseTargetPos = (currentTarget != null && hasTargetLineOfSight)
                ? currentTarget.position + Vector3.up * AimUpOffset
                : lastKnownTargetPos + Vector3.up * AimUpOffset;

            Vector3 dirToTarget = baseTargetPos - transform.position;
            if (dirToTarget.sqrMagnitude < 0.25f)
            {
                dirToTarget = transform.forward * 10f;
            }

            Quaternion aimOffsetRot = Quaternion.Euler(aimPitchOffset, aimYawOffset, 0f);
            Vector3 desiredAimPoint = transform.position + (aimOffsetRot * dirToTarget);
            smoothedAimPosition = Vector3.Lerp(smoothedAimPosition, desiredAimPoint, Time.deltaTime * 10f);
            characterBrain.LookAtPosition = smoothedAimPosition;

            bool shouldUseFireMode = (Role == CombatRole.RangedTactical) && (aimAction || fireAction);
            characterBrain.FiringMode = shouldUseFireMode;
            characterBrain.FiringModeIK = shouldUseFireMode;

            // 4. Apply Combat / Weapon Actions
            if (Role == CombatRole.MeleeRusher)
            {
                if (fireAction)
                {
                    characterBrain.DefaultUseOfAllItems(false, false, false, true, false, false);
                }
            }
            else
            {
                if (reloadAction && characterBrain.WeaponInUseRightHand != null &&
                    characterBrain.WeaponInUseRightHand.BulletsAmounts < characterBrain.WeaponInUseRightHand.BulletsPerMagazine)
                {
                    characterBrain.DefaultUseOfAllItems(false, false, true, false, false, false);
                }
                else if (fireAction && hasTargetLineOfSight)
                {
                    characterBrain.DefaultUseOfAllItems(true, true, false, false, switchAction, true);

                    // Small penalty if firing when not aligned with target (discourages blind spraying)
                    Vector3 toTargetNorm = (baseTargetPos - transform.position).normalized;
                    Vector3 aimDirNorm = (smoothedAimPosition - transform.position).normalized;
                    if (Vector3.Dot(toTargetNorm, aimDirNorm) < 0.85f)
                    {
                        ProvideReward(-0.005f);
                    }
                }
                else
                {
                    characterBrain.DefaultUseOfAllItems(false, false, false, false, switchAction, true);
                }
            }
        }

        /// <summary>
        /// Embedded Tactical RL Policy (used when BehaviorType is HeuristicOnly or Default without an external Python trainer).
        /// Combines NavMesh pathfinding, POMDP target memory, tactical strafing, cover reloading, and Sentis threat evaluation.
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

            // Evaluate Sentis tactical utility score (0 = defensive/cover, 1 = aggressive push)
            float aggressiveness = SentisTacticalEvaluator.EvaluateAggressiveness(this);

            if (currentTarget == null || (currentTargetHealth != null && currentTargetHealth.IsDead))
            {
                AcquireBestTarget();
            }

            Vector3 targetPos = currentTarget != null
                ? currentTarget.position
                : lastKnownTargetPos;
            Vector3 toTarget = targetPos - transform.position;
            float dist = toTarget.magnitude;

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

            // Compute navigation vector toward target or tactical range
            navPathRefreshTimer -= Time.deltaTime;
            if (navPathRefreshTimer <= 0f)
            {
                navPathRefreshTimer = 0.6f;
                Vector3 navGoal = JUPathFinder.GetClosestWalkablePoint(targetPos);
                cachedNavPath = JUPathFinder.CalculatePath(transform.position, navGoal);
                currentNavCornerIndex = 0;
            }

            Vector3 desiredWorldMove = Vector3.zero;
            if (Role == CombatRole.MeleeRusher)
            {
                // Melee Rushers sprint toward the target and attack within 2.3m
                desiredWorldMove = GetNextPathDirection(targetPos);
                cont[0] = Mathf.Clamp(desiredWorldMove.x, -1f, 1f);
                cont[1] = Mathf.Clamp(desiredWorldMove.z, -1f, 1f);
                disc[3] = dist > 3.0f ? 1 : 0; // Sprint when closing gap
                if (dist <= 2.3f && hasTargetLineOfSight)
                {
                    disc[0] = 1; // Melee strike
                }
            }
            else
            {
                // Ranged Tactical Bot: maintain optimal distance, strafe in combat, crouch/evade when low HP
                strafePhase += Time.deltaTime * 2.2f;
                Vector3 rightPerp = Vector3.Cross(Vector3.up, toTarget.normalized);
                float strafeSign = Mathf.Sin(strafePhase);

                if (hasTargetLineOfSight && dist <= DetectionRadius)
                {
                    disc[1] = 1; // Aim Mode ON

                    if (needsReload || aggressiveness < 0.35f)
                    {
                        // Back up while strafing and crouch to reduce hit profile
                        desiredWorldMove = (-toTarget.normalized * 0.6f + rightPerp * strafeSign * 0.8f).normalized;
                        disc[2] = 1; // Crouch
                    }
                    else if (dist > OptimalCombatRange * 1.25f)
                    {
                        // Advance with tactical weave
                        Vector3 pathDir = GetNextPathDirection(targetPos);
                        desiredWorldMove = (pathDir * 0.8f + rightPerp * strafeSign * 0.35f).normalized;
                        disc[3] = dist > OptimalCombatRange * 1.8f ? 1 : 0;
                    }
                    else if (dist < OptimalCombatRange * 0.55f)
                    {
                        // Retreat to keep spacing
                        desiredWorldMove = (-toTarget.normalized * 0.75f + rightPerp * strafeSign * 0.5f).normalized;
                    }
                    else
                    {
                        // Optimal firing band: lateral strafe
                        desiredWorldMove = rightPerp * strafeSign * 0.75f;
                    }

                    cont[0] = Mathf.Clamp(desiredWorldMove.x, -1f, 1f);
                    cont[1] = Mathf.Clamp(desiredWorldMove.z, -1f, 1f);

                    // Controlled burst firing when aimed on target
                    if (!needsReload && dist <= OptimalCombatRange * 2.2f)
                    {
                        bool burstWindow = Mathf.Sin(Time.time * 5.5f + GetInstanceID()) > -0.2f;
                        disc[0] = burstWindow ? 1 : 0;
                    }
                }
                else
                {
                    // Target out of sight: pursue last known position via NavMesh
                    if (timeSinceTargetSeen < 18f)
                    {
                        desiredWorldMove = GetNextPathDirection(lastKnownTargetPos);
                        cont[0] = Mathf.Clamp(desiredWorldMove.x, -1f, 1f);
                        cont[1] = Mathf.Clamp(desiredWorldMove.z, -1f, 1f);
                        disc[3] = 1; // Sprint to flank
                    }
                }
            }
        }

        private Vector3 GetNextPathDirection(Vector3 fallbackTarget)
        {
            if (cachedNavPath != null && cachedNavPath.Length > 0)
            {
                if (currentNavCornerIndex >= cachedNavPath.Length)
                {
                    currentNavCornerIndex = cachedNavPath.Length - 1;
                }
                Vector3 corner = cachedNavPath[currentNavCornerIndex];
                if (Vector3.Distance(transform.position, corner) < 1.5f && currentNavCornerIndex < cachedNavPath.Length - 1)
                {
                    currentNavCornerIndex++;
                    corner = cachedNavPath[currentNavCornerIndex];
                }
                Vector3 dir = corner - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                {
                    return dir.normalized;
                }
            }

            Vector3 direct = fallbackTarget - transform.position;
            direct.y = 0f;
            return direct.sqrMagnitude > 0.01f ? direct.normalized : Vector3.zero;
        }

        private void AcquireBestTarget()
        {
            float bestDist = float.MaxValue;
            Transform bestTransform = null;
            JUHealth bestHealth = null;

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
                    }
                }
            }

            if (bestTransform != currentTarget)
            {
                currentTarget = bestTransform;
                currentTargetHealth = bestHealth;
                prevTargetHealth = currentTargetHealth != null ? currentTargetHealth.Health : 100f;
            }
        }

        private void UpdateLineOfSightMemory()
        {
            if (currentTarget == null)
            {
                hasTargetLineOfSight = false;
                timeSinceTargetSeen += 0.25f;
                return;
            }

            Vector3 eyePos = transform.position + Vector3.up * (AimUpOffset + 0.35f);
            Vector3 targetChest = currentTarget.position + Vector3.up * AimUpOffset;
            Vector3 toTarget = targetChest - eyePos;
            float dist = toTarget.magnitude;

            if (dist > DetectionRadius)
            {
                hasTargetLineOfSight = false;
                timeSinceTargetSeen += 0.25f;
                return;
            }

            if (Physics.Raycast(eyePos, toTarget.normalized, out RaycastHit hit, dist, VisibilityLayerMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform == currentTarget || hit.transform.IsChildOf(currentTarget))
                {
                    hasTargetLineOfSight = true;
                    timeSinceTargetSeen = 0f;
                    lastKnownTargetPos = currentTarget.position;
                    Rigidbody targetRb = currentTarget.GetComponent<Rigidbody>();
                    lastKnownTargetVel = targetRb != null ? targetRb.velocity : Vector3.zero;
                    return;
                }
            }
            else
            {
                hasTargetLineOfSight = true;
                timeSinceTargetSeen = 0f;
                lastKnownTargetPos = currentTarget.position;
                return;
            }

            hasTargetLineOfSight = false;
            timeSinceTargetSeen += 0.25f;
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
                }
                prevTargetHealth = targetHp;

                if (currentTargetHealth.IsDead)
                {
                    ProvideReward(2.0f);
                    AcquireBestTarget();
                }
            }

            // 3. Tactical Spacing & Survival Shaping
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
