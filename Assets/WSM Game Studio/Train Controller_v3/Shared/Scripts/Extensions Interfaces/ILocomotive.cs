using System.Collections.Generic;
using UnityEngine;
using WSMGameStudio.Splines;

namespace WSMGameStudio.RailroadSystem
{
    public interface ILocomotive
    {
        bool EnginesOn { get; set; }
        float Acceleration { get; set; }
        float AccelerationRate { get; set; }
        float BrakingDecelerationRate { get; set; }
        float InertiaDecelerationRate { get; set; }
        bool AutomaticBrakes { get; set; }
        float Brake { get; set; }
        bool EmergencyBrakes { get; set; }
        float MaxSpeed { get; set; }
        SpeedUnits SpeedUnit { get; set; }

        AudioSource EngineSFX { get; set; }
        AudioSource HornSFX { get; set; }
        AudioSource BellSFX { get; set; }

        ParticleSystem SmokeParticles { get; set; }
        ParticleSystem[] BrakingSparksParticles { get; set; }
        Animator BellAnimator { get; set; }

        bool BellOn { get; }
        float Speed_MPS { get; }
        float Speed_KPH { get; }
        float Speed_MPH { get; }
        ITrainDoorsController DoorsController { get; }
        GameObject GetGameObject { get; }

        List<GameObject> ConnectedWagons { get; }

        void ToggleLights();
        void ToggleInternalLights();
        void Honk();
        void ToogleBell();
        void ToggleEngine();
        void ToggleEmergencyBrakes();
        void UpdateDoorController();
        void AddWagons(List<GameObject> newWagons);
        void RemoveAllWagons();
        void CalculateWagonsPositions();
        void CalculateWagonsPositions(List<Spline> targetRails);
        void AssignRoute(Route newRoute, float t = 0.5f);
    } 
}
