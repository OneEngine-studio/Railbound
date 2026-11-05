using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Profiling;
using TMPro;

namespace Michsky.UI.Reach
{
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

        private float totalVramMB;
        private float vramScaleTarget = 0.8f;
        private float updateInterval = 0.5f;
        private float nextUpdateTime = 0f;
        private static Sprite segmentedSprite;

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
            UpdateVRAMStats();
        }

        void Update()
        {
            if (Time.unscaledTime >= nextUpdateTime)
            {
                nextUpdateTime = Time.unscaledTime + updateInterval;
                UpdateVRAMStats();
            }
        }

        public void EnsureSegmentedSprite()
        {
            if (segmentedSprite == null)
            {
                int w = 600;
                int h = 18;
                Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;

                for (int x = 0; x < w; x++)
                {
                    bool isBar = (x % 6) < 4; // 4px solid vertical tick, 2px gap (100 distinct 1% ticks)
                    Color col = isBar ? Color.white : Color.clear;
                    for (int y = 0; y < h; y++)
                    {
                        tex.SetPixel(x, y, col);
                    }
                }
                tex.Apply();
                segmentedSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0f, 0.5f));
            }

            if (barBackground != null && barBackground.sprite != segmentedSprite)
            {
                barBackground.sprite = segmentedSprite;
                barBackground.type = Image.Type.Simple;
            }

            if (otherAppsFillBar != null && otherAppsFillBar.sprite != segmentedSprite)
            {
                otherAppsFillBar.sprite = segmentedSprite;
                otherAppsFillBar.type = Image.Type.Filled;
                otherAppsFillBar.fillMethod = Image.FillMethod.Horizontal;
            }

            if (gameUsageFillBar != null && gameUsageFillBar.sprite != segmentedSprite)
            {
                gameUsageFillBar.sprite = segmentedSprite;
                gameUsageFillBar.type = Image.Type.Filled;
                gameUsageFillBar.fillMethod = Image.FillMethod.Horizontal;
            }
        }

        public void SetTargetScale(float target01)
        {
            vramScaleTarget = Mathf.Clamp(target01, 0.5f, 1.0f);
            PlayerPrefs.SetFloat("Graphics_VRAMTarget", vramScaleTarget);
            UpdateVRAMStats();
        }

        private void LoadVRAMTarget()
        {
            if (PlayerPrefs.HasKey("Graphics_VRAMTarget"))
            {
                vramScaleTarget = PlayerPrefs.GetFloat("Graphics_VRAMTarget");
            }
            else
            {
                vramScaleTarget = 0.8f;
            }
        }

        public void UpdateVRAMStats()
        {
            if (totalVramMB <= 0) totalVramMB = Mathf.Max(512f, SystemInfo.graphicsMemorySize);
            if (vramScaleTarget <= 0) LoadVRAMTarget();
            EnsureSegmentedSprite();

            // Calculate active game VRAM (textures + driver allocation)
            float textureMB = (float)(Texture.currentTextureMemory / (1024.0 * 1024.0));
            float driverMB = (float)(Profiler.GetAllocatedMemoryForGraphicsDriver() / (1024.0 * 1024.0));
            float gameMB = Mathf.Max(textureMB, driverMB);

            // Other apps estimation (OS, DWM, background desktop apps)
            float otherAppsMB = Mathf.Clamp(totalVramMB * 0.08f, 350f, 1400f);
            float totalUsedMB = gameMB + otherAppsMB;

            float gameRatio = Mathf.Clamp01(gameMB / totalVramMB);
            float totalRatio = Mathf.Clamp01(totalUsedMB / totalVramMB);

            // Update filled bars
            if (gameUsageFillBar != null)
            {
                gameUsageFillBar.fillAmount = gameRatio;
                gameUsageFillBar.color = gameColor;
            }

            if (otherAppsFillBar != null)
            {
                otherAppsFillBar.fillAmount = totalRatio;
                otherAppsFillBar.color = otherAppsColor;
            }

            if (barBackground != null)
            {
                barBackground.color = darkTrackColor;
            }

            // Update Target & Cap positions
            float barWidth = 570f;
            if (barBackground != null)
            {
                barWidth = barBackground.rectTransform.rect.width;
                if (barWidth <= 0) barWidth = 570f;
            }

            float targetX = barWidth * vramScaleTarget;

            if (targetLine != null)
            {
                targetLine.anchoredPosition = new Vector2(targetX, targetLine.anchoredPosition.y);
            }

            if (targetLabel != null)
            {
                targetLabel.anchoredPosition = new Vector2(targetX, targetLabel.anchoredPosition.y);
            }

            if (rightCapLine != null)
            {
                rightCapLine.anchoredPosition = new Vector2(barWidth, rightCapLine.anchoredPosition.y);
            }

            // Text outputs in GB (Clean modern tactical telemetry)
            float gameGB = gameMB / 1024f;
            float otherAppsGB = otherAppsMB / 1024f;
            float totalUsedGB = totalUsedMB / 1024f;
            float totalVramGB = totalVramMB / 1024f;

            string title = string.IsNullOrEmpty(gameTitle) ? "GAME" : gameTitle.ToUpper();
            if (title.Contains("MODERN") || title.Contains("WARFARE")) title = "GAME";
            string gameHex = ColorUtility.ToHtmlStringRGB(gameColor);
            string otherHex = ColorUtility.ToHtmlStringRGB(otherAppsColor);

            if (gameLegendText != null)
            {
                gameLegendText.text = $"<color=#{gameHex}>■■■</color> <color=#FFFFFF>{title}:</color> <color=#{gameHex}>{gameGB:F2} GB</color>";
            }

            if (otherAppsLegendText != null)
            {
                otherAppsLegendText.text = $"<color=#{otherHex}>■■■</color> <color=#FFFFFF>OTHER APPS:</color> <color=#{otherHex}>{otherAppsGB:F2} GB</color>";
            }

            if (totalUsageText != null)
            {
                totalUsageText.text = $"<color=#FFFFFF>{totalUsedGB:F2}</color><color=#9A9A9A> /{totalVramGB:F2} GB</color>";
            }
        }
    }
}
