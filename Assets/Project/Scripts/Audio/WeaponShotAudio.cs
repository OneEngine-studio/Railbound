using System.Collections.Generic;
using UnityEngine;
using JUTPS;

namespace Railbound.Audio
{
    /// <summary>
    /// Realistic gunshot audio for every weapon (players and bots).
    ///
    /// Before this, each weapon AudioSource was fully 2D (spatialBlend = 0), so every shot played at full volume
    /// wherever it was fired and could be heard across the whole map. Shots now go through a pool of 3D sources:
    ///  - volume falls off with distance and reaches silence at a per-gun range,
    ///  - far shots are low-passed (the sharp crack is lost over distance),
    ///  - sound travels at ~343 m/s, so a shot 300 m away is heard about 0.9 s later,
    ///  - walls, hills and terrain between the shot and the listener muffle it further.
    /// </summary>
    public static class WeaponShotAudio
    {
        private const float SpeedOfSound = 343f;
        private const int PoolSize = 32;

        // Audible range per weapon (metres). Unknown weapons use DefaultRange.
        private const float DefaultRange = 220f;
        private static readonly Dictionary<string, float> RangeByWeapon = new Dictionary<string, float>
        {
            { "P226", 150f },
            { "P226 Dual", 170f },
            { "UMP", 230f },
            { "Benelli M4", 200f },
            { "M82", 450f }
        };

        // Normalised distance (0..1 of the weapon range) -> volume. Close to inverse-distance, reaching 0 at the range.
        private static readonly AnimationCurve Rolloff = new AnimationCurve(
            new Keyframe(0.00f, 1.00f),
            new Keyframe(0.04f, 0.70f),
            new Keyframe(0.12f, 0.38f),
            new Keyframe(0.30f, 0.14f),
            new Keyframe(0.60f, 0.04f),
            new Keyframe(1.00f, 0.00f));

        private sealed class PooledSource
        {
            public AudioSource Source;
            public AudioLowPassFilter LowPass;
            public float FreeAtTime;
        }

        private static readonly List<PooledSource> pool = new List<PooledSource>();
        private static Transform poolRoot;
        private static AudioListener listener;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            pool.Clear();
            poolRoot = null;
            listener = null;
        }

        public static float RangeFor(string weaponName)
        {
            if (!string.IsNullOrEmpty(weaponName) && RangeByWeapon.TryGetValue(weaponName, out float r)) return r;
            return DefaultRange;
        }

        /// <summary>
        /// The weapon's own AudioSource now only carries quiet gear sounds (reload, equip, empty click),
        /// which should be heard close by, not across the map.
        /// </summary>
        public static void ConfigureGearSource(AudioSource source)
        {
            if (source == null) return;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 1.5f;
            source.maxDistance = 35f;
        }

        /// <summary>Plays one gunshot from a world position with distance, delay, muffling and occlusion.</summary>
        public static void PlayShot(AudioClip clip, Vector3 position, float pitch, string weaponName)
        {
            if (clip == null) return;

            float range = RangeFor(weaponName);
            Transform listenerTransform = GetListenerTransform();

            float distance = 0f;
            if (listenerTransform != null)
            {
                distance = Vector3.Distance(listenerTransform.position, position);
                if (distance >= range) return; // inaudible: do not spend a voice on it
            }

            PooledSource ps = Acquire();
            if (ps == null) return;

            float t = Mathf.Clamp01(distance / range);

            // Distance muffling: sharp crack lost over distance
            float cutoff = Mathf.Lerp(22000f, 1700f, Mathf.Pow(t, 0.6f));
            float volumeScale = 1f;

            // Occlusion: solid geometry or terrain between the shot and the listener
            if (listenerTransform != null && distance > 8f && IsOccluded(listenerTransform.position, position))
            {
                cutoff *= 0.35f;
                volumeScale = 0.6f;
            }

            AudioSource s = ps.Source;
            s.transform.position = position;
            s.clip = clip;
            s.pitch = pitch;
            s.volume = volumeScale;
            s.maxDistance = range;
            s.SetCustomCurve(AudioSourceCurveType.CustomRolloff, Rolloff);
            ps.LowPass.cutoffFrequency = Mathf.Clamp(cutoff, 800f, 22000f);

            float delay = distance > 20f ? distance / SpeedOfSound : 0f;
            ps.FreeAtTime = Time.time + delay + clip.length / Mathf.Max(0.1f, pitch) + 0.25f;

            if (delay > 0.02f)
            {
                s.PlayScheduled(AudioSettings.dspTime + delay);
            }
            else
            {
                s.Play();
            }
        }

        // ------------------------------------------------------------------ helpers

        private static Transform GetListenerTransform()
        {
            if (listener == null || !listener.isActiveAndEnabled)
            {
                listener = Object.FindObjectOfType<AudioListener>();
            }
            return listener != null ? listener.transform : null;
        }

        private static bool IsOccluded(Vector3 from, Vector3 to)
        {
            if (!Physics.Linecast(from, to, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore)) return false;

            Collider c = hit.collider;
            if (c == null) return false;

            // Characters and loose props do not muffle sound; walls, vehicles' bodies and terrain do.
            if (c.GetComponentInParent<JUCharacterController>() != null) return false;
            Rigidbody rb = c.GetComponentInParent<Rigidbody>();
            if (rb != null && !rb.isKinematic) return false;
            return true;
        }

        private static PooledSource Acquire()
        {
            EnsureRoot();

            PooledSource oldest = null;
            for (int i = 0; i < pool.Count; i++)
            {
                PooledSource p = pool[i];
                if (p.Source == null) continue;

                if (!p.Source.isPlaying && Time.time >= p.FreeAtTime) return p;
                if (oldest == null || p.FreeAtTime < oldest.FreeAtTime) oldest = p;
            }

            if (pool.Count < PoolSize)
            {
                return CreateSource();
            }

            // Everything is busy: steal the one that finishes first
            if (oldest != null) oldest.Source.Stop();
            return oldest;
        }

        private static void EnsureRoot()
        {
            if (poolRoot != null) return;

            var root = new GameObject("[Audio] Weapon Shot Pool");
            root.hideFlags = HideFlags.DontSave;
            Object.DontDestroyOnLoad(root);
            poolRoot = root.transform;
            pool.Clear();
        }

        private static PooledSource CreateSource()
        {
            var go = new GameObject("ShotSource " + pool.Count);
            go.transform.SetParent(poolRoot, false);

            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = false;
            s.spatialBlend = 1f;
            s.dopplerLevel = 0f;
            s.spread = 25f;
            s.minDistance = 4f;
            s.rolloffMode = AudioRolloffMode.Custom;

            var lp = go.AddComponent<AudioLowPassFilter>();
            lp.cutoffFrequency = 22000f;

            var ps = new PooledSource { Source = s, LowPass = lp, FreeAtTime = 0f };
            pool.Add(ps);
            return ps;
        }
    }
}
