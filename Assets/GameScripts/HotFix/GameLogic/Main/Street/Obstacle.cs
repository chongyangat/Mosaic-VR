using UnityEngine;
using UnityGameFramework.Runtime;

namespace GameLogic
{
    /// <summary>
    /// 障碍物
    /// </summary>
    public class Obstacle : MonoBehaviour
    {
        #region 触发

        /// <summary>
        /// 处理碰撞
        /// </summary>
        private void HandleHit()
        {
            // 输出日志
            Log.Info($"障碍物‘{gameObject.name}’触发。");
            // 发送事件
            GameEvent.EventMgr.GetInterface<ITaskLogicEvent>().AddCurTaskScore(-1);
        }

        #endregion

        #region U3D

        private void OnCollisionEnter(Collision collision)
        {
            // 仅玩家才能触发
            if (!collision.gameObject.CompareTag("Player"))
            {
                return;
            }
            // 输出日志
            Log.Info($"‘{collision.gameObject.name}’撞到障碍物‘{gameObject.name}’。");
            // 
            HandleHit();
        }

        #endregion
    }
}
