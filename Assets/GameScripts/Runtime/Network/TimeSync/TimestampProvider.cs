using System;
using UnityEngine;
using GameLogic;
using Mirror;

namespace GameMain
{
    /// <summary>
    /// 时间戳提供器实现
    ///
    /// 实现 ITimestampProvider 接口，为 Packages 层（如 GazeDataSender）提供时间戳。
    ///
    /// 设计说明：
    /// - 由于 Unity 编译顺序问题，Packages 层不能直接引用 Assets 层
    /// - 因此在 Packages 层定义接口（抽象层），在 Assets 层实现（具体层）
    /// - 通过依赖注入的方式，将具体实现注入到 Packages 层的组件中
    ///
    /// 时间同步逻辑：
    /// - 使用 SystemClockSync 获取服务端操作系统时间
    /// - 每次客户端连接服务端时自动同步
    /// - 不依赖具体 NetworkBehaviour 对象
    /// </summary>
    public class TimestampProvider : MetaQuestProEyeGazeUDP.ITimestampProvider
    {
        // 高精度时间戳相关字段
        private static DateTime s_baseTime;
        private static long s_baseTimestamp;
        private static readonly object s_timeLock = new object();
        
        /// <summary>
        /// 获取高精度的Unix时间戳（毫秒）
        /// </summary>
        private static long GetHighPrecisionUnixTimeMilliseconds()
        {
            lock (s_timeLock)
            {
                // 初始化基准时间
                if (s_baseTime == default(DateTime))
                {
                    s_baseTime = DateTime.UtcNow;
                    s_baseTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
                }
                
                // 计算经过的时间，保留亚毫秒精度
                long elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - s_baseTimestamp;
                // 转换为毫秒，保留亚毫秒精度
                double elapsedMilliseconds = (double)elapsed / System.Diagnostics.Stopwatch.Frequency * 1000.0;
                
                // 计算最终的Unix时间戳
                DateTimeOffset baseDateTimeOffset = new DateTimeOffset(s_baseTime);
                return baseDateTimeOffset.ToUnixTimeMilliseconds() + (long)Math.Round(elapsedMilliseconds);
            }
        }
        
        /// <summary>
        /// 是否使用时钟偏移补偿
        /// </summary>
        private readonly bool _useClockCompensation;

        /// <summary>
        /// VR端与参考时钟的偏移量（毫秒）
        /// </summary>
        private long _clockOffsetMs;

        /// <summary>
        /// 是否已同步过
        /// </summary>
        private bool _isSynced;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="useClockCompensation">是否使用时钟偏移补偿，默认true</param>
        public TimestampProvider(bool useClockCompensation = true)
        {
            _useClockCompensation = useClockCompensation;
            _isSynced = false;

            SystemClockSync.Initialize();
            SystemClockSync.OnSyncCompleted += OnSyncStatusChanged;
        }

        /// <summary>
        /// 同步状态变化回调
        /// </summary>
        private void OnSyncStatusChanged()
        {
            _isSynced = SystemClockSync.IsSynced;
            _clockOffsetMs = SystemClockSync.CalculateSystemClockDifferenceMs();
            Debug.Log($"[TimestampProvider] Sync status changed - Synced: {_isSynced}, Offset: {_clockOffsetMs}ms");
        }

        /// <summary>
        /// 获取同步后的Unix时间戳（毫秒，含小数精度）
        ///
        /// 使用 SystemClockSync 获取服务端操作系统时间。
        /// 保留亚毫秒精度，避免整数截断导致延迟测量误差。
        /// </summary>
        /// <returns>Unix时间戳（毫秒，含小数）</returns>
        public long GetSyncedUnixTimeMilliseconds()
        {
            return (long)SystemClockSync.GetServerSystemTimeMsD();
        }

        /// <summary>
        /// 获取同步后的Unix时间戳（毫秒，高精度小数）
        /// </summary>
        /// <returns>Unix时间戳（毫秒，含小数）</returns>
        public double GetSyncedUnixTimeMillisecondsD()
        {
            return SystemClockSync.GetServerSystemTimeMsD();
        }

        /// <summary>
        /// 获取本地Unix时间戳（毫秒）
        /// </summary>
        /// <returns>本地Unix时间戳（毫秒）</returns>
        public long GetLocalUnixTimeMilliseconds()
        {
            return GetHighPrecisionUnixTimeMilliseconds();
        }

