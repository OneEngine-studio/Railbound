using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Project.Editor
{
    /// <summary>
    /// Automated project-wide converter to migrate Railbound from URP to HDRP.
    /// Converts Render Pipeline Assets, Quality Settings, Materials, Terrain Layers,
    /// Lights, Cameras, and Scene Volumes.
    /// </summary>
    public static class HDRPProjectConverter
    {
        private const string HdrpHighAssetPath = "Assets/TerrainDemoScene_HDRP/Settings/HDRP/HDRP High Quality.asset";
        private const string HdrpUltraAssetPath = "Assets/TerrainDemoScene_HDRP/Settings/HDRP/HDRP Ultra Quality.asset";
        private const string HdrpMediumAssetPath = "Assets/TerrainDemoScene_HDRP/Settings/HDRP/HDRP Medium Quality.asset";
        private const string HdrpLowAssetPath = "Assets/TerrainDemoScene_HDRP/Settings/HDRP/HDRP Low Quality.asset";
        private const string HdrpGlobalSettingsPath = "Assets/TerrainDemoScene_HDRP/Settings/HDRP/HDRenderPipelineGlobalSettings.asset";

        [MenuItem("Tools/Railbound/Convert Project to HDRP")]
        public static void ConvertWholeProjectToHDRP()
        {
            Debug.Log("[HDRPConverter] Starting full project conversion to HDRP...");

            int pipelineUpdated = SetupRenderPipelineAssets();
            int materialsConverted = ConvertAllMaterials();
            int terrainLayersConverted = ConvertTerrainLayers();
            int sceneObjectsUpgraded = UpgradeActiveScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[HDRPConverter] Complete! Updated {pipelineUpdated} pipeline settings, converted {materialsConverted} materials, {terrainLayersConverted} terrain layers, and {sceneObjectsUpgraded} scene objects.");
        }

        public static int SetupRenderPipelineAssets()
        {
            int changes = 0;
            var highAsset = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(HdrpHighAssetPath);
            var ultraAsset = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(HdrpUltraAssetPath);
            var medAsset = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(HdrpMediumAssetPath);
            var lowAsset = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(HdrpLowAssetPath);

            if (highAsset == null)
            {
                Debug.LogError($"[HDRPConverter] Could not load HDRP High asset at {HdrpHighAssetPath}");
                return 0;
            }

            // 1. GraphicsSettings
            GraphicsSettings.defaultRenderPipeline = highAsset;
            changes++;

            // 2. QualitySettings
            int qualityCount = QualitySettings.names.Length;
            for (int i = 0; i < qualityCount; i++)
            {
                string name = QualitySettings.names[i].ToLower();
                HDRenderPipelineAsset target = highAsset;
                if (name.Contains("ultra") && ultraAsset != null) target = ultraAsset;
                else if (name.Contains("low") && lowAsset != null) target = lowAsset;
                else if (name.Contains("med") && medAsset != null) target = medAsset;

                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = target;
                changes++;
            }

            // Set current level back to High/Ultra
            QualitySettings.SetQualityLevel(Math.Min(qualityCount - 1, 3), true);

            Debug.Log("[HDRPConverter] Assigned HDRP assets to GraphicsSettings and QualitySettings.");
            return changes;
        }

        public static int ConvertAllMaterials()
        {
            Shader hdrpLit = Shader.Find("HDRP/Lit");
            Shader hdrpUnlit = Shader.Find("HDRP/Unlit");
            Shader hdrpTerrain = Shader.Find("HDRP/TerrainLit");

            if (hdrpLit == null)
            {
                Debug.LogError("[HDRPConverter] HDRP/Lit shader not found. Ensure HDRP package is installed.");
                return 0;
            }

            string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
            int count = 0;

            for (int i = 0; i < matGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(matGuids[i]);
                if (path.StartsWith("Assets/TerrainDemoScene_HDRP"))
                    continue; // Already HDRP

                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null || mat.shader == null) continue;

                string sName = mat.shader.name;
                bool isUrpLit = sName.Contains("Universal Render Pipeline/Lit") || sName.Contains("Universal Render Pipeline/Simple Lit");
                bool isStandard = sName == "Standard" || sName == "Standard (Specular setup)";
                bool isUrpUnlit = sName.Contains("Universal Render Pipeline/Unlit");
                bool isTerrainLit = sName.Contains("Terrain/Lit") || sName == "Nature/Terrain/Standard";

                if (isUrpLit || isStandard)
                {
                    Texture baseMap = mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : (mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null);
                    Color baseColor = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : (mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white);
                    Texture normalMap = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
                    float normalScale = mat.HasProperty("_BumpScale") ? mat.GetFloat("_BumpScale") : 1.0f;
                    Texture metallicMap = mat.HasProperty("_MetallicGlossMap") ? mat.GetTexture("_MetallicGlossMap") : null;
                    float metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0.0f;
                    float smoothness = mat.HasProperty("_Smoothness") ? mat.GetFloat("_Smoothness") : (mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : 0.5f);
                    Texture emissionMap = mat.HasProperty("_EmissionMap") ? mat.GetTexture("_EmissionMap") : null;
                    Color emissionColor = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;
                    Vector2 mainScale = mat.HasProperty("_BaseMap") ? mat.GetTextureScale("_BaseMap") : (mat.HasProperty("_MainTex") ? mat.GetTextureScale("_MainTex") : Vector2.one);
                    Vector2 mainOffset = mat.HasProperty("_BaseMap") ? mat.GetTextureOffset("_BaseMap") : (mat.HasProperty("_MainTex") ? mat.GetTextureOffset("_MainTex") : Vector2.zero);

                    mat.shader = hdrpLit;

                    // Assign HDRP properties
                    if (baseMap != null)
                    {
                        mat.SetTexture("_BaseColorMap", baseMap);
                        mat.SetTextureScale("_BaseColorMap", mainScale);
                        mat.SetTextureOffset("_BaseColorMap", mainOffset);
                    }
                    mat.SetColor("_BaseColor", baseColor);

                    if (normalMap != null)
                    {
                        mat.SetTexture("_NormalMap", normalMap);
                        mat.SetFloat("_NormalScale", normalScale);
                        mat.EnableKeyword("_NORMALMAP");
                    }

                    if (metallicMap != null)
                    {
                        mat.SetTexture("_MaskMap", metallicMap);
                    }
                    mat.SetFloat("_Metallic", metallic);
                    mat.SetFloat("_Smoothness", smoothness);

                    if (emissionMap != null || (emissionColor.maxColorComponent > 0.001f))
                    {
                        if (emissionMap != null) mat.SetTexture("_EmissiveColorMap", emissionMap);
                        mat.SetColor("_EmissiveColor", emissionColor);
                        mat.SetFloat("_EmissiveIntensity", 1.0f);
                        mat.EnableKeyword("_EMISSION");
                    }

                    EditorUtility.SetDirty(mat);
                    count++;
                }
                else if (isUrpUnlit && hdrpUnlit != null)
                {
                    Texture baseMap = mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : (mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null);
                    Color color = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : (mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white);

                    mat.shader = hdrpUnlit;
                    if (baseMap != null) mat.SetTexture("_UnlitColorMap", baseMap);
                    mat.SetColor("_UnlitColor", color);

                    EditorUtility.SetDirty(mat);
                    count++;
                }
                else if (isTerrainLit && hdrpTerrain != null)
                {
                    mat.shader = hdrpTerrain;
                    EditorUtility.SetDirty(mat);
                    count++;
                }
            }

            Debug.Log($"[HDRPConverter] Converted {count} materials to HDRP shaders.");
            return count;
        }

        public static int ConvertTerrainLayers()
        {
            string[] layerGuids = AssetDatabase.FindAssets("t:TerrainLayer", new[] { "Assets" });
            int count = 0;
            foreach (var guid in layerGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
                if (layer != null)
                {
                    // Ensure normal scale is valid
                    if (layer.normalMapTexture != null && layer.normalScale <= 0.01f)
                    {
                        layer.normalScale = 1.0f;
                    }
                    EditorUtility.SetDirty(layer);
                    count++;
                }
            }
            return count;
        }

        public static int UpgradeActiveScene()
        {
            int count = 0;

            // 1. Upgrade Cameras
            Camera[] cameras = UnityEngine.Object.FindObjectsOfType<Camera>(true);
            foreach (var cam in cameras)
            {
                if (cam.gameObject.GetComponent<HDAdditionalCameraData>() == null)
                {
                    var hdCam = cam.gameObject.AddComponent<HDAdditionalCameraData>();
                    hdCam.clearColorMode = HDAdditionalCameraData.ClearColorMode.Sky;
                    EditorUtility.SetDirty(cam.gameObject);
                    count++;
                }
            }

            // 2. Upgrade Lights
            Light[] lights = UnityEngine.Object.FindObjectsOfType<Light>(true);
            foreach (var light in lights)
            {
                HDAdditionalLightData hdLight = light.gameObject.GetComponent<HDAdditionalLightData>();
                if (hdLight == null)
                {
                    hdLight = light.gameObject.AddComponent<HDAdditionalLightData>();
                    count++;
                }

                if (light.type == LightType.Directional)
                {
                    // Directional Sunlight in HDRP needs physical lux (typically 20,000 - 50,000 Lux for daylight)
                    if (light.intensity < 100f)
                    {
                        light.intensity = 25000f; // 25,000 Lux daylight
                    }
                    hdLight.lightUnit = LightUnit.Lux;
                }
                else if (light.type == LightType.Point || light.type == LightType.Spot)
                {
                    if (light.intensity < 10f)
                    {
                        light.intensity = 800f; // 800 Lumens
                    }
                    hdLight.lightUnit = LightUnit.Lumen;
                }

                EditorUtility.SetDirty(light.gameObject);
            }

            // 3. Ensure Global HDRP Sky & Post Volume exists
            Volume[] volumes = UnityEngine.Object.FindObjectsOfType<Volume>(true);
            bool hasGlobalSkyVolume = false;
            foreach (var v in volumes)
            {
                if (v.isGlobal && v.sharedProfile != null)
                {
                    if (v.sharedProfile.Has<VisualEnvironment>() || v.sharedProfile.Has<PhysicallyBasedSky>())
                    {
                        hasGlobalSkyVolume = true;
                        break;
                    }
                }
            }

            if (!hasGlobalSkyVolume)
            {
                GameObject skyVolObj = new GameObject("HDRP Global Environment Volume");
                var vol = skyVolObj.AddComponent<Volume>();
                vol.isGlobal = true;
                vol.priority = 1;

                var profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "HDRP_Default_Environment_Profile";

                // Visual Environment
                var visualEnv = profile.Add<VisualEnvironment>(true);
                visualEnv.skyType.overrideState = true;
                visualEnv.skyType.value = (int)SkyType.PhysicallyBased;

                // Physically Based Sky
                var sky = profile.Add<PhysicallyBasedSky>(true);
                sky.planetaryRadius.overrideState = true;

                // Exposure
                var exposure = profile.Add<Exposure>(true);
                exposure.mode.overrideState = true;
                exposure.mode.value = ExposureMode.Fixed;
                exposure.fixedExposure.overrideState = true;
                exposure.fixedExposure.value = 12.5f;

                // Bloom
                var bloom = profile.Add<Bloom>(true);
                bloom.intensity.overrideState = true;
                bloom.intensity.value = 0.25f;

                // Tonemapping
                var tonemapping = profile.Add<Tonemapping>(true);
                tonemapping.mode.overrideState = true;
                tonemapping.mode.value = TonemappingMode.ACES;

                AssetDatabase.CreateAsset(profile, "Assets/Project/HDRP_Global_Environment_Profile.asset");
                vol.sharedProfile = profile;

                EditorUtility.SetDirty(skyVolObj);
                count++;
                Debug.Log("[HDRPConverter] Created HDRP Global Environment Volume with Physically Based Sky & Exposure.");
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            return count;
        }
    }
}
