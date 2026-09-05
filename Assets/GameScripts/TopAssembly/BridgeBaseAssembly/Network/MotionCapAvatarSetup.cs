// ===================================================
// Copyright ©. All rights reserved.
// Author：ASSJ
// CreateTime：2024/12/18 9:21:11
// Version: v1.0
// Description：动捕替身配置
// ===================================================

using Cysharp.Threading.Tasks;
using GameFramework.Event;
using GameMain;
using Mirror;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using UnityEngine;

namespace VBSOED
{
    /// <summary>
    /// 动捕替身配置
    /// </summary>
    public class MotionCapAvatarSetup : MonoBehaviour
    {
        #region 字段

        /// <summary>
        /// 动捕是否已初始化
        /// </summary>
        private bool m_IsMotionCaptureInitialized = false;

        #endregion

        #region Nokov

        [Header("Nokov")]
        [Tooltip("Nokov客户端")]
        [SerializeField]
        private StreamingClient m_NokovClient;

        /// <summary>
        /// 启动动捕客户端
        /// </summary>
        private void StartNokovClient(string ip)
        {
            m_NokovClient.ServerIp = ip;
            m_NokovClient.gameObject.SetActive(true);
        }

        /// <summary>
        /// 停止动捕客户端
        /// </summary>
        private void StopNokovClient()
        {
            m_NokovClient.gameObject.SetActive(false);
        }

        #endregion

        #region 虚拟替身

        [Header("Aavatar")]
        [Tooltip("数字人")]
        [SerializeField]
        private GameObject m_Aavatar;

        /// <summary>
        /// 启用/禁用数字人
        /// </summary>
        private void ToggleAavatar(bool isEnabled)
        {
            m_Aavatar.SetActive(isEnabled);
        }

        /// <summary>
        /// 传送角色完成
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnTeleportPlayer(object sender, GameEventArgs e)
        {
            TeleportPlayerEventArgs ne = (TeleportPlayerEventArgs)e;
            if (ne == null)
            {
                return;
            }
            //
            OnPostTeleportPlayer(ne).Forget();
        }

        private async UniTask OnPostTeleportPlayer(TeleportPlayerEventArgs ne)
        {
            // 检查是否需要翻转
            bool isNeedInvert = true;
            // 注意：延时后，事件中的NE会被回收，里面的数据在延时后需要的，就需要缓存处理。
            // 1.检查附加数据（先做，因延时后ne数据会被清理）
            if (ne.UserData != null)
            {
                // 检查附加数据是否为字典
                var userData = ne.UserData as Dictionary<string, object>;
                if (userData == null)
                {
                    // 输出日志
                    Debug.LogWarning("此次传送附加数据非字典，本模块忽略。");
                }
                else
                {
                    // 检查附加数据是否包含SceneName
                    if (!userData.ContainsKey("SceneName"))
                    {
                        // 输出日志
                        Debug.LogWarning("此次传送附加数据不包含SceneName，本模块忽略。");
                    }
                    else
                    {
                        // 
                        var sceneName = userData["SceneName"] as string;
                        // 如果场景非房间场景，就正常进入主场景
                        var networkManager = NetworkManager.singleton as ASUNetworkRoomManager;
                        // 只要不是大厅及离线场景就启用数字人
                        ToggleAavatar(sceneName != networkManager.RoomScene && sceneName != networkManager.offlineScene);
                    }
                    // 检查附加数据是否包含IsNeedInvert
                    if (!userData.ContainsKey("IsNeedInvert"))
                    {
                        // 输出日志
                        Debug.LogWarning("此次传送附加数据不包含IsNeedInvert，本模块忽略。");
                    }
                    else
                    {
                        // 检查附加数据是否包含IsNeedInvert
                        isNeedInvert = (bool)userData["IsNeedInvert"];
                        // 如果需要翻转，就翻转
                        if (isNeedInvert)
                        {
                            // 因为点位需要翻转，所以这里动捕就不需要翻转了
                            // 输出日志
                            Debug.Log("此次传送需要翻转，本模块翻转。");
                        }
                    }
                }
            }
            else
            {
                // 输出日志
                Debug.LogWarning("此次传送附加数据为空，本模块忽略。");
            }

            // 2.处理位置
            // 因延时后ne会被清理
            var teleportPos = ne.Position;
            var teleportRot = ne.Rotation;
            // 输出日志
            Debug.Log($"传送角色，动捕角色同步目标：位置：{teleportPos}，旋转：{teleportRot.eulerAngles}");
            // 设置旋转
            if (isNeedInvert)
            {
                // 需要绕Y轴旋转180度。
                transform.rotation = teleportRot * Quaternion.Euler(0, 180, 0);
            }
            else
            {
                transform.rotation = teleportRot;
            }

            // 等0.5秒，等数字人完成旋转
            await UniTask.Delay(500);
            // 每一次传送后都应该将数字人与头显的基准点对齐
            // 但是根据数字人当前动捕驱动的位置计算，要使得数字人与头显的基准点（teleportPos）对齐。
            // 不能直接移动数字人，而是需要将它的父对象平移，以达到数字人对齐的目的。
            // 计算数字人和目标位置的偏移量
            // 取得数字人的Avatar对象
            var animator = m_Aavatar.GetComponent<Animator>();
            // 根据avatar获取数字人Hips的位置
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Vector3 offset = teleportPos - hips.position;
            // 忽略高度的偏移
            offset.y = 0;
            // 输出日志
            Debug.Log($"传送角色，动捕角色同步位置，数字人与传送位置的偏移：{offset}，hips位置：{hips.position}，teleportPos位置：{teleportPos}。");
            // 调试用
            // await UniTask.Delay(15 * 1000);
            // 平移父对象以保持数字人与头显基准点对齐
            transform.position += offset;
            // 设置高度为头显基准点
            transform.position = new Vector3(transform.position.x, teleportPos.y, transform.position.z);

#if MANAGER_SERVER
            // 父对象平移后，重新锚定管理端摄像机跟随点的 X/Z；Y 轴保留
            // 有效的 Hips 高度，异常时退回安全高度。
            var cameraTarget = m_Aavatar.transform.Find("Center");
            if (cameraTarget == null)
            {
                Debug.LogWarning("动捕角色同步完成，但没有找到摄像机跟随点 avatar_001/Center。");
            }
            else
            {
                var positionFollower = cameraTarget.GetComponent<PositionFollower>();
                if (positionFollower != null)
                {
                    positionFollower.SnapHorizontalTo(teleportPos);
                }
                else
                {
                    cameraTarget.position = teleportPos + Vector3.up;
                }
            }
#endif

            // 输出日志
            // 把下面转成$""的形式
            Debug.Log($"传送角色，动捕角色同步位置完成：位置：{teleportPos}，旋转：{teleportRot.eulerAngles}");
        }

