using System;
using System.Collections.Generic;
using UnityEngine;
using WSMGameStudio.Splines;

namespace WSMGameStudio.RailroadSystem
{
    public static class CustomWagonCreator
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="profile"></param>
        /// <param name="modelPrefab"></param>
        /// <param name="name"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public static bool Validate(CustomWagonProfile profile, GameObject modelPrefab, string name, out string message)
        {
            message = string.Empty;

            if (profile == null)
            {
                message = string.Format("Profile cannot be null.{0}Please select a profile and try again.", System.Environment.NewLine);
                return false;
            }

            if (modelPrefab == null)
            {
                message = string.Format("Model cannot be null.{0}Please select your custom model and try again.", System.Environment.NewLine);
                return false;
            }

            if (name == null || name == string.Empty)
            {
                message = string.Format("Name cannot be null.{0}Please select a name and try again.", System.Environment.NewLine);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="profile"></param>
        /// <param name="modelPrefab"></param>
        /// <param name="offset"></param>
        /// <param name="name"></param>
        public static bool Create(CustomWagonProfile profile, GameObject modelPrefab, string name, out string message)
        {
            message = string.Empty;

            Vector3 railsOffset = profile.railsOffset;

            //Wagon/locomotive game object
            GameObject wagonInstance = new GameObject(name);
            //Model
            GameObject modelInstance;
            if (modelPrefab != null)
                modelInstance = InstantiateChild(modelPrefab, modelPrefab.name, profile.modelOffset, Quaternion.identity, wagonInstance.transform);
            //Default child instances
            GameObject wheels = InstantiateChild("Wheels", railsOffset, Quaternion.identity, wagonInstance.transform);
            GameObject sfx = InstantiateChild("SFX", Vector3.zero, Quaternion.identity, wagonInstance.transform);
            GameObject vfx = InstantiateChild("VFX", Vector3.zero, Quaternion.identity, wagonInstance.transform);
            GameObject colliders = InstantiateChild("Colliders", Vector3.zero, Quaternion.identity, wagonInstance.transform);
            GameObject bumpers = InstantiateChild("Bumpers", Vector3.zero, Quaternion.identity, wagonInstance.transform);
            GameObject doors = InstantiateChild("Doors", Vector3.zero, Quaternion.identity, wagonInstance.transform);
            GameObject lights = InstantiateChild("Lights", Vector3.zero, Quaternion.identity, wagonInstance.transform);
            GameObject sensors = null;
            GameObject suspension = null;

            if (profile.trainType == TrainType.PhysicsBased)
            {
                sensors = InstantiateChild("Sensors", railsOffset, Quaternion.identity, wagonInstance.transform);
                suspension = InstantiateChild("Suspension", railsOffset, Quaternion.identity, wheels.transform);
            }

            //Add common unity components
            Rigidbody rigidbody = wagonInstance.AddComponent<Rigidbody>();
            //Configure common unity components
            rigidbody.mass = 10000f;
            rigidbody.useGravity = (profile.trainType == TrainType.PhysicsBased);
            rigidbody.isKinematic = (profile.trainType == TrainType.SplineBased);
            rigidbody.interpolation = (profile.trainType == TrainType.PhysicsBased) ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
            rigidbody.detectCollisions = true;

            //Instantiate common profile components
            InstantiateWagonComponents(profile.bumper, bumpers.transform, null);
            if (profile.trainType == TrainType.PhysicsBased)
                InstantiateWagonComponents(profile.suspensionCollider, suspension.transform, null);
            InstantiateWagonComponents(profile.colliders, colliders.transform, null);
            InstantiateWagonComponents(profile.internalDetails, wagonInstance.transform, null);
            InstantiateWagonComponents(profile.passengerSensor, wagonInstance.transform, null);

            //Instantiate specific profile components
            if (profile.type == WagonType.Wagon)
            {
                IRailwayVehicle wagonScript = null;
                HingeJoint hingeJoint = null;

                //Add specific unity components
                if (profile.trainType == TrainType.PhysicsBased)
                {
                    wagonScript = wagonInstance.AddComponent<Wagon_v3>();
                    hingeJoint = wagonInstance.AddComponent<HingeJoint>();
                    hingeJoint.axis = new Vector3(1f, 1f, 0f);

                }
                else if (profile.trainType == TrainType.SplineBased)
                    wagonScript = wagonInstance.AddComponent<SplineBasedWagon>();

                //Instantiate specific unity components
                InstantiateWagonComponents(profile.wheelsSFX, sfx.transform, new Action<IRailwayVehicle, List<GameObject>>(ConfigureWheelSFX), wagonScript);
                InstantiateWagonComponents(profile.brakesSFX, sfx.transform, new Action<IRailwayVehicle, List<GameObject>>(ConfigureBrakesSFX), wagonScript);
                InstantiateWagonComponents(profile.wagonConnectionSFX, sfx.transform, new Action<IRailwayVehicle, List<GameObject>>(ConfigureConnectionSFX), wagonScript);
                //Couplers
                if (profile.trainType == TrainType.PhysicsBased)
                    InstantiateWagonComponents(profile.frontCoupler, wagonInstance.transform, new Action<Rigidbody, IRailwayVehicle, HingeJoint, List<GameObject>>(ConfigureFrontCoupler), rigidbody, wagonScript, hingeJoint);
                else if (profile.trainType == TrainType.SplineBased)
                    InstantiateWagonComponents(profile.frontCoupler, wagonInstance.transform, new Action<Rigidbody, IRailwayVehicle, List<GameObject>>(ConfigureFrontCoupler), rigidbody, wagonScript);

                InstantiateWagonComponents(profile.backCoupler, wagonInstance.transform, new Action<Rigidbody, IRailwayVehicle, List<GameObject>>(ConfigureBackCoupler), rigidbody, wagonScript);
                //Lights
                InstantiateWagonComponents(profile.externalLights, lights.transform, new Action<IRailwayVehicle, List<GameObject>>(ConfigureExternalLights), wagonScript);
                InstantiateWagonComponents(profile.internalLights, lights.transform, new Action<IRailwayVehicle, List<GameObject>>(ConfigureInternalLights), wagonScript);
                //Wheels
                InstantiateWagonComponents(profile.wheelsVisuals, wheels.transform, new Action<IRailwayVehicle, List<GameObject>>(ConfigureWheels), wagonScript);

                if (profile.trainType == TrainType.PhysicsBased)
                {
                    InstantiateWagonComponents(profile.wheelsPhysics, wheels.transform, new Action<Rigidbody, IRailwayVehicle, List<GameObject>>(ConfigurePhysicsWheels), rigidbody, wagonScript);
                    //Sensors
                    InstantiateWagonComponents(profile.railSensor, sensors.transform, new Action<IRailwayVehicle, List<GameObject>>(ConfigureSensors), wagonScript);
                    InstantiateWagonComponents(profile.defaultJointAnchor, wagonInstance.transform, new Action<IRailwayVehicle, List<GameObject>>(ConfigureDefaultJointAnchor), wagonScript);
                }
            }
            else if (profile.type == WagonType.Locomotive)
            {
                ILocomotive locomotiveScript = null;
                //Add specific unity components
                if (profile.trainType == TrainType.PhysicsBased)
                    locomotiveScript = wagonInstance.AddComponent<TrainController_v3>();
                else if (profile.trainType == TrainType.SplineBased)
                {
                    locomotiveScript = wagonInstance.AddComponent<SplineBasedLocomotive>();
                }

                TrainPlayerInput playerInput = wagonInstance.AddComponent<TrainPlayerInput>();
                TrainSpeedMonitor speedMonitor = wagonInstance.AddComponent<TrainSpeedMonitor>();
                TrainStationController stationController = wagonInstance.AddComponent<TrainStationController>();
                PassengerTags passengerTags = wagonInstance.AddComponent<PassengerTags>();
                passengerTags.passengerTags = new List<string>() { "Player", "NPC" };
                BoxCollider locomotiveTrigger = wagonInstance.AddComponent<BoxCollider>();

                //Configure specific unity components
                locomotiveScript.EnginesOn = true;
                locomotiveScript.Acceleration = 1f;
                locomotiveScript.SpeedUnit = profile.speedUnits;
                playerInput.inputSettings = profile.inputSettings;
                locomotiveTrigger.isTrigger = true;
                locomotiveTrigger.size = new Vector3(0.5f, 0.5f, 0.5f);
                locomotiveTrigger.center = profile.controlZoneTriggerPosition;

                //Instantitate specific unity components
                InstantiateWagonComponents(profile.engineSFX, sfx.transform, new Action<ILocomotive, List<GameObject>>(ConfigureEngineSFX), locomotiveScript);
                InstantiateWagonComponents(profile.brakesSFX, sfx.transform, new Action<IRailwayVehicle, List<GameObject>>(ConfigureBrakesSFX), locomotiveScript);
                InstantiateWagonComponents(profile.wheelsSFX, sfx.transform, new Action<IRailwayVehicle, List<GameObject>>(ConfigureWheelSFX), locomotiveScript);
                InstantiateWagonComponents(profile.hornSFX, sfx.transform, new Action<ILocomotive, List<GameObject>>(ConfigureHornSFX), locomotiveScript);
                InstantiateWagonComponents(profile.bellSFX, sfx.transform, new Action<ILocomotive, List<GameObject>>(ConfigureBellSFX), locomotiveScript);
                //Particles
                InstantiateWagonComponents(profile.smokeParticles, vfx.transform, new Action<ILocomotive, List<GameObject>>(ConfigureSmokeParticles), locomotiveScript);
                InstantiateWagonComponents(profile.brakingSparksParticles, vfx.transform, new Action<ILocomotive, List<GameObject>>(ConfigureBrakingSparksParticles), locomotiveScript);
                //Couplers
                InstantiateWagonComponents(profile.frontCoupler, wagonInstance.transform, new Action<Rigidbody, IRailwayVehicle, List<GameObject>>(ConfigureFrontCoupler), rigidbody, locomotiveScript);
                InstantiateWagonComponents(profile.backCoupler, wagonInstance.transform, new Action<Rigidbody, IRailwayVehicle, List<GameObject>>(ConfigureBackCoupler), rigidbody, locomotiveScript);
                //Lights
                InstantiateWagonComponents(profile.externalLights, lights.transform, new Action<IRailwayVehicle, List<GameObject>>(ConfigureExternalLights), locomotiveScript);
                InstantiateWagonComponents(profile.internalLights, lights.transform, new Action<IRailwayVehicle, List<GameObject>>(ConfigureInternalLights), locomotiveScript);
                //Wheels
                InstantiateWagonComponents(profile.wheelsVisuals, wheels.transform, new Action<IRailwayVehicle, List<GameObject>>(ConfigureWheels), locomotiveScript);
                //Steam Locomotive Rear wheels and Pistons
                InstantiateWagonComponents(profile.steamLocomotiveRearWheelsAndPistons, wheels.transform, new Action<IRailwayVehicle, List<GameObject>>(ConfigureWheels), locomotiveScript);
                //Bell
                InstantiateWagonComponents(profile.bell, wagonInstance.transform, new Action<ILocomotive, List<GameObject>>(ConfigureBell), locomotiveScript);

                if (profile.trainType == TrainType.PhysicsBased)
                {
                    InstantiateWagonComponents(profile.wheelsPhysics, wheels.transform, new Action<Rigidbody, IRailwayVehicle, List<GameObject>>(ConfigurePhysicsWheels), rigidbody, locomotiveScript);
                    //Sensors
                    InstantiateWagonComponents(profile.railSensor, sensors.transform, new Action<IRailwayVehicle, List<GameObject>>(ConfigureSensors), locomotiveScript);
                }
            }

            TrainDoorsController doorsController = wagonInstance.AddComponent<TrainDoorsController>();
            wagonInstance.AddComponent<SMR_IgnoredObject>();

            if (profile.trainType == TrainType.PhysicsBased)
                wagonInstance.AddComponent<TrainSuspension>();

            InstantiateWagonComponents(profile.cabinDoorLeft, doors.transform, new Action<TrainDoorsController, List<GameObject>>(ConfigureLeftCabinDoor), doorsController);
            InstantiateWagonComponents(profile.cabinDoorRight, doors.transform, new Action<TrainDoorsController, List<GameObject>>(ConfigureRightCabinDoor), doorsController);
            InstantiateWagonComponents(profile.passengerDoorLeft, doors.transform, new Action<TrainDoorsController, List<GameObject>>(ConfigureLeftPassengerDoors), doorsController);
            InstantiateWagonComponents(profile.passengerDoorRight, doors.transform, new Action<TrainDoorsController, List<GameObject>>(ConfigureRightPassengerDoors), doorsController);
            InstantiateWagonComponents(profile.openCabinDoorSFX, sfx.transform, new Action<TrainDoorsController, List<GameObject>>(ConfigureOpenCabinDoorSFX), doorsController);
            InstantiateWagonComponents(profile.closeCabinDoorSFX, sfx.transform, new Action<TrainDoorsController, List<GameObject>>(ConfigureCloseCabinDoorSFX), doorsController);
            InstantiateWagonComponents(profile.openPassengerDoorSFX, sfx.transform, new Action<TrainDoorsController, List<GameObject>>(ConfigureOpenPassengerDoorSFX), doorsController);
            InstantiateWagonComponents(profile.closePassengerDoorSFX, sfx.transform, new Action<TrainDoorsController, List<GameObject>>(ConfigureClosePassengerDoorSFX), doorsController);
            InstantiateWagonComponents(profile.closeDoorWarningSFX, sfx.transform, new Action<TrainDoorsController, List<GameObject>>(ConfigureCloseDoorWarningSFX), doorsController);

            string carType = profile.type.ToString();
            message = string.Format("{0} created successfully!", carType);
            return true;
        }

        /// <summary>
        /// Instantiate new child based on prefab
        /// </summary>
        /// <param name="prefab"></param>
        /// <param name="name"></param>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <param name="parent"></param>
        /// <returns></returns>
        private static GameObject InstantiateChild(GameObject prefab, string name, Vector3 position, Quaternion rotation, Transform parent)
        {
            GameObject child = GameObject.Instantiate(prefab, position, rotation, parent);
            child.name = name;

            return child;
        }

        /// <summary>
        /// Creates a new child game object instance
        /// </summary>
        /// <param name="name"></param>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <param name="parent"></param>
        /// <returns></returns>
        private static GameObject InstantiateChild(string name, Vector3 position, Quaternion rotation, Transform parent)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent);
            child.transform.position = position;
            child.transform.rotation = rotation;

            return child;
        }

