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

        #region 数据存储与获取

        /// <summary>
        /// 添加数据到循环缓冲区
        /// </summary>
        /// <param name="link">链路名称</param>
        /// <param name="data">性能数据</param>
        private void AddToHistory(string link, PerformanceData data)
        {
            var linkData = GetLinkData(link);
            linkData.historyBuffer[linkData.writeIndex] = data;
            linkData.writeIndex = (linkData.writeIndex + 1) % _bufferSize;
            linkData.validCount = Mathf.Min(linkData.validCount + 1, _bufferSize);
            //Debug.Log($"[NetworkPerformanceMonitor] AddToHistory for {link}: validCount={linkData.validCount}, writeIndex={linkData.writeIndex}");
        }

        /// <summary>
        /// 获取最新的性能数据
        /// </summary>
        /// <param name="link">链路名称（null 表示当前链路）</param>
        /// <returns>最新性能数据</returns>
        public PerformanceData GetLatestData(string link = null)
        {
            link = link ?? _currentLink;
            var linkData = GetLinkData(link);

            if (linkData.validCount == 0)
            {
                Debug.Log($"[NetworkPerformanceMonitor] No data for link {link}, validCount={linkData.validCount}");
                return new PerformanceData();
            }

            int latestIndex = (linkData.writeIndex - 1 + _bufferSize) % _bufferSize;
            PerformanceData data = linkData.historyBuffer[latestIndex];
            //Debug.Log($"[NetworkPerformanceMonitor] GetLatestData for {link}: latency={data.latency}ms, jitter={data.jitter}ms, packetLoss={data.packetLoss}%");
            return data;
        }

        /// <summary>
        /// 获取所有历史性能数据（按时间顺序，从旧到新）
        /// </summary>
        /// <param name="link">链路名称（null 表示当前链路）</param>
        /// <returns>历史数据列表</returns>
        public List<PerformanceData> GetAllHistoryData(string link = null)
        {
            link = link ?? _currentLink;
            var linkData = GetLinkData(link);

            List<PerformanceData> result = new List<PerformanceData>(linkData.validCount);

            for (int i = 0; i < linkData.validCount; i++)
            {
                int index = (linkData.writeIndex - linkData.validCount + i + _bufferSize) % _bufferSize;
                result.Add(linkData.historyBuffer[index]);
            }

            return result;
        }

        /// <summary>
        /// 获取指定时间范围内的历史数据
        /// </summary>
        /// <param name="startTime">开始时间（秒）</param>
        /// <param name="endTime">结束时间（秒）</param>
        /// <param name="link">链路名称（null 表示当前链路）</param>
        /// <returns>时间范围内的数据列表</returns>
        public List<PerformanceData> GetHistoryDataInRange(float startTime, float endTime, string link = null)
        {
            link = link ?? _currentLink;
            var linkData = GetLinkData(link);

            List<PerformanceData> result = new List<PerformanceData>();

            for (int i = 0; i < linkData.validCount; i++)
            {
                int index = (linkData.writeIndex - linkData.validCount + i + _bufferSize) % _bufferSize;
                PerformanceData data = linkData.historyBuffer[index];

                if (data.sentTimestamp >= startTime && data.sentTimestamp <= endTime)
                {
                    result.Add(data);
                }
            }

            return result;
        }

        /// <summary>
        /// 获取指定时间范围内的历史数据（UI代码使用的方法）
        /// </summary>
        /// <param name="startTime">开始时间（秒）</param>
        /// <param name="endTime">结束时间（秒）</param>
        /// <param name="link">链路名称（null 表示当前链路）</param>
        /// <returns>时间范围内的数据列表</returns>
        public List<PerformanceData> GetHistoryData(float startTime, float endTime, string link = null)
        {
            return GetHistoryDataInRange(startTime, endTime, link);
        }

        /// <summary>
        /// 导出性能数据到CSV文件（UI代码使用的方法）
        /// </summary>
        /// <param name="filePath">CSV文件路径</param>
        /// <param name="link">链路名称（null 表示所有链路）</param>
        public void ExportData(string filePath, string link = null)
        {
            ExportToCsv(filePath, link);
        }

        /// <summary>
        /// 获取最近N条历史数据
        /// </summary>
        /// <param name="count">数据条数</param>
        /// <param name="link">链路名称（null 表示当前链路）</param>
        /// <returns>最近N条数据</returns>
        public List<PerformanceData> GetRecentHistoryData(int count, string link = null)
        {
            link = link ?? _currentLink;
            var linkData = GetLinkData(link);

            count = Mathf.Min(count, linkData.validCount);
            List<PerformanceData> result = new List<PerformanceData>(count);

            for (int i = 0; i < count; i++)
            {
                int index = (linkData.writeIndex - 1 - i + _bufferSize) % _bufferSize;
                result.Add(linkData.historyBuffer[index]);
            }

            result.Reverse();
            return result;
        }

        /// <summary>
        /// 获取历史数据有效条数
        /// </summary>
        /// <param name="link">链路名称（null 表示当前链路）</param>
        /// <returns>历史数据有效条数</returns>
        public int GetHistoryCount(string link = null)
        {
            link = link ?? _currentLink;
            if (_linkDataDict.TryGetValue(link, out var linkData))
            {
                return linkData.validCount;
            }
            return 0;
        }

        #endregion

    }
}
