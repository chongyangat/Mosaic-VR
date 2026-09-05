using Cysharp.Threading.Tasks;
#if MANAGER_SERVER
using GameFramework.Event;
# endif
using GameMain;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UGFExtensions.Await;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace GameLogic.Street
{
    public class TaskListForm : UIFormLogic
    {
        #region 公共

        /// <summary>
        /// 开始时间
        /// </summary>
        private float m_StartTime;

        /// <summary>
        /// 任务统计
        /// </summary>
        private readonly Dictionary<int, TaskStaticticsData> m_TaskStatistics = new();

        /// <summary>
        /// 初始化任务系统
        /// </summary>
        public void Init()
        {
            // 初始化任务开始时间
            m_StartTime = Time.time;
        }

        /// <summary>
        /// 计算统计数据
        /// </summary>
        private (int, int) GetStatistics()
        {
            // 统计任务数量
            var completedTasksQty = 0;
            var hitTimesOnObstacles = 0;
            foreach (var taskVo in m_TaskStatistics)
            {
                if (taskVo.Value.IsCompleted)
                {
                    completedTasksQty++;
                }
                hitTimesOnObstacles += taskVo.Value.HitTimesOnObstacles;
            }
            return (completedTasksQty, hitTimesOnObstacles);
        }

        #endregion

        [Tooltip("进度显示")]
        public TextMeshProUGUI m_ProgressShower;

        private void UpdateProgress()
        {
#if MANAGER_SERVER
            // 更新进度显示
            m_ProgressShower.text = $"{GetStatistics().Item1}/{m_TaskStatistics.Count}";
#endif
        }

        /// <summary>
        /// 当前接收的任务列表
        /// </summary>
        private readonly List<TaskVo> m_CurTasksList = new();

        /// <summary>
        /// 是否已经收到过服务端的非空任务数据。
        /// 首次空列表通常表示 Mirror 尚未完成同步，不代表任务已完成。
        /// </summary>
#if !MANAGER_SERVER
        private bool m_HasReceivedTaskData;
#endif

        /// <summary>
        /// 防止多个列表更新同时重复打开任务结算界面。
        /// </summary>
        private bool m_IsOpeningResult;

        private void OnTrialReady()
        {
            // 发送请求获取任务列表
            RequestTasksList();
        }

        #region 列表

        private void OnTaskListReceived(List<TaskVo> taskVoList, object userData)
        {
            if (userData == null || userData is not string || !(userData as string).Equals(GetType().FullName))
            {
                // 输出日志
                Log.Warning("⚠ 任务列表更新，但不是当前界面触发的，忽略。");
                return;
            }
            if (taskVoList == null)
            {
                Log.Warning("⚠ 任务列表查询返回 null，等待 Mirror 任务对象完成同步。");
                return;
            }

            if (taskVoList.Count == 0)
            {
#if !MANAGER_SERVER
                if (m_HasReceivedTaskData)
                {
                    Log.Info("任务列表已从非空变为空，确认 Street 任务已全部完成。");
                    _ = GoToResult();
                }
                else
                {
                    Log.Warning("⚠ 首次 Street 任务查询为空，视为同步尚未完成，不进入任务结算。");
                }
#endif
                return;
            }

#if !MANAGER_SERVER
            m_HasReceivedTaskData = true;
#endif
            // 清空旧任务
            ClearOldTasks();
            //
            var isHasTask = taskVoList.Count > 0;
            var isAllTasksCompleted = isHasTask;
            // 创建新任务
            foreach (var task in taskVoList)
            {
                m_CurTasksList.Add(task);
                // 
                if (isAllTasksCompleted)
                {
                    isAllTasksCompleted = (task.Status & ETaskStatus.Completed) > 0;
                }
            }
            // 更新任务进度
            UpdateTaskProgress(taskVoList);
        }

        /// <summary>
        /// 更新任务进度
        /// </summary>
        public void UpdateTaskProgress(List<TaskVo> taskVoList)
        {
            // 检查任务是否全部完成
            for (int index = 0; index < taskVoList.Count; index++)
            {
                TaskVo task = taskVoList[index];
                UpdateTaskItemInfo(index, task);
            }
            // 更新进度显示
            UpdateProgress();
        }

        private void RequestTasksList()
        {
            GameEvent.EventMgr.GetInterface<ITaskLogicEvent>().GetTaskList(-1, ETaskStatus.UnCompleted, false, GetType().FullName);
        }

        private void OnTaskListUpdate()
        {
            // 任务系统告诉我们任务列表更新了，我们重新获取任务列表
            RequestTasksList();
        }

        /// <summary>
        /// 清空旧任务
        /// </summary>
        private void ClearOldTasks()
        {
            m_CurTasksList.Clear();
        }

        #endregion

        #region 切换结果界面

        /// <summary>
        /// 切换结果
        /// </summary>
        private async Task GoToResult()
        {
            if (m_IsOpeningResult)
            {
                return;
            }

            m_IsOpeningResult = true;
            // 准备数据
            // 消耗时间，用00:00:00表示，基于当前时间与m_StartTime计算。
            var caosTime = Time.time - m_StartTime;
            var hour = (int)caosTime / 3600;
            var minute = (int)(caosTime % 3600) / 60;
            var second = (int)(caosTime % 3600) % 60;
            var timeStr = $"{hour:D2}:{minute:D2}:{second:D2}";
            // 计算统计数据
            var statisticsData = GetStatistics();
            // 装载数据
            var data = new Dictionary<string, object>
                {
                    { "CostTime", timeStr },
                    { "CompletedTasksQty", statisticsData.Item1},
                    { "TotalTasksQty", m_TaskStatistics.Count},
                    { "HitTimesOnObstacles", statisticsData.Item2},
                };
#if MANAGER_SERVER
            // 打开任务结算界面
            await GameModule.UI.OpenUIFormAsync("Assets/AssetRaw/UI/Server/TaskS/StreetTasksResultForm.prefab", "UI", 0, false, data);
#else
            // 同步管理端
            GameModule.Event.Fire(NetSyncStateWillChangeEventArgs.EventId, NetSyncStateWillChangeEventArgs.Create("GoToResult", null));
            // 打开任务结算界面
            await GameModule.UI.OpenUIFormAsync("Assets/AssetRaw/UI/Client/TaskC/StreetTasksResultForm.prefab", "3DUIInHand", 0, false, data);
#endif
            // 关闭任务列表界面
            GameModule.UI.CloseUIForm(GetComponent<UIForm>());
        }

        #endregion

#if MANAGER_SERVER
        #region Mirror

        /// <summary>
        /// 网络同步状态改变后
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnNetSyncStateChanged(object sender, GameEventArgs e)
        {
            var ne = (NetSyncStateChangedEventArgs)e;
            if (ne == null)
            {
                return;
            }
            if (ne.StateName != "GoToResult")
            {
                return;
            }
            // 
            _ = GoToResult();
        }

        #endregion  
#endif

        #region 任务


        /// <summary>
        /// 任务更新
        /// </summary>
        private void OnTaskUpdate(TaskVo task)
        {
            var isFound = false;
            //
            var isHasTask = m_CurTasksList.Count > 0;
            var isAllTasksCompleted = isHasTask;
            // 是否已无任务可完成，即所有任务，要么已经完成，要么已放弃，要么已归档
            // TODO 先简化为已归档就行
            var isNotTaskCanComplete = true;
            // 查找列表中匹配的任务项
            for (int index = 0; index < m_CurTasksList.Count; index++)
            {
                TaskVo curTask = m_CurTasksList[index];
                if (curTask.id == task.id)
                {
                    isFound = true;
                    UpdateTaskItemInfo(index, task);
                    curTask = task;
                    // 检查剩余任务都完成了
                }
                // 检查任务都完成了
                if (isAllTasksCompleted)
                {
                    isAllTasksCompleted = (curTask.Status & ETaskStatus.Completed) > 0;
                }
                // 检查任务都归档了
                if (isNotTaskCanComplete)
                {
                    isNotTaskCanComplete = (curTask.Status & ETaskStatus.Completed) > 0;
                }
            }

            if (!isFound)
            {
                // 输出日志
                Debug.LogWarning($"任务更新，但未找到匹配任务(ID:{task.id})的任务项");
                return;
            }
            // 更新进度显示
            UpdateProgress();
            // 如已无任务可完成
            if (isNotTaskCanComplete)
            {
                // 输出日志
                Debug.LogWarning($"任务更新，但已无任务可完成");
                // 自动获取下一个批任务
                RequestTasksList();
            }
        }

        /// <summary>
        /// 更新任务项信息
        /// </summary>
        private void UpdateTaskItemInfo(int index, TaskVo task)
        {
            // 更新任务项信息
            m_CurTasksList[index] = task;
            // 更新统计
            var isCompleted = (task.Status & ETaskStatus.Completed) > 0;
            // 如有就更新，没有就添加
            if (!m_TaskStatistics.ContainsKey(task.id))
            {
                m_TaskStatistics.Add(task.id, new TaskStaticticsData());
            }
            var taskInStatistics = m_TaskStatistics[task.id];
            taskInStatistics.IsCompleted = isCompleted;
            // 碰撞时是减分，所以取绝对值。
            taskInStatistics.HitTimesOnObstacles = Mathf.Abs(task.Score);
        }

        #endregion

        #region 生命期

        protected override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            // 
            // 监听任务列表更新事件
            GameEvent.AddEventListener<List<TaskVo>, object>(ITaskLogicEvent_Event.OnGetTaskListReturn, OnTaskListReceived);
            // 监听任务列表更新事件
            GameEvent.AddEventListener(ITaskLogicEvent_Event.OnTaskListUpdate, OnTaskListUpdate);
            // 监听任务更新事件
            GameEvent.AddEventListener<TaskVo>(ITaskLogicEvent_Event.OnTaskUpdate, OnTaskUpdate);
            // 监听试验就绪事件
            GameEvent.AddEventListener(IActorLogicEvent_Event.OnTrialReady, OnTrialReady);
#if MANAGER_SERVER
            // 监听网络同步状态改变后事件
            GameModule.Event.Subscribe(NetSyncStateChangedEventArgs.EventId, OnNetSyncStateChanged);
#endif

            // 初始化任务
            Init();
#if !MANAGER_SERVER
            m_HasReceivedTaskData = false;
#endif
            m_IsOpeningResult = false;
        }

#if DEBUG_TASK_LIST_FORM
        public override void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
            base.OnUpdate(elapseSeconds, realElapseSeconds);

            // 测试获取任务
            if (Input.GetKeyDown(KeyCode.R))
            {
                RequestTasksList();
                // 输出日志
                Debug.Log("[测试]获取任务列表");
            }
            // 测试跳转到任务结算界面
            if (Input.GetKeyDown(KeyCode.T))
            {
                _ = GoToResult();
            }
        }
