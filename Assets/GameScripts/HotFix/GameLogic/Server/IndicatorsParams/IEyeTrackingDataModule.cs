using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace XNF.BaseFrame.EyeTracking
{
    public interface IEyeTrackingDataModule
    {
        /// <summary>
        /// 初始化函数
        /// </summary>
        public abstract void Initialize(params object[] _objs);
        /// <summary>
        /// 清理
        /// </summary>
        public abstract void Clear();
        /// <summary>
        /// 收集该模块的眼动数据
        /// </summary>
        public abstract void CollectData(Vector3 _currentEyePosition);
        /// <summary>
        /// 清除眼动数据
        /// </summary>
        public abstract void ClearData();

        // 公共属性
        bool IsInitialized { get; }
        string ModuleName { get; }
    }
}