        /// <summary>
        /// Instatiate wagon component based on profile settings
        /// </summary>
        /// <param name="wagonComponent"></param>
        /// <param name="parent"></param>
        private static void InstantiateWagonComponents(CustomWagonComponent wagonComponent, Transform parent, Delegate configMethod, params object[] configParams)
        {
            if (wagonComponent.prefab == null)
                return;

            string instanceName = string.Empty;

            List<GameObject> instances = new List<GameObject>();

            for (int i = 0; i < wagonComponent.positions.Length; i++)
            {

                instanceName = wagonComponent.customName != string.Empty ? wagonComponent.customName : wagonComponent.prefab.name;

                GameObject newComponentInstance = InstantiateChild(wagonComponent.prefab, instanceName, wagonComponent.positions[i].position, Quaternion.Euler(wagonComponent.positions[i].rotation), parent);

                EnforceUniqueName(newComponentInstance);

                instances.Add(newComponentInstance);
            }

            if (configMethod != null)
            {
                List<object> args = new List<object>();
                for (int i = 0; i < configParams.Length; i++)
                    args.Add(configParams[i]);
                args.Add(instances);

                configParams = args.ToArray();

                configMethod.DynamicInvoke(configParams);
            }
        }

        #region COMPONENTS CONFIGURATION METHODS
        /// <summary>
        /// 
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="wheels"></param>
        private static void ConfigurePhysicsWheels(Rigidbody parent, IRailwayVehicle carScript, List<GameObject> wheels)
        {
            ConnectHingeAnchor(parent, wheels);
            ConfigureWheels(carScript, wheels);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="carScript"></param>
        /// <param name="wheels"></param>
        private static void ConfigureWheels(IRailwayVehicle carScript, List<GameObject> wheels)
        {
            carScript.Wheels = carScript.Wheels == null ? new List<TrainWheel_v3>() : carScript.Wheels;

            foreach (var item in wheels)
            {
                foreach (Transform t in item.transform)
                {
                    TrainWheel_v3[] wheelScripts = t.GetComponentsInChildren<TrainWheel_v3>();
                    if (wheelScripts != null)
                        carScript.Wheels.AddRange(wheelScripts);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="carScript"></param>
        /// <param name="joints"></param>
        private static void ConfigureDefaultJointAnchor(IRailwayVehicle carScript, List<GameObject> joints)
        {
            if (joints != null && joints.Count > 0)
            {
                foreach (var item in joints)
                {
                    Rigidbody rigid = item.GetComponent<Rigidbody>();
                    if (rigid != null) carScript.JointAnchor = rigid;
                }
            }
        }

        /// <summary>
        /// Configure front coupler and connect to parent
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="carScript"></param>
        /// <param name="couplers"></param>
        private static void ConfigureFrontCoupler(Rigidbody parent, IRailwayVehicle carScript, HingeJoint parentJoint, List<GameObject> couplers)
        {
            ConnectHingeAnchor(parent, couplers);

            if (couplers != null && couplers.Count > 0)
            {
                foreach (var item in couplers)
                {
                    Rigidbody rigid = item.GetComponent<Rigidbody>();
                    if (rigid != null) carScript.FrontJoint = rigid;
                    parentJoint.anchor = item.transform.position;
                }
            }
        }

        /// <summary>
        /// Configure front coupler
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="carScript"></param>
        /// <param name="couplers"></param>
        private static void ConfigureFrontCoupler(Rigidbody parent, IRailwayVehicle carScript, List<GameObject> couplers)
        {
            ConnectHingeAnchor(parent, couplers);

            if (couplers != null && couplers.Count > 0)
            {
                foreach (var item in couplers)
                {
                    Rigidbody rigid = item.GetComponent<Rigidbody>();
                    if (rigid != null) carScript.FrontJoint = rigid;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="carScript">wagon or locomotive script</param>
        /// <param name="couplers"></param>
        private static void ConfigureBackCoupler(Rigidbody parent, IRailwayVehicle carScript, List<GameObject> couplers)
        {
            ConnectHingeAnchor(parent, couplers);

            if (couplers != null && couplers.Count > 0)
            {
                foreach (var item in couplers)
                {
                    Rigidbody rigid = item.GetComponent<Rigidbody>();
                    if (rigid != null) carScript.BackJoint = rigid;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="connectedBody"></param>
        /// <param name="toConnect"></param>
        private static void ConnectHingeAnchor(Rigidbody connectedBody, List<GameObject> toConnect)
        {
            foreach (var item in toConnect)
            {
                HingeJoint joint = item.GetComponent<HingeJoint>();
                if (joint != null) joint.connectedBody = connectedBody;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="carScript"></param>
        /// <param name="sfxs"></param>
        private static void ConfigureWheelSFX(IRailwayVehicle carScript, List<GameObject> sfxs)
        {
            if (sfxs != null && sfxs.Count > 0)
            {
                foreach (var item in sfxs)
                {
                    AudioSource wheelSFX = item.GetComponent<AudioSource>();
                    if (wheelSFX != null) carScript.WheelsSFX = wheelSFX;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="carScript"></param>
        /// <param name="sfxs"></param>
        private static void ConfigureConnectionSFX(IRailwayVehicle carScript, List<GameObject> sfxs)
        {
            if (sfxs != null && sfxs.Count > 0)
            {
                foreach (var item in sfxs)
                {
                    AudioSource connectionSFX = item.GetComponent<AudioSource>();
                    if (connectionSFX != null) carScript.WagonConnectionSFX = connectionSFX;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="locomotive"></param>
        /// <param name="sfxs"></param>
        private static void ConfigureHornSFX(ILocomotive locomotive, List<GameObject> sfxs)
        {
            if (sfxs != null && sfxs.Count > 0)
            {
                foreach (var item in sfxs)
                {
                    AudioSource audio = item.GetComponent<AudioSource>();
                    if (audio != null) locomotive.HornSFX = audio;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="locomotive"></param>
        /// <param name="sfxs"></param>
        private static void ConfigureBellSFX(ILocomotive locomotive, List<GameObject> sfxs)
        {
            if (sfxs != null && sfxs.Count > 0)
            {
                foreach (var item in sfxs)
                {
                    AudioSource audio = item.GetComponent<AudioSource>();
                    if (audio != null) locomotive.BellSFX = audio;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="locomotive"></param>
        /// <param name="sfxs"></param>
        private static void ConfigureEngineSFX(ILocomotive locomotive, List<GameObject> sfxs)
        {
            if (sfxs != null && sfxs.Count > 0)
            {
                foreach (var item in sfxs)
                {
                    AudioSource audio = item.GetComponent<AudioSource>();
                    if (audio != null) locomotive.EngineSFX = audio;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="carScript"></param>
        /// <param name="sfxs"></param>
        private static void ConfigureBrakesSFX(IRailwayVehicle carScript, List<GameObject> sfxs)
        {
            if (sfxs != null && sfxs.Count > 0)
            {
                foreach (var item in sfxs)
                {
                    AudioSource audio = item.GetComponent<AudioSource>();
                    if (audio != null) carScript.BrakesSFX = audio;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="carScript"></param>
        /// <param name="sensors"></param>
        private static void ConfigureSensors(IRailwayVehicle carScript, List<GameObject> sensors)
        {
            if (sensors != null && sensors.Count > 0)
            {
                carScript.Sensors = new Sensors();

                for (int i = 0; i < sensors.Count; i++)
                {
                    if (i % 2 == 0)
                        carScript.Sensors.leftSensor = sensors[i].GetComponent<RailSensor>();
                    else
                        carScript.Sensors.rightSensor = sensors[i].GetComponent<RailSensor>();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="carScript"></param>
        /// <param name="lights"></param>
        private static void ConfigureExternalLights(IRailwayVehicle carScript, List<GameObject> lights)
        {
            if (lights != null && lights.Count > 0)
            {
                carScript.ExternalLights = new List<Light>();

                foreach (var item in lights)
                {
                    Light light = item.GetComponent<Light>();
                    if (light != null) carScript.ExternalLights.Add(light);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="carScript"></param>
        /// <param name="lights"></param>
        private static void ConfigureInternalLights(IRailwayVehicle carScript, List<GameObject> lights)
        {
            if (lights != null && lights.Count > 0)
            {
                if (carScript.InternalLights == null) carScript.InternalLights = new List<Light>();

                foreach (var item in lights)
                {
                    Light light = item.GetComponent<Light>();
                    if (light != null) carScript.InternalLights.Add(light);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="doorController"></param>
        /// <param name="doors"></param>
        private static void ConfigureLeftCabinDoor(TrainDoorsController doorController, List<GameObject> doors)
        {
            if (doors != null && doors.Count > 0)
            {
                foreach (var item in doors)
                {
                    TrainDoor doorScript = item.GetComponent<TrainDoor>();
                    if (doorScript != null) doorController.cabinDoorLeft = doorScript;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="doorController"></param>
        /// <param name="doors"></param>
        private static void ConfigureRightCabinDoor(TrainDoorsController doorController, List<GameObject> doors)
        {
            if (doors != null && doors.Count > 0)
            {
                foreach (var item in doors)
                {
                    TrainDoor doorScript = item.GetComponent<TrainDoor>();
                    if (doorScript != null) doorController.cabinDoorRight = doorScript;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="doorController"></param>
        /// <param name="doors"></param>
        private static void ConfigureLeftPassengerDoors(TrainDoorsController doorController, List<GameObject> doors)
        {
            if (doors != null && doors.Count > 0)
            {
                doorController.passengerDoorsLeft = new List<TrainDoor>();

                foreach (var item in doors)
                {
                    TrainDoor doorScript = item.GetComponent<TrainDoor>();
                    if (doorScript != null) doorController.passengerDoorsLeft.Add(doorScript);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="doorController"></param>
        /// <param name="doors"></param>
        private static void ConfigureRightPassengerDoors(TrainDoorsController doorController, List<GameObject> doors)
        {
            if (doors != null && doors.Count > 0)
            {
                doorController.passengerDoorsRight = new List<TrainDoor>();

                foreach (var item in doors)
                {
                    TrainDoor doorScript = item.GetComponent<TrainDoor>();
                    if (doorScript != null) doorController.passengerDoorsRight.Add(doorScript);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="doorController"></param>
        /// <param name="sfxs"></param>
        private static void ConfigureOpenCabinDoorSFX(TrainDoorsController doorController, List<GameObject> sfxs)
        {
            if (sfxs != null && sfxs.Count > 0)
            {
                foreach (var item in sfxs)
                {
                    AudioSource openDoorSFX = item.GetComponent<AudioSource>();
                    if (openDoorSFX != null) doorController.openCabinDoorSFX = openDoorSFX;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="doorController"></param>
        /// <param name="sfxs"></param>
        private static void ConfigureOpenPassengerDoorSFX(TrainDoorsController doorController, List<GameObject> sfxs)
        {
            if (sfxs != null && sfxs.Count > 0)
            {
                foreach (var item in sfxs)
                {
                    AudioSource openDoorSFX = item.GetComponent<AudioSource>();
                    if (openDoorSFX != null) doorController.openPassengerDoorSFX = openDoorSFX;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="doorController"></param>
        /// <param name="sfxs"></param>
        private static void ConfigureCloseCabinDoorSFX(TrainDoorsController doorController, List<GameObject> sfxs)
        {
            if (sfxs != null && sfxs.Count > 0)
            {
                foreach (var item in sfxs)
                {
                    AudioSource closeDoorSFX = item.GetComponent<AudioSource>();
                    if (closeDoorSFX != null) doorController.closeCabinDoorSFX = closeDoorSFX;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="doorController"></param>
        /// <param name="sfxs"></param>
        private static void ConfigureClosePassengerDoorSFX(TrainDoorsController doorController, List<GameObject> sfxs)
        {
            if (sfxs != null && sfxs.Count > 0)
            {
                foreach (var item in sfxs)
                {
                    AudioSource closeDoorSFX = item.GetComponent<AudioSource>();
                    if (closeDoorSFX != null) doorController.closePassengerDoorSFX = closeDoorSFX;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="doorController"></param>
        /// <param name="sfxs"></param>
        private static void ConfigureCloseDoorWarningSFX(TrainDoorsController doorController, List<GameObject> sfxs)
        {
            if (sfxs != null && sfxs.Count > 0)
            {
                foreach (var item in sfxs)
                {
                    AudioSource closeDoorSFX = item.GetComponent<AudioSource>();
                    if (closeDoorSFX != null) doorController.closeDoorsWarningSFX = closeDoorSFX;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="locomotive"></param>
        /// <param name="smokeParticles"></param>
        private static void ConfigureSmokeParticles(ILocomotive locomotive, List<GameObject> smokeParticles)
        {
            if (smokeParticles != null && smokeParticles.Count > 0)
            {
                foreach (var item in smokeParticles)
                {
                    ParticleSystem smoke = item.GetComponent<ParticleSystem>();
                    if (smoke != null) locomotive.SmokeParticles = smoke;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="locomotive"></param>
        /// <param name="brakingSparksParticles"></param>
        private static void ConfigureBrakingSparksParticles(ILocomotive locomotive, List<GameObject> brakingSparksParticles)
        {
            if (brakingSparksParticles != null && brakingSparksParticles.Count > 0)
            {
                locomotive.BrakingSparksParticles = new ParticleSystem[brakingSparksParticles.Count];

                for (int i = 0; i < brakingSparksParticles.Count; i++)
                {
                    ParticleSystem sparks = brakingSparksParticles[i].GetComponent<ParticleSystem>();
                    if (sparks != null) locomotive.BrakingSparksParticles[i] = sparks;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="locomotive"></param>
        /// <param name="bells"></param>
        private static void ConfigureBell(ILocomotive locomotive, List<GameObject> bells)
        {
            if (bells != null && bells.Count > 0)
            {
                foreach (var item in bells)
                {
                    Animator bellAnimator = item.GetComponent<Animator>();
                    if (bellAnimator != null) locomotive.BellAnimator = bellAnimator;
                }
            }
        }

        /// <summary>
        /// Make sure new object has a unique name
        /// </summary>
        /// <param name="targetObject"></param>
        private static void EnforceUniqueName(GameObject targetObject)
        {
#if UNITY_EDITOR
            targetObject.name = targetObject.name.Replace("(Clone)", string.Empty);
            UnityEditor.GameObjectUtility.EnsureUniqueNameForSibling(targetObject);
#endif
        }
        #endregion
    }
}
