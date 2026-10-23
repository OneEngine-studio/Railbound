using System.Collections.Generic;
using WSMGameStudio.Splines;
using UnityEngine;
using UnityEditor;

namespace WSMGameStudio.RailroadSystem
{
    [ExecuteInEditMode]
    public class TrainSpawner : MonoBehaviour
    {
        #region SINGLETON
        private static TrainSpawner _instance;
        public static TrainSpawner Instance { get { return _instance; } }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                if (Application.isPlaying)
                    Destroy(this.gameObject);
                else
                    DestroyImmediate(this.gameObject);
            }
            else
            {
                _instance = this;
            }
        }
        #endregion

        [Range(0, 1)]
        [SerializeField] private float _positionAlongRails = 0.9f;

        public float PositionAlongRails { get { return _positionAlongRails; } set { _positionAlongRails = value; } }

        /// <summary>
        /// Spawns a instance of the selected train prefab on target rails
        /// </summary>
        /// <param name="trainPrefab"></param>
        /// <param name="targetRails"></param>
        /// <param name="t"></param>
        /// <returns></returns>
        public GameObject SpawnTrain_Prefab(GameObject trainPrefab, List<Spline> targetRails, float t, string trainName = "Train")
        {
            if (trainPrefab == null) return null;

            ILocomotive locomotiveValidation = trainPrefab.GetComponentInChildren<ILocomotive>();

            if (locomotiveValidation == null)
            {
                Debug.LogWarning(string.Format("{0} must have a locomotive!", trainPrefab.name));
                return null;
            }

            IRailwayVehicle[] wagonsValidation = trainPrefab.GetComponentsInChildren<IRailwayVehicle>();

            if (wagonsValidation == null)
            {
                Debug.LogWarning(string.Format("{0} must have at least 1 wagon!", trainPrefab.name));
                return null;
            }

            Vector3 position = GetTargetSpawnPosition(targetRails, t);
            Vector3 lookTarget = GetLookTarget(targetRails, t, position);

            GameObject trainInstance = SpawnInstance(trainPrefab, position, lookTarget);

            ILocomotive locomotive = trainInstance.GetComponentInChildren<ILocomotive>();
            SetupSplineBasedLocomotive(locomotive.GetGameObject, targetRails, t);

            return trainInstance;
        }

        /// <summary>
        /// Spawn train on target rails based on profile
        /// </summary>
        /// <param name="trainProfile"></param>
        /// <param name="targetRails"></param>
        /// <param name="t"></param>
        /// <returns></returns>
        public GameObject SpawnTrain_Profile(TrainProfile trainProfile, List<Spline> targetRails, float t, string trainName = "Train")
        {
            if (trainProfile == null) return null;

            if (trainProfile.locomotivePrefab == null)
            {
                Debug.LogWarning(string.Format("{0} must have a locomotive prefab reference!", trainProfile.name));
                return null;
            }

            if (trainProfile.wagonsPrefabs == null)
            {
                Debug.LogWarning(string.Format("{0} must have wagon prefabs references!", trainProfile.name));
                return null;
            }

            return SpawnTrain(trainProfile.locomotivePrefab, trainProfile.wagonsPrefabs, targetRails, t, trainName);
        }

        /// <summary>
        /// Spawn train on target rails
        /// </summary>
        /// <param name="locomotivePrefab"></param>
        /// <param name="wagonsPrefabs"></param>
        /// <param name="targetRails"></param>
        /// <param name="t"></param>
        /// <returns></returns>
        public GameObject SpawnTrain(GameObject locomotivePrefab, List<GameObject> wagonsPrefabs, List<Spline> targetRails, float t, string trainName)
        {
            if (locomotivePrefab.GetComponent<ILocomotive>() == null)
            {
                Debug.LogWarning(string.Format("{0} is not a locomotive prefab!", locomotivePrefab.name));
                return null;
            }

            GameObject locomotiveInstance = SpawnRailwayVehicle(locomotivePrefab, targetRails, t);
            ILocomotive locomotiveScript = locomotiveInstance.GetComponent<ILocomotive>();

            GameObject trainInstance = new GameObject(trainName);
#if UNITY_EDITOR
            UnityEditor.GameObjectUtility.EnsureUniqueNameForSibling(trainInstance);
#endif
            trainInstance.transform.position = locomotiveInstance.transform.position;
            trainInstance.transform.rotation = locomotiveInstance.transform.rotation;

            locomotiveInstance.transform.SetParent(trainInstance.transform);

            List<GameObject> wagonInstances = new List<GameObject>();

            foreach (GameObject wagonPrefab in wagonsPrefabs)
            {
                if (wagonPrefab.GetComponent<IRailwayVehicle>() == null) continue;
                GameObject wagonInstance = SpawnInstance(wagonPrefab, Vector3.zero, Quaternion.identity);
                wagonInstance.transform.SetParent(trainInstance.transform);
                wagonInstances.Add(wagonInstance);
            }

            SetupSplineBasedLocomotive(locomotiveInstance, targetRails, t);
            ConnectWagons(locomotiveScript, wagonInstances, targetRails);

            return trainInstance;
        }

        /// <summary>
        /// If locomotive is spline based, setup spline following properties
        /// </summary>
        /// <param name="locomotiveInstance"></param>
        /// <param name="targetRails"></param>
        /// <param name="t"></param>
        private static void SetupSplineBasedLocomotive(GameObject locomotiveInstance, List<Spline> targetRails, float t)
        {
            if (locomotiveInstance == null) return;

            SplineBasedLocomotive splineBasedLoco = locomotiveInstance.GetComponent<SplineBasedLocomotive>();

            if (splineBasedLoco == null) return;

            splineBasedLoco.customStartPosition = t * 100f;
            splineBasedLoco.splines = targetRails;
            splineBasedLoco.CalculateWagonsPositions();
        }

        /// <summary>
        /// Connect wagons to the locomotive
        /// </summary>
        /// <param name="locomotiveScript"></param>
        /// <param name="wagonInstances"></param>
        /// <param name="targetRails"></param>
        private static void ConnectWagons(ILocomotive locomotiveScript, List<GameObject> wagonInstances, List<Spline> targetRails)
        {
            locomotiveScript.RemoveAllWagons(); //Make sure wagons list will not be duplicated
            locomotiveScript.AddWagons(wagonInstances);
            locomotiveScript.CalculateWagonsPositions(targetRails);
        }

        /// <summary>
        /// Spawns either a locomotive or wagon at target position
        /// </summary>
        /// <param name="railwayVehiclePrefab"></param>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <returns></returns>
        public GameObject SpawnRailwayVehicle(GameObject railwayVehiclePrefab, Vector3 position, Quaternion rotation)
        {
            if (railwayVehiclePrefab.GetComponent<IRailwayVehicle>() == null)
            {
                Debug.LogWarning(string.Format("{0} is not a Railway Vehicle!", railwayVehiclePrefab.name));
                return null;
            }

            return SpawnInstance(railwayVehiclePrefab, position, rotation);
        }

        /// <summary>
        /// Spawns either a locomotive or wagon at target position
        /// </summary>
        /// <param name="railwayVehiclePrefab"></param>
        /// <param name="position"></param>
        /// <param name="lookTarget"></param>
        /// <returns></returns>
        public GameObject SpawnRailwayVehicle(GameObject railwayVehiclePrefab, Vector3 position, Vector3 lookTarget)
        {
            if (railwayVehiclePrefab.GetComponent<IRailwayVehicle>() == null)
            {
                Debug.LogWarning(string.Format("{0} is not a Railway Vehicle!", railwayVehiclePrefab.name));
                return null;
            }

            return SpawnInstance(railwayVehiclePrefab, position, lookTarget);
        }

        /// <summary>
        /// Spawns either a locomotive or wagon along target rails
        /// </summary>
        /// <param name="railwayVehiclePrefab"></param>
        /// <param name="targetRails"></param>
        /// <param name="t"></param>
        /// <returns></returns>
        public GameObject SpawnRailwayVehicle(GameObject railwayVehiclePrefab, List<Spline> targetRails, float t)
        {
            Vector3 position = GetTargetSpawnPosition(targetRails, t);
            Vector3 lookTarget = GetLookTarget(targetRails, t, position);

            GameObject railwayVehicleInstance = SpawnRailwayVehicle(railwayVehiclePrefab, position, lookTarget);
            SetupSplineBasedLocomotive(railwayVehicleInstance, targetRails, t);

            return railwayVehicleInstance;
        }

        /// <summary>
        /// Get Target Spawn Position
        /// </summary>
        /// <param name="targetRails"></param>
        /// <returns></returns>
        public Vector3 GetTargetSpawnPosition(List<Spline> targetRails)
        {
            return GetTargetSpawnPosition(targetRails, _positionAlongRails);
        }

        /// <summary>
        /// Get Target Spawn Position
        /// </summary>
        /// <param name="targetRails"></param>
        /// <param name="t"></param>
        /// <returns></returns>
        private Vector3 GetTargetSpawnPosition(List<Spline> targetRails, float t)
        {
            return targetRails[0].GetPoint(t);
        }

        /// <summary>
        /// Get Look Target
        /// </summary>
        /// <param name="targetRails"></param>
        /// <param name="t"></param>
        /// <param name="position"></param>
        /// <returns></returns>
        private Vector3 GetLookTarget(List<Spline> targetRails, float t, Vector3 position)
        {
            return position + targetRails[0].GetDirection(t);
        }

        /// <summary>
        /// Spawn instance and enforce unique name
        /// </summary>
        /// <param name="prefab"></param>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <returns></returns>
        private GameObject SpawnInstance(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            GameObject instance = null;

            if (Application.isEditor)
            {
#if UNITY_EDITOR
                instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                instance.transform.position = position;
                instance.transform.rotation = rotation;
#endif
            }
            else
                instance = Instantiate(prefab, position, rotation);

            EnforceUniqueName(instance);
            return instance;
        }

        /// <summary>
        /// Spawns either a locomotive or wagon along target rails
        /// </summary>
        /// <param name="prefab"></param>
        /// <param name="position"></param>
        /// <param name="lookTarget"></param>
        /// <returns></returns>
        private GameObject SpawnInstance(GameObject prefab, Vector3 position, Vector3 lookTarget)
        {
            GameObject instance = null;

            if (Application.isEditor)
            {
#if UNITY_EDITOR
                instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                instance.transform.position = position;
                instance.transform.rotation = Quaternion.identity;
#endif
            }
            else
                instance = Instantiate(prefab, position, Quaternion.identity);

            instance.transform.LookAt(lookTarget, Vector3.up);
            EnforceUniqueName(instance);
            return instance;
        }

        /// <summary>
        /// Make sure new object has a unique name
        /// </summary>
        /// <param name="targetObject"></param>
        private void EnforceUniqueName(GameObject targetObject)
        {
#if UNITY_EDITOR
            targetObject.name = targetObject.name.Replace("(Clone)", string.Empty);
            UnityEditor.GameObjectUtility.EnsureUniqueNameForSibling(targetObject);
#endif
        }
    }
}
