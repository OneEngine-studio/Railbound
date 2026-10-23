using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using UnityEngine.SceneManagement;

namespace WSMGameStudio.RailroadSystem
{
    [CustomEditor(typeof(RailroadSwitch_v3))]
    public class RailroadSwitchInspector : Editor
    {
        private RailroadSwitch_v3 _railroadSwitch;
        private RouteManager _routeManager;

        private int _selectedMenuIndex = 0;
        private string[] _toolbarMenuOptions = new[] { "Physics Based", "Spline Based", "Events" };
        private GUIStyle _menuBoxStyle;
        private GUIStyle _warningTextStyle;

        SerializedProperty _railsColliders;
        SerializedProperty _railsSplines;
        SerializedProperty _onActivate;
        SerializedProperty _onDeactivate;
        SerializedProperty _onSwitch;

        private int _selectedRoute = 0;
        private string[] _routes;

        private void OnEnable()
        {
            _railsColliders = serializedObject.FindProperty("_railsColliders");
            _railsSplines = serializedObject.FindProperty("_railsSplines");
            _onActivate = serializedObject.FindProperty("_onActivate");
            _onDeactivate = serializedObject.FindProperty("_onDeactivate");
            _onSwitch = serializedObject.FindProperty("_onSwitch");

            _routeManager = FindObjectOfType<RouteManager>();
        }

        public override void OnInspectorGUI()
        {
            _railroadSwitch = target as RailroadSwitch_v3;
            _railroadSwitch.ValidateAffectedRoutes();

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

            serializedObject.Update();

            GUILayout.BeginVertical(_menuBoxStyle);

            if (_selectedMenuIndex == (int)RailroadSwitchInspectorMenu.PhysicsBased)
            {
                #region Physics Based
                GUILayout.Label("SETTINGS", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_railsColliders, true);

                GUILayout.Label("OPERATIONS", EditorStyles.boldLabel);
                if (GUILayout.Button("Switch Rails"))
                {
                    Undo.RecordObject(_railroadSwitch, "Switch Rails");
                    _railroadSwitch.SwitchRails();
                    MarkSceneAlteration();
                }
                #endregion
            }
            else if (_selectedMenuIndex == (int)RailroadSwitchInspectorMenu.SplineBased)
            {
                #region Spline Based
                GUILayout.Label("SETTINGS", EditorStyles.boldLabel);
                //EditorGUILayout.PropertyField(_railsSplines, true);

                if (_routeManager != null)
                {
                    if (_routeManager.Routes != null)
                    {
                        GUILayout.BeginHorizontal();
                        RouteSelectionGUI();

                        using (new EditorGUI.DisabledScope(_routeManager.Routes.Count == 0 || _selectedRoute > _routeManager.Routes.Count || (_routeManager.Routes[_selectedRoute].Splines != null && _routeManager.Routes[_selectedRoute].Splines.Count == 0)))
                        {
                            if (GUILayout.Button("Add Route"))
                            {
                                if (!_railroadSwitch.AffectedRoutes.Contains(_selectedRoute))
                                {
                                    _railroadSwitch.AffectedRoutes.Add(_selectedRoute);
                                    MarkSceneAlteration();
                                }
                            } 
                        }

                        GUILayout.EndHorizontal();

                        ShowInvalidRouteGUI();

                        //Selected routes
                        GUILayout.BeginVertical(_menuBoxStyle);
                        GUILayout.Label("SELECTED ROUTES", EditorStyles.boldLabel);
                        int routeIndex;
                        for (int i = 0; i < _railroadSwitch.AffectedRoutes.Count; i++)
                        {
                            routeIndex = _railroadSwitch.AffectedRoutes[i];
                            GUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField(string.Format("{0} - {1}", i, _routeManager.Routes[routeIndex].Name));
                            if (GUILayout.Button("Remove"))
                            {
                                _railroadSwitch.AffectedRoutes.Remove(routeIndex);
                                MarkSceneAlteration();
                            }
                            GUILayout.EndHorizontal();
                        }
                        GUILayout.EndVertical();
                    }
                }

                RouteManagerValidationMessageGUI();

                #endregion
            }
            else if (_selectedMenuIndex == (int)RailroadSwitchInspectorMenu.Events)
            {
                #region Events
                GUILayout.Label("CUSTOM EVENTS", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_onActivate, false);
                EditorGUILayout.PropertyField(_onDeactivate, false);
                EditorGUILayout.PropertyField(_onSwitch, false);
                #endregion
            }

            GUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// Show invalid route message
        /// </summary>
        private void ShowInvalidRouteGUI()
        {
            if (_routeManager != null && _routeManager.Routes != null && _routeManager.Routes.Count > 0)
            {
                if (_routeManager.Routes[_selectedRoute].Splines != null && _routeManager.Routes[_selectedRoute].Splines.Count == 0)
                {
                    _warningTextStyle = new GUIStyle(EditorStyles.label);
                    _warningTextStyle.normal.textColor = Color.red;
                    EditorGUILayout.LabelField("Route splines not assigned at the Route Manager", _warningTextStyle);
                }
            }
        }

        /// <summary>
        /// Show route selction GUI
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
                    _selectedRoute = _selectedRoute < 0 ? 0 : _selectedRoute;
                }
                else
                {
                    _routes = null;
                    _selectedRoute = 0;
                }

                if (_routeManager.Routes != null)
                {
                    _selectedRoute = EditorGUILayout.Popup("Available Routes", _selectedRoute, _routes);
                }
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

        private void MarkSceneAlteration()
        {
#if UNITY_EDITOR
            EditorUtility.SetDirty(_railroadSwitch);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
#endif
        }
    }
}
