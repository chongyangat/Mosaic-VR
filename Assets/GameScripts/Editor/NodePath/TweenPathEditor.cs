using UnityEngine;
using UnityEditor;
using System.Text;
using System.Reflection;
using UnityEditorInternal;
using System.Globalization;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using static Codice.Client.Commands.WkTree.WorkspaceTreeNode;

namespace VBSOED
{
    internal enum RepaintMode
    {
        None,
        Scene,
        Inspector,
        SceneAndInspector
    }

    public class WpHandle
    {
        internal int controlId;
        internal int wpIndex;

        public WpHandle(int wpIndex)
        {
            this.wpIndex = wpIndex;
        }

        public override string ToString()
        {
            return $"controlId:{controlId} wpIndex:{wpIndex}";
        }
    }
    [CustomEditor(typeof(TweenPath))]
    public class TweenPathEditor : UnityEditor.Editor
    {
        private readonly Color _wpColor = Color.white;
        private readonly Color _arrowsColor = new Color(1f, 1f, 1f, 0.85f);
        private readonly Color _wpColorEnd = Color.red;

        static Transform pointRoot;
        static SerializedProperty trafficLight;

        public override void OnInspectorGUI()
        {
            EditorGUILayout.BeginHorizontal();
            TrafficLightObject tlobj = EditorGUILayout.ObjectField("红绿灯", tweenPath.trafficLight, typeof(TrafficLightObject), true) as TrafficLightObject;
            if (tlobj != tweenPath.trafficLight)
            {
                tweenPath.trafficLight = tlobj;
                serializedObject.ApplyModifiedProperties();
            }
            //bool r = EditorGUILayout.PropertyField(trafficLight);
            //if (serializedObject.ApplyModifiedProperties())
            //{
            //    serializedObject.Update();
            //}
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            pointRoot = (Transform)EditorGUILayout.ObjectField("根点", pointRoot, typeof(Transform), true);
            bool isSet = GUILayout.Button("根据根点重置");
            EditorGUILayout.EndHorizontal();
            if (isSet && pointRoot != null)
            {
                TweenPath tp = target as TweenPath;
                tp.path.points.Clear();
                int cnt = pointRoot.childCount;
                for (int i = 0; i < cnt; ++i)
                {
                    tp.path.points.Add(pointRoot.GetChild(i).position);
                }
            }

            SerializedProperty path_sp = serializedObject.FindProperty("path");
            Path.InspectorGUI(path_sp);

            EditorGUILayout.PropertyField(livePreview);
            EditorGUILayout.PropertyField(showIndexes);
            EditorGUILayout.PropertyField(showWpLength);
            EditorGUILayout.PropertyField(onDrawGizmos);

            OnWapPointGUI(tweenPath.path);
            EditorGUILayout.LabelField("总长度:" + tweenPath.path.Length);
            if (serializedObject.ApplyModifiedPropertiesWithoutUndo())
                tweenPath.SetDirty();
            opType = (OpType)EditorGUILayout.EnumPopup("操作类型", opType);
        }

        private TweenPath tweenPath;
        private TweenPath _src;

        SerializedProperty path;
        SerializedProperty livePreview;
        SerializedProperty showIndexes;
        SerializedProperty showWpLength;
        SerializedProperty onDrawGizmos;
        
        public void OnEnable()
        {
            _src = tweenPath = target as TweenPath;
            path = serializedObject.FindProperty("path");
            livePreview = serializedObject.FindProperty("livePreview");
            showWpLength = serializedObject.FindProperty("showWpLength");
            showIndexes = serializedObject.FindProperty("showIndexes");
            onDrawGizmos = serializedObject.FindProperty("onDrawGizmos");
            trafficLight = serializedObject.FindProperty("trafficLight");
            _src.SetDirty();
        }

