using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// Markers for the loot a killed bot drops (medkit, ammo box). The pickups are small (~40 cm) and the map's grass is knee to waist
    /// high, so a drop was easy to lose even a few metres away. Each tracked drop gets a diamond with an icon above it and a label with
    /// the distance; within reach the label tells the player to walk over it (pickups are collected on touch).
    ///
    /// States: pop-in pulse when the drop appears, full opacity up to 25 m, fading out by 60 m, hidden behind the camera, "near" within
    /// 3.5 m (filled diamond, prompt), removed once collected or gone. Runtime-built uGUI (no scene changes), 1920x1080 reference canvas,
    /// drawn just below the match HUD. Clients only: a headless server draws nothing.
    /// </summary>
    public sealed class LootMarkersHUD : MonoBehaviour
    {
        public enum Kind { Medkit, Ammo }

        private const float FullAlphaDistance = 25f, MaxDistance = 60f, NearDistance = 3.5f, HeightOffset = 0.55f;
        private const int MaxMarkers = 24;
        private static readonly Color MedkitAccent = new Color(0.49f, 1f, 0.61f, 1f);   // #7CFF9B
        private static readonly Color AmmoAccent = new Color(1f, 0.84f, 0.31f, 1f);     // #FFD54F, as the "+AMMO COLLECTED" toast

        private static LootMarkersHUD instance;
        public static int TrackedCount => instance != null ? instance.markers.Count : 0;

        /// <summary>Show a marker over this dropped pickup until it is collected or destroyed.</summary>
        public static void Track(GameObject item, Kind kind)
        {
            if (item == null || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (DedicatedServerRuntime.IsDedicatedServer) return;
            if (instance == null)
            {
                var go = new GameObject("[HUD] Loot Markers");
                instance = go.AddComponent<LootMarkersHUD>();   // lives in the match scene, gone with it
            }
            instance.Add(item, kind);
        }

        private sealed class Marker
        {
            public GameObject Item;
            public Kind Kind;
            public RectTransform Root;
            public CanvasGroup Group;
            public Image DiamondFill;
            public TextMeshProUGUI Label;
            public float Born;
            public bool Near;
            public float Distance;
        }

        private readonly List<Marker> markers = new List<Marker>();
        private RectTransform layer;
        private Canvas canvas;
        private TMP_FontAsset font;
        private Sprite pixel;

        private void Awake()
        {
            var cgo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler));
            cgo.transform.SetParent(transform, false);
            canvas = cgo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 39;   // the match HUD is 40
            var scaler = cgo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            layer = (RectTransform)cgo.transform;
            font = FindFont("Rajdhani-SemiBold") ?? FindFont("Rajdhani");
            pixel = MakePixel();
        }

        private void OnDestroy() { if (instance == this) instance = null; }

        private void Add(GameObject item, Kind kind)
        {
            foreach (var m in markers) if (m.Item == item) return;
            if (markers.Count >= MaxMarkers) RemoveAt(0);   // the oldest goes first
            markers.Add(Build(item, kind));
        }

        private Marker Build(GameObject item, Kind kind)
        {
            Color accent = kind == Kind.Medkit ? MedkitAccent : AmmoAccent;
            var root = NewRect("Marker", layer, new Vector2(120, 70));
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false; group.interactable = false;

            // diamond: dark plate, accent outline, fill that shows when the player is close
            var diamond = NewRect("Diamond", root, new Vector2(26, 26));
            diamond.anchoredPosition = new Vector2(0, 14);
            diamond.localRotation = Quaternion.Euler(0, 0, 45);
            NewImage("Outline", diamond, accent, Vector2.zero, new Vector2(26, 26));
            NewImage("Plate", diamond, new Color(0.04f, 0.05f, 0.06f, 0.82f), Vector2.zero, new Vector2(21, 21));
            var fill = NewImage("Fill", diamond, accent, Vector2.zero, new Vector2(21, 21));
            fill.color = new Color(accent.r, accent.g, accent.b, 0f);

            // icon, upright (child of the marker, not of the rotated diamond)
            var icon = NewRect("Icon", root, new Vector2(14, 14));
            icon.anchoredPosition = new Vector2(0, 14);
            if (kind == Kind.Medkit)
            {
                NewImage("H", icon, Color.white, Vector2.zero, new Vector2(12, 3.5f));
                NewImage("V", icon, Color.white, Vector2.zero, new Vector2(3.5f, 12));
            }
            else
            {
                for (int i = -1; i <= 1; i++) NewImage("Round", icon, Color.white, new Vector2(i * 4f, 0), new Vector2(2.5f, 11));
            }

            // label on a dark plate
            var plate = NewRect("LabelPlate", root, new Vector2(150, 22));
            plate.anchoredPosition = new Vector2(0, -12);
            var plateImg = plate.gameObject.AddComponent<Image>();
            plateImg.sprite = pixel; plateImg.color = new Color(0f, 0f, 0f, 0.55f); plateImg.raycastTarget = false;
            var label = NewText(plate, kind == Kind.Medkit ? "MEDKIT" : "AMMO");

            return new Marker { Item = item, Kind = kind, Root = root, Group = group, DiamondFill = fill, Label = label, Born = Time.unscaledTime };
        }

        private void LateUpdate()
        {
            var cam = Camera.main;
            Transform me = JUTPS.JUGameManager.PlayerController != null ? JUTPS.JUGameManager.PlayerController.transform : (cam != null ? cam.transform : null);
            for (int i = markers.Count - 1; i >= 0; i--)
            {
                var m = markers[i];
                if (IsGone(m)) { RemoveAt(i); continue; }
                if (cam == null || me == null) { m.Group.alpha = 0f; continue; }

                Vector3 world = m.Item.transform.position + Vector3.up * HeightOffset;
                Vector3 screen = cam.WorldToScreenPoint(world);
                float distance = Vector3.Distance(me.position, m.Item.transform.position);
                if (screen.z <= 0.1f || distance > MaxDistance) { m.Group.alpha = 0f; continue; }

                RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screen, null, out Vector2 local);
                m.Root.anchoredPosition = local + new Vector2(0, 24);

                float alpha = distance <= FullAlphaDistance ? 1f : 1f - Mathf.InverseLerp(FullAlphaDistance, MaxDistance, distance);
                float age = Time.unscaledTime - m.Born;
                float pop = age < 0.4f ? Mathf.Lerp(1.6f, 1f, Mathf.SmoothStep(0f, 1f, age / 0.4f)) : 1f;
                m.Group.alpha = alpha * Mathf.Clamp01(age / 0.25f);

                bool near = distance <= NearDistance;
                float scale = pop * (near ? 1.15f : 1f);
                m.Root.localScale = new Vector3(scale, scale, 1f);
                var c = m.DiamondFill.color;
                c.a = Mathf.MoveTowards(c.a, near ? 0.9f : 0f, Time.unscaledDeltaTime * 4f);
                m.DiamondFill.color = c;

                string name = m.Kind == Kind.Medkit ? "MEDKIT" : "AMMO";
                string text = near ? "WALK OVER TO PICK UP" : name + "  ·  " + Mathf.RoundToInt(distance) + " M";
                if (m.Near != near || m.Label.text != text) { m.Label.text = text; m.Near = near; }
                m.Distance = distance;
            }
            Declutter();
        }

        // A bot often drops a medkit and an ammo box side by side: their markers landed on top of each other. The nearer marker keeps
        // its place; any marker that would overlap an already placed one moves up one row.
        private readonly List<Marker> placed = new List<Marker>();
        private void Declutter()
        {
            placed.Clear();
            foreach (var m in markers) if (m.Group.alpha > 0.01f) placed.Add(m);
            placed.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            for (int i = 1; i < placed.Count; i++)
            {
                var pos = placed[i].Root.anchoredPosition;
                for (int guard = 0; guard < 6; guard++)
                {
                    bool overlaps = false;
                    for (int j = 0; j < i; j++)
                    {
                        Vector2 d = placed[j].Root.anchoredPosition - pos;
                        if (Mathf.Abs(d.x) < 150f && Mathf.Abs(d.y) < 52f) { overlaps = true; break; }
                    }
                    if (!overlaps) break;
                    pos.y += 54f;
                }
                placed[i].Root.anchoredPosition = pos;
            }
        }

        private static bool IsGone(Marker m)
        {
            if (m.Item == null || !m.Item.activeInHierarchy) return true;
            if (m.Kind == Kind.Medkit) { var p = m.Item.GetComponent<Railbound.Gameplay.MedkitPickup>(); return p != null && p.IsPickedUp; }
            var a = m.Item.GetComponent<Railbound.Gameplay.AmmoBoxPickup>();
            return a != null && a.IsCollected;
        }

        private void RemoveAt(int i)
        {
            if (markers[i].Root != null) Destroy(markers[i].Root.gameObject);
            markers.RemoveAt(i);
        }

        // ------------------------------------------------------------------ building blocks
        private static RectTransform NewRect(string name, Transform parent, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = size;
            return r;
        }

        private Image NewImage(string name, Transform parent, Color color, Vector2 pos, Vector2 size)
        {
            var r = NewRect(name, parent, size);
            r.anchoredPosition = pos;
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = pixel; img.color = color; img.raycastTarget = false;
            return img;
        }

        private TextMeshProUGUI NewText(Transform parent, string text)
        {
            var r = NewRect("Label", parent, new Vector2(150, 22));
            var t = r.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text; t.fontSize = 16; t.characterSpacing = 4f; t.color = new Color(1f, 1f, 1f, 0.95f);
            t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
            t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        private static TMP_FontAsset FindFont(string namePart)
        {
            foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>()) if (f != null && f.name.Contains(namePart)) return f;
            return null;
        }

        private static Sprite MakePixel()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var cols = new Color32[16];
            for (int i = 0; i < cols.Length; i++) cols[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(cols); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
