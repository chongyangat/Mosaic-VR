using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace XNF.BaseFrame.EyeTracking
{
    /// <summary>
    /// 扫视眼动数据基类
    /// </summary>
    public class SaccadeModuleBase : EyeTrackingDataModuleBase
    {
        /// <summary>
        /// 扫视平均速度（单位：像素/ms）
        /// </summary>
        protected int m_meanSpeed = 0;

        /// <summary>
        /// 扫视速度的峰值（单位：像素/ms）
        /// </summary>
        protected int m_maxSpeed = 0;

        /// <summary>
        /// 扫视速度的最低值（单位：像素/ms）
        /// </summary>
        protected int m_minSpeed = 0;

        /// <summary>
        /// 扫视平均幅度(单位：像素)
        /// </summary>
        protected int m_meanAmplitude = 0;

        /// <summary>
        /// 任务成功率（单位：%）
        /// </summary>
        protected int m_successRate = 0;

        public SaccadeModuleBase()
        {

        }

        public override void Clear()
        {
            base.Clear();

        }
    }
}
