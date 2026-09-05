using GameFramework;
using GameFramework.Event;

namespace GameMain
{
    /// <summary>
    /// 房间连接状态改变后事件。
    /// </summary>
    public sealed class RoomDisOrConnectedChangedEventArgs : GameEventArgs
    {
        /// <summary>
        /// 房间连接状态改变后事件编号。
        /// </summary>
        public static readonly int EventId = typeof(RoomDisOrConnectedChangedEventArgs).GetHashCode();

        /// <summary>
        /// 初始化房间连接状态改变后事件的新实例。
        /// </summary>
        public RoomDisOrConnectedChangedEventArgs()
        {
            IsServer = false;
            IsConnected = false;
            UserData = null;
        }

        /// <summary>
        /// 获取房间连接状态改变后事件编号。
        /// </summary>
        public override int Id
        {
            get
            {
                return EventId;
            }
        }

        /// <summary>
        /// 获取是否是服务端上的连接状态。
        /// </summary>
        public bool IsServer
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取是否连接的状态。
        /// </summary>
        public bool IsConnected
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
        /// 创建房间连接状态改变后事件
        /// </summary>
        /// <param name="isServer">是否是服务端上的连接状态</param>
        /// <param name="isConnected">是否是连接的状态</param>
        /// <param name="userData">用户自定义数据</param>
        /// <returns>创建的房间连接状态改变后事件。</returns>
        public static RoomDisOrConnectedChangedEventArgs Create(bool isServer, bool isConnected, object userData = null)
        {
            RoomDisOrConnectedChangedEventArgs loadPackageEventArgs = ReferencePool.Acquire<RoomDisOrConnectedChangedEventArgs>();
            loadPackageEventArgs.IsServer = isServer;
            loadPackageEventArgs.IsConnected = isConnected;
            loadPackageEventArgs.UserData = userData;
            return loadPackageEventArgs;
        }

        /// <summary>
        /// 清理房间连接状态改变后事件。
        /// </summary>
        public override void Clear()
        {
            IsServer = false;
            IsConnected = false;
            UserData = null;
        }
    }
}
