// ===================================================
// Copyright ©. All rights reserved.
// Author：ASSJ
// CreateTime：2024/9/19 16:31:16
// Version: v1.0
// Description：提示
// ===================================================

// using System.Collections.Generic;
using UnityEngine;

namespace VBSOED
{
    /// <summary>
    /// 提示
    /// </summary>
    public class Tip : MonoBehaviour
    {
        [Tooltip("根对象")]
        [SerializeField]
        private GameObject m_Root;

        [Tooltip("提示文本显示")]
        [SerializeField]
        private TMPro.TextMeshProUGUI m_TipText;

        [Tooltip("正常显示时间")]
        [SerializeField]
        private float m_ShowTime = 1f;

        //[Tooltip("最小显示时间")]
        //[SerializeField]
        //private float m_MinShowTime = 0.5f;

        ///<summary>
        /// 开始显示的时间
        /// </summary>
        private float m_StartShowTime;

        /////<summary>
        ///// 显示队列
        ///// </summary>
        //private Queue<string> m_ShowQueue = new();

        /// <summary>
        /// 显示提示
        /// </summary>
        /// <param name="content">提示内容</param>
        public void ShowTip(string content)
        {
            //m_ShowQueue.Enqueue(content);
            StartShow(content);
        }

        /// <summary>
        /// 开始显示
        /// </summary>
        /// <param name="content">提示内容</param>
        private void StartShow(string content)
        {
            m_TipText.text = content;
            // 记录开始显示的时间
            m_StartShowTime = Time.time;
            // 显示提示
            m_Root.SetActive(true);
        }

        private void Update()
        {
            //if (m_ShowQueue.Count > 0)
            //{
            //    // 取出队列中的第一个提示
            //    var content = m_ShowQueue.Dequeue();
            //    StartShow(content);
            //}
            // 时间到，隐藏提示
            if (m_Root.activeSelf && Time.time - m_StartShowTime > m_ShowTime)
            {
                m_Root.SetActive(false);
            }
        }

    }
}
