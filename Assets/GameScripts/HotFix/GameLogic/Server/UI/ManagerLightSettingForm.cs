using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

namespace GameLogic
{
    public class ManagerLightSettingForm : UIFormLogic, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Slider引用")]
        [Tooltip("光照强度滑块")]
        [SerializeField]
        private Slider m_IntensitySlider;

        [Header("Header和Content")]
        [Tooltip("Header(标题区域,始终显示)")]
        [SerializeField]
        private RectTransform m_Header;

        [Tooltip("Content(Slider区域,悬停展开)")]
        [SerializeField]
        private RectTransform m_Content;

        [Header("动画设置")]
        [Tooltip("展开/收起动画时长(秒)")]
        [SerializeField]
        private float m_AnimDuration = 0.2f;

        private ManagerLightController m_LightController;
        private bool m_IsExpanded = false;
        private Coroutine m_AnimCoroutine;

        private float m_CollapsedHeight;
        private float m_ExpandedHeight;

        protected override void OnOpen(object userData)
        {
            base.OnOpen(userData);

            if (m_IntensitySlider == null)
            {
                Log.Error("光照强度Slider未绑定");
                return;
            }

            if (m_Header == null)
            {
                Log.Error("Header未绑定");
                return;
            }

            if (m_Content == null)
            {
                Log.Error("Content未绑定");
                return;
            }

            m_LightController = Object.FindObjectOfType<ManagerLightController>();
            if (m_LightController == null)
            {
                Log.Error("未找到ManagerLightController，无法控制灯光");
                return;
            }

            m_IntensitySlider.minValue = 0f;
            m_IntensitySlider.maxValue = 5f;
            m_IntensitySlider.value = m_LightController.globalIntensity;
            m_IntensitySlider.onValueChanged.AddListener(OnIntensityChanged);

            var panelRect = GetComponent<RectTransform>();
            if (panelRect == null) return;

            m_ExpandedHeight = panelRect.sizeDelta.y;
            m_CollapsedHeight = m_Header.sizeDelta.y;

            panelRect.sizeDelta = new Vector2(panelRect.sizeDelta.x, m_CollapsedHeight);
            m_Content.gameObject.SetActive(false);
        }

        public override void OnClose(bool isShutdown, object userData)
        {
            base.OnClose(isShutdown, userData);
            if (m_IntensitySlider != null)
            {
                m_IntensitySlider.onValueChanged.RemoveListener(OnIntensityChanged);
            }
        }

        private void OnIntensityChanged(float value)
        {
            if (m_LightController == null) return;

            m_LightController.SetIntensity(value);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (m_IsExpanded) return;
            m_IsExpanded = true;

            if (m_AnimCoroutine != null) StopCoroutine(m_AnimCoroutine);
            m_AnimCoroutine = StartCoroutine(AnimatePanelHeight(m_CollapsedHeight, m_ExpandedHeight, true));
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!m_IsExpanded) return;
            m_IsExpanded = false;

            if (m_AnimCoroutine != null) StopCoroutine(m_AnimCoroutine);
            m_AnimCoroutine = StartCoroutine(AnimatePanelHeight(m_ExpandedHeight, m_CollapsedHeight, false));
        }

        private IEnumerator AnimatePanelHeight(float fromHeight, float toHeight, bool expanding)
        {
            var panelRect = GetComponent<RectTransform>();
            if (panelRect == null) yield break;

            if (!expanding)
            {
                m_Content.gameObject.SetActive(false);
            }

            float width = panelRect.sizeDelta.x;
            float elapsed = 0f;
            while (elapsed < m_AnimDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / m_AnimDuration);
                float smoothT = t * t * (3f - 2f * t);

                float currentHeight = Mathf.Lerp(fromHeight, toHeight, smoothT);
                panelRect.sizeDelta = new Vector2(width, currentHeight);

                yield return null;
            }

            panelRect.sizeDelta = new Vector2(width, toHeight);

            if (expanding)
            {
                m_Content.gameObject.SetActive(true);
            }

            m_AnimCoroutine = null;
        }
    }
}
