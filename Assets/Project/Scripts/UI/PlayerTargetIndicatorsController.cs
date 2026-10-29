using System.Collections.Generic;
using UnityEngine;
using TargetIndicators;
using TargetIndicators.Samples;
using JUTPS;
using JUTPS.ActionScripts;
using JUTPS.VehicleSystem;

namespace Project.UI
{
    /// <summary>
    /// Connects the Player (TPS Character) with both the Off-Screen Target Indicator Manager
    /// and the Compass Tape Target Indicator Manager, automatically tracking Enemies, Vehicles,
    /// and Map Points of Interest (POIs), and dynamically updating visibility when enemies die
    /// or when the player enters/exits a vehicle.
    /// </summary>
    [AddComponentMenu("Project/UI/Player Target Indicators Controller")]
    public class PlayerTargetIndicatorsController : MonoBehaviour
    {
        [Header("Managers")]
        public TargetIndicatorManager OffScreenCoreManager;
        public VisualIndicatorManager OffScreenVisualManager;
        public TargetIndicatorManager CompassCoreManager;
        public CompassTapeVisualIndicatorManager CompassVisualManager;

        [Header("Off-Screen Indicator Prefabs")]
        public VisualIndicator EnemyOffScreenPrefab;
        public VisualIndicator VehicleOffScreenPrefab;
        public VisualIndicator POIOffScreenPrefab;

        [Header("Compass Tape Indicator Prefabs")]
        public CompassTapeVisualIndicator EnemyCompassPrefab;
        public CompassTapeVisualIndicator VehicleCompassPrefab;
        public CompassTapeVisualIndicator POICompassPrefab;

        [Header("Tracked Targets")]
        public List<Transform> POITargets = new List<Transform>();

        private struct TrackedEntry
        {
            public Transform RootTransform;
            public Transform AnchorTransform;
            public JUHealth Health;
            public Vehicle Vehicle;
            public bool HasOffScreen;
            public TargetIndicatorId OffScreenId;
            public bool HasCompass;
            public TargetIndicatorId CompassId;
            public bool HiddenWhileDriving;
        }

        private readonly List<TrackedEntry> _trackedEntries = new List<TrackedEntry>();
        private DriveVehicles _playerDriveAbility;
        private Camera _mainCamera;

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
            // 1. Track all Enemies (tag == "Enemy" or AI characters)
            JUCharacterController[] characters = FindObjectsOfType<JUCharacterController>();
            foreach (JUCharacterController ch in characters)
            {
                if (ch == null || ch.gameObject == gameObject || ch.CompareTag("Player"))
                    continue;

                JUHealth health = ch.GetComponent<JUHealth>();
                if (health != null && health.IsDead)
                    continue;

                Transform anchor = CreateOrGetAnchor(ch.transform, "IndicatorAnchor", new Vector3(0f, 1.35f, 0f));
                AddTargetEntry(ch.transform, anchor, health, null, EnemyOffScreenPrefab, EnemyCompassPrefab);
            }

            // 2. Track all Vehicles
            Vehicle[] vehicles = FindObjectsOfType<Vehicle>();
            foreach (Vehicle v in vehicles)
            {
                if (v == null) continue;

                JUHealth health = v.GetComponent<JUHealth>();
                if (health != null && health.IsDead)
                    continue;

                Transform anchor = CreateOrGetAnchor(v.transform, "IndicatorAnchor", new Vector3(0f, 1.25f, 0f));
                AddTargetEntry(v.transform, anchor, health, v, VehicleOffScreenPrefab, VehicleCompassPrefab);
            }

            // 3. Track Map POIs
            foreach (Transform poi in POITargets)
            {
                if (poi == null) continue;
                AddTargetEntry(poi, poi, null, null, POIOffScreenPrefab, POICompassPrefab);
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
            VisualIndicator offScreenPrefab,
            CompassTapeVisualIndicator compassPrefab)
        {
            TrackedEntry entry = new TrackedEntry
            {
                RootTransform = root,
                AnchorTransform = anchor,
                Health = health,
                Vehicle = vehicle
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

        private void LateUpdate()
        {
            Vehicle currentDrivenVehicle = (_playerDriveAbility != null && _playerDriveAbility.IsDriving)
                ? _playerDriveAbility.CurrentVehicle
                : null;

            for (int i = _trackedEntries.Count - 1; i >= 0; i--)
            {
                TrackedEntry entry = _trackedEntries[i];

                // Remove indicator if target was destroyed or died
                bool isDeadOrMissing = (entry.RootTransform == null) ||
                                       (entry.Health != null && (entry.Health.IsDead || entry.Health.Health <= 0f));

                if (isDeadOrMissing)
                {
                    if (entry.HasOffScreen && OffScreenVisualManager != null)
                        OffScreenVisualManager.RemoveTargetIndicator(entry.OffScreenId);

                    if (entry.HasCompass && CompassVisualManager != null)
                        CompassVisualManager.RemoveTargetIndicator(entry.CompassId);

                    _trackedEntries.RemoveAt(i);
                    continue;
                }

                // Hide vehicle indicator while the player is actively driving that vehicle
                if (entry.Vehicle != null)
                {
                    bool shouldHide = (currentDrivenVehicle == entry.Vehicle);
                    if (shouldHide != entry.HiddenWhileDriving)
                    {
                        entry.HiddenWhileDriving = shouldHide;
                        if (shouldHide)
                        {
                            if (entry.HasOffScreen && OffScreenVisualManager != null)
                                OffScreenVisualManager.HideVisualIndicator(entry.OffScreenId);
                            if (entry.HasCompass && CompassVisualManager != null)
                                CompassVisualManager.HideVisualIndicator(entry.CompassId);
                        }
                        else
                        {
                            if (entry.HasOffScreen && OffScreenVisualManager != null)
                                OffScreenVisualManager.ShowVisualIndicator(entry.OffScreenId);
                            if (entry.HasCompass && CompassVisualManager != null)
                                CompassVisualManager.ShowVisualIndicator(entry.CompassId);
                        }
                        _trackedEntries[i] = entry;
                    }
                }
            }
        }
    }
}
