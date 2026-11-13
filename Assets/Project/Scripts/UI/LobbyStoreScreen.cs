using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// The STORE menu. The OPERATORS tab is real: it lists the operators of the game with their role and skills, and EQUIP picks one for
    /// the lobby and the match. Weapons, Bundles and Currency need a payment backend that does not exist yet, so they show an honest
    /// COMING SOON panel: there are no prices and nothing can be bought.
    /// </summary>
    public class LobbyStoreScreen : LobbyMenuScreen
    {
        protected override string Title => "STORE";
        protected override string Subtitle => "OPERATORS & ARSENAL";
        protected override int NavIndex => 5;
        protected override string Hints => "A D  CHANGE TAB      ESC  BACK";

        private static readonly string[] TabNames = { "OPERATORS", "WEAPONS", "BUNDLES", "CURRENCY" };
        private static readonly string[] SoonText =
        {
            "",
            "Weapon skins and attachments will appear here when the store opens.",
            "Bundles of operators, looks and weapons will appear here when the store opens.",
            "Buying coins and gems needs payments, which are not connected yet.",
        };

        private readonly List<Image> tabBg = new List<Image>();
        private readonly List<TextMeshProUGUI> tabLabel = new List<TextMeshProUGUI>();
        private RectTransform body;
        private int tab;
        private float fade;
        private bool subscribed;

        protected override void BuildContent(RectTransform c)
        {
            BuildTabs(c, TabNames, -880f, 360f, 220f, SetTab, tabBg, tabLabel);
            body = NewRect("Body", c, new Vector2(0.5f, 0.5f), new Vector2(1760f, 780f), new Vector2(0f, -30f));
        }

        protected override void OnOpened()
        {
            tab = 0;
            var preview = LobbyCharacterPreviewManager.Instance;
            if (preview != null && !subscribed) { preview.OnOperatorChanged += OnOperatorChanged; subscribed = true; }
            Refresh();
        }

        private void OnDisable() { Unsubscribe(); }
        protected override void OnDestroy() { Unsubscribe(); base.OnDestroy(); }
        private void Unsubscribe()
        {
            var preview = LobbyCharacterPreviewManager.Instance;
            if (preview != null && subscribed) preview.OnOperatorChanged -= OnOperatorChanged;
            subscribed = false;
        }

        private void OnOperatorChanged(OperatorDefinition op) { if (IsOpen && tab == 0) Refresh(false); }

        protected override void OnKeys()
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.Q)) SetTab(tab - 1);
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.E)) SetTab(tab + 1);
        }

        protected override void OnTick()
        {
            if (fade >= 1f) return;
            fade = Mathf.MoveTowards(fade, 1f, Time.unscaledDeltaTime * 4f);
            float e = 1f - (1f - fade) * (1f - fade);
            for (int i = 0; i < body.childCount; i++)
            {
                var item = (RectTransform)body.GetChild(i);
                var g = item.GetComponent<CanvasGroup>();
                if (g != null) g.alpha = Mathf.Clamp01(e * 1.7f - i * 0.14f);
            }
        }

        private void SetTab(int t)
        {
            t = Mathf.Clamp(t, 0, TabNames.Length - 1);
            if (t == tab) return;
            tab = t;
            Refresh();
        }

        private void Refresh(bool animate = true)
        {
            MarkTabs(tabBg, tabLabel, tab);
            for (int i = body.childCount - 1; i >= 0; i--) Destroy(body.GetChild(i).gameObject);
            if (tab == 0) BuildOperators(); else BuildSoon();
            fade = animate ? 0f : 1f;
            if (!animate) for (int i = 0; i < body.childCount; i++) { var g = body.GetChild(i).GetComponent<CanvasGroup>(); if (g != null) g.alpha = 1f; }
        }

        // ---------------- operators (real) ----------------
        private void BuildOperators()
        {
            var preview = LobbyCharacterPreviewManager.Instance;
            int n = preview != null ? preview.TotalOperators : 0;
            if (n == 0) { BuildSoon("NO OPERATORS FOUND"); return; }

            const int cols = 5;
            int rows = (n + cols - 1) / cols;
            float cardW = 336f, gap = 20f;
            float cardH = rows == 1 ? 640f : 380f;
            float totalW = Mathf.Min(n, cols) * cardW + (Mathf.Min(n, cols) - 1) * gap;
            float x0 = -totalW * 0.5f + cardW * 0.5f;
            float top = (rows * cardH + (rows - 1) * gap) * 0.5f;

            for (int i = 0; i < n; i++)
            {
                var op = preview.GetOperator(i);
                if (op == null) continue;
                int col = i % cols, row = i / cols;
                var card = NewRect("Operator " + i, body, new Vector2(0.5f, 0.5f), new Vector2(cardW, cardH), new Vector2(x0 + col * (cardW + gap), top - cardH * 0.5f - row * (cardH + gap)));
                card.gameObject.AddComponent<CanvasGroup>().alpha = 0f;
                bool equipped = i == preview.CurrentOperatorIndex;
                int index = i;

                var frame = Img("Frame", card, equipped ? Color.white : new Color(1f, 1f, 1f, 0.14f)); Stretch(frame.rectTransform);
                var bg = Img("Bg", card, Panel);
                bg.rectTransform.anchorMin = Vector2.zero; bg.rectTransform.anchorMax = Vector2.one; bg.rectTransform.offsetMin = new Vector2(3, 3); bg.rectTransform.offsetMax = new Vector2(-3, -3);

                float posterH = rows == 1 ? 250f : 130f;
                float posterY = cardH * 0.5f - 3f - posterH * 0.5f;
                var band = Img("Band", card, new Color(1f, 1f, 1f, 0.18f)); band.sprite = gradient;
                Place(band.rectTransform, new Vector2(0f, posterY), new Vector2(cardW - 6f, posterH));
                LabelC(card, Initials(op.displayName), rows == 1 ? 110 : 64, true, Color.white, new Vector2(0f, posterY), new Vector2(cardW - 20f, posterH));
                var rule = Img("Rule", card, new Color(1f, 1f, 1f, 0.9f)); Place(rule.rectTransform, new Vector2(0f, posterY - posterH * 0.5f), new Vector2(cardW - 6f, 3f));

                float y = posterY - posterH * 0.5f - 44f;
                var nm = LabelC(card, (op.displayName ?? "OPERATOR").ToUpperInvariant(), 26, true, Color.white, new Vector2(0f, y), new Vector2(cardW - 30f, 40f)); nm.characterSpacing = 6f;
                y -= 32f;
                LabelC(card, (op.roleCategory ?? "").ToUpperInvariant(), 15, false, Dim, new Vector2(0f, y), new Vector2(cardW - 30f, 24f));

                if (rows == 1)
                {
                    y -= 50f;
                    SkillLine(card, "ACTIVE", op.activeSkillName, y); y -= 44f;
                    SkillLine(card, "PASSIVE", op.passiveSkillName, y); y -= 44f;
                    SkillLine(card, "ULTIMATE", op.ultimateSkillName, y);
                }

                var tag = Img("Tag", card, equipped ? Color.white : new Color(1f, 1f, 1f, 0.14f));
                var tg = tag.rectTransform; tg.anchorMin = tg.anchorMax = new Vector2(0f, 1f); tg.pivot = new Vector2(0f, 1f); tg.anchoredPosition = new Vector2(3f, -3f); tg.sizeDelta = new Vector2(equipped ? 130f : 120f, 30f);
                LabelC(tg, equipped ? "EQUIPPED" : "UNLOCKED", 13, true, equipped ? Color.black : Dim, Vector2.zero, new Vector2(116f, 26f));

                var btn = MakeButton(card, equipped ? "EQUIPPED" : "EQUIP", new Vector2(0f, -cardH * 0.5f + 44f), new Vector2(cardW - 40f, 52f), !equipped, () => Equip(index));
                if (equipped)
                {
                    btn.interactable = false;
                    btn.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.1f);
                    btn.GetComponentInChildren<TextMeshProUGUI>().color = Dim;
                }
            }
        }

        private void SkillLine(RectTransform card, string kind, string skill, float y)
        {
            float x = -150f;
            LabelL(card, kind, 12, false, Faint, x, y + 12f, 300f);
            var s = LabelL(card, string.IsNullOrEmpty(skill) ? "-" : skill.ToUpperInvariant(), 16, true, Color.white, x, y - 8f, 300f);
            s.overflowMode = TextOverflowModes.Ellipsis;
        }

        private static string Initials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Split(new[] { ' ', '-', '_' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "?";
            if (parts.Length == 1) return parts[0].Substring(0, Mathf.Min(2, parts[0].Length)).ToUpperInvariant();
            return (parts[0].Substring(0, 1) + parts[parts.Length - 1].Substring(0, 1)).ToUpperInvariant();
        }

        private void Equip(int index)
        {
            var preview = LobbyCharacterPreviewManager.Instance;
            if (preview != null) preview.SelectOperator(index);   // raises OnOperatorChanged, which redraws the cards
        }

        // ---------------- coming soon ----------------
        private void BuildSoon(string message = null)
        {
            var box = NewRect("Soon", body, new Vector2(0.5f, 0.5f), new Vector2(1760f, 700f), new Vector2(0f, 0f));
            box.gameObject.AddComponent<CanvasGroup>().alpha = 0f;
            var bg = Img("Bg", box, Panel); Stretch(bg.rectTransform);
            var edge = Img("Edge", box, new Color(1f, 1f, 1f, 0.5f));
            var er = edge.rectTransform; er.anchorMin = new Vector2(0f, 1f); er.anchorMax = new Vector2(1f, 1f); er.pivot = new Vector2(0.5f, 1f); er.anchoredPosition = Vector2.zero; er.sizeDelta = new Vector2(0f, 3f);

            var mark = LabelC(box, TabNames[tab], 150, true, new Color(1f, 1f, 1f, 0.07f), new Vector2(0f, 190f), new Vector2(1700f, 220f)); mark.characterSpacing = 30f;
            var head = LabelC(box, "COMING SOON", 56, true, Color.white, new Vector2(0f, 40f), new Vector2(1200f, 80f)); head.characterSpacing = 16f;
            var text = LabelC(box, message ?? SoonText[tab], 20, false, Dim, new Vector2(0f, -40f), new Vector2(1000f, 80f));
            text.enableWordWrapping = true;
            LabelC(box, "NOTHING CAN BE BOUGHT YET", 14, true, Faint, new Vector2(0f, -110f), new Vector2(800f, 24f));
        }
    }
}
