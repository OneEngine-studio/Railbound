using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// The level-up popup: a dark dim, slowly turning light rays, a flash line, "LEVEL UP" opening up letter by letter, the level number
    /// rolling from the old level to the new one with a punch and a burst of sparks for each level, then the reward (coins counting up)
    /// and the XP bar of the new level, and a CONTINUE button. Driven by one timeline on unscaled time, built at runtime in the
    /// black-and-white style of the lobby. Enter, Space, Esc or the button close it.
    /// </summary>
    public class LobbyLevelUpPopup : MonoBehaviour
    {
        private static readonly Color Dim = new Color(1f, 1f, 1f, 0.6f);
        private static readonly Color Faint = new Color(1f, 1f, 1f, 0.3f);

        public bool IsOpen { get; private set; }

        private TMP_FontAsset font;
        private Sprite px, soft;
        private Canvas canvasComp;
        private RectTransform root, raysRoot, content, rewardBlock, barBlock;
        private CanvasGroup group, rewardGroup, barGroup, buttonGroup;
        private Image flashLine, flashFull, barFill;
        private TextMeshProUGUI title, levelNumber, levelCaption, coinText, coinLabel, barText, barCaption;
        private Button continueButton;

        private int fromLevel, toLevel, bonusCoins;
        private float t;                   // seconds since the popup opened
        private int shownLevel;
        private int levelsDone;
        private float punchAt = -10f;
        private bool closing;
        private float closeT;
        private Action onClosed, playSound;

        // sparks
        private sealed class Spark { public RectTransform rt; public Image img; public Vector2 pos, vel; public float age, life, size; public bool alive; }
        private readonly List<Spark> sparks = new List<Spark>();

        private const float StepStart = 1.15f, StepGap = 0.6f;

        public static LobbyLevelUpPopup Create(TMP_FontAsset fontAsset)
        {
            var go = new GameObject("[Lobby] Level Up Popup");
            var p = go.AddComponent<LobbyLevelUpPopup>();
            p.font = fontAsset != null ? fontAsset : TMP_Settings.defaultFontAsset;
            p.Build();
            return p;
        }

        // ---------------- show / hide ----------------
        public void Show(int from, int to, int bonus, Action closed, Action sound)
        {
            fromLevel = from; toLevel = Mathf.Max(to, from + 1); bonusCoins = bonus;
            onClosed = closed; playSound = sound;
            t = 0f; closing = false; levelsDone = 0; shownLevel = fromLevel; punchAt = -10f;
            foreach (var s in sparks) { s.alive = false; s.rt.gameObject.SetActive(false); }

            levelNumber.text = fromLevel.ToString();
            coinText.text = "+ 0  <size=45%><color=#FFFFFF99>COINS</color></size>";
            title.text = "LEVEL UP";
            rewardGroup.alpha = 0f; barGroup.alpha = 0f; buttonGroup.alpha = 0f;
            buttonGroup.interactable = false; buttonGroup.blocksRaycasts = false;
            flashFull.color = new Color(1f, 1f, 1f, 0f);
            group.alpha = 0f;

            barText.text = PlayerProgression.Current.level >= PlayerProgression.MaxLevel ? "MAX LEVEL" : "";
            barFill.rectTransform.anchorMax = new Vector2(0f, 1f);

            IsOpen = true;
            canvasComp.enabled = true;
            gameObject.SetActive(true);
            EnsureEventSystem();
            if (playSound != null) playSound();
        }

        private void Close()
        {
            if (closing) return;
            closing = true; closeT = 0f;
            buttonGroup.interactable = false; buttonGroup.blocksRaycasts = false;
        }

        // ---------------- timeline ----------------
        private void Update()
        {
            if (!IsOpen) return;
            float dt = Time.unscaledDeltaTime;
            t += dt;
            int steps = Mathf.Max(1, toLevel - fromLevel);

            if (closing)
            {
                closeT += dt;
                group.alpha = Mathf.Clamp01(1f - closeT / 0.35f);
                if (closeT >= 0.35f)
                {
                    IsOpen = false; canvasComp.enabled = false; gameObject.SetActive(false);
                    var cb = onClosed; onClosed = null;
                    if (cb != null) cb();
                }
                TickSparks(dt);
                return;
            }

            group.alpha = Mathf.Clamp01(t / 0.35f);

            // light rays: grow in, then turn slowly
            float rayIn = EaseOut(Mathf.Clamp01(t / 0.9f));
            raysRoot.localScale = Vector3.one * Mathf.Lerp(0.2f, 1f, rayIn);
            raysRoot.localRotation = Quaternion.Euler(0f, 0f, t * 6f);
            SetGroupAlpha(raysRoot, rayIn);

            // the flash line across the screen
            float lineT = Mathf.Clamp01((t - 0.12f) / 0.45f);
            var lr = flashLine.rectTransform;
            lr.localScale = new Vector3(EaseOut(lineT), 1f, 1f);
            flashLine.color = new Color(1f, 1f, 1f, Mathf.Sin(lineT * Mathf.PI) * 0.9f);

            // "LEVEL UP" opens up
            float titleT = EaseOut(Mathf.Clamp01((t - 0.3f) / 0.8f));
            title.characterSpacing = Mathf.Lerp(70f, 22f, titleT);
            title.color = new Color(1f, 1f, 1f, Mathf.Clamp01(titleT * 1.5f));

            // the level number: appears with an overshoot, then ticks up once per level
            float numT = Mathf.Clamp01((t - 0.55f) / 0.5f);
            float scale = Mathf.Lerp(0.4f, 1f, EaseOutBack(numT));
            levelCaption.color = new Color(1f, 1f, 1f, Mathf.Clamp01(numT) * 0.6f);
            levelNumber.color = new Color(1f, 1f, 1f, Mathf.Clamp01(numT * 2f));

            while (levelsDone < steps && t >= StepStart + levelsDone * StepGap)
            {
                levelsDone++;
                shownLevel = fromLevel + levelsDone;
                levelNumber.text = shownLevel.ToString();
                punchAt = t;
                Burst(levelsDone == steps ? 46 : 22, levelsDone == steps ? 520f : 360f);
                flashFull.color = new Color(1f, 1f, 1f, levelsDone == steps ? 0.38f : 0.2f);
                if (playSound != null && levelsDone > 1) playSound();
            }
            float punch = t - punchAt;
            if (punch >= 0f && punch < 0.45f) scale *= 1f + 0.38f * Mathf.Exp(-punch * 9f) * Mathf.Cos(punch * 16f);
            levelNumber.rectTransform.localScale = new Vector3(scale, scale, 1f);
            flashFull.color = Color.Lerp(flashFull.color, new Color(1f, 1f, 1f, 0f), dt * 6f);

            // after the last level: reward, XP bar, button
            float doneAt = StepStart + (steps - 1) * StepGap + 0.45f;
            float rewardT = Mathf.Clamp01((t - doneAt) / 0.5f);
            rewardGroup.alpha = rewardT;
            rewardBlock.anchoredPosition = new Vector2(0f, Mathf.Lerp(-18f, 0f, EaseOut(rewardT)));
            if (rewardT > 0f)
            {
                float count = EaseOut(Mathf.Clamp01((t - doneAt - 0.2f) / 1.0f));
                coinText.text = "+ " + Mathf.RoundToInt(bonusCoins * count).ToString("N0") + "  <size=45%><color=#FFFFFF99>COINS</color></size>";
            }

            float barT = Mathf.Clamp01((t - doneAt - 0.35f) / 0.5f);
            barGroup.alpha = barT;
            if (barT > 0f)
            {
                float fill = EaseOut(Mathf.Clamp01((t - doneAt - 0.5f) / 0.9f)) * PlayerProgression.XpFill;
                barFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(fill), 1f);
                var d = PlayerProgression.Current;
                barText.text = d.level >= PlayerProgression.MaxLevel ? "MAX LEVEL" : Mathf.RoundToInt(d.xp * Mathf.Clamp01(fill / Mathf.Max(0.0001f, PlayerProgression.XpFill))).ToString("N0") + " / " + PlayerProgression.XpForLevel(d.level).ToString("N0") + " XP";
            }

            float btnT = Mathf.Clamp01((t - doneAt - 0.9f) / 0.4f);
            buttonGroup.alpha = btnT;
            bool clickable = btnT > 0.6f;
            buttonGroup.interactable = clickable; buttonGroup.blocksRaycasts = clickable;

            TickSparks(dt);

            // keys close it once the button has appeared
            if (btnT > 0.6f && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape)))
                Close();
        }

        // ---------------- sparks ----------------
        private void Burst(int count, float speed)
        {
            for (int i = 0; i < count; i++)
            {
                var s = GetSpark();
                float a = UnityEngine.Random.value * Mathf.PI * 2f;
                float v = speed * (0.35f + UnityEngine.Random.value * 0.9f);
                s.pos = new Vector2(0f, 20f);
                s.vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * v;
                s.age = 0f; s.life = 0.8f + UnityEngine.Random.value * 0.9f;
                s.size = 3f + UnityEngine.Random.value * 7f;
                s.alive = true;
                s.rt.gameObject.SetActive(true);
                s.rt.sizeDelta = new Vector2(s.size, s.size);
                s.rt.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.value * 90f);
            }
        }

        private Spark GetSpark()
        {
            foreach (var s in sparks) if (!s.alive) return s;
            var go = new GameObject("Spark", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(content, false);
            var img = go.GetComponent<Image>(); img.sprite = px; img.raycastTarget = false;
            var rt = (RectTransform)go.transform; rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            var sp = new Spark { rt = rt, img = img };
            sparks.Add(sp);
            return sp;
        }

        private void TickSparks(float dt)
        {
            foreach (var s in sparks)
            {
                if (!s.alive) continue;
                s.age += dt;
                if (s.age >= s.life) { s.alive = false; s.rt.gameObject.SetActive(false); continue; }
                s.vel *= Mathf.Exp(-1.8f * dt);
                s.vel.y -= 160f * dt;
                s.pos += s.vel * dt;
                s.rt.anchoredPosition = s.pos;
                float k = s.age / s.life;
                s.img.color = new Color(1f, 1f, 1f, (1f - k) * (1f - k));
            }
        }

        // ---------------- build ----------------
        private void Build()
        {
            px = MakePixel();
            soft = px;

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            canvasGo.transform.SetParent(transform, false);
            canvasComp = canvasGo.GetComponent<Canvas>(); canvasComp.renderMode = RenderMode.ScreenSpaceOverlay; canvasComp.overrideSorting = true; canvasComp.sortingOrder = 890;
            var sc = canvasGo.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
            group = canvasGo.GetComponent<CanvasGroup>();
            root = (RectTransform)canvasGo.transform;

            var dim = Img("Dim", root, new Color(0.01f, 0.012f, 0.016f, 0.9f)); Stretch(dim.rectTransform); dim.raycastTarget = true;

            // rays
            raysRoot = NewRect("Rays", root, Vector2.zero, Vector2.zero);
            raysRoot.gameObject.AddComponent<CanvasGroup>();
            var wedge = MakeWedge();
            const int beams = 16;
            for (int i = 0; i < beams; i++)
            {
                bool big = i % 2 == 0;
                var ray = Img("Ray", raysRoot, new Color(1f, 1f, 1f, big ? 0.11f : 0.06f));
                ray.sprite = wedge;
                var rr = ray.rectTransform; rr.anchorMin = rr.anchorMax = new Vector2(0.5f, 0.5f); rr.pivot = new Vector2(0f, 0.5f);
                rr.sizeDelta = new Vector2(big ? 1250f : 850f, big ? 190f : 120f);
                rr.localRotation = Quaternion.Euler(0f, 0f, i * (360f / beams));
            }

            content = NewRect("Content", root, new Vector2(0f, 20f), Vector2.zero);

            flashLine = Img("FlashLine", content, Color.white);
            var fl = flashLine.rectTransform; fl.sizeDelta = new Vector2(1920f, 3f); fl.anchoredPosition = new Vector2(0f, 20f);

            title = Txt("Title", content, "LEVEL UP", 46, true, Color.white, new Vector2(0f, 250f), new Vector2(1200f, 70f));
            levelCaption = Txt("Caption", content, "YOU REACHED LEVEL", 16, false, Dim, new Vector2(0f, 190f), new Vector2(700f, 26f));
            levelCaption.characterSpacing = 10f;
            levelNumber = Txt("Level", content, "1", 300, true, Color.white, new Vector2(0f, 40f), new Vector2(900f, 340f));
            levelNumber.characterSpacing = 0f;

            var rule = Img("Rule", content, new Color(1f, 1f, 1f, 0.4f)); Place(rule.rectTransform, new Vector2(0f, -150f), new Vector2(520f, 2f));

            // reward block
            rewardBlock = NewRect("Reward", content, new Vector2(0f, 0f), Vector2.zero);
            rewardGroup = rewardBlock.gameObject.AddComponent<CanvasGroup>();
            coinLabel = Txt("CoinLabel", rewardBlock, "LEVEL REWARD", 15, false, Dim, new Vector2(0f, -190f), new Vector2(500f, 24f));
            coinLabel.characterSpacing = 8f;
            coinText = Txt("Coins", rewardBlock, "+ 0", 50, true, new Color(1f, 0.86f, 0.3f, 1f), new Vector2(0f, -240f), new Vector2(700f, 64f));
            coinText.richText = true; coinText.characterSpacing = 4f;

            // XP bar of the new level
            barBlock = NewRect("Bar", content, new Vector2(0f, 0f), Vector2.zero);
            barGroup = barBlock.gameObject.AddComponent<CanvasGroup>();
            barCaption = Txt("BarCaption", barBlock, "PROGRESS TO THE NEXT LEVEL", 13, false, Faint, new Vector2(0f, -305f), new Vector2(600f, 22f));
            barCaption.characterSpacing = 6f;
            var track = Img("Track", barBlock, new Color(1f, 1f, 1f, 0.14f)); Place(track.rectTransform, new Vector2(0f, -334f), new Vector2(520f, 10f));
            barFill = Img("Fill", track.rectTransform, Color.white);
            var bf = barFill.rectTransform; bf.anchorMin = Vector2.zero; bf.anchorMax = new Vector2(0f, 1f); bf.offsetMin = bf.offsetMax = Vector2.zero;
            barText = Txt("BarText", barBlock, "", 15, true, Dim, new Vector2(0f, -362f), new Vector2(600f, 24f));
            barText.characterSpacing = 4f;

            // continue button
            var btnGo = Img("Continue", content, Color.white);
            Place(btnGo.rectTransform, new Vector2(0f, -430f), new Vector2(340f, 58f));
            btnGo.raycastTarget = true;
            buttonGroup = btnGo.gameObject.AddComponent<CanvasGroup>();
            continueButton = btnGo.gameObject.AddComponent<Button>(); continueButton.targetGraphic = btnGo;
            var cb = continueButton.colors; cb.normalColor = Color.white; cb.highlightedColor = new Color(0.82f, 0.82f, 0.82f, 1f); cb.pressedColor = new Color(0.6f, 0.6f, 0.6f, 1f); cb.fadeDuration = 0.08f; continueButton.colors = cb;
            continueButton.onClick.AddListener(Close);
            var bl = Txt("Label", btnGo.rectTransform, "CONTINUE", 22, true, Color.black, Vector2.zero, new Vector2(320f, 40f)); bl.characterSpacing = 10f;

            // the screen flash on each level
            flashFull = Img("Flash", root, new Color(1f, 1f, 1f, 0f)); Stretch(flashFull.rectTransform);

            canvasComp.enabled = false;
            gameObject.SetActive(false);
        }

        // ---------------- helpers ----------------
        private static float EaseOut(float x) { x = Mathf.Clamp01(x); return 1f - (1f - x) * (1f - x) * (1f - x); }
        private static float EaseOutBack(float x)
        {
            x = Mathf.Clamp01(x);
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }

        private static void SetGroupAlpha(RectTransform r, float a)
        {
            var g = r.GetComponent<CanvasGroup>(); if (g != null) g.alpha = a;
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.EventSystems.EventSystem.current != null) return;
            new GameObject("[Lobby] EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        }

        private TextMeshProUGUI Txt(string name, RectTransform parent, string text, float size, bool bold, Color color, Vector2 pos, Vector2 box)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.font = font; t.text = text; t.fontSize = size; t.color = color; t.alignment = TextAlignmentOptions.Center;
            if (bold) t.fontStyle = FontStyles.Bold;
            t.characterSpacing = bold ? 8f : 3f;
            t.raycastTarget = false; t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Overflow;
            Place(t.rectTransform, pos, box);
            return t;
        }

        private Image Img(string name, RectTransform parent, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var i = go.GetComponent<Image>(); i.sprite = px; i.color = c; i.raycastTarget = false;
            var r = (RectTransform)go.transform; r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            return i;
        }

        private static RectTransform NewRect(string name, RectTransform parent, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = pos; r.sizeDelta = size;
            return r;
        }

        private static void Place(RectTransform r, Vector2 pos, Vector2 size)
        {
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = pos; r.sizeDelta = size;
        }

        private static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero; }

        // a soft light beam: bright at the left (the centre of the screen), widening and fading towards the right
        private static Sprite MakeWedge()
        {
            const int w = 256, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var cols = new Color32[w * h];
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)(w - 1);
                float half = Mathf.Lerp(0.04f, 0.5f, u);                // half-height as a share of the texture, growing with distance
                float along = Mathf.Pow(1f - u, 1.6f);
                for (int y = 0; y < h; y++)
                {
                    float v = Mathf.Abs((y + 0.5f) / h - 0.5f);
                    float across = Mathf.Clamp01(1f - v / half);
                    across = across * across * (3f - 2f * across);      // smooth edges
                    cols[y * w + x] = new Color(1f, 1f, 1f, along * across);
                }
            }
            tex.SetPixels32(cols); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0f, 0.5f), 100f);
        }

        private static Sprite MakePixel()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var cols = new Color32[16]; for (int i = 0; i < cols.Length; i++) cols[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(cols); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
