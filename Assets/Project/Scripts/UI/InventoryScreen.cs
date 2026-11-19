using System.Collections.Generic;
using JUTPS;
using JUTPS.CameraSystems;
using JUTPS.InventorySystem;
using JUTPS.ItemSystem;
using JUTPS.WeaponSystem;
using Railbound.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// The in-match inventory (TAB, or D-pad up on a gamepad), in place of JU's stock slot grid. It only shows what the player really
    /// carries: the weapons found so far (equip one with a click or 1-6), the ammunition, medkits, sonar charges and throwables, and the
    /// loot lying nearby. The match keeps running: the player can still walk while it is open (WASD), but cannot shoot by accident.
    /// Built at runtime in the game's black-and-white style.
    /// </summary>
    public class InventoryScreen : MonoBehaviour
    {
        public static InventoryScreen Instance { get; private set; }
        public bool IsOpen { get; private set; }
        public static int Equips;   // test counter

        // ---------------- bootstrap ----------------
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void OnSceneLoaded(Scene s, LoadSceneMode m)
        {
            if (Application.isBatchMode) return;
            if (Instance == null)
            {
                var go = new GameObject("[HUD] Inventory");
                DontDestroyOnLoad(go);
                go.AddComponent<InventoryScreen>();
            }
            DisableStockInventory();
        }

        /// <summary>JU's own inventory screen would open on the same key.</summary>
        private static void DisableStockInventory()
        {
            foreach (var ui in FindObjectsByType<JUTPS.InventorySystem.UI.InventoryUIManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (ui == null || ui.IsLootView) continue;
                if (ui.InventoryScreen != null) ui.InventoryScreen.SetActive(false);
                ui.enabled = false;
            }
        }

        // ---------------- palette ----------------
        private static readonly Color Panel = new Color(0.03f, 0.035f, 0.04f, 0.82f);
        private static readonly Color PanelHover = new Color(0.10f, 0.11f, 0.12f, 0.92f);
        private static readonly Color Line = new Color(1f, 1f, 1f, 0.14f);
        private static readonly Color Dim = new Color(1f, 1f, 1f, 0.42f);
        private static readonly Color Soft = new Color(1f, 1f, 1f, 0.7f);
        private static readonly Color Low = new Color(1f, 0.36f, 0.31f, 1f);

        // ---------------- state ----------------
        private Canvas canvas;
        private CanvasGroup group;
        private RectTransform content;
        private Sprite px, gradient;
        private TMP_FontAsset fontBold, fontSemi;
        private Material silhouette;
        private float openedAt;
        private bool restoreInput;
        private JUCharacterController player;
        private JUInventory inventory;
        private WeaponCatalogAsset catalog;

        private class Card
        {
            public RectTransform root;
            public Image bg, frame, icon, sweep;
            public TextMeshProUGUI key, name, cls, ammo, mode, tag;
            public JUHoldableItem item;
            public bool hover;
            public float sweepAt = -10f;
        }
        private readonly List<Card> cards = new List<Card>();
        private const int Slots = 6;

        private TextMeshProUGUI hpValue, medkitValue, medkitHint, sonarValue, sonarHint, throwValue, ammoList, nearbyList, nearbyEmpty, title, subtitle;
        private Image hpFill;
        private float nextNearby;

        private void Awake()
        {
            Instance = this;
            px = MakePixel();
            gradient = MakeGradient();
            fontBold = FindFont("Rajdhani-Bold") ?? TMP_Settings.defaultFontAsset;
            fontSemi = FindFont("Rajdhani-SemiBold") ?? fontBold;
            var sh = Shader.Find("Railbound/UI/Silhouette");
            if (sh != null) silhouette = new Material(sh);
            catalog = WeaponCatalogAsset.Load();
            Build();
            group.alpha = 0f;
            canvas.enabled = false;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        // ---------------- open / close ----------------
        private void Update()
        {
            var pc = LocalPlayer.Controller;
            if (pc != player) { player = pc; inventory = pc != null ? pc.GetComponent<JUInventory>() : null; if (IsOpen) Close(); }
            var health = player != null ? player.GetComponent<JUHealth>() : null;
            bool canOpen = player != null && inventory != null && (health == null || !health.IsDead) && !player.IsDriving;

            var kb = Keyboard.current;
            var pad = Gamepad.current;
            bool toggle = (kb != null && kb.tabKey.wasPressedThisFrame) || (pad != null && pad.dpad.up.wasPressedThisFrame);
            if (toggle) { if (IsOpen) Close(); else if (canOpen) Open(); }
            if (IsOpen && !canOpen) Close();

            // fade
            float target = IsOpen ? 1f : 0f;
            group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime / 0.15f);
            canvas.enabled = group.alpha > 0.001f;
            float slide = (1f - group.alpha) * 24f;
            content.anchoredPosition = new Vector2(0f, -slide);
            if (!IsOpen) return;

            // walk while it is open (the default input is off so a click on a card does not fire the gun)
            if (kb != null && player != null)
            {
                float x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
                float z = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
                player._Move(x, z, kb.leftShiftKey.isPressed);
                for (int i = 0; i < Slots; i++)
                    if (kb[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame && i < cards.Count && cards[i].item != null) Equip(cards[i].item);
            }
            Refresh();
        }

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            openedAt = Time.unscaledTime;
            restoreInput = player.UseDefaultControllerInput;
            player.UseDefaultControllerInput = false;
            player._Move(0f, 0f, false);
            JUCameraController.LockMouse(false, false);
            EnsureEventSystem();
            nextNearby = 0f;
            Refresh();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            if (player != null)
            {
                player._Move(0f, 0f, false);
                if (restoreInput) player.UseDefaultControllerInput = true;
            }
            JUCameraController.LockMouse(true, true);
        }

        private void Equip(JUHoldableItem item)
        {
            if (player == null || item == null || !item.Unlocked) return;
            if (inventory != null && inventory.CurrentRightHandItemID == item.ItemSwitchID) return;
            player.SwitchToItem(item.ItemSwitchID, true);
            Equips++;
            foreach (var c in cards) if (c.item == item) c.sweepAt = Time.unscaledTime;
        }

        // ---------------- data ----------------
        private void Refresh()
        {
            if (inventory == null) return;
            // the weapons carried: right-hand weapons and the melee weapon, in the inventory's order
            var owned = new List<JUHoldableItem>();
            if (inventory.HoldableItensRightHand != null)
                foreach (var it in inventory.HoldableItensRightHand)
                    if (it != null && it.Unlocked && !(it is ThrowableItem)) owned.Add(it);
            int held = inventory.CurrentRightHandItemID;
            float t = Time.unscaledTime - openedAt;
            for (int i = 0; i < cards.Count; i++)
            {
                var c = cards[i];
                var it = i < owned.Count ? owned[i] : null;
                c.item = it;
                float appear = Mathf.Clamp01((t - i * 0.03f) / 0.18f);
                c.root.localScale = Vector3.one * Mathf.Lerp(0.97f, 1f, appear);
                bool equipped = it != null && it.ItemSwitchID == held;
                c.key.text = (i + 1).ToString();
                if (it == null)
                {
                    c.bg.color = new Color(Panel.r, Panel.g, Panel.b, 0.45f);
                    c.frame.color = Line;
                    c.icon.enabled = false;
                    c.name.text = "EMPTY SLOT";
                    c.name.color = new Color(1f, 1f, 1f, 0.22f);
                    c.cls.text = "FIND A WEAPON IN THE WORLD";
                    c.cls.color = new Color(1f, 1f, 1f, 0.18f);
                    c.ammo.text = ""; c.mode.text = ""; c.tag.text = "";
                    continue;
                }
                var entry = CatalogEntry(it);
                c.icon.enabled = it.ItemIcon != null;
                c.icon.sprite = it.ItemIcon;
                c.icon.color = equipped ? Color.white : Soft;
                c.name.text = (entry != null ? entry.displayName : it.ItemName).ToUpperInvariant();
                c.name.color = Color.white;
                c.cls.text = entry != null ? WeaponCatalogAsset.CategoryName(entry.category) : (it is Weapon ? "WEAPON" : "MELEE");
                c.cls.color = Dim;
                if (it is Weapon w)
                {
                    bool low = w.BulletsAmounts <= Mathf.Max(1, w.BulletsPerMagazine / 4);
                    string mag = low ? "<color=#FF5C4F>" + w.BulletsAmounts + "</color>" : w.BulletsAmounts.ToString();
                    c.ammo.text = mag + "<size=60%><color=#8C8C8C>  /  " + w.TotalBullets + "</color></size>";
                    c.mode.text = FireModeName(w.FireMode) + "   |   MAG " + w.BulletsPerMagazine;
                }
                else { c.ammo.text = ""; c.mode.text = "CLOSE QUARTERS"; }
                bool reloading = equipped && player.IsReloading;
                c.tag.text = reloading ? "RELOADING" : equipped ? "EQUIPPED" : "";
                c.bg.color = c.hover && !equipped ? PanelHover : Panel;
                c.frame.color = equipped ? Color.white : (c.hover ? new Color(1f, 1f, 1f, 0.45f) : Line);
                float s = Mathf.Clamp01((Time.unscaledTime - c.sweepAt) / 0.35f);
                c.sweep.enabled = s < 1f;
                c.sweep.rectTransform.anchorMin = new Vector2(Mathf.Lerp(-0.3f, 1f, s), 0f);
                c.sweep.rectTransform.anchorMax = new Vector2(Mathf.Lerp(0f, 1.3f, s), 1f);
            }

            // status
            var health = player.GetComponent<JUHealth>();
            float hp = health != null ? health.Health : 0f, max = health != null ? Mathf.Max(1f, health.MaxHealth) : 100f;
            hpValue.text = Mathf.CeilToInt(hp) + "<size=55%><color=#8C8C8C>  /  " + Mathf.RoundToInt(max) + "</color></size>";
            hpFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(hp / max), 1f);
            hpFill.color = hp / max < 0.3f ? Low : Color.white;

            var medkit = player.GetComponentInChildren<PlayerMedkitSystem>(true);
            medkitValue.text = medkit != null ? medkit.CurrentMedkits + "<size=55%><color=#8C8C8C>  /  " + medkit.MaxMedkits + "</color></size>" : "--";
            medkitHint.text = medkit != null ? (medkit.CurrentMedkits > 0 ? "PRESS " + medkit.healKey.ToString().ToUpperInvariant() + " TO HEAL " + Mathf.RoundToInt(medkit.HealAmount) : "NONE CARRIED") : "";

            var sonar = player.GetComponentInChildren<PlayerSonarSkill>(true);
            sonarValue.text = sonar != null ? sonar.Charges + "<size=55%><color=#8C8C8C>  /  " + sonar.MaxCharges + "</color></size>" : "--";
            sonarHint.text = sonar == null ? "" : sonar.Charges <= 0 ? "NO CHARGES LEFT" : sonar.IsReady ? "READY   |   PRESS " + sonar.triggerKey.ToString().ToUpperInvariant() : "RECHARGING   " + Mathf.CeilToInt(sonar.CooldownRemaining) + " S";

            int throwables = 0;
            if (inventory.HoldableItensRightHand != null)
                foreach (var it in inventory.HoldableItensRightHand) if (it is ThrowableItem th && th.Unlocked) throwables += Mathf.Max(0, th.ItemQuantity);
            if (inventory.HoldableItensLeftHand != null)
                foreach (var it in inventory.HoldableItensLeftHand) if (it is ThrowableItem th && th.Unlocked) throwables += Mathf.Max(0, th.ItemQuantity);
            throwValue.text = throwables.ToString();

            // ammunition carried, per weapon
            var sb = new System.Text.StringBuilder();
            foreach (var it in owned)
            {
                if (!(it is Weapon w)) continue;
                var entry = CatalogEntry(it);
                string n = (entry != null ? entry.displayName : it.ItemName).ToUpperInvariant();
                int total = w.BulletsAmounts + w.TotalBullets;
                string col = total <= w.BulletsPerMagazine ? "#FF5C4F" : "#FFFFFF";
                sb.Append("<color=#9A9A9A>").Append(n).Append("</color><pos=72%><color=").Append(col).Append('>').Append(total).Append("</color>\n");
            }
            ammoList.text = sb.Length > 0 ? sb.ToString() : "<color=#5A5A5A>NO WEAPONS YET</color>";

            if (Time.unscaledTime >= nextNearby) { nextNearby = Time.unscaledTime + 0.5f; RefreshNearby(); }
        }

        private void RefreshNearby()
        {
            if (player == null) return;
            Vector3 p = player.transform.position;
            var found = new List<(string name, float d)>();
            foreach (var w in FindObjectsByType<Weapon>(FindObjectsSortMode.None))
            {
                if (w == null || !w.gameObject.activeInHierarchy || w.GetComponentInParent<JUInventory>() != null) continue;
                float d = Vector3.Distance(p, w.transform.position);
                if (d > 25f) continue;
                var entry = CatalogEntry(w);
                found.Add(((entry != null ? entry.displayName : w.ItemName).ToUpperInvariant(), d));
            }
            foreach (var a in FindObjectsByType<AmmoBoxPickup>(FindObjectsSortMode.None))
            {
                if (a == null || !a.gameObject.activeInHierarchy) continue;
                float d = Vector3.Distance(p, a.transform.position);
                if (d <= 25f) found.Add(("AMMO BOX", d));
            }
            foreach (var m in FindObjectsByType<MedkitPickup>(FindObjectsSortMode.None))
            {
                if (m == null || !m.gameObject.activeInHierarchy) continue;
                float d = Vector3.Distance(p, m.transform.position);
                if (d <= 25f) found.Add(("MEDKIT", d));
            }
            found.Sort((a, b) => a.d.CompareTo(b.d));
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < found.Count && i < 7; i++)
                sb.Append(found[i].name).Append("<pos=72%><color=#9A9A9A>").Append(Mathf.RoundToInt(found[i].d)).Append(" M</color>\n");
            nearbyList.text = sb.ToString();
            nearbyEmpty.gameObject.SetActive(found.Count == 0);
        }

        private WeaponCatalogAsset.Entry CatalogEntry(JUHoldableItem it)
        {
            if (catalog == null || it == null) return null;
            string id = WeaponIds.FromItemName(it.ItemName);
            foreach (var e in catalog.weapons) if (e != null && e.id == id) return e;
            return null;
        }

        private static string FireModeName(Weapon.WeaponFireMode m)
        {
            switch (m)
            {
                case Weapon.WeaponFireMode.Auto: return "AUTOMATIC";
                case Weapon.WeaponFireMode.SemiAuto: return "SEMI-AUTO";
                case Weapon.WeaponFireMode.BoltAction: return "BOLT ACTION";
                default: return "PUMP ACTION";
            }
        }

        // ---------------- build ----------------
        private void Build()
        {
            var cgo = new GameObject("Inventory Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            cgo.transform.SetParent(transform, false);
            canvas = cgo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 95;   // above the match HUD (minimap, zone panel, counters)
            var sc = cgo.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight = 0.5f;
            group = cgo.GetComponent<CanvasGroup>();

            // the game stays visible: a dark wash, darker at the edges
            var wash = NewImage("Wash", cgo.transform, null, new Color(0f, 0f, 0f, 0.74f));
            Stretch(wash.rectTransform);
            var edge = NewImage("Edge", cgo.transform, gradient, new Color(0f, 0f, 0f, 0.6f));
            Stretch(edge.rectTransform);

            content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(cgo.transform, false);
            Stretch(content);

            // header
            title = NewText("Title", content, "INVENTORY", 54, fontBold, Color.white, 14f, TextAlignmentOptions.Left);
            Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(96f, -64f), new Vector2(700f, 64f));
            subtitle = NewText("Subtitle", content, "WHAT YOU CARRY RIGHT NOW", 18, fontSemi, Dim, 6f, TextAlignmentOptions.Left);
            Place(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(100f, -122f), new Vector2(700f, 28f));
            var rule = NewImage("Rule", content, null, Line);
            Place(rule.rectTransform, new Vector2(0f, 1f), new Vector2(96f, -158f), new Vector2(1728f, 1f), new Vector2(0f, 0.5f));

            // ---------- weapons
            Header(content, "WEAPONS", new Vector2(96f, -186f));
            for (int i = 0; i < Slots; i++) cards.Add(MakeCard(content, i));

            // ---------- status
            float sx = 96f + 2f * (420f + 16f) + 40f;   // after two columns of cards
            Header(content, "STATUS", new Vector2(sx, -186f));
            var hpBox = Box(content, new Vector2(sx, -222f), new Vector2(420f, 150f));
            Label(hpBox, "HEALTH", new Vector2(24f, -20f));
            hpValue = NewText("HP", hpBox, "100", 56, fontBold, Color.white, 2f, TextAlignmentOptions.Left);
            Place(hpValue.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -46f), new Vector2(372f, 60f));
            var track = NewImage("Track", hpBox, null, new Color(1f, 1f, 1f, 0.12f));
            Place(track.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -120f), new Vector2(372f, 6f));
            hpFill = NewImage("Fill", track.transform, null, Color.white);
            hpFill.rectTransform.anchorMin = Vector2.zero; hpFill.rectTransform.anchorMax = Vector2.one; hpFill.rectTransform.offsetMin = hpFill.rectTransform.offsetMax = Vector2.zero;

            var medBox = Box(content, new Vector2(sx, -388f), new Vector2(420f, 112f));
            Label(medBox, "MEDKITS", new Vector2(24f, -20f));
            medkitValue = NewText("Medkits", medBox, "0", 42, fontBold, Color.white, 2f, TextAlignmentOptions.Left);
            Place(medkitValue.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -44f), new Vector2(200f, 46f));
            medkitHint = NewText("Hint", medBox, "", 15, fontSemi, Dim, 3f, TextAlignmentOptions.Right);
            Place(medkitHint.rectTransform, new Vector2(0f, 1f), new Vector2(180f, -58f), new Vector2(216f, 24f));

            var sonBox = Box(content, new Vector2(sx, -516f), new Vector2(420f, 112f));
            Label(sonBox, "SONAR", new Vector2(24f, -20f));
            sonarValue = NewText("Sonar", sonBox, "0", 42, fontBold, Color.white, 2f, TextAlignmentOptions.Left);
            Place(sonarValue.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -44f), new Vector2(200f, 46f));
            sonarHint = NewText("Hint", sonBox, "", 15, fontSemi, Dim, 3f, TextAlignmentOptions.Right);
            Place(sonarHint.rectTransform, new Vector2(0f, 1f), new Vector2(150f, -58f), new Vector2(246f, 24f));

            var thrBox = Box(content, new Vector2(sx, -644f), new Vector2(420f, 112f));
            Label(thrBox, "THROWABLES", new Vector2(24f, -20f));
            throwValue = NewText("Throwables", thrBox, "0", 42, fontBold, Color.white, 2f, TextAlignmentOptions.Left);
            Place(throwValue.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -44f), new Vector2(200f, 46f));

            // ---------- ammunition and nearby loot
            float rx = sx + 420f + 40f;
            float rw = 1920f - 96f - rx;
            Header(content, "AMMUNITION", new Vector2(rx, -186f));
            var ammoBox = Box(content, new Vector2(rx, -222f), new Vector2(rw, 278f));
            ammoList = NewText("Ammo", ammoBox, "", 21, fontSemi, Color.white, 3f, TextAlignmentOptions.TopLeft);
            ammoList.lineSpacing = 18f;
            Place(ammoList.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -22f), new Vector2(rw - 48f, 240f));

            Header(content, "NEARBY", new Vector2(rx, -528f));
            var nearBox = Box(content, new Vector2(rx, -564f), new Vector2(rw, 192f));
            nearbyList = NewText("Nearby", nearBox, "", 19, fontSemi, Color.white, 3f, TextAlignmentOptions.TopLeft);
            nearbyList.lineSpacing = 6f;
            Place(nearbyList.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -18f), new Vector2(rw - 48f, 170f));
            nearbyEmpty = NewText("Nothing", nearBox, "NOTHING WITHIN 25 M", 18, fontSemi, new Color(1f, 1f, 1f, 0.25f), 4f, TextAlignmentOptions.Center);
            Place(nearbyEmpty.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -80f), new Vector2(rw, 30f));
            var pickHint = NewText("Pick", content, "WALK OVER LOOT, OR PRESS F, TO PICK IT UP", 14, fontSemi, Dim, 4f, TextAlignmentOptions.Left);
            Place(pickHint.rectTransform, new Vector2(0f, 1f), new Vector2(rx, -766f), new Vector2(rw, 22f));

            // ---------- hints
            var bar = NewImage("Hint bar", content, null, new Color(0f, 0f, 0f, 0.6f));
            Place(bar.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(1920f, 56f), new Vector2(0f, 0f));
            bar.rectTransform.anchorMin = new Vector2(0f, 0f); bar.rectTransform.anchorMax = new Vector2(1f, 0f);
            bar.rectTransform.offsetMin = new Vector2(0f, 0f); bar.rectTransform.offsetMax = new Vector2(0f, 56f);
            var hints = NewText("Hints", bar.transform, "<color=#FFFFFF>TAB</color>  CLOSE        <color=#FFFFFF>1 - 6</color>  EQUIP        <color=#FFFFFF>CLICK</color>  EQUIP        <color=#FFFFFF>WASD</color>  KEEP MOVING", 17, fontSemi, Dim, 5f, TextAlignmentOptions.Center);
            Stretch(hints.rectTransform);
        }

        private Card MakeCard(Transform parent, int i)
        {
            const float W = 420f, H = 168f;
            int col = i % 2, row = i / 2;
            var c = new Card();
            var go = new GameObject("Card " + (i + 1), typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            c.root = go.GetComponent<RectTransform>();
            Place(c.root, new Vector2(0f, 1f), new Vector2(96f + col * (W + 16f), -222f - row * (H + 16f)), new Vector2(W, H));
            c.bg = go.GetComponent<Image>(); c.bg.sprite = px; c.bg.color = Panel;
            var button = go.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => Equip(c.item));
            var trigger = go.AddComponent<EventTrigger>();
            AddTrigger(trigger, EventTriggerType.PointerEnter, () => c.hover = true);
            AddTrigger(trigger, EventTriggerType.PointerExit, () => c.hover = false);

            // a frame made of four thin lines (the centre stays clear)
            c.frame = FrameLines(c.root);

            c.sweep = NewImage("Sweep", c.root, null, new Color(1f, 1f, 1f, 0.12f));
            c.sweep.rectTransform.offsetMin = c.sweep.rectTransform.offsetMax = Vector2.zero;
            c.sweep.enabled = false;

            var keyBg = NewImage("Key", c.root, null, new Color(1f, 1f, 1f, 0.1f));
            Place(keyBg.rectTransform, new Vector2(0f, 1f), new Vector2(14f, -14f), new Vector2(28f, 28f));
            c.key = NewText("Key", keyBg.transform, (i + 1).ToString(), 18, fontBold, Color.white, 0f, TextAlignmentOptions.Center);
            Stretch(c.key.rectTransform);

            c.icon = NewImage("Icon", c.root, null, Color.white);
            c.icon.preserveAspect = true;
            if (silhouette != null) c.icon.material = silhouette;
            Place(c.icon.rectTransform, new Vector2(1f, 0.5f), new Vector2(-20f, 6f), new Vector2(190f, 110f), new Vector2(1f, 0.5f));

            c.name = NewText("Name", c.root, "", 28, fontBold, Color.white, 3f, TextAlignmentOptions.Left);
            Place(c.name.rectTransform, new Vector2(0f, 1f), new Vector2(54f, -12f), new Vector2(330f, 34f));
            c.cls = NewText("Class", c.root, "", 15, fontSemi, Dim, 5f, TextAlignmentOptions.Left);
            Place(c.cls.rectTransform, new Vector2(0f, 1f), new Vector2(56f, -44f), new Vector2(330f, 22f));
            c.ammo = NewText("Ammo", c.root, "", 40, fontBold, Color.white, 1f, TextAlignmentOptions.Left);
            Place(c.ammo.rectTransform, new Vector2(0f, 0f), new Vector2(18f, 40f), new Vector2(220f, 46f), new Vector2(0f, 0f));
            c.mode = NewText("Mode", c.root, "", 14, fontSemi, Dim, 4f, TextAlignmentOptions.Left);
            Place(c.mode.rectTransform, new Vector2(0f, 0f), new Vector2(20f, 14f), new Vector2(300f, 22f), new Vector2(0f, 0f));
            c.tag = NewText("Tag", c.root, "", 14, fontBold, Color.white, 5f, TextAlignmentOptions.Right);
            Place(c.tag.rectTransform, new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(160f, 20f), new Vector2(1f, 1f));
            return c;
        }

        private Image FrameLines(RectTransform parent)
        {
            // one Image drives the colour; the other three follow it in Refresh through the parent's children
            var top = NewImage("Frame", parent, null, Line);
            top.rectTransform.anchorMin = new Vector2(0f, 1f); top.rectTransform.anchorMax = new Vector2(1f, 1f);
            top.rectTransform.offsetMin = new Vector2(0f, -1.5f); top.rectTransform.offsetMax = Vector2.zero;
            var follow = top.gameObject.AddComponent<FrameFollower>();
            follow.Build(this, parent);
            return top;
        }

        /// <summary>Keeps the other three edges of a card's frame the same colour as its top edge.</summary>
        private class FrameFollower : MonoBehaviour
        {
            private Image src;
            private Image[] edges;
            public void Build(InventoryScreen s, RectTransform parent)
            {
                src = GetComponent<Image>();
                edges = new Image[3];
                for (int e = 0; e < 3; e++)
                {
                    var img = s.NewImage("Edge", parent, null, src.color);
                    var r = img.rectTransform;
                    if (e == 0) { r.anchorMin = new Vector2(0f, 0f); r.anchorMax = new Vector2(1f, 0f); r.offsetMin = Vector2.zero; r.offsetMax = new Vector2(0f, 1.5f); }
                    else if (e == 1) { r.anchorMin = new Vector2(0f, 0f); r.anchorMax = new Vector2(0f, 1f); r.offsetMin = Vector2.zero; r.offsetMax = new Vector2(1.5f, 0f); }
                    else { r.anchorMin = new Vector2(1f, 0f); r.anchorMax = new Vector2(1f, 1f); r.offsetMin = new Vector2(-1.5f, 0f); r.offsetMax = Vector2.zero; }
                    edges[e] = img;
                }
            }
            private void LateUpdate() { if (src != null) foreach (var e in edges) if (e != null) e.color = src.color; }
        }

        private void Header(Transform parent, string text, Vector2 pos)
        {
            var t = NewText("Header " + text, parent, text, 17, fontBold, Dim, 8f, TextAlignmentOptions.Left);
            Place(t.rectTransform, new Vector2(0f, 1f), pos, new Vector2(420f, 24f));
        }

        private RectTransform Box(Transform parent, Vector2 pos, Vector2 size)
        {
            var img = NewImage("Box", parent, null, Panel);
            Place(img.rectTransform, new Vector2(0f, 1f), pos, size);
            var top = NewImage("Accent", img.transform, null, new Color(1f, 1f, 1f, 0.5f));
            top.rectTransform.anchorMin = new Vector2(0f, 1f); top.rectTransform.anchorMax = new Vector2(0f, 1f); top.rectTransform.pivot = new Vector2(0f, 1f);
            top.rectTransform.anchoredPosition = Vector2.zero; top.rectTransform.sizeDelta = new Vector2(36f, 2f);
            return img.rectTransform;
        }

        private void Label(Transform parent, string text, Vector2 pos)
        {
            var t = NewText("Label", parent, text, 15, fontSemi, Dim, 6f, TextAlignmentOptions.Left);
            Place(t.rectTransform, new Vector2(0f, 1f), pos, new Vector2(300f, 22f));
        }

        // ---------------- small UI helpers ----------------
        private static void AddTrigger(EventTrigger t, EventTriggerType type, System.Action a)
        {
            var e = new EventTrigger.Entry { eventID = type };
            e.callback.AddListener(_ => a());
            t.triggers.Add(e);
        }

        private static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }

        private static void Place(RectTransform r, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = pivot ?? new Vector2(0f, 1f);
            r.anchoredPosition = pos;
            r.sizeDelta = size;
        }

        internal Image NewImage(string name, Transform parent, Sprite s, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = s != null ? s : px;
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        private TextMeshProUGUI NewText(string name, Transform parent, string text, float size, TMP_FontAsset font, Color c, float spacing, TextAlignmentOptions al)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.font = font; t.color = c; t.characterSpacing = spacing; t.alignment = al;
            // no vertical truncation: a line taller than its box would vanish entirely
            t.raycastTarget = false; t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Overflow; t.richText = true;
            return t;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            new GameObject("[HUD] EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        }

        private static TMP_FontAsset FindFont(string namePart)
        {
            foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                if (f != null && f.name.Contains(namePart)) return f;
            return null;
        }

        private static Sprite MakePixel()
        {
            var t = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "InvPixel" };
            var c = new Color32[16]; for (int i = 0; i < 16; i++) c[i] = new Color32(255, 255, 255, 255);
            t.SetPixels32(c); t.Apply();
            return Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
        }

        // darker towards the edges of the screen
        private static Sprite MakeGradient()
        {
            const int W = 128, H = 72;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "InvEdge" };
            var c = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = x / (W - 1f) * 2f - 1f, v = y / (H - 1f) * 2f - 1f;
                    float d = Mathf.Clamp01(Mathf.Sqrt(u * u * 0.7f + v * v) - 0.35f);
                    c[y * W + x] = new Color32(255, 255, 255, (byte)(d * 255f));
                }
            t.SetPixels32(c); t.Apply();
            return Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}

