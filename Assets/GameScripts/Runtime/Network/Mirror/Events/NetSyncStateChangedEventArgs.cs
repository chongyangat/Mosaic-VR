using GameFramework;
using GameFramework.Event;

namespace GameMain
{
    /// <summary>
    /// 网络同步状态改变后事件。
    /// </summary>
    public sealed class NetSyncStateChangedEventArgs : GameEventArgs
    {
        /// <summary>
        /// 网络同步状态改变后事件编号。
        /// </summary>
        public static readonly int EventId = typeof(NetSyncStateChangedEventArgs).GetHashCode();

        /// <summary>
        /// 初始化网络同步状态改变后事件的新实例。
        /// </summary>
        public NetSyncStateChangedEventArgs()
        {
            Clear();
        }

        /// <summary>
        /// 获取网络同步状态改变后事件编号。
        /// </summary>
        public override int Id
        {
            get
            {
                return EventId;
            }
        }

        /// <summary>
        /// 获取网络同步状态名。
        /// </summary>
        public string StateName
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取网络同步状态。
        /// </summary>
        public string State
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
        /// 创建网络同步状态改变后事件
        /// </summary>
        /// <param name="stateName">网络同步状态名</param>
        /// <param name="state">网络同步状态</param>
        /// <param name="userData">用户自定义数据</param>
        /// <returns>创建的网络同步状态改变后事件。</returns>
        public static NetSyncStateChangedEventArgs Create(string stateName, string state, object userData = null)
        {
            var eventArgs = ReferencePool.Acquire<NetSyncStateChangedEventArgs>();
            eventArgs.StateName = stateName;
            eventArgs.State = state;
            eventArgs.UserData = userData;
            return eventArgs;
        }

        /// <summary>
        /// 清理网络同步状态改变后事件。
        /// </summary>
        public override void Clear()
        {
            StateName = default;
            UserData = null;
        }
    }
}
