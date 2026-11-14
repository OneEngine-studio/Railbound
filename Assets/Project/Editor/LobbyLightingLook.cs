#if UNITY_EDITOR
using Railbound.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Railbound.EditorTools
{
    /// <summary>
    /// Menu "Railbound / Apply Lobby Lighting Look": sets the lobby's light rig, post-processing and anti-aliasing to the tuned portrait look
    /// (a strong warm key, a face light so the eyes read, cool and warm rim lights that cut the silhouette out of a darker background, a
    /// little volumetric haze, ACES grade with more contrast and less grain, temporal anti-aliasing). The values were tuned by eye in Play
    /// Mode; running the tool again puts them back after anyone moves a slider. It edits the open GameLobby scene and the
    /// Lobby_AAA_Profile asset, then saves both.
    /// </summary>
    public static class LobbyLightingLook
    {
        private const string ScenePath = "Assets/Project/GameLobby.unity";
        private const string ProfilePath = "Assets/Project/Settings/Lobby_AAA_Profile.asset";

        private struct Rig { public string name; public float candela; public Color? color; public float angle; public float volumetric; }

        private static readonly Rig[] Rigs =
        {
            new Rig { name = "Key_Warm", candela = 28000f, color = new Color(1f, 0.84f, 0.66f), angle = -1f, volumetric = -1f },
            new Rig { name = "Fill_Party_L", candela = 6000f, angle = -1f, volumetric = -1f },
            new Rig { name = "Fill_Party_R", candela = 22000f, color = new Color(0.84f, 0.9f, 1f), angle = 55f, volumetric = -1f },
            new Rig { name = "Fill_Cool", candela = 200f, angle = -1f, volumetric = -1f },
            new Rig { name = "Rim_Cool_L", candela = 60000f, angle = 40f, volumetric = 1.4f },
            new Rig { name = "Rim_Warm_R", candela = 48000f, angle = 40f, volumetric = 1.4f },
            new Rig { name = "Wash_BackWall", candela = 2600f, angle = -1f, volumetric = -1f },
            new Rig { name = "Wash_LeftWall", candela = 1800f, angle = -1f, volumetric = -1f },
            new Rig { name = "Practical_BackRight", candela = 6000f, angle = -1f, volumetric = -1f },
            new Rig { name = "Practical_BackLeft", candela = 6500f, angle = -1f, volumetric = -1f },
            new Rig { name = "Practical_Mid", candela = 4200f, angle = -1f, volumetric = -1f },
        };

        [MenuItem("Railbound/Apply Lobby Lighting Look")]
        public static void Apply()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath);

            var root = GameObject.Find("Lobby_AAA_Lighting");
            if (root == null) { Debug.LogWarning("[LobbyLook] Lobby_AAA_Lighting not found in " + ScenePath); return; }

            foreach (var r in Rigs)
            {
                var t = root.transform.Find(r.name);
                if (t == null) { Debug.LogWarning("[LobbyLook] Missing light " + r.name); continue; }
                var l = t.GetComponent<Light>(); var hd = t.GetComponent<HDAdditionalLightData>();
                if (l == null || hd == null) continue;
                hd.SetIntensity(r.candela, LightUnit.Candela);
                if (r.color.HasValue) l.color = r.color.Value;
                if (r.angle > 0f) l.spotAngle = r.angle;
                if (r.volumetric >= 0f) hd.volumetricDimmer = r.volumetric;
                EditorUtility.SetDirty(l); EditorUtility.SetDirty(hd);
            }

            // the right fill is aimed at the fourth squad member, who stands back and to the right and would otherwise be lost in the dark
            var fillR = root.transform.Find("Fill_Party_R");
            if (fillR != null) { fillR.rotation = Quaternion.LookRotation(new Vector3(2.2f, 1.0f, 0.4f) - fillR.position); EditorUtility.SetDirty(fillR); }

            // a soft light on the face, so the eyes read under the helmet
            var face = root.transform.Find("Face_Light");
            if (face == null)
            {
                var go = new GameObject("Face_Light");
                go.transform.SetParent(root.transform, false);
                go.AddHDLight(HDLightTypeAndShape.ConeSpot);
                face = go.transform;
                Undo.RegisterCreatedObjectUndo(go, "Create Face_Light");
            }
            {
                var l = face.GetComponent<Light>(); var hd = face.GetComponent<HDAdditionalLightData>();
                l.color = new Color(1f, 0.95f, 0.88f); l.spotAngle = 38f;
                hd.innerSpotPercent = 40f; hd.EnableShadows(false); hd.volumetricDimmer = 0f;
                hd.SetIntensity(2800f, LightUnit.Candela);
                face.position = new Vector3(0.45f, 1.95f, -2.3f);
                face.rotation = Quaternion.LookRotation(new Vector3(-0.1f, 1.5f, 0f) - face.position);
                EditorUtility.SetDirty(l); EditorUtility.SetDirty(hd);
            }

            // the director's own spotlight and wide fill
            var director = Object.FindFirstObjectByType<LobbyStageDirector>();
            if (director != null)
            {
                var so = new SerializedObject(director);
                var spot = so.FindProperty("spotlightIntensity"); if (spot != null) spot.floatValue = 12000f;
                var fill = so.FindProperty("fillIntensity"); if (fill != null) fill.floatValue = 26000f;

                // framing: far enough back that the operator (and the nameplate above the head) sits below the top navigation bar
                var solo = so.FindProperty("soloPosition"); if (solo != null) solo.vector3Value = new Vector3(0f, 1.15f, -4.6f);
                var party = so.FindProperty("partyPosition"); if (party != null) party.vector3Value = new Vector3(0f, 1.25f, -5.2f);
                var look = so.FindProperty("lookTarget"); if (look != null) look.vector3Value = new Vector3(0f, 1.1f, 0f);
                so.ApplyModifiedProperties();
            }

            // grade
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile != null)
            {
                if (profile.TryGet<FilmGrain>(out var fg)) { fg.intensity.overrideState = true; fg.intensity.value = 0.12f; }
                if (profile.TryGet<ColorAdjustments>(out var ca)) { ca.contrast.value = 24f; ca.saturation.value = 6f; }
                if (profile.TryGet<Vignette>(out var vg)) vg.intensity.value = 0.36f;
                if (profile.TryGet<Bloom>(out var bl)) bl.intensity.value = 0.35f;
                if (profile.TryGet<Fog>(out var fog)) fog.meanFreePath.value = 46f;
                EditorUtility.SetDirty(profile);
            }
            else Debug.LogWarning("[LobbyLook] Profile not found at " + ProfilePath);

            // temporal anti-aliasing: clean edges on the operator without the grain of FXAA
            var cam = Camera.main != null ? Camera.main.GetComponent<HDAdditionalCameraData>() : null;
            if (cam != null) { cam.antialiasing = HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing; EditorUtility.SetDirty(cam); }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[LobbyLook] Applied the lobby lighting look and saved " + ScenePath);
        }
    }
}
#endif
