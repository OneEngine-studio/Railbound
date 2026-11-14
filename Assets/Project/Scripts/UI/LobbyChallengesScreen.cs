using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// The CHALLENGES menu: Daily / Weekly / Season tabs with a row per challenge (description, progress bar, XP and coin reward), a side
    /// panel with the reset timer and totals, and a CLAIM button on each completed challenge. The progress is real: it is counted from the
    /// player's finished matches by <see cref="ChallengeService"/> and kept with the player in Nakama.
    /// </summary>
    public class LobbyChallengesScreen : LobbyMenuScreen
    {
        protected override string Title => "CHALLENGES";
        protected override string Subtitle => "COMPLETE ASSIGNMENTS TO EARN XP AND COINS";
        protected override int NavIndex => 4;
        protected override string Hints => "A D  CHANGE TAB      ESC  BACK";

        private static readonly string[] TabNames = { "DAILY", "WEEKLY", "SEASON" };

        private readonly List<Image> tabBg = new List<Image>();
        private readonly List<TextMeshProUGUI> tabLabel = new List<TextMeshProUGUI>();
        private RectTransform listRoot;
        private TextMeshProUGUI resetLabel, resetValue, totalXp, doneCount, noteBody;
        private int tab;
        private float nextClock;
        private float fade;   // rows slide in after a tab change

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

            LabelL(side.rectTransform, "HOW IT WORKS", 15, true, Color.white, -240f, -100f, 480f);
            noteBody = LabelL(side.rectTransform, "Play matches to fill the bars. When one is complete, press CLAIM to collect its XP and coins. Daily challenges reset at midnight UTC, weekly ones on Monday.", 16, false, Dim, -240f, -190f, 480f, 150f);
            noteBody.enableWordWrapping = true; noteBody.alignment = TextAlignmentOptions.TopLeft; noteBody.characterSpacing = 1f;
        }

        protected override void OnOpened()
        {
            tab = 0;
            ChallengeService.Changed -= OnChanged; ChallengeService.Changed += OnChanged;
            Refresh(true);
        }

        protected override void OnClosing() { ChallengeService.Changed -= OnChanged; }
        protected override void OnDestroy() { ChallengeService.Changed -= OnChanged; base.OnDestroy(); }

        private void OnChanged() { if (IsOpen) Refresh(false); }

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
            Refresh(true);
        }

        private ChallengeService.Tab CurrentTab => (ChallengeService.Tab)tab;

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

        private void Refresh(bool animate)
        {
            MarkTabs(tabBg, tabLabel, tab);
            UpdateTimer();

            var list = ChallengeService.Get(CurrentTab);
            totalXp.text = list.Where(x => !x.claimed).Sum(x => x.xp).ToString("N0");
            doneCount.text = list.Count(x => x.Complete) + " / " + list.Count;

            for (int i = listRoot.childCount - 1; i >= 0; i--) Destroy(listRoot.GetChild(i).gameObject);
            const float rowH = 104f, gap = 12f;
            float y = 350f - rowH * 0.5f;   // list is 700 tall, centred
            foreach (var ch in list)
            {
                var row = NewRect("Row", listRoot, new Vector2(0.5f, 0.5f), new Vector2(1180f, rowH), new Vector2(0f, y));
                row.gameObject.AddComponent<CanvasGroup>().alpha = animate ? 0f : 1f;
                y -= rowH + gap;

                var bg = Img("Bg", row, Panel); Stretch(bg.rectTransform);
                var side = Img("Side", row, ch.claimed ? new Color(1f, 1f, 1f, 0.2f) : (ch.Complete ? Color.white : new Color(1f, 1f, 1f, 0.35f)));
                var sr = side.rectTransform; sr.anchorMin = Vector2.zero; sr.anchorMax = new Vector2(0f, 1f); sr.pivot = new Vector2(0f, 0.5f); sr.sizeDelta = new Vector2(5f, 0f); sr.anchoredPosition = Vector2.zero;

                var marker = Img("Marker", row, ch.Complete ? Color.white : new Color(1f, 1f, 1f, 0.5f));
                Place(marker.rectTransform, new Vector2(-545f, 0f), new Vector2(16f, 16f)); marker.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

                LabelL(row, ch.text.ToUpperInvariant(), 24, true, ch.claimed ? Dim : Color.white, -510f, 20f, 700f);
                Bar(row, new Vector2(-510f + 260f, -22f), new Vector2(520f, 10f), ch.goal > 0 ? (float)ch.progress / ch.goal : 0f, ch.claimed ? Dim : Color.white);
                LabelL(row, ch.progress.ToString("N0") + " / " + ch.goal.ToString("N0"), 18, true, Dim, -510f + 540f, -22f, 160f);

                // reward chip, and the state / CLAIM button on the right
                var chip = Img("Reward", row, new Color(1f, 1f, 1f, 0.1f)); Place(chip.rectTransform, new Vector2(300f, 0f), new Vector2(210f, 60f));
                LabelC(chip.rectTransform, ch.xp.ToString("N0") + " XP", 20, true, Color.white, new Vector2(0f, 10f), new Vector2(200f, 26f));
                LabelC(chip.rectTransform, ch.coins.ToString("N0") + " COINS", 13, false, new Color(1f, 0.86f, 0.3f, 1f), new Vector2(0f, -14f), new Vector2(200f, 20f));

                if (ch.CanClaim)
                {
                    string key = ch.key; var t = CurrentTab;
                    MakeButton(row, "CLAIM", new Vector2(480f, 0f), new Vector2(160f, 56f), true, () => { _ = ChallengeService.ClaimAsync(t, key); }, 20f);
                }
                else LabelC(row, ch.claimed ? "CLAIMED" : "IN PROGRESS", 14, true, ch.claimed ? Dim : Faint, new Vector2(480f, 0f), new Vector2(160f, 24f));
            }

            // a locked bonus row closes the daily list, as in the lobby's daily card
            if (tab == 0)
            {
                bool all = list.All(x => x.Complete);
                var bonus = NewRect("Row", listRoot, new Vector2(0.5f, 0.5f), new Vector2(1180f, 64f), new Vector2(0f, y + 20f));
                bonus.gameObject.AddComponent<CanvasGroup>().alpha = animate ? 0f : 1f;
                var bb = Img("Bg", bonus, new Color(0.05f, 0.055f, 0.065f, 1f)); Stretch(bb.rectTransform);
                LabelL(bonus, "BONUS CHALLENGE", 20, true, all ? Color.white : Dim, -560f, 0f, 500f);
                LabelR(bonus, all ? "ALL DAILY CHALLENGES COMPLETE" : "COMPLETE EVERY CHALLENGE ABOVE TO UNLOCK", 14, false, all ? Dim : Faint, 560f, 0f, 700f);
            }
            fade = animate ? 0f : 1f;
        }
    }
}
