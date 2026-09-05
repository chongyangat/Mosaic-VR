using System.Collections;
using UnityEngine;

namespace MetaQuestProEyeGazeUDP
{
    /// <summary>
    /// UDP通信
    /// </summary>
    public class UDPManager : MonoBehaviour
    {
        public static UDPManager m_Instance;

        public static UDPManager Instance
        {
            get
            {
                if (m_Instance == null)
                {
                    m_Instance = FindObjectOfType<UDPManager>();
                }
                return m_Instance;
            }
        }

        private void Awake()
        {
#if MANAGER_SERVER
            // 初始化UDP
            Init();
#endif
        }
        public UDP udp;
        public string m_SendTargetIP = "172.16.76.47";
        public int m_SendTargetPort = 8081;
        public int m_ReceivePort = 8082;

        private bool m_IsInitialized = false;

        public void SetIP(string ip, int port)
        {
            if (udp == null)
            {
                udp = new UDP();
            }
            udp.UDPClientAddRess = ip;
            udp.UDPClientPort = port;
            m_SendTargetIP = udp.UDPClientAddRess;
            m_SendTargetPort = udp.UDPClientPort;
            
            // 设置IP后初始化UDP
            if (!m_IsInitialized)
            {
                Init();
            }
        }

        public void Init()
        {
            if (udp == null)
            {
                udp = new UDP();
            }
            udp.UDPClientAddRess = m_SendTargetIP;
            udp.UDPClientPort = m_SendTargetPort;
            udp.UDPRevPort = m_ReceivePort;
            udp.InitSocket();
            // 直接启动接收线程和处理线程，无需等待连接状态（UDP无连接）
            udp.StartReceive();
            udp.StartProcessThread();
            m_IsInitialized = true;
            Debug.Log("UDP接收线程和处理线程已启动");
        }

        public int Send(string msg)
        {
            return udp.SocketSend(msg);
        }
        public void Send(byte[] msg)
        {
            udp.SocketSend(msg);
        }

        private void Update()
        {
            // 调用MainThreadTaskQueue.Update()，以便主线程任务能够执行
            MainThreadTaskQueue.Update();
        }

        /// <summary>
        /// 程序最后退出时销毁udp
        /// </summary
        private void OnDestroy()
        {
            if (udp != null)
            {
                udp.SocketQuit();
            }
        }

    }
}

