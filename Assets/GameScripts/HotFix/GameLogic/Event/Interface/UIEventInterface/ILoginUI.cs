using UnityGameFramework.Runtime;

namespace GameLogic
{
    /// <summary>
    /// 登录界面事件
    /// </summary>
    [EventInterface(EEventGroup.GroupUI)]
    public interface ILoginUI
    {
        /// <summary>
        /// 用户就绪状态改变
        /// </summary>
        /// <param name="isReady"></param>
        public void OnUserReadyChange(bool isReady);

    }
}