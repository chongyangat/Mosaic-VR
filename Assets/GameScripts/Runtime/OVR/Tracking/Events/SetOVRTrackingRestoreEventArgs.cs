using GameFramework;
using GameFramework.Event;
using UnityEngine;

namespace GameMain
{
    /// <summary>
    /// 设置OVR追踪恢复事件。
    /// </summary>
    public sealed class SetOVRTrackingRestoreEventArgs : GameEventArgs
    {
        /// <summary>
        /// 设置OVR追踪恢复事件编号。
        /// </summary>
        public static readonly int EventId = typeof(SetOVRTrackingRestoreEventArgs).GetHashCode();

        /// <summary>
        /// 初始化设置OVR追踪恢复事件的新实例。
        /// </summary>
        public SetOVRTrackingRestoreEventArgs()
        {
            Clear();
        }

        /// <summary>
        /// 获取设置OVR追踪恢复事件编号。
        /// </summary>
        public override int Id
        {
            get
            {
                return EventId;
            }
        }

        /// <summary>
        /// 是否开启。
        /// </summary>
        public bool IsEnable
        {
            get;
            private set;
        }

        /// <summary>
        /// 是否重新开始。
        /// </summary>
        public bool IsRestart
        {
            get;
            private set;
        }

        /// <summary>
        /// 恢复的位置。
        /// </summary>
        public Vector3? Position
        {
            get;
            private set;
        }

        /// <summary>
        /// 恢复的朝向。
        /// </summary>
        public Quaternion? Rotation
        {
            get;
            private set;
        }

        /// <summary>
        /// 是否显示位置日志。
        /// </summary>
        public bool IsShowPositionLog
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
        /// 创建设置OVR追踪恢复事件
        /// </summary>
        /// <param name="isEnable">是否开启</param>
        /// <param name="isRestart">是否重新开始，如果传入方位信息，则此项不能为true</param>
        /// <param name="position">位置</param>
        /// <param name="rotation">朝向</param>
        /// <param name="isShowPositionLog">是否显示位置日志</param>
        /// <param name="userData">用户自定义数据</param>
        /// <returns>创建的设置OVR追踪恢复事件。</returns>
        public static SetOVRTrackingRestoreEventArgs Create(bool isEnable, bool isRestart = true, 
            Vector3? position = null, Quaternion? rotation = null, bool isShowPositionLog = false, object userData = null)
        {
            SetOVRTrackingRestoreEventArgs loadPackageEventArgs = ReferencePool.Acquire<SetOVRTrackingRestoreEventArgs>();
            loadPackageEventArgs.IsEnable = isEnable;
            loadPackageEventArgs.IsRestart = isRestart;
            loadPackageEventArgs.Position = position;
            loadPackageEventArgs.Rotation = rotation;
            loadPackageEventArgs.IsShowPositionLog = isShowPositionLog;
            loadPackageEventArgs.UserData = userData;
            return loadPackageEventArgs;
        }

        /// <summary>
        /// 清理设置OVR追踪恢复事件。
        /// </summary>
        public override void Clear()
        {
            IsEnable = default;
            IsRestart = true;
            Position = default;
            Rotation = default;
            IsShowPositionLog = default;
            UserData = null;
        }

    }
}
