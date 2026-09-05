// ===================================================
// Copyright ©. All rights reserved.
// Author：ASSJ
// CreateTime：2024/8/27 11:00:16
// Version: v1.0
// Description：自助收银机
// ===================================================

using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace GameLogic
{
    /// <summary>
    /// 自助收银机
    /// </summary>
    public class CashRegister : MonoBehaviour
    {

        #region 数量

        [Tooltip("数量")]
        [SerializeField]
        private TextMeshPro m_GoodTotalAmountShower;

        /// <summary>
        /// 总数量
        /// </summary>
        private int m_GoodTotalAmount;

        /// <summary>
        /// 更新总数量
        /// </summary>
        private void UpdateTotalAmount()
        {
            m_GoodTotalAmountShower.text = $"{m_GoodTotalAmount}个";
        } 

        #endregion

        #region 总价格

        [Tooltip("总价格")]
        [SerializeField]
        private TextMeshPro m_GoodTotalPriceShower;

        /// <summary>
        /// 总价格
        /// </summary>
        private float m_GoodTotalPrice;

        /// <summary>
        /// 更新总价格
        /// </summary>
        private void UpdateTotalPrice()
        {
            m_GoodTotalPriceShower.text = $"￥{m_GoodTotalPrice}";
        }

        #endregion

        [Tooltip("自助收银机扫描器")]
        [SerializeField]
        private CashRegisterScanner m_CashRegisterScanner;

        #region 商品

        /// <summary>
        /// 已扫描的商品列表
        /// </summary>
        private HashSet<int> m_ScannedProductsList = new ();

        ///<summary>
        /// 此商品是否已经扫描过
        ///</summary>
        ///<param name="productId">商品ID</param>
        ///<returns></returns>
        public bool IsScanned(int productId)
        {
            return m_ScannedProductsList.Contains(productId);
        }

        ///<summary>
        /// 添加商品
        /// </summary>
        /// <param name="product">商品</param>
        /// <returns></returns>
        public bool AddGood(Product product)
        {
            // 判断是否已经添加过该商品
            if (m_ScannedProductsList.Contains(product.Id))
            {
                Debug.LogWarning($"该商品((id:{product.Id}){product.name})已经添加过");
                return false;
            }
            // 计算
            m_GoodTotalAmount++;
            m_GoodTotalPrice += product.Price;
            // 添加到购物车
            m_ScannedProductsList.Add(product.Id);
            // 更新UI
            UpdateTotalAmount();
            UpdateTotalPrice();

            return true;
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 获取总价格
        /// </summary>
        /// <returns></returns>
        public float GetTotalPrice()
        {
            return m_GoodTotalPrice;
        }

        /// <summary>
        /// 获取总数量
        /// </summary>
        /// <returns></returns>
        public int GetTotalAmount()
        {
            return m_GoodTotalAmount;
        }

        /// <summary>
        /// 重置数据
        /// </summary>
        public void ResetData()
        {
            Init();
        }

        #endregion

        /// <summary>
        /// 初始化
        /// </summary>
        private void Init()
        {
            m_GoodTotalAmount = 0;
            m_GoodTotalPrice = 0;
            UpdateTotalAmount();
            UpdateTotalPrice();
            // 清空
            m_ScannedProductsList.Clear();
        }

        #region U3D

        private void Start()
        {
            Init();
        }

        private void OnEnable()
        {
            m_CashRegisterScanner.OnAddProduct += AddGood;
        }

        private void OnDisable()
        {
            m_CashRegisterScanner.OnAddProduct -= AddGood;
        }

        #endregion
    }

}
