using Cysharp.Threading.Tasks;
#if MANAGER_SERVER
using GameFramework.Event;
#else
using System;
# endif
using GameMain;
using System.Collections.Generic;
using System.Threading.Tasks;
using UGFExtensions.Await;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

namespace GameLogic.Supermarket
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
        private Dictionary<int, bool> m_TaskStatistics = new Dictionary<int, bool>();

        /// <summary>
        /// 初始化任务系统
        /// </summary>
        public void Init()
        {
            // 初始化任务开始时间
            m_StartTime = Time.time;
        }

        private void OnTrialReady()
        {
            // 发送请求获取任务列表
            RequestTasksList();
        }

        #endregion

        [Header("Tasks List")]
        [Tooltip("任务项预制体")]
        public GameObject taskShowerPrefab; // 任务项预制体

        [Tooltip("任务列表父物体")]
        public Transform content; // Content（任务列表父物体）

        #region 列表

        // 通过方法动态加载任务数据
        private void OnTaskListReceived(List<TaskVo> taskVoList, object userData = null)
        {
            if (userData == null || userData is not string || !(userData as string).Equals(GetType().FullName))
            {
                // 输出日志
                Log.Warning("⚠ 任务列表更新，但不是当前界面触发的，忽略。");
                return;
            }
            if (taskVoList == null || taskVoList.Count == 0)
            {
                Debug.LogWarning("⚠ 任务列表为空，可能已经没有要做的任务了。");
            }
            _ = UpdateTaskList(taskVoList);
        }

        /// <summary>
        /// 更新任务列表
        /// </summary>
        public async Task UpdateTaskList(List<TaskVo> taskVoList)
        {
            // 清空旧任务
            ClearOldTasks();
            //
            var isHasTask = taskVoList.Count > 0;
            var isAllTasksCompleted = isHasTask;
            // 创建新任务
            foreach (var taskVo in taskVoList)
            {
                var taskShower = await CreateTaskItem(taskVo);
                // 
                if (taskShower == null)
                {
                    // 输出日志
                    Debug.LogWarning($"⚠ 任务{taskVo.id}创建失败，请检查任务配置。");
                    return;
                }
                // 
                if (isAllTasksCompleted)
                {
                    isAllTasksCompleted = taskShower.IsCompleted;
                }
            }
#if !MANAGER_SERVER
            // 检查任务是否全部完成
            SwitchButton(isHasTask, isAllTasksCompleted);
#endif
        }

        private void RequestTasksList()
        {
            GameEvent.EventMgr.GetInterface<ITaskLogicEvent>().GetTaskList(-1, ETaskStatus.UnCompleted, true, GetType().FullName);
        }

        private void OnTaskListUpdate()
        {
            // 任务系统告诉我们任务列表更新了，我们重新获取任务列表
            RequestTasksList();
        }

        #endregion

        #region 切换结果界面

        /// <summary>
        /// 切换结果
        /// </summary>
        private async Task GoToResult()
        {
            // 准备数据
            // 消耗时间，用00:00:00表示，基于当前时间与m_StartTime计算。
            var caosTime = Time.time - m_StartTime;
            var hour = (int)caosTime / 3600;
            var minute = (int)(caosTime % 3600) / 60;
            var second = (int)(caosTime % 3600) % 60;
            var timeStr = $"{hour:D2}:{minute:D2}:{second:D2}";
            // 统计任务数量
            var completedTasksQty = 0;
            foreach (var taskVo in m_TaskStatistics)
            {
                if (taskVo.Value)
                {
                    completedTasksQty++;
                }
            }
            // 装载数据
            var data = new Dictionary<string, object>
                {
                    { "CostTime", timeStr },
                    { "CompletedTasksQty", completedTasksQty},
                    { "TotalTasksQty", m_TaskStatistics.Count},
                };
#if MANAGER_SERVER
            // 打开任务结算界面
            await GameModule.UI.OpenUIFormAsync("Assets/AssetRaw/UI/Server/TaskS/SupermarketTasksResultForm.prefab", "UI", 0, false, data);
#else
            // 同步管理端
            GameModule.Event.Fire(NetSyncStateWillChangeEventArgs.EventId, NetSyncStateWillChangeEventArgs.Create("GoToResult", null));
            // 打开任务结算界面
            await GameModule.UI.OpenUIFormAsync("Assets/AssetRaw/UI/Client/TaskC/SupermarketTasksResultForm.prefab", "3DUIInHand", 0, false, data);
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

        #region 任务操作

        [Header("Task Operation")]
        [Tooltip("放弃任务")]
        [SerializeField]
        private Button m_GiveUpButton;

        /// <summary>
        /// 是否为操作更新
        /// </summary>
        private bool m_IsOperatorUpdate = false;

#if !MANAGER_SERVER
        /// <summary>
        /// 放弃任务
        /// </summary>
        private void OnGiveUpTaskClick()
        {
            // 先禁用按钮，避免重复点击
            m_GiveUpButton.interactable = false;
            // 放弃未完成的任务
            foreach (Transform child in content)
            {
                var taskShower = child.GetComponent<TaskShowerController>();
                if (!taskShower.IsCompleted)
                {
                    GameEvent.EventMgr.GetInterface<ITaskLogicEvent>().GiveUp(taskShower.m_TaskID);
                }
                // 归档任务
                GameEvent.EventMgr.GetInterface<ITaskLogicEvent>().Archive(taskShower.m_TaskID);
            }
            // 
            _ = RefreshTaskList(
                // 刷新后重新启用按钮
                () => m_GiveUpButton.interactable = true
                );
        }
#endif

        [Tooltip("完成任务")]
        [SerializeField]
        private Button m_CompletedButton;

#if !MANAGER_SERVER
        /// <summary>
        /// 完成任务
        /// </summary>
        private void OnCompletedTaskClick()
        {
            // 先禁用按钮，避免重复点击
            m_CompletedButton.interactable = false;
            // 归档所有的任务
            ArchiveAllTasks();
            // 
            _ = RefreshTaskList(
                // 刷新后重新启用按钮
                () => m_CompletedButton.interactable = true
                );
        }

        /// <summary>
        /// 刷新任务列表
        /// </summary>
        /// <returns></returns>
        private async Task RefreshTaskList(Action onRefreshed = null)
        {
            // TODO 临时：等服务端处理完再刷新，这里可以优化，万一服务端处理慢了，这里就获取的就不是最新数据了。
            await UniTask.Delay(1000);
            // 清空购物篮
            GameEvent.EventMgr.GetInterface<IActorLogicEvent>().ClearShoppingBasket();
            // 获取下一个批任务
            RequestTasksList();

            // 标记为操作更新
            m_IsOperatorUpdate = true;
            // 
            onRefreshed?.Invoke();
        }

        /// <summary>
        /// 切换按钮
        /// </summary>
        /// <param name="isHasTask"></param>
        /// <param name="isAllCompleted"></param>
        private void SwitchButton(bool isHasTask, bool isAllCompleted)
        {
            m_CompletedButton.interactable = isHasTask;
            m_CompletedButton.gameObject.SetActive(isAllCompleted);
            m_GiveUpButton.interactable = isHasTask;
            m_GiveUpButton.gameObject.SetActive(!isAllCompleted);
        }

#endif

        /// <summary>
        /// 归档当前任务列表任务
        /// </summary>
        private void ArchiveAllTasks()
        {
            foreach (Transform child in content)
            {
                var taskShower = child.GetComponent<TaskShowerController>();
                GameEvent.EventMgr.GetInterface<ITaskLogicEvent>().Archive(taskShower.m_TaskID);
            }
        }

#if DEBUG_TASK_LIST_FORM
        /// <summary>
        /// 放弃当前任务列表任务
        /// </summary>
        private void GiveUpAllTasks()
        {
            foreach (Transform child in content)
            {
                var taskShower = child.GetComponent<TaskShowerController>();
                GameEvent.EventMgr.GetInterface<ITaskLogicEvent>().GiveUp(taskShower.m_TaskID);
            }
        }
#endif

        #endregion

        #region 任务

        /// <summary>
        /// 创建单个任务项
        /// </summary>
        private async Task<TaskShowerController> CreateTaskItem(TaskVo task)
        {
            // TODO: 希望在服务端生成
            // 生成任务项
            GameObject newTask = Instantiate(taskShowerPrefab, content.transform);
            //#if !MANAGER_SERVER
            //            // 同步在服务端生成
            //            GameModule.Event.Fire(CreateNetObjEventArgs.EventId, CreateNetObjEventArgs.Create(newTask));
            //#endif      
            // 将对象名称设置为任务ID
            newTask.name = task.id.ToString();
            // 获取任务项
            await UpdateTaskItemInfo(newTask.GetComponent<TaskShowerController>(), task);
            // 
            if (newTask != null)
            {
                return newTask.GetComponent<TaskShowerController>();
            }
            return null;
        }

        /// <summary>
        /// 任务更新
        /// </summary>
        private void OnTaskUpdate(TaskVo task)
        {
            _ = UpdateTaskAsync(task);
        }

        private async Task UpdateTaskAsync(TaskVo task)
        {
            var isFound = false;
            //
            var isHasTask = content.childCount > 0;
            var isAllTasksCompleted = isHasTask;
#if MANAGER_SERVER
            // 是否已无任务可完成，即所有任务，要么已经完成，要么已放弃，要么已归档
            // TODO 先简化为已归档就行
            var isNotTaskCanComplete = true;
#endif
            // 查找列表中匹配的任务项
            foreach (Transform child in content)
            {
                var taskShower = child.GetComponent<TaskShowerController>();
                if (taskShower.m_TaskID == task.id)
                {
                    isFound = true;
                    await UpdateTaskItemInfo(taskShower, task);
                    // 检查剩余任务都完成了
                }
                // 检查任务都完成了
                if (isAllTasksCompleted)
                {
                    isAllTasksCompleted = taskShower.IsCompleted;
                }
#if MANAGER_SERVER
                // 检查任务都归档了
                if (isNotTaskCanComplete)
                {
                    isNotTaskCanComplete = (taskShower.Status & ETaskStatus.Archived) > 0;
                }
#endif
            }

            if (!isFound)
            {
                // 输出日志
                Debug.LogWarning($"任务更新，但未找到匹配任务(ID:{task.id})的任务项");
                return;
            }
#if MANAGER_SERVER
            // 如已无任务可完成
            if (isNotTaskCanComplete)
            {
                // 输出日志
                Debug.LogWarning($"任务更新，但已无任务可完成");
                // 自动获取下一个批任务
                RequestTasksList();
            }
#else
            // 检查任务是否全部完成
            SwitchButton(isHasTask, isAllTasksCompleted);
#endif

        }

        /// <summary>
        /// 更新任务项信息
        /// </summary>
        private async Task UpdateTaskItemInfo(TaskShowerController taskShower, TaskVo task)
        {
            taskShower.m_TaskID = task.id;
            // 更新所有任务项
            await taskShower.SetImage(task.imageUrl); // 加载任务图片
            taskShower.SetProgress(task.progress, task.target); // 设置进度
            taskShower.SetCompletedStatus((task.Status & ETaskStatus.Completed) > 0); // 设置是否完成
            // 设置任务状态
            taskShower.Status = task.Status;

            // 更新统计
            // 如有就更新，没有就添加
            if (m_TaskStatistics.ContainsKey(task.id))
            {
                m_TaskStatistics[task.id] = taskShower.IsCompleted;
            }
            else
            {
                m_TaskStatistics.Add(task.id, taskShower.IsCompleted);
            }
        }

        /// <summary>
        /// 清空旧任务
        /// </summary>
        private void ClearOldTasks()
        {
            foreach (Transform child in content)
            {
                Destroy(child.gameObject);
            }
        }

#endregion

        #region 视角修正

        [Header("View Adjustment")]
        [SerializeField]
        private Button m_ViewAdjustmentButton;

        /// <summary>
        /// 视角修正
        /// </summary>
        private void OnViewAdjustmentClick()
        {
            GameEvent.EventMgr.GetInterface<IActorLogicEvent>().FixViewByMotionCapture();
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
            // 监听结算完成事件
            GameEvent.AddEventListener(IActorLogicEvent_Event.OnSettlementCompleted, OnSettlementCompleted);
#if MANAGER_SERVER
            // 监听网络同步状态改变后事件
            GameModule.Event.Subscribe(NetSyncStateChangedEventArgs.EventId, OnNetSyncStateChanged);
#else
            //
            m_GiveUpButton.onClick.AddListener(OnGiveUpTaskClick);
            m_ViewAdjustmentButton.onClick.AddListener(OnViewAdjustmentClick);
            m_CompletedButton.onClick.AddListener(OnCompletedTaskClick);
#endif

            // 初始化任务
            Init();
        }

        /// <summary>
        /// 结算完成后跳转到结果界面
        /// </summary>
        private void OnSettlementCompleted()
        {
            _ = GoToResult();
        }

#if DEBUG_TASK_LIST_FORM
        public override void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
            base.OnUpdate(elapseSeconds, realElapseSeconds);

#if !MANAGER_SERVER
            // 测试完成任务
            if (Input.GetKeyDown(KeyCode.C))
            {
                OnCompletedTaskClick();
                // 输出日志
                Debug.Log("[测试]完成任务");
            }
            // 测试放弃任务
            if (Input.GetKeyDown(KeyCode.G))
            {
                OnGiveUpTaskClick();
                // 输出日志
                Debug.Log("[测试]放弃任务");
            }
#endif
            // 测试获取任务
            if (Input.GetKeyDown(KeyCode.R))
            {
                RequestTasksList();
                // 输出日志
                Debug.Log("[测试]获取任务列表");
            }
            // 测试归档所有任务
            if (Input.GetKeyDown(KeyCode.A))
            {
                GiveUpAllTasks();
                ArchiveAllTasks();
                // 输出日志
                Debug.Log("[测试]放弃并归档所有任务");
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
            // 移除结算完成事件监听
            GameEvent.RemoveEventListener(IActorLogicEvent_Event.OnSettlementCompleted, OnSettlementCompleted);
#if MANAGER_SERVER
            // 移除网络同步状态改变后事件监听
            GameModule.Event.Unsubscribe(NetSyncStateChangedEventArgs.EventId, OnNetSyncStateChanged);
#else

            //
            m_GiveUpButton.onClick.RemoveListener(OnGiveUpTaskClick);
            m_ViewAdjustmentButton.onClick.RemoveListener(OnViewAdjustmentClick);
            m_CompletedButton.onClick.RemoveListener(OnCompletedTaskClick);
#endif
            // 清空任务列表
            ClearOldTasks();
            // 
            base.OnClose(isShutdown, userData);
        }

        #endregion
    }

}

