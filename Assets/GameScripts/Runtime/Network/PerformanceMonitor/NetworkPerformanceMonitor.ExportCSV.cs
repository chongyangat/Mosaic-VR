using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Globalization;
using UnityEngine;

namespace GameMain
{
    /// <summary>
    /// 网络性能监控核心类，负责监控延迟、抖动、丢包率等网络性能指标
    /// 支持多链路监控，每个链路有独立的性能数据
    /// </summary>
    public partial class NetworkPerformanceMonitor : MonoBehaviour
    {
        #region CSV导出

        /// <summary>
        /// 导出所有历史数据到CSV文件
        /// </summary>
        /// <param name="filePath">CSV文件路径</param>
        /// <param name="link">链路名称（null 表示所有链路）</param>
        /// <returns>是否导出成功</returns>
        public bool ExportToCsv(string filePath, string link = null)
        {
            try
            {
                List<PerformanceData> dataList;

                if (string.IsNullOrEmpty(link))
                {
                    // 导出所有链路数据
                    dataList = new List<PerformanceData>();
                    foreach (var linkName in _linkDataDict.Keys)
                    {
                        dataList.AddRange(GetAllHistoryData(linkName));
                    }
                }
                else
                {
                    // 导出指定链路数据
                    dataList = GetAllHistoryData(link);
                }

                return ExportToCsv(filePath, dataList);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkPerformanceMonitor] 导出CSV失败: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 导出指定时间范围内的数据到CSV文件
        /// </summary>
        /// <param name="filePath">CSV文件路径</param>
        /// <param name="startTime">开始时间</param>
        /// <param name="endTime">结束时间</param>
        /// <param name="link">链路名称（null 表示所有链路）</param>
        /// <returns>是否导出成功</returns>
        public bool ExportToCsv(string filePath, float startTime, float endTime, string link = null)
        {
            try
            {
                List<PerformanceData> dataList;

                if (string.IsNullOrEmpty(link))
                {
                    // 导出所有链路数据
                    dataList = new List<PerformanceData>();
                    foreach (var linkName in _linkDataDict.Keys)
                    {
                        dataList.AddRange(GetHistoryDataInRange(startTime, endTime, linkName));
                    }
                }
                else
                {
                    // 导出指定链路数据
                    dataList = GetHistoryDataInRange(startTime, endTime, link);
                }

                return ExportToCsv(filePath, dataList);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkPerformanceMonitor] 导出CSV失败: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 导出指定数据列表到CSV文件
        /// </summary>
        /// <param name="filePath">CSV文件路径</param>
        /// <param name="dataList">性能数据列表</param>
        /// <returns>是否导出成功</returns>
        private bool ExportToCsv(string filePath, List<PerformanceData> dataList)
        {
            if (dataList == null || dataList.Count == 0)
            {
                Debug.LogWarning("[NetworkPerformanceMonitor] 没有数据可导出");
                return false;
            }

            try
            {
                StringBuilder sb = new StringBuilder();

                // CSV表头
                sb.AppendLine(GetCsvHeader());

                // 数据行
                int index = 1;
                foreach (PerformanceData data in dataList)
                {
                    string latency = data.latency.ToString("F2", CultureInfo.InvariantCulture);
                    string jitter = data.jitter.ToString("F2", CultureInfo.InvariantCulture);
                    string packetLoss = data.packetLoss.ToString("F2", CultureInfo.InvariantCulture);
                    string latencyMin = data.latencyMin.ToString("F2", CultureInfo.InvariantCulture);
                    string latencyMax = data.latencyMax.ToString("F2", CultureInfo.InvariantCulture);
                    string latencyMedian = data.latencyMedian.ToString("F2", CultureInfo.InvariantCulture);
                    string latency95th = data.latency95th.ToString("F2", CultureInfo.InvariantCulture);
                    string latency99th = data.latency99th.ToString("F2", CultureInfo.InvariantCulture);
                    string frameArrivalDelay = data.frameArrivalDelay.ToString("F2", CultureInfo.InvariantCulture);
                    string frameInterval = data.frameInterval.ToString("F2", CultureInfo.InvariantCulture);
                    string frameLossRate = data.frameLossRate.ToString("F2", CultureInfo.InvariantCulture);
                    
                    // 将毫秒级时间戳转换为 UTC+8 日期时间格式，精确到毫秒
                    string sendTimeStr = string.Empty;
                    if (data.sentTimestamp > 0)
                    {
                        DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(data.sentTimestamp);
                        sendTimeStr = FormatTimestamp(dateTime);
                    }
                    // data.sampleTimestamp转换成本地时间格式，精确到毫秒
                    // DateTime sampleDateTime = DateTime.Parse(data.sampleTimestamp).ToLocalTime();
                    // string sampleTimeStr = sampleDateTime.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    //Debug.Log($"[NetworkPerformanceMonitor] 原始时间戳: {data.sentTimestamp}, 转换后时间戳: {sendTimeStr}");
                    sb.AppendLine(
                        $"{index},{data.sampleTimestamp},{sendTimeStr},{data.link},{latency},{jitter},{packetLoss}," +
                        $"{latencyMin},{latencyMax},{latencyMedian},{latency95th},{latency99th}," +
                        $"{frameArrivalDelay},{frameInterval},{frameLossRate},{data.measurementType}," +
                        $"{(data.syncValid ? "true" : "false")},{data.syncState}," +
                        data.clockOffsetMs.ToString("F3", CultureInfo.InvariantCulture) + "," +
                        data.syncRttMs.ToString("F3", CultureInfo.InvariantCulture) + "," +
                        data.syncAgeMs.ToString("F1", CultureInfo.InvariantCulture) + "," +
                        data.offsetUncertaintyMs.ToString("F3", CultureInfo.InvariantCulture) + "," +
                        $"{data.syncSampleCount}");
                    index++;
                }

                // 确保目录存在
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 写入文件
                File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
                Debug.Log($"[NetworkPerformanceMonitor] CSV导出成功: {filePath}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkPerformanceMonitor] 导出CSV时发生异常: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 导出所有链路数据到各自独立的CSV文件
        /// 每个链路生成一个单独的CSV文件
        /// </summary>
        /// <param name="baseFilePath">基础文件路径，会为每个链路添加后缀</param>
        /// <returns>是否导出成功</returns>
        public bool ExportAllLinksToCsv(string baseFilePath)
        {
            try
            {
                bool allSuccess = true;
                string directory = Path.GetDirectoryName(baseFilePath);
                string fileName = Path.GetFileNameWithoutExtension(baseFilePath);
                string extension = Path.GetExtension(baseFilePath);

                foreach (var linkName in _linkDataDict.Keys)
                {
                    // 为每个链路生成唯一的文件名
                    string linkFileName = $"{fileName}_{linkName.Replace("<->", "_").Replace("→", "_")}{extension}";
                    string linkFilePath = Path.Combine(directory, linkFileName);

                    bool success = ExportToCsv(linkFilePath, linkName);
                    if (!success)
                    {
                        allSuccess = false;
                    }
                }

                return allSuccess;
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkPerformanceMonitor] 导出所有链路CSV失败: {e.Message}");
                return false;
            }
        }

        #endregion
    }
}
