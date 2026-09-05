using System;
using MetaQuestProEyeGazeUDP;
using Mirror;
using UnityEngine;

namespace GameMain
{
    /// <summary>
    /// 动作捕捉网络性能监控
    /// </summary>
    public partial class NetworkPerformanceMonitor : MonoBehaviour
    {
        #region 动捕网络

        /// <summary>
        /// 处理动作捕捉帧接收事件（无参数版本，不推荐使用）
        /// </summary>
        public void OnMoCapFrameReceived()
        {
            // 这个方法不推荐使用，因为没有sequence和timestamp参数，无法正确计算延迟和丢包率
            // 请使用带参数的 OnMoCapFrameReceived(uint sequence, ulong timestamp) 版本
            SetCurrentLink("MoCap→PC");
            Debug.LogWarning("[NetworkPerformanceMonitor] 建议使用带参数的 OnMoCapFrameReceived(uint sequence, ulong timestamp) 方法");
        }

        /// <summary>
        /// 处理动作捕捉帧接收事件（带参数）
        /// </summary>
        /// <param name="sequence">帧序列号</param>
        /// <param name="timestamp">帧时间戳（浮点数版本，为了兼容性）</param>
        public void OnMoCapFrameReceived(uint sequence, float timestamp)
        {
            SetCurrentLink("MoCap→PC");
            Debug.LogWarning("[NetworkPerformanceMonitor] 建议使用带 ulong 类型 timestamp 的 OnMoCapFrameReceived(uint sequence, ulong timestamp) 方法");
        }

#if MANAGER_SERVER

        /// <summary>
        /// 处理 MoCap→PC 动捕帧接收事件
        /// </summary>
        /// <param name="sequence">帧序列号</param>
        /// <param name="timestamp">帧时间戳（发送时间）The FrameGroup timestamp, the number of milliseconds since the Epoch 1970-01-01 00:00:00 +0000 (UTC)</param>
        /// <param name="fLatency">动捕软件处理延迟（捕获到发送的时间差，单位：毫秒）The host defined time delta between capture and send</param>
        public void OnMoCapToPCFrameReceived(uint sequence, ulong timestamp, float fLatency = 0f)
        {
            SetCurrentLink("MoCap→PC");
            RecordMoCapFrame("MoCap→PC", sequence, timestamp, fLatency, false);
        }

#else

        /// <summary>
        /// 处理 MoCap→VR 动捕帧接收事件
        /// </summary>
        /// <param name="sequence">帧序列号</param>
        /// <param name="timestamp">帧时间戳（发送时间）The FrameGroup timestamp, the number of milliseconds since the Epoch 1970-01-01 00:00:00 +0000 (UTC)</param>
        /// <param name="fLatency">动捕软件处理延迟（捕获到发送的时间差，单位：毫秒）The host defined time delta between capture and send</param>
        public void OnMoCapToVRFrameReceived(uint sequence, ulong timestamp, float fLatency = 0f)
        {
            SetCurrentLink("MoCap→VR");
            RecordMoCapFrame("MoCap→VR", sequence, timestamp, fLatency);
        }

#endif

