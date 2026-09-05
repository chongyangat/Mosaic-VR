using GameFramework;
using GameFramework.Event;

namespace GameMain
{
    /// <summary>
    /// 创建网络对象事件。
    /// </summary>
    public sealed class CreateNetObjEventArgs : GameEventArgs
    {
        /// <summary>
        /// 创建网络对象事件编号。
        /// </summary>
        public static readonly int EventId = typeof(CreateNetObjEventArgs).GetHashCode();

        /// <summary>
        /// 初始化创建网络对象事件的新实例。
        /// </summary>
        public CreateNetObjEventArgs()
        {
            Clear();
        }

        /// <summary>
        /// 获取创建网络对象事件编号。
        /// </summary>
        public override int Id
        {
            get
            {
                return EventId;
            }
        }

        /// <summary>
        /// 获取网络对象预置体。
        /// </summary>
        public UnityEngine.GameObject ObjPrefab
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
        /// 创建创建网络对象事件
        /// </summary>
        /// <param name="objPrefab">网络对象预置体</param>
        /// <param name="userData">用户自定义数据</param>
        /// <returns>创建的创建网络对象事件。</returns>
        public static CreateNetObjEventArgs Create(UnityEngine.GameObject objPrefab, object userData = null)
        {
            CreateNetObjEventArgs loadPackageEventArgs = ReferencePool.Acquire<CreateNetObjEventArgs>();
            loadPackageEventArgs.ObjPrefab = objPrefab;
            loadPackageEventArgs.UserData = userData;
            return loadPackageEventArgs;
        }

        /// <summary>
        /// 清理创建网络对象事件。
        /// </summary>
        public override void Clear()
        {
            ObjPrefab = default;
            UserData = default;
        }
    }
}
