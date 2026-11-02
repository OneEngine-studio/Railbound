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
    /// Terrains, Tree Colliders, Lights, Cameras, and Scene Volumes.
    /// </summary>
    public static class HDRPProjectConverter
    {
        private const string HdrpHighAssetPath = "Assets/TerrainDemoScene_HDRP/Settings/HDRP/HDRP High Quality.asset";
        private const string HdrpUltraAssetPath = "Assets/TerrainDemoScene_HDRP/Settings/HDRP/HDRP Ultra Quality.asset";
        private const string HdrpMediumAssetPath = "Assets/TerrainDemoScene_HDRP/Settings/HDRP/HDRP Medium Quality.asset";
        private const string HdrpLowAssetPath = "Assets/TerrainDemoScene_HDRP/Settings/HDRP/HDRP Low Quality.asset";
        private const string HdrpTerrainMatPath = "Assets/TerrainDemoScene_HDRP/Terrain/Materials/TerrainLit.mat";

        [InitializeOnLoadMethod]
        private static void EnsureLocalHDRPMaterialsConverted()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                var terrainMat = AssetDatabase.LoadAssetAtPath<Material>(HdrpTerrainMatPath);
                if (terrainMat != null && terrainMat.shader != null && terrainMat.shader.name.Contains("Universal Render Pipeline"))
                {
                    Debug.Log("[HDRPConverter] Detected unconverted local TerrainDemoScene_HDRP materials; auto-upgrading to HDRP...");
                    ConvertWholeProjectToHDRP();
                }
            };
        }

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
            Shader hdrpAutodesk = Shader.Find("HDRP/AutodeskInteractive") ?? hdrpLit;
            Shader sgRock = Shader.Find("Shader Graphs/Rock") ?? hdrpLit;
            Shader sgTerrainGrass = Shader.Find("Shader Graphs/TerrainGrass") ?? hdrpLit;
            Shader sgWater = Shader.Find("Shader Graphs/WaterDepthBased") ?? hdrpLit;
            Shader sgSpeedTreeBark = Shader.Find("HDRP/Nature/SpeedTree_Bark") ?? Shader.Find("HDRP/Nature/SpeedTree8") ?? hdrpLit;
            Shader sgSpeedTreeBranch = Shader.Find("HDRP/Nature/SpeedTree_Branch") ?? Shader.Find("HDRP/Nature/SpeedTree8") ?? hdrpLit;

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
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null || mat.shader == null) continue;

                // 1. Restore TerrainDemoScene_HDRP materials to their original HDRP shaders
                if (path.StartsWith("Assets/TerrainDemoScene_HDRP"))
                {
                    Shader targetDemoShader = null;
                    string fileName = Path.GetFileNameWithoutExtension(path);

                    if (path.Contains("/Details/Materials/"))
                    {
                        targetDemoShader = sgTerrainGrass;
                    }
                    else if (fileName.StartsWith("Rock_Overgrown"))
                    {
                        targetDemoShader = sgRock;
                    }
                    else if (fileName.Contains("WaterDepthBased"))
                    {
                        targetDemoShader = sgWater;
                    }
                    else if (fileName == "TerrainLit")
                    {
                        targetDemoShader = hdrpTerrain;
                    }
                    else if (path.Contains("/Trees/"))
                    {
                        if (fileName.EndsWith("_Bark") || fileName.EndsWith("_Cap"))
                            targetDemoShader = sgSpeedTreeBark;
                        else
                            targetDemoShader = sgSpeedTreeBranch;
                    }
                    else if (mat.shader.name.Contains("Universal Render Pipeline") || mat.shader.name == "Hidden/InternalErrorShader")
                    {
                        targetDemoShader = hdrpLit;
                    }

                    if (targetDemoShader != null && mat.shader != targetDemoShader)
                    {
                        mat.shader = targetDemoShader;
                        EditorUtility.SetDirty(mat);
                        count++;
                    }
                    continue;
                }

                string sName = mat.shader.name;
                bool isUrpLit = sName.Contains("Universal Render Pipeline/Lit") || sName.Contains("Universal Render Pipeline/Simple Lit") || sName.Contains("Universal Render Pipeline/Baked Lit");
                bool isStandard = sName == "Standard" || sName == "Standard (Specular setup)" || sName == "Standard-DoubleSided";
                bool isAutodesk = sName == "Autodesk Interactive" || sName == "Universal Render Pipeline/Autodesk Interactive";
                bool isUnlitOrParticle = sName.Contains("Universal Render Pipeline/Unlit")
                    || sName.Contains("Universal Render Pipeline/Particles/")
                    || sName.Contains("Particles/Standard")
                    || sName.Contains("Sprite-Unlit")
                    || sName == "Unlit/GridShader"
                    || sName == "Unlit/Texture"
                    || sName == "Unlit/Color"
                    || sName == "Unlit/Transparent";
                bool isTerrainLit = sName.Contains("Terrain/Lit") || sName == "Nature/Terrain/Standard";
                bool isSpeedTree = sName.Contains("SpeedTree");

                if (isAutodesk && hdrpAutodesk != null)
                {
                    Texture baseMap = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : (mat.HasProperty("_BaseColorMap") ? mat.GetTexture("_BaseColorMap") : null);
                    Color baseColor = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
                    Texture normalMap = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
                    Texture metallicMap = mat.HasProperty("_MetallicGlossMap") ? mat.GetTexture("_MetallicGlossMap") : null;
                    Texture roughnessMap = mat.HasProperty("_SpecGlossMap") ? mat.GetTexture("_SpecGlossMap") : null;

                    mat.shader = hdrpAutodesk;
                    if (baseMap != null)
                    {
                        if (mat.HasProperty("_BaseColorMap")) mat.SetTexture("_BaseColorMap", baseMap);
                        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", baseMap);
                    }
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
                    if (normalMap != null && mat.HasProperty("_NormalMap")) mat.SetTexture("_NormalMap", normalMap);
                    if (metallicMap != null && mat.HasProperty("_MetallicMap")) mat.SetTexture("_MetallicMap", metallicMap);
                    if (roughnessMap != null && mat.HasProperty("_RoughnessMap")) mat.SetTexture("_RoughnessMap", roughnessMap);

                    EditorUtility.SetDirty(mat);
                    count++;
                }
                else if (isUrpLit || isStandard)
                {
                    bool doubleSided = sName == "Standard-DoubleSided" || (mat.HasProperty("_Cull") && mat.GetFloat("_Cull") == 0f);
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

                    if (doubleSided)
                    {
                        if (mat.HasProperty("_DoubleSidedEnable")) mat.SetFloat("_DoubleSidedEnable", 1f);
                        mat.EnableKeyword("_DOUBLESIDED_ON");
                    }

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
                else if (isUnlitOrParticle && hdrpUnlit != null)
                {
                    bool isTransparent = sName.Contains("Particles") || sName.Contains("Transparent") || sName.Contains("Sprite") || (mat.HasProperty("_Surface") && mat.GetFloat("_Surface") == 1f);
                    Texture baseMap = mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : (mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null);
                    Color color = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : (mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white);

                    mat.shader = hdrpUnlit;
                    if (baseMap != null) mat.SetTexture("_UnlitColorMap", baseMap);
                    mat.SetColor("_UnlitColor", color);

                    if (isTransparent)
                    {
                        if (mat.HasProperty("_SurfaceType")) mat.SetFloat("_SurfaceType", 1f);
                        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        mat.renderQueue = (int)RenderQueue.Transparent;
                    }

                    EditorUtility.SetDirty(mat);
                    count++;
                }
                else if (isTerrainLit && hdrpTerrain != null)
                {
                    mat.shader = hdrpTerrain;
                    EditorUtility.SetDirty(mat);
                    count++;
                }
                else if (isSpeedTree && sgSpeedTreeBranch != null)
                {
                    mat.shader = sgSpeedTreeBranch;
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

            // 1. Upgrade Terrains and Tree Colliders
            Material hdrpTerrainMat = AssetDatabase.LoadAssetAtPath<Material>(HdrpTerrainMatPath);
            if (hdrpTerrainMat == null)
            {
                hdrpTerrainMat = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipelineResources/Material/DefaultHDTerrainMaterial.mat");
            }

            Terrain[] terrains = UnityEngine.Object.FindObjectsOfType<Terrain>(true);
            foreach (var terrain in terrains)
            {
                if (hdrpTerrainMat != null && terrain.materialTemplate != hdrpTerrainMat)
                {
                    terrain.materialTemplate = hdrpTerrainMat;
                    EditorUtility.SetDirty(terrain);
                    count++;
                }

                // Fix TerrainCollider: MeshCollider is not supported on terrain at the moment
                if (terrain.terrainData != null && terrain.terrainData.treePrototypes != null)
                {
                    foreach (var proto in terrain.terrainData.treePrototypes)
                    {
                        if (proto.prefab == null) continue;
                        MeshCollider[] meshCols = proto.prefab.GetComponentsInChildren<MeshCollider>(true);
                        if (meshCols != null && meshCols.Length > 0)
                        {
                            // Ensure a CapsuleCollider exists on root if we remove MeshCollider
                            if (proto.prefab.GetComponent<CapsuleCollider>() == null)
                            {
                                var cap = proto.prefab.AddComponent<CapsuleCollider>();
                                var mf = proto.prefab.GetComponentInChildren<MeshFilter>();
                                if (mf != null && mf.sharedMesh != null)
                                {
                                    var b = mf.sharedMesh.bounds;
                                    cap.center = b.center;
                                    cap.radius = Mathf.Max(b.extents.x, b.extents.z);
                                    cap.height = b.size.y;
                                }
                                else
                                {
                                    cap.radius = 0.35f;
                                    cap.height = 6f;
                                    cap.center = new Vector3(0f, 3f, 0f);
                                }
                            }
                            foreach (var mc in meshCols)
                            {
                                UnityEngine.Object.DestroyImmediate(mc, true);
                            }
                            EditorUtility.SetDirty(proto.prefab);
                            PrefabUtility.SavePrefabAsset(proto.prefab);
                            count++;
                        }
                    }
                }
            }

            // 2. Replace any built-in URP package materials referenced directly on scene Renderers
            Material defaultHdMat = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipelineResources/Material/DefaultHDMaterial.mat");
            Renderer[] renderers = UnityEngine.Object.FindObjectsOfType<Renderer>(true);
            foreach (var r in renderers)
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null && mats[i].shader != null)
                    {
                        string sn = mats[i].shader.name;
                        if (sn.Contains("Universal Render Pipeline") || sn == "Hidden/InternalErrorShader")
                        {
                            string matPath = AssetDatabase.GetAssetPath(mats[i]);
                            if (matPath.StartsWith("Packages/com.unity.render-pipelines.universal") && defaultHdMat != null)
                            {
                                mats[i] = defaultHdMat;
                                changed = true;
                            }
                        }
                    }
                }
                if (changed)
                {
                    r.sharedMaterials = mats;
                    EditorUtility.SetDirty(r);
                    count++;
                }
            }

            // 3. Upgrade Cameras
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

            // 4. Upgrade Lights
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
                    if (light.intensity < 100f)
                    {
                        light.intensity = 25000f;
                    }
                    hdLight.lightUnit = LightUnit.Lux;
                }
                else if (light.type == LightType.Point || light.type == LightType.Spot)
                {
                    if (light.intensity < 10f)
                    {
                        light.intensity = 800f;
                    }
                    hdLight.lightUnit = LightUnit.Lumen;
                }

                EditorUtility.SetDirty(light.gameObject);
            }

            // 5. Ensure Global HDRP Sky & Post Volume exists
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

                var visualEnv = profile.Add<VisualEnvironment>(true);
                visualEnv.skyType.overrideState = true;
                visualEnv.skyType.value = (int)SkyType.PhysicallyBased;

                var sky = profile.Add<PhysicallyBasedSky>(true);
                sky.planetaryRadius.overrideState = true;

                var exposure = profile.Add<Exposure>(true);
                exposure.mode.overrideState = true;
                exposure.mode.value = ExposureMode.Fixed;
                exposure.fixedExposure.overrideState = true;
                exposure.fixedExposure.value = 12.5f;

                var bloom = profile.Add<Bloom>(true);
                bloom.intensity.overrideState = true;
                bloom.intensity.value = 0.25f;

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
