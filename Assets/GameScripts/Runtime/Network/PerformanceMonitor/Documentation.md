# 网络性能监控系统使用文档

## 系统概述

网络性能监控系统是一个专为Unity项目设计的网络性能分析工具，基于Mirror网络库，提供实时的网络延时、抖动和丢包数据监控。

### 主要功能

- **实时监控**：实时显示网络延时、抖动、丢包率和带宽使用
- **历史分析**：存储和分析历史性能数据
- **数据导出**：支持将性能数据导出为CSV格式
- **异常检测**：自动检测网络性能异常
- **告警系统**：基于阈值的性能告警和趋势分析
- **网络质量评估**：综合评估网络质量并提供优化建议
- **MoCap监控**：专门的动作捕捉系统网络监控
- **事件同步监控**：关键事件的同步延迟监控
- **可视化界面**：直观的UI显示和多类型图表
- **低性能开销**：优化的算法确保对游戏性能影响最小

## 安装方法

### 方法一：自动安装

1. 在Unity场景中选择一个GameObject
2. 添加`NetworkPerformanceIntegration`脚本
3. 在Inspector面板中点击"Setup Network Performance Monitor"
4. 选择是否启用UI显示

### 方法二：手动安装

1. 创建一个空GameObject，命名为"NetworkPerformanceMonitor"
2. 添加`NetworkPerformanceMonitor`组件
3. （可选）创建UI画布，添加`NetworkPerformanceUI`组件
4. 在`NetworkPerformanceUI`组件中设置相关UI元素

## 配置选项

### NetworkPerformanceMonitor配置

| 选项 | 描述 | 默认值 |
|------|------|--------|
| Enabled | 是否启用网络性能监控 | true |
| Update Interval | 数据更新间隔（秒） | 1.0 |
| History Duration | 历史数据存储时间（秒） | 300.0 |
| Latency Alert Threshold | 延迟告警阈值（毫秒） | 200.0 |
| Jitter Alert Threshold | 抖动告警阈值（毫秒） | 50.0 |
| Packet Loss Alert Threshold | 丢包率告警阈值（百分比） | 5.0 |
| Frame Loss Alert Threshold | 帧丢失率告警阈值（百分比） | 10.0 |
| Event Sync Alert Threshold | 事件同步延迟告警阈值（毫秒） | 200.0 |

### NetworkPerformanceUI配置

| 选项 | 描述 | 默认值 |
|------|------|--------|
| Enabled | 是否启用UI显示 | true |
| Update Interval | UI更新间隔（秒） | 0.1 |
| Chart Time Range | 图表显示时间范围（秒） | 60.0 |
| Latency Text | 延时显示文本组件 | - |
| Jitter Text | 抖动显示文本组件 | - |
| Packet Loss Text | 丢包率显示文本组件 | - |
| Bandwidth Text | 带宽显示文本组件 | - |
| Chart Container | 图表容器RectTransform | - |
| Chart Prefab | 图表点预制体 | - |

## 使用方法

### 基本使用

1. **自动集成**：
   ```csharp
   // 在NetworkManager的Start方法中添加
   void Start()
   {
       NetworkPerformanceIntegration.SetupNetworkPerformanceMonitor(this, true);
   }
   ```

2. **手动集成**：
   ```csharp
   // 获取监控器实例
   NetworkPerformanceMonitor monitor = NetworkPerformanceMonitor.Instance;
   
   // 订阅性能更新事件
   monitor.OnPerformanceUpdated += OnPerformanceUpdated;
   
   // 获取最新性能数据
   var data = monitor.GetLatestData();
   Debug.Log($"Latency: {data.latency}ms, Jitter: {data.jitter}ms, Loss: {data.packetLoss}%");
   
   // 导出数据
   monitor.ExportData(Application.persistentDataPath + "/network_performance.csv");
   ```

### 自定义INetwork实现集成

