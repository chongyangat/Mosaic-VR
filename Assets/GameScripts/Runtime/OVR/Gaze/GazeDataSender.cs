using System;
using System.Text;
using System.Threading;
using System.Diagnostics;
using UnityEngine;
using MetaQuestProEyeGazeUDP;
using GameMain;

namespace GameLogic
{
    /// <summary>
    /// 凝视点数据发送（TODO 临时：已直接修改）
    /// </summary>
    public class GazeDataSender : MonoBehaviour
    {

        /// <summary>
        /// 左眼凝视点数据
        /// </summary>
        private Vector3? m_LeftEyeGazePosition;

        /// <summary>
        /// 右眼凝视点数据
        /// </summary>
        private Vector3? m_RightEyeGazePosition;

        /// <summary>
        /// 双眼凝视点数据
        /// </summary>
        private Vector3? m_DoubleEyesGazePosition;

        [Header("发送间隔")]
        [Tooltip("UDP数据发送间隔（秒），眼动追踪建议使用0.016-0.033秒（30-60Hz）")]
        public float udpSendRate = 0.016f;

        //发送的数据格式
        enum DataType
        {
            Json,
            Byte,
        }
        [SerializeField] private DataType dataType;

        /// <summary>
        /// 发送数据线程
        /// </summary>
        private Thread m_SendThread;

        /// <summary>
        /// 线程运行标志
        /// </summary>
        private volatile bool m_IsRunning;

        /// <summary>
        /// 线程安全锁对象
        /// </summary>
        private object m_LockObj = new object();

        /// <summary>
        /// 时间戳提供器
        /// 通过依赖注入获得，用于获取同步后的时间戳
        /// </summary>
        private ITimestampProvider m_TimestampProvider;

        /// <summary>
        /// 是否开启日志输出
        /// </summary>
        [SerializeField]
        private bool m_IsShowLog = false;

        private readonly StringBuilder m_JsonBuilder = new StringBuilder(256);

        /// <summary>
        /// 上次发送时间（毫秒）
        /// 用于计算发送频率，使用Stopwatch毫秒值
        /// </summary>
        private long m_LastSendTime;
        private uint m_SendSequence;

        /// <summary>
        /// 发送频率更新事件（注意这是在独立线程调用的）
        /// </summary>/
        public static event Action<float> OnGazeSendFrequencyUpdated;

        #region Unity生命周期

        private void OnEnable()
        {
            UDPManager.Instance.udp.receiveCallback += GetReceive;
            m_IsRunning = true;
            m_SendThread = new Thread(SendDataThread);
            m_SendThread.IsBackground = true;
            m_SendThread.Start();
            m_LastSendTime = 0;
        }

        private void OnDisable()
        {
            m_IsRunning = false;
            if (m_SendThread != null)
            {
                m_SendThread.Abort();
                m_SendThread = null;
            }
            UDPManager.Instance.udp.receiveCallback -= GetReceive;
        }

        #endregion

