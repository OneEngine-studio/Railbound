using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Profiling;
using TMPro;

namespace Michsky.UI.Reach
{
    /// <summary>
    /// GPU VRAM telemetry widget: a segmented bar (game / other apps / unfilled track), a target marker and a legend.
    /// The bars and numbers ease toward the latest sample every frame (unscaled time, so it also animates while
    /// the game is paused) and the segment texture is anti-aliased, so nothing jumps or shimmers.
    /// </summary>
    public class HardwareVRAMMonitor : MonoBehaviour
    {
        [Header("Game Title")]
        public string gameTitle = "GAME";

        [Header("Bar References")]
        public Image barBackground;      // Unfilled dark track (segmented)
        public Image otherAppsFillBar;   // Light grey fill (segmented)
        public Image gameUsageFillBar;   // Lime green fill (segmented)
        public RectTransform targetLine; // Vertical white tick at target
        public RectTransform targetLabel;// "TARGET" label above target line
        public RectTransform rightCapLine; // Vertical white tick at right end

        [Header("Text References")]
        public TextMeshProUGUI estimatedTitleText;
        public TextMeshProUGUI gameLegendText;
        public TextMeshProUGUI otherAppsLegendText;
        public TextMeshProUGUI totalUsageText;

        [Header("Telemetry Colors")]
        public Color gameColor = new Color(0.48f, 0.92f, 0.22f, 1f);       // #7BEB38 Tactical Lime Green
        public Color otherAppsColor = new Color(0.85f, 0.85f, 0.85f, 1f);   // #D9D9D9 Neutral Silver
        public Color darkTrackColor = new Color(0.25f, 0.25f, 0.25f, 0.95f);// #404040 Visible Unfilled Track
        public Color targetLineColor = Color.white;

        [Header("Smoothing")]
        [Tooltip("Seconds the bars/numbers take to settle on a new sample (lower = snappier).")]
        [Range(0.05f, 1f)] public float smoothTime = 0.35f;

        private const int SegmentCount = 60;   // segments across the whole bar
        private const int CellPx = 20;         // texture pixels per segment (incl. gap)

        private float totalVramMB;
        private float vramScaleTarget = 0.8f;
        private float updateInterval = 0.5f;
        private float nextUpdateTime = 0f;
        private static Sprite segmentedSprite;

        // sampled (target) values
        private float gameMB, otherAppsMB;
        // displayed (smoothed) values
        private float dispGameMB, dispOtherMB, dispTarget = -1f;
        private float velGame, velOther, velTarget;
        private bool hasSample;
        private int lastTextKey = int.MinValue;

        void Awake()
        {
            totalVramMB = Mathf.Max(512f, SystemInfo.graphicsMemorySize);
            LoadVRAMTarget();
            EnsureSegmentedSprite();
        }

        void OnEnable()
        {
            LoadVRAMTarget();
            EnsureSegmentedSprite();
            Sample();
            // start from the real value (no sweep from zero every time the menu opens)
            dispGameMB = gameMB; dispOtherMB = otherAppsMB; dispTarget = vramScaleTarget;
            velGame = velOther = velTarget = 0f;
            lastTextKey = int.MinValue;
            ApplyVisuals(true);
        }

        void Update()
        {
            if (Time.unscaledTime >= nextUpdateTime)
            {
                nextUpdateTime = Time.unscaledTime + updateInterval;
                Sample();
            }

            float dt = Time.unscaledDeltaTime;
            dispGameMB = Mathf.SmoothDamp(dispGameMB, gameMB, ref velGame, smoothTime, Mathf.Infinity, dt);
            dispOtherMB = Mathf.SmoothDamp(dispOtherMB, otherAppsMB, ref velOther, smoothTime, Mathf.Infinity, dt);
            dispTarget = Mathf.SmoothDamp(dispTarget, vramScaleTarget, ref velTarget, smoothTime * 0.6f, Mathf.Infinity, dt);
            ApplyVisuals(false);
        }

