using System.Collections.Generic;
using UnityEngine;
using TargetIndicators;
using TargetIndicators.Samples;
using JUTPS;
using JUTPS.ActionScripts;
using JUTPS.VehicleSystem;
using Railbound.BR;

namespace Project.UI
{
    /// <summary>
    /// Connects the Player (TPS Character) with the Off-Screen and Compass Target Indicator Managers.
    /// Tracks specific locations (POIs), safe zone survival direction, and specific high-value loot.
    /// Enemy indicators are disabled by default as requested to preserve tactical battle royale stealth.
    /// </summary>
    [AddComponentMenu("Project/UI/Player Target Indicators Controller")]
    public class PlayerTargetIndicatorsController : MonoBehaviour
    {
        [Header("Managers")]
        public TargetIndicatorManager OffScreenCoreManager;
        public VisualIndicatorManager OffScreenVisualManager;
        public TargetIndicatorManager CompassCoreManager;
        public CompassTapeVisualIndicatorManager CompassVisualManager;

        [Header("Tracking Filters")]
        [Tooltip("If false, enemies will NOT be marked with Target Indicators (preserves stealth and realism).")]
        public bool trackEnemies = false;

        [Tooltip("Track specific Points of Interest (POIs) such as Depot, Radar, Quarry.")]
        public bool trackPOIs = true;

        [Tooltip("Track vehicles on the map.")]
        public bool trackVehicles = true;

        [Tooltip("Track Battle Royale Safe Zone direction to guide players to safety when outside.")]
        public bool trackSafeZone = true;

        [Tooltip("Only show Safe Zone off-screen indicator when player is outside in danger.")]
        public bool showSafeZoneOnlyWhenOutside = true;

        [Tooltip("Track specific high-value loot items or supply drops.")]
        public bool trackLoot = true;

        [Header("Off-Screen Indicator Prefabs")]
        public VisualIndicator EnemyOffScreenPrefab;
        public VisualIndicator VehicleOffScreenPrefab;
        public VisualIndicator POIOffScreenPrefab;
        public VisualIndicator SafeZoneOffScreenPrefab;
        public VisualIndicator LootOffScreenPrefab;

        [Header("Compass Tape Indicator Prefabs")]
        public CompassTapeVisualIndicator EnemyCompassPrefab;
        public CompassTapeVisualIndicator VehicleCompassPrefab;
        public CompassTapeVisualIndicator POICompassPrefab;
        public CompassTapeVisualIndicator SafeZoneCompassPrefab;
        public CompassTapeVisualIndicator LootCompassPrefab;

        [Header("Specific Points of Interest (POIs)")]
        public List<Transform> POITargets = new List<Transform>();

        [Header("Specific Loot Targets")]
        public List<Transform> LootTargets = new List<Transform>();

        private struct TrackedEntry
        {
            public Transform RootTransform;
            public Transform AnchorTransform;
            public JUHealth Health;
            public Vehicle Vehicle;
            public bool IsSafeZone;
            public bool HasOffScreen;
            public TargetIndicatorId OffScreenId;
            public bool HasCompass;
            public TargetIndicatorId CompassId;
            public bool HiddenWhileDriving;
            public bool HiddenState;
        }

        private readonly List<TrackedEntry> _trackedEntries = new List<TrackedEntry>();
        private DriveVehicles _playerDriveAbility;
        private Camera _mainCamera;
        private Transform _safeZoneAnchor;
        private int _safeZoneEntryIndex = -1;

        private void Start()
        {
            _playerDriveAbility = GetComponent<DriveVehicles>();
            if (_playerDriveAbility == null)
            {
                GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
                if (playerGo != null) _playerDriveAbility = playerGo.GetComponent<DriveVehicles>();
            }

            _mainCamera = Camera.main;
            if (_mainCamera != null)
            {
                if (OffScreenCoreManager != null && OffScreenCoreManager.Camera == null)
                    OffScreenCoreManager.Camera = _mainCamera;
                if (CompassCoreManager != null && CompassCoreManager.Camera == null)
                    CompassCoreManager.Camera = _mainCamera;
            }

            if (OffScreenVisualManager != null)
                OffScreenVisualManager.AddIndicatorMode = AddIndicatorMode.Manual;

            if (CompassVisualManager != null)
                CompassVisualManager.AddIndicatorMode = AddIndicatorMode.Manual;

            RegisterInitialTargets();
        }

