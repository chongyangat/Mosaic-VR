using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using GameBase;
using MetaQuestProEyeGazeUDP;
using UnityEngine;
using UnityGameFramework.Runtime;
using XNF.BaseFrame.EyeTracking;

namespace GameLogic
{
    /// <summary>
    /// 眼动指标系统。
    /// </summary>
    public class EyesTrackingMetricsSystem : BaseLogicSys<EyesTrackingMetricsSystem>
    {


        #region 注视指标

        /// <summary>
        /// 是否限制注视点在相机可视范围内
        /// </summary>
        private bool m_LimitToCameraView = true;

        /// <summary>
        /// 注视数据模块
        /// </summary>
        NativeFixationDataModule m_FixationDataModule;

        /// <summary>
        /// 初始化注视数据
        /// </summary>
        private void InitFixationData()
        {
            ETDataModuleManager.Instance.Initialize();
            ETDataModuleManager.Instance.InitializeModule<NativeFixationDataModule>(40f, 100);// 初始化需要的模块（可以带参数，第一个参数一定要带f，表示是浮点数）
            // 获取注视数据模块
            m_FixationDataModule = ETDataModuleManager.Instance.GetModule<NativeFixationDataModule>();
            if (m_FixationDataModule != null)
            {
                // 订阅注视点新增事件
                m_FixationDataModule.OnFixationPointAdded += OnFixationPointAdded;
            }
        }

        /// <summary>
        /// 释放注视数据
        /// </summary>
        private void ReleaseFixationDataReceiver()
        {
            if (m_FixationDataModule == null)
            {
                return;
            }
            // 取消订阅注视点新增事件
            m_FixationDataModule.OnFixationPointAdded -= OnFixationPointAdded;
        }

        /// <summary>
        /// 保存双眼注视的屏幕位置
        /// </summary>
        private Vector3 m_DoubleEyesGazeScreenPos = Vector3.zero;

        /// <summary>
        /// 保存双眼注视的世界位置（额中位置）
        /// </summary>
        private Vector3 m_DoubleEyesGazeWorldPos = Vector3.zero;

        /// <summary>
        /// 处理双眼注视位置
        /// </summary>
        /// <param name="pos">双眼注视位置</param>
        private void HandleDoubleEyesGazePos(Vector3 pos)
        {
            // 保存双眼注视的世界坐标（额中位置）
            m_DoubleEyesGazeWorldPos = pos;

            // 保存双眼注视位置，转换成当前相机的屏幕坐标
            m_DoubleEyesGazeScreenPos = Camera.main.WorldToScreenPoint(pos);
            // 检查是否限制在相机可视范围内
            if (m_LimitToCameraView)
            {
                // 检查是否在相机可视范围内
                if (m_DoubleEyesGazeScreenPos.x >= 0 && m_DoubleEyesGazeScreenPos.x <= Screen.width &&
                    m_DoubleEyesGazeScreenPos.y >= 0 && m_DoubleEyesGazeScreenPos.y <= Screen.height)
                {
                    // 在范围内，保存数据
                    m_DoubleEyesGazeScreenPos = Camera.main.WorldToScreenPoint(pos);
                }
                else
                {
                    // 不在范围内，不保存屏幕坐标数据，但仍然保存世界坐标
                    m_DoubleEyesGazeScreenPos = Vector3.zero;
                }
            }
            else
            {
                // 输出警告，提示注视点不在相机可视范围内，不保存数据，但仍然保存世界坐标
                Debug.LogWarning($"注视点({pos})不在相机可视范围内，不保存数据，但仍然保存世界坐标");
            }
        }

        /// <summary>
        /// 更新注视指标数据收集
        /// </summary>
        private void UpdateFixationMetrics()
        {
            if (!m_IsEnabled)
            {
                return;
            }
            // 收集注视数据模块的数据
            ETDataModuleManager.Instance.CollectData<NativeFixationDataModule>(m_DoubleEyesGazeScreenPos);
        }

        /// <summary>
        /// 注视点新增事件处理
        /// </summary>
        private void OnFixationPointAdded(Vector2 fixationPoint, int stayDuration, int saccadeDuration)
        {
            if (m_IsEnabled && !string.IsNullOrEmpty(m_SaveFilePath))
            {
                SaveFixationData(fixationPoint, stayDuration, saccadeDuration);
            }
        }

        #endregion

        #region 注视数据接收

        /// <summary>
        /// 注视眼动数据接收器
        /// </summary>
        GazeDataReceiver m_GazeDataReceiver;

