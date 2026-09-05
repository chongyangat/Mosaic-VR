using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

namespace GameMain
{
    /// <summary>
    /// GC 监控模块：每帧检测 GC 回收事件，记录日志用于与网络延迟高峰关联分析
    /// </summary>
    public partial class NetworkPerformanceMonitor
    {
        #region GC 监控

        /// <summary>
        /// 是否启用 GC 监控日志
        /// </summary>
        [Header("GC 监控")]
        [Tooltip("是否启用 GC 回收事件日志输出")]
        [SerializeField]
        private bool _enableGCMonitoring = true;

        private int _lastGen0Count = -1;
        private int _lastGen1Count = -1;
        private int _lastGen2Count = -1;

        /// <summary>
        /// 上一次 GC 事件发生的游戏时间（秒）
        /// </summary>
        private float _lastGCEventTime = -1f;

        /// <summary>
        /// 监控启动时的游戏时间（秒），用于计算 GC 事件距启动的间隔
        /// </summary>
        private float _gcMonitorStartTime = 0f;

        /// <summary>
        /// GC 监控累计事件计数
        /// </summary>
        private int _gcEventCount = 0;

        /// <summary>
        /// 帧计时器：在每帧 Update 入口记录、出口停止，差值即为帧耗时（含 GC 暂停）
        /// </summary>
        private readonly Stopwatch _frameStopwatch = new Stopwatch();

        #endregion

        /// <summary>
        /// 初始化 GC 监控基线（在 Start 中调用）
        /// </summary>
        private void InitGCMonitor()
        {
            _lastGen0Count = GC.CollectionCount(0);
            _lastGen1Count = GC.CollectionCount(1);
            _lastGen2Count = GC.CollectionCount(2);
            _gcMonitorStartTime = Time.realtimeSinceStartup;
            _gcEventCount = 0;

            Debug.Log($"[GCMonitor] 初始化完成 | 基线: Gen0={_lastGen0Count}, Gen1={_lastGen1Count}, Gen2={_lastGen2Count}, " +
                      $"TotalMemory={GC.GetTotalMemory(false) / 1024}KB, " +
                      $"ProfilerAllocated={Profiler.GetTotalAllocatedMemoryLong() / 1024}KB");
        }

        /// <summary>
        /// 每帧检测 GC 回收事件，如有新回收则输出日志
        /// 在 Update() 中调用，传入上一帧的总耗时（含 GC 暂停）
        /// </summary>
        /// <param name="lastFrameMs">上一帧的实际耗时（毫秒），由帧计时器测量</param>
        private void CheckGCEvents(double lastFrameMs)
        {
            if (!_enableGCMonitoring)
                return;

            // 首次调用时初始化基线
            if (_lastGen0Count < 0)
            {
                InitGCMonitor();
                return;
            }

            int curGen0 = GC.CollectionCount(0);
            int curGen1 = GC.CollectionCount(1);
            int curGen2 = GC.CollectionCount(2);

            bool gen0Changed = curGen0 != _lastGen0Count;
            bool gen1Changed = curGen1 != _lastGen1Count;
            bool gen2Changed = curGen2 != _lastGen2Count;

            if (!gen0Changed && !gen1Changed && !gen2Changed)
                return;

            // 确定本次回收的最高代
            int maxGen = 0;
            if (gen2Changed) maxGen = 2;
            else if (gen1Changed) maxGen = 1;

            _gcEventCount++;

            float nowReal = Time.realtimeSinceStartup;
            float timeSinceStart = nowReal - _gcMonitorStartTime;
            float timeSinceLastGC = _lastGCEventTime > 0 ? nowReal - _lastGCEventTime : -1f;
            _lastGCEventTime = nowReal;

            long totalMemoryKB = GC.GetTotalMemory(false) / 1024;
            long profilerAllocatedKB = Profiler.GetTotalAllocatedMemoryLong() / 1024;
            long profilerReservedKB = Profiler.GetTotalReservedMemoryLong() / 1024;

            string timestamp = FormatTimestamp(DateTime.UtcNow);
            string intervalStr = timeSinceLastGC >= 0 ? $"{timeSinceLastGC:F2}s" : "N/A";

            // lastFrameMs 是上一帧的实际耗时，GC 暂停包含在其中
            // 由于 GC 在上一帧发生，该帧耗时即为含 GC 暂停的总帧耗时
            Debug.Log($"[GCMonitor] #{_gcEventCount} GC 回收事件 | " +
                      $"时间={timestamp}, " +
                      $"距启动={timeSinceStart:F2}s, " +
                      $"距上次GC={intervalStr}, " +
                      $"GC帧耗时={lastFrameMs:F2}ms, " +
                      $"最高回收代=Gen{maxGen}, " +
                      $"Gen0: {_lastGen0Count}→{curGen0}(+{curGen0 - _lastGen0Count}), " +
                      $"Gen1: {_lastGen1Count}→{curGen1}(+{curGen1 - _lastGen1Count}), " +
                      $"Gen2: {_lastGen2Count}→{curGen2}(+{curGen2 - _lastGen2Count}), " +
                      $"GC.TotalMemory={totalMemoryKB}KB, " +
                      $"Profiler.Allocated={profilerAllocatedKB}KB, " +
                      $"Profiler.Reserved={profilerReservedKB}KB");

            _lastGen0Count = curGen0;
            _lastGen1Count = curGen1;
            _lastGen2Count = curGen2;
        }
    }
}
