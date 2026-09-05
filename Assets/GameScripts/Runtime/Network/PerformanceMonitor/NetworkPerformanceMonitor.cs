using System;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using MetaQuestProEyeGazeUDP;

namespace GameMain
{
    /// <summary>
    /// 负延迟输出模式
    /// </summary>
    public enum NegativeLatencyMode
    {
        /// <summary>输出实际负值（可看到时钟偏移方向）</summary>
        Raw,
        /// <summary>负值取绝对值，不产生负数或假0</summary>
        Processed
    }

    /// <summary>
    /// 网络性能监控核心类，负责监控延迟、抖动、丢包率等网络性能指标
    /// 支持多链路监控，每个链路有独立的性能数据
    /// </summary>
    public partial class NetworkPerformanceMonitor : MonoBehaviour
    {

        #region 配置参数

        [Header("监控配置")]
        [Tooltip("是否启用网络性能监控")]
        [SerializeField]
        private bool _isEnabled = true;

        [Tooltip("普通链路的数据采样间隔（秒）；PC<->VR 状态同步链路自动跟随 Mirror 发送频率")]
        [SerializeField]
        [Range(0.1f, 5f)]
        private float _sampleInterval = 1.0f;

        [Tooltip("循环缓冲区大小（历史数据条数）")]
        [SerializeField]
        [Range(10, 1000)]
        //private int _bufferSize = 300;
        private int _bufferSize = 90 * 60 * 10;

        [Tooltip("抖动计算滑动窗口大小")]
        [SerializeField]
        [Range(5, 100)]
        private int _jitterWindowSize = 20;

        /// <summary>
        /// 是否显示日志
        /// </summary>
        public bool IsShowLog = false;

        [Header("负延迟处理")]
        [Tooltip("Raw = 输出实际负值（可看到时钟偏移方向）\n" +
                 "Processed = 负值取绝对值（不产生负数或假0）")]
        private NegativeLatencyMode _negativeLatencyMode = NegativeLatencyMode.Raw;

        /// <summary>
        /// 负延迟输出模式
        /// </summary>
        public NegativeLatencyMode NegativeLatencyMode => _negativeLatencyMode;

        #endregion

        #region 事件

        /// <summary>
        /// 性能数据更新事件
        /// </summary>
        public event Action<PerformanceData> OnPerformanceUpdated;

        #endregion

        #region 私有字段

        /// <summary>
        /// 存储所有链路的数据
        /// </summary>
        private Dictionary<string, LinkData> _linkDataDict = new Dictionary<string, LinkData>();

        /// <summary>
        /// 当前活动链路
        /// </summary>
        private string _currentLink = StateSyncLinkName;

        /// <summary>
        /// 上次采样时间
        /// </summary>
        private float _lastSampleTime = 0f;

        /// <summary>
        /// PC&lt;-&gt;VR 状态同步链路的上次采样时间。
        /// 使用与 Mirror 广播相同的高精度时钟和间隔算法，避免 60 Hz 被帧时间余数拖低。
        /// </summary>
        private double _lastStateSyncSampleTime;

        private const string StateSyncLinkName = "PC<->VR 状态同步";

        /// <summary>
        /// 上次性能数据上报时间（VR端）
        /// </summary>
        private float _lastReportTime = 0f;

        /// <summary>
        /// 性能数据上报间隔（秒）
        /// </summary>
        [SerializeField]
        private float _reportInterval = 1.0f;

        /// <summary>
        /// 性能更新事件的数据传递（零 GC 分配方案）
        /// 接收线程写入 _pendingUpdateData，主线程 Update 读取并触发事件
        /// "最新数据优先" 语义：多次写入只保留最新值，适合 UI 刷新场景
        /// </summary>
        private PerformanceData _pendingUpdateData;
        private volatile bool _hasPendingUpdate;
        private readonly object _pendingUpdateLock = new object();

        #endregion

        #region 单例模式

        /// <summary>
        /// 单例实例
        /// </summary>
        public static NetworkPerformanceMonitor Instance { get; private set; }

        #endregion

        #region 公共属性

        /// <summary>
        /// 是否启用监控
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set => _isEnabled = value;
        }

