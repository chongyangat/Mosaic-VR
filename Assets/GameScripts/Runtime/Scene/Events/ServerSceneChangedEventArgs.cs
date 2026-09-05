using GameFramework;
using GameFramework.Event;

namespace GameMain
{
    /// <summary>
    /// 服务端场景改变后事件。
    /// </summary>
    public sealed class ServerSceneChangedEventArgs : GameEventArgs
    {
        /// <summary>
        /// 服务端场景改变后事件编号。
        /// </summary>
        public static readonly int EventId = typeof(ServerSceneChangedEventArgs).GetHashCode();

        /// <summary>
        /// 初始化服务端场景改变后事件的新实例。
        /// </summary>
        public ServerSceneChangedEventArgs()
        {
            NewSceneName = default;
            UserData = null;
        }

        /// <summary>
        /// 获取服务端场景改变后事件编号。
        /// </summary>
        public override int Id
        {
            get
            {
                return EventId;
            }
        }

        /// <summary>
        /// 获取新场景名称。
        /// </summary>
        public string NewSceneName
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
        /// 创建服务端场景改变后事件
        /// </summary>
        /// <param name="newSceneName">新场景名称</param>
        /// <param name="userData">用户自定义数据</param>
        /// <returns>创建的服务端场景改变后事件。</returns>
        public static ServerSceneChangedEventArgs Create(string newSceneName, object userData = null)
        {
            ServerSceneChangedEventArgs loadPackageEventArgs = ReferencePool.Acquire<ServerSceneChangedEventArgs>();
            loadPackageEventArgs.NewSceneName = newSceneName;
            loadPackageEventArgs.UserData = userData;
            return loadPackageEventArgs;
        }

        /// <summary>
        /// 清理服务端场景改变后事件。
        /// </summary>
        public override void Clear()
        {
            NewSceneName = default;
            UserData = null;
        }
    }
}
