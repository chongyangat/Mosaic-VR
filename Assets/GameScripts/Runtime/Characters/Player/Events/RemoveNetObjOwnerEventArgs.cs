using GameFramework;
using GameFramework.Event;
using Mirror;

namespace GameMain
{
    /// <summary>
    /// 移除网络物体所有权事件。
    /// </summary>
    public sealed class RemoveNetObjOwnerEventArgs : GameEventArgs
    {
        /// <summary>
        /// 移除网络物体所有权事件编号。
        /// </summary>
        public static readonly int EventId = typeof(RemoveNetObjOwnerEventArgs).GetHashCode();

        /// <summary>
        /// 初始化移除网络物体所有权事件的新实例。
        /// </summary>
        public RemoveNetObjOwnerEventArgs()
        {
            NetID = default;
            UserData = null;
        }

        /// <summary>
        /// 获取移除网络物体所有权事件编号。
        /// </summary>
        public override int Id
        {
            get
            {
                return EventId;
            }
        }

        /// <summary>
        /// 获取网络物体ID。
        /// </summary>
        public NetworkIdentity NetID
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
        /// 创建移除网络物体所有权事件
        /// </summary>
        /// <param name="netId">网络物体ID</param>
        /// <param name="userData">用户自定义数据</param>
        /// <returns>创建的移除网络物体所有权事件。</returns>
        public static RemoveNetObjOwnerEventArgs Create(NetworkIdentity netId, object userData = null)
        {
            RemoveNetObjOwnerEventArgs loadPackageEventArgs = ReferencePool.Acquire<RemoveNetObjOwnerEventArgs>();
            loadPackageEventArgs.NetID = netId;
            loadPackageEventArgs.UserData = userData;
            return loadPackageEventArgs;
        }

        /// <summary>
        /// 清理移除网络物体所有权事件。
        /// </summary>
        public override void Clear()
        {
            NetID = default;
            UserData = null;
        }
    }
}
