using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;

namespace GameMain
{
    /// <summary>
    /// Applies a conservative Quest performance profile without changing game,
    /// networking, eye-tracking, or hand-tracking behaviour.
    /// </summary>
    [DefaultExecutionOrder(-32000)]
    public sealed class QuestPerformanceBootstrap : MonoBehaviour
    {
        private const int QuestQualityLevel = 1; // Low / mobile performant URP preset.
        private const int TargetRefreshRate = 72;
        private const float MinimumComfortFps = 60f;
        private const float QualityRecoveryFps = 68f;
        private const float AdaptiveSampleSeconds = 1f;
        private const float DegradeHoldSeconds = 1.5f;
        private const float UpgradeHoldSeconds = 8f;
        private const float MinDynamicResolutionScale = 0.72f;
        private const float MaxDynamicResolutionScale = 1.0f;
        private const float MetricsIntervalSeconds = 10f;

        private enum AdaptiveTier
        {
            Quality,
            Balanced,
            Performance
        }

        private static QuestPerformanceBootstrap s_Instance;

        private float m_MetricsElapsed;
        private int m_MetricsFrames;
        private float m_NextSettingsCheck;
        private float m_AdaptiveElapsed;
        private int m_AdaptiveFrames;
        private float m_BelowMinimumElapsed;
        private float m_AboveRecoveryElapsed;
        private AdaptiveTier m_AdaptiveTier = AdaptiveTier.Balanced;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Create()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (s_Instance != null)
            {
                return;
            }

