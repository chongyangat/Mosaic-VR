using GameFramework;
using GameFramework.Event;
using Mirror;

namespace GameMain
{
    /// <summary>
    /// 分配网络物体所有权事件。
    /// </summary>
    public sealed class AssignNetObjOwnerEventArgs : GameEventArgs
    {
        /// <summary>
        /// 分配网络物体所有权事件编号。
        /// </summary>
        public static readonly int EventId = typeof(AssignNetObjOwnerEventArgs).GetHashCode();

        /// <summary>
        /// 初始化分配网络物体所有权事件的新实例。
        /// </summary>
        public AssignNetObjOwnerEventArgs()
        {
            NetID = default;
            UserData = null;
        }

        /// <summary>
        /// 获取分配网络物体所有权事件编号。
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
        /// 创建分配网络物体所有权事件
        /// </summary>
        /// <param name="netId">网络物体ID</param>
        /// <param name="userData">用户自定义数据</param>
        /// <returns>创建的分配网络物体所有权事件。</returns>
        public static AssignNetObjOwnerEventArgs Create(NetworkIdentity netId, object userData = null)
        {
            AssignNetObjOwnerEventArgs loadPackageEventArgs = ReferencePool.Acquire<AssignNetObjOwnerEventArgs>();
            loadPackageEventArgs.NetID = netId;
            loadPackageEventArgs.UserData = userData;
            return loadPackageEventArgs;
        }

        /// <summary>
        /// 清理分配网络物体所有权事件。
        /// </summary>
        public override void Clear()
        {
            NetID = default;
            UserData = null;
        }
    }
}
