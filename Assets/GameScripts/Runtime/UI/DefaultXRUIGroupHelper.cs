using UnityEngine;
using UnityEngine.UI;
#if UNITY_ANDROID && XR_INTERACTION_TOOLKIT
using UnityEngine.XR.Interaction.Toolkit.UI;
# endif
using UnityGameFramework.Runtime;

namespace GameMain
{
    /// <summary>
    /// 默认界面组辅助器。
    /// </summary>
    public class DefaultXRUIGroupHelper : UIGroupHelperBase
    {
        public const int DepthFactor = 10000;

        private int m_Depth = 0;
        private Canvas m_CachedCanvas = null;
        
        /// <summary>
        /// 设置界面组深度。
        /// </summary>
        /// <param name="depth">界面组深度。</param>
        public override void SetDepth(int depth)
        {
            m_Depth = depth;
            m_CachedCanvas.overrideSorting = true;
            m_CachedCanvas.sortingOrder = DepthFactor * depth;
        }

        private void Awake()
        {
            m_CachedCanvas = gameObject.GetOrAddComponent<Canvas>();
            m_CachedCanvas.vertexColorAlwaysGammaSpace = true;
            gameObject.GetOrAddComponent<GraphicRaycaster>();
#if UNITY_ANDROID && XR_INTERACTION_TOOLKIT
            // 新增XRCanvas组件
            var graphicRaycaster = gameObject.GetOrAddComponent<TrackedDeviceGraphicRaycaster>();
            graphicRaycaster.ignoreReversedGraphics = true;
#endif
        }

        private void Start()
        {
            m_CachedCanvas.overrideSorting = true;
            m_CachedCanvas.sortingOrder = DepthFactor + m_Depth;
            this.transform.localPosition = Vector3.zero;
            RectTransform rectTransform = GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero;
            // 重置旋转，以匹配父级对象。
            transform.localRotation = Quaternion.identity;
        }
    }
}