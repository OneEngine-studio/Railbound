using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Railbound.UI;

namespace Railbound.Editor
{
    public static class LoginMenuRestyler
    {
        private const string ScenePath = "Assets/Project/LoginMenu.unity";

        // Palettes
        private static readonly Color ColorWarzoneYellow = new Color(1.0f, 0.784f, 0.0f, 1.0f); // #FFC800
        private static readonly Color ColorDarkBg        = new Color(0.043f, 0.063f, 0.090f, 0.96f); // #0B1017
        private static readonly Color ColorDarkCard      = new Color(0.055f, 0.082f, 0.118f, 0.88f); // #0E151E
        private static readonly Color ColorCardBorder    = new Color(0.35f, 0.48f, 0.62f, 0.40f);
        private static readonly Color ColorButtonBg      = new Color(0.082f, 0.118f, 0.165f, 0.95f); // #151E2A
        private static readonly Color ColorButtonBorder  = new Color(0.28f, 0.38f, 0.50f, 0.60f);
        private static readonly Color ColorTextWhite     = new Color(0.96f, 0.97f, 0.98f, 1.0f);
        private static readonly Color ColorTextMuted     = new Color(0.60f, 0.68f, 0.78f, 1.0f);
        private static readonly Color ColorTextDim       = new Color(0.40f, 0.48f, 0.58f, 1.0f);
        private static readonly Color ColorCyanAccent    = new Color(0.0f, 0.83f, 1.0f, 1.0f);
        private static readonly Color ColorGreenAccent   = new Color(0.18f, 0.92f, 0.44f, 1.0f);

        [MenuItem("Railbound/Restyle LoginMenu to Warzone Lobby")]
        public static void ApplyRestyle()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            // Load Reach UI assets
            var fontBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Reach - Complete Sci-Fi UI/Fonts/Rajdhani-Bold SDF.asset");
            var fontSemi = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Reach - Complete Sci-Fi UI/Fonts/Rajdhani-SemiBold SDF.asset");
            var fontMed  = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Reach - Complete Sci-Fi UI/Fonts/Rajdhani-Medium SDF.asset");

            var bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Reach - Complete Sci-Fi UI/Textures/Placeholders/Background/Static Background.png");
            var intelSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Reach - Complete Sci-Fi UI/Textures/Placeholders/Background/Placeholder Image 2.png");
            var vShadow = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Reach - Complete Sci-Fi UI/Textures/Shadows/Gradient/Vertical Gradient Shadow (High).png");
            var hShadow = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Reach - Complete Sci-Fi UI/Textures/Shadows/Gradient/Horizontal Gradient Shadow (High).png");
            var sqFilled = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Reach - Complete Sci-Fi UI/Textures/Borders/Flat/Square Filled.png");
            var crossFrame = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Reach - Complete Sci-Fi UI/Textures/Borders/Special/Cross Frame.png");

            var bgMusic = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Reach - Complete Sci-Fi UI/Audio/Background.wav");
            var clickSfx = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Reach - Complete Sci-Fi UI/Audio/Click.wav");
            var hoverSfx = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Reach - Complete Sci-Fi UI/Audio/Hover.wav");

            // Setup Camera
            var cam = Camera.main;
            if (cam != null)
            {
                Undo.RecordObject(cam, "Configure Camera");
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.035f, 0.051f, 0.071f, 1.0f);
            }

            var canvasGo = GameObject.Find("Canvas");
            if (canvasGo == null)
            {
                Debug.LogError("Canvas not found in LoginMenu!");
                return;
            }

