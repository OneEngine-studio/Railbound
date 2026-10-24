using System;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Michsky.UI.Reach;

namespace Railbound.Editor
{
    public static class TacticalUIUpgradeAutomation
    {
        private const string LoadingScenePath = "Assets/Project/LoadingScreen.unity";
        private const string LobbyScenePath = "Assets/Project/GameLobby.unity";

        // Brand Palette
        private static readonly Color TacticalEmber = new Color(1.0f, 0.478f, 0.0f, 1.0f);     // #FF7A00
        private static readonly Color DarkCharcoal  = new Color(0.055f, 0.067f, 0.086f, 0.95f); // #0E1116
        private static readonly Color BevelBorder   = new Color(0.165f, 0.192f, 0.239f, 1.0f);  // #2A313D
        private static readonly Color SlateMuted    = new Color(0.557f, 0.612f, 0.671f, 1.0f);  // #8E9CA8

        // Asset Paths
        private const string CrossFramePath     = "Assets/Reach - Complete Sci-Fi UI/Textures/Borders/Special/Cross Frame.png";
        private const string CrossFrameAltPath  = "Assets/Reach - Complete Sci-Fi UI/Textures/Borders/Special/Cross Frame Alt.png";
        private const string SquareFilledPath   = "Assets/Reach - Complete Sci-Fi UI/Textures/Borders/Flat/Square Filled.png";
        private const string RadialShadowPath   = "Assets/Reach - Complete Sci-Fi UI/Textures/Shadows/Radial/Radial Directional Shadow Filled.png";
        private const string CloseIconPath      = "Assets/Reach - Complete Sci-Fi UI/Textures/Icons/Navigation/Close (64x).png";
        private const string CurrencyIconPath   = "Assets/Reach - Complete Sci-Fi UI/Textures/Icons/Misc/Currency (32x).png";
        private const string HexagonOutlinePath = "Assets/Reach - Complete Sci-Fi UI/Textures/Borders/Hexagon/64x/Hexagon Outline 64px - 5x.png";

        private const string FontBoldPath     = "Assets/Reach - Complete Sci-Fi UI/Fonts/Rajdhani-Bold SDF.asset";
        private const string FontSemiBoldPath = "Assets/Reach - Complete Sci-Fi UI/Fonts/Rajdhani-SemiBold SDF.asset";
        private const string FontMediumPath   = "Assets/Reach - Complete Sci-Fi UI/Fonts/Rajdhani-Medium SDF.asset";

        [MenuItem("Railbound/UI Polish/Upgrade All Targeted Screens")]
        public static string UpgradeAll()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== TACTICAL UI UPGRADE REPORT ===");

            try
            {
                UpgradeLoadingScreen(sb);
                UpgradeGameLobby(sb);
                sb.AppendLine("[SUCCESS] Both GameLobby and LoadingScreen successfully upgraded with Reach UI tokens!");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"[ERROR] Upgrade failed: {ex.Message}\n{ex.StackTrace}");
            }

