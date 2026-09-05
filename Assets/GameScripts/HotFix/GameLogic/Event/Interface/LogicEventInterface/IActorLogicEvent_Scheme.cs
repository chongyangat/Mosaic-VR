using UnityGameFramework.Runtime;

namespace GameLogic
{
    /// <summary>
    /// 试验方案逻辑事件接口
    /// 定义与试验方案控制相关的事件
    /// </summary>
    [EventInterface(EEventGroup.GroupLogic)]
    interface IActorLogicEvent_Scheme
    {

        /// <summary>
        /// 读取试验方案文件事件
        /// </summary>
        /// <param name="schemeFilePath">试验方案文件完整路径（.vbsoedtd）</param>
        void OnRead(string schemeFilePath);

        /// <summary>
        /// 保存试验方案文件开始事件
        /// </summary>
        /// <param name="schemeFileSavePath">试验方案文件保存路径</param>
        void SaveStart(string schemeFileSavePath);

        /// <summary>
        /// 保存试验方案文件完成事件
        /// </summary>
        /// <param name="schemeFileSavePath">试验方案文件保存路径</param>
        void SaveComplete(string schemeFileSavePath);

        /// <summary>
        /// 方案文件夹创建完成事件
        /// </summary>
        /// <param name="folderPath">方案文件夹完整路径</param>
        void OnSchemeFolderCreated(string folderPath);

    }
}