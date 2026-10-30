using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using JUTPS;
using JUTPS.UI;
using Railbound.Gameplay;

namespace Railbound.UI
{
    /// <summary>
    /// Displays player health as a 7-slot segmented bar alongside the medkit counter (+ 2:),
    /// matching the tactical HUD design from the user's reference image.
    /// Replaces the legacy continuous health slider in JUTPS User Interface.
    /// </summary>
    [ExecuteAlways]
    public class SegmentedHealthBarUI : MonoBehaviour
    {
        public const int TOTAL_SLOTS = 7;

        [Header("Target References")]
        [SerializeField] private JUHealth healthComponent;
        [SerializeField] private PlayerMedkitSystem medkitSystem;

        [Header("UI References - Medkit")]
        [SerializeField] private Image medkitIconImage;
        [SerializeField] private Text medkitCountText;
        [SerializeField] private TextMeshProUGUI medkitCountTMP;

        [Header("UI References - 7 Health Slots")]
        [SerializeField] private List<Image> slotBackgroundImages = new List<Image>();
        [SerializeField] private List<Image> slotFillImages = new List<Image>();

        [Header("Color Styling (Reference Image)")]
        [Tooltip("Color of full/healthy slots (warm light cream matching reference).")]
        [SerializeField] private Color healthyFillColor = new Color(0.92f, 0.90f, 0.82f, 1f); // #EBE6D1
        [Tooltip("Color of slot outlines/borders.")]
        [SerializeField] private Color slotBorderColor = new Color(0.78f, 0.74f, 0.65f, 0.9f);  // #C7BDA6
        [Tooltip("Background of empty slots.")]
        [SerializeField] private Color slotEmptyColor = new Color(0.12f, 0.12f, 0.14f, 0.65f);
        [Tooltip("Color when health is critical (<= 2 slots).")]
        [SerializeField] private Color criticalHealthColor = new Color(1.0f, 0.28f, 0.28f, 1f); // #FF4747
        [Tooltip("Flash color during active healing.")]
        [SerializeField] private Color healingPulseColor = new Color(0.3f, 1.0f, 0.5f, 1f);   // #4DFF80

        [Header("Animation Settings")]
        [SerializeField] private float fillLerpSpeed = 12f;
        [SerializeField] private bool enableLowHealthPulse = true;

        private float[] _currentSlotFills = new float[TOTAL_SLOTS];
        private float[] _targetSlotFills = new float[TOTAL_SLOTS];
        private float _healingFlashTimer = 0f;
        private int _cachedMedkits = -1;

        private void Awake()
        {
            FindReferences();
        }

        private void OnEnable()
        {
            FindReferences();
            if (medkitSystem != null)
            {
                medkitSystem.OnMedkitsChanged += HandleMedkitsChanged;
                medkitSystem.OnHealed += HandlePlayerHealed;
            }
            UpdateMedkitDisplay();
        }

        private void OnDisable()
        {
            if (medkitSystem != null)
            {
                medkitSystem.OnMedkitsChanged -= HandleMedkitsChanged;
                medkitSystem.OnHealed -= HandlePlayerHealed;
            }
        }

        private void Start()
        {
            FindReferences();
            UpdateMedkitDisplay();
            SnapHealthToCurrent();
        }

        private void Update()
        {
            if (healthComponent == null)
            {
                FindReferences();
                if (healthComponent == null) return;
            }

            if (_healingFlashTimer > 0f)
            {
                _healingFlashTimer -= Time.deltaTime;
            }

            UpdateHealthSlots(Time.deltaTime);
        }

        public void FindReferences()
        {
            if (healthComponent == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    healthComponent = player.GetComponent<JUHealth>();
                    medkitSystem = player.GetComponent<PlayerMedkitSystem>();
                }
            }

            if (medkitSystem == null && healthComponent != null)
            {
                medkitSystem = healthComponent.GetComponent<PlayerMedkitSystem>() ?? PlayerMedkitSystem.Instance;
            }
        }