namespace Railbound.UI
{
    /// <summary>
    /// Test aid, only with <c>-invtest DIR</c> on a client: once the match is live it gives the player a few weapons (test only), opens the
    /// inventory, saves a screenshot, equips another weapon through the screen, checks the hand changed, saves a second screenshot and closes it.
    /// </summary>
    public class InventoryTestAid : MonoBehaviour
    {
        private string dir;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string[] a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
            {
                if (a[i] != "-invtest" || FindFirstObjectByType<InventoryTestAid>() != null) continue;
                var go = new GameObject("[Test] Inventory");
                DontDestroyOnLoad(go);
                go.AddComponent<InventoryTestAid>().dir = a[i + 1];
                return;
            }
        }

        private System.Collections.IEnumerator Start()
        {
            System.IO.Directory.CreateDirectory(dir);
            float waited = 0f;
            while (waited < 240f && (Railbound.BR.BRMatchCoordinator.Instance == null || !Railbound.BR.BRMatchCoordinator.Instance.IsMatchLive || Railbound.Gameplay.LocalPlayer.Controller == null || InventoryScreen.Instance == null))
            { waited += 0.5f; yield return new WaitForSecondsRealtime(0.5f); }
            yield return new WaitForSecondsRealtime(6f);
            var pc = Railbound.Gameplay.LocalPlayer.Controller;
            var inv = pc != null ? pc.GetComponent<JUTPS.InventorySystem.JUInventory>() : null;
            if (inv == null) { Debug.LogError("[InvTest] no inventory"); yield break; }
            int given = 0;
            foreach (var it in inv.HoldableItensRightHand)
                if (it is JUTPS.WeaponSystem.Weapon w && !w.Unlocked && given < 3) { w.Unlocked = true; if (w.ItemQuantity <= 0) w.ItemQuantity = 1; given++; }
            Debug.Log("[InvTest] gave " + given + " weapons; holding " + inv.CurrentRightHandItemID);

            var screen = InventoryScreen.Instance;
            screen.Open();
            yield return new WaitForSecondsRealtime(1.2f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "inventory_open.png"));
            yield return new WaitForSecondsRealtime(0.6f);