#endif

        public override void OnClose(bool isShutdown, object userData)
        {
            // 移除任务列表更新事件监听
            GameEvent.RemoveEventListener<List<TaskVo>, object>(ITaskLogicEvent_Event.OnGetTaskListReturn, OnTaskListReceived);
            // 移除任务列表更新事件监听
            GameEvent.RemoveEventListener(ITaskLogicEvent_Event.OnTaskListUpdate, OnTaskListUpdate);
            // 移除任务更新事件监听
            GameEvent.RemoveEventListener<TaskVo>(ITaskLogicEvent_Event.OnTaskUpdate, OnTaskUpdate);
            // 移除试验就绪事件监听
            GameEvent.RemoveEventListener(IActorLogicEvent_Event.OnTrialReady, OnTrialReady);
#if MANAGER_SERVER
            // 移除网络同步状态改变后事件监听
            GameModule.Event.Unsubscribe(NetSyncStateChangedEventArgs.EventId, OnNetSyncStateChanged);
#endif
            // 清空任务列表
            ClearOldTasks();
            // 
            base.OnClose(isShutdown, userData);
        }

        #endregion

        /// <summary>
        /// 任务统计数据
        /// </summary>
        private class TaskStaticticsData
        {
            public bool IsCompleted;

            public int HitTimesOnObstacles;

        }

    }

}

