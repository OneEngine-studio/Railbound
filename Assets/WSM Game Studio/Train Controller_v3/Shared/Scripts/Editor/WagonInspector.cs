using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using UnityEngine.SceneManagement;

namespace WSMGameStudio.RailroadSystem
{
    [CustomEditor(typeof(Wagon_v3))]
    public class WagonInspector : Editor
    {
        private Wagon_v3 _wagon;

        private int _selectedMenuIndex = 0;
        private string[] _toolbarMenuOptions = new[] { "Coupling", "Lights", "SFX", "Other Settings" };
        private GUIStyle _menuBoxStyle;

        GUIContent _frontCouplerLabel = new GUIContent("Front Coupler");
        GUIContent _backCouplerLabel = new GUIContent("Back Coupler");

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
        SerializedProperty _sensors;
        SerializedProperty _backJoint;
        SerializedProperty _frontJoint;
        SerializedProperty _jointAnchor;

        private void OnEnable()
        {
            _coupling = serializedObject.FindProperty("coupling");
            _decouplingSettings = serializedObject.FindProperty("decouplingSettings");
            _recouplingTimeout = serializedObject.FindProperty("recouplingTimeout");
            _externalLights = serializedObject.FindProperty("externalLights");
            _internalLights = serializedObject.FindProperty("internalLights");
            _wheelsSFX = serializedObject.FindProperty("wheelsSFX");
            _wagonConnectionSFX = serializedObject.FindProperty("wagonConnectionSFX");
            _minWheelsPitch = serializedObject.FindProperty("minWheelsPitch");
            _maxWheelsPitch = serializedObject.FindProperty("maxWheelsPitch");
            _wheelsScripts = serializedObject.FindProperty("wheelsScripts");
            _sensors = serializedObject.FindProperty("sensors");
            _backJoint = serializedObject.FindProperty("backJoint");
            _frontJoint = serializedObject.FindProperty("frontJoint");
            _jointAnchor = serializedObject.FindProperty("jointAnchor");
        }

        public override void OnInspectorGUI()
        {
            //base.OnInspectorGUI();
            _wagon = target as Wagon_v3;

            EditorGUI.BeginChangeCheck();
            _selectedMenuIndex = GUILayout.Toolbar(_selectedMenuIndex, _toolbarMenuOptions);
            if (EditorGUI.EndChangeCheck())
            {
                GUI.FocusControl(null);
            }

            //Set up the box style if null
            if (_menuBoxStyle == null)
            {
                _menuBoxStyle = new GUIStyle(GUI.skin.box);
                _menuBoxStyle.normal.textColor = GUI.skin.label.normal.textColor;
                _menuBoxStyle.fontStyle = FontStyle.Bold;
                _menuBoxStyle.alignment = TextAnchor.UpperLeft;
            }
            GUILayout.BeginVertical(_menuBoxStyle);

            if (_selectedMenuIndex == (int)WagonInspectorMenu.Coupling)
            {
                #region Coupling

                GUILayout.Label("COUPLING SETTINGS", EditorStyles.boldLabel);

                EditorGUI.BeginChangeCheck();
                serializedObject.Update();
                EditorGUILayout.PropertyField(_coupling, true);
                EditorGUILayout.PropertyField(_decouplingSettings, true);
                EditorGUILayout.PropertyField(_recouplingTimeout, true);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_wagon, "Changed Coupling Settings");
                    serializedObject.ApplyModifiedProperties();
                    MarkSceneAlteration();
                }
                #endregion
            }
            else if (_selectedMenuIndex == (int)WagonInspectorMenu.Lights)
            {
                #region Lights

                GUILayout.Label("WAGON LIGHTS", EditorStyles.boldLabel);

                EditorGUI.BeginChangeCheck();
                serializedObject.Update();
                EditorGUILayout.PropertyField(_externalLights, true);
                EditorGUILayout.PropertyField(_internalLights, true);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_wagon, "Changed Lights");
                    serializedObject.ApplyModifiedProperties();
                    MarkSceneAlteration();
                }
                #endregion
            }
            else if (_selectedMenuIndex == (int)WagonInspectorMenu.SFX)
            {
                #region SFX

                GUILayout.Label("WAGON SFXS", EditorStyles.boldLabel);

                EditorGUI.BeginChangeCheck();
                serializedObject.Update();
                EditorGUILayout.PropertyField(_wheelsSFX, true);
                EditorGUILayout.PropertyField(_wagonConnectionSFX, true);
                EditorGUILayout.PropertyField(_minWheelsPitch, true);
                EditorGUILayout.PropertyField(_maxWheelsPitch, true);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_wagon, "Changed SFX");
                    serializedObject.ApplyModifiedProperties();
                    MarkSceneAlteration();
                }
                #endregion
            }
            else if (_selectedMenuIndex == (int)WagonInspectorMenu.OtherSettings)
            {
                #region Other Settings

                GUILayout.Label("MECHANICAL COMPONENTS", EditorStyles.boldLabel);

                EditorGUI.BeginChangeCheck();
                serializedObject.Update();
                EditorGUILayout.PropertyField(_wheelsScripts, true);
                EditorGUILayout.PropertyField(_sensors, true);
                EditorGUILayout.PropertyField(_frontJoint, _frontCouplerLabel, true);
                EditorGUILayout.PropertyField(_backJoint, _backCouplerLabel, true);
                EditorGUILayout.PropertyField(_jointAnchor, true);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_wagon, "Changed Other Settings");
                    serializedObject.ApplyModifiedProperties();
                    MarkSceneAlteration();
                }
                #endregion
            }

            GUILayout.EndVertical();
        }

        private void MarkSceneAlteration()
        {
            if (!Application.isPlaying)
            {
#if UNITY_EDITOR
                EditorUtility.SetDirty(_wagon);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
#endif
            }
        }
    }
}