            var canvas = canvasGo.GetComponent<Canvas>();
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                Undo.RecordObject(scaler, "Configure Scaler");
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
            }

            // 1. BACKGROUND & ATMOSPHERIC VIGNETTE
            var loginBgObj = canvasGo.transform.Find("LoginBackground")?.gameObject;
            if (loginBgObj == null)
            {
                loginBgObj = new GameObject("LoginBackground", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                loginBgObj.transform.SetParent(canvasGo.transform, false);
                loginBgObj.transform.SetSiblingIndex(0);
            }
            var bgRt = loginBgObj.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.sizeDelta = Vector2.zero;
            bgRt.anchoredPosition = Vector2.zero;
            var bgImg = loginBgObj.GetComponent<Image>();
            bgImg.sprite = bgSprite;
            bgImg.type = Image.Type.Simple;
            bgImg.preserveAspect = false;
            bgImg.color = new Color(0.44f, 0.50f, 0.58f, 1.0f); // Cinematic slate tint

            // Subtle Darkener Overlay
            var darkenerObj = canvasGo.transform.Find("BackgroundDarkener")?.gameObject;
            if (darkenerObj == null)
            {
                darkenerObj = new GameObject("BackgroundDarkener", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                darkenerObj.transform.SetParent(canvasGo.transform, false);
                darkenerObj.transform.SetSiblingIndex(1);
            }
            var darkRt = darkenerObj.GetComponent<RectTransform>();
            darkRt.anchorMin = Vector2.zero;
            darkRt.anchorMax = Vector2.one;
            darkRt.sizeDelta = Vector2.zero;
            darkRt.anchoredPosition = Vector2.zero;
            var darkImg = darkenerObj.GetComponent<Image>();
            darkImg.sprite = null;
            darkImg.color = new Color(0.035f, 0.051f, 0.071f, 0.65f);
            darkImg.raycastTarget = false;

            // Vignette & Shadows
            var topShadow = canvasGo.transform.Find("Shadow_Top") ?? canvasGo.transform.Find("Vignette");
            if (topShadow != null)
            {
                topShadow.name = "Shadow_Top";
                var vRt = topShadow.GetComponent<RectTransform>();
                vRt.anchorMin = new Vector2(0, 1);
                vRt.anchorMax = new Vector2(1, 1);
                vRt.pivot = new Vector2(0.5f, 1f);
                vRt.sizeDelta = new Vector2(0, 220);
                vRt.anchoredPosition = Vector2.zero;
                var vImg = topShadow.GetComponent<Image>();
                vImg.sprite = vShadow;
                vImg.type = Image.Type.Simple;
                vImg.color = new Color(0f, 0f, 0f, 0.90f);
                vImg.raycastTarget = false;
            }

            var botShadowObj = canvasGo.transform.Find("Shadow_Bottom")?.gameObject;
            if (botShadowObj == null)
            {
                botShadowObj = new GameObject("Shadow_Bottom", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                botShadowObj.transform.SetParent(canvasGo.transform, false);
                botShadowObj.transform.SetSiblingIndex(3);
            }
            var bRt = botShadowObj.GetComponent<RectTransform>();
            bRt.anchorMin = new Vector2(0, 0);
            bRt.anchorMax = new Vector2(1, 0);
            bRt.pivot = new Vector2(0.5f, 0f);
            bRt.sizeDelta = new Vector2(0, 220);
            bRt.anchoredPosition = Vector2.zero;
            bRt.localScale = new Vector3(1, -1, 1);
            var bImg = botShadowObj.GetComponent<Image>();
            bImg.sprite = vShadow;
            bImg.color = new Color(0f, 0f, 0f, 0.92f);
            bImg.raycastTarget = false;

            var leftShadowObj = canvasGo.transform.Find("Shadow_Left")?.gameObject;
            if (leftShadowObj == null)
            {
                leftShadowObj = new GameObject("Shadow_Left", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                leftShadowObj.transform.SetParent(canvasGo.transform, false);
                leftShadowObj.transform.SetSiblingIndex(4);
            }
            var lRt = leftShadowObj.GetComponent<RectTransform>();
            lRt.anchorMin = new Vector2(0, 0);
            lRt.anchorMax = new Vector2(0, 1);
            lRt.pivot = new Vector2(0f, 0.5f);
            lRt.sizeDelta = new Vector2(750, 0);
            lRt.anchoredPosition = Vector2.zero;
            var lImg = leftShadowObj.GetComponent<Image>();
            lImg.sprite = hShadow;
            lImg.color = new Color(0f, 0f, 0f, 0.85f);
            lImg.raycastTarget = false;

            var rightShadowObj = canvasGo.transform.Find("Shadow_Right")?.gameObject;
            if (rightShadowObj == null)
            {
                rightShadowObj = new GameObject("Shadow_Right", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                rightShadowObj.transform.SetParent(canvasGo.transform, false);
                rightShadowObj.transform.SetSiblingIndex(5);
            }
            var rRt = rightShadowObj.GetComponent<RectTransform>();
            rRt.anchorMin = new Vector2(1, 0);
            rRt.anchorMax = new Vector2(1, 1);
            rRt.pivot = new Vector2(1f, 0.5f);
            rRt.sizeDelta = new Vector2(500, 0);
            rRt.anchoredPosition = Vector2.zero;
            rRt.localScale = new Vector3(-1, 1, 1);
            var rImg = rightShadowObj.GetComponent<Image>();
            rImg.sprite = hShadow;
            rImg.color = new Color(0f, 0f, 0f, 0.70f);
            rImg.raycastTarget = false;

            // 2. TOP HEADER BAR
            var topBarObj = canvasGo.transform.Find("TopHeaderBar")?.gameObject;
            if (topBarObj == null)
            {
                topBarObj = new GameObject("TopHeaderBar", typeof(RectTransform));
                topBarObj.transform.SetParent(canvasGo.transform, false);
            }
            var tbRt = topBarObj.GetComponent<RectTransform>();
            tbRt.anchorMin = new Vector2(0, 1);
            tbRt.anchorMax = new Vector2(1, 1);
            tbRt.pivot = new Vector2(0.5f, 1f);
            tbRt.sizeDelta = new Vector2(0, 90);
            tbRt.anchoredPosition = Vector2.zero;

            // Hide redundant raster logo so title is unified & sharp
            var logo = GameObject.Find("Logo_Railbound_Yellow");
            if (logo != null) logo.SetActive(false);

            // BrandTitle (Left)
            var brandTitle = GameObject.Find("BrandTitle");
            if (brandTitle != null)
            {
                brandTitle.transform.SetParent(topBarObj.transform, false);
                var btRt = brandTitle.GetComponent<RectTransform>();
                btRt.anchorMin = new Vector2(0, 1);
                btRt.anchorMax = new Vector2(0, 1);
                btRt.pivot = new Vector2(0, 1);
                btRt.anchoredPosition = new Vector2(80, -18);
                btRt.sizeDelta = new Vector2(500, 48);

                var titleTmp = brandTitle.GetComponent<TMP_Text>();
                if (titleTmp != null)
                {
                    titleTmp.font = fontBold;
                    titleTmp.fontSize = 44;
                    titleTmp.text = "<color=#FFC800>RAIL</color>BOUND";
                    titleTmp.characterSpacing = 3;
                    titleTmp.alignment = TextAlignmentOptions.Left;
                }
            }

            // Top Subtitle
            var subTitleObj = topBarObj.transform.Find("SubTitle")?.gameObject;
            if (subTitleObj == null)
            {
                subTitleObj = new GameObject("SubTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                subTitleObj.transform.SetParent(topBarObj.transform, false);
            }
            var stRt = subTitleObj.GetComponent<RectTransform>();
            stRt.anchorMin = new Vector2(0, 1);
            stRt.anchorMax = new Vector2(0, 1);
            stRt.pivot = new Vector2(0, 1);
            stRt.anchoredPosition = new Vector2(82, -66);
            stRt.sizeDelta = new Vector2(500, 20);
            var stTmp = subTitleObj.GetComponent<TextMeshProUGUI>();
            stTmp.font = fontSemi;
            stTmp.fontSize = 12;
            stTmp.characterSpacing = 2.5f;
            stTmp.color = ColorTextMuted;
            stTmp.text = "OPERATION: IRONLINE  //  SEASON 01 PRE-ALPHA";

            // Server Status Bar (Top Right)
            var statusBarObj = topBarObj.transform.Find("StatusBarBg")?.gameObject;
            if (statusBarObj == null)
            {
                statusBarObj = new GameObject("StatusBarBg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                statusBarObj.transform.SetParent(topBarObj.transform, false);
            }
            var sbRt = statusBarObj.GetComponent<RectTransform>();
            sbRt.anchorMin = new Vector2(1, 1);
            sbRt.anchorMax = new Vector2(1, 1);
            sbRt.pivot = new Vector2(1, 1);
            sbRt.anchoredPosition = new Vector2(-80, -24);
            sbRt.sizeDelta = new Vector2(250, 36);

            var sbImg = statusBarObj.GetComponent<Image>();
            sbImg.sprite = sqFilled;
            sbImg.type = Image.Type.Sliced;
            sbImg.color = new Color(0.06f, 0.09f, 0.13f, 0.85f);

            // Subtle border on status bar
            AddCardBorder(statusBarObj.transform, crossFrame, new Color(0.3f, 0.45f, 0.6f, 0.35f));

            var serverStatus = GameObject.Find("ServerStatus");
            if (serverStatus != null)
            {
                serverStatus.transform.SetParent(statusBarObj.transform, false);
                var ssRt = serverStatus.GetComponent<RectTransform>();
                ssRt.anchorMin = Vector2.zero;
                ssRt.anchorMax = Vector2.one;
                ssRt.pivot = new Vector2(0.5f, 0.5f);
                ssRt.anchoredPosition = new Vector2(16, 0);
                ssRt.sizeDelta = new Vector2(-20, 0);

                var ssTmp = serverStatus.GetComponent<TMP_Text>();
                if (ssTmp != null)
                {
                    ssTmp.font = fontBold;
                    ssTmp.fontSize = 12.5f;
                    ssTmp.characterSpacing = 1.5f;
                    ssTmp.color = ColorTextWhite;
                    ssTmp.alignment = TextAlignmentOptions.MidlineLeft;
                    ssTmp.text = "STATUS: ONLINE";
                }

                var dot = serverStatus.transform.Find("OnlineDot");
                if (dot != null)
                {
                    var dRt = dot.GetComponent<RectTransform>();
                    dRt.anchorMin = new Vector2(0, 0.5f);
                    dRt.anchorMax = new Vector2(0, 0.5f);
                    dRt.pivot = new Vector2(0.5f, 0.5f);
                    dRt.anchoredPosition = new Vector2(-12, 0);
                    dRt.sizeDelta = new Vector2(10, 10);
                    var dImg = dot.GetComponent<Image>();
                    if (dImg != null)
                    {
                        dImg.sprite = sqFilled;
                        dImg.color = ColorGreenAccent;
                    }
                }
            }

            // 3. LEFT DEPLOYMENT MENU
            var loginPanel = GameObject.Find("LoginPanel");
            if (loginPanel != null)
            {
                var lpRt = loginPanel.GetComponent<RectTransform>();
                lpRt.anchorMin = new Vector2(0, 0.5f);
                lpRt.anchorMax = new Vector2(0, 0.5f);
                lpRt.pivot = new Vector2(0, 0.5f);
                lpRt.anchoredPosition = new Vector2(80, -20);
                lpRt.sizeDelta = new Vector2(460, 560);

                var lpImg = loginPanel.GetComponent<Image>();
                if (lpImg != null)
                {
                    lpImg.sprite = sqFilled;
                    lpImg.type = Image.Type.Sliced;
                    lpImg.color = ColorDarkCard;
                }

                AddCardBorder(loginPanel.transform, crossFrame, ColorCardBorder);

                var rail = loginPanel.transform.Find("LoginRail");
                if (rail != null) rail.gameObject.SetActive(false);

                var acc = loginPanel.transform.Find("AccentLine");
                if (acc != null) acc.gameObject.SetActive(false);

                // Section Label: // AUTHENTICATION PROTOCOL
                var secLabelObj = loginPanel.transform.Find("SectionLabel")?.gameObject;
                if (secLabelObj == null)
                {
                    secLabelObj = new GameObject("SectionLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                    secLabelObj.transform.SetParent(loginPanel.transform, false);
                }
                var secRt = secLabelObj.GetComponent<RectTransform>();
                secRt.anchorMin = new Vector2(0, 1);
                secRt.anchorMax = new Vector2(1, 1);
                secRt.pivot = new Vector2(0.5f, 1f);
                secRt.anchoredPosition = new Vector2(0, -18);
                secRt.sizeDelta = new Vector2(-40, 26);
                var secTmp = secLabelObj.GetComponent<TextMeshProUGUI>();
                secTmp.font = fontBold;
                secTmp.fontSize = 13;
                secTmp.characterSpacing = 2.5f;
                secTmp.color = ColorWarzoneYellow;
                secTmp.text = "// AUTHENTICATION PROTOCOL";

                // BUTTON 1: GOOGLE LOGIN
                var googleBtn = loginPanel.transform.Find("GoogleLoginButton")?.gameObject;
                if (googleBtn != null)
                {
                    var gRt = googleBtn.GetComponent<RectTransform>();
                    gRt.anchorMin = new Vector2(0.5f, 1);
                    gRt.anchorMax = new Vector2(0.5f, 1);
                    gRt.pivot = new Vector2(0.5f, 1);
                    gRt.anchoredPosition = new Vector2(0, -60);
                    gRt.sizeDelta = new Vector2(420, 60);

                    var gImg = googleBtn.GetComponent<Image>();
                    if (gImg != null)
                    {
                        gImg.sprite = sqFilled;
                        gImg.type = Image.Type.Sliced;
                        gImg.color = ColorButtonBg;
                    }

                    var shadow = googleBtn.transform.Find("GoogleLoginButton's Shadow");
                    if (shadow != null) shadow.gameObject.SetActive(false);

                    // Left Yellow Accent Bar
                    var gAccent = googleBtn.transform.Find("YellowStripe")?.gameObject;
                    if (gAccent == null)
                    {
                        gAccent = new GameObject("YellowStripe", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                        gAccent.transform.SetParent(googleBtn.transform, false);
                    }
                    var gaRt = gAccent.GetComponent<RectTransform>();
                    gaRt.anchorMin = new Vector2(0, 0);
                    gaRt.anchorMax = new Vector2(0, 1);
                    gaRt.pivot = new Vector2(0, 0.5f);
                    gaRt.anchoredPosition = Vector2.zero;
                    gaRt.sizeDelta = new Vector2(6, 0);
                    var gaImg = gAccent.GetComponent<Image>();
                    gaImg.sprite = sqFilled;
                    gaImg.color = ColorWarzoneYellow;
                    gaImg.raycastTarget = false;

                    // Google Icon
                    var gIcon = googleBtn.transform.Find("GoogleIcon");
                    if (gIcon != null)
                    {
                        var giRt = gIcon.GetComponent<RectTransform>();
                        giRt.anchorMin = new Vector2(0, 0.5f);
                        giRt.anchorMax = new Vector2(0, 0.5f);
                        giRt.pivot = new Vector2(0.5f, 0.5f);
                        giRt.anchoredPosition = new Vector2(36, 0);
                        giRt.sizeDelta = new Vector2(26, 26);
                    }

                    // Label
                    var gLabel = googleBtn.transform.Find("Label")?.GetComponent<TMP_Text>();
                    if (gLabel != null)
                    {
                        gLabel.font = fontBold;
                        gLabel.fontSize = 16;
                        gLabel.characterSpacing = 1.8f;
                        gLabel.color = ColorTextWhite;
                        gLabel.text = "CONTINUE WITH GOOGLE";
                        gLabel.alignment = TextAlignmentOptions.MidlineLeft;
                        gLabel.margin = new Vector4(70, 0, 12, 0);
                    }

                    EnsureTacticalHover(googleBtn, hoverSfx, clickSfx);
                }

                // DIVIDER
                var divider = loginPanel.transform.Find("Divider")?.gameObject;
                if (divider != null)
                {
                    var dRt = divider.GetComponent<RectTransform>();
                    dRt.anchorMin = new Vector2(0.5f, 1);
                    dRt.anchorMax = new Vector2(0.5f, 1);
                    dRt.pivot = new Vector2(0.5f, 1);
                    dRt.anchoredPosition = new Vector2(0, -130);
                    dRt.sizeDelta = new Vector2(420, 24);

                    var dLabel = divider.transform.Find("Label")?.GetComponent<TMP_Text>();
                    if (dLabel != null)
                    {
                        dLabel.font = fontSemi;
                        dLabel.fontSize = 11.5f;
                        dLabel.characterSpacing = 2.0f;
                        dLabel.color = ColorTextMuted;
                        dLabel.text = "— OR SIGN IN WITH CREDENTIALS —";
                        dLabel.alignment = TextAlignmentOptions.Center;
                    }
                }

                // BUTTON 2: MORE BUTTON (ACCOUNT LOGIN)
                var moreBtn = loginPanel.transform.Find("MoreButton")?.gameObject;
                if (moreBtn != null)
                {
                    var mRt = moreBtn.GetComponent<RectTransform>();
                    mRt.anchorMin = new Vector2(0.5f, 1);
                    mRt.anchorMax = new Vector2(0.5f, 1);
                    mRt.pivot = new Vector2(0.5f, 1);
                    mRt.anchoredPosition = new Vector2(0, -162);
                    mRt.sizeDelta = new Vector2(420, 54);

                    var mImg = moreBtn.GetComponent<Image>();
                    if (mImg != null)
                    {
                        mImg.sprite = sqFilled;
                        mImg.type = Image.Type.Sliced;
                        mImg.color = ColorButtonBg;
                    }

                    var shadow = moreBtn.transform.Find("MoreButton's Shadow");
                    if (shadow != null) shadow.gameObject.SetActive(false);

                    var mAccent = moreBtn.transform.Find("MoreAccent")?.gameObject;
                    if (mAccent != null)
                    {
                        var maRt = mAccent.GetComponent<RectTransform>();
                        maRt.anchorMin = new Vector2(0, 0);
                        maRt.anchorMax = new Vector2(0, 1);
                        maRt.pivot = new Vector2(0, 0.5f);
                        maRt.anchoredPosition = Vector2.zero;
                        maRt.sizeDelta = new Vector2(5, 0);
                        var maImg = mAccent.GetComponent<Image>();
                        if (maImg != null)
                        {
                            maImg.sprite = sqFilled;
                            maImg.color = ColorCyanAccent;
                        }
                    }

                    var mLabel = moreBtn.transform.Find("Label")?.GetComponent<TMP_Text>();
                    if (mLabel != null)
                    {
                        mLabel.font = fontBold;
                        mLabel.fontSize = 15.5f;
                        mLabel.characterSpacing = 1.8f;
                        mLabel.color = ColorTextWhite;
                        mLabel.text = "OPERATOR SIGN IN / REGISTER";
                        mLabel.alignment = TextAlignmentOptions.MidlineLeft;
                        mLabel.margin = new Vector4(24, 0, 12, 0);
                    }

                    EnsureTacticalHover(moreBtn, hoverSfx, clickSfx);
                }

                // BUTTON 3: GUEST BUTTON (PLAY AS GUEST)
                var guestBtn = loginPanel.transform.Find("GuestButton")?.gameObject;
                if (guestBtn != null)
                {
                    var gRt = guestBtn.GetComponent<RectTransform>();
                    gRt.anchorMin = new Vector2(0.5f, 1);
                    gRt.anchorMax = new Vector2(0.5f, 1);
                    gRt.pivot = new Vector2(0.5f, 1);
                    gRt.anchoredPosition = new Vector2(0, -226);
                    gRt.sizeDelta = new Vector2(420, 54);

                    var gImg = guestBtn.GetComponent<Image>();
                    if (gImg != null)
                    {
                        gImg.sprite = sqFilled;
                        gImg.type = Image.Type.Sliced;
                        gImg.color = ColorButtonBg;
                    }

                    var shadow = guestBtn.transform.Find("GuestButton's Shadow");
                    if (shadow != null) shadow.gameObject.SetActive(false);

                    var gAccent = guestBtn.transform.Find("GuestAccent")?.gameObject;
                    if (gAccent != null)
                    {
                        var gaRt = gAccent.GetComponent<RectTransform>();
                        gaRt.anchorMin = new Vector2(0, 0);
                        gaRt.anchorMax = new Vector2(0, 1);
                        gaRt.pivot = new Vector2(0, 0.5f);
                        gaRt.anchoredPosition = Vector2.zero;
                        gaRt.sizeDelta = new Vector2(5, 0);
                        var gaImg = gAccent.GetComponent<Image>();
                        if (gaImg != null)
                        {
                            gaImg.sprite = sqFilled;
                            gaImg.color = ColorGreenAccent;
                        }
                    }

                    var gLabel = guestBtn.transform.Find("Label")?.GetComponent<TMP_Text>();
                    if (gLabel != null)
                    {
                        gLabel.font = fontBold;
                        gLabel.fontSize = 15.5f;
                        gLabel.characterSpacing = 1.8f;
                        gLabel.color = ColorTextWhite;
                        gLabel.text = "DEPLOY AS GUEST (QUICK PLAY)";
                        gLabel.alignment = TextAlignmentOptions.MidlineLeft;
                        gLabel.margin = new Vector4(24, 0, 12, 0);
                    }

                    EnsureTacticalHover(guestBtn, hoverSfx, clickSfx);
                }

                // TERMS & CONDITIONS
                var terms = GameObject.Find("Terms");
                if (terms != null)
                {
                    terms.transform.SetParent(loginPanel.transform, false);
                    var tRt = terms.GetComponent<RectTransform>();
                    tRt.anchorMin = new Vector2(0.5f, 0);
                    tRt.anchorMax = new Vector2(0.5f, 0);
                    tRt.pivot = new Vector2(0.5f, 0);
                    tRt.anchoredPosition = new Vector2(0, 20);
                    tRt.sizeDelta = new Vector2(420, 80);

                    var cb1 = terms.transform.Find("Checkbox1");
                    var ageTxt = terms.transform.Find("AgeText");
                    var cb2 = terms.transform.Find("Checkbox2");
                    var termsTxt = terms.transform.Find("TermsText");

                    if (cb1 != null)
                    {
                        var cbRt = cb1.GetComponent<RectTransform>();
                        cbRt.anchorMin = new Vector2(0, 1);
                        cbRt.anchorMax = new Vector2(0, 1);
                        cbRt.pivot = new Vector2(0, 1);
                        cbRt.anchoredPosition = new Vector2(0, 0);
                        cbRt.sizeDelta = new Vector2(22, 22);
                        var cbImg = cb1.GetComponent<Image>();
                        if (cbImg != null) { cbImg.sprite = sqFilled; cbImg.color = new Color(0.12f, 0.16f, 0.22f, 0.9f); }
                    }

                    if (ageTxt != null)
                    {
                        var aRt = ageTxt.GetComponent<RectTransform>();
                        aRt.anchorMin = new Vector2(0, 1);
                        aRt.anchorMax = new Vector2(1, 1);
                        aRt.pivot = new Vector2(0, 1);
                        aRt.anchoredPosition = new Vector2(32, 0);
                        aRt.sizeDelta = new Vector2(-32, 22);
                        var aTmp = ageTxt.GetComponent<TMP_Text>();
                        if (aTmp != null)
                        {
                            aTmp.font = fontMed;
                            aTmp.fontSize = 12.5f;
                            aTmp.color = ColorTextMuted;
                            aTmp.text = "I confirm that my age information is true and valid.";
                        }
                    }

                    if (cb2 != null)
                    {
                        var cbRt = cb2.GetComponent<RectTransform>();
                        cbRt.anchorMin = new Vector2(0, 1);
                        cbRt.anchorMax = new Vector2(0, 1);
                        cbRt.pivot = new Vector2(0, 1);
                        cbRt.anchoredPosition = new Vector2(0, -32);
                        cbRt.sizeDelta = new Vector2(22, 22);
                        var cbImg = cb2.GetComponent<Image>();
                        if (cbImg != null) { cbImg.sprite = sqFilled; cbImg.color = new Color(0.12f, 0.16f, 0.22f, 0.9f); }
                    }

                    if (termsTxt != null)
                    {
                        var tTxtRt = termsTxt.GetComponent<RectTransform>();
                        tTxtRt.anchorMin = new Vector2(0, 1);
                        tTxtRt.anchorMax = new Vector2(1, 1);
                        tTxtRt.pivot = new Vector2(0, 1);
                        tTxtRt.anchoredPosition = new Vector2(32, -32);
                        tTxtRt.sizeDelta = new Vector2(-32, 22);
                        var tTmp = termsTxt.GetComponent<TMP_Text>();
                        if (tTmp != null)
                        {
                            tTmp.font = fontMed;
                            tTmp.fontSize = 12.5f;
                            tTmp.color = ColorTextMuted;
                            tTmp.text = "I have read and agree to the <color=#FFC800><u>Terms</u></color> & <color=#FFC800><u>Privacy Policy</u></color>.";
                        }
                    }
                }
            }

            // 4. RIGHT SIDE: WARZONE TACTICAL INTEL & MISSION DOSSIER CARD
            var dossierObj = canvasGo.transform.Find("TacticalDossierCard")?.gameObject;
            if (dossierObj == null)
            {
                dossierObj = new GameObject("TacticalDossierCard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                dossierObj.transform.SetParent(canvasGo.transform, false);
            }
            dossierObj.transform.SetSiblingIndex(loginPanel != null ? loginPanel.transform.GetSiblingIndex() + 1 : 6);
            var dosRt = dossierObj.GetComponent<RectTransform>();
            dosRt.anchorMin = new Vector2(1, 0.5f);
            dosRt.anchorMax = new Vector2(1, 0.5f);
            dosRt.pivot = new Vector2(1, 0.5f);
            dosRt.anchoredPosition = new Vector2(-80, -20);
            dosRt.sizeDelta = new Vector2(560, 560);

            var dosImg = dossierObj.GetComponent<Image>();
            dosImg.sprite = sqFilled;
            dosImg.type = Image.Type.Sliced;
            dosImg.color = ColorDarkCard;

            AddCardBorder(dossierObj.transform, crossFrame, ColorCardBorder);

            // Dossier Header Category
            var dosCat = CreateTextChild(dossierObj.transform, "Category", fontBold, 11.5f, ColorWarzoneYellow,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -18), new Vector2(-40, 20));
            dosCat.characterSpacing = 2.5f;
            dosCat.text = "[MISSION INTEL  //  OPERATIONAL BRIEFING]";

            // Dossier Header Title
            var dosTitle = CreateTextChild(dossierObj.transform, "Title", fontBold, 24f, ColorTextWhite,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -42), new Vector2(-40, 32));
            dosTitle.characterSpacing = 1.5f;
            dosTitle.text = "OPERATION: IRONLINE INFILTRATION";

            // Dossier Artwork Banner
            var bannerObj = dossierObj.transform.Find("BannerImage")?.gameObject;
            if (bannerObj == null)
            {
                bannerObj = new GameObject("BannerImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                bannerObj.transform.SetParent(dossierObj.transform, false);
            }
            var bnrRt = bannerObj.GetComponent<RectTransform>();
            bnrRt.anchorMin = new Vector2(0.5f, 1);
            bnrRt.anchorMax = new Vector2(0.5f, 1);
            bnrRt.pivot = new Vector2(0.5f, 1);
            bnrRt.anchoredPosition = new Vector2(0, -82);
            bnrRt.sizeDelta = new Vector2(520, 200);
            var bnrImg = bannerObj.GetComponent<Image>();
            bnrImg.sprite = intelSprite;
            bnrImg.type = Image.Type.Simple;
            bnrImg.preserveAspect = false;
            bnrImg.color = new Color(0.85f, 0.88f, 0.92f, 1.0f);

            var bnrOverlay = bannerObj.transform.Find("Overlay")?.gameObject;
            if (bnrOverlay == null)
            {
                bnrOverlay = new GameObject("Overlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                bnrOverlay.transform.SetParent(bannerObj.transform, false);
            }
            var boRt = bnrOverlay.GetComponent<RectTransform>();
            boRt.anchorMin = Vector2.zero;
            boRt.anchorMax = Vector2.one;
            boRt.sizeDelta = Vector2.zero;
            var boImg = bnrOverlay.GetComponent<Image>();
            boImg.sprite = vShadow;
            boImg.color = new Color(0.04f, 0.06f, 0.09f, 0.80f);
            boImg.raycastTarget = false;

            var bnrTag = CreateTextChild(bannerObj.transform, "BannerTag", fontBold, 12f, ColorWarzoneYellow,
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(-28, 24));
            bnrTag.characterSpacing = 2.0f;
            bnrTag.text = "SECTOR 04 // TACTICAL RAIL JUNCTION // HOSTILE CONTROL";

            var dosBody = CreateTextChild(dossierObj.transform, "Body", fontMed, 13f, ColorTextMuted,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -294), new Vector2(510, 68));
            dosBody.lineSpacing = 1.3f;
            dosBody.text = "Infiltrate high-velocity transit networks, coordinate tactical loadouts with squad operators, and secure the primary extraction express before rival contractors extract the payload.";

            var badgeBar = CreateTextChild(dossierObj.transform, "BadgeBar", fontBold, 12f, ColorCyanAccent,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -370), new Vector2(510, 26));
            badgeBar.characterSpacing = 1.5f;
            badgeBar.text = "[NETCODE: 60Hz DEDICATED]   [CROSSPLAY: READY]   [ANTI-CHEAT: ONLINE]";

            var featList = CreateTextChild(dossierObj.transform, "FeatureList", fontSemi, 12.5f, ColorTextWhite,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -406), new Vector2(510, 130));
            featList.lineSpacing = 1.4f;
            featList.text =
                "<color=#FFC800>►</color>  <b>Ranked Royale & Rapid Incursion Modes</b>\n" +
                "<color=#FFC800>►</color>  <b>Nakama Cloud Progression & Cross-Platform Auth</b>\n" +
                "<color=#FFC800>►</color>  <b>Modular Operator Weapon Blueprints & Loadouts</b>\n" +
                "<color=#FFC800>►</color>  <b>Dedicated Low-Latency Matchmaking Grid</b>";

            // 5. BOTTOM STATUS & HOTKEY FOOTER
            var buildTag = GameObject.Find("BuildTag");
            if (buildTag != null)
            {
                var btRt = buildTag.GetComponent<RectTransform>();
                btRt.anchorMin = new Vector2(0, 0);
                btRt.anchorMax = new Vector2(0, 0);
                btRt.pivot = new Vector2(0, 0);
                btRt.anchoredPosition = new Vector2(80, 18);
                btRt.sizeDelta = new Vector2(500, 22);

                var btTmp = buildTag.GetComponent<TMP_Text>();
                if (btTmp != null)
                {
                    btTmp.font = fontSemi;
                    btTmp.fontSize = 11.5f;
                    btTmp.characterSpacing = 1.5f;
                    btTmp.color = ColorTextDim;
                    btTmp.text = "RAILBOUND  |  SEASON 01  |  BUILD 1.0.4  |  WARZONE PROTOCOL";
                }
            }

            var hotkeysObj = canvasGo.transform.Find("HotkeyFooter")?.gameObject;
            if (hotkeysObj == null)
            {
                hotkeysObj = new GameObject("HotkeyFooter", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                hotkeysObj.transform.SetParent(canvasGo.transform, false);
            }
            var hkRt = hotkeysObj.GetComponent<RectTransform>();
            hkRt.anchorMin = new Vector2(1, 0);
            hkRt.anchorMax = new Vector2(1, 0);
            hkRt.pivot = new Vector2(1, 0);
            hkRt.anchoredPosition = new Vector2(-80, 18);
            hkRt.sizeDelta = new Vector2(500, 22);
            var hkTmp = hotkeysObj.GetComponent<TextMeshProUGUI>();
            hkTmp.font = fontBold;
            hkTmp.fontSize = 11.5f;
            hkTmp.characterSpacing = 1.8f;
            hkTmp.color = ColorTextDim;
            hkTmp.alignment = TextAlignmentOptions.MidlineRight;
            hkTmp.text = "[MOUSE 1] SELECT    [ENTER] CONFIRM    [ESC] CANCEL";

            // 6. POPUPS POLISH
            RestyleAccountPopup(canvasGo, fontBold, fontSemi, fontMed, sqFilled, crossFrame, hoverSfx, clickSfx);
            RestyleCreateAccountPopup(canvasGo, fontBold, fontSemi, fontMed, sqFilled, crossFrame, hoverSfx, clickSfx);
            RestyleErrorPopup(canvasGo, fontBold, fontSemi, fontMed, sqFilled, crossFrame, hoverSfx, clickSfx);

            // 7. WIRE LOGINUI & AUDIO
            var loginUI = canvasGo.GetComponentInChildren<LoginUI>(true);
            if (loginUI != null)
            {
                var so = new SerializedObject(loginUI);

                var bgProp = so.FindProperty("backgroundMusicClip");
                if (bgProp != null && bgMusic != null) bgProp.objectReferenceValue = bgMusic;

                var tickProp = so.FindProperty("tickSoundClip");
                if (tickProp != null && clickSfx != null) tickProp.objectReferenceValue = clickSfx;

                var closeProp = so.FindProperty("errorCloseSoundClip");
                if (closeProp != null && clickSfx != null) closeProp.objectReferenceValue = clickSfx;

                var termsObj = GameObject.Find("Terms");
                if (termsObj != null)
                {
                    var cb1 = termsObj.transform.Find("Checkbox1")?.GetComponent<Button>();
                    var cb1Img = termsObj.transform.Find("Checkbox1")?.GetComponent<Image>();
                    var cb2 = termsObj.transform.Find("Checkbox2")?.GetComponent<Button>();
                    var cb2Img = termsObj.transform.Find("Checkbox2")?.GetComponent<Image>();

                    so.FindProperty("ageCheckboxButton").objectReferenceValue = cb1;
                    so.FindProperty("ageCheckboxImage").objectReferenceValue = cb1Img;
                    so.FindProperty("termsCheckboxButton").objectReferenceValue = cb2;
                    so.FindProperty("termsCheckboxImage").objectReferenceValue = cb2Img;
                }

                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(loginUI);
            }

            // Mark Scene Dirty and Save
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[LoginMenuRestyler] LoginMenu scene successfully updated with Reach UI Call of Duty Warzone lobby styling!");
        }

        private static void AddCardBorder(Transform parent, Sprite crossFrame, Color borderColor)
        {
            var borderObj = parent.Find("CardBorder")?.gameObject;
            if (borderObj == null)
            {
                borderObj = new GameObject("CardBorder", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                borderObj.transform.SetParent(parent, false);
            }
            var bRt = borderObj.GetComponent<RectTransform>();
            bRt.anchorMin = Vector2.zero;
            bRt.anchorMax = Vector2.one;
            bRt.sizeDelta = Vector2.zero;
            bRt.anchoredPosition = Vector2.zero;

            var bImg = borderObj.GetComponent<Image>();
            bImg.sprite = crossFrame;
            bImg.type = Image.Type.Sliced;
            bImg.pixelsPerUnitMultiplier = 3.2f; // Refine and slim down the corner brackets
            bImg.color = borderColor;
            bImg.raycastTarget = false;
        }

        private static void RestyleAccountPopup(GameObject canvas, TMP_FontAsset fontBold, TMP_FontAsset fontSemi, TMP_FontAsset fontMed, Sprite sqFilled, Sprite crossFrame, AudioClip hoverSfx, AudioClip clickSfx)
        {
            var accObj = canvas.transform.Find("AccountLoginPopup")?.gameObject;
            if (accObj == null) return;
            var card = accObj.transform.Find("AccountLoginCard");
            if (card == null) return;

            var cardRt = card.GetComponent<RectTransform>();
            cardRt.sizeDelta = new Vector2(520, 560);
            var cardImg = card.GetComponent<Image>();
            if (cardImg != null)
            {
                cardImg.sprite = sqFilled;
                cardImg.type = Image.Type.Sliced;
                cardImg.color = ColorDarkBg;
            }

            AddCardBorder(card, crossFrame, ColorCardBorder);

            var title = card.Find("PopupTitle")?.GetComponent<TMP_Text>();
            if (title != null)
            {
                title.font = fontBold;
                title.fontSize = 24;
                title.characterSpacing = 2.0f;
                title.color = ColorWarzoneYellow;
                title.text = "OPERATOR AUTHENTICATION";
            }

            var subtitle = card.Find("PopupSubtitle")?.GetComponent<TMP_Text>();
            if (subtitle != null)
            {
                subtitle.font = fontSemi;
                subtitle.fontSize = 12;
                subtitle.color = ColorTextMuted;
                subtitle.text = "SIGN IN TO LOAD SQUAD PROGRESSION & BLUEPRINTS";
            }

            StyleInputField(card.Find("UsernameInput")?.GetComponent<TMP_InputField>(), fontBold, fontMed, sqFilled, "ENTER OPERATOR USERNAME...");
            StyleInputField(card.Find("PasswordInput")?.GetComponent<TMP_InputField>(), fontBold, fontMed, sqFilled, "ENTER SECURITY PASSCODE...");

            var loginBtn = card.Find("LoginButton")?.gameObject;
            if (loginBtn != null)
            {
                var lImg = loginBtn.GetComponent<Image>();
                if (lImg != null) { lImg.sprite = sqFilled; lImg.color = ColorWarzoneYellow; }
                var lTxt = loginBtn.transform.Find("Label")?.GetComponent<TMP_Text>();
                if (lTxt != null)
                {
                    lTxt.font = fontBold;
                    lTxt.fontSize = 16;
                    lTxt.color = new Color(0.05f, 0.07f, 0.09f, 1.0f);
                    lTxt.text = "AUTHENTICATE & DEPLOY";
                }
                EnsureTacticalHover(loginBtn, hoverSfx, clickSfx);
            }

            var regBtn = card.Find("RegisterButton")?.gameObject;
            if (regBtn != null)
            {
                var rImg = regBtn.GetComponent<Image>();
                if (rImg != null) { rImg.sprite = sqFilled; rImg.color = ColorButtonBg; }
                var rTxt = regBtn.transform.Find("Label")?.GetComponent<TMP_Text>();
                if (rTxt != null)
                {
                    rTxt.font = fontBold;
                    rTxt.fontSize = 14;
                    rTxt.color = ColorTextWhite;
                    rTxt.text = "CREATE NEW OPERATOR ACCOUNT";
                }
                EnsureTacticalHover(regBtn, hoverSfx, clickSfx);
            }

            var closeBtn = card.Find("CloseButton")?.gameObject;
            if (closeBtn != null)
            {
                EnsureTacticalHover(closeBtn, hoverSfx, clickSfx);
            }
        }

        private static void RestyleCreateAccountPopup(GameObject canvas, TMP_FontAsset fontBold, TMP_FontAsset fontSemi, TMP_FontAsset fontMed, Sprite sqFilled, Sprite crossFrame, AudioClip hoverSfx, AudioClip clickSfx)
        {
            var createObj = canvas.transform.Find("CreateAccountPopup")?.gameObject;
            if (createObj == null) return;
            var card = createObj.transform.Find("CreateAccountCard");
            if (card == null) return;

            var cardRt = card.GetComponent<RectTransform>();
            cardRt.sizeDelta = new Vector2(520, 620);
            var cardImg = card.GetComponent<Image>();
            if (cardImg != null)
            {
                cardImg.sprite = sqFilled;
                cardImg.type = Image.Type.Sliced;
                cardImg.color = ColorDarkBg;
            }

            AddCardBorder(card, crossFrame, ColorCardBorder);

            var title = card.Find("PopupTitle")?.GetComponent<TMP_Text>();
            if (title != null)
            {
                title.font = fontBold;
                title.fontSize = 24;
                title.characterSpacing = 2.0f;
                title.color = ColorWarzoneYellow;
                title.text = "NEW OPERATOR REGISTRATION";
            }

            var subtitle = card.Find("PopupSubtitle")?.GetComponent<TMP_Text>();
            if (subtitle != null)
            {
                subtitle.font = fontSemi;
                subtitle.fontSize = 12;
                subtitle.color = ColorTextMuted;
                subtitle.text = "REGISTER A PERSISTENT CALLSIGN ON THE IRONLINE NETWORK";
            }

            StyleInputField(card.Find("UsernameInput")?.GetComponent<TMP_InputField>(), fontBold, fontMed, sqFilled, "CHOOSE OPERATOR CALLSIGN...");
            StyleInputField(card.Find("PasswordInput")?.GetComponent<TMP_InputField>(), fontBold, fontMed, sqFilled, "CHOOSE SECURITY PASSWORD...");
            StyleInputField(card.Find("ConfirmPasswordInput")?.GetComponent<TMP_InputField>(), fontBold, fontMed, sqFilled, "CONFIRM SECURITY PASSWORD...");

            var createBtn = card.Find("CreateButton")?.gameObject;
            if (createBtn != null)
            {
                var cImg = createBtn.GetComponent<Image>();
                if (cImg != null) { cImg.sprite = sqFilled; cImg.color = ColorWarzoneYellow; }
                var cTxt = createBtn.transform.Find("Label")?.GetComponent<TMP_Text>();
                if (cTxt != null)
                {
                    cTxt.font = fontBold;
                    cTxt.fontSize = 16;
                    cTxt.color = new Color(0.05f, 0.07f, 0.09f, 1.0f);
                    cTxt.text = "COMPLETE REGISTRATION";
                }
                EnsureTacticalHover(createBtn, hoverSfx, clickSfx);
            }

            var closeBtn = card.Find("CloseButton")?.gameObject;
            if (closeBtn != null)
            {
                EnsureTacticalHover(closeBtn, hoverSfx, clickSfx);
            }
        }

        private static void RestyleErrorPopup(GameObject canvas, TMP_FontAsset fontBold, TMP_FontAsset fontSemi, TMP_FontAsset fontMed, Sprite sqFilled, Sprite crossFrame, AudioClip hoverSfx, AudioClip clickSfx)
        {
            var errObj = canvas.transform.Find("ErrorPopup")?.gameObject;
            if (errObj == null) return;
            var card = errObj.transform.Find("ErrorCard");
            if (card == null) return;

            var cardRt = card.GetComponent<RectTransform>();
            cardRt.sizeDelta = new Vector2(500, 280);
            var cardImg = card.GetComponent<Image>();
            if (cardImg != null)
            {
                cardImg.sprite = sqFilled;
                cardImg.type = Image.Type.Sliced;
                cardImg.color = ColorDarkBg;
            }

            AddCardBorder(card, crossFrame, ColorCardBorder);

            var title = card.Find("ErrorTitle")?.GetComponent<TMP_Text>();
            if (title != null)
            {
                title.font = fontBold;
                title.fontSize = 20;
                title.characterSpacing = 2.0f;
            }

            var msg = card.Find("ErrorMessage")?.GetComponent<TMP_Text>();
            if (msg != null)
            {
                msg.font = fontMed;
                msg.fontSize = 13.5f;
            }

            var okBtn = card.Find("OKButton")?.gameObject;
            if (okBtn != null)
            {
                var okTxt = okBtn.transform.Find("Label")?.GetComponent<TMP_Text>();
                if (okTxt != null) okTxt.font = fontBold;
                EnsureTacticalHover(okBtn, hoverSfx, clickSfx);
            }

            var cancelBtn = card.Find("CancelButton")?.gameObject;
            if (cancelBtn != null)
            {
                var cTxt = cancelBtn.transform.Find("Label")?.GetComponent<TMP_Text>();
                if (cTxt != null) cTxt.font = fontBold;
                EnsureTacticalHover(cancelBtn, hoverSfx, clickSfx);
            }
        }

        private static void StyleInputField(TMP_InputField input, TMP_FontAsset textFont, TMP_FontAsset phFont, Sprite bgSprite, string placeholderText)
        {
            if (input == null) return;

            var img = input.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = bgSprite;
                img.type = Image.Type.Sliced;
                img.color = new Color(0.08f, 0.11f, 0.16f, 0.95f);
            }

            var rt = input.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, 48);

            if (input.textComponent != null)
            {
                input.textComponent.font = textFont;
                input.textComponent.fontSize = 15;
                input.textComponent.color = ColorTextWhite;
            }

            if (input.placeholder is TMP_Text ph)
            {
                ph.font = phFont;
                ph.fontSize = 14;
                ph.color = ColorTextDim;
                if (!string.IsNullOrEmpty(placeholderText)) ph.text = placeholderText;
            }
        }

        private static void EnsureTacticalHover(GameObject btnObj, AudioClip hoverSfx, AudioClip clickSfx)
        {
            if (btnObj == null) return;
            var hover = btnObj.GetComponent<TacticalUIHoverEffect>();
            if (hover == null) hover = btnObj.AddComponent<TacticalUIHoverEffect>();
        }

        private static TextMeshProUGUI CreateTextChild(Transform parent, string name, TMP_FontAsset font, float size, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 sizeDelta)
        {
            var child = parent.Find(name)?.gameObject;
            if (child == null)
            {
                child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                child.transform.SetParent(parent, false);
            }
            var rt = child.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;

            var tmp = child.GetComponent<TextMeshProUGUI>();
            tmp.font = font;
            tmp.fontSize = size;
            tmp.color = color;
            return tmp;
        }
    }
}
