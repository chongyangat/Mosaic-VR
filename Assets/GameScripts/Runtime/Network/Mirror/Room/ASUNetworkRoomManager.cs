using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityGameFramework.Runtime;

/*
	Documentation: https://mirror-networking.gitbook.io/docs/components/network-manager
	API Reference: https://mirror-networking.com/docs/api/Mirror.NetworkManager.html
*/

namespace GameMain
{
    public class ASUNetworkRoomManager : NetworkRoomManager
    {
        public static new ASUNetworkRoomManager singleton => NetworkManager.singleton as ASUNetworkRoomManager;

        #region 场景切换

        public override void OnServerChangeScene(string newSceneName)
        {
            // 发送事件
            GameModule.Event.Fire(ServerSceneChangeEventArgs.EventId, ServerSceneChangeEventArgs.Create(newSceneName));
        }

        /// <summary>
        /// This is called on the server when a networked scene finishes loading.
        /// </summary>
        /// <param name="sceneName">Name of the new scene.</param>
        public override void OnRoomServerSceneChanged(string sceneName)
        {
            // 发送事件
            GameModule.Event.Fire(ServerSceneChangedEventArgs.EventId, ServerSceneChangedEventArgs.Create(sceneName));
        }

        public override void OnClientChangeScene(string newSceneName, SceneOperation sceneOperation, bool customHandling)
        {
            // 发送事件
            GameModule.Event.Fire(ClientSceneChangeEventArgs.EventId, ClientSceneChangeEventArgs.Create(newSceneName));
        }

        public override void OnClientSceneChanged()
        {
            if (Utils.IsSceneActive(RoomScene))
            {
                if (NetworkClient.isConnected)
                {
                    OnRoomClientEnter();
                    foreach (NetworkRoomPlayer player in roomSlots)
                    {
                        if (player != null)
                        {
                            player.OnClientEnterRoom();
                        }
                    }
                }
                // 房间场景自动处理，游戏场景手动处理
                base.OnClientSceneChanged();
            }
            else
            {
                OnRoomClientExit();
                foreach (NetworkRoomPlayer player in roomSlots)
                {
                    if (player != null)
                    {
                        player.OnClientExitRoom();
                    }
                }
            }

            OnRoomClientSceneChanged();
        }

        public override void OnRoomClientSceneChanged()
        {
            // 发送事件
            GameModule.Event.Fire(ClientSceneChangedEventArgs.EventId, ClientSceneChangedEventArgs.Create(networkSceneName));
        }

        #endregion

        #region 就绪状态

        public override void OnRoomServerPlayersReady()
        {
            // 发送事件
            GameModule.Event.Fire(RoomAllPlayerReadyChangedEventArgs.EventId, RoomAllPlayerReadyChangedEventArgs.Create(true));
            // calling the base method calls ServerChangeScene as soon as all players are in Ready state.
            if (Utils.IsHeadless())
            {
                base.OnRoomServerPlayersReady();
            }
            else
            {
                showStartButton = true;
            }
        }

        public override void OnRoomServerPlayersNotReady()
        {
            // 发送事件
            GameModule.Event.Fire(RoomAllPlayerReadyChangedEventArgs.EventId, RoomAllPlayerReadyChangedEventArgs.Create(false));
        }

        #endregion

        #region 服务器启动

        public override void OnStartServer()
        {
            base.OnStartServer();
            // 激活网络链路
            var monitor = GameMain.NetworkPerformanceMonitor.Instance;
            if (monitor != null)
            {
                monitor.UpdateLinkActivity("PC<->VR 状态同步");
                Log.Info("Network link activated on server start.");
            }
        }

        /// <summary>
        /// 服务端：客户端连接成功时触发
        /// 在此处获取远端 Client IP，设置 UDP 时钟同步目标地址
        /// </summary>
        public override void OnServerConnect(Mirror.NetworkConnectionToClient conn)
        {
            base.OnServerConnect(conn);

            // 跳过本机 host 连接（connectionId=0，address="localhost"）
            if (conn == null || conn.connectionId == 0)
                return;

            // conn.address 格式为 "IP:Port"，提取 IP
            string clientIP = conn.address.Split(':')[0];
            if (!string.IsNullOrEmpty(clientIP))
            {
                UDPClockSync.SetClientAddress(clientIP);
                Log.Info($"[ASUNetworkRoomManager] Client connected, UDP clock sync target set to: {clientIP}");
            }
        }

        #endregion

        #region 连接

        public override void OnRoomClientConnect()
        {
            // 发送事件
            GameModule.Event.Fire(RoomDisOrConnectedChangedEventArgs.EventId, RoomDisOrConnectedChangedEventArgs.Create(false, true));
        }

        public override void OnRoomClientDisconnect()
        {
            // 发送事件
            GameModule.Event.Fire(RoomDisOrConnectedChangedEventArgs.EventId, RoomDisOrConnectedChangedEventArgs.Create(false, false));
        }

        #endregion

        #region 界面

        /// <summary>
        /// This code below is to demonstrate how to do a Start button that only appears for the Host player
        /// showStartButton is a local bool that's needed because OnRoomServerPlayersReady is only fired when
        /// all players are ready, but if a player cancels their ready state there's no callback to set it back to false
        /// Therefore, allPlayersReady is used in combination with showStartButton to show/hide the Start button correctly.
        /// Setting showStartButton false when the button is pressed hides it in the game scene since NetworkRoomManager
        /// is set as DontDestroyOnLoad = true.
        /// </summary>
        bool showStartButton;

        public override void OnGUI()
        {
            if (!showRoomGUI)
                return;

            if (NetworkServer.active && SceneManager.GetSceneByPath(GameplayScene).isLoaded)
            {
                GUILayout.BeginArea(new Rect(Screen.width - 150f, 10f, 140f, 30f));
                if (GUILayout.Button("Return to Room"))
                {
                    ServerChangeScene(RoomScene);
                }
                GUILayout.EndArea();
            }

            if (Utils.IsSceneActive(RoomScene))
                GUI.Box(new Rect(10f, 180f, 520f, 150f), "PLAYERS");

            if (allPlayersReady && showStartButton && GUI.Button(new Rect(150, 300, 120, 20), "START GAME"))
            {
                // set to false to hide it in the game scene
                showStartButton = false;

                ServerChangeScene(GameplayScene);
            }
        } 

        #endregion
    }
}
