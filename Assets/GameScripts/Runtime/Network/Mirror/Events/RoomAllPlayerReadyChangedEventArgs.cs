using GameFramework;
using GameFramework.Event;

namespace GameMain
{
    /// <summary>
    /// 房间所有用户就绪状态改变后事件。
    /// </summary>
    public sealed class RoomAllPlayerReadyChangedEventArgs : GameEventArgs
    {
        /// <summary>
        /// 房间所有用户就绪状态改变后事件编号。
        /// </summary>
        public static readonly int EventId = typeof(RoomAllPlayerReadyChangedEventArgs).GetHashCode();

        /// <summary>
        /// 初始化房间所有用户就绪状态改变后事件的新实例。
        /// </summary>
        public RoomAllPlayerReadyChangedEventArgs()
        {
            IsReady = false;
            UserData = null;
        }

        /// <summary>
        /// 获取房间所有用户就绪状态改变后事件编号。
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
        /// 创建房间所有用户就绪状态改变后事件
        /// </summary>
        /// <param name="isReady">是否就绪的状态</param>
        /// <param name="userData">用户自定义数据</param>
        /// <returns>创建的房间所有用户就绪状态改变后事件。</returns>
        public static RoomAllPlayerReadyChangedEventArgs Create(bool isReady, object userData = null)
        {
            RoomAllPlayerReadyChangedEventArgs loadPackageEventArgs = ReferencePool.Acquire<RoomAllPlayerReadyChangedEventArgs>();
            loadPackageEventArgs.IsReady = isReady;
            loadPackageEventArgs.UserData = userData;
            return loadPackageEventArgs;
        }

        /// <summary>
        /// 清理房间所有用户就绪状态改变后事件。
        /// </summary>
        public override void Clear()
        {
            IsReady = false;
            UserData = null;
        }
    }
}
