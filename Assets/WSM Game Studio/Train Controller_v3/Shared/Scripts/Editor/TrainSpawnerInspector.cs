using UnityEngine;
using UnityEngine.SceneManagement;
using WSMGameStudio.Splines;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace WSMGameStudio.RailroadSystem
{
    [CustomEditor(typeof(TrainSpawner))]
    public class TrainSpawnerInspector : Editor
    {
        private TrainSpawner _trainSpawner;
        private RouteManager _routeManager;

        private int _selectedMenuIndex = 0;
        private string[] _toolbarMenuOptions = new[] { "Spawn Railway Vehicle", "Spawn Train (Profile)", "Spawn Train (Prefab)" };
        private GUIStyle _menuBoxStyle;
        private GUIStyle _warningTextStyle;
        private const string btnSpawn = "Spawn";

        private TrainProfile _trainProfile;
        private GameObject _locomotivePrefab;
        private GameObject _trainPrefab;
        private List<Spline> _targetRails;
        private int _selectedRoute = 0;
        private string[] _routes;

        private SerializedProperty _positionAlongRails;

        private Vector3 _targetSpawnGizmosPosition;
        private Vector3 _targetSpawnGizmosPositionOffset = new Vector3(0f, 20f, 0f);
        private Quaternion _targetSpawnGizmosRotation;

        /// <summary>
        /// Initialize
        /// </summary>
        private void OnEnable()
        {
            _positionAlongRails = serializedObject.FindProperty("_positionAlongRails");
            _routeManager = FindObjectOfType<RouteManager>();
            _trainSpawner = target as TrainSpawner;

            _targetSpawnGizmosPosition = _targetSpawnGizmosPositionOffset;
            _targetSpawnGizmosRotation = Quaternion.FromToRotation(Vector3.forward, Vector3.down);
        }

        /// <summary>
        /// Custom inspector
        /// </summary>
        public override void OnInspectorGUI()
        {
            _trainSpawner = target as TrainSpawner;

            //Set up the box style if null
            if (_menuBoxStyle == null)
            {
                _menuBoxStyle = new GUIStyle(GUI.skin.box);
                _menuBoxStyle.normal.textColor = GUI.skin.label.normal.textColor;
                _menuBoxStyle.fontStyle = FontStyle.Bold;
                _menuBoxStyle.alignment = TextAnchor.UpperLeft;
            }

            EditorGUI.BeginChangeCheck();
            _selectedMenuIndex = GUILayout.Toolbar(_selectedMenuIndex, _toolbarMenuOptions);
            if (EditorGUI.EndChangeCheck())
            {
                GUI.FocusControl(null);
            }

            //base.DrawDefaultInspector();
            GUILayout.BeginVertical(_menuBoxStyle);

            if (_selectedMenuIndex == (int)TrainSpawnerInspectorMenu.SpawnRailwayVehicle)
            {
                _locomotivePrefab = EditorGUILayout.ObjectField("Locomotive/Wagon Prefab", _locomotivePrefab, typeof(GameObject), false) as GameObject;

                RouteSelectionGUI();
                PositionAlongRailsGUI();

                using (new EditorGUI.DisabledScope(_locomotivePrefab == null || _targetRails == null || _targetRails.Count == 0))
                {
                    if (GUILayout.Button(btnSpawn))
                    {
                        GameObject locomotiveInstance = _trainSpawner.SpawnRailwayVehicle(_locomotivePrefab, _targetRails, _trainSpawner.PositionAlongRails);
                        MarkSceneAlteration(locomotiveInstance);
                    }
                }

                ShowInvalidRouteGUI();
            }
            else if (_selectedMenuIndex == (int)TrainSpawnerInspectorMenu.SpawnTrain_Profile)
            {
                _trainProfile = EditorGUILayout.ObjectField("Train Profile", _trainProfile, typeof(TrainProfile), false) as TrainProfile;

                RouteSelectionGUI();
                PositionAlongRailsGUI();

                using (new EditorGUI.DisabledScope(_trainProfile == null || _targetRails == null || _targetRails.Count == 0))
                {
                    if (GUILayout.Button(btnSpawn))
                    {
                        GameObject trainInstance = _trainSpawner.SpawnTrain_Profile(_trainProfile, _targetRails, _trainSpawner.PositionAlongRails);
                        MarkSceneAlteration(trainInstance);
                    }
                }

                ShowInvalidRouteGUI();
            }
            else if (_selectedMenuIndex == (int)TrainSpawnerInspectorMenu.SpawnTrain_Prefab)
            {
                _trainPrefab = EditorGUILayout.ObjectField("Train Prefab", _trainPrefab, typeof(GameObject), false) as GameObject;

                RouteSelectionGUI();
                PositionAlongRailsGUI();

                using (new EditorGUI.DisabledScope(_trainPrefab == null || _targetRails == null || _targetRails.Count == 0))
                {
                    if (GUILayout.Button(btnSpawn))
                    {
                        GameObject locomotiveInstance = _trainSpawner.SpawnTrain_Prefab(_trainPrefab, _targetRails, _trainSpawner.PositionAlongRails);
                        MarkSceneAlteration(locomotiveInstance);
                    }
                }

                ShowInvalidRouteGUI();
            }

            RouteManagerValidationMessageGUI();

            GUILayout.EndVertical();
        }

        private void OnSceneGUI()
        {
            DrawTargetPositionGizmos();
        }

        /// <summary>
        /// Draw Target Spawning Position Gizmos
        /// </summary>
        private void DrawTargetPositionGizmos()
        {
            if (_targetRails != null && _targetRails.Count > 0)
            {
                _targetSpawnGizmosPosition = _trainSpawner.GetTargetSpawnPosition(_targetRails) + _targetSpawnGizmosPositionOffset;

                float size = HandleUtility.GetHandleSize(_targetSpawnGizmosPosition) * 6f;
                Color originalHandleColor = Handles.color;
                Handles.color = Color.green;
                Handles.Button(_targetSpawnGizmosPosition, _targetSpawnGizmosRotation, size * 0.04f, size * 0.06f, Handles.ConeHandleCap);
                Handles.color = originalHandleColor;
            }
        }

        /// <summary>
        /// Shows position along rails GUI
        /// </summary>
        private void PositionAlongRailsGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(_positionAlongRails);
            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// Shows route selection GUI
        /// </summary>
        private void RouteSelectionGUI()
        {
            if (_routeManager != null)
            {
                if (_routeManager.Routes != null)
                {
                    _routes = new string[_routeManager.Routes.Count];
                    for (int i = 0; i < _routeManager.Routes.Count; i++)
                    {
                        _routes[i] = string.Format("{0} - {1}", i, _routeManager.Routes[i].Name);
                    }

                    _selectedRoute = _selectedRoute >= _routeManager.Routes.Count ? _routeManager.Routes.Count - 1 : _selectedRoute;
                    _targetRails = _routeManager.Routes.Count == 0 ? null : _routeManager.Routes[_selectedRoute].Splines;
                }
                else
                {
                    _routes = null;
                    _selectedRoute = 0;
                    _targetRails = null;
                }

                if (_routeManager.Routes != null)
                {
                    float lastRoute = _selectedRoute;
                    _selectedRoute = EditorGUILayout.Popup("Target Route", _selectedRoute, _routes);

                    if (_selectedRoute != lastRoute)
                    {
                        SceneView.RepaintAll(); //Update scene view to update target position gizmos position immediately
                    }
                }
            }
        }

        /// <summary>
        /// Shows invalid route message
        /// </summary>
        private void ShowInvalidRouteGUI()
        {
            if (_targetRails != null && _targetRails.Count == 0)
            {
                _warningTextStyle = new GUIStyle(EditorStyles.label);
                _warningTextStyle.normal.textColor = Color.red;
                EditorGUILayout.LabelField("Route splines not assigned at the Route Manager", _warningTextStyle);
            }
        }

        /// <summary>
        /// Validate if route manager exists and has routes
        /// </summary>
        private void RouteManagerValidationMessageGUI()
        {
            _warningTextStyle = new GUIStyle(EditorStyles.label);
            _warningTextStyle.normal.textColor = Color.red;

            if (_routeManager == null)
                EditorGUILayout.LabelField("Route Manager not found", _warningTextStyle);
            else if (_routeManager.Routes == null || _routeManager.Routes.Count == 0)
                EditorGUILayout.LabelField("Route Manager has no routes", _warningTextStyle);
        }

        /// <summary>
        /// Show player the scene needs to be saved
        /// </summary>
        private void MarkSceneAlteration(Object target)
        {
            if (!Application.isPlaying)
            {
                if (target != null) EditorUtility.SetDirty(target);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }
        }
    }
}
