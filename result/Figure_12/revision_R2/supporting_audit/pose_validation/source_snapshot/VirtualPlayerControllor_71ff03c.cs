using GameFramework.Event;
using Mirror;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace GameMain
{
    /// <summary>
    /// 虚拟玩家控制器
    /// </summary>
    public class VirtualPlayerControllor : NetworkBehaviour
    {
        #region 网络同步状态改变

        /// <summary>
        /// 网络同步状态改变
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnNetSyncStateWillChange(object sender, GameEventArgs e)
        {
            var ne = (NetSyncStateWillChangeEventArgs)e;
            if (ne == null)
            {
                return;
            }
            if (!isLocalPlayer)
            {
                return;
            }
            if (!ne.IsInGameScene)
            {
                return;
            }
            // 输出日志
            Log.Info($"网络同步状态将改变:{ne.StateName} {ne.State}");
            // 如果ne.UserData不为空，则序列化为Json字符串，网络同步不支持传输object类型
            string userData = "";
            if (ne.UserData != null && ne.UserData is not string)
            {
                userData = JsonUtility.ToJson(ne.UserData);
            }
            switch (ne.CallState)
            {
                case ECallState.CMD:
                    CmdNetSyncStateChange(ne.StateName, ne.State, userData);
                    break;
                case ECallState.CLIENT_RPC:
                    RpcNetSyncStateChange(ne.StateName, ne.State, userData);
                    break;
                default:
                    // 输出日志
                    Log.Warning($"未知的网络同步状态改变调用方式:{ne.CallState}");
                    return;
            }
        }

        /// <summary>
        /// 设置网络同步状态
        /// </summary>
        /// <param name="stateName"></param>
        /// <param name="state"></param>
        /// <param name="data"></param>
        [Command]
        void CmdNetSyncStateChange(string stateName, string state, string data)
        {
            // 输出日志
            Log.Info($"网络同步状态已改变:{stateName} {state} {data}");
            // 触发事件
            GameModule.Event.Fire(NetSyncStateChangedEventArgs.EventId, NetSyncStateChangedEventArgs.Create(stateName, state, data));
        }

        /// <summary>
        /// 设置网络同步状态
        /// </summary>
        /// <param name="stateName"></param>
        /// <param name="state"></param>
        /// <param name="data"></param>
        [ClientRpc]
        void RpcNetSyncStateChange(string stateName, string state, string data)
        {
            // 输出日志
            Log.Info($"网络同步状态已改变:{stateName} {state} {data}");
            // 
            GameModule.Event.Fire(NetSyncStateChangedEventArgs.EventId, NetSyncStateChangedEventArgs.Create(stateName, state, data));
        }

        #endregion

        #region 生成网络物体

        /// <summary>
        /// 生成网络物体
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnCreateNetObj(object sender, GameEventArgs e)
        {
            var ne = (CreateNetObjEventArgs)e;
            if (ne == null)
            {
                return;
            }
            if (!isLocalPlayer)
            {
                return;
            }
            // TODO: ObjPrefab未生成，不能序列化，传过去。
            CmdCreateNetObj(ne.ObjPrefab);
        }

        /// <summary>
        /// 生成网络物体
        /// </summary>
        /// <param name="netObjPrefab"></param>
        [Command]
        void CmdCreateNetObj(GameObject netObjPrefab)
        {
            NetworkServer.Spawn(netObjPrefab, connectionToClient);
        }

        #endregion

        #region 客户端就绪状态

        private void OnClientReadyChanged(object sender, GameEventArgs e)
        {
            var ne = (ClientReadyChangedEventArgs)e;
            if (ne == null)
            {
                return;
            }
            if (!isLocalPlayer)
            {
                return;
            }
            // 输出日志 
            Debug.Log($"OnClientReadyChanged:{ne.IsReady}");
            //
            CmdSetCleintReady(ne.IsReady);
        }

        [Command]
        void CmdSetCleintReady(bool isReady)
        {
            // 输出日志
            Debug.Log($"CmdSetCleintReady:{isReady}");
            if (isReady)
            {
                NetworkServer.SetClientReady(connectionToClient);
            }
            else
            {
                NetworkServer.SetClientNotReady(connectionToClient);
            }
        }

        #endregion

        #region 网络物体权限分配

        private void OnReassignNetObjOwner(object sender, GameEventArgs e)
        {
            var ne = (AssignNetObjOwnerEventArgs)e;
            if (ne == null)
            {
                return;
            }
            if (!isLocalPlayer)
            {
                return;
            }
            CmdReassignNetObjOwner(ne.NetID);
        }

        private void OnRemoveNetObjOwner(object sender, GameEventArgs e)
        {
            var ne = (RemoveNetObjOwnerEventArgs)e;
            if (ne == null)
            {
                return;
            }
            if (!isLocalPlayer)
            {
                return;
            }
            CmdRemoveNetObjOwner(ne.NetID);
        }

        ///<summary>
        /// 重新分配网络物体所有者
        /// </summary>
        /// <param name="objId"></param>
        [Command]
        void CmdReassignNetObjOwner(NetworkIdentity objId)
        {
            objId.AssignClientAuthority(connectionToClient);
            // 输出日志
            Debug.Log("Reassign authority to network object " + objId + " for client " + connectionToClient);
        }

        ///<summary>
        /// 移除网络物体所有者
        /// </summary>
        /// <param name="objId"></param>
        [Command]
        void CmdRemoveNetObjOwner(NetworkIdentity objId)
        {
            objId.RemoveClientAuthority();
            // 输出日志
            Debug.Log("Remove authority from network object " + objId);
        }

        #endregion

        #region 同步位置

        /// <summary>
        /// 头显的朝向（欧拉角）
        /// </summary>
        [SyncVar]
        private Vector3 m_PlayerDir;

        /// <summary>
        /// Quest CenterEyeAnchor 的 Step.Render 预测位姿和读取时间。该
        /// NetworkBehaviour 使用 ClientToServer 同步方向，因此这些字段与新增的
        /// Immediate raw 位姿按既有 Mirror 节拍一起上送。
        /// </summary>
        [SyncVar]
        private Vector3 m_QuestTrackingSpacePosition;

        [SyncVar]
        private Vector3 m_QuestTrackingSpaceRotation;

        [SyncVar]
        private Quaternion m_QuestTrackingSpaceQuaternion;

        [SyncVar]
        private Vector3 m_QuestWorldPosition;

        [SyncVar]
        private Quaternion m_QuestWorldQuaternion;

        [SyncVar]
        private double m_QuestPoseTimestampMs;

        [SyncVar]
        private uint m_QuestPoseSequence;

        [SyncVar]
        private Vector3 m_QuestRawTrackingSpacePosition;

        [SyncVar]
        private Quaternion m_QuestRawTrackingSpaceQuaternion;

        [SyncVar]
        private Vector3 m_QuestRawWorldPosition;

        [SyncVar]
        private Quaternion m_QuestRawWorldQuaternion;

        [SyncVar]
        private double m_QuestPoseReadTimeOvrSeconds;

        [SyncVar]
        private double m_QuestRawPoseSampleTimeOvrSeconds;

        [SyncVar]
        private double m_QuestPredictedDisplayTimeOvrSeconds;

        [SyncVar]
        private float m_QuestPredictionHorizonMs;

        [SyncVar]
        private float m_QuestRawSampleAgeMs;

        [SyncVar]
        private int m_QuestUnityFrame;

        [SyncVar]
        private byte m_QuestPoseSamplePhase;

        [SyncVar]
        private bool m_QuestRawPoseValid;

        [SyncVar]
        private bool m_QuestRenderPoseTimeValid;

        /// <summary>
        /// 头显的朝向（欧拉角）
        /// </summary>
        public Vector3 PlayerDir => m_PlayerDir;

#if MANAGER_SERVER
        /// <summary>
        /// 更新头显方位
        /// </summary>
        private void UpdateHDMPosDir()
        {
            GameModule.Event.Fire(UpdateHMDInfoEventArgs.EventId, 
                // 自身的位置就是用户端HMD同步过来的数据
                UpdateHMDInfoEventArgs.Create(
                    m_QuestPoseSequence != 0
                        ? m_QuestWorldPosition
                        : transform.position,
                    m_PlayerDir,
                    m_QuestTrackingSpacePosition,
                    m_QuestTrackingSpaceRotation,
                    m_QuestPoseTimestampMs,
                    m_QuestPoseSequence,
                    netId,
                    worldQuaternion: m_QuestWorldQuaternion,
                    trackingSpaceQuaternion: m_QuestTrackingSpaceQuaternion,
                    rawTrackingSpacePosition: m_QuestRawTrackingSpacePosition,
                    rawTrackingSpaceQuaternion: m_QuestRawTrackingSpaceQuaternion,
                    rawWorldPosition: m_QuestRawWorldPosition,
                    rawWorldQuaternion: m_QuestRawWorldQuaternion,
                    poseReadTimeOvrSeconds: m_QuestPoseReadTimeOvrSeconds,
                    rawPoseSampleTimeOvrSeconds: m_QuestRawPoseSampleTimeOvrSeconds,
                    predictedDisplayTimeOvrSeconds: m_QuestPredictedDisplayTimeOvrSeconds,
                    predictionHorizonMs: m_QuestPredictionHorizonMs,
                    rawSampleAgeMs: m_QuestRawSampleAgeMs,
                    questUnityFrame: m_QuestUnityFrame,
                    samplePhase: (QuestPoseSamplePhase)m_QuestPoseSamplePhase,
                    rawPoseValid: m_QuestRawPoseValid,
                    renderPoseTimeValid: m_QuestRenderPoseTimeValid));
        }
#else
        /// <summary>
        /// 角色对象（头显两眼中间的位置）
        /// </summary>
        private Transform m_PlayerTransform;
        private OVRCameraRig m_CameraRig;

        private const string PlayerTransformPath =
            "UserClient/[BuildingBlock] Camera Rig/TrackingSpace/CenterEyeAnchor";
        private const float MinQuaternionSqrMagnitude = 0.000001f;
        private const float InvalidPoseWarningInterval = 5f;

        private Vector3 m_LastValidPosition;
        private Quaternion m_LastValidRotation = Quaternion.identity;
        private bool m_HasLastValidPose;
        private float m_NextInvalidPoseWarningTime;
        private int m_LastAnchorSampleFrame = -1;
        private bool m_LoggedImmediatePoseUnavailable;

        /// <summary>
        /// 同步位置
        /// </summary>
        private void UpdateByPlayer(QuestPoseSamplePhase samplePhase)
        {
            if (m_PlayerTransform == null)
            {
                return;
            }

            Vector3 playerPosition = m_PlayerTransform.position;
            Quaternion playerRotation = m_PlayerTransform.rotation;
            Vector3 trackingSpacePosition = m_PlayerTransform.localPosition;
            Quaternion trackingSpaceRotation = m_PlayerTransform.localRotation;

            // QuestTimestampMs/PoseReadTime 是调用位姿 API 时的“读取时刻”。保留
            // Unix 时间用于原有四时间戳同步，同时记录 OVR 单调时钟；两者都不
            // 冒充传感器采样时刻。
            double questPoseTimestampMs = UDPClockSync.GetLocalUnixTimeMsD();
            double poseReadTimeOvrSeconds = OVRPlugin.GetTimeInSeconds();
            OVRPlugin.PoseStatef rawPoseState =
                OVRPlugin.GetNodePoseStateImmediate(OVRPlugin.Node.EyeCenter);
            OVRPlugin.PoseStatef renderPoseState =
                OVRPlugin.GetNodePoseStateRaw(
                    OVRPlugin.Node.EyeCenter,
                    OVRPlugin.Step.Render);

            bool rawPoseValid = TryGetRawPose(
                rawPoseState,
                out Vector3 rawTrackingSpacePosition,
                out Quaternion rawTrackingSpaceRotation);
            Transform trackingSpace = m_CameraRig != null
                ? m_CameraRig.trackingSpace
                : m_PlayerTransform.parent;
            Vector3 rawWorldPosition = default;
            Quaternion rawWorldRotation = default;
            if (rawPoseValid && trackingSpace != null)
            {
                rawWorldPosition = trackingSpace.TransformPoint(rawTrackingSpacePosition);
                rawWorldRotation = trackingSpace.rotation * rawTrackingSpaceRotation;
                rawPoseValid = IsFinite(rawWorldPosition)
                    && TryNormalize(rawWorldRotation, out rawWorldRotation);
            }
            else
            {
                rawPoseValid = false;
            }

            double rawPoseSampleTimeOvrSeconds = rawPoseValid
                && IsPositiveFinite(rawPoseState.Time)
                    ? rawPoseState.Time
                    : 0d;
            double predictedDisplayTimeOvrSeconds =
                IsPositiveFinite(renderPoseState.Time)
                    ? renderPoseState.Time
                    : 0d;
            bool renderPoseTimeValid = IsPositiveFinite(poseReadTimeOvrSeconds)
                && IsPositiveFinite(predictedDisplayTimeOvrSeconds);
            float predictionHorizonMs = renderPoseTimeValid
                ? (float)((predictedDisplayTimeOvrSeconds - poseReadTimeOvrSeconds) * 1000d)
                : 0f;
            float rawSampleAgeMs = rawPoseSampleTimeOvrSeconds > 0d
                && IsPositiveFinite(poseReadTimeOvrSeconds)
                    ? (float)((poseReadTimeOvrSeconds - rawPoseSampleTimeOvrSeconds) * 1000d)
                    : 0f;
            if (!TryNormalize(playerRotation, out Quaternion normalizedPlayerRotation)
                || !TryNormalize(trackingSpaceRotation, out Quaternion normalizedTrackingRotation)
                || !IsFinite(playerPosition)
                || !IsFinite(trackingSpacePosition)
                || !IsPositiveFinite(questPoseTimestampMs))
            {
                RestoreLastValidPoseIfNeeded();
                WarnInvalidPlayerPose(playerPosition, playerRotation);
                return;
            }

            Vector3 playerDirection = normalizedPlayerRotation.eulerAngles;
            Vector3 trackingSpaceDirection = normalizedTrackingRotation.eulerAngles;
            if (!IsFinite(playerDirection) || !IsFinite(trackingSpaceDirection))
            {
                RestoreLastValidPoseIfNeeded();
                WarnInvalidPlayerPose(playerPosition, playerRotation);
                return;
            }

            // 只同步 Y 轴旋转。不要从当前玩家 Transform 回读 X/Z：
            // 一旦某帧 XR 跟踪返回 NaN，回读会让非法旋转永久自我保持。
            Quaternion yawRotation = Quaternion.Euler(0f, playerDirection.y, 0f);
            if (!TryNormalize(yawRotation, out Quaternion normalizedYawRotation))
            {
                RestoreLastValidPoseIfNeeded();
                WarnInvalidPlayerPose(playerPosition, playerRotation);
                return;
            }

            transform.SetPositionAndRotation(playerPosition, normalizedYawRotation);
            m_PlayerDir = playerDirection;
            m_QuestTrackingSpacePosition = trackingSpacePosition;
            m_QuestTrackingSpaceRotation = trackingSpaceDirection;
            m_QuestTrackingSpaceQuaternion = trackingSpaceRotation;
            m_QuestWorldPosition = playerPosition;
            m_QuestWorldQuaternion = playerRotation;
            m_QuestPoseTimestampMs = questPoseTimestampMs;
            m_QuestRawTrackingSpacePosition = rawTrackingSpacePosition;
            m_QuestRawTrackingSpaceQuaternion = rawTrackingSpaceRotation;
            m_QuestRawWorldPosition = rawWorldPosition;
            m_QuestRawWorldQuaternion = rawWorldRotation;
            m_QuestPoseReadTimeOvrSeconds = poseReadTimeOvrSeconds;
            m_QuestRawPoseSampleTimeOvrSeconds = rawPoseSampleTimeOvrSeconds;
            m_QuestPredictedDisplayTimeOvrSeconds = predictedDisplayTimeOvrSeconds;
            m_QuestPredictionHorizonMs = predictionHorizonMs;
            m_QuestRawSampleAgeMs = rawSampleAgeMs;
            m_QuestUnityFrame = Time.frameCount;
            m_QuestPoseSamplePhase = (byte)samplePhase;
            m_QuestRawPoseValid = rawPoseValid;
            m_QuestRenderPoseTimeValid = renderPoseTimeValid;
            unchecked
            {
                m_QuestPoseSequence++;
                if (m_QuestPoseSequence == 0)
                {
                    m_QuestPoseSequence = 1;
                }
            }
            CacheValidPose(playerPosition, normalizedYawRotation);

            if (!rawPoseValid && !m_LoggedImmediatePoseUnavailable)
            {
                m_LoggedImmediatePoseUnavailable = true;
                Log.Warning(
                    "[VirtualPlayer] OVRPlugin Immediate EyeCenter pose is unavailable; "
                    + "render-predicted pose continues to sync and raw CSV fields remain empty.");
            }
        }

        private bool TryGetRawPose(
            OVRPlugin.PoseStatef poseState,
            out Vector3 position,
            out Quaternion rotation)
        {
            OVRPose rawPose = poseState.Pose.ToOVRPose();
            position = rawPose.position;
            rotation = rawPose.orientation;
            // v72 在 native 调用失败时返回 PoseStatef.identity；它的 Time 为 0。
            // 必须同时检查 Time，避免把 identity 错记成有效 raw HMD 位姿。
            return IsPositiveFinite(poseState.Time)
                && OVRPlugin.GetNodePositionValid(OVRPlugin.Node.EyeCenter)
                && OVRPlugin.GetNodeOrientationValid(OVRPlugin.Node.EyeCenter)
                && IsFinite(position)
                && TryNormalize(rotation, out rotation);
        }

        private void OnCameraRigUpdatedAnchors(OVRCameraRig cameraRig)
        {
            if (!isActiveAndEnabled || !isLocalPlayer || !isOwned)
            {
                return;
            }

            QuestPoseSamplePhase samplePhase;
            if (Time.inFixedTimeStep)
            {
                samplePhase = QuestPoseSamplePhase.FixedUpdate;
            }
            else if (m_LastAnchorSampleFrame == Time.frameCount)
            {
                // 标准 OVRCameraRig 每帧先在 Update 更新一次，再在
                // Application.onBeforeRender 更新一次。第二次回调就是 BeforeRender。
                samplePhase = QuestPoseSamplePhase.BeforeRender;
            }
            else
            {
                samplePhase = QuestPoseSamplePhase.Update;
            }

            m_LastAnchorSampleFrame = Time.frameCount;
            UpdateByPlayer(samplePhase);
        }

        private bool TryBindCameraRig()
        {
            if (m_CameraRig != null)
            {
                return true;
            }

            if (m_PlayerTransform == null)
            {
                GameObject playerTransformObject = GameObject.Find(PlayerTransformPath);
                if (playerTransformObject == null)
                {
                    return false;
                }
                m_PlayerTransform = playerTransformObject.transform;
            }

            m_CameraRig = m_PlayerTransform.GetComponentInParent<OVRCameraRig>();
            if (m_CameraRig == null)
            {
                return false;
            }

            m_CameraRig.UpdatedAnchors += OnCameraRigUpdatedAnchors;
            return true;
        }

        private void UnbindCameraRig()
        {
            if (m_CameraRig == null)
            {
                return;
            }

            m_CameraRig.UpdatedAnchors -= OnCameraRigUpdatedAnchors;
            m_CameraRig = null;
        }

        private void CacheValidPose(Vector3 position, Quaternion rotation)
        {
            if (!IsFinite(position) || !TryNormalize(rotation, out Quaternion normalizedRotation))
            {
                return;
            }

            m_LastValidPosition = position;
            m_LastValidRotation = normalizedRotation;
            m_HasLastValidPose = true;
        }

        private void RestoreLastValidPoseIfNeeded()
        {
            if (!m_HasLastValidPose)
            {
                return;
            }

            if (!IsFinite(transform.position)
                || !TryNormalize(transform.rotation, out _))
            {
                transform.SetPositionAndRotation(m_LastValidPosition, m_LastValidRotation);
            }
        }

        private void WarnInvalidPlayerPose(Vector3 position, Quaternion rotation)
        {
            if (Time.unscaledTime < m_NextInvalidPoseWarningTime)
            {
                return;
            }

            m_NextInvalidPoseWarningTime = Time.unscaledTime + InvalidPoseWarningInterval;
            Log.Warning(
                $"[VirtualPlayer] Ignored invalid HMD pose on local player. "
                + $"position={position}, rotation=({rotation.x}, {rotation.y}, {rotation.z}, {rotation.w})");
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsPositiveFinite(double value)
        {
            return value > 0d && !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool TryNormalize(Quaternion value, out Quaternion normalized)
        {
            float sqrMagnitude = value.x * value.x
                + value.y * value.y
                + value.z * value.z
                + value.w * value.w;
            if (!IsFinite(value.x) || !IsFinite(value.y)
                || !IsFinite(value.z) || !IsFinite(value.w)
                || !IsFinite(sqrMagnitude) || sqrMagnitude < MinQuaternionSqrMagnitude)
            {
                normalized = Quaternion.identity;
                return false;
            }

            float inverseMagnitude = 1f / Mathf.Sqrt(sqrMagnitude);
            normalized = new Quaternion(
                value.x * inverseMagnitude,
                value.y * inverseMagnitude,
                value.z * inverseMagnitude,
                value.w * inverseMagnitude);
            return true;
        }
#endif

            #endregion

            #region Mirror

        public override void OnStartAuthority()
        {
            if (!isOwned)
            {
                // 取消Player标签
                gameObject.tag = "Untagged";
            }
        }

        public override void OnStopAuthority()
        {
            if (!isOwned)
            {
                // 恢复Player标签
                gameObject.tag = "Player";
            }
        }

        #endregion

        #region U3D


        private void Start()
        {
#if !MANAGER_SERVER
            if (!TryBindCameraRig())
            {
                Log.Warning(
                    $"[VirtualPlayer] OVRCameraRig/HMD transform is not ready: {PlayerTransformPath}. "
                    + "Update fallback will retry without changing the Mirror pose flow.");
            }
            CacheValidPose(transform.position, transform.rotation);
#endif
        }

        private void OnEnable()
        {
            // 此时isLocalPlayer还没有设置
            //if (!isLocalPlayer)
            //{
            //    return;
            //}
            //
            // 输出日志
            Debug.Log("Player " + gameObject.name + " is spawned: Enabled ");
            GameModule.Event.Subscribe(AssignNetObjOwnerEventArgs.EventId, OnReassignNetObjOwner);
            GameModule.Event.Subscribe(RemoveNetObjOwnerEventArgs.EventId, OnRemoveNetObjOwner);
            GameModule.Event.Subscribe(ClientReadyChangedEventArgs.EventId, OnClientReadyChanged);
            GameModule.Event.Subscribe(CreateNetObjEventArgs.EventId, OnCreateNetObj);
            GameModule.Event.Subscribe(NetSyncStateWillChangeEventArgs.EventId, OnNetSyncStateWillChange);
        }

        private void OnDisable()
        {
            // 此时isLocalPlayer还没有设置
            //if (!isLocalPlayer)
            //{
            //    return;
            //}
            //
            GameModule.Event.Unsubscribe(AssignNetObjOwnerEventArgs.EventId, OnReassignNetObjOwner);
            GameModule.Event.Unsubscribe(RemoveNetObjOwnerEventArgs.EventId, OnRemoveNetObjOwner);
            GameModule.Event.Unsubscribe(ClientReadyChangedEventArgs.EventId, OnClientReadyChanged);
            GameModule.Event.Unsubscribe(CreateNetObjEventArgs.EventId, OnCreateNetObj);
            GameModule.Event.Unsubscribe(NetSyncStateWillChangeEventArgs.EventId, OnNetSyncStateWillChange);
#if !MANAGER_SERVER
            UnbindCameraRig();
#endif
        }

        private void Update()
        {
#if MANAGER_SERVER
            // 仅用户端副本执行
            if (!isLocalPlayer)
            {
                UpdateHDMPosDir();
            }
#else
            // Quest 上同时存在本地玩家和远端镜像。只有本地拥有的玩家
            // 可以从 HMD 写入位姿，否则会与 Mirror NetworkTransform 每帧争抢 Transform。
            if (!isLocalPlayer || !isOwned)
            {
                return;
            }

            if (TryBindCameraRig())
            {
                // 正常路径由 OVRCameraRig.UpdatedAnchors 回调采样，保证 Anchor 已完成
                // Step.Render 更新，避免脚本 Update 执行顺序造成一帧错配。
                return;
            }

            // 非 Oculus/测试场景兼容路径：找不到 OVRCameraRig 时保留旧的 Update 采样。
            UpdateByPlayer(QuestPoseSamplePhase.Update);
#endif
        }

        #endregion
    }
}