using System.Net.Sockets;
using System.Net;
using System.Text;

using UnityEngine;
using System.Threading;
using System;
using System.IO;
using System.Collections.Generic;
namespace MetaQuestProGazeDateReceiver
{
    public class GazeDataReceiver : MonoBehaviour
    {
        private UdpClient server;
        private IPEndPoint remoteEndPoint;
        private Thread listenThread;

        private void Start()
        {
            // 创建UdpClient实例，指定监听的端口号
            server = new UdpClient(60000);
            // 初始化remoteEndPoint，用于接收数据时使用
            remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            // 启动接收数据
            listenThread = new Thread(ListenForUdp);
            listenThread.Start();
        }

        void ListenForUdp()
        {
            while (true)
            {
                try
                {
                    // 使用Receive方法来阻塞线程，直到接收到数据
                    byte[] data = server.Receive(ref remoteEndPoint);
                    // 将接收到的字节转换为字符串
                    string message = Encoding.UTF8.GetString(data);
                    // 输出接收到的消息和发送者的IP和端口
                    //Debug.Log($"Received: {message} from {remoteEndPoint.Address}:{remoteEndPoint.Port}");

                    WriteLastLine(Application.streamingAssetsPath + "/AccuracyData.txt", message);
                }
                catch (Exception ex)
                {
                    // 处理异常
                    Debug.LogError(ex.Message);
                }
            }
        }

        public static void WriteLastLine(string filePath, string content)
        {
            List<string> lines = new List<string>();

            // 检查文件是否存在
            if (File.Exists(filePath))
            {
                // 读取所有行，除了最后一行
                using (StreamReader reader = new StreamReader(filePath))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        lines.Add(line);
                    }
                }
            }

            // 添加新的一行
            lines.Add(content);

            // 写入文件，覆盖原有内容
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                foreach (string line in lines)
                {
                    writer.WriteLine(line);
                }
            }
        }

        private void OnDestroy()
        {
            // 确保在关闭游戏时释放UdpClient资源
            server.Close();
            listenThread.Abort();
        }
    }

}

