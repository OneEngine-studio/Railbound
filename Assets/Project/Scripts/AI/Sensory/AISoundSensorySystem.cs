using System.Collections.Generic;
using UnityEngine;

namespace Project.AI.Sensory
{
    public interface IAISoundListener
    {
        Vector3 ListenerPosition { get; }
        GameObject ListenerGameObject { get; }
        float HearingSensitivityMultiplier { get; }
        void OnSoundHeard(SoundStimulus stimulus, Vector3 perceivedDirection, float perceivedDistance);
    }

    /// <summary>
    /// Global 3D Acoustic Sensory Dispatcher for AI perception.
    /// Broadcasts gunshots, explosions, and footsteps to all active AI listeners within hearing distance.
    /// Calculates distance attenuation, vector direction, and notifies AI to establish Threat Locations.
    /// </summary>
    public static class AISoundSensorySystem
    {
        private static readonly List<IAISoundListener> activeListeners = new List<IAISoundListener>();

        public static event System.Action<SoundStimulus> OnAnySoundEmitted;

        public static void RegisterListener(IAISoundListener listener)
        {
            if (listener != null && !activeListeners.Contains(listener))
            {
                activeListeners.Add(listener);
            }
        }

        public static void UnregisterListener(IAISoundListener listener)
        {
            if (listener != null)
            {
                activeListeners.Remove(listener);
            }
        }

        /// <summary>
        /// Registers a general sound stimulus at a given world position.
        /// </summary>
        public static void RegisterSound(Vector3 position, GameObject instigator, SoundType type, float loudness)
        {
            float now = Time.time;
            SoundStimulus stimulus = new SoundStimulus(position, instigator, type, loudness, now);
            OnAnySoundEmitted?.Invoke(stimulus);

            for (int i = activeListeners.Count - 1; i >= 0; i--)
            {
                var listener = activeListeners[i];
                if (listener == null || listener.ListenerGameObject == null)
                {
                    activeListeners.RemoveAt(i);
                    continue;
                }

                // Do not alert the entity that made the sound unless desired
                if (listener.ListenerGameObject == instigator)
                {
                    continue;
                }

                Vector3 listenerPos = listener.ListenerPosition;
                Vector3 toSound = position - listenerPos;
                float distance = toSound.magnitude;

                float maxAudibleRadius = loudness * Mathf.Max(0.1f, listener.HearingSensitivityMultiplier);
                if (distance <= maxAudibleRadius)
                {
                    Vector3 perceivedDir = distance > 0.01f ? toSound.normalized : Vector3.forward;
                    listener.OnSoundHeard(stimulus, perceivedDir, distance);
                }
            }
        }

        /// <summary>
        /// Convenience method to register gunshot audio events (standard 100m audible radius).
        /// </summary>
        public static void RegisterGunshot(Vector3 position, GameObject shooter, float loudness = 100f)
        {
            RegisterSound(position, shooter, SoundType.Gunshot, loudness);
        }

        /// <summary>
        /// Convenience method to register explosion audio events (standard 160m audible radius).
        /// </summary>
        public static void RegisterExplosion(Vector3 position, GameObject instigator, float loudness = 160f)
        {
            RegisterSound(position, instigator, SoundType.Explosion, loudness);
        }
    }
}