        /// <summary>
        /// 记录动捕帧数据
        /// </summary>
        /// <param name="link">链路名称</param>
        /// <param name="sequence">帧序列号</param>
        /// <param name="timestamp">帧时间戳（发送时间）The FrameGroup timestamp, the number of milliseconds since the Epoch 1970-01-01 00:00:00 +0000 (UTC)</param>
        /// <param name="fLatency">动捕软件处理延迟（捕获到发送的时间差，单位：毫秒）The host defined time delta between capture and send</param>
        /// <param name="mocapApplyClockDiff">是否应用时钟偏移补偿，默认true，
        ///  MoCap→PC: 动捕服务器与 PC 同机，时间戳已在 PC 时钟域，不需要 ClockDiff
        ///  MoCap→VR: 动捕服务器在 PC 上，VR 需要用 ClockDiff 校正 PC 时间戳到 VR 时间域</param>
        private void RecordMoCapFrame(string link, uint sequence, ulong timestamp, float fLatency = 0f, bool mocapApplyClockDiff = true)
        {
            var linkData = GetLinkData(link);

            // 检测是否是新会话的开始
            bool isNewSession = false;
            // 判断条件：
            // 1. 序列号从1开始，或者
            // 2. 链路之前不活跃，或者
            // 3. 当前序列号比上次序列号小（可能重制了），或者
            // 4. 当前序列号比上次序列号大超过1000（异常跳跃）
            if (sequence == 1 ||
                !linkData.isActive ||
                (linkData.lastSequenceNumber > 0 && sequence < linkData.lastSequenceNumber) ||
                (linkData.lastSequenceNumber > 0 && sequence > linkData.lastSequenceNumber + 1000))
            {
                // 重置链路数据，确保新会话从干净状态开始
                linkData.Reset();
                // 关键修复：直接设置lastSequenceNumber为当前序列号，避免第一个数据计算错误的丢包
                linkData.lastSequenceNumber = sequence;
                linkData.currentSequenceNumber = sequence;
                isNewSession = true;
                Debug.Log($"[NetworkPerformanceMonitor] 新会话开始，重置链路数据: {link}, 初始序列号: {sequence}");
            }

            // 无条件标记链路为活跃（只要接收到数据，链路就是活跃的）
            linkData.isActive = true;
            linkData.lastActiveDateTime = DateTime.Now;

            linkData.currentSequenceNumber = sequence;

            // 如果是新会话，直接跳过丢包计算，只更新最后序列号
            float frameArrivalDelay, frameInterval, frameJitter, frameLossRate, packetLoss;
            if (isNewSession)
            {
                // 只计算延迟，不计算丢包率
                frameArrivalDelay = CalculateFrameArrivalDelay(timestamp, 0, mocapApplyClockDiff);
                frameInterval = CalculateFrameInterval(link, timestamp);
                frameJitter = CalculateFrameJitter(link, frameInterval);
                // 新会话，丢包率和丢帧率都为0
                frameLossRate = 0f;
                packetLoss = 0f;
            }
            else
            {
                // 正常计算所有指标
                frameArrivalDelay = CalculateFrameArrivalDelay(timestamp, 0, mocapApplyClockDiff);
                frameInterval = CalculateFrameInterval(link, timestamp);
                frameJitter = CalculateFrameJitter(link, frameInterval);
                frameLossRate = CalculateFrameLossRate(link, sequence);
                packetLoss = CalculatePacketLoss(link);
            }

            // 叠加动捕软件处理延迟（FLatency: 捕获到发送的时间差）
            // CalculateFrameArrivalDelay 仅计算了网络传输时间（发送→到达），
            // 需要加上 FLatency（捕获→发送）才是完整的捕获→到达延迟
            // FLatency 单位为毫秒
            float fLatencyMs = fLatency;
            frameArrivalDelay += fLatencyMs;

            // sentTimestamp: 发送端 PC 时钟域，后面会返回给发送端，就不用转换了，直接用 PC 时间
            var mocapSentTimestamp = timestamp;

            // 采集时间戳：转换为PC时钟域
            // MoCap→PC: 同机，直接用 PC 时间
            // MoCap→VR: PC时间 = VR时间 - ClockDiff (VR - PC)
            // 即 VR 时间 = PC 时间 + ClockDiff（正值表示 VR 快，负值表示 VR 慢）
            var sampleTimestamp = GetCompensatedDateTime(mocapApplyClockDiff
                ? -UDPClockSync.ClockDiff
                : 0);

            // 格式化时间戳字符串一次，复用给 tempData 和 data（避免重复 ToString 产生 GC 垃圾）
            string sampleTimestampStr = FormatTimestamp(sampleTimestamp);

            // 先临时添加当前延迟到历史（不保存文件），以便统计计算包含当前数据
            PerformanceData tempData = new PerformanceData
            {
                sentTimestamp = mocapSentTimestamp,
                sampleTimestamp = sampleTimestampStr,
                latency = frameArrivalDelay >= 0 ? frameArrivalDelay : -1,
                jitter = frameJitter >= 0 ? frameJitter : -1,
                packetLoss = packetLoss >= 0 ? packetLoss : -1,
                latencyMin = -1,
                latencyMax = -1,
                latencyMedian = -1,
                latency95th = -1,
                latency99th = -1,
                frameArrivalDelay = frameArrivalDelay >= 0 ? frameArrivalDelay : -1,
                frameInterval = frameInterval >= 0 ? frameInterval : -1,
                frameLossRate = frameLossRate >= 0 ? frameLossRate : -1,
                sequenceNumber = sequence,
                link = link
            };

            // 第1步：添加临时数据到历史缓冲区
            // 先添加到历史缓冲区（不调用 AddToHistoryWithRecording 以避免重复写入）
            AddToHistory(link, tempData);

            // 第2步：计算基于历史数据的统计信息（包含刚才写入的临时数据）
            // 计算基于帧到达延迟的统计数据（包含当前数据）
            var (latencyMin, latencyMax, latencyMedian, latency95th, latency99th) = CalculateFrameArrivalDelayStatistics(link);

            // 创建完整的性能数据对象
            PerformanceData data = new PerformanceData
            {
                sentTimestamp = mocapSentTimestamp,
                sampleTimestamp = sampleTimestampStr,
                // 目前就应用帧到达延迟
                latency = frameArrivalDelay >= 0 ? frameArrivalDelay : -1,
                jitter = frameJitter >= 0 ? frameJitter : -1,
                packetLoss = packetLoss >= 0 ? packetLoss : -1,
                latencyMin = latencyMin,
                latencyMax = latencyMax,
                latencyMedian = latencyMedian,
                latency95th = latency95th,
                latency99th = latency99th,
                frameArrivalDelay = frameArrivalDelay >= 0 ? frameArrivalDelay : -1,
                frameInterval = frameInterval >= 0 ? frameInterval : -1,
                frameLossRate = frameLossRate >= 0 ? frameLossRate : -1,
                sequenceNumber = sequence,
                link = link
            };

            // 第3步：用完整数据替换刚才的临时数据
            // 更新历史缓冲区中的数据
            int lastIndex = (linkData.writeIndex - 1 + _bufferSize) % _bufferSize;
            linkData.historyBuffer[lastIndex] = data;
            linkData.lastSequenceNumber = sequence;

            // 现在将完整数据（包含统计信息）添加到录制文件
            if (_isRecording)
            {
                WriteToRecordingFile(data);
            }

            // 调试日志
            if (IsShowLog)
            {
                Debug.Log($"[NetworkPerformanceMonitor] MoCap Frame {link}: seq={sequence},  ClockDiff={UDPClockSync.ClockDiff}ms, arrivalDelay={frameArrivalDelay:F2}ms (fLatency={fLatencyMs:F2}ms + transit={frameArrivalDelay - fLatencyMs:F2}ms), interval={frameInterval:F2}ms, jitter={frameJitter:F2}ms, lossRate={frameLossRate:F2}%");
            }
            // 触发性能数据更新事件（零 GC 分配，无需 lambda 闭包）
            EnqueuePerformanceUpdate(data);
        }
        #endregion


