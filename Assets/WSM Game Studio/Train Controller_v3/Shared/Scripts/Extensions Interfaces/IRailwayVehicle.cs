using UnityEngine;
using System.Collections.Generic;

namespace WSMGameStudio.RailroadSystem
{
    public interface IRailwayVehicle
    {
        Rigidbody JointAnchor { get; set; }
        Rigidbody FrontJoint { get; set; }
        Rigidbody BackJoint { get; set; }
        List<TrainWheel_v3> Wheels { get; set; }
        AudioSource WheelsSFX { get; set; }
        AudioSource BrakesSFX { get; set; }
        AudioSource WagonConnectionSFX { get; set; }
        Sensors Sensors { get; set; }
        List<Light> ExternalLights { get; set; }
        List<Light> InternalLights { get; set; }

        GameObject GetGameObject { get; }
        TrainType TrainType { get; }

        int LocalDirection { get; }
        float CouplersDistance { get; }

        void ToggleLights();
        void ToggleInternalLights();
    } 
}
