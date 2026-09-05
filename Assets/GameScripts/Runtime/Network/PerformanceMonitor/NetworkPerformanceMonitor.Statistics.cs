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
        #region 统计分析

        /// <summary>
        /// 获取平均延迟
        /// </summary>
        /// <param name="link">链路名称（null 表示当前链路）</param>
        /// <returns>平均延迟（毫秒）</returns>
        public float GetAverageLatency(string link = null)
        {
            link = link ?? _currentLink;
            var linkData = GetLinkData(link);

            if (linkData.validCount == 0)
                return 0f;

            float sum = 0f;
            for (int i = 0; i < linkData.validCount; i++)
            {
                int index = (linkData.writeIndex - linkData.validCount + i + _bufferSize) % _bufferSize;
                sum += linkData.historyBuffer[index].latency;
            }
            return sum / linkData.validCount;
        }

        /// <summary>
        /// 获取平均抖动
        /// </summary>
        /// <param name="link">链路名称（null 表示当前链路）</param>
        /// <returns>平均抖动（毫秒）</returns>
        public float GetAverageJitter(string link = null)
        {
            link = link ?? _currentLink;
            var linkData = GetLinkData(link);

            if (linkData.validCount == 0)
                return 0f;

            float sum = 0f;
            for (int i = 0; i < linkData.validCount; i++)
            {
                int index = (linkData.writeIndex - linkData.validCount + i + _bufferSize) % _bufferSize;
                sum += linkData.historyBuffer[index].jitter;
            }
            return sum / linkData.validCount;
        }

        /// <summary>
        /// 获取平均丢包率
        /// </summary>
        /// <param name="link">链路名称（null 表示当前链路）</param>
        /// <returns>平均丢包率（0-100）</returns>
        public float GetAveragePacketLoss(string link = null)
        {
            link = link ?? _currentLink;
            var linkData = GetLinkData(link);

            if (linkData.totalPackets == 0)
                return 0f;
            return (float)linkData.totalLostPackets / linkData.totalPackets * 100f;
        }

        /// <summary>
        /// 获取指定时间范围内的平均性能数据
        /// </summary>
        /// <param name="duration">时间范围（秒）</param>
        /// <param name="link">链路名称（null 表示当前链路）</param>
        /// <returns>平均性能数据</returns>
        public PerformanceData GetAverageData(float duration, string link = null)
        {
            link = link ?? _currentLink;
            float endTime = Time.time;
            float startTime = endTime - duration;
            List<PerformanceData> dataList = GetHistoryDataInRange(startTime, endTime, link);

            if (dataList.Count == 0)
                return new PerformanceData();

            // 计算平均值
            PerformanceData avgData = new PerformanceData();
            float totalLatency = 0;
            float totalJitter = 0;
            float totalPacketLoss = 0;
            float totalLatencyMin = float.MaxValue;
            float totalLatencyMax = float.MinValue;
            float totalLatencyMedian = 0;
            float totalLatency95th = 0;
            float totalLatency99th = 0;
            float totalFrameLossRate = 0;

            foreach (PerformanceData data in dataList)
            {
                totalLatency += data.latency;
                totalJitter += data.jitter;
                totalPacketLoss += data.packetLoss;
                totalLatencyMin = Mathf.Min(totalLatencyMin, data.latencyMin);
                totalLatencyMax = Mathf.Max(totalLatencyMax, data.latencyMax);
                totalLatencyMedian += data.latencyMedian;
                totalLatency95th += data.latency95th;
                totalLatency99th += data.latency99th;
                totalFrameLossRate += data.frameLossRate;
            }

            int count = dataList.Count;
            avgData.sentTimestamp = GetCurrentUnixTimeMilliseconds();
            avgData.sampleTimestamp = dataList.Count > 0 ? dataList[0].sampleTimestamp : FormatTimestamp(GetCompensatedDateTime());
            avgData.latency = totalLatency / count;
            avgData.jitter = totalJitter / count;
            avgData.packetLoss = totalPacketLoss / count;
            avgData.latencyMin = totalLatencyMin;
            avgData.latencyMax = totalLatencyMax;
            avgData.latencyMedian = totalLatencyMedian / count;
            avgData.latency95th = totalLatency95th / count;
            avgData.latency99th = totalLatency99th / count;
            avgData.frameLossRate = totalFrameLossRate / count;
            avgData.link = dataList.Count > 0 ? dataList[0].link : link;

            return avgData;
        }

        /// <summary>
        /// 获取所有链路的聚合性能数据
        /// </summary>
        /// <param name="duration">时间范围（秒）</param>
        /// <returns>所有链路的聚合性能数据</returns>
        public Dictionary<string, PerformanceData> GetAllLinksAggregatedData(float duration)
        {
            Dictionary<string, PerformanceData> aggregatedData = new Dictionary<string, PerformanceData>();
            float endTime = Time.time;
            float startTime = endTime - duration;

            foreach (string link in _linkDataDict.Keys)
            {
                List<PerformanceData> dataList = GetHistoryDataInRange(startTime, endTime, link);
                if (dataList.Count > 0)
                {
                    PerformanceData avgData = CalculateAverageData(dataList);
                    aggregatedData[link] = avgData;
                }
            }

            return aggregatedData;
        }

        /// <summary>
        /// 计算数据列表的平均值
        /// </summary>
        /// <param name="dataList">性能数据列表</param>
        /// <returns>平均性能数据</returns>
        private PerformanceData CalculateAverageData(List<PerformanceData> dataList)
        {
            if (dataList.Count == 0)
                return new PerformanceData();

            PerformanceData avgData = new PerformanceData();
            float totalLatency = 0;
            float totalJitter = 0;
            float totalPacketLoss = 0;

            float totalLatencyMin = float.MaxValue;
            float totalLatencyMax = float.MinValue;
            float totalLatencyMedian = 0;
            float totalLatency95th = 0;
            float totalLatency99th = 0;

            float totalFrameLossRate = 0;

            foreach (PerformanceData data in dataList)
            {
                totalLatency += data.latency;
                totalJitter += data.jitter;
                totalPacketLoss += data.packetLoss;
                totalLatencyMin = Mathf.Min(totalLatencyMin, data.latencyMin);
                totalLatencyMax = Mathf.Max(totalLatencyMax, data.latencyMax);
                totalLatencyMedian += data.latencyMedian;
                totalLatency95th += data.latency95th;
                totalLatency99th += data.latency99th;
                totalFrameLossRate += data.frameLossRate;
            }

            int count = dataList.Count;
            avgData.sentTimestamp = GetCurrentUnixTimeMilliseconds();
            avgData.sampleTimestamp = dataList[0].sampleTimestamp;
            avgData.latency = totalLatency / count;
            avgData.jitter = totalJitter / count;
            avgData.packetLoss = totalPacketLoss / count;
            avgData.latencyMin = totalLatencyMin;
            avgData.latencyMax = totalLatencyMax;
            avgData.latencyMedian = totalLatencyMedian / count;
            avgData.latency95th = totalLatency95th / count;
            avgData.latency99th = totalLatency99th / count;
            avgData.frameLossRate = totalFrameLossRate / count;
            avgData.link = dataList[0].link;

            return avgData;
        }

        /// <summary>
        /// 获取所有链路的最新性能数据
        /// </summary>
        /// <returns>所有链路的最新性能数据</returns>
        public Dictionary<string, PerformanceData> GetAllLinksLatestData()
        {
            Dictionary<string, PerformanceData> latestData = new Dictionary<string, PerformanceData>();

            foreach (string link in _linkDataDict.Keys)
            {
                latestData[link] = GetLatestData(link);
            }

            return latestData;
        }

        /// <summary>
        /// 计算延迟分布统计数据
        /// </summary>
        /// <param name="link">链路名称（null 表示当前链路）</param>
        /// <returns>包含延迟统计数据的元组 (min, max, median, 95th, 99th)</returns>
        private (float min, float max, float median, float percentile95, float percentile99) CalculateLatencyStatistics(string link = null)
        {
            link = link ?? _currentLink;
            var linkData = GetLinkData(link);

            if (linkData.validCount == 0)
            {
                return (-1, -1, -1, -1, -1);
            }

            // 收集有效的延迟数据（复用预分配列表，避免 GC 压力）
            var latencyValues = linkData.reusableDelayValues;
            latencyValues.Clear();
            for (int i = 0; i < linkData.validCount; i++)
            {
                int index = (linkData.writeIndex - linkData.validCount + i + _bufferSize) % _bufferSize;
                float latency = linkData.historyBuffer[index].latency;
                if (latency > 0) // 只考虑有效的延迟值
                {
                    latencyValues.Add(latency);
                }
            }

            if (latencyValues.Count == 0)
            {
                return (-1, -1, -1, -1, -1);
            }

            // 排序延迟值
            latencyValues.Sort();

            // 计算统计值
            float min = latencyValues[0];
            float max = latencyValues[latencyValues.Count - 1];
            float median = CalculateMedian(latencyValues);
            float percentile95 = CalculatePercentile(latencyValues, 95);
            float percentile99 = CalculatePercentile(latencyValues, 99);

            return (min, max, median, percentile95, percentile99);
        }

        /// <summary>
        /// 计算中位数
        /// </summary>
        /// <param name="values">排序后的值列表</param>
        /// <returns>中位数</returns>
        private float CalculateMedian(List<float> values)
        {
            int count = values.Count;
            if (count % 2 == 0)
            {
                return (values[count / 2 - 1] + values[count / 2]) / 2;
            }
            else
            {
                return values[count / 2];
            }
        }

        /// <summary>
        /// 计算百分位值
        /// </summary>
        /// <param name="values">排序后的值列表</param>
        /// <param name="percentile">百分位（0-100）</param>
        /// <returns>百分位值</returns>
        private float CalculatePercentile(List<float> values, int percentile)
        {
            int count = values.Count;
            float index = (count - 1) * percentile / 100f;
            int lowerIndex = Mathf.FloorToInt(index);
            int upperIndex = Mathf.CeilToInt(index);

            if (lowerIndex == upperIndex)
            {
                return values[lowerIndex];
            }

            float fraction = index - lowerIndex;
            return values[lowerIndex] * (1 - fraction) + values[upperIndex] * fraction;
        }

        /// <summary>
        /// 计算帧到达延迟的分布统计数据
        /// </summary>
        /// <param name="link">链路名称（null 表示当前链路）</param>
        /// <returns>包含延迟统计数据的元组 (min, max, median, 95th, 99th)</returns>
        private (float min, float max, float median, float percentile95, float percentile99) CalculateFrameArrivalDelayStatistics(string link = null)
        {
            link = link ?? _currentLink;
            var linkData = GetLinkData(link);

            if (linkData.validCount == 0)
            {
                return (-1, -1, -1, -1, -1);
            }

            // 收集有效的帧到达延迟数据（复用预分配列表，避免 GC 压力）
            var delayValues = linkData.reusableDelayValues;
            delayValues.Clear();
            for (int i = 0; i < linkData.validCount; i++)
            {
                int index = (linkData.writeIndex - linkData.validCount + i + _bufferSize) % _bufferSize;
                float delay = linkData.historyBuffer[index].frameArrivalDelay;
                if (delay >= 0) // 只考虑有效的延迟值，包括0
                {
                    delayValues.Add(delay);
                }
            }

            if (delayValues.Count == 0)
            {
                return (-1, -1, -1, -1, -1);
            }

            // 排序延迟值
            delayValues.Sort();

            // 计算统计值
            float min = delayValues[0];
            float max = delayValues[delayValues.Count - 1];
            float median = CalculateMedian(delayValues);
            float percentile95 = CalculatePercentile(delayValues, 95);
            float percentile99 = CalculatePercentile(delayValues, 99);

            return (min, max, median, percentile95, percentile99);
        }

        #endregion


    }
}
