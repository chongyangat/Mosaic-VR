using GameFramework.Event;
using Mirror;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace GameMain.NetworkRoom
{
    public class ASUNetworkRoomPlayer : NetworkRoomPlayer
    {
        /// <summary>
        /// 服务端玩家是否自动准备
        /// </summary>
        [Header("Ready")]
        [Tooltip("服务端玩家是否自动准备")]
        [SerializeField]
        private bool m_IsServerPlayerAutoReady = false;

        public override void OnClientEnterRoom()
        {
            if (m_IsServerPlayerAutoReady && isServer)
            {
                // 此时客户端已经进入房间，但未就绪
                if (!NetworkClient.ready)
                {
                    NetworkClient.Ready();
                }
                // 执行此方法需要客户端就绪
                CmdChangeReadyState(true);
            }
        }

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
            if (ne.IsInGameScene)
            {
                return;
            }
            // 输出日志
            Log.Info($"网络同步状态将改变:{ne.StateName} {ne.State}");
            // 如果ne.UserData不为空，则序列化为Json字符串，网络同步不支持传输object类型
            string userData = "";
            if(ne.UserData != null && ne.UserData is not string)
            {
                userData = JsonUtility.ToJson(ne.UserData);
            }
            // 
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

        #region U3D

        private void OnEnable()
        {
            // 注册事件
            GameModule.Event.Subscribe(NetSyncStateWillChangeEventArgs.EventId, OnNetSyncStateWillChange);
        }

        public override void OnDisable()
        {
            // 注销事件
            GameModule.Event.Unsubscribe(NetSyncStateWillChangeEventArgs.EventId, OnNetSyncStateWillChange);
            base.OnDisable();
        }

        #endregion
    }
}
