using UnityEngine;

namespace VBSOED
{
    public enum PathType
    {
        // 线性
#if UNITY_EDITOR
        [Header("线性")]
#endif
        Linear,

        // 曲线动画
#if UNITY_EDITOR
        [Header("曲线")]
#endif
        CatmullRom,

        // 三次贝塞尔曲线,每个点需要两个额外的控制点
        //CubicBezier
    }
}