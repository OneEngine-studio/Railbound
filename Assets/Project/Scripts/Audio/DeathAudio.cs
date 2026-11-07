using UnityEngine;
using UnityEngine.SceneManagement;

namespace Railbound.Gameplay
{
    /// <summary>
    /// The elimination sound. It starts when the local player dies, is a 3D source at the body (only the local player ever
    /// plays it, nothing is networked), and survives the scene reload so it can be closed slowly after the respawn:
    /// the volume eases to zero while the muffled world opens back up, the way AAA games let a death sting ring out.
    /// </summary>
    public class DeathAudio : MonoBehaviour
    {
        private const string ClipPath = "Audio/Dead_sound";
        private static DeathAudio instance;

        /// <summary>True while the sound is closing after the respawn; it owns the listener's low-pass filter meanwhile.</summary>
        public static bool IsClosing => instance != null && instance.respawned;

        /// <summary>Start the elimination sound at <paramref name="position"/> (the body).</summary>
        public static void Play(Vector3 position)
        {
            if (instance != null) Destroy(instance.gameObject);
            var go = new GameObject("[Audio] Death");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<DeathAudio>();
            instance.Begin(position);
        }

        [Tooltip("Seconds the sound takes to close after the respawn.")]
        public float closeTime = 3.5f;
        public float volume = 0.9f;
        [Tooltip("World muffle (Hz) while the death sound plays; it opens to 22 kHz over the close time.")]
        public float muffledCutoff = 650f;

        private AudioSource source;
        private AudioLowPassFilter lowPass;
        private AudioListener listener;
        private bool respawned;
        private float closeProgress, born, nextScan;

        private void Begin(Vector3 position)
        {
            born = Time.unscaledTime;
            transform.position = position;
            var clip = Resources.Load<AudioClip>(ClipPath);
            if (clip == null) { Debug.LogWarning("[DeathAudio] Resources/" + ClipPath + " not found."); Destroy(gameObject); return; }

            source = gameObject.AddComponent<AudioSource>();
            source.clip = clip; source.playOnAwake = false; source.loop = false; source.volume = 0f;
            // 3D at the body; heard fully within 10 m (the player's camera is always inside that) and spread wide
            source.spatialBlend = 1f; source.rolloffMode = AudioRolloffMode.Linear; source.minDistance = 10f; source.maxDistance = 45f; source.spread = 150f; source.dopplerLevel = 0f;
            source.bypassListenerEffects = true;   // stays clear while the world muffles
            source.ignoreListenerPause = true;
            source.Play();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (instance == this) instance = null;
        }

        private void OnSceneLoaded(Scene s, LoadSceneMode m)
        {
            // the player respawned: close the sound slowly from here on
            respawned = true;
            closeProgress = 0f;
            listener = null;
        }

        private void Update()
        {
            if (source == null) return;

            if (!respawned)
            {
                // quick fade in so the sting does not pop
                source.volume = volume * Mathf.Clamp01((Time.unscaledTime - born) / 0.25f);
                return;
            }

            // a new map initialises with long frame hitches; clamping each step makes the close start once the game runs smoothly
            closeProgress += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float k = Mathf.Clamp01(closeProgress / Mathf.Max(0.5f, closeTime));
            float eased = Mathf.SmoothStep(0f, 1f, k);
            source.volume = volume * (1f - eased);

            // follow the new camera so it is still heard at the new spawn point
            if (listener == null && Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + 0.1f;
                listener = FindFirstObjectByType<AudioListener>();
                if (listener != null && lowPass == null)
                {
                    lowPass = listener.GetComponent<AudioLowPassFilter>();
                    if (lowPass == null) lowPass = listener.gameObject.AddComponent<AudioLowPassFilter>();
                    lowPass.lowpassResonanceQ = 1f;
                }
            }
            if (listener != null) transform.position = listener.transform.position;

            if (lowPass != null)
            {
                // open the world back up, logarithmically so the sweep sounds even
                float cutoff = Mathf.Exp(Mathf.Lerp(Mathf.Log(Mathf.Max(200f, muffledCutoff)), Mathf.Log(22000f), eased));
                lowPass.cutoffFrequency = cutoff;
                lowPass.enabled = k < 0.999f;
            }

            if (k >= 1f)
            {
                if (lowPass != null) { lowPass.cutoffFrequency = 22000f; lowPass.enabled = false; }
                Destroy(gameObject);
            }
        }
    }
}
