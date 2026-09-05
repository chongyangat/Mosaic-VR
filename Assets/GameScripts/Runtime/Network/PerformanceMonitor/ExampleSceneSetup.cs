using UnityEngine;
using Mirror;

namespace GameMain
{
    /// <summary>
    /// 网络性能监控系统示例场景设置
    /// </summary>
    public class ExampleSceneSetup : MonoBehaviour
    {
        [Tooltip("是否自动设置网络性能监控")]
        public bool autoSetup = true;
        
        [Tooltip("是否启用UI显示")]
        public bool enableUI = true;
        
        [Tooltip("是否运行性能测试")]
        public bool runPerformanceTest = false;
        
        [Tooltip("性能测试消息频率（条/秒）")]
        public int testMessageRate = 10;
        
        [Tooltip("性能测试持续时间（秒）")]
        public float testDuration = 30.0f;
        
        private NetworkManager networkManager;
        private NetworkPerformanceMonitor monitor;
        private PerformanceTest performanceTest;
        
        private void Awake()
        {
            // 查找或创建NetworkManager
            networkManager = FindObjectOfType<NetworkManager>();
            if (networkManager == null)
            {
                GameObject managerObj = new GameObject("NetworkManager");
                networkManager = managerObj.AddComponent<NetworkManager>();
                
                // 设置默认传输层
                Transport transport = Transport.active;
                if (transport == null)
                {
                    Debug.LogWarning("No active transport found. Please add a transport component.");
                }
            }
        }
        
        private void Start()
        {
            if (autoSetup)
            {
                // 自动设置网络性能监控
                SetupNetworkPerformanceMonitor();
            }
            
            if (runPerformanceTest)
            {
                // 运行性能测试
                SetupPerformanceTest();
            }
        }
        
        private void SetupNetworkPerformanceMonitor()
        {
            // 使用集成助手设置监控器
            monitor = NetworkPerformanceIntegration.SetupNetworkPerformanceMonitor(networkManager, enableUI);
            
            if (monitor != null)
            {
                Debug.Log("Network performance monitor setup completed.");
                
                // 订阅性能更新事件
                monitor.OnPerformanceUpdated += OnPerformanceUpdated;
            }
            else
            {
                Debug.LogError("Failed to setup network performance monitor.");
            }
        }
        
        private void SetupPerformanceTest()
        {
            // 添加性能测试组件
            performanceTest = gameObject.AddComponent<PerformanceTest>();
            performanceTest.messageRate = testMessageRate;
            performanceTest.testDuration = testDuration;
            
            Debug.Log($"Performance test setup: {testMessageRate} messages/s for {testDuration}s");
        }
        
        private void OnPerformanceUpdated(NetworkPerformanceMonitor.PerformanceData data)
        {
            // 性能数据更新回调
            Debug.Log($"Network performance updated: Latency={data.latency:F1}ms, Jitter={data.jitter:F1}ms, Loss={data.packetLoss:F1}%");
        }
        
        /// <summary>
        /// 手动设置网络性能监控
        /// </summary>
        public void ManualSetup()
        {
            SetupNetworkPerformanceMonitor();
        }
        
        /// <summary>
        /// 开始性能测试
        /// </summary>
        public void StartPerformanceTest()
        {
            if (performanceTest != null)
            {
                performanceTest.RunStressTest(testMessageRate, testDuration);
            }
            else
            {
                SetupPerformanceTest();
            }
        }
        
        /// <summary>
        /// 导出性能数据
        /// </summary>
        public void ExportPerformanceData()
        {
            if (monitor != null)
            {
                string filePath = Application.persistentDataPath + "/NetworkPerformance_example_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";
                monitor.ExportData(filePath);
                Debug.Log($"Performance data exported to: {filePath}");
            }
            else
            {
                Debug.LogError("Network performance monitor not found.");
            }
        }
        
        /// <summary>
        /// 显示性能数据
        /// </summary>
        public void DisplayPerformanceData()
        {
            if (monitor != null)
            {
                var data = monitor.GetLatestData();
                Debug.Log($"Current performance: \n" +
                          $"Latency: {data.latency:F1}ms\n" +
                          $"Jitter: {data.jitter:F1}ms\n" +
                          $"Packet Loss: {data.packetLoss:F1}%\n"
                          );
            }
            else
            {
                Debug.LogError("Network performance monitor not found.");
            }
        }
    }
}
