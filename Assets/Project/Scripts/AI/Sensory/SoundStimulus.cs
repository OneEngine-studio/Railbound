using UnityEngine;

namespace Project.AI.Sensory
{
    public enum SoundType
    {
        Gunshot = 0,
        Footstep = 1,
        Explosion = 2,
        Reload = 3,
        Impact = 4
    }

    /// <summary>
    /// Represents an acoustic stimulus event in 3D world space.
    /// Used by AISoundSensorySystem to alert nearby AI agents of gunfire, footsteps, and explosions.
    /// </summary>
    public struct SoundStimulus
    {
        public Vector3 Position;
        public GameObject Instigator;
        public SoundType Type;
        public float Loudness; // Audible radius in meters
        public float Timestamp;

        public SoundStimulus(Vector3 position, GameObject instigator, SoundType type, float loudness, float timestamp)
        {
            Position = position;
            Instigator = instigator;
            Type = type;
            Loudness = loudness;
            Timestamp = timestamp;
        }
    }
}
