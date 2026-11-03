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

            if (fontBold == null || fontSemi == null || fontMed == null ||
                bgSprite == null || intelSprite == null || vShadow == null || hShadow == null ||
                sqFilled == null || crossFrame == null)
            {
                Debug.LogError("[LoginMenuRestyler] Required Reach UI assets not found. Restyle aborted.");
                return;
            }

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

            // rest of original method remains unchanged...
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
            bImg.pixelsPerUnitMultiplier = 3.2f;
            bImg.color = borderColor;
            bImg.raycastTarget = false;
        }

        // Rest of the file remains unchanged...
    }
}
