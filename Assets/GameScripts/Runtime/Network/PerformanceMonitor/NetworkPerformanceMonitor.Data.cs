using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameMain
{
    /// <summary>
    /// 网络性能监控核心类，负责监控延迟、抖动、丢包率等网络性能指标
    /// 支持多链路监控，每个链路有独立的性能数据
    /// </summary>
    public partial class NetworkPerformanceMonitor : MonoBehaviour
    {
        #region 数据结构

        /// <summary>
        /// 性能数据结构，存储单次采样的网络性能指标
        /// </summary>
        [Serializable]
        public struct PerformanceData
        {
            /// <summary>
            /// 采集时间（数据采集时的同步时间，格式：yyyy-MM-dd HH:mm:ss.fff）
            /// </summary>
            public string sampleTimestamp;

            /// <summary>
            /// 发出时间（数据发送方的时间戳，毫秒，Unix时间戳）
            /// </summary>
            public ulong sentTimestamp;

            /// <summary>
            /// 网络延迟（毫秒）
            /// </summary>
            public float latency;

            /// <summary>
            /// 网络抖动（毫秒）
            /// </summary>
            public float jitter;

            /// <summary>
            /// 丢包率（百分比，0-100）
            /// </summary>
            public float packetLoss;

            /// <summary>
            /// 最小延迟
            /// </summary>
            public float latencyMin;

            /// <summary>
            /// 最大延迟
            /// </summary>
            public float latencyMax;

            /// <summary>
            /// 中位延迟
            /// </summary>
            public float latencyMedian;

            /// <summary>
            /// 95百分位延迟
            /// </summary>
            public float latency95th;

            /// <summary>
            /// 99百分位延迟
            /// </summary>
            public float latency99th;

            /// <summary>
            /// 帧到达延迟
            /// </summary>
            public float frameArrivalDelay;

            /// <summary>
            /// 帧间隔（毫秒）
            /// </summary>
            public float frameInterval;

            /// <summary>
            /// 丢帧率
            /// </summary>
            public float frameLossRate;

            /// <summary>
            /// 当前序列号
            /// </summary>
            public uint sequenceNumber;

            /// <summary>
            /// 链路标识，用于区分不同链路上的性能数据
            /// </summary>
            public string link;

            /// <summary>Measurement semantics, for example RTT or estimated one-way delay.</summary>
            public string measurementType;

            /// <summary>Whether the directional measurement had a fresh, stable clock estimate.</summary>
            public bool syncValid;

            /// <summary>Clock synchronization state at collection time.</summary>
            public string syncState;

            /// <summary>Estimated remote-local clock difference in milliseconds.</summary>
            public double clockOffsetMs;

            /// <summary>Network-only RTT of the selected four-timestamp synchronization sample.</summary>
            public double syncRttMs;

            /// <summary>Age of the latest accepted synchronization sample.</summary>
            public double syncAgeMs;

            /// <summary>Upper-bound estimate of offset ambiguity caused by path asymmetry.</summary>
            public double offsetUncertaintyMs;

            /// <summary>Number of accepted synchronization samples in the current session.</summary>
            public int syncSampleCount;
        }

        /// <summary>
        /// 链路优先级枚举
        /// </summary>
        public enum LinkPriority
        {
            Low = 0,
            Medium = 1,
            High = 2,
            Critical = 3
        }

        /// <summary>
        /// 链路数据类，存储单个链路的所有数据
        /// </summary>
        private class LinkData
        {
            /// <summary>
            /// 循环缓冲区存储历史性能数据
            /// </summary>
            public PerformanceData[] historyBuffer;

            /// <summary>
            /// 缓冲区写入索引
            /// </summary>
            public int writeIndex;

            /// <summary>
            /// 缓冲区中有效数据条数
            /// </summary>
            public int validCount;

            /// <summary>
            /// 延迟样本数组（用于抖动计算）
            /// </summary>
            public float[] latencySamples;

            /// <summary>
            /// 延迟样本索引
            /// </summary>
            public int latencySampleIndex;

            /// <summary>
            /// 延迟样本有效数量
            /// </summary>
            public int latencySampleCount;

            /// <summary>
            /// 上一次采样的序列号
            /// </summary>
            public uint lastSequenceNumber;

            /// <summary>
            /// 累计丢包数
            /// </summary>
            public int totalLostPackets;

            /// <summary>
            /// 累计总包数
            /// </summary>
            public int totalPackets;

            /// <summary>
            /// 当前序列号（用于丢包检测）
            /// </summary>
            public uint currentSequenceNumber;

            /// <summary>
            /// 链路是否活跃
            /// </summary>
            public bool isActive;

            /// <summary>
            /// 上次活动日期时间（用于跨线程安全的计时）
            /// </summary>
            public DateTime lastActiveDateTime;

            /// <summary>
            /// 链路优先级
            /// </summary>
            public LinkPriority priority;

            /// <summary>
            /// 上一帧的时间戳（用于计算帧间隔）
            /// </summary>
            public float lastFrameTimestamp;

            /// <summary>
            /// 帧间隔样本数组（用于抖动计算）
            /// </summary>
            public float[] frameIntervalSamples;

            /// <summary>
            /// 帧间隔样本索引
            /// </summary>
            public int frameIntervalSampleIndex;

            /// <summary>
            /// 帧间隔样本有效数量
            /// </summary>
            public int frameIntervalSampleCount;

            /// <summary>
            /// 累计帧总数
            /// </summary>
            public long totalFrames;

            /// <summary>
            /// 累计丢失帧数
            /// </summary>
            public int totalLostFrames;

            /// <summary>
            /// 上一帧的Unix时间戳（毫秒）
            /// </summary>
            public ulong lastUnixTimestamp;

            /// <summary>
            /// 上次上报的索引位置（用于跟踪已发送和未发送的数据）
            /// </summary>
            public int lastReportedIndex;

            /// <summary>
            /// 可复用的延迟值列表（避免每包 new List&lt;float&gt;）
            /// 仅供 CalculateFrameArrivalDelayStatistics / CalculateLatencyStatistics 使用
            /// </summary>
            public List<float> reusableDelayValues = new List<float>();

            /// <summary>
            /// 初始化链路数据
            /// </summary>
            /// <param name="bufferSize">缓冲区大小</param>
            /// <param name="jitterWindowSize">抖动计算窗口大小</param>
            public LinkData(int bufferSize, int jitterWindowSize)
            {
                historyBuffer = new PerformanceData[bufferSize];
                latencySamples = new float[jitterWindowSize];
                frameIntervalSamples = new float[jitterWindowSize];
                writeIndex = 0;
                validCount = 0;
                latencySampleIndex = 0;
                latencySampleCount = 0;
                frameIntervalSampleIndex = 0;
                frameIntervalSampleCount = 0;
                lastSequenceNumber = 0;
                totalLostPackets = 0;
                totalPackets = 0;
                currentSequenceNumber = 0;
                isActive = false;
                lastActiveDateTime = DateTime.Now;
                lastFrameTimestamp = 0;
                lastUnixTimestamp = 0;
                totalFrames = 0;
                totalLostFrames = 0;
                lastReportedIndex = 0;
                priority = LinkPriority.Medium; // 默认中等优先级
            }

            /// <summary>
            /// 重置链路数据
            /// </summary>
            public void Reset()
            {
                writeIndex = 0;
                validCount = 0;
                latencySampleIndex = 0;
                latencySampleCount = 0;
                frameIntervalSampleIndex = 0;
                frameIntervalSampleCount = 0;
                lastSequenceNumber = 0;
                totalLostPackets = 0;
                totalPackets = 0;
                currentSequenceNumber = 0;
                lastFrameTimestamp = 0;
                lastUnixTimestamp = 0;
                totalFrames = 0;
                totalLostFrames = 0;
                lastReportedIndex = 0;
                // ResetStatistics only clears measurements. Preserve link
                // availability and priority so starting a new recording does not
                // silently stop sampling an already-connected endpoint.
                Array.Clear(historyBuffer, 0, historyBuffer.Length);
                Array.Clear(latencySamples, 0, latencySamples.Length);
                Array.Clear(frameIntervalSamples, 0, frameIntervalSamples.Length);
            }

            /// <summary>
            /// 尝试获取上一帧的Unix时间戳
            /// </summary>
            /// <param name="timestamp">上一帧的Unix时间戳</param>
            /// <returns>是否成功获取</returns>
            public bool TryGetLastUnixTimestamp(out ulong timestamp)
            {
                timestamp = lastUnixTimestamp;
                return lastUnixTimestamp > 0;
            }

            /// <summary>
            /// 设置上一帧的Unix时间戳
            /// </summary>
            /// <param name="timestamp">Unix时间戳</param>
            public void SetLastUnixTimestamp(ulong timestamp)
            {
                lastUnixTimestamp = timestamp;
            }
        }

        #endregion

    }
}
