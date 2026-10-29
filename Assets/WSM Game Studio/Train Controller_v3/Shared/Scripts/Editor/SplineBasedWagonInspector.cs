using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using UnityEngine.SceneManagement;

namespace WSMGameStudio.RailroadSystem
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(SplineBasedWagon))]
    public class SplineBasedWagonInspector : Editor
    {
        private int _selectedMenuIndex = 0;
        private string[] _toolbarMenuOptions = new[] { "Settings", "Lights", "SFX", "Other Settings" };
        private GUIStyle _menuBoxStyle;

        private GUIContent _btnRecalculateOffset = new GUIContent("Recalculate Offset", "Recalculate following offset");
        GUIContent _frontCouplerLabel = new GUIContent("Front Coupler");
        GUIContent _backCouplerLabel = new GUIContent("Back Coupler");

        SerializedProperty _followingOffset;
        SerializedProperty _coupling;
        SerializedProperty _decouplingSettings;
        SerializedProperty _recouplingTimeout;
        SerializedProperty _externalLights;
        SerializedProperty _internalLights;
        SerializedProperty _wheelsSFX;
        SerializedProperty _wagonConnectionSFX;
        SerializedProperty _minWheelsPitch;
        SerializedProperty _maxWheelsPitch;
        SerializedProperty _wheelsScripts;
        SerializedProperty _backJoint;
        SerializedProperty _frontJoint;

        private void OnEnable()
        {
            _followingOffset = serializedObject.FindProperty("followingOffset");
            _coupling = serializedObject.FindProperty("_coupling");
            _decouplingSettings = serializedObject.FindProperty("_decouplingSettings");
            _recouplingTimeout = serializedObject.FindProperty("_recouplingTimeout");
            _externalLights = serializedObject.FindProperty("_externalLights");
            _internalLights = serializedObject.FindProperty("_internalLights");
            _wheelsSFX = serializedObject.FindProperty("_wheelsSFX");
            _wagonConnectionSFX = serializedObject.FindProperty("_wagonConnectionSFX");
            _minWheelsPitch = serializedObject.FindProperty("_minWheelsPitch");
            _maxWheelsPitch = serializedObject.FindProperty("_maxWheelsPitch");
            _wheelsScripts = serializedObject.FindProperty("_wheelsScripts");
            _backJoint = serializedObject.FindProperty("_backJoint");
            _frontJoint = serializedObject.FindProperty("_frontJoint");
        }

        public override void OnInspectorGUI()
        {
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

            if (_selectedMenuIndex == (int)WagonInspectorMenu.Coupling)
            {
                #region Settings

                GUILayout.Label("WAGON SETTINGS", EditorStyles.boldLabel);
                float offset = _followingOffset.floatValue;
                EditorGUILayout.PropertyField(_followingOffset, new GUIContent("Wagon Following Offset"));
                if (offset != _followingOffset.floatValue) _followingOffset.floatValue = Mathf.Abs(_followingOffset.floatValue);
                EditorGUILayout.PropertyField(_coupling, true);
                EditorGUILayout.PropertyField(_decouplingSettings, true);
                EditorGUILayout.PropertyField(_recouplingTimeout, true);

                GUILayout.Label("WAGON OPERATIONS", EditorStyles.boldLabel);
                if (GUILayout.Button(_btnRecalculateOffset))
                {
                    foreach (var selected in Selection.gameObjects)
                    {
                        SplineBasedWagon follower = selected.GetComponent<SplineBasedWagon>();
                        if (follower != null)
                        {
                            follower.RecalculateOffset();
                            MarkSceneAlteration(selected);
                        }
                    }
                }

                #endregion
            }
            else if (_selectedMenuIndex == (int)WagonInspectorMenu.Lights)
            {
                #region Lights
                GUILayout.Label("WAGON LIGHTS", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_externalLights, true);
                EditorGUILayout.PropertyField(_internalLights, true);
                #endregion
            }
            else if (_selectedMenuIndex == (int)WagonInspectorMenu.SFX)
            {
                #region SFX
                GUILayout.Label("WAGON SFX", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_wheelsSFX, false);
                EditorGUILayout.PropertyField(_wagonConnectionSFX, true);
                EditorGUILayout.PropertyField(_minWheelsPitch, false);
                EditorGUILayout.PropertyField(_maxWheelsPitch, false);
                #endregion
            }
            else if (_selectedMenuIndex == (int)WagonInspectorMenu.OtherSettings)
            {
                #region Other Settings
                GUILayout.Label("MECHANICAL COMPONENTS", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_wheelsScripts, true);
                EditorGUILayout.PropertyField(_frontJoint, _frontCouplerLabel, false);
                EditorGUILayout.PropertyField(_backJoint, _backCouplerLabel, false);
                #endregion
            }

            GUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// Show player the scene needs to be saved
        /// </summary>
        private void MarkSceneAlteration(Object target)
        {
            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(target);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }
        }
    } 
}
