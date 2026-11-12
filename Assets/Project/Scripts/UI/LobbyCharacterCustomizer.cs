using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// The operator customization screen: a close-up of the local operator on the stage, a category list on the left and the
    /// options for that category on the right. Opened from the OPERATORS tab; the lobby HUD fades out while it is open and
    /// Esc / BACK returns to the lobby.
    ///
    /// Keys: Up / Down browse options, Left / Right change category, Enter equips, R resets the operator, Esc goes back.
    /// Built at runtime as its own overlay canvas, so nothing in the scene needs wiring.
    /// </summary>
    [DisallowMultipleComponent]
    public class LobbyCharacterCustomizer : MonoBehaviour
    {
        private enum Category { Operator, Uniform, Vest, Headgear, Headset }
        private static readonly string[] CategoryNames = { "OPERATOR", "UNIFORM", "VEST", "HEADGEAR", "HEADSET" };

        private static readonly Color Ink = new Color(0.05f, 0.055f, 0.065f, 1f);
        private static readonly Color Soft = new Color(0.96f, 0.96f, 0.97f, 0.72f);
        private static readonly Color Faint = new Color(0.96f, 0.96f, 0.97f, 0.45f);
        private static readonly Color Clear = new Color(1f, 1f, 1f, 0f);

        private struct Option { public string name, sub; public int value; public bool hasChip; public Color chip; }

        private class OptionRow
        {
            public RectTransform rt; public Image bg, chip; public TextMeshProUGUI name, sub, tag;
        }

        // ------------------------------------------------------------------ creation
        public static LobbyCharacterCustomizer Create(TMP_FontAsset font, GameObject hud, Action onClosed)
        {
            var go = new GameObject("Lobby_CharacterCustomizer", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();

            var comp = go.AddComponent<LobbyCharacterCustomizer>();
            comp.font = font;
            comp.hud = hud;
            comp.onClosed = onClosed;
            comp.group = go.AddComponent<CanvasGroup>();
            comp.group.alpha = 0f;           // fades in on Open
            comp.Build();
            go.SetActive(false);
            return comp;
        }

        private TMP_FontAsset font;
        private GameObject hud;
        private CanvasGroup hudGroup, group;
        private Action onClosed;

        private RectTransform root;
        private TextMeshProUGUI titleName, titleRole, skills, rightHeader, rightCount;
        private readonly List<Image> menuBg = new List<Image>();
        private readonly List<TextMeshProUGUI> menuLabel = new List<TextMeshProUGUI>();
        private RectTransform rightList;
        private readonly List<OptionRow> rows = new List<OptionRow>();
        private readonly List<Option> options = new List<Option>();

        private Category category;
        private int highlight;
        private int closedFrame = -1;

        public bool IsOpen { get; private set; }
        /// <summary>True while open and on the frame it closed, so the HUD does not also act on the same Esc press.</summary>
        public bool BusyThisFrame { get { return IsOpen || closedFrame == Time.frameCount; } }

        // ------------------------------------------------------------------ open / close
        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            gameObject.SetActive(true);

            if (hud != null && hudGroup == null)
            {
                hudGroup = hud.GetComponent<CanvasGroup>();
                if (hudGroup == null) hudGroup = hud.AddComponent<CanvasGroup>();
            }

            LobbyParty.Instance.Select(0);       // the local operator is the one on show
            var director = FindObjectOfType<LobbyStageDirector>();
            if (director != null) director.SetCustomizing(true);
            var preview = LobbyCharacterPreviewManager.Instance;
            if (preview != null) preview.OnOperatorChanged += OnOperatorChanged;

            category = Category.Operator;
            RebuildOptions();
            RefreshTitle();
            RefreshVisuals();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            closedFrame = Time.frameCount;

            var director = FindObjectOfType<LobbyStageDirector>();
            if (director != null) director.SetCustomizing(false);
            var preview = LobbyCharacterPreviewManager.Instance;
            if (preview != null) preview.OnOperatorChanged -= OnOperatorChanged;

            if (onClosed != null) onClosed();
        }

        private void OnDestroy()
        {
            var preview = LobbyCharacterPreviewManager.Instance;
            if (preview != null) preview.OnOperatorChanged -= OnOperatorChanged;
            if (hudGroup != null) { hudGroup.alpha = 1f; hudGroup.blocksRaycasts = true; hudGroup.interactable = true; }
        }

        private void OnOperatorChanged(OperatorDefinition op)
        {
            RebuildOptions();
            RefreshTitle();
            RefreshVisuals();
        }

        // ------------------------------------------------------------------ per frame
        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            group.alpha = Mathf.MoveTowards(group.alpha, IsOpen ? 1f : 0f, dt * 4f);
            group.blocksRaycasts = IsOpen;
            if (hudGroup != null)
            {
                hudGroup.alpha = 1f - group.alpha;
                bool hudLive = group.alpha < 0.05f;
                hudGroup.blocksRaycasts = hudLive;
                hudGroup.interactable = hudLive;
            }
            if (!IsOpen)
            {
                if (group.alpha <= 0f) gameObject.SetActive(false);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
            if (Input.GetKeyDown(KeyCode.R)) ResetLook();
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) MoveCategory(-1);
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) MoveCategory(1);
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) MoveOption(-1);
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) MoveOption(1);
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Equip(highlight);
        }

        // ------------------------------------------------------------------ actions
        private void SetCategory(Category c)
        {
            if (c == category) return;
            category = c;
            RebuildOptions();
            RefreshVisuals();
        }

        private void MoveCategory(int delta)
        {
            int n = CategoryNames.Length;
            SetCategory((Category)(((int)category + delta + n) % n));
        }

        private void MoveOption(int delta)
        {
            if (options.Count == 0) return;
            highlight = Mathf.Clamp(highlight + delta, 0, options.Count - 1);
            RefreshVisuals();
        }

        private static LookPart PartOf(Category c)
        {
            switch (c)
            {
                case Category.Vest: return LookPart.Vest;
                case Category.Headgear: return LookPart.Headgear;
                case Category.Headset: return LookPart.Headset;
                default: return LookPart.Uniform;
            }
        }

        private void Equip(int index)
        {
            if (index < 0 || index >= options.Count) return;
            highlight = index;
            var preview = LobbyCharacterPreviewManager.Instance;
            if (preview == null) return;

            if (category == Category.Operator)
            {
                preview.SelectOperator(options[index].value);    // raises OnOperatorChanged, which refreshes everything
                return;
            }
            LobbyOperatorLooks.SetChoice(preview.CurrentOperatorIndex, PartOf(category), options[index].value);
            preview.RefreshLook();
            RefreshVisuals();
        }

        private void ResetLook()
        {
            var preview = LobbyCharacterPreviewManager.Instance;
            if (preview == null) return;
            LobbyOperatorLooks.Reset(preview.CurrentOperatorIndex);
            preview.RefreshLook();
            RebuildOptions();
            RefreshVisuals();
        }

        // ------------------------------------------------------------------ data -> rows
        private int EquippedValue()
        {
            var preview = LobbyCharacterPreviewManager.Instance;
            if (preview == null) return LobbyOperatorLooks.Default;
            return category == Category.Operator
                ? preview.CurrentOperatorIndex
                : LobbyOperatorLooks.GetChoice(preview.CurrentOperatorIndex, PartOf(category));
        }

        private void RebuildOptions()
        {
            options.Clear();
            var preview = LobbyCharacterPreviewManager.Instance;

            if (category == Category.Operator)
            {
                int total = preview != null ? preview.TotalOperators : 0;
                for (int i = 0; i < total; i++)
                {
                    var op = preview.GetOperator(i);
                    if (op == null) continue;
                    options.Add(new Option { name = op.displayName, sub = op.roleCategory, value = i });
                }
            }
            else
            {
                options.Add(new Option { name = "DEFAULT", value = LobbyOperatorLooks.Default });
                if (category == Category.Headgear || category == Category.Headset)
                    options.Add(new Option { name = "NONE", value = LobbyOperatorLooks.None });
                for (int i = 0; i < LobbyOperatorLooks.Swatches.Length; i++)
                {
                    var s = LobbyOperatorLooks.Swatches[i];
                    options.Add(new Option { name = s.name, value = i, hasChip = true, chip = s.display });
                }
            }

            int equipped = EquippedValue();
            highlight = Mathf.Max(0, options.FindIndex(o => o.value == equipped));

            for (int i = rightList.childCount - 1; i >= 0; i--) Destroy(rightList.GetChild(i).gameObject);
            rows.Clear();
            for (int i = 0; i < options.Count; i++) rows.Add(BuildRow(i));
        }

        private void RefreshTitle()
        {
            var preview = LobbyCharacterPreviewManager.Instance;
            var op = preview != null ? preview.CurrentOperator : null;
            titleName.text = op != null ? op.displayName : "OPERATOR";
            titleRole.text = op != null ? op.roleCategory : "";
            skills.text = op == null ? "" :
                "<color=#8E959C>ACTIVE</color>   " + op.activeSkillName + "\n" +
                "<color=#8E959C>PASSIVE</color>   " + op.passiveSkillName + "\n" +
                "<color=#8E959C>ULTIMATE</color>   " + op.ultimateSkillName;
        }

        private void RefreshVisuals()
        {
            for (int i = 0; i < menuBg.Count; i++)
            {
                bool on = i == (int)category;
                menuBg[i].color = on ? Color.white : Clear;
                menuLabel[i].color = on ? Ink : Soft;
                menuLabel[i].fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
            }

            int equipped = EquippedValue();
            for (int i = 0; i < rows.Count && i < options.Count; i++)
            {
                var row = rows[i];
                bool isEquipped = options[i].value == equipped;
                bool isHot = i == highlight;
                row.bg.color = isHot ? new Color(1f, 1f, 1f, 0.14f) : Clear;
                row.name.color = isEquipped || isHot ? Color.white : Soft;
                row.name.fontStyle = isEquipped ? FontStyles.Bold : FontStyles.Normal;
                row.tag.gameObject.SetActive(isEquipped);
            }

            rightHeader.text = CategoryNames[(int)category];
            rightCount.text = options.Count > 0 ? (highlight + 1) + " / " + options.Count : "";
        }

        // ------------------------------------------------------------------ building the screen
        private void Build()
        {
            root = (RectTransform)transform;
            AddShading();

            // title block, top left
            var label = NewText("Title_Label", root, "OPERATORS", 22f, Faint, TextAlignmentOptions.TopLeft, 6f);
            Anchor(label.rectTransform, 0f, 1f, 0f, 1f, new Vector2(0f, 1f), new Vector2(80f, -60f), new Vector2(600f, 30f));
            titleName = NewText("Title_Name", root, "", 58f, Color.white, TextAlignmentOptions.TopLeft, 2f);
            titleName.fontStyle = FontStyles.Bold;
            Anchor(titleName.rectTransform, 0f, 1f, 0f, 1f, new Vector2(0f, 1f), new Vector2(80f, -92f), new Vector2(900f, 72f));
            titleRole = NewText("Title_Role", root, "", 22f, Soft, TextAlignmentOptions.TopLeft, 4f);
            Anchor(titleRole.rectTransform, 0f, 1f, 0f, 1f, new Vector2(0f, 1f), new Vector2(82f, -162f), new Vector2(700f, 30f));

            // category menu, left
            var menu = NewRect("Menu", root, Vector2.zero);
            Anchor(menu, 0f, 1f, 0f, 1f, new Vector2(0f, 1f), new Vector2(80f, -300f), new Vector2(400f, CategoryNames.Length * 60f));
            for (int i = 0; i < CategoryNames.Length; i++)
            {
                int index = i;
                var row = NewRect("Menu_" + CategoryNames[i], menu, Vector2.zero);
                Anchor(row, 0f, 1f, 1f, 1f, new Vector2(0.5f, 1f), new Vector2(0f, -i * 60f), new Vector2(0f, 54f));
                var bg = row.gameObject.AddComponent<Image>();
                bg.color = Clear;
                var btn = row.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg; btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() => SetCategory((Category)index));
                var text = NewText("Label", row, CategoryNames[i], 26f, Soft, TextAlignmentOptions.MidlineLeft, 3f);
                Stretch(text.rectTransform, 22f, 0f);
                menuBg.Add(bg); menuLabel.Add(text);
            }

            // option list, right
            var panel = NewRect("Options", root, Vector2.zero);
            Anchor(panel, 1f, 1f, 1f, 1f, new Vector2(1f, 1f), new Vector2(-80f, -300f), new Vector2(460f, 700f));
            rightHeader = NewText("Header", panel, "", 24f, Color.white, TextAlignmentOptions.TopLeft, 4f);
            rightHeader.fontStyle = FontStyles.Bold;
            Anchor(rightHeader.rectTransform, 0f, 1f, 0f, 1f, new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(300f, 34f));
            rightCount = NewText("Count", panel, "", 20f, Faint, TextAlignmentOptions.TopRight, 2f);
            Anchor(rightCount.rectTransform, 1f, 1f, 1f, 1f, new Vector2(1f, 1f), new Vector2(0f, -2f), new Vector2(120f, 30f));
            var line = NewRect("Rule", panel, Vector2.zero);
            Anchor(line, 0f, 1f, 1f, 1f, new Vector2(0.5f, 1f), new Vector2(0f, -42f), new Vector2(0f, 1.5f));
            var lineImg = line.gameObject.AddComponent<Image>(); lineImg.color = new Color(1f, 1f, 1f, 0.25f); lineImg.raycastTarget = false;
            rightList = NewRect("List", panel, Vector2.zero);
            Anchor(rightList, 0f, 1f, 1f, 1f, new Vector2(0.5f, 1f), new Vector2(0f, -56f), new Vector2(0f, 640f));

            // skills, under the menu
            skills = NewText("Skills", root, "", 20f, Soft, TextAlignmentOptions.TopLeft, 2f);
            skills.lineSpacing = 18f;
            Anchor(skills.rectTransform, 0f, 0f, 0f, 0f, new Vector2(0f, 0f), new Vector2(82f, 130f), new Vector2(560f, 100f));

            // back button and key hints, bottom left
            var back = NewRect("Back", root, Vector2.zero);
            Anchor(back, 0f, 0f, 0f, 0f, new Vector2(0f, 0f), new Vector2(80f, 64f), new Vector2(140f, 44f));
            var backBg = back.gameObject.AddComponent<Image>(); backBg.color = Clear;
            var backBtn = back.gameObject.AddComponent<Button>(); backBtn.targetGraphic = backBg; backBtn.transition = Selectable.Transition.None;
            backBtn.onClick.AddListener(Close);
            var backText = NewText("Label", back, "BACK", 24f, Color.white, TextAlignmentOptions.MidlineLeft, 3f);
            backText.fontStyle = FontStyles.Bold;
            Stretch(backText.rectTransform, 0f, 0f);

            var hints = NewText("Hints", root, "[ESC] BACK     [R] RESET     [ARROWS] BROWSE     [ENTER] EQUIP", 18f, Faint, TextAlignmentOptions.BottomRight, 3f);
            Anchor(hints.rectTransform, 1f, 0f, 1f, 0f, new Vector2(1f, 0f), new Vector2(-80f, 40f), new Vector2(900f, 30f));
        }

        private OptionRow BuildRow(int i)
        {
            var o = options[i];
            var row = new OptionRow();
            row.rt = NewRect("Option_" + i, rightList, Vector2.zero);
            Anchor(row.rt, 0f, 1f, 1f, 1f, new Vector2(0.5f, 1f), new Vector2(0f, -i * 50f), new Vector2(0f, 46f));
            row.bg = row.rt.gameObject.AddComponent<Image>();
            row.bg.color = Clear;
            var btn = row.rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = row.bg; btn.transition = Selectable.Transition.None;
            int index = i;
            btn.onClick.AddListener(() => Equip(index));
            var hover = row.rt.gameObject.AddComponent<RowHover>();
            hover.onEnter = () => { highlight = index; RefreshVisuals(); };

            float textX = 18f;
            if (o.hasChip)
            {
                var chipRt = NewRect("Chip", row.rt, new Vector2(22f, 22f));
                Anchor(chipRt, 0f, 0.5f, 0f, 0.5f, new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(22f, 22f));
                row.chip = chipRt.gameObject.AddComponent<Image>();
                row.chip.color = o.chip; row.chip.raycastTarget = false;
                textX = 52f;
            }

            row.name = NewText("Name", row.rt, o.name, 24f, Soft, TextAlignmentOptions.MidlineLeft, 2f);
            Stretch(row.name.rectTransform, textX, 0f);
            if (!string.IsNullOrEmpty(o.sub))
            {
                row.sub = NewText("Sub", row.rt, o.sub, 15f, Faint, TextAlignmentOptions.MidlineRight, 2f);
                Anchor(row.sub.rectTransform, 1f, 0.5f, 1f, 0.5f, new Vector2(1f, 0.5f), new Vector2(-14f, -12f), new Vector2(190f, 20f));
                row.sub.enableWordWrapping = false; row.sub.overflowMode = TextOverflowModes.Ellipsis;
                row.name.rectTransform.offsetMax = new Vector2(-200f, 0f);
            }
            row.tag = NewText("Tag", row.rt, "EQUIPPED", 14f, Color.white, TextAlignmentOptions.MidlineRight, 3f);
            Anchor(row.tag.rectTransform, 1f, 0.5f, 1f, 0.5f, new Vector2(1f, 0.5f), new Vector2(-14f, string.IsNullOrEmpty(o.sub) ? 0f : 12f), new Vector2(120f, 20f));
            row.tag.gameObject.SetActive(false);
            return row;
        }

        /// <summary>Dark ramps in from the left, right, top and bottom so the lists read over the 3D stage and the operator stays clear.</summary>
        private void AddShading()
        {
            AddRamp("Shade_Left", 0f, 0f, 0f, 1f, new Vector2(0f, 0.5f), new Vector2(980f, 0f), false, false, 0.88f);
            AddRamp("Shade_Right", 1f, 0f, 1f, 1f, new Vector2(1f, 0.5f), new Vector2(860f, 0f), false, true, 0.88f);
            AddRamp("Shade_Top", 0f, 1f, 1f, 1f, new Vector2(0.5f, 1f), new Vector2(0f, 300f), true, false, 0.7f);
            AddRamp("Shade_Bottom", 0f, 0f, 1f, 0f, new Vector2(0.5f, 0f), new Vector2(0f, 260f), true, true, 0.8f);
        }

        private void AddRamp(string name, float ax0, float ay0, float ax1, float ay1, Vector2 pivot, Vector2 size, bool vertical, bool flip, float alpha)
        {
            var rt = NewRect(name, root, size);
            rt.anchorMin = new Vector2(ax0, ay0); rt.anchorMax = new Vector2(ax1, ay1);
            rt.pivot = pivot; rt.anchoredPosition = Vector2.zero;
            // a stretched axis takes its size from the anchors, so only the other axis keeps the explicit size
            rt.sizeDelta = new Vector2(ax0 == ax1 ? size.x : 0f, ay0 == ay1 ? size.y : 0f);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Ramp(vertical);
            img.color = new Color(0f, 0f, 0f, alpha);
            img.raycastTarget = false;
            if (flip) rt.localScale = vertical ? new Vector3(1f, -1f, 1f) : new Vector3(-1f, 1f, 1f);
        }

        private static Sprite rampH, rampV;
        /// <summary>A white sprite that fades from opaque (left / top) to transparent, tinted black by the Image.</summary>
        private static Sprite Ramp(bool vertical)
        {
            if (vertical && rampV != null) return rampV;
            if (!vertical && rampH != null) return rampH;
            const int n = 128;
            var tex = new Texture2D(vertical ? 1 : n, vertical ? n : 1, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                // horizontal: x = 0 is opaque; vertical: y = n - 1 (the top of the texture) is opaque
                float a = Mathf.Pow(vertical ? t : 1f - t, 1.7f);
                tex.SetPixel(vertical ? 0 : i, vertical ? i : 0, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            if (vertical) rampV = sprite; else rampH = sprite;
            return sprite;
        }

        // ------------------------------------------------------------------ tiny uGUI helpers
        private static RectTransform NewRect(string name, Transform parent, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Anchors (min x/y, max x/y), pivot, position and size in one call.</summary>
        private static void Anchor(RectTransform r, float ax0, float ay0, float ax1, float ay1, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            r.anchorMin = new Vector2(ax0, ay0); r.anchorMax = new Vector2(ax1, ay1);
            r.pivot = pivot; r.anchoredPosition = pos; r.sizeDelta = size;
        }

        private static void Stretch(RectTransform r, float left, float right)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(left, 0f); r.offsetMax = new Vector2(-right, 0f);
        }

        private TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align, float spacing)
        {
            var rt = NewRect(name, parent, Vector2.zero);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text; t.fontSize = size; t.color = color; t.alignment = align;
            t.characterSpacing = spacing; t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false; t.richText = true;
            return t;
        }

        /// <summary>Lets a list row react to the mouse moving over it.</summary>
        private class RowHover : MonoBehaviour, IPointerEnterHandler
        {
            public Action onEnter;
            public void OnPointerEnter(PointerEventData eventData) { if (onEnter != null) onEnter(); }
        }
    }
}
