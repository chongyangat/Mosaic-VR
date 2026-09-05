using System;
using UnityEngine;
using Mirror;

namespace GameMain
{
    public static class SystemClockSync
    {
        // 高精度时间戳相关字段
        private static DateTime s_baseTime;
        private static long s_baseTimestamp;
        private static readonly object s_timeLock = new object();

        // 时钟偏移量（毫秒）：serverTime - clientTime
        private static long _clockOffsetMs;
        private static bool _isSynced;
        private static bool _isInitialized;

        /// <summary>
        /// 获取高精度的Unix时间戳（毫秒，含小数）
        /// 保留亚毫秒精度，避免整数截断导致的 ±1ms 量化误差
        /// </summary>
        private static double GetHighPrecisionUnixTimeMillisecondsD()
        {
            lock (s_timeLock)
            {
                if (s_baseTime == default(DateTime))
                {
                    s_baseTime = DateTime.UtcNow;
                    s_baseTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
                }

                long elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - s_baseTimestamp;
                double elapsedMilliseconds = (double)elapsed / System.Diagnostics.Stopwatch.Frequency * 1000.0;

                DateTimeOffset baseDateTimeOffset = new DateTimeOffset(s_baseTime);
                return baseDateTimeOffset.ToUnixTimeMilliseconds() + elapsedMilliseconds;
            }
        }

        /// <summary>
        /// 获取高精度的Unix时间戳（毫秒，整数）
        /// 保留向后兼容性，内部使用高精度计算后取整
        /// </summary>
        private static long GetHighPrecisionUnixTimeMilliseconds()
        {
            return (long)GetHighPrecisionUnixTimeMillisecondsD();
        }

        public static event Action OnSyncCompleted;

        static SystemClockSync()
        {
            NetworkClient.RegisterHandler<SystemClockSyncMsg>(OnClientMessageReceived);
            NetworkServer.RegisterHandler<SystemClockSyncMsg>(OnServerMessageReceived);
            Debug.Log("[SystemClockSync] Static constructor - handlers registered");
        }

        public static void Initialize()
        {
            if (_isInitialized) return;
            _isInitialized = true;
            Debug.Log("[SystemClockSync] Initialized");
        }

        public static void Shutdown()
        {
            _isInitialized = false;
            _isSynced = false;
            _clockOffsetMs = 0;
            Debug.Log("[SystemClockSync] Shutdown");
        }

        [Client]
        private static void OnClientMessageReceived(SystemClockSyncMsg msg)
        {
            if (msg.isRequest) return;

            // NTP 式时钟偏移计算
            long clientReceiveTimeMs = GetHighPrecisionUnixTimeMilliseconds();
            long rtt = clientReceiveTimeMs - (long)msg.clientRequestTimeMs;
            long oneWayDelay = rtt / 2;

            // 计算新的时钟偏移
            long newOffset = (long)msg.serverResponseTimeMs + oneWayDelay - clientReceiveTimeMs;

            if (!_isSynced)
            {
                // 首次同步：直接使用新偏移
                _clockOffsetMs = newOffset;
            }
            else
            {
                // 后续重同步：使用 EMA 平滑过渡，避免时钟跳变
                // α=0.5 意味着新值和旧值各占一半权重
                _clockOffsetMs = (_clockOffsetMs + newOffset) / 2;
            }

            _isSynced = true;

            Debug.Log($"[SystemClockSync][{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] Synced - RTT: {rtt}ms, OneWay: {oneWayDelay}ms, NewOffset: {newOffset}ms, SmoothedOffset: {_clockOffsetMs}ms");

            OnSyncCompleted?.Invoke();
        }

        [Server]
        private static void OnServerMessageReceived(NetworkConnectionToClient conn, SystemClockSyncMsg msg)
        {
            if (!msg.isRequest) return;

            var response = new SystemClockSyncMsg
            {
                isRequest = false,
                clientRequestTimeMs = msg.clientRequestTimeMs,
                serverResponseTimeMs = (ulong)GetHighPrecisionUnixTimeMilliseconds()
            };
            conn.Send(response);

            Debug.Log($"[SystemClockSync][{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] Server sent time to client {conn.connectionId}");
        }

        [Server]
        public static void SetServerStartupTime()
        {
            if (!NetworkServer.active) return;
            _isSynced = true;
            Debug.Log($"[SystemClockSync][{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] Server started, using local time directly");
        }

        [Client]
        public static void RequestServerTime(bool force = false)
        {
            if (NetworkServer.active || !NetworkClient.active) return;
            if (_isSynced && !force) return;

            long clientRequestTimeMs = GetHighPrecisionUnixTimeMilliseconds();
            var msg = new SystemClockSyncMsg
            {
                isRequest = true,
                clientRequestTimeMs = (ulong)clientRequestTimeMs
            };
            NetworkClient.Send(msg);

            Debug.Log($"[SystemClockSync][{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] Client requesting server time (force={force})");
        }

        /// <summary>
        /// 获取服务器系统时间（毫秒，含小数精度）
        /// 用于延迟计算，避免整数截断导致 ±1ms 量化误差
        /// </summary>
        public static double GetServerSystemTimeMsD()
        {
            if (NetworkServer.active)
            {
                return GetHighPrecisionUnixTimeMillisecondsD();
            }
            else if (NetworkClient.active && _isSynced)
            {
                return GetHighPrecisionUnixTimeMillisecondsD() + _clockOffsetMs;
            }

            return GetHighPrecisionUnixTimeMillisecondsD();
        }

        /// <summary>
        /// 获取服务器系统时间（毫秒，整数）
        /// 保留向后兼容性
        /// </summary>
        public static ulong GetServerSystemTimeMs()
        {
            return (ulong)GetServerSystemTimeMsD();
        }

        public static long CalculateSystemClockDifferenceMs()
        {
            if (!NetworkClient.active || !_isSynced)
            {
                return 0;
            }
            return _clockOffsetMs;
        }

        public static bool IsSynced => _isSynced || NetworkServer.active;

        public static string GetStatus()
        {
            if (NetworkServer.active)
            {
                return $"Server | Synced: {_isSynced}";
            }
            else if (NetworkClient.active)
            {
                return $"Client | Synced: {_isSynced} | Offset: {_clockOffsetMs}ms";
            }
            else
            {
                return "Offline | Not connected";
            }
        }
    }

    public struct SystemClockSyncMsg : NetworkMessage
    {
        public bool isRequest;
        // 客户端发送请求时的本地时间（Unix ms）
        public ulong clientRequestTimeMs;
        // 服务器收到请求时的当前时间（Unix ms）
        public ulong serverResponseTimeMs;
    }
}
