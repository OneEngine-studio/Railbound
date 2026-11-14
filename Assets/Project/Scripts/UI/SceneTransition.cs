using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// One smooth way to change scene: fade to black, load in the background, hold until the new scene has settled, fade up from black.
    /// It lives across scene loads on its own overlay (above every other canvas), so Login -> Lobby -> Loading screen -> Map -> Lobby are
    /// all the same dip through black instead of a hard cut with a frozen frame. A small "RAILBOUND / LOADING" card with a sweeping line
    /// shows while the screen is black, so a long load never looks like a hang. The game also starts from black and fades in.
    /// Use <see cref="Go"/> instead of SceneManager.LoadScene; use <see cref="Cover"/> / <see cref="Reveal"/> when a screen of your own
    /// (the loading screen) wants to finish with a dip to black.
    /// </summary>
    public sealed class SceneTransition : MonoBehaviour
    {
        public struct Options
        {
            public string label;        // shown on the card while black, for example "ENTERING LOBBY" (null = no card)
            public float fadeOut;       // seconds to black
            public float fadeIn;        // seconds from black
            public float minBlack;      // the least time the screen stays black (hides short loads, lets the card be read)
            public static Options Default(string label) { return new Options { label = label, fadeOut = 0.35f, fadeIn = 0.7f, minBlack = 0.6f }; }
            public static Options Quick() { return new Options { label = null, fadeOut = 0.3f, fadeIn = 0.5f, minBlack = 0f }; }
        }

        private static SceneTransition instance;
        private CanvasGroup overlay, card;
        private RectTransform sweep;
        private TextMeshProUGUI cardLabel;
        private Coroutine routine;
        private bool loading;            // a scene is being loaded (a plain fade-in does not count: the next load may interrupt it)
        private float pendingReveal = -1f;
        private float sweepClock;

        /// <summary>True while a transition is running (a second request is ignored).</summary>
        public static bool Busy => instance != null && instance.loading;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            if (Application.isBatchMode) return;   // a dedicated server has no screen
            Ensure();
            // the very first scene appears from black
            instance.overlay.alpha = 1f;
            instance.routine = instance.StartCoroutine(instance.StartupFade());
        }

        private static SceneTransition Ensure()
        {
            if (instance != null) return instance;
            var go = new GameObject("[Scene Transition]");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<SceneTransition>();
            instance.Build();
            return instance;
        }

        // ---------------- public API ----------------
        /// <summary>Changes scene with a dip through black. Does nothing if a transition is already running or the scene is already active.</summary>
        public static void Go(string sceneName, Options options)
        {
            if (string.IsNullOrEmpty(sceneName)) return;
            if (Application.isBatchMode || !Application.isPlaying) { SceneManager.LoadScene(sceneName); return; }
            var t = Ensure();
            if (t.loading) return;
            if (SceneManager.GetActiveScene().name == sceneName) return;
            if (t.routine != null) t.StopCoroutine(t.routine);
            t.routine = t.StartCoroutine(t.GoRoutine(sceneName, options));
        }

        public static void Go(string sceneName, string label = null) { Go(sceneName, label != null ? Options.Default(label) : Options.Quick()); }

        /// <summary>Fades to black and waits for it (for screens that handle their own scene change).</summary>
        public static IEnumerator Cover(float seconds = 0.4f, string label = null)
        {
            var t = Ensure();
            t.SetLabel(label);
            yield return t.Fade(t.overlay.alpha, 1f, seconds);
        }

        /// <summary>Fades up from black as soon as the next scene has loaded (for a screen that is destroyed by the scene change itself).</summary>
        public static void RevealAfterNextLoad(float seconds = 0.9f)
        {
            var t = Ensure();
            t.pendingReveal = seconds;
        }

        /// <summary>Fades up from black (does not wait).</summary>
        public static void Reveal(float seconds = 0.9f)
        {
            var t = Ensure();
            if (t.routine != null) t.StopCoroutine(t.routine);
            t.routine = t.StartCoroutine(t.RevealRoutine(seconds));
        }

        // ---------------- routines ----------------
        private IEnumerator StartupFade()
        {
            yield return null; yield return null;
            yield return new WaitForSecondsRealtime(0.25f);
            yield return Fade(1f, 0f, 0.8f);
            routine = null;
        }

        private IEnumerator GoRoutine(string sceneName, Options o)
        {
            loading = true;
            SetLabel(o.label);
            yield return Fade(overlay.alpha, 1f, o.fadeOut);

            float black = 0f;
            if (!Application.CanStreamedLevelBeLoaded(sceneName)) { Debug.LogError("[SceneTransition] Scene '" + sceneName + "' is not in the build."); yield return Fade(1f, 0f, o.fadeIn); loading = false; routine = null; yield break; }
            var op = SceneManager.LoadSceneAsync(sceneName);
            op.allowSceneActivation = false;
            while (op.progress < 0.9f || black < o.minBlack) { black += Time.unscaledDeltaTime; yield return null; }
            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;

            // let the new scene settle: its first frames are heavy (shaders, lights, UI built at runtime) and must happen behind black
            for (int i = 0; i < 3; i++) yield return null;
            yield return new WaitForSecondsRealtime(0.25f);

            yield return Fade(1f, 0f, o.fadeIn);
            loading = false;
            routine = null;
        }

        private IEnumerator RevealRoutine(float seconds)
        {
            yield return null; yield return null;
            yield return Fade(overlay.alpha, 0f, seconds);
            routine = null;
        }

        private IEnumerator Fade(float from, float to, float seconds)
        {
            overlay.blocksRaycasts = true;
            float cardFrom = card.alpha, cardTo = to > from ? (cardLabel != null && !string.IsNullOrEmpty(cardLabel.text) ? 1f : 0f) : 0f;
            if (seconds <= 0.01f) { overlay.alpha = to; card.alpha = cardTo; }
            else
            {
                for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
                {
                    float k = t / seconds; k = k * k * (3f - 2f * k);   // smoothstep
                    overlay.alpha = Mathf.Lerp(from, to, k);
                    // the card comes in with the black, and leaves in the first third of the fade up, before the new scene shows through
                    card.alpha = to > from ? Mathf.Lerp(cardFrom, cardTo, k) : Mathf.Lerp(cardFrom, 0f, Mathf.Clamp01(k * 3f));
                    yield return null;
                }
                overlay.alpha = to; card.alpha = cardTo;
            }
            overlay.blocksRaycasts = to > 0.01f;
        }

        private void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
        private void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (pendingReveal < 0f || loading) return;
            float seconds = pendingReveal; pendingReveal = -1f;
            Reveal(seconds);
        }

        private void SetLabel(string label)
        {
            if (cardLabel == null) return;
            cardLabel.text = string.IsNullOrEmpty(label) ? "" : label;
        }

        private void Update()
        {
            // the line under the card sweeps while it is visible
            if (card == null || card.alpha < 0.01f) return;
            sweepClock += Time.unscaledDeltaTime;
            float k = Mathf.Repeat(sweepClock / 1.4f, 1f);
            float eased = k < 0.5f ? 2f * k * k : 1f - Mathf.Pow(-2f * k + 2f, 2f) / 2f;
            sweep.anchoredPosition = new Vector2(Mathf.Lerp(-170f, 170f, eased), 0f);
        }

        // ---------------- build ----------------
        private void Build()
        {
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.overrideSorting = true; canvas.sortingOrder = 32000;
            var sc = canvasGo.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
            overlay = canvasGo.GetComponent<CanvasGroup>(); overlay.alpha = 0f; overlay.blocksRaycasts = false;

            var px = MakePixel();
            var black = NewImage("Black", (RectTransform)canvasGo.transform, px, Color.black);
            Stretch(black.rectTransform); black.raycastTarget = true;

            var font = FindFont("Rajdhani-Bold") ?? TMP_Settings.defaultFontAsset;
            var cardRt = NewRect("Card", (RectTransform)canvasGo.transform, new Vector2(700f, 160f), Vector2.zero);
            card = cardRt.gameObject.AddComponent<CanvasGroup>(); card.alpha = 0f;

            var title = NewText("Title", cardRt, "RAILBOUND", 54, font, new Color(1f, 1f, 1f, 0.95f), new Vector2(0f, 34f), new Vector2(700f, 70f)); title.characterSpacing = 22f;
            cardLabel = NewText("Label", cardRt, "", 17, font, new Color(1f, 1f, 1f, 0.6f), new Vector2(0f, -26f), new Vector2(700f, 26f)); cardLabel.characterSpacing = 10f;

            var track = NewImage("Track", cardRt, px, new Color(1f, 1f, 1f, 0.12f));
            Place(track.rectTransform, new Vector2(0f, -62f), new Vector2(340f, 3f));
            track.gameObject.AddComponent<RectMask2D>();
            var bar = NewImage("Sweep", track.rectTransform, px, Color.white);
            Place(bar.rectTransform, Vector2.zero, new Vector2(120f, 3f));
            sweep = bar.rectTransform;
        }

        private static TMP_FontAsset FindFont(string namePart)
        {
            foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>()) if (f != null && f.name.Contains(namePart)) return f;
            return null;
        }

        private static TextMeshProUGUI NewText(string name, RectTransform parent, string text, float size, TMP_FontAsset font, Color color, Vector2 pos, Vector2 box)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.font = font; t.text = text; t.fontSize = size; t.color = color; t.alignment = TextAlignmentOptions.Center; t.fontStyle = FontStyles.Bold;
            t.raycastTarget = false; t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Overflow;
            Place(t.rectTransform, pos, box);
            return t;
        }

        private static Image NewImage(string name, RectTransform parent, Sprite px, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var i = go.GetComponent<Image>(); i.sprite = px; i.color = c; i.raycastTarget = false;
            return i;
        }

        private static RectTransform NewRect(string name, RectTransform parent, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform; Place(r, pos, size); return r;
        }

        private static void Place(RectTransform r, Vector2 pos, Vector2 size)
        {
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = pos; r.sizeDelta = size;
        }

        private static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero; }

        private static Sprite MakePixel()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var cols = new Color32[16]; for (int i = 0; i < cols.Length; i++) cols[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(cols); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
