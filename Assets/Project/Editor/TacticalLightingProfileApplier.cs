using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Railbound.Editor
{
    public static class TacticalLightingProfileApplier
    {
        private const string ProfilePath = "Assets/all_Projects/Demos/Post Processing Profiles/PC DESKTOP(URP).asset";
        private const string ScenePath = "Assets/Project/MAP-1.unity";

        [MenuItem("Tools/Lighting/Apply Tactical Lighting Profile")]
        public static void ApplyTacticalLighting()
        {
            Debug.Log("[TacticalLighting] Starting Lighting & Post-Processing Upgrade...");

            // 1. Configure Post-Processing Profile
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                Debug.LogError($"[TacticalLighting] Failed to find profile at {ProfilePath}");
                return;
            }

            // Clean any dangling null components
            profile.components.RemoveAll(c => c == null);

            // --- 1.1 Tone Mapping & Exposure ---
            if (!profile.TryGet<Tonemapping>(out var tonemapping))
            {
                tonemapping = VolumeProfileFactory.CreateVolumeComponent<Tonemapping>(profile, true, false);
            }
            tonemapping.active = true;
            tonemapping.mode.overrideState = true;
            tonemapping.mode.value = TonemappingMode.ACES;
            Debug.Log("[TacticalLighting] Configured Tonemapping: ACES");

            if (!profile.TryGet<ColorAdjustments>(out var colorAdj))
            {
                colorAdj = VolumeProfileFactory.CreateVolumeComponent<ColorAdjustments>(profile, true, false);
            }
            colorAdj.active = true;
            colorAdj.postExposure.overrideState = true;
            colorAdj.postExposure.value = 0.65f;
            colorAdj.contrast.overrideState = true;
            colorAdj.contrast.value = 5f; // Soften harsh contrast from 15f down to 5f
            colorAdj.saturation.overrideState = true;
            colorAdj.saturation.value = 5f; // Grounded tactical saturation down from 20f
            Debug.Log("[TacticalLighting] Configured ColorAdjustments: PostExposure=0.65, Contrast=5, Saturation=5");

            // --- 1.2 Lift Crushed Blacks & Shadow Recovery ---
            if (!profile.TryGet<LiftGammaGain>(out var lgg))
            {
                lgg = VolumeProfileFactory.CreateVolumeComponent<LiftGammaGain>(profile, true, false);
            }
            lgg.active = true;

            // #111318 lift: RGB tint with cool slate/charcoal bias + offset 0.055 to lift deep black floor
            lgg.lift.overrideState = true;
            lgg.lift.value = new Vector4(0.95f, 0.98f, 1.05f, 0.055f);

            // Gamma midtones: gentle positive offset to soften transition between harsh sun and shadows
            lgg.gamma.overrideState = true;
            lgg.gamma.value = new Vector4(1.0f, 1.0f, 1.0f, 0.06f);

            // Gain: crisp controlled highlights
            lgg.gain.overrideState = true;
            lgg.gain.value = new Vector4(1.0f, 1.0f, 1.0f, 0.0f);
            Debug.Log("[TacticalLighting] Configured LiftGammaGain: Lift floor lifted towards #111318 slate/charcoal");

            // --- 1.3 Color Grading & Split-Toning ---
            if (!profile.TryGet<SplitToning>(out var splitToning))
            {
                splitToning = VolumeProfileFactory.CreateVolumeComponent<SplitToning>(profile, true, false);
            }
            splitToning.active = true;

            // Highlights: Amber/gold #FFD1A9 at ~13% intensity from neutral 0.5 grey
            Color amberTint = new Color(1.0f, 0.8196f, 0.6627f);
            splitToning.highlights.overrideState = true;
            splitToning.highlights.value = Color.Lerp(new Color(0.5f, 0.5f, 0.5f), amberTint, 0.13f);

            // Shadows: Military teal/slate #1A2530 at ~20% intensity from neutral 0.5 grey
            Color tealTint = new Color(0.102f, 0.145f, 0.1882f);
            splitToning.shadows.overrideState = true;
            splitToning.shadows.value = Color.Lerp(new Color(0.5f, 0.5f, 0.5f), tealTint, 0.20f);

            splitToning.balance.overrideState = true;
            splitToning.balance.value = -5f; // Slight shadow favor for cool forest/rock depth
            Debug.Log("[TacticalLighting] Configured SplitToning: Amber highlights + Military Teal shadows");

            // --- 1.4 Soften Vignette & Bloom ---
            if (profile.TryGet<Vignette>(out var vignette))
            {
                vignette.intensity.overrideState = true;
                vignette.intensity.value = 0.22f; // Soften vignette edge darkening
                vignette.smoothness.overrideState = true;
                vignette.smoothness.value = 0.35f;
            }

            if (profile.TryGet<Bloom>(out var bloom))
            {
                bloom.threshold.overrideState = true;
                bloom.threshold.value = 1.3f;
                bloom.intensity.overrideState = true;
                bloom.intensity.value = 1.0f; // Controlled bloom, no blinding flares
            }

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Debug.Log("[TacticalLighting] Saved VolumeProfile asset");

            // 2. Configure SSAO on Universal Renderer Data
            ConfigureRendererSSAO();

            // 3. Configure Scene Lighting in MAP-1.unity
            var scene = EditorSceneManager.OpenScene(ScenePath);
            Debug.Log($"[TacticalLighting] Opened Scene: {scene.name}");

            // Directional Light
            var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var light in lights)
            {
                if (light.type == LightType.Directional)
                {
                    light.shadows = LightShadows.Soft;
                    light.shadowStrength = 0.86f; // Lift contact shadow darkness from 1.0
                    light.bounceIntensity = 1.25f; // Richer dynamic bounce
                    light.intensity = 0.95f;
                    EditorUtility.SetDirty(light);
                    Debug.Log($"[TacticalLighting] Updated Directional Light: shadowStrength=0.86, bounce=1.25");
                }
            }

            // RenderSettings: Ambient illumination and bounce
            RenderSettings.ambientIntensity = 1.15f;
            RenderSettings.ambientSkyColor = new Color(0.38f, 0.43f, 0.50f, 1.0f);
            RenderSettings.ambientEquatorColor = new Color(0.26f, 0.28f, 0.30f, 1.0f);
            RenderSettings.ambientGroundColor = new Color(0.18f, 0.19f, 0.16f, 1.0f); // Warm terrain bounce
            RenderSettings.subtractiveShadowColor = new Color(0.42f, 0.45f, 0.48f, 1.0f);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[TacticalLighting] Saved MAP-1.unity scene successfully");

            Debug.Log("[TacticalLighting] All lighting and post-processing upgrades applied successfully!");
        }

        private static void ConfigureRendererSSAO()
        {
            Type ssaoType = typeof(UniversalRendererData).Assembly.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion");
            if (ssaoType == null)
            {
                Debug.LogWarning("[TacticalLighting] Could not find ScreenSpaceAmbientOcclusion type via reflection");
                return;
            }

            string[] rendererGuids = AssetDatabase.FindAssets("t:ScriptableRendererData");
            foreach (var guid in rendererGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(path);
                if (rendererData == null) continue;

                var serializedData = new SerializedObject(rendererData);
                var featuresProp = serializedData.FindProperty("m_RendererFeatures");
                var featureMapProp = serializedData.FindProperty("m_RendererFeatureMap");

                ScriptableRendererFeature ssaoFeature = null;
                for (int i = 0; i < featuresProp.arraySize; i++)
                {
                    var feat = featuresProp.GetArrayElementAtIndex(i).objectReferenceValue as ScriptableRendererFeature;
                    if (feat != null && feat.GetType() == ssaoType)
                    {
                        ssaoFeature = feat;
                        break;
                    }
                }

                if (ssaoFeature == null)
                {
                    ssaoFeature = ScriptableObject.CreateInstance(ssaoType) as ScriptableRendererFeature;
                    ssaoFeature.name = "ScreenSpaceAmbientOcclusion";
                    if (EditorUtility.IsPersistent(rendererData))
                    {
                        AssetDatabase.AddObjectToAsset(ssaoFeature, rendererData);
                    }
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ssaoFeature, out var _, out long localId);

                    featuresProp.arraySize++;
                    featuresProp.GetArrayElementAtIndex(featuresProp.arraySize - 1).objectReferenceValue = ssaoFeature;
                    featureMapProp.arraySize++;
                    featureMapProp.GetArrayElementAtIndex(featureMapProp.arraySize - 1).longValue = localId;
                }

                // Soften SSAO intensity and expand radius
                var serializedSSAO = new SerializedObject(ssaoFeature);
                var settingsProp = serializedSSAO.FindProperty("m_Settings");
                if (settingsProp != null)
                {
                    var intensityProp = settingsProp.FindPropertyRelative("Intensity");
                    if (intensityProp != null) intensityProp.floatValue = 0.9f; // Softened from harsh 3.0

                    var radiusProp = settingsProp.FindPropertyRelative("Radius");
                    if (radiusProp != null) radiusProp.floatValue = 0.28f; // Expanded radius from tight 0.035

                    var directLightingProp = settingsProp.FindPropertyRelative("DirectLightingStrength");
                    if (directLightingProp != null) directLightingProp.floatValue = 0.2f;

                    serializedSSAO.ApplyModifiedProperties();
                }

                serializedData.ApplyModifiedProperties();
                rendererData.SetDirty();
                EditorUtility.SetDirty(rendererData);
                Debug.Log($"[TacticalLighting] SSAO configured on {rendererData.name}: Intensity=0.9, Radius=0.28");
            }
            AssetDatabase.SaveAssets();
        }
    }
}