        private void CopyWaypointsToClipboard()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("Vector3[] waypoints = new[] { ");
            var points = tweenPath.wps;
            int cnt = points.Count;
            for (int i = 0; i < cnt; i++)
            {
                Vector3 vector = points[i];
                if (i > 0)
                {
                    builder.Append(", ");
                }
                string introduced3 = vector.x.ToString(CultureInfo.InvariantCulture);
                string introduced4 = vector.y.ToString(CultureInfo.InvariantCulture);
                builder.Append($"new Vector3({introduced3}f,{introduced4}f,{vector.z.ToString(CultureInfo.InvariantCulture)}f)");
            }
            builder.Append(" };");
            EditorGUIUtility.systemCopyBuffer = builder.ToString();
        }
        
        private static readonly Regex _CopyWpsFromClipboardRegex = new Regex(@"(\(|\,)([-+]?[0-9]*\.?[0-9]+)");
        private void PasteWaypointsFromClipboard()
        {
            string systemCopyBuffer = EditorGUIUtility.systemCopyBuffer;
            MatchCollection matchs = _CopyWpsFromClipboardRegex.Matches(systemCopyBuffer);
            if (matchs.Count != 0)
            {
                List<Vector3> list = new List<Vector3>();
                for (int i = 0; i < matchs.Count; i += 3)
                    list.Add(new Vector3(float.Parse(matchs[i].Groups[2].Value, CultureInfo.InvariantCulture), float.Parse(matchs[i + 1].Groups[2].Value, CultureInfo.InvariantCulture), float.Parse(matchs[i + 2].Groups[2].Value, CultureInfo.InvariantCulture)));

                tweenPath.path.points.Clear();
                tweenPath.path.points.AddRange(list);
                RefreshPath(RepaintMode.SceneAndInspector, false);
            }
        }

        void OnWapPointGUI(Path path)
        {
            if (path.points == null)
            {
                path.points = new List<Vector3>();
                _wpsList = null;
            }

            if (_wpsList == null)
            {
                _wpsList = new ReorderableList(path.points, typeof(Vector3), true, false, true, true);
                //_wpsList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "路径点");
                _wpsList.onReorderCallback = list => this.RefreshPath(RepaintMode.Scene, true);
                _wpsList.drawElementCallback = delegate (Rect rect, int index, bool isActive, bool isFocused)
                {
                    Rect position = new Rect(rect.xMin, rect.yMin, 23f, rect.height);
                    Rect rect3 = new Rect(position.xMax, position.yMin, rect.width - 23f, position.height);
                    GUI.Label(position, index.ToString());
                    _src.wps[index] = EditorGUI.Vector3Field(rect3, "", _src.wps[index]);
                };
            }
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            tweenPath.wpsDropdown = EditorGUILayout.Foldout(tweenPath.wpsDropdown, $"{(tweenPath.wpsDropdown ? "\u25BC" : "\u25B6")} 路径点({path.points.Count})", true, EditorStyles.label);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("复制坐标点"))
            {
                CopyWaypointsToClipboard();
            }
            else if (GUILayout.Button("粘贴坐标点"))
            {
                PasteWaypointsFromClipboard();
            }
            else if (GUILayout.Button("保存为世界坐标"))
            {
                tweenPath.SaveToWorldPosition();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            if (tweenPath.wpsDropdown)
            {
                ++EditorGUI.indentLevel;
                GUI.changed = false;
                _wpsList.DoLayoutList();
                if (GUI.changed)
                    tweenPath.SetDirty();
                --EditorGUI.indentLevel;
            }
        }

        private ReorderableList _wpsList;

        private Camera _fooSceneCam;
        private Camera _sceneCam
        {
            get
            {
                if (_fooSceneCam == null)
                {
                    SceneView currentDrawingSceneView = SceneView.currentDrawingSceneView;
                    if (currentDrawingSceneView == null)
                    {
                        return null;
                    }
                    _fooSceneCam = currentDrawingSceneView.camera;
                }
                return _fooSceneCam;
            }
        }
        private Transform _fooSceneCamTrans;
        private Transform _sceneCamTrans
        {
            get
            {
                if (_fooSceneCamTrans == null)
                {
                    if (_sceneCam == null)
                    {
                        return null;
                    }
                    _fooSceneCamTrans = _sceneCam.transform;
                }
                return _fooSceneCamTrans;
            }
        }

