#if GAZE_DATA_SIMULATOR
using UnityEngine;
using System;

/// <summary>
/// 凝视点数据发送模拟器
/// 用于模拟发送EyeGaze数据，测试接收模块
/// 通过GAZE_DATA_SIMULATOR宏开启/关闭
/// </summary>
public class GazeDataSimulator : MonoBehaviour
{
    #region 常量和静态变量
    /// <summary>
    /// 静态实例，方便全局访问
    /// </summary>
    private static GazeDataSimulator s_Instance;
    #endregion

    #region 序列化字段
    [Header("基本配置")]
    /// <summary>
    /// 发送间隔（秒）
    /// </summary>
    [Tooltip("发送数据的时间间隔，单位：秒")]
    [SerializeField]
    private float m_SendInterval = 0.5f;

    /// <summary>
    /// 发送目标IP地址
    /// </summary>
    [Tooltip("发送数据的目标IP地址")]
    [SerializeField]
    private string m_TargetIP = "127.0.0.1";

    /// <summary>
    /// 发送目标端口
    /// </summary>
    [Tooltip("发送数据的目标端口")]
    [SerializeField]
    private int m_TargetPort = 8082;

    /// <summary>
    /// 日志输出开关
    /// </summary>
    [Tooltip("是否启用日志输出")]
    [SerializeField]
    private bool m_EnableLogging = true;

    [Header("数据发送配置")]
    /// <summary>
    /// 是否发送左眼数据
    /// </summary>
    [Tooltip("是否发送左眼凝视点数据")]
    [SerializeField]
    private bool m_SendLeftEyeData = true;

    /// <summary>
    /// 是否发送右眼数据
    /// </summary>
    [Tooltip("是否发送右眼凝视点数据")]
    [SerializeField]
    private bool m_SendRightEyeData = true;

    /// <summary>
    /// 是否发送双眼数据
    /// </summary>
    [Tooltip("是否发送双眼凝视点数据")]
    [SerializeField]
    private bool m_SendDoubleEyeData = true;

    [Header("位置随机范围配置")]
    /// <summary>
    /// 是否限制生成点在相机可视范围内
    /// </summary>
    [Tooltip("是否限制生成的点在当前相机的可视范围内")]
    [SerializeField]
    private bool m_LimitToCameraView = false;

    /// <summary>
    /// X轴随机范围
    /// </summary>
    [Tooltip("凝视点X轴数据的随机范围")]
    [SerializeField]
    private float m_RandomRangeX = 1.0f;

    /// <summary>
    /// Y轴随机范围
    /// </summary>
    [Tooltip("凝视点Y轴数据的随机范围")]
    [SerializeField]
    private float m_RandomRangeY = 1.0f;

    /// <summary>
    /// 最小Z轴值
    /// </summary>
    [Tooltip("生成点的最小Z轴值")]
    [SerializeField]
    private float m_MinZValue = 0.5f;

    /// <summary>
    /// Z轴随机范围
    /// </summary>
    [Tooltip("凝视点Z轴数据的随机范围")]
    [SerializeField]
    private float m_RandomRangeZ = 3.0f;

    [Header("密集点功能配置")]
    /// <summary>
    /// 生成密集点的开关
    /// </summary>
    [Tooltip("是否启用密集点生成功能")]
    [SerializeField]
    private bool m_EnableDensePoints = false;

    /// <summary>
    /// 稀疏点最小持续时间（秒）
    /// </summary>
    [Tooltip("稀疏点模式的最小持续时间（秒）")]
    [SerializeField]
    private float m_SparseDurationMin = 1.0f;

    /// <summary>
    /// 稀疏点最大持续时间（秒）
    /// </summary>
    [Tooltip("稀疏点模式的最大持续时间（秒）")]
    [SerializeField]
    private float m_SparseDurationMax = 5.0f;

