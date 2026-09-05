using GameBase;
using Mirror;
using System.Collections.Generic;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace GameLogic
{
    /// <summary>
    /// 任务系统。
    /// </summary>
    public class TaskSystem : BaseLogicSys<TaskSystem>
    {
        #region 同步

        /// <summary>
        /// 同步任务列表。
        /// </summary>
        private TaskList m_SyncTaskList;

        /// <summary>
        /// 尝试绑定 Mirror 生成的同步任务列表。
        /// TaskList 可能晚于 OnTrialReady 到达客户端，绑定成功后
        /// 需主动通知 UI 重新查询，不能依赖已经错过的 SyncList 初始同步回调。
        /// </summary>
        private bool TryBindSyncTaskList(bool notifyTaskListUpdate)
        {
            if (m_SyncTaskList != null)
            {
                return true;
            }

            m_SyncTaskList = GameObject.FindFirstObjectByType<TaskList>();
            if (m_SyncTaskList == null)
            {
                return false;
            }

            m_SyncTaskList.m_TaskList.Callback -= OnTasksListChange;
            m_SyncTaskList.m_TaskList.Callback += OnTasksListChange;
            Log.Info($"可同步任务列表已找到，当前任务数：{m_SyncTaskList.m_TaskList.Count}。");

            if (notifyTaskListUpdate)
            {
                GameEvent.EventMgr.GetInterface<ITaskLogicEvent>().OnTaskListUpdate();
            }

            return true;
        }

        private void OnTasksListChange(SyncList<TaskVo>.Operation operation, int index, TaskVo oldTask, TaskVo newTask)
        {
            // 输出日志
            Log.Info($"同步任务列表发生变化，操作：{operation}，索引：{index}，任务：{oldTask.id} -> {newTask.id}");
            // 根据操作类型处理任务列表变化
            switch (operation)
            {
                case SyncList<TaskVo>.Operation.OP_ADD:
                case SyncList<TaskVo>.Operation.OP_REMOVEAT:
                    // 发送任务列表更新事件
                    GameEvent.EventMgr.GetInterface<ITaskLogicEvent>().OnTaskListUpdate();
                    break;
                case SyncList<TaskVo>.Operation.OP_SET:
                    // 发送任务更新事件
                    GameEvent.EventMgr.GetInterface<ITaskLogicEvent>().OnTaskUpdate(newTask);
                    break;
            }
        }

        #endregion

        #region 添加任务

        public void Add(TaskVo task)
        {
            if (!TryBindSyncTaskList(false))
            {
                Log.Error($"添加任务失败：可同步任务列表尚未生成，任务ID：{task.id}。");
                return;
            }

            // 输出日志
            Log.Info("添加任务，任务ID：{0}", task.id);
            m_SyncTaskList.Add(task);
        }

        public void AddList(List<TaskVo> list)
        {
            if (list == null)
            {
                Log.Error("添加任务列表失败：任务数据为 null。");
                return;
            }

            if (!TryBindSyncTaskList(false))
            {
                Log.Error($"添加任务列表失败：可同步任务列表尚未生成，任务数量：{list.Count}。");
                return;
            }

            // 输出日志
            Log.Info("添加任务列表，任务数量：{0}", list.Count);
            m_SyncTaskList.AddList(list);
        }

        #endregion

        #region 删除任务

        public void Remove(int id)
        {
            m_SyncTaskList.Remove(id);
        }

        public void Release()
        {
            if (m_SyncTaskList == null)
            {
                Log.Warning("可同步任务列表不存在或已被销毁，跳过清空任务列表。");
                return;
            }

            m_SyncTaskList.Clear();
        }

        #endregion

        #region 修改任务

        /// <summary>
        /// 更新任务进度
        /// </summary>
        /// <param name="id"></param>
        /// <param name="add_progress"></param>
        public void UpdateProgress(int id, int add_progress)
        {
            // 输出日志
            Log.Info("更新任务进度，任务ID：{0}，追加进度：{1}", id, add_progress);
            m_SyncTaskList.UpdateProgress(id, add_progress);
        }

        /// <summary>
        /// 更新任务
        /// </summary>
        /// <param name="proId">商品ID</param>
        /// <param name="add_progress">追加进度</param>
        private void UpdateTaskProgressByProduct(int proId, int add_progress)
        {
            // 输出日志
            Log.Info("更新任务进度，商品ID：{0}，追加进度：{1}", proId, add_progress);
            m_SyncTaskList.UpdateTaskProgressByTargetObj(proId, add_progress);
        }

        /// <summary>
        /// 增加任务积分
        /// </summary>
        /// <param name="score"></param>
        private void AddCurTaskScore(int score)
        {
            // 输出日志
            Log.Info("更新当前任务积分：{0}", score);
            m_SyncTaskList.AddCurTaskScore(score);
        }

        /// <summary>
        /// 放弃任务
        /// </summary>
        /// <param name="id"></param>
        private void OnGiveUp(int id)
        {
            m_SyncTaskList.OnGiveUp(id);
        }

        /// <summary>
        /// 归档任务
        /// </summary>
        /// <param name="id"></param>
        private void OnArchive(int id)
        {
            m_SyncTaskList.OnArchive(id);
        }

        #endregion

        #region 查询

        /// <summary>
        /// 获取任务列表
        /// </summary>
        /// <param name="status">获取的任务状态</param>
        /// <param name="qty">获取数量，非正数表示全部获取。
        /// <br/>如果<paramref name="isOnlySameGroup"/>为true，则可能无法返回足够数量的任务，因与第一个任务同一组的剩余任务数量可能不足</param>
        /// <param name="isOnlySameGroup">是否只获取同组的任务</param>
        /// <returns></returns>
        public List<TaskVo> GetTaskList(ETaskStatus status, int qty, bool isOnlySameGroup)
        {
            if (!TryBindSyncTaskList(false))
            {
                Log.Warning("可同步任务列表还未找到，无法获取列表数据。");
                return null;
            }
            // 当任务列表中的任务状态为传入的任务状态中的任意一种时，返回该任务。
            // 按位处理，比如：status = ETaskStatus.Abandoned | ETaskStatus.Completed，则返回任务状态为Abandoned或Completed的任务都返回。
            // 如果qty为正数，则返回qty个任务，否则返回所有任务。
            List<TaskVo> taskList = new(qty > 0 ? qty : m_SyncTaskList.m_TaskList.Count);
            foreach (var itor in m_SyncTaskList.m_TaskList)
            {
                // 似乎归档与放弃的任务不应该被获取
                if ((itor.Status & status) != 0)
                {
                    // 如果isOnlySameGroup为true，则只获取同组的任务
                    if (!isOnlySameGroup || taskList.Count == 0 || taskList[0].GroupId == itor.GroupId)
                    {
                        taskList.Add(itor);
                    }
                    // 如果数量已经达到，则退出循环
                    if (qty > 0 && taskList.Count == qty)
                    {
                        break;
                    }
                }
            }

            // 
            return taskList;
        }

        private void OnGetTask(int id)
        {
            var vo = m_SyncTaskList.GetTaskVo(id);
            if (vo == null)
            {
                Log.Warning($"未找到任务数据,id={id}");
                return;
            }
            GameEvent.EventMgr.GetInterface<ITaskLogicEvent>().OnGetTaskReturn(vo.Value);
        }

        private void OnGetTaskList(int qty, ETaskStatus status, bool isAllisOnlySameGroup, object userData)
        {
            var list = GetTaskList(status, qty, isAllisOnlySameGroup);
            if (list == null)
            {
                Log.Warning("查询任务数据列表失败。");
                return;
            }
            GameEvent.EventMgr.GetInterface<ITaskLogicEvent>().OnGetTaskListReturn(list, userData);
        }

        #endregion

        #region 生命周期

        override public void OnStart()
        {
            GameEvent.AddEventListener<TaskVo>(ITaskLogicEvent_Event.Add, Add);
            GameEvent.AddEventListener<List<TaskVo>>(ITaskLogicEvent_Event.AddList, AddList);
            GameEvent.AddEventListener<int, int>(ITaskLogicEvent_Event.UpdateProgress, UpdateProgress);
            GameEvent.AddEventListener<int, int>(ITaskLogicEvent_Event.UpdateTaskProgressByProduct, UpdateTaskProgressByProduct);
            GameEvent.AddEventListener<int>(ITaskLogicEvent_Event.AddCurTaskScore, AddCurTaskScore);
            GameEvent.AddEventListener<int>(ITaskLogicEvent_Event.Remove, Remove);
            GameEvent.AddEventListener<int>(ITaskLogicEvent_Event.GetTask, OnGetTask);
            GameEvent.AddEventListener<int, ETaskStatus, bool, object>(ITaskLogicEvent_Event.GetTaskList, OnGetTaskList);
            GameEvent.AddEventListener(ITaskLogicEvent_Event.Release, Release);
            GameEvent.AddEventListener<int>(ITaskLogicEvent_Event.GiveUp, OnGiveUp);
            GameEvent.AddEventListener<int>(ITaskLogicEvent_Event.Archive, OnArchive);
            // 
            TryBindSyncTaskList(false);
        }

        public override void OnUpdate()
        {
            TryBindSyncTaskList(true);
        }

        public override void OnDestroy()
        {
            GameEvent.RemoveEventListener<TaskVo>(ITaskLogicEvent_Event.Add, Add);
            GameEvent.RemoveEventListener<List<TaskVo>>(ITaskLogicEvent_Event.AddList, AddList);
            GameEvent.RemoveEventListener<int, int>(ITaskLogicEvent_Event.UpdateProgress, UpdateProgress);
            GameEvent.RemoveEventListener<int, int>(ITaskLogicEvent_Event.UpdateTaskProgressByProduct, UpdateTaskProgressByProduct);
            GameEvent.RemoveEventListener<int>(ITaskLogicEvent_Event.AddCurTaskScore, AddCurTaskScore);
            GameEvent.RemoveEventListener<int>(ITaskLogicEvent_Event.Remove, Remove);
            GameEvent.RemoveEventListener<int>(ITaskLogicEvent_Event.GetTask, OnGetTask);
            GameEvent.RemoveEventListener<int, ETaskStatus, bool, object>(ITaskLogicEvent_Event.GetTaskList, OnGetTaskList);
            GameEvent.RemoveEventListener(ITaskLogicEvent_Event.Release, Release);
            GameEvent.RemoveEventListener<int>(ITaskLogicEvent_Event.GiveUp, OnGiveUp);
            GameEvent.RemoveEventListener<int>(ITaskLogicEvent_Event.Archive, OnArchive);

            // Mirror may destroy the spawned TaskList before GameApp tears down its
            // logic systems. Do not send the Clear command during destruction: the
            // network object may already be invalid and its SyncList is discarded
            // together with the object anyway.
            if (m_SyncTaskList != null)
            {
                m_SyncTaskList.m_TaskList.Callback -= OnTasksListChange;
            }

            m_SyncTaskList = null;
        }

        #endregion
    }

}
