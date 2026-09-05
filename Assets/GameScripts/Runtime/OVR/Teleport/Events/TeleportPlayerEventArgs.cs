using GameFramework;
using GameFramework.Event;
using UnityEngine;

namespace GameMain
{
    /// <summary>
    /// 传送角色事件。
    /// </summary>
    public sealed class TeleportPlayerEventArgs : GameEventArgs
    {
        /// <summary>
        /// 传送角色事件编号。
        /// </summary>
        public static readonly int EventId = typeof(TeleportPlayerEventArgs).GetHashCode();

        /// <summary>
        /// 初始化传送角色事件的新实例。
        /// </summary>
        public TeleportPlayerEventArgs()
        {
            Clear();
        }

        /// <summary>
        /// 获取传送角色事件编号。
        /// </summary>
        public override int Id
        {
            get
            {
                return EventId;
            }
        }

        /// <summary>
        /// 获取传送位置。
        /// </summary>
        public Vector3 Position
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取传送朝向。
        /// </summary>
        public Quaternion Rotation
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取是否基于当前视觉位置传送。
        /// <br/>如果是，则传送位置和朝向将基于当前视觉位置与Camera Rig的偏移量进行计算，以达到视觉位置到达传送位置。
        /// 但如果重置视角，则会去到一个预期外的位置。
        /// <br/>如果否，则传送位置和朝向将直接应用于Camera Rig，需要传送前后重置一下视角才能够到达预期的视觉位置。
        /// </summary>
        public bool IsUseCurViewPos
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取是否重置中心眼位置。
        /// </summary>
        //public bool IsResetCenterEyePos
        //{
        //    get;
        //    private set;
        //}

        /// <summary>
        /// 获取用户自定义数据。
        /// </summary>
        public object UserData
        {
            get;
            private set;
        }

        /// <summary>
        /// 创建传送角色事件
        /// </summary>
        /// <param name="position">传送位置</param>
        /// <param name="rotation">传送朝向</param>
        /// <param name="isUseCurViewPos">
        /// 是否基于当前视觉位置传送。
        /// 获取是否基于当前视觉位置传送。
        /// <br/>如果是，则传送位置和朝向将基于当前视觉位置与Camera Rig的偏移量进行计算，以达到视觉位置到达传送位置。
        /// 但如果重置视角，则会去到一个预期外的位置。
        /// <br/>如果否，则传送位置和朝向将直接应用于Camera Rig，需要传送前后重置一下视角才能够到达预期的视觉位置。
        /// </param>
        /// <param name="userData">用户自定义数据</param>
        /// <returns>创建的传送角色事件。</returns>
        public static TeleportPlayerEventArgs Create(Vector3 position, Quaternion rotation, 
            bool isUseCurViewPos = true, 
            //bool isResetCenterEyePos = false,
            object userData = null)
        {
            TeleportPlayerEventArgs loadPackageEventArgs = ReferencePool.Acquire<TeleportPlayerEventArgs>();
            loadPackageEventArgs.Position = position;
            loadPackageEventArgs.Rotation = rotation;
            loadPackageEventArgs.IsUseCurViewPos = isUseCurViewPos;
            //loadPackageEventArgs.IsResetCenterEyePos = isResetCenterEyePos;
            loadPackageEventArgs.UserData = userData;
            return loadPackageEventArgs;
        }

        /// <summary>
        /// 清理传送角色事件。
        /// </summary>
        public override void Clear()
        {
            Position = default;
            Rotation = default;
            IsUseCurViewPos = true;
            UserData = null;
        }
    }
}
