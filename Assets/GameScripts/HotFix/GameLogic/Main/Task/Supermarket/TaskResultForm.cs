using TMPro;
using UnityEngine;
using UnityGameFramework.Runtime;
using System.Collections.Generic;
using UnityEngine.UI;

namespace GameLogic.Supermarket
{
    public class TaskResultForm : UIFormLogic
    {

        [Header("Info")]
        public TextMeshProUGUI m_TimeText;

        public TextMeshProUGUI m_CompletedText;

        public TextMeshProUGUI m_TotalText;

        public TextMeshProUGUI m_CompletedRatioText;

        #region 功能

        [Header("Fun")]
        [Tooltip("仅管理端填")]
        [SerializeField]
        private Button m_Return;

        private void OnReturnClick()
        {
            // 停止试验
            GameEvent.EventMgr.GetInterface<IActorLogicEvent>().OnStopTrial();
        }

        private void OnStopTrial()
        {
            if (TryGetComponent<UIForm>(out var uiForm))
            {
                // 关闭自身
                try
                {
                    GameModule.UI.CloseUIForm(uiForm);
                }
                catch (System.Exception ex)
                {
                    Log.Warning(ex.ToString());
                }
            }
        }

        #endregion

        #region 提取数据

        /// <summary>
        /// 显示数据
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private void ShowData(Dictionary<string, object> data)
        {
            var costTime = data["CostTime"] as string;
            m_TimeText.text = costTime;

            var completedTasksQty = (int)data["CompletedTasksQty"];
            m_CompletedText.text = completedTasksQty.ToString();

            var totalTasksQty = (int)data["TotalTasksQty"];
            m_TotalText.text = totalTasksQty.ToString();

            // 计算完成比例
            var completedRatio = 0f;
            if (totalTasksQty > 0)
            {
                completedRatio = (float)completedTasksQty / totalTasksQty;
            }
            m_CompletedRatioText.text = completedRatio.ToString("P0");
        }

        #endregion

        #region 生命期

        protected override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            // 
            GameEvent.AddEventListener(IActorLogicEvent_Event.OnStopTrial, OnStopTrial);
            // 
            if (m_Return != null)
            {
                m_Return.onClick.AddListener(OnReturnClick);
            }
            // 提取数据
            var data = userData as Dictionary<string, object>;
            if (data == null)
            {
                // 输出日志
                Log.Warning("TaskResultForm OnOpen userData is null");
                return;
            }
            // 显示数据
            ShowData(data);
        }

        protected override void Close()
        {
            GameEvent.RemoveEventListener(IActorLogicEvent_Event.OnStopTrial, OnStopTrial);
            if (m_Return != null)
            {
                m_Return.onClick.RemoveListener(OnReturnClick);
            }

            base.Close();
        }

        #endregion
    }

}

