using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// The OneEngine Studio logo intro, right after Unity's splash screen and before the first menu (about 4.5 s, any key skips it):
    /// a line of light draws across the dark screen, the mark builds out of it (the 1/L shape rises from its base, the bar drops in,
    /// the E slides in) on a deep impact with a soft flash, STUDIO rises in letter by letter, one gloss passes over the logo, then
    /// it fades and the menu is revealed. The sound is generated here (drone, whoosh, impact, shimmer); the menu's own audio waits.
    /// Plays once per launch; never on a server or in a test run (<c>-introtest DIR</c> forces it and saves frames).
    /// </summary>
    public class StudioIntro : MonoBehaviour
    {
        public static bool Finished { get; private set; }
        private static bool started;

        // test runs and servers start straight away
        private static readonly string[] SkipArgs =
        {
            "-nointro", "-server", "-dedicated", "-client", "-host", "-maptest", "-aimtest", "-bottest", "-spectest", "-invtest", "-hittest",
            "-netshot", "-leaveflow", "-partyflow", "-socialshot", "-autoplay", "-rltrain",
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { started = false; Finished = false; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            if (started || Application.isBatchMode) { Finished = true; return; }
            string[] a = System.Environment.GetCommandLineArgs();
            string testDir = null;
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == "-introtest") testDir = a[i + 1];
            if (testDir == null)
                foreach (var s in a)
                    foreach (var skip in SkipArgs)
                        if (string.Equals(s, skip, System.StringComparison.OrdinalIgnoreCase)) { Finished = true; return; }
            started = true;
            var go = new GameObject("[Intro] OneEngine Studio");
            DontDestroyOnLoad(go);
            go.AddComponent<StudioIntro>().testDir = testDir;
        }

        // ---------------- timeline (seconds after Unity's splash) ----------------
        private const float LineIn = 0.35f, LineFull = 0.85f;
        private const float MarkStart = 0.8f, Impact = 1.25f;
        private const float LettersStart = 1.38f, LetterStep = 0.07f, LetterTime = 0.48f;
        private const float GlossStart = 2.15f, GlossTime = 0.7f;
        private const float FadeOut = 3.7f, FadeOutTime = 0.6f, Reveal = 4.3f, RevealTime = 0.45f;

        private string testDir;
        private float t = -1f;          // -1 while Unity's splash still shows
        private float skipAt = -1f;     // when the player skipped (the fades start from there)
        private int frameNo;
        private float nextFrame;

        private Canvas canvas;
        private CanvasGroup overlay, logoGroup;
        private RectTransform logo;
        private Image line, lineGlow, flash, gloss;
        private RawImage grain;
        private Texture2D grainTex;
        private AudioSource audioSource;
        private bool pausedAudio;

        private class Part { public RectTransform root, inner; public Image img; public StudioLogoData.Part data; public Vector2 home; }
        private readonly Dictionary<string, Part> parts = new Dictionary<string, Part>();
        private readonly List<Part> letters = new List<Part>();

        private void Awake()
        {
            Build();
            // the first menu's music and sounds wait until the logo is done (the intro's own sound ignores the pause)
            AudioListener.pause = true;
            pausedAudio = true;
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.ignoreListenerPause = true;
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            audioSource.volume = 0.85f;
            audioSource.clip = MakeSound();
        }

        private void OnDestroy()
        {
            if (pausedAudio) AudioListener.pause = false;
            Finished = true;
        }

        private void Update()
        {
            if (t < 0f)
            {
                // Unity's own splash first
                if (!UnityEngine.Rendering.SplashScreen.isFinished) return;
                t = 0f;
                audioSource.Play();
                if (testDir != null) { System.IO.Directory.CreateDirectory(testDir); Debug.Log("[IntroTest] started"); WriteWav(audioSource.clip, System.IO.Path.Combine(testDir, "intro_sound.wav")); }
            }
            else t += Mathf.Min(Time.unscaledDeltaTime, 0.05f);   // a loading hitch underneath must not make the animation jump

            if (skipAt < 0f && t > 0.5f && t < FadeOut && AnyInput())
            {
                skipAt = t;
                StartAudioFade();
            }
            Animate();

            if (testDir != null && t >= nextFrame && frameNo < 60)
            {
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(testDir, $"intro_{frameNo:00}.png"));
                frameNo++;
                nextFrame = t + 0.09f;
            }
        }

        private static bool AnyInput()
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
            if (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame)) return true;
            var pad = Gamepad.current;
            return pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame);
        }

        private float fadeVolumeFrom = -1f, fadeVolumeAt;
        private void StartAudioFade() { fadeVolumeFrom = audioSource.volume; fadeVolumeAt = t; }

        // ---------------- the animation ----------------
        private void Animate()
        {
            float fadeStart = skipAt >= 0f ? skipAt : FadeOut;
            float fadeLen = skipAt >= 0f ? 0.25f : FadeOutTime;
            float revealStart = skipAt >= 0f ? skipAt + 0.25f : Reveal;
            float revealLen = skipAt >= 0f ? 0.3f : RevealTime;

            // a slow camera push over the whole intro
            float push = Mathf.Lerp(1f, 1.04f, Smooth(0f, FadeOut + FadeOutTime, t));
            logo.localScale = Vector3.one * LogoScale * push;

            // the line of light
            float lineW = EaseOutExpo(Clamp01((t - LineIn) / (LineFull - LineIn)));
            float lineFade = 1f - Smooth(1.0f, 1.45f, t);
            line.rectTransform.sizeDelta = new Vector2(StudioLogoData.Width * 1.08f * lineW, Mathf.Lerp(3f, 1.5f, Smooth(0.9f, 1.4f, t)));
            line.color = new Color(1f, 1f, 1f, Smooth(LineIn, LineIn + 0.12f, t) * lineFade);
            lineGlow.rectTransform.sizeDelta = new Vector2(StudioLogoData.Width * 1.2f * lineW, 70f);
            lineGlow.color = new Color(1f, 1f, 1f, 0.22f * Smooth(LineIn, LineIn + 0.2f, t) * lineFade);

            // the mark: the 1/L shape rises from its base, the bar drops in, the E slides in from the right
            var l = parts["mark_L"];
            float lk = EaseInOutCubic(Clamp01((t - MarkStart) / 0.45f));
            l.root.sizeDelta = new Vector2(l.data.W, l.data.H * lk);
            l.img.color = new Color(1f, 1f, 1f, Smooth(MarkStart, MarkStart + 0.1f, t));
            var bar = parts["mark_bar"];
            float bk = EaseOutCubic(Clamp01((t - 0.98f) / 0.32f));
            bar.root.anchoredPosition = bar.home + new Vector2(0f, Mathf.Lerp(90f, 0f, bk));
            bar.img.color = new Color(1f, 1f, 1f, bk);
            var e = parts["mark_E"];
            float ek = EaseOutBack(Clamp01((t - 1.02f) / 0.4f));
            e.root.anchoredPosition = e.home + new Vector2(Mathf.Lerp(70f, 0f, ek), 0f);
            e.img.color = new Color(1f, 1f, 1f, Clamp01((t - 1.02f) / 0.2f));

            // the impact flash
            float since = t - Impact;
            flash.color = new Color(1f, 1f, 1f, since < 0f ? 0f : 0.28f * Mathf.Exp(-since * 6f));

            // STUDIO, letter by letter: rises and tightens into place
            for (int i = 0; i < letters.Count; i++)
            {
                var p = letters[i];
                float k = EaseOutCubic(Clamp01((t - (LettersStart + i * LetterStep)) / LetterTime));
                p.root.anchoredPosition = p.home + new Vector2(Mathf.Lerp(34f + i * 6f, 0f, k), Mathf.Lerp(-22f, 0f, k));
                p.img.color = new Color(1f, 1f, 1f, k);
            }

            // one gloss over the whole logo (clipped to its shape)
            float g = Clamp01((t - GlossStart) / GlossTime);
            gloss.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-260f, StudioLogoData.Width + 260f, EaseInOutCubic(g)), 0f);
            gloss.color = new Color(1f, 1f, 1f, g > 0f && g < 1f ? 0.55f * Mathf.Sin(g * Mathf.PI) : 0f);

            // fade the logo, then reveal the menu
            logoGroup.alpha = 1f - Smooth(fadeStart, fadeStart + fadeLen, t);
            overlay.alpha = 1f - Smooth(revealStart, revealStart + revealLen, t);
            if (fadeVolumeFrom < 0f && t >= FadeOut) StartAudioFade();
            if (fadeVolumeFrom >= 0f) audioSource.volume = Mathf.Lerp(fadeVolumeFrom, 0f, Clamp01((t - fadeVolumeAt) / (skipAt >= 0f ? 0.4f : 1.1f)));

            // film grain
            if (grain != null) grain.uvRect = new Rect(Random.value, Random.value, 3f, 1.7f);

            if (t >= revealStart + revealLen)
            {
                if (testDir != null) Debug.Log("[IntroTest] DONE after " + t.ToString("0.00") + " s" + (skipAt >= 0f ? " (skipped)" : ""));
                Destroy(gameObject);
            }
        }

        private const float LogoScale = 0.78f;   // the logo is 1280 px wide: about half of a 1920 screen

        // ---------------- build ----------------
        private void Build()
        {
            var cgo = new GameObject("Intro Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            cgo.transform.SetParent(transform, false);
            canvas = cgo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            var scaler = cgo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            overlay = cgo.GetComponent<CanvasGroup>();

            var black = Img("Black", cgo.transform, null, Color.black);
            Stretch(black.rectTransform);
            black.raycastTarget = true;   // nothing behind the intro can be clicked

            // the logo, centred
            var lgo = new GameObject("Logo", typeof(RectTransform), typeof(CanvasGroup));
            lgo.transform.SetParent(cgo.transform, false);
            logo = lgo.GetComponent<RectTransform>();
            logo.anchorMin = logo.anchorMax = new Vector2(0.5f, 0.5f);
            logo.pivot = new Vector2(0.5f, 0.5f);
            logo.sizeDelta = new Vector2(StudioLogoData.Width, StudioLogoData.Height);
            logoGroup = lgo.GetComponent<CanvasGroup>();

            var soft = SoftSprite();
            // the impact glow sits on the mark (the letters are not there yet)
            flash = Img("Flash", logo, soft, new Color(1f, 1f, 1f, 0f));
            Centre(flash.rectTransform, new Vector2(StudioLogoData.Height * 2.2f, StudioLogoData.Height * 2.2f));
            flash.rectTransform.anchoredPosition = new Vector2(-StudioLogoData.Width * 0.5f + 206f, 0f);

            // the line sits where the mark's foot will be, so the mark rises out of it
            float lineY = -StudioLogoData.Height * 0.5f + (StudioLogoData.Height - 26f);
            lineGlow = Img("Line glow", logo, soft, new Color(1f, 1f, 1f, 0f));
            Centre(lineGlow.rectTransform, new Vector2(0f, 70f)); lineGlow.rectTransform.anchoredPosition = new Vector2(0f, -lineY);
            line = Img("Line", logo, null, new Color(1f, 1f, 1f, 0f));
            Centre(line.rectTransform, new Vector2(0f, 3f)); line.rectTransform.anchoredPosition = new Vector2(0f, -lineY);

            // the parts, each where it sits in the logo (logo space: top-left origin, y down)
            var full = new Texture2D(StudioLogoData.Width, StudioLogoData.Height, TextureFormat.RGBA32, false) { name = "StudioLogo" };
            var clear = new Color32[StudioLogoData.Width * StudioLogoData.Height];
            foreach (var d in StudioLogoData.Parts)
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "Logo_" + d.Name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                tex.LoadImage(System.Convert.FromBase64String(d.Png));
                // into the whole-logo texture too (the gloss is clipped to it)
                var src = tex.GetPixels32();
                for (int y = 0; y < tex.height; y++)
                    for (int x = 0; x < tex.width; x++)
                    {
                        var c = src[y * tex.width + x];
                        if (c.a == 0) continue;
                        int fx = d.X + x, fy = StudioLogoData.Height - (d.Y + tex.height) + y;
                        if (fx < 0 || fy < 0 || fx >= full.width || fy >= full.height) continue;
                        int i = fy * full.width + fx;
                        if (c.a > clear[i].a) clear[i] = new Color32(255, 255, 255, c.a);
                    }
                var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0f), 100f);
                var p = new Part { data = d };
                // the root is a clipping window (the 1/L shape is revealed by growing it upwards from the base)
                var rgo = new GameObject(d.Name, typeof(RectTransform), typeof(RectMask2D));
                rgo.transform.SetParent(logo, false);
                p.root = rgo.GetComponent<RectTransform>();
                p.root.anchorMin = p.root.anchorMax = new Vector2(0f, 1f);
                p.root.pivot = new Vector2(0f, 0f);
                p.home = new Vector2(d.X, -(d.Y + d.H));
                p.root.anchoredPosition = p.home;
                p.root.sizeDelta = new Vector2(d.W, d.H);
                p.img = Img("Image", p.root, sprite, new Color(1f, 1f, 1f, 0f));
                var ir = p.img.rectTransform;
                ir.anchorMin = ir.anchorMax = new Vector2(0f, 0f); ir.pivot = new Vector2(0f, 0f);
                ir.anchoredPosition = Vector2.zero; ir.sizeDelta = new Vector2(d.W, d.H);
                parts[d.Name] = p;
                if (!d.Name.StartsWith("mark")) letters.Add(p);
            }
            full.SetPixels32(clear);
            full.Apply();

            // the gloss band, clipped to the logo's shape
            var mgo = new GameObject("Gloss mask", typeof(RectTransform), typeof(Image), typeof(Mask));
            mgo.transform.SetParent(logo, false);
            var mr = mgo.GetComponent<RectTransform>();
            Stretch(mr);
            var mi = mgo.GetComponent<Image>();
            mi.sprite = Sprite.Create(full, new Rect(0, 0, full.width, full.height), new Vector2(0.5f, 0.5f), 100f);
            mi.raycastTarget = false;
            mgo.GetComponent<Mask>().showMaskGraphic = false;
            gloss = Img("Gloss", mr, BandSprite(), new Color(1f, 1f, 1f, 0f));
            var gr = gloss.rectTransform;
            gr.anchorMin = gr.anchorMax = new Vector2(0f, 0.5f); gr.pivot = new Vector2(0.5f, 0.5f);
            gr.sizeDelta = new Vector2(220f, StudioLogoData.Height * 2f);
            gr.localRotation = Quaternion.Euler(0f, 0f, -18f);

            // vignette and grain over everything
            var vig = Img("Vignette", cgo.transform, VignetteSprite(), new Color(0f, 0f, 0f, 0.85f));
            Stretch(vig.rectTransform);
            grainTex = GrainTexture();
            var ggo = new GameObject("Grain", typeof(RectTransform), typeof(RawImage));
            ggo.transform.SetParent(cgo.transform, false);
            grain = ggo.GetComponent<RawImage>();
            grain.texture = grainTex;
            grain.color = new Color(1f, 1f, 1f, 0.018f);   // barely there: the background stays black
            grain.raycastTarget = false;
            Stretch(grain.rectTransform);
        }

        // ---------------- the sound ----------------
        /// <summary>Drone swell, a rising whoosh, a deep impact with a crisp transient and a short shimmer, a soft swish for the gloss.</summary>
        private static AudioClip MakeSound()
        {
            const int sr = 44100;
            float len = Reveal + RevealTime + 0.4f;
            int n = Mathf.CeilToInt(len * sr);
            var data = new float[n];
            var rng = new System.Random(1971);
            float lpWhoosh = 0f, lpTail = 0f, hpPrev = 0f, lpSwish = 0f;
            float phaseSub = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float s = 0f;

                // drone: two low tones and an octave, swelling in, held, released
                float de = Smooth(0f, 1.2f, t) * (1f - Smooth(FadeOut - 0.2f, FadeOut + 0.9f, t)) * (1f - 0.4f * Smooth(Impact, Impact + 0.5f, t));
                s += (Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 82.4f * t) * 0.28f + Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.12f) * de * 0.2f;

                // whoosh: noise through a low-pass that opens as the line draws, cut at the impact
                if (t > 0.5f && t < Impact + 0.05f)
                {
                    float fc = Mathf.Lerp(180f, 5200f, Smooth(0.5f, Impact, t));
                    float a = 1f - Mathf.Exp(-2f * Mathf.PI * fc / sr);
                    lpWhoosh += a * (noise - lpWhoosh);
                    s += lpWhoosh * Smooth(0.5f, Impact - 0.05f, t) * 0.3f;
                }

                float ti = t - Impact;
                if (ti >= 0f)
                {
                    // sub hit: a falling tone with a long decay
                    float f = Mathf.Lerp(38f, 72f, Mathf.Exp(-ti * 6f));
                    phaseSub += 2f * Mathf.PI * f / sr;
                    s += Mathf.Sin(phaseSub) * Mathf.Exp(-ti / 0.6f) * 0.8f;
                    // body
                    s += Mathf.Sin(2f * Mathf.PI * 146f * ti) * Mathf.Exp(-ti / 0.12f) * 0.25f;
                    // transient: a short high-passed noise crack
                    if (ti < 0.05f) { float hp = noise - hpPrev; s += hp * (1f - ti / 0.05f) * 0.35f; }
                    // tail: a low wash that dies away
                    lpTail += 0.04f * (noise - lpTail);
                    s += lpTail * Mathf.Exp(-ti / 1.6f) * 0.45f;
                    // shimmer: a few high tones ringing out
                    float sh = Mathf.Exp(-ti / 1.5f) * Smooth(0f, 0.08f, ti) * 0.03f;
                    s += (Mathf.Sin(2f * Mathf.PI * 2093f * t) + Mathf.Sin(2f * Mathf.PI * 3136f * t * 1.0007f) * 0.7f + Mathf.Sin(2f * Mathf.PI * 4186f * t) * 0.45f) * sh;
                }
                hpPrev = noise;

                // gloss: a very soft high swish
                float tg = t - GlossStart;
                if (tg > 0f && tg < GlossTime)
                {
                    lpSwish += 0.35f * (noise - lpSwish);
                    s += (noise - lpSwish) * Mathf.Sin(tg / GlossTime * Mathf.PI) * 0.05f;
                }
                data[i] = s;
            }
            float peak = 0.001f;
            for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            float gain = 0.85f / peak;
            for (int i = 0; i < n; i++) data[i] *= gain;
            var clip = AudioClip.Create("StudioIntro", n, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Test aid: the generated sound as a 16-bit WAV file.</summary>
        private static void WriteWav(AudioClip clip, string path)
        {
            var data = new float[clip.samples * clip.channels];
            clip.GetData(data, 0);
            using (var w = new System.IO.BinaryWriter(System.IO.File.Create(path)))
            {
                int bytes = data.Length * 2;
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + bytes); w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                w.Write(16); w.Write((short)1); w.Write((short)clip.channels); w.Write(clip.frequency); w.Write(clip.frequency * clip.channels * 2);
                w.Write((short)(clip.channels * 2)); w.Write((short)16); w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(bytes);
                foreach (var f in data) w.Write((short)(Mathf.Clamp(f, -1f, 1f) * 32767f));
            }
        }

        // ---------------- helpers ----------------
        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
        private static float Smooth(float a, float b, float x) { float k = Clamp01((x - a) / (b - a)); return k * k * (3f - 2f * k); }
        private static float EaseOutCubic(float k) { k = 1f - k; return 1f - k * k * k; }
        private static float EaseInOutCubic(float k) => k < 0.5f ? 4f * k * k * k : 1f - Mathf.Pow(-2f * k + 2f, 3f) * 0.5f;
        private static float EaseOutExpo(float k) => k >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * k);
        private static float EaseOutBack(float k) { const float c = 1.3f; float m = k - 1f; return 1f + (c + 1f) * m * m * m + c * m * m; }

        private static Image Img(string name, Transform parent, Sprite s, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = s;
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        private static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
        private static void Centre(RectTransform r, Vector2 size) { r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.pivot = new Vector2(0.5f, 0.5f); r.anchoredPosition = Vector2.zero; r.sizeDelta = size; }

        // a soft round glow
        private static Sprite SoftSprite()
        {
            const int N = 128;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "IntroSoft" };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v));
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
                }
            t.SetPixels32(px); t.Apply();
            return Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
        }

        // a vertical band, bright in the middle (the gloss)
        private static Sprite BandSprite()
        {
            const int W = 64, H = 4;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "IntroBand" };
            var px = new Color32[W * H];
            for (int x = 0; x < W; x++)
            {
                float u = (x + 0.5f) / W * 2f - 1f;
                byte a = (byte)(Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(u)), 2.2f) * 255f);
                for (int y = 0; y < H; y++) px[y * W + x] = new Color32(255, 255, 255, a);
            }
            t.SetPixels32(px); t.Apply();
            return Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite VignetteSprite()
        {
            const int W = 160, H = 90;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "IntroVignette" };
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = (x + 0.5f) / W * 2f - 1f, v = (y + 0.5f) / H * 2f - 1f;
                    float d = Mathf.Clamp01((Mathf.Sqrt(u * u * 0.8f + v * v) - 0.45f) / 0.75f);
                    px[y * W + x] = new Color32(255, 255, 255, (byte)(d * d * 255f));
                }
            t.SetPixels32(px); t.Apply();
            return Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Texture2D GrainTexture()
        {
            const int N = 256;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point, name = "IntroGrain" };
            var px = new Color32[N * N];
            var rng = new System.Random(7);
            for (int i = 0; i < px.Length; i++) { byte v = (byte)rng.Next(256); px[i] = new Color32(v, v, v, 255); }
            t.SetPixels32(px); t.Apply();
            return t;
        }
    }
}
