using System;
using System.Collections.Generic;
using Railbound.Social;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// LEADERBOARDS: the four boards of the Nakama module (weekly kills, weekly wins, season XP, all-time kills) in three views (global
    /// top 50, around the player, the player and their friends). A podium for the top 3 with the weekly coin payouts, the ranked table
    /// with the player's own row highlighted (and pinned under the table when it is not on screen), and the time to the weekly reset.
    /// Scores are only ever written by the game server; this screen reads them.
    /// </summary>
    public class LobbyLeaderboardScreen : LobbyMenuScreen
    {
        protected override string Title => "LEADERBOARDS";
        protected override string Subtitle => "SEASON 01   |   SCORES COME FROM YOUR MATCHES";
        protected override int NavIndex => -1;
        protected override string Hints => "Q E  BOARD      1 2 3  VIEW      R  REFRESH      MOUSE WHEEL  SCROLL      ESC  BACK";

        private static readonly string[] Boards = { "weekly_kills", "weekly_wins", "season_xp", "alltime_kills" };
        private static readonly string[] BoardTitles = { "WEEKLY KILLS", "WEEKLY WINS", "SEASON XP", "ALL-TIME KILLS" };
        private static readonly string[] BoardHow =
        {
            "Every elimination in a match counts. The top 3 of the week are paid in coins when the board resets on Monday 00:00 UTC.",
            "Win a match to score. The top 3 of the week are paid in coins when the board resets on Monday 00:00 UTC.",
            "All XP you earn in matches this season. Climb it to show how much you played and how well.",
            "Every elimination since you started. It never resets."
        };
        private static readonly string[] Scopes = { "top", "around_me", "friends" };
        private static readonly string[] ScopeTitles = { "GLOBAL", "AROUND ME", "FRIENDS" };
        private static readonly int[] DefaultPayouts = { 1000, 500, 250 };

        private static readonly Color Gold = new Color(1f, 0.78f, 0.25f, 1f);
        private static readonly Color Silver = new Color(0.82f, 0.86f, 0.92f, 1f);
        private static readonly Color Bronze = new Color(0.86f, 0.55f, 0.32f, 1f);

        private int board, scope;
        private readonly List<Image> boardBg = new List<Image>();
        private readonly List<TextMeshProUGUI> boardLabel = new List<TextMeshProUGUI>();
        private readonly Image[] scopeBg = new Image[3];
        private readonly TextMeshProUGUI[] scopeLabel = new TextMeshProUGUI[3];

        private RectTransform podium, tableContent, tableView, pinned, stateBox;
        private ScrollRect tableScroll;
        private TextMeshProUGUI howText, resetText, payoutText, myRankText, myScoreText, stateText, scoreHeader;
        private RectTransform spinner;
        private LeaderboardPage page;
        private int requestId;
        private bool loading;
        private float nextCountdown;

        protected override void BuildContent(RectTransform c)
        {
            BuildTabs(c, BoardTitles, -880f, 360f, 214f, SetBoard, boardBg, boardLabel);
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                var bg = Img("Scope " + Scopes[i], c, PanelHi);
                Place(bg.rectTransform, new Vector2(880f - 150f * (2 - i) - 72f, 360f), new Vector2(144f, 48f));
                bg.raycastTarget = true;
                var b = bg.gameObject.AddComponent<Button>(); b.targetGraphic = bg; b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => SetScope(index));
                UIFeedback.Attach(bg.gameObject, 1.02f, UISfx.Kind.Open);
                var l = Txt("Label", bg.rectTransform, ScopeTitles[i], 15, Dim, TextAlignmentOptions.Center);
                l.fontStyle = FontStyles.Bold; l.characterSpacing = 4f; Stretch(l.rectTransform);
                scopeBg[i] = bg; scopeLabel[i] = l;
            }

            // left column: the podium and the board's facts
            podium = NewRect("Podium", c, new Vector2(0.5f, 0.5f), new Vector2(600f, 380f), new Vector2(-580f, 120f));
            var info = Img("Info", c, Panel); Place(info.rectTransform, new Vector2(-580f, -260f), new Vector2(600f, 270f));
            var ir = info.rectTransform;
            var edge = Img("Edge", ir, new Color(1f, 1f, 1f, 0.5f));
            var er = edge.rectTransform; er.anchorMin = new Vector2(0f, 1f); er.anchorMax = new Vector2(1f, 1f); er.pivot = new Vector2(0.5f, 1f); er.anchoredPosition = Vector2.zero; er.sizeDelta = new Vector2(0f, 3f);
            LabelL(ir, "HOW TO CLIMB", 13, true, Faint, -270f, 104f, 300f).characterSpacing = 6f;
            howText = LabelL(ir, "", 16, false, Dim, -270f, 58f, 540f, 64f);
            howText.enableWordWrapping = true; howText.alignment = TextAlignmentOptions.TopLeft; howText.characterSpacing = 1f;
            var rule = Img("Rule", ir, Line); Place(rule.rectTransform, new Vector2(0f, 10f), new Vector2(540f, 1f));
            Icon(ir, "UI/Icons/Icon_Timer", new Vector2(-256f, -22f), new Vector2(22f, 22f), Dim);
            resetText = LabelL(ir, "", 16, true, Color.white, -236f, -22f, 500f);
            Icon(ir, "UI/Lobby/Icon_GameMoney_G", new Vector2(-256f, -58f), new Vector2(22f, 22f), Dim);
            payoutText = LabelL(ir, "", 16, true, Color.white, -236f, -58f, 500f);
            var meBox = Img("Me", ir, new Color(1f, 1f, 1f, 0.07f)); Place(meBox.rectTransform, new Vector2(0f, -106f), new Vector2(540f, 54f));
            LabelL(meBox.rectTransform, "YOUR RANK", 12, true, Faint, -256f, 0f, 120f).characterSpacing = 4f;
            myRankText = LabelL(meBox.rectTransform, "-", 24, true, Color.white, -140f, 0f, 120f, 34f);
            LabelL(meBox.rectTransform, "SCORE", 12, true, Faint, 20f, 0f, 80f).characterSpacing = 4f;
            myScoreText = LabelL(meBox.rectTransform, "-", 24, true, Color.white, 100f, 0f, 160f, 34f);
            StaggerIn(podium, 0.08f); StaggerIn(ir, 0.14f);

            // right column: the table
            var table = Img("Table", c, Panel); Place(table.rectTransform, new Vector2(320f, -30f), new Vector2(1120f, 720f));
            var tr = table.rectTransform;
            var tedge = Img("Edge", tr, new Color(1f, 1f, 1f, 0.5f));
            var ter = tedge.rectTransform; ter.anchorMin = new Vector2(0f, 1f); ter.anchorMax = new Vector2(1f, 1f); ter.pivot = new Vector2(0.5f, 1f); ter.anchoredPosition = Vector2.zero; ter.sizeDelta = new Vector2(0f, 3f);
            LabelL(tr, "RANK", 12, true, Faint, -530f, 330f, 100f).characterSpacing = 4f;
            LabelL(tr, "PLAYER", 12, true, Faint, -400f, 330f, 300f).characterSpacing = 4f;
            LabelR(tr, "LEVEL", 12, true, Faint, 300f, 330f, 100f).characterSpacing = 4f;
            scoreHeader = LabelR(tr, "SCORE", 12, true, Faint, 530f, 330f, 200f);
            scoreHeader.characterSpacing = 4f;
            var hr = Img("HeadRule", tr, Line); Place(hr.rectTransform, new Vector2(0f, 312f), new Vector2(1080f, 1f));

            tableView = NewRect("View", tr, new Vector2(0.5f, 0.5f), new Vector2(1100f, 560f), new Vector2(0f, 26f));
            var hit = Img("Hit", tableView, new Color(0f, 0f, 0f, 0f)); Stretch(hit.rectTransform); hit.raycastTarget = true;
            tableView.gameObject.AddComponent<RectMask2D>();
            tableContent = NewRect("Rows", tableView, new Vector2(0.5f, 1f), new Vector2(1100f, 0f), Vector2.zero);
            tableContent.pivot = new Vector2(0.5f, 1f); tableContent.anchoredPosition = Vector2.zero;
            tableScroll = tableView.gameObject.AddComponent<ScrollRect>();
            tableScroll.viewport = tableView; tableScroll.content = tableContent;
            tableScroll.horizontal = false; tableScroll.vertical = true;
            tableScroll.movementType = ScrollRect.MovementType.Clamped; tableScroll.scrollSensitivity = 30f;

            pinned = NewRect("Pinned", tr, new Vector2(0.5f, 0.5f), new Vector2(1100f, 58f), new Vector2(0f, -318f));
            stateBox = NewRect("State", tr, new Vector2(0.5f, 0.5f), new Vector2(900f, 140f), new Vector2(0f, 40f));
            spinner = Spinner(stateBox, new Vector2(0f, 30f), 40f);
            stateText = LabelC(stateBox, "", 16, true, Dim, new Vector2(0f, -24f), new Vector2(880f, 50f));
            stateText.enableWordWrapping = true; stateText.characterSpacing = 3f;
            StaggerIn(tr, 0.1f);
        }

        // ---------------- open / input ----------------
        protected override void OnOpened()
        {
            MarkTabs(boardBg, boardLabel, board);
            MarkScopes();
            Load();
        }

        public void TestSet(int b, int s) { board = Mathf.Clamp(b, 0, 3); scope = Mathf.Clamp(s, 0, 2); MarkTabs(boardBg, boardLabel, board); MarkScopes(); Load(); }

        protected override void OnKeys()
        {
            if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.LeftArrow)) SetBoard(board - 1);
            else if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.RightArrow)) SetBoard(board + 1);
            else if (Input.GetKeyDown(KeyCode.Alpha1)) SetScope(0);
            else if (Input.GetKeyDown(KeyCode.Alpha2)) SetScope(1);
            else if (Input.GetKeyDown(KeyCode.Alpha3)) SetScope(2);
            else if (Input.GetKeyDown(KeyCode.R)) { UISfx.Play(UISfx.Kind.Click); Load(); }
        }

        protected override void OnTick()
        {
            if (Time.unscaledTime >= nextCountdown) { nextCountdown = Time.unscaledTime + 1f; UpdateCountdown(); }
        }

        private void SetBoard(int b)
        {
            b = (b + Boards.Length) % Boards.Length;
            if (b == board) return;
            board = b;
            MarkTabs(boardBg, boardLabel, board);
            Load();
        }

        private void SetScope(int s)
        {
            s = Mathf.Clamp(s, 0, 2);
            if (s == scope) return;
            scope = s;
            MarkScopes();
            Load();
        }

        private void MarkScopes()
        {
            for (int i = 0; i < 3; i++)
            {
                scopeBg[i].color = i == scope ? Color.white : PanelHi;
                scopeLabel[i].color = i == scope ? Color.black : Dim;
            }
        }

        // ---------------- loading ----------------
        private async void Load()
        {
            int id = ++requestId;
            loading = true;
            howText.text = BoardHow[board];
            scoreHeader.text = board == 2 ? "SEASON XP" : board == 1 ? "WINS" : "KILLS";
            ShowState(SocialService.Instance.Online ? "LOADING " + BoardTitles[board] : "", SocialService.Instance.Online);
            if (!SocialService.Instance.Online)
            {
                page = null;
                Render();
                ShowState("OFFLINE  -  SIGN IN TO SEE THE LEADERBOARDS", false);
                return;
            }
            var result = await SocialService.Instance.GetLeaderboardAsync(Boards[board], Scopes[scope], 50);
            if (id != requestId || !IsOpen) return;   // a newer request or the screen closed
            loading = false;
            page = result;
            Render();
        }

        private void ShowState(string text, bool spin)
        {
            stateBox.gameObject.SetActive(!string.IsNullOrEmpty(text));
            stateText.text = text;
            spinner.gameObject.SetActive(spin);
        }

        private void ClearChildren(RectTransform r)
        {
            ClearReveals(r);
            for (int i = r.childCount - 1; i >= 0; i--) Destroy(r.GetChild(i).gameObject);
        }

        private void Render()
        {
            ClearChildren(podium); ClearChildren(tableContent); ClearChildren(pinned);
            var payouts = page != null && page.payouts != null && page.payouts.Count > 0 ? page.payouts : (board <= 1 ? new List<int>(DefaultPayouts) : new List<int>());
            payoutText.text = payouts.Count > 0 ? "WEEKLY REWARDS   #1  " + N(payouts[0]) + "   #2  " + (payouts.Count > 1 ? N(payouts[1]) : "-") + "   #3  " + (payouts.Count > 2 ? N(payouts[2]) : "-") : "NO COIN REWARDS ON THIS BOARD";
            UpdateCountdown();

            if (page == null)
            {
                myRankText.text = "-"; myScoreText.text = "-";
                if (SocialService.Instance.Online && !loading) ShowState("THE LEADERBOARD COULD NOT BE LOADED  -  PRESS R TO TRY AGAIN", false);
                BuildPodium(null, payouts);
                return;
            }
            var me = page.me;
            myRankText.text = me != null && me.rank > 0 ? "#" + N(me.rank) : "-";
            myScoreText.text = me != null ? N(me.score) : "0";

            var rows = page.records ?? new List<LeaderboardEntry>();
            if (rows.Count == 0)
            {
                ShowState(scope == 2 ? "NO SCORES FROM YOU OR YOUR FRIENDS YET  -  PLAY A MATCH TO GET ON THE BOARD"
                        : board <= 1 ? "NO SCORES YET THIS WEEK  -  PLAY A MATCH TO GET ON THE BOARD" : "NO SCORES YET  -  PLAY A MATCH TO GET ON THE BOARD", false);
            }
            else ShowState("", false);

            // podium: the global top 3 (in the friends view: the top 3 of the friends)
            BuildPodium(scope == 1 ? null : rows, payouts);

            const float rowH = 52f;
            tableContent.sizeDelta = new Vector2(1100f, rows.Count * rowH);
            bool meVisible = false;
            for (int i = 0; i < rows.Count; i++)
            {
                var r = BuildRow(tableContent, rows[i], new Vector2(0f, -rowH * 0.5f - i * rowH), i);
                StaggerIn(r, 0.03f + Mathf.Min(i, 14) * 0.025f);
                if (rows[i].isMe) meVisible = true;
            }
            tableScroll.verticalNormalizedPosition = 1f;
            if (scope == 1)
            {
                // around me: start with the player in view
                int at = rows.FindIndex(x => x.isMe);
                if (at > 4 && rows.Count > 10) tableContent.anchoredPosition = new Vector2(0f, Mathf.Min((at - 4) * rowH, tableContent.sizeDelta.y - 560f));
            }
            // the player's row under the table when it is not in the list (or far down it)
            int meIndex = rows.FindIndex(x => x.isMe);
            if (me != null && (!meVisible || meIndex >= 10))
            {
                var r = BuildRow(pinned, me, Vector2.zero, -1);
                StaggerIn(r, 0.2f);
            }
            else if (me == null && rows.Count > 0)
            {
                LabelC(pinned, "YOU ARE NOT ON THIS BOARD YET", 14, true, Faint, Vector2.zero, new Vector2(1000f, 30f)).characterSpacing = 4f;
            }
        }

        private RectTransform BuildRow(RectTransform parent, LeaderboardEntry e, Vector2 pos, int index)
        {
            var row = NewRect("Row " + e.rank, parent, new Vector2(0.5f, index < 0 ? 0.5f : 1f), new Vector2(1080f, 48f), pos);
            Color medal = e.rank == 1 ? Gold : e.rank == 2 ? Silver : e.rank == 3 ? Bronze : Color.white;
            bool top3 = e.rank >= 1 && e.rank <= 3;
            var bg = Img("Bg", row, e.isMe ? new Color(1f, 1f, 1f, 0.16f) : (index % 2 == 0 ? new Color(1f, 1f, 1f, 0.035f) : new Color(1f, 1f, 1f, 0f)));
            Stretch(bg.rectTransform); bg.raycastTarget = true;
            UIFeedback.Attach(bg.gameObject, 1f).silentClick = true;
            var baseColor = bg.color;
            OnHover(bg.gameObject, on => { if (bg != null) bg.color = on && !e.isMe ? new Color(1f, 1f, 1f, 0.08f) : baseColor; });
            if (e.isMe)
            {
                var acc = Img("Accent", row, Color.white);
                var ar = acc.rectTransform; ar.anchorMin = Vector2.zero; ar.anchorMax = new Vector2(0f, 1f); ar.pivot = new Vector2(0f, 0.5f); ar.sizeDelta = new Vector2(4f, 0f); ar.anchoredPosition = Vector2.zero;
            }
            if (top3)
            {
                var chip = Img("Medal", row, medal); Place(chip.rectTransform, new Vector2(-500f, 0f), new Vector2(36f, 28f));
                LabelC(chip.rectTransform, e.rank.ToString(), 17, true, Color.black, Vector2.zero, new Vector2(36f, 26f)).characterSpacing = 0f;
            }
            else LabelL(row, e.rank > 0 ? "#" + N(e.rank) : "-", 17, true, Dim, -520f, 0f, 110f).characterSpacing = 2f;
            Icon(row, "UI/Lobby/Avatar_Operator", new Vector2(-380f, 0f), new Vector2(32f, 32f), new Color(1f, 1f, 1f, 0.85f));
            var name = LabelL(row, string.IsNullOrEmpty(e.username) ? "PLAYER" : e.username, 19, true, e.isMe ? Color.white : new Color(1f, 1f, 1f, 0.88f), -350f, 0f, 520f);
            name.characterSpacing = 2f;
            if (e.isMe)
            {
                var youChip = Img("YouChip", row, Color.white);
                Place(youChip.rectTransform, new Vector2(-350f + Mathf.Min(name.preferredWidth, 480f) + 30f, 0f), new Vector2(40f, 18f));
                LabelC(youChip.rectTransform, "YOU", 11, true, Color.black, Vector2.zero, new Vector2(40f, 16f)).characterSpacing = 2f;
            }
            LabelR(row, e.level > 0 ? "LV " + e.level : "-", 15, true, Dim, 300f, 0f, 100f).characterSpacing = 2f;
            var sc = LabelR(row, N(e.score), 22, true, top3 ? medal : Color.white, 530f, 0f, 200f, 30f);
            sc.characterSpacing = 2f;
            return row;
        }

        private void BuildPodium(List<LeaderboardEntry> rows, List<int> payouts)
        {
            // order on the stage: #2, #1, #3
            int[] order = { 1, 0, 2 };
            float[] xs = { -200f, 0f, 200f };
            float[] hs = { 250f, 300f, 220f };
            for (int k = 0; k < 3; k++)
            {
                int place = order[k];
                var e = rows != null && place < rows.Count && rows[place].rank == place + 1 ? rows[place] : null;
                Color medal = place == 0 ? Gold : place == 1 ? Silver : Bronze;
                float h = hs[k];
                var card = Img("Place " + (place + 1), podium, e != null && e.isMe ? PanelHi : Panel);
                var cr = card.rectTransform;
                Place(cr, new Vector2(xs[k], -190f + h * 0.5f), new Vector2(188f, h));
                var top = Img("Medal", cr, e != null ? medal : new Color(medal.r, medal.g, medal.b, 0.3f));
                var tr = top.rectTransform; tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0.5f, 1f); tr.anchoredPosition = Vector2.zero; tr.sizeDelta = new Vector2(0f, 5f);
                var glow = Img("Glow", cr, new Color(medal.r, medal.g, medal.b, e != null ? 0.16f : 0.05f)); glow.sprite = gradient;
                Place(glow.rectTransform, new Vector2(0f, h * 0.5f - 60f), new Vector2(182f, 110f));
                LabelC(cr, (place + 1).ToString(), place == 0 ? 64 : 52, true, e != null ? medal : new Color(1f, 1f, 1f, 0.15f), new Vector2(0f, h * 0.5f - 52f), new Vector2(180f, 70f)).characterSpacing = 0f;
                if (e != null)
                {
                    Icon(cr, "UI/Lobby/Avatar_Operator", new Vector2(0f, h * 0.5f - 112f), new Vector2(44f, 44f), Color.white);
                    var nm = LabelC(cr, e.username, 17, true, Color.white, new Vector2(0f, h * 0.5f - 152f), new Vector2(176f, 26f));
                    nm.characterSpacing = 1f; nm.enableAutoSizing = true; nm.fontSizeMin = 11f; nm.fontSizeMax = 17f;
                    LabelC(cr, N(e.score), 26, true, medal, new Vector2(0f, h * 0.5f - 186f), new Vector2(176f, 34f)).characterSpacing = 1f;
                    if (e.isMe) { var you = Img("You", cr, Color.white); Place(you.rectTransform, new Vector2(0f, -h * 0.5f + 46f), new Vector2(46f, 18f)); LabelC(you.rectTransform, "YOU", 11, true, Color.black, Vector2.zero, new Vector2(46f, 16f)); }
                }
                else LabelC(cr, "OPEN", 14, true, Faint, new Vector2(0f, h * 0.5f - 130f), new Vector2(176f, 24f)).characterSpacing = 6f;
                if (payouts != null && place < payouts.Count)
                {
                    var pay = Img("Payout", cr, new Color(1f, 1f, 1f, 0.08f)); Place(pay.rectTransform, new Vector2(0f, -h * 0.5f + 18f), new Vector2(176f, 28f));
                    Icon(pay.rectTransform, "UI/Lobby/Icon_GameMoney_G", new Vector2(-50f, 0f), new Vector2(18f, 18f), Color.white);
                    LabelL(pay.rectTransform, N(payouts[place]), 15, true, Color.white, -34f, 0f, 110f).characterSpacing = 2f;
                }
                StaggerIn(cr, 0.05f + k * 0.07f);
            }
        }

        private void UpdateCountdown()
        {
            if (resetText == null) return;
            long resets = page != null ? page.resetsAt : 0;
            if (board > 1 || resets <= 0) { resetText.text = board > 1 ? "THIS BOARD NEVER RESETS" : "RESETS MONDAY 00:00 UTC"; return; }
            var left = DateTimeOffset.FromUnixTimeSeconds(resets) - DateTimeOffset.UtcNow;
            if (left.TotalSeconds <= 0) { resetText.text = "RESETTING NOW"; return; }
            resetText.text = "RESETS IN  " + (left.Days > 0 ? left.Days + "D " : "") + left.Hours.ToString("00") + "H " + left.Minutes.ToString("00") + "M " + left.Seconds.ToString("00") + "S";
        }
    }
}