        private void RegisterInitialTargets()
        {
            // 1. Enemies (DISABLED BY DEFAULT as requested)
            if (trackEnemies && (EnemyOffScreenPrefab != null || EnemyCompassPrefab != null))
            {
                JUCharacterController[] characters = FindObjectsOfType<JUCharacterController>();
                foreach (JUCharacterController ch in characters)
                {
                    if (ch == null || ch.gameObject == gameObject || ch.CompareTag("Player"))
                        continue;

                    JUHealth health = ch.GetComponent<JUHealth>();
                    if (health != null && health.IsDead)
                        continue;

                    Transform anchor = CreateOrGetAnchor(ch.transform, "IndicatorAnchor", new Vector3(0f, 1.35f, 0f));
                    AddTargetEntry(ch.transform, anchor, health, null, false, EnemyOffScreenPrefab, EnemyCompassPrefab);
                }
            }

            // 2. Specific Places / Points of Interest (POIs)
            if (trackPOIs)
            {
                foreach (Transform poi in POITargets)
                {
                    if (poi == null) continue;
                    AddTargetEntry(poi, poi, null, null, false, POIOffScreenPrefab, POICompassPrefab);
                }
            }

            // 3. Vehicles
            if (trackVehicles)
            {
                Vehicle[] vehicles = FindObjectsOfType<Vehicle>();
                foreach (Vehicle v in vehicles)
                {
                    if (v == null) continue;

                    JUHealth health = v.GetComponent<JUHealth>();
                    if (health != null && health.IsDead)
                        continue;

                    Transform anchor = CreateOrGetAnchor(v.transform, "IndicatorAnchor", new Vector3(0f, 1.25f, 0f));
                    AddTargetEntry(v.transform, anchor, health, v, false, VehicleOffScreenPrefab, VehicleCompassPrefab);
                }
            }

            // 4. Specific High-Value Loot Targets
            if (trackLoot)
            {
                foreach (Transform loot in LootTargets)
                {
                    if (loot == null) continue;
                    var offPrefab = LootOffScreenPrefab ?? POIOffScreenPrefab;
                    var compPrefab = LootCompassPrefab ?? POICompassPrefab;
                    AddTargetEntry(loot, loot, null, null, false, offPrefab, compPrefab);
                }
            }

            // 5. Safe Zone Direction Indicator ("zone direction where player need to go to save their life")
            if (trackSafeZone)
            {
                SetupSafeZoneIndicator();
            }
        }

        private void SetupSafeZoneIndicator()
        {
            if (_safeZoneAnchor == null)
            {
                var go = new GameObject("SafeZone_DirectionAnchor");
                _safeZoneAnchor = go.transform;
                _safeZoneAnchor.position = transform.position + Vector3.forward * 50f;
            }

            var offPrefab = SafeZoneOffScreenPrefab ?? POIOffScreenPrefab;
            var compPrefab = SafeZoneCompassPrefab ?? POICompassPrefab;

            _safeZoneEntryIndex = _trackedEntries.Count;
            AddTargetEntry(_safeZoneAnchor, _safeZoneAnchor, null, null, true, offPrefab, compPrefab);
        }

        public void RegisterLootTarget(Transform lootTransform)
        {
            if (lootTransform == null) return;
            if (!LootTargets.Contains(lootTransform)) LootTargets.Add(lootTransform);

            var offPrefab = LootOffScreenPrefab ?? POIOffScreenPrefab;
            var compPrefab = LootCompassPrefab ?? POICompassPrefab;
            AddTargetEntry(lootTransform, lootTransform, null, null, false, offPrefab, compPrefab);
        }

        public void UnregisterLootTarget(Transform lootTransform)
        {
            if (lootTransform == null) return;
            LootTargets.Remove(lootTransform);

            for (int i = _trackedEntries.Count - 1; i >= 0; i--)
            {
                if (_trackedEntries[i].RootTransform == lootTransform)
                {
                    RemoveTrackedEntryAt(i);
                    break;
                }
            }
        }

        private static Transform CreateOrGetAnchor(Transform parent, string name, Vector3 localOffset)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing;

            GameObject anchorGo = new GameObject(name);
            anchorGo.transform.SetParent(parent, false);
            anchorGo.transform.localPosition = localOffset;
            return anchorGo.transform;
        }

