using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using HD = UnityEngine.Rendering.HighDefinition;

namespace Michsky.UI.Reach
{
    public class GraphicsManager : MonoBehaviour
    {
        // Resources
        [SerializeField] private Dropdown resolutionDropdown;

        // Settings
        [SerializeField] private bool initializeResolutions = true;

        [Header("URP & Camera References")]
        [SerializeField] private Volume postProcessingVolume;
        [SerializeField] private Camera mainCamera;

        // Helpers
        Resolution[] resolutions;
        private UniversalRenderPipelineAsset urpAsset;
        private ScriptableRendererFeature ssaoFeature;

        public enum TextureOption { FullRes, HalfRes, QuarterRes, EighthResh }
        public enum AnisotropicOption { None, PerTexture, ForcedOn }

        void Awake()
        {
            InitReferences();

            if (initializeResolutions == true && resolutionDropdown != null)
            {
                InitializeResolutions();
            }

            LoadAndApplyAllSettings();
        }

        private void InitReferences()
        {
            urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            if (postProcessingVolume == null)
            {
                var volObj = GameObject.Find("Post Processing Volume");
                if (volObj != null)
                {
                    postProcessingVolume = volObj.GetComponent<Volume>();
                }
                else
                {
                    postProcessingVolume = FindObjectsOfType<Volume>().FirstOrDefault(v => v.isGlobal);
                }
            }

            FindSSAOFeature();
        }

