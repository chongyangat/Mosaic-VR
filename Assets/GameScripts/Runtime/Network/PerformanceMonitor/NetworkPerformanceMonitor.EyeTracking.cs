using System;
using System.Collections.Generic;
using MetaQuestProEyeGazeUDP;
using UnityEngine;

namespace GameMain
{
    /// <summary>
    /// 动作捕捉网络性能监控
    /// </summary>
    public partial class NetworkPerformanceMonitor : MonoBehaviour
    {
        #region 眼动网络

        /// <summary>
        /// 处理眼动UDP数据接收事件（无参数版本，不推荐使用）
        /// </summary>
        public void OnEyeTrackingDataReceived()
        {
            // 这个方法不推荐使用，因为没有sequence和timestamp参数，无法正确计算延迟和丢包率
            // 请使用带参数的 OnEyeTrackingDataReceived(uint sequence, ulong timestamp) 版本
            SetCurrentLink("EyeTracking→PC");
            Debug.LogWarning("[NetworkPerformanceMonitor] 建议使用带参数的 OnEyeTrackingDataReceived(uint sequence, ulong timestamp) 方法");
        }

        /// <summary>
        /// 处理眼动UDP数据接收事件（带参数）（非主线程调用）
        /// </summary>
        /// <param name="sequence">数据序列号</param>
        /// <param name="timestamp">数据时间戳（发送时间）</param>
        public void OnEyeTrackingDataReceived(uint sequence, ulong timestamp)
        {
            OnEyeTrackingDataReceived(sequence, timestamp, 0);
        }

        /// <summary>
        /// 处理眼动UDP数据接收事件（带参数和到达时间）（非主线程调用）
        /// </summary>
        /// <param name="sequence">数据序列号</param>
        /// <param name="timestamp">数据时间戳（发送时间）</param>
        /// <param name="arrivalTimeMs">数据包到达时间（Unix毫秒时间戳，含小数精度。0表示未提供）</param>
        public void OnEyeTrackingDataReceived(uint sequence, ulong timestamp, double arrivalTimeMs)
        {
            SetCurrentLink("EyeTracking→PC");
            RecordEyeTrackingData("EyeTracking→PC", sequence, timestamp, arrivalTimeMs);
        }

