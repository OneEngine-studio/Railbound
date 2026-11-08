#if UNITY_EDITOR
using Railbound.Gameplay;
using Railbound.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Railbound.Editor
{
    public static class Map1MiniMapSetup
    {
        /// <summary>
        /// Makes sure MAP-2 has a MiniMapRoot with the <see cref="Map1MiniMapController"/> (the controller builds the whole
        /// minimap / full map UI itself at runtime from the terrain) and that the player has a sonar skill.
        /// </summary>
        [MenuItem("Tools/Setup MAP-2 Minimap and Sonar")]
        public static string Setup()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (!activeScene.name.Contains("MAP-2"))
            {
                activeScene = EditorSceneManager.OpenScene("Assets/TerrainDemoScene_HDRP/Scenes/MAP-2.unity");
            }

            var uiParentGO = GameObject.Find("User Interface/UI");
            if (uiParentGO == null) uiParentGO = GameObject.Find("User Interface");
            if (uiParentGO == null) return "Error: User Interface not found in MAP-2!";

            Transform existing = uiParentGO.transform.Find("MiniMapRoot");
            GameObject rootGO;
            if (existing != null) rootGO = existing.gameObject;
            else
            {
                rootGO = new GameObject("MiniMapRoot", typeof(RectTransform));
                rootGO.transform.SetParent(uiParentGO.transform, false);
                Undo.RegisterCreatedObjectUndo(rootGO, "Create MiniMapRoot");
            }
            if (rootGO.GetComponent<Map1MiniMapController>() == null) rootGO.AddComponent<Map1MiniMapController>();

            var playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO != null)
            {
                var sonarSkill = playerGO.GetComponent<PlayerSonarSkill>();
                if (sonarSkill == null) sonarSkill = playerGO.AddComponent<PlayerSonarSkill>();
                sonarSkill.triggerKey = KeyCode.I;
                sonarSkill.scanRadius = 1600f;
                sonarSkill.waveSpeed = 180f;
                sonarSkill.cooldown = 10f;
                sonarSkill.revealDuration = 12f;

                var sonarParent = Object.FindAnyObjectByType<SimpleSonarShader_Parent>();
                if (sonarParent == null) new GameObject("SimpleSonarShader_Parent").AddComponent<SimpleSonarShader_Parent>();
            }

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            return "SUCCESS: MAP-2 minimap controller and sonar skill configured and saved.";
        }
    }
}
#endif
