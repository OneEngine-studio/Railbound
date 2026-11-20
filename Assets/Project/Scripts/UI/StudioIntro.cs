using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// The OneEngine Studio logo intro, right after Unity's splash screen and before the first menu (about 5.5 s, any key skips it), in
    /// the game's own monochrome HUD language with a cinematic finish:
    ///   HUD boot: a faint grid and a scan line, corner brackets fly in and frame the logo, small labels type on at the frame corners.
    ///   Ignition: a line of light ignites with an anamorphic lens streak on a rising sound.
    ///   Glitch-in: the mark arrives in horizontal strips that jitter in with red / cyan colour splitting and snap together on the
    ///   impact (screen shake, flash, light rays, a burst of dust).
    ///   Decode: STUDIO decodes letter by letter (flicker, tick, slide into place); a gloss and a flare sweep across; slow push in.
    ///   Exit: a short glitch-out, the brackets fly away, and the menu is revealed.
    /// The sound is generated here; the menu's own audio waits. Plays once per launch; never on a server or in a test run
    /// (<c>-introtest DIR</c> forces it and saves frames and the sound).
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
        private const float GridIn = 0.15f, ScanStart = 0.2f, ScanTime = 0.75f;
        private const float BracketsIn = 0.25f, BracketsTime = 0.5f, LabelsIn = 0.55f;
        private const float Ignite = 0.85f, IgniteTime = 0.45f;
        private const float GlitchIn = 1.3f, Impact = 1.75f;
        private const float DecodeStart = 2.0f, LetterStep = 0.065f, LetterTime = 0.42f;
        private const float GlossStart = 2.9f, GlossTime = 0.75f;
        private const float ExitStart = 4.7f, ExitTime = 0.4f, Reveal = 5.1f, RevealTime = 0.45f;
        private const float LogoScale = 0.78f;   // the logo is 1280 px wide: about half of a 1920 screen

        private string testDir;
        private float t = -1f;          // -1 while Unity's splash still shows
        private float skipAt = -1f;     // when the player skipped (the exit starts from there)
        private int frameNo;
        private float nextFrame;

        private Canvas canvas;
        private CanvasGroup overlay, logoGroup, frameGroup;
        private RectTransform logo, frame, stage;
        private Image line, lineCore, flare, flareCore, flash, gloss, scan, glossFlare;
        private RawImage grid, grain;
        private AudioSource audioSource;
        private bool pausedAudio;
        private TMP_FontAsset fontBold, fontSemi;
        private System.Random rng = new System.Random(2026);

        // the mark, in horizontal strips (each with a red and a cyan copy behind for the colour split)
        private class Strip { public RectTransform root; public Image main, red, cyan; public Vector2 home; public float seed, delay; }
        private readonly List<Strip> strips = new List<Strip>();
        private class Letter { public RectTransform root; public Image main, red, cyan; public Vector2 home; public float seed; }
        private readonly List<Letter> letters = new List<Letter>();
        private class Ray { public RectTransform r; public Image img; public float angle, speed, len; }
        private readonly List<Ray> rays = new List<Ray>();
        private class Mote { public RectTransform r; public Image img; public Vector2 pos, vel; public float life, born, size; }
        private readonly List<Mote> motes = new List<Mote>();
        private class Bracket { public RectTransform r; public Vector2 home, from; }
        private readonly List<Bracket> brackets = new List<Bracket>();
        private class Label { public TextMeshProUGUI text; public string full; public float at; }
        private readonly List<Label> labels = new List<Label>();
        private Label timecode;
        private Vector2 markCentre;   // in logo space, from the centre

        private void Awake()
        {
            // the first menu's music and sounds wait until the logo is done (the intro's own sound ignores the pause)
            AudioListener.pause = true;
            pausedAudio = true;
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.ignoreListenerPause = true;
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            audioSource.volume = 0.9f;
            audioSource.clip = MakeSound();
            BuildBackdrop();   // black at once; the rest once the first scene (and its fonts) is there
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
                BuildStage();
                t = 0f;
                audioSource.Play();
                if (testDir != null) { System.IO.Directory.CreateDirectory(testDir); Debug.Log("[IntroTest] started"); WriteWav(audioSource.clip, System.IO.Path.Combine(testDir, "intro_sound.wav")); }
            }
            else t += Mathf.Min(Time.unscaledDeltaTime, 0.05f);   // a loading hitch underneath must not make the animation jump

            if (skipAt < 0f && t > 0.5f && t < ExitStart && AnyInput()) skipAt = t;
            Animate();

            if (testDir != null && t >= nextFrame && frameNo < 70)
            {
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(testDir, $"intro_{frameNo:00}.png"));
                frameNo++;
                nextFrame = t + 0.08f;
            }
        }

        private static bool AnyInput()
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
            if (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame)) return true;
            var pad = Gamepad.current;
            return pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame);
        }

        // ================================================================== the animation
        private void Animate()
        {
            float exit = skipAt >= 0f ? skipAt : ExitStart;
            float exitLen = skipAt >= 0f ? 0.25f : ExitTime;
            float reveal = skipAt >= 0f ? skipAt + 0.25f : Reveal;
            float revealLen = skipAt >= 0f ? 0.3f : RevealTime;
            float ex = Clamp01((t - exit) / exitLen);   // 0 .. 1 through the exit

            // camera: a slow push, a short shake on the impact
            float push = Mathf.Lerp(1f, 1.035f, Smooth(Impact, exit + exitLen, t));
            float sinceImpact = t - Impact;
            Vector2 shake = Vector2.zero;
            if (sinceImpact > 0f && sinceImpact < 0.22f)
            {
                float a = (1f - sinceImpact / 0.22f) * 7f;
                shake = new Vector2(Noise(t * 60f, 1f), Noise(t * 60f, 7f)) * a;
            }
            stage.anchoredPosition = shake;
            logo.localScale = Vector3.one * LogoScale * push;

            // HUD boot: grid, scan line
            grid.color = new Color(1f, 1f, 1f, 0.05f * Smooth(GridIn, GridIn + 0.5f, t) * (1f - 0.45f * Smooth(Impact, Impact + 1f, t)) * (1f - ex));
            grid.uvRect = new Rect(0f, -t * 0.01f, 30f, 17f);
            float sk = Clamp01((t - ScanStart) / ScanTime);
            scan.rectTransform.anchorMin = new Vector2(0f, 1f - EaseInOutCubic(sk));
            scan.rectTransform.anchorMax = new Vector2(1f, 1f - EaseInOutCubic(sk));
            scan.color = new Color(1f, 1f, 1f, sk > 0f && sk < 1f ? 0.12f * Mathf.Sin(sk * Mathf.PI) : 0f);

            // brackets fly in from the screen corners, breathe, fly out
            float bk = EaseOutExpo(Clamp01((t - BracketsIn) / BracketsTime));
            float breathe = Mathf.Sin(t * 1.6f) * 3f * Smooth(BracketsIn + BracketsTime, BracketsIn + BracketsTime + 0.5f, t);
            float bout = EaseInCubic(ex);
            foreach (var b in brackets)
            {
                Vector2 dir = b.home.normalized;
                Vector2 p = Vector2.LerpUnclamped(b.from, b.home, bk) + dir * breathe + dir * 260f * bout;
                b.r.anchoredPosition = p;
            }
            frameGroup.alpha = Smooth(BracketsIn, BracketsIn + 0.15f, t) * (1f - ex);

            // labels type on (one character every 18 ms); the timecode runs at 24 frames a second
            foreach (var l in labels)
            {
                int n = Mathf.Clamp(Mathf.FloorToInt((t - l.at) / 0.018f), 0, l.full.Length);
                l.text.text = l.full.Substring(0, n) + (n < l.full.Length && n > 0 ? "_" : "");
            }
            if (timecode != null && t >= timecode.at)
            {
                int frames = Mathf.FloorToInt(t * 24f);
                timecode.text.text = $"TC 00:00:{frames / 24:00}:{frames % 24:00}";
            }

            // ignition: the line and its anamorphic streak
            float ig = EaseOutExpo(Clamp01((t - Ignite) / IgniteTime));
            float lineFade = 1f - Smooth(Impact, Impact + 0.35f, t);
            line.rectTransform.sizeDelta = new Vector2(StudioLogoData.Width * 1.15f * ig, 2f);
            line.color = new Color(1f, 1f, 1f, 0.9f * Smooth(Ignite, Ignite + 0.08f, t) * lineFade);
            lineCore.color = new Color(1f, 1f, 1f, Smooth(Ignite, Ignite + 0.06f, t) * lineFade * (0.8f + 0.2f * Mathf.Sin(t * 40f)));
            float flareA = 0.45f * Smooth(Ignite, Ignite + 0.2f, t) * (1f - Smooth(Impact + 0.1f, Impact + 0.9f, t)) + (sinceImpact > 0f ? 0.5f * Mathf.Exp(-sinceImpact * 4f) : 0f);
            flareA += 0.5f * Smooth(exit, exit + exitLen * 0.4f, t) * (1f - Smooth(exit + exitLen * 0.4f, exit + exitLen, t));
            flare.color = new Color(0.62f, 0.82f, 1f, flareA * 0.55f);
            flare.rectTransform.sizeDelta = new Vector2(2600f * Mathf.Max(ig, 0.2f), 46f);
            flareCore.color = new Color(1f, 1f, 1f, Mathf.Clamp01(flareA * 1.1f));

            // the mark: strips jitter in with colour split, snap on the impact, tear apart on the exit
            float chroma = 0f;
            if (t < Impact) chroma = Mathf.Lerp(0f, 16f, Smooth(GlitchIn, Impact - 0.05f, t));
            else chroma = 1.2f + 14f * Mathf.Exp(-sinceImpact * 7f);
            chroma += 22f * ex;
            foreach (var s in strips)
            {
                float k = Clamp01((t - (GlitchIn + s.delay)) / (Impact - GlitchIn - s.delay));
                float on = t >= GlitchIn + s.delay ? 1f : 0f;
                // jitter: big early, shrinking to nothing at the impact; plus flicker
                float jitter = (1f - EaseOutCubic(k)) * 140f * Noise(t * 22f, s.seed) * (t < Impact ? 1f : 0f);
                float tear = ex * 180f * Noise(t * 18f, s.seed + 3f);
                bool flick = t < Impact && Noise(t * 30f, s.seed + 9f) > 0.55f;
                float alpha = on * (flick ? 0.35f : 1f) * (1f - ex);
                s.root.anchoredPosition = s.home + new Vector2(jitter + tear, 0f);
                s.main.color = new Color(1f, 1f, 1f, alpha);
                s.red.color = new Color(1f, 0.18f, 0.2f, alpha * 0.85f);
                s.cyan.color = new Color(0.2f, 0.9f, 1f, alpha * 0.85f);
                s.red.rectTransform.anchoredPosition = new Vector2(-chroma, 0f);
                s.cyan.rectTransform.anchoredPosition = new Vector2(chroma, 0f);
            }

            // the impact: flash, light rays, dust
            flash.color = new Color(1f, 1f, 1f, sinceImpact < 0f ? 0f : 0.35f * Mathf.Exp(-sinceImpact * 6f));
            float rayA = sinceImpact < 0f ? 0f : 0.14f * Mathf.Exp(-sinceImpact * 2.2f) * Smooth(0f, 0.05f, sinceImpact);
            foreach (var r in rays)
            {
                r.r.localRotation = Quaternion.Euler(0f, 0f, r.angle + t * r.speed);
                r.r.sizeDelta = new Vector2(r.len * Mathf.Lerp(0.6f, 1.15f, EaseOutCubic(Clamp01(sinceImpact / 1.5f))), r.r.sizeDelta.y);
                r.img.color = new Color(1f, 1f, 1f, rayA * (1f - ex));
            }
            foreach (var m in motes)
            {
                float age = t - m.born;
                if (age < 0f) { m.img.color = new Color(1f, 1f, 1f, 0f); continue; }
                m.vel *= Mathf.Exp(-Time.unscaledDeltaTime * 1.6f);
                m.vel += new Vector2(0f, 14f) * Time.unscaledDeltaTime;   // a slow drift upwards
                m.pos += m.vel * Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                m.r.anchoredPosition = m.pos;
                float life = Clamp01(age / m.life);
                m.img.color = new Color(1f, 1f, 1f, 0.75f * (1f - life) * Smooth(0f, 0.05f, age) * (1f - ex));
            }

            // STUDIO decodes letter by letter
            for (int i = 0; i < letters.Count; i++)
            {
                var l = letters[i];
                float start = DecodeStart + i * LetterStep;
                float k = Clamp01((t - start) / LetterTime);
                float flickerPhase = Clamp01((t - start) / 0.16f);
                bool lit = t >= start && (flickerPhase >= 1f || Noise(t * 45f, l.seed) > 0.1f);
                float alpha = lit ? EaseOutCubic(Mathf.Max(k, flickerPhase * 0.7f)) : 0f;
                float tear = ex * 120f * Noise(t * 16f, l.seed + 5f);
                l.root.anchoredPosition = l.home + new Vector2(Mathf.Lerp(28f, 0f, EaseOutCubic(k)) + tear, 0f);
                alpha *= 1f - ex;
                l.main.color = new Color(1f, 1f, 1f, alpha);
                float lc = (1f - EaseOutCubic(k)) * 8f + 1f + 18f * ex;
                l.red.color = new Color(1f, 0.18f, 0.2f, alpha * 0.8f);
                l.cyan.color = new Color(0.2f, 0.9f, 1f, alpha * 0.8f);
                l.red.rectTransform.anchoredPosition = new Vector2(-lc, 0f);
                l.cyan.rectTransform.anchoredPosition = new Vector2(lc, 0f);
            }

            // one gloss over the logo, with a flare riding on it
            float g = Clamp01((t - GlossStart) / GlossTime);
            float gx = Mathf.Lerp(-260f, StudioLogoData.Width + 260f, EaseInOutCubic(g));
            gloss.rectTransform.anchoredPosition = new Vector2(gx, 0f);
            gloss.color = new Color(1f, 1f, 1f, g > 0f && g < 1f ? 0.6f * Mathf.Sin(g * Mathf.PI) : 0f);
            glossFlare.rectTransform.anchoredPosition = new Vector2(gx - StudioLogoData.Width * 0.5f, 0f);
            glossFlare.color = new Color(0.7f, 0.86f, 1f, g > 0f && g < 1f ? 0.35f * Mathf.Sin(g * Mathf.PI) : 0f);

            logoGroup.alpha = 1f - Smooth(exit + exitLen * 0.5f, exit + exitLen, t);
            overlay.alpha = 1f - Smooth(reveal, reveal + revealLen, t);
            audioSource.volume = 0.9f * (skipAt >= 0f ? 1f - Clamp01((t - skipAt) / 0.4f) : 1f);
            if (grain != null) grain.uvRect = new Rect((float)rng.NextDouble(), (float)rng.NextDouble(), 3f, 1.7f);

            if (t >= reveal + revealLen)
            {
                if (testDir != null) Debug.Log("[IntroTest] DONE after " + t.ToString("0.00") + " s" + (skipAt >= 0f ? " (skipped)" : ""));
                Destroy(gameObject);
            }
        }

        // ================================================================== build
        private void BuildBackdrop()
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
        }

        private void BuildStage()
        {
            fontBold = FindFont("Rajdhani-Bold") ?? TMP_Settings.defaultFontAsset;
            fontSemi = FindFont("Rajdhani-SemiBold") ?? fontBold;
            var soft = SoftSprite();
            var root = canvas.transform;

            var sgo = new GameObject("Stage", typeof(RectTransform));
            sgo.transform.SetParent(root, false);
            stage = sgo.GetComponent<RectTransform>();
            Stretch(stage);

            // grid and scan line
            var ggo = new GameObject("Grid", typeof(RectTransform), typeof(RawImage));
            ggo.transform.SetParent(stage, false);
            grid = ggo.GetComponent<RawImage>();
            grid.texture = GridTexture();
            grid.color = new Color(1f, 1f, 1f, 0f);
            grid.raycastTarget = false;
            Stretch(grid.rectTransform);
            scan = Img("Scan", stage, BandSpriteH(), new Color(1f, 1f, 1f, 0f));
            scan.rectTransform.anchorMin = new Vector2(0f, 1f); scan.rectTransform.anchorMax = new Vector2(1f, 1f);
            scan.rectTransform.pivot = new Vector2(0.5f, 0.5f); scan.rectTransform.sizeDelta = new Vector2(0f, 120f);

            // the logo space: 1280 x 440, centred
            var lgo = new GameObject("Logo", typeof(RectTransform), typeof(CanvasGroup));
            lgo.transform.SetParent(stage, false);
            logo = lgo.GetComponent<RectTransform>();
            logo.anchorMin = logo.anchorMax = new Vector2(0.5f, 0.5f);
            logo.pivot = new Vector2(0.5f, 0.5f);
            logo.sizeDelta = new Vector2(StudioLogoData.Width, StudioLogoData.Height);
            logoGroup = lgo.GetComponent<CanvasGroup>();
            logoGroup.alpha = 1f;

            // decode the parts; the mark into one texture (for the strips), the letters each on their own
            var markTex = new Texture2D(StudioLogoData.Width, StudioLogoData.Height, TextureFormat.RGBA32, false) { name = "StudioMark", wrapMode = TextureWrapMode.Clamp };
            var full = new Texture2D(StudioLogoData.Width, StudioLogoData.Height, TextureFormat.RGBA32, false) { name = "StudioLogo", wrapMode = TextureWrapMode.Clamp };
            var markPx = new Color32[StudioLogoData.Width * StudioLogoData.Height];
            var fullPx = new Color32[StudioLogoData.Width * StudioLogoData.Height];
            int mx0 = int.MaxValue, my0 = int.MaxValue, mx1 = 0, my1 = 0;
            var letterData = new List<(StudioLogoData.Part d, Texture2D tex)>();
            foreach (var d in StudioLogoData.Parts)
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "Logo_" + d.Name, wrapMode = TextureWrapMode.Clamp };
                tex.LoadImage(System.Convert.FromBase64String(d.Png));
                bool isMark = d.Name.StartsWith("mark");
                var src = tex.GetPixels32();
                for (int y = 0; y < tex.height; y++)
                    for (int x = 0; x < tex.width; x++)
                    {
                        var c = src[y * tex.width + x];
                        if (c.a == 0) continue;
                        int fx = d.X + x, fy = StudioLogoData.Height - (d.Y + tex.height) + y;
                        if (fx < 0 || fy < 0 || fx >= StudioLogoData.Width || fy >= StudioLogoData.Height) continue;
                        int i = fy * StudioLogoData.Width + fx;
                        if (c.a > fullPx[i].a) fullPx[i] = new Color32(255, 255, 255, c.a);
                        if (isMark && c.a > markPx[i].a) markPx[i] = new Color32(255, 255, 255, c.a);
                    }
                if (isMark) { mx0 = Mathf.Min(mx0, d.X); my0 = Mathf.Min(my0, d.Y); mx1 = Mathf.Max(mx1, d.X + d.W); my1 = Mathf.Max(my1, d.Y + d.H); }
                else letterData.Add((d, tex));
            }
            markTex.SetPixels32(markPx); markTex.Apply();
            full.SetPixels32(fullPx); full.Apply();
            markCentre = new Vector2((mx0 + mx1) * 0.5f - StudioLogoData.Width * 0.5f, StudioLogoData.Height * 0.5f - (my0 + my1) * 0.5f);

            // light rays behind the mark
            var beam = BeamSprite();
            for (int i = 0; i < 14; i++)
            {
                var img = Img("Ray", logo, beam, new Color(1f, 1f, 1f, 0f));
                var r = img.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                r.pivot = new Vector2(0f, 0.5f);
                r.anchoredPosition = markCentre;
                float len = 700f + (float)rng.NextDouble() * 900f;
                r.sizeDelta = new Vector2(len, 18f + (float)rng.NextDouble() * 40f);
                rays.Add(new Ray { r = r, img = img, angle = i * (360f / 14f) + (float)rng.NextDouble() * 12f, speed = ((float)rng.NextDouble() - 0.5f) * 10f, len = len });
            }

            // the impact glow
            flash = Img("Flash", logo, soft, new Color(1f, 1f, 1f, 0f));
            Centre(flash.rectTransform, new Vector2(StudioLogoData.Height * 2.4f, StudioLogoData.Height * 2.4f));
            flash.rectTransform.anchoredPosition = markCentre;

            // the line and the anamorphic streak, at the mark's foot
            float footY = StudioLogoData.Height * 0.5f - (my1 - 14f);
            flare = Img("Flare", logo, soft, new Color(1f, 1f, 1f, 0f));
            Centre(flare.rectTransform, new Vector2(2600f, 46f)); flare.rectTransform.anchoredPosition = new Vector2(0f, footY);
            line = Img("Line", logo, null, new Color(1f, 1f, 1f, 0f));
            Centre(line.rectTransform, new Vector2(0f, 2f)); line.rectTransform.anchoredPosition = new Vector2(0f, footY);
            lineCore = Img("Line core", logo, soft, new Color(1f, 1f, 1f, 0f));
            Centre(lineCore.rectTransform, new Vector2(160f, 26f)); lineCore.rectTransform.anchoredPosition = new Vector2(0f, footY);
            flareCore = Img("Flare core", logo, soft, new Color(1f, 1f, 1f, 0f));
            Centre(flareCore.rectTransform, new Vector2(90f, 90f)); flareCore.rectTransform.anchoredPosition = new Vector2(0f, footY);

            // the mark in 14 strips
            int strips = 14;
            float sh = (my1 - my0) / (float)strips;
            for (int i = 0; i < strips; i++)
            {
                float top = my0 + i * sh;
                // the texture's y is up: the strip's rectangle in it
                var rect = new Rect(mx0, StudioLogoData.Height - (top + sh), mx1 - mx0, sh);
                var sprite = Sprite.Create(markTex, rect, new Vector2(0f, 0f), 100f);
                var sgo2 = new GameObject("Strip " + i, typeof(RectTransform));
                sgo2.transform.SetParent(logo, false);
                var r = sgo2.GetComponent<RectTransform>();
                r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
                r.pivot = new Vector2(0f, 0f);
                var home = new Vector2(mx0, -(top + sh));
                r.anchoredPosition = home;
                r.sizeDelta = new Vector2(mx1 - mx0, sh);
                var red = Img("Red", r, sprite, Color.clear); Fill(red.rectTransform);
                var cyan = Img("Cyan", r, sprite, Color.clear); Fill(cyan.rectTransform);
                var main = Img("Main", r, sprite, Color.clear); Fill(main.rectTransform);
                this.strips.Add(new Strip { root = r, main = main, red = red, cyan = cyan, home = home, seed = i * 3.7f + 0.5f, delay = (float)rng.NextDouble() * 0.22f });
            }

            // the letters
            int li = 0;
            foreach (var (d, tex) in letterData)
            {
                var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0f, 0f), 100f);
                var lgo2 = new GameObject(d.Name, typeof(RectTransform));
                lgo2.transform.SetParent(logo, false);
                var r = lgo2.GetComponent<RectTransform>();
                r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
                r.pivot = new Vector2(0f, 0f);
                var home = new Vector2(d.X, -(d.Y + d.H));
                r.anchoredPosition = home;
                r.sizeDelta = new Vector2(d.W, d.H);
                var red = Img("Red", r, sprite, Color.clear); Fill(red.rectTransform);
                var cyan = Img("Cyan", r, sprite, Color.clear); Fill(cyan.rectTransform);
                var main = Img("Main", r, sprite, Color.clear); Fill(main.rectTransform);
                letters.Add(new Letter { root = r, main = main, red = red, cyan = cyan, home = home, seed = li++ * 5.3f + 1.1f });
            }

            // the gloss and its flare, clipped to the logo
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
            gr.sizeDelta = new Vector2(200f, StudioLogoData.Height * 2f);
            gr.localRotation = Quaternion.Euler(0f, 0f, -18f);
            glossFlare = Img("Gloss flare", logo, soft, new Color(1f, 1f, 1f, 0f));
            Centre(glossFlare.rectTransform, new Vector2(900f, 18f));

            // dust: a burst from the mark on the impact
            for (int i = 0; i < 70; i++)
            {
                var img = Img("Mote", logo, null, new Color(1f, 1f, 1f, 0f));
                var r = img.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                float size = 2f + (float)rng.NextDouble() * 4f;
                r.sizeDelta = new Vector2(size, size);
                float a = (float)(rng.NextDouble() * Mathf.PI * 2f), sp = 120f + (float)rng.NextDouble() * 520f;
                motes.Add(new Mote { r = r, img = img, pos = markCentre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 30f, vel = new Vector2(Mathf.Cos(a) * sp, Mathf.Sin(a) * sp * 0.6f), life = 1.2f + (float)rng.NextDouble() * 2.2f, born = Impact + (float)rng.NextDouble() * 0.08f, size = size });
            }

            // the HUD frame: brackets at the corners of the logo, labels beside them
            var fgo = new GameObject("Frame", typeof(RectTransform), typeof(CanvasGroup));
            fgo.transform.SetParent(stage, false);
            frame = fgo.GetComponent<RectTransform>();
            frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 0.5f);
            frameGroup = fgo.GetComponent<CanvasGroup>();
            frameGroup.alpha = 0f;
            float hw = StudioLogoData.Width * LogoScale * 0.5f + 90f, hh = StudioLogoData.Height * LogoScale * 0.5f + 70f;
            for (int c = 0; c < 4; c++)
            {
                float sx = c % 2 == 0 ? -1f : 1f, sy = c < 2 ? 1f : -1f;
                var bgo = new GameObject("Bracket", typeof(RectTransform));
                bgo.transform.SetParent(frame, false);
                var br = bgo.GetComponent<RectTransform>();
                br.sizeDelta = Vector2.zero;
                var hl = Img("H", br, null, new Color(1f, 1f, 1f, 0.85f));
                hl.rectTransform.pivot = new Vector2(sx < 0 ? 0f : 1f, 0.5f);
                hl.rectTransform.sizeDelta = new Vector2(46f, 2f);
                var vl = Img("V", br, null, new Color(1f, 1f, 1f, 0.85f));
                vl.rectTransform.pivot = new Vector2(0.5f, sy > 0 ? 1f : 0f);
                vl.rectTransform.sizeDelta = new Vector2(2f, 46f);
                var home = new Vector2(sx * hw, sy * hh);
                brackets.Add(new Bracket { r = br, home = home, from = new Vector2(sx * 1060f, sy * 600f) });
            }
            string version = string.IsNullOrEmpty(Application.version) ? "" : "BUILD " + Application.version.ToUpperInvariant();
            AddLabel("ONEENGINE  STUDIO", new Vector2(-hw + 2f, hh + 22f), TextAlignmentOptions.BottomLeft, LabelsIn, Color.white, 15f);
            timecode = AddLabel("TC 00:00:00:00", new Vector2(hw - 2f, hh + 22f), TextAlignmentOptions.BottomRight, LabelsIn + 0.1f, new Color(1f, 1f, 1f, 0.6f), 15f);
            AddLabel(version, new Vector2(-hw + 2f, -hh - 22f), TextAlignmentOptions.TopLeft, LabelsIn + 0.2f, new Color(1f, 1f, 1f, 0.6f), 13f);
            AddLabel("PRESS ANY KEY TO SKIP", new Vector2(hw - 2f, -hh - 22f), TextAlignmentOptions.TopRight, LabelsIn + 0.3f, new Color(1f, 1f, 1f, 0.45f), 13f);
            labels.Remove(timecode);

            // vignette and grain over everything
            var vig = Img("Vignette", root, VignetteSprite(), new Color(0f, 0f, 0f, 0.85f));
            Stretch(vig.rectTransform);
            var ngo = new GameObject("Grain", typeof(RectTransform), typeof(RawImage));
            ngo.transform.SetParent(root, false);
            grain = ngo.GetComponent<RawImage>();
            grain.texture = GrainTexture();
            grain.color = new Color(1f, 1f, 1f, 0.01f);
            grain.raycastTarget = false;
            Stretch(grain.rectTransform);
        }

        private Label AddLabel(string text, Vector2 pos, TextAlignmentOptions align, float at, Color c, float size)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(frame, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.font = fontSemi; t.fontSize = size; t.color = c; t.characterSpacing = 9f; t.alignment = align;
            t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Overflow; t.raycastTarget = false; t.text = "";
            var r = t.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            bool right = align == TextAlignmentOptions.BottomRight || align == TextAlignmentOptions.TopRight;
            bool bottom = align == TextAlignmentOptions.BottomLeft || align == TextAlignmentOptions.BottomRight;
            r.pivot = new Vector2(right ? 1f : 0f, bottom ? 0f : 1f);
            r.sizeDelta = new Vector2(600f, 30f);
            r.anchoredPosition = pos;
            var l = new Label { text = t, full = text, at = at };
            labels.Add(l);
            return l;
        }

        // ================================================================== the sound
        /// <summary>
        /// Sub rumble, HUD blips and typing ticks, a riser with a reverse swell into the impact (sub, metallic ring, crack, wash), glitch
        /// stutters, letter ticks, a soft swish for the gloss and a whoosh on the way out.
        /// </summary>
        private static AudioClip MakeSound()
        {
            const int sr = 44100;
            float len = Reveal + RevealTime + 0.6f;
            int n = Mathf.CeilToInt(len * sr);
            var data = new float[n];
            var rng = new System.Random(1971);
            float lp1 = 0f, lp2 = 0f, lp3 = 0f, lpTail = 0f, prev = 0f, phaseSub = 0f, phaseRise = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float s = 0f;

                // sub rumble under everything
                float rumble = Smooth(0f, 0.8f, t) * (1f - Smooth(ExitStart, Reveal + 0.3f, t)) * (1f - 0.5f * Smooth(Impact, Impact + 0.4f, t));
                lp1 += 0.012f * (noise - lp1);
                s += (lp1 * 3.2f + Mathf.Sin(2f * Mathf.PI * 41f * t) * 0.35f) * rumble * 0.22f;

                // HUD blips as the brackets land, ticks as the labels type
                s += Blip(t, BracketsIn + 0.18f, 1760f, 0.05f) * 0.12f + Blip(t, BracketsIn + 0.32f, 2349f, 0.04f) * 0.1f;
                if (t > LabelsIn && t < LabelsIn + 0.75f && ((int)((t - LabelsIn) / 0.018f)) % 2 == 0 && ((t - LabelsIn) % 0.018f) < 0.002f) s += noise * 0.06f;

                // riser: a sweep and opening noise from the ignition to the impact
                if (t > Ignite - 0.1f && t < Impact)
                {
                    float k = Smooth(Ignite - 0.1f, Impact, t);
                    phaseRise += 2f * Mathf.PI * Mathf.Lerp(180f, 1400f, k * k) / sr;
                    lp2 += (1f - Mathf.Exp(-2f * Mathf.PI * Mathf.Lerp(300f, 7000f, k) / sr)) * (noise - lp2);
                    s += (Mathf.Sin(phaseRise) * 0.08f + lp2 * 0.28f) * k;
                }
                // glitch stutter while the mark jitters in, and on the way out
                if ((t > GlitchIn && t < Impact) || (t > ExitStart && t < ExitStart + ExitTime))
                {
                    bool gate = ((int)(t * 46f) % 3) == 0;
                    if (gate) s += (noise - prev) * 0.18f;
                }

                float ti = t - Impact;
                if (ti >= 0f)
                {
                    float f = Mathf.Lerp(34f, 80f, Mathf.Exp(-ti * 7f));
                    phaseSub += 2f * Mathf.PI * f / sr;
                    s += Mathf.Sin(phaseSub) * Mathf.Exp(-ti / 0.75f) * 0.9f;                          // sub hit
                    if (ti < 0.06f) s += (noise - prev) * (1f - ti / 0.06f) * 0.45f;                   // crack
                    float ring = Mathf.Exp(-ti / 1.1f) * 0.05f;                                         // metallic ring (inharmonic)
                    s += (Mathf.Sin(2f * Mathf.PI * 523f * t) + Mathf.Sin(2f * Mathf.PI * 1311f * t) * 0.7f + Mathf.Sin(2f * Mathf.PI * 2467f * t) * 0.45f + Mathf.Sin(2f * Mathf.PI * 3907f * t) * 0.25f) * ring;
                    lpTail += 0.03f * (noise - lpTail);
                    s += lpTail * Mathf.Exp(-ti / 1.9f) * 0.55f;                                         // wash
                }
                else if (ti > -0.3f)
                {
                    // reverse swell into the impact
                    lp3 += 0.2f * (noise - lp3);
                    float k = 1f + ti / 0.3f;
                    s += lp3 * k * k * k * 0.35f;
                }

                // letter ticks
                for (int l = 0; l < 6; l++) s += Blip(t, DecodeStart + l * LetterStep, 3200f + l * 140f, 0.018f) * 0.05f;
                // gloss swish
                float tg = t - GlossStart;
                if (tg > 0f && tg < GlossTime) s += (noise - lp1) * Mathf.Sin(tg / GlossTime * Mathf.PI) * 0.045f;
                // outro whoosh, falling
                float to = t - ExitStart;
                if (to > 0f && to < 0.8f) { lp2 += 0.08f * (noise - lp2); s += lp2 * Mathf.Sin(Mathf.Clamp01(to / 0.8f) * Mathf.PI) * 0.3f; }

                prev = noise;
                data[i] = s;
            }
            float peak = 0.001f;
            for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            float gain = 0.88f / peak;
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(data[i] * gain, -1f, 1f);
            var clip = AudioClip.Create("StudioIntro", n, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float Blip(float t, float at, float freq, float dur)
        {
            float k = t - at;
            if (k < 0f || k > dur) return 0f;
            return Mathf.Sin(2f * Mathf.PI * freq * k) * (1f - k / dur) * Smooth(0f, 0.003f, k);
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

        // ================================================================== helpers
        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
        private static float Smooth(float a, float b, float x) { float k = Clamp01((x - a) / (b - a)); return k * k * (3f - 2f * k); }
        private static float EaseOutCubic(float k) { k = 1f - k; return 1f - k * k * k; }
        private static float EaseInCubic(float k) => k * k * k;
        private static float EaseInOutCubic(float k) => k < 0.5f ? 4f * k * k * k : 1f - Mathf.Pow(-2f * k + 2f, 3f) * 0.5f;
        private static float EaseOutExpo(float k) => k >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * k);
        /// <summary>Smooth noise from -1 to 1.</summary>
        private static float Noise(float x, float seed) => Mathf.PerlinNoise(x, seed * 13.1f) * 2f - 1f;

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
        private static void Fill(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; r.pivot = new Vector2(0.5f, 0.5f); }
        private static void Centre(RectTransform r, Vector2 size) { r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.pivot = new Vector2(0.5f, 0.5f); r.anchoredPosition = Vector2.zero; r.sizeDelta = size; }

        private static TMP_FontAsset FindFont(string namePart)
        {
            foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                if (f != null && f.name.Contains(namePart)) return f;
            return null;
        }

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

        // a horizontal band (the scan line)
        private static Sprite BandSpriteH()
        {
            const int W = 4, H = 64;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "IntroScan" };
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
            {
                float v = (y + 0.5f) / H * 2f - 1f;
                byte a = (byte)(Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(v)), 3f) * 255f);
                for (int x = 0; x < W; x++) px[y * W + x] = new Color32(255, 255, 255, a);
            }
            t.SetPixels32(px); t.Apply();
            return Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
        }

        // a light ray: bright at its root, fading along its length and soft across
        private static Sprite BeamSprite()
        {
            const int W = 128, H = 16;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "IntroBeam" };
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = x / (W - 1f), v = (y + 0.5f) / H * 2f - 1f;
                    float a = Mathf.Pow(1f - u, 1.6f) * Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(v)), 1.5f);
                    px[y * W + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            t.SetPixels32(px); t.Apply();
            return Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(0f, 0.5f), 100f);
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

        // one grid cell: thin lines on two edges (tiled over the screen)
        private static Texture2D GridTexture()
        {
            const int N = 64;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "IntroGrid" };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    bool lineX = x == 0, lineY = y == 0;
                    bool dot = (x == 0 && y == 0);
                    byte a = dot ? (byte)255 : (lineX || lineY) ? (byte)120 : (byte)0;
                    px[y * N + x] = new Color32(255, 255, 255, a);
                }
            t.SetPixels32(px); t.Apply();
            return t;
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
