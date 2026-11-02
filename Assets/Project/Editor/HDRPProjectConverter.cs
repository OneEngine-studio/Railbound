using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Rendering.HighDefinition;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Project.Editor
{
    /// <summary>
    /// Automated project-wide converter to migrate Railbound from URP/Built-in to HDRP.
    /// Converts Render Pipeline Assets, Quality Settings, Materials, Decals, Particles,
    /// Terrain Layers, Terrains, Tree Colliders, Lights, Cameras, and Scene Volumes.
    /// </summary>
    public static class HDRPProjectConverter
    {
        private const string HdrpHighAssetPath = "Assets/TerrainDemoScene_HDRP/Settings/HDRP/HDRP High Quality.asset";
        private const string HdrpUltraAssetPath = "Assets/TerrainDemoScene_HDRP/Settings/HDRP/HDRP Ultra Quality.asset";
        private const string HdrpMediumAssetPath = "Assets/TerrainDemoScene_HDRP/Settings/HDRP/HDRP Medium Quality.asset";
        private const string HdrpLowAssetPath = "Assets/TerrainDemoScene_HDRP/Settings/HDRP/HDRP Low Quality.asset";
        private const string HdrpTerrainMatPath = "Assets/TerrainDemoScene_HDRP/Terrain/Materials/TerrainLit.mat";

        private static readonly Dictionary<string, string> RestoredTextureGuids = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Assets/Nokobot/Modern Guns - Handgun/Materials/M1911 Handgun_Steel.mat", "7474c63c5c65b1b4fa45a47bd1bf528f" },
            { "Assets/Nokobot/Modern Guns - Handgun/Materials/M1911 Magazine_Black.mat", "c11766a3d3e2f124eb11ea77d2d2fd99" },
            { "Assets/Nokobot/Modern Guns - Handgun/Materials/M1911 Magazine_Steel.mat", "7714950642003ee488bdddb49dd229e0" },
            { "Assets/all_Projects/Demos/Materials/Prototype Props/Black Grid.mat", "64600d436d4b10c4c8809d6dbc297498" },
            { "Assets/all_Projects/Demos/Materials/Prototype Props/Black Wall.mat", "64600d436d4b10c4c8809d6dbc297498" },
            { "Assets/all_Projects/Demos/Materials/Prototype Props/Cyan Grid.mat", "64600d436d4b10c4c8809d6dbc297498" },
            { "Assets/all_Projects/Demos/Materials/Prototype Props/Red Grid Emissive.mat", "64600d436d4b10c4c8809d6dbc297498" },
            { "Assets/all_Projects/Demos/Materials/Prototype Props/Red Grid.mat", "64600d436d4b10c4c8809d6dbc297498" },
            { "Assets/all_Projects/Demos/Materials/Prototype Props/Yellow Grid.mat", "64600d436d4b10c4c8809d6dbc297498" },
            { "Assets/all_Projects/Demos/Materials/Surfaces/RockSurface.mat", "a541ea20669413d45ae74c10c955c04d" },
            { "Assets/all_Projects/Demos/Models/Characters/Cyber Robot/Materials/AA_Top_diffuse 1.mat", "bea6b4f7fd2a0b24d87d26e0d87eb541" },
            { "Assets/all_Projects/Demos/Models/Characters/JU Robot/Model/Materials/JURobotMetalic01_Blue.mat", "dc82cba180e64c74bb88507fcd420ece" },
            { "Assets/all_Projects/Demos/Models/Characters/JU Robot/Model/Materials/JURobotMetalic01_Orange.mat", "dc82cba180e64c74bb88507fcd420ece" },
            { "Assets/all_Projects/Demos/Models/Characters/JU Robot/Model/Materials/JURobotMetalic01_Purple.mat", "dc82cba180e64c74bb88507fcd420ece" },
            { "Assets/all_Projects/Demos/Models/Characters/JU Robot/Model/Materials/JURobotMetalic01_Yellow.mat", "dc82cba180e64c74bb88507fcd420ece" },
            { "Assets/all_Projects/Demos/Models/Characters/JU Robot/Model/Materials/JURobotMetalic02.mat", "dc82cba180e64c74bb88507fcd420ece" },
        };

        private static readonly Dictionary<string, Color> RestoredBaseColors = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
        {
            { "Assets/Nokobot/Modern Guns - Handgun/Demo/Materials/ Floor.mat", new Color(0.195f, 0.207f, 0.235f, 1.0f) },
            { "Assets/Nokobot/Modern Guns - Handgun/Demo/Materials/Table Bottom.mat", new Color(0.243f, 0.243f, 0.243f, 1.0f) },
            { "Assets/Nokobot/Modern Guns - Handgun/Demo/Materials/Table Top.mat", new Color(0.603f, 0.603f, 0.603f, 1.0f) },
            { "Assets/all_Projects/Demos/Materials/Prototype Props/Black Grid.mat", new Color(0.453f, 0.562f, 0.623f, 1.0f) },
            { "Assets/all_Projects/Demos/Materials/Prototype Props/Black Wall.mat", new Color(0.270f, 0.304f, 0.340f, 1.0f) },
            { "Assets/all_Projects/Demos/Materials/Prototype Props/Cyan Grid.mat", new Color(0.239f, 0.671f, 0.906f, 1.0f) },
            { "Assets/all_Projects/Demos/Materials/Prototype Props/Red Grid Emissive.mat", new Color(1.0f, 0.386f, 0.325f, 1.0f) },
            { "Assets/all_Projects/Demos/Materials/Prototype Props/Red Grid.mat", new Color(1.0f, 0.386f, 0.325f, 1.0f) },
            { "Assets/all_Projects/Demos/Materials/Prototype Props/Yellow Grid.mat", new Color(1.0f, 0.588f, 0.259f, 1.0f) },
            { "Assets/all_Projects/Demos/Materials/Surfaces/Metal Default.mat", new Color(0.557f, 0.557f, 0.557f, 1.0f) },
            { "Assets/all_Projects/Demos/Materials/Surfaces/Plank Wood.mat", new Color(1.0f, 0.779f, 0.684f, 1.0f) },
            { "Assets/all_Projects/Demos/Materials/TransparentMat.mat", new Color(0.0f, 0.713f, 1.0f, 0.239f) },
            { "Assets/all_Projects/Demos/Models/Characters/JU Robot/Model/Materials/JURobotMetalic01_Blue.mat", new Color(0.289f, 0.667f, 1.0f, 1.0f) },
            { "Assets/all_Projects/Demos/Models/Characters/JU Robot/Model/Materials/JURobotMetalic01_Orange.mat", new Color(0.953f, 0.567f, 0.256f, 1.0f) },
            { "Assets/all_Projects/Demos/Models/Characters/JU Robot/Model/Materials/JURobotMetalic01_Purple.mat", new Color(0.506f, 0.391f, 1.0f, 1.0f) },
            { "Assets/all_Projects/Demos/Models/Characters/JU Robot/Model/Materials/JURobotMetalic01_Yellow.mat", new Color(1.0f, 0.805f, 0.288f, 1.0f) },
            { "Assets/all_Projects/Demos/Models/Characters/JU Robot/Model/Materials/JURobotMetalic02.mat", new Color(0.377f, 0.377f, 0.377f, 1.0f) },
            { "Assets/all_Projects/Demos/Models/Vehicles/Materials/Blue Vehicle Paint.mat", new Color(0.277f, 0.453f, 0.906f, 1.0f) },
            { "Assets/all_Projects/Demos/Models/Vehicles/Materials/Orange Vehicle Paint.mat", new Color(1.0f, 0.431f, 0.0f, 1.0f) },
            { "Assets/all_Projects/Demos/Models/Vehicles/Materials/Vehicle Bottom.mat", new Color(0.245f, 0.245f, 0.245f, 1.0f) },
            { "Assets/all_Projects/Demos/Models/Vehicles/Materials/Yellow Vehicle Paint.mat", new Color(1.0f, 0.642f, 0.0f, 1.0f) },
            { "Assets/all_Projects/Materials/Particle Materials/BulletCasing.mat", new Color(1.0f, 0.767f, 0.382f, 1.0f) },
        };

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
            int prefabsFixed = FixAllPrefabsAndFX();
            int terrainLayersConverted = ConvertTerrainLayers();
            int sceneObjectsUpgraded = UpgradeActiveScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[HDRPConverter] Complete! Updated {pipelineUpdated} pipeline settings, converted {materialsConverted} materials, fixed {prefabsFixed} prefabs, {terrainLayersConverted} terrain layers, and {sceneObjectsUpgraded} scene objects.");
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

            GraphicsSettings.defaultRenderPipeline = highAsset;
            changes++;

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

            QualitySettings.SetQualityLevel(Math.Min(qualityCount - 1, 3), true);
            Debug.Log("[HDRPConverter] Assigned HDRP assets to GraphicsSettings and QualitySettings.");
            return changes;
        }

        public static int ConvertAllMaterials()
        {
            Shader hdrpLit = Shader.Find("HDRP/Lit");
            Shader hdrpUnlit = Shader.Find("HDRP/Unlit");
            Shader hdrpParticleUnlit = Shader.Find("HDRP/Particles/Unlit") ?? hdrpUnlit;
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
                        targetDemoShader = sgTerrainGrass;
                    else if (fileName.StartsWith("Rock_Overgrown"))
                        targetDemoShader = sgRock;
                    else if (fileName.Contains("WaterDepthBased"))
                        targetDemoShader = sgWater;
                    else if (fileName == "TerrainLit")
                        targetDemoShader = hdrpTerrain;
                    else if (path.Contains("/Trees/"))
                        targetDemoShader = (fileName.EndsWith("_Bark") || fileName.EndsWith("_Cap")) ? sgSpeedTreeBark : sgSpeedTreeBranch;
                    else if (mat.shader.name.Contains("Universal Render Pipeline") || mat.shader.name == "Hidden/InternalErrorShader")
                        targetDemoShader = hdrpLit;

                    if (targetDemoShader != null && mat.shader != targetDemoShader)
                    {
                        mat.shader = targetDemoShader;
                        EditorUtility.SetDirty(mat);
                        count++;
                    }
                    continue;
                }

                // Skip UI / TextMeshPro / Skybox materials
                string sName = mat.shader.name;
                if (sName.StartsWith("TextMeshPro") || sName.StartsWith("UI/") || sName.StartsWith("Skybox/") || sName.StartsWith("GUI/"))
                    continue;

                // 2. Check if this material is a Decal, Particle, Trail, or FX Unlit material
                if (IsDecalOrParticleMaterial(path, sName))
                {
                    ConfigureDecalOrParticleMaterial(mat, path, hdrpParticleUnlit);
                    EditorUtility.SetDirty(mat);
                    count++;
                    continue;
                }

                // 3. Check Terrain or SpeedTree materials outside TerrainDemoScene_HDRP
                if (sName.Contains("Terrain/Lit") || sName == "Nature/Terrain/Standard")
                {
                    if (hdrpTerrain != null && mat.shader != hdrpTerrain)
                    {
                        mat.shader = hdrpTerrain;
                        EditorUtility.SetDirty(mat);
                        count++;
                    }
                    continue;
                }
                if (sName.Contains("SpeedTree"))
                {
                    if (sgSpeedTreeBranch != null && mat.shader != sgSpeedTreeBranch)
                    {
                        mat.shader = sgSpeedTreeBranch;
                        EditorUtility.SetDirty(mat);
                        count++;
                    }
                    continue;
                }

                // 4. Check Autodesk Interactive materials
                bool isAutodesk = sName == "Autodesk Interactive" || sName == "Universal Render Pipeline/Autodesk Interactive" || sName == "HDRP/AutodeskInteractive";
                if (isAutodesk && hdrpAutodesk != null)
                {
                    Texture baseMap = GetFirstTexture(mat, "_BaseColorMap", "_MainTex", "_BaseMap");
                    Color baseColor = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : (mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white);
                    Texture normalMap = GetFirstTexture(mat, "_NormalMap", "_BumpMap");
                    Texture metallicMap = GetFirstTexture(mat, "_MetallicMap", "_MetallicGlossMap");
                    Texture roughnessMap = GetFirstTexture(mat, "_RoughnessMap", "_SpecGlossMap");

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
                    HDShaderUtils.ResetMaterialKeywords(mat);
                    EditorUtility.SetDirty(mat);
                    count++;
                    continue;
                }

                // 5. Check opaque Unlit materials (such as grid shaders, prototype backgrounds, etc.)
                if ((sName == "HDRP/Unlit" || sName.Contains("Unlit")) && !path.EndsWith("BulletCasing.mat", StringComparison.OrdinalIgnoreCase))
                {
                    Texture baseMap = GetFirstTexture(mat, "_UnlitColorMap", "_BaseMap", "_MainTex");
                    Color color = mat.HasProperty("_UnlitColor") ? mat.GetColor("_UnlitColor") : (mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : (mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white));
                    mat.shader = hdrpUnlit;
                    if (baseMap != null) mat.SetTexture("_UnlitColorMap", baseMap);
                    mat.SetColor("_UnlitColor", color);
                    mat.SetFloat("_SurfaceType", 0f);
                    HDShaderUtils.ResetMaterialKeywords(mat);
                    EditorUtility.SetDirty(mat);
                    count++;
                    continue;
                }

                // 6. Standard / URP Lit / HDRP Lit 3D Surface Materials
                ConfigureLitMaterial(mat, path, hdrpLit);
                EditorUtility.SetDirty(mat);
                count++;
            }

            Debug.Log($"[HDRPConverter] Converted and validated {count} materials.");
            return count;
        }

        private static bool IsDecalOrParticleMaterial(string path, string shaderName)
        {
            string lowerPath = path.ToLowerInvariant();
            string fileName = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();

            if (fileName == "bulletcasing") return false;
            if (lowerPath.Contains("essential 2d particle fx")) return true;
            if (lowerPath.Contains("/particles/") || lowerPath.Contains("/particle materials/")) return true;
            if (lowerPath.Contains("/bullet hole/")) return true;
            if (lowerPath.Contains("/muzzleflash")) return true;
            if (lowerPath.Contains("/electricspark/")) return true;
            if (lowerPath.Contains("/trainsmoke/")) return true;
            if (fileName == "footstepdecal" || fileName == "bullethole" || fileName == "bulletholeskin" || fileName == "m_stormring") return true;
            if (fileName == "smokeparticle" || fileName == "fireparticlesmaterial" || fileName == "bullettrailmaterial") return true;
            if (fileName.Contains("trail") || fileName == "particlesgreen" || fileName == "particlesred" || fileName == "particle urp") return true;
            if (shaderName == "HDRP/Particles/Unlit" || shaderName.Contains("Particles/")) return true;

            return false;
        }

        private static void ConfigureDecalOrParticleMaterial(Material mat, string path, Shader hdrpParticleUnlit)
        {
            string fileName = Path.GetFileNameWithoutExtension(path);
            string lowerName = fileName.ToLowerInvariant();
            string lowerPath = path.ToLowerInvariant();

            Texture tex = GetFirstTexture(mat, "_UnlitColorMap", "_BaseColorMap", "_BaseMap", "_MainTex");
            Color col = Color.white;
            if (mat.HasProperty("_UnlitColor")) col = mat.GetColor("_UnlitColor");
            else if (mat.HasProperty("_BaseColor")) col = mat.GetColor("_BaseColor");
            else if (mat.HasProperty("_Color")) col = mat.GetColor("_Color");
            else if (mat.HasProperty("_TintColor")) col = mat.GetColor("_TintColor");

            // Explicit textures & colors for core FX decals and particles
            if (lowerName == "footstepdecal")
            {
                tex = AssetDatabase.LoadAssetAtPath<Texture>("Assets/all_Projects/Textures/FootstepDecal.png") ?? tex;
                col = new Color(0f, 0f, 0f, 0.35f);
            }
            else if (lowerName == "bullethole")
            {
                tex = AssetDatabase.LoadAssetAtPath<Texture>("Assets/all_Projects/Models/FX/Bullet Hole/BulletHole.png") ?? tex;
                col = Color.white;
            }
            else if (lowerName == "bulletholeskin")
            {
                tex = AssetDatabase.LoadAssetAtPath<Texture>("Assets/all_Projects/Models/FX/Bullet Hole/BulletHoleRed.png") ?? tex;
                col = Color.white;
            }
            else if (lowerName == "m_stormring")
            {
                col = new Color(0.25f, 0.55f, 1.0f, 0.25f);
            }
            else if (lowerName == "smokeparticle")
            {
                tex = AssetDatabase.LoadAssetAtPath<Texture>("Assets/all_Projects/Textures/Particle FX/Smoke.png") ?? tex;
                col = new Color(1f, 1f, 1f, 0.55f);
            }
            else if (lowerName == "fireparticlesmaterial")
            {
                tex = AssetDatabase.LoadAssetAtPath<Texture>("Assets/all_Projects/Textures/Particle FX/Muzzle Flash.png") ?? tex;
                col = Color.white;
            }
            else if (lowerName == "bullettrailmaterial")
            {
                tex = AssetDatabase.LoadAssetAtPath<Texture>("Assets/all_Projects/Textures/Particle FX/Muzzle Flash.png") ?? tex;
                col = new Color(1f, 0.72f, 0.35f, 1f);
            }
            else if (lowerName == "muzzleflash")
            {
                tex = AssetDatabase.LoadAssetAtPath<Texture>("Assets/Nokobot/Modern Guns - Handgun/Effects/MuzzleFlash_SpriteSheet.png") ?? tex;
                col = new Color(1f, 0.85f, 0.55f, 1f);
            }
            else if (lowerName == "electricalsparks")
            {
                tex = AssetDatabase.LoadAssetAtPath<Texture>("Assets/WSM Game Studio/Train Controller_v3/Shared/Particles/Textures/SparkParticle.png") ?? tex;
                col = new Color(1f, 0.85f, 0.45f, 1f);
            }
            else if (lowerName == "smokelight")
            {
                tex = AssetDatabase.LoadAssetAtPath<Texture>("Assets/WSM Game Studio/Train Controller_v3/Shared/Particles/Textures/SmokePuff.png") ?? tex;
                col = Color.white;
            }
            else if (lowerName == "particle urp" && tex == null)
            {
                tex = AssetDatabase.LoadAssetAtPath<Texture>("Assets/Essential 2D Particle FX/Textures/Spark 1.png");
                col = Color.white;
            }
            else if (lowerName.Contains("trail") && tex == null)
            {
                tex = AssetDatabase.LoadAssetAtPath<Texture>("Assets/all_Projects/Textures/Glow.png");
            }

            bool isQuadDecal = lowerName == "footstepdecal" || lowerName == "bullethole" || lowerName == "bulletholeskin" || lowerName == "m_stormring";

            bool isAdditiveGlow = !isQuadDecal && (
                lowerName.Contains("fire") ||
                lowerName.Contains("spark") ||
                lowerName.Contains("muzzle") ||
                lowerName.Contains("bullettrail") ||
                lowerName.Contains("trail") ||
                lowerName.Contains("slash") ||
                lowerName.Contains("star") ||
                lowerName.Contains("soul") ||
                lowerName.Contains("spike") ||
                lowerName.Contains("particlesgreen") ||
                lowerName.Contains("particlesred") ||
                lowerName == "particle urp"
            );

            mat.shader = hdrpParticleUnlit;

            if (tex != null)
            {
                mat.SetTexture("_UnlitColorMap", tex);
                mat.SetTexture("_MainTex", tex);
            }
            mat.SetColor("_UnlitColor", col);
            mat.SetColor("_Color", col);

            mat.SetFloat("_UseVertexColor", isQuadDecal ? 0f : 1f);
            mat.SetFloat("_LuminanceToAlpha", isAdditiveGlow ? 1f : 0f);
            mat.SetFloat("_SurfaceType", 1f);
            mat.SetFloat("_BlendMode", isAdditiveGlow ? 1f : 0f);
            mat.SetFloat("_SrcBlend", 1f); // BlendMode.One (ApplyBlendMode premultiplies color * opacity)
            mat.SetFloat("_DstBlend", isAdditiveGlow ? 1f : 10f); // One for Additive, OneMinusSrcAlpha for Alpha
            mat.SetFloat("_AlphaSrcBlend", 1f);
            mat.SetFloat("_AlphaDstBlend", isAdditiveGlow ? 1f : 10f);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_TransparentZWrite", 0f);
            mat.SetFloat("_CullMode", 0f); // Cull Off
            mat.SetFloat("_DoubleSidedEnable", 1f);
            mat.SetInt("_ZTestDepthEqualForOpaque", (int)CompareFunction.LessEqual);

            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
        }

        private static void ConfigureLitMaterial(Material mat, string path, Shader hdrpLit)
        {
            string sName = mat.shader.name;
            bool wasStandardOrUrp = sName != "HDRP/Lit";
            bool doubleSided = sName == "Standard-DoubleSided"
                || (mat.HasProperty("_Cull") && mat.GetFloat("_Cull") == 0f)
                || (mat.HasProperty("_DoubleSidedEnable") && mat.GetFloat("_DoubleSidedEnable") > 0.5f);

            Texture baseMap = GetFirstTexture(mat, "_BaseColorMap", "_BaseMap", "_MainTex");
            Vector2 mainScale = Vector2.one;
            Vector2 mainOffset = Vector2.zero;
            if (mat.HasProperty("_BaseColorMap") && mat.GetTexture("_BaseColorMap") != null)
            {
                mainScale = mat.GetTextureScale("_BaseColorMap");
                mainOffset = mat.GetTextureOffset("_BaseColorMap");
            }
            else if (mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") != null)
            {
                mainScale = mat.GetTextureScale("_BaseMap");
                mainOffset = mat.GetTextureOffset("_BaseMap");
            }
            else if (mat.HasProperty("_MainTex") && mat.GetTexture("_MainTex") != null)
            {
                mainScale = mat.GetTextureScale("_MainTex");
                mainOffset = mat.GetTextureOffset("_MainTex");
            }

            // Restore any textures lost during previous conversions
            if (baseMap == null && RestoredTextureGuids.TryGetValue(path, out string restoredGuid))
            {
                string texPath = AssetDatabase.GUIDToAssetPath(restoredGuid);
                if (!string.IsNullOrEmpty(texPath))
                {
                    baseMap = AssetDatabase.LoadAssetAtPath<Texture>(texPath);
                    if (path.Contains("/Prototype Props/"))
                    {
                        mainScale = new Vector2(2f, 2f);
                    }
                }
            }

            Color baseColor = Color.white;
            if (RestoredBaseColors.TryGetValue(path, out Color restoredCol))
            {
                baseColor = restoredCol;
            }
            else if (mat.HasProperty("_BaseColor") && mat.shader.name != "HDRP/Particles/Unlit")
            {
                baseColor = mat.GetColor("_BaseColor");
            }
            else if (mat.HasProperty("_UnlitColor"))
            {
                baseColor = mat.GetColor("_UnlitColor");
            }
            else if (mat.HasProperty("_Color"))
            {
                baseColor = mat.GetColor("_Color");
            }

            Texture normalMap = GetFirstTexture(mat, "_NormalMap", "_BumpMap");
            float normalScale = mat.HasProperty("_NormalScale") ? mat.GetFloat("_NormalScale") : (mat.HasProperty("_BumpScale") ? mat.GetFloat("_BumpScale") : 1.0f);
            Texture maskMap = GetFirstTexture(mat, "_MaskMap", "_MetallicGlossMap");
            float metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0.0f;
            float smoothness = mat.HasProperty("_Smoothness") ? mat.GetFloat("_Smoothness") : (mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : 0.45f);
            Texture emissionMap = GetFirstTexture(mat, "_EmissiveColorMap", "_EmissionMap");
            Color emissionColor = mat.HasProperty("_EmissiveColor") ? mat.GetColor("_EmissiveColor") : (mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black);

            if (mat.shader != hdrpLit)
            {
                mat.shader = hdrpLit;
            }

            if (baseMap != null)
            {
                mat.SetTexture("_BaseColorMap", baseMap);
                mat.SetTextureScale("_BaseColorMap", mainScale);
                mat.SetTextureOffset("_BaseColorMap", mainOffset);
                mat.SetTexture("_MainTex", baseMap);
                mat.SetTextureScale("_MainTex", mainScale);
                mat.SetTextureOffset("_MainTex", mainOffset);
            }

            // Fix AmmoBox.mat where Standard stored smoothness in Albedo Alpha (0.08)
            string fileName = Path.GetFileNameWithoutExtension(path);
            string lowerName = fileName.ToLowerInvariant();
            if (lowerName == "ammobox")
            {
                baseColor = Color.white;
            }

            // Detect transparent glass / windows / water
            bool isGlassOrTransparent = lowerName.Contains("glass")
                || lowerName.Contains("window")
                || lowerName == "waterbottle"
                || lowerName == "m_lakewater"
                || lowerName == "transparentmat";

            if (isGlassOrTransparent)
            {
                // Ensure glass isn't pitch black (e.g., (0.05, 0.05, 0.05))
                if (baseColor.maxColorComponent < 0.2f && lowerName != "transparentmat")
                {
                    baseColor = new Color(0.65f, 0.78f, 0.88f, Mathf.Clamp(baseColor.a, 0.25f, 0.45f));
                }
                else
                {
                    baseColor.a = Mathf.Clamp(baseColor.a < 0.95f ? baseColor.a : 0.35f, 0.2f, 0.65f);
                }
                mat.SetFloat("_SurfaceType", 1f);
                mat.SetFloat("_BlendMode", 0f);
                smoothness = Mathf.Max(smoothness, 0.85f);
            }
            else
            {
                baseColor.a = 1.0f;
                mat.SetFloat("_SurfaceType", 0f);
            }

            mat.SetColor("_BaseColor", baseColor);
            mat.SetColor("_Color", baseColor);

            if (normalMap != null)
            {
                mat.SetTexture("_NormalMap", normalMap);
                mat.SetFloat("_NormalScale", Mathf.Clamp(normalScale, 0.1f, 2.0f));
            }

            // Fix _MaskMap channel mismatch:
            // Standard/URP _MetallicGlossMap has R=Metallic, G=0 (black!), B=0, A=Smoothness.
            // In HDRP/Lit, _MaskMap.g is Ambient Occlusion (AO). If AO remap is [0, 1], G=0 makes the object PITCH BLACK!
            // Also in HDRP/Lit, Metallic=1.0 zeroes out diffuse albedo (diffuse = baseColor * (1 - metallic)), making unbaked metals black!
            if (maskMap != null)
            {
                mat.SetTexture("_MaskMap", maskMap);
            }
            mat.SetFloat("_AORemapMin", 1.0f);
            mat.SetFloat("_AORemapMax", 1.0f);
            mat.SetFloat("_Metallic", Mathf.Min(metallic, 0.35f));
            mat.SetFloat("_MetallicRemapMin", 0.0f);
            mat.SetFloat("_MetallicRemapMax", 0.35f);
            mat.SetFloat("_Smoothness", Mathf.Clamp(smoothness, 0.15f, isGlassOrTransparent ? 0.95f : 0.75f));
            mat.SetFloat("_SmoothnessRemapMin", 0.1f);
            mat.SetFloat("_SmoothnessRemapMax", isGlassOrTransparent ? 0.9f : 0.65f);

            if (doubleSided)
            {
                mat.SetFloat("_DoubleSidedEnable", 1f);
            }

            if (emissionMap != null || (emissionColor.maxColorComponent > 0.001f && emissionColor != Color.white))
            {
                if (emissionMap != null) mat.SetTexture("_EmissiveColorMap", emissionMap);
                mat.SetColor("_EmissiveColor", emissionColor);
                mat.SetColor("_EmissiveColorLDR", emissionColor);
                mat.SetFloat("_EmissiveIntensity", 1.0f);
            }

            HDShaderUtils.ResetMaterialKeywords(mat);
        }

        private static Texture GetFirstTexture(Material mat, params string[] propNames)
        {
            foreach (string prop in propNames)
            {
                if (mat.HasProperty(prop))
                {
                    Texture t = mat.GetTexture(prop);
                    if (t != null) return t;
                }
            }
            return null;
        }

        public static int FixAllPrefabsAndFX()
        {
            int fixedCount = 0;

            // 1. Fix MuzzleFlash.prefab missing ParticleSystemRenderer materials
            Material muzzleFlashMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Nokobot/Modern Guns - Handgun/Effects/MuzzleFlash.mat")
                ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/Nokobot/Modern Guns - Handgun/Effects/Materials/MuzzleFlash.mat");
            GameObject muzzlePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Nokobot/Modern Guns - Handgun/Effects/MuzzleFlash.prefab");
            if (muzzlePrefab != null && muzzleFlashMat != null)
            {
                bool changed = false;
                foreach (var psr in muzzlePrefab.GetComponentsInChildren<ParticleSystemRenderer>(true))
                {
                    if (psr.sharedMaterial == null)
                    {
                        psr.sharedMaterial = muzzleFlashMat;
                        changed = true;
                    }
                }
                if (changed)
                {
                    EditorUtility.SetDirty(muzzlePrefab);
                    PrefabUtility.SavePrefabAsset(muzzlePrefab);
                    fixedCount++;
                }
            }

            // 2. Fix BrakingSparks.prefab & Train Prefabs missing ParticleSystemRenderer materials
            Material sparkMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/WSM Game Studio/Train Controller_v3/Shared/Particles/Materials/ElectricalSparks.mat")
                ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/Train/FX/ElectricSpark/ElectricalSparks.mat");
            if (sparkMat != null)
            {
                string[] trainPrefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/WSM Game Studio" });
                foreach (string guid in trainPrefabGuids)
                {
                    string p = AssetDatabase.GUIDToAssetPath(guid);
                    GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                    if (go == null) continue;
                    bool changed = false;
                    foreach (var psr in go.GetComponentsInChildren<ParticleSystemRenderer>(true))
                    {
                        if (psr.sharedMaterial == null)
                        {
                            psr.sharedMaterial = sparkMat;
                            changed = true;
                        }
                    }
                    if (changed)
                    {
                        EditorUtility.SetDirty(go);
                        PrefabUtility.SavePrefabAsset(go);
                        fixedCount++;
                    }
                }
            }

            // 3. Fix M1911 Base Prefabs using Legacy Default-Diffuse
            Material m1911Black = AssetDatabase.LoadAssetAtPath<Material>("Assets/Nokobot/Modern Guns - Handgun/Materials/M1911 Handgun_Black.mat");
            Material m1911MagBlack = AssetDatabase.LoadAssetAtPath<Material>("Assets/Nokobot/Modern Guns - Handgun/Materials/M1911 Magazine_Black.mat");
            GameObject m1911Base = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Nokobot/Modern Guns - Handgun/_Prefabs/Base Prefabs/M1911 Handgun Base.prefab");
            if (m1911Base != null && m1911Black != null)
            {
                bool changed = false;
                foreach (var r in m1911Base.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (r.sharedMaterial == null || r.sharedMaterial.name.StartsWith("Default"))
                    {
                        r.sharedMaterial = m1911Black;
                        changed = true;
                    }
                }
                if (changed)
                {
                    EditorUtility.SetDirty(m1911Base);
                    PrefabUtility.SavePrefabAsset(m1911Base);
                    fixedCount++;
                }
            }
            GameObject m1911MagBase = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Nokobot/Modern Guns - Handgun/_Prefabs/Base Prefabs/M1911 Magazine Base.prefab");
            if (m1911MagBase != null && m1911MagBlack != null)
            {
                bool changed = false;
                foreach (var r in m1911MagBase.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (r.sharedMaterial == null || r.sharedMaterial.name.StartsWith("Default"))
                    {
                        r.sharedMaterial = m1911MagBlack;
                        changed = true;
                    }
                }
                if (changed)
                {
                    EditorUtility.SetDirty(m1911MagBase);
                    PrefabUtility.SavePrefabAsset(m1911MagBase);
                    fixedCount++;
                }
            }

            return fixedCount;
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

                if (terrain.terrainData != null && terrain.terrainData.treePrototypes != null)
                {
                    foreach (var proto in terrain.terrainData.treePrototypes)
                    {
                        if (proto.prefab == null) continue;
                        MeshCollider[] meshCols = proto.prefab.GetComponentsInChildren<MeshCollider>(true);
                        if (meshCols != null && meshCols.Length > 0)
                        {
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

            // 2. Replace any built-in URP package materials or null BrakingSparks materials on scene Renderers
            Material defaultHdMat = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipelineResources/Material/DefaultHDMaterial.mat");
            Material sparkMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/WSM Game Studio/Train Controller_v3/Shared/Particles/Materials/ElectricalSparks.mat")
                ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/Train/FX/ElectricSpark/ElectricalSparks.mat");
            Renderer[] renderers = UnityEngine.Object.FindObjectsOfType<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r is ParticleSystemRenderer psr && psr.sharedMaterial == null && sparkMat != null)
                {
                    psr.sharedMaterial = sparkMat;
                    EditorUtility.SetDirty(psr);
                    count++;
                    continue;
                }

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
