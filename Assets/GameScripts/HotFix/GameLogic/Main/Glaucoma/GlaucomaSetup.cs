// ===================================================
// Copyright ©. All rights reserved.
// Author：ASSJ
// CreateTime：2024/8/27 17:05:16
// Version: v1.0
// Description：青光眼设置
// ===================================================

using UnityEngine;
using UnityEngine.UI;

namespace VBSOED
{
    /// <summary>
    /// 青光眼设置
    /// </summary>
    public class GlaucomaSetup : MonoBehaviour
    {
        [Tooltip("模式列表")]
        [SerializeField]
        private Toggle[] m_ModeDropdown;

        [Tooltip("镜头效果")]
        [SerializeField]
        private GlaucomaEffect m_CameraEffect;

        void Start()
        {
            // 设置模式列表响应事件
            for (int i = 0; i < m_ModeDropdown.Length; i++)
            {
                var mode = m_ModeDropdown[i];
                var index = i;
                mode.onValueChanged.AddListener((bool isOn) => 
                {
                    if (isOn)
                    {
                        OnModeChanged(index);
                    }
                    // 
                    SetDefaultMode();
                });
                
            }
        }

        /// <summary>
        /// 设置默认选中项
        /// </summary>
        private void SetDefaultMode()
        {
            // 遍历模式列表
            var isAnyModeSelected = false;
            for (int i = 0; i < m_ModeDropdown.Length; i++)
            {
                // 
                if (m_ModeDropdown[i].isOn)
                {
                    isAnyModeSelected = true;
                    break;
                }
            }
            // 如果所有模式都不选中，则默认选中第一个
            if (isAnyModeSelected)
            {
                return;
            }
            // 默认选中第一个
            m_ModeDropdown[0].isOn = true;
        }

        void OnDestroy()
        {
            // 移除模式列表响应事件
            for (int i = 0; i < m_ModeDropdown.Length; i++)
            {
                m_ModeDropdown[i].onValueChanged.RemoveAllListeners();
            }
        }

        void OnModeChanged(int value)
        {
            // 设置镜头效果
            m_CameraEffect.SetEffectType((GlaucomaEffectType)value);
        }
    }

}