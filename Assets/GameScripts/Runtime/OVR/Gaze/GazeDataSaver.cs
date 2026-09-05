using UnityEngine;
using System.IO;
using System.Collections.Generic;
using GameMain;
using GameFramework.Event;
using System.Text;
using MetaQuestProEyeGazePointDraw;
using MetaQuestProEyeGazeUDP;
using System.Threading;

namespace GameLogic
{
    /// <summary>
    /// 眼动数据保存器
    /// </summary>
    public class GazeDataSaver : MonoBehaviour
    {
        // 写入线程
        private Thread m_SaveThread;
        // 线程安全队列
        private readonly Queue<string> m_DataQueue = new Queue<string>();
        private readonly object m_QueueLock = new object();
        // 线程控制
        private volatile bool m_IsRunning = false;
        private readonly AutoResetEvent m_DataEvent = new AutoResetEvent(false);

        //点编号计数器
        private int pointCounter = 0;

        private PointDraw m_PointDraw;


        #region 控制

        /// <summary>
        /// 是否启用
        /// </summary>
        private bool m_IsEnabled = false;

        /// <summary>
        /// 配置眼动数据保存
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void OnConfigured(object sender, GameEventArgs e)
        {
            var args = e as GazeSavedEventArgs;
            if (args == null) return;
            
            bool previousEnabled = m_IsEnabled;
            m_IsEnabled = args.IsEnabled;
            
            // 禁用时需要重置头显数据
            ResetHMD();
            
            // 重置编号计数器
            if (m_IsEnabled)
            {
                pointCounter = 0;
                // 重置绘制点计数
                if (m_PointDraw != null)
                {
                    m_PointDraw.ResetPointsCount();
                }
                
                // 如果之前未启用，现在启用，启动写入线程
                if (!previousEnabled)
                {
                    StartSaveThread();
                }
            }
            else
            {
                // 如果之前启用，现在禁用，停止写入线程
                if (previousEnabled)
                {
                    StopSaveThread();
                }
            }
            
            m_SaveFilePath = args.SavePath;
            Debug.Log($"GazeDataSaver: {m_IsEnabled}, {m_SaveFilePath}");
        }

        /// <summary>
        /// 启动写入线程
        /// </summary>
        private void StartSaveThread()
        {
            if (m_SaveThread != null && m_SaveThread.IsAlive) return;
            
            m_IsRunning = true;
            m_SaveThread = new Thread(SaveThreadLoop)
            {
                IsBackground = true,
                Name = "GazeDataSaveThread"
            };
            m_SaveThread.Start();
            Debug.Log("GazeDataSaver: 写入线程已启动");
        }

        /// <summary>
        /// 停止写入线程
        /// </summary>
        private void StopSaveThread()
        {
            if (m_SaveThread == null || !m_SaveThread.IsAlive) return;
            
            m_IsRunning = false;
            m_DataEvent.Set(); // 唤醒线程
            m_SaveThread.Join(1000); // 等待最多1秒
            Debug.Log("GazeDataSaver: 写入线程已停止");
        }

        /// <summary>
        /// 写入线程循环
        /// </summary>
        private void SaveThreadLoop()
        {
            while (m_IsRunning)
            {
                // 等待数据
                m_DataEvent.WaitOne();
                
                // 处理队列中的所有数据
                while (m_IsRunning)
                {
                    string lineToWrite = null;
                    
                    lock (m_QueueLock)
                    {
                        if (m_DataQueue.Count > 0)
                        {
                            lineToWrite = m_DataQueue.Dequeue();
                        }
                    }
                    
                    if (lineToWrite == null)
                    {
                        break; // 队列已空
                    }
                    
                    // 写入文件
                    try
                    {
                        WriteLastLine(m_SaveFilePath, lineToWrite);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"GazeDataSaver: 写入文件失败 - {ex}");
                    }
                }
            }
        }

        #endregion

        #region 接收与解析数据

