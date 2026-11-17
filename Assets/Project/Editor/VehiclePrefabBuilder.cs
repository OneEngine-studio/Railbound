using System.Collections.Generic;
using System.IO;
using Railbound.Vehicles;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace Railbound.Editor
{
    /// <summary>
    /// Builds the match's network vehicles from the NWH Vehicle Physics 2 prefabs: a standalone copy (the NWH cameras, mirror
    /// cameras, audio listeners and reflection probes removed), with a NetworkObject, an owner-authoritative transform and
    /// <see cref="RailboundVehicle"/> (seats, steering wheel, spawn weight), saved to Resources/Vehicles and registered in the
    /// default network prefab list. Run it again after changing a seat or after an NWH update.
    /// </summary>
    public static class VehiclePrefabBuilder
    {
        private const string OutDir = "Assets/Project/Resources/Vehicles";
        private const string NetworkPrefabsAsset = "Assets/DefaultNetworkPrefabs.asset";
        private const string Nwh = "Assets/NWH/Vehicle Physics 2/Vehicles/";

        private struct Spec
        {
            public string Source, Out, Name, Wheel;
            public float Weight, WheelRadius;
            public VehicleSeatDef[] Seats;
        }

        private static VehicleSeatDef Seat(string name, float x, float y, float z, bool driver = false)
        {
            return new VehicleSeatDef { Name = name, Hips = new Vector3(x, y, z), Driver = driver };
        }

        // the pelvis of each seat in vehicle space (from the NWH driver cameras: about 0.6 m below and 0.1 m behind the eyes)
        private static readonly Spec[] Specs =
        {
            new Spec
            {
                Source = Nwh + "Niva/Niva.prefab", Out = "RB Niva", Name = "OFFROADER", Wheel = "Body/niva_steeringWheel_low_Circle.003",
                Weight = 3f, WheelRadius = 0.18f,
                Seats = new[] { Seat("DRIVER", -0.42f, 0.74f, -0.30f, true), Seat("PASSENGER", 0.42f, 0.74f, -0.30f), Seat("REAR LEFT", -0.40f, 0.76f, -1.02f), Seat("REAR RIGHT", 0.40f, 0.76f, -1.02f) }
            },
            new Spec
            {
                Source = Nwh + "Sports Car by GR3D/Sports Car.prefab", Out = "RB Sports Car", Name = "SPORTS CAR", Wheel = "Steering Wheel",
                Weight = 2f, WheelRadius = 0.17f,
                Seats = new[] { Seat("DRIVER", -0.36f, 0.34f, -0.04f, true), Seat("PASSENGER", 0.36f, 0.34f, -0.04f) }
            },
            new Spec
            {
                Source = Nwh + "MonsterTruck/MonsterTruck.prefab", Out = "RB Monster Truck", Name = "MONSTER TRUCK", Wheel = "SteeringWheel",
                Weight = 1.5f, WheelRadius = 0.18f,
                Seats = new[] { Seat("DRIVER", -0.28f, 1.02f, 0.28f, true), Seat("PASSENGER", 0.30f, 1.02f, 0.28f) }
            },
        };

        [MenuItem("Tools/Railbound/Vehicles/Build Network Vehicle Prefabs")]
        public static void BuildAll()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Project/Resources")) AssetDatabase.CreateFolder("Assets/Project", "Resources");
            if (!AssetDatabase.IsValidFolder(OutDir)) AssetDatabase.CreateFolder("Assets/Project/Resources", "Vehicles");

            var built = new List<GameObject>();
            foreach (var spec in Specs)
            {
                var go = Build(spec);
                if (go != null) built.Add(go);
            }
            Register(built);
            AssetDatabase.SaveAssets();
            Debug.Log($"[VehiclePrefabBuilder] Built {built.Count}/{Specs.Length} vehicle prefabs.");
        }

        /// <summary>Batch mode: -executeMethod Railbound.Editor.VehiclePrefabBuilder.BuildAllBatch</summary>
        public static void BuildAllBatch()
        {
            int code = 0;
            try { BuildAll(); if (LoadBuilt().Count != Specs.Length) code = 2; }
            catch (System.Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }

        private static List<GameObject> LoadBuilt()
        {
            var list = new List<GameObject>();
            foreach (var spec in Specs)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(OutDir + "/" + spec.Out + ".prefab");
                if (go != null && go.GetComponent<RailboundVehicle>() != null) list.Add(go);
            }
            return list;
        }

        private static GameObject Build(Spec spec)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(spec.Source);
            if (src == null) { Debug.LogError("[VehiclePrefabBuilder] Missing " + spec.Source); return null; }

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
            try
            {
                PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                inst.name = spec.Out;
                inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                RailboundVehicle.StripStandaloneParts(inst);

                var rb = inst.GetComponent<Rigidbody>();
                if (rb != null) rb.interpolation = RigidbodyInterpolation.Interpolate;
                var vc = inst.GetComponent<NWH.VehiclePhysics2.VehicleController>();
                if (vc != null) vc.input.autoSetInput = false;

                var no = inst.GetComponent<NetworkObject>() ?? inst.AddComponent<NetworkObject>();
                no.DontDestroyWithOwner = true;
                no.AutoObjectParentSync = false;
                var nt = inst.GetComponent<ClientNetworkTransform>() ?? inst.AddComponent<ClientNetworkTransform>();
                nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
                nt.Interpolate = true;
                nt.InLocalSpace = false;

                var wheel = inst.transform.Find(spec.Wheel);
                var rv = inst.GetComponent<RailboundVehicle>() ?? inst.AddComponent<RailboundVehicle>();
                rv.BakeForPrefab(spec.Name, spec.Seats, spec.Weight, wheel);
                var so = new SerializedObject(rv);
                so.FindProperty("steeringWheelRadius").floatValue = spec.WheelRadius;
                so.ApplyModifiedPropertiesWithoutUndo();

                string path = OutDir + "/" + spec.Out + ".prefab";
                var saved = PrefabUtility.SaveAsPrefabAsset(inst, path, out bool ok);
                if (!ok || saved == null) { Debug.LogError("[VehiclePrefabBuilder] Could not save " + path); return null; }

                // the NetworkObject's id comes from the asset's GUID, which exists only now: open, touch and save once more
                var contents = PrefabUtility.LoadPrefabContents(path);
                EditorUtility.SetDirty(contents.GetComponent<NetworkObject>());
                PrefabUtility.SaveAsPrefabAsset(contents, path);
                PrefabUtility.UnloadPrefabContents(contents);
                saved = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                var hash = new SerializedObject(saved.GetComponent<NetworkObject>()).FindProperty("GlobalObjectIdHash");
                var savedRv = saved.GetComponent<RailboundVehicle>();
                Debug.Log($"[VehiclePrefabBuilder] {path}: hash={(hash != null ? hash.longValue.ToString() : "?")} seats={savedRv.SeatCount} " +
                          $"wheel={(savedRv.SteeringWheel != null ? savedRv.SteeringWheel.name : "NONE")} bounds={savedRv.LocalBounds.center:F2}/{savedRv.LocalBounds.size:F2} " +
                          $"cameras={saved.GetComponentsInChildren<Camera>(true).Length} listeners={saved.GetComponentsInChildren<AudioListener>(true).Length}");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(inst);
            }
        }

        private static void Register(List<GameObject> prefabs)
        {
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetworkPrefabsAsset);
            if (list == null) { Debug.LogError("[VehiclePrefabBuilder] Missing " + NetworkPrefabsAsset); return; }
            foreach (var go in prefabs)
            {
                bool present = false;
                foreach (var p in list.PrefabList) if (p.Prefab == go) { present = true; break; }
                if (present) continue;
                list.Add(new NetworkPrefab { Prefab = go });
                Debug.Log("[VehiclePrefabBuilder] Registered network prefab " + go.name);
            }
            EditorUtility.SetDirty(list);
        }
    }
}
