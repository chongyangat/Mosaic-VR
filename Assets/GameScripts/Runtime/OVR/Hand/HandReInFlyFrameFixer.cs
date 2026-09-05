using Oculus.Interaction.Input;
using UnityEngine;

namespace GameMain
{
    /// <summary>
    /// 手部碰撞器瞬移帧修复
    /// </summary>
    public class HandReInFlyFrameFixer : MonoBehaviour
    {
        [Header("Skip Frame")]
        [Tooltip("跳过的瞬移帧数，物理循环默认每秒50帧，具体根据设置与效果调整")]
        [SerializeField]
        private int m_SkipFrame = 30;

        /// <summary>
        /// 显示/隐藏手部碰撞器
        /// </summary>
        void ToggleHandColliders(Collider[] handColliders, bool isEnabled)
        {
            // 设置所有手部碰撞器的状态为Trigger
            foreach (var item in handColliders)
            {
                item.isTrigger = !isEnabled;
            }
        }

        /// <summary>
        /// 判断手是否可见
        /// </summary>
        /// <param name="handCapsulesRoot"></param>
        /// <returns></returns>
        private bool IsHandVisuabled(Transform handCapsulesRoot)
        {
            foreach (Transform item in handCapsulesRoot)
            {
                if (item.gameObject.activeInHierarchy)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 跳过瞬移帧后显示手部碰撞器
        /// </summary>
        private void SkipFrameToShowHideHandColliders()
        {
            // 寻找左手和右手capsules
            if (m_LeftHandCapsules == null)
            {
                m_LeftHandCapsules = m_LeftHandCapsulesRoot.transform.Find("Capsules");
                if (m_LeftHandCapsules != null)
                {
                    m_LeftHandColliders = m_LeftHandCapsules.GetComponentsInChildren<Collider>();
                }
            }
            else
            {
                if (IsHandVisuabled(m_LeftHandCapsules))
                {
                    if (m_LeftHandCurSkipFrame++ > m_SkipFrame)
                    {
                        // 输出日志
                        if (m_IsDebug)
                        {
                            Debug.Log($"已跳过{m_LeftHandCurSkipFrame}帧，将显示左手手部碰撞根对象。"); 
                        }
                        ToggleHandColliders(m_LeftHandColliders, true);
                    }
                }
                else
                {
                    m_LeftHandCurSkipFrame = 0;
                    ToggleHandColliders(m_LeftHandColliders, false);
                }
            }
            //
            if (m_RightHandCapsules == null)
            {
                m_RightHandCapsules = m_RightHandCapsulesRoot.transform.Find("Capsules");
                if (m_RightHandCapsules != null)
                {
                    m_RightHandColliders = m_RightHandCapsules.GetComponentsInChildren<Collider>();
                }
            }
            else
            {
                if (IsHandVisuabled(m_RightHandCapsules))
                {
                    if (m_RightHandCurSkipFrame++ > m_SkipFrame)
                    {
                        // 输出日志
                        if (m_IsDebug)
                        {
                            Debug.Log($"已跳过{m_RightHandCurSkipFrame}帧，将显示右手手部碰撞根对象。");
                        }          
                        ToggleHandColliders(m_RightHandColliders, true);
                    }
                }
                else
                {
                    m_RightHandCurSkipFrame = 0;
                    ToggleHandColliders(m_RightHandColliders, false);
                }
            }
        }

        #region 左手

        [Header("Hand Capsules")]
        [Tooltip("左手手部碰撞胶囊体父级")]
        [SerializeField]
        private HandPhysicsCapsules m_LeftHandCapsulesRoot;

        /// <summary>
        /// 左手手部碰撞胶囊体
        /// </summary>
        Transform m_LeftHandCapsules;

        /// <summary>
        /// 左手手部碰撞对象列表
        /// </summary>
        Collider[] m_LeftHandColliders;

        /// <summary>
        /// 跳过若干帧后显示左手手部碰撞器
        /// </summary>
        private int m_LeftHandCurSkipFrame = 0; 

        #endregion

        #region 右手

        [Tooltip("右手手部碰撞胶囊体父级")]
        [SerializeField]
        public HandPhysicsCapsules m_RightHandCapsulesRoot;

        /// <summary>
        /// 右手手部碰撞根对象
        /// </summary>
        Transform m_RightHandCapsules;

        /// <summary>
        /// 右手手部碰撞对象列表
        /// </summary>
        Collider[] m_RightHandColliders;

        /// <summary>
        /// 跳过若干帧后显示右手手部碰撞器
        /// </summary>
        private int m_RightHandCurSkipFrame = 0;

        #endregion

        #region Debug

        [Header("Debug")]
        [SerializeField]
        private bool m_IsDebug = false;

        #endregion

        #region U3D

        private void FixedUpdate()
        {
            SkipFrameToShowHideHandColliders();
        }

        #endregion
    }

}