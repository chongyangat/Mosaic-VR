// ===================================================
// Copyright ©. All rights reserved.
// Author：ASSJ
// CreateTime：2024/9/12 16:09:16
// Version: v1.0
// Description：界面助手
// ===================================================

using UnityEngine;
#if XR_INTERACTION_TOOLKIT
using UnityEngine.XR.Interaction.Toolkit.Interactors; 
#endif

namespace VBSOED
{
    /// <summary>
    /// 界面助手
    /// </summary>
    public class UIHelper : MonoBehaviour
    {
        #region 手部远距离交互

#if XR_INTERACTION_TOOLKIT
        [Tooltip("左手远距离交互器")]
        [SerializeField]
        private NearFarInteractor m_LeftNearFarInteractor;

        [Tooltip("右手远距离交互器")]
        [SerializeField]
        private NearFarInteractor m_RightNearFarInteractor; 
#endif

        /// <summary>
        /// 开启/禁用远距离交互
        /// </summary>
        /// <param name="enable"></param>
        public void ToggleFarCasting(bool enable)
        {
#if XR_INTERACTION_TOOLKIT
            m_LeftNearFarInteractor.enableFarCasting = enable;
            m_RightNearFarInteractor.enableFarCasting = enable; 
#endif
        }

        #endregion

        #region U3D

        private void OnEnable()
        {
            ToggleFarCasting(true);
        }

        private void OnDisable()
        {
            ToggleFarCasting(false);
        }

        #endregion
    }
}
