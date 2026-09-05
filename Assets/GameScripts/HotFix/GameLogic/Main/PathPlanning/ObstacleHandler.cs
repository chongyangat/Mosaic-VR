// ===================================================
// Copyright ©. All rights reserved.
// Author：ASSJ
// CreateTime：2024/9/14 17:14:16
// Version: v1.0
// Description：障碍物处理器
// ===================================================

using UnityEngine;

namespace VBSOED
{
    /// <summary>
    /// 障碍物处理器
    /// </summary>
    public class ObstacleHandler : MonoBehaviour
    {
        [Tooltip("名称")]
        [SerializeField]
        private string m_DisplayName;

        private void OnTriggerEnter(Collider other)
        {
            // 输出日志
            // Debug.Log("碰撞检测：" + other.gameObject.name);
            // 检测是否为玩家
            if (other.gameObject.CompareTag("Player")
                // TODO: 临时先用相机，因目前仅相机跟Quest2移动而移动
                //|| other.gameObject.CompareTag("MainCamera")
                )
            {
                // 玩家进入触发器
                Debug.Log($"障碍物：{other.gameObject.name}撞到我了");
                // 处理玩家进入触发器的事件
                other.gameObject.SendMessage(Msg.HIT_OBSTACLE, m_DisplayName, SendMessageOptions.DontRequireReceiver);
            }
        }
    }
}
