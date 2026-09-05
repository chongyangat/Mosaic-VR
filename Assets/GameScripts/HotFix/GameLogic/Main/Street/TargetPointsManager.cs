using GameFramework.Event;
using GameMain;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace GameLogic
{
    public class TargetPointsManager : MonoBehaviour
    {
        #region 任务

        /// <summary>
        /// 是否激活了任意一个目标点
        /// </summary>
        //private bool m_IsActivatedAnyTargetPoint = true;

        private void OnTaskListReceived(List<TaskVo> tasksList, object userData)
        {
            if (userData == null || userData is not string || !(userData as string).Equals(GetType().FullName))
            {
                // 输出日志
                Log.Warning("⚠ 任务列表更新，但不是当前模块触发的，忽略。");
                return;
            }
            if (tasksList == null || tasksList.Count == 0)
            {
                Log.Warning("⚠ 任务列表为空，可能已经没有要做的任务了。");
                //#if !MANAGER_SERVER
                //                // 人为操作才有效任务列表为空，应该已经做完本次任务。
                //                if (m_IsActivatedAnyTargetPoint)
                //                {
                //                    // TODO 临时，通知任务列表模块为空，需要重新获取任务列表
                //                    GameEvent.EventMgr.GetInterface<ITaskLogicEvent>().GetTaskList(1, ETaskStatus.UnCompleted, false, "GameLogic.Street.TaskListForm");
                //                    m_IsActivatedAnyTargetPoint = false;
                //                    // 人为操作就不更新任务列表
                //                    return;
                //                }
                //#endif
                return;
            }
            // 是否激活目标点
            var isActivateTargetPoint = false;
            // 检查任务列表中是否有匹配的任务
            foreach (var task in tasksList)
            {
                // 如果任务状态不是未完成，则跳过
                if (task.Status != ETaskStatus.UnCompleted)
                {
                    continue;
                }
                // 如果任务是匹配的，则激活对应的目标点
                if (m_TargetPointsList.TryGetValue(task.name, out var targetPoint))
                {
                    // 设置任务ID
                    targetPoint.SetTaskId(task.id);
                    // 激活目标点
                    targetPoint.gameObject.SetActive(true);
                    // 输出日志
                    Log.Info("激活目标点: " + targetPoint.ToString());
                    // 
                    isActivateTargetPoint = true;
                    //m_IsActivatedAnyTargetPoint = true;
                }
            }
            // 如果没有匹配的任务，则输出
            if (!isActivateTargetPoint)
            {
                // 输出任务表中各项的名称
                var log = new StringBuilder();
                foreach (var task in tasksList)
                {
                    log.AppendLine(task.name);
                }
                
                // 输出日志
                Log.Warning($"没有匹配的任务（总任务：{tasksList.Count}），没有激活目标点：\n{log}");
            }
        }

        /// <summary>
        /// 获取下一批任务列表
        /// </summary>
        public void RequestNextTaskList()
        {
            GameEvent.EventMgr.GetInterface<ITaskLogicEvent>().GetTaskList(1, ETaskStatus.UnCompleted, false, GetType().FullName);
        }

        /// <summary>
        /// 任务更新
        /// </summary>
        private void OnTaskUpdate(TaskVo task)
        {
            // 如果任务是匹配的，则修改对应的目标点
            if (m_TargetPointsList.TryGetValue(task.name, out var matchedTargetPoint))
            {
                matchedTargetPoint.gameObject.SetActive(task.Status == ETaskStatus.UnCompleted);
                // 输出日志
                Log.Info("更新目标点: " + matchedTargetPoint.ToString() + "，状态: " + task.Status.ToString());
            }
            // 如果没有点位是激活状态，则请求下一批任务列表
            var isAllTargetPointUnactivated = true;
            foreach (var targetPoint in m_TargetPointsList.Values)
            {
                if (targetPoint.gameObject.activeSelf)
                {
                    isAllTargetPointUnactivated = false;
                    break;
                }
            }
            if (isAllTargetPointUnactivated)
            {
                // 输出日志
                Log.Info("所有目标点都未激活，请求下一批任务列表");
                RequestNextTaskList();
            }
        }

        #endregion

        #region 目标点

        /// <summary>
        /// 目标点列表
        /// </summary>
        private readonly Dictionary<string, TargetPoint> m_TargetPointsList = new();

        /// <summary>
        /// 初始化目标点
        /// </summary>
        private void InitTargetPoints()
        {
            // 从子对象中获取目标点
            foreach (Transform child in transform)
            {
                var targetPoint = child.GetComponent<TargetPoint>();
                m_TargetPointsList.Add(targetPoint.gameObject.name, targetPoint);
            }
        }

        #endregion

        private void OnTrialReady()
        {
            // 发送请求获取任务列表
            RequestNextTaskList();
        }

        /// <summary>
        /// 传送角色完成
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnTeleportPlayer(object sender, GameEventArgs e)
        {
            TeleportPlayerEventArgs ne = (TeleportPlayerEventArgs)e;
            if (ne == null)
            {
                return;
            }
            // 传送成功，发送请求获取下一批任务列表
            RequestNextTaskList();
        }

        /// <summary>
        /// 重置
        /// </summary>
        private void ResetSelf()
        {
            //m_IsActivatedAnyTargetPoint = false;
        }

        #region U3D

        private void Awake()
        {
            InitTargetPoints();
        }

        private void OnEnable()
        {
            // 监听任务列表更新事件
            GameEvent.AddEventListener<List<TaskVo>, object>(ITaskLogicEvent_Event.OnGetTaskListReturn, OnTaskListReceived);
            // 监听任务更新事件
            GameEvent.AddEventListener<TaskVo>(ITaskLogicEvent_Event.OnTaskUpdate, OnTaskUpdate);
            // 监听试验就绪事件
            GameEvent.AddEventListener(IActorLogicEvent_Event.OnTrialReady, OnTrialReady);
            // 监听传送事件
            GameModule.Event.Subscribe(TeleportPlayerEventArgs.EventId, OnTeleportPlayer);
        }

        private void OnDisable()
        {
            // 取消监听任务列表更新事件
            GameEvent.RemoveEventListener<List<TaskVo>, object>(ITaskLogicEvent_Event.OnGetTaskListReturn, OnTaskListReceived);
            // 移除任务更新事件监听
            GameEvent.RemoveEventListener<TaskVo>(ITaskLogicEvent_Event.OnTaskUpdate, OnTaskUpdate);
            // 取消监听试验就绪事件
            GameEvent.RemoveEventListener(IActorLogicEvent_Event.OnTrialReady, OnTrialReady);
            // 取消监听传送事件
            GameModule.Event.Unsubscribe(TeleportPlayerEventArgs.EventId, OnTeleportPlayer);
            // 重置
            ResetSelf();
        }

        #endregion
    }
}
