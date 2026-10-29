using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using UnityEngine.SceneManagement;

namespace WSMGameStudio.RailroadSystem
{
    [CustomEditor(typeof(TrainDoorsController))]
    public class TrainDoorsControllerInspector : Editor
    {
        private int _selectedMenuIndex = 0;
        private string[] _toolbarMenuOptions = new[] { "Doors", "SFX" };
        private GUIStyle _menuBoxStyle;

        SerializedProperty _openCabinDoorSFX;
        SerializedProperty _closeCabinDoorSFX;
        SerializedProperty _openPassengerDoorSFX;
        SerializedProperty _closePassengerDoorSFX;
        SerializedProperty _closeDoorsWarningSFX;

        SerializedProperty _cabinDoorLeft;
        SerializedProperty _cabinDoorRight;
        SerializedProperty _passengerDoorsLeft;
        SerializedProperty _passengerDoorsRight;

        private void OnEnable()
        {
            _openCabinDoorSFX = serializedObject.FindProperty("openCabinDoorSFX");
            _closeCabinDoorSFX = serializedObject.FindProperty("closeCabinDoorSFX");
            _openPassengerDoorSFX = serializedObject.FindProperty("openPassengerDoorSFX");
            _closePassengerDoorSFX = serializedObject.FindProperty("closePassengerDoorSFX");
            _closeDoorsWarningSFX = serializedObject.FindProperty("closeDoorsWarningSFX");

            _cabinDoorLeft = serializedObject.FindProperty("cabinDoorLeft");
            _cabinDoorRight = serializedObject.FindProperty("cabinDoorRight");
            _passengerDoorsLeft = serializedObject.FindProperty("passengerDoorsLeft");
            _passengerDoorsRight = serializedObject.FindProperty("passengerDoorsRight");
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

            if (_selectedMenuIndex == (int)TrainDoorsControllerMenu.Doors)
            {
                GUILayout.Label("DOORS SETTINGS", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_cabinDoorLeft);
                EditorGUILayout.PropertyField(_cabinDoorRight);
                EditorGUILayout.PropertyField(_passengerDoorsLeft, true);
                EditorGUILayout.PropertyField(_passengerDoorsRight, true);
            }
            else if (_selectedMenuIndex == (int)TrainDoorsControllerMenu.SFX)
            {
                GUILayout.Label("DOORS SFX", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_openCabinDoorSFX);
                EditorGUILayout.PropertyField(_closeCabinDoorSFX);
                EditorGUILayout.PropertyField(_openPassengerDoorSFX);
                EditorGUILayout.PropertyField(_closePassengerDoorSFX);
                EditorGUILayout.PropertyField(_closeDoorsWarningSFX);
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
