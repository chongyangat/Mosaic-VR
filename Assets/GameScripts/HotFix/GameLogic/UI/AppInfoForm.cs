using UnityEngine;
using TMPro;
using UnityGameFramework.Runtime;
using GameLogic;

namespace VBSOED
{
    public class AppInfoForm : UIFormLogic
    {
        [Tooltip("信息显示")]
        [SerializeField]
        private TextMeshProUGUI m_InfoShower;

        [Tooltip("信息刷新间隔")]
        [SerializeField]
        private float m_RefreshInterval = 0.2f;

        private float m_LastRefreshTime;
        private float m_GazeSendFrequency;

        public override void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
            if (Time.time - m_LastRefreshTime > m_RefreshInterval)
            {
                var log = $"FPS:{(1.0f / Time.deltaTime).ToString("f0")}({Application.version}) Gaze:{m_GazeSendFrequency:F1}Hz";
                m_InfoShower.text = log;
                m_LastRefreshTime = Time.time;
            }
        }

        protected override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            GazeDataSender.OnGazeSendFrequencyUpdated += OnGazeFrequencyUpdated;
        }

        public override void OnClose(bool isShutdown, object userData)
        {
            GazeDataSender.OnGazeSendFrequencyUpdated -= OnGazeFrequencyUpdated;
            base.OnClose(isShutdown, userData);
        }

        private void OnGazeFrequencyUpdated(float frequency)
        {
            m_GazeSendFrequency = frequency;
        }
    }
}