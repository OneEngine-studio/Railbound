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
using Project.AI.Exploration;

namespace Project.AI.RL
{

    public partial class JUCharacterRLAgent
    {
        #region Exploration & Looting

        private void ResetExplorationState()
        {
            if (explorationMap == null)
            {
                explorationMap = new AIExplorationMap(ExplorationCellSize, ExplorationMapRadius, SafeZoneCenter);
            }
            explorationMap.Clear();

            if (lootMemory == null) lootMemory = new AILootMemory();
            lootMemory.Clear();

            trackedLootValid = false;
            hasFrontier = false;
            exploreSampleTimer = 0f;
            lootScanTimer = 0f;
            frontierTimer = 0f;
            trackedPickupAttempts = 0;
            WeaponsCollected = 0;
            ExplorationCoverage = 0f;
            VisitedCellCount = 0;
        }

        /// <summary>True when the bot holds, or has unlocked, at least one ranged weapon.</summary>
        private bool HasAnyRangedWeapon()
        {
            if (characterBrain == null) return false;
            if (characterBrain.WeaponInUseRightHand != null) return true;

            var inv = characterBrain.Inventory;
            if (inv == null || inv.HoldableItensRightHand == null) return false;

            for (int i = 0; i < inv.HoldableItensRightHand.Length; i++)
            {
                var item = inv.HoldableItensRightHand[i];
                if (item != null && item.Unlocked && item is JUTPS.WeaponSystem.Weapon) return true;
            }
            return false;
        }

        private bool IsLowOnAmmo()
        {
            var w = characterBrain != null ? characterBrain.WeaponInUseRightHand : null;
            if (w == null) return false;
            return w.TotalBullets + w.BulletsAmounts < Mathf.Max(1, w.BulletsPerMagazine) * 2;
        }

        /// <summary>Locks every starting weapon so the bot has to find one on the map.</summary>
        /// <summary>Returns true once the inventory was ready and the strip was applied (or is not needed).</summary>
        private bool StripStartingWeapons()
        {
            if (Role != CombatRole.RangedTactical) return true;
            if (characterBrain == null) return false;

            var inv = characterBrain.Inventory;
            if (inv == null || inv.HoldableItensRightHand == null || inv.HoldableItensRightHand.Length == 0) return false;

            for (int i = 0; i < inv.HoldableItensRightHand.Length; i++)
            {
                var item = inv.HoldableItensRightHand[i];
                if (item != null && item is JUTPS.WeaponSystem.Weapon)
                {
                    item.Unlocked = false;
                }
            }
            // JUInventory.Update unequips any held item whose Unlocked flag is false.
            inv.RefreshInBodyItemVisibility();
            return true;
        }

        private void UpdateExplorationAndLoot()
        {
            if (explorationMap == null || lootMemory == null) ResetExplorationState();

            if (pendingWeaponStrip && StripStartingWeapons())
            {
                pendingWeaponStrip = false;
            }

            float dt = Time.deltaTime;
            bool armed = HasAnyRangedWeapon();

            // 1. Exploration: reward each newly visited cell
            exploreSampleTimer -= dt;
            if (exploreSampleTimer <= 0f)
            {
                exploreSampleTimer = 0.25f;
                if (explorationMap.MarkVisited(transform.position))
                {
                    ProvideReward(NewCellReward);
                }
                ExplorationCoverage = explorationMap.Coverage;
                VisitedCellCount = explorationMap.VisitedCount;
            }

            // 2. Being unarmed is a cost, which pushes the bot to find a weapon
            if (Role == CombatRole.RangedTactical && !armed)
            {
                ProvideReward(-UnarmedPenaltyPerSecond * dt);
            }

            // 3. Loot scan: pick what the bot currently needs
            lootScanTimer -= dt;
            if (lootScanTimer <= 0f)
            {
                lootScanTimer = 0.5f;
                lootMemory.Sense(transform.position, LootSenseRadius);

                if (Role == CombatRole.RangedTactical &&
                    lootMemory.TryGetBest(transform.position, !armed, armed && IsLowOnAmmo(), out LootEntry best, out float bestDistance))
                {
                    if (!trackedLootValid || trackedLoot.Source != best.Source)
                    {
                        trackedLoot = best;
                        trackedLootValid = true;
                        trackedWasUnarmed = !armed;
                        trackedPickupAttempts = 0;
                        trackedBestDistance = bestDistance;
                        trackedNoProgressTimer = 0f;
                    }
                    trackedLastDistance = bestDistance;
                }
                else if (trackedLootValid && !trackedLoot.IsAvailable())
                {
                    // Handled below as "collected"
                }
                else if (trackedLootValid)
                {
                    trackedLootValid = false;
                }
            }

            // 4. Track the chosen loot: reward on collection, pick up weapons when close
            pickupCooldown -= dt;
            if (trackedLootValid)
            {
                if (!trackedLoot.IsAvailable())
                {
                    if (trackedLastDistance <= 4f)
                    {
                        if (trackedLoot.Kind == LootKind.Weapon)
                        {
                            WeaponsCollected++;
                            ProvideReward(trackedWasUnarmed ? FirstWeaponReward : ExtraWeaponReward);
                        }
                        else
                        {
                            ProvideReward(AmmoPickupReward);
                        }
                    }
                    trackedLootValid = false;
                    lootScanTimer = 0f;
                }
                else
                {
                    trackedLastDistance = FlatDistance(transform.position, trackedLoot.Position);

                    // Watchdog: if the bot cannot get closer to this loot, stop chasing it so it explores instead of standing still.
                    if (trackedLastDistance < trackedBestDistance - 1f)
                    {
                        trackedBestDistance = trackedLastDistance;
                        trackedNoProgressTimer = 0f;
                    }
                    else if (trackedLastDistance > LootPickupRadius)
                    {
                        trackedNoProgressTimer += dt;
                        if (trackedNoProgressTimer >= LootNoProgressTimeout)
                        {
                            lootMemory.Ignore(trackedLoot.Source);
                            trackedLootValid = false;
                            trackedNoProgressTimer = 0f;
                            lootScanTimer = 0f;
                            return;
                        }
                    }

                    if (trackedLoot.Kind == LootKind.Weapon && trackedLastDistance <= LootPickupRadius && pickupCooldown <= 0f)
                    {
                        pickupCooldown = 1f;
                        TryPickUpWeapon();
                    }
                }
            }
        }