        private bool _sceneCamStored;
        private Vector3 _lastSceneViewCamPosition;
        private Quaternion _lastSceneViewCamRotation;

        private void StoreSceneCamData()
        {
            if (this._sceneCam == null)
            {
                this._sceneCamStored = false;
            }
            else if (!this._sceneCamStored && (this._sceneCam != null))
            {
                this._sceneCamStored = true;
                this._lastSceneViewCamPosition = this._sceneCamTrans.position;
                this._lastSceneViewCamRotation = this._sceneCamTrans.rotation;
            }
        }

        private readonly List<WpHandle> _wpsByDepth = new List<WpHandle>();

        private void FillWpIndexByDepth()
        {
            if (!_sceneCamStored)
                return;

            int count = tweenPath.pointCount;
            if (count == 0)
                return;

            _wpsByDepth.Clear();
            for (int i = 0; i < count; i++)
                _wpsByDepth.Add(new WpHandle(i));

            var points = tweenPath.path.points;
            Vector3 _sceneCamTrans_position = _sceneCamTrans.position;
            _wpsByDepth.Sort((x, y) =>
            {
                float num = Vector3.Distance(_sceneCamTrans_position, points[x.wpIndex]);
                float num2 = Vector3.Distance(_sceneCamTrans_position, points[y.wpIndex]);
                if (num > num2) return -1;
                if (num < num2) return 1;
                return 0;
            });
        }

        static float _editorUiScaling = -1;
        public static float GetEditorUIScaling()
        {
            if (_editorUiScaling < 0)
            {
                // EditorPrefs method: I prefer Reflection method because I'm not sure on OSX, where you can't set UI scaling manually, this is used
                //                _editorUiScaling = EditorPrefs.GetInt("CustomEditorUIScale") * 0.01f;
                PropertyInfo p = typeof(GUIUtility).GetProperty("pixelsPerPoint", BindingFlags.Static | BindingFlags.NonPublic);
                if (p != null) _editorUiScaling = (float)p.GetValue(null, null);
                else _editorUiScaling = 1;
            }
            return _editorUiScaling;
        }

        private int _minHandleControlId;
        private int _maxHandleControlId;
        private void FindSelectedWaypointIndex()
        {
            _lastSelectedWpIndex = _selectedWpIndex;
            _selectedWpIndex = -1;
            int count = tweenPath.pointCount;
            if (count != 0)
            {
                int nearestControl = HandleUtility.nearestControl;
                if (((nearestControl != 0) && (nearestControl >= _minHandleControlId)) && (nearestControl <= _maxHandleControlId))
                {
                    int num3 = -1;
                    for (int i = 0; i < count; i++)
                    {
                        int controlId = _wpsByDepth[i].controlId;
                        switch (controlId)
                        {
                            case -1:
                            case 0:
                                break;

                            default:
                                {
                                    int wpIndex = _wpsByDepth[i].wpIndex;
                                    if (controlId > nearestControl)
                                    {
                                        _selectedWpIndex = _wpsByDepth[(num3 == -1) ? i : num3].wpIndex;
                                        _lastCreatedWpIndex = -1;
                                        return;
                                    }
                                    if (controlId == nearestControl)
                                    {
                                        _selectedWpIndex = wpIndex;
                                        _lastCreatedWpIndex = -1;
                                        return;
                                    }
                                    num3 = i;
                                    break;
                                }
                        }
                    }

                    if (_selectedWpIndex == -1)
                    {
                        _selectedWpIndex = _wpsByDepth[num3].wpIndex;
                        _lastCreatedWpIndex = -1;
                    }
                }
            }
        }

        internal void RefreshPath(RepaintMode repaintMode, bool refreshWpIndexByDepth)
        {
            tweenPath.SetDirty();
            if (tweenPath.pointCount >= 1)
            {
                DORepaint(repaintMode, refreshWpIndexByDepth);
            }
        }

