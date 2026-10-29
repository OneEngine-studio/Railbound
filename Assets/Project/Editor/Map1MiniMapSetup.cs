#if UNITY_EDITOR
using System.Linq;
using Arikan;
using Railbound.Gameplay;
using Railbound.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Railbound.Editor
{
    public static class Map1MiniMapSetup
    {
        [MenuItem("Tools/Setup MAP-1 Minimap and Sonar")]
        public static string Setup()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (!activeScene.name.Contains("MAP-1"))
            {
                EditorSceneManager.OpenScene("Assets/Project/MAP-1.unity");
            }

            // 1. Create or update MiniMapBounds
            var boundsGO = GameObject.Find("MiniMapBounds");
            if (boundsGO == null)
            {
                boundsGO = new GameObject("MiniMapBounds");
                Undo.RegisterCreatedObjectUndo(boundsGO, "Create MiniMapBounds");
            }

            var boundsComp = boundsGO.GetComponent<MiniMapBounds>();
            if (boundsComp == null) boundsComp = boundsGO.AddComponent<MiniMapBounds>();
            boundsComp.plane = MiniMapPlane.XZ;

            var bl = boundsGO.transform.Find("BottomLeft");
            if (bl == null)
            {
                var blGO = new GameObject("BottomLeft");
                blGO.transform.SetParent(boundsGO.transform, false);
                bl = blGO.transform;
            }
            bl.position = new Vector3(-800f, 0f, -800f);

            var tr = boundsGO.transform.Find("TopRight");
            if (tr == null)
            {
                var trGO = new GameObject("TopRight");
                trGO.transform.SetParent(boundsGO.transform, false);
                tr = trGO.transform;
            }
            tr.position = new Vector3(800f, 0f, 800f);

            boundsComp.bottomLeft = bl;
            boundsComp.topRight = tr;

            // 2. Locate HUD Canvas
            var uiParentGO = GameObject.Find("User Interface/UI");
            if (uiParentGO == null) uiParentGO = GameObject.Find("User Interface");
            if (uiParentGO == null)
            {
                return "Error: User Interface not found in MAP-1!";
            }

            // 3. Load Baked Map Sprite and MiniMap Assets
            var mapSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Project/Textures/MAP1_MiniMap.png");
            var arrowSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Reach - Complete Sci-Fi UI/Textures/Icons/HUD/Map Arrow (64x).png");
            if (arrowSprite == null) arrowSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UnitySimpleMiniMap/Demo/Arrow.png");
            var circleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UnitySimpleMiniMap/Demo/4_Circle.png");
            var maskSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UnitySimpleMiniMap/Demo/PictureMask.png");
            var dotPrefab = AssetDatabase.LoadAssetAtPath<Image>("Assets/UnitySimpleMiniMap/Demo/uiDot1.prefab");

            // 4. Create or configure Minimap Root in HUD
            Transform existingMinimap = uiParentGO.transform.Find("MiniMapRoot");
            if (existingMinimap != null)
            {
                Object.DestroyImmediate(existingMinimap.gameObject);
            }

            var rootGO = new GameObject("MiniMapRoot", typeof(RectTransform));
            rootGO.transform.SetParent(uiParentGO.transform, false);
            var rootRt = rootGO.GetComponent<RectTransform>();

            // Anchor Top-Left
            rootRt.anchorMin = new Vector2(0f, 1f);
            rootRt.anchorMax = new Vector2(0f, 1f);
            rootRt.pivot = new Vector2(0f, 1f);
            rootRt.anchoredPosition = new Vector2(24f, -24f);
            rootRt.sizeDelta = new Vector2(240f, 240f);

            // Frame / Background border (Sci-Fi Dark styling)
            var frameGO = new GameObject("Frame", typeof(RectTransform));
            frameGO.transform.SetParent(rootRt, false);
            var frameRt = frameGO.GetComponent<RectTransform>();
            frameRt.anchorMin = Vector2.zero;
            frameRt.anchorMax = Vector2.one;
            frameRt.sizeDelta = Vector2.zero;
            frameRt.anchoredPosition = Vector2.zero;

            var frameImg = frameGO.AddComponent<Image>();
            frameImg.sprite = maskSprite;
            frameImg.color = new Color(0.02f, 0.03f, 0.04f, 0.95f);

            // Sci-Fi Outer Ring
            var ringGO = new GameObject("RingAccent", typeof(RectTransform));
            ringGO.transform.SetParent(rootRt, false);
            var ringRt = ringGO.GetComponent<RectTransform>();
            ringRt.anchorMin = Vector2.zero;
            ringRt.anchorMax = Vector2.one;
            ringRt.sizeDelta = new Vector2(6f, 6f);
            ringRt.anchoredPosition = Vector2.zero;
            var ringImg = ringGO.AddComponent<Image>();
            ringImg.sprite = maskSprite;
            ringImg.color = new Color(0.0f, 0.95f, 1.0f, 0.45f);

            // Viewport Mask
            var maskGO = new GameObject("Mask", typeof(RectTransform));
            maskGO.transform.SetParent(rootRt, false);
            var maskRt = maskGO.GetComponent<RectTransform>();
            maskRt.anchorMin = Vector2.zero;
            maskRt.anchorMax = Vector2.one;
            maskRt.sizeDelta = new Vector2(-12f, -12f);
            maskRt.anchoredPosition = Vector2.zero;

            var maskImg = maskGO.AddComponent<Image>();
            maskImg.sprite = maskSprite;
            maskImg.color = Color.white;

            var maskComp = maskGO.AddComponent<Mask>();
            maskComp.showMaskGraphic = false;

            // Full Map Canvas (Child of Mask)
            var mapCanvasGO = new GameObject("FullMapCanvas", typeof(RectTransform));
            mapCanvasGO.transform.SetParent(maskRt, false);
            var mapCanvasRt = mapCanvasGO.GetComponent<RectTransform>();
            mapCanvasRt.anchorMin = new Vector2(0.5f, 0.5f);
            mapCanvasRt.anchorMax = new Vector2(0.5f, 0.5f);
            mapCanvasRt.pivot = new Vector2(0.5f, 0.5f);
            mapCanvasRt.sizeDelta = new Vector2(800f, 800f);
            mapCanvasRt.anchoredPosition = Vector2.zero;

            var mapImg = mapCanvasGO.AddComponent<Image>();
            mapImg.sprite = mapSprite;
            mapImg.color = new Color(0.85f, 0.9f, 0.88f, 1.0f);

            // MiniMapView component on MiniMapRoot
            var miniMapView = rootGO.AddComponent<MiniMapView>();
            miniMapView.centeredDotCanvas = maskRt;
            miniMapView.otherDotCanvas = mapCanvasRt;
            miniMapView.miniMapBounds = boundsComp;
            miniMapView.defaultSprite = circleSprite;
            miniMapView.uiDotPrefab = dotPrefab;
            miniMapView.RotationMode = MiniMapRotationMode.RotateWithTarget;
            miniMapView.SetZoomLimits(0.8f, 3.5f);
            miniMapView.Zoom = 1.6f;

            // Map1MiniMapController
            var controller = rootGO.AddComponent<Map1MiniMapController>();
            controller.miniMap = miniMapView;
            controller.bounds = boundsComp;
            controller.playerArrowSprite = arrowSprite;
            controller.enemyDotSprite = circleSprite;
            controller.enemyPingedSprite = circleSprite;
            controller.playerColor = new Color(0.0f, 0.95f, 1.0f, 1.0f);
            controller.enemyIdleColor = new Color(1.0f, 0.25f, 0.25f, 0.8f);
            controller.enemyPingedColor = new Color(1.0f, 0.15f, 0.15f, 1.0f);

            // 5. Setup Player Sonar Skill on TPS Character
            var playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO != null)
            {
                var sonarSkill = playerGO.GetComponent<PlayerSonarSkill>();
                if (sonarSkill == null) sonarSkill = playerGO.AddComponent<PlayerSonarSkill>();
                sonarSkill.triggerKey = KeyCode.I;
                sonarSkill.scanRadius = 1600f; // Full map
                sonarSkill.waveSpeed = 180f;   // 180 m/s
                sonarSkill.cooldown = 10f;
                sonarSkill.revealDuration = 12f;

                var sonarParent = Object.FindAnyObjectByType<SimpleSonarShader_Parent>();
                if (sonarParent == null)
                {
                    var parentGO = new GameObject("SimpleSonarShader_Parent");
                    sonarParent = parentGO.AddComponent<SimpleSonarShader_Parent>();
                }
            }

            // Save Scene
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            return "SUCCESS: MAP-1 Full-Map Minimap and Sonar X-Ray Skill configured and saved!";
        }
    }
}
#endif
