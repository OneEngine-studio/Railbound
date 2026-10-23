using UnityEngine;

namespace WSMGameStudio.RailroadSystem
{
    [CreateAssetMenu(fileName = "New Wagon Profile", menuName = "WSM Game Studio/Train Controller/Custom Wagon Profile", order = 1)]
    public class CustomWagonProfile : ScriptableObject
    {
        public WagonType type;
        public TrainType trainType;
        public SpeedUnits speedUnits;
        public TrainInputSettings inputSettings;
        public Vector3 modelOffset;
        public Vector3 controlZoneTriggerPosition;
        public Vector3 railsOffset = new Vector3(0f, 0.14f, 0f);
        public CustomWagonComponent wheelsPhysics;
        public CustomWagonComponent wheelsVisuals;
        public CustomWagonComponent steamLocomotiveRearWheelsAndPistons;
        public CustomWagonComponent frontCoupler;
        public CustomWagonComponent backCoupler;
        public CustomWagonComponent defaultJointAnchor;
        public CustomWagonComponent bumper;
        public CustomWagonComponent externalLights;
        public CustomWagonComponent internalLights;
        public CustomWagonComponent railSensor;
        public CustomWagonComponent passengerSensor;
        public CustomWagonComponent suspensionCollider;
        public CustomWagonComponent colliders;
        public CustomWagonComponent cabinDoorLeft;
        public CustomWagonComponent cabinDoorRight;
        public CustomWagonComponent passengerDoorLeft;
        public CustomWagonComponent passengerDoorRight;
        public CustomWagonComponent wheelsSFX;
        public CustomWagonComponent wagonConnectionSFX;
        public CustomWagonComponent engineSFX;
        public CustomWagonComponent brakesSFX;
        public CustomWagonComponent hornSFX;
        public CustomWagonComponent bellSFX;
        public CustomWagonComponent openCabinDoorSFX;
        public CustomWagonComponent closeCabinDoorSFX;
        public CustomWagonComponent openPassengerDoorSFX;
        public CustomWagonComponent closePassengerDoorSFX;
        public CustomWagonComponent closeDoorWarningSFX;
        public CustomWagonComponent smokeParticles;
        public CustomWagonComponent brakingSparksParticles;
        public CustomWagonComponent internalDetails;
        public CustomWagonComponent bell;
    }
}
