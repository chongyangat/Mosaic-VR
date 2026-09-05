using System;

namespace MetaQuestProEyeGazeUDP
{
    /// <summary>
    /// 时间戳提供器接口
    /// 
    /// 设计目的：
    /// 由于 Packages 层（如 MetaQuestPro）在编译时早于 Assets 层（如 GameMain），
    /// 因此 Packages 层的代码不能直接引用 Assets 层的类型。
    /// 
    /// 此接口定义于 Packages 层（抽象层），由 Assets 层（实现层）负责实现，
    /// 通过依赖注入的方式注入到 Packages 层的组件中。
    /// 
    /// 这样既保持了 Packages 层的独立性和可移植性，又能在运行时
    /// 使用 Assets 层提供的具体实现。
    /// 
    /// 使用示例：
    /// 1. GazeDataSender 持有 ITimestampProvider 引用
    /// 2. 在 Assets 层的初始化脚本中创建 TimestampProvider 实例并注入
    /// 3. GazeDataSender 调用 GetSyncedUnixTimeMilliseconds() 获取时间戳
    /// </summary>
    public interface ITimestampProvider
    {
        /// <summary>
        /// 获取同步后的Unix时间戳（毫秒，整数）
        /// </summary>
        long GetSyncedUnixTimeMilliseconds();

        /// <summary>
        /// 获取同步后的Unix时间戳（毫秒，含小数精度）
        /// 
        /// 保留亚毫秒精度，避免整数截断导致延迟测量误差。
        /// </summary>
        double GetSyncedUnixTimeMillisecondsD();

        /// <summary>
        /// 获取原始本地时间戳（毫秒）
        /// </summary>
        long GetLocalUnixTimeMilliseconds();
    }
}
