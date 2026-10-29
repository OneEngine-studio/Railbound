using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using WSMGameStudio.Splines;
using System;

namespace WSMGameStudio.RailroadSystem
{
    public class RailroadTurnoutCreatorWindow : EditorWindow
    {
        private string _turnoutPrefabName;
        private string _outputDirectory = string.Empty;
        private string _selectedFolder = string.Empty;
        private string _defaultDiretory = "WSM Game Studio/Train Controller_v3/Prefabs/Turnouts/";
        private string _txtMessage;
        private float _labelsWidth = 140f;
        private Color _txtColor = Color.black;
        private GUIStyle _errorMessageStyle;
        private GUIStyle _instructionsTitleStyle;
        private GUIStyle _instructionsStyle;
        private GUIStyle _menuBoxStyle;

        private Transform _referenceTransform;

        private GameObject _mainRails;
        private GameObject _turnoutRails;
        private GameObject _paralledRails;

        //public List<GameObject> mainRails;
        //public GameObject turnoutRails;

        [MenuItem("WSM Game Studio/Train Controller/Utilities/Railroad Turnout Creator")]
        [MenuItem("Window/WSM Game Studio/Railroad Turnout Creator")]
        public static void ShowWindow()
        {
            RailroadTurnoutCreatorWindow currentWindow = GetWindow<RailroadTurnoutCreatorWindow>("Railroad Turnout Creator");
            currentWindow.minSize = new Vector2(540, 240);
        }

        /// <summary>
        /// Render Window
        /// </summary>
        private void OnGUI()
        {
            GUILayout.Label("Railroad Turnout Creator", EditorStyles.boldLabel);

            //Set up the box style if null
            if (_menuBoxStyle == null)
            {
                _menuBoxStyle = new GUIStyle(GUI.skin.box);
                _menuBoxStyle.normal.textColor = GUI.skin.label.normal.textColor;
                _menuBoxStyle.fontStyle = FontStyle.Bold;
                _menuBoxStyle.alignment = TextAnchor.UpperLeft;
            }

            if (_outputDirectory == string.Empty)
                _outputDirectory = _defaultDiretory;

            GUILayout.BeginVertical(_menuBoxStyle);

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(_labelsWidth));
            EditorGUILayout.LabelField("Main Rails", GUILayout.Width(_labelsWidth));
            EditorGUILayout.LabelField("Turnout Rails", GUILayout.Width(_labelsWidth));
            EditorGUILayout.LabelField("Parallel Rails (optional)", GUILayout.Width(_labelsWidth));
            EditorGUILayout.LabelField("Name", GUILayout.Width(_labelsWidth));
            EditorGUILayout.LabelField("Output Directory", GUILayout.Width(_labelsWidth));
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            _mainRails = EditorGUILayout.ObjectField(_mainRails, typeof(GameObject), true) as GameObject;
            _turnoutRails = EditorGUILayout.ObjectField(_turnoutRails, typeof(GameObject), true) as GameObject;
            _paralledRails = EditorGUILayout.ObjectField(_paralledRails, typeof(GameObject), true) as GameObject;
            _turnoutPrefabName = EditorGUILayout.TextField(_turnoutPrefabName);
            using (new EditorGUI.DisabledScope(true))
            {
                _outputDirectory = EditorGUILayout.TextField(_outputDirectory);
            }
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            if (GUILayout.Button("Browse Folder"))
            {
                _selectedFolder = EditorUtility.OpenFolderPanel("Select Output Directory", _defaultDiretory, "");
                if (_selectedFolder != string.Empty)
                    _outputDirectory = string.Concat(_selectedFolder.Replace(string.Format("{0}/", Application.dataPath), string.Empty), "/");
            }

            if (GUILayout.Button("Create"))
            {
                CreateTurnout();
            }

            _errorMessageStyle = new GUIStyle();
            _errorMessageStyle.normal.textColor = _txtColor;
            GUILayout.Label(_txtMessage, _errorMessageStyle);

            string instructions = string.Format(
            " 1) Drag & Drop the rails (Baked Segments) prefabs to the scene {0}" +
            " 2) Select a name and output folder{0}" +
            " 3) Select the rails segments from your scene{0}" +
            " 4) Click on the Create Button", System.Environment.NewLine);

            _instructionsTitleStyle = new GUIStyle();
            _instructionsTitleStyle.normal.textColor = Color.blue;
            _instructionsTitleStyle.fontStyle = FontStyle.Bold;
            GUILayout.Label(" Instructions", _instructionsTitleStyle);

            _instructionsStyle = new GUIStyle();
            _instructionsStyle.normal.textColor = Color.blue;
            GUILayout.Label(instructions, _instructionsStyle);

            GUILayout.EndVertical();
        }

