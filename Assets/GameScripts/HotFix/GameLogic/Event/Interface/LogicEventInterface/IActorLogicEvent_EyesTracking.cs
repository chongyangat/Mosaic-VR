using UnityGameFramework.Runtime;

namespace GameLogic
{
    /// <summary>
    /// 眼动数据记录逻辑事件接口
    /// 定义与眼动数据记录相关的事件
    /// </summary>
    [EventInterface(EEventGroup.GroupLogic)]
    interface IActorLogicEvent_EyesTracking
    {

        /// <summary>
        /// 读取眼动数据记录文件事件
        /// </summary>
        /// <param name="schemeFilePath">眼动数据记录文件完整路径</param>
        void OnRead(string schemeFilePath);

        /// <summary>
        /// 保存眼动数据记录文件开始事件
        /// </summary>
        /// <param name="schemeFileSavePath">眼动数据记录文件保存路径</param>
        void SaveStart(string schemeFileSavePath);

        /// <summary>
        /// 保存眼动数据记录文件完成事件
        /// </summary>
        /// <param name="schemeFileSavePath">眼动数据记录文件保存路径</param>
        void SaveComplete(string schemeFileSavePath);

    }
}