using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Railbound.UI;

namespace Railbound.Editor
{
    [InitializeOnLoad]
    public static class LoginMenuSetupHelper
    {
        private const string ScenePath = "Assets/Project/LoginMenu.unity";
        private const string EmberPrefabPath = "Assets/Project/UI/UIEmberOverlay.prefab";
        private const string GuestModalPrefabPath = "Assets/Project/UI/GuestNamePopup.prefab";

        static LoginMenuSetupHelper()
        {
            EditorApplication.delayCall += () =>
            {
                var scene = EditorSceneManager.GetActiveScene();
                if (scene.name == "LoginMenu")
                {
                    var canvas = Object.FindFirstObjectByType<Canvas>();
                    if (canvas != null && canvas.transform.Find("UIEmberOverlay") == null)
                    {
                        SetupLoginMenu();
                    }
                }
            };
        }

        [MenuItem("Railbound/Setup Login Menu VFX & UI")]
        public static void SetupLoginMenu()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != ScenePath)
            {
                activeScene = EditorSceneManager.OpenScene(ScenePath);
            }

            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[LoginMenuSetupHelper] Canvas not found in scene!");
                return;
            }

            var canvasT = canvas.transform;

            // 1. Setup Ambient VFX (UIEmberOverlay) right above LoginBackground
            var loginBg = canvasT.Find("LoginBackground");
            var existingVfx = canvasT.Find("UIEmberOverlay");
            GameObject vfxObj = null;

            if (existingVfx == null)
            {
                var vfxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EmberPrefabPath);
                if (vfxPrefab != null)
                {
                    vfxObj = (GameObject)PrefabUtility.InstantiatePrefab(vfxPrefab, canvasT);
                    vfxObj.name = "UIEmberOverlay";
                    var rt = vfxObj.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.anchorMin = Vector2.zero;
                        rt.anchorMax = Vector2.one;
                        rt.offsetMin = Vector2.zero;
                        rt.offsetMax = Vector2.zero;
                    }

                    if (loginBg != null)
                    {
                        vfxObj.transform.SetSiblingIndex(loginBg.GetSiblingIndex() + 1);
                    }
                    Debug.Log("[LoginMenuSetupHelper] UIEmberOverlay instantiated behind UI panels.");
                }
                else
                {
                    Debug.LogWarning($"[LoginMenuSetupHelper] Prefab not found at {EmberPrefabPath}");
                }
            }
            else
            {
                vfxObj = existingVfx.gameObject;
                if (loginBg != null)
                {
                    vfxObj.transform.SetSiblingIndex(loginBg.GetSiblingIndex() + 1);
                }
            }

            // 2. Setup GuestNamePopup
            var existingGuestPopup = canvasT.Find("GuestNamePopup");
            if (existingGuestPopup == null)
            {
                var guestPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GuestModalPrefabPath);
                if (guestPrefab != null)
                {
                    var guestPopupObj = (GameObject)PrefabUtility.InstantiatePrefab(guestPrefab, canvasT);
                    guestPopupObj.name = "GuestNamePopup";
                    var rt = guestPopupObj.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.anchorMin = Vector2.zero;
                        rt.anchorMax = Vector2.one;
                        rt.offsetMin = Vector2.zero;
                        rt.offsetMax = Vector2.zero;
                    }
                    Debug.Log("[LoginMenuSetupHelper] GuestNamePopup instantiated.");
                }
            }

            // 3. Setup Bottom-Left Artwork Buttons (Language, Settings, Quit)
            SetupBottomButton(canvasT, "LanguageButton", new Vector2(83f, 59f), new Vector2(95f, 55f));
            SetupBottomButton(canvasT, "SettingsButton", new Vector2(179f, 59f), new Vector2(60f, 55f));
            SetupBottomButton(canvasT, "QuitButton", new Vector2(253f, 59f), new Vector2(60f, 55f));

            // 4. Ensure LoginPanel has LoginMenuController
            var loginPanel = canvasT.Find("LoginPanel");
            if (loginPanel != null)
            {
                var controller = loginPanel.GetComponent<LoginMenuController>();
                if (controller == null)
                {
                    controller = loginPanel.gameObject.AddComponent<LoginMenuController>();
                }

                // Add RailboundButtonEffects to main buttons
                AttachButtonEffects(loginPanel.Find("GuestButton")?.gameObject);
                AttachButtonEffects(loginPanel.Find("GoogleLoginButton")?.gameObject);
                AttachButtonEffects(loginPanel.Find("MoreButton")?.gameObject);
            }

            // 5. Ensure non-interactive panels don't block clicks
            var loginDock = canvasT.Find("LoginDock")?.GetComponent<Image>();
            if (loginDock != null) loginDock.raycastTarget = false;

            var bgImg = loginBg?.GetComponent<Image>();
            if (bgImg != null) bgImg.raycastTarget = false;

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            Debug.Log("[LoginMenuSetupHelper] ✅ LoginMenu scene successfully configured and saved!");
        }

        private static void SetupBottomButton(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            var t = parent.Find(name);
            GameObject btnObj;
            if (t == null)
            {
                btnObj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                btnObj.transform.SetParent(parent, false);
            }
            else
            {
                btnObj = t.gameObject;
            }

            var rt = btnObj.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var img = btnObj.GetComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0f);
            img.raycastTarget = true;

            var btn = btnObj.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.interactable = true;

            AttachButtonEffects(btnObj);
        }

        private static void AttachButtonEffects(GameObject obj)
        {
            if (obj == null) return;
            var fx = obj.GetComponent<RailboundButtonEffects>();
            if (fx == null)
            {
                fx = obj.AddComponent<RailboundButtonEffects>();
            }

            var clickClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Reach - Complete Sci-Fi UI/Audio/Click.wav");
            var hoverClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Reach - Complete Sci-Fi UI/Audio/Hover.wav");

            var so = new SerializedObject(fx);
            var clickProp = so.FindProperty("clickSfx");
            var hoverProp = so.FindProperty("hoverSfx");
            if (clickProp != null && clickClip != null) clickProp.objectReferenceValue = clickClip;
            if (hoverProp != null && hoverClip != null) hoverProp.objectReferenceValue = hoverClip;
            so.ApplyModifiedProperties();
        }
    }
}
