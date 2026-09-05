using GameFramework;
using GameFramework.Event;

namespace GameMain
{
    /// <summary>
    /// 网络同步状态将改变事件。
    /// </summary>
    public sealed class NetSyncStateWillChangeEventArgs : GameEventArgs
    {
        /// <summary>
        /// 网络同步状态将改变事件编号。
        /// </summary>
        public static readonly int EventId = typeof(NetSyncStateWillChangeEventArgs).GetHashCode();

        /// <summary>
        /// 初始化网络同步状态将改变事件的新实例。
        /// </summary>
        public NetSyncStateWillChangeEventArgs()
        {
            Clear();
        }

        /// <summary>
        /// 获取网络同步状态将改变事件编号。
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
        /// 获取调用方式状态。
        /// </summary>
        public ECallState CallState
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取是否在游戏场景。
        /// </summary>
        public bool IsInGameScene
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
        /// 创建网络同步状态将改变事件
        /// </summary>
        /// <param name="stateName">网络同步状态名</param>
        /// <param name="state">网络同步状态</param>
        /// <param name="callState">调用方式状态</param>
        /// <param name="isInGameScene">是否在游戏场景</param>
        /// <param name="userData">用户自定义数据</param>
        /// <returns>创建的网络同步状态将改变事件。</returns>
        public static NetSyncStateWillChangeEventArgs Create(string stateName, string state, 
            ECallState callState = ECallState.CMD, bool isInGameScene = true, object userData = null)
        {
            var eventArgs = ReferencePool.Acquire<NetSyncStateWillChangeEventArgs>();
            eventArgs.StateName = stateName;
            eventArgs.State = state;
            eventArgs.CallState = callState;
            eventArgs.IsInGameScene = isInGameScene;
            eventArgs.UserData = userData;
            return eventArgs;
        }

        /// <summary>
        /// 清理网络同步状态将改变事件。
        /// </summary>
        public override void Clear()
        {
            StateName = default;
            UserData = null;
        }
    }

    public enum ECallState
    {
        /// <summary>
        /// Call this from a client to run this function on the server.
        /// </summary>
        CMD,

        /// <summary>
        /// The server uses a Remote Procedure Call (RPC) to run this function on clients.
        /// </summary>
        CLIENT_RPC,
    }
}
