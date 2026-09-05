using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MetaQuestProEyeGazeUDP;

namespace MetaQuestProEyeGazePointDraw
{
    public class PointDraw : MonoBehaviour
    {
        //绘制点预制体
        [SerializeField] private GameObject pointPrefab;
        //线
        [SerializeField] private LineRenderer lineRenderer;
        //UI Canvas（用于显示数字编号）
        [SerializeField] private Canvas uiCanvas;
        //UI Group（用于整理数字编号对象）
        [SerializeField] private Transform uiGroup;

        [Header("最大绘制点数量")]
        public int maxCount = 5;
        [Header("绘制间隔")]
        public float drawInterval = 0.5f;
        [Header("数字垂直偏移量（像素）")]
        public float numberOffsetY = 30f;
        private float _drawInterval = 0;
        private bool _drawCooling = false;

        //已生成的点
        private List<Transform> points = new List<Transform>();
        //回收的点
        private Queue<GameObject> readyPointsQueue = new Queue<GameObject>();
        //点编号计数器
        private int pointCounter = 0;

        /// <summary>
        /// 重置绘制点计数
        /// </summary>
        public void ResetPointsCount()
        {
            pointCounter = 0;
            pointNumberMap.Clear();
        }

        //点编号映射表（GameObject -> 编号）
        private Dictionary<GameObject, int> pointNumberMap = new Dictionary<GameObject, int>();

        //点编号对象映射表（点GameObject -> 编号TextMeshProUGUI对象）
        private Dictionary<GameObject, TextMeshProUGUI> pointTextMeshMap = new Dictionary<GameObject, TextMeshProUGUI>();

        //回收的编号对象队列
        private Queue<TextMeshProUGUI> readyTextMeshQueue = new Queue<TextMeshProUGUI>();

        //所有创建的编号对象集合（用于强制清理）
        private HashSet<TextMeshProUGUI> allTextMeshObjects = new HashSet<TextMeshProUGUI>();

        private void Awake()
        {
            Init();
        }

        /// <summary>
        /// 初始化
        /// </summary>
        private void Init()
        {
            //初始化Canvas
            if (uiCanvas == null)
            {
                uiCanvas = FindObjectOfType<Canvas>();
                if (uiCanvas == null)
                {
                    GameObject canvasObj = new GameObject("PointDrawCanvas");
                    uiCanvas = canvasObj.AddComponent<Canvas>();
                    uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvasObj.AddComponent<CanvasScaler>();
                    canvasObj.AddComponent<GraphicRaycaster>();
                }
            }

            //初始化UIGroup
            if (uiGroup == null)
            {
                Transform existingGroup = uiCanvas.transform.Find("PointNumberUIGroup");
                if (existingGroup != null)
                {
                    uiGroup = existingGroup;
                }
                else
                {
                    GameObject groupObj = new GameObject("PointNumberUIGroup");
                    groupObj.transform.SetParent(uiCanvas.transform, false);
                    RectTransform groupRect = groupObj.AddComponent<RectTransform>();
                    groupRect.anchorMin = Vector2.zero;
                    groupRect.anchorMax = Vector2.one;
                    groupRect.sizeDelta = Vector2.zero;
                    uiGroup = groupObj.transform;
                }
            }

            //清空对象池，初始化数据
            foreach (var item in readyPointsQueue)
            {
                Destroy(item);
            }

            foreach (var item in points)
            {
                if (item)
                {
                    Destroy(item.gameObject);
                }
            }
            points.Clear();

            //清空编号TextMeshProUGUI对象
            foreach (var pair in pointTextMeshMap)
            {
                if (pair.Value != null)
                {
                    Destroy(pair.Value.gameObject);
                }
            }
            pointTextMeshMap.Clear();

            //清空回收的编号对象队列
            foreach (var textMesh in readyTextMeshQueue)
            {
                if (textMesh != null)
                {
                    Destroy(textMesh.gameObject);
                }
            }
            readyTextMeshQueue.Clear();

            //清空所有编号对象集合
            foreach (var textMesh in allTextMeshObjects)
            {
                if (textMesh != null)
                {
                    Destroy(textMesh.gameObject);
                }
            }
            allTextMeshObjects.Clear();

            //重置编号计数器
            pointCounter = 0;

            //清空编号映射表
            pointNumberMap.Clear();

            for (int i = 0; i < maxCount; i++)
            {
                GameObject point = Instantiate(pointPrefab);
                readyPointsQueue.Enqueue(point);
                point.SetActive(false);
            }
        }

        private void Update()
        {
            DrawInterval();
            UpdateAllPointNumbers();
        }

