using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;

using UnityEngine;
using System;

namespace MetaQuestProEyeGazeUDP
{
    /// <summary>
    /// UDP通信
    /// </summary>
    public class UDP
    {
        public string recvStr;

        private string m_UDPClientAddRess = "127.0.0.1"; //目标服务器地址
        private int m_UDPClientPort = 8081; //目标服务器端口号
        private int m_UDPRevPort = 8082; //接收数据的端口号

        public string UDPClientAddRess
        {
            get { return m_UDPClientAddRess; }
            set
            {
                m_UDPClientAddRess = value;
                UpdateRemotePoint();
            }
        }
        public int UDPClientPort
        {
            get { return m_UDPClientPort; }
            set
            {
                m_UDPClientPort = value;
                UpdateRemotePoint();
            }
        }
        public int UDPRevPort
        {
            get { return m_UDPRevPort; }
            set { m_UDPRevPort = value; }
        }

        /// <summary>
        /// 更新远程端点
        /// </summary>
        private void UpdateRemotePoint()
        {
            try
            {
                string ip = m_UDPClientAddRess.Trim();
                if (!string.IsNullOrEmpty(ip))
                {
                    remotePoint = new IPEndPoint(IPAddress.Parse(ip), m_UDPClientPort);
                    Debug.Log($"UDP远程端点已更新: {ip}:{m_UDPClientPort}");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"更新UDP远程端点失败: {e.Message}");
            }
        }
        Socket socket;
        EndPoint serverEnd;
        IPEndPoint remotePoint;
        byte[] recvData = new byte[1024];
        byte[] sendData = new byte[1024];
        int recvLen = 0;
        Thread connectThread;

        // 接收线程 → 处理线程 的数据队列（含到达时间）
        private struct QueueItem
        {
            public string msg;
            public double arrivalTimeMs;
        }
        private readonly System.Collections.Concurrent.ConcurrentQueue<QueueItem> _processQueue = new System.Collections.Concurrent.ConcurrentQueue<QueueItem>();
        private Thread _processThread;
        private volatile bool _isProcessing = false;

        // 高精度计时（接收线程用）
        private static DateTime s_baseTime;
        private static long s_baseTimestamp;
        private static readonly object s_timeLock = new object();

        /// <summary>
        /// Shared monotonic Unix clock used by UDP receive timestamps, clock
        /// synchronization and gaze send timestamps. Keeping all three on one
        /// time base avoids a small but measurable bias from independently
        /// initialized Stopwatch/DateTime anchors.
        /// </summary>
        public static double GetHighPrecisionUnixTimeMsD()
        {
            lock (s_timeLock)
            {
                if (s_baseTime == default(DateTime))
                {
                    s_baseTime = DateTime.UtcNow;
                    s_baseTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
                }
                long elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - s_baseTimestamp;
                double elapsedMs = (double)elapsed / System.Diagnostics.Stopwatch.Frequency * 1000.0;
                DateTimeOffset baseOffset = new DateTimeOffset(s_baseTime);
                return baseOffset.ToUnixTimeMilliseconds() + elapsedMs;
            }
        }

        /// <summary>
        /// 接收数据回调（在处理线程调用，非主线程）
        /// 参数：msg=消息内容, arrivalTimeMs=接收线程捕获的到达时间
        /// </summary>
        public Action<string, double> receiveCallback;
        
        /// <summary>
        /// 连接服务器
        /// </summary>
        /// <param name="message"></param>
        public void InitSocket()
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            try
            {
                socket.Bind(new IPEndPoint(IPAddress.Any, UDPRevPort));
                // 增大接收缓冲区，减少因缓冲区满导致的数据包积压
                socket.ReceiveBufferSize = 256 * 1024; // 256KB
                Debug.Log($"UDP绑定成功，监听端口: {UDPRevPort}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"UDP绑定失败，端口: {UDPRevPort}，错误: {ex.Message}");
            }

            UDPClientAddRess = UDPClientAddRess.Trim();
            remotePoint = new IPEndPoint(IPAddress.Parse(UDPClientAddRess), UDPClientPort);

            IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
            serverEnd = sender;
            Debug.Log($"UDP初始化完成，目标IP: {UDPClientAddRess}，目标端口: {UDPClientPort}，接收端口: {UDPRevPort}");
            SocketSend("Connect");
        }

        public bool ConnectState()
        {
            // UDP是无连接协议，socket.Connected永远返回false
            // 我们使用socket是否为null且未被释放来判断连接状态
            return socket != null && socket.IsBound;
        }

        public void StartReceive()
        {
            connectThread = new Thread(new ThreadStart(SocketReceive));
            connectThread.Priority = System.Threading.ThreadPriority.AboveNormal;
            connectThread.Start();
        }

        /// <summary>
        /// 启动处理线程（独立于接收线程，避免回调阻塞接收）
        /// </summary>
        public void StartProcessThread()
        {
            _isProcessing = true;
            _processThread = new Thread(new ThreadStart(ProcessLoop));
            _processThread.IsBackground = true;
            _processThread.Priority = System.Threading.ThreadPriority.Normal;
            _processThread.Start();
        }

