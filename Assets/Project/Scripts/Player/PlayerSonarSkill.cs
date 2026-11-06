using System.Collections;
using System.Collections.Generic;
using JUTPS;
using Railbound.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Railbound.Gameplay
{
    /// <summary>
    /// Advanced Full-Map Sonar Enemy Scanning Skill with See-Through-Walls X-Ray Vision.
    /// Triggered by pressing the 'I' key on the keyboard.
    /// Emits a full-battlefield sonar shockwave, interacts with SimpleSonarShader,
    /// reveals enemy skeletons and body silhouettes through walls, and pings them on the Minimap.
    /// </summary>
    public class PlayerSonarSkill : MonoBehaviour
    {
        [Header("World Scan Skill Settings (INab FX Style)")]
        public KeyCode triggerKey = KeyCode.I;
        public float scanRadius = 140f;   // Limited tactical battlefield sector (140m)
        public float scanDuration = 3.8f; // Multi-second smooth growing wavefront
        public float waveSpeed = 40f;     // Expansion speed
        public float cooldown = 10f;
        public float revealDuration = 14f; // Enemies visible through walls for 14 seconds
        public Color scanWaveColor = new Color(0.0f, 1.8f, 2.8f, 1.0f); // Vibrant Tactical Cyan / Azure Glow

        [Header("Audio")]
        public AudioSource audioSource;
        public AudioClip sonarLaunchSfx;
        public AudioClip enemyAcquiredSfx;

        [Header("Limited Charges")]
        [Min(1)] public int maxCharges = 3;
        [Tooltip("Charges the player starts a life with (clamped to Max Charges).")]
        [Min(0)] public int startingCharges = 3;

        [Header("HUD (Auto-Created)")]
        public Sprite deviceSprite;                 // falls back to Resources/HUD/Sonar_Device
        [System.NonSerialized] public Vector2 hudPosition = new Vector2(28f, 126f); // bottom-left, above the vitals card
        [System.NonSerialized] public float hudSize = 100f;

        private float _lastUsedTime = -100f;
        private int _charges;

        // HUD refs
        private RectTransform _widget;
        private CanvasGroup _widgetGroup;
        private Image _iconFill, _iconGhost, _iconShadow, _flash, _ring;
        private TextMeshProUGUI _countText, _keyText;
        private CanvasGroup _keyCapGroup;
        private RectTransform _iconRoot, _ringRt;

        // HUD animation state (unscaled time, so it keeps animating while paused)
        private float _popStart = -10f, _shakeStart = -10f, _flashStart = -10f, _rippleStart = -10f;
        private bool _wasReady = true;
        private int _shownCharges = -1;
        private static Sprite _ringSprite;

        public int Charges => _charges;
        public int MaxCharges => maxCharges;
        public event System.Action<int> ChargesChanged;
        private Coroutine _scanWaveRoutine;

        public bool IsReady => Time.time >= _lastUsedTime + cooldown;
        public float CooldownRemaining => Mathf.Max(0f, (_lastUsedTime + cooldown) - Time.time);

#if UNITY_EDITOR
        private void Reset()
        {
            if (sonarLaunchSfx == null)
            {
                sonarLaunchSfx = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Project/SoundClips/Game_sounds/sonar_sound.mp3");
            }
        }
#endif

        private void Awake()
        {
            // the player can end up with two copies of this component (scene + setup tool); a second one would
            // fire a second scan and burn a second charge on every key press
            var all = GetComponents<PlayerSonarSkill>();
            if (all.Length > 1 && all[0] != this)
            {
                enabled = false;
                Destroy(this);
            }
        }

        private void Start()
        {
            if (this == null || !enabled) return;
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.playOnAwake = false;
                    audioSource.spatialBlend = 0f; // 2D player audio
                }
            }

            if (sonarLaunchSfx == null)
            {
#if UNITY_EDITOR
                sonarLaunchSfx = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Project/SoundClips/Game_sounds/sonar_sound.mp3");
#endif
                if (sonarLaunchSfx == null)
                {
                    sonarLaunchSfx = GenerateSubBassPingClip();
                }
            }

            if (enemyAcquiredSfx == null)
            {
                enemyAcquiredSfx = GenerateAcquiredChirpClip();
            }

            string savedKey = PlayerPrefs.GetString("Player_Sonar_Key", "");
            if (!string.IsNullOrEmpty(savedKey) && System.Enum.TryParse<KeyCode>(savedKey, true, out var parsed))
            {
                triggerKey = parsed;
            }

            _charges = Mathf.Clamp(startingCharges, 0, Mathf.Max(1, maxCharges));
            CreateOrFindHUD();
        }

        private void Update()
        {
            bool keyPressed = Input.GetKeyDown(triggerKey);

#if ENABLE_INPUT_SYSTEM
            if (!keyPressed && UnityEngine.InputSystem.Keyboard.current != null)
            {
                try
                {
                    var keyControl = UnityEngine.InputSystem.Keyboard.current[triggerKey.ToString().ToLower()] as UnityEngine.InputSystem.Controls.KeyControl;
                    if (keyControl != null && keyControl.wasPressedThisFrame) keyPressed = true;
                }
                catch { }

                if (!keyPressed && triggerKey == KeyCode.I && UnityEngine.InputSystem.Keyboard.current.iKey.wasPressedThisFrame)
                {
                    keyPressed = true;
                }
            }
#endif

            if (keyPressed)
            {
                if (_charges > 0 && IsReady)
                {
                    ActivateSonar();
                }
                else
                {
                    // out of charges, or still recharging: shake the icon (no text spam)
                    _shakeStart = Time.unscaledTime;
                }
            }

            UpdateUI();
        }

        /// <summary>Adds charges (e.g. from a pickup); clamped to Max Charges.</summary>
        public void AddCharges(int amount)
        {
            int before = _charges;
            _charges = Mathf.Clamp(_charges + amount, 0, Mathf.Max(1, maxCharges));
            if (_charges != before)
            {
                _popStart = Time.unscaledTime;
                ChargesChanged?.Invoke(_charges);
            }
        }

        public void ActivateSonar()
        {
            _lastUsedTime = Time.time;
            _charges = Mathf.Max(0, _charges - 1);
            _popStart = _rippleStart = Time.unscaledTime;
            ChargesChanged?.Invoke(_charges);
            Vector3 origin = transform.position;

            // 1. Initial Sonar Activation Sound
            if (audioSource != null && sonarLaunchSfx != null)
            {
                audioSource.PlayOneShot(sonarLaunchSfx, 0.95f);
            }

            // 2. Full-Scene Volumetric World Scan Wavefront (INab FX Style)
            SonarPulseWaveEffect.Create(origin, scanRadius, scanDuration, scanWaveColor);

            // 3. SimpleSonarShader trigger (passes world pos & range into materials)
            SimpleSonarShader_Object.TriggerSonar(origin, scanRadius);
            if (SimpleSonarShader_Parent.Instance != null)
            {
                SimpleSonarShader_Parent.Instance.StartSonarRing(origin, scanRadius);
            }

            // 4. Begin Expanding Wavefront Scan Coroutine
            if (_scanWaveRoutine != null) StopCoroutine(_scanWaveRoutine);
            _scanWaveRoutine = StartCoroutine(FullMapScanRoutine(origin));
        }

        private IEnumerator FullMapScanRoutine(Vector3 origin)
        {
            // Collect all enemies in the level
            GameObject[] enemyGOs = GameObject.FindGameObjectsWithTag("Enemy");
            var pendingEnemies = new List<EnemyScanEntry>();

            foreach (var go in enemyGOs)
            {
                if (go == null || !go.activeInHierarchy) continue;
                JUHealth health = go.GetComponent<JUHealth>();
                if (health != null && health.IsDead) continue;

                float dist = Vector3.Distance(origin, go.transform.position);
                if (dist <= scanRadius)
                {
                    pendingEnemies.Add(new EnemyScanEntry
                    {
                        GameObject = go,
                        Distance = dist,
                        Delay = (dist / Mathf.Max(scanRadius, 1f)) * scanDuration
                    });
                }
            }

            // Sort enemies by distance so wavefront reveals them in order
            pendingEnemies.Sort((a, b) => a.Distance.CompareTo(b.Distance));

            float startTime = Time.time;

            for (int i = 0; i < pendingEnemies.Count; i++)
            {
                var entry = pendingEnemies[i];
                float elapsed = Time.time - startTime;
                if (elapsed < entry.Delay)
                {
                    yield return new WaitForSeconds(entry.Delay - elapsed);
                }

                if (entry.GameObject != null && entry.GameObject.activeInHierarchy)
                {
                    JUHealth health = entry.GameObject.GetComponent<JUHealth>();
                    if (health != null && health.IsDead) continue;

                    // A) Through-Wall X-Ray Silhouette (Visible through walls & obstacles)
                    EnemyThroughWallSilhouette.RevealEnemy(entry.GameObject, revealDuration);

                    // B) 3D Tactical Ping Marker (Visible through walls)
                    SonarEnemyPingMarker.Attach(entry.GameObject, revealDuration);

                    // C) Top-Left Minimap Radar Pulse
                    if (Map1MiniMapController.Instance != null)
                    {
                        Map1MiniMapController.Instance.PingEnemy(entry.GameObject.transform, revealDuration);
                    }

                    // Tactical Target Acquisition Chirp
                    if (audioSource != null && enemyAcquiredSfx != null)
                    {
                        audioSource.PlayOneShot(enemyAcquiredSfx, 0.65f);
                    }
                }
            }

            _scanWaveRoutine = null;
        }

        private struct EnemyScanEntry
        {
            public GameObject GameObject;
            public float Distance;
            public float Delay;
        }

        // ------------------------------------------------------------------ HUD
        private static readonly Color Ink = Color.white;
        private static readonly Color Warn = new Color(1f, 0.33f, 0.30f, 1f);

        private void UpdateUI()
        {
            if (_widget == null) return;

            float now = Time.unscaledTime;
            bool hasCharges = _charges > 0;
            bool ready = IsReady;

            // recharge fill: bottom-up while cooling down, empty when out of charges
            float fill = !hasCharges ? 0f : (ready ? 1f : 1f - CooldownRemaining / Mathf.Max(0.01f, cooldown));
            _iconFill.fillAmount = fill;
            Color tint = hasCharges ? Ink : Warn;
            if (_keyCapGroup != null) _keyCapGroup.alpha = hasCharges ? (ready ? 1f : 0.6f) : 0.35f;
            _iconGhost.color = new Color(tint.r, tint.g, tint.b, hasCharges ? 0.30f : 0.45f);

            // count label
            if (_shownCharges != _charges)
            {
                _shownCharges = _charges;
                _countText.text = "X" + _charges;
                _countText.color = tint;
            }

            // flash when the device becomes ready again
            if (hasCharges && ready && !_wasReady) _flashStart = now;
            _wasReady = ready;

            // pop (scale) on use / pickup
            float pop = Mathf.Clamp01((now - _popStart) / 0.32f);
            float popScale = 1f + 0.22f * Mathf.Sin(pop * Mathf.PI) * (1f - pop);
            // shake when the key is pressed but the device can't fire
            float sh = Mathf.Clamp01((now - _shakeStart) / 0.35f);
            float shakeX = Mathf.Sin(sh * Mathf.PI * 7f) * 7f * (1f - sh);
            _iconRoot.localScale = Vector3.one * popScale;
            _iconRoot.anchoredPosition = new Vector2(shakeX, 0f);
            // red blink while shaking
            float blink = (sh < 1f) ? Mathf.Sin(sh * Mathf.PI) : 0f;
            _iconFill.color = Color.Lerp(tint, Warn, blink);

            // white flash overlay (ready / activation)
            float fl = Mathf.Clamp01((now - _flashStart) / 0.55f);
            float flashA = (1f - fl) * (1f - fl);
            _flash.color = new Color(1f, 1f, 1f, flashA * 0.9f);
            _flash.rectTransform.localScale = Vector3.one * (1f + fl * 0.18f);

            // expanding white sonar ripple on activation
            float rp = Mathf.Clamp01((now - _rippleStart) / 0.9f);
            float ease = 1f - (1f - rp) * (1f - rp);
            _ringRt.localScale = Vector3.one * Mathf.Lerp(0.7f, 2.1f, ease);
            _ring.color = new Color(1f, 1f, 1f, (rp > 0f && rp < 1f) ? (1f - rp) * 0.75f : 0f);
        }

        private static string KeyLabel(KeyCode k)
        {
            string n = k.ToString();
            if (n.StartsWith("Alpha")) return n.Substring(5);
            if (n.StartsWith("Keypad")) return n.Substring(6);
            switch (k)
            {
                case KeyCode.LeftShift: case KeyCode.RightShift: return "SHFT";
                case KeyCode.LeftControl: case KeyCode.RightControl: return "CTRL";
                case KeyCode.LeftAlt: case KeyCode.RightAlt: return "ALT";
                case KeyCode.Space: return "SPC";
                case KeyCode.Tab: return "TAB";
                case KeyCode.Return: return "ENT";
                case KeyCode.Backspace: return "BKSP";
            }
            return n.ToUpper();
        }

        private static Sprite GetRingSprite()
        {
            if (_ringSprite != null) return _ringSprite;
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.name = "SonarHudRing";
            var px = new Color32[size * size];
            float c = (size - 1) * 0.5f, r = size * 0.46f, thick = 2.2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Abs(Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) - r);
                    byte a = (byte)(Mathf.Clamp01(1f - d / thick) * 255f);
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            _ringSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _ringSprite;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            return rt;
        }

        private static Image NewIcon(string name, Transform parent, Sprite sprite, Color color)
        {
            var rt = NewRect(name, parent);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            img.preserveAspect = true;
            return img;
        }

        private static TextMeshProUGUI NewLabel(string name, Transform parent, float size, TextAlignmentOptions align)
        {
            var rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.fontSize = size;
            t.fontStyle = FontStyles.Bold;
            t.alignment = align;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            t.color = Color.white;
            t.outlineWidth = 0.22f;
            t.outlineColor = new Color32(0, 0, 0, 200);
            return t;
        }

        private void CreateOrFindHUD()
        {
            if (_widget != null) return;
            if (deviceSprite == null) deviceSprite = Resources.Load<Sprite>("HUD/Sonar_Device");

            GameObject uiCanvas = GameObject.Find("User Interface/UI");
            if (uiCanvas == null) uiCanvas = GameObject.Find("User Interface");
            if (uiCanvas == null) return;

            // drop the legacy text/bar widget if an older build left it behind
            var legacy = uiCanvas.transform.Find("SonarSkillWidget");
            if (legacy != null) Destroy(legacy.gameObject);
            var existing = uiCanvas.transform.Find("SonarChargeWidget");
            if (existing != null) Destroy(existing.gameObject);

            _widget = NewRect("SonarChargeWidget", uiCanvas.transform);
            _widget.anchorMin = _widget.anchorMax = Vector2.zero;
            _widget.pivot = Vector2.zero;
            _widget.anchoredPosition = hudPosition;
            _widget.sizeDelta = new Vector2(hudSize, hudSize);
            _widgetGroup = _widget.gameObject.AddComponent<CanvasGroup>();
            _widgetGroup.blocksRaycasts = false;
            _widgetGroup.interactable = false;

            // ripple ring (behind everything)
            _ringRt = NewRect("Ripple", _widget);
            _ringRt.sizeDelta = new Vector2(hudSize, hudSize);
            _ring = _ringRt.gameObject.AddComponent<Image>();
            _ring.sprite = GetRingSprite();
            _ring.raycastTarget = false;
            _ring.color = new Color(1f, 1f, 1f, 0f);

            // icon stack (animated together)
            _iconRoot = NewRect("Icon", _widget);
            _iconRoot.anchorMin = Vector2.zero;
            _iconRoot.anchorMax = Vector2.one;
            _iconRoot.sizeDelta = Vector2.zero;

            _iconShadow = NewIcon("Shadow", _iconRoot, deviceSprite, new Color(0f, 0f, 0f, 0.55f));
            _iconShadow.rectTransform.anchoredPosition = new Vector2(2.5f, -2.5f);
            _iconGhost = NewIcon("Ghost", _iconRoot, deviceSprite, new Color(1f, 1f, 1f, 0.30f));
            _iconFill = NewIcon("Fill", _iconRoot, deviceSprite, Ink);
            _iconFill.type = Image.Type.Filled;
            _iconFill.fillMethod = Image.FillMethod.Vertical;
            _iconFill.fillOrigin = (int)Image.OriginVertical.Bottom;
            _iconFill.fillAmount = 1f;
            _flash = NewIcon("Flash", _iconRoot, deviceSprite, new Color(1f, 1f, 1f, 0f));

            // "X3" charge count, bottom-right of the icon
            _countText = NewLabel("Count", _widget, hudSize * 0.30f, TextAlignmentOptions.BottomRight);
            var cr = _countText.rectTransform;
            cr.anchorMin = cr.anchorMax = new Vector2(1f, 0f);
            cr.pivot = new Vector2(1f, 0f);
            cr.sizeDelta = new Vector2(hudSize * 0.7f, hudSize * 0.38f);
            cr.anchoredPosition = new Vector2(hudSize * 0.10f, -hudSize * 0.04f);

            // key hint: Reach-style hotkey plate (dark glass square + corner brackets + light letter), top-right of the icon
            float cap = hudSize * 0.36f;
            var capRt = NewRect("KeyCap", _widget);
            capRt.anchorMin = capRt.anchorMax = new Vector2(1f, 1f);
            capRt.pivot = new Vector2(1f, 1f);
            capRt.sizeDelta = new Vector2(cap, cap);
            capRt.anchoredPosition = new Vector2(hudSize * 0.04f, hudSize * 0.03f);
            _keyCapGroup = capRt.gameObject.AddComponent<CanvasGroup>();
            _keyCapGroup.blocksRaycasts = false;
            _keyCapGroup.interactable = false;

            var capFill = NewIcon("Fill", capRt, null, new Color(0.20f, 0.20f, 0.21f, 0.80f));
            capFill.preserveAspect = false;
            var capFrame = NewIcon("Frame", capRt, Resources.Load<Sprite>("HUD/Hud_CornerFrame"), new Color(1f, 1f, 1f, 0.95f));
            capFrame.preserveAspect = false;
            capFrame.type = Image.Type.Sliced;
            capFrame.pixelsPerUnitMultiplier = 2.4f;

            _keyText = NewLabel("Key", capRt, cap * 0.70f, TextAlignmentOptions.Center);
            var kr = _keyText.rectTransform;
            kr.anchorMin = Vector2.zero; kr.anchorMax = Vector2.one;
            kr.sizeDelta = Vector2.zero; kr.anchoredPosition = Vector2.zero;
            _keyText.outlineWidth = 0f;
            _keyText.enableAutoSizing = true;
            _keyText.fontSizeMin = cap * 0.2f;
            _keyText.fontSizeMax = cap * 0.70f;
            _keyText.margin = new Vector4(3f, 0f, 3f, 0f);
            _keyText.color = Color.white;
            _keyText.text = KeyLabel(triggerKey);

            _shownCharges = -1;
            _wasReady = true;
            UpdateUI();
        }

        /// <summary>Called when the player rebinds the sonar key.</summary>
        public void RefreshKeyHint()
        {
            if (_keyText != null) _keyText.text = KeyLabel(triggerKey);
        }

        private void OnDestroy()
        {
            if (_widget != null) Destroy(_widget.gameObject);
        }
        private AudioClip GenerateSubBassPingClip()
        {
            int sampleRate = 44100;
            float duration = 0.95f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            // Deep tactical sonar pulse: 900 Hz dropping to 220 Hz with heavy sub-bass tail
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float progress = t / duration;
                float freq = Mathf.Lerp(940f, 240f, Mathf.Pow(progress, 0.7f));
                float envelope = Mathf.Exp(-t * 4.2f) * Mathf.Sin(Mathf.Clamp01(t * 90f) * Mathf.PI * 0.5f);
                float wave = Mathf.Sin(2f * Mathf.PI * freq * t);
                float sub = Mathf.Sin(2f * Mathf.PI * (freq * 0.5f) * t) * 0.4f;
                samples[i] = (wave + sub) * envelope * 0.55f;
            }

            AudioClip clip = AudioClip.Create("SubBassSonarPulse", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip GenerateAcquiredChirpClip()
        {
            int sampleRate = 44100;
            float duration = 0.22f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            // Tactical electronic target-lock double-pip
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float freq = t < 0.1f ? 1650f : 2100f;
                float env = Mathf.Sin(t / duration * Mathf.PI);
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.28f;
            }

            AudioClip clip = AudioClip.Create("TargetAcquiredChirp", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
