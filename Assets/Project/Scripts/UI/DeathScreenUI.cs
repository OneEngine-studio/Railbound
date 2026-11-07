using System;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// Death / elimination screen: the world drains of colour, cinematic bars slide in, a dark vignette closes the edges and a
    /// large ELIMINATED title with a thin countdown line fades up. Monochrome to match the rest of the UI. Built at runtime
    /// and shown by <see cref="Railbound.Gameplay.Map1DeathRestartController"/>; everything is driven by unscaled time so it
    /// plays correctly during the death slow-motion.
    /// </summary>
    public class DeathScreenUI : MonoBehaviour
    {
        private static DeathScreenUI active;

        /// <summary>True while the death screen is up (the in-game HUD fades out meanwhile).</summary>
        public static bool IsShowing => active != null;

        /// <summary>Show the screen. <paramref name="remaining"/> returns the seconds left before the restart.</summary>
        public static DeathScreenUI Show(float total, Func<float> remaining, string destination)
        {
            if (active != null) Destroy(active.gameObject);
            var go = new GameObject("[UI] Death Screen");
            active = go.AddComponent<DeathScreenUI>();
            active.Begin(total, remaining, destination);
            return active;
        }

        private const float BarHeight = 88f;
        private static readonly Color Dim = new Color(1f, 1f, 1f, 0.55f);

        private Func<float> remaining;
        private float total = 3f, startTime, surviveSeconds;
        private string destination = "";

        private CanvasGroup rootGroup, contentGroup;
        private RectTransform barTop, barBottom, fill;
        private TextMeshProUGUI title, countdown, stats, rule2;
        private RectTransform rule;
        private Image vignette;
        private Volume gradeVolume;
        private ColorAdjustments grade;
        private TMP_FontAsset fontBold, fontSemi;
        private Sprite px;
        private int lastSecond = -1;
        private float tickStart = -10f;

        private void Begin(float totalSeconds, Func<float> remainingSeconds, string dest)
        {
            total = Mathf.Max(0.5f, totalSeconds);
            remaining = remainingSeconds;
            destination = (dest ?? "").Replace('_', ' ').ToUpperInvariant();
            startTime = Time.unscaledTime;
            surviveSeconds = Time.timeSinceLevelLoad;

            fontBold = FindFont("Rajdhani-Bold") ?? TMP_Settings.defaultFontAsset;
            fontSemi = FindFont("Rajdhani-SemiBold") ?? fontBold;
            px = MakePixel();
            BuildGrade();
            BuildUI();
        }

        private void OnDestroy()
        {
            if (gradeVolume != null) Destroy(gradeVolume.gameObject);
            if (active == this) active = null;
        }

        private void Update()
        {
            float t = Time.unscaledTime - startTime;

            // 1. world drains of colour and darkens
            if (gradeVolume != null) gradeVolume.weight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 1.1f));

            // 2. bars + vignette
            float bars = EaseOut(Mathf.Clamp01(t / 0.7f));
            barTop.anchoredPosition = new Vector2(0, Mathf.Lerp(BarHeight, 0f, bars));
            barBottom.anchoredPosition = new Vector2(0, Mathf.Lerp(-BarHeight, 0f, bars));
            vignette.color = new Color(0, 0, 0, Mathf.Clamp01(t / 0.9f) * 0.92f);

            // 3. content fades up a beat after the bars; the title tracking settles
            float c = Mathf.Clamp01((t - 0.45f) / 0.7f);
            contentGroup.alpha = c;
            title.characterSpacing = Mathf.Lerp(46f, 20f, EaseOut(c));
            rule.sizeDelta = new Vector2(Mathf.Lerp(0f, 460f, EaseOut(Mathf.Clamp01((t - 0.7f) / 0.6f))), 2f);

            // 4. countdown text and the thin line that drains
            float left = remaining != null ? Mathf.Max(0f, remaining()) : 0f;
            int sec = Mathf.CeilToInt(left);
            if (sec != lastSecond) { lastSecond = sec; tickStart = Time.unscaledTime; }
            float k = Time.unscaledTime - tickStart;
            string num = "<size=" + (k < 0.18f ? Mathf.Lerp(30f, 24f, k / 0.18f).ToString("0") : "24") + ">" + Mathf.Max(1, sec) + "</size>";
            countdown.text = "REDEPLOYING IN  <color=#FFFFFF>" + num + "</color>";
            fill.anchorMax = new Vector2(Mathf.Clamp01(left / total), 1f);
            stats.text = "SURVIVED  <color=#FFFFFF>" + FormatTime(surviveSeconds) + "</color>";
            rule2.text = destination.Length > 0 ? "NEXT  " + destination : "";
        }

        // ---------------- build ----------------
        private void BuildGrade()
        {
            // global post-process volume: desaturate + slight darken, faded in by weight
            var go = new GameObject("[Death] Grade"); go.transform.SetParent(transform, false);
            gradeVolume = go.AddComponent<Volume>(); gradeVolume.isGlobal = true; gradeVolume.priority = 1000f; gradeVolume.weight = 0f;
            gradeVolume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
            grade = gradeVolume.profile.Add<ColorAdjustments>(true);
            grade.saturation.Override(-100f);
            grade.contrast.Override(18f);
            grade.postExposure.Override(-0.9f);
        }

        private void BuildUI()
        {
            var cgo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup)); cgo.transform.SetParent(transform, false);
            var canvas = cgo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 200;
            var sc = cgo.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
            rootGroup = cgo.GetComponent<CanvasGroup>(); rootGroup.blocksRaycasts = false; rootGroup.interactable = false;
            var root = (RectTransform)cgo.transform;

            // dark radial vignette
            vignette = NewImage("Vignette", root, MakeVignette(), new Color(0, 0, 0, 0)); Stretch(vignette.rectTransform);
            // cinematic bars
            barTop = Bar("Bar Top", root, true);
            barBottom = Bar("Bar Bottom", root, false);

            // centred content
            var content = NewRect("Content", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(1000, 360));
            contentGroup = content.gameObject.AddComponent<CanvasGroup>(); contentGroup.alpha = 0f;

            title = NewText("Title", content, "ELIMINATED", 96, fontBold, Color.white, 46f, TextAlignmentOptions.Center);
            var tr = title.rectTransform; tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0.5f); tr.pivot = new Vector2(0.5f, 0.5f); tr.anchoredPosition = new Vector2(0, 60); tr.sizeDelta = new Vector2(1000, 120);

            var ruleGo = NewImage("Rule", content, px, new Color(1, 1, 1, 0.35f)); rule = ruleGo.rectTransform;
            rule.anchorMin = rule.anchorMax = new Vector2(0.5f, 0.5f); rule.pivot = new Vector2(0.5f, 0.5f); rule.anchoredPosition = new Vector2(0, -12); rule.sizeDelta = new Vector2(0, 2);

            countdown = NewText("Countdown", content, "", 22, fontSemi, Dim, 8f, TextAlignmentOptions.Center); countdown.richText = true;
            var cr = countdown.rectTransform; cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0.5f); cr.pivot = new Vector2(0.5f, 0.5f); cr.anchoredPosition = new Vector2(0, -52); cr.sizeDelta = new Vector2(800, 40);

            // thin countdown line
            var track = NewImage("Track", content, px, new Color(1, 1, 1, 0.16f)); var trr = track.rectTransform;
            trr.anchorMin = trr.anchorMax = new Vector2(0.5f, 0.5f); trr.pivot = new Vector2(0.5f, 0.5f); trr.anchoredPosition = new Vector2(0, -92); trr.sizeDelta = new Vector2(360, 2);
            var f = NewImage("Fill", track.rectTransform, px, Color.white); fill = f.rectTransform; fill.anchorMin = new Vector2(0, 0); fill.anchorMax = new Vector2(1, 1); fill.pivot = new Vector2(0, 0.5f); fill.offsetMin = fill.offsetMax = Vector2.zero;

            // details inside the bottom bar
            stats = NewText("Stats", root, "", 18, fontSemi, Dim, 6f, TextAlignmentOptions.Left); stats.richText = true;
            var sr = stats.rectTransform; sr.anchorMin = sr.anchorMax = new Vector2(0, 0); sr.pivot = new Vector2(0, 0.5f); sr.anchoredPosition = new Vector2(90, BarHeight * 0.5f); sr.sizeDelta = new Vector2(600, 30);
            rule2 = NewText("Next", root, "", 18, fontSemi, Dim, 6f, TextAlignmentOptions.Right);
            var nr = rule2.rectTransform; nr.anchorMin = nr.anchorMax = new Vector2(1, 0); nr.pivot = new Vector2(1, 0.5f); nr.anchoredPosition = new Vector2(-90, BarHeight * 0.5f); nr.sizeDelta = new Vector2(600, 30);
            // the details ride on the bottom bar
            stats.transform.SetParent(barBottom, false); rule2.transform.SetParent(barBottom, false);
            sr.anchorMin = sr.anchorMax = new Vector2(0, 0.5f); sr.anchoredPosition = new Vector2(90, 0);
            nr.anchorMin = nr.anchorMax = new Vector2(1, 0.5f); nr.anchoredPosition = new Vector2(-90, 0);
        }

        private RectTransform Bar(string name, RectTransform parent, bool top)
        {
            var im = NewImage(name, parent, px, Color.black); var r = im.rectTransform;
            r.anchorMin = new Vector2(0, top ? 1 : 0); r.anchorMax = new Vector2(1, top ? 1 : 0); r.pivot = new Vector2(0.5f, top ? 1 : 0);
            r.sizeDelta = new Vector2(0, BarHeight); r.anchoredPosition = new Vector2(0, top ? BarHeight : -BarHeight);
            return r;
        }

        // ---------------- helpers ----------------
        private static float EaseOut(float x) { x = Mathf.Clamp01(x); return 1f - (1f - x) * (1f - x) * (1f - x); }

        private static string FormatTime(float s)
        {
            int m = Mathf.FloorToInt(s / 60f); int sec = Mathf.FloorToInt(s % 60f);
            return m.ToString("00") + ":" + sec.ToString("00");
        }

        private static TMP_FontAsset FindFont(string namePart)
        {
            foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                if (f != null && f.name.Contains(namePart)) return f;
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
                    float d = Mathf.Sqrt(dx * dx * 0.8f + dy * dy);               // wider than tall
                    float a = Mathf.SmoothStep(0.25f, 1.25f, d);
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

        private static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero; }
    }
}
