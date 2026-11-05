using System.Collections;
using System.Collections.Generic;
using JUTPS;
using Railbound.BR;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// Clean grid-based automap for MAP-1.
    ///  - A small widescreen minimap (top-left) that follows the camera and rotates with the view.
    ///  - Press <see cref="mapKey"/> (M) for a large north-up map with the 200 m grid, grid references and a details panel.
    /// The map base is a RenderTexture baked from the terrain heightmap (see <see cref="MapTerrainBaker"/>) and shown
    /// through RawImages on UI canvases. The safe-zone boundary, the next zone and the safe path to it are drawn with dots.
    /// Enemies stay hidden (fog of war) and only appear through <see cref="PingEnemy"/> (sonar).
    /// </summary>
    public class Map1MiniMapController : MonoBehaviour
    {
        public static Map1MiniMapController Instance { get; private set; }

        [Header("Input")]
        public KeyCode mapKey = KeyCode.M;

        [Header("Map data")]
        [Tooltip("Terrain below this height (metres) is tinted as water on the map. Leave at -999 to disable.")]
        [System.NonSerialized] public float waterLevel = -999f;

        // ---- layout (kept out of the scene so a stale scene value can never override these) ----
        [System.NonSerialized] private Vector2 miniSize = new Vector2(300f, 176f);
        [System.NonSerialized] private Vector2 miniPosition = new Vector2(20f, -20f);
        [System.NonSerialized] private float miniViewMeters = 300f;     // metres visible across the minimap width
        [System.NonSerialized] private float fullMapPixels = 820f;      // square size of the big map (canvas units)
        [System.NonSerialized] private int textureSize = 1536;

        private static readonly Color White = Color.white;
        private static readonly Color Danger = new Color(1f, 0.36f, 0.30f, 1f);
        private static readonly Color Amber = new Color(1f, 0.74f, 0.25f, 1f);
        private static readonly Color Panel = new Color(0.02f, 0.025f, 0.03f, 0.88f);

        // ---- map space ----
        private Terrain _terrain;
        private Vector2 _mapCenter;      // world XZ of the map centre
        private float _mapSize = 1600f;  // metres (square)
        private Vector2 _mapOrigin;      // world XZ of the south-west corner
        private RenderTexture _mapRT;
        private bool _baked;

        // ---- scene refs ----
        private Transform _player;
        private Camera _cam;
        private float _nextResolve;

        // ---- views ----
        private class MapView
        {
            public RectTransform viewport, world;
            public RawImage image;
            public RectTransform playerArrow;
            public float ppm;                 // UI pixels per metre
            public bool isFull;
            public readonly List<Image> zoneDots = new List<Image>();
            public readonly List<Image> nextDots = new List<Image>();
            public readonly List<Image> pathDots = new List<Image>();
        }
        private MapView _mini, _full;

        private class PingData { public Transform Target; public float End; public Image MiniIcon, FullIcon; }
        private readonly Dictionary<Transform, PingData> _pings = new Dictionary<Transform, PingData>();

        // mini extras
        private CanvasGroup _miniGroup;
        private TextMeshProUGUI _miniGrid, _miniNorth;

        // full-map overlay
        private RectTransform _overlay, _overlayContent;
        private CanvasGroup _overlayGroup;
        private TextMeshProUGUI _tTitle, _tGrid, _tCoords, _tPhase, _tZoneState, _tTimer, _tDist, _tRadius, _tHostiles;
        private float _openT;                 // 0..1 animated
        private bool _wantOpen;
        private float _nextTextUpdate, _nextRingUpdate;

        // sprites
        private static Sprite _dot, _arrow;

        public bool IsFullMapOpen => _wantOpen;

        // ------------------------------------------------------------------------------------------------ lifecycle
        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_overlay != null) Destroy(_overlay.gameObject);
            if (_mapRT != null) { _mapRT.Release(); Destroy(_mapRT); }
        }

        private void Start()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { enabled = false; return; }

            FindMapData();
            Resolve(true);

            var rt = (RectTransform)transform;
            // wipe the previous (MiniMapView based) hierarchy
            for (int i = rt.childCount - 1; i >= 0; i--) Destroy(rt.GetChild(i).gameObject);
            var oldView = GetComponent("MiniMapView");
            if (oldView != null) Destroy(oldView);

            _mapRT = MapTerrainBaker.CreateTarget(textureSize);
            BuildMini(rt);
            BuildFull(rt);

            if (_terrain != null)
            {
                var s = MapTerrainBaker.Settings.Default;
                s.textureSize = textureSize;
                s.waterLevel = waterLevel;
                StartCoroutine(MapTerrainBaker.Bake(_terrain, _mapRT, s, () => _baked = true));
            }
        }

        private void FindMapData()
        {
            _terrain = Terrain.activeTerrain != null ? Terrain.activeTerrain : FindObjectOfType<Terrain>();
            if (_terrain != null && _terrain.terrainData != null)
            {
                Vector3 p = _terrain.transform.position;
                Vector3 sz = _terrain.terrainData.size;
                _mapSize = Mathf.Max(sz.x, sz.z);
                _mapOrigin = new Vector2(p.x, p.z);
            }
            else
            {
                _mapSize = 1600f;
                _mapOrigin = new Vector2(-800f, -800f);
            }
            _mapCenter = _mapOrigin + Vector2.one * (_mapSize * 0.5f);
        }

        private void Resolve(bool force = false)
        {
            if (!force && Time.unscaledTime < _nextResolve) return;
            _nextResolve = Time.unscaledTime + 0.5f;
            if (_player == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) _player = p.transform;
            }
            if (_cam == null) _cam = Camera.main;
        }

        // ------------------------------------------------------------------------------------------------ build: minimap
        private void BuildMini(RectTransform root)
        {
            if (root.parent != null) root.gameObject.layer = root.parent.gameObject.layer;   // UI layer (setup tool created it on Default)
            root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = miniPosition;
            root.sizeDelta = miniSize;
            root.localScale = Vector3.one;

            // own canvas: the minimap re-batches every frame and must not dirty the rest of the HUD
            if (root.GetComponent<Canvas>() == null) root.gameObject.AddComponent<Canvas>();
            _miniGroup = root.GetComponent<CanvasGroup>();
            if (_miniGroup == null) _miniGroup = root.gameObject.AddComponent<CanvasGroup>();
            _miniGroup.blocksRaycasts = false; _miniGroup.interactable = false;

            var back = NewImage("Back", root, null, Panel);
            Stretch(back.rectTransform, 0f);

            _mini = new MapView { ppm = miniSize.x / miniViewMeters };
            BuildView(_mini, root, "Viewport", new Vector2(miniSize.x - 6f, miniSize.y - 6f), false);

            // player arrow, fixed at the centre (the map rotates under it)
            _mini.playerArrow = NewImage("Player", _mini.viewport, GetArrow(), White).rectTransform;
            _mini.playerArrow.sizeDelta = new Vector2(18f, 18f);

            AddBorder(root, 1.5f, new Color(1f, 1f, 1f, 0.55f));

            _miniNorth = NewText("North", root, "N", 17f, TextAlignmentOptions.Center, White, true);
            _miniNorth.rectTransform.sizeDelta = new Vector2(22f, 22f);

            _miniGrid = NewText("Grid", root, "", 15f, TextAlignmentOptions.BottomLeft, new Color(1f, 1f, 1f, 0.85f), true);
            var gr = _miniGrid.rectTransform;
            gr.anchorMin = gr.anchorMax = new Vector2(0f, 0f);
            gr.pivot = new Vector2(0f, 0f);
            gr.sizeDelta = new Vector2(120f, 24f);
            gr.anchoredPosition = new Vector2(9f, 6f);
        }

        // ------------------------------------------------------------------------------------------------ build: full map
        private void BuildFull(RectTransform miniRoot)
        {
            var canvas = miniRoot.GetComponentInParent<Canvas>();
            Transform canvasRoot = canvas != null ? canvas.rootCanvas.transform : miniRoot.root;

            _overlay = new GameObject("MapOverlay", typeof(RectTransform)).GetComponent<RectTransform>();
            _overlay.gameObject.layer = canvasRoot.gameObject.layer;
            _overlay.SetParent(canvasRoot, false);
            Stretch(_overlay, 0f);
            // sit just under the pause / settings screens
            int idx = canvasRoot.childCount - 1;
            for (int i = 0; i < canvasRoot.childCount; i++)
            {
                string childName = canvasRoot.GetChild(i).name;
                if (childName == "Pause Screen" || childName == "Settings Screen") { idx = i; break; }
            }
            _overlay.SetSiblingIndex(idx);

            var oc = _overlay.gameObject.AddComponent<Canvas>();
            oc.overrideSorting = false;
            _overlayGroup = _overlay.gameObject.AddComponent<CanvasGroup>();
            _overlayGroup.alpha = 0f; _overlayGroup.blocksRaycasts = false; _overlayGroup.interactable = false;

            var dim = NewImage("Dim", _overlay, null, new Color(0f, 0f, 0f, 0.8f));
            Stretch(dim.rectTransform, 0f);
            dim.raycastTarget = false;

            const float pad = 34f, gap = 26f, infoW = 400f;
            float mapPx = fullMapPixels;
            float totalW = mapPx + pad * 2f + gap + infoW;
            float totalH = mapPx + pad * 2f;

            _overlayContent = NewRect("Content", _overlay);
            _overlayContent.sizeDelta = new Vector2(totalW, totalH);

            // --- map frame (with grid labels) ---
            var frame = NewImage("Frame", _overlayContent, null, Panel);
            frame.rectTransform.anchorMin = frame.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            frame.rectTransform.pivot = new Vector2(0f, 0.5f);
            frame.rectTransform.anchoredPosition = Vector2.zero;
            frame.rectTransform.sizeDelta = new Vector2(mapPx + pad * 2f, totalH);

            _full = new MapView { isFull = true, ppm = mapPx / _mapSize };
            BuildView(_full, frame.rectTransform, "Viewport", new Vector2(mapPx, mapPx), true);

            _full.playerArrow = NewImage("Player", _full.world, GetArrow(), White).rectTransform;
            _full.playerArrow.sizeDelta = new Vector2(26f, 26f);

            AddBorder(_full.viewport.parent as RectTransform, 1.5f, new Color(1f, 1f, 1f, 0.6f), _full.viewport, 0f);

            // grid labels: A..H along the top, 1..8 down the left side (cells are 200 m)
            int cells = Mathf.Max(1, Mathf.RoundToInt(_mapSize / 200f));
            float cellPx = mapPx / cells;
            for (int i = 0; i < cells; i++)
            {
                var col = NewText("Col" + i, frame.rectTransform, ((char)('A' + i)).ToString(), 18f, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.85f), true);
                col.rectTransform.sizeDelta = new Vector2(cellPx, 26f);
                col.rectTransform.anchoredPosition = new Vector2(-mapPx * 0.5f + cellPx * (i + 0.5f), mapPx * 0.5f + pad * 0.5f);
                var row = NewText("Row" + i, frame.rectTransform, (i + 1).ToString(), 18f, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.85f), true);
                row.rectTransform.sizeDelta = new Vector2(26f, cellPx);
                row.rectTransform.anchoredPosition = new Vector2(-mapPx * 0.5f - pad * 0.5f, mapPx * 0.5f - cellPx * (i + 0.5f));
            }

            // --- info panel ---
            var info = NewImage("Info", _overlayContent, null, Panel);
            var ir = info.rectTransform;
            ir.anchorMin = ir.anchorMax = new Vector2(1f, 0.5f);
            ir.pivot = new Vector2(1f, 0.5f);
            ir.anchoredPosition = Vector2.zero;
            ir.sizeDelta = new Vector2(infoW, totalH);
            AddBorder(ir, 1.5f, new Color(1f, 1f, 1f, 0.35f));

            float y = -26f;
            _tTitle = InfoText(ir, "MAP-1", 40f, ref y, 52f, White, true);
            _tGrid = InfoText(ir, "GRID --", 22f, ref y, 34f, new Color(1f, 1f, 1f, 0.8f), true);
            _tCoords = InfoText(ir, "", 18f, ref y, 28f, new Color(1f, 1f, 1f, 0.6f), false);
            Divider(ir, ref y);
            _tPhase = InfoText(ir, "", 20f, ref y, 30f, White, true);
            _tZoneState = InfoText(ir, "", 17f, ref y, 26f, new Color(1f, 1f, 1f, 0.6f), true);
            _tTimer = InfoText(ir, "--:--", 46f, ref y, 58f, White, true);
            _tDist = InfoText(ir, "", 20f, ref y, 30f, White, true);
            _tRadius = InfoText(ir, "", 17f, ref y, 28f, new Color(1f, 1f, 1f, 0.6f), false);
            Divider(ir, ref y);
            _tHostiles = InfoText(ir, "", 18f, ref y, 28f, White, true);
            Divider(ir, ref y);

            // legend
            LegendRow(ir, ref y, "YOU", LegendKind.Arrow, White);
            LegendRow(ir, ref y, "SAFE ZONE", LegendKind.Dots, White);
            LegendRow(ir, ref y, "NEXT ZONE", LegendKind.DotsSmall, new Color(1f, 1f, 1f, 0.5f));
            LegendRow(ir, ref y, "SAFE PATH", LegendKind.Path, Amber);
            LegendRow(ir, ref y, "HOSTILE (SONAR)", LegendKind.Dot, Danger);

            var close = NewText("Close", ir, "[" + mapKey + "]  CLOSE", 17f, TextAlignmentOptions.BottomRight, new Color(1f, 1f, 1f, 0.7f), true);
            close.rectTransform.anchorMin = close.rectTransform.anchorMax = new Vector2(1f, 0f);
            close.rectTransform.pivot = new Vector2(1f, 0f);
            close.rectTransform.sizeDelta = new Vector2(200f, 26f);
            close.rectTransform.anchoredPosition = new Vector2(-18f, 14f);

            // north label on the frame
            var n = NewText("N", frame.rectTransform, "N", 18f, TextAlignmentOptions.Center, White, true);
            n.rectTransform.sizeDelta = new Vector2(26f, 26f);
            n.rectTransform.anchoredPosition = new Vector2(-mapPx * 0.5f - pad * 0.5f, mapPx * 0.5f + pad * 0.5f);

            _overlay.gameObject.SetActive(false);
        }

        private enum LegendKind { Arrow, Dots, DotsSmall, Path, Dot }

        private void LegendRow(RectTransform parent, ref float y, string label, LegendKind kind, Color col)
        {
            var row = NewRect("Legend " + label, parent);
            row.anchorMin = row.anchorMax = new Vector2(0f, 1f);
            row.pivot = new Vector2(0f, 1f);
            row.sizeDelta = new Vector2(360f, 26f);
            row.anchoredPosition = new Vector2(20f, y);

            switch (kind)
            {
                case LegendKind.Arrow:
                    Icon(row, new Vector2(30f, -13f), GetArrow(), col, 18f);
                    break;
                case LegendKind.Dot:
                    Icon(row, new Vector2(30f, -13f), GetDot(), col, 10f);
                    break;
                case LegendKind.Dots:
                case LegendKind.DotsSmall:
                    for (int i = 0; i < 4; i++) Icon(row, new Vector2(10f + i * 14f, -13f), GetDot(), col, kind == LegendKind.Dots ? 7f : 5f);
                    break;
                case LegendKind.Path:
                    for (int i = 0; i < 4; i++) Icon(row, new Vector2(10f + i * 14f, -13f), GetDot(), col, 6f);
                    break;
            }

            var t = NewText("Label", row, label, 17f, TextAlignmentOptions.MidlineLeft, new Color(1f, 1f, 1f, 0.9f), true);
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            t.rectTransform.pivot = new Vector2(0f, 0.5f);
            t.rectTransform.sizeDelta = new Vector2(280f, 26f);
            t.rectTransform.anchoredPosition = new Vector2(76f, 0f);
            y -= 30f;
        }

        private static void Icon(RectTransform row, Vector2 pos, Sprite sp, Color col, float size)
        {
            var img = NewImage("Icon", row, sp, col);
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0f, 1f);
            img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            img.rectTransform.anchoredPosition = pos;
            img.rectTransform.sizeDelta = new Vector2(size, size);
        }

        private static TextMeshProUGUI InfoText(RectTransform parent, string text, float size, ref float y, float h, Color col, bool bold)
        {
            var t = NewText("Text", parent, text, size, TextAlignmentOptions.MidlineLeft, col, bold);
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0f, 1f);
            t.rectTransform.pivot = new Vector2(0f, 1f);
            t.rectTransform.sizeDelta = new Vector2(360f, h);
            t.rectTransform.anchoredPosition = new Vector2(20f, y);
            y -= h + 2f;
            return t;
        }

        private static void Divider(RectTransform parent, ref float y)
        {
            y -= 8f;
            var d = NewImage("Divider", parent, null, new Color(1f, 1f, 1f, 0.2f));
            d.rectTransform.anchorMin = d.rectTransform.anchorMax = new Vector2(0f, 1f);
            d.rectTransform.pivot = new Vector2(0f, 1f);
            d.rectTransform.sizeDelta = new Vector2(360f, 1.5f);
            d.rectTransform.anchoredPosition = new Vector2(20f, y);
            y -= 14f;
        }

        // ------------------------------------------------------------------------------------------------ shared view builder
        private void BuildView(MapView v, RectTransform parent, string name, Vector2 size, bool centered)
        {
            v.viewport = NewRect(name, parent);
            v.viewport.sizeDelta = size;
            v.viewport.gameObject.AddComponent<RectMask2D>();

            float worldPx = _mapSize * v.ppm;
            v.world = NewRect("World", v.viewport);
            v.world.sizeDelta = new Vector2(worldPx, worldPx);

            v.image = new GameObject("Map", typeof(RectTransform)).AddComponent<RawImage>();
            v.image.gameObject.layer = v.world.gameObject.layer;
            v.image.rectTransform.SetParent(v.world, false);
            Stretch(v.image.rectTransform, 0f);
            v.image.texture = _mapRT;
            v.image.color = Color.white;
            v.image.raycastTarget = false;
        }

        // ------------------------------------------------------------------------------------------------ public API
        public void ToggleFullMap() { SetFullMap(!_wantOpen); }

        public void SetFullMap(bool open)
        {
            _wantOpen = open;
            if (open && _overlay != null) _overlay.gameObject.SetActive(true);
        }

        /// <summary>
        /// Reveals an enemy on both maps with a pulsing red marker for <paramref name="duration"/> seconds,
        /// then it fades back into fog of war.
        /// </summary>
        public void PingEnemy(Transform enemy, float duration = 12f)
        {
            if (enemy == null || _mini == null) return;
            if (!_pings.TryGetValue(enemy, out var p))
            {
                p = new PingData { Target = enemy };
                p.MiniIcon = NewImage("Ping", _mini.world, GetDot(), Danger);
                p.FullIcon = NewImage("Ping", _full.world, GetDot(), Danger);
                _pings[enemy] = p;
            }
            p.End = Mathf.Max(p.End, Time.time + duration);
            p.MiniIcon.gameObject.SetActive(true);
            p.FullIcon.gameObject.SetActive(true);
        }

        // ------------------------------------------------------------------------------------------------ update
        private void Update()
        {
            if (_mini == null) return;
            Resolve();

            HandleInput();

            if (JUPauseGame.IsPaused && _wantOpen) SetFullMap(false);

            // open / close animation (unscaled so it also works with a frozen timeScale)
            _openT = Mathf.MoveTowards(_openT, _wantOpen ? 1f : 0f, Time.unscaledDeltaTime * 6.5f);
            float ease = 1f - (1f - _openT) * (1f - _openT);
            if (_overlay != null)
            {
                _overlayGroup.alpha = ease;
                _overlayContent.localScale = Vector3.one * Mathf.Lerp(0.95f, 1f, ease);
                if (_openT <= 0f && _overlay.gameObject.activeSelf) _overlay.gameObject.SetActive(false);
            }
            _miniGroup.alpha = 1f - ease;

            bool ringsDue = Time.unscaledTime >= _nextRingUpdate;
            if (ringsDue) _nextRingUpdate = Time.unscaledTime + 0.15f;

            UpdateZoneData(out bool hasZone, out Vector3 zc, out float zr, out Vector3 tc, out float tr, out bool outside);

            Vector3 pp = _player != null ? _player.position : Vector3.zero;
            float yaw = ViewYaw();

            // ---- minimap ----
            if (_miniGroup.alpha > 0.01f)
            {
                PositionMini(pp, yaw);
                if (ringsDue) UpdateRings(_mini, hasZone, zc, zr, tc, tr);
                UpdatePath(_mini, hasZone, pp, tc, tr, outside);
                UpdateNorth(yaw);
                if (_miniGrid != null && ringsDue) _miniGrid.text = _player != null ? GridRef(pp) : "";
            }

            // ---- full map ----
            if (_openT > 0f)
            {
                PositionFull(pp, yaw);
                if (ringsDue) UpdateRings(_full, hasZone, zc, zr, tc, tr);
                UpdatePath(_full, hasZone, pp, tc, tr, outside);
                if (ringsDue) _full.playerArrow.SetAsLastSibling();   // keep the player arrow above the dots
                if (Time.unscaledTime >= _nextTextUpdate)
                {
                    _nextTextUpdate = Time.unscaledTime + 0.25f;
                    UpdateInfo(hasZone, pp, zc, zr, tc, tr, outside);
                }
            }

            UpdatePings();
        }

        private void HandleInput()
        {
            if (JUPauseGame.IsPaused) return;
            bool pressed = Input.GetKeyDown(mapKey);
#if ENABLE_INPUT_SYSTEM
            if (!pressed && UnityEngine.InputSystem.Keyboard.current != null && mapKey == KeyCode.M)
                pressed = UnityEngine.InputSystem.Keyboard.current.mKey.wasPressedThisFrame;
#endif
            if (pressed) ToggleFullMap();
        }

        private float ViewYaw()
        {
            if (_cam != null) return _cam.transform.eulerAngles.y;
            if (_player != null) return _player.eulerAngles.y;
            return 0f;
        }

        private Vector2 ToMap(Vector3 w, float ppm) => new Vector2((w.x - _mapCenter.x) * ppm, (w.z - _mapCenter.y) * ppm);

        private void PositionMini(Vector3 pp, float yaw)
        {
            _mini.world.localRotation = Quaternion.Euler(0f, 0f, yaw);
            Vector2 local = ToMap(pp, _mini.ppm);
            float a = yaw * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            Vector2 rotated = new Vector2(local.x * c - local.y * s, local.x * s + local.y * c);
            _mini.world.anchoredPosition = -rotated;
            _mini.playerArrow.anchoredPosition = Vector2.zero;
            _mini.playerArrow.localRotation = Quaternion.identity;
        }

        private void PositionFull(Vector3 pp, float yaw)
        {
            _full.world.localRotation = Quaternion.identity;
            _full.world.anchoredPosition = Vector2.zero;
            _full.playerArrow.anchoredPosition = ToMap(pp, _full.ppm);
            _full.playerArrow.localRotation = Quaternion.Euler(0f, 0f, -yaw);
        }

        private void UpdateNorth(float yaw)
        {
            float a = yaw * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(-Mathf.Sin(a), Mathf.Cos(a));          // where north points on the minimap
            float hw = miniSize.x * 0.5f - 14f, hh = miniSize.y * 0.5f - 14f;
            float t = Mathf.Min(Mathf.Abs(dir.x) < 0.0001f ? 9999f : hw / Mathf.Abs(dir.x), Mathf.Abs(dir.y) < 0.0001f ? 9999f : hh / Mathf.Abs(dir.y));
            var r = _miniNorth.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = dir * t;
        }

        // ------------------------------------------------------------------------------------------------ zone dots / path
        private void UpdateZoneData(out bool hasZone, out Vector3 zc, out float zr, out Vector3 tc, out float tr, out bool outside)
        {
            var zm = BRZoneManager.Instance;
            hasZone = zm != null && zm.PhaseCount > 0;
            zc = tc = Vector3.zero; zr = tr = 0f; outside = false;
            if (!hasZone) return;
            zc = zm.CurrentCenter; zr = zm.CurrentRadius;
            tc = zm.TargetCenter; tr = zm.TargetRadius;
            if (_player != null) outside = !zm.IsPositionInSafeZone(_player.position);
        }

        private void UpdateRings(MapView v, bool hasZone, Vector3 zc, float zr, Vector3 tc, float tr)
        {
            float spacing = v.isFull ? 16f : 15f;
            DrawRing(v, v.zoneDots, hasZone ? zc : Vector3.zero, hasZone ? zr : 0f, spacing, v.isFull ? 6f : 7f, new Color(1f, 1f, 1f, 0.95f));
            // next zone only when it differs from the current one
            bool showNext = hasZone && (Mathf.Abs(tr - zr) > 1f || (tc - zc).sqrMagnitude > 1f);
            DrawRing(v, v.nextDots, showNext ? tc : Vector3.zero, showNext ? tr : 0f, spacing * 0.85f, v.isFull ? 4.5f : 5f, new Color(1f, 1f, 1f, 0.5f));
        }

        private void DrawRing(MapView v, List<Image> pool, Vector3 center, float radius, float spacingPx, float size, Color col)
        {
            int n = radius > 1f ? Mathf.Clamp(Mathf.RoundToInt(2f * Mathf.PI * radius * v.ppm / spacingPx), 12, 320) : 0;
            while (pool.Count < n) pool.Add(NewImage("Dot", v.world, GetDot(), col));
            Vector2 c = ToMap(center, v.ppm);
            float rp = radius * v.ppm;
            for (int i = 0; i < pool.Count; i++)
            {
                var d = pool[i];
                if (i >= n) { if (d.gameObject.activeSelf) d.gameObject.SetActive(false); continue; }
                if (!d.gameObject.activeSelf) d.gameObject.SetActive(true);
                float a = (i / (float)n) * Mathf.PI * 2f;
                d.rectTransform.anchoredPosition = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rp;
                d.rectTransform.sizeDelta = new Vector2(size, size);
                d.color = col;
            }
        }

        /// <summary>Marching dotted route from the player toward the safe zone (only while the player is not safe).</summary>
        private void UpdatePath(MapView v, bool hasZone, Vector3 pp, Vector3 tc, float tr, bool outsideCurrent)
        {
            const float spacingM = 20f;
            int count = 0;
            Vector2 p2 = new Vector2(pp.x, pp.z), t2 = new Vector2(tc.x, tc.z);
            float dist = Vector2.Distance(p2, t2);
            bool needPath = hasZone && _player != null && (outsideCurrent || dist > tr);
            if (needPath)
            {
                float len = Mathf.Max(0f, dist - tr * 0.85f);       // stop just inside the next safe circle
                Vector2 dir = dist > 0.01f ? (t2 - p2) / dist : Vector2.up;
                float offset = (Time.unscaledTime * 16f) % spacingM;
                Color col = outsideCurrent ? Danger : Amber;
                float size = v.isFull ? 6f : 6.5f;
                count = Mathf.Min(90, Mathf.CeilToInt(len / spacingM));
                for (int i = 0; i < count; i++)
                {
                    float along = offset + i * spacingM;
                    if (along > len) { count = i; break; }
                    while (v.pathDots.Count <= i) v.pathDots.Add(NewImage("Path", v.world, GetDot(), col));
                    var d = v.pathDots[i];
                    if (!d.gameObject.activeSelf) d.gameObject.SetActive(true);
                    Vector2 w = p2 + dir * along;
                    d.rectTransform.anchoredPosition = new Vector2((w.x - _mapCenter.x) * v.ppm, (w.y - _mapCenter.y) * v.ppm);
                    d.rectTransform.sizeDelta = new Vector2(size, size);
                    d.color = col;
                }
            }
            for (int i = count; i < v.pathDots.Count; i++)
                if (v.pathDots[i].gameObject.activeSelf) v.pathDots[i].gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------------------------------------ pings
        private readonly List<Transform> _scratch = new List<Transform>();

        private void UpdatePings()
        {
            if (_pings.Count == 0) return;
            float time = Time.time;
            _scratch.Clear();
            foreach (var kv in _pings)
            {
                var p = kv.Value;
                float remaining = p.End - time;
                if (p.Target == null || remaining <= 0f)
                {
                    if (p.MiniIcon != null) Destroy(p.MiniIcon.gameObject);
                    if (p.FullIcon != null) Destroy(p.FullIcon.gameObject);
                    _scratch.Add(kv.Key);
                    continue;
                }
                var h = p.Target.GetComponent<JUHealth>();
                if (h != null && h.IsDead) { p.End = 0f; continue; }

                float pulse = 1f + 0.28f * Mathf.Sin(time * 9f);
                float alpha = remaining < 1.5f ? remaining / 1.5f : 1f;
                var col = new Color(Danger.r, Danger.g, Danger.b, alpha);

                p.MiniIcon.rectTransform.anchoredPosition = ToMap(p.Target.position, _mini.ppm);
                p.MiniIcon.rectTransform.sizeDelta = Vector2.one * (11f * pulse);
                p.MiniIcon.color = col;
                p.FullIcon.rectTransform.anchoredPosition = ToMap(p.Target.position, _full.ppm);
                p.FullIcon.rectTransform.sizeDelta = Vector2.one * (13f * pulse);
                p.FullIcon.color = col;
            }
            foreach (var k in _scratch) _pings.Remove(k);
        }

        // ------------------------------------------------------------------------------------------------ info panel
        private string GridRef(Vector3 w)
        {
            int cells = Mathf.Max(1, Mathf.RoundToInt(_mapSize / 200f));
            int col = Mathf.Clamp(Mathf.FloorToInt((w.x - _mapOrigin.x) / 200f), 0, cells - 1);
            int row = Mathf.Clamp(Mathf.FloorToInt((_mapOrigin.y + _mapSize - w.z) / 200f), 0, cells - 1);
            return ((char)('A' + col)).ToString() + (row + 1);
        }

        private void UpdateInfo(bool hasZone, Vector3 pp, Vector3 zc, float zr, Vector3 tc, float tr, bool outside)
        {
            _tGrid.text = _player != null ? "GRID  " + GridRef(pp) : "GRID  --";
            _tCoords.text = _player != null ? $"X {pp.x:0}   Z {pp.z:0}   ALT {pp.y:0} m" : "";

            int hostile = 0;
            foreach (var kv in _pings) if (kv.Value.Target != null && kv.Value.End > Time.time) hostile++;
            _tHostiles.text = hostile > 0 ? $"HOSTILES PINGED  {hostile}" : "NO HOSTILES DETECTED";

            var zm = BRZoneManager.Instance;
            if (!hasZone || zm == null)
            {
                _tPhase.text = "NO ACTIVE ZONE";
                _tZoneState.text = "";
                _tTimer.text = "--:--";
                _tDist.text = "";
                _tRadius.text = "";
                return;
            }

            _tPhase.text = zm.CurrentPhaseName.ToUpperInvariant();
            _tZoneState.text = zm.IsShrinking ? "ZONE CLOSING" : "NEXT ZONE IN";
            int secs = Mathf.Max(0, Mathf.CeilToInt(zm.TimeRemaining));
            _tTimer.text = $"{secs / 60:00}:{secs % 60:00}";
            _tRadius.text = $"RADIUS  {zr:0} m  >  {tr:0} m";

            if (_player != null)
            {
                float d = Vector2.Distance(new Vector2(pp.x, pp.z), new Vector2(zc.x, zc.z)) - zr;
                if (outside) { _tDist.text = $"OUTSIDE ZONE  {Mathf.Abs(d):0} m"; _tDist.color = Danger; }
                else { _tDist.text = $"INSIDE ZONE  {Mathf.Abs(d):0} m TO EDGE"; _tDist.color = White; }
            }
        }

        // ------------------------------------------------------------------------------------------------ ui helpers
        private static RectTransform NewRect(string name, Transform parent)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.gameObject.layer = parent.gameObject.layer;     // UI layer, so camera-space canvases render it too
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite; img.color = color; img.raycastTarget = false;
            return img;
        }

        private static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, TextAlignmentOptions align, Color col, bool bold)
        {
            var rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.alignment = align; t.color = col;
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            return t;
        }

        private static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
        }

        /// <summary>Thin rectangular outline (four strips) around <paramref name="parent"/>, or around <paramref name="around"/>'s rect.</summary>
        private static void AddBorder(RectTransform parent, float thickness, Color col, RectTransform around = null, float outset = 0f)
        {
            Vector2 size = around != null ? around.rect.size : parent.rect.size;
            if (around != null) size = around.sizeDelta;
            if (size.x < 1f) size = parent.sizeDelta;
            Vector2 center = around != null ? around.anchoredPosition : Vector2.zero;
            float w = size.x + outset * 2f, h = size.y + outset * 2f;
            MakeStrip(parent, "BT", center + new Vector2(0f, h * 0.5f - thickness * 0.5f), new Vector2(w, thickness), col);
            MakeStrip(parent, "BB", center + new Vector2(0f, -h * 0.5f + thickness * 0.5f), new Vector2(w, thickness), col);
            MakeStrip(parent, "BL", center + new Vector2(-w * 0.5f + thickness * 0.5f, 0f), new Vector2(thickness, h), col);
            MakeStrip(parent, "BR", center + new Vector2(w * 0.5f - thickness * 0.5f, 0f), new Vector2(thickness, h), col);
        }

        private static void MakeStrip(RectTransform parent, string name, Vector2 pos, Vector2 size, Color col)
        {
            var img = NewImage(name, parent, null, col);
            img.rectTransform.anchoredPosition = pos;
            img.rectTransform.sizeDelta = size;
        }

        // ------------------------------------------------------------------------------------------------ procedural sprites
        private static Sprite GetDot()
        {
            if (_dot != null) return _dot;
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "MapDot" };
            var px = new Color32[n * n];
            float c = (n - 1) * 0.5f, r = n * 0.5f - 1.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(r - d + 0.5f) * 255f));
                }
            tex.SetPixels32(px); tex.Apply(true);
            _dot = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            return _dot;
        }

        private static Sprite GetArrow()
        {
            if (_arrow != null) return _arrow;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "MapArrow" };
            var poly = new Vector2[] { new Vector2(32f, 60f), new Vector2(55f, 6f), new Vector2(32f, 19f), new Vector2(9f, 6f) };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int hit = 0;
                    for (int sy = 0; sy < 4; sy++)
                        for (int sx = 0; sx < 4; sx++)
                            if (Inside(poly, x + (sx + 0.5f) / 4f, y + (sy + 0.5f) / 4f)) hit++;
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(hit * 255 / 16));
                }
            tex.SetPixels32(px); tex.Apply(true);
            _arrow = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            return _arrow;
        }

        private static bool Inside(Vector2[] poly, float x, float y)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if (((poly[i].y > y) != (poly[j].y > y)) &&
                    (x < (poly[j].x - poly[i].x) * (y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x))
                    inside = !inside;
            }
            return inside;
        }
    }
}
