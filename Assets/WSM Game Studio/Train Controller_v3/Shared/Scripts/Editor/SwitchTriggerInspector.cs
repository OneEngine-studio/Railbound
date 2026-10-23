using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using UnityEngine.SceneManagement;

namespace WSMGameStudio.RailroadSystem
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(SwitchTrigger))]
    public class SwitchTriggerInspector : Editor
    {
        private SwitchTrigger _switchTrigger;

        private int _selectedMenuIndex = 0;
        private string[] _toolbarMenuOptions = new[] { "General Settings", "Spline Based Settings" };
        private GUIStyle _menuBoxStyle;
        private GUIStyle _warningTextStyle;

        private SerializedProperty _switchMode;
        private SerializedProperty _randomSwitchProbability;
        private SerializedProperty _railroadSwitches;
        private SerializedProperty _leftRouteIndex;
        private SerializedProperty _rightRouteIndex;

        private void OnEnable()
        {
            _switchMode = serializedObject.FindProperty("_switchMode");
            _randomSwitchProbability = serializedObject.FindProperty("_randomSwitchProbability");
            _railroadSwitches = serializedObject.FindProperty("_railroadSwitches");
            _leftRouteIndex = serializedObject.FindProperty("_leftRouteIndex");
            _rightRouteIndex = serializedObject.FindProperty("_rightRouteIndex");
        }

        /// <summary>
        /// 
        /// </summary>
        public override void OnInspectorGUI()
        {
            _switchTrigger = target as SwitchTrigger;

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
            serializedObject.Update();

            GUILayout.BeginVertical(_menuBoxStyle);

            if (_selectedMenuIndex == (int)SwitchTriggerMenu.GeneralSettings)
            {
                GUILayout.Label("GENERAL SETTINGS", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_railroadSwitches, true);
                EditorGUILayout.PropertyField(_switchMode);
                if (_switchTrigger.SwitchMode == SwitchMode.Random)
                    EditorGUILayout.PropertyField(_randomSwitchProbability);
            }
            else if (_selectedMenuIndex == (int)SwitchTriggerMenu.SplineBasedSettings)
            {
                GUILayout.Label("SPLINE BASED SETTINGS", EditorStyles.boldLabel);
                GUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(_leftRouteIndex);
                EditorGUILayout.PropertyField(_rightRouteIndex);
                GUILayout.EndHorizontal();

                ShowAffectedRoutes();
            }

            GUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// Show Affected Routes
        /// </summary>
        private void ShowAffectedRoutes()
        {
            GUILayout.BeginVertical();

            _warningTextStyle = new GUIStyle(EditorStyles.label);
            _warningTextStyle.normal.textColor = Color.red;

            RouteManager routeManager = RouteManager.Instance;

#if UNITY_EDITOR
            routeManager = FindObjectOfType<RouteManager>();
#endif

            if (_switchTrigger.RailroadSwitches != null && routeManager != null)
            {
                foreach (var item in _switchTrigger.RailroadSwitches)
                {
                    if (item.AffectedRoutes == null || item.AffectedRoutes.Count == 0)
                        continue;

                    EditorGUILayout.LabelField(string.Empty, GUI.skin.horizontalSlider);
                    EditorGUILayout.LabelField(item.name, EditorStyles.boldLabel);

                    GUILayout.BeginHorizontal();
                    if (_leftRouteIndex.intValue >= 0 && _leftRouteIndex.intValue < item.AffectedRoutes.Count)
                    {
                        if (item.AffectedRoutes[_leftRouteIndex.intValue] >= 0 && item.AffectedRoutes[_leftRouteIndex.intValue] < routeManager.Routes.Count)
                            EditorGUILayout.LabelField(routeManager.Routes[item.AffectedRoutes[_leftRouteIndex.intValue]].Name);
                    }
                    else
                        EditorGUILayout.LabelField("Left Route Not found", _warningTextStyle);

                    if (_rightRouteIndex.intValue >= 0 && _rightRouteIndex.intValue < item.AffectedRoutes.Count)
                    {
                        if (item.AffectedRoutes[_rightRouteIndex.intValue] >= 0 && item.AffectedRoutes[_rightRouteIndex.intValue] < routeManager.Routes.Count)
                            EditorGUILayout.LabelField(routeManager.Routes[item.AffectedRoutes[_rightRouteIndex.intValue]].Name);
                    }
                    else
                        EditorGUILayout.LabelField("Right Route Not found", _warningTextStyle);

                    GUILayout.EndHorizontal();
                }
            }
            else
            {
                if (routeManager == null)
                    EditorGUILayout.LabelField("Route Manager not found", _warningTextStyle);
                else if (routeManager.Routes == null || routeManager.Routes.Count == 0)
                    EditorGUILayout.LabelField("Route Manager has no routes", _warningTextStyle);
            }

            GUILayout.EndVertical();
        }

        /// <summary>
        /// 
        /// </summary>
        private void MarkSceneAlteration()
        {
#if UNITY_EDITOR
            EditorUtility.SetDirty(_switchTrigger);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
#endif
        }
    }
}