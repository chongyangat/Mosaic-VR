using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;
using System.Linq;
using UnityGameFramework.Runtime;

namespace GameLogic
{
    /// <summary>
    /// 商品运动数据录制器
    /// 参考 NetworkPerformanceMonitor.Recording.cs 的异步线程保存模式
    /// </summary>
    public class ProductMovementRecorder : MonoBehaviour
    {
        #region 单例

        /// <summary>
        /// 单例实例
        /// </summary>
        private static ProductMovementRecorder m_Instance = null;

        /// <summary>
        /// 单例实例
        /// </summary>
        public static ProductMovementRecorder Instance
        {
            get
            {
                if (m_Instance == null)
                {
                    m_Instance = FindObjectOfType<ProductMovementRecorder>();
                }
                // 如果找不到就生成一个
                if (m_Instance == null)
                {
                    m_Instance = new GameObject("ProductMovementRecorder").AddComponent<ProductMovementRecorder>();
                    DontDestroyOnLoad(m_Instance.gameObject);
                }
                return m_Instance;
            }
        }



        #endregion

        #region 录制状态

        /// <summary>
        /// 是否正在录制
        /// </summary>
        private bool _isRecording = false;

        /// <summary>
        /// 当前录制的文件路径
        /// </summary>
        private string _recordingFilePath = string.Empty;

        /// <summary>
        /// 记录当前写入的记录序号
        /// </summary>
        private int _recordingIndex = 0;

        #endregion

        #region 商品状态跟踪

        /// <summary>
        /// 商品状态字典
        /// </summary>
        private readonly Dictionary<Product, ProductState> _productStates = new Dictionary<Product, ProductState>();
        private readonly object _productsLock = new object();

        #endregion

        #region 配置参数

        [Header("录制配置")]
        [Tooltip("采样间隔（秒）")]
        [SerializeField] private float _sampleInterval = 0.05f; // 默认20fps

        [Tooltip("位置变化阈值（单位），变化超过此值才记录")]
        [SerializeField] private float _positionThreshold = 0.01f;

        [Tooltip("旋转变化阈值（度），变化超过此值才记录")]
        [SerializeField] private float _rotationThreshold = 1f;

        /// <summary>
        /// 上次采样时间
        /// </summary>
        private float _lastSampleTime = 0f;

        #endregion

        #region 写入线程

        /// <summary>
        /// 写入线程
        /// </summary>
        private Thread _recordingThread = null;

        /// <summary>
        /// 线程安全队列
        /// </summary>
        private readonly Queue<string> _recordingQueue = new Queue<string>();
        private readonly object _queueLock = new object();

        /// <summary>
        /// 线程控制
        /// </summary>
        private volatile bool _recordingThreadRunning = false;
        private readonly AutoResetEvent _recordingEvent = new AutoResetEvent(false);

        /// <summary>
        /// 录制文件的StreamWriter
        /// </summary>
        private StreamWriter _recordingWriter = null;

        #endregion

        #region 公共属性

        /// <summary>
        /// 是否正在录制
        /// </summary>
        public bool IsRecording => _isRecording;

        #endregion

        #region Unity生命周期

        private void Update()
        {
            if (!_isRecording) return;
            if (Time.time - _lastSampleTime < _sampleInterval) return;

            _lastSampleTime = Time.time;

            lock (_productsLock)
            {
                // 遍历所有注册商品的状态
                foreach (var kvp in _productStates.ToList())
                {
                    var state = kvp.Value;
                    var product = kvp.Key;

                    if (product == null || !product.gameObject.activeInHierarchy)
                    {
                        // 清理已销毁或不活跃的商品
                        _productStates.Remove(product);
                        continue;
                    }

                    // 检查是否超过阈值
                    if (state.ExceedsThreshold(_positionThreshold, _rotationThreshold))
                    {
                        var data = CaptureProductData(product);
                        EnqueueData(data);
                        state.UpdateState();
                    }
                }
            }
        }

