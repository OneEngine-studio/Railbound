using JUTPS;
using Railbound.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UI;

namespace Railbound.Gameplay
{
    /// <summary>
    /// Low-health feedback. As health drops the player's senses degrade:
    ///  - visuals: a dark-red vignette closes in and pulses with the heartbeat, colour drains from the world,
    ///    and below the critical threshold a LOW HEALTH banner appears with a medkit prompt;
    ///  - audio: a heartbeat that speeds up (one beat per pulse, so the vignette is in sync), ragged breathing,
    ///    the world audio muffles through a low-pass filter, a faint tinnitus ring and a sub-bass sting when
    ///    the player first goes critical.
    /// All clips are loaded from Resources/SoundClips/LowHealth, so any of them can be swapped for real recordings
    /// (same file names) with no code change; procedural fallbacks are used if a file is missing.
    /// </summary>
    public class PlayerSurvivalTerrorController : MonoBehaviour
    {
        [Header("Health & Threat Tuning")]
        public float maxSurvivalHealth = 100f; // Player health pool
        public float dangerThreshold = 0.55f;  // Effects start below 55%
        public float criticalThreshold = 0.30f; // Full effect + banner below 30%

        [Header("Audio Clips (Resources/SoundClips/LowHealth)")]
        public AudioClip heartbeatClip;
        public AudioClip heavyBreathingClip;
        public AudioClip tinnitusClip;
        public AudioClip criticalStingClip;
        [Tooltip("Looping heartbeat / bass-pulse bed (Resources/Audio). If it is missing the short lub-dub clip is played beat by beat instead.")]
        public AudioClip heartbeatBedClip;
        [Tooltip("The bed is looped from 0 to this time (the file fades out after it), in seconds.")]
        public float heartbeatLoopEnd = 31f;
        [Tooltip("Extra body sounds on top of the heartbeat: breathing hiss, tinnitus ring and the critical sting. Off by default - only the heartbeat is heard.")]
        public bool bodySounds = false;
        [Range(0f, 1f)] public float heartbeatVolume = 0.95f;
        [Range(0f, 1f)] public float breathingVolume = 0.7f;
        [Range(0f, 1f)] public float tinnitusVolume = 0.14f;
        [Tooltip("Lowest low-pass cutoff (Hz) at full effect; 22000 = no muffling.")]
        public float muffledCutoff = 1100f;
        [Tooltip("Slowest / fastest heart rate (beats per minute).")]
        public Vector2 heartRate = new Vector2(68f, 150f);

        // legacy references kept so existing scene serialisation keeps loading
        [HideInInspector] public AudioSource heartbeatAudioSource;
        [HideInInspector] public AudioSource breathingAudioSource;
        [HideInInspector] public Image bloodVignetteImage;
        [HideInInspector] public TextMeshProUGUI criticalWarningText;

        private static readonly Color Blood = new Color(0.55f, 0.02f, 0.05f, 1f);

        private JUHealth health;
        private PlayerMedkitSystem medkit;
        private AudioSource beatSource, bedSource, breathSource, ringSource, stingSource;
        private AudioLowPassFilter lowPass;
        private AudioListener listener;
        private Volume gradeVolume;
        private ColorAdjustments grade;

        private Canvas canvas;
        private CanvasGroup rootGroup, bannerGroup;
        private Image vignette, lineTop, lineBottom;
        private TextMeshProUGUI headline, hint;
        private TMP_FontAsset fontBold, fontSemi;

        private float intensity;      // smoothed 0..1
        private float muffle;         // smoothed 0..1 low-pass amount
        private float bedLevel, bedMax = 0.05f;
        private readonly float[] bedSamples = new float[256];
        private float beatTimer, beatPulse;
        private bool wasCritical;
        private float nextListenerScan, muffleTarget;

        // ---------------- lifecycle ----------------
        private void Start()
        {
            // body sounds and the screen effects belong to the local player only: never run them for remote players
            var netObj = GetComponent<Unity.Netcode.NetworkObject>();
            if (netObj != null && Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening && !netObj.IsOwner)
            {
                enabled = false;
                return;
            }
            health = GetComponent<JUHealth>();
            medkit = GetComponent<PlayerMedkitSystem>();
            if (medkit == null) medkit = PlayerMedkitSystem.Instance;
            if (health != null)
            {
                // hardcore low max health
                health.MaxHealth = maxSurvivalHealth;
                health.Health = Mathf.Min(health.Health, maxSurvivalHealth);
            }
            LoadClips();
            BuildAudio();
            BuildGrade();
            BuildUI();
            RetireLegacyWidgets();
        }

