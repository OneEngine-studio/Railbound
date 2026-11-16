using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Railbound.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Railbound.UI
{
    /// <summary>
    /// The WEAPONS menu, two views:
    /// LOADOUT - the battle royale class: the primary (found in the field, with weapon mastery), the starting sidearm, melee and lethal,
    /// and the two field upgrades (Sonar, Medkit). Every change goes through the Nakama RPCs (the server checks level and coins); the
    /// rules (unlock levels, upgrade prices and effects) come from the server's tables.
    /// ARMORY - every weapon of the game with its real stats (WeaponCatalogAsset), its mastery, and a 3D preview that can be rotated.
    /// </summary>
    public class LobbyWeaponsScreen : LobbyMenuScreen
    {
        protected override string Title => "WEAPONS";
        protected override string Subtitle => "BATTLE ROYALE LOADOUT  &  ARMORY";
        protected override int NavIndex => 2;
        protected override string Hints => "TAB  LOADOUT / ARMORY      W S  BROWSE      Q E  CATEGORY      DRAG  ROTATE      ESC  BACK";

        private static readonly WeaponCategory[] Categories = { WeaponCategory.Pistol, WeaponCategory.Smg, WeaponCategory.Shotgun, WeaponCategory.Sniper, WeaponCategory.Melee };

        private WeaponCatalogAsset catalog;
        private WeaponPreviewStage stage;
        private int view;                       // 0 loadout, 1 armory
        private RectTransform loadoutRoot, armoryRoot;
        private CanvasGroup loadoutGroup, armoryGroup;
        private readonly Image[] viewBg = new Image[2];
        private readonly TextMeshProUGUI[] viewLabel = new TextMeshProUGUI[2];
        private float viewT = 1f;

        // thumbnails of the catalog's weapons, rendered once by the preview stage (side view)
        private static readonly Dictionary<string, RenderTexture> thumbs = new Dictionary<string, RenderTexture>();
        private bool capturing;

        protected override void BuildContent(RectTransform c)
        {
            catalog = WeaponCatalogAsset.Load();

            // view switch, on the title row
            for (int i = 0; i < 2; i++)
            {
                int index = i;
                var bg = Img(i == 0 ? "View Loadout" : "View Armory", root, PanelHi);
                Place(bg.rectTransform, new Vector2(440f + i * 168f, 410f), new Vector2(160f, 44f));
                bg.raycastTarget = true;
                var b = bg.gameObject.AddComponent<Button>(); b.targetGraphic = bg; b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => SetView(index));
                UIFeedback.Attach(bg.gameObject, 1.02f, UISfx.Kind.Open);
                var l = Txt("Label", bg.rectTransform, i == 0 ? "LOADOUT" : "ARMORY", 17, Dim, TextAlignmentOptions.Center);
                l.fontStyle = FontStyles.Bold; l.characterSpacing = 6f; Stretch(l.rectTransform);
                viewBg[i] = bg; viewLabel[i] = l;
            }

            loadoutRoot = NewRect("Loadout", c, new Vector2(0.5f, 0.5f), c.sizeDelta, Vector2.zero);
            loadoutGroup = loadoutRoot.gameObject.AddComponent<CanvasGroup>();
            armoryRoot = NewRect("Armory", c, new Vector2(0.5f, 0.5f), c.sizeDelta, Vector2.zero);
            armoryGroup = armoryRoot.gameObject.AddComponent<CanvasGroup>();
            BuildLoadout(loadoutRoot);
            BuildArmory(armoryRoot);
        }

        // ---------------- open / close ----------------
        protected override void OnOpened()
        {
            if (stage == null) stage = WeaponPreviewStage.Create();
            stage.SetActive(true);
            loPreview.texture = stage.Texture;
            arPreview.texture = stage.Texture;
            PlayerProgression.Changed += OnProgressChanged;
            if (PlayerProgression.UsesServer && PlayerProgression.Tables == null) RequestProfile();

            view = 0; tab = 0; focus = 0; viewT = 1f; revealMiddle = true; previewId = "";
            ApplyView(true);
            RebuildList();
            if (thumbs.Count == 0 && catalog != null) StartCoroutine(CaptureThumbs());
            else { SelectSlot(selectedSlot, false); RefreshLoadout(); }
        }

        protected override void OnClosing()
        {
            PlayerProgression.Changed -= OnProgressChanged;
            if (stage != null) stage.SetActive(false);
        }

        protected override void OnDestroy()
        {
            PlayerProgression.Changed -= OnProgressChanged;
            if (stage != null) stage.Dispose();
            base.OnDestroy();
        }

        private bool profileRequested;
        private async void RequestProfile()
        {
            if (profileRequested) return;
            profileRequested = true;
            try { await PlayerProgression.LoadAsync(); }
            catch (Exception e) { Debug.LogWarning("[Weapons] Profile load failed: " + e.Message); }
            profileRequested = false;
            if (IsOpen) RefreshLoadout();
        }

        private void OnProgressChanged() { if (IsOpen) { RefreshLoadout(); if (view == 1) Refresh(false); } }

        private IEnumerator CaptureThumbs()
        {
            capturing = true;
            loPreview.color = new Color(1f, 1f, 1f, 0f);
            bool first = true;
            foreach (var w in catalog.weapons)
            {
                if (w == null || w.prefab == null || string.IsNullOrEmpty(w.id) || thumbs.ContainsKey(w.id)) continue;
                stage.Show(w.prefab);
                // HDRP needs a frame to warm up new renderers; the first one also lets the stage camera's exposure settle
                for (int f = first ? 8 : 2; f > 0; f--) yield return null;
                first = false;
                if (stage.Texture == null) break;
                var rt = new RenderTexture(480, 310, 0, RenderTextureFormat.ARGB32) { name = "Thumb " + w.id };
                rt.Create();
                Graphics.Blit(stage.Texture, rt);
                thumbs[w.id] = rt;
            }
            stage.Show(null);
            capturing = false;
            SelectSlot(selectedSlot, false);
            RefreshLoadout();
            RefreshArmoryThumbs();
        }

        // ---------------- views ----------------
        private void SetView(int v)
        {
            v = Mathf.Clamp(v, 0, 1);
            if (v == view) return;
            view = v;
            viewT = 0f;
            ApplyView(false);
            if (view == 0) SelectSlot(selectedSlot, false); else Refresh(true);
        }

        private void ApplyView(bool instant)
        {
            for (int i = 0; i < 2; i++)
            {
                viewBg[i].color = i == view ? Color.white : PanelHi;
                viewLabel[i].color = i == view ? Color.black : Dim;
            }
            loadoutRoot.gameObject.SetActive(view == 0);
            armoryRoot.gameObject.SetActive(view == 1);
            if (instant) viewT = 1f;
        }

        protected override void OnTick()
        {
            float dt = Time.unscaledDeltaTime;
            if (viewT < 1f)
            {
                viewT = Mathf.MoveTowards(viewT, 1f, dt * 4.5f);
                float e = EaseOut(viewT);
                var g = view == 0 ? loadoutGroup : armoryGroup;
                var r = view == 0 ? loadoutRoot : armoryRoot;
                g.alpha = e;
                r.anchoredPosition = new Vector2((1f - e) * (view == 0 ? -40f : 40f), 0f);
            }
            if (stage != null && !capturing) stage.Tick(dt);
            TickLoadout(dt);
            TickArmory(dt);
        }

        protected override void OnKeys()
        {
            if (Input.GetKeyDown(KeyCode.Tab)) { SetView(1 - view); return; }
            if (view == 0)
            {
                if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) SelectSlot(Mathf.Max(0, selectedSlot - 1), true);
                else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) SelectSlot(Mathf.Min(SlotIds.Length - 1, selectedSlot + 1), true);
                return;
            }
            if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.LeftArrow)) SetTab(tab - 1);
            else if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.RightArrow)) SetTab(tab + 1);
            else if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) SetFocus(focus - 1);
            else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) SetFocus(focus + 1);
        }

        // ---------------- test hooks (LobbyShotAid) ----------------
        public void TestSelectSlot(int slot) { if (view != 0) SetView(0); SelectSlot(slot, true); }
        public void TestSetView(int v) { SetView(v); }
        public void TestUpgrade(string skill) { Upgrade(skill); }
        public void TestEquip(string slot, string id) { Equip(slot, id, "equip:" + slot + ":" + id, null); }

        // =====================================================================================================
        // LOADOUT
        // =====================================================================================================
        // slots, top to bottom: primary, sidearm, melee, lethal, then the two field upgrades side by side
        private static readonly string[] SlotIds = { "primary", "sidearm", "melee", "lethal", "sonar", "medkit" };
        private static readonly string[] SlotTitles = { "PRIMARY", "SIDEARM", "MELEE", "LETHAL", "FIELD UPGRADE", "FIELD UPGRADE" };

        private sealed class SlotCard
        {
            public RectTransform rect;
            public Image bg, accent;
            public Image[] outline;
            public TextMeshProUGUI title, name, sub, badge;
            public RawImage thumb;
            public Image icon;
            public Image[] pips;
            public Image dot;
            public bool hovered;
        }

        private readonly SlotCard[] cards = new SlotCard[6];
        private int selectedSlot = 1;
        private TextMeshProUGUI levelText, coinsText, syncText;
        private Image syncDot;
        private float shownCoins = -1f;

        // middle panel (built again for every slot) and the preview on the right
        private RectTransform middle;
        private RawImage loPreview;
        private Image loBigIcon;
        private TextMeshProUGUI loKind, loName, loBlurb, loFoot;
        private CanvasGroup loPreviewGroup;
        private float previewT = 1f;
        private string previewId = "";
        private string pending = "";            // the operation waiting for the server ("equip:sidearm:p226dual", "upgrade:sonar")
        private RectTransform pendingSpinner;

        private void BuildLoadout(RectTransform c)
        {
            // header strip: level, coins and where the loadout is kept
            var head = NewRect("Header", c, new Vector2(0.5f, 0.5f), new Vector2(1760f, 44f), new Vector2(0f, 362f));
            LabelL(head, "CUSTOM LOADOUT", 22, true, Color.white, -880f, 0f, 400f);
            LabelL(head, "STARTING EQUIPMENT FOR EVERY BATTLE ROYALE DROP", 13, false, Faint, -560f, -1f, 520f);
            syncDot = Img("SyncDot", head, Good); Place(syncDot.rectTransform, new Vector2(280f, 0f), new Vector2(8f, 8f));
            syncText = LabelL(head, "", 13, true, Dim, 294f, 0f, 260f);
            var lvBox = Img("Level", head, new Color(1f, 1f, 1f, 0.1f)); Place(lvBox.rectTransform, new Vector2(612f, 0f), new Vector2(130f, 40f));
            levelText = LabelC(lvBox.rectTransform, "LEVEL 1", 17, true, Color.white, Vector2.zero, new Vector2(126f, 36f));
            var coinBox = Img("Coins", head, new Color(1f, 1f, 1f, 0.1f)); Place(coinBox.rectTransform, new Vector2(790f, 0f), new Vector2(180f, 40f));
            Icon(coinBox.rectTransform, "UI/Lobby/Icon_GameMoney_G", new Vector2(-64f, 0f), new Vector2(26f, 26f), Color.white);
            coinsText = LabelR(coinBox.rectTransform, "0", 19, true, Color.white, 80f, 0f, 130f);
            StaggerIn(head, 0f);

            // left column: the class
            const float colX = -605f, colW = 550f;
            cards[0] = BuildSlotCard(c, 0, new Vector2(colX, 248f), new Vector2(colW, 150f));
            cards[1] = BuildSlotCard(c, 1, new Vector2(colX, 112f), new Vector2(colW, 106f));
            cards[2] = BuildSlotCard(c, 2, new Vector2(colX, -4f), new Vector2(colW, 106f));
            cards[3] = BuildSlotCard(c, 3, new Vector2(colX, -120f), new Vector2(colW, 106f));
            var fu = LabelL(c, "FIELD UPGRADES", 14, true, Dim, -880f, -196f, 300f);
            fu.characterSpacing = 6f;
            cards[4] = BuildSlotCard(c, 4, new Vector2(colX - 140f, -300f), new Vector2(270f, 176f));
            cards[5] = BuildSlotCard(c, 5, new Vector2(colX + 140f, -300f), new Vector2(270f, 176f));
            for (int i = 0; i < cards.Length; i++) StaggerIn(cards[i].rect, 0.05f + i * 0.045f);

            // middle: what the selected slot offers
            middle = NewRect("Middle", c, new Vector2(0.5f, 0.5f), new Vector2(640f, 720f), new Vector2(10f, -36f));

            // right: the preview
            var frame = Img("Preview", c, Panel); Place(frame.rectTransform, new Vector2(615f, -36f), new Vector2(530f, 720f));
            var fr = frame.rectTransform;
            StaggerIn(fr, 0.2f);
            loPreviewGroup = fr.gameObject.AddComponent<CanvasGroup>();
            var top = Img("Edge", fr, new Color(1f, 1f, 1f, 0.5f));
            var tr = top.rectTransform; tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0.5f, 1f); tr.anchoredPosition = Vector2.zero; tr.sizeDelta = new Vector2(0f, 3f);
            loKind = LabelL(fr, "", 14, true, Dim, -240f, 320f, 480f);
            loName = LabelL(fr, "", 40, true, Color.white, -240f, 276f, 480f, 54f);
            var glow = Img("Floor", fr, new Color(1f, 1f, 1f, 0.05f)); glow.sprite = gradient;
            Place(glow.rectTransform, new Vector2(0f, 10f), new Vector2(530f, 300f)); glow.rectTransform.localScale = new Vector2(1f, -1f);
            var raw = new GameObject("Stage", typeof(RectTransform), typeof(RawImage));
            raw.transform.SetParent(fr, false);
            loPreview = raw.GetComponent<RawImage>();
            Place((RectTransform)raw.transform, new Vector2(0f, 60f), new Vector2(510f, 330f));
            loPreview.raycastTarget = true;
            raw.AddComponent<PreviewDrag>().owner = this;
            loBigIcon = Icon(fr, "", new Vector2(0f, 60f), new Vector2(220f, 220f), new Color(1f, 1f, 1f, 0.9f));
            var rule = Img("Rule", fr, Line); Place(rule.rectTransform, new Vector2(0f, -140f), new Vector2(480f, 2f));
            loBlurb = LabelL(fr, "", 16, false, Dim, -240f, -226f, 480f, 150f);
            loBlurb.enableWordWrapping = true; loBlurb.alignment = TextAlignmentOptions.TopLeft; loBlurb.characterSpacing = 1f;
            loFoot = LabelL(fr, "", 12, true, Faint, -240f, -330f, 480f);
        }

        private SlotCard BuildSlotCard(RectTransform parent, int index, Vector2 pos, Vector2 size)
        {
            var sc = new SlotCard();
            sc.rect = NewRect("Slot " + SlotIds[index], parent, new Vector2(0.5f, 0.5f), size, pos);
            sc.bg = Img("Bg", sc.rect, Panel); Stretch(sc.bg.rectTransform); sc.bg.raycastTarget = true;
            var btn = sc.bg.gameObject.AddComponent<Button>(); btn.targetGraphic = sc.bg; btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => SelectSlot(index, true));
            UIFeedback.Attach(sc.bg.gameObject, 1f).silentClick = true;
            OnHover(sc.bg.gameObject, on => sc.hovered = on);
            sc.accent = Img("Accent", sc.rect, Faint);
            var ar = sc.accent.rectTransform; ar.anchorMin = Vector2.zero; ar.anchorMax = new Vector2(0f, 1f); ar.pivot = new Vector2(0f, 0.5f); ar.sizeDelta = new Vector2(4f, 0f); ar.anchoredPosition = Vector2.zero;
            sc.outline = Outline(sc.rect, new Color(1f, 1f, 1f, 0f));

            float w = size.x, h = size.y, left = -w * 0.5f + 22f;
            bool upgrade = index >= 4;
            if (!upgrade)
            {
                // the weapon picture on the right
                var raw = new GameObject("Thumb", typeof(RectTransform), typeof(RawImage));
                raw.transform.SetParent(sc.rect, false);
                sc.thumb = raw.GetComponent<RawImage>(); sc.thumb.raycastTarget = false;
                float th = h - 16f, tw = th * 480f / 310f;
                Place((RectTransform)raw.transform, new Vector2(w * 0.5f - tw * 0.5f - 8f, 0f), new Vector2(tw, th));
                sc.icon = Icon(sc.rect, "", new Vector2(w * 0.5f - 90f, 0f), new Vector2(140f, h - 30f), new Color(1f, 1f, 1f, 0.85f));
                sc.title = LabelL(sc.rect, SlotTitles[index], 13, true, Dim, left, h * 0.5f - 20f, 260f);
                sc.title.characterSpacing = 6f;
                sc.name = LabelL(sc.rect, "", index == 0 ? 30 : 28, true, Color.white, left, index == 0 ? 14f : 2f, 330f, 40f);
                sc.sub = LabelL(sc.rect, "", 13, false, Dim, left, index == 0 ? -22f : -30f, 330f);
                if (index == 0)
                {
                    sc.badge = LabelL(sc.rect, "", 13, true, Color.white, left, -50f, 330f);
                }
            }
            else
            {
                sc.icon = Icon(sc.rect, index == 4 ? "HUD/Sonar_Device" : "HUD/Hud_Icon_Medkit", new Vector2(-w * 0.5f + 52f, h * 0.5f - 52f), new Vector2(64f, 64f), Color.white);
                sc.title = LabelL(sc.rect, SlotTitles[index], 11, true, Faint, -w * 0.5f + 96f, h * 0.5f - 30f, 170f);
                sc.title.characterSpacing = 4f;
                sc.name = LabelL(sc.rect, index == 4 ? "SONAR" : "MEDKIT", 24, true, Color.white, -w * 0.5f + 96f, h * 0.5f - 58f, 170f, 34f);
                sc.sub = LabelL(sc.rect, "", 13, true, Dim, left, -26f, w - 44f);
                sc.pips = new Image[3];
                float pw = (w - 44f - 16f) / 3f;
                for (int k = 0; k < 3; k++)
                {
                    sc.pips[k] = Img("Pip", sc.rect, new Color(1f, 1f, 1f, 0.15f));
                    Place(sc.pips[k].rectTransform, new Vector2(left + pw * 0.5f + k * (pw + 8f), -54f), new Vector2(pw, 8f));
                }
                sc.badge = LabelL(sc.rect, "", 11, true, Color.white, left + 14f, -h * 0.5f + 16f, w - 60f);
                sc.dot = Img("Dot", sc.rect, Color.white); Place(sc.dot.rectTransform, new Vector2(left + 4f, -h * 0.5f + 16f), new Vector2(7f, 7f));
            }
            return sc;
        }

        // ---------------- state ----------------
        private static ProgressionTables T => PlayerProgression.Tables;
        private static ProgressData P => PlayerProgression.Current;

        private static string NameOf(string id)
        {
            if (id == "none") return "NONE";
            if (T != null && T.weapons != null && T.weapons.TryGetValue(id, out var n) && !string.IsNullOrEmpty(n)) return n.ToUpperInvariant();
            return WeaponIds.Display(id);
        }

        private string Equipped(string slot)
        {
            var l = P.loadout ?? new LoadoutData();
            return slot == "sidearm" ? l.sidearm : slot == "melee" ? l.melee : slot == "lethal" ? l.lethal : "";
        }

        private static string IconFor(string id)
        {
            switch (id)
            {
                case "katana": return "UI/Lobby/Badge_Katana";
                case "grenade": return "HUD/Hud_Icon_Grenade";
                case "sonar": return "HUD/Sonar_Device";
                case "medkit": return "HUD/Hud_Icon_Medkit";
                case "primary": return "UI/Lobby/Icon_TabLoadout";
                case "none": return "";
                default: return "UI/Lobby/Icon_TabLoadout";
            }
        }

        private WeaponCatalogAsset.Entry CatalogEntry(string id)
        {
            return catalog != null ? catalog.weapons.FirstOrDefault(w => w != null && w.id == id) : null;
        }

        /// <summary>The weapon's picture: its thumbnail if the catalog has a model, otherwise an icon.</summary>
        private void ShowPicture(RawImage thumb, Image icon, string id)
        {
            bool has = thumb != null && thumbs.TryGetValue(id ?? "", out var rt) && rt != null;
            if (thumb != null) { thumb.texture = has ? thumbs[id] : null; thumb.enabled = has; }
            if (icon != null) { if (has) icon.enabled = false; else SetIcon(icon, IconFor(id)); }
        }

        private void RefreshLoadout()
        {
            if (levelText == null) return;
            var p = P;
            levelText.text = "LEVEL " + p.level;
            if (shownCoins < 0f) shownCoins = p.coins;

            bool online = PlayerProgression.UsesServer && PlayerProgression.ServerReachable;
            bool ready = T != null;
            syncText.text = !PlayerProgression.UsesServer ? "OFFLINE  |  SIGN IN TO EDIT" : !PlayerProgression.ServerReachable ? "SERVER UNREACHABLE" : ready ? "SAVED TO YOUR PROFILE" : "LOADING PROFILE";
            syncDot.color = online && ready ? Good : (online ? Color.white : Bad);

            // primary: found in the field; shows the most mastered primary
            var best = BestPrimary();
            cards[0].name.text = "FOUND IN THE FIELD";
            cards[0].sub.text = "LOOT A PRIMARY IN THE MATCH.  KILLS LEVEL IT UP.";
            cards[0].badge.text = best != null ? "TOP MASTERY   " + NameOf(best) + "   LV " + PlayerProgression.WeaponOf(best).level : "NO PRIMARY MASTERY YET";
            ShowPicture(cards[0].thumb, cards[0].icon, best ?? "ump");
            if (cards[0].thumb.enabled) cards[0].thumb.color = new Color(1f, 1f, 1f, 0.55f);

            for (int s = 1; s <= 3; s++)
            {
                string slot = SlotIds[s], id = Equipped(slot);
                var card = cards[s];
                card.name.text = NameOf(id);
                if (id == "none") card.sub.text = s == 2 ? "NO MELEE WEAPON" : "NO LETHAL";
                else if (IsWeapon(id)) { var m = PlayerProgression.WeaponOf(id); card.sub.text = "MASTERY LV " + m.level + "   |   " + m.kills + " KILLS"; }
                else card.sub.text = "";
                ShowPicture(card.thumb, card.icon, id);
            }

            for (int s = 4; s <= 5; s++)
            {
                string skill = SlotIds[s];
                var card = cards[s];
                int lv = PlayerProgression.SkillLevel(skill), max = SkillMax(skill);
                card.sub.text = lv == 0 ? "BASE" : "LEVEL " + Roman(lv) + "  /  " + Roman(max);
                for (int k = 0; k < card.pips.Length; k++) card.pips[k].color = k < lv ? Color.white : new Color(1f, 1f, 1f, 0.15f);
                string why = UpgradeBlocker(skill, out _);
                bool can = why == null;
                card.badge.text = lv >= max ? "MAXED" : (can ? "UPGRADE AVAILABLE" : why);
                card.badge.color = can ? Color.white : Faint;
                card.dot.enabled = can;
            }

            SuppressReveal = true;
            BuildMiddle();
            SuppressReveal = false;
            UpdatePreview(false);
        }

        private string BestPrimary()
        {
            string best = null; int bestXp = -1;
            foreach (var id in new[] { "ump", "benelli", "m82" })
            {
                var m = PlayerProgression.WeaponOf(id);
                int score = m.level * 100000 + m.xp;
                if (m.kills > 0 && score > bestXp) { bestXp = score; best = id; }
            }
            return best;
        }

        private static bool IsWeapon(string id) { return WeaponIds.IsKnown(id); }

        private static int SkillMax(string skill)
        {
            return T != null && T.skills != null && T.skills.TryGetValue(skill, out var d) && d != null ? Mathf.Max(1, d.maxLevel) : 3;
        }

        private static string Roman(int n) { return n <= 0 ? "0" : n == 1 ? "I" : n == 2 ? "II" : n == 3 ? "III" : n == 4 ? "IV" : n.ToString(); }

        /// <summary>Why the next upgrade of a skill cannot be bought (null if it can); the price in coins.</summary>
        private static string UpgradeBlocker(string skill, out int cost)
        {
            cost = 0;
            if (T == null || T.skills == null || !T.skills.TryGetValue(skill, out var d) || d == null) return PlayerProgression.UsesServer ? "LOADING" : "OFFLINE";
            int lv = PlayerProgression.SkillLevel(skill);
            if (lv >= d.maxLevel) return "MAX LEVEL";
            cost = lv < d.costs.Count ? d.costs[lv] : 0;
            int req = lv < d.requiredLevel.Count ? d.requiredLevel[lv] : 1;
            if (P.level < req) return "REACH LEVEL " + req;
            if (P.coins < cost) return "NEED " + N(cost - P.coins) + " MORE COINS";
            if (!PlayerProgression.UsesServer) return "OFFLINE";
            return null;
        }

        // ---------------- selection ----------------
        private void SelectSlot(int index, bool sound)
        {
            if (index < 0 || index >= SlotIds.Length) return;
            bool changed = index != selectedSlot;
            selectedSlot = index;
            if (sound && changed) UISfx.Play(UISfx.Kind.Click);
            previewOverride = null;
            SuppressReveal = !(changed || sound || revealMiddle);
            revealMiddle = false;
            BuildMiddle();
            SuppressReveal = false;
            UpdatePreview(true);
        }

        private string previewOverride;     // an option the pointer is on in the middle panel
        private bool revealMiddle;          // the next SelectSlot animates the middle panel in (the screen just opened)

        private string PreviewIdFor()
        {
            if (!string.IsNullOrEmpty(previewOverride)) return previewOverride;
            string slot = SlotIds[selectedSlot];
            if (slot == "primary") return BestPrimary() ?? "ump";
            if (slot == "sonar" || slot == "medkit") return slot;
            return Equipped(slot);
        }

        private void UpdatePreview(bool animate)
        {
            if (loName == null || capturing) return;
            string id = PreviewIdFor();
            if (id == previewId && !animate) { FillPreviewText(id); return; }
            bool changed = id != previewId;
            previewId = id;
            if (changed) previewT = 0f;
            var entry = CatalogEntry(id);
            if (stage != null) stage.Show(entry != null ? entry.prefab : null);
            bool model = entry != null && entry.prefab != null;
            loPreview.enabled = model;
            if (model) loBigIcon.enabled = false; else SetIcon(loBigIcon, IconFor(id));
            FillPreviewText(id);
        }

        private void FillPreviewText(string id)
        {
            var entry = CatalogEntry(id);
            string slot = SlotIds[selectedSlot];
            if (id == "sonar" || id == "medkit")
            {
                loKind.text = "FIELD UPGRADE";
                loName.text = id == "sonar" ? "SONAR" : "MEDKIT";
                loBlurb.text = id == "sonar"
                    ? "A pulse that sweeps the area around you and shows every enemy inside it through walls for a few seconds. Upgrades widen the scan, shorten the cooldown and add charges."
                    : "Heals you over a short channel. Upgrades heal more per kit, start you with more kits and let you carry more of them.";
                loFoot.text = "LEVEL " + Roman(PlayerProgression.SkillLevel(id)) + "  |  APPLIED WHEN YOU DEPLOY";
                return;
            }
            if (id == "none")
            {
                loKind.text = SlotTitles[selectedSlot];
                loName.text = "NONE";
                loBlurb.text = slot == "melee" ? "Hands free. You can still pick up a melee weapon in the match." : "No lethal at the start. Grenades can still be found as loot.";
                loFoot.text = "";
                return;
            }
            loKind.text = entry != null ? WeaponCatalogAsset.CategoryName(entry.category).TrimEnd('S') : (id == "grenade" ? "LETHAL" : id == "katana" ? "MELEE" : "WEAPON");
            loName.text = NameOf(id);
            string blurb = entry != null && !string.IsNullOrEmpty(entry.blurb) ? entry.blurb
                : id == "katana" ? "A long, fast blade. Wide swings and a heavy hit on every connect."
                : id == "grenade" ? "A frag grenade. Cook it, throw it, and clear a room before you push." : "";
            loBlurb.text = blurb;
            if (IsWeapon(id))
            {
                var m = PlayerProgression.WeaponOf(id);
                int need = T != null ? T.XpForWeaponLevel(m.level) : 0;
                loFoot.text = "MASTERY LV " + m.level + (need > 0 ? "   |   " + N(m.xp) + " / " + N(need) + " XP" : "   |   MAX") + "   |   " + m.kills + " KILLS";
            }
            else loFoot.text = "";
        }

        private void TickLoadout(float dt)
        {
            if (view != 0 || cards[0] == null) return;
            // card states: selected (white outline and accent), hover (lighter), rest
            for (int i = 0; i < cards.Length; i++)
            {
                var sc = cards[i];
                bool sel = i == selectedSlot;
                var bgTarget = sel ? PanelHi : (sc.hovered ? new Color(0.1f, 0.105f, 0.12f, 1f) : Panel);
                sc.bg.color = Color.Lerp(sc.bg.color, bgTarget, 1f - Mathf.Exp(-dt * 16f));
                var acc = sel ? Color.white : (sc.hovered ? new Color(1f, 1f, 1f, 0.6f) : Faint);
                sc.accent.color = Color.Lerp(sc.accent.color, acc, 1f - Mathf.Exp(-dt * 16f));
                float ow = Mathf.Lerp(sc.accent.rectTransform.sizeDelta.x, sel ? 6f : 4f, 1f - Mathf.Exp(-dt * 16f));
                sc.accent.rectTransform.sizeDelta = new Vector2(ow, 0f);
                float oa = Mathf.MoveTowards(sc.outline[0].color.a, sel ? 0.85f : 0f, dt * 6f);
                SetOutline(sc.outline, new Color(1f, 1f, 1f, oa));
                if (sc.dot != null && sc.dot.enabled)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
                    sc.dot.color = new Color(1f, 1f, 1f, 0.4f + 0.6f * pulse);
                }
            }

            // coins count up / down to the real value
            if (coinsText != null)
            {
                float target = P.coins;
                if (shownCoins < 0f) shownCoins = target;
                shownCoins = Mathf.Abs(target - shownCoins) < 1f ? target : Mathf.Lerp(shownCoins, target, 1f - Mathf.Exp(-dt * 6f));
                coinsText.text = N(Mathf.RoundToInt(shownCoins));
            }

            if (previewT < 1f && loPreviewGroup != null)
            {
                previewT = Mathf.MoveTowards(previewT, 1f, dt * 4f);
                float e = EaseOut(previewT);
                loPreview.color = new Color(1f, 1f, 1f, e);
                loBigIcon.rectTransform.localScale = Vector3.one * (0.9f + 0.1f * e);
                var c = loBigIcon.color; c.a = 0.9f * e; loBigIcon.color = c;
                loName.rectTransform.anchoredPosition = new Vector2(loName.rectTransform.anchoredPosition.x, 276f - 10f * (1f - e));
            }

            for (int i = 0; i < rowFx.Count; i++) rowFx[i]?.Invoke(dt);
        }

        // ---------------- middle panel ----------------
        private readonly List<Action<float>> rowFx = new List<Action<float>>();

        private void ClearMiddle()
        {
            ClearReveals(middle);
            rowFx.Clear();
            pendingSpinner = null;
            for (int i = middle.childCount - 1; i >= 0; i--) Destroy(middle.GetChild(i).gameObject);
        }

        private void BuildMiddle()
        {
            if (middle == null) return;
            ClearMiddle();
            string slot = SlotIds[selectedSlot];
            if (slot == "primary") BuildMastery();
            else if (slot == "sonar" || slot == "medkit") BuildUpgrade(slot);
            else BuildPicker(slot);
        }

        private TextMeshProUGUI Header(string title, string sub)
        {
            var t = LabelL(middle, title, 30, true, Color.white, -320f, 334f, 640f, 44f);
            var s = LabelL(middle, sub, 13, false, Dim, -320f, 300f, 640f);
            StaggerIn(t.rectTransform, 0f); StaggerIn(s.rectTransform, 0.03f);
            return t;
        }

        private void BuildStateMessage(string text, bool spinner)
        {
            var r = NewRect("State", middle, new Vector2(0.5f, 0.5f), new Vector2(640f, 120f), new Vector2(0f, 120f));
            if (spinner) Spinner(r, new Vector2(0f, 26f), 40f);
            var t = LabelC(r, text, 15, true, Dim, new Vector2(0f, -22f), new Vector2(620f, 24f));
            t.characterSpacing = 4f;
            StaggerIn(r, 0.05f);
        }

        // --- sidearm / melee / lethal ---
        private void BuildPicker(string slot)
        {
            Header("SELECT " + SlotTitles[selectedSlot], "YOU DEPLOY WITH IT.  UNLOCKS WITH YOUR PLAYER LEVEL.  CHECKED BY THE SERVER.");
            if (T == null || T.loadoutSlots == null || !T.loadoutSlots.TryGetValue(slot, out var options) || options == null)
            {
                BuildStateMessage(PlayerProgression.UsesServer ? "LOADING YOUR PROFILE" : "OFFLINE  -  SIGN IN TO CHANGE YOUR LOADOUT", PlayerProgression.UsesServer);
                return;
            }
            var list = options.OrderBy(kv => kv.Value).ThenBy(kv => kv.Key == "none" ? 0 : 1).ToList();
            const float rowH = 100f, gap = 10f;
            float y = 250f - rowH * 0.5f;
            for (int i = 0; i < list.Count; i++)
            {
                BuildOption(slot, list[i].Key, list[i].Value, new Vector2(0f, y), new Vector2(640f, rowH), 0.06f + i * 0.05f);
                y -= rowH + gap;
            }
        }

        private void BuildOption(string slot, string id, int unlockLevel, Vector2 pos, Vector2 size, float delay)
        {
            bool equipped = Equipped(slot) == id;
            bool locked = P.level < unlockLevel;
            string key = "equip:" + slot + ":" + id;
            var row = NewRect("Option " + id, middle, new Vector2(0.5f, 0.5f), size, pos);
            var bg = Img("Bg", row, equipped ? PanelHi : Panel); Stretch(bg.rectTransform); bg.raycastTarget = true;
            var btn = bg.gameObject.AddComponent<Button>(); btn.targetGraphic = bg; btn.transition = Selectable.Transition.None;
            var fb = UIFeedback.Attach(bg.gameObject, 1f); fb.silentClick = true;
            var accent = Img("Accent", row, equipped ? Color.white : Faint);
            var ar = accent.rectTransform; ar.anchorMin = Vector2.zero; ar.anchorMax = new Vector2(0f, 1f); ar.pivot = new Vector2(0f, 0.5f); ar.sizeDelta = new Vector2(4f, 0f); ar.anchoredPosition = Vector2.zero;

            // picture
            var picBox = Img("PicBox", row, new Color(0f, 0f, 0f, 0.25f)); Place(picBox.rectTransform, new Vector2(-320f + 20f + 66f, 0f), new Vector2(132f, size.y - 16f));
            var raw = new GameObject("Thumb", typeof(RectTransform), typeof(RawImage)); raw.transform.SetParent(picBox.rectTransform, false);
            var thumb = raw.GetComponent<RawImage>(); thumb.raycastTarget = false; Stretch((RectTransform)raw.transform);
            var icon = Icon(picBox.rectTransform, "", Vector2.zero, new Vector2(110f, size.y - 34f), new Color(1f, 1f, 1f, 0.9f));
            ShowPicture(thumb, icon, id);
            if (id == "none") LabelC(picBox.rectTransform, "-", 30, true, Faint, Vector2.zero, new Vector2(120f, 40f));
            if (locked) { thumb.color = new Color(1f, 1f, 1f, 0.3f); icon.color = new Color(1f, 1f, 1f, 0.3f); }

            float tx = -320f + 172f;
            var name = LabelL(row, NameOf(id), 24, true, locked ? Dim : Color.white, tx, 18f, 300f, 34f);
            string sub = id == "none" ? (slot == "melee" ? "HANDS FREE" : "NOTHING AT THE START")
                       : IsWeapon(id) ? "MASTERY LV " + PlayerProgression.WeaponOf(id).level + "   |   " + PlayerProgression.WeaponOf(id).kills + " KILLS" : "";
            LabelL(row, sub, 12, false, Dim, tx, -12f, 300f);
            if (IsWeapon(id) && id != "none")
            {
                var m = PlayerProgression.WeaponOf(id);
                int need = T.XpForWeaponLevel(m.level);
                Bar(row, new Vector2(tx + 110f, -32f), new Vector2(220f, 4f), need > 0 ? m.xp / (float)need : 1f, new Color(1f, 1f, 1f, 0.8f));
            }

            // state on the right
            var chip = Img("State", row, equipped ? Color.white : new Color(1f, 1f, 1f, locked ? 0.06f : 0.12f));
            Place(chip.rectTransform, new Vector2(320f - 20f - 70f, 0f), new Vector2(140f, 40f));
            var chipText = LabelC(chip.rectTransform, equipped ? "EQUIPPED" : locked ? "LEVEL " + unlockLevel : "EQUIP", 14, true, equipped ? Color.black : (locked ? Dim : Color.white), new Vector2(locked ? 12f : 0f, 0f), new Vector2(130f, 30f));
            chipText.characterSpacing = 4f;
            if (locked) Icon(chip.rectTransform, "UI/Icons/Icon_Locked", new Vector2(-48f, 0f), new Vector2(18f, 18f), Dim);
            if (equipped) Icon(row, "UI/Icons/Icon_CheckHex", new Vector2(320f - 20f - 160f, 0f), new Vector2(24f, 24f), Color.white);

            bool hovered = false;
            OnHover(bg.gameObject, on =>
            {
                hovered = on;
                if (on) { previewOverride = id; UpdatePreview(true); }
                else if (previewOverride == id) { previewOverride = null; UpdatePreview(true); }
            });
            rowFx.Add(dt =>
            {
                if (bg == null) return;
                var target = equipped ? PanelHi : (hovered && !locked ? new Color(0.11f, 0.115f, 0.13f, 1f) : Panel);
                bg.color = Color.Lerp(bg.color, target, 1f - Mathf.Exp(-dt * 16f));
                if (!equipped) accent.color = Color.Lerp(accent.color, hovered ? (locked ? new Color(1f, 1f, 1f, 0.35f) : Color.white) : Faint, 1f - Mathf.Exp(-dt * 16f));
                if (pending == key && chipText != null) chipText.text = "SAVING";
            });

            btn.onClick.AddListener(() =>
            {
                if (!string.IsNullOrEmpty(pending)) { UISfx.Play(UISfx.Kind.Error); return; }
                if (equipped) { UISfx.Play(UISfx.Kind.Click); return; }
                if (locked) { Toast(NameOf(id) + " UNLOCKS AT LEVEL " + unlockLevel, true); return; }
                Equip(slot, id, key, chip.rectTransform);
            });
            StaggerIn(row, delay);
        }

        private async void Equip(string slot, string id, string key, RectTransform chip)
        {
            pending = key;
            UISfx.Play(UISfx.Kind.Click);
            if (chip != null) pendingSpinner = Spinner(chip, new Vector2(-50f, 0f), 18f);
            string status;
            try { status = await PlayerProgression.SetLoadoutAsync(slot, id); }
            catch (Exception e) { Debug.LogWarning("[Weapons] Set loadout failed: " + e.Message); status = "error"; }
            pending = "";
            if (!IsOpen) return;
            switch (status)
            {
                case "ok": Toast(SlotTitles[Array.IndexOf(SlotIds, slot)] + "  " + NameOf(id) + "  EQUIPPED", false, UISfx.Kind.Confirm); break;
                case "locked": Toast(NameOf(id) + " IS STILL LOCKED", true); break;
                case "offline": Toast("OFFLINE  -  THE LOADOUT CANNOT BE SAVED", true); break;
                default: Toast("COULD NOT SAVE THE LOADOUT  (" + status.ToUpperInvariant() + ")", true); break;
            }
            RefreshLoadout();
        }

        // --- primary: weapon mastery ---
        private void BuildMastery()
        {
            int per = T != null ? T.weaponXpPerKill : 100, max = T != null ? T.weaponMaxLevel : 20;
            Header("WEAPON MASTERY", "PRIMARIES ARE LOOT.  EVERY KILL WITH A WEAPON GIVES IT " + per + " XP, UP TO LEVEL " + max + ".");
            if (T == null || T.weapons == null || T.weapons.Count == 0)
            {
                BuildStateMessage(PlayerProgression.UsesServer ? "LOADING YOUR PROFILE" : "OFFLINE  -  MASTERY IS KEPT ON THE SERVER", PlayerProgression.UsesServer);
                return;
            }
            var ids = T.weapons.Keys.ToList();
            const float rowH = 70f, gap = 8f;
            float y = 250f - rowH * 0.5f;
            for (int i = 0; i < ids.Count; i++)
            {
                string id = ids[i];
                var m = PlayerProgression.WeaponOf(id);
                int need = T.XpForWeaponLevel(m.level);
                var row = NewRect("Mastery " + id, middle, new Vector2(0.5f, 0.5f), new Vector2(640f, rowH), new Vector2(0f, y));
                var bg = Img("Bg", row, Panel); Stretch(bg.rectTransform); bg.raycastTarget = true;
                UIFeedback.Attach(bg.gameObject, 1f).silentClick = true;
                bool hovered = false;
                OnHover(bg.gameObject, on =>
                {
                    hovered = on;
                    if (on) { previewOverride = id; UpdatePreview(true); }
                    else if (previewOverride == id) { previewOverride = null; UpdatePreview(true); }
                });
                rowFx.Add(dt => { if (bg != null) bg.color = Color.Lerp(bg.color, hovered ? PanelHi : Panel, 1f - Mathf.Exp(-dt * 16f)); });
                var picBox = Img("PicBox", row, new Color(0f, 0f, 0f, 0.25f)); Place(picBox.rectTransform, new Vector2(-320f + 10f + 50f, 0f), new Vector2(100f, rowH - 12f));
                var raw = new GameObject("Thumb", typeof(RectTransform), typeof(RawImage)); raw.transform.SetParent(picBox.rectTransform, false);
                var thumb = raw.GetComponent<RawImage>(); thumb.raycastTarget = false; Stretch((RectTransform)raw.transform);
                var icon = Icon(picBox.rectTransform, "", Vector2.zero, new Vector2(80f, rowH - 24f), Color.white);
                ShowPicture(thumb, icon, id);
                LabelL(row, NameOf(id), 20, true, m.kills > 0 ? Color.white : Dim, -320f + 126f, 12f, 230f, 28f);
                LabelL(row, m.kills + " KILLS", 12, false, Dim, -320f + 126f, -14f, 230f);
                var lv = LabelR(row, "LV " + m.level, 22, true, Color.white, 300f, 12f, 120f, 30f);
                lv.characterSpacing = 3f;
                LabelR(row, need > 0 ? N(m.xp) + " / " + N(need) + " XP" : "MAX LEVEL", 12, false, Dim, 300f, -14f, 200f);
                Bar(row, new Vector2(140f, -26f), new Vector2(150f, 4f), need > 0 ? m.xp / (float)need : 1f, Color.white);
                StaggerIn(row, 0.06f + i * 0.035f);
                y -= rowH + gap;
            }
        }

        // --- sonar / medkit upgrades ---
        private static readonly string[][] EffectRows =
        {
            new[] { "radius", "SCAN RADIUS", "{0} M", "up" }, new[] { "cooldown", "COOLDOWN", "{0} S", "down" }, new[] { "charges", "CHARGES", "{0}", "up" },
        };
        private static readonly string[][] MedkitRows =
        {
            new[] { "heal", "HEAL PER KIT", "{0} HP", "up" }, new[] { "starting", "STARTING KITS", "{0}", "up" }, new[] { "carry", "CARRY LIMIT", "{0}", "up" },
        };

        private void BuildUpgrade(string skill)
        {
            Header(skill == "sonar" ? "SONAR" : "MEDKIT", "FIELD UPGRADE.  BOUGHT WITH COINS, UNLOCKED BY YOUR LEVEL.  APPLIED WHEN YOU DEPLOY.");
            if (T == null || T.skills == null || !T.skills.TryGetValue(skill, out var def) || def == null)
            {
                BuildStateMessage(PlayerProgression.UsesServer ? "LOADING YOUR PROFILE" : "OFFLINE  -  UPGRADES ARE KEPT ON THE SERVER", PlayerProgression.UsesServer);
                return;
            }
            int lv = PlayerProgression.SkillLevel(skill), max = Mathf.Max(1, def.maxLevel);
            bool maxed = lv >= max;

            // level track: BASE - I - II - III
            var track = NewRect("Track", middle, new Vector2(0.5f, 0.5f), new Vector2(640f, 96f), new Vector2(0f, 220f));
            float step = 600f / max;
            var line = Img("Line", track, new Color(1f, 1f, 1f, 0.15f)); Place(line.rectTransform, new Vector2(0f, 10f), new Vector2(600f, 3f));
            var lineFill = Img("Fill", track, Color.white);
            var lf = lineFill.rectTransform; lf.anchorMin = lf.anchorMax = new Vector2(0.5f, 0.5f); lf.pivot = new Vector2(0f, 0.5f);
            lf.anchoredPosition = new Vector2(-300f, 10f); lf.sizeDelta = new Vector2(0f, 3f);
            float fillTarget = step * lv;
            rowFx.Add(dt => { if (lf != null) lf.sizeDelta = new Vector2(Mathf.Lerp(lf.sizeDelta.x, fillTarget, 1f - Mathf.Exp(-dt * 8f)), 3f); });
            for (int k = 0; k <= max; k++)
            {
                bool reached = k <= lv, current = k == lv;
                var node = Img("Node " + k, track, reached ? Color.white : new Color(0.16f, 0.17f, 0.19f, 1f));
                Place(node.rectTransform, new Vector2(-300f + k * step, 10f), new Vector2(current ? 26f : 18f, current ? 26f : 18f));
                node.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                if (current)
                {
                    var ring = Img("Ring", track, new Color(1f, 1f, 1f, 0.35f));
                    Place(ring.rectTransform, node.rectTransform.anchoredPosition, new Vector2(40f, 40f));
                    ring.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    ring.transform.SetSiblingIndex(node.transform.GetSiblingIndex());
                    var rr = ring.rectTransform;
                    rowFx.Add(dt => { if (rr != null) { float p = Mathf.Repeat(Time.unscaledTime * 0.8f, 1f); rr.localScale = Vector3.one * (0.7f + 0.6f * p); ring.color = new Color(1f, 1f, 1f, 0.45f * (1f - p)); } });
                }
                var lab = LabelC(track, k == 0 ? "BASE" : Roman(k), 15, true, reached ? Color.white : Faint, new Vector2(-300f + k * step, -24f), new Vector2(90f, 22f));
                lab.characterSpacing = 3f;
                if (k > 0)
                {
                    int reqLv = k - 1 < def.requiredLevel.Count ? def.requiredLevel[k - 1] : 1;
                    LabelC(track, "LV " + reqLv, 11, false, P.level >= reqLv ? Dim : Faint, new Vector2(-300f + k * step, -42f), new Vector2(90f, 18f));
                }
            }
            StaggerIn(track, 0.05f);

            // effects: now -> next
            var table = NewRect("Effects", middle, new Vector2(0.5f, 0.5f), new Vector2(640f, 230f), new Vector2(0f, 46f));
            var tbg = Img("Bg", table, Panel); Stretch(tbg.rectTransform);
            LabelL(table, "EFFECT", 12, true, Faint, -296f, 92f, 200f);
            LabelR(table, "NOW", 12, true, Faint, 110f, 92f, 120f);
            LabelR(table, maxed ? "" : "NEXT  (" + Roman(lv + 1) + ")", 12, true, Faint, 296f, 92f, 160f);
            var rows = skill == "sonar" ? EffectRows : MedkitRows;
            for (int r = 0; r < rows.Length; r++)
            {
                float y = 50f - r * 52f;
                string key = rows[r][0];
                float now = T.SkillEffect(skill, lv, key, 0f), next = maxed ? now : T.SkillEffect(skill, lv + 1, key, now);
                LabelL(table, rows[r][1], 17, true, Color.white, -296f, y, 260f);
                LabelR(table, string.Format(rows[r][2], Fmt(now)), 17, false, Dim, 110f, y, 140f);
                if (!maxed)
                {
                    bool better = rows[r][3] == "up" ? next > now : next < now;
                    var nt = LabelR(table, string.Format(rows[r][2], Fmt(next)), 19, true, better ? Good : Color.white, 296f, y, 160f);
                    if (better) Icon(table, "UI/Lobby/Icon_ChevronLeft", new Vector2(150f, y), new Vector2(14f, 14f), Good).rectTransform.localRotation = Quaternion.Euler(0f, 0f, rows[r][3] == "up" ? -90f : 90f);
                    nt.characterSpacing = 3f;
                }
                if (r < rows.Length - 1) { var ln = Img("Line", table, Line); Place(ln.rectTransform, new Vector2(0f, y - 26f), new Vector2(592f, 1f)); }
            }
            StaggerIn(table, 0.1f);

            // requirements and the button
            var req = NewRect("Requirements", middle, new Vector2(0.5f, 0.5f), new Vector2(640f, 90f), new Vector2(0f, -130f));
            if (!maxed)
            {
                int cost = lv < def.costs.Count ? def.costs[lv] : 0;
                int needLv = lv < def.requiredLevel.Count ? def.requiredLevel[lv] : 1;
                bool lvOk = P.level >= needLv, coinOk = P.coins >= cost;
                ReqLine(req, new Vector2(-150f, 0f), "PLAYER LEVEL " + needLv, lvOk, "UI/Icons/Icon_Shield");
                ReqLine(req, new Vector2(165f, 0f), N(cost) + " COINS", coinOk, "UI/Lobby/Icon_GameMoney_G");
            }
            else LabelC(req, "FULLY UPGRADED", 18, true, Color.white, Vector2.zero, new Vector2(600f, 30f)).characterSpacing = 8f;
            StaggerIn(req, 0.14f);

            string blocker = UpgradeBlocker(skill, out int price);
            string pkey = "upgrade:" + skill;
            string label = pending == pkey ? "UPGRADING" : maxed ? "MAX LEVEL" : blocker == null ? "UPGRADE   |   " + N(price) : blocker;
            var btn = MakeButton(middle, label, new Vector2(0f, -250f), new Vector2(640f, 72f), blocker == null && pending != pkey, () => Upgrade(skill), 20f);
            btn.GetComponent<UIFeedback>().silentClick = true;
            if (blocker != null || !string.IsNullOrEmpty(pending))
            {
                btn.interactable = false;
                btn.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);
                btn.GetComponentInChildren<TextMeshProUGUI>().color = Dim;
            }
            else
            {
                // a slow breathing glow on the button that can be pressed
                var glow = Img("Glow", (RectTransform)btn.transform, new Color(1f, 1f, 1f, 0f)); Stretch(glow.rectTransform);
                glow.transform.SetAsFirstSibling();
                var gr = glow.rectTransform; gr.offsetMin = new Vector2(-6f, -6f); gr.offsetMax = new Vector2(6f, 6f);
                rowFx.Add(dt => { if (glow != null) glow.color = new Color(1f, 1f, 1f, 0.12f + 0.1f * Mathf.Sin(Time.unscaledTime * 3f)); });
            }
            if (pending == pkey) pendingSpinner = Spinner((RectTransform)btn.transform, new Vector2(-130f, 0f), 22f);
            StaggerIn((RectTransform)btn.transform, 0.18f);

            LabelC(middle, "COINS ARE EARNED IN MATCHES AND FROM THE BATTLE PASS.  UPGRADES CANNOT BE UNDONE.", 12, false, Faint, new Vector2(0f, -310f), new Vector2(640f, 20f));
        }

        private void ReqLine(RectTransform parent, Vector2 pos, string text, bool ok, string icon)
        {
            var box = Img("Req", parent, new Color(1f, 1f, 1f, 0.06f)); Place(box.rectTransform, pos, new Vector2(300f, 56f));
            Icon(box.rectTransform, icon, new Vector2(-120f, 0f), new Vector2(26f, 26f), ok ? Color.white : Dim);
            LabelL(box.rectTransform, text, 16, true, ok ? Color.white : Dim, -96f, 0f, 200f);
            Icon(box.rectTransform, ok ? "UI/Icons/Icon_CheckHex" : "UI/Icons/Icon_Locked", new Vector2(124f, 0f), new Vector2(22f, 22f), ok ? Good : Bad);
        }

        private static string Fmt(float v) { return Mathf.Approximately(v, Mathf.Round(v)) ? Mathf.RoundToInt(v).ToString() : v.ToString("0.#"); }

        private async void Upgrade(string skill)
        {
            if (!string.IsNullOrEmpty(pending)) return;
            pending = "upgrade:" + skill;
            int before = PlayerProgression.SkillLevel(skill);
            BuildMiddle();
            string status;
            try { status = await PlayerProgression.UpgradeSkillAsync(skill); }
            catch (Exception e) { Debug.LogWarning("[Weapons] Upgrade failed: " + e.Message); status = "error"; }
            pending = "";
            if (!IsOpen) return;
            string name = skill == "sonar" ? "SONAR" : "MEDKIT";
            switch (status)
            {
                case "ok": Toast(name + " UPGRADED TO LEVEL " + Roman(PlayerProgression.SkillLevel(skill)), false, UISfx.Kind.Upgrade); break;
                case "locked": Toast("YOUR LEVEL IS TOO LOW FOR THIS UPGRADE", true); break;
                case "insufficient_coins": Toast("NOT ENOUGH COINS", true); break;
                case "max_level": Toast(name + " IS FULLY UPGRADED", true); break;
                case "offline": Toast("OFFLINE  -  THE UPGRADE WAS NOT BOUGHT", true); break;
                default: Toast("THE UPGRADE FAILED  (" + status.ToUpperInvariant() + ")", true); break;
            }
            RefreshLoadout();
        }

        // =====================================================================================================
        // ARMORY (every weapon, its stats and mastery)
        // =====================================================================================================
        private readonly List<Image> tabBg = new List<Image>();
        private readonly List<TextMeshProUGUI> tabLabel = new List<TextMeshProUGUI>();
        private RectTransform listRoot, detailRoot;
        private CanvasGroup detailGroup;
        private RawImage arPreview;
        private TextMeshProUGUI pName, pCategory, pFireMode, dBlurb, dMastery, dMasteryXp;
        private Image dMasteryFill;
        private readonly TextMeshProUGUI[] statName = new TextMeshProUGUI[5];
        private readonly TextMeshProUGUI[] statValue = new TextMeshProUGUI[5];
        private readonly Image[] statFill = new Image[5];
        private readonly float[] statTarget = new float[5];
        private readonly TextMeshProUGUI[] infoValue = new TextMeshProUGUI[3];
        private int tab;
        private int focus;
        private List<WeaponCatalogAsset.Entry> list = new List<WeaponCatalogAsset.Entry>();
        private float detailT = 1f;
        private float maxDamage = 1f, maxRpm = 1f, maxMag = 1f;

        private void BuildArmory(RectTransform c)
        {
            if (catalog != null && catalog.weapons.Count > 0)
            {
                maxDamage = Mathf.Max(1f, catalog.weapons.Max(w => w.damage));
                maxRpm = Mathf.Max(1f, catalog.weapons.Max(w => w.roundsPerMinute));
                maxMag = Mathf.Max(1f, catalog.weapons.Max(w => w.magazine));
            }

            string[] names = Categories.Select(WeaponCatalogAsset.CategoryName).ToArray();
            BuildTabs(c, names, -880f, 360f, 200f, SetTab, tabBg, tabLabel);

            listRoot = NewRect("List", c, new Vector2(0.5f, 0.5f), new Vector2(430f, 700f), new Vector2(-880f + 215f, -48f));

            var frame = Img("PreviewFrame", c, Panel); Place(frame.rectTransform, new Vector2(-10f, -12f), new Vector2(850f, 628f));
            var edge = Img("Edge", frame.rectTransform, new Color(1f, 1f, 1f, 0.5f));
            var er = edge.rectTransform; er.anchorMin = new Vector2(0f, 1f); er.anchorMax = new Vector2(1f, 1f); er.pivot = new Vector2(0.5f, 1f); er.anchoredPosition = Vector2.zero; er.sizeDelta = new Vector2(0f, 3f);
            var glow = Img("Floor", frame.rectTransform, new Color(1f, 1f, 1f, 0.04f)); glow.sprite = gradient;
            Place(glow.rectTransform, new Vector2(0f, -170f), new Vector2(850f, 280f)); glow.rectTransform.localScale = new Vector2(1f, -1f);

            var raw = new GameObject("Preview", typeof(RectTransform), typeof(RawImage));
            raw.transform.SetParent(frame.rectTransform, false);
            arPreview = raw.GetComponent<RawImage>();
            var rr = (RectTransform)raw.transform; rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one; rr.offsetMin = new Vector2(0f, 40f); rr.offsetMax = new Vector2(0f, -70f);
            arPreview.raycastTarget = true;
            arPreview.color = new Color(1f, 1f, 1f, 0f);
            raw.AddComponent<PreviewDrag>().owner = this;

            pCategory = LabelL(frame.rectTransform, "", 14, false, Dim, -400f, 286f, 500f);
            pName = LabelL(frame.rectTransform, "", 46, true, Color.white, -400f, 242f, 780f, 60f);
            pFireMode = LabelR(frame.rectTransform, "", 15, true, Dim, 400f, 286f, 400f);
            LabelC(frame.rectTransform, "DRAG TO ROTATE   |   PRIMARIES ARE FOUND AS LOOT IN A MATCH", 12, false, Faint, new Vector2(0f, -294f), new Vector2(700f, 20f));

            BuildDetail(c);
        }

        private void BuildDetail(RectTransform c)
        {
            detailRoot = NewRect("Detail", c, new Vector2(0.5f, 0.5f), new Vector2(400f, 784f), new Vector2(680f, -8f));
            detailGroup = detailRoot.gameObject.AddComponent<CanvasGroup>();
            var bg = Img("Bg", detailRoot, Panel); Stretch(bg.rectTransform);
            var edge = Img("Edge", detailRoot, new Color(1f, 1f, 1f, 0.5f));
            var er = edge.rectTransform; er.anchorMin = new Vector2(0f, 1f); er.anchorMax = new Vector2(1f, 1f); er.pivot = new Vector2(0.5f, 1f); er.anchoredPosition = Vector2.zero; er.sizeDelta = new Vector2(0f, 3f);

            const float L = -170f, W = 340f;
            LabelL(detailRoot, "STATS", 14, false, Dim, L, 350f, W);
            string[] names = { "DAMAGE", "FIRE RATE", "RECOIL CONTROL", "ACCURACY", "MAGAZINE" };
            for (int i = 0; i < 5; i++)
            {
                float y = 300f - i * 58f;
                statName[i] = LabelL(detailRoot, names[i], 14, true, Color.white, L, y + 14f, 220f);
                statValue[i] = LabelR(detailRoot, "", 14, true, Dim, L + W, y + 14f, 120f);
                statFill[i] = Bar(detailRoot, new Vector2(0f, y - 10f), new Vector2(W, 8f), 0f, Color.white);
            }

            var rule = Img("Rule", detailRoot, Line); Place(rule.rectTransform, new Vector2(0f, -10f), new Vector2(W, 2f));
            string[] info = { "FIRE MODE", "RESERVE AMMO", "AIMING" };
            for (int i = 0; i < 3; i++)
            {
                float y = -40f - i * 34f;
                LabelL(detailRoot, info[i], 13, false, Faint, L, y, 160f);
                infoValue[i] = LabelR(detailRoot, "", 16, true, Color.white, L + W, y, 200f);
            }

            var rule2 = Img("Rule2", detailRoot, Line); Place(rule2.rectTransform, new Vector2(0f, -146f), new Vector2(W, 2f));
            dBlurb = LabelL(detailRoot, "", 15, false, Dim, L, -206f, W, 100f);
            dBlurb.enableWordWrapping = true; dBlurb.alignment = TextAlignmentOptions.TopLeft; dBlurb.characterSpacing = 1f;

            // mastery of the weapon (server)
            var mBox = Img("Mastery", detailRoot, new Color(1f, 1f, 1f, 0.06f)); Place(mBox.rectTransform, new Vector2(0f, -320f), new Vector2(W + 20f, 92f));
            LabelL(mBox.rectTransform, "MASTERY", 12, true, Faint, L, 28f, 200f);
            dMastery = LabelL(mBox.rectTransform, "", 26, true, Color.white, L, 0f, 200f, 34f);
            dMasteryXp = LabelR(mBox.rectTransform, "", 13, true, Dim, L + W, 2f, 220f);
            dMasteryFill = Bar(mBox.rectTransform, new Vector2(0f, -28f), new Vector2(W, 5f), 0f, Color.white);
        }

        private void TickArmory(float dt)
        {
            if (view != 1 || detailGroup == null) return;
            if (detailT < 1f)
            {
                detailT = Mathf.MoveTowards(detailT, 1f, dt * 5f);
                float e = EaseOut(detailT);
                detailGroup.alpha = e;
                detailRoot.anchoredPosition = new Vector2(680f + (1f - e) * 30f, -8f);
                var col = arPreview.color; col.a = Mathf.Clamp01(e * 1.4f); arPreview.color = col;
            }
            // stat bars grow to their value
            for (int i = 0; i < 5; i++)
            {
                var r = statFill[i].rectTransform;
                float x = Mathf.Lerp(r.anchorMax.x, statTarget[i], 1f - Mathf.Exp(-dt * 10f));
                r.anchorMax = new Vector2(x, 1f);
            }
            for (int i = 0; i < rowBg.Count; i++)
            {
                bool on = i == focus;
                var target = on ? PanelHi : (rowHover[i] ? new Color(0.1f, 0.105f, 0.12f, 1f) : Panel);
                rowBg[i].color = Color.Lerp(rowBg[i].color, target, 1f - Mathf.Exp(-dt * 16f));
            }
        }

        private void SetTab(int t)
        {
            t = Mathf.Clamp(t, 0, Categories.Length - 1);
            if (t == tab) return;
            tab = t; focus = 0;
            RebuildList();
            Refresh(true);
        }

        private void SetFocus(int i)
        {
            if (list.Count == 0) return;
            i = Mathf.Clamp(i, 0, list.Count - 1);
            if (i == focus) return;
            focus = i;
            UISfx.Play(UISfx.Kind.Hover);
            Refresh(true);
        }

        private readonly List<Image> rowBg = new List<Image>();
        private readonly List<Image> rowBar = new List<Image>();
        private readonly List<bool> rowHover = new List<bool>();
        private readonly List<TextMeshProUGUI> rowName = new List<TextMeshProUGUI>();
        private readonly List<RawImage> rowThumb = new List<RawImage>();
        private readonly List<string> rowId = new List<string>();

        private void RebuildList()
        {
            ClearReveals(listRoot);
            for (int i = listRoot.childCount - 1; i >= 0; i--) Destroy(listRoot.GetChild(i).gameObject);
            rowBg.Clear(); rowBar.Clear(); rowName.Clear(); rowHover.Clear(); rowThumb.Clear(); rowId.Clear();
            list = catalog != null ? catalog.weapons.Where(w => w.category == Categories[tab]).ToList() : new List<WeaponCatalogAsset.Entry>();

            if (list.Count == 0)
            {
                var none = LabelL(listRoot, catalog == null ? "THE WEAPON CATALOG IS MISSING" : "NO WEAPONS IN THIS CATEGORY", 16, false, Dim, -200f, 300f, 420f);
                none.enableWordWrapping = true;
                return;
            }

            const float rowH = 96f, gap = 10f;
            for (int i = 0; i < list.Count; i++)
            {
                int index = i;
                var w = list[i];
                var row = NewRect("Weapon " + w.id, listRoot, new Vector2(0.5f, 0.5f), new Vector2(430f, rowH), new Vector2(0f, 350f - rowH * 0.5f - i * (rowH + gap)));
                var bg = Img("Bg", row, Panel); Stretch(bg.rectTransform); bg.raycastTarget = true;
                var btn = bg.gameObject.AddComponent<Button>(); btn.targetGraphic = bg; btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() => SetFocus(index));
                UIFeedback.Attach(bg.gameObject, 1f).silentClick = true;
                rowHover.Add(false);
                OnHover(bg.gameObject, on => { if (index < rowHover.Count) rowHover[index] = on; });
                var bar = Img("Bar", row, Color.white);
                var br = bar.rectTransform; br.anchorMin = Vector2.zero; br.anchorMax = new Vector2(0f, 1f); br.pivot = new Vector2(0f, 0.5f); br.sizeDelta = new Vector2(5f, 0f); br.anchoredPosition = Vector2.zero;
                var raw = new GameObject("Thumb", typeof(RectTransform), typeof(RawImage)); raw.transform.SetParent(row, false);
                var thumb = raw.GetComponent<RawImage>(); thumb.raycastTarget = false;
                Place((RectTransform)raw.transform, new Vector2(215f - 68f, 0f), new Vector2(124f, 80f));
                thumb.texture = thumbs.TryGetValue(w.id, out var t) ? t : null; thumb.enabled = thumb.texture != null;
                var nm = LabelL(row, w.displayName, 24, true, Color.white, -195f, 14f, 270f); nm.characterSpacing = 5f;
                var m = PlayerProgression.WeaponOf(w.id);
                LabelL(row, w.fireMode + (w.isMelee ? "" : "   |   " + w.magazine + " RND") + "   |   LV " + m.level, 12, false, Dim, -195f, -22f, 270f);
                rowBg.Add(bg); rowBar.Add(bar); rowName.Add(nm); rowThumb.Add(thumb); rowId.Add(w.id);
                StaggerIn(row, 0.03f + i * 0.04f);
            }
        }

        private void RefreshArmoryThumbs()
        {
            for (int i = 0; i < rowThumb.Count; i++)
            {
                if (rowThumb[i] == null) continue;
                rowThumb[i].texture = thumbs.TryGetValue(rowId[i], out var t) ? t : null;
                rowThumb[i].enabled = rowThumb[i].texture != null;
            }
        }

        private void Refresh(bool animate)
        {
            MarkTabs(tabBg, tabLabel, tab);
            for (int i = 0; i < rowBg.Count; i++)
            {
                bool on = i == focus;
                rowBar[i].color = on ? Color.white : new Color(1f, 1f, 1f, 0.2f);
                rowName[i].color = on ? Color.white : Dim;
            }

            var w = list.Count > 0 && focus < list.Count ? list[focus] : null;
            if (w == null)
            {
                pName.text = ""; pCategory.text = ""; pFireMode.text = ""; dBlurb.text = "";
                for (int i = 0; i < 5; i++) { statValue[i].text = ""; statTarget[i] = 0f; }
                for (int i = 0; i < 3; i++) infoValue[i].text = "";
                dMastery.text = ""; dMasteryXp.text = "";
                if (stage != null && view == 1) stage.Show(null);
                return;
            }

            if (animate) { detailT = 0f; for (int i = 0; i < 5; i++) SetBar(statFill[i], 0f); }
            pCategory.text = WeaponCatalogAsset.CategoryName(w.category).TrimEnd('S');
            pName.text = w.displayName;
            pFireMode.text = w.fireMode;
            dBlurb.text = w.blurb;

            if (w.isMelee)
            {
                statName[0].text = "DAMAGE"; statValue[0].text = w.damage.ToString("0"); statTarget[0] = w.damage / maxDamage;
                for (int i = 1; i < 5; i++) { statValue[i].text = "-"; statTarget[i] = 0f; }
                infoValue[0].text = "MELEE"; infoValue[1].text = "-"; infoValue[2].text = "-";
            }
            else
            {
                string dmg = w.pellets > 1 ? w.damage.ToString("0") + "  (" + w.pellets + " PELLETS)" : w.damage.ToString("0");
                statValue[0].text = dmg; statTarget[0] = w.damage / maxDamage;
                statValue[1].text = w.roundsPerMinute.ToString("0") + " RPM"; statTarget[1] = w.roundsPerMinute / maxRpm;
                float recoilControl = 1f - Mathf.Clamp01(w.recoil / 0.3f);
                statValue[2].text = Mathf.RoundToInt(recoilControl * 100f).ToString(); statTarget[2] = recoilControl;
                float accuracy = 1f - Mathf.Clamp01(w.spreadPerShot / 1.2f);
                statValue[3].text = Mathf.RoundToInt(accuracy * 100f).ToString(); statTarget[3] = accuracy;
                statValue[4].text = w.magazine.ToString(); statTarget[4] = w.magazine / maxMag;
                infoValue[0].text = w.fireMode; infoValue[1].text = w.reserve.ToString(); infoValue[2].text = w.aimMode;
            }

            var m = PlayerProgression.WeaponOf(w.id);
            int need = T != null ? T.XpForWeaponLevel(m.level) : 0;
            dMastery.text = "LV " + m.level;
            dMasteryXp.text = (need > 0 ? N(m.xp) + " / " + N(need) + " XP" : "MAX LEVEL") + "   |   " + m.kills + " KILLS";
            SetBar(dMasteryFill, need > 0 ? m.xp / (float)need : 1f);

            if (stage != null && view == 1) stage.Show(w.prefab);
        }

        private static void SetBar(Image fill, float amount)
        {
            var r = fill.rectTransform; r.anchorMax = new Vector2(Mathf.Clamp01(amount), 1f);
        }

        // ---------------- dragging the preview ----------------
        private sealed class PreviewDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
        {
            public LobbyWeaponsScreen owner;
            public void OnBeginDrag(PointerEventData e) { if (owner.stage != null) owner.stage.Dragging = true; }
            public void OnDrag(PointerEventData e) { if (owner.stage != null) owner.stage.AddYaw(-e.delta.x * 0.4f); }
            public void OnEndDrag(PointerEventData e) { if (owner.stage != null) owner.stage.Dragging = false; }
        }

        // =====================================================================================================
        /// <summary>
        /// A tiny studio far away from the lobby: a camera that renders into a texture, two lights and the weapon, stripped of its scripts so it
        /// only shows (a weapon prefab would otherwise try to find an owner and an inventory).
        /// </summary>
        private sealed class WeaponPreviewStage
        {
            private static readonly Vector3 Origin = new Vector3(4000f, 4000f, 4000f);
            private GameObject root, holder, current;
            private GameObject shownPrefab;
            private Transform spinner;
            private Camera cam;
            private RenderTexture rt;
            private float swingClock;
            private float spin;           // extra yaw from dragging
            private float release;        // time since the last drag
            public bool Dragging;
            public Texture Texture => rt;

            public static WeaponPreviewStage Create()
            {
                var s = new WeaponPreviewStage();
                s.Build();
                return s;
            }

            private void Build()
            {
                root = new GameObject("[Lobby] Weapon Preview Stage");
                root.transform.position = Origin;
                holder = new GameObject("Holder"); holder.SetActive(false); holder.transform.SetParent(root.transform, false);
                spinner = new GameObject("Spinner").transform; spinner.SetParent(root.transform, false);

                rt = new RenderTexture(1700, 1100, 24, RenderTextureFormat.ARGB32) { name = "WeaponPreview", antiAliasing = 1 };
                rt.Create();

                var camGo = new GameObject("Camera");
                camGo.transform.SetParent(root.transform, false);
                camGo.transform.localPosition = new Vector3(0f, 0.05f, -2.8f);
                cam = camGo.AddComponent<Camera>();
                cam.fieldOfView = 28f; cam.nearClipPlane = 0.1f; cam.farClipPlane = 40f;
                cam.targetTexture = rt;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.02f, 0.02f, 0.024f, 1f);
                cam.allowHDR = true;
                cam.depth = -50;
                var hd = camGo.AddComponent<HDAdditionalCameraData>();
                hd.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
                hd.backgroundColorHDR = new Color(0.02f, 0.02f, 0.024f, 1f);
                hd.antialiasing = HDAdditionalCameraData.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                hd.volumeLayerMask = 1;   // default layer only: the lobby's volumes still apply, nothing else interferes

                MakeLight("Key", new Vector3(-1.6f, 1.6f, -1.8f), new Vector3(0f, 0f, 0f), new Color(1f, 0.97f, 0.92f), 12000f, 60f, true);
                MakeLight("Rim", new Vector3(1.8f, 0.9f, 1.4f), new Vector3(0f, 0f, 0f), new Color(0.92f, 0.95f, 1f), 14000f, 70f, false);
                MakeLight("Fill", new Vector3(1.4f, 0.2f, -2.4f), new Vector3(0f, 0f, 0f), new Color(1f, 1f, 1f), 7000f, 90f, false);
                root.SetActive(false);
            }

            private void MakeLight(string name, Vector3 localPos, Vector3 lookAtLocal, Color color, float candela, float angle, bool shadows)
            {
                var go = new GameObject(name);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = localPos;
                go.transform.rotation = Quaternion.LookRotation(root.transform.TransformPoint(lookAtLocal) - go.transform.position);
                var hd = go.AddHDLight(HDLightTypeAndShape.ConeSpot);
                var l = go.GetComponent<Light>();
                l.color = color; l.spotAngle = angle;
                hd.innerSpotPercent = 40f;
                hd.SetIntensity(candela, LightUnit.Candela);
                hd.EnableShadows(shadows);
                hd.volumetricDimmer = 0f;
            }

            public void SetActive(bool on)
            {
                if (root != null) root.SetActive(on);
                if (!on) Show(null);
            }

            public void Show(GameObject prefab)
            {
                if (prefab != null && prefab == shownPrefab && current != null) return;   // already on the stage
                if (current != null) { Object.Destroy(current); current = null; }
                shownPrefab = prefab;
                spin = 0f; swingClock = 0f; release = 99f;
                if (prefab == null || holder == null) return;

                var go = Object.Instantiate(prefab, holder.transform);   // the holder is inactive: nothing in the prefab wakes up
                StripBehaviours(go);

                // measure it at the stage centre, then scale it so the longest side is about 1.5 m and centre it on the spinner
                spinner.localRotation = Quaternion.identity;
                go.transform.SetParent(spinner, false);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                go.transform.localPosition = Vector3.zero;
                var rends = go.GetComponentsInChildren<Renderer>(false).Where(r => r.enabled && (r is MeshRenderer || r is SkinnedMeshRenderer)).ToArray();
                if (rends.Length > 0)
                {
                    var bounds = rends[0].bounds;
                    foreach (var r in rends) bounds.Encapsulate(r.bounds);
                    float length = Mathf.Max(bounds.size.x, bounds.size.z), height = bounds.size.y;
                    float scale = 1f;
                    if (length > 0.0001f) scale = 1.5f / length;
                    if (height > 0.0001f) scale = Mathf.Min(scale, 0.8f / height);   // compact weapons must not be taller than the frame
                    go.transform.localScale = Vector3.one * scale;
                    go.transform.localPosition = -(bounds.center - spinner.position) * scale;
                }
                go.SetActive(true);
                current = go;
                ApplyYaw();
            }

            // Removes every script, collider, rigidbody and effect from the clone so it only shows. Components that other components
            // [RequireComponent] cannot be removed first, so the removal repeats until nothing is left that can go.
            private static void StripBehaviours(GameObject go)
            {
                for (int pass = 0; pass < 8; pass++)
                {
                    bool removed = false;
                    foreach (var c in go.GetComponentsInChildren<Component>(true))
                    {
                        if (c == null || c is Transform || c is MeshRenderer || c is SkinnedMeshRenderer || c is MeshFilter) continue;
                        if (c is Renderer && !(c is TrailRenderer) && !(c is LineRenderer)) continue;
                        if (RequiredByOther(c)) continue;
                        Object.DestroyImmediate(c);
                        removed = true;
                    }
                    if (!removed) break;
                }
            }

            private static bool RequiredByOther(Component c)
            {
                var type = c.GetType();
                foreach (var other in c.gameObject.GetComponents<Component>())
                {
                    if (other == null || other == c) continue;
                    foreach (RequireComponent req in System.Attribute.GetCustomAttributes(other.GetType(), typeof(RequireComponent), true))
                    {
                        if ((req.m_Type0 != null && req.m_Type0.IsAssignableFrom(type)) ||
                            (req.m_Type1 != null && req.m_Type1.IsAssignableFrom(type)) ||
                            (req.m_Type2 != null && req.m_Type2.IsAssignableFrom(type))) return true;
                    }
                }
                return false;
            }

            public void AddYaw(float d) { spin += d; release = 0f; ApplyYaw(); }

            public void Tick(float dt)
            {
                if (current == null) return;
                if (!Dragging) release += dt;
                // a slow swing around the side view; it starts again a moment after the player lets go
                if (release > 1.5f)
                {
                    swingClock += dt;
                    spin = Mathf.Lerp(spin, 0f, dt * 0.8f);
                }
                ApplyYaw();
            }

            // barrel to the right is 90 degrees; the swing and the player's drag are added to it
            private void ApplyYaw()
            {
                float swing = release > 1.5f ? Mathf.Sin(swingClock * 0.7f) * 22f : 0f;
                spinner.localRotation = Quaternion.Euler(0f, 90f + spin + swing, 0f);
            }

            public void Dispose()
            {
                if (current != null) Object.Destroy(current);
                if (rt != null) { rt.Release(); Object.Destroy(rt); }
                if (root != null) Object.Destroy(root);
            }
        }
    }
}
