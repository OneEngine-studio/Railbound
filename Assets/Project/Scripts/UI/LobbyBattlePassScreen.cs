using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// The BATTLE PASS menu: the season tier the server computed from the player's season XP, and a scrollable track of 40 tiers with
    /// a Free and a Premium reward each (the rewards come from the server's table). Coin rewards of reached tiers are claimed through
    /// railbound_claim_tier (the server pays them, once); cosmetics and the premium track are previews (COMING SOON).
    /// Scroll with the mouse wheel, by dragging, or with A / D.
    /// </summary>
    public class LobbyBattlePassScreen : LobbyMenuScreen
    {
        protected override string Title => "BATTLE PASS";
        protected override string Subtitle => "SEASON 01   |   EARN XP IN EVERY MATCH";
        protected override int NavIndex => 3;
        protected override string Hints => "A D  SCROLL TIERS      MOUSE WHEEL OR DRAG  SCROLL      ESC  BACK";

        private const int DefaultTiers = 40;
        private const float ColW = 176f, CardW = 160f, ViewW = 1650f, ViewH = 600f, ViewX = 105f, ViewY = -110f;

        private static readonly Color Common = new Color(0.62f, 0.66f, 0.72f, 1f);
        private static readonly Color Rare = new Color(0.3f, 0.62f, 1f, 1f);
        private static readonly Color Epic = new Color(0.72f, 0.42f, 1f, 1f);
        private static readonly Color Legendary = new Color(1f, 0.74f, 0.22f, 1f);

        private sealed class TierUI
        {
            public int tier;
            public RewardDef free, premium;
            public Image chip; public TextMeshProUGUI chipLabel;
            public Image freeCard; public Image[] freeOutline;
            public Image foot; public TextMeshProUGUI footLabel; public Image footIcon;
            public Button claim; public Image claimGlow;
            public Image premiumFoot; public TextMeshProUGUI premiumLabel;
        }

        private ScrollRect scroll;
        private RectTransform content, track;
        private TextMeshProUGUI tierNum, tierOf, xpText, coinsText, seasonText, stateText;
        private Image xpFill, progressFill;
        private Button claimAll;
        private TextMeshProUGUI claimAllLabel;
        private readonly List<TierUI> tiers = new List<TierUI>();
        private float keyTarget = float.NaN;
        private float keyUntil;
        private int builtFor = -1;              // how many tiers the track was built with
        private bool builtFromServer;
        private int pendingTier;                // the tier whose claim is waiting for the server (0 none, -1 claim all)
        private float shownCoins = -1f, shownXpFill;
        private RectTransform viewport;

        protected override void BuildContent(RectTransform c)
        {
            // season strip: tier, XP bar, coins and claim all
            var strip = Img("Strip", c, Panel); Place(strip.rectTransform, new Vector2(0f, 318f), new Vector2(1760f, 136f));
            var edge = Img("Edge", strip.rectTransform, new Color(1f, 1f, 1f, 0.5f));
            var er = edge.rectTransform; er.anchorMin = new Vector2(0f, 1f); er.anchorMax = new Vector2(1f, 1f); er.pivot = new Vector2(0.5f, 1f); er.anchoredPosition = Vector2.zero; er.sizeDelta = new Vector2(0f, 3f);
            var s = strip.rectTransform;
            StaggerIn(s, 0f);
            LabelL(s, "YOUR TIER", 14, false, Dim, -850f, 40f, 200f);
            tierNum = LabelL(s, "1", 72, true, Color.white, -850f, -12f, 130f, 90f);
            tierOf = LabelL(s, "/ 40", 26, true, Dim, -740f, -26f, 120f);

            seasonText = LabelL(s, "SEASON XP", 14, false, Dim, -560f, 40f, 400f);
            var track0 = Img("XpTrack", s, new Color(1f, 1f, 1f, 0.12f)); Place(track0.rectTransform, new Vector2(-560f + 300f, 4f), new Vector2(600f, 14f));
            xpFill = Img("XpFill", track0.rectTransform, Color.white);
            var xf = xpFill.rectTransform; xf.anchorMin = Vector2.zero; xf.anchorMax = new Vector2(0f, 1f); xf.offsetMin = xf.offsetMax = Vector2.zero;
            xpText = LabelL(s, "", 16, true, Dim, -560f, -30f, 600f);
            stateText = LabelR(s, "", 13, true, Faint, 60f, -30f, 380f);

            var coinBox = Img("Coins", s, new Color(1f, 1f, 1f, 0.08f)); Place(coinBox.rectTransform, new Vector2(250f, 0f), new Vector2(200f, 64f));
            Icon(coinBox.rectTransform, "UI/Lobby/Icon_GameMoney_G", new Vector2(-70f, 0f), new Vector2(32f, 32f), Color.white);
            coinsText = LabelR(coinBox.rectTransform, "0", 24, true, Color.white, 88f, 0f, 140f);

            claimAll = MakeButton(s, "CLAIM ALL", new Vector2(500f, 0f), new Vector2(260f, 64f), true, ClaimAll, 18f);
            claimAll.GetComponent<UIFeedback>().silentClick = true;
            claimAllLabel = claimAll.GetComponentInChildren<TextMeshProUGUI>();
            var buy = MakeButton(s, "PREMIUM  |  COMING SOON", new Vector2(755f, 0f), new Vector2(230f, 64f), false, () => Toast("THE PREMIUM PASS IS NOT ON SALE YET", true), 14f);
            buy.GetComponent<UIFeedback>().silentClick = true;
            buy.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);
            buy.GetComponentInChildren<TextMeshProUGUI>().color = Dim;

            // row names on the left, track on the right
            var fl = LabelL(c, "FREE", 20, true, Color.white, -870f, ViewY + 112f, 100f);
            var pl = LabelL(c, "PREMIUM", 20, true, Dim, -870f, ViewY - 128f, 120f);
            var plk = Icon(c, "UI/Icons/Icon_Locked", new Vector2(-858f, ViewY - 160f), new Vector2(20f, 20f), Faint);
            var tl = LabelL(c, "TIER", 14, false, Faint, -870f, ViewY + 262f, 100f);
            StaggerIn(fl.rectTransform, 0.1f); StaggerIn(pl.rectTransform, 0.14f); StaggerIn(tl.rectTransform, 0.06f); StaggerIn(plk.rectTransform, 0.14f);

            viewport = NewRect("Viewport", c, new Vector2(0.5f, 0.5f), new Vector2(ViewW, ViewH), new Vector2(ViewX, ViewY));
            var hit = Img("Hit", viewport, new Color(0f, 0f, 0f, 0f)); Stretch(hit.rectTransform); hit.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            StaggerIn(viewport, 0.12f);
        }

        // ---------------- the track ----------------
        private static ProgressionTables T => PlayerProgression.Tables;

        private int TierCount => T != null && T.season != null && T.season.tiers > 0 ? T.season.tiers : DefaultTiers;
        private int XpPerTier => T != null && T.season != null && T.season.xpPerTier > 0 ? T.season.xpPerTier : 1000;

        private RewardDef RewardOf(int tier, bool premium)
        {
            if (T != null && T.season != null && T.season.rewards != null && tier - 1 < T.season.rewards.Count && T.season.rewards[tier - 1] != null)
            {
                var r = premium ? T.season.rewards[tier - 1].premium : T.season.rewards[tier - 1].free;
                if (r != null) return r;
            }
            return null;   // not known yet (no server reply): the card shows a placeholder
        }

        private void BuildTrack()
        {
            int count = TierCount;
            if (content != null) { ClearReveals(content); Destroy(content.gameObject); }
            tiers.Clear();
            builtFor = count;
            builtFromServer = T != null;

            content = NewRect("Track", viewport, new Vector2(0f, 0.5f), new Vector2(ColW * count + 16f, ViewH), Vector2.zero);
            content.pivot = new Vector2(0f, 0.5f); content.anchoredPosition = Vector2.zero;

            // the progress line behind the tier chips
            var line = Img("Line", content, new Color(1f, 1f, 1f, 0.1f));
            var lr = line.rectTransform; lr.anchorMin = lr.anchorMax = new Vector2(0f, 0.5f); lr.pivot = new Vector2(0f, 0.5f);
            lr.anchoredPosition = new Vector2(8f + ColW * 0.5f, 262f); lr.sizeDelta = new Vector2(ColW * (count - 1), 4f);
            progressFill = Img("Fill", content, Color.white);
            var pr = progressFill.rectTransform; pr.anchorMin = pr.anchorMax = new Vector2(0f, 0.5f); pr.pivot = new Vector2(0f, 0.5f);
            pr.anchoredPosition = new Vector2(8f + ColW * 0.5f, 262f); pr.sizeDelta = new Vector2(0f, 4f);

            for (int i = 0; i < count; i++) tiers.Add(BuildTier(i + 1, count));

            scroll = viewport.gameObject.GetComponent<ScrollRect>();
            if (scroll == null) scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = true; scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true; scroll.decelerationRate = 0.08f;
            scroll.scrollSensitivity = 40f;
        }

        private TierUI BuildTier(int tier, int count)
        {
            var ui = new TierUI { tier = tier, free = RewardOf(tier, false), premium = RewardOf(tier, true) };
            // children of the track use left-middle anchors
            float cx = 8f + ColW * 0.5f + (tier - 1) * ColW;
            var col = NewRect("Tier " + tier, content, new Vector2(0f, 0.5f), new Vector2(ColW, ViewH), new Vector2(cx, 0f));
            bool milestone = tier % 10 == 0;

            ui.chip = Img("Chip", col, new Color(0.14f, 0.15f, 0.17f, 1f));
            Place(ui.chip.rectTransform, new Vector2(0f, 262f), new Vector2(milestone ? 66f : 54f, 34f));
            ui.chipLabel = LabelC(ui.chip.rectTransform, tier.ToString("00"), milestone ? 20 : 18, true, Color.white, Vector2.zero, new Vector2(64f, 30f));
            ui.chipLabel.characterSpacing = 2f;

            ui.freeCard = BuildCard(col, new Vector2(0f, 112f), ui.free, false, milestone, out ui.foot, out ui.footLabel, out ui.footIcon);
            ui.freeOutline = Outline(ui.freeCard.rectTransform, new Color(1f, 1f, 1f, 0f));
            BuildCard(col, new Vector2(0f, -136f), ui.premium, true, milestone, out ui.premiumFoot, out ui.premiumLabel, out _);

            // a claim button over the free card's foot (shown only for reached, unclaimed coin tiers)
            var claimBg = Img("Claim", ui.freeCard.rectTransform, Color.white);
            Place(claimBg.rectTransform, new Vector2(0f, -88f), new Vector2(CardW - 12f, 34f));
            claimBg.raycastTarget = true;
            ui.claimGlow = Img("Glow", ui.freeCard.rectTransform, new Color(1f, 1f, 1f, 0f));
            Place(ui.claimGlow.rectTransform, new Vector2(0f, -88f), new Vector2(CardW - 4f, 42f));
            ui.claimGlow.transform.SetSiblingIndex(claimBg.transform.GetSiblingIndex());
            ui.claim = claimBg.gameObject.AddComponent<Button>();
            ui.claim.targetGraphic = claimBg;
            var cc = ui.claim.colors; cc.highlightedColor = new Color(0.85f, 0.85f, 0.85f, 1f); cc.pressedColor = new Color(0.65f, 0.65f, 0.65f, 1f); cc.fadeDuration = 0.06f; ui.claim.colors = cc;
            ui.claim.onClick.AddListener(() => Claim(tier));
            UIFeedback.Attach(claimBg.gameObject, 1.04f).silentClick = true;
            var cl = LabelC(claimBg.rectTransform, "CLAIM", 15, true, Color.black, Vector2.zero, new Vector2(CardW - 20f, 28f));
            cl.characterSpacing = 6f;

            LabelC(col, N(XpPerTier) + " XP", 12, false, Faint, new Vector2(0f, -284f), new Vector2(ColW, 20f));
            StaggerIn(col, 0.15f + Mathf.Min(tier - 1, 10) * 0.03f);
            return ui;
        }

        private Image BuildCard(RectTransform col, Vector2 pos, RewardDef r, bool premium, bool milestone, out Image foot, out TextMeshProUGUI footLabel, out Image footIcon)
        {
            var card = Img(premium ? "Premium" : "Free", col, premium ? new Color(0.075f, 0.08f, 0.095f, 1f) : Panel);
            Place(card.rectTransform, pos, new Vector2(CardW, 220f));
            var cr = card.rectTransform;
            card.raycastTarget = true;
            UIFeedback.Attach(card.gameObject, 1.025f).silentClick = true;

            Color rarity = RarityColor(r);
            var top = Img("Rarity", cr, premium ? new Color(rarity.r, rarity.g, rarity.b, 0.55f) : rarity);
            var tr = top.rectTransform; tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0.5f, 1f); tr.anchoredPosition = Vector2.zero; tr.sizeDelta = new Vector2(0f, milestone ? 5f : 3f);
            var band = Img("Band", cr, new Color(rarity.r, rarity.g, rarity.b, premium ? 0.07f : 0.14f)); band.sprite = gradient;
            Place(band.rectTransform, new Vector2(0f, 56f), new Vector2(CardW - 6f, 104f));

            var icon = Icon(cr, IconOf(r), new Vector2(0f, 52f), new Vector2(milestone ? 84f : 72f, milestone ? 84f : 72f), premium ? new Color(1f, 1f, 1f, 0.45f) : Color.white);
            if (r == null) LabelC(cr, "?", 46, true, Faint, new Vector2(0f, 52f), new Vector2(100f, 70f));

            var name = LabelC(cr, NameOf(r), 14, true, premium ? Dim : Color.white, new Vector2(0f, -18f), new Vector2(CardW - 10f, 36f));
            name.enableWordWrapping = true; name.characterSpacing = 3f;
            var amount = LabelC(cr, AmountOf(r), 13, true, r != null && r.type == "coins" ? Color.white : new Color(rarity.r, rarity.g, rarity.b, premium ? 0.6f : 1f), new Vector2(0f, -50f), new Vector2(CardW - 10f, 22f));
            amount.characterSpacing = 3f;

            foot = Img("Foot", cr, new Color(1f, 1f, 1f, 0.06f));
            Place(foot.rectTransform, new Vector2(0f, -88f), new Vector2(CardW - 12f, 34f));
            footIcon = Icon(foot.rectTransform, "UI/Icons/Icon_Locked", new Vector2(-58f, 0f), new Vector2(16f, 16f), Faint);
            footLabel = LabelC(foot.rectTransform, premium ? "COMING SOON" : "", 12, true, premium ? Faint : Dim, new Vector2(premium ? 8f : 8f, 0f), new Vector2(CardW - 40f, 24f));
            footLabel.characterSpacing = 3f;
            return card;
        }

        private static Color RarityColor(RewardDef r)
        {
            if (r == null) return new Color(1f, 1f, 1f, 0.3f);
            if (r.type == "coins") return Color.white;
            switch ((r.rarity ?? "").ToUpperInvariant())
            {
                case "LEGENDARY": return Legendary;
                case "EPIC": return Epic;
                case "RARE": return Rare;
                default: return Common;
            }
        }

        private static string IconOf(RewardDef r)
        {
            if (r == null) return "";
            if (r.type == "coins") return "UI/Lobby/Icon_GameMoney_G";
            switch ((r.kind ?? "").ToUpperInvariant())
            {
                case "EMBLEM": return "UI/Icons/Icon_Emblem";
                case "CALLING CARD": return "UI/Icons/Icon_CallingCard";
                case "WEAPON CHARM": return "UI/Icons/Icon_WeaponCharm";
                case "OPERATOR LOOK": return "UI/Icons/Icon_OperatorLook";
                case "STICKER": return "UI/Icons/Icon_Sticker";
                default: return "UI/Icons/Icon_Box";
            }
        }

        private static string NameOf(RewardDef r)
        {
            if (r == null) return "REWARD";
            return r.type == "coins" ? "COINS" : (r.kind ?? "COSMETIC").ToUpperInvariant();
        }

        private static string AmountOf(RewardDef r)
        {
            if (r == null) return "";
            return r.type == "coins" ? "x " + N(r.amount) : (r.rarity ?? "").ToUpperInvariant();
        }

        // ---------------- state ----------------
        private int CurrentTier => Mathf.Clamp(PlayerProgression.SeasonTier, 1, TierCount);

        private bool Claimable(TierUI ui)
        {
            return ui.free != null && ui.free.type == "coins" && ui.tier <= CurrentTier && !PlayerProgression.TierClaimed(ui.tier);
        }

        private void RefreshTier(bool scrollToCurrent)
        {
            if (content == null || builtFor != TierCount || builtFromServer != (T != null)) { BuildTrack(); scrollToCurrent = true; }
            int current = CurrentTier, count = TierCount, per = XpPerTier;
            long seasonXp = PlayerProgression.Current.season != null ? PlayerProgression.Current.season.xp : 0;
            long inTier = current >= count ? per : seasonXp - (current - 1) * (long)per;
            tierNum.text = current.ToString();
            tierOf.text = "/ " + count;
            seasonText.text = "SEASON XP   |   " + N(seasonXp) + " TOTAL";
            xpText.text = current >= count ? "MAX TIER REACHED" : N(Math.Max(0, inTier)) + " / " + N(per) + " XP   TO TIER " + (current + 1);
            shownXpFill = Mathf.Clamp01(inTier / (float)per);

            bool online = PlayerProgression.UsesServer && PlayerProgression.ServerReachable;
            stateText.text = !PlayerProgression.UsesServer ? "OFFLINE  |  SIGN IN TO CLAIM" : !PlayerProgression.ServerReachable ? "SERVER UNREACHABLE" : T == null ? "LOADING SEASON" : "";

            int claimable = 0;
            foreach (var ui in tiers)
            {
                bool reached = ui.tier <= current, isCurrent = ui.tier == current, claimed = PlayerProgression.TierClaimed(ui.tier);
                bool canClaim = Claimable(ui) && online;
                if (Claimable(ui)) claimable++;
                ui.chip.color = isCurrent ? Color.white : (reached ? new Color(1f, 1f, 1f, 0.5f) : new Color(0.14f, 0.15f, 0.17f, 1f));
                ui.chipLabel.color = isCurrent || reached ? Color.black : Color.white;
                ui.freeCard.color = isCurrent ? PanelHi : Panel;

                ui.claim.gameObject.SetActive(canClaim && pendingTier != ui.tier && pendingTier != -1);
                ui.foot.gameObject.SetActive(!ui.claim.gameObject.activeSelf);
                bool coins = ui.free != null && ui.free.type == "coins";
                string label; string icon; Color lc;
                if (pendingTier == ui.tier || (pendingTier == -1 && Claimable(ui))) { label = "CLAIMING"; icon = ""; lc = Color.white; }
                else if (coins && claimed) { label = "CLAIMED"; icon = "UI/Icons/Icon_CheckHex"; lc = Good; }
                else if (coins && reached) { label = online ? "CLAIM" : "OFFLINE"; icon = ""; lc = Dim; }
                else if (!coins && reached) { label = "COMING SOON"; icon = ""; lc = Dim; }
                else { label = "TIER " + ui.tier; icon = "UI/Icons/Icon_Locked"; lc = Faint; }
                ui.footLabel.text = label; ui.footLabel.color = lc;
                SetIcon(ui.footIcon, icon);
                if (ui.footIcon.enabled) ui.footIcon.color = lc;
                ui.foot.color = coins && claimed ? new Color(Good.r, Good.g, Good.b, 0.12f) : new Color(1f, 1f, 1f, 0.06f);
            }

            bool busy = pendingTier != 0;
            claimAll.interactable = claimable > 0 && online && !busy;
            claimAllLabel.text = busy ? "CLAIMING" : claimable > 0 ? "CLAIM ALL  (" + claimable + ")" : "ALL CLAIMED";
            claimAll.GetComponent<Image>().color = claimAll.interactable ? Color.white : new Color(1f, 1f, 1f, 0.1f);
            claimAllLabel.color = claimAll.interactable ? Color.black : Dim;

            if (scrollToCurrent)
            {
                Canvas.ForceUpdateCanvases();
                float max = Mathf.Max(0f, content.rect.width - ViewW);
                content.anchoredPosition = new Vector2(-Mathf.Clamp((current - 3) * ColW, 0f, max), 0f);
            }
        }

        protected override void OnOpened()
        {
            if (PlayerProgression.UsesServer && T == null) RequestProfile();
            PlayerProgression.Changed += OnChanged;
            shownCoins = PlayerProgression.Current.coins;
            RefreshTier(true);
            keyTarget = float.NaN;
            if (scroll != null) scroll.velocity = Vector2.zero;
        }

        protected override void OnClosing() { PlayerProgression.Changed -= OnChanged; }

        protected override void OnDestroy()
        {
            PlayerProgression.Changed -= OnChanged;
            base.OnDestroy();
        }

        private void OnChanged() { if (IsOpen) RefreshTier(false); }

        private bool profileRequested;
        private async void RequestProfile()
        {
            if (profileRequested) return;
            profileRequested = true;
            try { await PlayerProgression.LoadAsync(); }
            catch (Exception e) { Debug.LogWarning("[BattlePass] Profile load failed: " + e.Message); }
            profileRequested = false;
            if (IsOpen) RefreshTier(true);
        }

        // ---------------- claiming ----------------
        public void TestClaimAll() { ClaimAll(); }

        private async void Claim(int tier)
        {
            if (pendingTier != 0) { UISfx.Play(UISfx.Kind.Error); return; }
            pendingTier = tier;
            UISfx.Play(UISfx.Kind.Click);
            RefreshTier(false);
            string status;
            try { status = await PlayerProgression.ClaimTierAsync(tier); }
            catch (Exception e) { Debug.LogWarning("[BattlePass] Claim failed: " + e.Message); status = "error"; }
            pendingTier = 0;
            if (!IsOpen) return;
            var r = RewardOf(tier, false);
            ReportClaim(status, r != null ? r.amount : 0, 1);
            RefreshTier(false);
        }

        private async void ClaimAll()
        {
            if (pendingTier != 0) return;
            var list = new List<int>();
            foreach (var ui in tiers) if (Claimable(ui)) list.Add(ui.tier);
            if (list.Count == 0) return;
            pendingTier = -1;
            UISfx.Play(UISfx.Kind.Click);
            RefreshTier(false);
            int paid = 0, ok = 0; string last = "ok";
            foreach (int t in list)
            {
                string status;
                try { status = await PlayerProgression.ClaimTierAsync(t); }
                catch (Exception e) { Debug.LogWarning("[BattlePass] Claim failed: " + e.Message); status = "error"; }
                if (status == "ok") { ok++; var r = RewardOf(t, false); paid += r != null ? r.amount : 0; }
                else if (status != "already_claimed") { last = status; break; }
                if (!IsOpen) break;
            }
            pendingTier = 0;
            if (!IsOpen) return;
            ReportClaim(ok > 0 ? "ok" : last, paid, ok);
            if (ok > 0 && last != "ok") Toast("SOME REWARDS COULD NOT BE CLAIMED  (" + last.ToUpperInvariant() + ")", true);
            RefreshTier(false);
        }

        private void ReportClaim(string status, int coins, int count)
        {
            switch (status)
            {
                case "ok": Toast("+" + N(coins) + " COINS" + (count > 1 ? "   |   " + count + " TIERS CLAIMED" : ""), false, UISfx.Kind.Reward); break;
                case "already_claimed": Toast("ALREADY CLAIMED", true); break;
                case "not_reached": Toast("REACH THIS TIER FIRST", true); break;
                case "not_claimable": Toast("THIS REWARD IS COMING SOON", true); break;
                case "offline": Toast("OFFLINE  -  REWARDS CANNOT BE CLAIMED", true); break;
                default: Toast("THE CLAIM FAILED  (" + (status ?? "error").ToUpperInvariant() + ")", true); break;
            }
        }

        // ---------------- input and animation ----------------
        protected override void OnKeys()
        {
            if (content == null) return;
            float max = Mathf.Max(0f, content.rect.width - ViewW);
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            {
                float from = float.IsNaN(keyTarget) ? -content.anchoredPosition.x : keyTarget;
                keyTarget = Mathf.Clamp(from - ColW * 3f, 0f, max); keyUntil = Time.unscaledTime + 0.6f;
                UISfx.Play(UISfx.Kind.Hover);
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            {
                float from = float.IsNaN(keyTarget) ? -content.anchoredPosition.x : keyTarget;
                keyTarget = Mathf.Clamp(from + ColW * 3f, 0f, max); keyUntil = Time.unscaledTime + 0.6f;
                UISfx.Play(UISfx.Kind.Hover);
            }
        }

        protected override void OnTick()
        {
            float dt = Time.unscaledDeltaTime;
            // the XP bar and the progress line grow to their values; coins count to the real number
            var xf = xpFill.rectTransform;
            xf.anchorMax = new Vector2(Mathf.Lerp(xf.anchorMax.x, shownXpFill, 1f - Mathf.Exp(-dt * 6f)), 1f);
            if (progressFill != null)
            {
                float target = (CurrentTier - 1) * ColW;
                var pr = progressFill.rectTransform;
                pr.sizeDelta = new Vector2(Mathf.Lerp(pr.sizeDelta.x, target, 1f - Mathf.Exp(-dt * 5f)), 4f);
            }
            float coins = PlayerProgression.Current.coins;
            if (shownCoins < 0f) shownCoins = coins;
            shownCoins = Mathf.Abs(coins - shownCoins) < 1f ? coins : Mathf.Lerp(shownCoins, coins, 1f - Mathf.Exp(-dt * 6f));
            coinsText.text = N(Mathf.RoundToInt(shownCoins));

            // the current tier's card pulses its outline; claim buttons breathe
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.2f);
            int current = CurrentTier;
            foreach (var ui in tiers)
            {
                SetOutline(ui.freeOutline, new Color(1f, 1f, 1f, ui.tier == current ? 0.45f + 0.45f * pulse : 0f));
                if (ui.claim.gameObject.activeSelf) ui.claimGlow.color = new Color(1f, 1f, 1f, 0.12f + 0.2f * pulse);
                else ui.claimGlow.color = new Color(1f, 1f, 1f, 0f);
            }

            if (float.IsNaN(keyTarget) || content == null) return;
            if (Time.unscaledTime > keyUntil) { keyTarget = float.NaN; return; }
            scroll.velocity = Vector2.zero;
            float x = Mathf.Lerp(content.anchoredPosition.x, -keyTarget, dt * 10f);
            content.anchoredPosition = new Vector2(x, 0f);
        }
    }
}