        private void OnDisable() { RestoreListener(); }

        private void OnDestroy()
        {
            RestoreListener();
            if (gradeVolume != null) Destroy(gradeVolume.gameObject);
            if (canvas != null) Destroy(canvas.gameObject);
        }

        private void Update()
        {
            if (health == null) return;
            bool dead = health.IsDead || DeathScreenUI.IsShowing;

            if (health.MaxHealth > maxSurvivalHealth)
            {
                health.MaxHealth = maxSurvivalHealth;
                if (health.Health > maxSurvivalHealth) health.Health = maxSurvivalHealth;
            }

            float hp = Mathf.Clamp01(health.Health / maxSurvivalHealth);
            float target = dead ? 0f : Mathf.Clamp01((dangerThreshold - hp) / Mathf.Max(0.01f, dangerThreshold - criticalThreshold));
            // rise quickly when hurt, recover slowly
            float speed = target > intensity ? 2.4f : 0.7f;
            intensity = Mathf.MoveTowards(intensity, target, speed * Time.unscaledDeltaTime);

            bool critical = !dead && hp <= criticalThreshold;
            if (critical && !wasCritical && stingSource != null && criticalStingClip != null) stingSource.PlayOneShot(criticalStingClip, 0.9f);
            wasCritical = critical;

            muffleTarget = dead ? 1f : Mathf.SmoothStep(0f, 1f, intensity);
            muffle = Mathf.MoveTowards(muffle, muffleTarget, (dead ? 1.6f : 1.2f) * Time.unscaledDeltaTime);
            UpdateHeartbeat(dead);
            UpdateAudioMix(dead);
            UpdateVisuals(critical, dead);
        }

        // ---------------- heartbeat / audio ----------------
        private void UpdateHeartbeat(bool dead)
        {
            if (bedSource != null && heartbeatBedClip != null) { UpdateHeartbeatBed(); return; }

            beatPulse = Mathf.MoveTowards(beatPulse, 0f, Time.deltaTime * 3.2f);
            if (dead || intensity < 0.02f) { beatTimer = 0f; return; }

            float bpm = Mathf.Lerp(heartRate.x, heartRate.y, intensity);
            float period = 60f / bpm;
            beatTimer -= Time.deltaTime;
            if (beatTimer <= 0f)
            {
                beatTimer += period;
                beatPulse = 1f;
                if (beatSource != null && heartbeatClip != null)
                {
                    // squeeze the lub-dub into the shorter period when the heart races
                    beatSource.pitch = Mathf.Clamp(heartbeatClip.length / (period * 0.95f), 0.95f, 1.7f);
                    beatSource.PlayOneShot(heartbeatClip, heartbeatVolume * Mathf.Lerp(0.35f, 1f, intensity));
                }
            }
        }

        // the heartbeat bed loops continuously; its volume follows the intensity and the vignette pulse follows its loudness
        private void UpdateHeartbeatBed()
        {
            float vol = heartbeatVolume * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.02f, 0.7f, intensity));
            // short dip around the loop point so the jump back to the start does not click
            float loopFade = 1f;
            if (heartbeatLoopEnd > 0.5f && bedSource.isPlaying)
            {
                float t = bedSource.time;
                if (t >= heartbeatLoopEnd) { bedSource.time = 0f; t = 0f; }
                float toEnd = heartbeatLoopEnd - t;
                if (toEnd < 0.12f) loopFade = Mathf.Clamp01(toEnd / 0.12f);
                else if (t < 0.12f) loopFade = Mathf.Clamp01(t / 0.12f);
            }
            bedSource.volume = vol * loopFade;
            bedSource.pitch = Mathf.Lerp(0.98f, 1.08f, intensity);
            if (vol > 0.001f && !bedSource.isPlaying) bedSource.Play();
            if (vol <= 0.001f && bedSource.isPlaying) bedSource.Pause();