        /// <summary>
        /// 调发送时间间隔
        /// </summary>
        /// <param name="value"></param>
        public void SetUDPSendRate(string value)
        {
            try
            {
                float v = float.Parse(value);
                if (v <= 0)
                    v = 0.1f;
                udpSendRate = v;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public void UpdateGazePosition(Vector3? leftEyeGazePos, Vector3? rightEyeGazePos, Vector3? doubleEyesGazePos)
        {
            lock (m_LockObj)
            {
                m_LeftEyeGazePosition = leftEyeGazePos;
                m_RightEyeGazePosition = rightEyeGazePos;
                m_DoubleEyesGazePosition = doubleEyesGazePos;
            }
        }

        /// <summary>
        /// 设置时间戳提供器
        /// 
        /// 此方法用于依赖注入，由 Assets 层调用并传入具体实现。
        /// 必须在组件启用前调用，以确保能获取到正确的时间戳。
        /// 
        /// 如果未设置，将使用本地 UTC 时间作为默认值。
        /// </summary>
        /// <param name="provider">时间戳提供器实例（通常由 Assets 层创建）</param>
        public void SetTimestampProvider(ITimestampProvider provider)
        {
            m_TimestampProvider = provider;
            // 输出日志
            UnityEngine.Debug.Log("SetTimestampProvider: " + provider);
        }

        #region 接收数据

        /// <summary>
        /// UDP接收回调
        /// </summary>
        /// <param name="msg">接收到的消息</param>
        private void GetReceive(string msg, double arrivalTimeMs)
        {
            // 处理时钟同步请求（来自 PC 端）
            if (msg.Contains("\"clocksync\"") && !msg.Contains("clocksync_resp"))
            {
                UDPClockSync.HandleMessage(msg, arrivalTimeMs);
                return;
            }

            // switch (dataType)
            // {
            //     case DataType.Json:
            //         //GazeNetData data = JsonUtility.FromJson<GazeNetData>(msg);
            //         break;
            //     case DataType.Byte:
            //         break;
            //     default:
            //         break;
            // }
        }



        #endregion

        #region 发送数据

        /// <summary>
        /// 发数据，循环（后台线程）
        /// </summary>
        private void SendDataThread()
        {
            // 重置发送序列号
            m_SendSequence = 0;
            long sendIntervalMs = (long)(udpSendRate * 1000);
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var trueStr = bool.TrueString.ToLowerInvariant();
            var falseStr = bool.FalseString.ToLowerInvariant();

            while (m_IsRunning)
            {
                while (!UDPManager.Instance.udp.ConnectState())
                {
                    Thread.Sleep(10);
                    if (!m_IsRunning) return;
                }

                switch (dataType)
                {
                    case DataType.Json:
                        m_SendSequence++;
                        m_JsonBuilder.Clear();
                        m_JsonBuilder.Append("{");
                        m_JsonBuilder.Append($"\"sequence\":{m_SendSequence},");
                        Vector3? leftEye, rightEye, doubleEye;
                        lock (m_LockObj)
                        {
                            leftEye = m_LeftEyeGazePosition;
                            rightEye = m_RightEyeGazePosition;
                            doubleEye = m_DoubleEyesGazePosition;
                        }

                        bool isLeftValid = leftEye != null;
                        m_JsonBuilder.Append($"\"isLeftEyeValid\":{(isLeftValid ? trueStr : falseStr)},");
                        if (isLeftValid)
                        {
                            m_JsonBuilder.Append($"\"leftEyeGazePos\":\"{leftEye.Value.x:F3},{leftEye.Value.y:F3},{leftEye.Value.z:F3}\",");
                        }
                        else
                        {
                            m_JsonBuilder.Append($"\"leftEyeGazePos\":\"(0,0,0)\",");
                        }

                        bool isRightValid = rightEye != null;
                        m_JsonBuilder.Append($"\"isRightEyeValid\":{(isRightValid ? trueStr : falseStr)},");
                        if (isRightValid)
                        {
                            m_JsonBuilder.Append($"\"rightEyeGazePos\":\"{rightEye.Value.x:F3},{rightEye.Value.y:F3},{rightEye.Value.z:F3}\",");
                        }
                        else
                        {
                            m_JsonBuilder.Append($"\"rightEyeGazePos\":\"(0,0,0)\",");
                        }

                        bool isDoubleValid = doubleEye != null;
                        m_JsonBuilder.Append($"\"isDoubleEyesValid\":{(isDoubleValid ? trueStr : falseStr)},");
                        if (isDoubleValid)
                        {
                            m_JsonBuilder.Append($"\"doubleEyesGazePos\":\"{doubleEye.Value.x:F3},{doubleEye.Value.y:F3},{doubleEye.Value.z:F3}\",");
                        }
                        else
                        {
                            m_JsonBuilder.Append($"\"doubleEyesGazePos\":\"(0,0,0)\",");
                        }

                        // Capture the Quest-local timestamp immediately before
                        // serialization completes and Socket.SendTo is called.
                        // Use exactly the same monotonic local clock as the four-
                        // timestamp synchronization and UDP receive boundary.
                        // TimestampProvider may use a separately initialized time
                        // base, which would introduce a small constant bias.
                        long ticks = (long)UDPClockSync.GetLocalUnixTimeMsD();
                        m_JsonBuilder.Append($"\"timeStamp\":{ticks}");

                        m_JsonBuilder.Append("}");

                        string sendMsg = m_JsonBuilder.ToString();
                        var sentCnt = UDPManager.Instance.Send(sendMsg);

                        long currentElapsedMs = stopwatch.ElapsedMilliseconds;
                        float deltaTime = (currentElapsedMs - m_LastSendTime) / 1000f;
                        float frequency = deltaTime > 0.001f ? 1.0f / deltaTime : 0f;
                        m_LastSendTime = currentElapsedMs;

                        OnGazeSendFrequencyUpdated?.Invoke(frequency);

                        if (m_IsShowLog)
                        {
                            UnityEngine.Debug.Log($"Gaze UDP Send({frequency:F2}Hz):{sentCnt}):{sendMsg}");
                        }
                        break;
                    case DataType.Byte:
                        break;
                    default:
                        break;
                }

                Thread.Sleep((int)sendIntervalMs);
            }
        }

        #endregion

        public byte[] ToBytes(Vector3 vector)
        {
            byte[] bytes = new byte[12];
            System.Buffer.BlockCopy(BitConverter.GetBytes(vector.x), 0, bytes, 0, 4);
            System.Buffer.BlockCopy(BitConverter.GetBytes(vector.y), 0, bytes, 4, 4);
            System.Buffer.BlockCopy(BitConverter.GetBytes(vector.z), 0, bytes, 8, 4);
            return bytes;
        }

    }

}