        private void HandleMedkitsChanged(int newCount)
        {
            UpdateMedkitDisplay();
        }

        private void HandlePlayerHealed(float amount)
        {
            _healingFlashTimer = 0.8f;
        }

        public void UpdateMedkitDisplay()
        {
            int count = medkitSystem != null ? medkitSystem.CurrentMedkits : 2;
            _cachedMedkits = count;

            string textStr = $"{count}:";

            if (medkitCountText != null)
            {
                medkitCountText.text = textStr;
                medkitCountText.color = count > 0 ? healthyFillColor : new Color(0.6f, 0.25f, 0.25f, 0.8f);
            }

            if (medkitCountTMP != null)
            {
                medkitCountTMP.text = textStr;
                medkitCountTMP.color = count > 0 ? healthyFillColor : new Color(0.6f, 0.25f, 0.25f, 0.8f);
            }

            if (medkitIconImage != null)
            {
                medkitIconImage.color = count > 0 ? healthyFillColor : new Color(0.6f, 0.25f, 0.25f, 0.8f);
            }
        }

        public void SnapHealthToCurrent()
        {
            if (healthComponent == null) return;

            float norm = Mathf.Clamp01(healthComponent.Health / Mathf.Max(1f, healthComponent.MaxHealth));
            for (int i = 0; i < TOTAL_SLOTS; i++)
            {
                float slotStart = (float)i / TOTAL_SLOTS;
                float slotEnd = (float)(i + 1) / TOTAL_SLOTS;
                float slotFill = Mathf.Clamp01((norm - slotStart) / (slotEnd - slotStart));

                _targetSlotFills[i] = slotFill;
                _currentSlotFills[i] = slotFill;

                if (i < slotFillImages.Count && slotFillImages[i] != null)
                {
                    slotFillImages[i].fillAmount = slotFill;
                }
            }
        }

        private void UpdateHealthSlots(float dt)
        {
            float norm = Mathf.Clamp01(healthComponent.Health / Mathf.Max(1f, healthComponent.MaxHealth));
            bool isCritical = norm <= (2f / TOTAL_SLOTS);
            float pulse = 1f;

            if (isCritical && enableLowHealthPulse && Application.isPlaying)
            {
                pulse = 0.7f + 0.3f * Mathf.Sin(Time.time * 8f);
            }

            Color baseColor = isCritical ? criticalHealthColor * pulse : healthyFillColor;
            if (_healingFlashTimer > 0f)
            {
                float t = _healingFlashTimer / 0.8f;
                baseColor = Color.Lerp(baseColor, healingPulseColor, t);
            }

            for (int i = 0; i < TOTAL_SLOTS; i++)
            {
                float slotStart = (float)i / TOTAL_SLOTS;
                float slotEnd = (float)(i + 1) / TOTAL_SLOTS;
                float targetFill = Mathf.Clamp01((norm - slotStart) / (slotEnd - slotStart));
                _targetSlotFills[i] = targetFill;

                if (Application.isPlaying)
                {
                    _currentSlotFills[i] = Mathf.MoveTowards(_currentSlotFills[i], targetFill, fillLerpSpeed * dt);
                }
                else
                {
                    _currentSlotFills[i] = targetFill;
                }

                if (i < slotFillImages.Count && slotFillImages[i] != null)
                {
                    slotFillImages[i].fillAmount = _currentSlotFills[i];
                    slotFillImages[i].color = baseColor;
                }

                if (i < slotBackgroundImages.Count && slotBackgroundImages[i] != null)
                {
                    slotBackgroundImages[i].color = isCritical ? new Color(0.35f, 0.1f, 0.1f, 0.7f) : slotBorderColor;
                }
            }
        }

