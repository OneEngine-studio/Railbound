using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using UnityEngine.SceneManagement;

namespace WSMGameStudio.RailroadSystem
{
    [CustomEditor(typeof(TrainController_v3))]
    public class TrainControllerInspector : Editor
    {
        private TrainController_v3 _trainController_v3;

        private int _selectedMenuIndex = 0;
        private string[] _toolbarMenuOptions = new[] { "Controls", "Wagons", "Lights", "VFX/SFX", "Other Settings" };
        private GUIStyle _menuBoxStyle;

        GUIContent _frontCouplerLabel = new GUIContent("Front Coupler");
        GUIContent _backCouplerLabel = new GUIContent("Back Coupler");

        private const string stopEngine = "Stop Engine";
        private const string startEngine = "Start Engine";
        private const string openLeftCabinDoor = "Open Left Cabin Door";
        private const string openRightCabinDoor = "Open Right Cabin Door";
        private const string openLeftPassengerDoor = "Open Left Passenger Door";
        private const string openRightPassengerDoor = "Open Right Passenger Door";
        private const string closeLeftCabinDoor = "Close Left Cabin Door";
        private const string closeRightCabinDoor = "Close Right Cabin Door";
        private const string closeLeftPassengerDoor = "Close Left Passenger Door";
        private const string closeRightPassengerDoor = "Close Right Passenger Door";

        //Train controls
        private SerializedProperty _enginesOn;
        private SerializedProperty _maxSpeed;
        private SerializedProperty _speedUnit;
        private SerializedProperty _acceleration;
        private SerializedProperty _automaticBrakes;
        private SerializedProperty _brake;
        private SerializedProperty _emergencyBrakes;
        //Acceleration settings
        private SerializedProperty _accelerationRate;
        private SerializedProperty _brakingDecelerationRate;
        private SerializedProperty _inertiaDecelerationRate;
        //Wagons
        private SerializedProperty _wagons;
        //Train lights
        private SerializedProperty _externalLights;
        private SerializedProperty _internalLights;
        //VFX
        private SerializedProperty _enableSmoke;
        private SerializedProperty _enableBrakingSparks;
        private SerializedProperty _minSmokeEmission;
        private SerializedProperty _maxSmokeEmission;
        private SerializedProperty _smokeParticles;
        private SerializedProperty _brakingSparksParticles;
        //SFX
        private SerializedProperty _hornSFX;
        private SerializedProperty _bellSFX;
        private SerializedProperty _engineSFX;
        private SerializedProperty _wheelsSFX;
        private SerializedProperty _brakesSFX;
        private SerializedProperty _idleEnginePitch;
        private SerializedProperty _maxEnginePitch;
        private SerializedProperty _minWheelsPitch;
        private SerializedProperty _maxWheelsPitch;
        //Mechanical parts
        private SerializedProperty _wheelsScripts;
        private SerializedProperty _backJoint;
        private SerializedProperty _frontJoint;
        private SerializedProperty _sensors;
        private SerializedProperty _bell;

        private void OnEnable()
        {
            //Train controls
            _enginesOn = serializedObject.FindProperty("enginesOn");
            _maxSpeed = serializedObject.FindProperty("maxSpeed");
            _speedUnit = serializedObject.FindProperty("speedUnit");
            _acceleration = serializedObject.FindProperty("acceleration");
            _automaticBrakes = serializedObject.FindProperty("automaticBrakes");
            _brake = serializedObject.FindProperty("brake");
            _emergencyBrakes = serializedObject.FindProperty("emergencyBrakes");
            //Acceleration settings
            _accelerationRate = serializedObject.FindProperty("accelerationRate");
            _brakingDecelerationRate = serializedObject.FindProperty("brakingDecelerationRate");
            _inertiaDecelerationRate = serializedObject.FindProperty("inertiaDecelerationRate");
            //Wagons
            _wagons = serializedObject.FindProperty("wagons");
            //Train lights
            _externalLights = serializedObject.FindProperty("externalLights");
            _internalLights = serializedObject.FindProperty("internalLights");
            //VFX
            _enableSmoke = serializedObject.FindProperty("enableSmoke");
            _enableBrakingSparks = serializedObject.FindProperty("enableBrakingSparks");
            _minSmokeEmission = serializedObject.FindProperty("minSmokeEmission");
            _maxSmokeEmission = serializedObject.FindProperty("maxSmokeEmission");
            _smokeParticles = serializedObject.FindProperty("smokeParticles");
            _brakingSparksParticles = serializedObject.FindProperty("brakingSparksParticles");
            //SFX
            _hornSFX = serializedObject.FindProperty("hornSFX");
            _bellSFX = serializedObject.FindProperty("bellSFX");
            _engineSFX = serializedObject.FindProperty("engineSFX");
            _wheelsSFX = serializedObject.FindProperty("wheelsSFX");
            _brakesSFX = serializedObject.FindProperty("brakesSFX");
            _idleEnginePitch = serializedObject.FindProperty("idleEnginePitch");
            _maxEnginePitch = serializedObject.FindProperty("maxEnginePitch");
            _minWheelsPitch = serializedObject.FindProperty("minWheelsPitch");
            _maxWheelsPitch = serializedObject.FindProperty("maxWheelsPitch");
            //Mechanical Parts
            _wheelsScripts = serializedObject.FindProperty("wheelsScripts");
            _backJoint = serializedObject.FindProperty("backJoint");
            _frontJoint = serializedObject.FindProperty("frontJoint");
            _sensors = serializedObject.FindProperty("sensors");
            _bell = serializedObject.FindProperty("bell");
        }

