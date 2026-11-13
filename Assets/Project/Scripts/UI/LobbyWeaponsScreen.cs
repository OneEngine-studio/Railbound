using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// The WEAPONS menu (the old LOADOUT tab): category tabs, the list of the game's weapons, a 3D preview of the chosen weapon that can be
    /// rotated with the mouse, and its real stats (read from the weapon prefabs, see WeaponCatalogAsset). The customization slots (skin,
    /// optic, muzzle, grip, magazine) are shown as COMING SOON: weapons are found as loot in a match, and nothing here changes them yet.
    /// </summary>
    public class LobbyWeaponsScreen : LobbyMenuScreen
    {
        protected override string Title => "WEAPONS";
        protected override string Subtitle => "ARMORY & CUSTOMIZATION";
        protected override int NavIndex => 2;
        protected override string Hints => "W S  BROWSE      Q E  CATEGORY      DRAG  ROTATE      ESC  BACK";

        private static readonly WeaponCategory[] Categories = { WeaponCategory.Pistol, WeaponCategory.Smg, WeaponCategory.Shotgun, WeaponCategory.Sniper, WeaponCategory.Melee };
        private static readonly string[] SlotNames = { "SKIN", "OPTIC", "MUZZLE", "GRIP", "MAGAZINE" };

        private WeaponCatalogAsset catalog;
        private readonly List<Image> tabBg = new List<Image>();
        private readonly List<TextMeshProUGUI> tabLabel = new List<TextMeshProUGUI>();
        private RectTransform listRoot, detailRoot;
        private CanvasGroup detailGroup;
        private RawImage previewImage;
        private TextMeshProUGUI pName, pCategory, pFireMode, dBlurb, dNote;
        private readonly TextMeshProUGUI[] statName = new TextMeshProUGUI[5];
        private readonly TextMeshProUGUI[] statValue = new TextMeshProUGUI[5];
        private readonly Image[] statFill = new Image[5];
        private readonly TextMeshProUGUI[] infoValue = new TextMeshProUGUI[3];

        private int tab;
        private int focus;
        private List<WeaponCatalogAsset.Entry> list = new List<WeaponCatalogAsset.Entry>();
        private float detailT = 1f;
        private WeaponPreviewStage stage;

        // largest values over the whole catalog: the bars compare weapons with each other
        private float maxDamage = 1f, maxRpm = 1f, maxMag = 1f;

        protected override void BuildContent(RectTransform c)
        {
            catalog = WeaponCatalogAsset.Load();
            if (catalog != null && catalog.weapons.Count > 0)
            {
                maxDamage = Mathf.Max(1f, catalog.weapons.Max(w => w.damage));
                maxRpm = Mathf.Max(1f, catalog.weapons.Max(w => w.roundsPerMinute));
                maxMag = Mathf.Max(1f, catalog.weapons.Max(w => w.magazine));
            }

            string[] names = Categories.Select(WeaponCatalogAsset.CategoryName).ToArray();
            BuildTabs(c, names, -880f, 360f, 200f, SetTab, tabBg, tabLabel);

            // left: the weapons of the category
            listRoot = NewRect("List", c, new Vector2(0.5f, 0.5f), new Vector2(430f, 700f), new Vector2(-880f + 215f, -48f));

            // centre: 3D preview with the name over it, and the customization slots under it
            var frame = Img("PreviewFrame", c, Panel); Place(frame.rectTransform, new Vector2(-10f, 22f), new Vector2(850f, 560f));
            var edge = Img("Edge", frame.rectTransform, new Color(1f, 1f, 1f, 0.5f));
            var er = edge.rectTransform; er.anchorMin = new Vector2(0f, 1f); er.anchorMax = new Vector2(1f, 1f); er.pivot = new Vector2(0.5f, 1f); er.anchoredPosition = Vector2.zero; er.sizeDelta = new Vector2(0f, 3f);

            var raw = new GameObject("Preview", typeof(RectTransform), typeof(RawImage));
            raw.transform.SetParent(frame.rectTransform, false);
            previewImage = raw.GetComponent<RawImage>();
            var rr = (RectTransform)raw.transform; rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one; rr.offsetMin = new Vector2(0f, 0f); rr.offsetMax = new Vector2(0f, -3f);
            previewImage.raycastTarget = true;
            previewImage.color = new Color(1f, 1f, 1f, 0f);
            raw.AddComponent<PreviewDrag>().owner = this;

            pCategory = LabelL(frame.rectTransform, "", 14, false, Dim, -400f, 252f, 500f);
            pName = LabelL(frame.rectTransform, "", 46, true, Color.white, -400f, 208f, 780f, 60f);
            pFireMode = LabelR(frame.rectTransform, "", 15, true, Dim, 400f, 252f, 400f);
            LabelC(frame.rectTransform, "DRAG TO ROTATE", 12, false, Faint, new Vector2(0f, -258f), new Vector2(400f, 20f));

            // customization slots (not available yet)
            LabelL(c, "CUSTOMIZATION", 14, false, Dim, -435f, -283f, 400f);
            for (int i = 0; i < SlotNames.Length; i++)
            {
                float w = 160f, gap = 12f;
                float x = -435f + w * 0.5f + i * (w + gap);
                var slot = Img("Slot " + SlotNames[i], c, new Color(0.09f, 0.095f, 0.11f, 1f));
                Place(slot.rectTransform, new Vector2(x, -334f), new Vector2(w, 66f));
                var top = Img("Top", slot.rectTransform, new Color(1f, 1f, 1f, 0.18f));
                var tr = top.rectTransform; tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0.5f, 1f); tr.anchoredPosition = Vector2.zero; tr.sizeDelta = new Vector2(0f, 2f);
                LabelC(slot.rectTransform, SlotNames[i], 15, true, Dim, new Vector2(0f, 12f), new Vector2(w - 10f, 22f));
                LabelC(slot.rectTransform, "COMING SOON", 12, false, Faint, new Vector2(0f, -14f), new Vector2(w - 10f, 18f));
            }

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
                float y = 300f - i * 62f;
                statName[i] = LabelL(detailRoot, names[i], 14, true, Color.white, L, y + 14f, 220f);
                statValue[i] = LabelR(detailRoot, "", 14, true, Dim, L + W, y + 14f, 120f);
                var f = Bar(detailRoot, new Vector2(0f, y - 12f), new Vector2(W, 10f), 0f, Color.white);
                statFill[i] = f;
            }

            var rule = Img("Rule", detailRoot, Line); Place(rule.rectTransform, new Vector2(0f, -22f), new Vector2(W, 2f));

            string[] info = { "FIRE MODE", "RESERVE AMMO", "AIMING" };
            for (int i = 0; i < 3; i++)
            {
                float y = -56f - i * 38f;
                LabelL(detailRoot, info[i], 13, false, Faint, L, y, 160f);
                infoValue[i] = LabelR(detailRoot, "", 16, true, Color.white, L + W, y, 200f);
            }

            var rule2 = Img("Rule2", detailRoot, Line); Place(rule2.rectTransform, new Vector2(0f, -176f), new Vector2(W, 2f));
            dBlurb = LabelL(detailRoot, "", 16, false, Dim, L, -270f, W, 160f);
            dBlurb.enableWordWrapping = true; dBlurb.alignment = TextAlignmentOptions.TopLeft; dBlurb.characterSpacing = 1f;
            dNote = LabelL(detailRoot, "FOUND AS LOOT IN A MATCH", 12, true, Faint, L, -352f, W);
        }

        // ---------------- open / close ----------------
        protected override void OnOpened()
        {
            tab = 0; focus = 0;
            if (stage == null) stage = WeaponPreviewStage.Create();
            stage.SetActive(true);
            previewImage.texture = stage.Texture;
            RebuildList();
            Refresh(true);
        }

        protected override void OnClosing()
        {
            if (stage != null) stage.SetActive(false);
        }

        protected override void OnDestroy()
        {
            if (stage != null) stage.Dispose();
            base.OnDestroy();
        }

        // ---------------- input and animation ----------------
        protected override void OnTick()
        {
            if (detailT < 1f)
            {
                detailT = Mathf.MoveTowards(detailT, 1f, Time.unscaledDeltaTime * 5f);
                float e = 1f - (1f - detailT) * (1f - detailT);
                detailGroup.alpha = e;
                detailRoot.anchoredPosition = new Vector2(680f + (1f - e) * 30f, -8f);
                var col = previewImage.color; col.a = Mathf.Clamp01(e * 1.4f); previewImage.color = col;
            }
            if (stage != null) stage.Tick(Time.unscaledDeltaTime);
        }

        protected override void OnKeys()
        {
            if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.LeftArrow)) SetTab(tab - 1);
            else if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.RightArrow)) SetTab(tab + 1);
            else if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) SetFocus(focus - 1);
            else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) SetFocus(focus + 1);
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
            Refresh(true);
        }

        // ---------------- list and detail ----------------
        private readonly List<Image> rowBg = new List<Image>();
        private readonly List<Image> rowBar = new List<Image>();
        private readonly List<TextMeshProUGUI> rowName = new List<TextMeshProUGUI>();
        private readonly List<TextMeshProUGUI> rowSub = new List<TextMeshProUGUI>();

        private void RebuildList()
        {
            for (int i = listRoot.childCount - 1; i >= 0; i--) Destroy(listRoot.GetChild(i).gameObject);
            rowBg.Clear(); rowBar.Clear(); rowName.Clear(); rowSub.Clear();
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
                var bar = Img("Bar", row, Color.white);
                var br = bar.rectTransform; br.anchorMin = Vector2.zero; br.anchorMax = new Vector2(0f, 1f); br.pivot = new Vector2(0f, 0.5f); br.sizeDelta = new Vector2(5f, 0f); br.anchoredPosition = Vector2.zero;
                var nm = LabelL(row, w.displayName, 26, true, Color.white, -195f, 14f, 400f); nm.characterSpacing = 6f;
                var sub = LabelL(row, w.fireMode + (w.isMelee ? "" : "   |   " + w.magazine + " ROUND MAG"), 13, false, Dim, -195f, -22f, 400f);
                rowBg.Add(bg); rowBar.Add(bar); rowName.Add(nm); rowSub.Add(sub);
            }
        }

        private void Refresh(bool animate)
        {
            MarkTabs(tabBg, tabLabel, tab);
            for (int i = 0; i < rowBg.Count; i++)
            {
                bool on = i == focus;
                rowBg[i].color = on ? PanelHi : Panel;
                rowBar[i].color = on ? Color.white : new Color(1f, 1f, 1f, 0.2f);
                rowName[i].color = on ? Color.white : Dim;
            }

            var w = list.Count > 0 && focus < list.Count ? list[focus] : null;
            if (w == null)
            {
                pName.text = ""; pCategory.text = ""; pFireMode.text = ""; dBlurb.text = "";
                for (int i = 0; i < 5; i++) { statValue[i].text = ""; SetBar(statFill[i], 0f); }
                for (int i = 0; i < 3; i++) infoValue[i].text = "";
                if (stage != null) stage.Show(null);
                return;
            }

            if (animate) detailT = 0f;
            pCategory.text = WeaponCatalogAsset.CategoryName(w.category).TrimEnd('S');
            pName.text = w.displayName;
            pFireMode.text = w.fireMode;
            dBlurb.text = w.blurb;

            if (w.isMelee)
            {
                statName[0].text = "DAMAGE"; statValue[0].text = w.damage.ToString("0"); SetBar(statFill[0], w.damage / maxDamage);
                for (int i = 1; i < 5; i++) { statValue[i].text = "-"; SetBar(statFill[i], 0f); }
                infoValue[0].text = "MELEE"; infoValue[1].text = "-"; infoValue[2].text = "-";
            }
            else
            {
                string dmg = w.pellets > 1 ? w.damage.ToString("0") + "  (" + w.pellets + " PELLETS)" : w.damage.ToString("0");
                statValue[0].text = dmg; SetBar(statFill[0], w.damage / maxDamage);
                statValue[1].text = w.roundsPerMinute.ToString("0") + " RPM"; SetBar(statFill[1], w.roundsPerMinute / maxRpm);
                float recoilControl = 1f - Mathf.Clamp01(w.recoil / 0.3f);
                statValue[2].text = Mathf.RoundToInt(recoilControl * 100f).ToString();
                SetBar(statFill[2], recoilControl);
                float accuracy = 1f - Mathf.Clamp01(w.spreadPerShot / 1.2f);
                statValue[3].text = Mathf.RoundToInt(accuracy * 100f).ToString();
                SetBar(statFill[3], accuracy);
                statValue[4].text = w.magazine.ToString(); SetBar(statFill[4], w.magazine / maxMag);
                infoValue[0].text = w.fireMode; infoValue[1].text = w.reserve.ToString(); infoValue[2].text = w.aimMode;
            }
            if (stage != null) stage.Show(w.prefab);
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
                if (current != null) { Object.Destroy(current); current = null; }
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
