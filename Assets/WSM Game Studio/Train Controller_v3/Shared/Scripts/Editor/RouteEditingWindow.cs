using System.Collections.Generic;
using WSMGameStudio.Splines;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

namespace WSMGameStudio.RailroadSystem
{
    public class RouteEditingWindow : EditorWindow
    {
        private GUIStyle _menuBoxStyle;
        private GUIStyle _dragDropBoxStyle;
        private Color _lightYellow = new Color(1f, 0.95f, 0.7f);

        private static int _index;
        private static string _name;
        private static List<Spline> _splines;

        private static RouteManager _routeManager;

        /// <summary>
        /// Show route editing window
        /// </summary>
        /// <param name="routeIndex"></param>
        public static void ShowWindow(int routeIndex)
        {
            RouteEditingWindow currentWindow = GetWindow<RouteEditingWindow>("Route Editing");
            currentWindow.minSize = new Vector2(400, 160);
            currentWindow.maxSize = new Vector2(400, currentWindow.maxSize.y);

            _routeManager = FindObjectOfType<RouteManager>();

            _index = routeIndex;
            _name = _routeManager.Routes[_index].Name;

            _splines = new List<Spline>();
            foreach (var item in _routeManager.Routes[_index].Splines)
            {
                _splines.Add(item);
            }
        }

        void OnFocus()
        {
            // Remove delegate listener if it has previously been assigned.
            SceneView.duringSceneGui -= this.OnSceneViewGUI;
            // Add (or re-add) the delegate.
            SceneView.duringSceneGui += this.OnSceneViewGUI;
        }

        void OnDestroy()
        {
            // When the window is destroyed, remove the delegate so that it will no longer do any drawing.
            SceneView.duringSceneGui -= this.OnSceneViewGUI;
        }