            // loudness of the track -> pulse (normalised against a slowly decaying peak)
            float target = 0f;
            if (bedSource.isPlaying)
            {
                bedSource.GetOutputData(bedSamples, 0);
                float sum = 0f; for (int i = 0; i < bedSamples.Length; i++) sum += bedSamples[i] * bedSamples[i];
                bedLevel = Mathf.Sqrt(sum / bedSamples.Length);
                bedMax = Mathf.Max(bedMax * (1f - 0.1f * Time.unscaledDeltaTime), bedLevel, 0.01f);
                target = Mathf.Clamp01(bedLevel / bedMax);
            }
            float rise = 14f, fall = 4.5f;
            beatPulse = target > beatPulse ? Mathf.MoveTowards(beatPulse, target, rise * Time.unscaledDeltaTime) : Mathf.MoveTowards(beatPulse, target, fall * Time.unscaledDeltaTime);
        }

        private void UpdateAudioMix(bool dead)
        {
            if (breathSource != null)
            {
                breathSource.volume = breathingVolume * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 1f, intensity));
                breathSource.pitch = Mathf.Lerp(0.92f, 1.28f, intensity);
            }
            if (ringSource != null)
                ringSource.volume = tinnitusVolume * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 1f, intensity));

            // muffle the whole world as the player fades
            if (lowPass == null && Time.unscaledTime >= nextListenerScan)
            {
                nextListenerScan = Time.unscaledTime + 1f;
                listener = FindFirstObjectByType<AudioListener>();
                if (listener != null)
                {
                    lowPass = listener.GetComponent<AudioLowPassFilter>();
                    if (lowPass == null) lowPass = listener.gameObject.AddComponent<AudioLowPassFilter>();
                    lowPass.lowpassResonanceQ = 1f;
                }
            }
            if (lowPass != null && !DeathAudio.IsClosing)   // after a respawn the death sound opens the filter back up itself
            {
                float k = muffle;
                // logarithmic so the sweep sounds even; dying muffles the world further than low health does
                float low = Mathf.Max(200f, dead ? muffledCutoff * 0.6f : muffledCutoff);
                float cutoff = Mathf.Exp(Mathf.Lerp(Mathf.Log(22000f), Mathf.Log(low), k));
                lowPass.cutoffFrequency = cutoff;
                lowPass.enabled = k > 0.005f;
            }
        }

        private void RestoreListener()
        {
            if (lowPass != null) { lowPass.cutoffFrequency = 22000f; lowPass.enabled = false; }
        }

        // ---------------- visuals ----------------
        private void UpdateVisuals(bool critical, bool dead)
        {
            if (rootGroup == null) return;
            rootGroup.alpha = Mathf.MoveTowards(rootGroup.alpha, dead ? 0f : 1f, Time.unscaledDeltaTime * (dead ? 3f : 6f));

            float pulse = beatPulse * beatPulse;
            // vignette: always breathing a little with the heartbeat, harder near death
            float a = intensity * (0.30f + 0.55f * pulse) + (critical ? 0.12f : 0f);
            vignette.color = new Color(Blood.r, Blood.g, Blood.b, Mathf.Clamp01(a));
            vignette.rectTransform.localScale = Vector3.one * (1f + 0.025f * pulse * intensity);

            if (gradeVolume != null) gradeVolume.weight = Mathf.SmoothStep(0f, 1f, intensity);

            // LOW HEALTH banner
            float bannerTarget = critical ? 1f : 0f;
            bannerGroup.alpha = Mathf.MoveTowards(bannerGroup.alpha, bannerTarget, Time.unscaledDeltaTime * (critical ? 4f : 2f));
            if (bannerGroup.alpha > 0.01f)
            {
                Color accent = Color.Lerp(Color.white, new Color(1f, 0.30f, 0.27f), 0.35f + 0.65f * pulse);
                headline.color = accent;
                lineTop.color = new Color(accent.r, accent.g, accent.b, 0.35f + 0.5f * pulse);
                lineBottom.color = lineTop.color;
                headline.characterSpacing = 14f + 3f * pulse;

                int kits = medkit != null ? medkit.CurrentMedkits : 0;
                if (medkit == null) hint.text = "";
                else if (kits > 0) hint.text = "PRESS  <b><color=#FFFFFF>" + KeyName(medkit.healKey) + "</color></b>  TO USE A MEDKIT  <color=#FFFFFF66>•</color>  " + kits + " LEFT";
                else hint.text = "<color=#FF6B61>NO MEDKITS</color>  •  FIND MEDICAL SUPPLIES";
            }
        }

        private static string KeyName(KeyCode k)
        {
            string s = k.ToString();
            if (s.StartsWith("Alpha")) s = s.Substring(5);
            return s.ToUpperInvariant();
        }

        // ---------------- build ----------------
        private void LoadClips()
        {
            if (heartbeatBedClip == null) heartbeatBedClip = Resources.Load<AudioClip>("Audio/tomas_herudek-heartbeat-and-fast-bass-pulses-529667");
            if (heartbeatClip == null) heartbeatClip = Resources.Load<AudioClip>("SoundClips/LowHealth/lowhealth_heartbeat");
            if (heavyBreathingClip == null) heavyBreathingClip = Resources.Load<AudioClip>("SoundClips/LowHealth/lowhealth_breath");
            if (tinnitusClip == null) tinnitusClip = Resources.Load<AudioClip>("SoundClips/LowHealth/lowhealth_tinnitus");
            if (criticalStingClip == null) criticalStingClip = Resources.Load<AudioClip>("SoundClips/LowHealth/lowhealth_enter");
            if (heartbeatClip == null) heartbeatClip = MakeHeartbeat();
            if (heavyBreathingClip == null) heavyBreathingClip = MakeBreath();
        }

        private AudioSource NewSource(string name, bool loop, AudioClip clip)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 1.6f, 0f);   // at the head
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false; s.loop = loop; s.volume = 0f; s.clip = clip;
            // 3D, but full volume anywhere near the player and spread wide so it never pings left/right as the camera orbits
            s.spatialBlend = 1f; s.rolloffMode = AudioRolloffMode.Linear; s.minDistance = 10f; s.maxDistance = 45f; s.spread = 150f; s.dopplerLevel = 0f;
            s.bypassEffects = true; s.bypassListenerEffects = true;   // keep the player's own body sounds clear while the world muffles
            if (loop && clip != null) s.Play();
            return s;
        }

        private void BuildAudio()
        {
            beatSource = NewSource("Heartbeat", false, null); beatSource.volume = 1f;
            bedSource = NewSource("Heartbeat Bed", true, heartbeatBedClip);
            if (bedSource.isPlaying) bedSource.Pause();
            if (!bodySounds) return;   // heartbeat only
            breathSource = NewSource("Breathing", true, heavyBreathingClip);
            ringSource = NewSource("Tinnitus", true, tinnitusClip);
            stingSource = NewSource("Critical Sting", false, null); stingSource.volume = 1f;
        }

        private void BuildGrade()
        {
            var go = new GameObject("[Low Health] Grade"); go.transform.SetParent(transform, false);
            gradeVolume = go.AddComponent<Volume>(); gradeVolume.isGlobal = true; gradeVolume.priority = 500f; gradeVolume.weight = 0f;
            gradeVolume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
            grade = gradeVolume.profile.Add<ColorAdjustments>(true);
            grade.saturation.Override(-55f);
            grade.contrast.Override(10f);
            grade.postExposure.Override(-0.35f);
        }

        private void BuildUI()
        {
            fontBold = FindFont("Rajdhani-Bold") ?? TMP_Settings.defaultFontAsset;
            fontSemi = FindFont("Rajdhani-SemiBold") ?? fontBold;

            var cgo = new GameObject("[HUD] Low Health", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            canvas = cgo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 3;
            var sc = cgo.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
            rootGroup = cgo.GetComponent<CanvasGroup>(); rootGroup.blocksRaycasts = false; rootGroup.interactable = false;
            var root = (RectTransform)cgo.transform;

            // radial vignette: transparent centre, blood at the edges
            vignette = NewImage("Vignette", root, MakeVignette(), new Color(Blood.r, Blood.g, Blood.b, 0f));
            vignette.rectTransform.anchorMin = Vector2.zero; vignette.rectTransform.anchorMax = Vector2.one; vignette.rectTransform.offsetMin = vignette.rectTransform.offsetMax = Vector2.zero;

            // banner
            var banner = NewRect("Banner", root, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 300), new Vector2(900, 110));
            bannerGroup = banner.gameObject.AddComponent<CanvasGroup>(); bannerGroup.alpha = 0f;
            var px = MakePixel();
            lineTop = NewImage("Line Top", banner, px, new Color(1, 1, 1, 0.5f)); Anchor(lineTop.rectTransform, new Vector2(0.5f, 1), new Vector2(380, 2), new Vector2(0, 0));
            headline = NewText("Headline", banner, "LOW HEALTH", 34, fontBold, Color.white, 14f, TextAlignmentOptions.Center);
            Anchor(headline.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(900, 48), new Vector2(0, 8));
            lineBottom = NewImage("Line Bottom", banner, px, new Color(1, 1, 1, 0.5f)); Anchor(lineBottom.rectTransform, new Vector2(0.5f, 0), new Vector2(380, 2), new Vector2(0, 40));
            hint = NewText("Hint", banner, "", 17, fontSemi, new Color(1, 1, 1, 0.7f), 4f, TextAlignmentOptions.Center); hint.richText = true;
            Anchor(hint.rectTransform, new Vector2(0.5f, 0), new Vector2(900, 28), new Vector2(0, 8));
        }

        private void RetireLegacyWidgets()
        {
            // earlier versions created these under the JUTPS canvas; the new overlay replaces them
            var ui = GameObject.Find("User Interface/UI");
            if (ui == null) return;
            var v = ui.transform.Find("TerrorBloodVignette"); if (v != null) v.gameObject.SetActive(false);
            var w = ui.transform.Find("CriticalThreatWarning"); if (w != null) w.gameObject.SetActive(false);
        }

        // ---------------- ui helpers ----------------
        private static TMP_FontAsset FindFont(string namePart)
        {
            foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>()) if (f != null && f.name.Contains(namePart)) return f;
            return null;
        }

        private static Sprite MakePixel()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var cols = new Color32[16]; for (int i = 0; i < cols.Length; i++) cols[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(cols); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite MakeVignette()
        {
            const int n = 128; var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var cols = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx * 0.75f + dy * dy);
                    float a = Mathf.SmoothStep(0.35f, 1.15f, d);
                    cols[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            tex.SetPixels32(cols); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }

        private static RectTransform NewRect(string name, Transform parent, Vector2 amin, Vector2 amax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform; r.anchorMin = amin; r.anchorMax = amax; r.pivot = pivot; r.anchoredPosition = pos; r.sizeDelta = size;
            return r;
        }

        private static Image NewImage(string name, Transform parent, Sprite s, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var i = go.GetComponent<Image>(); i.sprite = s; i.color = c; i.raycastTarget = false;
            return i;
        }

        private static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, TMP_FontAsset font, Color c, float spacing, TextAlignmentOptions al)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>(); t.font = font; t.text = text; t.fontSize = size; t.color = c; t.characterSpacing = spacing; t.alignment = al;
            t.raycastTarget = false; t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        // anchor point on the banner (x centred, y = 0 bottom / 0.5 mid / 1 top), size and offset
        private static void Anchor(RectTransform r, Vector2 anchor, Vector2 size, Vector2 offset)
        {
            r.anchorMin = r.anchorMax = anchor; r.pivot = new Vector2(0.5f, anchor.y == 1f ? 1f : anchor.y == 0f ? 0f : 0.5f);
            r.sizeDelta = size; r.anchoredPosition = offset;
        }

        // ---------------- procedural fallbacks (only used if the WAV files are missing) ----------------
        private static AudioClip MakeHeartbeat()
        {
            const int sr = 44100; int n = (int)(sr * 0.62f); var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr, s = 0f;
                if (t >= 0.02f && t <= 0.32f) { float l = t - 0.02f; s += Mathf.Sin(2f * Mathf.PI * 55f * l) * Mathf.Exp(-l * 16f); }
                if (t >= 0.27f && t <= 0.53f) { float l = t - 0.27f; s += Mathf.Sin(2f * Mathf.PI * 50f * l) * Mathf.Exp(-l * 18f) * 0.6f; }
                d[i] = Mathf.Clamp(s, -1f, 1f);
            }
            var c = AudioClip.Create("Heartbeat", n, 1, sr, false); c.SetData(d, 0); return c;
        }

        private static AudioClip MakeBreath()
        {
            const int sr = 44100; int n = (int)(sr * 2.3f); var d = new float[n]; var rnd = new System.Random(42);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr;
                float env = t < 1f ? Mathf.Sin(t / 1f * Mathf.PI) * 0.3f : Mathf.Sin((t - 1f) / 1.3f * Mathf.PI) * 0.4f;
                d[i] = (float)(rnd.NextDouble() * 2.0 - 1.0) * env;
            }
            var c = AudioClip.Create("Breath", n, 1, sr, false); c.SetData(d, 0); return c;
        }
    }
}
