// ===================================================
// Copyright ©. All rights reserved.
// Author：ASSJ
// CreateTime：2024/8/27 11:00:16
// Version: v1.0
// Description：结算界面（UI层）
// ===================================================

using UnityEngine;
using UnityGameFramework.Runtime;
using GameMain;

namespace GameLogic
{
    /// <summary>
    /// 结算交互（UI层）
    /// 通过碰撞体检测手部进入触发结算
    /// </summary>
    public class SettlementUI : MonoBehaviour
    {
        private bool m_HasTriggered = false;

        private void OnTriggerEnter(Collider other)
        {
            if (!m_HasTriggered)
            {
                m_HasTriggered = true;
                Debug.Log("[SettlementUI] 检测到手部交互，触发结算");
                GameEvent.EventMgr.GetInterface<IActorLogicEvent>().OnSettlementClicked();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            m_HasTriggered = false;
        }
    }
}
