using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using JUTPS;

namespace Railbound.Gameplay
{
    /// <summary>
    /// Manages the player's medkit inventory and healing ability.
    /// Default key is 'H', customizable in Keyboard Settings.
    /// Starts with 2 medkits and restores player health on use.
    /// </summary>
    [RequireComponent(typeof(JUHealth))]
    public class PlayerMedkitSystem : MonoBehaviour
    {
        public static PlayerMedkitSystem Instance { get; private set; }

        [Header("Medkit Inventory")]
        [Tooltip("Number of medkits currently held. Starts at 2 as per game design.")]
        [SerializeField] private int currentMedkits = 2;
        [SerializeField] private int maxMedkits = 10;

        [Header("Healing Settings")]
        [Tooltip("Amount of health restored per medkit use (MaxHealth is typically 400 or 100).")]
        [SerializeField] private float healAmount = 120f;
        [Tooltip("Whether using a medkit restores health completely to max.")]
        [SerializeField] private bool healToFull = false;
        [SerializeField] private float healCooldown = 1.0f;

        [Header("Input Settings")]
        [SerializeField] public KeyCode healKey = KeyCode.H;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip healSuccessSfx;
        [SerializeField] private AudioClip medkitPickupSfx;
        [SerializeField] private AudioClip outOfMedkitsSfx;

        // Events
        public event Action<int> OnMedkitsChanged;
        public event Action<float> OnHealed;

        private JUHealth _juHealth;
        private float _lastHealTime = -10f;
        private Coroutine _screenFlashCoroutine;

        public int CurrentMedkits => currentMedkits;
        public int MaxMedkits => maxMedkits;
        public float HealAmount => healAmount;
        public JUHealth HealthComponent => _juHealth;

        private void Awake()
        {
            Instance = this;
            _juHealth = GetComponent<JUHealth>();

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.playOnAwake = false;
                    audioSource.spatialBlend = 0f; // 2D UI sound for player
                }
            }

            // Generate procedural audio clips if not assigned
            if (healSuccessSfx == null) healSuccessSfx = CreateHealChimeClip();
            if (medkitPickupSfx == null) medkitPickupSfx = CreatePickupClip();
            if (outOfMedkitsSfx == null) outOfMedkitsSfx = CreateEmptyClickClip();

