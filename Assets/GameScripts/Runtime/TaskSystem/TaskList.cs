using Mirror;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace GameLogic
{
    /// <summary>
    /// 同步任务列表
    /// </summary>
    public class TaskList : NetworkBehaviour
    {
        /// <summary>
        /// TODO （可同步）任务列表（目前仅能放在AOT层，否则会无法找到NetworkWriter解析，时间关系，原因未知）
        /// </summary>
        public readonly SyncList<TaskVo> m_TaskList = new();

        #region 添加任务

        [Command(requiresAuthority = false)]
        public void Add(TaskVo task)
        {
            // 输出日志
            Log.Info($"添加任务:{task.id}");
            // 添加任务
            m_TaskList.Add(task);
        }

        [Command(requiresAuthority = false)]
        public void AddList(List<TaskVo> list)
        {
            // 输出日志
            Log.Info($"添加任务列表:{(list != null ? list.Count : string.Empty)}");
            // 添加任务列表
            m_TaskList.AddRange(list);
        }

        #endregion

        #region 删除任务

        [Command(requiresAuthority = false)]
        public void Remove(int id)
        {
            // 输出日志
            Log.Info($"删除任务,id={id}");
            // 根据id获取任务
            var index = m_TaskList.FindIndex(itor => itor.id == id);
            if (index < 0) return;
            m_TaskList.RemoveAt(index);
        }

        [Command(requiresAuthority = false)]
        public void Clear()
        {
            // 输出日志
            Log.Info("清空任务列表");
            m_TaskList.Clear();
        }

        #endregion

        #region 修改任务

        /// <summary>
        /// 获取任务
        /// </summary>
        /// <param name="id"></param>
        /// <param name="progressDelta">进度变化量</param>
        [Command(requiresAuthority = false)]
        public void UpdateProgress(int id, int progressDelta)
        {
            // 输出日志
            Log.Info($"更新任务进度,id={id},进度变化量={progressDelta}");
            // 根据id获取任务，要求任务状态不能是放弃或归档
            var task = GetTaskVo(id, true, task => { 
                task.UpdateProgress(progressDelta); 
                return task;
            });
            if (task == null)
            {
                Log.Warning($"未找到任务数据,id={id}");
                return;
            }
        }

        /// <summary>
        /// 更新任务
        /// </summary>
        /// <param name="targetId">目标物品ID</param>
        /// <param name="progressDelta">追加进度</param>
        [Command(requiresAuthority = false)]
        public void UpdateTaskProgressByTargetObj(int targetId, int progressDelta)
        {
            // 输出日志
            Log.Info($"更新任务进度,商品id={targetId},追加进度={progressDelta}");
            // 根据proId获取任务，要求任务状态不能是放弃或归档
            foreach (var task in m_TaskList)
            {
                if ((task.Status & ETaskStatus.Abandoned) == 0 && (task.Status & ETaskStatus.Archived) == 0
                    && (string.IsNullOrEmpty(task.TargetScope) || task.TargetScope.Contains(targetId.ToString())))
                {
                    UpdateProgress(task.id, progressDelta);
                    return;
                }
            }

            Log.Warning($"未找到任务数据，商品id={targetId}");
            return;
        }

        /// <summary>
        /// 增加任务积分
        /// </summary>
        /// <param name="id"></param>
        /// <param name="extra_data"></param>
        [Command(requiresAuthority = false)]
        public void AddCurTaskScore(int score)
        {
            // 输出日志
            Log.Info("更新当前任务积分：{0}", score);
            // 获取最近一个未完成的任务，要求任务状态不能是放弃或归档
            var task = GetRecentUnCompletedTask(true, task => { task.Score += score; return task; });
            if (task == null)
            {
                Log.Warning($"未找到任何未完成的任务(总计：{m_TaskList.Count})。");
            }
        }

        /// <summary>
        /// 放弃任务
        /// </summary>
        /// <param name="id"></param>
        [Command(requiresAuthority = false)]
        public void OnGiveUp(int id)
        {
            // 输出日志
            Log.Info($"放弃任务,id={id}");
            // 根据id获取任务，要求任务状态不能是放弃或归档
            var task = GetTaskVo(id, true, task =>
            {
                task.ChangeStatus(ETaskStatus.Abandoned);
                return task;
            });
            if (task == null)
            {
                Log.Warning($"未找到任务数据,id={id}");
                return;
            }
        }

        /// <summary>
        /// 归档任务
        /// </summary>
        /// <param name="id"></param>
        [Command(requiresAuthority = false)]
        public void OnArchive(int id)
        {
            // 输出日志
            Log.Info($"归档任务,id={id}");
            // 根据id获取任务，要求任务状态不能是放弃或归档
            var task = GetTaskVo(id, true,
                task =>
                {
                    // 叠加原来的状态
                    task.ChangeStatus(task.Status | ETaskStatus.Archived);
                    return task;
                }
                );
            if (task == null)
            {
                Log.Warning($"未找到任务数据,id={id}");
                return;
            }
        }

        #endregion

        #region 查询

        /// <summary>
        /// 获取任务
        /// </summary>
        /// <param name="id"></param>
        /// <param name="isNoticeSync">是否通知同步</param>
        /// <param name="onFoundTaskForNoticeSync">找到匹配任务后，在这里修改任务信息（仅<paramref name="isNoticeSync"/>为true时生效）</param>
        /// <returns></returns>
        public TaskVo? GetTaskVo(int id, bool isNoticeSync = false, Func<TaskVo, TaskVo> onFoundTaskForNoticeSync = null)
        {
            for (var index = 0; index < m_TaskList.Count; index++)
            {
                var task = m_TaskList[index];
                if (task.id == id)
                {
                    if (isNoticeSync)
                    {
                        // 在这里更新任务信息
                        if (onFoundTaskForNoticeSync != null)
                        {
                            // 执行回调
                            task = onFoundTaskForNoticeSync(task);
                        }
                        // 输出日志
                        // Debug.Log($"[同步任务]id:{task.id}，progress:{task.progress}/{task.target}");
                        // 重新设置任务，触发更新同步。
                        m_TaskList[index] = task;
                    }
                    // 返回任务
                    return task;
                }
            }
            return null;
        }

        /// <summary>
        /// 获取最近未完成的任务
        /// </summary>
        /// <param name="isNoticeSync">是否通知同步</param>
        /// <param name="onFoundTaskForNoticeSync">找到匹配任务后，在这里修改任务信息（仅<paramref name="isNoticeSync"/>为true时生效）</param>
        /// <returns></returns>
        public TaskVo? GetRecentUnCompletedTask(bool isNoticeSync = false, Func<TaskVo, TaskVo> onFoundTaskForNoticeSync = null)
        {
            for (var index = 0; index < m_TaskList.Count; index++)
            {
                var task = m_TaskList[index];
                if (task.Status == ETaskStatus.UnCompleted)
                {
                    if (isNoticeSync)
                    {
                        // 在这里更新任务信息
                        if (onFoundTaskForNoticeSync != null)
                        {
                            // 执行回调
                            task = onFoundTaskForNoticeSync(task);
                        }
                        // 输出日志
                        // Debug.Log($"[同步任务]id:{task.id}，progress:{task.progress}/{task.target}");
                        // 重新设置任务，触发更新同步。
                        m_TaskList[index] = task;
                    }
                    // 返回任务
                    return task;
                }
            }
            return null;
        }

        #endregion

    }

    /// <summary>
    /// 任务状态
    /// （支持按位多选操作）
    /// </summary>
    public enum ETaskStatus : byte
    {

        /// <summary>
        /// 未完成
        /// </summary>
        UnCompleted = 1 << 0,

        /// <summary>
        /// 已完成
        /// </summary>
        Completed = 1 << 1,

        /// <summary>
        /// 已放弃
        /// </summary>
        Abandoned = 1 << 2,

        /// <summary>
        /// 已归档
        /// </summary>
        Archived = 1 << 3,
    }

    [Serializable]
    public struct TaskVo
    {
        /// <summary>
        /// 任务ID
        /// </summary>
        public int id;

        /// <summary>
        /// 任务组ID
        /// </summary>
        public int GroupId;

        /// <summary>
        /// 适用范围
        /// </summary>
        //public HashSet<int> ProductList;
        public string TargetScope;

        /// <summary>
        /// 任务名称
        /// </summary>
        public string name;
        public string imageUrl;
        
        /// <summary>
        /// 得分
        /// </summary>
        public int Score;

        public int target;

        /// <summary>
        /// 任务状态
        /// </summary>
        public ETaskStatus Status;

        /// <summary>
        /// 改变任务状态
        /// </summary>
        public void ChangeStatus(ETaskStatus status)
        {
            Status = status;
        }

        #region 进度

        public int progress;

        public string progress_str_des => $"{progress}/{target}";

        public void UpdateProgress(int add_progress)
        {
            progress += add_progress;
            progress = Mathf.Clamp(progress, 0, target);

            // 更新状态
            if (progress >= target)
            {
                ChangeStatus(ETaskStatus.Completed);
            }
            else
            {
                // 只修改状态为完成的任务，修改其状态为未完成
                if (Status == ETaskStatus.Completed)
                {
                    ChangeStatus(ETaskStatus.UnCompleted);
                }
            }
        }

        #endregion

        /// <summary>
        /// 创建任务
        /// </summary>
        /// <param name="id"></param>
        /// <param name="name"></param>
        /// <param name="targetScope">完成目标应用的对象范围ID列表</param>
        /// <param name="imageUrl"></param>
        /// <param name="target"></param>
        /// <param name="groupId">任务组ID，可以没有组</param>
        /// <returns></returns>
        public TaskVo(int id, string name, string targetScope, string imageUrl, int target,
            int score = 0, int groupId = -1, ETaskStatus status = ETaskStatus.UnCompleted)
        {
            this.id = id;
            this.name = name;
            this.TargetScope = targetScope;
            this.imageUrl = imageUrl;
            this.target = target;
            this.progress = 0;
            this.GroupId = groupId;
            this.Status = status;
            this.Score = score;
        }

    }
}
