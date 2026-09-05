// ===================================================
// Copyright ©. All rights reserved.
// Author：ASSJ
// CreateTime：2024/8/27 11:12:16
// Version: v1.0
// Description：货物
// ===================================================
using GameMain;
using Mirror;
#if DEBUG_PRODUCT2
using UnityEditor;
# endif
using UnityEngine;
using UnityGameFramework.Runtime;

namespace GameLogic
{
    /// <summary>
    /// 货物
    /// </summary>
    public class Product : NetworkBehaviour
    {

        #region 配置

        [SyncVar]
        private int m_Id;

        public int Id => m_Id;

        /// <summary>
        /// 货物价格
        /// </summary>
        private float m_Price;

        /// <summary>
        /// 货物价格
        /// </summary>
        public float Price => m_Price;

        /// <summary>
        /// 配置
        /// </summary>
        /// <param name="id"></param>
        /// <param name="price"></param>
        public void Setup(int? id, float price)
        {
            if (id != null)
            {
                m_Id = id.Value;
            }
            m_Price = price;
        }

        #endregion

        #region Mirror

        public override void OnStartClient()
        {
            if (isServer)
            {
                return;
            }
            // TODO 工期关系，暂时所有商品都被最近连接的客户端获取所有权，仅适用单一客户端的情况，后期需要修改。
            // 输出日志
            Log.Debug($"商品((id:{m_Id}){gameObject.name})({netId})获取网络所有权");
            // 客户端开始时，获取商品的网络所有权
            GameModule.Event.Fire(AssignNetObjOwnerEventArgs.EventId, AssignNetObjOwnerEventArgs.Create(netIdentity));
        }

        public override void OnStopClient()
        {
            if (isServer)
            {
                return;
            }
            // 输出日志
            Log.Debug($"商品((id:{m_Id}){gameObject.name})({netId})释放网络所有权");
            // 客户端停止时，释放商品的网络所有权
            GameModule.Event.Fire(RemoveNetObjOwnerEventArgs.EventId, RemoveNetObjOwnerEventArgs.Create(netIdentity));
        }

        #endregion

        #region U3D
#if DEBUG_PRODUCT

        private Vector3 m_StartPos;

        [Header("Debug")]

        private int m_ProductId = 465;

        public Vector3 m_EndPos = new Vector3(-1.126518f, 1.086f, 1.540711f);

        public float m_Speed = 0.5f;

        private Rigidbody m_Rigidbody;

        private bool m_IsForword = false;

        private void Start()
        {
            if (!isServer)
            {
                // 记录当前位置
                m_StartPos = transform.position;
                // 获取刚体组件
                m_Rigidbody = GetComponent<Rigidbody>();
            }
        }

        private void Update()
        {
            if (!isServer && m_ProductId == m_Id && isOwned)
            {
                // 
                if (null != m_Rigidbody && !m_Rigidbody.isKinematic)
                {
                    m_Rigidbody.isKinematic = true;
                }
                // 移动到目标点，再移动到起始点
                if (m_IsForword)
                {
                    transform.position = Vector3.MoveTowards(transform.position, m_EndPos, m_Speed * Time.deltaTime);
                }
                else
                {
                    transform.position = Vector3.MoveTowards(transform.position, m_StartPos, m_Speed * Time.deltaTime);
                }
                // 
                if (transform.position == m_EndPos)
                {
                    m_IsForword = false;
                }
                if (transform.position == m_StartPos)
                {
                    m_IsForword = true;
                }
            }
        }

#endif
        private void OnCollisionEnter(Collision collision)
        {
            // 输出日志
            Log.Debug($"商品((id:{m_Id}){gameObject.name})({netId})与{collision.gameObject.name}碰撞");
            // 如果碰撞对象的父对象是Capsules，则暂停编辑器播放
#if DEBUG_PRODUCT2
            var parentObj = collision.gameObject.transform.parent;
            if (parentObj != null && parentObj.name == "Capsules")
            {
                // 输出日志
                Log.Debug($"手{collision.gameObject.name}(位置：{collision.transform.position})(父级：{parentObj.parent.name})撞到" +
                    $"商品((id:{m_Id}){gameObject.name})({netId})的{collision.GetContact(0).point}地方了！");
                EditorApplication.isPaused = true;
            } 
#endif
        }

        #endregion

        private void OnEnable()
        {
            if (ProductMovementRecorder.Instance != null)
            {
                ProductMovementRecorder.Instance.RegisterProduct(this);
            }
        }

        private void OnDisable()
        {
            if (ProductMovementRecorder.Instance != null)
            {
                ProductMovementRecorder.Instance.UnregisterProduct(this);
            }
        }

    }
}
