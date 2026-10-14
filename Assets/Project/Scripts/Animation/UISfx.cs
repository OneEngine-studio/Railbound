using UnityEngine;

namespace Railbound.UI.Anim
{
    /// <summary>
    /// UI sound feedback. Click sounds are synthesized on first use so buttons work without
    /// any audio assets; assign <see cref="clickClip"/> to use a custom clip instead.
    /// </summary>
    public static class UISfx
    {
        public static bool Muted;
        public static float Volume = 0.5f;
        public static AudioClip clickClip;      // optional override
        public static AudioClip hoverClip;      // optional override (silent by default)

        private static AudioSource _source;
        private static AudioClip _proceduralClick;

        public static void PlayClick()
        {
            if (!Application.isPlaying || Muted) return;
            var clip = clickClip != null ? clickClip : (_proceduralClick != null ? _proceduralClick : _proceduralClick = BuildClick());
            Source().PlayOneShot(clip, Volume);
        }

        public static void PlayHover()
        {
            if (!Application.isPlaying || Muted || hoverClip == null) return;
            Source().PlayOneShot(hoverClip, Volume * 0.4f);
        }

        private static AudioSource Source()
        {
            if (_source == null)
            {
                var go = new GameObject("__UISfx__");
                go.hideFlags = HideFlags.HideInHierarchy;
                Object.DontDestroyOnLoad(go);
                _source = go.AddComponent<AudioSource>();
                _source.playOnAwake = false;
                _source.spatialBlend = 0f;
            }
            return _source;
        }

        /// <summary>Short synthesized tick: descending sine sweep with a fast decay.</summary>
        private static AudioClip BuildClick()
        {
            const int sampleRate = 44100;
            const float duration = 0.055f;
            var samples = Mathf.CeilToInt(sampleRate * duration);
            var data = new float[samples];
            for (var i = 0; i < samples; i++)
            {
                var t = (float)i / sampleRate;
                var progress = t / duration;
                var frequency = Mathf.Lerp(1350f, 780f, progress);
                var envelope = Mathf.Pow(1f - progress, 2.4f);
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * 0.8f;
            }

            var clip = AudioClip.Create("UI_Click", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
