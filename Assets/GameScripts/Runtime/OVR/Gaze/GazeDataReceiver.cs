using MetaQuestProEyeGazeUDP;
using UnityEngine;
using UnityEngine.Events;
using GameMain;
using UnityGameFramework.Runtime;

namespace GameLogic
{
    public class GazeDataReceiver : MonoBehaviour
    {
        //发送的数据格式
        enum DataType
        {
            Json,
            Byte,
        }

        /// <summary>
        /// 接收的数据类型
        /// </summary>
        [SerializeField]
        private DataType dataType;

        /// <summary>
        /// 发送凝视点数据（左眼、右眼、双眼）（主线程 Update）
        /// </summary>
        [SerializeField]
        public UnityEvent<GazeNetData, Vector3?, Vector3?, Vector3?> OnReceivedGazePointInUpdate;

        /// <summary>
        /// 发送凝视点数据（左眼、右眼、双眼）（非主线程）
        /// </summary>
        [SerializeField]
        public UnityEvent<GazeNetData, Vector3?, Vector3?, Vector3?> OnReceivedGazePoint;

        /// <summary>
        /// 是否已收到第一帧数据
        /// </summary>
        private bool m_receivedFirstFrame = false;

        /// <summary>
        /// 眼动数据序列号
        /// </summary>
        private uint m_eyeTrackingSequence = 0;

        /// <summary>
        /// 最新的眼动数据缓存
        /// </summary>
        private struct CachedGazeData
        {
            public bool hasData;
            public GazeNetData data;
            public Vector3? leftEyePoint;
            public Vector3? rightEyePoint;
            public Vector3? bothEyePoint;
        }

        private CachedGazeData m_cachedData;
        private readonly object m_cacheLock = new object();

        #region U3D

        private void OnEnable()
        {
            UDPManager.Instance.udp.receiveCallback += GetReceive;
        }

        private void OnDisable()
        {
            UDPManager.Instance.udp.receiveCallback -= GetReceive;
        }

        private void Update()
        {
            // 在主线程 Update 中处理缓存的最新数据
            CachedGazeData dataToProcess;
            lock (m_cacheLock)
            {
                dataToProcess = m_cachedData;
                // 清空缓存标记
                m_cachedData.hasData = false;
            }

            if (dataToProcess.hasData)
            {
                OnReceivedGazePointInUpdate?.Invoke(dataToProcess.data, dataToProcess.leftEyePoint, dataToProcess.rightEyePoint, dataToProcess.bothEyePoint);
            }
        }

        #endregion

#if DEBUG_GAZE_RECEIVER
        /// <summary>
        /// 上次接收时间（ticks）
        /// 用于计算接收频率，使用 System.DateTime.Ticks 避免跨线程访问问题
        /// </summary>
        long m_lastReceiveTicks = 0;
        private static readonly long TicksPerSecond = System.TimeSpan.TicksPerSecond;
#endif

        /// <summary>
        /// UDP接收（在处理线程）
        /// </summary>
        /// <param name="msg">消息内容</param>
        /// <param name="arrivalTimeMs">接收线程捕获的到达时间（Unix毫秒，含小数精度）</param>
        private void GetReceive(string msg, double arrivalTimeMs)
        {
            // 防止处理异常终止整个 UDP 处理线程
            try
            {
                // 检查是否是时钟同步响应消息
                if (msg.Contains("\"clocksync_resp\""))
                {
                    UDPClockSync.HandleMessage(msg, arrivalTimeMs);
                    return;
                }

                // arrivalTimeMs 已在接收线程捕获，直接传递给处理流程

                // 计时在接收线程完成，不影响数据接收计时
#if DEBUG_GAZE_RECEIVER
                long currentTicks = System.DateTime.UtcNow.Ticks;
                float frequency = 0.0f;
                if (m_lastReceiveTicks > 0)
                {
                    float deltaTime = (float)(currentTicks - m_lastReceiveTicks) / TicksPerSecond;
                    if (deltaTime > 0.0001f)
                    {
                        frequency = 1.0f / deltaTime;
                    }
                }
                m_lastReceiveTicks = currentTicks;
                Debug.Log($"Receive gaze point({frequency:F2}Hz):{msg}");
#endif

                ProcessReceivedData(msg, arrivalTimeMs);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"[GazeDataReceiver] 接收处理异常（已恢复）: {e}");
            }
        }