        public void OnRecieved(GazeNetData gazePoint, Vector3? leftEyeGazePos, Vector3? rightEyeGazePos, Vector3? doubleEyesGazePos)
        {
            if (!m_IsEnabled) return;
            var message = new StringBuilder();
            // 格式化的时间戳：年-月-日 时:分:秒
            // var time = new System.DateTime(timeStamp);
            // TODO 临时 头显传送过程的时间戳有问题，先用管理端本地的时间戳代替
            var time = System.DateTime.Now;

            // 点编号
            message.Append(pointCounter.ToString());
            message.Append(',');

            // 日期
            message.Append(time.ToString("yyyy-MM-dd"));
            // 时间
            message.Append(',');
            message.Append('\'');
            message.Append(time.ToString("HH:mm:ss.fff"));
            // 双眼
            message.Append(',');
            message.Append(Vec3ToString(doubleEyesGazePos));
            // 左眼
            message.Append(',');
            message.Append(Vec3ToString(leftEyeGazePos));
            // 右眼
            message.Append(',');
            message.Append(Vec3ToString(rightEyeGazePos));
            // 头显位置
            message.Append(',');
            message.Append(Vec3ToString(m_HMDPos));
            // 头显朝向
            message.Append(',');
            message.Append(Vec3ToString(m_HMDRotation));
            
            // 将数据放入队列，由单独线程写入文件
            string lineToWrite = message.ToString();
            lock (m_QueueLock)
            {
                m_DataQueue.Enqueue(lineToWrite);
            }
            m_DataEvent.Set(); // 唤醒写入线程

            //增加计数器
            pointCounter++;
        }

        /// <summary>
        /// Vector3转字符串
        /// </summary>
        private string Vec3ToString(Vector3? pos)
        {
            if (pos == null)
            {
                return "--,--,--";
            }
            else
            {
                return $"{pos.Value.x},{pos.Value.y},{pos.Value.z}";
            }
        }

        #endregion

        #region 头显方位数据

        /// <summary>
        /// 头显位置
        /// </summary>
        private Vector3? m_HMDPos;

        /// <summary>
        /// 头显朝向
        /// </summary>
        private Vector3? m_HMDRotation;

        /// <summary>
        /// 更新头显位置
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="rotation"></param>
        private void UpdateHMD(Vector3 pos, Vector3 rotation)
        {
            if (!m_IsEnabled) return;
            //
            m_HMDPos = pos;
            m_HMDRotation = rotation;
        }

        private void OnUpdateHMDInfo(object sender, GameEventArgs e)
        {
            var args = e as UpdateHMDInfoEventArgs;
            if (args == null)
            {
                return;
            }
            UpdateHMD(args.Position, args.Rotation);
        }

        /// <summary>
        /// 重置头显数据
        /// </summary>
        private void ResetHMD()
        {
            m_HMDPos = null;
            m_HMDRotation = null;
        }

        #endregion

        #region U3D

        private void Awake()
        {
            m_PointDraw = FindObjectOfType<PointDraw>();
        }

        private void OnEnable()
        {
            // 订阅眼动数据保存事件
            GameModule.Event.Subscribe(GazeSavedEventArgs.EventId, OnConfigured);
            // 订阅头显位置更新事件
            GameModule.Event.Subscribe(UpdateHMDInfoEventArgs.EventId, OnUpdateHMDInfo);
        }

        private void OnDisable()
        {
            // 取消订阅眼动数据保存事件
            GameModule.Event.Unsubscribe(GazeSavedEventArgs.EventId, OnConfigured);
            // 取消订阅头显位置更新事件
            GameModule.Event.Unsubscribe(UpdateHMDInfoEventArgs.EventId, OnUpdateHMDInfo);
            // 停止写入线程
            StopSaveThread();
        }

        private void OnDestroy()
        {
            // 停止写入线程
            StopSaveThread();
            // 释放事件资源
            m_DataEvent?.Dispose();
        }

        #endregion

        #region 写入数据

        /// <summary>
        /// 保存文件的路径
        /// </summary>
        private string m_SaveFilePath;

        public static void WriteLastLine(string filePath, string content)
        {
            string headerLine = "编号,日期,时间,额中x,y,z,左眼x,y,z,右眼x,y,z,头显位置x,y,z,头显朝向x,y,z";

            if (!File.Exists(filePath))
            {
                File.WriteAllText(filePath, headerLine + System.Environment.NewLine);
            }

            File.AppendAllText(filePath, content + System.Environment.NewLine);
        }

        #endregion
    }

}

