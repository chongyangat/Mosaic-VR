using System.Collections.Generic;
using UnityGameFramework.Runtime;

namespace GameLogic
{
    /// <summary>
    /// 逻辑事件
    /// </summary>
    [EventInterface(EEventGroup.GroupLogic)]
    interface ITaskLogicEvent
    {
        #region 增减

        /// <summary>
        /// 添加任务
        /// </summary>
        /// <param name="task"></param>
        void Add(TaskVo task);
        /// <summary>
        /// 添加任务
        /// </summary>
        /// <param name="task"></param>
        void AddList(List<TaskVo> list);
        /// <summary>
        /// 移除任务
        /// </summary>
        /// <param name="id"></param>
        void Remove(int id);

        /// <summary>
        /// 清空任务
        /// </summary>
        void Release();

        #endregion

        #region 修改

        /// <summary>
        /// 更新任务进度
        /// </summary>
        /// <param name="taskId"></param>
        /// <param name="add_progress"></param>
        void UpdateProgress(int taskId, int add_progress);

        /// <summary>
        /// 更新商品任务进度
        /// </summary>
        /// <param name="proId"></param>
        /// <param name="add_progress"></param>
        void UpdateTaskProgressByProduct(int proId, int add_progress);

        /// <summary>
        /// 增加任务积分
        /// </summary>
        /// <param name="score"></param>
        void AddCurTaskScore(int score);

        /// <summary>
        /// 放弃任务
        /// </summary>
        /// <param name="id"></param>
        void GiveUp(int id);

        /// <summary>
        /// 放弃任务
        /// </summary>
        /// <param name="id"></param>
        void Archive(int id);

        #endregion

        #region 查询

        #region 列表

        /// <summary>
        /// 获取任务列表
        /// </summary>
        /// <param name="qty">获取数量，非正数表示全部获取。
        /// <br/>如果<paramref name="isOnlySameGroup"/>为true，则可能无法返回足够数量的任务，因与第一个任务同一组的剩余任务数量可能不足</param>
        /// <param name="status">获取的任务状态</param>
        /// <param name="isOnlySameGroup">仅相同组</param>
        /// <param name="userData">用户数据</param>
        void GetTaskList(int qty = -1, ETaskStatus status = ETaskStatus.UnCompleted, 
            bool isOnlySameGroup = false, object userData = null);

        /// <summary>
        /// 获取任务列表返回
        /// </summary>
        /// <param name="list"></param>
        /// <param name="userData"></param>
        void OnGetTaskListReturn(List<TaskVo> list, object userData);

        /// <summary>
        /// 任务列表更新
        /// </summary>
        /// <param name="selectScence"></param>
        void OnTaskListUpdate(); 

        #endregion

        #region 任务

        /// <summary>
        /// 根据id获取任务
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        void GetTask(int id);

        /// <summary>
        /// 获取任务返回
        /// </summary>
        /// <param name="task"></param>
        void OnGetTaskReturn(TaskVo task);

        /// <summary>
        /// 任务更新
        /// </summary>
        void OnTaskUpdate(TaskVo task); 

        #endregion

        #endregion

    }
}