// ===================================================
// Copyright ©. All rights reserved.
// Author：ASSJ
// CreateTime：2025/1/6 19:24:11
// Version: v1.0
// Description：购物篮
// ===================================================

using GameLogic;
using System.Collections.Generic;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace VBSOED
{
    /// <summary>
    /// 购物篮
    /// </summary>
    public class ShoppingBasket : MonoBehaviour
    {

        #region 数量

        //[Tooltip("数量")]
        //[SerializeField]
        //private TextMeshPro m_GoodTotalAmountShower;

        /// <summary>
        /// 总数量
        /// </summary>
        //private int m_GoodTotalAmount;

        ///// <summary>
        ///// 更新总数量
        ///// </summary>
        //private void UpdateTotalAmount()
        //{
        //    //m_GoodTotalAmountShower.text = $"{m_GoodTotalAmount}个";
        //} 

        #endregion

        [Tooltip("购物篮扫描器")]
        [SerializeField]
        private ShoppingBasketScanner m_ShoppingBasketScanner;

        [Tooltip("收银机")]
        [SerializeField]
        private CashRegister m_CashRegister;

        #region 商品

        /// <summary>
        /// 已在购物篮且已扫码商品
        /// </summary>
        private Dictionary<int, GameObject> m_ScannedProdsInBasket = new ();

        ///<summary>
        /// 添加商品
        /// </summary>
        /// <param name="product">商品</param>
        /// <returns></returns>
        public bool AddGood(Product product)
        {
            // 判断是否已经添加过该商品
            if (m_ScannedProdsInBasket.ContainsKey(product.Id))
            {
                Debug.LogWarning($"该商品((id:{product.Id}){product.name})已经添加过");
                return false;
            }
            // 判断商品是否已经扫码
            if (!m_CashRegister.IsScanned(product.Id))
            {
                Debug.LogWarning($"该商品((id:{product.Id}){product.name})未扫码");
                return false;
            }
            // 获得商品
            //m_TaskSys.PickedProduct(product.name);
            // 此处应该传入任务ID，而不是商品ID
#if !MANAGER_SERVER
            GameEvent.EventMgr.GetInterface<ITaskLogicEvent>().UpdateTaskProgressByProduct(product.Id, 1);
#endif            
            // 计算
            // m_GoodTotalAmount++;
            // 添加到购物篮
            m_ScannedProdsInBasket.Add(product.Id, product.gameObject);
            // 更新UI
            // UpdateTotalAmount();

            return true;
        }

        /// <summary>
        /// 移除商品
        /// </summary>
        /// <param name="product"></param>
        private void RemoveProduct(Product product)
        {
            // 判断是否已经添加过该商品
            if (!m_ScannedProdsInBasket.ContainsKey(product.Id))
            {
                Debug.LogWarning($"该商品((id:{product.Id}){product.name})未添加过");
                return;
            }
            // 同名但非同对象
            if (m_ScannedProdsInBasket[product.Id] != product.gameObject)
            {
                Debug.LogWarning($"该商品((id:{product.Id}){product.name})虽与添加过的商品同名，但非该对象，忽略。");
                return;
            }
#if !MANAGER_SERVER
            // 取消获得商品
            GameEvent.EventMgr.GetInterface<ITaskLogicEvent>().UpdateTaskProgressByProduct(product.Id, -1); 
#endif
            //m_TaskSys.UnpickedProduct(product.name);
            // 移除
            m_ScannedProdsInBasket.Remove(product.Id);
        }

        /// <summary>
        /// 清空商品
        /// </summary>
        public void ClearGoods()
        {
            // 销毁
            foreach (var item in m_ScannedProdsInBasket.Values)
            {
                Destroy(item);
            }
            // 清空
            m_ScannedProdsInBasket.Clear();
        }

        /// <summary>
        /// 获取已扫码商品数量
        /// </summary>
        /// <returns></returns>
        public int GetScannedProductCount()
        {
            return m_ScannedProdsInBasket.Count;
        }

        #endregion

        #region U3D

        private void OnEnable()
        {
            m_ShoppingBasketScanner.OnAddProduct += AddGood;
            m_ShoppingBasketScanner.OnRemoveProduct += RemoveProduct;
            // 注册清理购物篮事件
            GameEvent.AddEventListener(IActorLogicEvent_Event.ClearShoppingBasket, ClearGoods);
        }

        private void OnDisable()
        {
            m_ShoppingBasketScanner.OnAddProduct -= AddGood;
            m_ShoppingBasketScanner.OnRemoveProduct -= RemoveProduct;
            // 注销清理购物篮事件
            GameEvent.RemoveEventListener(IActorLogicEvent_Event.ClearShoppingBasket, ClearGoods);
        }

        #endregion
    }

}