        /// <summary>
        /// 设置时钟偏移量
        /// </summary>
        /// <param name="offsetMs">时钟偏移量（毫秒）</param>
        public void SetClockOffset(long offsetMs)
        {
            _clockOffsetMs = offsetMs;
            _isSynced = offsetMs != 0;
            Debug.Log($"[TimestampProvider] Clock offset manually set to {offsetMs}ms");
        }

        /// <summary>
        /// 获取当前设置的时钟偏移量
        /// </summary>
        /// <returns>时钟偏移量（毫秒）</returns>
        public long GetClockOffset()
        {
            return _clockOffsetMs;
        }

        /// <summary>
        /// 获取是否已同步
        /// </summary>
        /// <returns>是否已成功同步</returns>
        public bool IsSynced()
        {
            return _isSynced;
        }

        /// <summary>
        /// 获取当前网络时间（RTT往返延迟）
        /// </summary>
        /// <returns>RTT（秒）</returns>
        public double GetNetworkRtt()
        {
            return NetworkTime.rtt;
        }

        /// <summary>
        /// 获取同步状态描述
        /// </summary>
        /// <returns>状态描述字符串</returns>
        public string GetSyncStatus()
        {
            return SystemClockSync.GetStatus();
        }

        /// <summary>
        /// 获取系统时钟偏移（毫秒）
        /// </summary>
        /// <returns>时钟偏移（毫秒）</returns>
        public long GetSystemClockOffsetMs()
        {
            return SystemClockSync.CalculateSystemClockDifferenceMs();
        }

        /// <summary>
        /// 销毁时清理
        /// </summary>
        public void Dispose()
        {
            SystemClockSync.OnSyncCompleted -= OnSyncStatusChanged;
        }
    }

    /// <summary>
    /// 时间戳提供者初始化器
    ///
    /// 负责在运行时将 TimestampProvider 注入到所有需要它的 Packages 层组件中。
    ///
    /// 使用方式：
    /// 在某个 Assets 层的初始化脚本的 Start() 或 Awake() 中调用 InitializeAll() 方法。
    /// </summary>
    public static class TimestampProviderInitializer
    {
        /// <summary>
        /// 已创建的提供器实例（单例）
        /// </summary>
        private static TimestampProvider _instance;

        /// <summary>
        /// 初始化并注入到所有需要的组件
        ///
        /// 扫描场景中的 GazeDataSender 组件，并注入时间戳提供器。
        /// </summary>
        /// <param name="useClockCompensation">是否启用时钟补偿</param>
        /// <returns>创建的时间戳提供器实例</returns>
        public static TimestampProvider InitializeAll(bool useClockCompensation = true)
        {
            if (_instance == null)
            {
                _instance = new TimestampProvider(useClockCompensation);

                SystemClockSync.Initialize();
            }

            InjectToGazeDataSender();

            Debug.Log($"[TimestampProviderInitializer] Initialization completed");
            Debug.Log($"[TimestampProviderInitializer] Status: {_instance.GetSyncStatus()}");
            return _instance;
        }

        /// <summary>
        /// 注入到 GazeDataSender 组件
        /// </summary>
        private static void InjectToGazeDataSender()
        {
            var gazeSenders = UnityEngine.Object.FindObjectsOfType<GazeDataSender>(true);

            foreach (var sender in gazeSenders)
            {
                if (sender != null)
                {
                    sender.SetTimestampProvider(_instance);
                    Debug.Log($"[TimestampProviderInitializer] Injected to GazeDataSender: {sender.gameObject.name}");
                }
            }

            if (gazeSenders.Length == 0)
            {
                Debug.Log("[TimestampProviderInitializer] No GazeDataSender found in scene");
            }
        }

        /// <summary>
        /// 获取已初始化的提供器实例
        /// </summary>
        /// <returns>提供器实例，如果未初始化则返回null</returns>
        public static TimestampProvider GetInstance()
        {
            return _instance;
        }

        /// <summary>
        /// 获取当前同步状态
        /// </summary>
        /// <returns>状态描述字符串</returns>
        public static string GetSyncStatus()
        {
            if (_instance == null)
                return "Not initialized";
            return _instance.GetSyncStatus();
        }

        /// <summary>
        /// 获取系统时钟同步状态
        /// </summary>
        /// <returns>状态描述字符串</returns>
        public static string GetSystemClockSyncStatus()
        {
            return SystemClockSync.GetStatus();
        }
    }
}
