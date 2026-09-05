using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace XNF.BaseFrame.EyeTracking
{
    /// <summary>
    /// 延迟扫视眼动数据模块
    /// </summary>
    public class DelayedSaccadeDataModule : SaccadeModuleBase
    {
        /// <summary>
        /// 侵入性扫视发生率
        /// </summary>
        protected int m_intrusiveSacRate = 0;

        public DelayedSaccadeDataModule()
        {

        }

        public override void Clear()
        {
            base.Clear();

        }
    }
}