    /// <summary>
    /// 密集点最小持续时间（秒）
    /// </summary>
    [Tooltip("密集点模式的最小持续时间（秒）")]
    [SerializeField]
    private float m_DenseDurationMin = 1.0f;

    /// <summary>
    /// 密集点最大持续时间（秒）
    /// </summary>
    [Tooltip("密集点模式的最大持续时间（秒）")]
    [SerializeField]
    private float m_DenseDurationMax = 5.0f;

    /// <summary>
    /// 密集点发送间隔（秒）
    /// </summary>
    [Tooltip("密集点模式下的数据发送间隔（秒）")]
    [SerializeField]
    private float m_DenseSendInterval = 0.05f;

    /// <summary>
    /// 位置密集度系数
    /// </summary>
    [Tooltip("密集点模式下位置的密集程度，值越小越密集")]
    [SerializeField]
    private float m_DensePositionFactor = 0.3f;

    /// <summary>
    /// 密集点中心位置X轴随机范围
    /// </summary>
    [Tooltip("密集点中心位置X轴的随机范围")]
    [SerializeField]
    private float m_DenseCenterRangeX = 1.0f;

    /// <summary>
    /// 密集点中心位置Y轴随机范围
    /// </summary>
    [Tooltip("密集点中心位置Y轴的随机范围")]
    [SerializeField]
    private float m_DenseCenterRangeY = 1.0f;

    /// <summary>
    /// 密集点中心位置Z轴随机范围
    /// </summary>
    [Tooltip("密集点中心位置Z轴的随机范围")]
    [SerializeField]
    private float m_DenseCenterRangeZ = 3.0f;
    #endregion

    #region 私有变量
    /// <summary>
    /// 发送数据协程
    /// </summary>
    private Coroutine m_SendCoroutine;

    /// <summary>
    /// UDP管理器引用
    /// </summary>
    private MetaQuestProEyeGazeUDP.UDPManager m_UDPManager;

    /// <summary>
    /// 当前是否为密集点模式
    /// </summary>
    private bool m_IsDenseMode = false;

    /// <summary>
    /// 当前模式剩余持续时间
    /// </summary>
    private float m_ModeRemainingDuration = 0.0f;

    /// <summary>
    /// 原始发送间隔
    /// </summary>
    private float m_OriginalSendInterval = 0.5f;

    /// <summary>
    /// 当前密集点中心位置
    /// </summary>
    private Vector3 m_CurrentDenseCenter = Vector3.zero;

    /// <summary>
    /// 当前相机引用
    /// </summary>
    private Camera m_CurrentCamera;
    #endregion

    #region 单例模式
    /// <summary>
    /// 获取模拟器实例
    /// </summary>
    public static GazeDataSimulator Instance
    {
        get
        {
            if (s_Instance == null)
            {
                s_Instance = FindObjectOfType<GazeDataSimulator>();
                if (s_Instance == null)
                {
                    // 如果场景中没有找到实例，创建一个新的GameObject并添加组件
                    GameObject go = new GameObject("GazeDataSimulator");
                    s_Instance = go.AddComponent<GazeDataSimulator>();
                    Debug.Log("GazeDataSimulator: 创建了新的实例");
                }
            }
            return s_Instance;
        }
    }

    /// <summary>
    /// 检查是否有可用的实例
    /// </summary>
    public static bool HasInstance
    {
        get { return s_Instance != null; }
    }
    #endregion

