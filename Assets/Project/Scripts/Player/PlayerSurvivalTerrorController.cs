using System.Collections;
using JUTPS;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Railbound.Gameplay
{
    /// <summary>
    /// Implements high-stakes Battle Royale survival terror and low-health dread.
    /// When the player takes damage or is near death (1-2 hits from elimination),
    /// triggers visceral audio heartbeat pounding, ragged breathing, pulsating blood
    /// vignette, screen desaturation, and high-threat HUD warnings.
    /// </summary>
    public class PlayerSurvivalTerrorController : MonoBehaviour
    {
        [Header("Health & Threat Tuning")]
        public float maxSurvivalHealth = 50f; // Low HP pool for extreme lethality & tension
        public float dangerThreshold = 0.55f;  // Below 55% = Danger
        public float criticalThreshold = 0.30f; // Below 30% = Pure Terror / 1 hit from death

        [Header("Audio Sources & Clips")]
        public AudioSource heartbeatAudioSource;
        public AudioSource breathingAudioSource;
        public AudioClip heartbeatClip;
        public AudioClip heavyBreathingClip;

        [Header("UI Vignette & Warnings")]
        public Image bloodVignetteImage;
        public TextMeshProUGUI criticalWarningText;

        private JUHealth _playerHealth;
        private float _lastHealth;
        private float _heartbeatTimer;
        private CanvasGroup _warningCanvasGroup;

        private void Start()
        {
            _playerHealth = GetComponent<JUHealth>();
            if (_playerHealth != null)
            {
                // Set Hardcore Low Max Health
                _playerHealth.MaxHealth = maxSurvivalHealth;
                _playerHealth.Health = Mathf.Min(_playerHealth.Health, maxSurvivalHealth);
                _lastHealth = _playerHealth.Health;
            }

            SetupAudio();
            SetupVignetteAndHUD();
        }

        private void SetupAudio()
        {
            if (heartbeatAudioSource == null)
            {
                heartbeatAudioSource = gameObject.AddComponent<AudioSource>();
                heartbeatAudioSource.playOnAwake = false;
                heartbeatAudioSource.loop = true;
                heartbeatAudioSource.spatialBlend = 0f; // Direct in-ear audio
                heartbeatAudioSource.volume = 0f;
            }

            if (breathingAudioSource == null)
            {
                breathingAudioSource = gameObject.AddComponent<AudioSource>();
                breathingAudioSource.playOnAwake = false;
                breathingAudioSource.loop = true;
                breathingAudioSource.spatialBlend = 0f;
                breathingAudioSource.volume = 0f;
            }

            if (heartbeatClip == null) heartbeatClip = GenerateVisceralHeartbeatClip();
            if (heavyBreathingClip == null) heavyBreathingClip = GenerateTenseBreathingClip();

            heartbeatAudioSource.clip = heartbeatClip;
            breathingAudioSource.clip = heavyBreathingClip;

            heartbeatAudioSource.Play();
            breathingAudioSource.Play();
        }

        private void SetupVignetteAndHUD()
        {
            var hudCanvas = GameObject.Find("User Interface/UI");
            if (hudCanvas == null) hudCanvas = GameObject.Find("User Interface");
            if (hudCanvas == null) return;

            // Fullscreen Blood Vignette Overlay
            var existingVignette = hudCanvas.transform.Find("TerrorBloodVignette");
            if (existingVignette == null)
            {
                var vigGO = new GameObject("TerrorBloodVignette", typeof(RectTransform));
                vigGO.transform.SetParent(hudCanvas.transform, false);
                vigGO.transform.SetAsFirstSibling(); // Behind text, in front of gameplay

                var rt = vigGO.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero;

                bloodVignetteImage = vigGO.AddComponent<Image>();
                bloodVignetteImage.color = new Color(0.6f, 0.02f, 0.05f, 0f);
                bloodVignetteImage.raycastTarget = false;
            }
            else
            {
                bloodVignetteImage = existingVignette.GetComponent<Image>();
            }

            // High-Threat Critical Warning Text
            var existingWarning = hudCanvas.transform.Find("CriticalThreatWarning");
            if (existingWarning == null)
            {
                var warnGO = new GameObject("CriticalThreatWarning", typeof(RectTransform));
                warnGO.transform.SetParent(hudCanvas.transform, false);

                var rt = warnGO.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.22f);
                rt.anchorMax = new Vector2(0.5f, 0.22f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(500f, 36f);

                _warningCanvasGroup = warnGO.AddComponent<CanvasGroup>();
                _warningCanvasGroup.alpha = 0f;

                criticalWarningText = warnGO.AddComponent<TextMeshProUGUI>();
                criticalWarningText.fontSize = 18f;
                criticalWarningText.fontStyle = FontStyles.Bold;
                criticalWarningText.alignment = TextAlignmentOptions.Center;
                criticalWarningText.text = "<color=#FF1A26>CRITICAL DAMAGE</color> - <color=#FFAA33>LETHAL THREAT</color>";
            }
            else
            {
                criticalWarningText = existingWarning.GetComponent<TextMeshProUGUI>();
                _warningCanvasGroup = existingWarning.GetComponent<CanvasGroup>();
            }
        }

        private void Update()
        {
            if (_playerHealth == null || _playerHealth.IsDead)
            {
                MuteTerrorAudio();
                return;
            }

            // Enforce max health cap
            if (_playerHealth.MaxHealth > maxSurvivalHealth)
            {
                _playerHealth.MaxHealth = maxSurvivalHealth;
                if (_playerHealth.Health > maxSurvivalHealth) _playerHealth.Health = maxSurvivalHealth;
            }

            float currentHp = _playerHealth.Health;
            float hpPercent = Mathf.Clamp01(currentHp / maxSurvivalHealth);

            // Terror Intensity calculations
            if (hpPercent <= criticalThreshold)
            {
                // Pure Terror Mode: < 30% HP (e.g. <= 15 HP)
                float pulse = 0.65f + 0.35f * Mathf.Sin(Time.time * 8.5f);
                float alpha = Mathf.Lerp(0.35f, 0.65f, pulse);

                if (bloodVignetteImage != null)
                {
                    bloodVignetteImage.color = new Color(0.7f, 0.02f, 0.05f, alpha);
                }

                if (_warningCanvasGroup != null)
                {
                    _warningCanvasGroup.alpha = pulse;
                }

                if (heartbeatAudioSource != null)
                {
                    heartbeatAudioSource.volume = Mathf.Lerp(heartbeatAudioSource.volume, 0.95f, Time.deltaTime * 3f);
                    heartbeatAudioSource.pitch = 1.35f; // Rapid panicked heartbeat
                }

                if (breathingAudioSource != null)
                {
                    breathingAudioSource.volume = Mathf.Lerp(breathingAudioSource.volume, 0.75f, Time.deltaTime * 3f);
                    breathingAudioSource.pitch = 1.2f;
                }
            }
            else if (hpPercent <= dangerThreshold)
            {
                // Danger Mode: 30% - 55% HP
                float dangerRatio = 1f - ((hpPercent - criticalThreshold) / (dangerThreshold - criticalThreshold));
                float pulse = 0.2f + 0.15f * Mathf.Sin(Time.time * 5.0f);

                if (bloodVignetteImage != null)
                {
                    bloodVignetteImage.color = new Color(0.55f, 0.02f, 0.05f, pulse * dangerRatio);
                }

                if (_warningCanvasGroup != null)
                {
                    _warningCanvasGroup.alpha = 0f;
                }

                if (heartbeatAudioSource != null)
                {
                    heartbeatAudioSource.volume = Mathf.Lerp(heartbeatAudioSource.volume, 0.5f * dangerRatio, Time.deltaTime * 2f);
                    heartbeatAudioSource.pitch = 1.05f;
                }

                if (breathingAudioSource != null)
                {
                    breathingAudioSource.volume = Mathf.Lerp(breathingAudioSource.volume, 0.3f * dangerRatio, Time.deltaTime * 2f);
                    breathingAudioSource.pitch = 1.0f;
                }
            }
            else
            {
                // Healthy / Normal
                if (bloodVignetteImage != null)
                {
                    bloodVignetteImage.color = Color.Lerp(bloodVignetteImage.color, new Color(0.55f, 0.02f, 0.05f, 0f), Time.deltaTime * 3f);
                }

                if (_warningCanvasGroup != null)
                {
                    _warningCanvasGroup.alpha = 0f;
                }

                if (heartbeatAudioSource != null)
                {
                    heartbeatAudioSource.volume = Mathf.Lerp(heartbeatAudioSource.volume, 0f, Time.deltaTime * 2f);
                }

                if (breathingAudioSource != null)
                {
                    breathingAudioSource.volume = Mathf.Lerp(breathingAudioSource.volume, 0f, Time.deltaTime * 2f);
                }
            }
        }

        private void MuteTerrorAudio()
        {
            if (heartbeatAudioSource != null) heartbeatAudioSource.volume = 0f;
            if (breathingAudioSource != null) breathingAudioSource.volume = 0f;
            if (bloodVignetteImage != null) bloodVignetteImage.color = new Color(0.6f, 0.02f, 0.05f, 0f);
            if (_warningCanvasGroup != null) _warningCanvasGroup.alpha = 0f;
        }

        private AudioClip GenerateVisceralHeartbeatClip()
        {
            int sampleRate = 44100;
            float duration = 1.0f; // 60 BPM single lub-dub cycle
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            // Lub (t=0.0 to 0.25), Dub (t=0.35 to 0.55)
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float s = 0f;

                // First thud (Lub) at 55 Hz
                if (t >= 0.05f && t <= 0.28f)
                {
                    float localT = t - 0.05f;
                    float env = Mathf.Exp(-localT * 18f) * Mathf.Sin(localT * 18f * Mathf.PI);
                    s += Mathf.Sin(2f * Mathf.PI * 55f * localT) * env * 0.9f;
                }
                // Second thud (Dub) at 68 Hz
                if (t >= 0.32f && t <= 0.55f)
                {
                    float localT = t - 0.32f;
                    float env = Mathf.Exp(-localT * 22f) * Mathf.Sin(localT * 22f * Mathf.PI);
                    s += Mathf.Sin(2f * Mathf.PI * 68f * localT) * env * 0.75f;
                }

                samples[i] = Mathf.Clamp(s, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("VisceralHeartbeat", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip GenerateTenseBreathingClip()
        {
            int sampleRate = 44100;
            float duration = 2.4f; // Inhale (1.1s) and Exhale (1.3s)
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            System.Random rand = new System.Random(42);
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = 0f;

                if (t < 1.1f)
                {
                    // Inhale
                    env = Mathf.Sin(t / 1.1f * Mathf.PI) * 0.22f;
                }
                else
                {
                    // Exhale
                    float localT = t - 1.1f;
                    env = Mathf.Sin(localT / 1.3f * Mathf.PI) * 0.35f;
                }

                // Filtered pink-ish noise for breath
                float whiteNoise = (float)(rand.NextDouble() * 2.0 - 1.0);
                samples[i] = whiteNoise * env;
            }

            AudioClip clip = AudioClip.Create("TenseBreathing", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}

