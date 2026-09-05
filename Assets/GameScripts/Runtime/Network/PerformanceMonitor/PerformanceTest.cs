using UnityEngine;
using System.Diagnostics;
using System.Collections.Generic;
using Debug = UnityEngine.Debug;

namespace GameMain
{
    /// <summary>
    /// 网络性能监控系统测试脚本
    /// </summary>
    public class PerformanceTest : MonoBehaviour
    {
        [Tooltip("测试持续时间（秒）")]
        public float testDuration = 60.0f;
        
        [Tooltip("测试消息频率（条/秒）")]
        public int messageRate = 10;
        
        [Tooltip("消息大小（字节）")]
        public int messageSize = 100;
        
        private NetworkPerformanceMonitor monitor;
        private Stopwatch stopwatch;
        private float testStartTime;
        private int messagesSent = 0;
        private float lastMessageTime = 0;
        
        private List<float> cpuUsageSamples = new List<float>();
        private List<float> memoryUsageSamples = new List<float>();
        
        private void Start()
        {
            // 获取或创建监控器
            monitor = NetworkPerformanceMonitor.Instance ?? gameObject.AddComponent<NetworkPerformanceMonitor>();
            
            // 初始化测试
            stopwatch = new Stopwatch();
            testStartTime = Time.time;
            lastMessageTime = Time.time;
            
            Debug.Log("Performance test started.");
            Debug.Log($"Test duration: {testDuration}s");
            Debug.Log($"Message rate: {messageRate} messages/s");
            Debug.Log($"Message size: {messageSize} bytes");
            
            // 开始计时
            stopwatch.Start();
        }
        
        /// <summary>
        /// 运行综合性能测试
        /// </summary>
        /// <param name="testDuration">测试持续时间</param>
        /// <param name="messageRate">消息频率</param>
        /// <param name="includeMoCapTest">是否包含MoCap测试</param>
        /// <param name="includeEventTest">是否包含事件测试</param>
        public void RunComprehensiveTest(float testDuration, int messageRate, bool includeMoCapTest = true, bool includeEventTest = true)
        {
            this.testDuration = testDuration;
            this.messageRate = messageRate;
            
            // 重置测试
            messagesSent = 0;
            testStartTime = Time.time;
            lastMessageTime = Time.time;
            cpuUsageSamples.Clear();
            memoryUsageSamples.Clear();
            
            // 标记测试开始事件
            if (includeEventTest)
            {
                NetworkPerformanceIntegration.MarkEventStart("test_start");
            }
            
            // 开始MoCap模拟
            if (includeMoCapTest)
            {
                StartCoroutine(SimulateMoCapFrames());
            }
            
            stopwatch.Restart();
            enabled = true;
            
            Debug.Log($"Comprehensive test started: {messageRate} messages/s for {testDuration}s");
        }
        
        private System.Collections.IEnumerator SimulateMoCapFrames()
        {
            uint frameSequence = 0;
            while (enabled)
            {
                // 模拟MoCap帧到达
                float timestamp = Time.time;
                monitor.OnMoCapFrameReceived(frameSequence, timestamp);
                frameSequence++;
                
                // 模拟30fps的MoCap数据
                yield return new WaitForSeconds(1.0f / 30.0f);
            }
        }
        
        private void Update()
        {
            // 检查测试是否结束
            if (Time.time - testStartTime >= testDuration)
            {
                CompleteTest();
                return;
            }
            
            // 发送测试消息
            float messageInterval = 1.0f / messageRate;
            if (Time.time - lastMessageTime >= messageInterval)
            {
                SendTestMessage();
                lastMessageTime = Time.time;
            }
            
            // 每1秒记录一次性能数据
            if (Mathf.FloorToInt(Time.time) > Mathf.FloorToInt(Time.time - Time.deltaTime))
            {
                RecordPerformanceData();
            }
        }
        
        private void SendTestMessage()
        {
            // 生成测试消息
            string testMessage = GenerateTestMessage(messageSize);
            
            // 这里只是模拟消息发送，实际项目中应该通过真实的网络发送
            // 但为了测试监控系统，我们可以手动触发事件
            
            // 模拟发送事件
            messagesSent++;
            
            // 注意：实际项目中，这些事件会由Mirror自动触发
            // 这里只是为了测试监控系统的性能
        }
        
