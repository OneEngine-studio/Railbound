using UnityEngine;
using UnityEditor;
using WSMGameStudio.Splines;

namespace WSMGameStudio.RailroadSystem
{
    public class TC_CreateObjectMenus
    {
        [MenuItem("WSM Game Studio/Train Controller/Create/Railroad Builder (Physics Based)", false, 10)]
        [MenuItem("GameObject/WSM Game Studio/Railroad Builder (Physics Based)", false, 10)]
        static void CreatePhysicsBasedRailroadBuilder(MenuCommand menuCommand)
        {
            SMR_MeshGenerationProfile generationProfile = (SMR_MeshGenerationProfile)LocateScriptableObject("Assets/WSM Game Studio/Train Controller_v3/Railroad Builder/Generation Profiles/Railroad Builder (Physics Based).asset");

            CreateRailroadBuilder(menuCommand, generationProfile);
        }

        [MenuItem("WSM Game Studio/Train Controller/Create/Railroad Builder (Spline Based)", false, 10)]
        [MenuItem("GameObject/WSM Game Studio/Railroad Builder (Spline Based)", false, 10)]
        static void CreateSplineBasedRailroadBuilder(MenuCommand menuCommand)
        {
            SMR_MeshGenerationProfile generationProfile = (SMR_MeshGenerationProfile)LocateScriptableObject("Assets/WSM Game Studio/Train Controller_v3/Railroad Builder/Generation Profiles/Railroad Builder (Spline Based).asset");

            CreateRailroadBuilder(menuCommand, generationProfile);
        }

        /// <summary>
        /// Create railroad builder and apply profile
        /// </summary>
        /// <param name="menuCommand"></param>
        /// <param name="generationProfile"></param>
        private static void CreateRailroadBuilder(MenuCommand menuCommand, SMR_MeshGenerationProfile generationProfile)
        {
            GameObject go = CreateAndSelectNewObject(menuCommand, "RailroadBuilder");

            GameObject aux1 = new GameObject("Aux1");
            GameObject aux2 = new GameObject("Aux2");
            aux1.transform.SetParent(go.transform);
            aux2.transform.SetParent(go.transform);

            Spline spline = go.AddComponent<Spline>();
            spline.Theme = (SMR_Theme)LocateScriptableObject("Assets/WSM Game Studio/Train Controller_v3/Railroad Builder/Themes/Railroad-Theme.asset");
            spline.NewCurveLength = 100f;
            spline.ResetLastCurve(); //Update first curve length

            SplineMeshRenderer splineMeshRenderer = go.AddComponent<SplineMeshRenderer>();
            splineMeshRenderer.Spline = spline;
            splineMeshRenderer.MeshGenerationProfile = generationProfile;
            splineMeshRenderer.ExtrudeMesh();
        }

        /// <summary>
        /// Tries to locate ScriptableObject by path
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private static ScriptableObject LocateScriptableObject(string path)
        {
            ScriptableObject so = AssetDatabase.LoadAssetAtPath(path, typeof(ScriptableObject)) as ScriptableObject;
            return so;
        }

        [MenuItem("WSM Game Studio/Train Controller/Create/Railroad Control Zones/Custom Event Zone", false, 10)]
        [MenuItem("GameObject/WSM Game Studio/Railroad Control Zones/Custom Event Zone", false, 10)]
        static void CreateCustomEventZone(MenuCommand menuCommand)
        {
            GameObject go = CreateAndSelectNewObject(menuCommand, "CustomEventZone");
            CreateTrigger(ref go);
            go.AddComponent<CustomEventZone>();
        }

        [MenuItem("WSM Game Studio/Train Controller/Create/Railroad Control Zones/Bell Zone", false, 10)]
        [MenuItem("GameObject/WSM Game Studio/Railroad Control Zones/Bell Zone", false, 10)]
        static void CreateBellZone(MenuCommand menuCommand)
        {
            GameObject go = CreateAndSelectNewObject(menuCommand, "BellZone");
            CreateTrigger(ref go);
            go.AddComponent<BellZone>();
        }

        [MenuItem("WSM Game Studio/Train Controller/Create/Railroad Control Zones/Honk Zone", false, 10)]
        [MenuItem("GameObject/WSM Game Studio/Railroad Control Zones/Honk Zone", false, 10)]
        static void CreateHonkZone(MenuCommand menuCommand)
        {
            GameObject go = CreateAndSelectNewObject(menuCommand, "HonkZone");
            CreateTrigger(ref go);
            go.AddComponent<HonkZone_v3>();
        }