            // equip a different weapon through the screen
            int before = inv.CurrentRightHandItemID;
            JUTPS.ItemSystem.JUHoldableItem other = null;
            foreach (var it in inv.HoldableItensRightHand) if (it is JUTPS.WeaponSystem.Weapon w2 && w2.Unlocked && w2.ItemSwitchID != before) { other = w2; break; }
            var equip = typeof(InventoryScreen).GetMethod("Equip", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (other != null && equip != null) equip.Invoke(screen, new object[] { other });
            yield return new WaitForSecondsRealtime(1.5f);
            Debug.Log($"[InvTest] equip {(other != null ? other.ItemName : "none")}: hand {before} -> {inv.CurrentRightHandItemID} (wanted {(other != null ? other.ItemSwitchID : -1)}), open={screen.IsOpen}, cursor={Cursor.lockState}");
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "inventory_equipped.png"));
            yield return new WaitForSecondsRealtime(0.8f);
            screen.Close();
            yield return new WaitForSecondsRealtime(0.6f);
            Debug.Log($"[InvTest] closed: open={screen.IsOpen}, input restored={pc.UseDefaultControllerInput}, cursor={Cursor.lockState}");
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "inventory_closed.png"));
            yield return new WaitForSecondsRealtime(0.6f);
            Debug.Log("[InvTest] DONE");
        }
    }
}
