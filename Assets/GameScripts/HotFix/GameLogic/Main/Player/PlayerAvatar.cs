// ===================================================
// Copyright ©. All rights reserved.
// Author：ASSJ
// CreateTime：2024/9/19 19:14:16
// Version: v1.0
// Description：角色代理
// ===================================================

using UnityEngine;

namespace VBSOED
{
    /// <summary>
    /// 角色代理
    /// </summary>
    public class PlayerAvatar : MonoBehaviour
    {
        [Tooltip("角色")]
        [SerializeField]
        private Transform m_Player;

        private void Update()
        {
            // 仅同步位置中的x轴与y轴
            transform.position = new Vector3(m_Player.position.x, transform.position.y, m_Player.position.z);
        }
    }
}