        public override void OnInspectorGUI()
        {
            //base.OnInspectorGUI();
            _trainController_v3 = target as TrainController_v3;

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

            serializedObject.Update();

            if (_selectedMenuIndex == (int)TrainControllerInspectorMenu.Controls)
            {
                #region Controls

                GUILayout.Label("TRAIN CONTROLS", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_enginesOn, false);
                EditorGUILayout.PropertyField(_acceleration, false);
                EditorGUILayout.PropertyField(_automaticBrakes, false);
                EditorGUILayout.PropertyField(_brake, false);
                EditorGUILayout.PropertyField(_emergencyBrakes, false);

                EditorGUILayout.LabelField(string.Empty, GUI.skin.horizontalSlider);
                GUILayout.Label("SPEED SETTINGS", EditorStyles.boldLabel);

                GUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(_maxSpeed, false);
                EditorGUILayout.PropertyField(_speedUnit, GUIContent.none, false, GUILayout.MaxWidth(50));
                _maxSpeed.floatValue = _trainController_v3.EnforceSpeedLimit(_maxSpeed.floatValue);
                GUILayout.EndHorizontal();

                EditorGUILayout.PropertyField(_accelerationRate, false);
                EditorGUILayout.PropertyField(_brakingDecelerationRate, false);
                EditorGUILayout.PropertyField(_inertiaDecelerationRate, false);

                EditorGUILayout.LabelField(string.Empty, GUI.skin.horizontalSlider);
                GUILayout.Label("TRAIN OPERATIONS", EditorStyles.boldLabel);

                GUILayout.BeginVertical();
                if (_trainController_v3.EnginesOn)
                {
                    if (GUILayout.Button(stopEngine))
                    {
                        _trainController_v3.ToggleEngine();
                        MarkSceneAlteration();
                    }
                }
                else
                {
                    if (GUILayout.Button(startEngine))
                    {
                        _trainController_v3.ToggleEngine();
                        MarkSceneAlteration();
                    }
                }

                GUILayout.BeginVertical();

                using (new EditorGUI.DisabledScope(!Application.isPlaying))
                {
                    if (_trainController_v3.DoorsController != null)
                    {
                        GUILayout.BeginHorizontal();
                        //Cabin left door
                        if (!_trainController_v3.DoorsController.CabinLeftDoorOpen)
                        {
                            if (GUILayout.Button(openLeftCabinDoor))
                                _trainController_v3.DoorsController.OpenCabinDoorLeft();
                        }
                        else
                        {
                            if (GUILayout.Button(closeLeftCabinDoor))
                                _trainController_v3.DoorsController.CloseCabinDoorLeft();
                        }
                        //Cabin right door
                        if (!_trainController_v3.DoorsController.CabinRightDoorOpen)
                        {
                            if (GUILayout.Button(openRightCabinDoor))
                                _trainController_v3.DoorsController.OpenCabinDoorRight();
                        }
                        else
                        {
                            if (GUILayout.Button(closeRightCabinDoor))
                                _trainController_v3.DoorsController.CloseCabinDoorRight();
                        }
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        //Passengers left door
                        if (!_trainController_v3.DoorsController.PassengerLeftDoorOpen)
                        {
                            if (GUILayout.Button(openLeftPassengerDoor))
                            {
                                _trainController_v3.DoorsController.StationDoorDirection = StationDoorDirection.Left;
                                _trainController_v3.DoorsController.OpenPassengersDoors();
                            }
                        }
                        else
                        {
                            if (GUILayout.Button(closeLeftPassengerDoor))
                                _trainController_v3.DoorsController.ClosePassengersLeftDoors();
                        }
                        //Passengers right door
                        if (!_trainController_v3.DoorsController.PassengerRightDoorOpen)
                        {
                            if (GUILayout.Button(openRightPassengerDoor))
                            {
                                _trainController_v3.DoorsController.StationDoorDirection = StationDoorDirection.Right; _trainController_v3.DoorsController.OpenPassengersDoors();
                            }
                        }
                        else
                        {
                            if (GUILayout.Button(closeRightPassengerDoor))
                                _trainController_v3.DoorsController.ClosePassengersRightDoors();
                        }
                        GUILayout.EndHorizontal();
                    }
                }

                GUILayout.EndHorizontal();
                GUILayout.EndHorizontal();

                #endregion
            }
            else if (_selectedMenuIndex == (int)TrainControllerInspectorMenu.Wagons)
            {
                #region Wagons

                GUILayout.Label("TRAIN WAGONS", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_wagons, true);

                EditorGUILayout.LabelField(string.Empty, GUI.skin.horizontalSlider);
                GUILayout.Label("WAGONS OPERATIONS", EditorStyles.boldLabel);

                #region EDITOR MODE ONLY

                if (GUILayout.Button("Connect Wagons"))
                {
#if UNITY_EDITOR
                    Undo.RecordObject(_trainController_v3, "Wagons Connected");
#endif
                    _trainController_v3.CalculateWagonsPositions();
#if UNITY_EDITOR
                    if (_trainController_v3.ConnectedWagons != null)
                    {
                        for (int index = 0; index < _trainController_v3.ConnectedWagons.Count; index++)
                            EditorUtility.SetDirty(_trainController_v3.ConnectedWagons[index]);
                    }
                    MarkSceneAlteration();
#endif
                }
                #endregion

                #region PLAY MODE ONLY

                using (new EditorGUI.DisabledScope(!Application.isPlaying))
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Decouple First Wagon"))
                    {
                        _trainController_v3.DecoupleFirstWagon();
                    }

                    if (GUILayout.Button("Decouple Last Wagon"))
                    {
                        _trainController_v3.DecoupleLastWagon();
                    }
                    GUILayout.EndHorizontal();
                }
                #endregion

                #endregion
            }
            else if (_selectedMenuIndex == (int)TrainControllerInspectorMenu.Lights)
            {
                #region Lights

                GUILayout.Label("TRAIN LIGHTS", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_externalLights, true);
                EditorGUILayout.PropertyField(_internalLights, true);

                EditorGUILayout.LabelField(string.Empty, GUI.skin.horizontalSlider);
                GUILayout.Label("LIGHTS OPERATIONS", EditorStyles.boldLabel);

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("External Lights"))
                {
#if UNITY_EDITOR
                    Undo.RecordObject(_trainController_v3, "Toggled External Lights");
#endif
                    _trainController_v3.ToggleLights();
#if UNITY_EDITOR
                    MarkSceneAlteration();
#endif
                }

