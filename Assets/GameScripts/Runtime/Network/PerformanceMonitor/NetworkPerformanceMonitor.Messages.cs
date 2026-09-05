using Mirror;
using static GameMain.NetworkPerformanceMonitor;

namespace GameMain
{
    /// <summary>
    /// 批量性能数据上报消息 - 用于VR客户端将多个MoCap→VR性能数据批量发送到管理端
    /// </summary>
    public struct BatchPerformanceDataReportMsg : NetworkMessage
    {
        /// <summary>
        /// 性能数据数组
        /// </summary>
        public PerformanceData[] dataArray;

        /// <summary>
        /// 客户端唯一标识
        /// </summary>
        public string clientId;

        /// <summary>
        /// 构造函数
        /// </summary>
        public BatchPerformanceDataReportMsg(PerformanceData[] dataArray, string clientId)
        {
            this.dataArray = dataArray;
            this.clientId = clientId;
        }
    }
}