        private void FindSSAOFeature()
        {
            if (urpAsset == null) return;

            var prop = typeof(UniversalRenderPipelineAsset).GetProperty("scriptableRendererData",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var rendererData = prop?.GetValue(urpAsset) as ScriptableRendererData;

            if (rendererData != null && rendererData.rendererFeatures != null)
            {
                ssaoFeature = rendererData.rendererFeatures
                    .FirstOrDefault(f => f != null && f.name.Contains("ScreenSpaceAmbientOcclusion"));
            }
        }

        public void InitializeResolutions()
        {
            resolutions = Screen.resolutions;
            resolutionDropdown.items.Clear();

            int currentResolutionIndex = 0;

            for (int i = 0; i < resolutions.Length; i++)
            {
                int index = i;
                string option = resolutions[i].width + "x" + resolutions[i].height;

                resolutionDropdown.CreateNewItem(option, false);
                resolutionDropdown.items[i].onItemSelection.AddListener(delegate { SetResolution(index); });

#if UNITY_2022_2_OR_NEWER
#if !UNITY_EDITOR
                if (resolutions[i].refreshRateRatio.numerator != Screen.currentResolution.refreshRateRatio.numerator) { resolutionDropdown.items[i].isInvisible = true; }
#endif
                if (resolutions[i].width == Screen.currentResolution.width
                    && resolutions[i].height == Screen.currentResolution.height
                    && resolutions[i].refreshRateRatio.numerator == Screen.currentResolution.refreshRateRatio.numerator)
                {
                    currentResolutionIndex = index;
                }
#else
#if !UNITY_EDITOR
                if (resolutions[i].refreshRate != Screen.currentResolution.refreshRate) { resolutionDropdown.items[i].isInvisible = true; }
#endif
                if (resolutions[i].width == Screen.currentResolution.width
                    && resolutions[i].height == Screen.currentResolution.height
                    && resolutions[i].refreshRate == Screen.currentResolution.refreshRate)
                {
                    currentResolutionIndex = index;
                }
#endif
            }

            resolutionDropdown.selectedItemIndex = currentResolutionIndex;
            resolutionDropdown.Initialize();
        }

        public void SetResolution(int resolutionIndex)
        {
#if !UNITY_EDITOR
            if (resolutions != null && resolutionIndex >= 0 && resolutionIndex < resolutions.Length)
            {
                Screen.SetResolution(resolutions[resolutionIndex].width, resolutions[resolutionIndex].height, Screen.fullScreen);
            }
#endif
        }

        public void SetVSync(bool value)
        {
            QualitySettings.vSyncCount = value ? 1 : 0;
            PlayerPrefs.SetInt("Graphics_VSync", value ? 1 : 0);
        }

        public void SetFrameRate(int value)
        {
            Application.targetFrameRate = value;
            PlayerPrefs.SetInt("Graphics_FrameRate", value);
        }

        public void SetWindowFullscreen()
        {
            Screen.fullScreen = true;
            Screen.fullScreenMode = FullScreenMode.ExclusiveFullScreen;
            PlayerPrefs.SetInt("Graphics_WindowMode", 1);
        }

        public void SetWindowBorderless()
        {
            Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerPrefs.SetInt("Graphics_WindowMode", 0);
        }

        public void SetWindowWindowed()
        {
            Screen.fullScreen = false;
            Screen.fullScreenMode = FullScreenMode.Windowed;
            PlayerPrefs.SetInt("Graphics_WindowMode", 2);
        }

        public void SetTextureQuality(TextureOption option)
        {
#if UNITY_2022_2_OR_NEWER
            if (option == TextureOption.FullRes) { QualitySettings.globalTextureMipmapLimit = 0; }
            else if (option == TextureOption.HalfRes) { QualitySettings.globalTextureMipmapLimit = 1; }
            else if (option == TextureOption.QuarterRes) { QualitySettings.globalTextureMipmapLimit = 2; }
            else if (option == TextureOption.EighthResh) { QualitySettings.globalTextureMipmapLimit = 3; }
#else
            if (option == TextureOption.FullRes) { QualitySettings.masterTextureLimit = 0; }
            else if (option == TextureOption.HalfRes) { QualitySettings.masterTextureLimit = 1; }
            else if (option == TextureOption.QuarterRes) { QualitySettings.masterTextureLimit = 2; }
            else if (option == TextureOption.EighthResh) { QualitySettings.masterTextureLimit = 3; }
#endif
        }

        public void SetTextureQuality(int index)
        {
            if (index == 0) { SetTextureQuality(TextureOption.FullRes); }
            else if (index == 1) { SetTextureQuality(TextureOption.HalfRes); }
            else if (index == 2) { SetTextureQuality(TextureOption.QuarterRes); }
            else if (index == 3) { SetTextureQuality(TextureOption.EighthResh); }
            PlayerPrefs.SetInt("Graphics_TextureQuality", index);
        }

        public void SetAnisotropicFiltering(AnisotropicOption option)
        {
            if (option == AnisotropicOption.ForcedOn) { QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable; }
            else if (option == AnisotropicOption.PerTexture) { QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable; }
            else if (option == AnisotropicOption.None) { QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable; }
        }

        public void SetAnisotropicFiltering(int index)
        {
            if (index == 2) { SetAnisotropicFiltering(AnisotropicOption.ForcedOn); }
            else if (index == 1) { SetAnisotropicFiltering(AnisotropicOption.PerTexture); }
            else if (index == 0) { SetAnisotropicFiltering(AnisotropicOption.None); }
            PlayerPrefs.SetInt("Graphics_AnisotropicFiltering", index);
        }

        // ==========================================
        // MODERN GRAPHICS & URP EXTENSIONS (OPTION 1)
        // ==========================================

        // ==========================================
        // HDRP SUPPORT: the pause-menu Visuals options drive the map's own HDRP volumes / camera
        // ==========================================

        private static bool IsHDRP => GraphicsSettings.currentRenderPipeline is HD.HDRenderPipelineAsset;

        private float renderScalePercent = 100f;
        private bool dynResScalerSet;

        /// <summary>
        /// Volume.profile clones the shared profile; that throws when one of its components has been destroyed (it did on every map start,
        /// from SliderManager.Awake). A volume like that is skipped instead of aborting the whole settings pass.
        /// </summary>
        private static VolumeProfile SafeProfile(Volume v)
        {
            try { return v.profile; }
            catch (Exception) { return null; }
        }

        /// <summary>Runtime copies of every global volume's profile in the loaded map(s); the saved assets are never touched.</summary>
        private List<VolumeProfile> HdProfiles()
        {
            var list = new List<VolumeProfile>();
            foreach (var v in FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (v == null || !v.isGlobal || v.sharedProfile == null) continue;
                var p = SafeProfile(v);
                if (p != null) list.Add(p);
            }
            return list;
        }

        private void HdEach<T>(Action<T> apply) where T : VolumeComponent
        {
            foreach (var p in HdProfiles())
                if (p.TryGet<T>(out var c) && c != null) apply(c);
        }

        private HD.HDAdditionalCameraData HdCamera()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null) return null;
            var d = mainCamera.GetComponent<HD.HDAdditionalCameraData>();
            if (d == null) d = mainCamera.gameObject.AddComponent<HD.HDAdditionalCameraData>();
            return d;
        }