            var bootstrapObject = new GameObject("[Quest] Performance Bootstrap");
            DontDestroyOnLoad(bootstrapObject);
            s_Instance = bootstrapObject.AddComponent<QuestPerformanceBootstrap>();
#endif
        }

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_Instance = this;
            DontDestroyOnLoad(gameObject);
            ApplyUnitySettings();
            StartCoroutine(ApplyOculusSettingsWhenReady());
        }

        private void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;
            m_MetricsElapsed += deltaTime;
            m_MetricsFrames++;
            m_AdaptiveElapsed += deltaTime;
            m_AdaptiveFrames++;

            // Some legacy game code changes targetFrameRate after scene entry.
            // Reasserting this infrequently has no per-frame performance cost.
            if (Time.unscaledTime >= m_NextSettingsCheck)
            {
                m_NextSettingsCheck = Time.unscaledTime + 2f;
                if (Application.targetFrameRate != TargetRefreshRate)
                {
                    Application.targetFrameRate = TargetRefreshRate;
                }

                TryEnableEyeTrackedFoveation();
            }

            EvaluateAdaptiveQuality();

            if (m_MetricsElapsed < MetricsIntervalSeconds)
            {
                return;
            }

            float measuredFps = m_MetricsFrames / Mathf.Max(0.001f, m_MetricsElapsed);
            float oculusFps = OVRManager.display != null ? OVRManager.display.appFramerate : 0f;
            float displayHz = OVRManager.display != null ? OVRManager.display.displayFrequency : 0f;

            Debug.LogWarning(
                $"[QuestPerformance] measuredFps={measuredFps:F1}, oculusFps={oculusFps:F1}, " +
                $"displayHz={displayHz:F0}, quality={QualitySettings.names[QualitySettings.GetQualityLevel()]}, " +
                $"tier={m_AdaptiveTier}, eyeScale={XRSettings.eyeTextureResolutionScale:F2}, " +
                $"viewportScale={XRSettings.renderViewportScale:F2}, " +
                $"eyeTrackedFoveation={OVRManager.eyeTrackedFoveatedRenderingEnabled}");

            m_MetricsElapsed = 0f;
            m_MetricsFrames = 0;
        }

        private static void ApplyUnitySettings()
        {
            Application.targetFrameRate = TargetRefreshRate;
            QualitySettings.vSyncCount = 0;
            QualitySettings.SetQualityLevel(QuestQualityLevel, true);
            QualitySettings.pixelLightCount = 1;
            QualitySettings.shadows = ShadowQuality.Disable;
            QualitySettings.realtimeReflectionProbes = false;
            QualitySettings.antiAliasing = 2;
            QualitySettings.lodBias = 0.6f;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;

            // Keep warnings/errors available over ADB while eliminating expensive
            // stack traces and ordinary log output in the optimized APK.
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
            if (!Debug.isDebugBuild)
            {
                Debug.unityLogger.filterLogType = LogType.Warning;
            }
        }

        private static IEnumerator ApplyOculusSettingsWhenReady()
        {
            while (OVRManager.instance == null || !OVRPlugin.initialized)
            {
                yield return null;
            }

            OVRManager.suggestedCpuPerfLevel = OVRManager.ProcessorPerformanceLevel.SustainedHigh;
            OVRManager.suggestedGpuPerfLevel = OVRManager.ProcessorPerformanceLevel.SustainedHigh;
            OVRManager.foveatedRenderingLevel = OVRManager.FoveatedRenderingLevel.Medium;
            OVRManager.useDynamicFoveatedRendering = true;

            OVRManager manager = OVRManager.instance;
            manager.enableDynamicResolution = true;
            manager.quest2MinDynamicResolutionScale = MinDynamicResolutionScale;
            manager.quest2MaxDynamicResolutionScale = MaxDynamicResolutionScale;
            manager.quest3MinDynamicResolutionScale = MinDynamicResolutionScale;
            manager.quest3MaxDynamicResolutionScale = MaxDynamicResolutionScale;
            manager.minDynamicResolutionScale = MinDynamicResolutionScale;
            manager.maxDynamicResolutionScale = MaxDynamicResolutionScale;

            // Allocate a full-resolution eye buffer and let the runtime reduce only
            // the viewport when GPU headroom is tight. The previous 0.85 maximum
            // permanently reduced clarity even while the device was sustaining 72 Hz.
            XRSettings.eyeTextureResolutionScale = MaxDynamicResolutionScale;
            XRSettings.renderViewportScale = MaxDynamicResolutionScale;
            TrySetPipelineRenderScale(MaxDynamicResolutionScale);

            TryEnableEyeTrackedFoveation();

            float[] availableFrequencies = OVRManager.display.displayFrequenciesAvailable;
            if (availableFrequencies != null)
            {
                for (int i = 0; i < availableFrequencies.Length; i++)
                {
                    if (Mathf.Approximately(availableFrequencies[i], TargetRefreshRate))
                    {
                        OVRManager.display.displayFrequency = TargetRefreshRate;
                        break;
                    }
                }
            }

            Debug.LogWarning(
                $"[QuestPerformance] Adaptive balanced profile active: {TargetRefreshRate}Hz target, " +
                $"{MinimumComfortFps:F0} FPS floor, {MinDynamicResolutionScale:F2}-{MaxDynamicResolutionScale:F2} " +
                "dynamic resolution, eye-tracked/dynamic foveation, sustained CPU/GPU.");
        }

        private void EvaluateAdaptiveQuality()
        {
            if (m_AdaptiveElapsed < AdaptiveSampleSeconds)
            {
                return;
            }

            float fps = m_AdaptiveFrames / Mathf.Max(0.001f, m_AdaptiveElapsed);
            float sampleDuration = m_AdaptiveElapsed;
            m_AdaptiveElapsed = 0f;
            m_AdaptiveFrames = 0;

            if (fps < MinimumComfortFps)
            {
                m_BelowMinimumElapsed += sampleDuration;
                m_AboveRecoveryElapsed = 0f;
            }
            else if (fps >= QualityRecoveryFps)
            {
                m_AboveRecoveryElapsed += sampleDuration;
                m_BelowMinimumElapsed = 0f;
            }
            else
            {
                m_BelowMinimumElapsed = 0f;
                m_AboveRecoveryElapsed = 0f;
            }

            if (m_BelowMinimumElapsed >= DegradeHoldSeconds && m_AdaptiveTier < AdaptiveTier.Performance)
            {
                ApplyAdaptiveTier((AdaptiveTier)((int)m_AdaptiveTier + 1), fps);
            }
            else if (m_AboveRecoveryElapsed >= UpgradeHoldSeconds && m_AdaptiveTier > AdaptiveTier.Quality)
            {
                ApplyAdaptiveTier((AdaptiveTier)((int)m_AdaptiveTier - 1), fps);
            }
        }

        private void ApplyAdaptiveTier(AdaptiveTier tier, float measuredFps)
        {
            m_AdaptiveTier = tier;
            m_BelowMinimumElapsed = 0f;
            m_AboveRecoveryElapsed = 0f;

            switch (tier)
            {
                case AdaptiveTier.Quality:
                    QualitySettings.pixelLightCount = 1;
                    QualitySettings.lodBias = 0.7f;
                    OVRManager.foveatedRenderingLevel = OVRManager.FoveatedRenderingLevel.Low;
                    break;
                case AdaptiveTier.Balanced:
                    QualitySettings.pixelLightCount = 1;
                    QualitySettings.lodBias = 0.6f;
                    OVRManager.foveatedRenderingLevel = OVRManager.FoveatedRenderingLevel.Medium;
                    break;
                default:
                    QualitySettings.pixelLightCount = 0;
                    QualitySettings.lodBias = 0.45f;
                    OVRManager.foveatedRenderingLevel = OVRManager.FoveatedRenderingLevel.High;
                    break;
            }

            Debug.LogWarning(
                $"[QuestPerformance] Adaptive tier -> {tier} at {measuredFps:F1} FPS; " +
                $"foveation={OVRManager.foveatedRenderingLevel}, lodBias={QualitySettings.lodBias:F2}.");
        }

        private static void TryEnableEyeTrackedFoveation()
        {
            if (OVRManager.eyeTrackedFoveatedRenderingSupported &&
                !OVRManager.eyeTrackedFoveatedRenderingEnabled)
            {
                OVRManager.eyeTrackedFoveatedRenderingEnabled = true;
            }
        }

        private static void TrySetPipelineRenderScale(float renderScale)
        {
            RenderPipelineAsset pipelineAsset = GraphicsSettings.currentRenderPipeline;
            if (pipelineAsset == null)
            {
                return;
            }

            // GameMain.Runtime deliberately has no compile-time URP dependency.
            // URP exposes renderScale publicly, so reflection lets this bootstrap
            // restore full resolution without widening the assembly references.
            PropertyInfo renderScaleProperty = pipelineAsset.GetType().GetProperty(
                "renderScale",
                BindingFlags.Instance | BindingFlags.Public);

            if (renderScaleProperty != null && renderScaleProperty.CanWrite)
            {
                renderScaleProperty.SetValue(pipelineAsset, renderScale, null);
            }
        }
    }
}
