using GameFramework;
using GameFramework.Event;

namespace GameMain
{
    /// <summary>
    /// 客户端场景改变后事件。
    /// </summary>
    public sealed class ClientSceneChangedEventArgs : GameEventArgs
    {
        /// <summary>
        /// 客户端场景改变后事件编号。
        /// </summary>
        public static readonly int EventId = typeof(ClientSceneChangedEventArgs).GetHashCode();

        /// <summary>
        /// 初始化客户端场景改变后事件的新实例。
        /// </summary>
        public ClientSceneChangedEventArgs()
        {
            NewSceneName = null;
            UserData = null;
        }

        /// <summary>
        /// 获取客户端场景改变后事件编号。
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
        /// 创建客户端场景改变后事件
        /// </summary>
        /// <param name="newSceneName">新场景名称</param>
        /// <param name="userData">用户自定义数据</param>
        /// <returns>创建的客户端场景改变后事件。</returns>
        public static ClientSceneChangedEventArgs Create(string newSceneName, object userData = null)
        {
            ClientSceneChangedEventArgs loadPackageEventArgs = ReferencePool.Acquire<ClientSceneChangedEventArgs>();
            loadPackageEventArgs.NewSceneName = newSceneName;
            loadPackageEventArgs.UserData = userData;
            return loadPackageEventArgs;
        }

        /// <summary>
        /// 清理客户端场景改变后事件。
        /// </summary>
        public override void Clear()
        {
            NewSceneName = null;
            UserData = null;
        }
    }
}