        // ---------------- exposure ----------------
        public void SetExposureAutomatic() { ApplyExposureMode(HD.ExposureMode.AutomaticHistogram, 0); }
        public void SetExposureFixed() { ApplyExposureMode(HD.ExposureMode.Fixed, 1); }

        private void ApplyExposureMode(HD.ExposureMode mode, int saved)
        {
            PlayerPrefs.SetInt("Graphics_ExposureMode", saved);
            if (!IsHDRP) return;
            HdEach<HD.Exposure>(e => e.mode.Override(mode));
        }

        /// <summary>0..1 slider, 0.5 = the map's own exposure compensation (+/- 2 EV).</summary>
        public void SetExposureCompensation(float value01)
        {
            PlayerPrefs.SetFloat("Graphics_ExposureComp", value01);
            if (!IsHDRP) return;
            float offset = (Mathf.Clamp01(value01) - 0.5f) * 4f;
            foreach (var v in FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (v == null || !v.isGlobal || v.sharedProfile == null) continue;
                if (v.sharedProfile.TryGet<HD.Exposure>(out var authored) && SafeProfile(v) is VolumeProfile liveProfile && liveProfile.TryGet<HD.Exposure>(out var live))
                    live.compensation.Override(authored.compensation.value + offset);
            }
        }

        /// <summary>0 = slow, 1 = normal (the map's own), 2 = fast eye adaptation for automatic exposure.</summary>
        public void SetExposureAdaptation(int index)
        {
            PlayerPrefs.SetInt("Graphics_ExposureAdapt", index);
            if (!IsHDRP) return;
            float k = index <= 0 ? 0.4f : index == 1 ? 1f : 2.5f;
            foreach (var v in FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (v == null || !v.isGlobal || v.sharedProfile == null) continue;
                if (v.sharedProfile.TryGet<HD.Exposure>(out var authored) && SafeProfile(v) is VolumeProfile liveProfile && liveProfile.TryGet<HD.Exposure>(out var live))
                {
                    live.adaptationMode.Override(HD.AdaptationMode.Progressive);
                    live.adaptationSpeedDarkToLight.Override(authored.adaptationSpeedDarkToLight.value * k);
                    live.adaptationSpeedLightToDark.Override(authored.adaptationSpeedLightToDark.value * k);
                }
            }
        }

        // ---------------- NVIDIA DLSS ----------------
        /// <summary>True when the GPU / driver can run DLSS (an NVIDIA RTX card with a recent driver).</summary>
        public static bool IsDLSSSupported => IsHDRP && HD.HDDynamicResolutionPlatformCapabilities.DLSSDetected;

        public void SetDLSS(bool enabled)
        {
            PlayerPrefs.SetInt("Graphics_DLSS", enabled ? 1 : 0);
            if (!IsHDRP) return;
            var hd = HdCamera();
            if (hd == null) return;
            hd.allowDeepLearningSuperSampling = enabled;
            if (enabled) hd.allowDynamicResolution = true;
        }

        // index: 0 Ultra Performance, 1 Performance, 2 Balanced, 3 Quality, 4 DLAA (native resolution anti-aliasing)
        private static readonly uint[] DlssQualityMap = { 3u, 0u, 1u, 2u, 4u };

        public void SetDLSSQuality(int index)
        {
            PlayerPrefs.SetInt("Graphics_DLSSQuality", index);
            if (!IsHDRP) return;
            var hd = HdCamera();
            if (hd == null) return;
            hd.deepLearningSuperSamplingUseCustomQualitySettings = true;
            hd.deepLearningSuperSamplingQuality = DlssQualityMap[Mathf.Clamp(index, 0, DlssQualityMap.Length - 1)];
            hd.deepLearningSuperSamplingUseOptimalSettings = true;
        }