```csharp
// 包装自定义网络实现
INetwork myNetwork = new MyNetworkImplementation();
INetwork wrappedNetwork = NetworkPerformanceIntegration.WrapNetworkWithPerformanceMonitor(myNetwork);

// 使用包装后的网络
wrappedNetwork.Send("Hello world");
wrappedNetwork.OnReceive += OnNetworkReceive;
```

### 性能测试

1. 在场景中添加`PerformanceTest`组件
2. 配置测试参数：
   - Test Duration：测试持续时间
   - Message Rate：消息频率
   - Message Size：消息大小
3. 运行场景，查看控制台输出的测试结果
4. 测试数据会自动导出到PersistentDataPath

## 性能数据结构

```csharp
public struct PerformanceData
{
    public float timestamp;      // 时间戳
    public float latency;        // 延时（毫秒）
    public float jitter;         // 抖动（毫秒）
    public float packetLoss;     // 丢包率（百分比）
    public long bytesSent;       // 发送字节数
    public long bytesReceived;   // 接收字节数
    public int packetsSent;      // 发送数据包数
    public int packetsReceived;  // 接收数据包数
    
    // 延迟分布统计
    public float latencyMin;     // 最小延迟
    public float latencyMax;     // 最大延迟
    public float latencyMedian;  // 中位延迟
    public float latency95th;    // 95百分位延迟
    public float latency99th;    // 99百分位延迟
    
    // MoCap相关
    public float frameArrivalDelay; // 帧到达延迟
    public float frameInterval;     // 帧间隔
    public float frameLossRate;     // 丢帧率
    public int totalFrames;         // 总帧数
    public int lostFrames;          // 丢失帧数
    
    // 事件同步
    public float eventSyncDelay;    // 事件同步延迟
    public int eventCount;          // 事件数量
}
```

## 公共API

### NetworkPerformanceMonitor

| 方法 | 描述 | 参数 | 返回值 |
|------|------|------|--------|
| GetHistoryData() | 获取所有历史数据 | 无 | List<PerformanceData> |
| GetHistoryData(startTime, endTime) | 获取指定时间范围的历史数据 | startTime: 开始时间<br>endTime: 结束时间 | List<PerformanceData> |
| GetLatestData() | 获取最新性能数据 | 无 | PerformanceData |
| GetAverageData(timeWindow) | 获取指定时间窗口的平均性能数据 | timeWindow: 时间窗口（秒） | PerformanceData |
| GetTrendData(metric, timeWindow) | 获取指定指标的趋势数据 | metric: 指标名称<br>timeWindow: 时间窗口（秒） | float[] |
| IsAnomalyDetected(threshold) | 检测是否有性能异常 | threshold: 异常阈值 | bool |
| ExportData(filePath) | 导出所有性能数据 | filePath: 文件路径 | void |
| ExportData(filePath, startTime, endTime) | 导出指定时间范围的性能数据 | filePath: 文件路径<br>startTime: 开始时间<br>endTime: 结束时间 | void |
| OnMoCapFrameReceived(sequence, timestamp) | 处理MoCap帧到达 | sequence: 帧序列号<br>timestamp: 帧时间戳 | void |
| GetFrameLossPatterns() | 获取丢帧模式统计 | 无 | int[] |
| GetConsecutiveFrameLoss() | 获取连续丢帧数 | 无 | int |
| GetLastFrameLossTime() | 获取上次丢帧时间 | 无 | float |
| MarkEventStart(eventId) | 标记事件开始 | eventId: 事件ID | void |
| MarkEventEnd(eventId) | 标记事件结束并计算同步延迟 | eventId: 事件ID | float |
| GetEventSyncDelays() | 获取事件同步延迟历史 | 无 | List<float> |
| GetAverageEventSyncDelay() | 获取平均事件同步延迟 | 无 | float |
| AssessNetworkQuality() | 评估网络质量 | 无 | NetworkQualityAssessment |
| GetNetworkQualitySummary() | 获取网络质量评估摘要 | 无 | string |

### 事件

| 事件 | 描述 | 参数 |
|------|------|------|
| OnPerformanceUpdated | 性能数据更新时触发 | PerformanceData |
| OnAlertTriggered | 告警触发时触发 | AlertData |

