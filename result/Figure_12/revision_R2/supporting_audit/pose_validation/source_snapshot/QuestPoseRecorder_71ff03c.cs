using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace GameMain
{
    /// <summary>
    /// Writes Quest HMD poses observed by the manager to a session CSV. Source
    /// samples are de-duplicated by the sequence generated on Quest, while a
    /// sequence of zero remains a world-pose fallback for older/uninitialized clients.
    /// </summary>
    public sealed class QuestPoseRecorder : IDisposable
    {
        private const int FlushSampleInterval = 60;

        private const string CsvHeader =
            "序号,PC采集时间,PC时间戳(ms),录制相对时间(ms),Unity帧,网络对象ID," +
            "Quest位姿序号,PoseReadTime-QuestUnix(ms),PoseReadTime-PC时钟域(ms)," +
            "PoseReadTime至PC观测延迟(ms),RenderPredicted-TrackingSpace位置X," +
            "RenderPredicted-TrackingSpace位置Y,RenderPredicted-TrackingSpace位置Z," +
            "RenderPredicted-TrackingSpace旋转X,RenderPredicted-TrackingSpace旋转Y," +
            "RenderPredicted-TrackingSpace旋转Z,RenderPredicted-Unity世界位置X," +
            "RenderPredicted-Unity世界位置Y,RenderPredicted-Unity世界位置Z," +
            "RenderPredicted-Unity世界旋转X,RenderPredicted-Unity世界旋转Y," +
            "RenderPredicted-Unity世界旋转Z,时钟同步有效," +
            "时钟同步状态,时钟偏移(ms),同步RTT(ms),同步年龄(ms)," +
            "偏移不确定度上界(ms),同步样本数," +
            "RenderPredicted-TrackingSpace四元数X,RenderPredicted-TrackingSpace四元数Y," +
            "RenderPredicted-TrackingSpace四元数Z,RenderPredicted-TrackingSpace四元数W," +
            "RenderPredicted-Unity世界四元数X,RenderPredicted-Unity世界四元数Y," +
            "RenderPredicted-Unity世界四元数Z,RenderPredicted-Unity世界四元数W," +
            "PoseReadTime-OVR单调时钟(s),RawPoseSampleTime-OVR单调时钟(s)," +
            "RawPoseSampleTime-QuestUnix估算(ms),PredictedDisplayTime代理-OVR单调时钟(s)," +
            "PredictedDisplayTime代理-QuestUnix估算(ms),PredictionHorizon(ms)," +
            "RawSampleAge(ms),QuestUnity帧,Quest采样阶段,RawPose有效," +
            "RenderPoseTime有效,Raw-TrackingSpace位置X,Raw-TrackingSpace位置Y," +
            "Raw-TrackingSpace位置Z,Raw-TrackingSpace四元数X," +
            "Raw-TrackingSpace四元数Y,Raw-TrackingSpace四元数Z," +
            "Raw-TrackingSpace四元数W,Raw-Unity世界位置X,Raw-Unity世界位置Y," +
            "Raw-Unity世界位置Z,Raw-Unity世界四元数X,Raw-Unity世界四元数Y," +
            "Raw-Unity世界四元数Z,Raw-Unity世界四元数W";

        private readonly Dictionary<uint, uint> m_LastSequenceBySource =
            new Dictionary<uint, uint>();
        private readonly StringBuilder m_LineBuilder = new StringBuilder(512);

        private StreamWriter m_Writer;
        private string m_RecordingFilePath = string.Empty;
        private double m_RecordingStartedAtMs;
        private int m_RecordingIndex;
        private int m_SamplesSinceFlush;

        public bool IsRecording => m_Writer != null;

        public string RecordingFilePath => m_RecordingFilePath;

        public static string GetCsvHeader()
        {
            return CsvHeader;
        }

        public bool StartRecording(string filePath)
        {
            if (IsRecording)
            {
                Debug.LogWarning("[QuestPoseRecorder] A recording is already active.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                Debug.LogError("[QuestPoseRecorder] Recording path is empty.");
                return false;
            }

            try
            {
                string directoryPath = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                m_Writer = new StreamWriter(filePath, false, Encoding.UTF8)
                {
                    AutoFlush = false
                };
                m_Writer.WriteLine(CsvHeader);

                m_RecordingFilePath = filePath;
                m_RecordingStartedAtMs = UDPClockSync.GetLocalUnixTimeMsD();
                m_RecordingIndex = 0;
                m_SamplesSinceFlush = 0;
                m_LastSequenceBySource.Clear();

                Debug.Log($"[QuestPoseRecorder] Recording started: {filePath}");
                return true;
            }
            catch (Exception exception)
            {
                CloseWriterAfterFailure();
                Debug.LogError(
                    $"[QuestPoseRecorder] Unable to start recording '{filePath}': " +
                    exception.Message);
                return false;
            }
        }

        public bool Record(UpdateHMDInfoEventArgs eventArgs)
        {
            if (eventArgs == null)
            {
                return false;
            }

            return RecordPose(
                eventArgs.SourceNetId,
                eventArgs.SampleSequence,
                eventArgs.QuestTimestampMs,
                eventArgs.TrackingSpacePosition,
                eventArgs.TrackingSpaceRotation,
                eventArgs.Position,
                eventArgs.Rotation,
                eventArgs.TrackingSpaceQuaternion,
                eventArgs.WorldQuaternion,
                eventArgs.RawTrackingSpacePosition,
                eventArgs.RawTrackingSpaceQuaternion,
                eventArgs.RawWorldPosition,
                eventArgs.RawWorldQuaternion,
                eventArgs.PoseReadTimeOvrSeconds,
                eventArgs.RawPoseSampleTimeOvrSeconds,
                eventArgs.PredictedDisplayTimeOvrSeconds,
                eventArgs.PredictionHorizonMs,
                eventArgs.RawSampleAgeMs,
                eventArgs.QuestUnityFrame,
                eventArgs.SamplePhase,
                eventArgs.RawPoseValid,
                eventArgs.RenderPoseTimeValid);
        }

        public bool RecordPose(
            uint sourceNetId,
            uint sampleSequence,
            double questTimestampMs,
            Vector3 trackingSpacePosition,
            Vector3 trackingSpaceRotation,
            Vector3 worldPosition,
            Vector3 worldRotation,
            Quaternion trackingSpaceQuaternion = default,
            Quaternion worldQuaternion = default,
            Vector3 rawTrackingSpacePosition = default,
            Quaternion rawTrackingSpaceQuaternion = default,
            Vector3 rawWorldPosition = default,
            Quaternion rawWorldQuaternion = default,
            double poseReadTimeOvrSeconds = 0d,
            double rawPoseSampleTimeOvrSeconds = 0d,
            double predictedDisplayTimeOvrSeconds = 0d,
            float predictionHorizonMs = 0f,
            float rawSampleAgeMs = 0f,
            int questUnityFrame = 0,
            QuestPoseSamplePhase samplePhase = QuestPoseSamplePhase.Unknown,
            bool rawPoseValid = false,
            bool renderPoseTimeValid = false)
        {
            if (!IsRecording)
            {
                return false;
            }

            if (!IsFinite(worldPosition)
                || !IsFinite(worldRotation)
                || !IsFinite(trackingSpacePosition)
                || !IsFinite(trackingSpaceRotation))
            {
                Debug.LogWarning("[QuestPoseRecorder] An invalid Quest pose was ignored.");
                return false;
            }

            // A non-zero sequence is generated on Quest and advances once per XR
            // sample. The manager event runs every frame, so suppress repeated views
            // of the same network sample. Zero remains a compatibility fallback.
            if (sampleSequence != 0)
            {
                if (m_LastSequenceBySource.TryGetValue(sourceNetId, out uint lastSequence)
                    && lastSequence == sampleSequence)
                {
                    return false;
                }
                m_LastSequenceBySource[sourceNetId] = sampleSequence;
            }

            try
            {
                double pcTimestampMs = UDPClockSync.GetLocalUnixTimeMsD();
                UDPClockSync.ClockSyncSnapshot sync = UDPClockSync.GetSnapshot();
                bool hasQuestTimestamp = IsFinite(questTimestampMs)
                    && questTimestampMs > 0d;
                bool canCompareClocks = hasQuestTimestamp && sync.IsReady;
                double questTimestampInPcClockMs = canCompareClocks
                    ? questTimestampMs - sync.ClockDiff
                    : double.NaN;
                double observedDelayMs = canCompareClocks
                    ? pcTimestampMs - questTimestampInPcClockMs
                    : double.NaN;

                m_RecordingIndex++;
                BuildCsvLine(
                    m_RecordingIndex,
                    DateTime.Now,
                    pcTimestampMs,
                    sourceNetId,
                    sampleSequence,
                    questTimestampMs,
                    questTimestampInPcClockMs,
                    observedDelayMs,
                    trackingSpacePosition,
                    trackingSpaceRotation,
                    worldPosition,
                    worldRotation,
                    trackingSpaceQuaternion,
                    worldQuaternion,
                    rawTrackingSpacePosition,
                    rawTrackingSpaceQuaternion,
                    rawWorldPosition,
                    rawWorldQuaternion,
                    poseReadTimeOvrSeconds,
                    rawPoseSampleTimeOvrSeconds,
                    predictedDisplayTimeOvrSeconds,
                    predictionHorizonMs,
                    rawSampleAgeMs,
                    questUnityFrame,
                    samplePhase,
                    rawPoseValid,
                    renderPoseTimeValid,
                    sync);
                m_Writer.WriteLine(m_LineBuilder.ToString());

                m_SamplesSinceFlush++;
                if (m_SamplesSinceFlush >= FlushSampleInterval)
                {
                    m_Writer.Flush();
                    m_SamplesSinceFlush = 0;
                }
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[QuestPoseRecorder] Unable to write Quest pose: " +
                    exception.Message);
                return false;
            }
        }

        public string StopRecording()
        {
            string filePath = m_RecordingFilePath;
            if (!IsRecording)
            {
                return filePath;
            }

            try
            {
                m_Writer.Flush();
                m_Writer.Close();
                m_Writer.Dispose();
                Debug.Log($"[QuestPoseRecorder] Recording stopped: {filePath}");
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[QuestPoseRecorder] Unable to close recording '{filePath}': " +
                    exception.Message);
            }
            finally
            {
                m_Writer = null;
                m_RecordingFilePath = string.Empty;
                m_LastSequenceBySource.Clear();
            }

            return filePath;
        }

        public void Dispose()
        {
            StopRecording();
        }

        private void BuildCsvLine(
            int index,
            DateTime pcCollectionTime,
            double pcTimestampMs,
            uint sourceNetId,
            uint sampleSequence,
            double questTimestampMs,
            double questTimestampInPcClockMs,
            double observedDelayMs,
            Vector3 trackingSpacePosition,
            Vector3 trackingSpaceRotation,
            Vector3 worldPosition,
            Vector3 worldRotation,
            Quaternion trackingSpaceQuaternion,
            Quaternion worldQuaternion,
            Vector3 rawTrackingSpacePosition,
            Quaternion rawTrackingSpaceQuaternion,
            Vector3 rawWorldPosition,
            Quaternion rawWorldQuaternion,
            double poseReadTimeOvrSeconds,
            double rawPoseSampleTimeOvrSeconds,
            double predictedDisplayTimeOvrSeconds,
            float predictionHorizonMs,
            float rawSampleAgeMs,
            int questUnityFrame,
            QuestPoseSamplePhase samplePhase,
            bool rawPoseValid,
            bool renderPoseTimeValid,
            UDPClockSync.ClockSyncSnapshot sync)
        {
            m_LineBuilder.Clear();
            m_LineBuilder.Append(index).Append(',');
            m_LineBuilder.Append(pcCollectionTime.ToString(
                "yyyy-MM-dd HH:mm:ss.fff",
                CultureInfo.InvariantCulture)).Append(',');
            AppendDouble(m_LineBuilder, pcTimestampMs, "F3").Append(',');
            AppendDouble(
                m_LineBuilder,
                Math.Max(0d, pcTimestampMs - m_RecordingStartedAtMs),
                "F3").Append(',');
            m_LineBuilder.Append(Time.frameCount).Append(',');
            m_LineBuilder.Append(sourceNetId).Append(',');
            m_LineBuilder.Append(sampleSequence).Append(',');
            AppendPositiveDouble(m_LineBuilder, questTimestampMs, "F3").Append(',');
            AppendPositiveDouble(
                m_LineBuilder,
                questTimestampInPcClockMs,
                "F3").Append(',');
            AppendFiniteDouble(m_LineBuilder, observedDelayMs, "F3").Append(',');

            AppendVector3(m_LineBuilder, trackingSpacePosition);
            m_LineBuilder.Append(',');
            AppendVector3(m_LineBuilder, trackingSpaceRotation);
            m_LineBuilder.Append(',');
            AppendVector3(m_LineBuilder, worldPosition);
            m_LineBuilder.Append(',');
            AppendVector3(m_LineBuilder, worldRotation);
            m_LineBuilder.Append(',');

            m_LineBuilder.Append(sync.IsReady ? "true" : "false").Append(',');
            m_LineBuilder.Append(sync.State ?? string.Empty).Append(',');
            AppendDouble(m_LineBuilder, sync.ClockDiff, "F3").Append(',');
            AppendNonNegativeDouble(m_LineBuilder, sync.RoundTripTimeMs, "F3").Append(',');
            AppendNonNegativeDouble(m_LineBuilder, sync.AgeMs, "F1").Append(',');
            AppendNonNegativeDouble(
                m_LineBuilder,
                sync.OffsetUncertaintyMs,
                "F3").Append(',');
            m_LineBuilder.Append(sync.SampleCount).Append(',');

            AppendQuaternion(m_LineBuilder, trackingSpaceQuaternion);
            m_LineBuilder.Append(',');
            AppendQuaternion(m_LineBuilder, worldQuaternion);
            m_LineBuilder.Append(',');

            // OVR 单调时钟与 Quest Unix 时钟在 PoseReadTime 处建立一次局部映射。
            // 这不会改动 UDPClockSync 的四时间戳估计算法，只让 raw sample 和
            // render prediction target 可以与现有 Quest Unix 时间列一起分析。
            double rawSampleQuestUnixMs = rawPoseValid
                && IsPositiveFinite(questTimestampMs)
                && IsPositiveFinite(poseReadTimeOvrSeconds)
                && IsPositiveFinite(rawPoseSampleTimeOvrSeconds)
                    ? questTimestampMs
                        + (rawPoseSampleTimeOvrSeconds - poseReadTimeOvrSeconds) * 1000d
                    : double.NaN;
            double predictedDisplayQuestUnixMs = renderPoseTimeValid
                && IsPositiveFinite(questTimestampMs)
                && IsPositiveFinite(poseReadTimeOvrSeconds)
                && IsPositiveFinite(predictedDisplayTimeOvrSeconds)
                    ? questTimestampMs
                        + (predictedDisplayTimeOvrSeconds - poseReadTimeOvrSeconds) * 1000d
                    : double.NaN;

            AppendPositiveDouble(m_LineBuilder, poseReadTimeOvrSeconds, "F9").Append(',');
            AppendPositiveDouble(m_LineBuilder, rawPoseSampleTimeOvrSeconds, "F9").Append(',');
            AppendPositiveDouble(m_LineBuilder, rawSampleQuestUnixMs, "F3").Append(',');
            AppendPositiveDouble(m_LineBuilder, predictedDisplayTimeOvrSeconds, "F9").Append(',');
            AppendPositiveDouble(m_LineBuilder, predictedDisplayQuestUnixMs, "F3").Append(',');
            if (renderPoseTimeValid)
            {
                AppendFiniteDouble(m_LineBuilder, predictionHorizonMs, "F3");
            }
            m_LineBuilder.Append(',');
            if (rawPoseValid && IsPositiveFinite(rawPoseSampleTimeOvrSeconds))
            {
                AppendFiniteDouble(m_LineBuilder, rawSampleAgeMs, "F3");
            }
            m_LineBuilder.Append(',');
            if (questUnityFrame > 0)
            {
                m_LineBuilder.Append(questUnityFrame);
            }
            m_LineBuilder.Append(',');
            m_LineBuilder.Append(GetSamplePhaseName(samplePhase)).Append(',');
            m_LineBuilder.Append(rawPoseValid ? "true" : "false").Append(',');
            m_LineBuilder.Append(renderPoseTimeValid ? "true" : "false").Append(',');

            AppendOptionalVector3(m_LineBuilder, rawTrackingSpacePosition, rawPoseValid);
            m_LineBuilder.Append(',');
            AppendOptionalQuaternion(
                m_LineBuilder,
                rawTrackingSpaceQuaternion,
                rawPoseValid);
            m_LineBuilder.Append(',');
            AppendOptionalVector3(m_LineBuilder, rawWorldPosition, rawPoseValid);
            m_LineBuilder.Append(',');
            AppendOptionalQuaternion(m_LineBuilder, rawWorldQuaternion, rawPoseValid);
        }

        private static StringBuilder AppendVector3(StringBuilder builder, Vector3 value)
        {
            AppendDouble(builder, value.x, "F6").Append(',');
            AppendDouble(builder, value.y, "F6").Append(',');
            return AppendDouble(builder, value.z, "F6");
        }

        private static StringBuilder AppendQuaternion(
            StringBuilder builder,
            Quaternion value)
        {
            if (!IsValidQuaternion(value))
            {
                // Preserve all four CSV columns while clearly marking a sample from
                // an older client that did not supply raw quaternion components.
                return builder.Append(",,,");
            }

            AppendFloat(builder, value.x).Append(',');
            AppendFloat(builder, value.y).Append(',');
            AppendFloat(builder, value.z).Append(',');
            return AppendFloat(builder, value.w);
        }

        private static StringBuilder AppendOptionalVector3(
            StringBuilder builder,
            Vector3 value,
            bool isValid)
        {
            return isValid && IsFinite(value)
                ? AppendVector3(builder, value)
                : builder.Append(",,");
        }

        private static StringBuilder AppendOptionalQuaternion(
            StringBuilder builder,
            Quaternion value,
            bool isValid)
        {
            return isValid
                ? AppendQuaternion(builder, value)
                : builder.Append(",,,");
        }

        private static string GetSamplePhaseName(QuestPoseSamplePhase samplePhase)
        {
            switch (samplePhase)
            {
                case QuestPoseSamplePhase.Update:
                    return "Update";
                case QuestPoseSamplePhase.BeforeRender:
                    return "BeforeRender";
                case QuestPoseSamplePhase.FixedUpdate:
                    return "FixedUpdate";
                default:
                    return "Unknown";
            }
        }

        private static StringBuilder AppendFloat(StringBuilder builder, float value)
        {
            return builder.Append(value.ToString("R", CultureInfo.InvariantCulture));
        }

        private static StringBuilder AppendDouble(
            StringBuilder builder,
            double value,
            string format)
        {
            return builder.Append(value.ToString(format, CultureInfo.InvariantCulture));
        }

        private static StringBuilder AppendFiniteDouble(
            StringBuilder builder,
            double value,
            string format)
        {
            if (!IsFinite(value))
            {
                return builder;
            }
            return AppendDouble(builder, value, format);
        }

        private static StringBuilder AppendPositiveDouble(
            StringBuilder builder,
            double value,
            string format)
        {
            return value > 0d
                ? AppendFiniteDouble(builder, value, format)
                : builder;
        }

        private static StringBuilder AppendNonNegativeDouble(
            StringBuilder builder,
            double value,
            string format)
        {
            return value >= 0d
                ? AppendFiniteDouble(builder, value, format)
                : builder;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x)
                && IsFinite(value.y)
                && IsFinite(value.z)
                && IsFinite(value.w);
        }

        private static bool IsValidQuaternion(Quaternion value)
        {
            if (!IsFinite(value))
            {
                return false;
            }

            float sqrMagnitude = value.x * value.x
                + value.y * value.y
                + value.z * value.z
                + value.w * value.w;
            return IsFinite(sqrMagnitude) && sqrMagnitude > 0.000001f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool IsPositiveFinite(double value)
        {
            return value > 0d && IsFinite(value);
        }

        private void CloseWriterAfterFailure()
        {
            try
            {
                m_Writer?.Dispose();
            }
            catch
            {
                // Preserve the original start-recording exception.
            }
            m_Writer = null;
            m_RecordingFilePath = string.Empty;
        }
    }
}