        /// <summary>
        /// 有效眼动数据接收器
        /// </summary>
        GazeDataReceiver ValidGazeDataReceiver
        {
            get
            {
                if (m_GazeDataReceiver == null)
                {
                    m_GazeDataReceiver = Object.FindObjectOfType<GazeDataReceiver>();
                    if (m_GazeDataReceiver != null)
                    {
                        // 注册眼动数据接收事件（主线程 Update）
                        // Bug：OnGazeDataReceived 事件在非主线程调用，与本模块不兼容
                        // 解决：在主线程调用 OnReceivedGazePointInUpdate 方法，但代价是会丢失一些数据，因眼动接收频率高于 Update 帧率，导致数据丢失
                        m_GazeDataReceiver.OnReceivedGazePointInUpdate.AddListener(OnGazeDataReceived);
                    }
                }
                return m_GazeDataReceiver;
            }
        }



        /// <summary>
        /// 释放眼动数据接收
        /// </summary>
        private void ReleaseGazeDataReceiver()
        {
            if (ValidGazeDataReceiver == null)
            {
                return;
            }
            ValidGazeDataReceiver.OnReceivedGazePointInUpdate.RemoveListener(OnGazeDataReceived);
        }

        /// <summary>
        /// 眼动数据接收
        /// </summary>
        private void OnGazeDataReceived(GazeNetData gazePoint, Vector3? leftEyeGazePos, Vector3? rightEyeGazePos, Vector3? doubleEyesGazePos)
        {
            if (m_IsEnabled && doubleEyesGazePos.HasValue)
            {
                HandleDoubleEyesGazePos(doubleEyesGazePos.Value);
            }
        }

        #endregion

        #region 控制

        /// <summary>
        /// 是否启用
        /// </summary>
        private bool m_IsEnabled = false;

        private void OnSaveStart(string filePath)
        {
            // 检查ValidGazeDataReceiver是否为空
            if (ValidGazeDataReceiver == null)
            {
                Log.Error("ValidGazeDataReceiver为空，无法配置眼动数据。");
                return;
            }
            // 
            m_IsEnabled = true;
            // 每次启用前清理所有模块数据
            ETDataModuleManager.Instance.Clear();
            // 输出日志
            Debug.Log("清理指标所有模块数据");
            // 
            m_SaveFilePath = filePath;
            // 输出日志
            Debug.Log($"EyesTrackingMetricsSystem: {m_IsEnabled}, {m_SaveFilePath}");
            // 启动写入线程
            StartSaveThread();
        }

        private void OnSaveComplete(string filePath)
        {
            // 禁用眼动数据接收
            m_IsEnabled = false;
            // 停止写入线程
            StopSaveThread();
            // 输出日志
            Debug.Log($"EyesTrackingMetricsSystem: {m_IsEnabled}, {m_SaveFilePath}");
        }

        #endregion

        #region 数据保存

        /// <summary>
        /// 保存文件的路径
        /// </summary>
        private string m_SaveFilePath;

        /// <summary>
        /// 写入线程
        /// </summary>
        private Thread m_SaveThread;

        /// <summary>
        /// 线程安全队列
        /// </summary>
        private readonly Queue<string> m_DataQueue = new Queue<string>();
        private readonly object m_QueueLock = new object();

        /// <summary>
        /// 线程控制
        /// </summary>
        private volatile bool m_IsRunning = false;
        private readonly AutoResetEvent m_DataEvent = new AutoResetEvent(false);

