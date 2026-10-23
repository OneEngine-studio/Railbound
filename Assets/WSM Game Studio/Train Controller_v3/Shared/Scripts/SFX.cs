using UnityEngine;

namespace WSMGameStudio.RailroadSystem
{
    [System.Serializable]
    public class SFX
    {
        public AudioSource hornSFX;
        public AudioSource bellSFX;
        public AudioSource engineSFX;
        public AudioSource wheelsSFX;
        public AudioSource brakesSFX;
        public AudioSource wagonConnectionSFX;
        public float minWheelsPitch = 0.6f;
        public float maxWheelsPitch = 1.5f;
        public float idleEnginePitch = 0.7f;
        public float maxEnginePitch = 1f;
    } 
}
