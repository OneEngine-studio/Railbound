using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Railbound.Editor
{
    /// <summary>
    /// Helper to attach and position the shared UIEmberOverlay in LoadingScreen scene.
    /// </summary>
    public static class LoadingScreenSetupHelper
    {
        private const string ScenePath = "Assets/Project/LoadingScreen.unity";
        private const string EmberPrefabPath = "Assets/Project/UI/UIEmberOverlay.prefab";

        [MenuItem("Railbound/Setup LoadingScreen VFX")]
        public static void SetupLoadingScreenVFX()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != ScenePath)
            {
                activeScene = EditorSceneManager.OpenScene(ScenePath);
            }

            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[LoadingScreenSetupHelper] Canvas not found in LoadingScreen!");
                return;
            }

            var existing = canvas.transform.Find("UIEmberOverlay");
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EmberPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[LoadingScreenSetupHelper] Prefab not found at {EmberPrefabPath}!");
                return;
            }

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
            inst.name = "UIEmberOverlay";

            var rt = inst.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.localScale = Vector3.one;
            }

            var bg = canvas.transform.Find("loadingUI");
            if (bg != null)
            {
                inst.transform.SetSiblingIndex(bg.GetSiblingIndex() + 1);
            }

            foreach (var graphic in inst.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            Debug.Log("[LoadingScreenSetupHelper] UIEmberOverlay successfully attached and saved in LoadingScreen!");
        }
    }
}
