using GameFramework;
using GameFramework.Event;
using Mirror;
using UnityEngine;

namespace GameMain
{
    /// <summary>
    /// 更新头显信息事件。
    /// </summary>
    public sealed class UpdateHMDInfoEventArgs : GameEventArgs
    {
        /// <summary>
        /// 更新头显信息事件编号。
        /// </summary>
        public static readonly int EventId = typeof(UpdateHMDInfoEventArgs).GetHashCode();

        /// <summary>
        /// 初始化更新头显信息事件的新实例。
        /// </summary>
        public UpdateHMDInfoEventArgs()
        {
            Clear();
        }

        /// <summary>
        /// 获取更新头显信息事件编号。
        /// </summary>
        public override int Id
        {
            get
            {
                return EventId;
            }
        }

        /// <summary>
        /// 头显位置。
        /// </summary>
        public Vector3 Position
        {
            get;
            private set;
        }

        /// <summary>
        /// 头显朝向。
        /// </summary>
        public Vector3 Rotation
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
        /// 创建更新头显信息事件
        /// </summary>
        /// <param name="pos">头显位置</param>
        /// <param name="rot">头显朝向</param>
        /// <returns>创建的更新头显信息事件。</returns>
        public static UpdateHMDInfoEventArgs Create(Vector3 pos, Vector3 rot, object userData = null)
        {
            UpdateHMDInfoEventArgs loadPackageEventArgs = ReferencePool.Acquire<UpdateHMDInfoEventArgs>();
            loadPackageEventArgs.Position = pos;
            loadPackageEventArgs.Rotation = rot;
            loadPackageEventArgs.UserData = userData;
            return loadPackageEventArgs;
        }

        /// <summary>
        /// 清理更新头显信息事件。
        /// </summary>
        public override void Clear()
        {
            Position = default;
            Rotation = default;
            UserData = null;
        }
    }
}
