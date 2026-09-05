using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic
{
    public class TimeBar : MonoBehaviour
    {
        [Tooltip("根对象")]
        [SerializeField]
        private GameObject m_Root;

        #region 进度

        [Tooltip("进度条")]
        [SerializeField]
        private Image m_ProgressBar;

        [Tooltip("进度文本")]
        [SerializeField]
        private TextMeshProUGUI m_ProgressText;

        /// <summary>
        /// 更新进度
        /// </summary>
        /// <param name="progress"></param>
        private void ProgressUpdate(float progress)
        {
            m_ProgressBar.fillAmount = progress;
            // 只显示整数部分
            m_ProgressText.text = Mathf.FloorToInt(progress * 100).ToString();
        }

        #endregion

        #region 控制

        /// <summary>
        /// 开始更新
        /// </summary>
        /// <param name="scecond">时间，单位：秒</param>
        public void StartUpdate(float scecond)
        {
            m_Scecond = scecond;
            elapsedTime = 0;
            m_IsStarted = true;
            m_Root.SetActive(true);
        }

        /// <summary>
        /// 停止更新
        /// </summary>
        public void StopUpdate()
        {
            m_IsStarted = false;
            m_Root.SetActive(false);
        }

        /// <summary>
        /// 是否开始更新
        /// </summary>
        private bool m_IsStarted = false;

        /// <summary>
        /// 时间到时
        /// </summary>
        public Action OnTimeUp;

        /// <summary>
        /// 当时间到时
        /// </summary> 
        private void OnTimeOver()
        {
            m_Root.SetActive(false);
            OnTimeUp?.Invoke();
        }

        #endregion

        /// <summary>
        /// 时间，单位：秒
        /// </summary>
        private float m_Scecond = 0;

        private float elapsedTime = 0.0f; // 已经过的时间

        #region U3D

        private void Awake()
        {
            m_Root.SetActive(false);
        }

        void Update()
        {
            if (m_IsStarted)
            {
                // 计算已经过去的时间
                elapsedTime += Time.deltaTime;

                // 计算进度比例
                float progress = elapsedTime / m_Scecond;

                // 更新进度条
                ProgressUpdate(progress);

                // 如果进度达到或超过1，重置进度条
                if (m_ProgressBar.fillAmount >= 1)
                {
                    m_IsStarted = false;
                    OnTimeOver();
                }
            }
        }

        #endregion
    }
}
