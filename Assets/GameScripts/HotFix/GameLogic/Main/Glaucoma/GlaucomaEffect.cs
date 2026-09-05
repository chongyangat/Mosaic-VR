// ===================================================
// Copyright ©. All rights reserved.
// Author：ASSJ
// CreateTime：2024/8/29 11:11:16
// Version: v1.0
// Description：青光眼效果
// ===================================================

using UnityEngine;

namespace VBSOED
{
    /// <summary>
    /// 青光眼效果
    /// </summary>
    public class GlaucomaEffect : MonoBehaviour
    {
        /// <summary>
        /// 特效相机
        /// </summary>
        private Camera m_Camera;

        //[Tooltip("青光眼效果后处理")]
        //[SerializeField]
        //private Vignette m_Vignette;

        private void Awake()
        {
            m_Camera = GetComponent<Camera>();
        }

        /// <summary>
        /// 设置效果
        /// </summary>
        /// <param name="type">参考<see cref="GlaucomaEffectType"/></param>
        public void SetEffectType(GlaucomaEffectType type)
        {
            switch (type)
            {
                // 无
                case GlaucomaEffectType.None:
                    m_Camera.enabled = false;
                    break;
                // 左眼
                case GlaucomaEffectType.Left:
                    m_Camera.stereoTargetEye = StereoTargetEyeMask.Left;
                    break;
                // 右眼
                case GlaucomaEffectType.Right:
                    m_Camera.stereoTargetEye = StereoTargetEyeMask.Right;
                    break;
                // 双眼
                case GlaucomaEffectType.Double:
                    m_Camera.enabled = true;
                    break;
                // 默认
                default:
                    m_Camera.stereoTargetEye = StereoTargetEyeMask.None;
                    // 输出警告日志
                    Debug.LogWarning("未找到对应类型，请检查GlaucomaEffectType，采用默认GlaucomaEffectType.None模式。");
                    break;
            }
        }


    }

    /// <summary>
    /// 青光眼效果类型
    /// </summary>
    public enum GlaucomaEffectType
    {
        /// <summary>
        /// 无
        /// </summary>
        None,

        /// <summary>
        /// 左眼
        /// </summary>
        Left,

        /// <summary>
        /// 右眼
        /// </summary>
        Right,

        /// <summary>
        /// 双眼
        /// </summary>
        Double
    }

}