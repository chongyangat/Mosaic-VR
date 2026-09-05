using UnityEngine;

namespace GameMain
{
    /// <summary>
    /// 系统时钟同步触发器
    /// 
    /// 使用普通 MonoBehaviour，不依赖 NetworkBehaviour，避免场景同步问题。
    /// 通过 Update 检测网络状态变化来触发同步。
    /// </summary>
    public class SystemClockSyncTrigger : MonoBehaviour
    {
        [Header("同步设置")]
        [Tooltip("是否在客户端连接后自动请求时间同步")]
        public bool autoRequestOnClientConnect = true;

        [Tooltip("是否在服务端启动时自动设置启动时间")]
        public bool autoSetOnServerStart = true;

        [Tooltip("周期性重同步间隔（秒），用于修正时钟漂移，0表示不重同步")]
        public float resyncInterval = 10f;

        private bool _hasRequested = false;
        private bool _hasSetServerTime = false;
        private float _lastResyncTime;

        private void Start()
        {
            SystemClockSync.Initialize();
        }

        private void Update()
        {
            if (autoSetOnServerStart && !_hasSetServerTime)
            {
                if (IsServer())
                {
                    SystemClockSync.SetServerStartupTime();
                    _hasSetServerTime = true;
                }
            }

            if (autoRequestOnClientConnect && !_hasRequested)
            {
                if (IsClientConnected() && !IsServer())
                {
                    SystemClockSync.RequestServerTime();
                    _hasRequested = true;
                    _lastResyncTime = Time.unscaledTime;
                }
            }

            // 周期性重同步，修正时钟漂移
            if (resyncInterval > 0 && _hasRequested && IsClientConnected() && !IsServer())
            {
                if (Time.unscaledTime - _lastResyncTime >= resyncInterval)
                {
                    SystemClockSync.RequestServerTime(force: true);
                    _lastResyncTime = Time.unscaledTime;
                }
            }
        }

        private bool IsServer()
        {
            return Mirror.NetworkServer.active;
        }

        private bool IsClientConnected()
        {
            return Mirror.NetworkClient.active && Mirror.NetworkClient.isConnected;
        }

        [ContextMenu("手动请求时间同步")]
        public void ManualRequestSync()
        {
            if (IsClientConnected())
            {
                SystemClockSync.RequestServerTime(force: true);
                _lastResyncTime = Time.unscaledTime;
            }
            else if (IsServer())
            {
                SystemClockSync.SetServerStartupTime();
                _hasSetServerTime = false;
            }
        }

        private void OnEnable()
        {
            _hasRequested = false;
            _hasSetServerTime = false;
        }
    }
}