        [MenuItem("WSM Game Studio/Train Controller/Create/Railroad Control Zones/Reverse Direction Zone", false, 10)]
        [MenuItem("GameObject/WSM Game Studio/Railroad Control Zones/Reverse Direction Zone", false, 10)]
        static void CreateReverseDirectionZone(MenuCommand menuCommand)
        {
            GameObject go = CreateAndSelectNewObject(menuCommand, "ReverseDirectionZone");
            CreateTrigger(ref go);
            go.AddComponent<ReverseDirectionZone>();
        }

        [MenuItem("WSM Game Studio/Train Controller/Create/Railroad Control Zones/Speed Change Zone", false, 10)]
        [MenuItem("GameObject/WSM Game Studio/Railroad Control Zones/Speed Change Zone", false, 10)]
        static void CreateSpeedChangeZone(MenuCommand menuCommand)
        {
            GameObject go = CreateAndSelectNewObject(menuCommand, "SpeedChangeZone");
            CreateTrigger(ref go);
            go.AddComponent<SpeedChangeZone_v3>();
        }

        [MenuItem("WSM Game Studio/Train Controller/Create/Railroad Control Zones/Station Stop Zone", false, 10)]
        [MenuItem("GameObject/WSM Game Studio/Railroad Control Zones/Station Stop Zone", false, 10)]
        static void CreateStationStopZone(MenuCommand menuCommand)
        {
            GameObject go = CreateAndSelectNewObject(menuCommand, "StationStopZone");
            CreateTrigger(ref go);
            go.AddComponent<StationStopTrigger>();
        }

        [MenuItem("WSM Game Studio/Train Controller/Create/Railroad Control Zones/Switch Trigger Zone", false, 10)]
        [MenuItem("GameObject/WSM Game Studio/Railroad Control Zones/Switch Trigger Zone", false, 10)]
        static void CreateSwitchTriggerZone(MenuCommand menuCommand)
        {
            GameObject go = CreateAndSelectNewObject(menuCommand, "SwitchTriggerZone");
            CreateTrigger(ref go);
            go.AddComponent<SwitchTrigger>();
        }

        [MenuItem("WSM Game Studio/Train Controller/Create/Route Manager", false, 10)]
        [MenuItem("GameObject/WSM Game Studio/Route Manager", false, 10)]
        static void CreateRouteManager(MenuCommand menuCommand)
        {
            GameObject go = CreateAndSelectNewObject(menuCommand, "RouteManager", true);
            go.AddComponent<RouteManager>();
        }

        [MenuItem("WSM Game Studio/Train Controller/Create/Train Spawner", false, 10)]
        [MenuItem("GameObject/WSM Game Studio/Train Spawner", false, 10)]
        static void CreateTrainSpawner(MenuCommand menuCommand)
        {
            GameObject go = CreateAndSelectNewObject(menuCommand, "TrainSpawner", true);
            go.AddComponent<TrainSpawner>();
        }

        /// <summary>
        /// Create add trigger collider to object
        /// </summary>
        /// <param name="target"></param>
        private static void CreateTrigger(ref GameObject target)
        {
            BoxCollider boxCollider = target.AddComponent<BoxCollider>();
            boxCollider.size = new Vector3(10, 10, 3);
            boxCollider.isTrigger = true;
            target.AddComponent<SMR_IgnoredObject>();
        }

        /// <summary>
        /// Create and select new object on the Hierarchy
        /// </summary>
        /// <param name="menuCommand"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        private static GameObject CreateAndSelectNewObject(MenuCommand menuCommand, string name, bool createOnWorldOrigin = false)
        {
            Vector3 worldPos = Vector3.zero;

            if (!createOnWorldOrigin && SceneView.lastActiveSceneView.camera != null)
            {
                float distanceToGround;
                Ray worldRay = SceneView.lastActiveSceneView.camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 1.0f));
                Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
                groundPlane.Raycast(worldRay, out distanceToGround);
                worldPos = worldRay.GetPoint(distanceToGround);
            }

            // Create a custom game object
            GameObject newObject = new GameObject(name);
            newObject.transform.position = worldPos;
            // Ensure it gets reparented if this was a context click (otherwise does nothing)
            GameObjectUtility.SetParentAndAlign(newObject, menuCommand.context as GameObject);
            // Ensure Unique naming for this object
            GameObjectUtility.EnsureUniqueNameForSibling(newObject);
            // Register the creation in the undo system
            Undo.RegisterCreatedObjectUndo(newObject, "Create " + newObject.name);
            Selection.activeObject = newObject;
            return newObject;
        }
    } 
}
