using System;

namespace GameLogic
{
    /// <summary>
    /// 商品运动数据结构
    /// </summary>
    public struct ProductMovementData
    {
        public int productId;           // 商品ID
        public string productName;      // 商品名称
        public string timestamp;        // 采集时间戳 (yyyy-MM-dd HH:mm:ss.fff)
        
        // 位置信息
        public float posX, posY, posZ;  // 世界坐标位置
        
        // 旋转信息（欧拉角）
        public float rotX, rotY, rotZ;  // 世界坐标旋转

        /// <summary>
        /// 获取CSV表头
        /// </summary>
        public static string GetCsvHeader()
        {
            return "序号,采集时间,商品ID,商品名称,位置X,位置Y,位置Z,旋转X,旋转Y,旋转Z";
        }

        /// <summary>
        /// 转换为CSV行字符串
        /// </summary>
        /// <param name="index">序号</param>
        public string ToCsvLine(int index)
        {
            return $"{index},{timestamp},{productId},{productName},{posX:F6},{posY:F6},{posZ:F6},{rotX:F6},{rotY:F6},{rotZ:F6}";
        }
    }
}
