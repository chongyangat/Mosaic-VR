# GazeDataSimulator 使用说明

## 概述

GazeDataSimulator 是一个用于模拟发送EyeGaze数据的工具，通过UDP协议将模拟的凝视点数据发送到指定的IP和端口，用于测试接收模块。

## 功能特性

- 使用 GAZE_DATA_SIMULATOR 宏控制开关
- 支持设置发送间隔、目标IP和端口
- 可分别开关左眼、右眼和双眼数据发送
- 支持随机生成凝视点数据
- 提供单例模式，方便全局访问
- 支持测试发送单次数据
- 包含编辑器扩展，提供可视化配置界面
- 支持密集点生成功能，可自动在稀疏点和密集点模式之间切换
- 可配置稀疏点和密集点的持续时间范围
- 可配置密集点发送间隔
- 支持配置密集点位置密集度，控制生成点的聚集程度
- 可配置密集点中心位置的随机范围
- 支持限制生成点在当前相机的可视范围内
- 提供日志输出选项，可控制是否输出日志
- 支持设置最小Z轴值，控制生成点的最小深度

## 集成方法

### 在GameApp中初始化

可以在GameApp的InitSystem方法中添加以下代码来初始化GazeDataSimulator：

```csharp
#if GAZE_DATA_SIMULATOR
    // 初始化凝视点数据模拟器
    GazeDataSimulator.Create();
    Debug.Log("GameApp: 初始化GazeDataSimulator");
#endif
```

### 手动创建和配置

```csharp
#if GAZE_DATA_SIMULATOR
    // 创建模拟器实例
    GazeDataSimulator simulator = GazeDataSimulator.Create(0.1f, "127.0.0.1", 8081);
    
    // 设置发送间隔
    simulator.SetSendInterval(0.5f);
    
    // 设置目标IP和端口
    simulator.SetTarget("192.168.1.100", 8081);
    
    // 测试发送单次数据
    simulator.TestSendSingleData();
#endif
```

## 使用方法

### 编辑器配置

1. 在Unity编辑器中，选择包含GazeDataSimulator组件的GameObject
2. 在Inspector面板中配置以下参数：
   - 发送间隔：发送数据的时间间隔（秒）
   - 目标IP：接收数据的IP地址
   - 目标端口：接收数据的端口号
   - 日志输出：是否启用日志输出
   - 左眼数据：是否发送左眼凝视点数据
   - 右眼数据：是否发送右眼凝视点数据
   - 双眼数据：是否发送双眼凝视点数据
   - X轴随机范围：凝视点数据X轴的随机范围
   - Y轴随机范围：凝视点数据Y轴的随机范围
   - 最小Z轴值：生成点的最小Z轴值
   - Z轴随机范围：凝视点数据Z轴的随机范围
   - 限制在相机可视范围：是否限制生成的点在当前相机的可视范围内
   - 启用密集点：是否启用密集点生成功能
   - 稀疏点最小持续时间：稀疏点模式的最小持续时间（秒）
   - 稀疏点最大持续时间：稀疏点模式的最大持续时间（秒）
   - 密集点最小持续时间：密集点模式的最小持续时间（秒）
   - 密集点最大持续时间：密集点模式的最大持续时间（秒）
   - 密集点发送间隔：密集点模式下的数据发送间隔（秒）
   - 位置密集度系数：密集点模式下位置的密集程度，值越小越密集
   - 密集点中心位置X轴随机范围：密集点中心位置X轴的随机范围
   - 密集点中心位置Y轴随机范围：密集点中心位置Y轴的随机范围
   - 密集点中心位置Z轴随机范围：密集点中心位置Z轴的随机范围

3. 使用测试按钮：
   - 发送一次：发送单次测试数据
   - 重置配置：恢复默认配置

### 运行时控制

```csharp
#if GAZE_DATA_SIMULATOR
    // 获取模拟器实例
    GazeDataSimulator simulator = GazeDataSimulator.Instance;
    
    // 检查实例是否存在
    if (GazeDataSimulator.HasInstance)
    {
        // 控制发送数据
        simulator.enabled = true; // 开始发送
        simulator.enabled = false; // 停止发送
        
        // 测试发送
        simulator.TestSendSingleData();
    }
#endif
```

## 数据格式

模拟器发送的JSON数据格式示例：

```json
{
  "timeStamp": 1234567890123,
  "isLeftEyeValid": true,
  "leftEyeGazePos": "(0.123,0.456,1.234)",
  "isRightEyeValid": true,
  "rightEyeGazePos": "(0.234,0.567,1.345)",
  "isDoubleEyesValid": true,
  "doubleEyesGazePos": "(0.178,0.511,1.289)"
}
```

## 宏控制

在PlayerSettings或脚本中定义GAZE_DATA_SIMULATOR宏来启用模拟器：

```csharp
#define GAZE_DATA_SIMULATOR
```

## 性能考虑

- 发送间隔建议设置在0.01秒以上，避免发送过于频繁影响性能
- 随机范围不宜过大，建议保持在1.0左右
- 在不需要时可以禁用模拟器组件以节省资源
- 启用密集点功能时，密集点发送间隔建议不要设置太小，避免网络拥塞
- 密集点功能可能会增加CPU和网络负载，建议根据实际测试情况调整参数
- 在低性能设备上使用时，可以考虑降低密集点发送频率或禁用密集点功能

## 注意事项

- 确保UDPManager已正确配置并运行
- 目标IP和端口需要与接收端一致
- 在生产环境中应关闭GAZE_DATA_SIMULATOR宏