using UnityEngine;

namespace WSMGameStudio.RailroadSystem
{
    [System.Serializable]
    public class VFX
    {
        public ParticleSystem smokeParticles;
        public ParticleSystem[] brakingSparksParticles;
        public float minSmokeEmission = 2f;
        public float maxSmokeEmission = 60f;
    } 
}