        /// <summary>
        /// 处理线程主循环：从队列取数据，执行回调
        /// </summary>
        private void ProcessLoop()
        {
            while (_isProcessing)
            {
                if (_processQueue.TryDequeue(out QueueItem item))
                {
                    try
                    {
                        receiveCallback?.Invoke(item.msg, item.arrivalTimeMs);
                    }
                    catch (Exception e)
                    {
                        MainThreadTaskQueue.EnqueueTask(delegate
                        {
                            Debug.LogError("UDP处理线程回调异常（已恢复）:");
                            Debug.LogError(e);
                        });
                    }
                }
                else
                {
                    Thread.Sleep(1);
                }
            }
        }

        string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new System.Exception("No network adapters with an IPv4 address in the system!");
        }

        /// <summary>
        /// 是否开启日志输出
        /// </summary>
        private bool m_IsLog = false;

        public void SetLog(bool isLog)
        {
            Debug.Log($"UDP SetLog: {isLog}");
            m_IsLog = isLog;
        }

        /// <summary>
        /// 想服务端发送需要发送的内容
        /// </summary>
        /// <param name="sendMessage"></param>
        public int SocketSend(string sendMessage)
        {
            //首先清空所有
            sendData = new byte[1024];
            //转换数据
            sendData = Encoding.UTF8.GetBytes(sendMessage);
            //将数据发送到服务端
            try
            {
                return socket.SendTo(sendData, sendData.Length, SocketFlags.None, remotePoint);
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// 想服务端发送需要发送的内容
        /// </summary>
        /// <param name="sendMessage"></param>
        public void SocketSend(byte[] sendMessage)
        {
            //首先清空所有
            sendData = sendMessage;
            //将数据发送到服务端
            socket.SendTo(sendData, sendData.Length, SocketFlags.None, remotePoint);
        }

        /// <summary>
        /// 接收来自服务端的消息
        /// </summary>
        void SocketReceive()
        {
            MainThreadTaskQueue.EnqueueTask(delegate
            {
                Debug.Log("SocketReceive: 开始接收UDP数据");
            });

            while (true)
            {
                if (socket == null || !socket.IsBound)
                {
                    break;
                }

                try
                {
                    recvLen = socket.ReceiveFrom(recvData, ref serverEnd);

                    if (recvLen > 0)
                    {
                        // 创建局部变量保存当前接收的数据，避免闭包捕获问题
                        string currentRecvStr = Encoding.UTF8.GetString(recvData, 0, recvLen);

                        // 复制服务器端点信息
                        IPEndPoint ipEndPoint = (IPEndPoint)serverEnd;
                        string currentEndPointStr = $"{ipEndPoint.Address}:{ipEndPoint.Port}";

                        if (m_IsLog)
                        {
                            System.Diagnostics.Debug.WriteLine($"UDP收到数据: {currentRecvStr} (来自: {currentEndPointStr})");
                        }
                        // 在接收线程立即捕获到达时间，然后入队
                        double arrivalTime = GetHighPrecisionUnixTimeMsD();
                        _processQueue.Enqueue(new QueueItem { msg = currentRecvStr, arrivalTimeMs = arrivalTime });
                    }
                }
                catch (SocketException e)
                {
                    if (e.ErrorCode == 10004)
                    {
                        MainThreadTaskQueue.EnqueueTask(delegate
                        {
                            Debug.Log("SocketReceive: UDP接收线程正常退出");
                        });
                        break;
                    }
                    else
                    {
                        // 输出异常信息（加堆栈信息）
                        MainThreadTaskQueue.EnqueueTask(delegate
                                                {
                                                    Debug.LogError("UDP接收异常:");
                                                    Debug.LogError(e);
                                                });
                    }
                }
                catch (Exception e)
                {
                    // 回调中的异常不应终止整个接收线程
                    // 仅记录错误并继续接收下一帧
                    MainThreadTaskQueue.EnqueueTask(delegate
                    {
                        Debug.LogError("UDP接收回调异常（已恢复）:");
                        Debug.LogError(e);
                    });
                }
            }

            MainThreadTaskQueue.EnqueueTask(delegate
            {
                Debug.Log("SocketReceive: 结束接收UDP数据");
            });
        }

        /// <summary>
        /// 关闭与服务器的连接
        /// </summary>
        public void SocketQuit()
        {
            //如果线程还在就需要关闭线程
            if (connectThread != null && connectThread.IsAlive)
            {
                try
                {
                    connectThread.Interrupt();
                    connectThread.Join(100); // 等待线程结束，最多100ms
                    if (connectThread.IsAlive)
                    {
                        connectThread.Abort(); // 如果线程仍在运行，强制终止
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("关闭UDP接收线程异常:" + e.Message);
                }
                connectThread = null;
            }

            // 关闭处理线程
            _isProcessing = false;
            if (_processThread != null && _processThread.IsAlive)
            {
                try
                {
                    _processThread.Join(200);
                    if (_processThread.IsAlive)
                    {
                        _processThread.Abort();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("关闭UDP处理线程异常:" + e.Message);
                }
                _processThread = null;
            }

            //最后关闭socket
            if (socket != null)
            {
                try
                {
                    socket.Shutdown(SocketShutdown.Both);
                }
                catch (Exception e)
                {
                    // 忽略关闭异常，因为socket可能已经关闭
                    Debug.LogWarning("关闭UDP socket异常:" + e.Message);
                }
                finally
                {
                    socket.Close();
                    socket = null;
                }
            }

            Debug.Log("销毁UDP");
        }

    }

}