        private void OnDestroy()
        {
            // 确保录制线程和资源被正确清理
            if (_isRecording)
            {
                StopRecording();
            }
            // 释放事件资源
            _recordingEvent?.Dispose();
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 开始录制
        /// </summary>
        /// <param name="filePath">CSV文件保存路径</param>
        /// <returns>是否成功开始录制</returns>
        public bool StartRecording(string filePath)
        {
            if (_isRecording)
            {
                Log.Warning("[ProductMovementRecorder] 已经在录制中");
                return false;
            }

            if (string.IsNullOrEmpty(filePath))
            {
                Log.Error("[ProductMovementRecorder] 文件路径不能为空");
                return false;
            }

            try
            {
                // 确保目录存在
                string directoryPath = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                // 打开文件流，准备追加写入
                _recordingWriter = new StreamWriter(filePath, false, Encoding.UTF8);
                _recordingWriter.AutoFlush = true;

                // 写入CSV表头
                _recordingWriter.WriteLine(ProductMovementData.GetCsvHeader());

                _recordingFilePath = filePath;
                _isRecording = true;
                _recordingIndex = 0;

                // 启动写入线程
                StartRecordingThread();

                Log.Info($"[ProductMovementRecorder] 开始录制: {filePath}");
                return true;
            }
            catch (Exception e)
            {
                Log.Error($"[ProductMovementRecorder] 开始录制失败: {e.Message}");
                StopRecordingInternal();
                return false;
            }
        }

        /// <summary>
        /// 停止录制
        /// </summary>
        /// <returns>录制的文件路径</returns>
        public string StopRecording()
        {
            if (!_isRecording)
            {
                Debug.LogWarning("[ProductMovementRecorder] 没有在录制中");
                return string.Empty;
            }

            string filePath = _recordingFilePath;
            StopRecordingInternal();

            Debug.Log($"[ProductMovementRecorder] 停止录制: {filePath}");
            return filePath;
        }

        /// <summary>
        /// 注册要录制的商品
        /// </summary>
        public void RegisterProduct(Product product)
        {
            if (product == null) return;

            lock (_productsLock)
            {
                if (!_productStates.ContainsKey(product))
                {
                    _productStates[product] = new ProductState(product);
                    Log.Info($"[ProductMovementRecorder] 注册商品: {product.gameObject.name}");
                }
            }
        }

        /// <summary>
        /// 注销商品录制
        /// </summary>
        public void UnregisterProduct(Product product)
        {
            if (product == null) return;

            lock (_productsLock)
            {
                _productStates.Remove(product);
                Log.Info($"[ProductMovementRecorder] 注销商品: {product.gameObject.name}");
            }
        }

        #endregion

        #region 后台线程逻辑

        /// <summary>
        /// 启动写入线程
        /// </summary>
        private void StartRecordingThread()
        {
            if (_recordingThread != null && _recordingThread.IsAlive)
                return;

            _recordingThreadRunning = true;
            _recordingThread = new Thread(RecordingThreadLoop)
            {
                IsBackground = true,
                Name = "ProductMovementRecordingThread"
            };
            _recordingThread.Start();
            Log.Info("[ProductMovementRecorder] 录制写入线程已启动");
        }

        /// <summary>
        /// 停止写入线程
        /// </summary>
        private void StopRecordingThread()
        {
            if (_recordingThread == null || !_recordingThread.IsAlive)
                return;

            _recordingThreadRunning = false;
            _recordingEvent.Set(); // 唤醒线程
            _recordingThread.Join(1000); // 等待最多1秒
            Log.Info("[ProductMovementRecorder] 录制写入线程已停止");
        }

        /// <summary>
        /// 写入线程循环
        /// </summary>
        private void RecordingThreadLoop()
        {
            while (_recordingThreadRunning)
            {
                // 等待数据
                _recordingEvent.WaitOne();

                // 处理队列中的所有数据
                while (_recordingThreadRunning)
                {
                    string lineToWrite = null;

                    lock (_queueLock)
                    {
                        if (_recordingQueue.Count > 0)
                        {
                            lineToWrite = _recordingQueue.Dequeue();
                        }
                    }

                    if (lineToWrite == null)
                    {
                        break; // 队列已空
                    }

                    // 写入文件
                    try
                    {
                        if (_recordingWriter != null)
                        {
                            _recordingWriter.WriteLine(lineToWrite);
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error($"[ProductMovementRecorder] 写入录制文件失败: {e.Message}");
                    }
                }
            }
        }

        #endregion

        #region 内部方法

        /// <summary>
        /// 采集商品运动数据
        /// </summary>
        private ProductMovementData CaptureProductData(Product product)
        {
            var transform = product.transform;
            return new ProductMovementData
            {
                productId = product.Id,
                productName = product.gameObject.name,
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                posX = transform.position.x,
                posY = transform.position.y,
                posZ = transform.position.z,
                rotX = transform.eulerAngles.x,
                rotY = transform.eulerAngles.y,
                rotZ = transform.eulerAngles.z
            };
        }

        /// <summary>
        /// 将数据加入队列
        /// </summary>
        private void EnqueueData(ProductMovementData data)
        {
            try
            {
                _recordingIndex++;
                string line = data.ToCsvLine(_recordingIndex);

                lock (_queueLock)
                {
                    _recordingQueue.Enqueue(line);
                }
                _recordingEvent.Set(); // 唤醒写入线程
            }
            catch (Exception e)
            {
                Log.Error($"[ProductMovementRecorder] 数据入队失败: {e.Message}");
            }
        }

        /// <summary>
        /// 内部停止录制（清理资源）
        /// </summary>
        private void StopRecordingInternal()
        {
            _isRecording = false;
            _recordingFilePath = string.Empty;

            // 停止写入线程
            StopRecordingThread();

            if (_recordingWriter != null)
            {
                try
                {
                    _recordingWriter.Flush();
                    _recordingWriter.Close();
                    _recordingWriter.Dispose();
                }
                catch (Exception e)
                {
                    Log.Error($"[ProductMovementRecorder] 关闭录制文件失败: {e.Message}");
                }
                _recordingWriter = null;
            }
        }

        #endregion

        #region 商品状态类

        /// <summary>
        /// 商品状态跟踪
        /// </summary>
        private class ProductState
        {
            public Product product;
            public float lastPosX, lastPosY, lastPosZ;
            public float lastRotX, lastRotY, lastRotZ;
            public bool hasRecordedFirst; // 标记是否已记录过第一次

            public ProductState(Product p)
            {
                product = p;
                lastPosX = p.transform.position.x;
                lastPosY = p.transform.position.y;
                lastPosZ = p.transform.position.z;
                lastRotX = p.transform.eulerAngles.x;
                lastRotY = p.transform.eulerAngles.y;
                lastRotZ = p.transform.eulerAngles.z;
                hasRecordedFirst = false;
            }

            // 检查是否超过阈值
            public bool ExceedsThreshold(float posThreshold, float rotThreshold)
            {
                // 第一次记录总是记录
                if (!hasRecordedFirst) return true;

                var pos = product.transform.position;
                var rot = product.transform.eulerAngles;

                // 检查位置变化
                float posDelta = Mathf.Abs(pos.x - lastPosX) +
                               Mathf.Abs(pos.y - lastPosY) +
                               Mathf.Abs(pos.z - lastPosZ);

                // 检查旋转变化（处理360度环绕问题）
                float rotDelta = Mathf.Abs(Mathf.DeltaAngle(rot.x, lastRotX)) +
                               Mathf.Abs(Mathf.DeltaAngle(rot.y, lastRotY)) +
                               Mathf.Abs(Mathf.DeltaAngle(rot.z, lastRotZ));

                return posDelta > posThreshold || rotDelta > rotThreshold;
            }

            // 更新记录的状态
            public void UpdateState()
            {
                var pos = product.transform.position;
                var rot = product.transform.eulerAngles;
                lastPosX = pos.x;
                lastPosY = pos.y;
                lastPosZ = pos.z;
                lastRotX = rot.x;
                lastRotY = rot.y;
                lastRotZ = rot.z;
                hasRecordedFirst = true;
            }
        }

        #endregion
    }
}
