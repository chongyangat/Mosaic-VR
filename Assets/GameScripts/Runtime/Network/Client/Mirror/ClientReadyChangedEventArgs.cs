using GameFramework;
using GameFramework.Event;

namespace GameMain
{
    /// <summary>
    /// 客户端就绪状态改变后事件。
    /// </summary>
    public sealed class ClientReadyChangedEventArgs : GameEventArgs
    {
        /// <summary>
        /// 客户端就绪状态改变后事件编号。
        /// </summary>
        public static readonly int EventId = typeof(ClientReadyChangedEventArgs).GetHashCode();

        /// <summary>
        /// 初始化客户端就绪状态改变后事件的新实例。
        /// </summary>
        public ClientReadyChangedEventArgs()
        {
            IsReady = false;
            UserData = null;
        }

        /// <summary>
        /// 获取客户端就绪状态改变后事件编号。
        /// </summary>
        public override int Id
        {
            get
            {
                return EventId;
            }
        }

        /// <summary>
        /// 获取是否就绪的状态。
        /// </summary>
        public bool IsReady
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
        /// 创建客户端就绪状态改变后事件
        /// </summary>
        /// <param name="isReady">是否就绪的状态</param>
        /// <param name="userData">用户自定义数据</param>
        /// <returns>创建的客户端就绪状态改变后事件。</returns>
        public static ClientReadyChangedEventArgs Create(bool isReady, object userData = null)
        {
            ClientReadyChangedEventArgs loadPackageEventArgs = ReferencePool.Acquire<ClientReadyChangedEventArgs>();
            loadPackageEventArgs.IsReady = isReady;
            loadPackageEventArgs.UserData = userData;
            return loadPackageEventArgs;
        }

        /// <summary>
        /// 清理客户端就绪状态改变后事件。
        /// </summary>
        public override void Clear()
        {
            IsReady = false;
            UserData = null;
        }
    }
}
