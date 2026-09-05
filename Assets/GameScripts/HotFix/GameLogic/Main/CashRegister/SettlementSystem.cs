// ===================================================
// Copyright ©. All rights reserved.
// Author：ASSJ
// CreateTime：2024/8/27 11:00:16
// Version: v1.0
// Description：结算系统（逻辑层）
// ===================================================

using UnityEngine;
using UnityGameFramework.Runtime;

namespace GameLogic
{
    /// <summary>
    /// 结算系统（逻辑层）
    /// 负责处理结算业务逻辑
    /// </summary>
    public class SettlementSystem : MonoBehaviour
    {
        [Tooltip("收银机")]
        [SerializeField]
        private CashRegister m_CashRegister;

        [Tooltip("购物篮")]
        [SerializeField]
        private VBSOED.ShoppingBasket m_ShoppingBasket;

        private void OnEnable()
        {
            GameEvent.AddEventListener(IActorLogicEvent_Event.OnSettlementClicked, OnSettlementClicked);
        }

        private void OnDisable()
        {
            GameEvent.RemoveEventListener(IActorLogicEvent_Event.OnSettlementClicked, OnSettlementClicked);
        }

        private void OnSettlementClicked()
        {
            var productCount = m_ShoppingBasket.GetScannedProductCount();

            if (productCount <= 0)
            {
                Debug.LogWarning("结算失败：购物篮中没有已核销商品");
                return;
            }

            var totalPrice = m_CashRegister.GetTotalPrice();

            m_CashRegister.ResetData();
            GameEvent.EventMgr.GetInterface<IActorLogicEvent>().ClearShoppingBasket();

            GameEvent.EventMgr.GetInterface<IActorLogicEvent>().OnSettlementCompleted();
        }
    }
}