    #region 生命周期方法
    private void Awake()
    {
        // 实现单例模式
        if (s_Instance == null)
        {
            s_Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (s_Instance != this)
        {
            Debug.LogWarning("GazeDataSimulator: 已存在模拟器实例，当前实例将被销毁");
            Destroy(gameObject);
            return;
        }

        // 获取UDPManager实例（使用小写instance）
        m_UDPManager = MetaQuestProEyeGazeUDP.UDPManager.Instance;

        // 保存原始发送间隔
        m_OriginalSendInterval = m_SendInterval;

        // 初始化相机引用
        UpdateCurrentCamera();

        // 初始化模式切换逻辑
        InitializeModeSwitching();
    }

    private void Update()
    {
        // 更新当前相机引用
        UpdateCurrentCamera();

        // 如果启用了密集点模式，更新当前模式剩余时间
        if (m_EnableDensePoints)
        {
            m_ModeRemainingDuration -= Time.deltaTime;

            // 如果当前模式时间结束，切换模式
            if (m_ModeRemainingDuration <= 0.0f)
            {
                SwitchMode();
            }
        }
    }

    /// <summary>
    /// 更新当前相机引用
    /// </summary>
    private void UpdateCurrentCamera()
    {
        m_CurrentCamera = Camera.main;
        if (m_CurrentCamera == null)
        {
            // 如果没有找到Main Camera，尝试获取场景中激活的相机
            Camera[] cameras = Camera.allCameras;
            if (cameras.Length > 0)
            {
                m_CurrentCamera = cameras[0];
            }
        }
    }

    private void OnEnable()
    {
        // 初始化UDP
        m_UDPManager.SetIP(m_TargetIP, m_TargetPort);

        // 开始发送模拟数据
        if (m_SendCoroutine == null)
        {
            m_SendCoroutine = StartCoroutine(SendSimulationData());
        }

        Debug.Log("GazeDataSimulator: 开始发送模拟数据");
    }

    private void OnDisable()
    {
        // 停止发送模拟数据
        if (m_SendCoroutine != null)
        {
            StopCoroutine(m_SendCoroutine);
            m_SendCoroutine = null;
        }

        Debug.Log("GazeDataSimulator: 停止发送模拟数据");
    }
    #endregion

    #region 公共方法
    /// <summary>
    /// 设置发送间隔
    /// </summary>
    /// <param name="interval">间隔时间（秒）</param>
    public void SetSendInterval(float interval)
    {
        m_SendInterval = Mathf.Max(0.01f, interval);
    }

    /// <summary>
    /// 设置目标IP和端口
    /// </summary>
    /// <param name="ip">IP地址</param>
    /// <param name="port">端口号</param>
    public void SetTarget(string ip, int port)
    {
        m_TargetIP = ip;
        m_TargetPort = port;
        m_UDPManager.SetIP(ip, port);
    }

    /// <summary>
    /// 测试发送单次模拟数据（用于编辑器测试）
    /// </summary>
    public void TestSendSingleData()
    {
        if (m_UDPManager == null)
        {
            Debug.LogError("GazeDataSimulator: UDPManager未初始化，无法发送测试数据");
            return;
        }

        // 生成模拟数据
        Vector3? leftEyePos = m_SendLeftEyeData ? GenerateRandomPosition() : (Vector3?)null;
        Vector3? rightEyePos = m_SendRightEyeData ? GenerateRandomPosition() : (Vector3?)null;
        Vector3? doubleEyePos = m_SendDoubleEyeData ? CalculateDoubleEyePosition(leftEyePos, rightEyePos) : (Vector3?)null;

        // 构建发送数据
        GazeNetData simulationData = new GazeNetData();
        simulationData.timeStamp = GetCurrentTimeStamp();

        // 左眼数据
        simulationData.isLeftEyeValid = leftEyePos.HasValue;
        simulationData.leftEyeGazePos = leftEyePos.HasValue ? leftEyePos.ToString() : "(0,0,0)";

        // 右眼数据
        simulationData.isRightEyeValid = rightEyePos.HasValue;
        simulationData.rightEyeGazePos = rightEyePos.HasValue ? rightEyePos.ToString() : "(0,0,0)";

        // 双眼数据
        simulationData.isDoubleEyesValid = doubleEyePos.HasValue;
        simulationData.doubleEyesGazePos = doubleEyePos.HasValue ? doubleEyePos.ToString() : "(0,0,0)";

        // 序列化为JSON
        string jsonData = JsonUtility.ToJson(simulationData);

        // 发送数据
        int sentCount = m_UDPManager.Send(jsonData);
        Debug.Log(string.Format("GazeDataSimulator: 测试发送数据 ({0} bytes) - {1}", sentCount, jsonData));
    }

    /// <summary>
    /// 创建并初始化模拟器实例
    /// </summary>
    /// <param name="sendInterval">发送间隔（秒）</param>
    /// <param name="targetIP">目标IP</param>
    /// <param name="targetPort">目标端口</param>
    /// <returns>模拟器实例</returns>
    public static GazeDataSimulator Create(float sendInterval = 0.01f, string targetIP = "127.0.0.1", int targetPort = 8082)
    {
        GazeDataSimulator simulator = Instance;
        simulator.SetSendInterval(sendInterval);
        simulator.SetTarget(targetIP, targetPort);
        return simulator;
    }
    #endregion

    #region 密集点功能方法
    /// <summary>
    /// 初始化模式切换逻辑
    /// </summary>
    private void InitializeModeSwitching()
    {
        // 初始化为稀疏模式
        m_IsDenseMode = false;
        // 设置初始模式持续时间
        m_ModeRemainingDuration = UnityEngine.Random.Range(m_SparseDurationMin, m_SparseDurationMax);
    }

    /// <summary>
    /// 切换模式
    /// </summary>
    private void SwitchMode()
    {
        // 切换模式状态
        m_IsDenseMode = !m_IsDenseMode;

        // 根据模式设置发送间隔和其他参数
        if (m_IsDenseMode)
        {
            // 切换到密集模式
            m_SendInterval = m_DenseSendInterval;
            // 生成新的密集点中心位置
            m_CurrentDenseCenter = GenerateDenseCenterPosition();
            // 设置密集模式持续时间
            m_ModeRemainingDuration = UnityEngine.Random.Range(m_DenseDurationMin, m_DenseDurationMax);
            if (m_EnableLogging)
                Debug.Log(string.Format("GazeDataSimulator: 切换到密集点模式，持续 {0} 秒，中心位置: {1}", m_ModeRemainingDuration, m_CurrentDenseCenter));
        }
        else
        {
            // 切换到稀疏模式
            m_SendInterval = m_OriginalSendInterval;
            // 设置稀疏模式持续时间
            m_ModeRemainingDuration = UnityEngine.Random.Range(m_SparseDurationMin, m_SparseDurationMax);
            if (m_EnableLogging)
                Debug.Log(string.Format("GazeDataSimulator: 切换到稀疏点模式，持续 {0} 秒", m_ModeRemainingDuration));
        }

        // 如果协程正在运行，重启协程以应用新的发送间隔
        if (m_SendCoroutine != null)
        {
            StopCoroutine(m_SendCoroutine);
            m_SendCoroutine = StartCoroutine(SendSimulationData());
        }
    }

    /// <summary>
    /// 生成密集点中心位置
    /// </summary>
    private Vector3 GenerateDenseCenterPosition()
    {
        float x = UnityEngine.Random.Range(-m_DenseCenterRangeX, m_DenseCenterRangeX);
        float y = UnityEngine.Random.Range(-m_DenseCenterRangeY, m_DenseCenterRangeY);
        float z = UnityEngine.Random.Range(m_MinZValue, m_DenseCenterRangeZ);

        Vector3 centerPos = new Vector3(x, y, z);

        // 如果限制在相机可视范围内，则基于相机位置生成密集点中心
        if (m_LimitToCameraView && m_CurrentCamera != null)
        {
            centerPos = m_CurrentCamera.transform.position + centerPos;
        }

        return centerPos;
    }
    #endregion

    #region 数据生成和处理方法
    /// <summary>
    /// 发送模拟数据协程
    /// </summary>
    private System.Collections.IEnumerator SendSimulationData()
    {
        while (true)
        {
            // 等待UDP连接建立
            yield return new WaitUntil(() => m_UDPManager.udp.ConnectState());

            // 根据当前模式设置延迟
            float currentInterval = m_EnableDensePoints ? m_SendInterval : m_OriginalSendInterval;
            WaitForSeconds delay = new WaitForSeconds(currentInterval);

            // 生成模拟数据
            Vector3? leftEyePos = m_SendLeftEyeData ? GenerateRandomPosition() : (Vector3?)null;
            Vector3? rightEyePos = m_SendRightEyeData ? GenerateRandomPosition() : (Vector3?)null;
            Vector3? doubleEyePos = m_SendDoubleEyeData ? CalculateDoubleEyePosition(leftEyePos, rightEyePos) : (Vector3?)null;

            // 构建发送数据
            GazeNetData simulationData = new GazeNetData();
            simulationData.timeStamp = GetCurrentTimeStamp();

            // 左眼数据
            simulationData.isLeftEyeValid = leftEyePos.HasValue;
            simulationData.leftEyeGazePos = leftEyePos.HasValue ? leftEyePos.ToString() : "(0,0,0)";

            // 右眼数据
            simulationData.isRightEyeValid = rightEyePos.HasValue;
            simulationData.rightEyeGazePos = rightEyePos.HasValue ? rightEyePos.ToString() : "(0,0,0)";

            // 双眼数据
            simulationData.isDoubleEyesValid = doubleEyePos.HasValue;
            simulationData.doubleEyesGazePos = doubleEyePos.HasValue ? doubleEyePos.ToString() : "(0,0,0)";

            // 序列化为JSON
            string jsonData = JsonUtility.ToJson(simulationData);

            // 发送数据
            int sentCount = m_UDPManager.Send(jsonData);

            // 输出日志，包含当前模式
            if (m_EnableLogging)
            {
                string modeStr = m_EnableDensePoints ? (m_IsDenseMode ? "密集点" : "稀疏点") : "普通";
                Debug.Log(string.Format("GazeDataSimulator: [{0}] 发送数据 ({1} bytes) -> {2}", modeStr, sentCount, jsonData));
            }

            // 等待下一次发送
            yield return delay;
        }
    }

    /// <summary>
    /// 检查点是否在相机可视范围内
    /// </summary>
    /// <param name="point">要检查的点</param>
    /// <returns>是否在可视范围内</returns>
    private bool IsPointInCameraView(Vector3 point)
    {
        if (m_CurrentCamera == null)
        {
            return true; // 如果没有相机，默认所有点都有效
        }

        // 将点转换为屏幕坐标
        Vector3 screenPoint = m_CurrentCamera.WorldToScreenPoint(point);

        // 检查屏幕坐标是否在视图范围内
        bool isInScreen = screenPoint.x >= 0 && screenPoint.x <= Screen.width &&
                         screenPoint.y >= 0 && screenPoint.y <= Screen.height &&
                         screenPoint.z > 0; // z值必须大于0，表示在相机前面

        // 检查点是否在相机视锥体内部
        Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(m_CurrentCamera);
        bool isInFrustum = GeometryUtility.TestPlanesAABB(frustumPlanes, new Bounds(point, Vector3.one * Vector3.kEpsilon));

        return isInScreen && isInFrustum;
    }

    /// <summary>
    /// 生成随机凝视点位置
    /// </summary>
    private Vector3 GenerateRandomPosition()
    {
        Vector3 result;
        int maxAttempts = 100; // 最大重试次数，避免无限循环
        int attempts = 0;

        do
        {
            if (m_EnableDensePoints && m_IsDenseMode)
            {
                // 密集点模式：在中心位置附近生成密集点
                float x = m_CurrentDenseCenter.x + UnityEngine.Random.Range(-m_RandomRangeX * m_DensePositionFactor, m_RandomRangeX * m_DensePositionFactor);
                float y = m_CurrentDenseCenter.y + UnityEngine.Random.Range(-m_RandomRangeY * m_DensePositionFactor, m_RandomRangeY * m_DensePositionFactor);
                float z = m_CurrentDenseCenter.z + UnityEngine.Random.Range(-m_RandomRangeZ * m_DensePositionFactor, m_RandomRangeZ * m_DensePositionFactor);

                // 确保Z轴不会小于最小Z轴值
                z = Mathf.Max(m_MinZValue, z);

                result = new Vector3(x, y, z);
            }
            else
            {
                // 稀疏点模式：生成正常范围的随机点
                float x = UnityEngine.Random.Range(-m_RandomRangeX, m_RandomRangeX);
                float y = UnityEngine.Random.Range(-m_RandomRangeY, m_RandomRangeY);
                float z = UnityEngine.Random.Range(m_MinZValue, m_RandomRangeZ); // 使用Z轴随机范围，从最小Z轴值开始

                Vector3 randomPos = new Vector3(x, y, z);

                // 如果限制在相机可视范围内，则基于相机位置生成点
                if (m_LimitToCameraView && m_CurrentCamera != null)
                {
                    // 基于相机位置生成点，将随机位置作为相机相对位置
                    result = m_CurrentCamera.transform.position + randomPos;
                }
                else
                {
                    // 否则生成世界坐标点
                    result = randomPos;
                }
            }

            attempts++;
            // 如果attempts超过最大次数
            if (attempts >= maxAttempts)
            {
                Debug.LogWarning("GenerateRandomPosition: 超过最大重试次数，返回默认值");
            }

            // 检查点是否在相机可视范围内
        } while (m_LimitToCameraView && !IsPointInCameraView(result) && attempts < maxAttempts);

        return result;
    }

    /// <summary>
    /// 计算双眼凝视点位置
    /// </summary>
    /// <param name="leftEyePos">左眼位置</param>
    /// <param name="rightEyePos">右眼位置</param>
    private Vector3? CalculateDoubleEyePosition(Vector3? leftEyePos, Vector3? rightEyePos)
    {
        if (leftEyePos.HasValue && rightEyePos.HasValue)
        {
            // 取左右眼的平均值
            return (leftEyePos.Value + rightEyePos.Value) * 0.5f;
        }
        else if (leftEyePos.HasValue)
        {
            return leftEyePos.Value;
        }
        else if (rightEyePos.HasValue)
        {
            return rightEyePos.Value;
        }
        else
        {
            return null;
        }
    }

    /// <summary>
    /// 获取当前时间戳（Unix时间，毫秒级）
    /// </summary>
    private long GetCurrentTimeStamp()
    {
        DateTime dtFrom = new DateTime(1970, 1, 1, 0, 0, 0, 0);
        DateTime dtNow = DateTime.Now.ToUniversalTime();
        TimeSpan toNow = dtNow.Subtract(dtFrom);
        return Convert.ToInt64(toNow.TotalMilliseconds);
    }
    #endregion

    #region 嵌套类
    /// <summary>
    /// 凝视点网络数据结构
    /// 与GazeDataSender中的GazeNetData保持一致
    /// </summary>
    [Serializable]
    private class GazeNetData
    {
        /// <summary>
        /// 时间戳(毫秒级)
        /// </summary>
        public long timeStamp;

        /// <summary>
        /// 左眼数据是否有效
        /// </summary>
        public bool isLeftEyeValid;

        /// <summary>
        /// 左眼凝视点数据
        /// </summary>
        public string leftEyeGazePos;

        /// <summary>
        /// 右眼数据是否有效
        /// </summary>
        public bool isRightEyeValid;

        /// <summary>
        /// 右眼凝视点数据
        /// </summary>
        public string rightEyeGazePos;

        /// <summary>
        /// 双目数据是否有效
        /// </summary>
        public bool isDoubleEyesValid;

        /// <summary>
        /// 双目凝视点数据
        /// </summary>
        public string doubleEyesGazePos;
    }
    #endregion
}
#endif