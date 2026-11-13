using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// Base of the full-screen lobby menus (Challenges, Battle Pass, Store): its own overlay canvas under the lobby HUD, the HUD fades out
    /// while it is open, and it draws the same top navigation, a big title, a row of tabs and a key hint footer. A subclass fills the
    /// content area in <see cref="BuildContent"/>. Black-and-white style, 1920x1080 reference, built at runtime.
    /// </summary>
    public abstract class LobbyMenuScreen : MonoBehaviour
    {
        // ---- palette ----
        protected static readonly Color Dim = new Color(1f, 1f, 1f, 0.6f);
        protected static readonly Color Faint = new Color(1f, 1f, 1f, 0.3f);
        protected static readonly Color Panel = new Color(0.07f, 0.075f, 0.09f, 1f);
        protected static readonly Color PanelHi = new Color(0.12f, 0.13f, 0.15f, 1f);
        protected static readonly Color Line = new Color(1f, 1f, 1f, 0.12f);

        // ---- layout (centre origin, 1920x1080) ----
        protected const float Left = -880f, Right = 880f, ContentTop = 330f, ContentBottom = -470f;

        private static readonly string[] NavNames = { "PLAY", "OPERATORS", "LOADOUT", "BATTLE PASS", "CHALLENGES", "STORE" };
        private static readonly float[] NavX = { 501f, 641f, 796f, 959f, 1136f, 1286f };   // label centres on screen, as in the lobby

        public bool IsOpen { get; private set; }
        /// <summary>True while open and on the frame it closed, so the lobby does not also act on the same Esc press.</summary>
        public bool BusyThisFrame => IsOpen || closedFrame == Time.frameCount;

        protected abstract string Title { get; }
        protected abstract string Subtitle { get; }
        protected abstract int NavIndex { get; }
        protected virtual string Hints => "ESC  BACK";
        protected abstract void BuildContent(RectTransform content);
        protected virtual void OnOpened() { }
        protected virtual void OnTick() { }
        protected virtual void OnKeys() { }

        protected TMP_FontAsset font;
        protected Sprite px, gradient;
        protected RectTransform root;
        private CanvasGroup group, hudGroup;
        private GameObject hud;
        private Action<int> onNavigate;
        private Action onBack;
        private int closedFrame = -1;
        private bool built;

        /// <summary>Creates a screen of type T on its own root object.</summary>
        public static T Create<T>(TMP_FontAsset fontAsset, GameObject hud, Action<int> navigate, Action back) where T : LobbyMenuScreen
        {
            var go = new GameObject("[Lobby] " + typeof(T).Name, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;   // above the lobby HUD canvas (50), which fades out while this is open
            var sc = go.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920f, 1080f);
            sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; sc.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            var comp = go.AddComponent<T>();
            comp.group = go.AddComponent<CanvasGroup>();
            comp.group.alpha = 0f;
            comp.font = fontAsset != null ? fontAsset : TMP_Settings.defaultFontAsset;
            comp.hud = hud; comp.onNavigate = navigate; comp.onBack = back;
            comp.root = (RectTransform)go.transform;
            go.SetActive(false);
            return comp;
        }

        // ---------------- open / close ----------------
        public void Open()
        {
            if (IsOpen) return;
            if (!built) { built = true; px = MakePixel(); gradient = MakeGradient(); BuildFrame(); }
            IsOpen = true;
            gameObject.SetActive(true);
            EnsureEventSystem();
            if (hud != null && hudGroup == null)
            {
                hudGroup = hud.GetComponent<CanvasGroup>();
                if (hudGroup == null) hudGroup = hud.AddComponent<CanvasGroup>();
            }
            OnOpened();
        }

        /// <summary>Closes without telling the lobby (it already knows, because it is switching to another tab).</summary>
        public void CloseQuietly()
        {
            if (!IsOpen) return;
            IsOpen = false;
            closedFrame = Time.frameCount;
        }

        private void GoBack()
        {
            if (!IsOpen) return;
            CloseQuietly();
            if (onBack != null) onBack();
        }

        protected virtual void OnDestroy()
        {
            if (hudGroup != null) { hudGroup.alpha = 1f; hudGroup.blocksRaycasts = true; hudGroup.interactable = true; }
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (group == null) return;
            group.alpha = Mathf.MoveTowards(group.alpha, IsOpen ? 1f : 0f, dt * 5f);
            group.blocksRaycasts = IsOpen;
            if (hudGroup != null)   // runs only while the screen is active, including the fade-out that hands the lobby back
            {
                hudGroup.alpha = 1f - group.alpha;
                bool live = group.alpha < 0.05f;
                hudGroup.blocksRaycasts = live; hudGroup.interactable = live;
            }
            if (!IsOpen)
            {
                if (group.alpha <= 0f) gameObject.SetActive(false);
                return;
            }

            OnTick();
            if (Input.GetKeyDown(KeyCode.Escape)) { GoBack(); return; }
            OnKeys();
        }

        // ---------------- frame ----------------
        private void BuildFrame()
        {
            var bg = Img("Bg", root, new Color(0.025f, 0.03f, 0.04f, 0.995f)); Stretch(bg.rectTransform); bg.raycastTarget = true;
            var glow = Img("Glow", root, new Color(1f, 1f, 1f, 0.025f)); glow.sprite = gradient;
            var gr = glow.rectTransform; gr.anchorMin = new Vector2(0f, 0.5f); gr.anchorMax = new Vector2(1f, 1f); gr.offsetMin = gr.offsetMax = Vector2.zero;

            // top navigation, in the same places as the lobby's own
            for (int i = 0; i < NavNames.Length; i++)
            {
                int index = i;
                bool on = i == NavIndex;
                float w = i == 0 ? 110f : 150f;
                var tab = Img("Nav " + NavNames[i], root, on ? Color.white : new Color(1f, 1f, 1f, 0f));
                Place(tab.rectTransform, new Vector2(NavX[i] - 960f, 503f), new Vector2(w, 48f));
                tab.raycastTarget = true;
                var btn = tab.gameObject.AddComponent<Button>(); btn.targetGraphic = tab; btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() => { if (index != NavIndex && onNavigate != null) onNavigate(index); });
                var t = Txt("Label", tab.rectTransform, NavNames[i], 19, on ? new Color(0.05f, 0.055f, 0.065f, 1f) : new Color(0.96f, 0.96f, 0.97f, 0.78f), TextAlignmentOptions.Center);
                t.fontStyle = FontStyles.Bold; t.characterSpacing = 4f; Stretch(t.rectTransform);
                if (!on) { var hover = tab.gameObject.AddComponent<HoverTint>(); hover.target = t; }
            }
            var logo = Txt("Logo", root, "RAILBOUND", 30, Color.white, TextAlignmentOptions.Left);
            logo.fontStyle = FontStyles.Bold; logo.characterSpacing = 6f; Place(logo.rectTransform, new Vector2(-724f, 503f), new Vector2(400f, 44f));

            var rule = Img("NavRule", root, Line); Place(rule.rectTransform, new Vector2(0, 468f), new Vector2(1920f, 2f));

            // title block
            var title = Txt("Title", root, Title, 64, Color.white, TextAlignmentOptions.Left);
            title.fontStyle = FontStyles.Bold; title.characterSpacing = 12f; Place(title.rectTransform, new Vector2(Left + 450f, 410f), new Vector2(900f, 80f));
            var sub = Txt("Subtitle", root, Subtitle, 17, Dim, TextAlignmentOptions.Left);
            sub.characterSpacing = 4f; Place(sub.rectTransform, new Vector2(Left + 450f, 360f), new Vector2(900f, 26f));

            // content and footer
            var content = NewRect("Content", root, new Vector2(0.5f, 0.5f), new Vector2(Right - Left, ContentTop - ContentBottom), new Vector2(0f, (ContentTop + ContentBottom) * 0.5f - 20f));
            BuildContent(content);

            var foot = Txt("Hints", root, Hints, 14, Faint, TextAlignmentOptions.Center);
            foot.characterSpacing = 4f; Place(foot.rectTransform, new Vector2(0, -508f), new Vector2(1500f, 24f));
            var back = MakeButton(root, "BACK   ESC", new Vector2(790f, 410f), new Vector2(200f, 44f), false, GoBack);
            back.name = "Back Button";
        }

        private sealed class HoverTint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public TextMeshProUGUI target;
            public void OnPointerEnter(PointerEventData e) { if (target != null) target.color = Color.white; }
            public void OnPointerExit(PointerEventData e) { if (target != null) target.color = new Color(0.96f, 0.96f, 0.97f, 0.78f); }
        }

        // ---------------- helpers for subclasses ----------------
        protected static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            new GameObject("[Lobby] EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        }

        protected Button MakeButton(RectTransform parent, string label, Vector2 pos, Vector2 size, bool primary, UnityEngine.Events.UnityAction onClick, float fontSize = 16f)
        {
            var bg = Img(label + " Button", parent, primary ? Color.white : new Color(1f, 1f, 1f, 0.14f));
            Place(bg.rectTransform, pos, size);
            bg.raycastTarget = true;
            var btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            var c = btn.colors;
            c.normalColor = Color.white;
            c.highlightedColor = primary ? new Color(0.82f, 0.82f, 0.82f, 1f) : new Color(1.6f, 1.6f, 1.6f, 1f);
            c.pressedColor = primary ? new Color(0.6f, 0.6f, 0.6f, 1f) : new Color(2.2f, 2.2f, 2.2f, 1f);
            c.disabledColor = Color.white;
            c.fadeDuration = 0.08f;
            btn.colors = c;
            btn.onClick.AddListener(onClick);
            var t = Txt("Label", bg.rectTransform, label, fontSize, primary ? Color.black : Color.white, TextAlignmentOptions.Center);
            t.fontStyle = FontStyles.Bold; t.characterSpacing = 6f; Stretch(t.rectTransform);
            return btn;
        }

        /// <summary>A row of tab buttons; returns their backgrounds and labels so the caller can mark the active one.</summary>
        protected void BuildTabs(RectTransform parent, string[] names, float x, float y, float tabW, Action<int> onPick, List<Image> bgs, List<TextMeshProUGUI> labels)
        {
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                var bg = Img("Tab " + i, parent, PanelHi);
                Place(bg.rectTransform, new Vector2(x + tabW * 0.5f + i * (tabW + 8f), y), new Vector2(tabW, 48f));
                bg.raycastTarget = true;
                var btn = bg.gameObject.AddComponent<Button>(); btn.targetGraphic = bg; btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() => onPick(index));
                var l = Txt("Label", bg.rectTransform, names[i], 17, Dim, TextAlignmentOptions.Center);
                l.fontStyle = FontStyles.Bold; l.characterSpacing = 6f; Stretch(l.rectTransform);
                bgs.Add(bg); labels.Add(l);
            }
        }

        protected static void MarkTabs(List<Image> bgs, List<TextMeshProUGUI> labels, int active)
        {
            for (int i = 0; i < bgs.Count; i++)
            {
                bool on = i == active;
                bgs[i].color = on ? Color.white : PanelHi;
                labels[i].color = on ? Color.black : Dim;
            }
        }

        protected TextMeshProUGUI Txt(string name, RectTransform parent, string text, float size, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.font = font; t.text = text; t.fontSize = size; t.color = color; t.alignment = align;
            t.raycastTarget = false; t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        /// <summary>A label: <paramref name="left"/> is its left edge, <paramref name="y"/> its centre (both relative to the parent's centre).</summary>
        protected TextMeshProUGUI LabelL(RectTransform parent, string text, float size, bool bold, Color color, float left, float y, float width, float height = 0f)
        {
            if (height <= 0f) height = size * 1.5f;
            var t = Txt("Label", parent, text, size, color, TextAlignmentOptions.Left);
            if (bold) t.fontStyle = FontStyles.Bold;
            t.characterSpacing = bold ? 8f : 2.5f;
            Place(t.rectTransform, new Vector2(left + width * 0.5f, y), new Vector2(width, height));
            return t;
        }

        protected TextMeshProUGUI LabelC(RectTransform parent, string text, float size, bool bold, Color color, Vector2 pos, Vector2 box)
        {
            var t = Txt("Label", parent, text, size, color, TextAlignmentOptions.Center);
            if (bold) t.fontStyle = FontStyles.Bold;
            t.characterSpacing = bold ? 8f : 2.5f;
            Place(t.rectTransform, pos, box);
            return t;
        }

        protected TextMeshProUGUI LabelR(RectTransform parent, string text, float size, bool bold, Color color, float right, float y, float width, float height = 0f)
        {
            if (height <= 0f) height = size * 1.5f;
            var t = Txt("Label", parent, text, size, color, TextAlignmentOptions.Right);
            if (bold) t.fontStyle = FontStyles.Bold;
            t.characterSpacing = bold ? 8f : 2.5f;
            Place(t.rectTransform, new Vector2(right - width * 0.5f, y), new Vector2(width, height));
            return t;
        }

        protected Image Img(string name, RectTransform parent, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var i = go.GetComponent<Image>(); i.sprite = px; i.color = c; i.raycastTarget = false;
            return i;
        }

        protected static RectTransform NewRect(string name, RectTransform parent, Vector2 anchor, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            Place(r, pos, size);
            r.anchorMin = r.anchorMax = anchor;
            return r;
        }

        protected static void Place(RectTransform r, Vector2 pos, Vector2 size)
        {
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = pos; r.sizeDelta = size;
        }

        protected static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero; }

        /// <summary>A bar: dark track and a white fill from the left, <paramref name="amount"/> 0..1.</summary>
        protected Image Bar(RectTransform parent, Vector2 pos, Vector2 size, float amount, Color fill)
        {
            var track = Img("Track", parent, new Color(1f, 1f, 1f, 0.12f));
            Place(track.rectTransform, pos, size);
            var f = Img("Fill", track.rectTransform, fill);
            var fr = f.rectTransform; fr.anchorMin = new Vector2(0f, 0f); fr.anchorMax = new Vector2(Mathf.Clamp01(amount), 1f); fr.offsetMin = fr.offsetMax = Vector2.zero;
            return f;
        }

        /// <summary>Time left until the next midnight UTC (the daily reset), for example "11H 34M".</summary>
        protected static string TimeToMidnight()
        {
            var left = DateTime.UtcNow.Date.AddDays(1) - DateTime.UtcNow;
            return (int)left.TotalHours + "H " + left.Minutes.ToString("00") + "M";
        }

        private static Sprite MakePixel()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var cols = new Color32[16]; for (int i = 0; i < cols.Length; i++) cols[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(cols); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
        }

        // white, transparent at the bottom and opaque at the top (tinted by the Image colour)
        private static Sprite MakeGradient()
        {
            const int h = 64;
            var tex = new Texture2D(2, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var cols = new Color32[2 * h];
            for (int y = 0; y < h; y++)
            {
                float a = Mathf.Pow(y / (float)(h - 1), 1.4f);
                for (int x = 0; x < 2; x++) cols[y * 2 + x] = new Color(1f, 1f, 1f, a);
            }
            tex.SetPixels32(cols); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 2, h), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