        /// <summary>
        /// PC&lt;-&gt;VR 状态同步链路的采样间隔，始终跟随 Mirror 的发送间隔。
        /// </summary>
        public static float StateSyncSampleInterval => NetworkServer.sendInterval;

        /// <summary>
        /// 当前活动链路
        /// </summary>
        public string CurrentLink
        {
            get
            {
                // 使用链路锁保护_currentLink的读取
                lock (_linkLock)
                {
                    return _currentLink;
                }
            }
            set
            {
                // 使用链路锁保护_currentLink的写入
                lock (_linkLock)
                {
                    _currentLink = value;
                }
            }
        }

        /// <summary>
        /// 获取所有链路名称
        /// </summary>
        public List<string> AllLinks
        {
            get
            {
                lock (_linkLock)
                {
                    return new List<string>(_linkDataDict.Keys);
                }
            }
        }

        #endregion

        #region Unity生命周期

        /// <summary>
        /// 组件唤醒时初始化
        /// </summary>
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            // 根据运行环境初始化链路
            InitializeLink(StateSyncLinkName);
#if MANAGER_SERVER
            InitializeLink("MoCap→PC");
#else
            InitializeLink("MoCap→VR");
#endif
            InitializeLink("EyeTracking→PC");

            // 注册性能数据上报消息处理器
            RegisterPerformanceReportHandlers();
        }

        /// <summary>
        /// 组件启动时设置初始状态
        /// </summary>
        private void Start()
        {
            SystemClockSync.Initialize();
#if MANAGER_SERVER
            UDPClockSync.Initialize(isServer: true);
#else
            UDPClockSync.Initialize(isServer: false);
#endif
            _lastSampleTime = Time.time;
            _lastStateSyncSampleTime = NetworkTime.localTime;

            // 初始化 GC 监控基线
            InitGCMonitor();
        }

        /// <summary>
        /// 每帧更新
        /// </summary>
        private void Update()
        {
            // 帧计时：测量上一帧到本帧的实际耗时（含 GC 暂停）
            double lastFrameMs = _frameStopwatch.Elapsed.TotalMilliseconds;
            _frameStopwatch.Restart();

            // GC 监控：检测 GC 回收事件并输出日志
            CheckGCEvents(lastFrameMs);

            // 处理主线程任务队列
            MainThreadTaskQueue.Update();

            // 处理性能数据更新事件（零 GC 分配方案）
            if (_hasPendingUpdate)
            {
                PerformanceData data;
                lock (_pendingUpdateLock)
                {
                    data = _pendingUpdateData;
                    _hasPendingUpdate = false;
                }
                OnPerformanceUpdated?.Invoke(data);
            }

            // UDP 时钟同步（PC 端定时发起请求）
            UDPClockSync.Update();

            if (!_isEnabled)
                return;

            SampleStateSyncAtMirrorRate();

            if (Time.time - _lastSampleTime >= _sampleInterval)
            {
                SampleAllLinks();
                _lastSampleTime = Time.time;
            }

            // VR端定期上报性能数据到管理端（仅在非MANAGER_SERVER端执行
#if !MANAGER_SERVER
            UpdatePerformanceReporting();
#endif
        }

        /// <summary>
        /// 按 Mirror 的实际发送节拍采集 PC&lt;-&gt;VR 状态同步链路。
        /// </summary>
        private void SampleStateSyncAtMirrorRate()
        {
            double interval = StateSyncSampleInterval;
            if (interval <= 0d || double.IsNaN(interval) || double.IsInfinity(interval))
            {
                return;
            }

            if (!AccurateInterval.Elapsed(NetworkTime.localTime, interval, ref _lastStateSyncSampleTime))
            {
                return;
            }

            // 未连接时只推进采样时钟，不调用 SamplePerformanceData，避免每秒输出 60 条无效链路日志。
            if (IsLinkActive(StateSyncLinkName))
            {
                SamplePerformanceData(StateSyncLinkName);
            }
        }

        /// <summary>
        /// 采样所有链路的性能数据
        /// </summary>
        private void SampleAllLinks()
        {
            foreach (var linkName in _linkDataDict.Keys)
            {
                // 跳过有专门数据接收方法的链路，避免双重采样
                // 这些链路已经在 RecordEyeTrackingData() 和 RecordMoCapFrame() 中处理了
                if (linkName == StateSyncLinkName ||
                    linkName == "EyeTracking→PC" || linkName == "MoCap→PC" ||
                    linkName == "MoCap→VR" || linkName.StartsWith("MoCap→VR_Client"))
                {
                    continue;
                }

                SamplePerformanceData(linkName);
            }
        }

        #endregion

        private void OnDestroy()
        {
            // 仅在管理端处理录制功能
#if MANAGER_SERVER
            // 确保录制线程和资源被正确清理
            if (_isRecording)
            {
                StopRecording();
            }
            // 释放事件资源
            _recordingEvent?.Dispose();
#endif
        }

        #region 性能更新事件（零 GC 分配）

        /// <summary>
        /// 从接收线程安全地提交性能数据更新（零堆分配）
        /// 替代 MainThreadTaskQueue.EnqueueTask(() => OnPerformanceUpdated?.Invoke(dataCopy)) 模式
        /// </summary>
        private void EnqueuePerformanceUpdate(PerformanceData data)
        {
            lock (_pendingUpdateLock)
            {
                _pendingUpdateData = data;
                _hasPendingUpdate = true;
            }
        }

        #endregion

        #region 核心计算逻辑

        /// <summary>
        /// 采样并记录性能数据
        /// </summary>
        private void SamplePerformanceData()
        {
            SamplePerformanceData(_currentLink);
        }

        /// <summary>
        /// 采样并记录性能数据（指定链路）
        /// </summary>
        /// <param name="link">链路标识</param>
        public void SamplePerformanceData(string link)
        {
            var linkData = GetLinkData(link);

            // 检查链路是否活跃
            bool isActive = IsLinkActive(link);
            if (isActive)
            {
                linkData.currentSequenceNumber++;

                // 更新链路活动状态
                linkData.isActive = true;
                linkData.lastActiveDateTime = DateTime.Now;

                float latency = 0;
                float jitter = 0;
                float packetLoss = 0;
                float frameArrivalDelay = -1;
                float frameInterval = -1;
                float frameLossRate = -1;

                // 对于眼动UDP数据，使用不同的延迟计算方法
                if (link == StateSyncLinkName)
                {
                    // 状态同步链路使用NetworkTime.rtt
                    latency = CalculateLatency(link);
                    jitter = CalculateJitter(link, latency);
                    packetLoss = CalculatePacketLoss(link);
                }
                else
                {
                    // 其他链路使用默认的延迟计算方法
                    // 获取最近的帧到达延迟值
                    if (linkData.validCount > 0)
                    {
                        int lastIndex = (linkData.writeIndex - 1 + _bufferSize) % _bufferSize;
                        latency = linkData.historyBuffer[lastIndex].frameArrivalDelay;
                        jitter = linkData.historyBuffer[lastIndex].jitter;
                        packetLoss = linkData.historyBuffer[lastIndex].packetLoss;
                        frameArrivalDelay = linkData.historyBuffer[lastIndex].frameArrivalDelay;
                        frameInterval = linkData.historyBuffer[lastIndex].frameInterval;
                        frameLossRate = linkData.historyBuffer[lastIndex].frameLossRate;
                    }
                }

                // 计算延迟统计数据
                var (latencyMin, latencyMax, latencyMedian, latency95th, latency99th) = CalculateLatencyStatistics(link);

                // 调试日志
                if (IsShowLog)
                {
                    Debug.Log($"[NetworkPerformanceMonitor] Sampling {link}: latency={latency}ms, jitter={jitter}ms, packetLoss={packetLoss}%");
                    Debug.Log($"[NetworkPerformanceMonitor] NetworkTime.rtt={NetworkTime.rtt}");
                    Debug.Log($"[NetworkPerformanceMonitor] LinkData validCount={linkData.validCount}");
                    Debug.Log($"[NetworkPerformanceMonitor] Total packets: {linkData.totalPackets}, lost: {linkData.totalLostPackets}");
                    Debug.Log($"[NetworkPerformanceMonitor] Last sequence: {linkData.lastSequenceNumber}, current: {linkData.currentSequenceNumber}");
                    Debug.Log($"[NetworkPerformanceMonitor] Latency stats: min={latencyMin}ms, max={latencyMax}ms, median={latencyMedian}ms, 95th={latency95th}ms, 99th={latency99th}ms");
                }

                // 采集时间 - 使用服务器同步时间
                DateTime collectedTime = GetCompensatedDateTime();
                string collectedTimeStr = FormatTimestamp(collectedTime);

                // 发出时间 - 估算：采集时间 - 延迟
                ulong sendTimeMs;
                if (latency >= 0)
                {
                    DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
                    ulong collectedTimeMs = (ulong)(collectedTime - epoch).TotalMilliseconds;
                    sendTimeMs = collectedTimeMs - (ulong)Math.Max(0, latency);
                }
                else
                {
                    // 如果没有延迟数据，直接使用采集时间
                    DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
                    sendTimeMs = (ulong)(collectedTime - epoch).TotalMilliseconds;
                }

                UDPClockSync.ClockSyncSnapshot sync = UDPClockSync.GetSnapshot();
                bool isMirrorRtt = link == StateSyncLinkName;
                PerformanceData data = new PerformanceData
                {
                    sentTimestamp = sendTimeMs,
                    sampleTimestamp = collectedTimeStr,
                    latency = latency >= 0 ? latency : -1,
                    jitter = jitter >= 0 ? jitter : -1,
                    packetLoss = packetLoss >= 0 ? packetLoss : -1,
                    latencyMin = latencyMin,
                    latencyMax = latencyMax,
                    latencyMedian = latencyMedian,
                    latency95th = latency95th,
                    latency99th = latency99th,
                    frameArrivalDelay = frameArrivalDelay >= 0 ? frameArrivalDelay : -1,
                    frameInterval = frameInterval >= 0 ? frameInterval : -1,
                    frameLossRate = frameLossRate >= 0 ? frameLossRate : -1,
                    sequenceNumber = linkData.currentSequenceNumber,
                    link = link,
                    measurementType = isMirrorRtt ? "Mirror RTT" : "Sampled latency",
                    // RTT is a same-clock duration and remains valid without
                    // clock synchronization. The sync columns are still kept as
                    // diagnostics, but do not gate this metric.
                    syncValid = isMirrorRtt || sync.IsReady,
                    syncState = isMirrorRtt ? "Not required" : sync.State,
                    clockOffsetMs = sync.ClockDiff,
                    syncRttMs = sync.RoundTripTimeMs,
                    syncAgeMs = sync.AgeMs,
                    offsetUncertaintyMs = sync.OffsetUncertaintyMs,
                    syncSampleCount = sync.SampleCount
                };

                // 对所有链路都添加到历史记录和录制文件
                AddToHistoryWithRecording(link, data);
                linkData.lastSequenceNumber = linkData.currentSequenceNumber;

                // 触发性能数据更新事件（零 GC 分配，无需 lambda 闭包）
                EnqueuePerformanceUpdate(data);
            }
            else
            {
                // 未活跃链路不生成数据，避免无意义的数据占用内存
                Debug.Log($"[NetworkPerformanceMonitor] Link {link} is not active, skipping data collection");
            }
        }

        /// <summary>
        /// 计算网络延迟（毫秒）
        /// 使用Mirror的NetworkTime.rtt（往返时间，单位秒）转换为毫秒
        /// </summary>
        /// <param name="link">链路名称</param>
        /// <returns>延迟值（毫秒）</returns>
        private float CalculateLatency(string link)
        {
            // 在服务器上，需要从NetworkServer.connections获取客户端的rtt
            if (NetworkServer.active)
            {
                // 遍历所有客户端连接
                foreach (var conn in NetworkServer.connections.Values)
                {
                    // 只考虑有效连接且IP与服务器不同的客户端
                    if (conn != null && conn.address != "127.0.0.1")
                    {
                        // 获取客户端的rtt
                        float clientRtt = (float)conn.rtt;
                        if (clientRtt > 0)
                        {
                            if (IsShowLog)
                            {
                                Debug.Log($"[NetworkPerformanceMonitor] Client RTT: {clientRtt}s, Address: {conn.address}");
                            }
                            return clientRtt * 1000.0f; // 转换为毫秒
                        }
                    }
                }
                // 如果没有找到有效客户端连接，返回0
                return 0;
            }
            else
            {
                // 在客户端上，使用NetworkTime.rtt
                var rtt = NetworkTime.rtt;
                return rtt > 0 ? (float)(rtt * 1000.0) : 0;
            }
        }

        /// <summary>
        /// 计算网络抖动（毫秒）
        /// 使用延迟样本的标准差作为抖动值
        /// </summary>
        /// <param name="link">链路名称</param>
        /// <param name="newLatency">新的延迟样本</param>
        /// <returns>抖动值（毫秒）</returns>
        private float CalculateJitter(string link, float newLatency)
        {
            var linkData = GetLinkData(link);

            // 添加新样本到滑动窗口
            linkData.latencySamples[linkData.latencySampleIndex] = newLatency;
            linkData.latencySampleIndex = (linkData.latencySampleIndex + 1) % _jitterWindowSize;
            linkData.latencySampleCount = Mathf.Min(linkData.latencySampleCount + 1, _jitterWindowSize);

            if (linkData.latencySampleCount < 2)
                return 0f;

            // 计算平均值
            float sum = 0f;
            for (int i = 0; i < linkData.latencySampleCount; i++)
            {
                sum += linkData.latencySamples[i];
            }
            float mean = sum / linkData.latencySampleCount;

            // 计算方差
            float varianceSum = 0f;
            for (int i = 0; i < linkData.latencySampleCount; i++)
            {
                float diff = linkData.latencySamples[i] - mean;
                varianceSum += diff * diff;
            }
            float variance = varianceSum / linkData.latencySampleCount;

            // 标准差即为抖动
            return Mathf.Sqrt(variance);
        }

        /// <summary>
        /// 计算丢包率（百分比）
        /// 基于序列号检测丢包
        /// </summary>
        /// <param name="link">链路名称</param>
        /// <returns>丢包率（0-100）</returns>
        private float CalculatePacketLoss(string link)
        {
            var linkData = GetLinkData(link);
            linkData.totalPackets++;

            // 检测丢包：如果当前序列号比上次序列号大超过1，说明有丢包
            if (linkData.lastSequenceNumber > 0 && linkData.currentSequenceNumber > linkData.lastSequenceNumber + 1)
            {
                int lostCount = (int)(linkData.currentSequenceNumber - linkData.lastSequenceNumber - 1);
                linkData.totalLostPackets += lostCount;
            }

            if (linkData.totalPackets == 0)
                return 0f;

            return (float)linkData.totalLostPackets / linkData.totalPackets * 100f;
        }

        /// <summary>
        /// 计算帧到达延迟（毫秒）
        /// 使用服务器同步时间减去帧时间戳，得到帧到达延迟
        /// 
        /// 包含时钟偏移自动校正机制：持续观测原始延迟的负值比例，
        /// 动态修正 NTP 同步残差和 VR 端时钟漂移，避免负延迟被钳为 0 导致数据失真。
        /// </summary>
        /// <param name="frameTimestamp">帧时间戳（发送时间，Unix时间戳，毫秒）</param>
        /// <param name="arrivalTimeMs">数据包到达时间（Unix毫秒时间戳，含小数精度。0表示使用当前时间）</param>
        /// <param name="applyClockDiff">是否应用 UDPClockSync.ClockDiff</param>
        /// <returns>帧到达延迟（毫秒）</returns>
        private float CalculateFrameArrivalDelay(
            ulong frameTimestamp,
            double arrivalTimeMs = 0,
            bool applyClockDiff = true,
            UDPClockSync.ClockSyncSnapshot? synchronization = null)
        {
            // 使用传入的到达时间（在回调入口捕获），或回退到当前时间
            double receiveTimeD = arrivalTimeMs > 0
                ? arrivalTimeMs
                : UDPClockSync.GetLocalUnixTimeMsD();

            UDPClockSync.ClockSyncSnapshot sync = synchronization ?? UDPClockSync.GetSnapshot();

            // A directional cross-device delay is undefined until clock
            // synchronization is stable and fresh. Never manufacture a plausible
            // value from an unknown clock offset.
            if (applyClockDiff && !sync.IsReady)
            {
                return -1f;
            }

            // 应用 UDP 双向时钟同步测量的时钟差
            // ClockDiff = VR - PC（正值=VR快）
            // applyClockDiff=false: 发送端与本机同设备（MoCap→PC），不需要校正
            // applyClockDiff=true 且 PC 端: 接收 VR 数据，VR→PC: PC = VR - ClockDiff
            // applyClockDiff=true 且 VR 端: 接收 PC 数据，PC→VR: VR = PC + ClockDiff
            double adjustedFrameTimestamp = (double)frameTimestamp;
            if (applyClockDiff)
            {
#if MANAGER_SERVER
                // PC 端：接收 VR 数据，VR→PC: PC = VR - ClockDiff
                adjustedFrameTimestamp = (double)frameTimestamp - sync.ClockDiff;
#else
                // VR 端：接收 PC 数据，PC→VR: VR = PC + ClockDiff
                adjustedFrameTimestamp = (double)frameTimestamp + sync.ClockDiff;
#endif
            }

            double delayD = receiveTimeD - adjustedFrameTimestamp;

            float delay = (float)delayD;

            if (IsShowLog)
            {
                Debug.Log($"[NetworkPerformanceMonitor] Frame Arrival Delay: {delay}ms, Receive Time: {receiveTimeD}ms, Frame Timestamp: {frameTimestamp}ms, ClockDiff: {sync.ClockDiff}ms, SyncState: {sync.State}");
            }

            // 负延迟处理：Raw 输出实际值，Processed 取绝对值
            return _negativeLatencyMode == NegativeLatencyMode.Raw
                ? delay
                : (delay < 0f ? -delay : delay);
        }

        /// <summary>
        /// 计算帧间隔（毫秒）
        /// </summary>
        /// <param name="link">链路名称</param>
        /// <param name="frameTimestamp">帧时间戳（Unix时间戳，毫秒）</param>
        /// <returns>帧间隔（毫秒）</returns>
        private float CalculateFrameInterval(string link, ulong frameTimestamp)
        {
            var linkData = GetLinkData(link);

            // 使用一个单独的字段来存储上一帧的Unix时间戳
            if (!linkData.TryGetLastUnixTimestamp(out ulong lastUnixTimestamp))
            {
                linkData.SetLastUnixTimestamp(frameTimestamp);
                return 0f;
            }

            // 检查是否是重复时间戳（由于时间戳精度或网络问题）
            if (frameTimestamp == lastUnixTimestamp)
            {
                // 如果是重复时间戳，不更新lastUnixTimestamp，说明这是一个重复数据
                return 0f;
            }

            // 检查时间戳是否合理（防止数值下溢）
            // 如果当前帧时间戳小于上一帧时间戳，说明时间戳异常
            if (frameTimestamp < lastUnixTimestamp)
            {
                Debug.LogWarning($"[NetworkPerformanceMonitor] 检测到异常时间戳顺序！当前: {frameTimestamp}, 上一帧: {lastUnixTimestamp}");
                // 重置上一帧时间戳，避免后续计算持续异常
                linkData.SetLastUnixTimestamp(frameTimestamp);
                return 0f;
            }

            // 计算帧间隔：当前帧时间戳减去上一帧时间戳
            // 使用有符号整数相减防止溢出，并检查结果是否合理
            long intervalLong = (long)frameTimestamp - (long)lastUnixTimestamp;

            // 检查帧间隔是否合理（假设最大合理帧间隔为10秒）
            if (intervalLong < 0 || intervalLong > 10000)
            {
                Debug.LogWarning($"[NetworkPerformanceMonitor] 检测到异常帧间隔！间隔: {intervalLong}ms, 当前: {frameTimestamp}, 上一帧: {lastUnixTimestamp}");
                linkData.SetLastUnixTimestamp(frameTimestamp);
                return 0f;
            }

            float interval = (float)intervalLong;
            linkData.SetLastUnixTimestamp(frameTimestamp);

            return interval;
        }

        /// <summary>
        /// 计算帧抖动（毫秒）
        /// </summary>
        /// <param name="link">链路名称</param>
        /// <param name="frameInterval">帧间隔</param>
        /// <returns>帧抖动（毫秒）</returns>
        private float CalculateFrameJitter(string link, float frameInterval)
        {
            var linkData = GetLinkData(link);

            // 如果帧间隔是0或负数，表示这是一个异常数据，跳过此帧的抖动计算
            if (frameInterval <= 0)
            {
                // 返回之前计算的抖动值，保持连续性
                if (linkData.frameIntervalSampleCount >= 2)
                {
                    // 重新计算之前窗口的抖动值
                    return CalculateJitterFromSamples(linkData.frameIntervalSamples, linkData.frameIntervalSampleCount);
                }
                return 0f;
            }

            // 检查帧间隔是否合理（假设最大合理帧间隔为1秒，最小为1ms）
            // 眼动数据通常频率较高，帧间隔应该比较小
            if (frameInterval < 1f || frameInterval > 1000f)
            {
                Debug.LogWarning($"[NetworkPerformanceMonitor] 检测到异常帧间隔值！间隔: {frameInterval}ms，不加入抖动计算");
                // 返回之前计算的抖动值，保持连续性
                if (linkData.frameIntervalSampleCount >= 2)
                {
                    return CalculateJitterFromSamples(linkData.frameIntervalSamples, linkData.frameIntervalSampleCount);
                }
                return 0f;
            }

            // 添加新样本到滑动窗口
            linkData.frameIntervalSamples[linkData.frameIntervalSampleIndex] = frameInterval;
            linkData.frameIntervalSampleIndex = (linkData.frameIntervalSampleIndex + 1) % _jitterWindowSize;
            linkData.frameIntervalSampleCount = Mathf.Min(linkData.frameIntervalSampleCount + 1, _jitterWindowSize);

            if (linkData.frameIntervalSampleCount < 2)
                return 0f;

            return CalculateJitterFromSamples(linkData.frameIntervalSamples, linkData.frameIntervalSampleCount);
        }

        /// <summary>
        /// 从样本数组计算抖动值
        /// </summary>
        /// <param name="samples">样本数组</param>
        /// <param name="count">有效样本数量</param>
        /// <returns>抖动值（标准差）</returns>
        private float CalculateJitterFromSamples(float[] samples, int count)
        {
            // 计算平均值
            float sum = 0f;
            for (int i = 0; i < count; i++)
            {
                sum += samples[i];
            }
            float mean = sum / count;

            // 计算方差
            float varianceSum = 0f;
            for (int i = 0; i < count; i++)
            {
                float diff = samples[i] - mean;
                varianceSum += diff * diff;
            }
            float variance = varianceSum / count;

            // 标准差即为抖动
            return Mathf.Sqrt(variance);
        }

        /// <summary>
        /// 计算丢帧率（百分比）
        /// </summary>
        /// <param name="link">链路名称</param>
        /// <param name="sequence">帧序列号</param>
        /// <returns>丢帧率（0-100）</returns>
        private float CalculateFrameLossRate(string link, uint sequence)
        {
            var linkData = GetLinkData(link);
            linkData.totalFrames++;

            // 基于帧序列号检测丢帧
            if (linkData.lastSequenceNumber > 0)
            {
                // 计算序列号差值，差值减1即为丢失的帧数
                uint sequenceDiff = sequence - linkData.lastSequenceNumber;
                if (sequenceDiff > 1)
                {
                    int lostFrames = (int)(sequenceDiff - 1);
                    linkData.totalLostFrames += lostFrames;
                }
            }

            if (linkData.totalFrames == 0)
                return 0f;

            return (float)linkData.totalLostFrames / linkData.totalFrames * 100f;
        }

        #endregion

        #region 同步时间

        /// <summary>
        /// 将 UTC DateTime 转换为 UTC+8（中国标准时间）格式的字符串
        /// 统一使用此时区格式化，不依赖运行设备的本地时区设置（VR 端可能不在 UTC+8 时区）
        /// </summary>
        /// <param name="utcDateTime">UTC 时间（Kind=Utc）</param>
        /// <returns>UTC+8 格式的时间字符串 "yyyy-MM-dd HH:mm:ss.fff"</returns>
        private static string FormatTimestamp(DateTime utcDateTime)
        {
            return utcDateTime.AddHours(8).ToString("yyyy-MM-dd HH:mm:ss.fff");
        }

        /// <summary>
        /// 获取当前系统时间的Unix时间戳（毫秒）
        /// </summary>
        /// <returns>Unix时间戳（毫秒）</returns>
        private ulong GetCurrentUnixTimeMilliseconds()
        {
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return (ulong)(DateTime.Now.ToUniversalTime() - epoch).TotalMilliseconds;
        }

        /// <summary>
        /// 获取当前时间的 DateTime（统一使用 UDPClockSync 的 Stopwatch 时间源）
        /// </summary>
        /// <param name="clockDiffMs">时钟偏移补偿（毫秒），0=不补偿</param>
        /// <returns>补偿后的 DateTime</returns>
        private DateTime GetCompensatedDateTime(double clockDiffMs = 0.0)
        {
            double localMs = UDPClockSync.GetLocalUnixTimeMsD() + clockDiffMs;
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return epoch.AddMilliseconds(localMs);
        }

        /// <summary>
        /// 把传入的 Unix 时间戳转换为 DateTime 对象（毫秒）
        /// </summary>
        /// <param name="unixTimeMs">Unix 时间戳（毫秒）</param>
        /// <returns>DateTime 对象</returns>
        private DateTime UnixTimeMillisecondsToDateTime(ulong unixTimeMs)
        {
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return epoch.AddMilliseconds(unixTimeMs);
        }

        /// <summary>
        /// 获取补偿后的Unix时间戳（毫秒），用于外部调用
        /// </summary>
        /// <returns>补偿后的Unix时间戳（毫秒）</returns>
        public static ulong GetSyncedUnixTimeMilliseconds()
        {
            return SystemClockSync.GetServerSystemTimeMs();
        }

        /// <summary>
        /// 获取当前 Unix 时间戳（PC 本机时间，含小数精度），用于外部调用
        /// </summary>
        public static double GetSyncedUnixTimeMillisecondsD()
        {
            return UDPClockSync.GetLocalUnixTimeMsD();
        }

        #endregion

        #region 序列号管理

        /// <summary>
        /// 手动更新序列号（用于与实际网络消息同步）
        /// </summary>
        /// <param name="sequenceNumber">接收到的序列号</param>
        /// <param name="link">链路名称（null 表示当前链路）</param>
        public void UpdateSequenceNumber(uint sequenceNumber, string link = null)
        {
            link = link ?? _currentLink;
            var linkData = GetLinkData(link);
            linkData.currentSequenceNumber = sequenceNumber;
        }

        /// <summary>
        /// 记录接收到的数据包（用于更准确的丢包检测）
        /// </summary>
        /// <param name="sequenceNumber">数据包序列号</param>
        /// <param name="link">链路名称（null 表示当前链路）</param>
        public void RecordPacketReceived(uint sequenceNumber, string link = null)
        {
            link = link ?? _currentLink;
            var linkData = GetLinkData(link);

            linkData.totalPackets++;

            if (linkData.lastSequenceNumber > 0 && sequenceNumber > linkData.lastSequenceNumber + 1)
            {
                int lostCount = (int)(sequenceNumber - linkData.lastSequenceNumber - 1);
                linkData.totalLostPackets += lostCount;
            }

            linkData.lastSequenceNumber = sequenceNumber;
        }

        #endregion

        #region 外部接口方法

        /// <summary>
        /// 处理 PC<->VR 状态同步数据
        /// </summary>
        public void OnStateSyncData()
        {
            SetCurrentLink(StateSyncLinkName);
            SamplePerformanceData(StateSyncLinkName);
        }

        /// <summary>
        /// 标记事件开始
        /// </summary>
        public void MarkEventStart()
        {
            SetCurrentLink(StateSyncLinkName);
        }

        /// <summary>
        /// 标记事件开始（带事件ID）
        /// </summary>
        /// <param name="eventId">事件ID</param>
        public void MarkEventStart(string eventId)
        {
            SetCurrentLink(StateSyncLinkName);
        }

        /// <summary>
        /// 标记事件结束
        /// </summary>
        /// <returns>事件同步延迟</returns>
        public float MarkEventEnd()
        {
            SetCurrentLink(StateSyncLinkName);
            return 0f;
        }

        /// <summary>
        /// 标记事件结束（带事件ID）
        /// </summary>
        /// <param name="eventId">事件ID</param>
        /// <returns>事件同步延迟</returns>
        public float MarkEventEnd(string eventId)
        {
            SetCurrentLink(StateSyncLinkName);
            return 0f;
        }

        #endregion

    }
}