        #endregion

        #region 网络

        /// <summary>
        /// TODO 临时
        /// </summary>
        [SerializeField]
        private string m_DefaultIP = "";

#if ENABLED_MOTION_CAP_AVATAR

        /// <summary>
        /// 配置网络
        /// </summary>
        private string SetupNetwork()
        {
            // Unity获取本机IP地址
            var localIP = GetLocalIPAddress();
            // 输出本机IP地址
            Debug.Log("本机IP地址：" + localIP);
            // 有效才连接
            if (string.IsNullOrEmpty(localIP))
            {
                Debug.LogWarning($"本机IP地址无效！将使用默认地址：{m_DefaultIP}。");
                localIP = m_DefaultIP;
            }
            return localIP;
        }

        /// <summary>
        /// 获取本机IP
        /// </summary>
        /// <returns>ip地址</returns>
        private string GetLocalIPAddress()
        {
            string output = "";

            foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces())
            {
                NetworkInterfaceType _type1 = NetworkInterfaceType.Wireless80211;  //无线局域网适配器 

                if ((item.NetworkInterfaceType == _type1) && item.OperationalStatus == OperationalStatus.Up)
                {
                    foreach (UnicastIPAddressInformation ip in item.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            output = ip.Address.ToString();
                        }
                    }
                }
            }
            return output;
        }
#endif

        #endregion

        #region 事件处理

        /// <summary>
        /// 发现服务器事件处理
        /// </summary>
        private void OnServerDiscovered(object sender, GameEventArgs e)
        {
#if ENABLED_MOTION_CAP_AVATAR
            ServerDiscoveredEventArgs ne = (ServerDiscoveredEventArgs)e;
            if (ne == null)
            {
                return;
            }

            if (m_IsMotionCaptureInitialized)
            {
                return;
            }

            // 输出日志
            Debug.Log($"MotionCapAvatarSetup: 发现服务器，开始初始化动捕，IP：{ne.ServerIp}");

            StartNokovClient(ne.ServerIp);
            m_IsMotionCaptureInitialized = true;
#endif
        }

        #endregion

        #region U3D

        private void Awake()
        {
            // 
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
#if ENABLED_MOTION_CAP_AVATAR
#if MANAGER_SERVER
            var ip = SetupNetwork();
            StartNokovClient(ip);
#endif
            // 注册事件
            GameModule.Event.Subscribe(ServerDiscoveredEventArgs.EventId, OnServerDiscovered);
            GameModule.Event.Subscribe(TeleportPlayerEventArgs.EventId, OnTeleportPlayer);
#endif
        }

        private void OnDisable()
        {
#if ENABLED_MOTION_CAP_AVATAR
            StopNokovClient();
            ToggleAavatar(false);
            // 取消注册事件
            GameModule.Event.Unsubscribe(ServerDiscoveredEventArgs.EventId, OnServerDiscovered);
            GameModule.Event.Unsubscribe(TeleportPlayerEventArgs.EventId, OnTeleportPlayer);
#endif
        }

        #endregion
    }
}
