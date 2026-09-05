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
                UpdateHMDInfoEventArgs.Create(transform.position, m_PlayerDir));
        }
#else
        /// <summary>
        /// 角色对象（头显两眼中间的位置）
        /// </summary>
        private Transform m_PlayerTransform;

        private const string PlayerTransformPath =
            "UserClient/[BuildingBlock] Camera Rig/TrackingSpace/CenterEyeAnchor";
        private const float MinQuaternionSqrMagnitude = 0.000001f;
        private const float InvalidPoseWarningInterval = 5f;

        private Vector3 m_LastValidPosition;
        private Quaternion m_LastValidRotation = Quaternion.identity;
        private bool m_HasLastValidPose;
        private float m_NextInvalidPoseWarningTime;

        /// <summary>
        /// 同步位置
        /// </summary>
        private void UpdateByPlayer()
        {
            if (m_PlayerTransform == null)
            {
                return;
            }

            Vector3 playerPosition = m_PlayerTransform.position;
            Quaternion playerRotation = m_PlayerTransform.rotation;
            if (!TryNormalize(playerRotation, out Quaternion normalizedPlayerRotation)
                || !IsFinite(playerPosition))
            {
                RestoreLastValidPoseIfNeeded();
                WarnInvalidPlayerPose(playerPosition, playerRotation);
                return;
            }

            Vector3 playerDirection = normalizedPlayerRotation.eulerAngles;
            if (!IsFinite(playerDirection))
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
            CacheValidPose(playerPosition, normalizedYawRotation);
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
            GameObject playerTransformObject = GameObject.Find(PlayerTransformPath);
            if (playerTransformObject == null)
            {
                Log.Error($"[VirtualPlayer] HMD transform was not found: {PlayerTransformPath}");
                return;
            }

            m_PlayerTransform = playerTransformObject.transform;
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

            UpdateByPlayer();
#endif
        }

        #endregion
    }
}
