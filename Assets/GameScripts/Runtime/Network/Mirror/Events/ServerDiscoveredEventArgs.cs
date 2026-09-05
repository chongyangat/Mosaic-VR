using GameFramework;
using GameFramework.Event;

namespace GameMain
{
    /// <summary>
    /// 发现服务器事件。
    /// </summary>
    public sealed class ServerDiscoveredEventArgs : GameEventArgs
    {
        /// <summary>
        /// 发现服务器事件编号。
        /// </summary>
        public static readonly int EventId = typeof(ServerDiscoveredEventArgs).GetHashCode();

        /// <summary>
        /// 初始化发现服务器事件的新实例。
        /// </summary>
        public ServerDiscoveredEventArgs()
        {
            ServerIp = default;
            UserData = null;
        }

        /// <summary>
        /// 获取发现服务器事件编号。
        /// </summary>
        public override int Id
        {
            get
            {
                return EventId;
            }
        }

        /// <summary>
        /// 获取服务器IP。
        /// </summary>
        public string ServerIp
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取用户自定义数据。
        /// </summary>
        public object UserData
        {
            get;
            private set;
        }

        /// <summary>
        /// 创建发现服务器事件
        /// </summary>
        /// <param name="serverIp">服务器IP</param>
        /// <param name="userData">用户自定义数据</param>
        /// <returns>创建的发现服务器事件。</returns>
        public static ServerDiscoveredEventArgs Create(string serverIp, object userData = null)
        {
            ServerDiscoveredEventArgs eventArgs = ReferencePool.Acquire<ServerDiscoveredEventArgs>();
            eventArgs.ServerIp = serverIp;
            eventArgs.UserData = userData;
            return eventArgs;
        }

        /// <summary>
        /// 清理发现服务器事件。
        /// </summary>
        public override void Clear()
        {
            ServerIp = default;
            UserData = null;
        }
    }
}