        #region 数据上报

        #region VR端发送

        /// <summary>
        /// 注册性能数据上报消息处理器
        /// </summary>
        private void RegisterPerformanceReportHandlers()
        {
#if MANAGER_SERVER
            // 管理端：注册接收批量性能数据的消息处理器
            NetworkServer.RegisterHandler<BatchPerformanceDataReportMsg>(OnBatchPerformanceDataReportReceived);
            Debug.Log("[NetworkPerformanceMonitor] 管理端已注册性能数据上报消息处理器");
#endif
        }

#if !MANAGER_SERVER
        /// <summary>
        /// 定期上报性能数据到管理端（VR端）
        /// </summary>
        private void UpdatePerformanceReporting()
        {
            // 检查监控是否启用且网络已连接，不满足条件则直接返回
            if (!_isEnabled || !NetworkClient.isConnected)
                return;

            // 检查是否达到上报间隔时间
            if (Time.time - _lastReportTime >= _reportInterval)
            {
                // 获取 MoCap→VR 链路的数据对象
                var linkData = GetLinkData("MoCap→VR");
                // 检查链路是否活跃且有有效数据
                if (linkData.isActive && linkData.validCount > 0)
                {
                    // 计算需要发送的数据数量
                    int dataToSendCount = 0;
                    if (linkData.writeIndex >= linkData.lastReportedIndex)
                    {
                        // 缓冲区没有环绕的情况
                        dataToSendCount = linkData.writeIndex - linkData.lastReportedIndex;
                    }
                    else
                    {
                        // 缓冲区已环绕的情况
                        dataToSendCount = (_bufferSize - linkData.lastReportedIndex) + linkData.writeIndex;
                    }

                    // 确保不超过有效数据数量
                    dataToSendCount = Mathf.Min(dataToSendCount, linkData.validCount);

                    if (dataToSendCount > 0)
                    {
                        // 收集所有未发送的数据
                        PerformanceData[] dataArray = new PerformanceData[dataToSendCount];
                        int currentIndex = linkData.lastReportedIndex;
                        for (int i = 0; i < dataToSendCount; i++)
                        {
                            dataArray[i] = linkData.historyBuffer[currentIndex];
                            currentIndex = (currentIndex + 1) % _bufferSize;
                        }

                        // 批量发送数据
                        SendBatchPerformanceDataToServer(dataArray);

                        // 更新上次上报的索引位置
                        linkData.lastReportedIndex = linkData.writeIndex;
                    }
                }
                // 更新上次上报时间
                _lastReportTime = Time.time;
            }
        }

