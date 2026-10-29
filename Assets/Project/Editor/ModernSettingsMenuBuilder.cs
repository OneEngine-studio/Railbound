#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Michsky.UI.Reach
{
    [InitializeOnLoad]
    public class ModernSettingsMenuBuilder : MonoBehaviour
    {
        private static bool hasExecuted = false;

        static ModernSettingsMenuBuilder()
        {
            EditorApplication.delayCall += () =>
            {
                if (!hasExecuted && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    hasExecuted = true;
                    BuildMenu();
                }
            };
        }

        [MenuItem("Tools/Build Modern Settings in MAP-1")]
        public static void BuildMenu()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (!activeScene.name.Contains("MAP-1"))
            {
                EditorSceneManager.OpenScene("Assets/Project/MAP-1.unity");
            }

            // 1. Locate Canvas - Pause Menu
            var canvas = Resources.FindObjectsOfTypeAll<GameObject>()
                .FirstOrDefault(go => go.name == "Canvas - Pause Menu" && go.scene.name == "MAP-1");

            if (canvas == null)
            {
                Debug.LogError("[SettingsBuilder] Canvas - Pause Menu not found in MAP-1!");
                return;
            }

            // Set Reach UI background color to deep black
            var uim = AssetDatabase.LoadAssetAtPath<UIManager>("Assets/Reach - Complete Sci-Fi UI/Resources/Reach UI Manager.asset");
            if (uim != null)
            {
                uim.backgroundColor = new Color(0.015f, 0.015f, 0.02f, 1f);
                EditorUtility.SetDirty(uim);
            }

            // Update all blue/navy background images in canvas to black
            var targetCol = new Color(0.098f, 0.137f, 0.176f);
            var canvasImages = canvas.GetComponentsInChildren<Image>(true);
            foreach (var img in canvasImages)
            {
                if (Mathf.Abs(img.color.r - targetCol.r) < 0.03f &&
                    Mathf.Abs(img.color.g - targetCol.g) < 0.03f &&
                    Mathf.Abs(img.color.b - targetCol.b) < 0.03f)
                {
                    img.color = new Color(0.015f, 0.015f, 0.02f, img.color.a);
                    EditorUtility.SetDirty(img);
                }
            }

            // 2. Locate Graphics Manager
            var gmObj = canvas.transform.Find("Graphics Manager")?.gameObject;
            if (gmObj == null)
            {
                Debug.LogError("[SettingsBuilder] Graphics Manager GameObject not found!");
                return;
            }

            var gm = gmObj.GetComponent<GraphicsManager>();
            if (gm == null)
            {
                Debug.LogError("[SettingsBuilder] GraphicsManager component missing!");
                return;
            }

            // Populate references
            var vol = GameObject.Find("Post Processing Volume")?.GetComponent<UnityEngine.Rendering.Volume>();
            var cam = Camera.main;
            var gmSO = new SerializedObject(gm);
            gmSO.FindProperty("postProcessingVolume").objectReferenceValue = vol;
            gmSO.FindProperty("mainCamera").objectReferenceValue = cam;
            gmSO.ApplyModifiedProperties();
            EditorUtility.SetDirty(gm);

            // 3. Locate Layout Group
            var layoutGroupTransform = canvas.transform.Find("Pause Menu/Settings/Content/Panel Content/Panels/Visuals/Content/List/Layout Group");
            if (layoutGroupTransform == null)
            {
                Debug.LogError("[SettingsBuilder] Layout Group not found!");
                return;
            }

            // Locate Description Area
            var descArea = canvas.transform.Find("Pause Menu/Settings/Content/Panel Content/Description Area");

            // Load Prefabs
            var headerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Reach - Complete Sci-Fi UI/Prefabs/UI Elements/Settings/Settings Header.prefab");
            var switchPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Reach - Complete Sci-Fi UI/Prefabs/UI Elements/Settings/Settings Element (Switch).prefab");
            var sliderPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Reach - Complete Sci-Fi UI/Prefabs/UI Elements/Settings/Settings Element (Slider).prefab");
            var hsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Reach - Complete Sci-Fi UI/Prefabs/UI Elements/Settings/Settings Element (Horizontal Selector).prefab");

            // Locate Description Manager
            var descManager = canvas.GetComponentInChildren<SettingsDescriptionManager>(true);

            // Clean up previous generated items
            var existingItemsToRemove = new List<string> {
                "Brightness",
                "Upscaling & Anti-Aliasing Header",
                "Anti-Aliasing",
                "Render Scale",
                "AMD FSR Upscaling",
                "Super Resolution",
                "FSR Sharpness",
                "Super Resolution Sharpness",
                "Mesh Quality",
                "VRAM Scale Target",
                "Async Texture Streaming",
                "Shadows & Lighting Header",
                "Ambient Occlusion (SSAO)",
                "Shadow Quality",
                "Shadow Distance",
                "Post Processing Header",
                "Motion Blur",
                "Bloom",
                "Depth of Field",
                "Film Grain",
                "Hardware Header",
                "Async CPU-GPU Upload",
                "Camera & View Header",
                "Field of View"
            };

            for (int i = layoutGroupTransform.childCount - 1; i >= 0; i--)
            {
                var child = layoutGroupTransform.GetChild(i);
                if (existingItemsToRemove.Contains(child.name) || child.name.StartsWith("Custom_Spacer"))
                {
                    DestroyImmediate(child.gameObject);
                }
            }

            // -------------------------------------------------------------
            // Helper Lambdas
            // -------------------------------------------------------------
            GameObject CreateHeader(string name, string text)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(headerPrefab, layoutGroupTransform);
                go.name = name;
                var tmp = go.GetComponent<TextMeshProUGUI>();
                if (tmp != null) tmp.text = text;
                var loc = go.GetComponent<LocalizedObject>();
                if (loc != null) DestroyImmediate(loc);
                return go;
            }

            GameObject CreateSpacer(string name)
            {
                var spacer = new GameObject(name, typeof(RectTransform));
                spacer.transform.SetParent(layoutGroupTransform, false);
                var rt = spacer.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(1195.23f, 15f);
                return spacer;
            }

            GameObject CreateSwitch(string name, string title, string description, string saveKey, bool defaultVal, UnityAction<bool> action)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(switchPrefab, layoutGroupTransform);
                go.name = name;

                var textObj = go.transform.Find("Text");
                if (textObj != null)
                {
                    var tmp = textObj.GetComponent<TextMeshProUGUI>();
                    if (tmp != null) tmp.text = title;
                    var loc = textObj.GetComponent<LocalizedObject>();
                    if (loc != null) DestroyImmediate(loc);
                }

                var desc = go.GetComponent<SettingsDescription>();
                if (desc != null)
                {
                    desc.manager = descManager;
                    var descSO = new SerializedObject(desc);
                    descSO.FindProperty("title").stringValue = title;
                    descSO.FindProperty("description").stringValue = description;
                    descSO.FindProperty("titleKey").stringValue = "";
                    descSO.FindProperty("descriptionKey").stringValue = "";
                    descSO.ApplyModifiedProperties();
                }

                var sm = go.GetComponentInChildren<SwitchManager>(true);
                if (sm != null)
                {
                    sm.saveKey = saveKey;
                    sm.saveValue = true;
                    sm.isOn = defaultVal;
                    UnityEventTools.AddPersistentListener(sm.onValueChanged, action);
                }

                return go;
            }

            GameObject CreateSlider(string name, string title, string description, string saveKey, float minVal, float maxVal, bool wholeNumbers, float defaultVal, UnityAction<float> action)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(sliderPrefab, layoutGroupTransform);
                go.name = name;

                var textObj = go.transform.Find("Text");
                if (textObj != null)
                {
                    var tmp = textObj.GetComponent<TextMeshProUGUI>();
                    if (tmp != null) tmp.text = title;
                    var loc = textObj.GetComponent<LocalizedObject>();
                    if (loc != null) DestroyImmediate(loc);
                }

                var desc = go.GetComponent<SettingsDescription>();
                if (desc != null)
                {
                    desc.manager = descManager;
                    var descSO = new SerializedObject(desc);
                    descSO.FindProperty("title").stringValue = title;
                    descSO.FindProperty("description").stringValue = description;
                    descSO.FindProperty("titleKey").stringValue = "";
                    descSO.FindProperty("descriptionKey").stringValue = "";
                    descSO.ApplyModifiedProperties();
                }

                var slider = go.GetComponentInChildren<Slider>(true);
                if (slider != null)
                {
                    slider.minValue = minVal;
                    slider.maxValue = maxVal;
                    slider.wholeNumbers = wholeNumbers;
                    slider.value = defaultVal;
                }

                var sm = go.GetComponentInChildren<SliderManager>(true);
                if (sm != null)
                {
                    sm.saveKey = saveKey;
                    sm.saveValue = true;
                    UnityEventTools.AddPersistentListener(sm.onValueChanged, action);
                }

                return go;
            }

            GameObject CreateHorizontalSelector(string name, string title, string description, string saveKey, int defaultIndex, (string label, int arg)[] items, UnityAction<int> action)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(hsPrefab, layoutGroupTransform);
                go.name = name;

                var textObj = go.transform.Find("Text");
                if (textObj != null)
                {
                    var tmp = textObj.GetComponent<TextMeshProUGUI>();
                    if (tmp != null) tmp.text = title;
                    var loc = textObj.GetComponent<LocalizedObject>();
                    if (loc != null) DestroyImmediate(loc);
                }

                var desc = go.GetComponent<SettingsDescription>();
                if (desc != null)
                {
                    desc.manager = descManager;
                    var descSO = new SerializedObject(desc);
                    descSO.FindProperty("title").stringValue = title;
                    descSO.FindProperty("description").stringValue = description;
                    descSO.FindProperty("titleKey").stringValue = "";
                    descSO.FindProperty("descriptionKey").stringValue = "";
                    descSO.ApplyModifiedProperties();
                }

                var hs = go.GetComponentInChildren<HorizontalSelector>(true);
                if (hs != null)
                {
                    hs.saveKey = saveKey;
                    hs.saveSelected = true;
                    hs.useLocalization = false;
                    hs.items.Clear();

                    for (int i = 0; i < items.Length; i++)
                    {
                        var it = new HorizontalSelector.Item
                        {
                            itemTitle = items[i].label,
                            localizationKey = ""
                        };
                        UnityEventTools.AddIntPersistentListener(it.onItemSelect, action, items[i].arg);
                        hs.items.Add(it);
                    }

                    hs.defaultIndex = defaultIndex;
                    hs.index = defaultIndex;
                }

                return go;
            }

            // -------------------------------------------------------------
            // BUILD THE FULL MODERN WARFARE & UNIVERSAL SETTINGS
            // -------------------------------------------------------------

            // Section 1: DISPLAY
            CreateSlider("Brightness", "BRIGHTNESS", "Adjusts the overall display exposure and screen gamma.", "Brightness", 0f, 1f, false, 0.5f, gm.SetBrightness);

            // Section 2: UPSCALING & ANTI-ALIASING
            CreateHeader("Upscaling & Anti-Aliasing Header", "UPSCALING & ANTI-ALIASING");
            CreateHorizontalSelector("Anti-Aliasing", "ANTI-ALIASING", "Reduces jagged edges. SMAA or TAA is recommended for crisp visuals.", "AntiAliasing", 2,
                new (string, int)[] { ("Off", 0), ("FXAA", 1), ("SMAA", 2), ("TAA", 3) }, gm.SetAntiAliasing);
            CreateHorizontalSelector("Render Scale", "RENDER RESOLUTION", "Internal 3D render scale percentage. Lower for higher FPS, raise for supersampling.", "RenderScale", 2,
                new (string, int)[] { ("50%", 0), ("75%", 1), ("100%", 2), ("125%", 3) }, gm.SetRenderScale);
            CreateSwitch("Super Resolution", "SUPER RESOLUTION (SPATIAL)", "Universal high-performance spatial upscaler compatible with all NVIDIA, AMD, and Intel GPUs. Renders at lower resolution and reconstructs sharp high-res frames.", "FSR", false, gm.SetFSR);
            CreateSlider("Super Resolution Sharpness", "SUPER RESOLUTION SHARPNESS", "Controls edge sharpening filter when Super Resolution upscaling is active.", "FSRSharpness", 0f, 1f, false, 0.6f, gm.SetFSRSharpness);

            // Section 3: TEXTURES & DETAILS
            CreateHorizontalSelector("Mesh Quality", "DETAIL & MESH QUALITY (LOD)", "Controls 3D mesh level-of-detail and draw distances across the map.", "MeshQuality", 2,
                new (string, int)[] { ("Low", 0), ("Medium", 1), ("High", 2), ("Ultra", 3) }, gm.SetMeshQuality);
            CreateHorizontalSelector("VRAM Scale Target", "VRAM SCALE TARGET", "Limits GPU texture streaming budget (70% to 90%) to prevent VRAM overflow, stuttering, and memory thrashing.", "VRAMTarget", 1,
                new (string, int)[] { ("70% (Safe)", 0), ("80% (Balanced)", 1), ("90% (Maximum)", 2), ("Unlimited", 3) }, gm.SetVRAMScaleTarget);
            CreateSwitch("Async Texture Streaming", "TEXTURE STREAMING", "Dynamically loads high-res mipmaps on demand based on camera distance to reduce GPU VRAM consumption.", "TextureStreaming", true, gm.SetTextureStreaming);

            // Section 4: SHADOWS & LIGHTING
            CreateSpacer("Custom_Spacer_2");
            CreateHeader("Shadows & Lighting Header", "SHADOWS & LIGHTING");
            CreateSwitch("Ambient Occlusion (SSAO)", "AMBIENT OCCLUSION (SSAO)", "Calculates realistic contact shadows and depth shading around geometry crevices and corners.", "SSAO", true, gm.SetAmbientOcclusion);
            CreateHorizontalSelector("Shadow Quality", "SHADOW MAP RESOLUTION", "Resolution of main directional light shadows.", "ShadowQuality", 2,
                new (string, int)[] { ("Low (512)", 0), ("Med (1024)", 1), ("High (2048)", 2), ("Ultra (4096)", 3) }, gm.SetShadowQuality);
            CreateHorizontalSelector("Shadow Distance", "SHADOW DISTANCE", "Maximum render distance for real-time shadows.", "ShadowDistance", 2,
                new (string, int)[] { ("Near (50m)", 0), ("Medium (100m)", 1), ("Far (200m)", 2), ("Ultra (320m)", 3) }, gm.SetShadowDistance);

            // Section 5: POST PROCESSING
            CreateSpacer("Custom_Spacer_3");
            CreateHeader("Post Processing Header", "POST PROCESSING");
            CreateSwitch("Motion Blur", "CAMERA MOTION BLUR", "Simulates realistic motion blur during fast camera turns and movements.", "MotionBlur", true, gm.SetMotionBlur);
            CreateSwitch("Bloom", "BLOOM", "Simulates realistic light bleeding and glow from bright emissive surfaces.", "Bloom", true, gm.SetBloom);
            CreateSwitch("Depth of Field", "DEPTH OF FIELD", "Blurs objects outside the camera focus distance.", "DepthOfField", false, gm.SetDepthOfField);
            CreateSlider("Film Grain", "FILM GRAIN", "Adds cinematic film grain texture to the screen.", "FilmGrain", 0f, 1f, false, 0f, gm.SetFilmGrain);

            // Section 6: CPU & HARDWARE
            CreateSpacer("Custom_Spacer_4");
            CreateHeader("Hardware Header", "CPU & SYSTEM PERFORMANCE");
            CreateSwitch("Async CPU-GPU Upload", "ASYNC CPU-GPU UPLOAD", "Dispatches texture and geometry buffer uploads across background CPU threads to eliminate main thread hitches.", "AsyncUpload", true, gm.SetAsyncUpload);

            // Section 7: CAMERA & VIEW
            CreateSpacer("Custom_Spacer_5");
            CreateHeader("Camera & View Header", "VIEW & CAMERA");
            CreateSlider("Field of View", "FIELD OF VIEW (FOV)", "Adjusts camera horizontal viewing angle (60° to 110°).", "FOV", 60f, 110f, true, 70f, gm.SetFieldOfView);

            // -------------------------------------------------------------
            // RE-ORDER ELEMENTS TO MATCH MODERN WARFARE ORDER
            // -------------------------------------------------------------
            var vsyncTransform = layoutGroupTransform.Find("VSync");
            var brightnessTransform = layoutGroupTransform.Find("Brightness");
            if (vsyncTransform != null && brightnessTransform != null)
            {
                brightnessTransform.SetSiblingIndex(vsyncTransform.GetSiblingIndex() + 1);
            }

            var advHeaderTransform = layoutGroupTransform.Find("Advanced Header");
            if (advHeaderTransform != null)
            {
                int advIndex = advHeaderTransform.GetSiblingIndex();
                var upscalingItems = new string[] {
                    "Upscaling & Anti-Aliasing Header",
                    "Anti-Aliasing",
                    "Render Scale",
                    "Super Resolution",
                    "Super Resolution Sharpness"
                };

                for (int u = 0; u < upscalingItems.Length; u++)
                {
                    var itemT = layoutGroupTransform.Find(upscalingItems[u]);
                    if (itemT != null)
                    {
                        itemT.SetSiblingIndex(advIndex + u);
                    }
                }
            }

            // Move Texture & Detail additions right under Texture Quality
            var tqTransform = layoutGroupTransform.Find("Texture Quality");
            if (tqTransform != null)
            {
                int tqIndex = tqTransform.GetSiblingIndex();
                var detailItems = new string[] {
                    "Mesh Quality",
                    "VRAM Scale Target",
                    "Async Texture Streaming"
                };

                for (int d = 0; d < detailItems.Length; d++)
                {
                    var itemT = layoutGroupTransform.Find(detailItems[d]);
                    if (itemT != null)
                    {
                        itemT.SetSiblingIndex(tqIndex + 1 + d);
                    }
                }
            }

            // -------------------------------------------------------------
            // BUILD THE EXACT MODERN WARFARE VRAM WIDGET
            // -------------------------------------------------------------
            BuildVRAMWidget(descArea);

            // Force layout rebuild
            LayoutRebuilder.ForceRebuildLayoutImmediate(layoutGroupTransform.GetComponent<RectTransform>());

            // Mark dirty & save scene
            EditorUtility.SetDirty(canvas);
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Debug.Log("<color=green><b>[SettingsBuilder]</b> Modern Warfare VRAM UI & Black Background successfully built in MAP-1!</color>");
        }

        private static void BuildVRAMWidget(Transform descArea)
        {
            if (descArea == null) return;

            // Remove existing VRAM widget if present
            var oldWidget = descArea.Find("VRAM Widget");
            if (oldWidget != null) DestroyImmediate(oldWidget.gameObject);

            // Get font from Title
            var titleTmp = descArea.Find("Title")?.GetComponent<TextMeshProUGUI>();
            var fontAsset = titleTmp != null ? titleTmp.font : null;

            // Adjust Description offset so it doesn't overlap the VRAM widget
            var descObj = descArea.Find("Description");
            if (descObj != null)
            {
                var descRt = descObj.GetComponent<RectTransform>();
                descRt.offsetMin = new Vector2(descRt.offsetMin.x, 115f);
            }

            // 1. Root Container (Floating seamlessly without box borders)
            var widgetGo = new GameObject("VRAM Widget", typeof(RectTransform));
            widgetGo.transform.SetParent(descArea, false);
            var widgetRt = widgetGo.GetComponent<RectTransform>();
            widgetRt.anchorMin = new Vector2(0f, 0f);
            widgetRt.anchorMax = new Vector2(1f, 0f);
            widgetRt.pivot = new Vector2(0.5f, 0f);
            widgetRt.anchoredPosition = new Vector2(0f, 15f);
            widgetRt.sizeDelta = new Vector2(-60f, 85f);

            // 2. Row 1: Estimated VRAM Usage (Left)
            var estimatedGo = new GameObject("Estimated Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            estimatedGo.transform.SetParent(widgetGo.transform, false);
            var estimatedRt = estimatedGo.GetComponent<RectTransform>();
            estimatedRt.anchorMin = new Vector2(0f, 1f);
            estimatedRt.anchorMax = new Vector2(0.5f, 1f);
            estimatedRt.pivot = new Vector2(0f, 1f);
            estimatedRt.anchoredPosition = new Vector2(0f, 0f);
            estimatedRt.sizeDelta = new Vector2(250f, 18f);

            var estimatedTmp = estimatedGo.GetComponent<TextMeshProUGUI>();
            if (fontAsset != null) estimatedTmp.font = fontAsset;
            estimatedTmp.text = "ESTIMATED VRAM USAGE:";
            estimatedTmp.fontSize = 13f;
            estimatedTmp.fontStyle = FontStyles.Bold;
            estimatedTmp.color = new Color(0.85f, 0.88f, 0.9f, 1f);

            // Target Label (Positioned at 80% mark above the tick)
            var targetGo = new GameObject("Target Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            targetGo.transform.SetParent(widgetGo.transform, false);
            var targetRt = targetGo.GetComponent<RectTransform>();
            targetRt.anchorMin = new Vector2(0f, 1f);
            targetRt.anchorMax = new Vector2(0f, 1f);
            targetRt.pivot = new Vector2(0.5f, 1f);
            targetRt.anchoredPosition = new Vector2(456f, 0f); // 80% of 570px
            targetRt.sizeDelta = new Vector2(60f, 16f);

            var targetTmp = targetGo.GetComponent<TextMeshProUGUI>();
            if (fontAsset != null) targetTmp.font = fontAsset;
            targetTmp.text = "TARGET";
            targetTmp.fontSize = 11f;
            targetTmp.alignment = TextAlignmentOptions.Center;
            targetTmp.color = new Color(0.85f, 0.88f, 0.9f, 1f);

            // 3. Row 2: The Segmented Bar Container
            var barContainerGo = new GameObject("Bar Container", typeof(RectTransform));
            barContainerGo.transform.SetParent(widgetGo.transform, false);
            var barContainerRt = barContainerGo.GetComponent<RectTransform>();
            barContainerRt.anchorMin = new Vector2(0f, 1f);
            barContainerRt.anchorMax = new Vector2(1f, 1f);
            barContainerRt.pivot = new Vector2(0f, 1f);
            barContainerRt.anchoredPosition = new Vector2(0f, -22f);
            barContainerRt.sizeDelta = new Vector2(0f, 12f);

            // Background Track (Dark segmented ticks)
            var barBgGo = new GameObject("Bar Background", typeof(RectTransform), typeof(Image));
            barBgGo.transform.SetParent(barContainerGo.transform, false);
            var barBgRt = barBgGo.GetComponent<RectTransform>();
            barBgRt.anchorMin = Vector2.zero;
            barBgRt.anchorMax = Vector2.one;
            barBgRt.offsetMin = Vector2.zero;
            barBgRt.offsetMax = Vector2.zero;
            var barBgImg = barBgGo.GetComponent<Image>();
            barBgImg.color = new Color(0.13f, 0.16f, 0.13f, 1f);

            // Other Apps Fill Bar (Light grey segmented ticks)
            var otherAppsGo = new GameObject("Other Apps Fill", typeof(RectTransform), typeof(Image));
            otherAppsGo.transform.SetParent(barContainerGo.transform, false);
            var otherAppsRt = otherAppsGo.GetComponent<RectTransform>();
            otherAppsRt.anchorMin = Vector2.zero;
            otherAppsRt.anchorMax = Vector2.one;
            otherAppsRt.offsetMin = Vector2.zero;
            otherAppsRt.offsetMax = Vector2.zero;
            var otherAppsImg = otherAppsGo.GetComponent<Image>();
            otherAppsImg.type = Image.Type.Filled;
            otherAppsImg.fillMethod = Image.FillMethod.Horizontal;
            otherAppsImg.fillAmount = 0.28f;
            otherAppsImg.color = new Color(0.85f, 0.85f, 0.85f, 1f);

            // Game Usage Fill Bar (Tactical lime green segmented ticks)
            var gameUsageGo = new GameObject("Game Usage Fill", typeof(RectTransform), typeof(Image));
            gameUsageGo.transform.SetParent(barContainerGo.transform, false);
            var gameUsageRt = gameUsageGo.GetComponent<RectTransform>();
            gameUsageRt.anchorMin = Vector2.zero;
            gameUsageRt.anchorMax = Vector2.one;
            gameUsageRt.offsetMin = Vector2.zero;
            gameUsageRt.offsetMax = Vector2.zero;
            var gameUsageImg = gameUsageGo.GetComponent<Image>();
            gameUsageImg.type = Image.Type.Filled;
            gameUsageImg.fillMethod = Image.FillMethod.Horizontal;
            gameUsageImg.fillAmount = 0.20f;
            gameUsageImg.color = new Color(0.58f, 0.88f, 0.22f, 1f);

            // Target Vertical Tick Line
            var targetLineGo = new GameObject("Target Line", typeof(RectTransform), typeof(Image));
            targetLineGo.transform.SetParent(barContainerGo.transform, false);
            var targetLineRt = targetLineGo.GetComponent<RectTransform>();
            targetLineRt.anchorMin = new Vector2(0f, 0.5f);
            targetLineRt.anchorMax = new Vector2(0f, 0.5f);
            targetLineRt.pivot = new Vector2(0.5f, 0.5f);
            targetLineRt.anchoredPosition = new Vector2(456f, 0f);
            targetLineRt.sizeDelta = new Vector2(2f, 20f);
            var targetLineImg = targetLineGo.GetComponent<Image>();
            targetLineImg.color = Color.white;

            // Right End Cap Bracket Line
            var rightCapGo = new GameObject("Right Cap Line", typeof(RectTransform), typeof(Image));
            rightCapGo.transform.SetParent(barContainerGo.transform, false);
            var rightCapRt = rightCapGo.GetComponent<RectTransform>();
            rightCapRt.anchorMin = new Vector2(1f, 0.5f);
            rightCapRt.anchorMax = new Vector2(1f, 0.5f);
            rightCapRt.pivot = new Vector2(1f, 0.5f);
            rightCapRt.anchoredPosition = new Vector2(0f, 0f);
            rightCapRt.sizeDelta = new Vector2(2f, 14f);
            var rightCapImg = rightCapGo.GetComponent<Image>();
            rightCapImg.color = Color.white;

            // 4. Row 3: Legend & Totals (Bottom)
            var legendContainerGo = new GameObject("Legend Container", typeof(RectTransform));
            legendContainerGo.transform.SetParent(widgetGo.transform, false);
            var legendContainerRt = legendContainerGo.GetComponent<RectTransform>();
            legendContainerRt.anchorMin = new Vector2(0f, 0f);
            legendContainerRt.anchorMax = new Vector2(1f, 0f);
            legendContainerRt.pivot = new Vector2(0f, 0f);
            legendContainerRt.anchoredPosition = new Vector2(0f, 2f);
            legendContainerRt.sizeDelta = new Vector2(0f, 20f);

            // Game Legend (Left)
            var gameLegGo = new GameObject("Game Legend", typeof(RectTransform), typeof(TextMeshProUGUI));
            gameLegGo.transform.SetParent(legendContainerGo.transform, false);
            var gameLegRt = gameLegGo.GetComponent<RectTransform>();
            gameLegRt.anchorMin = new Vector2(0f, 0f);
            gameLegRt.anchorMax = new Vector2(0.48f, 1f);
            gameLegRt.pivot = new Vector2(0f, 0.5f);
            gameLegRt.offsetMin = Vector2.zero;
            gameLegRt.offsetMax = Vector2.zero;
            var gameLegTmp = gameLegGo.GetComponent<TextMeshProUGUI>();
            if (fontAsset != null) gameLegTmp.font = fontAsset;
            gameLegTmp.fontSize = 12f;
            gameLegTmp.alignment = TextAlignmentOptions.Left;
            gameLegTmp.text = "<color=#94E038>▮▮▮</color> MODERN WARFARE 4:  <color=#94E038>8.04 GB</color>";

            // Other Apps Legend (Center)
            var otherLegGo = new GameObject("Other Apps Legend", typeof(RectTransform), typeof(TextMeshProUGUI));
            otherLegGo.transform.SetParent(legendContainerGo.transform, false);
            var otherLegRt = otherLegGo.GetComponent<RectTransform>();
            otherLegRt.anchorMin = new Vector2(0.48f, 0f);
            otherLegRt.anchorMax = new Vector2(0.78f, 1f);
            otherLegRt.pivot = new Vector2(0f, 0.5f);
            otherLegRt.offsetMin = Vector2.zero;
            otherLegRt.offsetMax = Vector2.zero;
            var otherLegTmp = otherLegGo.GetComponent<TextMeshProUGUI>();
            if (fontAsset != null) otherLegTmp.font = fontAsset;
            otherLegTmp.fontSize = 12f;
            otherLegTmp.alignment = TextAlignmentOptions.Left;
            otherLegTmp.text = "<color=#D9D9D9>▮▮▮</color> OTHER APPS:  1.39 GB";

            // Total Usage Text (Right)
            var totalGo = new GameObject("Total Usage", typeof(RectTransform), typeof(TextMeshProUGUI));
            totalGo.transform.SetParent(legendContainerGo.transform, false);
            var totalRt = totalGo.GetComponent<RectTransform>();
            totalRt.anchorMin = new Vector2(0.78f, 0f);
            totalRt.anchorMax = new Vector2(1f, 1f);
            totalRt.pivot = new Vector2(1f, 0.5f);
            totalRt.offsetMin = Vector2.zero;
            totalRt.offsetMax = Vector2.zero;
            var totalTmp = totalGo.GetComponent<TextMeshProUGUI>();
            if (fontAsset != null) totalTmp.font = fontAsset;
            totalTmp.fontSize = 13f;
            totalTmp.fontStyle = FontStyles.Bold;
            totalTmp.alignment = TextAlignmentOptions.Right;
            totalTmp.color = Color.white;
            totalTmp.text = "9.44/23.57 GB";

            // 5. Attach HardwareVRAMMonitor
            var monitor = widgetGo.AddComponent<HardwareVRAMMonitor>();
            monitor.gameTitle = "MODERN WARFARE 4";
            monitor.barBackground = barBgImg;
            monitor.otherAppsFillBar = otherAppsImg;
            monitor.gameUsageFillBar = gameUsageImg;
            monitor.targetLine = targetLineRt;
            monitor.targetLabel = targetRt;
            monitor.rightCapLine = rightCapRt;
            monitor.estimatedTitleText = estimatedTmp;
            monitor.gameLegendText = gameLegTmp;
            monitor.otherAppsLegendText = otherLegTmp;
            monitor.totalUsageText = totalTmp;

            monitor.EnsureSegmentedSprite();
            monitor.UpdateVRAMStats();
        }
    }
}
#endif
