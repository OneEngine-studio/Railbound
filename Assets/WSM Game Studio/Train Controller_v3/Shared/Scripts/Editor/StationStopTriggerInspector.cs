using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WSMGameStudio.RailroadSystem
{
    [CustomEditor(typeof(StationStopTrigger))]
    public class StationStopTriggerInspector : Editor
    {
        private GUIStyle _menuBoxStyle;

        private SerializedProperty _stopMode;
        private SerializedProperty _stationDoorDirection;
        private SerializedProperty _stationBehaviour;
        private SerializedProperty _stopTimeout;
        private SerializedProperty _randomStopProbability;
        private SerializedProperty _turnOffEngines;
        private SerializedProperty _reverseTrainDirection;
        private SerializedProperty _reverseDirectionMode;

        private void OnEnable()
        {
            _stopMode = serializedObject.FindProperty("_stopMode");
            _stationDoorDirection = serializedObject.FindProperty("_stationDoorDirection");
            _stationBehaviour = serializedObject.FindProperty("_stationBehaviour");
            _stopTimeout = serializedObject.FindProperty("_stopTimeout");
            _randomStopProbability = serializedObject.FindProperty("_randomStopProbability");
            _turnOffEngines = serializedObject.FindProperty("_turnOffEngines");
            _reverseTrainDirection = serializedObject.FindProperty("_reverseTrainDirection");
            _reverseDirectionMode = serializedObject.FindProperty("_reverseDirectionMode");
        }

        public override void OnInspectorGUI()
        {
            //base.OnInspectorGUI();

            //Set up the box style if null
            if (_menuBoxStyle == null)
            {
                _menuBoxStyle = new GUIStyle(GUI.skin.box);
                _menuBoxStyle.normal.textColor = GUI.skin.label.normal.textColor;
                _menuBoxStyle.fontStyle = FontStyle.Bold;
                _menuBoxStyle.alignment = TextAnchor.UpperLeft;
            }

            GUILayout.BeginVertical(_menuBoxStyle);

            serializedObject.Update();

            EditorGUILayout.PropertyField(_stopMode, false);

            if (_stopMode.intValue == (int)StopMode.Random)
                EditorGUILayout.PropertyField(_randomStopProbability, false);

            EditorGUILayout.PropertyField(_stationDoorDirection, false);
            EditorGUILayout.PropertyField(_stationBehaviour, false);
            EditorGUILayout.PropertyField(_stopTimeout, false);
            EditorGUILayout.PropertyField(_turnOffEngines, false);
            EditorGUILayout.PropertyField(_reverseTrainDirection, false);

            if (_reverseTrainDirection.boolValue)
                EditorGUILayout.PropertyField(_reverseDirectionMode, false);

            serializedObject.ApplyModifiedProperties();

            GUILayout.EndVertical();
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