        private void AddTargetEntry(
            Transform root,
            Transform anchor,
            JUHealth health,
            Vehicle vehicle,
            bool isSafeZone,
            VisualIndicator offScreenPrefab,
            CompassTapeVisualIndicator compassPrefab)
        {
            TrackedEntry entry = new TrackedEntry
            {
                RootTransform = root,
                AnchorTransform = anchor,
                Health = health,
                Vehicle = vehicle,
                IsSafeZone = isSafeZone
            };

            if (OffScreenVisualManager != null && offScreenPrefab != null)
            {
                entry.HasOffScreen = OffScreenVisualManager.TryAddVisualIndicator(anchor, offScreenPrefab, out entry.OffScreenId);
            }

            if (CompassVisualManager != null && compassPrefab != null)
            {
                entry.HasCompass = CompassVisualManager.TryAddVisualIndicator(anchor, compassPrefab, out entry.CompassId);
            }

            _trackedEntries.Add(entry);
        }

        private void RemoveTrackedEntryAt(int index)
        {
            var entry = _trackedEntries[index];
            if (entry.HasOffScreen && OffScreenVisualManager != null)
                OffScreenVisualManager.RemoveTargetIndicator(entry.OffScreenId);

            if (entry.HasCompass && CompassVisualManager != null)
                CompassVisualManager.RemoveTargetIndicator(entry.CompassId);

            _trackedEntries.RemoveAt(index);
        }

        private void LateUpdate()
        {
            Vehicle currentDrivenVehicle = (_playerDriveAbility != null && _playerDriveAbility.IsDriving)
                ? _playerDriveAbility.CurrentVehicle
                : null;

            // Update Safe Zone Position & Visibility
            UpdateSafeZoneTracking();

            for (int i = _trackedEntries.Count - 1; i >= 0; i--)
            {
                TrackedEntry entry = _trackedEntries[i];

                if (entry.IsSafeZone) continue; // Managed separately

                // Remove indicator if target was destroyed or died
                bool isDeadOrMissing = (entry.RootTransform == null) ||
                                       (entry.Health != null && (entry.Health.IsDead || entry.Health.Health <= 0f));

                if (isDeadOrMissing)
                {
                    RemoveTrackedEntryAt(i);
                    continue;
                }

                // Hide vehicle indicator while driving it
                if (entry.Vehicle != null)
                {
                    bool shouldHide = (currentDrivenVehicle == entry.Vehicle);
                    if (shouldHide != entry.HiddenWhileDriving)
                    {
                        entry.HiddenWhileDriving = shouldHide;
                        SetIndicatorVisibility(entry, !shouldHide);
                        _trackedEntries[i] = entry;
                    }
                }
            }
        }

        private void UpdateSafeZoneTracking()
        {
            if (!trackSafeZone || _safeZoneAnchor == null || _safeZoneEntryIndex < 0 || _safeZoneEntryIndex >= _trackedEntries.Count)
                return;

            var zoneMgr = BRZoneManager.Instance;
            if (zoneMgr != null)
            {
                // Position anchor at the center of the safe zone
                _safeZoneAnchor.position = zoneMgr.CurrentCenter;

                if (showSafeZoneOnlyWhenOutside)
                {
                    bool isInside = zoneMgr.IsPositionInSafeZone(transform.position);
                    var entry = _trackedEntries[_safeZoneEntryIndex];

                    // If outside safe zone: show indicator to guide player to safety!
                    // If inside safe zone: hide off-screen indicator so HUD stays clean.
                    bool shouldShow = !isInside;

                    if (shouldShow == entry.HiddenState)
                    {
                        entry.HiddenState = !shouldShow;
                        if (shouldShow)
                        {
                            if (entry.HasOffScreen && OffScreenVisualManager != null)
                                OffScreenVisualManager.ShowVisualIndicator(entry.OffScreenId);
                        }
                        else
                        {
                            if (entry.HasOffScreen && OffScreenVisualManager != null)
                                OffScreenVisualManager.HideVisualIndicator(entry.OffScreenId);
                        }
                        _trackedEntries[_safeZoneEntryIndex] = entry;
                    }
                }
            }
        }

        private void SetIndicatorVisibility(TrackedEntry entry, bool visible)
        {
            if (visible)
            {
                if (entry.HasOffScreen && OffScreenVisualManager != null)
                    OffScreenVisualManager.ShowVisualIndicator(entry.OffScreenId);
                if (entry.HasCompass && CompassVisualManager != null)
                    CompassVisualManager.ShowVisualIndicator(entry.CompassId);
            }
            else
            {
                if (entry.HasOffScreen && OffScreenVisualManager != null)
                    OffScreenVisualManager.HideVisualIndicator(entry.OffScreenId);
                if (entry.HasCompass && CompassVisualManager != null)
                    CompassVisualManager.HideVisualIndicator(entry.CompassId);
            }
        }
    }
}
