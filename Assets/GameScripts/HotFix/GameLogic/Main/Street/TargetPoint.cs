using System.Collections.Generic;
using GameMain;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace GameLogic
{
    public class TargetPoint : MonoBehaviour
    {
        #region 任务

        /// <summary>
        /// 任务点ID
        /// </summary>

        private int m_TaskId = -1;

        /// <summary>
        /// 设置任务点ID
        /// </summary>
        /// <param name="taskId"></param>
        public void SetTaskId(int taskId)
        {
            m_TaskId = taskId;
        }

        #endregion

        #region 触发

        [Tooltip("触发延时，单位：秒")]
        [SerializeField]
        private float m_TriggerDelay = 1.5f;

        /// <summary>
        /// 延时触发
        /// </summary>
        private void Triggle()
        {
            // 输出日志
            Log.Info($"任务点‘{ToString()}’触发。");
            // 告诉任务系统
            GameEvent.EventMgr.GetInterface<ITaskLogicEvent>().UpdateProgress(m_TaskId, 1);
            // 传送角色去下一个点
            TeleportPlayer();
            // 关闭当前点
            gameObject.SetActive(false);
        }

        #endregion

        #region 传送

        [Tooltip("传送点")]
        [SerializeField]
        private Transform m_TeleportPoint;

        [Tooltip("是否需要传送反转处理")]
        [SerializeField]
        private bool m_IsNeedInvert = false;

        /// <summary>
        /// 传送角色
        /// </summary>
        private void TeleportPlayer()
        {
            // 如有传送至下一个点
            if (m_TeleportPoint != null)
            {
                // 输出日志
                Log.Info($"任务点‘{ToString()}’触发传送至下一个点‘{m_TeleportPoint.gameObject.name}’。");
                // 传送
                // 配置传送参数
                var paramsData = new Dictionary<string, object>()
                {
                    { "IsNeedInvert", m_IsNeedInvert}
                };
                GameModule.Event.Fire(TeleportPlayerEventArgs.EventId,
                    TeleportPlayerEventArgs.Create(m_TeleportPoint.position, m_TeleportPoint.rotation, true, paramsData));
            }
            else
            {
                // 输出日志
                Log.Warning($"任务点‘{ToString()}’触发传送，但已无下一个传送点。");
                // 请求刷新任务
                // TODO 临时，需要解耦
                transform.GetComponentInParent<TargetPointsManager>().RequestNextTaskList();
            }
        }

        #endregion

        #region 延时进度条

        /// <summary>
        /// 延时进度条
        /// </summary>
        private TimeBar m_TimeBar;

        #endregion

        public override string ToString()
        {
            return $"Name:{gameObject.name}，TaskId:{m_TaskId})";
        }

        #region U3D

        private void Start()
        {
            m_TimeBar = transform.GetComponentInChildren<TimeBar>();
            if (m_TimeBar == null)
            {
                // 输出日志
                Log.Warning($"任务点‘{gameObject.name}’没有找到TimeBar组件。");
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // 仅玩家才能触发
            if (!other.CompareTag("Player"))
            {
                return;
            }
            // 输出日志
            Log.Info($"‘{other.gameObject.name}’进入目标‘{gameObject.name}({m_TaskId})’。");
            // 延时触发
            Invoke(nameof(Triggle), m_TriggerDelay);
            // 显示进度条
            if (m_TimeBar != null)
            {
                m_TimeBar.StartUpdate(m_TriggerDelay);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            // 仅玩家才能触发
            if (!other.CompareTag("Player"))
            {
                return;
            }
            // 输出日志
            Log.Info($"‘{other.gameObject.name}’离开目标‘{gameObject.name}({m_TaskId})’。");
            // 取消延时触发
            CancelInvoke(nameof(Triggle));
            // 隐藏进度条
            if (m_TimeBar != null)
            {
                m_TimeBar.StopUpdate();
            }
        }

        #endregion
    }
}