        private void DORepaint(RepaintMode repaintMode, bool refreshWpIndexByDepth)
        {
            switch (repaintMode)
            {
                case RepaintMode.Scene:
                    SceneView.RepaintAll();
                    break;

                case RepaintMode.Inspector:
                    EditorUtility.SetDirty(tweenPath);
                    break;

                case RepaintMode.SceneAndInspector:
                    EditorUtility.SetDirty(tweenPath);
                    SceneView.RepaintAll();
                    break;
            }

            if (refreshWpIndexByDepth)
            {
                FillWpIndexByDepth();
            }
        }

        private int _selectedWpIndex = -1;
        private int _lastSelectedWpIndex = -1;
        private int _lastCreatedWpIndex = -1;
        private bool _isDragging;

        private void ResetIndexes()
        {
            _selectedWpIndex = _lastSelectedWpIndex = _lastCreatedWpIndex = -1;
        }

        private bool CheckTargetMoveOrRotate()
        {
            bool flag = false;
            if (this.tweenPath.lastSrcPosition != this.tweenPath.transform.position)
            {
                if (this.tweenPath.relative)
                {
                    Vector3 vector = this.tweenPath.transform.position - this.tweenPath.lastSrcPosition;
                    int count = this.tweenPath.pointCount;
                    for (int i = 0; i < count; i++)
                    {
                        this.tweenPath.path.points[i] += vector;
                    }
                }
                this.tweenPath.lastSrcPosition = this.tweenPath.transform.position;
                flag = true;
            }
            if (!(this.tweenPath.lastSrcRotation != this.tweenPath.transform.rotation))
            {
                return flag;
            }
            if (this.tweenPath.relative)
            {
                Quaternion rotation = this.tweenPath.transform.rotation * Quaternion.Inverse(this.tweenPath.lastSrcRotation);
                int count = this.tweenPath.pointCount;
                for (int i = 0; i < count; i++)
                {
                    this.tweenPath.path.points[i] = RotateAroundPivot(this.tweenPath.path.points[i], this.tweenPath.transform.position, rotation);
                }
            }

            this.tweenPath.lastSrcRotation = this.tweenPath.transform.rotation;
            return true;
        }

        internal static Vector3 RotateAroundPivot(Vector3 point, Vector3 pivot, Quaternion rotation)
        {
            return rotation * (point - pivot) + pivot;
        }

        private void DrawArrowFor(Vector3 start, Vector3 dst, int wpIndex, float handleSize)
        {
            Handles.color = _arrowsColor;
            Vector3 dir = dst - start;
            if (dir.magnitude >= (handleSize * 1.75f))
            {
                Handles.ConeHandleCap(wpIndex, start + Vector3.ClampMagnitude(dir, handleSize), Quaternion.LookRotation(dir), handleSize * 0.65f, UnityEngine.EventType.Repaint);
            }
            Handles.color = Handles.color;
        }

        private bool _changed;
        private bool _reselectAfterDrag;

        static GUIStyle handlelabelStyle_;
        static GUIStyle handlelabelStyle
        {
            get
            {
                if (handlelabelStyle_ == null)
                {
                    handlelabelStyle_ = new GUIStyle(UnityEngine.GUI.skin.label)
                    {
                        normal = { textColor = Color.white },
                        alignment = TextAnchor.MiddleLeft
                    };
                }

                return handlelabelStyle_;
            }
        }

        static GUIStyle handleSelectedLabelStyle_;
        static GUIStyle handleSelectedLabelStyle
        {
            get
            {
                if (handleSelectedLabelStyle_ == null)
                {
                    handleSelectedLabelStyle_ = new GUIStyle(handlelabelStyle)
                    {
                        normal = { textColor = Color.yellow },
                        fontStyle = FontStyle.Bold
                    };
                }

                return handleSelectedLabelStyle_;
            }
        }

