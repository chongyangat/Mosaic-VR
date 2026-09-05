// ===================================================
// Copyright ©. All rights reserved.
// Author：ASSJ
// CreateTime：2024/9/18 18:01:16
// Version: v1.0
// Description：玩家处理器
// ===================================================

using UnityEngine;

namespace VBSOED
{
    /// <summary>
    /// 玩家处理器
    /// </summary>
    public class PlayerHandler : MonoBehaviour
    {
        [Header("Tip")]
        [Header("Text")]
        [Tooltip("提示对象")]
        [SerializeField]
        private GameObject m_Tip;

        [Tooltip("提示语格式")]
        [SerializeField]
        private string m_TipContent = "小心！撞到{0:G}了！";

        [Header("Sound")]
        [Tooltip("声音播放器")]
        [SerializeField]
        private AudioSource m_SoundPlayer;

        [Tooltip("警示音效")]
        [SerializeField]
        private AudioClip m_WarnSound;

        /// <summary>
        /// 撞到障碍物
        /// </summary>
        /// <param name="obstacleName">障碍物名称</param>
        public void HitObstacle(string obstacleName)
        {
            // 输出日志
            Debug.Log($"玩家：撞到{obstacleName}");
            // 显示提示
            m_Tip.SendMessage(Msg.SHOW_TIP, string.Format(m_TipContent, obstacleName));
            // 播放警示音效
            m_SoundPlayer.PlayOneShot(m_WarnSound);
        }
    }
}