        /// <summary>
        /// 批量发送性能数据到管理端（VR端）
        /// </summary>
        /// <param name="dataArray">性能数据数组</param>
        private void SendBatchPerformanceDataToServer(PerformanceData[] dataArray)
        {
            if (!NetworkClient.isConnected)
            {
                Debug.LogWarning("[NetworkPerformanceMonitor] 网络未连接，无法发送批量性能数据");
                return;
            }

            if (dataArray == null || dataArray.Length == 0)
            {
                return;
            }

            // 使用网络连接的哈希码作为客户端标识
            string clientId = NetworkClient.connection.GetHashCode().ToString();
            var msg = new BatchPerformanceDataReportMsg(dataArray, clientId);
            NetworkClient.Send(msg);
            // 打印最新数据的统计信息
            var lastData = dataArray[dataArray.Length - 1];
            Debug.Log($"[NetworkPerformanceMonitor] 已批量发送{dataArray.Length}条MoCap→VR性能数据到管理端 (clientId={clientId})" +
                     $"最新延迟={lastData.latency:F2}ms, 丢包率={lastData.packetLoss:F2}%, " +
                     $"最新采集时间={lastData.sampleTimestamp}, 最新发送时间={lastData.sentTimestamp:F2}ms, " +
                     $"延迟统计[min={lastData.latencyMin:F2}, max={lastData.latencyMax:F2}, median={lastData.latencyMedian:F2}]");
        }
#endif
        #endregion


        #region 管理端接收


#if MANAGER_SERVER

        /// <summary>
        /// 处理来自VR客户端的批量性能数据上报（管理端）
        /// 直接使用VR端计算的统计数据，不重新计算
        /// </summary>
        /// <param name="conn">网络连接</param>
        /// <param name="msg">批量性能数据消息</param>
        private void OnBatchPerformanceDataReportReceived(NetworkConnection conn, BatchPerformanceDataReportMsg msg)
        {
            try
            {
                if (msg.dataArray == null || msg.dataArray.Length == 0)
                {
                    return;
                }

                // 目前只有一个端，所以链路名称为 MoCap→VR
                string clientLink = $"MoCap→VR";
                var linkData = GetLinkData(clientLink);

                for (int i = 0; i < msg.dataArray.Length; i++)
                {
                    var data = msg.dataArray[i];
                    data.link = clientLink;

                    // 更新链路活动状态
                    UpdateLinkActivity(clientLink);

                    // 直接使用VR端计算的统计数据，不重新计算
                    // VR端已经计算了 latency、packetLoss、frameLossRate、jitter 等
                    // 以及 latencyMin、latencyMax、latencyMedian、latency95th、latency99th

                    // 在 PC 端重建采集时间（UTC+8），不依赖 VR 端发来的时间字符串
                    // VR 端可能未更新代码，发来的是 UTC 时间字符串
                    // sampleTimestamp ≈ sentTimestamp + latency（误差为 fLatency，通常 <10ms）
                    if (data.sentTimestamp > 0 && data.latency >= 0)
                    {
                        double sampleMs = (double)data.sentTimestamp + data.latency;
                        DateTime sampleDt = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(sampleMs);
                        data.sampleTimestamp = FormatTimestamp(sampleDt);
                    }

                    // 直接添加到历史缓冲区（VR端数据已经是完整的）
                    AddToHistory(clientLink, data);

                    // 更新链路序列号信息
                    linkData.lastSequenceNumber = data.sequenceNumber;
                    linkData.currentSequenceNumber = data.sequenceNumber;

                    // 现在将完整数据（包含VR端计算的统计信息）添加到录制文件
                    if (_isRecording)
                    {
                        WriteToRecordingFile(data);
                    }

                    // 触发更新事件（零 GC 分配，无需 lambda 闭包；批量数据只保留最新值）
                    EnqueuePerformanceUpdate(data);
                }

                // 输出调试信息
                var lastData = msg.dataArray[msg.dataArray.Length - 1];
                Debug.Log($"[NetworkPerformanceMonitor] 已批量接收{msg.dataArray.Length}条MoCap→VR性能数据 (clientId={msg.clientId}), " +
                         $"最新延迟={lastData.latency:F2}ms, 丢包率={lastData.packetLoss:F2}%, " +
                         $"最新采集时间={lastData.sampleTimestamp}, 最新发送时间={lastData.sentTimestamp:F2}ms, " +
                         $"延迟统计[min={lastData.latencyMin:F2}, max={lastData.latencyMax:F2}, median={lastData.latencyMedian:F2}]");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[NetworkPerformanceMonitor] 处理批量性能数据上报失败: {e.Message}\n{e.StackTrace}");
            }
        }
#endif

        #endregion
        #endregion


    }
}