        public void SetVolumetricFog(bool enabled)
        {
            PlayerPrefs.SetInt("Graphics_VolumetricFog", enabled ? 1 : 0);
            if (!IsHDRP) return;
            HdEach<HD.Fog>(f => f.enableVolumetricFog.Override(enabled));
        }

        public void SetFog(bool enabled)
        {
            PlayerPrefs.SetInt("Graphics_Fog", enabled ? 1 : 0);
            if (!IsHDRP) return;
            HdEach<HD.Fog>(f => f.enabled.Override(enabled));
        }

        private VolumeProfile GetSafeProfile()
        {
            if (postProcessingVolume == null) return null;
            try
            {
                var sp = postProcessingVolume.sharedProfile;
                if (sp != null && sp.components != null)
                {
                    for (int i = sp.components.Count - 1; i >= 0; i--)
                    {
                        var comp = sp.components[i];
                        if (comp == null || comp.Equals(null))
                        {
                            sp.components.RemoveAt(i);
                        }
                    }
                }
                return postProcessingVolume.profile;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GraphicsManager] Handled Volume profile access: {ex.Message}");
                return postProcessingVolume.sharedProfile;
            }
        }

        public void SetBrightness(float value01)
        {
            if (IsHDRP)
            {
                PlayerPrefs.SetFloat("Graphics_Brightness", value01);
                float post = (Mathf.Clamp01(value01) - 0.5f) * 2f;            // 0.5 = unchanged, +/- 1 EV
                bool any = false;
                foreach (var v in FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (v == null || !v.isGlobal || v.sharedProfile == null) continue;
                    var liveProfile = SafeProfile(v);
                    if (liveProfile == null || !liveProfile.TryGet<HD.ColorAdjustments>(out var live)) continue;
                    float authored = v.sharedProfile.TryGet<HD.ColorAdjustments>(out var a0) ? a0.postExposure.value : 0f;
                    live.postExposure.Override(authored + post);
                    any = true;
                }
                if (!any)
                {
                    var profiles = HdProfiles();
                    if (profiles.Count > 0) profiles[0].Add<HD.ColorAdjustments>(false).postExposure.Override(post);
                }
                return;
            }
            PlayerPrefs.SetFloat("Graphics_Brightness", value01);
            try
            {
                var prof = GetSafeProfile();
                if (prof == null) return;

                if (prof.TryGet<ColorAdjustments>(out var colorAdj) && colorAdj != null)
                {
                    // Map 0..1 to -1.5 EV to +1.5 EV
                    colorAdj.postExposure.value = Mathf.Lerp(-1.5f, 1.5f, value01);
                }
                else
                {
                    var comp = prof.components.FirstOrDefault(c => c != null && (c.GetType().Name == "ColorAdjustments" || c.GetType().Name == "Exposure"));
                    if (comp != null)
                    {
                        var postExpField = comp.GetType().GetField("postExposure") ?? comp.GetType().GetField("compensation");
                        if (postExpField != null)
                        {
                            var postExpObj = postExpField.GetValue(comp);
                            var valueProp = postExpObj?.GetType().GetProperty("value");
                            if (valueProp != null)
                            {
                                valueProp.SetValue(postExpObj, Mathf.Lerp(-1.5f, 1.5f, value01));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GraphicsManager] Could not apply brightness: {ex.Message}");
            }
        }

        public void SetAntiAliasing(int index)
        {
            if (IsHDRP)
            {
                PlayerPrefs.SetInt("Graphics_AntiAliasing", index);
                var hd = HdCamera();
                if (hd == null) return;
                switch (index)
                {
                    case 0: hd.antialiasing = HD.HDAdditionalCameraData.AntialiasingMode.None; break;
                    case 1: hd.antialiasing = HD.HDAdditionalCameraData.AntialiasingMode.FastApproximateAntialiasing; break;
                    case 2:
                        hd.antialiasing = HD.HDAdditionalCameraData.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                        hd.SMAAQuality = HD.HDAdditionalCameraData.SMAAQualityLevel.High;
                        break;
                    default: hd.antialiasing = HD.HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing; break;
                }
                return;
            }
            PlayerPrefs.SetInt("Graphics_AntiAliasing", index);
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null) return;

            var camData = mainCamera.GetUniversalAdditionalCameraData();
            if (camData == null) return;

            switch (index)
            {
                case 0:
                    camData.antialiasing = AntialiasingMode.None;
                    break;
                case 1:
                    camData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
                    break;
                case 2:
                    camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                    camData.antialiasingQuality = AntialiasingQuality.High;
                    break;
                case 3:
                    camData.antialiasing = AntialiasingMode.TemporalAntiAliasing;
                    break;
            }
        }

        public void SetRenderScale(int index)
        {
            PlayerPrefs.SetInt("Graphics_RenderScaleIndex", index);
            float scale = 1.0f;
            switch (index)
            {
                case 0: scale = 0.5f; break;   // 50%
                case 1: scale = 0.75f; break;  // 75%
                case 2: scale = 1.0f; break;   // 100%
                case 3: scale = 1.25f; break;  // 125%
            }
            SetRenderScale(scale);
        }

        public void SetRenderScale(float scale)
        {
            if (IsHDRP)
            {
                // HDRP dynamic resolution: the percentage is clamped by the HDRP asset's min / max percentage
                renderScalePercent = Mathf.Clamp(scale, 0.5f, 2f) * 100f;
                if (!dynResScalerSet)
                {
                    DynamicResolutionHandler.SetDynamicResScaler(() => renderScalePercent, DynamicResScalePolicyType.ReturnsPercentage);
                    dynResScalerSet = true;
                }
                var hd = HdCamera();
                if (hd != null) hd.allowDynamicResolution = true;
                return;
            }
            if (urpAsset != null)
            {
                urpAsset.renderScale = Mathf.Clamp(scale, 0.5f, 2.0f);
            }
        }

        public void SetFSR(bool enabled)
        {
            if (IsHDRP)
            {
                PlayerPrefs.SetInt("Graphics_FSR", enabled ? 1 : 0);
                if (mainCamera == null) mainCamera = Camera.main;
                if (mainCamera != null)
                    DynamicResolutionHandler.SetUpscaleFilter(mainCamera, enabled ? DynamicResUpscaleFilter.EdgeAdaptiveScalingUpres : DynamicResUpscaleFilter.CatmullRom);
                return;
            }
            PlayerPrefs.SetInt("Graphics_FSR", enabled ? 1 : 0);
            if (urpAsset != null)
            {
                urpAsset.upscalingFilter = enabled ? UpscalingFilterSelection.FSR : UpscalingFilterSelection.Auto;
            }
        }

        public void SetFSRSharpness(float sharpness01)
        {
            if (IsHDRP) { PlayerPrefs.SetFloat("Graphics_FSRSharpness", sharpness01); return; }   // HDRP takes the sharpness from the render pipeline asset
            PlayerPrefs.SetFloat("Graphics_FSRSharpness", sharpness01);
            if (urpAsset != null)
            {
                urpAsset.fsrSharpness = Mathf.Clamp01(sharpness01);
            }
        }

        public void SetMeshQuality(int index)
        {
            PlayerPrefs.SetInt("Graphics_MeshQuality", index);
            switch (index)
            {
                case 0: QualitySettings.lodBias = 0.5f; break; // Low
                case 1: QualitySettings.lodBias = 1.0f; break; // Medium
                case 2: QualitySettings.lodBias = 1.5f; break; // High
                case 3: QualitySettings.lodBias = 2.0f; break; // Ultra
            }
        }

        public void SetAmbientOcclusion(bool enabled)
        {
            if (IsHDRP)
            {
                PlayerPrefs.SetInt("Graphics_SSAO", enabled ? 1 : 0);
                HdEach<HD.ScreenSpaceAmbientOcclusion>(a => a.active = enabled);
                return;
            }
            PlayerPrefs.SetInt("Graphics_SSAO", enabled ? 1 : 0);
            if (ssaoFeature == null) FindSSAOFeature();
            if (ssaoFeature != null)
            {
                ssaoFeature.SetActive(enabled);
            }
        }

        public void SetShadowQuality(int index)
        {
            if (IsHDRP)
            {
                PlayerPrefs.SetInt("Graphics_ShadowQuality", index);
                int level = Mathf.Clamp(index, 0, 3);
                foreach (var l in FindObjectsByType<HD.HDAdditionalLightData>(FindObjectsSortMode.None))
                {
                    var lt = l != null ? l.GetComponent<Light>() : null;
                    if (lt == null || lt.type != LightType.Directional) continue;
                    try { l.SetShadowResolutionLevel(level); } catch (Exception) { }
                }
                return;
            }
            PlayerPrefs.SetInt("Graphics_ShadowQuality", index);
            if (urpAsset == null) return;

            int res = 2048;
            switch (index)
            {
                case 0: res = 512; break;   // Low
                case 1: res = 1024; break;  // Medium
                case 2: res = 2048; break;  // High
                case 3: res = 4096; break;  // Ultra
            }

            try
            {
                var field = typeof(UniversalRenderPipelineAsset).GetField("m_MainLightShadowmapResolution",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                field?.SetValue(urpAsset, res);
            }
            catch (Exception) { }
        }

        public void SetShadowDistance(int index)
        {
            PlayerPrefs.SetInt("Graphics_ShadowDistanceIndex", index);
            float dist = 200f;
            switch (index)
            {
                case 0: dist = 50f; break;   // Near
                case 1: dist = 100f; break;  // Medium
                case 2: dist = 200f; break;  // Far
                case 3: dist = 320f; break;  // Ultra
            }
            SetShadowDistance(dist);
        }

        public void SetShadowDistance(float distance)
        {
            if (IsHDRP)
            {
                HdEach<HD.HDShadowSettings>(sh => sh.maxShadowDistance.Override(distance));
                return;
            }
            if (urpAsset != null)
            {
                urpAsset.shadowDistance = distance;
            }
        }

        public void SetMotionBlur(bool enabled)
        {
            if (IsHDRP)
            {
                PlayerPrefs.SetInt("Graphics_MotionBlur", enabled ? 1 : 0);
                HdEach<HD.MotionBlur>(m => m.active = enabled);
                return;
            }
            PlayerPrefs.SetInt("Graphics_MotionBlur", enabled ? 1 : 0);
            try
            {
                var prof = GetSafeProfile();
                if (prof != null && prof.TryGet<MotionBlur>(out var mb) && mb != null)
                {
                    mb.active = enabled;
                }
            }
            catch (Exception) { }
        }

        public void SetBloom(bool enabled)
        {
            if (IsHDRP)
            {
                PlayerPrefs.SetInt("Graphics_Bloom", enabled ? 1 : 0);
                HdEach<HD.Bloom>(b => b.active = enabled);
                return;
            }
            PlayerPrefs.SetInt("Graphics_Bloom", enabled ? 1 : 0);
            try
            {
                var prof = GetSafeProfile();
                if (prof != null && prof.TryGet<Bloom>(out var bloom) && bloom != null)
                {
                    bloom.active = enabled;
                }
            }
            catch (Exception) { }
        }

        public void SetDepthOfField(bool enabled)
        {
            if (IsHDRP)
            {
                PlayerPrefs.SetInt("Graphics_DepthOfField", enabled ? 1 : 0);
                HdEach<HD.DepthOfField>(d => d.active = enabled);
                return;
            }
            PlayerPrefs.SetInt("Graphics_DepthOfField", enabled ? 1 : 0);
            try
            {
                var prof = GetSafeProfile();
                if (prof != null && prof.TryGet<DepthOfField>(out var dof) && dof != null)
                {
                    dof.active = enabled;
                }
            }
            catch (Exception) { }
        }

        public void SetFilmGrain(float intensity01)
        {
            if (IsHDRP)
            {
                PlayerPrefs.SetFloat("Graphics_FilmGrain", intensity01);
                bool any = false;
                HdEach<HD.FilmGrain>(g => { any = true; g.active = intensity01 > 0.01f; g.intensity.Override(Mathf.Clamp01(intensity01)); });
                if (!any && intensity01 > 0.01f)
                {
                    var profiles = HdProfiles();
                    if (profiles.Count > 0)
                    {
                        var g = profiles[0].Add<HD.FilmGrain>(true);
                        g.type.Override(HD.FilmGrainLookup.Medium1);
                        g.intensity.Override(Mathf.Clamp01(intensity01));
                    }
                }
                return;
            }
            PlayerPrefs.SetFloat("Graphics_FilmGrain", intensity01);
            try
            {
                var prof = GetSafeProfile();
                if (prof != null)
                {
                    if (!prof.TryGet<FilmGrain>(out var grain) || grain == null)
                    {
                        grain = prof.Add<FilmGrain>(false);
                    }
                    if (grain != null)
                    {
                        grain.active = intensity01 > 0.01f;
                        grain.intensity.value = Mathf.Clamp01(intensity01);
                    }
                }
            }
            catch (Exception) { }
        }

        public void SetFieldOfView(float fov)
        {
            PlayerPrefs.SetFloat("Graphics_FOV", fov);
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera != null)
            {
                mainCamera.fieldOfView = Mathf.Clamp(fov, 60f, 120f);
            }
        }

        public void SetVRAMScaleTarget(int index)
        {
            float target = 0.8f;
            switch (index)
            {
                case 0: target = 0.7f; break; // 70% Safe
                case 1: target = 0.8f; break; // 80% Balanced
                case 2: target = 0.9f; break; // 90% Maximum
                case 3: target = 1.0f; break; // Unlimited
            }

            PlayerPrefs.SetInt("Graphics_VRAMTargetIndex", index);
            PlayerPrefs.SetFloat("Graphics_VRAMTarget", target);

            float totalVramMB = Mathf.Max(512f, SystemInfo.graphicsMemorySize);
            int budgetMB = Mathf.RoundToInt(totalVramMB * target * 0.7f);
            QualitySettings.streamingMipmapsActive = true;
            QualitySettings.streamingMipmapsMemoryBudget = budgetMB;

            var monitor = FindObjectOfType<HardwareVRAMMonitor>();
            if (monitor != null) monitor.SetTargetScale(target);
        }

        public void SetTextureStreaming(bool enabled)
        {
            QualitySettings.streamingMipmapsActive = enabled;
            PlayerPrefs.SetInt("Graphics_TextureStreaming", enabled ? 1 : 0);
        }

        public void SetAsyncUpload(bool enabled)
        {
            QualitySettings.asyncUploadTimeSlice = enabled ? 4 : 1;
            QualitySettings.asyncUploadBufferSize = enabled ? 32 : 16;
            PlayerPrefs.SetInt("Graphics_AsyncUpload", enabled ? 1 : 0);
        }

        public void LoadAndApplyAllSettings()
        {
            if (PlayerPrefs.HasKey("Graphics_VSync"))
            {
                SetVSync(PlayerPrefs.GetInt("Graphics_VSync") == 1);
            }
            if (PlayerPrefs.HasKey("Graphics_FrameRate"))
            {
                SetFrameRate(PlayerPrefs.GetInt("Graphics_FrameRate"));
            }
            if (PlayerPrefs.HasKey("Graphics_TextureQuality"))
            {
                SetTextureQuality(PlayerPrefs.GetInt("Graphics_TextureQuality"));
            }
            if (PlayerPrefs.HasKey("Graphics_AnisotropicFiltering"))
            {
                SetAnisotropicFiltering(PlayerPrefs.GetInt("Graphics_AnisotropicFiltering"));
            }
            if (PlayerPrefs.HasKey("Graphics_Brightness"))
            {
                SetBrightness(PlayerPrefs.GetFloat("Graphics_Brightness"));
            }
            if (PlayerPrefs.HasKey("Graphics_AntiAliasing"))
            {
                SetAntiAliasing(PlayerPrefs.GetInt("Graphics_AntiAliasing"));
            }
            if (PlayerPrefs.HasKey("Graphics_RenderScaleIndex"))
            {
                SetRenderScale(PlayerPrefs.GetInt("Graphics_RenderScaleIndex"));
            }
            if (PlayerPrefs.HasKey("Graphics_FSR"))
            {
                SetFSR(PlayerPrefs.GetInt("Graphics_FSR") == 1);
            }
            if (PlayerPrefs.HasKey("Graphics_FSRSharpness"))
            {
                SetFSRSharpness(PlayerPrefs.GetFloat("Graphics_FSRSharpness"));
            }
            if (PlayerPrefs.HasKey("Graphics_MeshQuality"))
            {
                SetMeshQuality(PlayerPrefs.GetInt("Graphics_MeshQuality"));
            }
            if (PlayerPrefs.HasKey("Graphics_SSAO"))
            {
                SetAmbientOcclusion(PlayerPrefs.GetInt("Graphics_SSAO") == 1);
            }
            if (PlayerPrefs.HasKey("Graphics_ShadowQuality"))
            {
                SetShadowQuality(PlayerPrefs.GetInt("Graphics_ShadowQuality"));
            }
            if (PlayerPrefs.HasKey("Graphics_ShadowDistanceIndex"))
            {
                SetShadowDistance(PlayerPrefs.GetInt("Graphics_ShadowDistanceIndex"));
            }
            if (PlayerPrefs.HasKey("Graphics_MotionBlur"))
            {
                SetMotionBlur(PlayerPrefs.GetInt("Graphics_MotionBlur") == 1);
            }
            if (PlayerPrefs.HasKey("Graphics_Bloom"))
            {
                SetBloom(PlayerPrefs.GetInt("Graphics_Bloom") == 1);
            }
            if (PlayerPrefs.HasKey("Graphics_DepthOfField"))
            {
                SetDepthOfField(PlayerPrefs.GetInt("Graphics_DepthOfField") == 1);
            }
            if (PlayerPrefs.HasKey("Graphics_FilmGrain"))
            {
                SetFilmGrain(PlayerPrefs.GetFloat("Graphics_FilmGrain"));
            }
            if (PlayerPrefs.HasKey("Graphics_FOV"))
            {
                SetFieldOfView(PlayerPrefs.GetFloat("Graphics_FOV"));
            }
            if (PlayerPrefs.HasKey("Graphics_VRAMTargetIndex"))
            {
                SetVRAMScaleTarget(PlayerPrefs.GetInt("Graphics_VRAMTargetIndex"));
            }
            if (PlayerPrefs.HasKey("Graphics_TextureStreaming"))
            {
                SetTextureStreaming(PlayerPrefs.GetInt("Graphics_TextureStreaming") == 1);
            }
            if (PlayerPrefs.HasKey("Graphics_AsyncUpload"))
            {
                SetAsyncUpload(PlayerPrefs.GetInt("Graphics_AsyncUpload") == 1);
            }
            if (PlayerPrefs.HasKey("Graphics_VolumetricFog"))
            {
                SetVolumetricFog(PlayerPrefs.GetInt("Graphics_VolumetricFog") == 1);
            }
            if (PlayerPrefs.HasKey("Graphics_Fog"))
            {
                SetFog(PlayerPrefs.GetInt("Graphics_Fog") == 1);
            }
            if (PlayerPrefs.HasKey("Graphics_ExposureMode"))
            {
                if (PlayerPrefs.GetInt("Graphics_ExposureMode") == 1) SetExposureFixed(); else SetExposureAutomatic();
            }
            if (PlayerPrefs.HasKey("Graphics_ExposureComp"))
            {
                SetExposureCompensation(PlayerPrefs.GetFloat("Graphics_ExposureComp"));
            }
            if (PlayerPrefs.HasKey("Graphics_ExposureAdapt"))
            {
                SetExposureAdaptation(PlayerPrefs.GetInt("Graphics_ExposureAdapt"));
            }
            if (PlayerPrefs.HasKey("Graphics_DLSSQuality"))
            {
                SetDLSSQuality(PlayerPrefs.GetInt("Graphics_DLSSQuality"));
            }
            if (PlayerPrefs.HasKey("Graphics_DLSS"))
            {
                SetDLSS(PlayerPrefs.GetInt("Graphics_DLSS") == 1);
            }
        }
    }
}