        /// <summary>
        /// 处理接收到的数据（在非主线程）
        /// </summary>
        /// <param name="msg">接收到的消息</param>
        /// <param name="arrivalTimeMs">数据包到达时间（Unix毫秒时间戳，含小数精度）</param>
        private void ProcessReceivedData(string msg, double arrivalTimeMs = 0)
        {
            switch (dataType)
            {
                case DataType.Json:
                    GazeNetData data;
                    try
                    {
                        data = JsonUtility.FromJson<GazeNetData>(msg);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"数据:{msg}Json解析异常:{e}");
                        return;
                    }
                    m_eyeTrackingSequence = data.sequence;
#if DEBUG_GAZE_RECEIVER
                    Log.Debug($"Receive gaze point:[{data.timeStamp}]{data.leftEyeGazePos},{data.rightEyeGazePos},{data.doubleEyesGazePos}");
#endif
                    // 记录接收数据的性能监控
                    if (NetworkPerformanceMonitor.Instance != null)
                    {
                        // timeStamp已经是毫秒级时间戳，直接使用
                        ulong timestampInMilliseconds = (ulong)data.timeStamp;
                        // 使用递增的序列号，时间戳作为时间戳参数
                        // 传入在回调入口捕获的到达时间，避免 JSON 解析耗时影响延迟测量
                        NetworkPerformanceMonitor.Instance.OnEyeTrackingDataReceived(m_eyeTrackingSequence, timestampInMilliseconds, arrivalTimeMs);

                        // 当收到第一帧数据时激活眼动链路
                        if (!m_receivedFirstFrame)
                        {
                            m_receivedFirstFrame = true;
                            NetworkPerformanceMonitor.Instance.UpdateLinkActivity("EyeTracking→PC");
                            Log.Info("EyeTracking→PC link activated on first frame.");
                        }
                    }

                    // 左眼
                    Vector3? leftEyePoint = GetGazePoint(data.isLeftEyeValid, data.leftEyeGazePos);
                    // 右眼
                    Vector3? rightEyePoint = GetGazePoint(data.isRightEyeValid, data.rightEyeGazePos);
                    // 双眼
                    Vector3? bothEyePoint = GetGazePoint(data.isDoubleEyesValid, data.doubleEyesGazePos);
                    // 触发事件
                    OnReceivedGazePoint?.Invoke(data, leftEyePoint, rightEyePoint, bothEyePoint);
                    // 缓存最新数据，不直接入队，避免队列爆炸
                    lock (m_cacheLock)
                    {
                        m_cachedData.data = data;
                        m_cachedData.leftEyePoint = leftEyePoint;
                        m_cachedData.rightEyePoint = rightEyePoint;
                        m_cachedData.bothEyePoint = bothEyePoint;
                        m_cachedData.hasData = true;
                    }
                    break;
                case DataType.Byte:
#if DEBUG_GAZE_RECEIVER
                    Log.Debug($"Receive gaze point:{msg}");
                    Log.Error("Byte data not implemented");
#endif                    
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// 获取凝视点
        /// </summary>
        /// <param name="isValid"></param>
        /// <param name="gazePos"></param>
        /// <returns></returns>
        private Vector3? GetGazePoint(bool isValid, string gazePos)
        {
            if (isValid)
            {
                return Vector3FromString(gazePos);
            }
            else
            {
                return null;
            }
        }

        // data.vector3是string类型，通过Vector3.ToString()转换的，需要转换回Vector3
        private Vector3 Vector3FromString(string vector3)
        {
            // 去除括号
            vector3 = vector3.Trim('(', ')');
            string[] vector3Array = vector3.Split(',');
            return new Vector3(float.Parse(vector3Array[0]), float.Parse(vector3Array[1]), float.Parse(vector3Array[2]));
        }

    }
}