                if (GUILayout.Button("Internal Lights"))
                {
#if UNITY_EDITOR
                    Undo.RecordObject(_trainController_v3, "Toggled Internal Lights");
#endif
                    _trainController_v3.ToggleInternalLights();
#if UNITY_EDITOR
                    MarkSceneAlteration();
#endif
                }
                GUILayout.EndHorizontal();

                #endregion
            }
            else if (_selectedMenuIndex == (int)TrainControllerInspectorMenu.SFX)
            {
                #region SFX

                GUILayout.Label("TRAIN VFX", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_enableSmoke, true);
                EditorGUILayout.PropertyField(_smokeParticles, true);
                EditorGUILayout.PropertyField(_minSmokeEmission, true);
                EditorGUILayout.PropertyField(_maxSmokeEmission, true);
                EditorGUILayout.PropertyField(_enableBrakingSparks, true);
                EditorGUILayout.PropertyField(_brakingSparksParticles, true);

                EditorGUILayout.LabelField(string.Empty, GUI.skin.horizontalSlider);
                GUILayout.Label("TRAIN SFX", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_hornSFX, true);
                EditorGUILayout.PropertyField(_bellSFX, true);
                EditorGUILayout.PropertyField(_engineSFX, true);
                EditorGUILayout.PropertyField(_wheelsSFX, true);
                EditorGUILayout.PropertyField(_brakesSFX, true);
                EditorGUILayout.PropertyField(_idleEnginePitch, true);
                EditorGUILayout.PropertyField(_maxEnginePitch, true);
                EditorGUILayout.PropertyField(_minWheelsPitch, true);
                EditorGUILayout.PropertyField(_maxWheelsPitch, true);

                EditorGUILayout.LabelField(string.Empty, GUI.skin.horizontalSlider);
                GUILayout.Label("SFX OPERATIONS", EditorStyles.boldLabel);

                //Enabled only on lpay mode
                using (new EditorGUI.DisabledScope(!Application.isPlaying))
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Honk"))
                    {
                        _trainController_v3.Honk();
                    }

                    if (GUILayout.Button("Bell"))
                    {
                        _trainController_v3.ToogleBell();
                    }
                    GUILayout.EndHorizontal();
                }

                #endregion
            }
            else if (_selectedMenuIndex == (int)TrainControllerInspectorMenu.OtherSettings)
            {
                #region OtherSettings

                GUILayout.Label("MECHANICAL COMPONENTS", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_wheelsScripts, true);
                EditorGUILayout.PropertyField(_sensors, true);
                EditorGUILayout.PropertyField(_frontJoint, _frontCouplerLabel, true);
                EditorGUILayout.PropertyField(_backJoint, _backCouplerLabel, true);
                EditorGUILayout.PropertyField(_bell, false);

                #endregion
            }

            serializedObject.ApplyModifiedProperties();
            GUILayout.EndVertical();
        }

        private void MarkSceneAlteration()
        {
            if (!Application.isPlaying)
            {
#if UNITY_EDITOR
                EditorUtility.SetDirty(_trainController_v3);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
#endif
            }
        }
    }
}