### NetworkPerformanceIntegration

| 方法 | 描述 | 参数 | 返回值 |
|------|------|------|--------|
| SetupNetworkPerformanceMonitor(networkManager, enableUI) | 自动设置网络性能监控 | networkManager: 网络管理器<br>enableUI: 是否启用UI | NetworkPerformanceMonitor |
| SetupMoCapMonitoring(monitor, mocapSystem) | 设置MoCap监控 | monitor: 网络性能监控器<br>mocapSystem: MoCap系统实例 | void |
| ProcessMoCapFrame(monitor, sequence, timestamp, dataSize) | 处理MoCap帧数据 | monitor: 网络性能监控器<br>sequence: 帧序列号<br>timestamp: 帧时间戳<br>dataSize: 数据大小 | void |
| SimulateMoCapData(monitor, frameRate, duration) | 模拟MoCap数据（用于测试） | monitor: 网络性能监控器<br>frameRate: 帧率<br>duration: 持续时间 | void |
| MarkEventStart(eventId) | 标记事件开始 | eventId: 事件ID | void |
| MarkEventEnd(eventId) | 标记事件结束 | eventId: 事件ID | float |
| WrapNetworkWithPerformanceMonitor(networkImplementation) | 为自定义INetwork实现添加性能监控 | networkImplementation: 自定义网络实现 | INetwork |

## 性能调优建议

### 减少性能开销

1. **调整更新间隔**：根据游戏需求调整`Update Interval`，减少高频更新
2. **限制历史数据**：根据内存情况调整`History Duration`
3. **禁用UI**：在发布版本中可以禁用UI显示
4. **选择性监控**：只在需要时启用监控

### 网络优化建议

1. **减少消息频率**：合并消息，减少网络传输次数
2. **优化消息大小**：压缩数据，减少传输字节数
3. **使用合适的传输层**：根据游戏类型选择合适的Transport
4. **实现消息优先级**：重要消息使用可靠通道，非重要消息使用不可靠通道

### 异常处理

1. **设置合理的异常阈值**：根据游戏类型设置合适的异常检测阈值
2. **实现自动重连**：当检测到网络异常时自动尝试重连
3. **提供用户反馈**：当网络性能下降时向用户显示警告

## 常见问题

### Q: 监控系统对游戏性能有影响吗？

A: 监控系统经过优化，使用固定大小的数组和高效的算法，对游戏性能影响很小（CPU占用<1%）。

### Q: 如何处理大量历史数据？

A: 监控系统使用循环缓冲区存储历史数据，自动覆盖旧数据，避免内存持续增长。

### Q: 可以在发布版本中使用吗？

A: 可以，但建议在发布版本中禁用UI显示，只保留核心监控功能。

### Q: 支持哪些网络库？

A: 主要支持Mirror网络库，同时提供了对自定义INetwork实现的包装支持。

## 示例场景

### 场景1：基本监控

1. 创建一个新场景
2. 添加NetworkManager组件
3. 添加NetworkPerformanceIntegration组件
4. 运行场景，查看UI显示的网络性能数据

### 场景2：性能测试

1. 创建一个新场景
2. 添加NetworkManager组件
3. 添加PerformanceTest组件
4. 配置测试参数
5. 运行场景，查看控制台输出的测试结果

### 场景3：自定义集成

1. 创建一个实现INetwork接口的自定义网络类
2. 使用NetworkPerformanceIntegration.WrapNetworkWithPerformanceMonitor包装
3. 使用包装后的网络实例进行通信
4. 查看性能监控数据

## 版本历史

### v1.1.0
- 增强异常检测和告警功能
- 完善MoCap监控的具体集成实现
- 增强数据可视化和分析能力
- 添加网络质量评估和自动优化建议
- 优化性能和内存使用
- 完善文档和示例代码

### v1.0.0
- 初始版本
- 实现基本的网络性能监控功能
- 支持Mirror网络库
- 提供实时UI显示
- 支持数据导出

## 联系我们

如有任何问题或建议，请联系开发团队。
