using System.Collections.Generic;
using JUTPS;
using JUTPS.InventorySystem;
using JUTPS.ItemSystem;
using JUTPS.WeaponSystem;
using Railbound.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// In-game loadout HUD (bottom-right): ability tiles (medkit, sonar, throwables) on the left, the weapon card with the
    /// weapon silhouette and a large magazine / reserve counter on the right, and the other weapons as small slots underneath.
    /// Built entirely at runtime and created automatically in every scene that has a player; monochrome to match the game UI.
    /// </summary>
    public class LoadoutHud : MonoBehaviour
    {
        // ---------------- bootstrap ----------------
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Spawn();
        }

        private static void OnSceneLoaded(Scene s, LoadSceneMode m) { Spawn(); }

        private static void Spawn()
        {
            if (FindObjectOfType<LoadoutHud>() != null) return;
            new GameObject("[HUD] Loadout").AddComponent<LoadoutHud>();
        }

        // ---------------- palette ----------------
        private static readonly Color Ink = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color Line = new Color(1f, 1f, 1f, 0.20f);
        private static readonly Color Dim = new Color(1f, 1f, 1f, 0.45f);
        private static readonly Color Low = new Color(1f, 0.36f, 0.31f, 1f);

        // ---------------- state ----------------
        private Canvas canvas;
        private CanvasGroup rootGroup;
        private Sprite px;
        private TMP_FontAsset fontBold, fontSemi;

        // weapon card
        private CanvasGroup cardGroup;
        private Image weaponIcon;
        private TextMeshProUGUI ammoMag, weaponName, fireMode, stateText;
        private RectTransform ammoRoot;
        private CanvasGroup ammoGroup;
        private int lastMag = -1;
        private float punchStart = -10f;

        // weapon slots
        private readonly List<SlotUI> slots = new List<SlotUI>();
        private RectTransform slotRow;

        // ability tiles
        private readonly List<Tile> tiles = new List<Tile>();
        private Tile medkitTile, sonarTile, throwTile;

        private JUCharacterController player;
        private PlayerSonarSkill sonar;
        private PlayerMedkitSystem medkit;
        private float nextScan;
        private Sprite medkitSprite, sonarSprite, grenadeSprite;
        private Material silhouette;

        private class Frame
        {
            public Image[] strips = new Image[4];
            public void Set(Color c) { foreach (var s in strips) if (s != null) s.color = c; }
        }
        private class SlotUI { public Image icon; public TextMeshProUGUI key; public JUHoldableItem item; public GameObject root; }
        private class Tile
        {
            public GameObject root; public Image icon, cooldown; public Frame border; public GameObject keyCap; public TextMeshProUGUI count, caption, key;
            public CanvasGroup group;
        }

        // ---------------- lifecycle ----------------
        private void Awake()
        {
            px = MakePixel();
            fontBold = FindFont("Rajdhani-Bold") ?? TMP_Settings.defaultFontAsset;
            fontSemi = FindFont("Rajdhani-SemiBold") ?? fontBold;
            sonarSprite = Resources.Load<Sprite>("HUD/Sonar_Device");
            medkitSprite = Resources.Load<Sprite>("HUD/Hud_Icon_Medkit");
            grenadeSprite = Resources.Load<Sprite>("HUD/Hud_Icon_Grenade");
            var sh = Shader.Find("Railbound/UI/Silhouette");
            if (sh != null) silhouette = new Material(sh);
            Build();
        }

        private void Update()
        {
            if (player == null && Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + 0.5f;
                player = JUGameManager.PlayerController;
                if (player == null)
                {
                    var go = GameObject.FindGameObjectWithTag("Player");
                    if (go != null) player = go.GetComponent<JUCharacterController>();
                }
                if (player != null)
                {
                    sonar = player.GetComponentInChildren<PlayerSonarSkill>(true);
                    medkit = player.GetComponentInChildren<PlayerMedkitSystem>(true);
                    HideLegacyWidgets();
                }
            }

            bool show = player != null && player.isActiveAndEnabled;
            float target = show && Time.timeScale > 0.01f && !DeathScreenUI.IsShowing ? 1f : 0f;
            rootGroup.alpha = Mathf.MoveTowards(rootGroup.alpha, target, Time.unscaledDeltaTime * 5f);
            if (!show) return;

            RefreshWeapon();
            RefreshSlots();
            RefreshTiles();
        }

        // the Rajdhani HUD fonts live outside Resources; the pause menu has them loaded in every gameplay scene
        private static TMP_FontAsset FindFont(string namePart)
        {
            foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                if (f != null && f.name.Contains(namePart)) return f;
            return null;
        }

        private void HideLegacyWidgets()
        {
            // the old bottom-left sonar widget is replaced by the ability tiles
            var ui = GameObject.Find("User Interface/UI");
            if (ui == null) return;
            var w = ui.transform.Find("SonarChargeWidget"); if (w != null) w.gameObject.SetActive(false);
            var l = ui.transform.Find("SonarSkillWidget"); if (l != null) l.gameObject.SetActive(false);
            var item = ui.transform.Find("Current Item Information"); if (item != null) item.gameObject.SetActive(false);   // replaced by the weapon card
        }

        // ---------------- refresh ----------------
        private void RefreshWeapon()
        {
            var held = player.HoldableItemInUseRightHand;
            var weapon = held as Weapon;
            var thrown = held as ThrowableItem;
            bool shown = weapon != null || thrown != null || held is MeleeWeapon;
            cardGroup.alpha = Mathf.MoveTowards(cardGroup.alpha, shown ? 1f : 0f, Time.unscaledDeltaTime * 6f);
            ammoGroup.alpha = Mathf.MoveTowards(ammoGroup.alpha, (weapon != null || thrown != null) ? 1f : 0f, Time.unscaledDeltaTime * 6f);
            if (!shown) { lastMag = -1; return; }

            weaponIcon.sprite = held.ItemIcon;
            weaponIcon.enabled = held.ItemIcon != null;
            weaponName.text = (held.ItemName ?? "").ToUpperInvariant();
            if (weapon == null)
            {
                lastMag = -1;
                if (thrown != null)
                {
                    int q = Mathf.Max(0, thrown.ItemQuantity);
                    fireMode.text = "THROWABLE";
                    ammoMag.text = q.ToString("00") + "<size=44%><color=#" + ColorUtility.ToHtmlStringRGBA(Dim) + "> LEFT</color></size>";
                    ammoMag.color = q > 0 ? Color.white : Low;
                    stateText.text = q > 0 ? "" : "EMPTY";
                    stateText.color = Low;
                    ammoRoot.localScale = Vector3.one;
                }
                else { fireMode.text = "MELEE"; stateText.text = ""; }
                return;
            }
            fireMode.text = FireModeLabel(weapon.FireMode);

            int mag = Mathf.Max(0, weapon.BulletsAmounts);
            int reserve = Mathf.Max(0, weapon.TotalBullets);
            if (mag != lastMag) { if (lastMag >= 0 && mag != lastMag) punchStart = Time.unscaledTime; lastMag = mag; }

            bool inf = weapon.InfiniteAmmo;
            bool lowMag = weapon.BulletsPerMagazine > 0 && mag <= Mathf.CeilToInt(weapon.BulletsPerMagazine * 0.25f);
            bool empty = mag == 0;
            Color c = lowMag ? Low : Color.white;
            if (lowMag && empty) c.a = 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f));

            string padded = mag.ToString("000");
            int lead = 0; while (lead < 2 && padded[lead] == '0') lead++;
            string dimHex = ColorUtility.ToHtmlStringRGBA(new Color(c.r, c.g, c.b, c.a * 0.35f));
            string reserveHex = ColorUtility.ToHtmlStringRGBA((!inf && reserve == 0) ? Low : Dim);
            ammoMag.text = (lead > 0 ? "<color=#" + dimHex + ">" + padded.Substring(0, lead) + "</color>" + padded.Substring(lead) : padded)
                + "<size=44%><color=#" + reserveHex + ">" + (inf ? "/∞" : "/" + reserve) + "</color></size>";
            ammoMag.color = c;

            if (!inf && empty && reserve == 0) stateText.text = "NO AMMO";
            else if (empty) stateText.text = "RELOAD";
            else stateText.text = "";
            stateText.color = new Color(Low.r, Low.g, Low.b, 0.6f + 0.4f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f)));

            float k = Time.unscaledTime - punchStart;
            ammoRoot.localScale = Vector3.one * (k < 0.15f ? Mathf.Lerp(1.08f, 1f, k / 0.15f) : 1f);
        }

        private static string FireModeLabel(Weapon.WeaponFireMode m)
        {
            switch (m)
            {
                case Weapon.WeaponFireMode.Auto: return "AUTO";
                case Weapon.WeaponFireMode.SemiAuto: return "SEMI";
                case Weapon.WeaponFireMode.BoltAction: return "BOLT";
                case Weapon.WeaponFireMode.Shotgun: return "SHOTGUN";
                default: return "";
            }
        }

        private void RefreshSlots()
        {
            var inv = player.Inventory; if (inv == null) return;
            var items = inv.HoldableItensRightHand; if (items == null) return;
            var held = player.HoldableItemInUseRightHand;

            int n = 0;
            for (int i = 0; i < items.Length; i++)
            {
                var it = items[i];
                if (it == null || !it.Unlocked || !(it is Weapon || it is MeleeWeapon)) continue;
                if (n >= slots.Count) slots.Add(NewSlot());
                var s = slots[n]; s.item = it; s.root.SetActive(true);
                s.icon.sprite = it.ItemIcon; s.icon.enabled = it.ItemIcon != null;
                s.icon.color = it == held ? Color.white : Dim;
                s.key.text = (n + 1).ToString();
                n++;
            }
            for (int i = n; i < slots.Count; i++) slots[i].root.SetActive(false);
        }

        private void RefreshTiles()
        {
            // medkit
            if (medkitTile != null)
            {
                bool has = medkit != null;
                medkitTile.root.SetActive(has);
                if (has)
                {
                    int c = medkit.CurrentMedkits;
                    medkitTile.count.text = c.ToString();
                    medkitTile.icon.color = c > 0 ? Color.white : Dim;
                    medkitTile.key.text = KeyLabel(medkit.healKey);
                    medkitTile.cooldown.fillAmount = 0f;
                    medkitTile.caption.text = c > 0 ? "READY" : "EMPTY";
                    medkitTile.caption.color = c > 0 ? Dim : Low;
                    medkitTile.border.Set(c > 0 ? new Color(1, 1, 1, 0.5f) : Line);
                }
            }
            // sonar
            if (sonarTile != null)
            {
                bool has = sonar != null;
                sonarTile.root.SetActive(has);
                if (has)
                {
                    float cd = sonar.CooldownRemaining; int ch = sonar.Charges;
                    sonarTile.count.text = ch.ToString();
                    bool ready = cd <= 0.01f && ch > 0;
                    sonarTile.icon.color = ready ? Color.white : Dim;
                    sonarTile.key.text = KeyLabel(sonar.triggerKey);
                    sonarTile.cooldown.fillAmount = sonar.cooldown > 0f ? Mathf.Clamp01(cd / sonar.cooldown) : 0f;
                    sonarTile.caption.text = ch <= 0 ? "EMPTY" : cd > 0.01f ? Mathf.CeilToInt(cd) + "s" : "READY";
                    sonarTile.caption.color = ch <= 0 ? Low : Dim;
                    sonarTile.border.Set(ready ? new Color(1, 1, 1, 0.5f) : Line);
                }
            }
            ShowKeyCaps(medkitTile); ShowKeyCaps(sonarTile); ShowKeyCaps(throwTile);
            // throwables (grenades) come from the inventory when the player has any
            int throwCount = 0; Sprite throwIcon = null; bool throwFound = false;
            var inv = player.Inventory;
            if (inv != null && inv.HoldableItensRightHand != null)
                foreach (var it in inv.HoldableItensRightHand)
                {
                    var t = it as ThrowableItem; if (t == null) continue;
                    throwCount += t.ItemQuantity; if (throwIcon == null) throwIcon = t.ItemIcon;
                }
            throwFound = throwCount > 0;
            if (throwTile != null)
            {
                throwTile.root.SetActive(throwFound);
                if (throwFound)
                {
                    throwTile.count.text = throwCount.ToString();
                    throwTile.icon.sprite = throwIcon != null ? throwIcon : grenadeSprite;
                    throwTile.icon.color = throwCount > 0 ? Color.white : Dim;
                    throwTile.caption.text = throwCount > 0 ? "" : "EMPTY";
                    throwTile.caption.color = Low;
                    throwTile.cooldown.fillAmount = 0f;
                    throwTile.border.Set(throwCount > 0 ? new Color(1, 1, 1, 0.5f) : Line);
                }
            }
        }

        private static void ShowKeyCaps(Tile t) { if (t.keyCap != null) t.keyCap.SetActive(!string.IsNullOrEmpty(t.key.text)); }

        private static string KeyLabel(KeyCode k)
        {
            string s = k.ToString();
            if (s.StartsWith("Alpha")) s = s.Substring(5);
            return s.Length > 3 ? s.Substring(0, 3).ToUpperInvariant() : s.ToUpperInvariant();
        }

        // ---------------- build ----------------
        private void Build()
        {
            var cgo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            cgo.transform.SetParent(transform, false);
            canvas = cgo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 4;
            var sc = cgo.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
            rootGroup = cgo.GetComponent<CanvasGroup>(); rootGroup.alpha = 0f; rootGroup.interactable = false; rootGroup.blocksRaycasts = false;
            var root = (RectTransform)cgo.transform;

            // ---- weapon card (bottom-right) ----
            var BR = new Vector2(1, 0);
            var card = NewRect("Weapon Card", root, BR, BR, BR, new Vector2(-56, 76), new Vector2(360, 128));
            cardGroup = card.gameObject.AddComponent<CanvasGroup>(); cardGroup.alpha = 0f;
            Panel(card, Ink, Line);
            weaponIcon = NewImage("Weapon Icon", card, null, Color.white); weaponIcon.preserveAspect = true;
            if (silhouette != null) weaponIcon.material = silhouette;
            // the item sprites carry a lot of empty padding, so draw them oversized and centred
            var wi = weaponIcon.rectTransform; wi.anchorMin = wi.anchorMax = new Vector2(0.5f, 0.5f); wi.pivot = new Vector2(0.5f, 0.5f); wi.anchoredPosition = new Vector2(0, 0); wi.sizeDelta = new Vector2(150, 150);
            weaponName = NewText("Weapon Name", card, "", 16, fontSemi, Dim, 4, TextAlignmentOptions.BottomLeft);
            var wn = weaponName.rectTransform; wn.anchorMin = new Vector2(0, 0); wn.anchorMax = new Vector2(0.6f, 0); wn.pivot = new Vector2(0, 0); wn.anchoredPosition = new Vector2(16, 10); wn.sizeDelta = new Vector2(0, 24);
            fireMode = NewText("Fire Mode", card, "", 14, fontSemi, Dim, 3, TextAlignmentOptions.TopRight);
            var fm = fireMode.rectTransform; fm.anchorMin = fm.anchorMax = new Vector2(1, 1); fm.pivot = new Vector2(1, 1); fm.anchoredPosition = new Vector2(-14, -10); fm.sizeDelta = new Vector2(160, 22);

            // ammo counter above the card, right-aligned
            ammoRoot = NewRect("Ammo", root, BR, BR, BR, new Vector2(-56, 214), new Vector2(360, 70));
            ammoRoot.pivot = new Vector2(1, 0);
            ammoGroup = ammoRoot.gameObject.AddComponent<CanvasGroup>(); ammoGroup.alpha = 0f;
            ammoMag = NewText("Magazine", ammoRoot, "000", 64, fontBold, Color.white, 1, TextAlignmentOptions.BottomRight);
            ammoMag.richText = true;
            var mr = ammoMag.rectTransform; mr.anchorMin = new Vector2(1, 0); mr.anchorMax = new Vector2(1, 0); mr.pivot = new Vector2(1, 0); mr.anchoredPosition = new Vector2(0, 0); mr.sizeDelta = new Vector2(320, 70);
            stateText = NewText("State", ammoRoot, "", 18, fontBold, Low, 6, TextAlignmentOptions.BottomLeft);
            var st = stateText.rectTransform; st.anchorMin = st.anchorMax = new Vector2(0, 0); st.pivot = new Vector2(0, 0); st.anchoredPosition = new Vector2(0, 14); st.sizeDelta = new Vector2(200, 26);

            // weapon slots under the card
            slotRow = NewRect("Weapon Slots", root, BR, BR, BR, new Vector2(-56, 30), new Vector2(360, 34));
            var hl = slotRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.childAlignment = TextAnchor.MiddleRight; hl.spacing = 16; hl.childControlWidth = false; hl.childControlHeight = false; hl.childForceExpandWidth = false; hl.childForceExpandHeight = false; hl.reverseArrangement = false;

            // ---- ability tiles to the left of the card ----
            var strip = NewRect("Abilities", root, BR, BR, BR, new Vector2(-56 - 360 - 22, 76), new Vector2(420, 128));
            var tl = strip.gameObject.AddComponent<HorizontalLayoutGroup>();
            tl.childAlignment = TextAnchor.LowerRight; tl.spacing = 10; tl.childControlWidth = false; tl.childControlHeight = false; tl.childForceExpandWidth = false; tl.childForceExpandHeight = false;
            medkitTile = NewTile(strip, "Medkit", medkitSprite);
            sonarTile = NewTile(strip, "Sonar", sonarSprite);
            throwTile = NewTile(strip, "Throwable", grenadeSprite);
            throwTile.root.SetActive(false);
            throwTile.keyCap.SetActive(false);
        }

        private Tile NewTile(Transform parent, string name, Sprite icon)
        {
            var t = new Tile();
            var rt = NewRect(name, parent, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0), Vector2.zero, new Vector2(86, 128));
            t.root = rt.gameObject; t.group = rt.gameObject.AddComponent<CanvasGroup>();
            var box = NewRect("Box", rt, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(86, 86));
            t.border = Panel(box, Ink, Line);
            t.icon = NewImage("Icon", box, icon, Color.white); t.icon.preserveAspect = true; if (silhouette != null) t.icon.material = silhouette; Stretch(t.icon.rectTransform, 16, 16, 16, 16);
            t.cooldown = NewImage("Cooldown", box, px, new Color(0, 0, 0, 0.62f)); Stretch(t.cooldown.rectTransform, 1, 1, 1, 1);
            t.cooldown.type = Image.Type.Filled; t.cooldown.fillMethod = Image.FillMethod.Vertical; t.cooldown.fillOrigin = 1; t.cooldown.fillAmount = 0f;
            t.count = NewText("Count", box, "0", 20, fontBold, Color.white, 0, TextAlignmentOptions.TopRight);
            var cr = t.count.rectTransform; cr.anchorMin = cr.anchorMax = new Vector2(1, 1); cr.pivot = new Vector2(1, 1); cr.anchoredPosition = new Vector2(-7, -4); cr.sizeDelta = new Vector2(40, 26);
            // key cap + caption under the tile
            var cap = NewRect("Key", rt, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 2), new Vector2(30, 22));
            Panel(cap, new Color(1, 1, 1, 0.10f), new Color(1, 1, 1, 0.30f)); t.keyCap = cap.gameObject;
            t.key = NewText("KeyText", cap, "", 14, fontBold, Dim, 0, TextAlignmentOptions.Center); Stretch(t.key.rectTransform, 0, 0, 0, 0);
            t.caption = NewText("Caption", rt, "", 15, fontSemi, Dim, 2, TextAlignmentOptions.Top);
            var cp = t.caption.rectTransform; cp.anchorMin = new Vector2(0, 0); cp.anchorMax = new Vector2(1, 0); cp.pivot = new Vector2(0.5f, 0); cp.anchoredPosition = new Vector2(0, 26); cp.sizeDelta = new Vector2(0, 18);
            tiles.Add(t);
            return t;
        }

        private SlotUI NewSlot()
        {
            var s = new SlotUI();
            var rt = NewRect("Slot", slotRow, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84, 30));
            s.root = rt.gameObject;
            s.icon = NewImage("Icon", rt, null, Dim); s.icon.preserveAspect = true; if (silhouette != null) s.icon.material = silhouette;
            var si = s.icon.rectTransform; si.anchorMin = si.anchorMax = new Vector2(0, 0.5f); si.pivot = new Vector2(0, 0.5f); si.anchoredPosition = new Vector2(0, 0); si.sizeDelta = new Vector2(58, 30);
            var cap = NewRect("Key", rt, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(22, 22));
            Panel(cap, new Color(1, 1, 1, 0.10f), new Color(1, 1, 1, 0.25f));
            s.key = NewText("KeyText", cap, "", 13, fontBold, Dim, 0, TextAlignmentOptions.Center); Stretch(s.key.rectTransform, 0, 0, 0, 0);
            return s;
        }

        // ---------------- tiny UI helpers ----------------
        private static Sprite MakePixel()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var cols = new Color32[16]; for (int i = 0; i < cols.Length; i++) cols[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(cols); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
        }

        private RectTransform NewRect(string name, Transform parent, Vector2 amin, Vector2 amax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform; r.anchorMin = amin; r.anchorMax = amax; r.pivot = pivot; r.anchoredPosition = pos; r.sizeDelta = size;
            return r;
        }

        private Image NewImage(string name, Transform parent, Sprite s, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var i = go.GetComponent<Image>(); i.sprite = s; i.color = c; i.raycastTarget = false;
            return i;
        }

        private Frame Panel(RectTransform rt, Color fill, Color border)
        {
            var i = rt.gameObject.AddComponent<Image>(); i.sprite = px; i.color = fill; i.raycastTarget = false;
            var f = new Frame();
            f.strips[0] = Strip(rt, "Top", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, 1));
            f.strips[1] = Strip(rt, "Bottom", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 1));
            f.strips[2] = Strip(rt, "Left", new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(1, 0));
            f.strips[3] = Strip(rt, "Right", new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(1, 0));
            f.Set(border);
            return f;
        }

        private Image Strip(RectTransform parent, string name, Vector2 amin, Vector2 amax, Vector2 pivot, Vector2 size)
        {
            var im = NewImage(name, parent, px, Color.white);
            var r = im.rectTransform; r.anchorMin = amin; r.anchorMax = amax; r.pivot = pivot; r.anchoredPosition = Vector2.zero; r.sizeDelta = size;
            return im;
        }

        private TextMeshProUGUI NewText(string name, Transform parent, string text, float size, TMP_FontAsset font, Color c, float spacing, TextAlignmentOptions al)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>(); t.font = font; t.text = text; t.fontSize = size; t.color = c; t.characterSpacing = spacing; t.alignment = al;
            t.raycastTarget = false; t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        private static void Stretch(RectTransform r, float l, float b, float rr, float t)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(l, b); r.offsetMax = new Vector2(-rr, -t);
        }
    }
}