        /// <summary>
        /// Save turnout prefab
        /// </summary>
        private void CreateTurnout()
        {
            _txtMessage = string.Empty;
            int railsCount = 0;
            int bakedSegmentsCount = 0;

            if (_turnoutPrefabName == string.Empty)
            {
                ShowMessage("Please, enter a name for your turnout before proceding", Splines.MessageType.Error);
                return;
            }

            if (_mainRails == null)
            {
                ShowMessage("Main rails not selected", Splines.MessageType.Error);
                return;
            }
            else
            {
                railsCount++;
                if (IsBakeSegment(_mainRails)) bakedSegmentsCount++;
            }

            if (_turnoutRails == null)
            {
                ShowMessage("Turnout rails not selected", Splines.MessageType.Error);
                return;
            }
            else
            {
                railsCount++;
                if (IsBakeSegment(_turnoutRails)) bakedSegmentsCount++;
            }

            if (_paralledRails != null)
            {
                railsCount++;
                if (IsBakeSegment(_paralledRails)) bakedSegmentsCount++;
            }

            if (bakedSegmentsCount != railsCount)
            {
                ShowMessage("All selected objects must be Baked Segments", Splines.MessageType.Error);
                return;
            }

            DirectoryInfo info = new DirectoryInfo(Application.dataPath);
            string folderPath = Path.Combine(info.Name, _outputDirectory);
            string prefabFilePath = Path.Combine(folderPath, _turnoutPrefabName + ".prefab");

            //Check folder existence
            if (!Directory.Exists(folderPath))
            {
                ShowMessage(string.Format("Folder does not exist: {0}", folderPath), Splines.MessageType.Error);
                return;
            }

            //Overwrite dialog
            if (Exists(prefabFilePath))
            {
                if (!ShowOverwriteDialog(_turnoutPrefabName))
                    return;
            }

            GameObject sceneObject = new GameObject(_turnoutPrefabName);
            RailroadSwitch_v3 railroadSwitchScript = sceneObject.AddComponent<RailroadSwitch_v3>();
            railroadSwitchScript.RailsColliders = new List<GameObject>();
            railroadSwitchScript.RailsSplines = new List<Spline>();

            _referenceTransform = _mainRails.transform;
            sceneObject.transform.SetParent(_referenceTransform.parent);
            sceneObject.transform.localPosition = _referenceTransform.localPosition;
            sceneObject.transform.localRotation = _referenceTransform.localRotation;

            ConfigureRail(sceneObject, _mainRails);
            ConfigureRail(sceneObject, _turnoutRails, true);
            if (_paralledRails != null) ConfigureRail(sceneObject, _paralledRails);

            //Save prefab as .prefab file
            bool success = false;
            GameObject prefab = SavePrefabFile(sceneObject, prefabFilePath, out success);

            if (success)
            {
                ShowMessage(string.Format("{0} Created Succesfuly!", _turnoutPrefabName), Splines.MessageType.Success);
                ResetForm();
            }
            else
                ShowMessage(string.Format("Could not create prefab.{0}Please check Console Window for more information.", System.Environment.NewLine), Splines.MessageType.Error);
        }

        //Reset form inputs
        private void ResetForm()
        {
            _mainRails = null;
            _turnoutRails = null;
            _paralledRails = null;
            _turnoutPrefabName = string.Empty;
        }

        /// <summary>
        /// Configure rails on railroad switch
        /// </summary>
        /// <param name="railroadSwitch"></param>
        /// <param name="rails"></param>
        /// <param name="deactivateRails"></param>
        private static void ConfigureRail(GameObject railroadSwitch, GameObject rails, bool deactivateRails = false)
        {
            RailroadSwitch_v3 railroadSwitchScript = railroadSwitch.GetComponent<RailroadSwitch_v3>();

            foreach (Transform child in rails.transform)
            {
                if (child.GetComponent<MeshCollider>() != null)
                {
                    railroadSwitchScript.RailsColliders.Add(child.gameObject);

                    if (deactivateRails)
                        child.gameObject.SetActive(false);
                }

                Spline spline = child.GetComponentInChildren<Spline>();
                if (spline != null)
                {
                    railroadSwitchScript.RailsSplines.Add(spline);
                }
            }

            rails.transform.SetParent(railroadSwitch.transform);
        }

        /// <summary>
        /// Validate if selected object is a baked segment
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        private bool IsBakeSegment(GameObject obj)
        {
            return (obj.GetComponent<BakedSegment>() != null);
        }

        #region Auxiliar Methods
        /// <summary>
        /// Save prefab as .prefab file
        /// </summary>
        /// <param name="prefabName"></param>
        /// <param name="prefabFilePath"></param>
        /// <returns></returns>
        private GameObject SavePrefabFile(GameObject sceneObject, string prefabFilePath, out bool success)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(sceneObject, prefabFilePath, InteractionMode.UserAction, out success);
            return prefab;
        }

        /// <summary>
        /// Show message on Window
        /// </summary>
        /// <param name="message"></param>
        /// <param name="type"></param>
        private void ShowMessage(string message, Splines.MessageType type)
        {
            _txtMessage = message;

            switch (type)
            {
                case Splines.MessageType.Success:
                    _txtColor = new Color(0, 0.5f, 0); //Dark green;
                    break;
                case Splines.MessageType.Warning:
                    _txtColor = Color.yellow;
                    break;
                case Splines.MessageType.Error:
                    _txtColor = Color.red;
                    break;
            }
        }

        /// <summary>
        /// Show overwrite dialog confirmation
        /// </summary>
        /// <param name="meshName"></param>
        /// <returns></returns>
        private bool ShowOverwriteDialog(string prefabName)
        {
            return EditorUtility.DisplayDialog("Are you sure?",
                            string.Format("A prefab named {0} already exists. Do you want to overwrite it?", prefabName.ToUpper()),
                            "Yes",
                            "No");
        }

        /// <summary>
        /// Check if file already exists
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        private bool Exists(string filePath)
        {
            return File.Exists(filePath);
        }
        #endregion
    }
}
