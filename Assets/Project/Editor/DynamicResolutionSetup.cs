using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Railbound.EditorTools
{
    /// <summary>
    /// Switches HDRP's dynamic resolution on in every HDRP asset of the project (one per quality level). It was off in all of them, so the
    /// settings menu's Render Scale, DLSS, DLSS quality and FSR did nothing in the game: HDRP only scales the image, and only runs DLSS,
    /// when dynamic resolution is enabled in the pipeline asset. Software dynamic resolution works on every graphics API; 50-100 %
    /// covers the menu's render scales (HDRP renders at most at full resolution).
    /// Menu: Railbound > Rendering > Enable Dynamic Resolution. Batch: -executeMethod Railbound.EditorTools.DynamicResolutionSetup.BatchApply
    /// </summary>
    public static class DynamicResolutionSetup
    {
        [MenuItem("Railbound/Rendering/Enable Dynamic Resolution (Render Scale, DLSS, FSR)")]
        public static void Apply()
        {
            int changed = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:HDRenderPipelineAsset"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(path);
                if (asset == null) continue;
                var so = new SerializedObject(asset);
                var dr = so.FindProperty("m_RenderPipelineSettings.dynamicResolutionSettings");
                if (dr == null) { Debug.LogWarning("[DynRes] no dynamic resolution settings in " + path); continue; }
                Set(dr, "enabled", true);
                Set(dr, "enableDLSS", true);
                Set(dr, "DLSSUseOptimalSettings", true);
                SetInt(dr, "dynResType", 0);        // software: works on every graphics API
                SetFloat(dr, "minPercentage", 50f);
                SetFloat(dr, "maxPercentage", 100f);
                SetInt(dr, "upsampleFilter", 1);    // Catmull-Rom (FSR is chosen per camera by the settings menu)
                if (so.ApplyModifiedPropertiesWithoutUndo()) { EditorUtility.SetDirty(asset); changed++; }
                Debug.Log("[DynRes] " + path + ": dynamic resolution on (software, 50-100 %), DLSS allowed.");
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[DynRes] changed " + changed + " HDRP asset(s).");
        }

        public static void BatchApply()
        {
            Apply();
            EditorApplication.Exit(0);
        }

        private static void Set(SerializedProperty parent, string name, bool value) { var p = parent.FindPropertyRelative(name); if (p != null) p.boolValue = value; else Debug.LogWarning("[DynRes] missing " + name); }
        private static void SetInt(SerializedProperty parent, string name, int value) { var p = parent.FindPropertyRelative(name); if (p != null) { if (p.propertyType == SerializedPropertyType.Enum) p.enumValueIndex = value; else p.intValue = value; } else Debug.LogWarning("[DynRes] missing " + name); }
        private static void SetFloat(SerializedProperty parent, string name, float value) { var p = parent.FindPropertyRelative(name); if (p != null) p.floatValue = value; else Debug.LogWarning("[DynRes] missing " + name); }
    }
}
