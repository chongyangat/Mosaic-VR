using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace XNF.BaseFrame.EyeTracking
{
    /// <summary>
    /// 眼动原始数据的基类
    /// </summary>
    public class EyeTrackingDataModuleBase : IEyeTrackingDataModule
    {
        /// <summary>
        /// 总持续时间(ms)
        /// </summary>
        protected int m_totalDuration = 0;
        /// <summary>
        /// 平均持续时间(ms)
        /// </summary>
        protected int m_meanDuration = 0;
        /// <summary>
        /// 发生总次数
        /// </summary>
        protected int m_totalCount = 0;
        /// <summary>
        /// 是否已经初始化
        /// </summary>
        public bool IsInitialized { get; protected set; }
        /// <summary>
        /// 眼动数据模块名称
        /// </summary>
        public string ModuleName { get; }
        /// <summary>
        /// 上一个眼动坐标值
        /// </summary>
        protected Vector3 m_lastEyePosition;
        /// <summary>
        /// 眼动数据收集的开始时间
        /// </summary>
        protected float m_dataCollectionStartTime;

        /// <summary>
        /// 初始化函数
        /// </summary>
        /// <param name="_objs"></param>
        public virtual void Initialize(params object[] _objs)
        {
            IsInitialized = true;
        }

        /// <summary>
        /// 清理函数
        /// </summary>
        public virtual void Clear()
        {
            ClearData();
            IsInitialized = false;
        }

        /// <summary>
        /// 实时计算模块数据(需每一帧都调用)
        /// </summary>
        /// <param name="_currentEyePosition"></param>
        public virtual void CollectData(Vector3 _currentEyePosition)
        {

        }

        /// <summary>
        /// 数据清理（重置数据）
        /// </summary>
        public virtual void ClearData()
        {
            m_lastEyePosition = Vector3.zero;
            m_dataCollectionStartTime = Time.time;
        }

        protected float GetCurrentDataCollectionTime()
        {
            return Time.time - m_dataCollectionStartTime;
        }
    }
}
