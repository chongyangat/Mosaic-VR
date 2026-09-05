// ===================================================
// Copyright ©. All rights reserved.
// Author：ASSJ
// CreateTime：2024/8/27 11:00:16
// Version: v1.0
// Description：结算调试工具（仅用于测试）
// ===================================================

#if DEBUG || UNITY_EDITOR
using UnityEngine;
using UnityGameFramework.Runtime;

namespace GameLogic
{
    /// <summary>
    /// 结算调试工具
    /// 按 P 键：自动扫描商品 + 放入购物篮 + 触发结算
    /// </summary>
    public class SettlementDebugger : MonoBehaviour
    {
        [Tooltip("收银机")]
        [SerializeField]
        private CashRegister m_CashRegister;

        [Tooltip("购物篮")]
        [SerializeField]
        private VBSOED.ShoppingBasket m_ShoppingBasket;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.P))
            {
                Debug.Log("[调试] 开始自动结算测试流程...");
                AutoSettlementTest();
            }
        }

        private void AutoSettlementTest()
        {
            var allProducts = FindObjectsOfType<Product>();

            if (allProducts == null || allProducts.Length == 0)
            {
                Debug.LogWarning("[调试] 场景中没有找到商品");
                return;
            }

            var testCount = Mathf.Min(3, allProducts.Length);
            int addedCount = 0;

            for (int i = 0; i < testCount; i++)
            {
                var product = allProducts[i];

                if (m_CashRegister != null && product != null)
                {
                    m_CashRegister.AddGood(product);
                    addedCount++;
                }

                if (m_ShoppingBasket != null && product != null)
                {
                    m_ShoppingBasket.AddGood(product);
                }
            }

            Debug.Log($"[调试] 已扫描并放入购物篮 {addedCount} 个商品");

            GameEvent.EventMgr.GetInterface<IActorLogicEvent>().OnSettlementClicked();
            Debug.Log("[调试] 已触发结算");
        }
    }
}
#endif