        private string GenerateTestMessage(int size)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < size; i++)
            {
                sb.Append('x');
            }
            return sb.ToString();
        }
        
        private void RecordPerformanceData()
        {
            // 记录CPU使用率（简化版，实际项目中可能需要更精确的测量）
            float cpuUsage = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1024f / 1024f;
            cpuUsageSamples.Add(cpuUsage);
            
            // 记录内存使用
            float memoryUsage = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / 1024f / 1024f;
            memoryUsageSamples.Add(memoryUsage);
            
            // 记录网络性能数据
            var perfData = monitor.GetLatestData();
            Debug.Log($"Time: {Time.time - testStartTime:F1}s, CPU: {cpuUsage:F2}MB, Memory: {memoryUsage:F2}MB, Latency: {perfData.latency:F1}ms, Jitter: {perfData.jitter:F1}ms, Loss: {perfData.packetLoss:F1}%");
        }
        
        private void CompleteTest()
        {
            stopwatch.Stop();
            
            // 计算平均性能数据
            float avgCpu = CalculateAverage(cpuUsageSamples);
            float avgMemory = CalculateAverage(memoryUsageSamples);
            var avgNetworkData = monitor.GetAverageData(testDuration);
            
            // 标记测试结束事件
            NetworkPerformanceIntegration.MarkEventEnd("test_start");
            
            // 输出测试结果
            Debug.Log("\n=== Performance Test Results ===");
            Debug.Log($"Test duration: {stopwatch.Elapsed.TotalSeconds:F1}s");
            Debug.Log($"Messages sent: {messagesSent}");
            Debug.Log($"Average CPU usage: {avgCpu:F2}MB");
            Debug.Log($"Average memory usage: {avgMemory:F2}MB");
            Debug.Log($"Average latency: {avgNetworkData.latency:F1}ms");
            Debug.Log($"Average jitter: {avgNetworkData.jitter:F1}ms");
            Debug.Log($"Average packet loss: {avgNetworkData.packetLoss:F1}%");
            
            // 新增测试结果
            Debug.Log($"Average latency min: {avgNetworkData.latencyMin:F1}ms");
            Debug.Log($"Average latency max: {avgNetworkData.latencyMax:F1}ms");
            Debug.Log($"Average latency median: {avgNetworkData.latencyMedian:F1}ms");
            Debug.Log($"Average latency 95th: {avgNetworkData.latency95th:F1}ms");
            Debug.Log($"Average frame arrival delay: {avgNetworkData.frameArrivalDelay:F1}ms");
            Debug.Log($"Average frame interval: {avgNetworkData.frameInterval:F1}ms");
            Debug.Log($"Average frame loss rate: {avgNetworkData.frameLossRate:F1}%");
            Debug.Log("===============================");
            
            // 导出测试数据
            string filePath = Application.persistentDataPath + "/performance_test_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";
            ExportTestData(filePath);
            
            // 禁用脚本
            enabled = false;
        }
        
        private float CalculateAverage(List<float> values)
        {
            if (values.Count == 0) return 0;
            
            float sum = 0;
            foreach (float value in values)
            {
                sum += value;
            }
            return sum / values.Count;
        }
        
        private void ExportTestData(string filePath)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("Time,CPU Usage (MB),Memory Usage (MB),Latency (ms),Jitter (ms),Packet Loss (%),Bytes Sent,Bytes Received,Latency Min,Latency Max,Latency Median,Latency 95th,Frame Arrival Delay,Frame Interval,Frame Loss Rate,Total Frames,Lost Frames,Event Sync Delay,Event Count");
            
            var historyData = monitor.GetHistoryData(testStartTime, Time.time);
            for (int i = 0; i < historyData.Count && i < cpuUsageSamples.Count; i++)
            {
                var data = historyData[i];
                float cpu = i < cpuUsageSamples.Count ? cpuUsageSamples[i] : 0;
                float memory = i < memoryUsageSamples.Count ? memoryUsageSamples[i] : 0;
                
                sb.AppendLine($"{data.sentTimestamp - testStartTime:F2},{cpu:F2},{memory:F2},{data.latency:F2},{data.jitter:F2},{data.packetLoss:F2},{data.latencyMin:F2},{data.latencyMax:F2},{data.latencyMedian:F2},{data.latency95th:F2},{data.frameArrivalDelay:F2},{data.frameInterval:F2},{data.frameLossRate:F2}");
            }
            
            System.IO.File.WriteAllText(filePath, sb.ToString());
            Debug.Log($"Test data exported to: {filePath}");
        }
        
        /// <summary>
        /// 运行压力测试
        /// </summary>
        /// <param name="messageRate">消息频率</param>
        /// <param name="duration">测试持续时间</param>
        public void RunStressTest(int messageRate, float duration)
        {
            this.messageRate = messageRate;
            this.testDuration = duration;
            
            // 重置测试
            messagesSent = 0;
            testStartTime = Time.time;
            lastMessageTime = Time.time;
            cpuUsageSamples.Clear();
            memoryUsageSamples.Clear();
            
            stopwatch.Restart();
            enabled = true;
            
            Debug.Log($"Stress test started: {messageRate} messages/s for {duration}s");
        }
    }
}
