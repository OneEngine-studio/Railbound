using UnityEngine;
using WSMGameStudio.Splines;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using UnityEngine.SceneManagement;

namespace WSMGameStudio.RailroadSystem
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(SplineBasedLocomotive))]
    public class SplineBasedLocomotiveInspector : Editor
    {
        private SplineBasedLocomotive _splineBasedLocomotive;

        private int _selectedMenuIndex = 0;
        private string[] _toolbarMenuOptions = new[] { "Controls", "Wagons", "Lights", "VFX/SFX", "Other Settings" };
        private GUIStyle _menuBoxStyle;

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

        private GUIContent _btnMoveToStart = new GUIContent("Move to Start Position", "Move object to start position along the spline");
        private GUIContent _btnRestart = new GUIContent("Restart Follower", "Restarts follower to its initial position");
        private GUIContent _btnRecalculateOffset = new GUIContent("Recalculate Offset", "Recalculate following offset");
        GUIContent _frontCouplerLabel = new GUIContent("Front Coupler");
        GUIContent _backCouplerLabel = new GUIContent("Back Coupler");
        GUIContent _routeSplinesLabel = new GUIContent("Route Splines");

        //Follower properties
        private SerializedProperty _splines;
        private SerializedProperty _followerBehaviour;
        private SerializedProperty _customStartPosition;
        private SerializedProperty _followersOffset;
        private SerializedProperty _linkedFollowersBehaviour;
        private SerializedProperty _linkedFollowers;
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
        private SerializedProperty _bell;
        //Events
        private SerializedProperty _onReachedEndOfRoute;
        //Debbuging
        private SerializedProperty _visualizeRouteOnEditor;

        private void OnEnable()
        {
            //Follower properties
            _splines = serializedObject.FindProperty("splines");
            _followerBehaviour = serializedObject.FindProperty("followerBehaviour");
            _customStartPosition = serializedObject.FindProperty("customStartPosition");
            _followersOffset = serializedObject.FindProperty("followersOffset");
            _linkedFollowersBehaviour = serializedObject.FindProperty("linkedFollowersBehaviour");
            _linkedFollowers = serializedObject.FindProperty("linkedFollowers");
            //Train controls
            _enginesOn = serializedObject.FindProperty("_enginesOn");
            _maxSpeed = serializedObject.FindProperty("_maxSpeed");
            _speedUnit = serializedObject.FindProperty("_speedUnit");
            _acceleration = serializedObject.FindProperty("_acceleration");
            _automaticBrakes = serializedObject.FindProperty("_automaticBrakes");
            _brake = serializedObject.FindProperty("_brake");
            _emergencyBrakes = serializedObject.FindProperty("_emergencyBrakes");
            //Acceleration settings
            _accelerationRate = serializedObject.FindProperty("_accelerationRate");
            _brakingDecelerationRate = serializedObject.FindProperty("_brakingDecelerationRate");
            _inertiaDecelerationRate = serializedObject.FindProperty("_inertiaDecelerationRate");
            //Train lights
            _externalLights = serializedObject.FindProperty("_externalLights");
            _internalLights = serializedObject.FindProperty("_internalLights");
            //VFX
            _enableSmoke = serializedObject.FindProperty("_enableSmoke");
            _enableBrakingSparks = serializedObject.FindProperty("_enableBrakingSparks");
            _minSmokeEmission = serializedObject.FindProperty("_minSmokeEmission");
            _maxSmokeEmission = serializedObject.FindProperty("_maxSmokeEmission");
            _smokeParticles = serializedObject.FindProperty("_smokeParticles");
            _brakingSparksParticles = serializedObject.FindProperty("_brakingSparksParticles");
            //SFX
            _hornSFX = serializedObject.FindProperty("_hornSFX");
            _bellSFX = serializedObject.FindProperty("_bellSFX");
            _engineSFX = serializedObject.FindProperty("_engineSFX");
            _wheelsSFX = serializedObject.FindProperty("_wheelsSFX");
            _brakesSFX = serializedObject.FindProperty("_brakesSFX");
            _idleEnginePitch = serializedObject.FindProperty("_idleEnginePitch");
            _maxEnginePitch = serializedObject.FindProperty("_maxEnginePitch");
            _minWheelsPitch = serializedObject.FindProperty("_minWheelsPitch");
            _maxWheelsPitch = serializedObject.FindProperty("_maxWheelsPitch");
            //Mechanical Parts
            _wheelsScripts = serializedObject.FindProperty("_wheelsScripts");
            _backJoint = serializedObject.FindProperty("_backJoint");
            _frontJoint = serializedObject.FindProperty("_frontJoint");
            _bell = serializedObject.FindProperty("_bell");
            //Events
            _onReachedEndOfRoute = serializedObject.FindProperty("_onReachedEndOfRoute");
            //Debbuging
            _visualizeRouteOnEditor = serializedObject.FindProperty("_visualizeRouteOnEditor");
        }

        /// <summary>
        /// 
        /// </summary>
        public override void OnInspectorGUI()
        {
            _splineBasedLocomotive = target as SplineBasedLocomotive;

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
                #region TRAIN CONTROLS
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
                _maxSpeed.floatValue = Mathf.Abs(_maxSpeed.floatValue);
                GUILayout.EndHorizontal();

                EditorGUILayout.PropertyField(_accelerationRate, false);
                EditorGUILayout.PropertyField(_brakingDecelerationRate, false);
                EditorGUILayout.PropertyField(_inertiaDecelerationRate, false);
                
                ///Follower Settings
                EditorGUILayout.LabelField(string.Empty, GUI.skin.horizontalSlider);
                GUILayout.Label("SPLINE SETTINGS", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_splines, _routeSplinesLabel, true);
                EditorGUILayout.PropertyField(_followerBehaviour, false);
                EditorGUILayout.PropertyField(_customStartPosition, false);
                EditorGUILayout.PropertyField(_visualizeRouteOnEditor, false);

                EditorGUILayout.LabelField(string.Empty, GUI.skin.horizontalSlider);
                GUILayout.Label("TRAIN OPERATIONS", EditorStyles.boldLabel);
                if (_splineBasedLocomotive.EnginesOn)
                {
                    if (GUILayout.Button(stopEngine))
                    {
                        _splineBasedLocomotive.ToggleEngine();
                        MarkSceneAlteration(_splineBasedLocomotive.gameObject);
                    }

                    using (new EditorGUI.DisabledScope(!Application.isPlaying))
                    {
                        if (_splineBasedLocomotive.DoorsController != null)
                        {
                            GUILayout.BeginHorizontal();
                            //Cabin left door
                            if (!_splineBasedLocomotive.DoorsController.CabinLeftDoorOpen)
                            {
                                if (GUILayout.Button(openLeftCabinDoor))
                                    _splineBasedLocomotive.DoorsController.OpenCabinDoorLeft();
                            }
                            else
                            {
                                if (GUILayout.Button(closeLeftCabinDoor))
                                    _splineBasedLocomotive.DoorsController.CloseCabinDoorLeft();
                            }
                            //Cabin right door
                            if (!_splineBasedLocomotive.DoorsController.CabinRightDoorOpen)
                            {
                                if (GUILayout.Button(openRightCabinDoor))
                                    _splineBasedLocomotive.DoorsController.OpenCabinDoorRight();
                            }
                            else
                            {
                                if (GUILayout.Button(closeRightCabinDoor))
                                    _splineBasedLocomotive.DoorsController.CloseCabinDoorRight();
                            }
                            GUILayout.EndHorizontal();

                            GUILayout.BeginHorizontal();
                            //Passengers left door
                            if (!_splineBasedLocomotive.DoorsController.PassengerLeftDoorOpen)
                            {
                                if (GUILayout.Button(openLeftPassengerDoor))
                                {
                                    _splineBasedLocomotive.DoorsController.StationDoorDirection = StationDoorDirection.Left;
                                    _splineBasedLocomotive.DoorsController.OpenPassengersDoors();
                                }
                            }
                            else
                            {
                                if (GUILayout.Button(closeLeftPassengerDoor))
                                    _splineBasedLocomotive.DoorsController.ClosePassengersLeftDoors();
                            }
                            //Passengers right door
                            if (!_splineBasedLocomotive.DoorsController.PassengerRightDoorOpen)
                            {
                                if (GUILayout.Button(openRightPassengerDoor))
                                {
                                    _splineBasedLocomotive.DoorsController.StationDoorDirection = StationDoorDirection.Right; _splineBasedLocomotive.DoorsController.OpenPassengersDoors();
                                }
                            }
                            else
                            {
                                if (GUILayout.Button(closeRightPassengerDoor))
                                    _splineBasedLocomotive.DoorsController.ClosePassengersRightDoors();
                            }
                            GUILayout.EndHorizontal();
                        }
                    }
                }
                else
                {
                    if (GUILayout.Button(startEngine))
                    {
                        _splineBasedLocomotive.ToggleEngine();
                        MarkSceneAlteration(_splineBasedLocomotive.gameObject);
                    }
                }

                DrawFollowerOperations();
                #endregion
            }
            else if (_selectedMenuIndex == (int)TrainControllerInspectorMenu.Wagons)
            {
                #region LINKED FOLLOWERS
                GUILayout.Label("TRAIN WAGONS", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_followersOffset, new GUIContent("First Wagon Offset"), false);
                EditorGUILayout.PropertyField(_linkedFollowersBehaviour, new GUIContent("Wagons Behaviour"), false);
                EditorGUILayout.PropertyField(_linkedFollowers, new GUIContent("Wagons"), true);

                EditorGUILayout.LabelField(string.Empty, GUI.skin.horizontalSlider);
                GUILayout.Label("WAGONS OPERATIONS", EditorStyles.boldLabel);

                #region EDITOR MODE ONLY

                if (GUILayout.Button("Connect Wagons"))
                {
#if UNITY_EDITOR
                    Undo.RecordObject(_splineBasedLocomotive, "Wagons Connected");
#endif
                    _splineBasedLocomotive.CalculateWagonsPositions();
#if UNITY_EDITOR
                    if (_splineBasedLocomotive.ConnectedWagons != null)
                    {
                        for (int index = 0; index < _splineBasedLocomotive.ConnectedWagons.Count; index++)
                            EditorUtility.SetDirty(_splineBasedLocomotive.ConnectedWagons[index]);
                    }
                    MarkSceneAlteration(_splineBasedLocomotive);
#endif
                }
                #endregion

                #region PLAY MODE ONLY

                using (new EditorGUI.DisabledScope(!Application.isPlaying))
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Decouple First Wagon"))
                    {
                        _splineBasedLocomotive.DecoupleFirstWagon();
                    }

                    if (GUILayout.Button("Decouple Last Wagon"))
                    {
                        _splineBasedLocomotive.DecoupleLastWagon();
                    }
                    GUILayout.EndHorizontal();
                }
                #endregion

                DrawFollowerOperations();

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
                    Undo.RecordObject(_splineBasedLocomotive, "Toggled External Lights");
#endif
                    _splineBasedLocomotive.ToggleLights();
#if UNITY_EDITOR
                    MarkSceneAlteration(_splineBasedLocomotive.gameObject);
#endif
                }

                if (GUILayout.Button("Internal Lights"))
                {
#if UNITY_EDITOR
                    Undo.RecordObject(_splineBasedLocomotive, "Toggled Internal Lights");
#endif
                    _splineBasedLocomotive.ToggleInternalLights();
#if UNITY_EDITOR
                    MarkSceneAlteration(_splineBasedLocomotive.gameObject);
#endif
                }
                GUILayout.EndHorizontal();

                #endregion
            }
            else if (_selectedMenuIndex == (int)TrainControllerInspectorMenu.SFX)
            {
                #region SFX

                GUILayout.Label("TRAIN VFX", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_enableSmoke, false);
                EditorGUILayout.PropertyField(_smokeParticles, false);
                EditorGUILayout.PropertyField(_minSmokeEmission, false);
                EditorGUILayout.PropertyField(_maxSmokeEmission, false);
                EditorGUILayout.PropertyField(_enableBrakingSparks, false);
                EditorGUILayout.PropertyField(_brakingSparksParticles, true);

                EditorGUILayout.LabelField(string.Empty, GUI.skin.horizontalSlider);
                GUILayout.Label("TRAIN SFX", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_hornSFX, false);
                EditorGUILayout.PropertyField(_bellSFX, false);
                EditorGUILayout.PropertyField(_engineSFX, false);
                EditorGUILayout.PropertyField(_wheelsSFX, false);
                EditorGUILayout.PropertyField(_brakesSFX, false);
                EditorGUILayout.PropertyField(_idleEnginePitch, false);
                EditorGUILayout.PropertyField(_maxEnginePitch, false);
                EditorGUILayout.PropertyField(_minWheelsPitch, false);
                EditorGUILayout.PropertyField(_maxWheelsPitch, false);

                EditorGUILayout.LabelField(string.Empty, GUI.skin.horizontalSlider);
                GUILayout.Label("SFX OPERATIONS", EditorStyles.boldLabel);
                //Enabled only on lpay mode
                using (new EditorGUI.DisabledScope(!Application.isPlaying))
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Honk"))
                    {
                        _splineBasedLocomotive.Honk();
                    }

                    if (GUILayout.Button("Bell"))
                    {
                        _splineBasedLocomotive.ToogleBell();
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
                EditorGUILayout.PropertyField(_frontJoint, _frontCouplerLabel, false);
                EditorGUILayout.PropertyField(_backJoint, _backCouplerLabel, false);
                EditorGUILayout.PropertyField(_bell, false);

                EditorGUILayout.LabelField(string.Empty, GUI.skin.horizontalSlider);
                GUILayout.Label("CUSTOM EVENTS", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_onReachedEndOfRoute, false);

                #endregion
            }

            serializedObject.ApplyModifiedProperties();
            GUILayout.EndVertical();
        }

        /// <summary>
        /// Draw Follower Operations
        /// </summary>
        private void DrawFollowerOperations()
        {
            #region FOLLOWER OPERATIONS
            /*
             * Follower Operations
             */
            EditorGUILayout.LabelField(string.Empty, GUI.skin.horizontalSlider);
            GUILayout.Label("SPLINE OPERATIONS", EditorStyles.boldLabel);

            GUILayout.BeginHorizontal();

            if (Selection.gameObjects != null)
            {
                if (GUILayout.Button(_btnMoveToStart))
                {
                    foreach (var selected in Selection.gameObjects)
                    {
                        SplineBasedLocomotive follower = selected.GetComponent<SplineBasedLocomotive>();
                        if (follower != null)
                        {
                            follower.CalculateWagonsPositions();
                            MarkSceneAlteration(selected);
                        }
                    }
                }

                if (GUILayout.Button(_btnRestart))
                {
                    foreach (var selected in Selection.gameObjects)
                    {
                        SplineBasedLocomotive follower = selected.GetComponent<SplineBasedLocomotive>();
                        if (follower != null)
                        {
                            follower.RestartFollower();
                            MarkSceneAlteration(selected);
                        }
                    }
                }

                if (GUILayout.Button(_btnRecalculateOffset))
                {
                    foreach (var selected in Selection.gameObjects)
                    {
                        SplineBasedLocomotive follower = selected.GetComponent<SplineBasedLocomotive>();
                        if (follower != null)
                        {
                            follower.RecalculateOffset();
                            MarkSceneAlteration(selected);
                        }
                    }
                }
            }

            GUILayout.EndHorizontal();
            #endregion
        }

        private void OnSceneGUI()
        {
            _splineBasedLocomotive = target as SplineBasedLocomotive;

            if (_splineBasedLocomotive.VisualizeRouteOnEditor)
                DrawSplinesOnSceneView(_splineBasedLocomotive.splines);
        }

        /// <summary>
        /// Draw Splines On Scene View
        /// </summary>
        /// <param name="sceneView"></param>
        private void DrawSplinesOnSceneView(List<Spline> splines)//, SceneView sceneView)
        {
            if (splines == null)
                return;

            if (splines.Count == 0)
                return;

            Transform splineTransform;
            Vector3 startPoint, endPoint, tangent1, tangent2, splineStart = Vector3.zero, splineEnd = Vector3.zero;
            Vector3 startDirection, endDirection;
            Quaternion startRotation, endRotation;

            for (int splineIndex = 0; splineIndex < splines.Count; splineIndex++)
            {
                if (splines[splineIndex] == null)
                {
                    Debug.LogWarning("Rote spline cannot be null. Please remove null reference from route");
                    continue;
                }

                splineTransform = splines[splineIndex].GetComponent<Transform>();

                for (int i = 0; i < splines[splineIndex].ControlPointCount - 1; i += 3)
                {
                    startPoint = splineTransform.TransformPoint(splines[splineIndex].GetControlPointPosition(i));
                    tangent1 = splineTransform.TransformPoint(splines[splineIndex].GetControlPointPosition(i + 1));
                    tangent2 = splineTransform.TransformPoint(splines[splineIndex].GetControlPointPosition(i + 2));
                    endPoint = splineTransform.TransformPoint(splines[splineIndex].GetControlPointPosition(i + 3));

                    Handles.DrawBezier(startPoint, endPoint, tangent1, tangent2, Color.magenta, null, 5f);

                    splineStart = i == 0 ? startPoint : splineStart;
                    splineEnd = endPoint;

                    if (i != 0)
                    {
                        startDirection = splineTransform.InverseTransformDirection(splines[splineIndex].GetDirection(startPoint, tangent1, tangent2, endPoint, 0f));
                        startRotation = Quaternion.FromToRotation(Vector3.forward, startDirection);
                        DrawDirectionCone(startPoint, startRotation, Color.white, 3.5f);
                    }
                }

                startDirection = splines[splineIndex].GetDirection(0);
                endDirection = splines[splineIndex].GetDirection(splines[splineIndex].ControlPointCount - 1);

                startRotation = Quaternion.FromToRotation(Vector3.forward, startDirection);
                endRotation = Quaternion.FromToRotation(Vector3.forward, endDirection);

                DrawDirectionCone(splineStart, startRotation, Color.yellow, 5f);
                DrawDirectionCone(splineEnd, endRotation, Color.yellow, 5f);
            }
        }

        /// <summary>
        /// Draw route direction cone
        /// </summary>
        /// <param name="conePosition"></param>
        /// <param name="coneRotation"></param>
        /// <param name="color"></param>
        /// <param name="coneSize"></param>
        private void DrawDirectionCone(Vector3 conePosition, Quaternion coneRotation, Color color, float coneSize)
        {
            float size = HandleUtility.GetHandleSize(conePosition) * coneSize;
            Color originalHandleColor = Handles.color;
            Handles.color = color;
            Handles.Button(conePosition, coneRotation, size * 0.04f, size * 0.06f, Handles.ConeHandleCap);
            Handles.color = originalHandleColor;
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