        /// <summary>
        /// 更新所有点编号的位置和旋转
        /// </summary>
        private void UpdateAllPointNumbers()
        {
            // 收集需要回收的映射
            List<GameObject> toRecycle = new List<GameObject>();

            foreach (var pair in pointTextMeshMap)
            {
                GameObject point = pair.Key;
                TextMeshProUGUI textMesh = pair.Value;

                if (point == null || textMesh == null)
                {
                    toRecycle.Add(point);
                    continue;
                }

                // 检查该点是否还在使用中（在points列表中）
                bool isPointInUse = false;
                foreach (var p in points)
                {
                    if (p != null && p.gameObject == point)
                    {
                        isPointInUse = true;
                        break;
                    }
                }

                if (!isPointInUse)
                {
                    // 如果点不在使用中，立即隐藏编号对象并回收
                    textMesh.gameObject.SetActive(false);
                    readyTextMeshQueue.Enqueue(textMesh);
                    toRecycle.Add(point);
                    continue;
                }

                // 将点的世界坐标转换为屏幕坐标，并添加垂直偏移
                if (Camera.main != null)
                {
                    Vector3 screenPosition = Camera.main.WorldToScreenPoint(point.transform.position);
                    screenPosition.y += numberOffsetY;
                    
                    // 对于 ScreenSpaceOverlay Canvas，直接设置 RectTransform 的 position
                    RectTransform rectTransform = textMesh.GetComponent<RectTransform>();
                    
                    // 将屏幕坐标转换为相对于 Canvas 的坐标
                    Vector2 canvasSize = uiCanvas.GetComponent<RectTransform>().sizeDelta;
                    Vector2 canvasScale = new Vector2(Screen.width / canvasSize.x, Screen.height / canvasSize.y);
                    
                    // 计算相对于 Canvas 中心的偏移
                    Vector2 relativePosition = new Vector2(
                        (screenPosition.x - Screen.width / 2) / canvasScale.x,
                        (screenPosition.y - Screen.height / 2) / canvasScale.y
                    );
                    
                    rectTransform.anchoredPosition = relativePosition;
                }
            }

            // 清理已回收的映射
            foreach (GameObject point in toRecycle)
            {
                pointTextMeshMap.Remove(point);
            }

            // 额外检查：隐藏所有回收队列中的编号对象
            foreach (var textMesh in readyTextMeshQueue)
            {
                if (textMesh != null && textMesh.gameObject.activeSelf)
                {
                    textMesh.gameObject.SetActive(false);
                }
            }

            // 强制清理：隐藏所有不在 pointTextMeshMap 中的编号对象
            HashSet<TextMeshProUGUI> activeTextMeshes = new HashSet<TextMeshProUGUI>(pointTextMeshMap.Values);
            foreach (var textMesh in allTextMeshObjects)
            {
                if (textMesh != null && textMesh.gameObject != null)
                {
                    if (!activeTextMeshes.Contains(textMesh))
                    {
                        if (textMesh.gameObject.activeSelf)
                        {
                            textMesh.gameObject.SetActive(false);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 绘制凝视点
        /// </summary>
        /// <param name="gazePoint"></param>
        public void Draw(GazeNetData gazePoint, Vector3? leftEyeGazePos, Vector3? rightEyeGazePos, Vector3? doubleEyesGazePos)
        {
            if (_drawCooling)
                return;

            //清理已被销毁的点
            for (int i = points.Count - 1; i >= 0; i--)
            {
                if (points[i] == null || points[i].gameObject == null)
                {
                    points.RemoveAt(i);
                }
            }

            GameObject point;
            //数量检查
            if (points.Count >= maxCount)
            {
                //超出绘制点数量限制，把最后一个拿来当第一个
                //检查第一个点是否还存在
                if (points[0] != null && points[0].gameObject != null)
                {
                    // 清理对应的编号对象
                    GameObject oldPoint = points[0].gameObject;
                    
                    if (pointTextMeshMap.ContainsKey(oldPoint))
                    {
                        if (pointTextMeshMap[oldPoint] != null)
                        {
                            // 回收编号对象而不是销毁，并立即隐藏
                            pointTextMeshMap[oldPoint].gameObject.SetActive(false);
                            readyTextMeshQueue.Enqueue(pointTextMeshMap[oldPoint]);
                        }
                        pointTextMeshMap.Remove(oldPoint);
                    }
                    // 清理编号映射
                    if (pointNumberMap.ContainsKey(oldPoint))
                    {
                        pointNumberMap.Remove(oldPoint);
                    }

                    readyPointsQueue.Enqueue(points[0].gameObject);
                }
                points.RemoveAt(0);
                point = readyPointsQueue.Dequeue();
                
                // 立即隐藏回收队列中的所有编号对象
                foreach (var textMesh in readyTextMeshQueue)
                {
                    if (textMesh != null && textMesh.gameObject.activeSelf)
                    {
                        textMesh.gameObject.SetActive(false);
                    }
                }
            }
            else
            {
                //如果队列为空，创建新点
                if (readyPointsQueue.Count == 0)
                {
                    point = Instantiate(pointPrefab);
                }
                else
                {
                    point = readyPointsQueue.Dequeue();
                }
            }

            if (point == null || doubleEyesGazePos == null)
            {
                return;
            }

            //更新点的位置
            point.SetActive(true);
            point.transform.position = doubleEyesGazePos.Value;
            points.Add(point.transform);

            //分配新编号（点被复用时也更新为最新编号）
            pointNumberMap[point] = pointCounter;
            pointCounter++;

            //更新编号显示
            UpdatePointNumber(point, pointNumberMap[point]);

            //更新线段
            lineRenderer.positionCount = points.Count;
            Vector3[] lineData = new Vector3[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                //检查点是否还存在
                if (points[i] != null)
                {
                    lineData[i] = points[i].position;
                }
                else
                {
                    //如果点不存在，使用上一个点的位置或零向量
                    lineData[i] = i > 0 ? lineData[i - 1] : Vector3.zero;
                }
            }
            lineRenderer.SetPositions(lineData);

            //绘制时间间隔
            _drawInterval = drawInterval;
            _drawCooling = true;
        }

        /// <summary>
        /// 更新点的编号显示
        /// </summary>
        /// <param name="point">点对象</param>
        /// <param name="number">编号</param>
        private void UpdatePointNumber(GameObject point, int number)
        {
            TextMeshProUGUI textMesh;

            // 检查是否已创建该点的编号对象
            if (!pointTextMeshMap.ContainsKey(point) || pointTextMeshMap[point] == null)
            {
                // 从回收队列中获取编号对象
                if (readyTextMeshQueue.Count > 0)
                {
                    textMesh = readyTextMeshQueue.Dequeue();
                    textMesh.gameObject.SetActive(true);
                }
                else
                {
                    // 创建UI编号对象，作为UIGroup的子对象
                    GameObject textObj = new GameObject("PointNumber_" + point.GetInstanceID());
                    textObj.transform.SetParent(uiGroup, false);

                    textMesh = textObj.AddComponent<TextMeshProUGUI>();

                    textMesh.fontSize = 24f;
                    textMesh.alignment = TextAlignmentOptions.Center;
                    textMesh.fontStyle = FontStyles.Bold;
                    textMesh.color = Color.white;
                    textMesh.faceColor = Color.white;
                    textMesh.outlineColor = new Color(0, 0.3f, 0.6f, 1f);
                    textMesh.outlineWidth = 0.5f;

                    // 设置RectTransform属性
                    RectTransform rectTransform = textObj.GetComponent<RectTransform>();
                    rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                    rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    rectTransform.sizeDelta = new Vector2(100, 50);
                    
                    // 添加到所有编号对象集合
                    allTextMeshObjects.Add(textMesh);
                }

                pointTextMeshMap[point] = textMesh;
            }
            else
            {
                textMesh = pointTextMeshMap[point];
                textMesh.gameObject.SetActive(true);
            }

            textMesh.text = number.ToString();

            // 设置屏幕坐标位置，并添加垂直偏移
            if (Camera.main != null)
            {
                Vector3 screenPosition = Camera.main.WorldToScreenPoint(point.transform.position);
                screenPosition.y += numberOffsetY;
                
                // 对于 ScreenSpaceOverlay Canvas，直接设置 RectTransform 的 position
                RectTransform rectTransform = textMesh.GetComponent<RectTransform>();
                
                // 将屏幕坐标转换为相对于 Canvas 的坐标
                Vector2 canvasSize = uiCanvas.GetComponent<RectTransform>().sizeDelta;
                Vector2 canvasScale = new Vector2(Screen.width / canvasSize.x, Screen.height / canvasSize.y);
                
                // 计算相对于 Canvas 中心的偏移
                Vector2 relativePosition = new Vector2(
                    (screenPosition.x - Screen.width / 2) / canvasScale.x,
                    (screenPosition.y - Screen.height / 2) / canvasScale.y
                );
                
                rectTransform.anchoredPosition = relativePosition;
            }
        }

        /// <summary>
        /// 凝视点时间间隔
        /// </summary>
        private void DrawInterval()
        {
            if (_drawInterval > 0)
            {
                _drawInterval -= Time.deltaTime;
                if (_drawInterval <= 0)
                {
                    _drawCooling = false;
                }
            }
        }

        /// <summary>
        /// 设置最大绘制数量
        /// </summary>
        /// <param name="value"></param>
        public void SetMaxDrawCount(string value)
        {
            try
            {
                int v = int.Parse(value);
                if (v <= 0)
                    v = 1;
                maxCount = v;
                Init();
            }
            catch (System.Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// 设置绘制时间间隔
        /// </summary>
        /// <param name="value"></param>
        public void SetDrawInterval(string value)
        {
            try
            {
                float v = float.Parse(value);
                if (v <= 0)
                    v = 0.1f;
                drawInterval = v;
            }
            catch (System.Exception)
            {
                throw;
            }
        }

    }

}
