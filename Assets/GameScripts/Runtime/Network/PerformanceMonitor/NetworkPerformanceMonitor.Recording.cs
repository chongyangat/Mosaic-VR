
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace GameMain
{
    public partial class NetworkPerformanceMonitor : MonoBehaviour
    {
        #region 录制功能

        /// <summary>
        /// 录制状态
        /// </summary>
        private bool _isRecording = false;

        /// <summary>
        /// 当前录制的文件路径
        /// </summary>
        private string _recordingFilePath = string.Empty;

        /// <summary>
        /// 录制文件的StreamWriter（用于高效追加写入）
        /// </summary>
        private StreamWriter _recordingWriter = null;

        /// <summary>
        /// 记录当前写入的记录序号
        /// </summary>
        private int _recordingIndex = 0;

        /// <summary>
        /// 写入线程
        /// </summary>
        private Thread _recordingThread = null;

        /// <summary>
        /// 线程安全队列（存储原始结构体，由录制线程负责格式化，避免接收线程产生 GC 垃圾）
        /// </summary>
        private readonly Queue<PerformanceData> _recordingQueue = new Queue<PerformanceData>();
        private readonly object _queueLock = new object();

        /// <summary>
        /// 线程控制
        /// </summary>
        private volatile bool _recordingThreadRunning = false;
        private readonly AutoResetEvent _recordingEvent = new AutoResetEvent(false);

        /// <summary>
        /// 是否正在录制
        /// </summary>
        public bool IsRecording => _isRecording;

        /// <summary>
        /// 开始录制
        /// </summary>
        /// <param name="filePath">CSV文件保存路径</param>
        /// <returns>是否成功开始录制</returns>
        public bool StartRecording(string filePath = null)
        {
            if (_isRecording)
            {
                Debug.LogWarning("[NetworkPerformanceMonitor] 已经在录制中");
                return false;
            }

            try
            {
                // 如果没有指定路径，生成默认路径
                if (string.IsNullOrEmpty(filePath))
                {
                    string directory = Path.Combine(Application.persistentDataPath, "PerformanceLogs");
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    string fileName = $"PerformanceLog_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                    filePath = Path.Combine(directory, fileName);
                }

                // 确保目录存在
                string directoryPath = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                // 打开文件流，准备追加写入
                _recordingWriter = new StreamWriter(filePath, false, Encoding.UTF8);
                _recordingWriter.AutoFlush = true;

                // 写入CSV表头
                _recordingWriter.WriteLine(GetCsvHeader());

                // A previous recording may have been stopped while the writer
                // thread still had queued samples. Never let stale rows leak into
                // the next session.
                lock (_queueLock)
                {
                    _recordingQueue.Clear();
                }

                _recordingFilePath = filePath;
                _recordingIndex = 0;
                _isRecording = true;

                // 启动写入线程
                StartRecordingThread();

                Debug.Log($"[NetworkPerformanceMonitor] 开始录制: {filePath}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkPerformanceMonitor] 开始录制失败: {e.Message}");
                StopRecordingInternal();
                return false;
            }
        }

        /// <summary>
        /// 启动写入线程
        /// </summary>
        private void StartRecordingThread()
        {
            if (_recordingThread != null && _recordingThread.IsAlive)
                return;

            _recordingThreadRunning = true;
            _recordingThread = new Thread(RecordingThreadLoop)
            {
                IsBackground = true,
                Name = "PerformanceRecordingThread"
            };
            _recordingThread.Start();
            Debug.Log("[NetworkPerformanceMonitor] 录制写入线程已启动");
        }

        /// <summary>
        /// 停止写入线程
        /// </summary>
        private void StopRecordingThread()
        {
            if (_recordingThread == null || !_recordingThread.IsAlive)
                return;

            _recordingThreadRunning = false;
            _recordingEvent.Set(); // 唤醒线程
            _recordingThread.Join(1000); // 等待最多1秒
            Debug.Log("[NetworkPerformanceMonitor] 录制写入线程已停止");
        }

        /// <summary>
        /// 录制线程可复用的 StringBuilder（仅录制线程访问，无需锁）
        /// </summary>
        private StringBuilder _recordingSb = new StringBuilder(256);

        /// <summary>
        /// 写入线程循环
        /// </summary>
        private void RecordingThreadLoop()
        {
            while (true)
            {
                // 等待数据
                _recordingEvent.WaitOne();

                // Drain every queued sample before honoring a stop request. The
                // old loop exited as soon as _recordingThreadRunning became false,
                // which silently discarded the final measurements.
                while (true)
                {
                    PerformanceData dataToWrite = default;
                    bool hasData = false;

                    lock (_queueLock)
                    {
                        if (_recordingQueue.Count > 0)
                        {
                            dataToWrite = _recordingQueue.Dequeue();
                            hasData = true;
                        }
                    }

                    if (!hasData)
                    {
                        break; // 队列已空
                    }

                    // 在录制线程中格式化并写入文件
                    try
                    {
                        if (_recordingWriter != null)
                        {
                            string line = FormatRecordingLine(dataToWrite);
                            _recordingWriter.WriteLine(line);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[NetworkPerformanceMonitor] 写入录制文件失败: {e.Message}");
                    }
                }

                if (!_recordingThreadRunning)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// 停止录制
        /// </summary>
        /// <returns>录制的文件路径</returns>
        public string StopRecording()
        {
            if (!_isRecording)
            {
                Debug.LogWarning("[NetworkPerformanceMonitor] 没有在录制中");
                return string.Empty;
            }

            string filePath = _recordingFilePath;
            StopRecordingInternal();

            Debug.Log($"[NetworkPerformanceMonitor] 停止录制: {filePath}");
            return filePath;
        }

        /// <summary>
        /// 内部停止录制（清理资源）
        /// </summary>
        private void StopRecordingInternal()
        {
            _isRecording = false;
            _recordingFilePath = string.Empty;

            // 停止写入线程
            StopRecordingThread();

            if (_recordingWriter != null)
            {
                try
                {
                    _recordingWriter.Flush();
                    _recordingWriter.Close();
                    _recordingWriter.Dispose();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[NetworkPerformanceMonitor] 关闭录制文件失败: {e.Message}");
                }
                _recordingWriter = null;
            }
        }

        /// <summary>
        /// 将单条性能数据写入录制文件（接收线程调用，零字符串分配）
        /// 仅将结构体入队，格式化由录制线程完成
        /// </summary>
        /// <param name="data">性能数据</param>
        private void WriteToRecordingFile(PerformanceData data)
        {
            if (!_isRecording || _recordingWriter == null)
            {
                return;
            }

            try
            {
                // 直接入队结构体，由录制线程负责格式化
                lock (_queueLock)
                {
                    _recordingQueue.Enqueue(data);
                }
                _recordingEvent.Set(); // 唤醒写入线程
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkPerformanceMonitor] 写入录制文件失败: {e.Message}");
            }
        }

        /// <summary>
        /// 将 PerformanceData 格式化为 CSV 行（仅录制线程调用）
        /// 使用可复用的 StringBuilder 减少字符串分配
        /// </summary>
        private string FormatRecordingLine(PerformanceData data)
        {
            _recordingSb.Clear();

            // 递增序号
            _recordingIndex++;

            _recordingSb.Append(_recordingIndex).Append(',');
            _recordingSb.Append(data.sampleTimestamp).Append(',');

            // 发出时间（UTC+8）
            if (data.sentTimestamp > 0)
            {
                DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(data.sentTimestamp);
                _recordingSb.Append(FormatTimestamp(dateTime));
            }
            _recordingSb.Append(',');

            _recordingSb.Append(data.link).Append(',');

            AppendFloat(_recordingSb, data.latency).Append(',');
            AppendFloat(_recordingSb, data.jitter).Append(',');
            AppendFloat(_recordingSb, data.packetLoss).Append(',');
            AppendFloat(_recordingSb, data.latencyMin).Append(',');
            AppendFloat(_recordingSb, data.latencyMax).Append(',');
            AppendFloat(_recordingSb, data.latencyMedian).Append(',');
            AppendFloat(_recordingSb, data.latency95th).Append(',');
            AppendFloat(_recordingSb, data.latency99th).Append(',');
            AppendFloat(_recordingSb, data.frameArrivalDelay).Append(',');
            AppendFloat(_recordingSb, data.frameInterval).Append(',');
            AppendFloat(_recordingSb, data.frameLossRate);
            _recordingSb.Append(',').Append(data.measurementType ?? string.Empty);
            _recordingSb.Append(',').Append(data.syncValid ? "true" : "false");
            _recordingSb.Append(',').Append(data.syncState ?? string.Empty);
            _recordingSb.Append(',').Append(data.clockOffsetMs.ToString("F3", CultureInfo.InvariantCulture));
            _recordingSb.Append(',').Append(data.syncRttMs.ToString("F3", CultureInfo.InvariantCulture));
            _recordingSb.Append(',').Append(data.syncAgeMs.ToString("F1", CultureInfo.InvariantCulture));
            _recordingSb.Append(',').Append(data.offsetUncertaintyMs.ToString("F3", CultureInfo.InvariantCulture));
            _recordingSb.Append(',').Append(data.syncSampleCount);

            return _recordingSb.ToString();
        }

        private const string CsvHeader =
            "序号,采集时间,发出时间,链路,延迟(毫秒),抖动(毫秒),丢包率(%),最小延迟,最大延迟," +
            "中位延迟,95百分位延迟,99百分位延迟,帧到达延迟,帧间隔（毫秒）,丢帧率(%)," +
            "测量类型,时钟同步有效,时钟同步状态,时钟偏移(毫秒),同步RTT(毫秒),同步年龄(毫秒)," +
            "偏移不确定度上界(毫秒),同步样本数";

        internal static string GetCsvHeader()
        {
            return CsvHeader;
        }

        /// <summary>
        /// 将 float 值追加到 StringBuilder，如需可将负值输出 "--"
        /// 可选：将负值输出 "--"
        /// </summary>
        private static StringBuilder AppendFloat(StringBuilder sb, float value, bool appendNegativeSign = false)
        {
            if (!appendNegativeSign || value >= 0)
            {
                sb.Append(value.ToString("F2", CultureInfo.InvariantCulture));
            }
            else
            {
                sb.Append("--");
            }
            return sb;
        }

        /// <summary>
        /// 在AddToHistory后同时写入录制文件
        /// （需要修改原始的AddToHistory方法来调用此方法）
        /// </summary>
        private void AddToHistoryWithRecording(string link, PerformanceData data)
        {
            AddToHistory(link, data);

            if (_isRecording)
            {
                WriteToRecordingFile(data);
            }
        }

        #endregion
    }
}