        /// <summary>
        /// 保存注视点数据
        /// </summary>
        /// <param name="fixationPoint">注视点位置</param>
        /// <param name="stayDuration">停留时间</param>
        /// <param name="saccadeDuration">扫视时间</param>
        private void SaveFixationData(Vector2 fixationPoint, int stayDuration, int saccadeDuration)
        {
            var message = new StringBuilder();
            // 获取当前时间
            var time = System.DateTime.Now;
            // 日期
            message.Append(time.ToString("yyyy-MM-dd"));
            // 时间
            message.Append(',');
            message.Append('\'');
            message.Append(time.ToString("HH:mm:ss.fff"));
            // 额中位置（世界坐标）
            message.Append(',');
            message.Append(m_DoubleEyesGazeWorldPos.x.ToString());
            message.Append(',');
            message.Append(m_DoubleEyesGazeWorldPos.y.ToString());
            message.Append(',');
            message.Append(m_DoubleEyesGazeWorldPos.z.ToString());
            // 注视点位置
            message.Append(',');
            message.Append(fixationPoint.x.ToString());
            message.Append(',');
            message.Append(fixationPoint.y.ToString());
            // 停留时间
            message.Append(',');
            message.Append(stayDuration.ToString());
            // 扫视时间
            message.Append(',');
            message.Append(saccadeDuration.ToString());

            // 获取所有Get方法的数据
            if (m_FixationDataModule != null)
            {
                // 平均注视时间
                message.Append(',');
                message.Append(m_FixationDataModule.GetAvgGazeTime().ToString());
                // 平均扫视时间
                message.Append(',');
                message.Append(m_FixationDataModule.GetAvgSacadeTime().ToString());
                // 平均扫视速度
                message.Append(',');
                message.Append(m_FixationDataModule.GetAvgSacadeSpeed().ToString());
                // 最大扫视速度
                message.Append(',');
                message.Append(m_FixationDataModule.GetMaxSacadeSpeed().ToString());
                // 平均扫视幅度
                message.Append(',');
                message.Append(m_FixationDataModule.GetSacadeAvgAmplitude().ToString());
                // 回视次数
                message.Append(',');
                message.Append(m_FixationDataModule.GetBackwardGazeCount().ToString());
            }

            // 将数据放入队列，由单独线程写入文件
            string lineToWrite = message.ToString();
            lock (m_QueueLock)
            {
                m_DataQueue.Enqueue(lineToWrite);
            }
            m_DataEvent.Set();
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
                Name = "EyesTrackingSaveThread"
            };
            m_SaveThread.Start();
            Debug.Log("EyesTrackingMetricsSystem: 写入线程已启动");
        }

        /// <summary>
        /// 停止写入线程
        /// </summary>
        private void StopSaveThread()
        {
            if (m_SaveThread == null || !m_SaveThread.IsAlive) return;

            m_IsRunning = false;
            m_DataEvent.Set();
            m_SaveThread.Join(1000);
            Debug.Log("EyesTrackingMetricsSystem: 写入线程已停止");
        }

        /// <summary>
        /// 写入线程循环
        /// </summary>
        private void SaveThreadLoop()
        {
            while (m_IsRunning)
            {
                m_DataEvent.WaitOne();

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
                        break;
                    }

                    try
                    {
                        WriteLastLine(m_SaveFilePath, lineToWrite);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"EyesTrackingMetricsSystem: 写入文件失败 - {ex}");
                    }
                }
            }
        }

        /// <summary>
        /// 写入数据到文件末尾
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="content">要写入的内容</param>
        private static void WriteLastLine(string filePath, string content)
        {
            string headerLine = "日期,时间,额中X,额中Y,额中Z,注视点X,注视点Y,停留时间(ms),扫视时间(ms),平均注视时间(ms),平均扫视时间(ms),平均扫视速度(像素/毫秒),最大扫视速度(像素/毫秒),平均扫视幅度(像素),回视次数";

            if (!File.Exists(filePath))
            {
                File.WriteAllText(filePath, headerLine + System.Environment.NewLine);
            }

            File.AppendAllText(filePath, content + System.Environment.NewLine);
        }

        #endregion

        #region 生命周期

        override public void OnStart()
        {
            base.OnStart();

            // 初始化注视数据
            InitFixationData();
            // 订阅眼动数据保存事件
            GameEvent.AddEventListener<string>(IActorLogicEvent_EyesTracking_Event.OnSaveStart, OnSaveStart);
            // 订阅眼动数据接收事件
            GameEvent.AddEventListener<string>(IActorLogicEvent_EyesTracking_Event.OnSaveComplete, OnSaveComplete);
        } 

        public override void OnUpdate()
        {
            base.OnUpdate();
            // 更新注视指标数据收集
            UpdateFixationMetrics();
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            // 停止写入线程
            StopSaveThread();
            // 释放注视数据
            ReleaseFixationDataReceiver();
            // 释放眼动数据接收
            ReleaseGazeDataReceiver();
            // 取消订阅眼动数据保存事件
            GameEvent.RemoveEventListener<string>(IActorLogicEvent_EyesTracking_Event.OnSaveStart, OnSaveStart);
            // 取消订阅眼动数据接收事件
            GameEvent.RemoveEventListener<string>(IActorLogicEvent_EyesTracking_Event.OnSaveComplete, OnSaveComplete);
        }

        #endregion
    }
}