using System.Collections.Generic;
using System.Text;
using Project.Character;
using UnityEditor;
using UnityEngine;

namespace Railbound.Editor
{
    /// <summary>Bakes the FBX characters in Assets/Project/character into skins, and removes the earlier glb characters.</summary>
    public static class CharacterSkinImport
    {
        private const string Root = "Assets/Project/character";
        private static readonly string[] OldGlbFolders = { "federation", "ghost", "modern_tactical_soldier", "s.w.a.t._operator" };
        private static readonly string[] OldSkinIds = { "federation", "ghost", "modern_soldier" };

        private static Texture2D Tex(string folder, string namePrefix)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileName(path).StartsWith(namePrefix, System.StringComparison.OrdinalIgnoreCase)) return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            return null;
        }

        private static void AsNormalMap(Texture2D t)
        {
            if (t == null) return;
            var imp = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(t)) as TextureImporter;
            if (imp != null && imp.textureType != TextureImporterType.NormalMap) { imp.textureType = TextureImporterType.NormalMap; imp.SaveAndReimport(); }
        }

        private static Material MakeMaterial(string id, string name, Texture2D albedo, Texture2D normal, Color color, float metallic, float smoothness)
        {
            string dir = CharacterSkinBaker.OutputDir + "/Materials";
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(CharacterSkinBaker.OutputDir, "Materials");
            string path = dir + "/" + id + "_" + name.Replace(' ', '_') + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("HDRP/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.name = id + "_" + name;
            if (albedo != null) m.SetTexture("_BaseColorMap", albedo);
            m.SetColor("_BaseColor", color);
            if (normal != null) { AsNormalMap(normal); m.SetTexture("_NormalMap", normal); m.EnableKeyword("_NORMALMAP"); }
            m.SetFloat("_Metallic", metallic); m.SetFloat("_Smoothness", smoothness);
            UnityEditor.Rendering.HighDefinition.HDShaderUtils.ResetMaterialKeywords(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Deletes the glb characters (they have no skeleton) with their baked skins and catalog entries.</summary>
        public static string RemoveGlbCharacters()
        {
            var sb = new StringBuilder();
            foreach (var f in OldGlbFolders)
            {
                string path = Root + "/" + f;
                sb.AppendLine((AssetDatabase.IsValidFolder(path) && AssetDatabase.DeleteAsset(path) ? "deleted " : "not found ") + path);
            }
            foreach (var id in OldSkinIds) sb.AppendLine(CharacterSkinSetup.DeleteBakedSkin(id));
            sb.AppendLine(CharacterSkinSetup.DeleteBakedSkin("swat"));
            return sb.ToString();
        }

        public static string BakeAll()
        {
            var sb = new StringBuilder();

            // ---- Cammy: nothing was connected, the textures use another naming
            string cammyTex = Root + "/cammy-game-ready-character/textures";
            var cammy = new Dictionary<string, Material>
            {
                { "clothes", MakeMaterial("cammy", "clothes", Tex(cammyTex, "RTC5_export_clothes_BaseColor"), Tex(cammyTex, "RTC5_export_clothes_Normal"), Color.white, 0.1f, 0.35f) },
                { "set_1", MakeMaterial("cammy", "skin", Tex(cammyTex, "RTC5_export_set_1_BaseColor"), Tex(cammyTex, "RTC5_export_set_1_Normal"), Color.white, 0f, 0.35f) },
                { "set_1 Subsur", MakeMaterial("cammy", "skin_subsur", Tex(cammyTex, "RTC5_export_set_1_BaseColor"), Tex(cammyTex, "RTC5_export_set_1_Normal"), Color.white, 0f, 0.35f) },
                { "hair", MakeMaterial("cammy", "hair", null, null, new Color(0.07f, 0.045f, 0.03f), 0f, 0.12f) },
                { "DefaultHDMaterial", MakeMaterial("cammy", "hair2", null, null, new Color(0.07f, 0.045f, 0.03f), 0f, 0.12f) },
            };

            // ---- Tactical: the plate and the headphones have only their normal map connected
            string tacTex = Root + "/game-ready-low-poly-character-tactical/textures";
            var tactical = new Dictionary<string, Material>
            {
                { "plate", MakeMaterial("tactical", "plate", Tex(tacTex, "ArmorPlateD_albedo"), Tex(tacTex, "ArmorPlateD_normal"), Color.white, 0.4f, 0.4f) },
                { "Headphones", MakeMaterial("tactical", "headphones", Tex(tacTex, "HeadPhT_albedo"), Tex(tacTex, "HeadPhT_normal"), Color.white, 0.3f, 0.4f) },
            };
            AssetDatabase.SaveAssets();

            sb.AppendLine("== striker"); sb.AppendLine(CharacterSkinBaker.Bake("striker", Root + "/jumpsuit-low-poly-game-ready-striker14/source/Striker14.fbx"));
            sb.AppendLine("== cammy"); sb.AppendLine(CharacterSkinBaker.Bake("cammy", Root + "/cammy-game-ready-character/source/RTC7d.fbx", cammy));
            sb.AppendLine("== tactical"); sb.AppendLine(CharacterSkinBaker.Bake("tactical", Root + "/game-ready-low-poly-character-tactical/source/Tactical.fbx", tactical));
            sb.AppendLine("== swat"); sb.AppendLine(CharacterSkinBaker.Bake("swat", Root + "/game-ready-swat-character/source/Swat.fbx"));
            return sb.ToString();
        }
    }
}
