using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// The lobby's Emotes section: a small "EMOTES [B]" button at the bottom centre that opens a radial wheel with
    /// the six emotes (click a slot or press 1-6; B / Esc closes). Replaces the old row of four chips.
    /// Built in code under the lobby canvas so it needs no scene setup.
    /// </summary>
    public class LobbyEmoteWheel : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }

        private CanvasGroup wheelGroup;
        private RectTransform wheelContent;
        private readonly Button[] slots = new Button[LobbyActorRig.EmoteCount];
        private float openT;
        private bool wantOpen;
        private Sprite chip, panel;
        private TMP_FontAsset font;

        private static readonly Color White = new Color(0.96f, 0.96f, 0.97f, 1f);
        private static readonly Color Dim = new Color(0.96f, 0.96f, 0.97f, 0.62f);
        private static readonly Color Yellow = new Color(0.97f, 0.975f, 1f, 1f);   // accent = white

        public static LobbyEmoteWheel Create(Transform canvasRoot, Transform launcherParent, TMP_FontAsset font)
        {
            var go = new GameObject("LobbyEmoteWheel");
            go.layer = canvasRoot.gameObject.layer;
            go.transform.SetParent(canvasRoot, false);
            var w = go.AddComponent<LobbyEmoteWheel>();
            w.font = font;
            w.Build(canvasRoot, launcherParent);
            return w;
        }

        private void Build(Transform canvasRoot, Transform launcherParent)
        {
            chip = Resources.Load<Sprite>("HUD/Hud_Chip");
            panel = Resources.Load<Sprite>("HUD/Hud_Panel");
            int layer = canvasRoot.gameObject.layer;

            // ---- launcher button (bottom centre) ----
            var launcher = NewUi("Btn_Emotes", launcherParent, layer);
            launcher.anchorMin = launcher.anchorMax = new Vector2(0.5f, 0f); launcher.pivot = new Vector2(0.5f, 0f);
            launcher.anchoredPosition = new Vector2(0f, 64f); launcher.sizeDelta = new Vector2(200f, 46f);
            var lImg = launcher.gameObject.AddComponent<Image>(); lImg.sprite = chip; lImg.type = Image.Type.Sliced; lImg.color = new Color(1f, 1f, 1f, 0.55f);
            var lBtn = launcher.gameObject.AddComponent<Button>(); lBtn.targetGraphic = lImg;
            lBtn.colors = Tint(0.55f, 0.9f, 1f);
            lBtn.onClick.AddListener(() => SetOpen(!wantOpen));
            var lt = Label("Label", launcher, layer, "EMOTES", 20, White, TextAlignmentOptions.Center);
            lt.rectTransform.anchorMin = Vector2.zero; lt.rectTransform.anchorMax = Vector2.one; lt.rectTransform.offsetMin = new Vector2(10f, 0f); lt.rectTransform.offsetMax = new Vector2(-52f, 0f);
            Key(launcher, layer, "B", new Vector2(-16f, 0f));

            // ---- wheel overlay ----
            var overlay = NewUi("EmoteWheel", transform, layer);
            Stretch(overlay);
            var cv = overlay.gameObject.AddComponent<Canvas>(); cv.overrideSorting = true; cv.sortingOrder = 600;
            overlay.gameObject.AddComponent<GraphicRaycaster>();
            wheelGroup = overlay.gameObject.AddComponent<CanvasGroup>(); wheelGroup.alpha = 0f; wheelGroup.blocksRaycasts = false; wheelGroup.interactable = false;

            var dim = NewUi("Dim", overlay, layer); Stretch(dim);
            var dimImg = dim.gameObject.AddComponent<Image>(); dimImg.color = new Color(0f, 0f, 0f, 0.55f);
            var dimBtn = dim.gameObject.AddComponent<Button>(); dimBtn.targetGraphic = dimImg; dimBtn.transition = Selectable.Transition.None; dimBtn.onClick.AddListener(() => SetOpen(false));

            wheelContent = NewUi("Content", overlay, layer);
            wheelContent.anchorMin = wheelContent.anchorMax = new Vector2(0.5f, 0.5f); wheelContent.sizeDelta = Vector2.zero;

            var title = Label("Title", wheelContent, layer, "EMOTES", 40, White, TextAlignmentOptions.Center);
            title.rectTransform.anchoredPosition = new Vector2(0f, 14f); title.rectTransform.sizeDelta = new Vector2(500f, 56f);
            var hint = Label("Hint", wheelContent, layer, "CLICK AN EMOTE  ·  1-6  ·  B / ESC TO CLOSE", 17, Dim, TextAlignmentOptions.Center);
            hint.rectTransform.anchoredPosition = new Vector2(0f, -28f); hint.rectTransform.sizeDelta = new Vector2(640f, 28f);

            for (int i = 0; i < LobbyActorRig.EmoteCount; i++)
            {
                float a = (90f - i * 60f) * Mathf.Deg2Rad;
                Vector2 pos = new Vector2(Mathf.Cos(a) * 330f, Mathf.Sin(a) * 215f);   // slightly wider than tall
                var slot = NewUi("Slot_" + LobbyActorRig.EmoteNames[i], wheelContent, layer);
                slot.anchoredPosition = pos; slot.sizeDelta = new Vector2(210f, 70f);
                var img = slot.gameObject.AddComponent<Image>(); img.sprite = panel; img.type = Image.Type.Sliced; img.color = Color.white;
                var btn = slot.gameObject.AddComponent<Button>(); btn.targetGraphic = img;
                var cb = Tint(1f, 1f, 1f); cb.highlightedColor = new Color(0.85f, 0.88f, 0.95f, 1f); cb.pressedColor = Yellow; btn.colors = cb;
                int id = i + 1; btn.onClick.AddListener(() => { PickEmote(id); });
                var name = Label("Name", slot, layer, LobbyActorRig.EmoteNames[i], 26, White, TextAlignmentOptions.Center);
                name.rectTransform.anchorMin = Vector2.zero; name.rectTransform.anchorMax = Vector2.one; name.rectTransform.offsetMin = new Vector2(46f, 0f); name.rectTransform.offsetMax = new Vector2(-8f, 0f);
                Key(slot, layer, (i + 1).ToString(), new Vector2(34f, 0f), left: true);
                slots[i] = btn;
            }

            overlay.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.B)) SetOpen(!wantOpen);
            if (wantOpen)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) SetOpen(false);
                for (int i = 0; i < LobbyActorRig.EmoteCount; i++)
                    if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i)) PickEmote(i + 1);
            }

            openT = Mathf.MoveTowards(openT, wantOpen ? 1f : 0f, Time.unscaledDeltaTime * 7f);
            float e = 1f - (1f - openT) * (1f - openT);
            if (wheelGroup != null)
            {
                wheelGroup.alpha = e;
                wheelContent.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, e);
                if (openT <= 0f && wheelGroup.gameObject.activeSelf) wheelGroup.gameObject.SetActive(false);
            }
        }

        private void SetOpen(bool open)
        {
            wantOpen = open; IsOpen = open;
            if (open && wheelGroup != null)
            {
                wheelGroup.gameObject.SetActive(true);
                wheelGroup.blocksRaycasts = true; wheelGroup.interactable = true;
            }
            else if (wheelGroup != null) { wheelGroup.blocksRaycasts = false; wheelGroup.interactable = false; }
        }

        private void PickEmote(int id)
        {
            LobbyCharacterPreviewManager.Instance?.PlayEmote(id);
            SetOpen(false);
        }

        private void OnDestroy() { IsOpen = false; }

        // ------------------------------------------------------------------ small UI helpers
        private static ColorBlock Tint(float normalA, float hover, float press)
        {
            var c = ColorBlock.defaultColorBlock;
            c.normalColor = new Color(1f, 1f, 1f, 1f); c.highlightedColor = new Color(1f, 1f, 1f, 1f) * hover; c.pressedColor = new Color(0.85f, 0.88f, 0.95f, 1f);
            c.selectedColor = c.normalColor; c.colorMultiplier = 1f; c.fadeDuration = 0.08f; return c;
        }

        private static RectTransform NewUi(string name, Transform parent, int layer)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = layer; go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero; }

        private TextMeshProUGUI Label(string name, Transform parent, int layer, string text, float size, Color col, TextAlignmentOptions align)
        {
            var r = NewUi(name, parent, layer);
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            var t = r.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.fontStyle = FontStyles.Bold; t.alignment = align; t.color = col; t.characterSpacing = 3f;
            t.enableWordWrapping = false; t.raycastTarget = false;
            if (font != null) t.font = font;
            return t;
        }

        /// <summary>Small keycap (light plate, dark letter) used for the B / 1-6 hints.</summary>
        private void Key(Transform parent, int layer, string text, Vector2 pos, bool left = false)
        {
            var k = NewUi("Key", parent, layer);
            k.anchorMin = k.anchorMax = new Vector2(left ? 0f : 1f, 0.5f); k.pivot = new Vector2(left ? 0f : 1f, 0.5f);
            k.anchoredPosition = pos; k.sizeDelta = new Vector2(30f, 28f);
            if (left) k.anchoredPosition = new Vector2(16f, 0f);
            var img = k.gameObject.AddComponent<Image>(); img.color = new Color(0.92f, 0.92f, 0.94f, 1f); img.raycastTarget = false;
            var t = Label("Text", k, layer, text, 17, new Color(0.05f, 0.05f, 0.06f, 1f), TextAlignmentOptions.Center);
            Stretch(t.rectTransform); t.characterSpacing = 0f;
        }
    }
}