        /// <summary>
        /// 记录眼动数据
        /// </summary>
        private void RecordEyeTrackingData(string link, uint sequence, ulong timestamp, double arrivalTimeMs)
        {
            var linkData = GetLinkData(link);
            UDPClockSync.ClockSyncSnapshot sync = UDPClockSync.GetSnapshot();

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

            linkData.isActive = true;
            linkData.lastActiveDateTime = DateTime.Now;

            linkData.currentSequenceNumber = sequence;

            // 如果是新会话，直接跳过丢包计算，只更新最后序列号
            float dataArrivalDelay, dataInterval, dataJitter, dataLossRate, packetLoss;
            if (isNewSession)
            {
                // 只计算延迟，不计算丢包率
                dataArrivalDelay = CalculateFrameArrivalDelay(timestamp, arrivalTimeMs, true, sync);
                dataInterval = CalculateFrameInterval(link, timestamp);
                dataJitter = CalculateFrameJitter(link, dataInterval);
                // 新会话，丢包率和丢帧率都为0
                dataLossRate = 0f;
                packetLoss = 0f;
            }
            else
            {
                // 正常计算所有指标
                dataArrivalDelay = CalculateFrameArrivalDelay(timestamp, arrivalTimeMs, true, sync);
                dataInterval = CalculateFrameInterval(link, timestamp);
                dataJitter = CalculateFrameJitter(link, dataInterval);
                dataLossRate = CalculateFrameLossRate(link, sequence);
                packetLoss = CalculatePacketLoss(link);
            }

            // Keep the sender time comparable to the PC receive time only while
            // synchronization is measurement-ready. Unsynchronized rows retain
            // their original Quest timestamp and are explicitly marked invalid.
            ulong sentTimestamp = sync.IsReady
                ? (ulong)Math.Max(0d, (double)timestamp - sync.ClockDiff)
                : 0UL;
            // sampleTimestamp = 采集时间（数据到达PC的接收时间，与CalculateFrameArrivalDelay使用同一到达时间）
            double receiveTimeMs = arrivalTimeMs > 0 ? arrivalTimeMs : UDPClockSync.GetLocalUnixTimeMsD();
            var sampleTimestamp = UnixTimeMillisecondsToDateTime((ulong)receiveTimeMs);

            string sampleTimestampStr = FormatTimestamp(sampleTimestamp);

            // 格式化时间戳字符串一次，复用给 tempData 和 data（避免重复 ToString 产生 GC 垃圾）
            // 先临时添加当前延迟到历史（不保存文件），以便统计计算包含当前数据
            PerformanceData tempData = new PerformanceData
            {
                sentTimestamp = sentTimestamp,
                sampleTimestamp = sampleTimestampStr,
                latency = dataArrivalDelay >= 0 ? dataArrivalDelay : -1,
                jitter = dataJitter >= 0 ? dataJitter : -1,
                packetLoss = packetLoss >= 0 ? packetLoss : -1,
                latencyMin = -1,
                latencyMax = -1,
                latencyMedian = -1,
                latency95th = -1,
                latency99th = -1,
                frameArrivalDelay = dataArrivalDelay >= 0 ? dataArrivalDelay : -1,
                frameInterval = dataInterval >= 0 ? dataInterval : -1,
                frameLossRate = dataLossRate >= 0 ? dataLossRate : -1,
                sequenceNumber = sequence,
                link = link,
                measurementType = "Estimated VR->PC one-way delay",
                syncValid = sync.IsReady,
                syncState = sync.State,
                clockOffsetMs = sync.ClockDiff,
                syncRttMs = sync.RoundTripTimeMs,
                syncAgeMs = sync.AgeMs,
                offsetUncertaintyMs = sync.OffsetUncertaintyMs,
                syncSampleCount = sync.SampleCount
            };

            // 先添加到历史缓冲区（不调用 AddToHistoryWithRecording 以避免重复写入）
            AddToHistory(link, tempData);

            // 现在计算基于所有历史数据（包括当前）的延迟统计数据
            var (latencyMin, latencyMax, latencyMedian, latency95th, latency99th) = CalculateFrameArrivalDelayStatistics(link);

            // 创建完整的性能数据对象
            PerformanceData data = new PerformanceData
            {
                sentTimestamp = sentTimestamp,
                sampleTimestamp = sampleTimestampStr,
                // 应用数据到达延迟作为延迟指标
                latency = dataArrivalDelay >= 0 ? dataArrivalDelay : -1,
                jitter = dataJitter >= 0 ? dataJitter : -1,
                packetLoss = packetLoss >= 0 ? packetLoss : -1,
                latencyMin = latencyMin,
                latencyMax = latencyMax,
                latencyMedian = latencyMedian,
                latency95th = latency95th,
                latency99th = latency99th,
                frameArrivalDelay = dataArrivalDelay >= 0 ? dataArrivalDelay : -1,
                frameInterval = dataInterval >= 0 ? dataInterval : -1,
                frameLossRate = dataLossRate >= 0 ? dataLossRate : -1,
                sequenceNumber = sequence,
                link = link,
                measurementType = "Estimated VR->PC one-way delay",
                syncValid = sync.IsReady,
                syncState = sync.State,
                clockOffsetMs = sync.ClockDiff,
                syncRttMs = sync.RoundTripTimeMs,
                syncAgeMs = sync.AgeMs,
                offsetUncertaintyMs = sync.OffsetUncertaintyMs,
                syncSampleCount = sync.SampleCount
            };

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
            //Debug.Log($"[NetworkPerformanceMonitor] EyeTracking Data {link}: arrivalDelay={dataArrivalDelay:F2}ms, interval={dataInterval:F2}ms, jitter={dataJitter:F2}ms, lossRate={dataLossRate:F2}%");

            // 触发性能数据更新事件（零 GC 分配，无需 lambda 闭包）
            EnqueuePerformanceUpdate(data);
        }

        #endregion

    }
}
