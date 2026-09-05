using GameFramework.Event;
#if !MANAGER_SERVER
using Oculus.Interaction.Locomotion;
using System;
#endif
using UnityEngine;
using UnityGameFramework.Runtime;

namespace GameMain
{
    /// <summary>
    /// 传送角色。
    /// </summary>
    public sealed class TeleportPlayer : MonoBehaviour
#if !MANAGER_SERVER
        , ILocomotionEventBroadcaster
#endif
    {
        [SerializeField]
        private Transform m_CameraRig;

        [SerializeField]
        private Transform m_CenterEye;

#if !MANAGER_SERVER
        public static readonly int Identifier = typeof(TeleportPlayer).GetHashCode();

        public event Action<LocomotionEvent> WhenLocomotionPerformed;

        /// <summary>
        /// 上一次传送位置
        /// </summary>
        private Vector3? m_LastTeleportPos;

        /// <summary>
        /// 上一次传送朝向
        /// </summary>
        private Quaternion? m_LastTeleportRot;

        /// <summary>
        /// 重置视角：
        /// 注意：事件执行与centerEyeAnchor重置先后顺序不保证。可能本事件方法先执行，然后再更新centerEyeAnchor的位置，也可能反过来。
        /// </summary>
        private void OnRecenteredPose()
        {
            // 输出日志
            Log.Info("传送重置视角");
            // 重置视角后，如果之前有传送过，则重置视角后，需要将传送位置重置到初始位置，否则会回到传送位置。
            if (m_LastTeleportPos.HasValue)
            {
                // 输出日志
                Log.Info($"传送重置视角，重置传送位置至:{m_LastTeleportPos.Value}");
                m_CameraRig.position = m_LastTeleportPos.Value;
            }
            if (m_LastTeleportRot.HasValue)
            {
                // 输出日志
                Log.Info($"传送重置视角，重置传送朝向至:{m_LastTeleportRot.Value.eulerAngles}");
                m_CameraRig.rotation = m_LastTeleportRot.Value;
            }
        }

#endif

        private void OnTeleportPlayer(object sender, GameEventArgs e)
        {
            TeleportPlayerEventArgs ne = (TeleportPlayerEventArgs)e;
            if(ne == null)
            {
                return;
            }
#if MANAGER_SERVER
            // 移动主相机
            Camera.main.transform.position = ne.Position;
            Camera.main.transform.rotation = ne.Rotation;
#else
            // 移动OVR相机
            if (ne.IsUseCurViewPos)
            {
                // 传送位置和朝向将基于当前视觉位置与Camera Rig的偏移量进行计算，以达到视觉位置到达传送位置。
                // 但如果重置视角，则会去到一个预期外的位置，因此时Camera Rig有一个偏移量。
                var locomotionEvent = new LocomotionEvent(Identifier, new Pose(ne.Position, ne.Rotation),
                        LocomotionEvent.TranslationType.Absolute,
                        LocomotionEvent.RotationType.Absolute);
                WhenLocomotionPerformed?.Invoke(locomotionEvent);
            }
            else
            {
                // 传送位置和朝向将直接应用于Camera Rig，需要传送前后重置一下视角（或者让用户走回去初始视角点位）才能够到达预期的视觉位置。
                m_CameraRig.position = ne.Position;
                m_CameraRig.rotation = ne.Rotation;
            }
            //// TODO 临时 是否重置中心眼位置（头显会拉回）
            //if (ne.IsResetCenterEyePos)
            //{
            //    // 保留Y，高度让头显继续控制
            //    m_CenterEye.localPosition = new Vector3(0, m_CenterEye.localPosition.y, 0);
            //}
            // 保存传送位置
            m_LastTeleportPos = ne.Position;
            m_LastTeleportRot = ne.Rotation;
#endif
        }

        #region U3D

        private bool m_IsRegistered = false;

        private void OnEnable()
        {
#if !MANAGER_SERVER && !ENABLED_OVR_TRACKING_RESET_KEEPER
            OVRManager.display.RecenteredPose += OnRecenteredPose;
#endif            
            //GameModule.Event.Subscribe(TeleportPlayerEventArgs.EventId, OnTeleportPlayer);
        }

        private void Update()
        {
            if(!m_IsRegistered && GameModule.Event != null)
            {
                GameModule.Event.Subscribe(TeleportPlayerEventArgs.EventId, OnTeleportPlayer);
                m_IsRegistered = true;
            }
        }

        private void OnDisable()
        {
#if !MANAGER_SERVER && !ENABLED_OVR_TRACKING_RESET_KEEPER
            // 注销重置事件
            OVRManager.display.RecenteredPose -= OnRecenteredPose; 
#endif
            GameModule.Event.Unsubscribe(TeleportPlayerEventArgs.EventId, OnTeleportPlayer);
        }

        #endregion
    }
}
