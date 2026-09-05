using System.Collections.Generic;

namespace MetaQuestProEyeGazeUDP
{
    /// <summary>
    /// 一个主线程任务队列，让子线程直接执行主线程的方法
    /// </summary>
    public static class MainThreadTaskQueue
    {
        public delegate void MainThreadAction();
        private static readonly Queue<MainThreadAction> tasks = new Queue<MainThreadAction>();
        private static readonly object queueLock = new object();
        
        /// <summary>
        /// 队列最大容量，默认60（适配60Hz帧率）
        /// </summary>
        public static int MaxQueueSize = 60;

        /// <summary>
        /// 将任务添加进队列内
        /// </summary>
        /// <param name="action"></param>
        public static void EnqueueTask(MainThreadAction action)
        {
            lock (queueLock)
            {
                if (tasks.Count >= MaxQueueSize)
                {
                    // 队列已满，移除最旧的任务
                    tasks.Dequeue();
                }
                tasks.Enqueue(action);
            }
        }

        /// <summary>
        /// 清空队列
        /// </summary>
        public static void Clear()
        {
            lock (queueLock)
            {
                tasks.Clear();
            }
        }

        /// <summary>
        /// 获取当前队列中的任务数量
        /// </summary>
        public static int Count
        {
            get
            {
                lock (queueLock)
                {
                    return tasks.Count;
                }
            }
        }

        public static void Update()
        {
            lock (queueLock)
            {
                while (tasks.Count > 0)
                {
                    var task = tasks.Dequeue();
                    task?.Invoke();
                }
            }
        }
    }

}