        #region Automatic Procedural Hierarchy Generator
        /// <summary>
        /// Automatically constructs the exact 7-slot health bar hierarchy in the canvas
        /// matching the user's reference image (+ 2: [ ][ ][ ][ ][ ][ ][ ]).
        /// </summary>
        [ContextMenu("Build Segmented Health Bar UI")]
        public void BuildUIHierarchy()
        {
            // Clear existing children
            while (transform.childCount > 0)
            {
                DestroyImmediate(transform.GetChild(0).gameObject);
            }

            slotBackgroundImages.Clear();
            slotFillImages.Clear();

            var rectTransform = GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.sizeDelta = new Vector2(340f, 32f);
            }

            // Create main horizontal container
            var horizontalObj = new GameObject("HealthAndMedkit_Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            horizontalObj.transform.SetParent(transform, false);

            var rowRect = horizontalObj.GetComponent<RectTransform>();
            rowRect.anchorMin = Vector2.zero;
            rowRect.anchorMax = Vector2.one;
            rowRect.sizeDelta = Vector2.zero;
            rowRect.anchoredPosition = Vector2.zero;

            var hLayout = horizontalObj.GetComponent<HorizontalLayoutGroup>();
            hLayout.childAlignment = TextAnchor.MiddleLeft;
            hLayout.spacing = 8f;
            hLayout.childControlWidth = false;
            hLayout.childControlHeight = false;
            hLayout.childForceExpandWidth = false;
            hLayout.childForceExpandHeight = false;

            // Generate Crisp Procedural Sprites
            var circleCrossSprite = CreateCircleCrossSprite();
            var slotBorderSprite = CreateSlotBorderSprite();
            var slotInnerSprite = CreateSolidSprite();

            // 1. Medkit Group (Icon + Count Text)
            var medkitGroupObj = new GameObject("Medkit_Group", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            medkitGroupObj.transform.SetParent(horizontalObj.transform, false);
            var medkitRect = medkitGroupObj.GetComponent<RectTransform>();
            medkitRect.sizeDelta = new Vector2(58f, 26f);

            var medkitLayout = medkitGroupObj.GetComponent<HorizontalLayoutGroup>();
            medkitLayout.childAlignment = TextAnchor.MiddleLeft;
            medkitLayout.spacing = 4f;
            medkitLayout.childControlWidth = false;
            medkitLayout.childControlHeight = false;
            medkitLayout.childForceExpandWidth = false;
            medkitLayout.childForceExpandHeight = false;

            // 1a. Circular Medkit (+) Icon
            var iconObj = new GameObject("Icon_PlusCircle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObj.transform.SetParent(medkitGroupObj.transform, false);
            var iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(22f, 22f);
            medkitIconImage = iconObj.GetComponent<Image>();
            medkitIconImage.sprite = circleCrossSprite;
            medkitIconImage.color = healthyFillColor;

            // 1b. Medkit Count Text (e.g. "2:")
            var textObj = new GameObject("Text_Count", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            textObj.transform.SetParent(medkitGroupObj.transform, false);
            var textRect = textObj.GetComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(28f, 24f);

            medkitCountText = textObj.GetComponent<Text>();
            try
            {
                medkitCountText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            catch { }
            if (medkitCountText.font == null)
            {
                medkitCountText.font = Resources.FindObjectsOfTypeAll<Font>().Length > 0 ? Resources.FindObjectsOfTypeAll<Font>()[0] : null;
            }
            medkitCountText.text = "2:";
            medkitCountText.fontSize = 18;
            medkitCountText.fontStyle = FontStyle.Bold;
            medkitCountText.alignment = TextAnchor.MiddleLeft;
            medkitCountText.color = healthyFillColor;

            var outline = textObj.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.7f);
            outline.effectDistance = new Vector2(1f, -1f);

            // 2. Health Slots Group (7 horizontal segments)
            var slotsGroupObj = new GameObject("Slots_Group", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            slotsGroupObj.transform.SetParent(horizontalObj.transform, false);
            var slotsRect = slotsGroupObj.GetComponent<RectTransform>();
            slotsRect.sizeDelta = new Vector2(270f, 22f);

            var slotsLayout = slotsGroupObj.GetComponent<HorizontalLayoutGroup>();
            slotsLayout.childAlignment = TextAnchor.MiddleLeft;
            slotsLayout.spacing = 3f;
            slotsLayout.childControlWidth = false;
            slotsLayout.childControlHeight = false;
            slotsLayout.childForceExpandWidth = false;
            slotsLayout.childForceExpandHeight = false;

            // Build 7 slots
            for (int i = 0; i < TOTAL_SLOTS; i++)
            {
                var slotObj = new GameObject($"Slot_{i + 1}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                slotObj.transform.SetParent(slotsGroupObj.transform, false);
                var slotRect = slotObj.GetComponent<RectTransform>();
                slotRect.sizeDelta = new Vector2(34f, 18f);

                // Slot border / background
                var bgImage = slotObj.GetComponent<Image>();
                bgImage.sprite = slotBorderSprite;
                bgImage.type = Image.Type.Sliced;
                bgImage.color = slotBorderColor;
                slotBackgroundImages.Add(bgImage);

                // Slot inner fill
                var fillObj = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                fillObj.transform.SetParent(slotObj.transform, false);
                var fillRect = fillObj.GetComponent<RectTransform>();
                fillRect.anchorMin = new Vector2(0.08f, 0.12f);
                fillRect.anchorMax = new Vector2(0.92f, 0.88f);
                fillRect.offsetMin = Vector2.zero;
                fillRect.offsetMax = Vector2.zero;

                var fillImage = fillObj.GetComponent<Image>();
                fillImage.sprite = slotInnerSprite;
                fillImage.type = Image.Type.Filled;
                fillImage.fillMethod = Image.FillMethod.Horizontal;
                fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
                fillImage.fillAmount = 1f;
                fillImage.color = healthyFillColor;
                slotFillImages.Add(fillImage);
            }

            // Disable legacy UIHealhBar on the same object if present
            var oldHealthBar = GetComponent<UIHealhBar>();
            if (oldHealthBar != null)
            {
                oldHealthBar.enabled = false;
            }

            UpdateMedkitDisplay();
            SnapHealthToCurrent();
        }

        private static Sprite CreateCircleCrossSprite()
        {
            int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            float center = size / 2f;
            float outerR = size * 0.46f;
            float innerR = size * 0.38f;
            float crossThickness = 4.5f;
            float crossLength = size * 0.28f;

            Color white = Color.white;
            Color clear = new Color(1f, 1f, 1f, 0f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    // Circular ring outline
                    bool inRing = dist <= outerR && dist >= innerR;

                    // Plus cross symbol inside
                    bool inCrossH = Mathf.Abs(dy) <= crossThickness && Mathf.Abs(dx) <= crossLength;
                    bool inCrossV = Mathf.Abs(dx) <= crossThickness && Mathf.Abs(dy) <= crossLength;

                    if (inRing || inCrossH || inCrossV)
                    {
                        tex.SetPixel(x, y, white);
                    }
                    else
                    {
                        tex.SetPixel(x, y, clear);
                    }
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private static Sprite CreateSlotBorderSprite()
        {
            int w = 64;
            int h = 32;
            int border = 4;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;

            Color white = Color.white;
            Color bg = new Color(0.12f, 0.12f, 0.15f, 0.7f);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool isBorder = x < border || x >= (w - border) || y < border || y >= (h - border);
                    tex.SetPixel(x, y, isBorder ? white : bg);
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        private static Sprite CreateSolidSprite()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            Color[] colors = new Color[16];
            for (int i = 0; i < 16; i++) colors[i] = Color.white;
            tex.SetPixels(colors);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        }
        #endregion
    }
}
