using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// The match mode screen of the lobby (opened from the mode card / key M). Three category tabs, a row of poster cards for the modes in
    /// the category, and a detail panel with the rules, the facts, a team size choice and the SELECT button. Modes the game cannot run yet
    /// are shown as COMING SOON and cannot be selected. Built at runtime in the black-and-white style of the lobby.
    ///
    /// Mouse: hover a card to light it, click to inspect it, double-click or SELECT MODE to choose it.
    /// Keyboard: A D / arrows browse, Q E change category, W S / up down change team size, Enter selects, Esc closes.
    /// </summary>
    public class LobbyGameModePanel : MonoBehaviour
    {
        private static readonly Color Dim = new Color(1f, 1f, 1f, 0.6f);
        private static readonly Color Faint = new Color(1f, 1f, 1f, 0.3f);
        private static readonly Color Ink = new Color(0.04f, 0.045f, 0.055f, 0.97f);

        private const float CardW = 300f, CardH = 560f, CardGap = 20f, CardsLeft = -900f, CardsY = -30f;
        private const float DetailX = 535f, DetailY = -10f;

        public bool IsOpen => canvasGo != null && canvasGo.activeSelf;

        private GameObject canvasGo;
        private RectTransform root, tabsRoot, cardsRoot, detailRoot;
        private CanvasGroup rootGroup, detailGroup;
        private TMP_FontAsset font;
        private Sprite px, gradient;

        // header
        private TextMeshProUGUI currentText;

        // tabs
        private readonly List<Image> tabBg = new List<Image>();
        private readonly List<TextMeshProUGUI> tabLabel = new List<TextMeshProUGUI>();
        private readonly List<TextMeshProUGUI> tabCount = new List<TextMeshProUGUI>();

        // cards
        private sealed class CardView
        {
            public GameModeDefinition mode;
            public RectTransform rt;
            public Image frame;
            public bool hover;
            public float scale = 1f;
        }
        private readonly List<CardView> cards = new List<CardView>();

        // detail
        private TextMeshProUGUI dCategory, dName, dTagline, dDesc, dFormatNote;
        private readonly TextMeshProUGUI[] dRules = new TextMeshProUGUI[3];
        private readonly TextMeshProUGUI[] dFactLabel = new TextMeshProUGUI[4];
        private readonly TextMeshProUGUI[] dFactValue = new TextMeshProUGUI[4];
        private RectTransform teamRow;
        private readonly Image[] teamBg = new Image[4];
        private readonly TextMeshProUGUI[] teamName = new TextMeshProUGUI[4];
        private readonly TextMeshProUGUI[] teamSub = new TextMeshProUGUI[4];
        private readonly Button[] teamBtn = new Button[4];
        private Button selectButton;
        private Image selectBg;
        private TextMeshProUGUI selectLabel;

        // state
        private int tab;
        private int focus;
        private TeamSize team = TeamSize.Solo;
        private float detailT = 1f;
        private IReadOnlyList<GameModeDefinition> list = new List<GameModeDefinition>();

        public static LobbyGameModePanel Create(Transform parent, TMP_FontAsset fontAsset)
        {
            // a root of its own (not under the lobby's canvas): its canvas then fills the screen at the 1920x1080 reference, whatever
            // scale the lobby's own canvas uses; it belongs to the lobby scene and goes with it
            var go = new GameObject("[Lobby] Mode Panel");
            var panel = go.AddComponent<LobbyGameModePanel>();
            panel.font = fontAsset != null ? fontAsset : TMP_Settings.defaultFontAsset;
            panel.Build();
            return panel;
        }

        // ---------------- open / close ----------------
        public void Toggle() { if (IsOpen) Hide(); else Show(); }

        public void Show()
        {
            if (root == null) return;
            EnsureEventSystem();
            canvasGo.SetActive(true);
            rootGroup.alpha = 0f;

            // start on the category and mode the player has chosen now
            var cur = GameModeCatalog.Selected;
            tab = (int)cur.Category;
            RebuildCards();
            focus = Mathf.Max(0, list.ToList().IndexOf(cur));
            team = GameModeCatalog.SelectedTeamSize;
            RefreshAll(true);
        }

        public void Hide()
        {
            if (root == null) return;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            canvasGo.SetActive(false);
        }

        // ---------------- input and animation ----------------
        private void Update()
        {
            if (!IsOpen) return;
            float dt = Time.unscaledDeltaTime;

            rootGroup.alpha = Mathf.MoveTowards(rootGroup.alpha, 1f, dt * 7f);
            detailT = Mathf.MoveTowards(detailT, 1f, dt * 5f);
            float ease = 1f - (1f - detailT) * (1f - detailT);
            detailRoot.anchoredPosition = new Vector2(DetailX + (1f - ease) * 36f, DetailY);
            detailGroup.alpha = ease;

            for (int i = 0; i < cards.Count; i++)
            {
                var c = cards[i];
                bool focused = i == focus;
                float target = focused ? 1.03f : (c.hover ? 1.015f : 1f);
                c.scale = Mathf.Lerp(c.scale, target, dt * 14f);
                c.rt.localScale = new Vector3(c.scale, c.scale, 1f);
                float fa = focused ? 1f : (c.hover ? 0.55f : 0.14f);
                var col = c.frame.color; col.a = Mathf.Lerp(col.a, fa, dt * 14f); c.frame.color = col;
            }

            if (Input.GetKeyDown(KeyCode.Escape)) { Hide(); return; }
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) MoveFocus(-1);
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) MoveFocus(1);
            else if (Input.GetKeyDown(KeyCode.Q)) SetTab(tab - 1);
            else if (Input.GetKeyDown(KeyCode.E)) SetTab(tab + 1);
            else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) MoveTeam(-1);
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) MoveTeam(1);
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Confirm();
        }

        private GameModeDefinition Focused => focus >= 0 && focus < list.Count ? list[focus] : null;

        private void MoveFocus(int d)
        {
            if (list.Count == 0) return;
            SetFocus(Mathf.Clamp(focus + d, 0, list.Count - 1));
        }

        private void SetTab(int t)
        {
            t = Mathf.Clamp(t, 0, 2);
            if (t == tab) return;
            tab = t;
            RebuildCards();
            focus = 0;
            team = DefaultTeamFor(Focused);
            RefreshAll(true);
        }

        private void SetFocus(int i)
        {
            if (i == focus) return;
            focus = i;
            team = DefaultTeamFor(Focused);
            RefreshAll(true);
        }

        private static TeamSize DefaultTeamFor(GameModeDefinition m)
        {
            if (m == null) return TeamSize.Solo;
            if (m == GameModeCatalog.Selected) return GameModeCatalog.SelectedTeamSize;
            return m.DefaultTeamSize;
        }

        private void MoveTeam(int d)
        {
            var m = Focused;
            if (m == null || !m.OffersTeamSizes) return;
            int idx = Array.IndexOf(m.TeamSizes, team);
            if (idx < 0) idx = 0;
            idx = Mathf.Clamp(idx + d, 0, m.TeamSizes.Length - 1);
            team = m.TeamSizes[idx];
            RefreshDetail(false);
        }

        private void SetTeam(TeamSize t)
        {
            team = t;
            RefreshDetail(false);
        }

        private bool CanChoose(GameModeDefinition m, TeamSize t) => m != null && m.IsPlayable && (!m.OffersTeamSizes || m.CanPlay(t));

        private void Confirm()
        {
            var m = Focused;
            if (!CanChoose(m, team)) return;
            if (GameModeCatalog.TrySelect(m, team)) Hide();
        }

        // ---------------- refresh ----------------
        private void RefreshAll(bool animate)
        {
            var cur = GameModeCatalog.Selected;
            currentText.text = "CURRENT   " + cur.CardTitle + (cur.OffersTeamSizes ? "  |  " + GameModeCatalog.TeamName(GameModeCatalog.SelectedTeamSize) : "");

            for (int i = 0; i < tabBg.Count; i++)
            {
                bool on = i == tab;
                tabBg[i].color = on ? Color.white : new Color(0.13f, 0.14f, 0.16f, 1f);
                tabLabel[i].color = on ? Color.black : Dim;
                tabCount[i].color = on ? new Color(0f, 0f, 0f, 0.55f) : Faint;
            }
            RefreshDetail(animate);
        }

        private void RefreshDetail(bool animate)
        {
            var m = Focused;
            if (m == null) return;
            if (animate) detailT = 0f;

            dCategory.text = GameModeCatalog.CategoryName(m.Category);
            dName.text = m.Name;
            dTagline.text = m.Tagline;
            dDesc.text = m.Description;
            for (int i = 0; i < dRules.Length; i++)
                dRules[i].text = i < m.Rules.Length ? "0" + (i + 1) + "    " + m.Rules[i] : "";

            string[] fl = { "PLAYERS", "MAP", "LENGTH", "FORMAT" };
            // the format fact is short: "UP TO 4" for modes with team sizes, "6 V 6" for fixed teams, "SOLO" for free-for-all modes
            string format = m.OffersTeamSizes ? "UP TO " + (int)m.TeamSizes.Max() : (m.FixedFormat.Contains(" V ") ? m.FixedFormat : "SOLO");
            string[] fv = { m.Players.ToString(), m.Map, m.Length.Replace("ABOUT ", "~ "), format };
            for (int i = 0; i < 4; i++) { dFactLabel[i].text = fl[i]; dFactValue[i].text = fv[i]; }

            // team size row
            teamRow.gameObject.SetActive(m.OffersTeamSizes);
            dFormatNote.gameObject.SetActive(!m.OffersTeamSizes);
            if (!m.OffersTeamSizes) dFormatNote.text = "FORMAT   " + m.FixedFormat;
            for (int i = 0; i < 4; i++)
            {
                var t = (TeamSize)(i + 1);
                bool offered = m.Offers(t);
                teamBtn[i].gameObject.SetActive(offered);
                if (!offered) continue;
                bool playable = m.CanPlay(t);
                bool on = t == team;
                teamBg[i].color = on ? (playable ? Color.white : new Color(1f, 1f, 1f, 0.3f)) : new Color(1f, 1f, 1f, 0.1f);
                teamName[i].color = on ? (playable ? Color.black : Color.white) : (playable ? Color.white : Faint);
                teamSub[i].text = playable ? "" : "SOON";
                teamSub[i].color = on && playable ? Color.black : Faint;
                teamBtn[i].interactable = true;
            }

            // select button: always tells the truth about why it cannot be pressed
            bool isCurrent = m == GameModeCatalog.Selected && (!m.OffersTeamSizes || team == GameModeCatalog.SelectedTeamSize);
            bool can = CanChoose(m, team);
            string label;
            if (!m.IsPlayable) label = "COMING SOON";
            else if (!can) label = GameModeCatalog.TeamName(team) + " IS COMING SOON";
            else if (isCurrent) label = "SELECTED";
            else label = "SELECT MODE";
            selectLabel.text = label;
            selectButton.interactable = can && !isCurrent;
            selectBg.color = can && !isCurrent ? Color.white : new Color(1f, 1f, 1f, 0.16f);
            selectLabel.color = can && !isCurrent ? Color.black : (isCurrent ? Color.white : Dim);
        }

        // ---------------- cards ----------------
        private void RebuildCards()
        {
            for (int i = cardsRoot.childCount - 1; i >= 0; i--) Destroy(cardsRoot.GetChild(i).gameObject);
            cards.Clear();
            list = GameModeCatalog.InCategory((ModeCategory)tab).ToList();

            var cur = GameModeCatalog.Selected;
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                int index = i;
                float cx = CardsLeft + CardW * 0.5f + i * (CardW + CardGap);
                var rt = NewRect("Card " + m.Id, cardsRoot, new Vector2(0.5f, 0.5f), new Vector2(CardW, CardH), new Vector2(cx, CardsY));

                var frame = NewImage("Frame", rt, new Color(1f, 1f, 1f, 0.14f)); Stretch(frame.rectTransform);
                var bg = NewImage("Bg", rt, m.IsPlayable ? new Color(0.075f, 0.08f, 0.095f, 1f) : new Color(0.055f, 0.058f, 0.068f, 1f));
                bg.rectTransform.anchorMin = Vector2.zero; bg.rectTransform.anchorMax = Vector2.one;
                bg.rectTransform.offsetMin = new Vector2(3, 3); bg.rectTransform.offsetMax = new Vector2(-3, -3);
                bg.raycastTarget = true;

                // poster: a light band behind the big mark, a thick rule under it
                var band = NewImage("Band", rt, new Color(1f, 1f, 1f, m.IsPlayable ? 0.2f : 0.1f));
                band.sprite = gradient;   // light at the top, fading into the card
                Place(band.rectTransform, new Vector2(0, 131), new Vector2(CardW - 6, 292));
                Label(rt, m.Glyph, m.Glyph.Length > 2 ? 92 : 128, true, m.IsPlayable ? Color.white : new Color(1f, 1f, 1f, 0.34f), new Vector2(0, 112), new Vector2(CardW - 20, 170));
                var rule = NewImage("Rule", rt, new Color(1f, 1f, 1f, m.IsPlayable ? 0.9f : 0.25f)); Place(rule.rectTransform, new Vector2(0, -15), new Vector2(CardW - 6, 3));

                var title = Label(rt, m.CardTitle, 28, true, m.IsPlayable ? Color.white : Dim, new Vector2(0, -75), new Vector2(CardW - 30, 70));
                title.enableWordWrapping = true; title.characterSpacing = 6f;
                string cardFormat = m.OffersTeamSizes
                    ? (m.TeamSizes.Length == 1 ? GameModeCatalog.TeamName(m.TeamSizes[0]) : GameModeCatalog.TeamName(m.TeamSizes.First()) + " TO " + GameModeCatalog.TeamName(m.TeamSizes.Last()))
                    : m.FixedFormat;
                Label(rt, cardFormat, 14, false, Dim, new Vector2(0, -135), new Vector2(CardW - 20, 24));
                Label(rt, m.Players + " PLAYERS   |   " + m.Map, 13, false, Faint, new Vector2(0, -160), new Vector2(CardW - 20, 22));

                // status badge at the bottom
                var badge = NewImage("Badge", rt, m.IsPlayable ? Color.white : new Color(1f, 1f, 1f, 0.1f));
                Place(badge.rectTransform, new Vector2(0, -230), new Vector2(CardW - 60, 40));
                var bt = Label(badge.rectTransform, m.IsPlayable ? "AVAILABLE" : "COMING SOON", 16, true, m.IsPlayable ? Color.black : Faint, Vector2.zero, new Vector2(CardW - 70, 30));
                bt.characterSpacing = 8f;

                if (m == cur)
                {
                    var tag = NewImage("Current", rt, Color.white);
                    var tr = tag.rectTransform; tr.anchorMin = tr.anchorMax = new Vector2(0f, 1f); tr.pivot = new Vector2(0f, 1f);
                    tr.anchoredPosition = new Vector2(3, -3); tr.sizeDelta = new Vector2(120, 30);
                    var tl = Label(tr, "SELECTED", 13, true, Color.black, Vector2.zero, new Vector2(116, 26)); tl.characterSpacing = 6f;
                }

                var handler = rt.gameObject.AddComponent<ModeCardHandler>();
                handler.OnEnter = () => { if (index < cards.Count) cards[index].hover = true; };
                handler.OnExit = () => { if (index < cards.Count) cards[index].hover = false; };
                handler.OnClick = clicks => { SetFocus(index); if (clicks >= 2) Confirm(); };

                cards.Add(new CardView { mode = m, rt = rt, frame = frame });
            }
        }

        // ---------------- build ----------------
        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            new GameObject("[Lobby] EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        }

        private void Build()
        {
            px = MakePixel();
            gradient = MakeGradient();

            canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.overrideSorting = true; canvas.sortingOrder = 870;
            var sc = canvasGo.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
            rootGroup = canvasGo.GetComponent<CanvasGroup>();
            root = (RectTransform)canvasGo.transform;

            var dim = NewImage("Dim", root, new Color(0.01f, 0.012f, 0.016f, 0.985f)); Stretch(dim.rectTransform); dim.raycastTarget = true;

            // header
            var edge = NewImage("Edge", root, Color.white);
            var er = edge.rectTransform; er.anchorMin = new Vector2(0f, 1f); er.anchorMax = new Vector2(1f, 1f); er.pivot = new Vector2(0.5f, 1f); er.anchoredPosition = Vector2.zero; er.sizeDelta = new Vector2(0f, 4f);
            LabelLeft(root, "SELECT MODE", 58, true, Color.white, -900f, 442f, 900f);
            LabelLeft(root, "CHOOSE HOW YOU WANT TO PLAY", 16, false, Dim, -900f, 392f, 900f);
            currentText = Label(root, "", 17, false, Dim, new Vector2(500, 442), new Vector2(700, 30), TextAlignmentOptions.Right);
            MakeButton(root, "CLOSE   ESC", new Vector2(790, 392), new Vector2(220, 40), false, Hide);

            // category tabs
            tabsRoot = NewRect("Tabs", root, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            string[] keys = { "", "", "" };
            for (int i = 0; i < 3; i++)
            {
                int t = i;
                var cat = (ModeCategory)i;
                float w = 330f;
                float cx = CardsLeft + w * 0.5f + i * (w + 10f);
                var bg = NewImage("Tab " + i, tabsRoot, new Color(0.13f, 0.14f, 0.16f, 1f));
                Place(bg.rectTransform, new Vector2(cx, 338), new Vector2(w, 54));
                bg.raycastTarget = true;
                var btn = bg.gameObject.AddComponent<Button>(); btn.targetGraphic = bg; btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() => SetTab(t));
                var l = Label(bg.rectTransform, GameModeCatalog.CategoryName(cat), 18, true, Dim, new Vector2(-14, 0), new Vector2(w - 70, 30));
                l.characterSpacing = 6f;
                var n = Label(bg.rectTransform, GameModeCatalog.InCategory(cat).Count() + "", 18, true, Faint, new Vector2(w * 0.5f - 24, 0), new Vector2(40, 30));
                tabBg.Add(bg); tabLabel.Add(l); tabCount.Add(n);
                _ = keys;
            }

            cardsRoot = NewRect("Cards", root, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            BuildDetail();

            Label(root, "A D  BROWSE      Q E  CATEGORY      W S  TEAM SIZE      ENTER  SELECT      ESC  CLOSE", 14, false, Faint, new Vector2(0, -492), new Vector2(1500, 24));

            canvasGo.SetActive(false);
        }

        private void BuildDetail()
        {
            detailRoot = NewRect("Detail", root, new Vector2(0.5f, 0.5f), new Vector2(730, 620), new Vector2(DetailX, DetailY));
            detailGroup = detailRoot.gameObject.AddComponent<CanvasGroup>();
            var bg = NewImage("Bg", detailRoot, Ink); Stretch(bg.rectTransform); bg.raycastTarget = true;
            var topEdge = NewImage("Edge", detailRoot, new Color(1f, 1f, 1f, 0.5f));
            var te = topEdge.rectTransform; te.anchorMin = new Vector2(0f, 1f); te.anchorMax = new Vector2(1f, 1f); te.pivot = new Vector2(0.5f, 1f); te.anchoredPosition = Vector2.zero; te.sizeDelta = new Vector2(0f, 3f);

            const float L = -330f, W = 660f;
            dCategory = LabelLeft(detailRoot, "", 14, false, Dim, L, 270f, W);
            dName = LabelLeft(detailRoot, "", 50, true, Color.white, L, 224f, W);
            dTagline = LabelLeft(detailRoot, "", 20, false, new Color(1f, 1f, 1f, 0.85f), L, 172f, W);
            var rule = NewImage("Rule", detailRoot, Faint); Place(rule.rectTransform, new Vector2(0, 142), new Vector2(W, 2));

            dDesc = LabelLeft(detailRoot, "", 19, false, Dim, L, 80f, W, 96f);
            dDesc.enableWordWrapping = true; dDesc.characterSpacing = 1.5f; dDesc.alignment = TextAlignmentOptions.TopLeft;

            for (int i = 0; i < 3; i++)
                dRules[i] = LabelLeft(detailRoot, "", 17, false, Color.white, L, 30f - i * 34f, W);

            // fact boxes
            for (int i = 0; i < 4; i++)
            {
                float fx = L + 80f + i * 170f;
                var box = NewImage("Fact " + i, detailRoot, new Color(1f, 1f, 1f, 0.06f));
                Place(box.rectTransform, new Vector2(fx, -112), new Vector2(160, 62));
                dFactLabel[i] = Label(box.rectTransform, "", 12, false, Faint, new Vector2(0, 16), new Vector2(150, 18));
                dFactValue[i] = Label(box.rectTransform, "", 20, true, Color.white, new Vector2(0, -8), new Vector2(150, 30));
                dFactValue[i].characterSpacing = 3f;
            }

            // team size choice
            teamRow = NewRect("TeamRow", detailRoot, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            LabelLeft(teamRow, "TEAM SIZE", 13, false, Dim, L, -160f, 300f);
            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                var t = (TeamSize)(i + 1);
                float bx = L + 80f + i * 170f;
                var b = NewImage("Team " + i, teamRow, new Color(1f, 1f, 1f, 0.1f));
                Place(b.rectTransform, new Vector2(bx, -198), new Vector2(160, 52));
                b.raycastTarget = true;
                var btn = b.gameObject.AddComponent<Button>(); btn.targetGraphic = b; btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() => SetTeam((TeamSize)(idx + 1)));
                var nm = Label(b.rectTransform, GameModeCatalog.TeamName(t), 20, true, Color.white, new Vector2(0, 6), new Vector2(150, 28)); nm.characterSpacing = 6f;
                var sub = Label(b.rectTransform, "", 11, false, Faint, new Vector2(0, -16), new Vector2(150, 16)); sub.characterSpacing = 6f;
                teamBg[i] = b; teamName[i] = nm; teamSub[i] = sub; teamBtn[i] = btn;
            }
            dFormatNote = LabelLeft(detailRoot, "", 20, true, Color.white, L, -198f, W);
            dFormatNote.characterSpacing = 6f;

            // select button
            selectBg = NewImage("Select", detailRoot, Color.white);
            Place(selectBg.rectTransform, new Vector2(0, -268), new Vector2(W, 58));
            selectBg.raycastTarget = true;
            selectButton = selectBg.gameObject.AddComponent<Button>(); selectButton.targetGraphic = selectBg;
            var c = selectButton.colors;
            c.normalColor = Color.white; c.highlightedColor = new Color(0.82f, 0.82f, 0.82f, 1f); c.pressedColor = new Color(0.6f, 0.6f, 0.6f, 1f);
            c.disabledColor = Color.white; c.fadeDuration = 0.08f;
            selectButton.colors = c;
            selectButton.onClick.AddListener(Confirm);
            selectLabel = Label(selectBg.rectTransform, "SELECT MODE", 24, true, Color.black, Vector2.zero, new Vector2(W - 20, 40));
            selectLabel.characterSpacing = 10f;
        }

        // ---------------- card pointer handler ----------------
        private sealed class ModeCardHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
        {
            public Action OnEnter, OnExit;
            public Action<int> OnClick;
            public void OnPointerEnter(PointerEventData e) { OnEnter?.Invoke(); }
            public void OnPointerExit(PointerEventData e) { OnExit?.Invoke(); }
            public void OnPointerClick(PointerEventData e) { OnClick?.Invoke(e.clickCount); }
        }

        // ---------------- helpers ----------------
        private Button MakeButton(RectTransform parent, string label, Vector2 pos, Vector2 size, bool primary, UnityEngine.Events.UnityAction onClick)
        {
            var bg = NewImage(label + " Button", parent, primary ? Color.white : new Color(1f, 1f, 1f, 0.14f));
            Place(bg.rectTransform, pos, size);
            bg.raycastTarget = true;
            var btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            var c = btn.colors;
            c.normalColor = Color.white;
            c.highlightedColor = primary ? new Color(0.82f, 0.82f, 0.82f, 1f) : new Color(1.6f, 1.6f, 1.6f, 1f);
            c.pressedColor = primary ? new Color(0.6f, 0.6f, 0.6f, 1f) : new Color(2.2f, 2.2f, 2.2f, 1f);
            c.fadeDuration = 0.08f;
            btn.colors = c;
            btn.onClick.AddListener(onClick);
            var t = NewText("Label", bg.rectTransform, label, 16, primary ? Color.black : Color.white, TextAlignmentOptions.Center);
            t.fontStyle = FontStyles.Bold; t.characterSpacing = 6f; Stretch(t.rectTransform);
            return btn;
        }

        private TextMeshProUGUI Label(RectTransform parent, string text, float size, bool bold, Color color, Vector2 pos, Vector2 box, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var t = NewText("Label", parent, text, size, color, align);
            if (bold) t.fontStyle = FontStyles.Bold;
            t.characterSpacing = bold ? 10f : 3f;
            Place(t.rectTransform, pos, box);
            return t;
        }

        /// <summary>A left-aligned label whose left edge is at <paramref name="left"/> (x) and whose vertical centre is at <paramref name="y"/>.</summary>
        private TextMeshProUGUI LabelLeft(RectTransform parent, string text, float size, bool bold, Color color, float left, float y, float width, float height = 0f)
        {
            if (height <= 0f) height = size * 1.5f;
            return Label(parent, text, size, bold, color, new Vector2(left + width * 0.5f, y), new Vector2(width, height), TextAlignmentOptions.Left);
        }

        private TextMeshProUGUI NewText(string name, RectTransform parent, string text, float size, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.font = font; t.text = text; t.fontSize = size; t.color = color; t.alignment = align;
            t.raycastTarget = false; t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        private Image NewImage(string name, RectTransform parent, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var i = go.GetComponent<Image>(); i.sprite = px; i.color = c; i.raycastTarget = false;
            return i;
        }

        private static RectTransform NewRect(string name, RectTransform parent, Vector2 anchor, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            Place(r, pos, size);
            r.anchorMin = r.anchorMax = anchor;
            return r;
        }

        private static void Place(RectTransform r, Vector2 pos, Vector2 size)
        {
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = pos; r.sizeDelta = size;
        }

        private static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero; }

        // white, opaque at the top and transparent at the bottom (tinted by the Image colour)
        private static Sprite MakeGradient()
        {
            const int h = 64;
            var tex = new Texture2D(2, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var cols = new Color32[2 * h];
            for (int y = 0; y < h; y++)
            {
                float a = Mathf.Pow(y / (float)(h - 1), 1.4f);   // y = 0 is the bottom row
                for (int x = 0; x < 2; x++) cols[y * 2 + x] = new Color(1f, 1f, 1f, a);
            }
            tex.SetPixels32(cols); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 2, h), new Vector2(0.5f, 0.5f), 100f);
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