        private void TryPickUpWeapon()
        {
            var inv = characterBrain != null ? characterBrain.Inventory : null;
            var item = trackedLoot.Source as JUTPS.ItemSystem.JUItem;
            if (inv == null || item == null) return;

            trackedPickupAttempts++;
            if (trackedPickupAttempts > 3)
            {
                // The bot's inventory has no matching slot for this weapon; stop chasing it.
                lootMemory.Ignore(trackedLoot.Source);
                trackedLootValid = false;
                return;
            }

            inv.ItemToPickUp = item;
            inv.PickUp();
        }

        private void RefreshFrontierIfNeeded()
        {
            frontierTimer -= Time.deltaTime;
            bool reached = hasFrontier && FlatDistance(transform.position, frontierPoint) < 4f;
            if (hasFrontier && !reached && frontierTimer > 0f) return;

            frontierTimer = 4f;
            hasFrontier = explorationMap != null &&
                          explorationMap.TryFindFrontier(transform.position, transform.forward, IsOnNavMesh, out frontierPoint);
        }

        private static bool IsOnNavMesh(Vector3 p)
        {
            return NavMesh.SamplePosition(p, out _, 8f, NavMesh.AllAreas);
        }

        private Vector3 GetExploreDirection(out bool farFromGoal)
        {
            RefreshFrontierIfNeeded();
            farFromGoal = false;

            if (hasFrontier)
            {
                farFromGoal = FlatDistance(transform.position, frontierPoint) > 20f;
                return ComputeSteeredDirectionTo(frontierPoint);
            }

            return GetWaypointPatrolDirection();
        }

        private void AddExplorationObservations(VectorSensor sensor)
        {
            if (trackedLootValid && trackedLoot.IsAvailable())
            {
                Vector3 to = trackedLoot.Position - transform.position;
                float d = to.magnitude;
                Vector3 local = d > 0.01f ? transform.InverseTransformDirection(to / d) : Vector3.forward;

                sensor.AddObservation(1f);
                sensor.AddObservation(local.x);
                sensor.AddObservation(local.z);
                sensor.AddObservation(Mathf.Clamp01(d / Mathf.Max(1f, LootSenseRadius)));
                sensor.AddObservation(trackedLoot.Kind == LootKind.Weapon ? 1f : 0f);
                sensor.AddObservation(trackedLoot.Kind == LootKind.Ammo ? 1f : 0f);
            }
            else
            {
                for (int i = 0; i < 6; i++) sensor.AddObservation(0f);
            }

            sensor.AddObservation(explorationMap != null ? explorationMap.Coverage : 0f);

            if (hasFrontier)
            {
                Vector3 toFrontier = frontierPoint - transform.position;
                toFrontier.y = 0f;
                Vector3 local = toFrontier.sqrMagnitude > 0.01f
                    ? transform.InverseTransformDirection(toFrontier.normalized)
                    : Vector3.forward;
                sensor.AddObservation(local.x);
                sensor.AddObservation(local.z);
            }
            else
            {
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
            }
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        #endregion
    }
}