        private bool Changed()
        {
            if (!GUI.changed)
            {
                if (_lastSelectedWpIndex != _selectedWpIndex)
                {
                    _lastSelectedWpIndex = _selectedWpIndex;
                    return true;
                }
                if (CheckTargetMoveOrRotate())
                {
                    return true;
                }
                if (!(_sceneCamTrans.position != _lastSceneViewCamPosition) && !(_sceneCamTrans.rotation != _lastSceneViewCamRotation))
                {
                    return false;
                }
                
                _lastSceneViewCamPosition = _sceneCamTrans.position;
                _lastSceneViewCamRotation = _sceneCamTrans.rotation;
            }
            return true;
        }


        // 操作类型
        enum OpType
        {
            无, // 无

            智能操作, // 移动类型

            加点, // 加点
            移点, // 移除点
       }

        static OpType opType = OpType.智能操作;


        void AddPoint()
        {
            // 添加新的点
            Ray worldRay = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero + Vector3.up * 0.01f); // 以世界原点为基准创建水平地面

            if (groundPlane.Raycast(worldRay, out float enter))
            {
                Vector3 hitPoint = worldRay.GetPoint(enter); // 计算交点
                Debug.Log("Scene 视图点击位置：" + hitPoint);
                hitPoint.Set(hitPoint.x, hitPoint.y, hitPoint.z);
                _src.wps.Add(_src.transform.worldToLocalMatrix.MultiplyPoint(hitPoint));
            }
            var origin = worldRay.origin;
        }

        static void Check(ref Vector3 p)
        {
            if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z))
            {
                p = Vector3.zero;
            }
        }

        void OnMouseDrag()
        {
            var current = Event.current;
            if (current.type == EventType.MouseDrag)
            {
                _isDragging = true;
                if (_src.livePreview)
                {
                    if (CheckTargetMoveOrRotate() ||  _selectedWpIndex != -1)
                    {
                        RefreshPath(RepaintMode.Scene, false);
                    }
                }
            }
            else if (current.rawType == EventType.MouseUp)
            {
                if (_isDragging)
                {
                    if (this._selectedWpIndex != -1)
                    {
                        _reselectAfterDrag = true;
                    }
                    _isDragging = false;
                    if (_selectedWpIndex != -1 || CheckTargetMoveOrRotate())
                    {
                        EditorUtility.SetDirty(target);
                    	RefreshPath(RepaintMode.Scene, true);
                    }
                }
            }
            else if (current.rawType == EventType.MouseDown)
            {
                FindSelectedWaypointIndex();
                if (current.alt)
                {
                    switch (opType)
                    {
                        case OpType.无:
                            break;
                        case OpType.加点:
                            {
                                Undo.RecordObject(_src, "加点");
                                // 添加新的点
                                AddPoint();
                                EditorUtility.SetDirty(_src);
								RefreshPath(RepaintMode.Scene, true);
                            }
                            return;
                        case OpType.移点:
                            {
                                if (_selectedWpIndex != -1)
                                {
                                    Undo.RecordObject(_src, "移除点");
                                    _src.wps.RemoveAt(_selectedWpIndex);
                                    EditorUtility.SetDirty(_src);
									RefreshPath(RepaintMode.Scene, true);
                                }
                            }
                            return;
                        case OpType.智能操作:
                            {
                                if (_selectedWpIndex != -1)
                                {
                                    Undo.RecordObject(_src, "移除点");
                                    _src.wps.RemoveAt(_selectedWpIndex);
                                    EditorUtility.SetDirty(_src);
									RefreshPath(RepaintMode.Scene, true);
                                    return;
                                }
                                else
                                {
                                    // 加点
                                    Undo.RecordObject(_src, "加点");
                                    AddPoint();
                                    EditorUtility.SetDirty(_src);
                    				RefreshPath(RepaintMode.Scene, true);
                                    return;
                                }
                            }
                            return;
                    }
                }
            }
            else if (CheckTargetMoveOrRotate())
            {
                RefreshPath(RepaintMode.Scene, false);
            }
            if (_changed && !_isDragging)
            {
                FillWpIndexByDepth();
                _changed = false;
            }
            int count = _src.wps.Count;
            var wps = _src.toWorldPoint(false);
            for (int i = 0; i < count; i++)
            {
                WpHandle handle = _wpsByDepth[i];
                int wpIndex = handle.wpIndex;
                bool isSelected = wpIndex == _selectedWpIndex;
                Vector3 position = wps[wpIndex];
                float handleSize = (_src.handlesDrawMode == HandlesDrawMode.Orthographic) ? (HandleUtility.GetHandleSize(position) * 0.2f) : _src.perspectiveHandleSize;
                if (isSelected)
                {
                    Handles.color = Color.yellow;
                }
                else if ((handle.wpIndex == (count - 1)) && !_src.isClosedPath)
                {
                    Handles.color = _wpColorEnd;
                }
                else
                {
                    Handles.color = _wpColor;
                }

                if (wpIndex == count - 1 && !_src.isClosedPath)
                {
                }
                else
                {
                    if (wpIndex == count - 1)
                        DrawArrowFor(position, wps[0], wpIndex, handleSize);
                    else
                        DrawArrowFor(position, wps[wpIndex + 1], wpIndex, handleSize);
                }

                int controlID = GUIUtility.GetControlID(FocusType.Passive);
                if (i == 0)
                {
                    _minHandleControlId = controlID;
                }
                float posy = position.y;
                if (_src.handlesType == HandlesType.Free)
                {
#if UNITY_2022_1_OR_NEWER
                    position = Handles.FreeMoveHandle(position, handleSize, Vector3.one, new Handles.CapFunction(Handles.SphereHandleCap));
#else
                    position = Handles.FreeMoveHandle(position, Quaternion.identity, handleSize, Vector3.one, new Handles.CapFunction(Handles.SphereHandleCap));
#endif
                }
                else
                {
                    position = Handles.PositionHandle(position, Quaternion.identity);
                }
                position.y = posy;
                _src.wps[handle.wpIndex] = _src.worldPointToLocal(position);
                int num6 = GUIUtility.GetControlID(FocusType.Passive);
                handle.controlId = (i == 0) ? (num6 - 1) : (controlID + 1);
                _maxHandleControlId = num6;

                Vector3 vector6 = _sceneCamTrans.InverseTransformPoint(position) + new Vector3(handleSize * 0.75f, 0.1f, 0f);
                vector6 = _sceneCamTrans.TransformPoint(vector6);
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                if (_src.showIndexes)
                    sb.Append($"{handle.wpIndex}");
                if (_src.showWpLength)
                {
                    int index = handle.wpIndex;
                    if (_src.isClosedPath && index == 0)
                        index = count;

                    sb.Append($"({_src.path.wpLengths[index].ToString("N2")})");
                }

                if (sb.Length != 0)
                    Handles.Label(vector6, sb.ToString(), isSelected ? handleSelectedLabelStyle : handlelabelStyle);
            }
            Handles.color = _src.pathColor;
            if (_src.pathType == PathType.Linear)
            {
                Handles.DrawPolyLine(_src.toWorldPoint(_src.path.isClosed).ToArray());
            }
            else
            {
                var points = _src.path.nonLinearDrawWps;
                if (points == null)
                {
                    _src.SetDirty();
                    points = _src.path.nonLinearDrawWps;
                }

                int cnt = points.Length;
                Vector3[] worlds = new Vector3[cnt];
                for (int i = 0; i < cnt; ++i)
                    worlds[i] = _src.localPointToWorld(points[i]);
                Handles.DrawPolyLine(worlds);
            }
            if (_reselectAfterDrag && (current.type == UnityEngine.EventType.Repaint))
            {
                _reselectAfterDrag = false;
            }
            if (!_changed)
            {
                _changed = this.Changed();
            }
            if (_changed)
            {
                EditorUtility.SetDirty(this._src);
            }
        }

        private void OnSceneGUI()
        {
            //if (Application.isPlaying)
            //    return;

            StoreSceneCamData();
            if (!_src.gameObject.activeInHierarchy || !_sceneCamStored)
                return;

            if (_src.wps == null)
                return;

            if (_wpsByDepth.Count != _src.wps.Count)
                FillWpIndexByDepth();

            OnMouseDrag();
        }
    }
}