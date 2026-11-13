using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// The CHALLENGES menu: Daily / Weekly / Season tabs, a row per challenge (description, progress bar, XP reward) and a side panel with the
    /// reset timer and totals. The lists are sample content until the challenge service is connected, and the screen says so.
    /// </summary>
    public class LobbyChallengesScreen : LobbyMenuScreen
    {
        protected override string Title => "CHALLENGES";
        protected override string Subtitle => "COMPLETE ASSIGNMENTS TO EARN XP";
        protected override int NavIndex => 4;
        protected override string Hints => "A D  CHANGE TAB      ESC  BACK";

        private static readonly string[] TabNames = { "DAILY", "WEEKLY", "SEASON" };

        private static readonly LobbyChallengesPanel.Challenge[] Weekly =
        {
            new LobbyChallengesPanel.Challenge("Win 3 matches", 0, 3, 15000),
            new LobbyChallengesPanel.Challenge("Eliminate 25 players", 0, 25, 15000),
            new LobbyChallengesPanel.Challenge("Reach the final zone 3 times", 0, 3, 12000),
            new LobbyChallengesPanel.Challenge("Deal 5000 damage", 0, 5000, 12000),
        };
        private static readonly LobbyChallengesPanel.Challenge[] Season =
        {
            new LobbyChallengesPanel.Challenge("Play 50 matches", 0, 50, 50000),
            new LobbyChallengesPanel.Challenge("Win 10 matches", 0, 10, 50000),
            new LobbyChallengesPanel.Challenge("Eliminate 500 players", 0, 500, 75000),
        };

        private readonly List<Image> tabBg = new List<Image>();
        private readonly List<TextMeshProUGUI> tabLabel = new List<TextMeshProUGUI>();
        private RectTransform listRoot;
        private TextMeshProUGUI resetLabel, resetValue, totalXp, doneCount;
        private int tab;
        private float nextClock;
        private float fade;   // rows slide in after a tab change

        private LobbyChallengesPanel.Challenge[] Current =>
            tab == 0 ? LobbyChallengesPanel.Daily : (tab == 1 ? Weekly : Season);

        protected override void BuildContent(RectTransform c)
        {
            BuildTabs(c, TabNames, -880f, 360f, 220f, SetTab, tabBg, tabLabel);

            listRoot = NewRect("List", c, new Vector2(0.5f, 0.5f), new Vector2(1180f, 700f), new Vector2(-880f + 590f, -48f));

            // side panel
            var side = Img("Side", c, Panel); Place(side.rectTransform, new Vector2(610f, -8f), new Vector2(540f, 784f));
            var edge = Img("Edge", side.rectTransform, new Color(1f, 1f, 1f, 0.5f));
            var er = edge.rectTransform; er.anchorMin = new Vector2(0f, 1f); er.anchorMax = new Vector2(1f, 1f); er.pivot = new Vector2(0.5f, 1f); er.anchoredPosition = Vector2.zero; er.sizeDelta = new Vector2(0f, 3f);
            resetLabel = LabelL(side.rectTransform, "RESETS IN", 15, false, Dim, -240f, 330f, 480f);
            resetValue = LabelL(side.rectTransform, "", 64, true, Color.white, -240f, 270f, 480f, 80f);
            var rule = Img("Rule", side.rectTransform, Line); Place(rule.rectTransform, new Vector2(0, 205f), new Vector2(480f, 2f));
            LabelL(side.rectTransform, "XP AVAILABLE", 15, false, Dim, -240f, 160f, 480f);
            totalXp = LabelL(side.rectTransform, "", 44, true, Color.white, -240f, 112f, 480f, 60f);
            LabelL(side.rectTransform, "COMPLETED", 15, false, Dim, -240f, 50f, 480f);
            doneCount = LabelL(side.rectTransform, "", 44, true, Color.white, -240f, 2f, 480f, 60f);
            var rule2 = Img("Rule2", side.rectTransform, Line); Place(rule2.rectTransform, new Vector2(0, -60f), new Vector2(480f, 2f));

            var note = LabelL(side.rectTransform, "SAMPLE LIST", 15, true, Color.white, -240f, -100f, 480f);
            var body = LabelL(side.rectTransform, "These challenges are examples. Progress tracking and real rewards go live when the challenge service is connected.", 16, false, Dim, -240f, -190f, 480f, 150f);
            body.enableWordWrapping = true; body.alignment = TextAlignmentOptions.TopLeft; body.characterSpacing = 1f;
            _ = note;
        }

        protected override void OnOpened()
        {
            tab = 0;
            Refresh();
        }

        protected override void OnKeys()
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.Q)) SetTab(tab - 1);
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.E)) SetTab(tab + 1);
        }

        protected override void OnTick()
        {
            if (Time.unscaledTime >= nextClock) { nextClock = Time.unscaledTime + 20f; UpdateTimer(); }
            if (fade < 1f)
            {
                fade = Mathf.MoveTowards(fade, 1f, Time.unscaledDeltaTime * 4f);
                float e = 1f - (1f - fade) * (1f - fade);
                for (int i = 0; i < listRoot.childCount; i++)
                {
                    var row = (RectTransform)listRoot.GetChild(i);
                    var g = row.GetComponent<CanvasGroup>();
                    float local = Mathf.Clamp01((e - i * 0.08f) / 0.6f);   // staggered, and every row has arrived when e reaches 1
                    if (g != null) g.alpha = local;
                    row.anchoredPosition = new Vector2(Mathf.Lerp(-40f, 0f, local), row.anchoredPosition.y);
                }
            }
        }

        private void SetTab(int t)
        {
            t = Mathf.Clamp(t, 0, TabNames.Length - 1);
            if (t == tab) return;
            tab = t;
            Refresh();
        }

        private void UpdateTimer()
        {
            if (resetValue == null) return;
            if (tab == 0) { resetLabel.text = "RESETS IN"; resetValue.text = TimeToMidnight(); }
            else if (tab == 1)
            {
                int days = ((int)DayOfWeek.Monday - (int)DateTime.UtcNow.DayOfWeek + 7) % 7;
                if (days == 0) days = 7;
                resetLabel.text = "RESETS IN"; resetValue.text = days + (days == 1 ? " DAY" : " DAYS");
            }
            else { resetLabel.text = "AVAILABLE"; resetValue.text = "ALL SEASON"; }
        }

        private void Refresh()
        {
            MarkTabs(tabBg, tabLabel, tab);
            UpdateTimer();

            var list = Current;
            totalXp.text = list.Sum(x => x.xp).ToString("N0");
            doneCount.text = list.Count(x => x.progress >= x.goal) + " / " + list.Length;

            for (int i = listRoot.childCount - 1; i >= 0; i--) Destroy(listRoot.GetChild(i).gameObject);
            const float rowH = 104f, gap = 12f;
            float y = 350f - rowH * 0.5f;   // list is 700 tall, centred
            foreach (var ch in list)
            {
                var row = NewRect("Row", listRoot, new Vector2(0.5f, 0.5f), new Vector2(1180f, rowH), new Vector2(0f, y));
                row.gameObject.AddComponent<CanvasGroup>().alpha = 0f;
                y -= rowH + gap;
                bool done = ch.progress >= ch.goal;

                var bg = Img("Bg", row, Panel); Stretch(bg.rectTransform);
                var side = Img("Side", row, done ? Color.white : new Color(1f, 1f, 1f, 0.35f));
                var sr = side.rectTransform; sr.anchorMin = Vector2.zero; sr.anchorMax = new Vector2(0f, 1f); sr.pivot = new Vector2(0f, 0.5f); sr.sizeDelta = new Vector2(5f, 0f); sr.anchoredPosition = Vector2.zero;

                var marker = Img("Marker", row, done ? Color.white : new Color(1f, 1f, 1f, 0.5f));
                Place(marker.rectTransform, new Vector2(-545f, 0f), new Vector2(16f, 16f)); marker.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

                LabelL(row, ch.text.ToUpperInvariant(), 24, true, Color.white, -510f, 20f, 700f);
                Bar(row, new Vector2(-510f + 260f, -22f), new Vector2(520f, 10f), ch.goal > 0 ? (float)ch.progress / ch.goal : 0f, Color.white);
                LabelL(row, ch.progress.ToString("N0") + " / " + ch.goal.ToString("N0"), 18, true, Dim, -510f + 540f, -22f, 160f);

                LabelR(row, done ? "COMPLETE" : "IN PROGRESS", 13, false, done ? Color.white : Dim, 540f, 24f, 220f);
                var chip = Img("Reward", row, new Color(1f, 1f, 1f, 0.1f)); Place(chip.rectTransform, new Vector2(465f, -16f), new Vector2(170f, 44f));
                LabelC(chip.rectTransform, ch.xp.ToString("N0") + " XP", 20, true, Color.white, Vector2.zero, new Vector2(160f, 30f));
            }

            // a locked bonus row closes the list, as in the lobby's daily card
            var bonus = NewRect("Row", listRoot, new Vector2(0.5f, 0.5f), new Vector2(1180f, 64f), new Vector2(0f, y + 20f));
            bonus.gameObject.AddComponent<CanvasGroup>().alpha = 0f;
            var bb = Img("Bg", bonus, new Color(0.05f, 0.055f, 0.065f, 1f)); Stretch(bb.rectTransform);
            LabelL(bonus, "BONUS CHALLENGE", 20, true, Dim, -560f, 0f, 500f);
            LabelR(bonus, "COMPLETE EVERY CHALLENGE ABOVE TO UNLOCK", 14, false, Faint, 560f, 0f, 700f);
            fade = 0f;
        }
    }
}