        /// <summary>Anti-aliased rounded segments; bilinear + mips so it stays clean at any UI scale.</summary>
        public void EnsureSegmentedSprite()
        {
            if (segmentedSprite == null)
            {
                int w = SegmentCount * CellPx;
                int h = 32;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
                tex.filterMode = FilterMode.Trilinear;
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.anisoLevel = 0;
                tex.name = "VRAM Segments";

                var px = new Color32[w * h];
                float segW = CellPx - 5f;         // bar width inside a cell (gap = 5px)
                float segH = h - 4f;
                float radius = 3.5f;
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        float cx = (x % CellPx) + 0.5f - CellPx * 0.5f;
                        float cy = y + 0.5f - h * 0.5f;
                        // signed distance to a rounded rectangle, 1px soft edge
                        float dx = Mathf.Abs(cx) - (segW * 0.5f - radius);
                        float dy = Mathf.Abs(cy) - (segH * 0.5f - radius);
                        float outside = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f));
                        float inside = Mathf.Min(Mathf.Max(dx, dy), 0f);
                        float d = outside + inside - radius;
                        byte a = (byte)(Mathf.Clamp01(0.5f - d) * 255f);
                        px[y * w + x] = new Color32(255, 255, 255, a);
                    }
                }
                tex.SetPixels32(px);
                tex.Apply(true, true);
                segmentedSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            }

            SetupBar(barBackground, false);
            SetupBar(otherAppsFillBar, true);
            SetupBar(gameUsageFillBar, true);
        }

        private void SetupBar(Image img, bool filled)
        {
            if (img == null) return;
            if (img.sprite != segmentedSprite) img.sprite = segmentedSprite;
            if (filled)
            {
                img.type = Image.Type.Filled;
                img.fillMethod = Image.FillMethod.Horizontal;
                img.fillOrigin = 0;
            }
            else img.type = Image.Type.Simple;
            img.raycastTarget = false;
        }

        public void SetTargetScale(float target01)
        {
            vramScaleTarget = Mathf.Clamp(target01, 0.5f, 1.0f);
            PlayerPrefs.SetFloat("Graphics_VRAMTarget", vramScaleTarget);
            Sample();
        }

        private void LoadVRAMTarget()
        {
            vramScaleTarget = PlayerPrefs.HasKey("Graphics_VRAMTarget") ? PlayerPrefs.GetFloat("Graphics_VRAMTarget") : 0.8f;
        }

        /// <summary>Re-reads the real numbers; the visuals ease toward them.</summary>
        public void UpdateVRAMStats()
        {
            Sample();
            ApplyVisuals(false);
        }

        private void Sample()
        {
            if (totalVramMB <= 0) totalVramMB = Mathf.Max(512f, SystemInfo.graphicsMemorySize);
            if (vramScaleTarget <= 0) LoadVRAMTarget();

            // Active game VRAM (textures + driver allocation)
            float textureMB = (float)(Texture.currentTextureMemory / (1024.0 * 1024.0));
            float driverMB = (float)(Profiler.GetAllocatedMemoryForGraphicsDriver() / (1024.0 * 1024.0));
            gameMB = Mathf.Max(textureMB, driverMB);

            // Other apps estimation (OS, DWM, background desktop apps)
            otherAppsMB = Mathf.Clamp(totalVramMB * 0.08f, 350f, 1400f);

            if (!hasSample)
            {
                hasSample = true;
                dispGameMB = gameMB; dispOtherMB = otherAppsMB; dispTarget = vramScaleTarget;
            }
        }

        private void ApplyVisuals(bool force)
        {
            if (totalVramMB <= 0) return;
            if (dispTarget < 0f) dispTarget = vramScaleTarget;

            float gameRatio = Mathf.Clamp01(dispGameMB / totalVramMB);
            float totalRatio = Mathf.Clamp01((dispGameMB + dispOtherMB) / totalVramMB);

            if (gameUsageFillBar != null) { gameUsageFillBar.fillAmount = gameRatio; gameUsageFillBar.color = gameColor; }
            if (otherAppsFillBar != null) { otherAppsFillBar.fillAmount = totalRatio; otherAppsFillBar.color = otherAppsColor; }
            if (barBackground != null) barBackground.color = darkTrackColor;

            // Target / cap markers follow the real bar width (anchors are at the left edge of the bar container)
            float barWidth = 570f;
            if (barBackground != null)
            {
                barWidth = barBackground.rectTransform.rect.width;
                if (barWidth <= 0) barWidth = 570f;
            }
            float targetX = barWidth * dispTarget;
            if (targetLine != null) targetLine.anchoredPosition = new Vector2(targetX, targetLine.anchoredPosition.y);
            if (targetLabel != null) targetLabel.anchoredPosition = new Vector2(targetX, targetLabel.anchoredPosition.y);
            if (rightCapLine != null) rightCapLine.anchoredPosition = new Vector2(barWidth, rightCapLine.anchoredPosition.y);

            // Text only re-built when the rounded numbers actually change (no per-frame garbage)
            float gameGB = dispGameMB / 1024f;
            float otherGB = dispOtherMB / 1024f;
            float totalGB = (dispGameMB + dispOtherMB) / 1024f;
            float vramGB = totalVramMB / 1024f;
            int key = Mathf.RoundToInt(gameGB * 100f) * 1000003 ^ Mathf.RoundToInt(otherGB * 100f) * 7919 ^ Mathf.RoundToInt(totalGB * 100f) * 31 ^ Mathf.RoundToInt(vramGB * 100f);
            if (!force && key == lastTextKey) return;
            lastTextKey = key;

            string title = string.IsNullOrEmpty(gameTitle) ? "GAME" : gameTitle.ToUpper();
            if (title.Contains("MODERN") || title.Contains("WARFARE")) title = "GAME";
            string gameHex = ColorUtility.ToHtmlStringRGB(gameColor);
            string otherHex = ColorUtility.ToHtmlStringRGB(otherAppsColor);

            if (gameLegendText != null)
                gameLegendText.text = $"<color=#{gameHex}>■</color>  <color=#FFFFFF>{title}</color>  <color=#{gameHex}>{gameGB:F2} GB</color>";
            if (otherAppsLegendText != null)
                otherAppsLegendText.text = $"<color=#{otherHex}>■</color>  <color=#FFFFFF>OTHER APPS</color>  <color=#{otherHex}>{otherGB:F2} GB</color>";
            if (totalUsageText != null)
                totalUsageText.text = $"<color=#FFFFFF>{totalGB:F2}</color><color=#9A9A9A> / {vramGB:F2} GB</color>";
        }
    }
}
