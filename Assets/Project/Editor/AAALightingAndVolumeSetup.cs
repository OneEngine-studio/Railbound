#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Project.Editor
{
    [InitializeOnLoad]
    public static class AAALightingAndVolumeSetup
    {
        private static bool _hasExecuted = false;

        static AAALightingAndVolumeSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (!_hasExecuted && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    _hasExecuted = true;
                    ApplyAAAGraphicsAndLighting();
                }
            };
        }

        [MenuItem("Tools/Railbound/Apply AAA Graphics & Lighting to MAP-BR")]
        public static void ApplyAAAGraphicsAndLighting()
        {
            Debug.Log("<color=cyan><b>[AAALighting]</b> Starting AAA Lighting, Post-Processing & Volume Setup...</color>");

            // 1. Upgrade HDRP Pipeline Assets
            UpgradeHDRPAssets();

            // 2. Open MAP-BR scene if not active
            var activeScene = EditorSceneManager.GetActiveScene();
            if (!activeScene.name.Contains("MAP-BR"))
            {
                activeScene = EditorSceneManager.OpenScene("Assets/TerrainDemoScene_HDRP/Scenes/MAP-BR.unity");
            }

            // 3. Clean up obsolete and duplicate empty volumes
            CleanOldVolumes();

            // 4. Setup AAA Environment Root with Handcrafted HDRP Volumes
            SetupAAAVolumes();

            // 5. Setup Reflection Probes
            SetupReflectionProbes();

            // 6. Calibrate Sun & Moon Direct Lighting
            SetupDirectionalLights();

            // 7. Calibrate Main Camera Post-Processing & TAA
            SetupCamera();

            // 8. Save dirty assets and scene
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();

            Debug.Log("<color=green><b>[AAALighting]</b> AAA Lighting, Dynamic Ambient Fill, Warfare Post-Processing & Shaders successfully applied and saved!</color>");
        }

        private static void UpgradeHDRPAssets()
        {
            string[] assetPaths = new string[]
            {
                "Assets/TerrainDemoScene_HDRP/Settings/HDRP/HDRP High Quality.asset",
                "Assets/TerrainDemoScene_HDRP/Settings/HDRP/HDRP Ultra Quality.asset",
                "Assets/TerrainDemoScene_HDRP/Settings/HDRP/HDRP Screenshot Quality.asset",
                "Assets/TerrainDemoScene_HDRP/Settings/HDRP/HDRP Medium Quality.asset"
            };

            foreach (var path in assetPaths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(path);
                if (asset == null) continue;

                var so = new SerializedObject(asset);
                var rps = so.FindProperty("m_RenderPipelineSettings");
                if (rps == null) continue;

                SetBool(rps, "supportSSR", true);
                SetBool(rps, "supportSSGI", true);
                SetBool(rps, "supportSSAO", true);
                SetBool(rps, "supportVolumetrics", true);
                SetBool(rps, "supportVolumetricClouds", true);
                SetBool(rps, "supportDecals", true);
                SetBool(rps, "supportDecalLayers", true);
                SetBool(rps, "supportSurfaceGradient", true);
                SetBool(rps, "supportSubsurfaceScattering", true);
                SetBool(rps, "supportDistortion", true);

                var shadows = rps.FindPropertyRelative("hdShadowInitParams");
                if (shadows != null)
                {
                    SetBool(shadows, "supportScreenSpaceShadows", true);
                    var dirRes = shadows.FindPropertyRelative("maxDirectionalShadowMapResolution");
                    if (dirRes != null) dirRes.intValue = 4096;
                }

                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(asset);
            }
        }

        private static void SetBool(SerializedProperty parent, string name, bool value)
        {
            var p = parent.FindPropertyRelative(name);
            if (p != null) p.boolValue = value;
        }

        private static void CleanOldVolumes()
        {
            var volumes = Object.FindObjectsByType<Volume>(FindObjectsSortMode.None);
            var toDestroy = new List<GameObject>();
            foreach (var v in volumes)
            {
                if (v == null || v.gameObject == null) continue;
                string name = v.gameObject.name;
                if (name.Contains("HDRP Global Environment Volume") || name == "Post Processing Volume")
                {
                    if (!toDestroy.Contains(v.gameObject))
                    {
                        toDestroy.Add(v.gameObject);
                    }
                }
            }

            foreach (var go in toDestroy)
            {
                Undo.DestroyObjectImmediate(go);
            }
            if (toDestroy.Count > 0)
            {
                Debug.Log($"[AAALighting] Removed {toDestroy.Count} legacy/empty volume GameObjects.");
            }
        }

        private static void SetupAAAVolumes()
        {
            var envRoot = GameObject.Find("[Environment] AAA Lighting & Volumes");
            if (envRoot == null)
            {
                envRoot = new GameObject("[Environment] AAA Lighting & Volumes");
                Undo.RegisterCreatedObjectUndo(envRoot, "Create AAA Environment Root");
            }

            string[] volumePrefabs = new string[]
            {
                "Assets/TerrainDemoScene_HDRP/Prefabs/Volumes/GlobalVolume.prefab",
                "Assets/TerrainDemoScene_HDRP/Prefabs/Volumes/SkyFogVolume.prefab",
                "Assets/TerrainDemoScene_HDRP/Prefabs/Volumes/RayTracingVolume.prefab"
            };

            foreach (var path in volumePrefabs)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                var existing = envRoot.transform.Find(prefab.name);
                GameObject instance = null;
                if (existing == null)
                {
                    instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, envRoot.transform);
                }
                else
                {
                    instance = existing.gameObject;
                }

                var vol = instance.GetComponent<Volume>();
                if (vol != null)
                {
                    vol.isGlobal = true;
                    vol.weight = 1.0f;

                    if (prefab.name == "GlobalVolume" && vol.sharedProfile != null)
                    {
                        ApplyMasterVolumeSettings(vol.sharedProfile);
                    }
                    else if (prefab.name == "SkyFogVolume" && vol.sharedProfile != null)
                    {
                        ApplySkyFogVolumeSettings(vol.sharedProfile);
                    }
                }
            }
        }

        public static void ApplySkyFogVolumeSettings(VolumeProfile profile)
        {
            // Set dynamic sky ambient mode so the sky actively illuminates shadows
            if (profile.TryGet<VisualEnvironment>(out var ve))
            {
                ve.skyAmbientMode.overrideState = true;
                ve.skyAmbientMode.value = SkyAmbientMode.Dynamic;
            }

            if (profile.TryGet<PhysicallyBasedSky>(out var pbs))
            {
                pbs.exposure.overrideState = true;
                pbs.exposure.value = 0.5f;
                pbs.multiplier.overrideState = true;
                pbs.multiplier.value = 1.5f;
                pbs.updateMode.overrideState = true;
                pbs.updateMode.value = EnvironmentUpdateMode.OnChanged;
            }

            EditorUtility.SetDirty(profile);
        }

        public static void ApplyMasterVolumeSettings(VolumeProfile profile)
        {
            // 1. Screen Space Ambient Occlusion (Warfare: Harsh, grounded realism)
            if (profile.TryGet<ScreenSpaceAmbientOcclusion>(out var ssao))
            {
                ssao.active = true;
                ssao.intensity.overrideState = true;
                ssao.intensity.value = 1.1f;
                ssao.radius.overrideState = true;
                ssao.radius.value = 1.35f;
                ssao.directLightingStrength.overrideState = true;
                ssao.directLightingStrength.value = 0.45f;
                ssao.temporalAccumulation.overrideState = true;
                ssao.temporalAccumulation.value = true;
                ssao.quality.overrideState = true;
                ssao.quality.value = 2; // High (Blue Noise)
            }

            // 2. Tonemapping (Filmic ACES response)
            if (profile.TryGet<Tonemapping>(out var tone))
            {
                tone.active = true;
                tone.mode.overrideState = true;
                tone.mode.value = TonemappingMode.ACES;
            }

            // 3. Color Adjustments (Tactical MW: Contrast 16, Saturation -8, Exposure 0.08)
            if (!profile.TryGet<ColorAdjustments>(out var ca))
            {
                ca = profile.Add<ColorAdjustments>();
            }
            ca.active = true;
            ca.contrast.overrideState = true;
            ca.contrast.value = 16f;
            ca.saturation.overrideState = true;
            ca.saturation.value = -8f;
            ca.postExposure.overrideState = true;
            ca.postExposure.value = 0.08f;

            // 4. Shadows, Midtones, Highlights & LiftGammaGain
            // Shadows (Lift): X: -0.05, Y: -0.03, Z: 0.01 (Deep, dark, cold undergrowths)
            // Midtones (Gamma): X: 0.03, Y: 0.04, Z: -0.01 (Rich earth and mud tones)
            if (profile.TryGet<ShadowsMidtonesHighlights>(out var smh))
            {
                smh.active = true;
                smh.shadows.overrideState = true;
                smh.shadows.value = new Vector4(0.95f, 0.97f, 1.01f, -0.05f);
                smh.midtones.overrideState = true;
                smh.midtones.value = new Vector4(1.03f, 1.04f, 0.99f, 0.0f);
            }

            if (profile.TryGet<LiftGammaGain>(out var lgg))
            {
                lgg.active = true;
                lgg.lift.overrideState = true;
                lgg.lift.value = new Vector4(0.95f, 0.97f, 1.01f, -0.05f);
                lgg.gamma.overrideState = true;
                lgg.gamma.value = new Vector4(1.03f, 1.04f, 0.99f, 0.0f);
            }

            // 5. Indirect Lighting Controller (Boosts shadow fill and indirect bounce)
            if (profile.TryGet<IndirectLightingController>(out var ilc))
            {
                ilc.active = true;
                ilc.indirectDiffuseLightingMultiplier.overrideState = true;
                ilc.indirectDiffuseLightingMultiplier.value = 2.5f;
                ilc.reflectionLightingMultiplier.overrideState = true;
                ilc.reflectionLightingMultiplier.value = 1.5f;
                ilc.reflectionProbeIntensityMultiplier.overrideState = true;
                ilc.reflectionProbeIntensityMultiplier.value = 1.5f;
            }

            // 6. Exposure Adaptation
            if (profile.TryGet<Exposure>(out var exp))
            {
                exp.active = true;
                exp.limitMin.overrideState = true;
                exp.limitMin.value = 7.0f; // Allows eye to adapt into dark forests/shadows
                exp.limitMax.overrideState = true;
                exp.limitMax.value = 14.5f;
                exp.compensation.overrideState = true;
                exp.compensation.value = 0.35f;
            }

            // 7. Bloom & Light Glint (Warfare: Intensity 0.12, Threshold 0.96, Scatter 0.55)
            if (profile.TryGet<Bloom>(out var bloom))
            {
                bloom.active = true;
                bloom.intensity.overrideState = true;
                bloom.intensity.value = 0.12f;
                bloom.threshold.overrideState = true;
                bloom.threshold.value = 0.96f;
                bloom.scatter.overrideState = true;
                bloom.scatter.value = 0.55f;
            }

            // 8. Motion Blur (Warfare: Quality High, Intensity 0.40)
            if (profile.TryGet<MotionBlur>(out var mb))
            {
                mb.active = true;
                mb.quality.overrideState = true;
                mb.quality.value = 2; // High
                mb.intensity.overrideState = true;
                mb.intensity.value = 0.40f;
            }

            EditorUtility.SetDirty(profile);
        }

        private static void SetupReflectionProbes()
        {
            var envRoot = GameObject.Find("[Environment] AAA Lighting & Volumes");
            if (envRoot == null) return;

            string probePrefabPath = "Assets/TerrainDemoScene_HDRP/Prefabs/Lighting/GlobalReflectionProbes.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(probePrefabPath);
            if (prefab != null)
            {
                var existing = envRoot.transform.Find(prefab.name);
                if (existing == null)
                {
                    PrefabUtility.InstantiatePrefab(prefab, envRoot.transform);
                    Debug.Log("[AAALighting] Instantiated GlobalReflectionProbes for seamless map reflections.");
                }
            }
        }

        private static void SetupDirectionalLights()
        {
            // Configure Sun
            var sunGo = GameObject.Find("Sun");
            if (sunGo != null)
            {
                var l = sunGo.GetComponent<Light>();
                if (l != null)
                {
                    l.type = LightType.Directional;
                    l.shadows = LightShadows.Soft;
                    l.useColorTemperature = true;
                    l.colorTemperature = 5600f;
                    l.intensity = 120000f;
                }

                var hdSun = sunGo.GetComponent<HDAdditionalLightData>();
                if (hdSun == null) hdSun = sunGo.AddComponent<HDAdditionalLightData>();

                hdSun.lightUnit = LightUnit.Lux;
                hdSun.intensity = 120000f;
                hdSun.shadowDimmer = 0.78f; // Soft ambient shadow fill to prevent pitch black voids
                hdSun.volumetricShadowDimmer = 0.85f;
                hdSun.useContactShadow.useOverride = true;
                hdSun.useContactShadow.@override = true;
                hdSun.shadowResolution.level = 3;
                EditorUtility.SetDirty(sunGo);
            }

            // Configure Moon if present
            var moonGo = GameObject.Find("Moon");
            if (moonGo != null)
            {
                var ml = moonGo.GetComponent<Light>();
                if (ml != null)
                {
                    ml.type = LightType.Directional;
                    ml.shadows = LightShadows.Soft;
                    ml.useColorTemperature = true;
                    ml.colorTemperature = 7500f;
                    ml.intensity = 1.2f;
                }

                var hdMoon = moonGo.GetComponent<HDAdditionalLightData>();
                if (hdMoon == null) hdMoon = moonGo.AddComponent<HDAdditionalLightData>();

                hdMoon.lightUnit = LightUnit.Lux;
                hdMoon.intensity = 1.2f;
                hdMoon.shadowDimmer = 0.78f;
                hdMoon.volumetricShadowDimmer = 0.85f;
                hdMoon.shadowResolution.level = 2;
                EditorUtility.SetDirty(moonGo);
            }

            // Also synchronize CycleManager instance if active in scene
            var dnc = Object.FindAnyObjectByType<CycleManager>();
            if (dnc != null)
            {
                dnc.sunIntensity = 120000f;
                dnc.moonIntensity = 1.2f;
                dnc.sunShadowDimmer = 0.78f;
                dnc.moonShadowDimmer = 0.78f;
                EditorUtility.SetDirty(dnc);
            }
        }

        private static void SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
                foreach (var c in cams)
                {
                    if (c.gameObject.name == "Main Camera")
                    {
                        cam = c;
                        break;
                    }
                }
            }

            if (cam != null)
            {
                var hdCam = cam.GetComponent<HDAdditionalCameraData>();
                if (hdCam == null) hdCam = cam.gameObject.AddComponent<HDAdditionalCameraData>();

                hdCam.antialiasing = HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing;
                hdCam.dithering = true;
                hdCam.volumeLayerMask = -1;
                hdCam.probeLayerMask = -1;
                hdCam.stopNaNs = true;

                EditorUtility.SetDirty(cam.gameObject);
                Debug.Log("[AAALighting] Main Camera configured with Temporal Anti-Aliasing (TAA) and Dithering.");
            }
        }
    }
}
#endif