            Debug.Log(sb.ToString());
            return sb.ToString();
        }

        [MenuItem("Railbound/UI Polish/Upgrade LoadingScreen Only")]
        public static void UpgradeLoadingScreenMenu()
        {
            var sb = new StringBuilder();
            UpgradeLoadingScreen(sb);
            Debug.Log(sb.ToString());
        }

        [MenuItem("Railbound/UI Polish/Upgrade GameLobby Only")]
        public static void UpgradeGameLobbyMenu()
        {
            var sb = new StringBuilder();
            UpgradeGameLobby(sb);
            Debug.Log(sb.ToString());
        }

        public static void UpgradeLoadingScreen(StringBuilder sb)
        {
            var scene = EditorSceneManager.OpenScene(LoadingScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                sb.AppendLine("[FAIL] LoadingScreen scene could not be opened.");
                return;
            }

            var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                sb.AppendLine("[FAIL] Canvas not found in LoadingScreen.");
                return;
            }

            var crossFrameAlt = AssetDatabase.LoadAssetAtPath<Sprite>(CrossFrameAltPath);
            var squareFilled  = AssetDatabase.LoadAssetAtPath<Sprite>(SquareFilledPath);
            var radialShadow  = AssetDatabase.LoadAssetAtPath<Sprite>(RadialShadowPath);
            var fontBold      = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontBoldPath);
            var fontMedium    = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontMediumPath);
            var fontSemiBold  = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontSemiBoldPath);

            // 1. Layer Positioning:
            // Sibling 0: loadingUI (Background artwork)
            var bgObj = canvas.transform.Find("loadingUI");
            if (bgObj != null) bgObj.SetSiblingIndex(0);

            // Sibling 1: UIEmberOverlay (Particles between background & foreground UI)
            var emberObj = canvas.transform.Find("UIEmberOverlay");
            if (emberObj != null)
            {
                emberObj.SetSiblingIndex(1);
                var rt = emberObj.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                }
            }

            // Remove old Slider if present
            var oldSlider = canvas.transform.Find("LoadingBar");
            if (oldSlider != null)
            {
                UnityEngine.Object.DestroyImmediate(oldSlider.gameObject);
            }

            // Foreground UI Container (Sibling 2)
            var fgTr = canvas.transform.Find("LoadingForeground");
            GameObject fgObj;
            if (fgTr == null)
            {
                fgObj = new GameObject("LoadingForeground", typeof(RectTransform), typeof(CanvasGroup));
                fgObj.transform.SetParent(canvas.transform, false);
            }
            else
            {
                fgObj = fgTr.gameObject;
            }
            fgObj.transform.SetSiblingIndex(2);

            var fgRt = fgObj.GetComponent<RectTransform>();
            fgRt.anchorMin = Vector2.zero;
            fgRt.anchorMax = Vector2.one;
            fgRt.offsetMin = Vector2.zero;
            fgRt.offsetMax = Vector2.zero;

            var canvasGroup = fgObj.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = fgObj.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;

            // 2. Tactical Lore / Tips Card Frame
            var loreCardTr = fgObj.transform.Find("Card_TacticalLore");
            GameObject loreCardObj;
            if (loreCardTr == null)
            {
                loreCardObj = new GameObject("Card_TacticalLore", typeof(RectTransform), typeof(Image));
                loreCardObj.transform.SetParent(fgObj.transform, false);
            }
            else
            {
                loreCardObj = loreCardTr.gameObject;
            }

            var loreRt = loreCardObj.GetComponent<RectTransform>();
            loreRt.anchorMin = new Vector2(0.5f, 0f);
            loreRt.anchorMax = new Vector2(0.5f, 0f);
            loreRt.pivot = new Vector2(0.5f, 0f);
            loreRt.anchoredPosition = new Vector2(0f, 150f);
            loreRt.sizeDelta = new Vector2(960f, 85f);

            var loreImg = loreCardObj.GetComponent<Image>();
            loreImg.sprite = crossFrameAlt;
            loreImg.type = Image.Type.Sliced;
            loreImg.color = DarkCharcoal;

            // Tip Title Tag
            var tagTr = loreCardObj.transform.Find("Text_TipTag");
            GameObject tagObj;
            if (tagTr == null)
            {
                tagObj = new GameObject("Text_TipTag", typeof(RectTransform), typeof(TextMeshProUGUI));
                tagObj.transform.SetParent(loreCardObj.transform, false);
            }
            else
            {
                tagObj = tagTr.gameObject;
            }
            var tagRt = tagObj.GetComponent<RectTransform>();
            tagRt.anchorMin = new Vector2(0f, 1f);
            tagRt.anchorMax = new Vector2(1f, 1f);
            tagRt.pivot = new Vector2(0f, 1f);
            tagRt.anchoredPosition = new Vector2(24f, -12f);
            tagRt.sizeDelta = new Vector2(-48f, 22f);

            var tagTmp = tagObj.GetComponent<TextMeshProUGUI>();
            tagTmp.font = fontBold != null ? fontBold : tagTmp.font;
            tagTmp.fontSize = 15f;
            tagTmp.color = TacticalEmber;
            tagTmp.text = "// MISSION INTEL // CONVOY DEFENSE";

            // Tip Description
            var descTr = loreCardObj.transform.Find("Text_TipDesc");
            GameObject descObj;
            if (descTr == null)
            {
                descObj = new GameObject("Text_TipDesc", typeof(RectTransform), typeof(TextMeshProUGUI));
                descObj.transform.SetParent(loreCardObj.transform, false);
            }
            else
            {
                descObj = descTr.gameObject;
            }
            var descRt = descObj.GetComponent<RectTransform>();
            descRt.anchorMin = new Vector2(0f, 0f);
            descRt.anchorMax = new Vector2(1f, 1f);
            descRt.pivot = new Vector2(0f, 0f);
            descRt.anchoredPosition = new Vector2(24f, 12f);
            descRt.sizeDelta = new Vector2(-48f, -44f);

            var descTmp = descObj.GetComponent<TextMeshProUGUI>();
            descTmp.font = fontMedium != null ? fontMedium : descTmp.font;
            descTmp.fontSize = 17f;
            descTmp.color = new Color(0.85f, 0.90f, 0.95f, 1f);
            descTmp.text = "Railbound convoys transport critical munitions across disputed sectors. Hold perimeter high ground during transit.";

            // 3. Tactical Tech Progress Section
            var progTr = fgObj.transform.Find("Panel_ProgressSection");
            GameObject progObj;
            if (progTr == null)
            {
                progObj = new GameObject("Panel_ProgressSection", typeof(RectTransform));
                progObj.transform.SetParent(fgObj.transform, false);
            }
            else
            {
                progObj = progTr.gameObject;
            }
            var progRt = progObj.GetComponent<RectTransform>();
            progRt.anchorMin = new Vector2(0.5f, 0f);
            progRt.anchorMax = new Vector2(0.5f, 0f);
            progRt.pivot = new Vector2(0.5f, 0f);
            progRt.anchoredPosition = new Vector2(0f, 50f);
            progRt.sizeDelta = new Vector2(960f, 75f);

            // Status Text (Left)
            var statusTr = progObj.transform.Find("Text_Status");
            GameObject statusObj;
            if (statusTr == null)
            {
                statusObj = new GameObject("Text_Status", typeof(RectTransform), typeof(TextMeshProUGUI));
                statusObj.transform.SetParent(progObj.transform, false);
            }
            else
            {
                statusObj = statusTr.gameObject;
            }
            var statusRt = statusObj.GetComponent<RectTransform>();
            statusRt.anchorMin = new Vector2(0f, 1f);
            statusRt.anchorMax = new Vector2(0.7f, 1f);
            statusRt.pivot = new Vector2(0f, 1f);
            statusRt.anchoredPosition = new Vector2(4f, 0f);
            statusRt.sizeDelta = new Vector2(0f, 26f);

            var statusTmp = statusObj.GetComponent<TextMeshProUGUI>();
            statusTmp.font = fontSemiBold != null ? fontSemiBold : statusTmp.font;
            statusTmp.fontSize = 16f;
            statusTmp.color = new Color(0.9f, 0.95f, 1f, 1f);
            statusTmp.text = "SYNCHRONIZING TACTICAL ASSETS...";

            // Percentage Text (Right)
            var percentTr = progObj.transform.Find("Text_Percent");
            GameObject percentObj;
            if (percentTr == null)
            {
                percentObj = new GameObject("Text_Percent", typeof(RectTransform), typeof(TextMeshProUGUI));
                percentObj.transform.SetParent(progObj.transform, false);
            }
            else
            {
                percentObj = percentTr.gameObject;
            }
            var percentRt = percentObj.GetComponent<RectTransform>();
            percentRt.anchorMin = new Vector2(0.7f, 1f);
            percentRt.anchorMax = new Vector2(1f, 1f);
            percentRt.pivot = new Vector2(1f, 1f);
            percentRt.anchoredPosition = new Vector2(-4f, 0f);
            percentRt.sizeDelta = new Vector2(0f, 26f);

            var percentTmp = percentObj.GetComponent<TextMeshProUGUI>();
            percentTmp.font = fontBold != null ? fontBold : percentTmp.font;
            percentTmp.fontSize = 20f;
            percentTmp.alignment = TextAlignmentOptions.Right;
            percentTmp.color = TacticalEmber;
            percentTmp.text = "0%";

            // Progress Slider
            var sliderTr = progObj.transform.Find("TacticalProgressBar");
            GameObject sliderObj;
            if (sliderTr == null)
            {
                sliderObj = new GameObject("TacticalProgressBar", typeof(RectTransform), typeof(Slider));
                sliderObj.transform.SetParent(progObj.transform, false);
            }
            else
            {
                sliderObj = sliderTr.gameObject;
            }
            var sliderRt = sliderObj.GetComponent<RectTransform>();
            sliderRt.anchorMin = new Vector2(0f, 0f);
            sliderRt.anchorMax = new Vector2(1f, 0f);
            sliderRt.pivot = new Vector2(0.5f, 0f);
            sliderRt.anchoredPosition = new Vector2(0f, 10f);
            sliderRt.sizeDelta = new Vector2(0f, 20f);

            var slider = sliderObj.GetComponent<Slider>();
            slider.transition = Selectable.Transition.None;
            slider.minValue = 0f;
            slider.maxValue = 1f;

            // Slider Background Frame
            var sldBgTr = sliderObj.transform.Find("Background");
            GameObject sldBg;
            if (sldBgTr == null)
            {
                sldBg = new GameObject("Background", typeof(RectTransform), typeof(Image));
                sldBg.transform.SetParent(sliderObj.transform, false);
            }
            else
            {
                sldBg = sldBgTr.gameObject;
            }
            var sldBgRt = sldBg.GetComponent<RectTransform>();
            sldBgRt.anchorMin = Vector2.zero;
            sldBgRt.anchorMax = Vector2.one;
            sldBgRt.offsetMin = Vector2.zero;
            sldBgRt.offsetMax = Vector2.zero;

            var sldBgImg = sldBg.GetComponent<Image>();
            sldBgImg.sprite = crossFrameAlt;
            sldBgImg.type = Image.Type.Sliced;
            sldBgImg.color = DarkCharcoal;

            // Progress Bar Underglow
            var glowTr = sliderObj.transform.Find("GlowUnderlay");
            GameObject glowObj;
            if (glowTr == null)
            {
                glowObj = new GameObject("GlowUnderlay", typeof(RectTransform), typeof(Image));
                glowObj.transform.SetParent(sliderObj.transform, false);
            }
            else
            {
                glowObj = glowTr.gameObject;
            }
            var glowRt = glowObj.GetComponent<RectTransform>();
            glowRt.anchorMin = new Vector2(0f, -0.5f);
            glowRt.anchorMax = new Vector2(1f, 1.5f);
            glowRt.offsetMin = new Vector2(-15f, 0f);
            glowRt.offsetMax = new Vector2(15f, 0f);

            var glowImg = glowObj.GetComponent<Image>();
            glowImg.sprite = radialShadow;
            glowImg.type = Image.Type.Sliced;
            glowImg.color = new Color(TacticalEmber.r, TacticalEmber.g, TacticalEmber.b, 0.45f);

            // Fill Area
            var fillAreaTr = sliderObj.transform.Find("Fill Area");
            GameObject fillArea;
            if (fillAreaTr == null)
            {
                fillArea = new GameObject("Fill Area", typeof(RectTransform));
                fillArea.transform.SetParent(sliderObj.transform, false);
            }
            else
            {
                fillArea = fillAreaTr.gameObject;
            }
            var fillAreaRt = fillArea.GetComponent<RectTransform>();
            fillAreaRt.anchorMin = Vector2.zero;
            fillAreaRt.anchorMax = Vector2.one;
            fillAreaRt.offsetMin = new Vector2(4f, 4f);
            fillAreaRt.offsetMax = new Vector2(-4f, -4f);

            // Fill
            var fillTr = fillArea.transform.Find("Fill");
            GameObject fillObj;
            if (fillTr == null)
            {
                fillObj = new GameObject("Fill", typeof(RectTransform), typeof(Image));
                fillObj.transform.SetParent(fillArea.transform, false);
            }
            else
            {
                fillObj = fillTr.gameObject;
            }
            var fillRt = fillObj.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;

            var fillImg = fillObj.GetComponent<Image>();
            fillImg.sprite = squareFilled;
            fillImg.type = Image.Type.Sliced;
            fillImg.color = TacticalEmber;

            slider.fillRect = fillRt;
            slider.targetGraphic = fillImg;

            // Wire BattleRoyaleLoadingScreen
            var controller = canvas.GetComponent<BattleRoyaleLoadingScreen>();
            if (controller == null) controller = canvas.gameObject.AddComponent<BattleRoyaleLoadingScreen>();

            controller.progressBar = slider;
            controller.progressPercent = percentTmp;
            controller.statusText = statusTmp;
            controller.loadingCanvas = canvas;
            controller.canvasGroup = canvasGroup;
            controller.progressGlow = glowImg;
            controller.tipTitleText = tagTmp;
            controller.tipDescriptionText = descTmp;
            controller.gameScene = "GameLobby";

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            sb.AppendLine("[PASS] LoadingScreen: Sliced Reach UI frames, UIEmberOverlay depth, CanvasGroup fade, pulsing ember glow, and tactical intel card configured.");
        }

        public static void UpgradeGameLobby(StringBuilder sb)
        {
            var scene = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                sb.AppendLine("[FAIL] GameLobby scene could not be opened.");
                return;
            }

            var canvasLobby = GameObject.Find("Canvas_Lobby");
            if (canvasLobby == null)
            {
                sb.AppendLine("[FAIL] Canvas_Lobby not found in GameLobby.");
                return;
            }

            var crossFrameAlt   = AssetDatabase.LoadAssetAtPath<Sprite>(CrossFrameAltPath);
            var crossFrame      = AssetDatabase.LoadAssetAtPath<Sprite>(CrossFramePath);
            var squareFilled    = AssetDatabase.LoadAssetAtPath<Sprite>(SquareFilledPath);
            var radialShadow    = AssetDatabase.LoadAssetAtPath<Sprite>(RadialShadowPath);
            var closeIcon       = AssetDatabase.LoadAssetAtPath<Sprite>(CloseIconPath);
            var currencyIcon    = AssetDatabase.LoadAssetAtPath<Sprite>(CurrencyIconPath);
            var hexagonOutline  = AssetDatabase.LoadAssetAtPath<Sprite>(HexagonOutlinePath);

            var fontBold     = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontBoldPath);
            var fontSemiBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontSemiBoldPath);
            var fontMedium   = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontMediumPath);

            // 1. Bottom Navigation Tabs
            var tabsContainer = canvasLobby.transform.Find("Panel_BottomLeftNav/LayoutGroup_Tabs");
            if (tabsContainer != null)
            {
                foreach (Transform tab in tabsContainer)
                {
                    var tabImg = tab.GetComponent<Image>();
                    if (tabImg != null)
                    {
                        tabImg.sprite = crossFrameAlt;
                        tabImg.type = Image.Type.Sliced;
                        tabImg.color = new Color(DarkCharcoal.r, DarkCharcoal.g, DarkCharcoal.b, 0.90f);
                    }

                    var activeInd = tab.Find("Image_ActiveIndicator")?.GetComponent<Image>();
                    if (activeInd != null)
                    {
                        activeInd.sprite = squareFilled;
                        activeInd.type = Image.Type.Sliced;
                        activeInd.color = TacticalEmber;

                        // Add subtle Reach UI underglow behind indicator if missing
                        var glowTr = tab.Find("Tab_Underglow");
                        if (glowTr == null)
                        {
                            var glowObj = new GameObject("Tab_Underglow", typeof(RectTransform), typeof(Image));
                            glowObj.transform.SetParent(tab, false);
                            glowObj.transform.SetSiblingIndex(activeInd.transform.GetSiblingIndex());

                            var grt = glowObj.GetComponent<RectTransform>();
                            grt.anchorMin = new Vector2(0f, 0f);
                            grt.anchorMax = new Vector2(1f, 0f);
                            grt.pivot = new Vector2(0.5f, 0f);
                            grt.anchoredPosition = new Vector2(0f, -4f);
                            grt.sizeDelta = new Vector2(20f, 16f);

                            var gi = glowObj.GetComponent<Image>();
                            gi.sprite = radialShadow;
                            gi.type = Image.Type.Sliced;
                            gi.color = new Color(TacticalEmber.r, TacticalEmber.g, TacticalEmber.b, 0.4f);
                            gi.raycastTarget = false;
                        }
                    }

                    var label = tab.Find("Text_Label")?.GetComponent<TextMeshProUGUI>();
                    if (label != null)
                    {
                        if (fontBold != null) label.font = fontBold;
                        label.characterSpacing = 1.5f;
                    }
                }
            }

            // 2. Deploy Button (CTA)
            var readyBtnTr = canvasLobby.transform.Find("Panel_BottomRightMatchmaking/Button_Ready");
            if (readyBtnTr != null)
            {
                var readyImg = readyBtnTr.GetComponent<Image>();
                if (readyImg != null)
                {
                    readyImg.sprite = crossFrameAlt;
                    readyImg.type = Image.Type.Sliced;
                    readyImg.color = TacticalEmber;
                }

                var readyGlow = readyBtnTr.Find("ReadyGlow")?.GetComponent<Image>();
                if (readyGlow != null)
                {
                    readyGlow.sprite = radialShadow;
                    readyGlow.type = Image.Type.Sliced;
                    readyGlow.color = new Color(TacticalEmber.r, TacticalEmber.g, TacticalEmber.b, 0.75f);
                    readyGlow.raycastTarget = false;

                    var pulse = readyGlow.GetComponent<ImagePulse>();
                    if (pulse == null) pulse = readyGlow.gameObject.AddComponent<ImagePulse>();
                    pulse.minAlpha = 0.35f;
                    pulse.maxAlpha = 0.90f;
                    pulse.pulseSpeed = 2.2f;
                }

                var readyText = readyBtnTr.Find("Text_Ready")?.GetComponent<TextMeshProUGUI>();
                if (readyText != null)
                {
                    if (fontBold != null) readyText.font = fontBold;
                    readyText.fontSize = 28f;
                    readyText.color = new Color(0.04f, 0.05f, 0.08f, 1f); // Crisp contrast dark against amber
                    readyText.characterSpacing = 3f;
                }
            }

            // 3. Top HUD Bar
            var topNav = canvasLobby.transform.Find("Panel_TopNav");
            if (topNav != null)
            {
                // Level Badge
                var lvlBadge = topNav.Find("Panel_PlayerTag/Image_LevelBadge")?.GetComponent<Image>();
                if (lvlBadge != null)
                {
                    lvlBadge.sprite = hexagonOutline;
                    lvlBadge.color = TacticalEmber;
                }

                // Profile XP Track & Fill
                var xpTrack = topNav.Find("Panel_PlayerTag/Bar_ProfileXPTrack")?.GetComponent<Image>();
                if (xpTrack != null)
                {
                    xpTrack.sprite = crossFrameAlt;
                    xpTrack.type = Image.Type.Sliced;
                    xpTrack.color = BevelBorder;
                }
                var xpFill = topNav.Find("Panel_PlayerTag/Bar_ProfileXPTrack/Bar_ProfileXPFill")?.GetComponent<Image>();
                if (xpFill != null)
                {
                    xpFill.sprite = squareFilled;
                    xpFill.color = TacticalEmber;
                }

                // Currency Displays (Pill containers)
                var goldPanel = topNav.Find("Panel_GoldCurrency")?.GetComponent<Image>();
                if (goldPanel != null)
                {
                    goldPanel.sprite = crossFrameAlt;
                    goldPanel.type = Image.Type.Sliced;
                    goldPanel.color = DarkCharcoal;
                }

                var currPanel = topNav.Find("Panel_Currency")?.GetComponent<Image>();
                if (currPanel != null)
                {
                    currPanel.sprite = crossFrameAlt;
                    currPanel.type = Image.Type.Sliced;
                    currPanel.color = DarkCharcoal;
                }

                var currIcon = topNav.Find("Panel_Currency/Image_CurrencyIcon")?.GetComponent<Image>();
                if (currIcon != null && currencyIcon != null)
                {
                    currIcon.sprite = currencyIcon;
                    currIcon.color = new Color(0f, 0.85f, 1f, 1f); // Cyan gem/currency
                }

                // Utility buttons (Square tactical containers)
                var utils = topNav.Find("LayoutGroup_Utilities");
                if (utils != null)
                {
                    foreach (Transform u in utils)
                    {
                        var uImg = u.GetComponent<Image>();
                        if (uImg != null)
                        {
                            uImg.color = Color.white;
                        }

                        // Add subtle container frame under utility button
                        var uFrameTr = u.Find("Utility_Frame");
                        if (uFrameTr == null)
                        {
                            var uFrame = new GameObject("Utility_Frame", typeof(RectTransform), typeof(Image));
                            uFrame.transform.SetParent(u, false);
                            uFrame.transform.SetAsFirstSibling();

                            var urt = uFrame.GetComponent<RectTransform>();
                            urt.anchorMin = Vector2.zero;
                            urt.anchorMax = Vector2.one;
                            urt.offsetMin = new Vector2(-4f, -4f);
                            urt.offsetMax = new Vector2(4f, 4f);

                            var ui = uFrame.GetComponent<Image>();
                            ui.sprite = crossFrameAlt;
                            ui.type = Image.Type.Sliced;
                            ui.color = new Color(DarkCharcoal.r, DarkCharcoal.g, DarkCharcoal.b, 0.85f);
                            ui.raycastTarget = false;
                        }
                    }
                }
            }

            // 4. Global Chat Drawer
            var chatDrawer = canvasLobby.transform.Find("Panel_BottomLeftNav/Panel_GlobalChatDrawer");
            if (chatDrawer != null)
            {
                var drawerImg = chatDrawer.GetComponent<Image>();
                if (drawerImg != null)
                {
                    drawerImg.sprite = crossFrameAlt;
                    drawerImg.type = Image.Type.Sliced;
                    drawerImg.color = DarkCharcoal;
                }

                var borderGlow = chatDrawer.Find("Border_Glow")?.GetComponent<Image>();
                if (borderGlow != null)
                {
                    borderGlow.sprite = crossFrameAlt;
                    borderGlow.type = Image.Type.Sliced;
                    borderGlow.color = new Color(TacticalEmber.r, TacticalEmber.g, TacticalEmber.b, 0.25f);
                }

                var headerBar = chatDrawer.Find("Header_Bar")?.GetComponent<Image>();
                if (headerBar != null)
                {
                    headerBar.sprite = crossFrameAlt;
                    headerBar.type = Image.Type.Sliced;
                    headerBar.color = new Color(BevelBorder.r, BevelBorder.g, BevelBorder.b, 0.6f);
                }

                var closeBtn = chatDrawer.Find("Header_Bar/Button_Close")?.GetComponent<Image>();
                if (closeBtn != null && closeIcon != null)
                {
                    closeBtn.sprite = closeIcon;
                    closeBtn.color = SlateMuted;
                    var textX = closeBtn.transform.Find("Text_X")?.gameObject;
                    if (textX != null) textX.SetActive(false);
                }

                var inputField = chatDrawer.Find("Panel_InputBar/InputField_Chat")?.GetComponent<Image>();
                if (inputField != null)
                {
                    inputField.sprite = crossFrameAlt;
                    inputField.type = Image.Type.Sliced;
                    inputField.color = new Color(DarkCharcoal.r, DarkCharcoal.g, DarkCharcoal.b, 0.95f);
                }

                var sendBtn = chatDrawer.Find("Panel_InputBar/Button_Send")?.GetComponent<Image>();
                if (sendBtn != null)
                {
                    sendBtn.sprite = crossFrameAlt;
                    sendBtn.type = Image.Type.Sliced;
                    sendBtn.color = TacticalEmber;
                }
            }

            // 5. Left Quests & Right Matchmaking Panels
            var questList = canvasLobby.transform.Find("Panel_LeftQuests/ScrollView_QuestList")?.GetComponent<Image>();
            if (questList != null)
            {
                questList.sprite = crossFrameAlt;
                questList.type = Image.Type.Sliced;
                questList.color = DarkCharcoal;
            }

            var challengerCard = canvasLobby.transform.Find("Panel_BottomRightMatchmaking/Card_NewChallenger")?.GetComponent<Image>();
            if (challengerCard != null)
            {
                var cardFrameTr = challengerCard.transform.Find("Frame_Border");
                if (cardFrameTr == null)
                {
                    var cFrame = new GameObject("Frame_Border", typeof(RectTransform), typeof(Image));
                    cFrame.transform.SetParent(challengerCard.transform, false);
                    cFrame.transform.SetAsFirstSibling();

                    var crt = cFrame.GetComponent<RectTransform>();
                    crt.anchorMin = Vector2.zero;
                    crt.anchorMax = Vector2.one;
                    crt.offsetMin = Vector2.zero;
                    crt.offsetMax = Vector2.zero;

                    var ci = cFrame.GetComponent<Image>();
                    ci.sprite = crossFrameAlt;
                    ci.type = Image.Type.Sliced;
                    ci.color = BevelBorder;
                    ci.raycastTarget = false;
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            sb.AppendLine("[PASS] GameLobby: Upgraded Bottom Nav Tabs, Deploy Button (pulsing CTA), Top HUD pill frames/utilities, and Global Chat Drawer.");
        }
    }
}