        /// <summary>
        /// OnGUI
        /// </summary>
        private void OnGUI()
        {
            if (_routeManager == null) this.Close();

            //Set up the box style if null
            if (_menuBoxStyle == null)
            {
                _menuBoxStyle = new GUIStyle(GUI.skin.box);
                _menuBoxStyle.normal.textColor = GUI.skin.label.normal.textColor;
                _menuBoxStyle.fontStyle = FontStyle.Bold;
                _menuBoxStyle.alignment = TextAnchor.UpperLeft;
            }

            GUILayout.BeginVertical(_menuBoxStyle);

            EditorGUILayout.LabelField("Route Index: " + _index, EditorStyles.boldLabel);
            _name = EditorGUILayout.TextField("Route Name", _name);

            _splines = _splines == null ? new List<Spline>() : _splines;

            DropAreaGUI();

            for (int i = 0; i < _splines.Count; i++)
            {
                GUILayout.BeginHorizontal();

                _splines[i] = (Spline)EditorGUILayout.ObjectField(_splines[i], typeof(Spline), true);
                if (GUILayout.Button("▲", GUILayout.MaxWidth(30)))
                {
                    if (i <= 0) return;

                    Spline aux = _splines[i];
                    _splines[i] = _splines[i - 1];
                    _splines[i - 1] = aux;
                }
                if (GUILayout.Button("▼", GUILayout.MaxWidth(30)))
                {
                    if (i >= _splines.Count - 1) return;

                    Spline aux = _splines[i];
                    _splines[i] = _splines[i + 1];
                    _splines[i + 1] = aux;
                }
                if (GUILayout.Button("x", GUILayout.MaxWidth(30)))
                {
                    _splines.RemoveAt(i);
                    MarkSceneAlteration();
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Save"))
            {
                _routeManager.Routes[_index].Name = _name;
                _routeManager.Routes[_index].Splines = _splines;
                MarkSceneAlteration();
                this.Close();
            }

            if (GUILayout.Button("Cancel"))
            {
                this.Close();
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        /// <summary>
        /// Drag & Drop objects on window
        /// </summary>
        public void DropAreaGUI()
        {
            if (_dragDropBoxStyle == null)
            {
                _dragDropBoxStyle = new GUIStyle(GUI.skin.box);
                _dragDropBoxStyle.normal.textColor = GUI.skin.label.normal.textColor;
                _dragDropBoxStyle.fontStyle = FontStyle.Bold;
                _dragDropBoxStyle.alignment = TextAnchor.MiddleCenter;
            }

            Event evt = Event.current;
            Rect drop_area = GUILayoutUtility.GetRect(0.0f, 30.0f, GUILayout.ExpandWidth(true));
            Color originalBackgroundColor = GUI.backgroundColor;
            GUI.backgroundColor = _lightYellow; //Color.yellow;
            GUI.Box(drop_area, "Drag & Drop Splines Here", _dragDropBoxStyle);
            GUI.backgroundColor = originalBackgroundColor;

            switch (evt.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (!drop_area.Contains(evt.mousePosition))
                        return;

                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();

                        foreach (Object dragged_object in DragAndDrop.objectReferences)
                        {
                            GameObject draggedGameObject = (GameObject)dragged_object;

                            // Do On Drag Stuff here
                            if (draggedGameObject == null) continue;

                            Spline spline = draggedGameObject.GetComponent<Spline>();

                            if (spline != null)
                                _splines.Add(spline);
                        }

                        MarkSceneAlteration();
                    }
                    GUIUtility.ExitGUI(); //Avoid GUI related errors
                    break;
            }
        }

        private void OnSceneViewGUI(SceneView sceneView)
        {
            DrawSplinesOnSceneView(sceneView);
        }

        /// <summary>
        /// Draw Splines On Scene View
        /// </summary>
        /// <param name="sceneView"></param>
        private void DrawSplinesOnSceneView(SceneView sceneView)
        {
            if (_splines == null)
                return;

            if (_splines.Count == 0)
                return;

            Transform splineTransform;
            Vector3 startPoint, endPoint, tangent1, tangent2, splineStart = Vector3.zero, splineEnd = Vector3.zero;
            Vector3 startDirection, endDirection;
            Quaternion startRotation, endRotation;

            for (int splineIndex = 0; splineIndex < _splines.Count; splineIndex++)
            {
                if (_splines[splineIndex] == null)
                {
                    Debug.LogWarning("Rote spline cannot be null. Please remove null references from route");
                    continue;
                }

                splineTransform = _splines[splineIndex].GetComponent<Transform>();

                for (int i = 0; i < _splines[splineIndex].ControlPointCount - 1; i += 3)
                {
                    startPoint = splineTransform.TransformPoint(_splines[splineIndex].GetControlPointPosition(i));
                    tangent1 = splineTransform.TransformPoint(_splines[splineIndex].GetControlPointPosition(i + 1));
                    tangent2 = splineTransform.TransformPoint(_splines[splineIndex].GetControlPointPosition(i + 2));
                    endPoint = splineTransform.TransformPoint(_splines[splineIndex].GetControlPointPosition(i + 3));

                    Handles.DrawBezier(startPoint, endPoint, tangent1, tangent2, Color.magenta, null, 5f);

                    splineStart = i == 0 ? startPoint : splineStart;
                    splineEnd = endPoint;

                    if (i != 0)
                    {
                        startDirection = splineTransform.InverseTransformDirection(_splines[splineIndex].GetDirection(startPoint, tangent1, tangent2, endPoint, 0f));
                        startRotation = Quaternion.FromToRotation(Vector3.forward, startDirection);
                        DrawDirectionCone(startPoint, startRotation, Color.white, 3.5f);
                    }
                }

                startDirection = _splines[splineIndex].GetDirection(0);
                endDirection = _splines[splineIndex].GetDirection(_splines[splineIndex].ControlPointCount - 1);

                startRotation = Quaternion.FromToRotation(Vector3.forward, startDirection);
                endRotation = Quaternion.FromToRotation(Vector3.forward, endDirection);

                DrawDirectionCone(splineStart, startRotation, Color.yellow, 5f);
                DrawDirectionCone(splineEnd, endRotation, Color.yellow, 5f);
                sceneView.Repaint();
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
        /// Mark Scene Alteration to save performed operations
        /// </summary>
        private void MarkSceneAlteration()
        {
#if UNITY_EDITOR
            EditorUtility.SetDirty(_routeManager);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
#endif
        }
    }
}
