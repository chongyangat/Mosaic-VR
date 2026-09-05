using UnityEngine;
using UnityGameFramework.Runtime;
using UnityEngine.UI;
using TMPro;

namespace GameLogic
{
    public class LoadingForm : UIFormLogic
    {
        [Tooltip("进度条")]
        [SerializeField]
        private Image m_ProgressBar;

        [Tooltip("进度文本")]
        [SerializeField]
        private TextMeshProUGUI m_ProgressText;

        /// <summary>
        /// 重置界面
        /// </summary>
        private void ResetUI()
        {
            m_ProgressBar.fillAmount = 0;
            m_ProgressText.text = "0";
        }

        #region 生命期

        protected override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            // 重置界面
            ResetUI();
            // 注册进度改变事件
            GameEvent.AddEventListener<float>(StringId.StringToHash("Progress"), ProgressUpdate);
        }

        private void ProgressUpdate(float progress)
        {
            m_ProgressBar.fillAmount = progress;
            // 只显示整数部分
            m_ProgressText.text = Mathf.FloorToInt(progress * 100).ToString();
        }

        public override void OnClose(bool isShutdown, object userData)
        {
            // 注销进度改变事件
            GameEvent.RemoveEventListener<float>(StringId.StringToHash("Progress"), ProgressUpdate);
            // 
            base.OnClose(isShutdown, userData);
        }

        #endregion
    }

}

