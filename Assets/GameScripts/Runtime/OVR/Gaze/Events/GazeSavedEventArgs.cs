using GameFramework;
using GameFramework.Event;

namespace GameMain
{
    /// <summary>
    /// 眼动数据保存事件。
    /// </summary>
    public sealed class GazeSavedEventArgs : GameEventArgs
    {
        /// <summary>
        /// 眼动数据保存事件编号。
        /// </summary>
        public static readonly int EventId = typeof(GazeSavedEventArgs).GetHashCode();

        /// <summary>
        /// 初始化眼动数据保存事件的新实例。
        /// </summary>
        public GazeSavedEventArgs()
        {
            Clear();
        }

        /// <summary>
        /// 获取眼动数据保存事件编号。
        /// </summary>
        public override int Id
        {
            get
            {
                return EventId;
            }
        }

        /// <summary>
        /// 是否启用。
        /// </summary>
        public bool IsEnabled
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取数据保存路径。
        /// </summary>
        public string SavePath
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
        /// 创建眼动数据保存事件
        /// </summary>
        /// <param name="isEnable">是否启用</param>
        /// <param name="savePath">数据保存路径</param>
        /// <param name="userData">用户自定义数据</param>
        /// <returns>创建的眼动数据保存事件。</returns>
        public static GazeSavedEventArgs Create(bool isEnable, string savePath, object userData = null)
        {
            GazeSavedEventArgs loadPackageEventArgs = ReferencePool.Acquire<GazeSavedEventArgs>();
            loadPackageEventArgs.IsEnabled = isEnable;
            loadPackageEventArgs.SavePath = savePath;
            loadPackageEventArgs.UserData = userData;
            return loadPackageEventArgs;
        }

        /// <summary>
        /// 清理眼动数据保存事件。
        /// </summary>
        public override void Clear()
        {
            IsEnabled = default;
            SavePath = default;
            UserData = null;
        }
    }
}