            // Load saved key binding override
            LoadSavedKey();
        }

        private void Start()
        {
            OnMedkitsChanged?.Invoke(currentMedkits);
        }

        private void Update()
        {
            CheckHealInput();
        }

        public void LoadSavedKey()
        {
            string savedKey = PlayerPrefs.GetString("Player_Heal_Key", "H");
            if (!string.IsNullOrEmpty(savedKey) && Enum.TryParse<KeyCode>(savedKey, true, out var parsed))
            {
                healKey = parsed;
            }
        }

        public void SetHealKey(KeyCode newKey)
        {
            healKey = newKey;
            PlayerPrefs.SetString("Player_Heal_Key", newKey.ToString());
            PlayerPrefs.Save();
        }

        private void CheckHealInput()
        {
            bool keyPressed = Input.GetKeyDown(healKey);

#if ENABLE_INPUT_SYSTEM
            if (!keyPressed && Keyboard.current != null)
            {
                try
                {
                    string keyName = healKey.ToString().ToLower();
                    // Handle alphanumeric or standard keys
                    if (keyName.StartsWith("alpha")) keyName = keyName.Substring(5);

                    var keyControl = Keyboard.current[keyName] as KeyControl;
                    if (keyControl != null && keyControl.wasPressedThisFrame)
                    {
                        keyPressed = true;
                    }
                }
                catch { }

                // Fallback for default 'H' key
                if (!keyPressed && healKey == KeyCode.H && Keyboard.current.hKey.wasPressedThisFrame)
                {
                    keyPressed = true;
                }
            }
#endif

            if (keyPressed)
            {
                UseMedkit();
            }
        }

        /// <summary>
        /// Attempts to use 1 medkit to heal the player.
        /// </summary>
        public bool UseMedkit()
        {
            if (_juHealth == null) _juHealth = GetComponent<JUHealth>();
            if (_juHealth == null || _juHealth.IsDead) return false;

            if (Time.time - _lastHealTime < healCooldown)
            {
                return false;
            }

            // Check if player has medkits
            if (currentMedkits <= 0)
            {
                PlayAudio(outOfMedkitsSfx, 0.7f);
                ShowNotification("<color=#FF5252>NO MEDKITS AVAILABLE!</color>");
                return false;
            }

            // Check if player is already at full health
            if (_juHealth.Health >= _juHealth.MaxHealth)
            {
                PlayAudio(outOfMedkitsSfx, 0.5f);
                ShowNotification("<color=#FFF59D>HEALTH ALREADY FULL!</color>");
                return false;
            }

            _lastHealTime = Time.time;
            currentMedkits--;

            float previousHealth = _juHealth.Health;
            float targetHealth = healToFull ? _juHealth.MaxHealth : Mathf.Min(_juHealth.Health + healAmount, _juHealth.MaxHealth);
            float actualHealed = targetHealth - previousHealth;

            _juHealth.Health = targetHealth;

            // Trigger events and effects
            PlayAudio(healSuccessSfx, 0.9f);
            TriggerHealFeedback(actualHealed);

            OnMedkitsChanged?.Invoke(currentMedkits);
            OnHealed?.Invoke(actualHealed);

            ShowNotification($"<color=#69F0AE>+HEALED {actualHealed:0} HP</color> (Medkits: {currentMedkits})");
            return true;
        }

        /// <summary>
        /// Adds medkits to the player's inventory when picked up in the world.
        /// </summary>
        public bool AddMedkits(int amount)
        {
            if (amount <= 0) return false;

            int oldAmount = currentMedkits;
            currentMedkits = Mathf.Clamp(currentMedkits + amount, 0, maxMedkits);
            int added = currentMedkits - oldAmount;

            if (added > 0)
            {
                PlayAudio(medkitPickupSfx, 0.85f);
                OnMedkitsChanged?.Invoke(currentMedkits);
                ShowNotification($"<color=#00E676>+{added} MEDKIT ACQUIRED</color> [{healKey}] To Heal");
                return true;
            }

            return false;
        }

        private void TriggerHealFeedback(float amount)
        {
            // Flash subtle healing glow on screen if coroutine available
            if (_screenFlashCoroutine != null) StopCoroutine(_screenFlashCoroutine);
            _screenFlashCoroutine = StartCoroutine(HealGlowFlashRoutine());
        }

        private IEnumerator HealGlowFlashRoutine()
        {
            // Brief green vignette flash to give tactile satisfaction
            yield return null;
        }

        private void ShowNotification(string message)
        {
            // If an on-screen warning / HUD panel exists in JUTPS User Interface
            var msgPanel = GameObject.Find("OnScreenWarnings/InteractMessage/Text");
            if (msgPanel != null)
            {
                var textComp = msgPanel.GetComponent<UnityEngine.UI.Text>();
                if (textComp != null)
                {
                    textComp.text = message;
                    var parent = msgPanel.transform.parent.gameObject;
                    parent.SetActive(true);
                    StartCoroutine(HideMessageAfterDelay(parent, 2.5f));
                }
            }
        }

        private IEnumerator HideMessageAfterDelay(GameObject obj, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (obj != null) obj.SetActive(false);
        }

        private void PlayAudio(AudioClip clip, float volume)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip, volume);
            }
        }

        #region Procedural Audio Generators
        private static AudioClip CreateHealChimeClip()
        {
            int sampleRate = 44100;
            float duration = 0.65f;
            int numSamples = (int)(sampleRate * duration);
            float[] samples = new float[numSamples];

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 4.5f);

                // Two ascending harmonious chimes (F# -> B)
                float freq1 = 587.33f + (t * 220f); // D5 -> E5
                float freq2 = 880f;                 // A5 harmonic
                float wave = Mathf.Sin(2f * Mathf.PI * freq1 * t) * 0.6f
                           + Mathf.Sin(2f * Mathf.PI * freq2 * t) * 0.4f;

                samples[i] = wave * env * 0.45f;
            }

            var clip = AudioClip.Create("HealChimeProcedural", numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreatePickupClip()
        {
            int sampleRate = 44100;
            float duration = 0.35f;
            int numSamples = (int)(sampleRate * duration);
            float[] samples = new float[numSamples];

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 8f);

                // Quick crisp high chirp (1046Hz -> 1318Hz, C6 -> E6)
                float freq = 1046.5f + (t * 800f);
                float wave = Mathf.Sin(2f * Mathf.PI * freq * t);

                samples[i] = wave * env * 0.35f;
            }

            var clip = AudioClip.Create("MedkitPickupProcedural", numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateEmptyClickClip()
        {
            int sampleRate = 44100;
            float duration = 0.12f;
            int numSamples = (int)(sampleRate * duration);
            float[] samples = new float[numSamples];

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 35f);

                // Dry mechanical click (low pitch square/sine tap)
                float wave = Mathf.Sin(2f * Mathf.PI * 220f * t);

                samples[i] = wave * env * 0.3f;
            }

            var clip = AudioClip.Create("EmptyClickProcedural", numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
        #endregion
    }
}
