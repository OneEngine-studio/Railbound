using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Railbound.UI;

namespace Railbound.Editor
{
    public static class UIManagerBuilder
    {
        private const string PrefabPath = "Assets/Project/UI/UIManager.prefab";

        [MenuItem("Railbound/Create or Setup UIManager Prefab")]
        public static void CreateUIManagerPrefab()
        {
            if (!Directory.Exists("Assets/Project/UI"))
                Directory.CreateDirectory("Assets/Project/UI");

            var root = new GameObject("UIManager", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(UIManager));
            var uiManager = root.GetComponent<UIManager>();

            ConfigureUIManager(root, uiManager);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            // Also update any UIManager in active scene
            var sceneMgr = Object.FindFirstObjectByType<UIManager>();
            if (sceneMgr != null)
            {
                ConfigureUIManager(sceneMgr.gameObject, sceneMgr);
                EditorUtility.SetDirty(sceneMgr);
                var activeScene = EditorSceneManager.GetActiveScene();
                EditorSceneManager.MarkSceneDirty(activeScene);
                EditorSceneManager.SaveScene(activeScene);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[UIManagerBuilder] Successfully configured {PrefabPath} and scene instances.");
        }

        public static void ConfigureUIManager(GameObject root, UIManager uiManager)
        {
            var canvas = root.GetComponent<Canvas>();
            if (canvas == null) canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = root.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            uiManager.EnsureCanvasAndLayers();

            var so = new SerializedObject(uiManager);

            // Wire layers
            so.FindProperty("rootCanvas").objectReferenceValue = canvas;
            so.FindProperty("backgroundLayer").objectReferenceValue = root.transform.Find("BackgroundLayer");
            so.FindProperty("screenLayer").objectReferenceValue = root.transform.Find("ScreenLayer");
            so.FindProperty("hudLayer").objectReferenceValue = root.transform.Find("HUDLayer");
            so.FindProperty("modalLayer").objectReferenceValue = root.transform.Find("ModalLayer");
            so.FindProperty("chatLayer").objectReferenceValue = root.transform.Find("ChatLayer");
            so.FindProperty("loadingLayer").objectReferenceValue = root.transform.Find("LoadingLayer");

            // Wire prefabs
            so.FindProperty("settingsViewPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/UI/SettingsView.prefab");
            so.FindProperty("matchmakingViewPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/UI/MatchmakingView.prefab");
            so.FindProperty("hudViewPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/UI/HUDView.prefab");
            so.FindProperty("loadoutViewPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/UI/LoadoutView.prefab");
            so.FindProperty("gameOverViewPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/UI/GameOverView.prefab");
            so.FindProperty("pauseMenuModalPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/UI/PauseMenuModal.prefab");
            so.FindProperty("scoreboardOverlayPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/UI/ScoreboardOverlay.prefab");
            so.FindProperty("confirmationModalPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/UI/ConfirmationModal.prefab");
            so.FindProperty("guestNamePopupPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/UI/GuestNamePopup.prefab");
            so.FindProperty("globalChatDrawerPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/UI/GlobalChatDrawer.prefab");
            so.FindProperty("levelUpUnlockModalPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/UI/LevelUpUnlockModal.prefab");
            so.FindProperty("emberOverlayPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/UI/UIEmberOverlay.prefab");

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(uiManager);
        }
    }
}
