using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace WSMGameStudio.RailroadSystem
{
    [CustomEditor(typeof(RouteManager))]
    public class RouteManagerInspector : Editor
    {
        private RouteManager _routeManager;

        private int _selectedMenuIndex = 0;
        private string[] _toolbarMenuOptions = new[] { "Route Settings", "Route Operations" };
        private GUIStyle _menuBoxStyle;
        private GUIStyle _placeholderTextStyle;
        private GUIStyle _invalidRouteStyle;
        private const string btnCreateRoute = "New Route";
        private const string btnAssignRoute = "Assign Route";

        private GameObject _targetLocomotive;
        private int _selectedRoute = 0;
        private string[] _routes;

        private SerializedProperty _positionAlongRails;

        private void OnEnable()
        {
            _positionAlongRails = serializedObject.FindProperty("_positionAlongRails");
        }

        /// <summary>
        /// OnInspectorGUI
        /// </summary>
        public override void OnInspectorGUI()
        {
            _routeManager = target as RouteManager;

            //Set up the box style if null
            if (_menuBoxStyle == null)
            {
                _menuBoxStyle = new GUIStyle(GUI.skin.box);
                _menuBoxStyle.normal.textColor = GUI.skin.label.normal.textColor;
                _menuBoxStyle.fontStyle = FontStyle.Bold;
                _menuBoxStyle.alignment = TextAnchor.UpperLeft;
            }

            if (_placeholderTextStyle == null)
            {
                _placeholderTextStyle = new GUIStyle(GUI.skin.textField);
                _placeholderTextStyle.normal.textColor = Color.gray;
                _placeholderTextStyle.alignment = TextAnchor.MiddleLeft;
            }

            _invalidRouteStyle = new GUIStyle(EditorStyles.label);
            _invalidRouteStyle.normal.textColor = Color.red;

            EditorGUI.BeginChangeCheck();
            _selectedMenuIndex = GUILayout.Toolbar(_selectedMenuIndex, _toolbarMenuOptions);
            if (EditorGUI.EndChangeCheck())
            {
                GUI.FocusControl(null);
            }

            GUILayout.BeginVertical(_menuBoxStyle);

            if (_selectedMenuIndex == (int)RouteManagerInspectorMenu.RouteSettings)
            {
                EditorGUILayout.LabelField("ROUTE SETTINGS", EditorStyles.boldLabel);

                //Custom Routes visualization here
                GUILayout.BeginVertical(_menuBoxStyle);
                EditorGUILayout.LabelField("ROUTES", EditorStyles.boldLabel);

                if (_routeManager.Routes != null)
                {
                    for (int routeIndex = 0; routeIndex < _routeManager.Routes.Count; routeIndex++)
                    {
                        GUILayout.BeginHorizontal();

                        if (_routeManager.Routes[routeIndex].IsValid)
                            EditorGUILayout.LabelField(string.Format("{0} - {1}", routeIndex, _routeManager.Routes[routeIndex].Name));
                        else
                            EditorGUILayout.LabelField(string.Format("{0} - {1} (route splines not assigned)", routeIndex, _routeManager.Routes[routeIndex].Name), _invalidRouteStyle);

                        if (GUILayout.Button("Edit", GUILayout.MaxWidth(70)))
                        {
                            RouteEditingWindow.ShowWindow(routeIndex);
                        }

                        if (GUILayout.Button("Delete", GUILayout.MaxWidth(70)))
                        {
                            bool delete = EditorUtility.DisplayDialog("WARNING",
                                string.Format("Are you sure you wanto to delete {0}?{1}This operation cannot be reverted", _routeManager.Routes[routeIndex].Name.ToUpper(), System.Environment.NewLine),
                                "Yes",
                                "Cancel");

                            if (delete)
                            {
                                _routeManager.Routes.RemoveAt(routeIndex);
                                MarkSceneAlteration(_routeManager.gameObject);
                            }
                        }
                        GUILayout.EndHorizontal();
                    }
                }

                if (GUILayout.Button(btnCreateRoute))
                {
                    _routeManager.CreateRoute();
                    MarkSceneAlteration(_routeManager.gameObject);
                }
                GUILayout.EndVertical();
            }
            else if (_selectedMenuIndex == (int)RouteManagerInspectorMenu.RouteOperations)
            {
                EditorGUILayout.LabelField("ROUTE OPERATIONS", EditorStyles.boldLabel);

                if (_routeManager.Routes != null)
                {
                    _routes = new string[_routeManager.Routes.Count];
                    for (int i = 0; i < _routeManager.Routes.Count; i++)
                    {
                        _routes[i] = _routeManager.Routes[i].Name;
                    }

                    _selectedRoute = _selectedRoute >= _routeManager.Routes.Count ? _routeManager.Routes.Count - 1 : _selectedRoute;
                }
                else
                {
                    _routes = new string[0];
                    _selectedRoute = 0;
                }

                _targetLocomotive = EditorGUILayout.ObjectField("Target Locomotive", _targetLocomotive, typeof(GameObject), true) as GameObject;

                serializedObject.Update();
                EditorGUILayout.PropertyField(_positionAlongRails);
                serializedObject.ApplyModifiedProperties();

                GUILayout.BeginHorizontal();
                _selectedRoute = EditorGUILayout.Popup("Route selection", _selectedRoute, _routes);
                using (new EditorGUI.DisabledScope(_targetLocomotive == null))
                {
                    if (GUILayout.Button(btnAssignRoute))
                    {
                        ILocomotive locomotiveScript = _targetLocomotive.GetComponent<ILocomotive>();

                        if (!Application.isPlaying)
                            Undo.RecordObject(locomotiveScript.GetGameObject, "Route Assigned");

                        if (locomotiveScript == null)
                        {
                            Debug.LogWarning(string.Format("{0} is not a locomotive!", _targetLocomotive.name.ToUpper()));
                        }
                        else if (_routeManager.ApplyRoute(locomotiveScript, _selectedRoute, true))
                        {
                            MarkSceneAlteration(locomotiveScript.GetGameObject);

                            if (!Application.isPlaying) //dont show editor notification on play mode
                                EditorUtility.DisplayDialog("ROUTE APPLIED",
                                    string.Format("{0} applied to {1}", _routeManager.Routes[_selectedRoute].Name.ToUpper(), _targetLocomotive.name.ToUpper()),
                                    "Ok");
                        }
                    }
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
        }

        /// <summary>
        /// Show player the scene needs to be saved
        /// </summary>
        private void MarkSceneAlteration(GameObject target)
        {
            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(target);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }
        }
    }
}