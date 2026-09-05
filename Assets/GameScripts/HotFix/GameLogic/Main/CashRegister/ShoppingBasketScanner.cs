// ===================================================
// Copyright ©. All rights reserved.
// Author：ASSJ
// CreateTime：2025/1/6 19:20:13
// Version: v1.0
// Description：购物篮扫描器
// ===================================================

using UnityEngine;

namespace GameLogic
{
    /// <summary>
    /// 购物篮扫描器
    /// </summary>
    public class ShoppingBasketScanner : MonoBehaviour
    {
        [Tooltip("货物层")]
        [SerializeField]
        private string m_GoodLayer = "Good";

        [Header("Audio Effect")]
        [Tooltip("添加商品成功声音")]
        [SerializeField]
        private AudioClip m_SucceedToAddAutClip;

        [Tooltip("添加商品失败声音")]
        [SerializeField]
        private AudioClip m_FailedToAddAutClip;

        /// <summary>
        /// 声音播放器
        /// </summary>
        private AudioSource m_AudioPlayer;

        #region 扫描

        /// <summary>
        /// 增加商品
        /// </summary>
        public event System.Func<Product, bool> OnAddProduct;

        /// <summary>
        /// 移除商品
        /// </summary>
        public event System.Action<Product> OnRemoveProduct;

        /// <summary>
        /// 扫描商品
        /// </summary>
        /// <param name="product"></param>
        /// <returns></returns>
        private bool ScanProducts(Product product)
        {
            if (product == null)
            {
                // 输出日志
                Debug.LogWarning("购物篮检测到物体，但该物体没有Good组件");
                return false;
            }
            // 输出日志
            Debug.Log($"购物篮扫描到商品((id:{product.Id}){product.name})");
            // 
            if (OnAddProduct == null)
            {
                // 输出日志
                Debug.LogWarning("没有注册任何OnAddProduct事件");
                return false;
            }
            // 添加失败
            if (!OnAddProduct.Invoke(product))
            {
                // 输出日志
                Debug.LogWarning($"购物篮添加商品商品((id:{product.Id}){product.name})失败");
                return false;
            }
            // 添加成功
            // 输出日志
            Debug.Log($"购物篮成功添加商品商品((id:{product.Id}){product.name})");
            return true;
        }

        #endregion

        #region U3D

        private void Awake()
        {
            m_AudioPlayer = GetComponent<AudioSource>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag(m_GoodLayer))
            {
                // 输出日志
                Debug.Log($"购物篮检测到物体{other.name}进入");
                //
                var good = other.GetComponentInChildren<Product>();
                if (!ScanProducts(good))
                {
                    // TODO 因有Bug会触发多次，所以暂时不播放失败音效
                    // m_AudioPlayer.PlayOneShot(m_FailedToAddAutClip);
                }
                else
                {
                    m_AudioPlayer.PlayOneShot(m_SucceedToAddAutClip);
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag(m_GoodLayer))
            {
                // 输出日志
                Debug.Log($"购物篮检测到物体{other.name}退出");
                // 
                OnRemoveProduct?.Invoke(other.GetComponentInChildren<Product>());
            }
        }

        #endregion
    }
}
