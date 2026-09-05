using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace GameMain.Editor.Assets.GameScripts.Editor.ProjectTools.Scenes
{
    public class CameraPathVisibilityTool : EditorWindow
    {
        private GameObject targetParent;
        private List<CameraSample> cameraSamples = new List<CameraSample>();
        private bool isRecording = false;
        private Vector2 scrollPos;

        [MenuItem("Tools/VBSOED/场景清理/Camera Path Visibility Tool")]
        public static void ShowWindow()
        {
            GetWindow<CameraPathVisibilityTool>("Visibility Cleaner");
        }

        void OnGUI()
        {
            GUILayout.Label("Camera Path Visibility Cleaner", EditorStyles.boldLabel);

            // 目标父物体选择
            targetParent = (GameObject)EditorGUILayout.ObjectField("Target Parent", targetParent, typeof(GameObject), true);

            // 状态显示
            GUILayout.Label($"Samples: {cameraSamples.Count}", EditorStyles.miniLabel);

            // 按钮区域
            EditorGUILayout.BeginHorizontal();
            {
                if (GUILayout.Button(isRecording ? "Stop Recording" : "Start Recording"))
                {
                    isRecording = !isRecording;
                    if (isRecording) SceneView.duringSceneGui += RecordCameraSample;
                    else SceneView.duringSceneGui -= RecordCameraSample;
                }

                GUI.enabled = cameraSamples.Count > 0 && targetParent != null;
                if (GUILayout.Button("Analyze & Hide"))
                {
                    AnalyzeAndHideObjects();
                }
                GUI.enabled = true;

                if (GUILayout.Button("Clear Samples"))
                {
                    cameraSamples.Clear();
                }

                if (GUILayout.Button("Reset Visibility"))
                {
                    ResetVisibility();
                }
            }
            EditorGUILayout.EndHorizontal();

            // 采样点列表
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.ExpandHeight(true));
            {
                for (int i = 0; i < cameraSamples.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    {
                        EditorGUILayout.LabelField($"Sample {i + 1}", GUILayout.Width(80));
                        EditorGUILayout.Vector3Field("", cameraSamples[i].position, GUILayout.ExpandWidth(true));

                        if (GUILayout.Button("X", GUILayout.Width(20)))
                        {
                            cameraSamples.RemoveAt(i);
                            break;
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
            EditorGUILayout.EndScrollView();
        }

        void RecordCameraSample(SceneView sceneView)
        {
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Space)
            {
                Camera sceneCamera = sceneView.camera;
                cameraSamples.Add(new CameraSample
                {
                    position = sceneCamera.transform.position,
                    rotation = sceneCamera.transform.rotation,
                    fov = sceneCamera.fieldOfView,
                    aspect = sceneCamera.aspect,
                    nearClip = sceneCamera.nearClipPlane,
                    farClip = sceneCamera.farClipPlane
                });

                Event.current.Use();
                Repaint();
            }
        }

        void AnalyzeAndHideObjects()
        {
            if (targetParent == null || cameraSamples.Count == 0) return;

            Undo.RecordObject(targetParent, "Hide Invisible Objects");

            // 获取所有带渲染器的子物体
            List<GameObject> allChildren = new List<GameObject>();
            GetRenderableChildren(targetParent.transform, allChildren);

            int hiddenCount = 0;

            foreach (GameObject child in allChildren)
            {
                if (!child.activeInHierarchy) continue;

                Renderer renderer = child.GetComponent<Renderer>();
                if (renderer == null) continue;

                bool everVisible = false;

                // 检查在所有采样点中是否可见
                foreach (CameraSample sample in cameraSamples)
                {
                    if (IsVisible(renderer, sample))
                    {
                        everVisible = true;
                        break;
                    }
                }

                // 如果从未可见，则隐藏
                if (!everVisible)
                {
                    child.SetActive(false);
                    hiddenCount++;
                    Debug.Log($"Hidden: {child.name}", child);
                }
            }

            Debug.Log($"Hidden {hiddenCount} objects. Total analyzed: {allChildren.Count}");
        }

        bool IsVisible(Renderer renderer, CameraSample sample)
        {
            // 创建虚拟摄像机
            Camera cam = new GameObject("TempCamera").AddComponent<Camera>();
            cam.transform.position = sample.position;
            cam.transform.rotation = sample.rotation;
            cam.fieldOfView = sample.fov;
            cam.aspect = sample.aspect;
            cam.nearClipPlane = sample.nearClip;
            cam.farClipPlane = sample.farClip;

            // 计算视锥体平面
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(cam);
            bool visible = GeometryUtility.TestPlanesAABB(planes, renderer.bounds);

            DestroyImmediate(cam.gameObject);
            return visible;
        }

        void GetRenderableChildren(Transform parent, List<GameObject> results)
        {
            foreach (Transform child in parent)
            {
                if (child.GetComponent<Renderer>() != null)
                {
                    results.Add(child.gameObject);
                }
                GetRenderableChildren(child, results);
            }
        }

        void ResetVisibility()
        {
            if (targetParent == null) return;

            Undo.RecordObject(targetParent, "Reset Visibility");

            foreach (Transform child in targetParent.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.SetActive(true);
            }

            Debug.Log("All children visibility reset");
        }

        private struct CameraSample
        {
            public Vector3 position;
            public Quaternion rotation;
            public float fov;
            public float aspect;
            public float nearClip;
            public float farClip;
        }
    }
}