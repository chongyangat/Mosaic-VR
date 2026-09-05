using GameFramework.Event;
using UnityEngine;
using UnityEngine.Serialization;

namespace GameMain
{
    /// <summary>
    /// 维持 HMD 跟踪重置前后的世界空间视角。
    /// 系统重置事件是强信号，逐帧位姿突变检测作为事件缺失时的后备信号。
    /// </summary>
    [DisallowMultipleComponent]
    public class OVRTrackingResetKeeper : MonoBehaviour
    {
        [Header("Debug")]
        [Tooltip("是否显示逐帧位置日志")]
        [SerializeField] private bool m_IsShowPosLog = false;

        [Header("Discontinuity Detection")]
        [Tooltip("单次位置突变触发阈值（米）")]
        [Min(0.01f)]
        [SerializeField] private float m_TriggerDistance = 0.1f;

        [FormerlySerializedAs("m_AngleTolerance")]
        [Tooltip("单次旋转突变触发阈值（度）")]
        [Range(1f, 180f)]
        [SerializeField] private float m_TriggerAngle = 15f;

        [Tooltip("低帧率时允许的自然头部移动速度（米/秒），用于避免帧卡顿误触发")]
        [Min(0.1f)]
        [SerializeField] private float m_MaxNaturalHeadSpeed = 2f;

        [Tooltip("低帧率时允许的自然头部转动速度（度/秒），用于避免帧卡顿误触发")]
        [Min(1f)]
        [SerializeField] private float m_MaxNaturalAngularSpeed = 240f;

        [Tooltip("旋转角速度的最小突增量（度/秒），用于区分连续转头与单帧重置")]
        [Min(1f)]
        [SerializeField] private float m_MinAngularSpeedJump = 360f;

        [Tooltip("旋转角加速度突变阈值（度/秒²）")]
        [Min(1f)]
        [SerializeField] private float m_TriggerAngularAcceleration = 4000f;

        [Tooltip("超过该角度时视为大幅旋转突变，不要求前一帧角速度较低")]
        [Range(1f, 180f)]
        [SerializeField] private float m_LargeRotationJumpAngle = 45f;

        [Header("Timing")]
        [Tooltip("启动或重新获得跟踪后延迟多少秒开始检测")]
        [Min(0f)]
        [SerializeField] private float m_StartCheckDelay = 1f;

        [Tooltip("需要连续稳定的时间（秒）")]
        [Min(0.05f)]
        [SerializeField] private float m_StabilizeWaitTime = 0.5f;

        [Tooltip("连续稳定检测的单帧位置变化阈值（米）")]
        [Min(0.0001f)]
        [SerializeField] private float m_StabilizePositionThreshold = 0.003f;

        [Tooltip("连续稳定检测的单帧旋转变化阈值（度）")]
        [Min(0.05f)]
        [SerializeField] private float m_StabilizeAngleThreshold = 1f;

        [Tooltip("待补偿状态的最大持续时间（秒），超时后放弃本次补偿，避免延迟跳变")]
        [Min(0.5f)]
        [SerializeField] private float m_MaxPendingDuration = 5f;

        [Tooltip("同一次 Meta 重置产生的连续参考空间事件必须安静多久才开始补偿（秒）")]
        [Min(0.1f)]
        [SerializeField] private float m_RecenterEventQuietPeriod = 1.25f;

        [Header("Intent Suppression")]
        [Tooltip("用户手动重置后忽略位姿跳变的时间（秒）")]
        [Min(0.1f)]
        [SerializeField] private float m_ManualRecenterSuppression = 2.5f;

        [Tooltip("传送后忽略位姿跳变的时间（秒）")]
        [Min(0.1f)]
        [SerializeField] private float m_TeleportSuppression = 0.75f;

        [Tooltip("系统手势结束后仍视为手动重置的宽限时间（秒）")]
        [Min(0.05f)]
        [SerializeField] private float m_SystemGestureGracePeriod = 3f;

        [Tooltip("Continuous stable time required after a Meta system recenter before avatar alignment (seconds)")]
        [Range(0.5f, 1f)]
        [SerializeField] private float m_SystemRecenterStabilizeWaitTime = 0.75f;

        [Tooltip("任何一次重置、显式对齐或抑制操作后的统一冷却时间（秒）")]
        [Min(0.1f)]
        [SerializeField] private float m_ResetCooldownDuration = 2f;

        [Header("Level Safety")]
        [Tooltip("TrackingSpace 相对世界竖直方向允许的最大倾斜角；超过后自动拉平（度）")]
        [Range(0.1f, 10f)]
        [SerializeField] private float m_MaxTrackingSpaceTilt = 0.5f;

        [Tooltip("参考空间事件的水平位移小于此值时，可视为地面高度更新（米）")]
        [Min(0.001f)]
        [SerializeField] private float m_FloorUpdateHorizontalTolerance = 0.03f;

        [Tooltip("参考空间事件的水平旋转小于此值时，可视为地面高度更新（度）")]
        [Range(0.1f, 10f)]
        [SerializeField] private float m_FloorUpdateYawTolerance = 2f;

        [Header("Setup")]
        [Tooltip("双眼视角锚点")]
        [SerializeField] private Transform m_CenterEyeAnchor;

        [Tooltip("跟踪空间，必须是双眼视角锚点的父级")]
        [SerializeField] private Transform m_TrackingSpace;

        [Tooltip("左手；未启用手势时可为空")]
        [SerializeField] private OVRHand m_LeftHand;

        [Tooltip("右手；未启用手势时可为空")]
        [SerializeField] private OVRHand m_RightHand;

#if ENABLED_OVR_TRACKING_RESET_KEEPER || UNITY_EDITOR

        private enum DetectionState
        {
            Disabled,
            Warmup,
            Monitoring,
            WaitingForStability,
            WaitingForSystemRecenterAlignment,
            Cooldown,
            TrackingLost
        }

        private enum ResetDetectionSource
        {
            None,
            PoseDiscontinuity,
            OvrRecenterEvent,
            ManualSystemRecenter,
            Teleport,
            ExplicitAvatarAlignment
        }

        private enum ResetOutcome
        {
            None,
            Pending,
            Applied,
            Suppressed,
            Cancelled,
            TimedOut
        }

        private struct RotationDiscontinuityResult
        {
            public bool IsDetected;
            public float DeltaAngle;
            public float AngularSpeed;
            public float PreviousAngularSpeed;
            public float AngularSpeedJump;
            public float AngularAcceleration;
            public float DynamicAngleThreshold;
            public Vector3 RotationAxis;
        }

        /// <summary>
        /// Captures the complete lifecycle of the most recent reset candidate. Keeping the
        /// reference and observed poses together makes event-order and false-positive issues
        /// diagnosable from a single log entry.
        /// </summary>
        private struct ResetStateData
        {
            public int Sequence;
            public DetectionState StateAtDetection;
            public ResetDetectionSource Source;
            public ResetOutcome Outcome;
            public float DetectedAt;
            public float CompletedAt;
            public Vector3 ReferencePosition;
            public Quaternion ReferenceRotation;
            public Vector3 ObservedPosition;
            public Quaternion ObservedRotation;
            public Vector3 TargetPosition;
            public Quaternion TargetRotation;
            public float PositionDelta;
            public float RotationDelta;
            public float AngularSpeed;
            public float AngularAcceleration;
            public float DynamicRotationThreshold;
            public Vector3 RotationAxis;
            public float ResidualPosition;
            public float ResidualRotation;
            public string Reason;
        }

        private const float EventPreviousFramePositionEpsilon = 0.005f;
        private const float EventPreviousFrameAngleEpsilon = 1f;

        private Vector3 m_LastCenterEyeAnchorPosition;
        private Quaternion m_LastCenterEyeAnchorRotation = Quaternion.identity;
        private Vector3 m_PreviousCenterEyeAnchorPosition;
        private Quaternion m_PreviousCenterEyeAnchorRotation = Quaternion.identity;

        private bool m_IsEnable;
        private bool m_ReferencesValid;
        private bool m_LastHeadsetTracked;
        private bool m_GameEventsSubscribed;
        private bool m_RecenterEventSubscribed;
        private DetectionState m_State = DetectionState.Disabled;

        private float m_EnableTime;
        private float m_CooldownUntilTime;
        private float m_PendingStartTime;
        private float m_PendingLastSignalTime = float.NegativeInfinity;
        private float m_StableSinceTime;
        private float m_LastReferenceSampleTime;
        private float m_PreviousReferenceSampleTime;
        private float m_LastDominantSystemGestureTime = float.NegativeInfinity;
        private Vector3 m_LastStableSamplePosition;
        private Quaternion m_LastStableSampleRotation = Quaternion.identity;
        private Vector3 m_LastValidTrackingSpacePosition;
        private Quaternion m_LastValidTrackingSpaceRotation = Quaternion.identity;
        private bool m_HasLastValidTrackingSpacePose;
        private float m_NextInvalidPoseWarningTime;
        private string m_PendingReason = string.Empty;
        private int m_ResetSequence;
        private ResetStateData m_ResetState;

        #region Public control

        /// <summary>
        /// 使用当前 HMD 位姿作为基准，清理所有旧状态并重新开始检测。
        /// </summary>
        public void Restart()
        {
            if (!EnsureReferences())
            {
                return;
            }

            m_IsEnable = true;
            ClearTransientState();
            InitializeReferencePose();
            m_EnableTime = Time.unscaledTime;
            m_LastHeadsetTracked = IsHeadsetTracked();
            m_State = m_LastHeadsetTracked ? DetectionState.Warmup : DetectionState.TrackingLost;
        }

        public void Pause()
        {
            m_IsEnable = false;
            ClearTransientState();
            m_State = DetectionState.Disabled;
        }

        /// <summary>
        /// 使用当前 HMD 位姿作为新基准恢复检测。
        /// </summary>
        public void Resume()
        {
            ResumeInternal(false);
        }

        private void ResumeInternal(bool preserveProvidedReference)
        {
            if (!EnsureReferences())
            {
                return;
            }

            m_IsEnable = true;
            ClearTransientState();
            m_EnableTime = Time.unscaledTime;
            m_LastHeadsetTracked = IsHeadsetTracked();

            if (!preserveProvidedReference)
            {
                InitializeReferencePose();
            }

            if (!m_LastHeadsetTracked)
            {
                m_State = DetectionState.TrackingLost;
            }
            else
            {
                // 外部传入的参考位姿（例如动捕眼位）需要立即参与补偿，不能被 warmup 覆盖。
                m_State = preserveProvidedReference
                    ? DetectionState.Monitoring
                    : DetectionState.Warmup;
            }
        }

        #endregion

        #region Game events

        private void OnSetOVRTrackingRestore(object sender, GameEventArgs e)
        {
            var args = e as SetOVRTrackingRestoreEventArgs;
            if (args == null)
            {
                return;
            }

            if (!args.IsEnable)
            {
                Pause();
                return;
            }

            bool hasProvidedReference = args.Position.HasValue || args.Rotation.HasValue;
            m_IsShowPosLog = args.IsShowPositionLog;

            // A caller-provided pose is an explicit alignment command (for example, the
            // midpoint and facing direction of the motion-capture avatar). It must not go
            // through the normal discontinuity thresholds: a small error below 10 cm/15
            // degrees is still visible and is precisely what the manual adjustment is
            // expected to remove.
            if (hasProvidedReference)
            {
                ApplyExplicitEyePose(args.Position, args.Rotation);
                return;
            }

            if (args.IsRestart)
            {
                Restart();
                return;
            }

            ResumeInternal(false);
        }

        private void OnTeleportPlayer(object sender, GameEventArgs e)
        {
            if (!(e is TeleportPlayerEventArgs) || !m_IsEnable || !EnsureReferences())
            {
                return;
            }

            // 事件可能先于或晚于 CameraRig 真正移动。抑制期间每帧跟随更新基准，
            // 因而两种执行顺序都不会把合法传送误判为跟踪重置。
            BeginCooldown(m_TeleportSuppression, ResetDetectionSource.Teleport, "teleport", true);
        }

        #endregion

        #region Unity lifecycle

        private void Awake()
        {
            m_ReferencesValid = ValidateReferences(true);
        }

        private void OnEnable()
        {
            m_ReferencesValid = ValidateReferences(true);
            m_IsEnable = false;
            ClearTransientState();
            m_State = DetectionState.Disabled;

            if (m_ReferencesValid)
            {
                CacheTrackingSpacePoseIfValid();
                if (HasValidPose(m_CenterEyeAnchor))
                {
                    InitializeReferencePose();
                    m_LastHeadsetTracked = IsHeadsetTracked();
                }
                else
                {
                    m_LastHeadsetTracked = false;
                    m_State = DetectionState.TrackingLost;
                }
            }

            TrySubscribeEvents();
        }

        private void OnDisable()
        {
#if !UNITY_EDITOR
            if (m_RecenterEventSubscribed && OVRManager.display != null)
            {
                OVRManager.display.RecenteredPose -= OnRecenteredPose;
            }
#endif
            m_RecenterEventSubscribed = false;

            if (m_GameEventsSubscribed && GameModule.Event != null)
            {
                GameModule.Event.Unsubscribe(SetOVRTrackingRestoreEventArgs.EventId, OnSetOVRTrackingRestore);
                GameModule.Event.Unsubscribe(TeleportPlayerEventArgs.EventId, OnTeleportPlayer);
                GameModule.Event.Unsubscribe(
                    RequestOVRSystemRecenterEventArgs.EventId,
                    OnRequestOVRSystemRecenter);
            }

            m_GameEventsSubscribed = false;
            ClearTransientState();
            m_IsEnable = false;
            m_State = DetectionState.Disabled;
        }

        private void OnValidate()
        {
            m_TriggerDistance = Mathf.Max(0.01f, m_TriggerDistance);
            m_TriggerAngle = Mathf.Clamp(m_TriggerAngle, 1f, 180f);
            m_MaxNaturalHeadSpeed = Mathf.Max(0.1f, m_MaxNaturalHeadSpeed);
            m_MaxNaturalAngularSpeed = Mathf.Max(1f, m_MaxNaturalAngularSpeed);
            m_MinAngularSpeedJump = Mathf.Max(1f, m_MinAngularSpeedJump);
            m_TriggerAngularAcceleration = Mathf.Max(1f, m_TriggerAngularAcceleration);
            m_LargeRotationJumpAngle = Mathf.Clamp(m_LargeRotationJumpAngle, m_TriggerAngle, 180f);
            m_StartCheckDelay = Mathf.Max(0f, m_StartCheckDelay);
            m_StabilizeWaitTime = Mathf.Max(0.05f, m_StabilizeWaitTime);
            m_StabilizePositionThreshold = Mathf.Max(0.0001f, m_StabilizePositionThreshold);
            m_StabilizeAngleThreshold = Mathf.Max(0.05f, m_StabilizeAngleThreshold);
            m_MaxPendingDuration = Mathf.Max(m_StabilizeWaitTime, m_MaxPendingDuration);
            m_RecenterEventQuietPeriod = Mathf.Max(0.1f, m_RecenterEventQuietPeriod);
            m_ManualRecenterSuppression = Mathf.Max(0.1f, m_ManualRecenterSuppression);
            m_TeleportSuppression = Mathf.Max(0.1f, m_TeleportSuppression);
            m_SystemGestureGracePeriod = Mathf.Max(0.05f, m_SystemGestureGracePeriod);
            m_SystemRecenterStabilizeWaitTime = Mathf.Clamp(
                m_SystemRecenterStabilizeWaitTime,
                0.5f,
                1f);
            m_ResetCooldownDuration = Mathf.Max(0.1f, m_ResetCooldownDuration);
            m_MaxTrackingSpaceTilt = Mathf.Clamp(m_MaxTrackingSpaceTilt, 0.1f, 10f);
            m_FloorUpdateHorizontalTolerance = Mathf.Max(0.001f, m_FloorUpdateHorizontalTolerance);
            m_FloorUpdateYawTolerance = Mathf.Clamp(m_FloorUpdateYawTolerance, 0.1f, 10f);
        }

        private void Update()
        {
            TrySubscribeEvents();

            if (!m_IsEnable || !EnsureReferences())
            {
                return;
            }

            UpdateDominantSystemGestureTimestamp();

            bool isTracked = IsHeadsetTracked();
            if (!isTracked)
            {
                HandleTrackingLost();
                return;
            }

            // Meta tracking can briefly report tracked while exposing an invalid
            // pose during proximity/focus/recenter transitions. Never feed such a
            // sample into compensation math or the TrackingSpace transform.
            if (!ValidateCurrentTrackingPoses())
            {
                HandleTrackingLost();
                return;
            }

            if (!m_LastHeadsetTracked || m_State == DetectionState.TrackingLost)
            {
                HandleTrackingRecovered();
                return;
            }

            m_LastHeadsetTracked = true;
            float now = Time.unscaledTime;

            if (m_State == DetectionState.Cooldown)
            {
                UpdateReferencePose();
                if (now >= m_CooldownUntilTime)
                {
                    Debug.Log($"[HMDResetKeeper] Cooldown completed; monitoring resumed after reset #{m_ResetState.Sequence}.");
                    m_State = DetectionState.Monitoring;
                }
                return;
            }

            if (m_State == DetectionState.Warmup)
            {
                UpdateReferencePose();
                if (now >= m_EnableTime + m_StartCheckDelay)
                {
                    m_State = DetectionState.Monitoring;
                }
                return;
            }

            if (m_State == DetectionState.WaitingForStability)
            {
                ProcessPendingCompensation();
                return;
            }

            if (m_State == DetectionState.WaitingForSystemRecenterAlignment)
            {
                ProcessSystemRecenterAlignment();
                return;
            }

            if (m_State != DetectionState.Monitoring)
            {
                return;
            }

            // A Meta system gesture can temporarily change the tracking reference and can
            // also stall app frames while the system UI is visible. Treating the resulting
            // eye-pose delta as an automatic reset makes TrackingSpace cancel real head
            // movement, which feels as if the whole scene is attached to the headset.
            // RecenteredPose is the authoritative signal for this path; until its grace
            // period ends, only refresh the reference and never run fallback compensation.
            if (WasRecentDominantSystemGesture())
            {
                UpdateReferencePose();
                return;
            }

            if (CorrectTrackingSpaceTiltIfNeeded())
            {
                return;
            }

            float positionDelta;
            RotationDiscontinuityResult rotationResult;
            if (ShouldResetTracking(out positionDelta, out rotationResult))
            {
                StartPendingCompensation(
                    ResetDetectionSource.PoseDiscontinuity,
                    $"pose discontinuity (position={positionDelta:F3}m, "
                    + $"angle={rotationResult.DeltaAngle:F1}deg, "
                    + $"angularSpeed={rotationResult.AngularSpeed:F1}deg/s, "
                    + $"angularAcceleration={rotationResult.AngularAcceleration:F1}deg/s2)",
                    positionDelta,
                    rotationResult);
                return;
            }

            if (m_IsShowPosLog)
            {
                Debug.Log(
                    $"[HMDResetKeeper] state={m_State}, eye={m_CenterEyeAnchor.position}, "
                    + $"trackingSpace={m_TrackingSpace.position}");
            }

            UpdateReferencePose();
        }

        #endregion

        #region Subscription and validation

        private void TrySubscribeEvents()
        {
            if (!m_GameEventsSubscribed && GameModule.Event != null)
            {
                GameModule.Event.Subscribe(SetOVRTrackingRestoreEventArgs.EventId, OnSetOVRTrackingRestore);
                GameModule.Event.Subscribe(TeleportPlayerEventArgs.EventId, OnTeleportPlayer);
                GameModule.Event.Subscribe(
                    RequestOVRSystemRecenterEventArgs.EventId,
                    OnRequestOVRSystemRecenter);
                m_GameEventsSubscribed = true;
            }

#if !UNITY_EDITOR
            if (!m_RecenterEventSubscribed && OVRManager.display != null)
            {
                OVRManager.display.RecenteredPose += OnRecenteredPose;
                m_RecenterEventSubscribed = true;
            }
#endif
        }

        private bool EnsureReferences()
        {
            if (!m_ReferencesValid)
            {
                m_ReferencesValid = ValidateReferences(false);
            }
            return m_ReferencesValid;
        }

        private bool ValidateReferences(bool logErrors)
        {
            bool valid = m_CenterEyeAnchor != null && m_TrackingSpace != null;
            if (!valid)
            {
                if (logErrors)
                {
                    Debug.LogError("[HMDResetKeeper] CenterEyeAnchor or TrackingSpace is missing.", this);
                }
                return false;
            }

            if (!m_CenterEyeAnchor.IsChildOf(m_TrackingSpace) && logErrors)
            {
                Debug.LogWarning(
                    "[HMDResetKeeper] CenterEyeAnchor is not a child of TrackingSpace; "
                    + "rigid compensation may not preserve the eye pose.", this);
            }
            return true;
        }

        #endregion

        #region Tracking state and intent

        private bool IsHeadsetTracked()
        {
#if UNITY_EDITOR
            return true;
#else
            return OVRPlugin.initialized
                && OVRPlugin.GetNodePositionTracked(OVRPlugin.Node.EyeCenter)
                && OVRPlugin.GetNodeOrientationTracked(OVRPlugin.Node.EyeCenter);
#endif
        }

        private void HandleTrackingLost()
        {
            if (m_LastHeadsetTracked || m_State != DetectionState.TrackingLost)
            {
                Debug.Log("[HMDResetKeeper] HMD tracking lost; pending compensation cancelled.");
            }

            if (m_ResetState.Outcome == ResetOutcome.Pending)
            {
                CompleteResetRecord(ResetOutcome.Cancelled);
            }
            m_LastHeadsetTracked = false;
            ClearTransientState();
            m_State = DetectionState.TrackingLost;
        }

        private void HandleTrackingRecovered()
        {
            Debug.Log("[HMDResetKeeper] HMD tracking recovered; reference pose reinitialized.");
            m_LastHeadsetTracked = true;
            ClearTransientState();
            InitializeReferencePose();
            m_EnableTime = Time.unscaledTime;
            m_State = DetectionState.Warmup;
        }

        private bool ValidateCurrentTrackingPoses()
        {
            bool trackingSpaceValid = HasValidPose(m_TrackingSpace);
            if (trackingSpaceValid)
            {
                CacheTrackingSpacePoseIfValid();
            }
            else if (m_HasLastValidTrackingSpacePose)
            {
                m_TrackingSpace.SetPositionAndRotation(
                    m_LastValidTrackingSpacePosition,
                    m_LastValidTrackingSpaceRotation);
            }

            bool eyePoseValid = HasValidPose(m_CenterEyeAnchor);
            if (!trackingSpaceValid || !eyePoseValid)
            {
                WarnInvalidTrackingPose(trackingSpaceValid, eyePoseValid);
                return false;
            }

            return true;
        }

        private void CacheTrackingSpacePoseIfValid()
        {
            if (!HasValidPose(m_TrackingSpace))
            {
                return;
            }

            m_LastValidTrackingSpacePosition = m_TrackingSpace.position;
            m_LastValidTrackingSpaceRotation = m_TrackingSpace.rotation;
            m_HasLastValidTrackingSpacePose = true;
        }

        private void WarnInvalidTrackingPose(bool trackingSpaceValid, bool eyePoseValid)
        {
            if (Time.unscaledTime < m_NextInvalidPoseWarningTime)
            {
                return;
            }

            m_NextInvalidPoseWarningTime = Time.unscaledTime + 5f;
            Debug.LogWarning(
                $"[HMDResetKeeper] Ignored invalid tracking pose. "
                + $"trackingSpaceValid={trackingSpaceValid}, eyePoseValid={eyePoseValid}.",
                this);
        }

        private void UpdateDominantSystemGestureTimestamp()
        {
            if (IsDominantSystemGestureActive(m_LeftHand)
                || IsDominantSystemGestureActive(m_RightHand))
            {
                m_LastDominantSystemGestureTime = Time.unscaledTime;
            }
        }

        private static bool IsDominantSystemGestureActive(OVRHand hand)
        {
            return hand != null
                && hand.IsValid()
                && hand.IsDataValid
                && hand.IsTracked
                && hand.IsDominantHand
                && hand.IsSystemGestureInProgress;
        }

        private bool WasRecentDominantSystemGesture()
        {
            return Time.unscaledTime - m_LastDominantSystemGestureTime
                <= m_SystemGestureGracePeriod;
        }

        /// <summary>
        /// Performs the same local Meta recenter used by the headset UI, but marks it as an
        /// intentional gesture-equivalent operation first. OVRDisplay raises RecenteredPose
        /// on the following frame, after the cached XR pose has been refreshed.
        /// </summary>
        private void OnRequestOVRSystemRecenter(object sender, GameEventArgs e)
        {
            var args = e as RequestOVRSystemRecenterEventArgs;
            if (args == null || !EnsureReferences())
            {
                return;
            }

            if (!m_IsEnable)
            {
                Restart();
            }

            m_LastHeadsetTracked = IsHeadsetTracked();
            if (!m_LastHeadsetTracked || !ValidateCurrentTrackingPoses())
            {
                Debug.LogWarning(
                    $"[HMDResetKeeper] Remote system recenter ignored because HMD tracking "
                    + $"is not currently valid (reason={args.Reason ?? "unspecified"}).");
                return;
            }

            // Supersede an older cooldown/candidate. The manager command is already debounced,
            // and this operation must be allowed even shortly after scene-entry teleport.
            ClearTransientState();
            InitializeReferencePose();
            m_State = DetectionState.Monitoring;
            m_LastDominantSystemGestureTime = Time.unscaledTime;

            if (OVRManager.display == null)
            {
                Debug.LogWarning(
                    "[HMDResetKeeper] OVRDisplay is unavailable; falling back to direct "
                    + "avatar view alignment.");
                GameModule.Event.Fire(
                    RequestAvatarViewAlignmentEventArgs.EventId,
                    RequestAvatarViewAlignmentEventArgs.Create(
                        args.Reason ?? "remote recenter fallback"));
                return;
            }

            Debug.Log(
                $"[HMDResetKeeper] Remote Meta system recenter requested "
                + $"(reason={args.Reason ?? "unspecified"}).");
            OVRManager.display.RecenterPose();
        }

        private bool CorrectTrackingSpaceTiltIfNeeded()
        {
            float tiltBefore = GetTrackingSpaceTiltAngle();
            if (tiltBefore <= m_MaxTrackingSpaceTilt)
            {
                return false;
            }

            Vector3 correctionAxis = Vector3.Cross(m_TrackingSpace.up, Vector3.up);
            if (correctionAxis.sqrMagnitude < 0.0001f)
            {
                correctionAxis = Vector3.forward;
            }
            else
            {
                correctionAxis.Normalize();
            }

            RotationDiscontinuityResult rotationResult = new RotationDiscontinuityResult
            {
                IsDetected = true,
                DeltaAngle = tiltBefore,
                RotationAxis = correctionAxis
            };
            BeginResetRecord(
                ResetDetectionSource.PoseDiscontinuity,
                "tracking-space level guard",
                0f,
                rotationResult);

            Vector3 eyePosition = m_CenterEyeAnchor.position;
            Quaternion eyeRotation = m_CenterEyeAnchor.rotation;
            MapCurrentEyePoseToTarget(eyePosition, eyeRotation);
            InitializeReferencePose();

            float tiltAfter = GetTrackingSpaceTiltAngle();
            CompleteResetRecord(
                ResetOutcome.Applied,
                Vector3.Distance(m_CenterEyeAnchor.position, eyePosition),
                tiltAfter);
            Debug.LogWarning(
                $"[HMDResetKeeper] TrackingSpace tilt guard corrected "
                + $"{tiltBefore:F2}deg -> {tiltAfter:F2}deg while preserving eye position and yaw.");
            BeginCooldown(
                m_ResetCooldownDuration,
                ResetDetectionSource.PoseDiscontinuity,
                "tracking-space level guard",
                false);
            return true;
        }

        private void BeginCooldown(
            float requestedDuration,
            ResetDetectionSource source,
            string reason,
            bool recordSuppressedReset)
        {
            if (m_ResetState.Outcome == ResetOutcome.Pending)
            {
                CompleteResetRecord(ResetOutcome.Cancelled);
            }

            if (recordSuppressedReset)
            {
                BeginResetRecord(
                    source,
                    reason,
                    Vector3.Distance(m_CenterEyeAnchor.position, m_LastCenterEyeAnchorPosition),
                    CreateRotationDiscontinuityResult(m_CenterEyeAnchor.rotation));
                CompleteResetRecord(ResetOutcome.Suppressed);
            }

            ClearPendingCompensation();
            float duration = Mathf.Max(requestedDuration, m_ResetCooldownDuration);
            m_CooldownUntilTime = Mathf.Max(m_CooldownUntilTime, Time.unscaledTime + duration);
            m_State = DetectionState.Cooldown;
            Debug.Log(
                $"[HMDResetKeeper] Cooldown entered for {duration:F2}s "
                + $"(source={source}, reason={reason}, reset=#{m_ResetState.Sequence}).");
        }

        private void BeginResetRecord(
            ResetDetectionSource source,
            string reason,
            float positionDelta,
            RotationDiscontinuityResult rotationResult)
        {
            m_ResetSequence++;
            m_ResetState = new ResetStateData
            {
                Sequence = m_ResetSequence,
                StateAtDetection = m_State,
                Source = source,
                Outcome = ResetOutcome.Pending,
                DetectedAt = Time.unscaledTime,
                CompletedAt = 0f,
                ReferencePosition = m_LastCenterEyeAnchorPosition,
                ReferenceRotation = m_LastCenterEyeAnchorRotation,
                ObservedPosition = m_CenterEyeAnchor.position,
                ObservedRotation = m_CenterEyeAnchor.rotation,
                TargetPosition = m_LastCenterEyeAnchorPosition,
                TargetRotation = m_LastCenterEyeAnchorRotation,
                PositionDelta = positionDelta,
                RotationDelta = rotationResult.DeltaAngle,
                AngularSpeed = rotationResult.AngularSpeed,
                AngularAcceleration = rotationResult.AngularAcceleration,
                DynamicRotationThreshold = rotationResult.DynamicAngleThreshold,
                RotationAxis = rotationResult.RotationAxis,
                ResidualPosition = 0f,
                ResidualRotation = 0f,
                Reason = reason
            };
        }

        private void CompleteResetRecord(
            ResetOutcome outcome,
            float residualPosition = 0f,
            float residualRotation = 0f)
        {
            m_ResetState.Outcome = outcome;
            m_ResetState.CompletedAt = Time.unscaledTime;
            m_ResetState.ResidualPosition = residualPosition;
            m_ResetState.ResidualRotation = residualRotation;
            Debug.Log(
                $"[HMDResetKeeper] Reset #{m_ResetState.Sequence}: "
                + $"source={m_ResetState.Source}, outcome={m_ResetState.Outcome}, "
                + $"positionDelta={m_ResetState.PositionDelta:F3}m, "
                + $"rotationDelta={m_ResetState.RotationDelta:F1}deg, "
                + $"angularSpeed={m_ResetState.AngularSpeed:F1}deg/s, "
                + $"angularAcceleration={m_ResetState.AngularAcceleration:F1}deg/s2, "
                + $"rotationThreshold={m_ResetState.DynamicRotationThreshold:F1}deg, "
                + $"rotationAxis={m_ResetState.RotationAxis}, "
                + $"residualPosition={m_ResetState.ResidualPosition:F4}m, "
                + $"residualRotation={m_ResetState.ResidualRotation:F2}deg, "
                + $"reason={m_ResetState.Reason}.");
        }

        #endregion

        #region Recenter detection

        /// <summary>
        /// Meta 事件与 CenterEyeAnchor 更新的先后顺序不固定，因此保留前两帧参考位姿，
        /// 并在事件后等待连续稳定再应用补偿。
        /// </summary>
        private void OnRecenteredPose()
        {
            if (!m_IsEnable || !EnsureReferences())
            {
                return;
            }

            if (!ValidateCurrentTrackingPoses())
            {
                return;
            }

            float now = Time.unscaledTime;

            // A Meta recenter may emit a batch of RecenteredPose callbacks. Once the first
            // callback has completed alignment, later callbacks must not restart the same
            // operation during cooldown; repeated TrackingSpace writes are perceived as a
            // head-locked or "sticky" scene.
            if (m_State == DetectionState.Warmup
                || m_State == DetectionState.Cooldown
                || m_State == DetectionState.TrackingLost)
            {
                return;
            }

            UpdateDominantSystemGestureTimestamp();
            if (WasRecentDominantSystemGesture())
            {
                StartSystemRecenterAlignment();
                return;
            }

            if (m_State == DetectionState.WaitingForSystemRecenterAlignment)
            {
                m_PendingLastSignalTime = now;
                m_StableSinceTime = now;
                m_LastStableSamplePosition = m_CenterEyeAnchor.position;
                m_LastStableSampleRotation = m_CenterEyeAnchor.rotation;
                return;
            }

            if (m_State == DetectionState.WaitingForStability)
            {
                m_PendingLastSignalTime = now;
                m_StableSinceTime = now;
                return;
            }

            Vector3 currentPosition = m_CenterEyeAnchor.position;
            Quaternion currentRotation = m_CenterEyeAnchor.rotation;
            float lastPositionDelta = Vector3.Distance(currentPosition, m_LastCenterEyeAnchorPosition);
            float lastAngleDelta = Quaternion.Angle(currentRotation, m_LastCenterEyeAnchorRotation);
            float previousPositionDelta = Vector3.Distance(currentPosition, m_PreviousCenterEyeAnchorPosition);
            float previousAngleDelta = Quaternion.Angle(currentRotation, m_PreviousCenterEyeAnchorRotation);

            // 若事件在本帧 Update 之后到达，Last 已经是重置后的位姿，而 Previous 仍是重置前位姿。
            bool previousLooksLikePreReset =
                previousPositionDelta > lastPositionDelta + EventPreviousFramePositionEpsilon
                || previousAngleDelta > lastAngleDelta + EventPreviousFrameAngleEpsilon;
            if (previousLooksLikePreReset)
            {
                m_LastCenterEyeAnchorPosition = m_PreviousCenterEyeAnchorPosition;
                m_LastCenterEyeAnchorRotation = m_PreviousCenterEyeAnchorRotation;
                m_LastReferenceSampleTime = m_PreviousReferenceSampleTime;
            }

            Vector3 referenceOffset = currentPosition - m_LastCenterEyeAnchorPosition;
            float horizontalDelta = new Vector2(referenceOffset.x, referenceOffset.z).magnitude;
            float yawDelta = GetHorizontalRotationAngle(
                currentRotation,
                m_LastCenterEyeAnchorRotation);
            if (horizontalDelta <= m_FloorUpdateHorizontalTolerance
                && yawDelta <= m_FloorUpdateYawTolerance)
            {
                BeginCooldown(
                    m_RecenterEventQuietPeriod,
                    ResetDetectionSource.OvrRecenterEvent,
                    "floor/reference-space update without horizontal discontinuity",
                    true);
                InitializeReferencePose();
                return;
            }

            RotationDiscontinuityResult rotationResult =
                CreateRotationDiscontinuityResult(currentRotation);
            StartPendingCompensation(
                ResetDetectionSource.OvrRecenterEvent,
                "OVR RecenteredPose event",
                Vector3.Distance(currentPosition, m_LastCenterEyeAnchorPosition),
                rotationResult);
        }

        private bool ShouldResetTracking(
            out float positionDelta,
            out RotationDiscontinuityResult rotationResult)
        {
            positionDelta = Vector3.Distance(
                m_CenterEyeAnchor.position,
                m_LastCenterEyeAnchorPosition);
            rotationResult = CreateRotationDiscontinuityResult(m_CenterEyeAnchor.rotation);

            float frameDuration = GetCurrentReferenceSampleDuration();
            float positionThreshold = Mathf.Max(
                m_TriggerDistance,
                m_MaxNaturalHeadSpeed * frameDuration);

            return positionDelta >= positionThreshold || rotationResult.IsDetected;
        }

        private RotationDiscontinuityResult CreateRotationDiscontinuityResult(
            Quaternion currentRotation)
        {
            float currentDuration = GetCurrentReferenceSampleDuration();
            float previousDuration = GetPreviousReferenceSampleDuration(currentDuration);
            float deltaAngle = Quaternion.Angle(currentRotation, m_LastCenterEyeAnchorRotation);
            float previousDeltaAngle = Quaternion.Angle(
                m_LastCenterEyeAnchorRotation,
                m_PreviousCenterEyeAnchorRotation);
            float angularSpeed = deltaAngle / currentDuration;
            float previousAngularSpeed = previousDeltaAngle / previousDuration;
            float angularSpeedJump = Mathf.Max(0f, angularSpeed - previousAngularSpeed);
            float angularAcceleration = angularSpeedJump / currentDuration;
            float dynamicAngleThreshold = Mathf.Max(
                m_TriggerAngle,
                m_MaxNaturalAngularSpeed * currentDuration);

            Quaternion deltaRotation = currentRotation
                * Quaternion.Inverse(m_LastCenterEyeAnchorRotation);
            deltaRotation.ToAngleAxis(out _, out Vector3 rotationAxis);
            if (!IsFinite(rotationAxis) || rotationAxis.sqrMagnitude < 0.0001f)
            {
                rotationAxis = Vector3.up;
            }
            else
            {
                rotationAxis.Normalize();
            }

            bool exceedsBaseThreshold = deltaAngle >= dynamicAngleThreshold
                && angularSpeed >= m_MaxNaturalAngularSpeed;
            bool hasAbruptVelocityChange = angularSpeedJump >= m_MinAngularSpeedJump
                && angularAcceleration >= m_TriggerAngularAcceleration;
            bool isLargeSingleFrameJump = deltaAngle >= m_LargeRotationJumpAngle;

            return new RotationDiscontinuityResult
            {
                IsDetected = exceedsBaseThreshold
                    && (hasAbruptVelocityChange || isLargeSingleFrameJump),
                DeltaAngle = deltaAngle,
                AngularSpeed = angularSpeed,
                PreviousAngularSpeed = previousAngularSpeed,
                AngularSpeedJump = angularSpeedJump,
                AngularAcceleration = angularAcceleration,
                DynamicAngleThreshold = dynamicAngleThreshold,
                RotationAxis = rotationAxis
            };
        }

        private float GetCurrentReferenceSampleDuration()
        {
            float duration = m_LastReferenceSampleTime > 0f
                ? Time.unscaledTime - m_LastReferenceSampleTime
                : Time.unscaledDeltaTime;
            return Mathf.Clamp(duration, 1f / 120f, 0.1f);
        }

        private float GetPreviousReferenceSampleDuration(float fallbackDuration)
        {
            float duration = m_PreviousReferenceSampleTime > 0f
                ? m_LastReferenceSampleTime - m_PreviousReferenceSampleTime
                : fallbackDuration;
            return Mathf.Clamp(duration, 1f / 120f, 0.1f);
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z)
                && !float.IsNaN(value.w) && !float.IsInfinity(value.w);
        }

        private static bool IsValidRotation(Quaternion value)
        {
            if (!IsFinite(value))
            {
                return false;
            }

            float sqrMagnitude = value.x * value.x
                + value.y * value.y
                + value.z * value.z
                + value.w * value.w;
            return !float.IsNaN(sqrMagnitude)
                && !float.IsInfinity(sqrMagnitude)
                && sqrMagnitude > 0.000001f;
        }

        private static bool HasValidPose(Transform pose)
        {
            return pose != null
                && IsFinite(pose.position)
                && IsValidRotation(pose.rotation);
        }

        #endregion

        #region Continuous stability and compensation

        private void StartSystemRecenterAlignment()
        {
            float now = Time.unscaledTime;
            if (m_State == DetectionState.WaitingForSystemRecenterAlignment)
            {
                m_PendingLastSignalTime = now;
                m_StableSinceTime = now;
                m_LastStableSamplePosition = m_CenterEyeAnchor.position;
                m_LastStableSampleRotation = m_CenterEyeAnchor.rotation;
                return;
            }

            if (m_ResetState.Outcome == ResetOutcome.Pending)
            {
                CompleteResetRecord(ResetOutcome.Cancelled);
            }

            RotationDiscontinuityResult rotationResult =
                CreateRotationDiscontinuityResult(m_CenterEyeAnchor.rotation);
            BeginResetRecord(
                ResetDetectionSource.ManualSystemRecenter,
                "Meta system recenter awaiting avatar alignment",
                Vector3.Distance(m_CenterEyeAnchor.position, m_LastCenterEyeAnchorPosition),
                rotationResult);

            m_State = DetectionState.WaitingForSystemRecenterAlignment;
            m_PendingStartTime = now;
            m_PendingLastSignalTime = now;
            m_StableSinceTime = now;
            m_LastStableSamplePosition = m_CenterEyeAnchor.position;
            m_LastStableSampleRotation = m_CenterEyeAnchor.rotation;
            m_PendingReason = "Meta system recenter awaiting avatar alignment";

            Debug.Log(
                $"[HMDResetKeeper] Meta system recenter detected; waiting "
                + $"{m_SystemRecenterStabilizeWaitTime:F2}s for a stable pose before "
                + "levelling and avatar alignment.");
        }

        private void ProcessSystemRecenterAlignment()
        {
            float now = Time.unscaledTime;
            Vector3 currentPosition = m_CenterEyeAnchor.position;
            Quaternion currentRotation = m_CenterEyeAnchor.rotation;
            float recentMove = Vector3.Distance(currentPosition, m_LastStableSamplePosition);
            float recentAngle = Quaternion.Angle(currentRotation, m_LastStableSampleRotation);
            bool stable = recentMove <= m_StabilizePositionThreshold
                && recentAngle <= m_StabilizeAngleThreshold;

            if (!stable)
            {
                m_StableSinceTime = now;
            }

            m_LastStableSamplePosition = currentPosition;
            m_LastStableSampleRotation = currentRotation;

            bool recenterEventBatchSettled =
                now - m_PendingLastSignalTime >= m_SystemRecenterStabilizeWaitTime;
            bool stableLongEnough =
                now - m_StableSinceTime >= m_SystemRecenterStabilizeWaitTime;
            bool timedOut = now - m_PendingStartTime >= m_MaxPendingDuration;

            if (!timedOut && (!stable || !recenterEventBatchSettled || !stableLongEnough))
            {
                return;
            }

            if (timedOut)
            {
                Debug.LogWarning(
                    $"[HMDResetKeeper] Meta recenter did not remain stable for "
                    + $"{m_SystemRecenterStabilizeWaitTime:F2}s within "
                    + $"{m_MaxPendingDuration:F2}s; cancelling this alignment to avoid "
                    + "moving TrackingSpace while the headset reference is still changing.");
                CompleteResetRecord(ResetOutcome.TimedOut);
                ClearPendingCompensation();
                InitializeReferencePose();
                BeginCooldown(
                    m_ManualRecenterSuppression,
                    ResetDetectionSource.ManualSystemRecenter,
                    "unstable Meta system recenter",
                    false);
                return;
            }

            LevelTrackingSpacePreservingEyePose("settled Meta system recenter");
            InitializeReferencePose();
            CompleteResetRecord(
                ResetOutcome.Applied,
                0f,
                GetTrackingSpaceTiltAngle());
            ClearPendingCompensation();
            BeginCooldown(
                m_ManualRecenterSuppression,
                ResetDetectionSource.ManualSystemRecenter,
                "settled Meta system recenter",
                false);

            Debug.Log(
                "[HMDResetKeeper] Meta system recenter settled; TrackingSpace levelled "
                + "and exact avatar alignment requested.");
            GameModule.Event.Fire(
                RequestAvatarViewAlignmentEventArgs.EventId,
                RequestAvatarViewAlignmentEventArgs.Create(
                    "settled Meta system recenter"));
        }

        /// <summary>
        /// Immediately maps the current HMD eye pose to an explicitly requested world pose.
        /// This path is used by the manual avatar-alignment command and deliberately bypasses
        /// the automatic reset detector's distance/angle thresholds and stability delay.
        /// </summary>
        private void ApplyExplicitEyePose(Vector3? requestedPosition, Quaternion? requestedRotation)
        {
            if (!EnsureReferences())
            {
                return;
            }

            Vector3 currentEyePosition = m_CenterEyeAnchor.position;
            Quaternion currentEyeRotation = m_CenterEyeAnchor.rotation;
            Vector3 targetEyePosition = requestedPosition ?? currentEyePosition;
            Quaternion targetEyeRotation = requestedRotation ?? currentEyeRotation;
            if (!ValidateCurrentTrackingPoses()
                || !IsFinite(targetEyePosition)
                || !IsValidRotation(targetEyeRotation))
            {
                WarnInvalidTrackingPose(
                    HasValidPose(m_TrackingSpace),
                    HasValidPose(m_CenterEyeAnchor));
                return;
            }

            m_IsEnable = true;
            ClearTransientState();
            RotationDiscontinuityResult rotationResult = new RotationDiscontinuityResult
            {
                IsDetected = true,
                DeltaAngle = Quaternion.Angle(currentEyeRotation, targetEyeRotation),
                RotationAxis = Vector3.up
            };
            BeginResetRecord(
                ResetDetectionSource.ExplicitAvatarAlignment,
                "explicit avatar alignment",
                Vector3.Distance(currentEyePosition, targetEyePosition),
                rotationResult);
            m_ResetState.TargetPosition = targetEyePosition;
            m_ResetState.TargetRotation = targetEyeRotation;

            if (!MapCurrentEyePoseToTarget(targetEyePosition, targetEyeRotation))
            {
                CompleteResetRecord(ResetOutcome.Cancelled);
                return;
            }
            InitializeReferencePose();
            m_EnableTime = Time.unscaledTime;
            m_LastHeadsetTracked = IsHeadsetTracked();

            float residualPosition = Vector3.Distance(m_CenterEyeAnchor.position, targetEyePosition);
            float residualAngle = GetHorizontalRotationAngle(
                m_CenterEyeAnchor.rotation,
                targetEyeRotation);
            CompleteResetRecord(ResetOutcome.Applied, residualPosition, residualAngle);

            if (m_LastHeadsetTracked)
            {
                // Follow the newly aligned pose briefly so the Meta recenter event and the
                // CameraRig transform can settle in either event/update ordering.
                BeginCooldown(
                    m_ManualRecenterSuppression,
                    ResetDetectionSource.ExplicitAvatarAlignment,
                    "explicit avatar alignment",
                    false);
            }
            else
            {
                m_State = DetectionState.TrackingLost;
            }

            Debug.Log(
                $"[HMDResetKeeper] Explicit avatar alignment applied: "
                + $"targetPosition={targetEyePosition}, targetRotation={targetEyeRotation.eulerAngles}, "
                + $"residualPosition={residualPosition:F4}m, residualAngle={residualAngle:F2}deg.");
        }

        private void StartPendingCompensation(
            ResetDetectionSource source,
            string reason,
            float positionDelta,
            RotationDiscontinuityResult rotationResult)
        {
            if (m_State == DetectionState.WaitingForStability
                || m_State == DetectionState.Cooldown)
            {
                return;
            }

            float now = Time.unscaledTime;
            BeginResetRecord(source, reason, positionDelta, rotationResult);
            m_State = DetectionState.WaitingForStability;
            m_PendingStartTime = now;
            m_PendingLastSignalTime = now;
            m_StableSinceTime = now;
            m_LastStableSamplePosition = m_CenterEyeAnchor.position;
            m_LastStableSampleRotation = m_CenterEyeAnchor.rotation;
            m_PendingReason = reason;
            Debug.Log($"[HMDResetKeeper] Candidate detected: {reason}; waiting for continuous stability.");
        }

        private void ProcessPendingCompensation()
        {
            float now = Time.unscaledTime;
            if (now - m_PendingStartTime > m_MaxPendingDuration)
            {
                Debug.LogWarning(
                    $"[HMDResetKeeper] Candidate timed out after {m_MaxPendingDuration:F2}s "
                    + $"({m_PendingReason}); compensation cancelled.");
                CompleteResetRecord(ResetOutcome.TimedOut);
                ClearPendingCompensation();
                UpdateReferencePose();
                BeginCooldown(
                    m_ResetCooldownDuration,
                    m_ResetState.Source,
                    "candidate timeout",
                    false);
                return;
            }

            Vector3 currentPosition = m_CenterEyeAnchor.position;
            Quaternion currentRotation = m_CenterEyeAnchor.rotation;
            float recentMove = Vector3.Distance(currentPosition, m_LastStableSamplePosition);
            float recentAngle = Quaternion.Angle(currentRotation, m_LastStableSampleRotation);
            bool stable = recentMove <= m_StabilizePositionThreshold
                && recentAngle <= m_StabilizeAngleThreshold;
            bool recenterBatchSettled = m_ResetState.Source != ResetDetectionSource.OvrRecenterEvent
                || now - m_PendingLastSignalTime >= m_RecenterEventQuietPeriod;

            if (!stable)
            {
                m_StableSinceTime = now;
            }

            m_LastStableSamplePosition = currentPosition;
            m_LastStableSampleRotation = currentRotation;

            if (stable
                && recenterBatchSettled
                && now - m_StableSinceTime >= m_StabilizeWaitTime)
            {
                ApplyTrackingReset();
                ClearPendingCompensation();
                BeginCooldown(
                    m_ResetCooldownDuration,
                    m_ResetState.Source,
                    "automatic compensation applied",
                    false);
            }
        }

        private void ApplyTrackingReset()
        {
            if (!EnsureReferences())
            {
                return;
            }

            Vector3 currentEyePosition = m_CenterEyeAnchor.position;
            Quaternion currentEyeRotation = m_CenterEyeAnchor.rotation;

            MapCurrentEyePoseToTarget(
                m_LastCenterEyeAnchorPosition,
                m_LastCenterEyeAnchorRotation);

            float residualPosition = Vector3.Distance(
                m_CenterEyeAnchor.position,
                m_LastCenterEyeAnchorPosition);
            float residualRotation = GetHorizontalRotationAngle(
                m_CenterEyeAnchor.rotation,
                m_LastCenterEyeAnchorRotation);
            CompleteResetRecord(ResetOutcome.Applied, residualPosition, residualRotation);

            Debug.Log(
                $"[HMDResetKeeper] Compensation applied ({m_PendingReason}): "
                + $"eyeDelta={Vector3.Distance(currentEyePosition, m_LastCenterEyeAnchorPosition):F3}m, "
                + $"angleDelta={Quaternion.Angle(currentEyeRotation, m_LastCenterEyeAnchorRotation):F1}deg.");

            InitializeReferencePose();
        }

        /// <summary>
        /// Maps the current eye position and horizontal facing direction to the requested pose.
        /// TrackingSpace is always rebuilt as a world-level (yaw-only) rotation so physical
        /// head pitch/roll can never accumulate as a permanent scene tilt.
        /// </summary>
        private bool MapCurrentEyePoseToTarget(
            Vector3 targetEyePosition,
            Quaternion targetEyeRotation)
        {
            if (!ValidateCurrentTrackingPoses()
                || !IsFinite(targetEyePosition)
                || !IsValidRotation(targetEyeRotation))
            {
                WarnInvalidTrackingPose(
                    HasValidPose(m_TrackingSpace),
                    HasValidPose(m_CenterEyeAnchor));
                return false;
            }

            Vector3 currentEyePosition = m_CenterEyeAnchor.position;
            Quaternion currentEyeRotation = m_CenterEyeAnchor.rotation;
            Vector3 originalTrackingPosition = m_TrackingSpace.position;
            Quaternion originalTrackingRotation = m_TrackingSpace.rotation;
            float tiltBefore = GetTrackingSpaceTiltAngle();

            // Remove historical pitch/roll first. Then calculate the yaw needed after
            // levelling so the user's horizontal gaze faces the requested direction.
            Quaternion levelTrackingRotation = GetHorizontalRotation(originalTrackingRotation);
            Quaternion eyeLocalRotation = Quaternion.Inverse(originalTrackingRotation)
                * currentEyeRotation;
            Quaternion eyeRotationAfterLevelling = levelTrackingRotation * eyeLocalRotation;
            Quaternion yawCorrection = GetHorizontalRotation(targetEyeRotation)
                * Quaternion.Inverse(GetHorizontalRotation(eyeRotationAfterLevelling));
            Quaternion newTrackingRotation = yawCorrection * levelTrackingRotation;
            Quaternion trackingRotationChange = newTrackingRotation
                * Quaternion.Inverse(originalTrackingRotation);

            // Rotate around the current eye and then translate it to the requested position.
            // This preserves exact eye placement without importing head pitch/roll into the rig.
            Vector3 currentEyeOffset = currentEyePosition - originalTrackingPosition;
            m_TrackingSpace.rotation = newTrackingRotation;
            m_TrackingSpace.position = targetEyePosition
                - trackingRotationChange * currentEyeOffset;

            if (!HasValidPose(m_TrackingSpace))
            {
                if (m_HasLastValidTrackingSpacePose)
                {
                    m_TrackingSpace.SetPositionAndRotation(
                        m_LastValidTrackingSpacePosition,
                        m_LastValidTrackingSpaceRotation);
                }

                WarnInvalidTrackingPose(false, HasValidPose(m_CenterEyeAnchor));
                return false;
            }

            CacheTrackingSpacePoseIfValid();

            Debug.Log(
                $"[HMDResetKeeper] Level yaw mapping: "
                + $"yawCorrection={Quaternion.Angle(Quaternion.identity, yawCorrection):F2}deg, "
                + $"trackingTilt={tiltBefore:F2}deg->{GetTrackingSpaceTiltAngle():F2}deg, "
                + $"targetPosition={targetEyePosition}.");
            return true;
        }

        private void LevelTrackingSpacePreservingEyePose(string reason)
        {
            float tiltBefore = GetTrackingSpaceTiltAngle();
            Vector3 eyePosition = m_CenterEyeAnchor.position;
            Quaternion eyeRotation = m_CenterEyeAnchor.rotation;
            MapCurrentEyePoseToTarget(eyePosition, eyeRotation);
            Debug.Log(
                $"[HMDResetKeeper] TrackingSpace levelled for {reason}: "
                + $"{tiltBefore:F2}deg->{GetTrackingSpaceTiltAngle():F2}deg.");
        }

        private float GetTrackingSpaceTiltAngle()
        {
            return Vector3.Angle(m_TrackingSpace.up, Vector3.up);
        }

        private static float GetHorizontalRotationAngle(Quaternion a, Quaternion b)
        {
            return Quaternion.Angle(GetHorizontalRotation(a), GetHorizontalRotation(b));
        }

        private static Quaternion GetHorizontalRotation(Quaternion rotation)
        {
            if (!IsValidRotation(rotation))
            {
                return Quaternion.identity;
            }

            Vector3 forward = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
            {
                Vector3 right = Vector3.ProjectOnPlane(rotation * Vector3.right, Vector3.up);
                if (right.sqrMagnitude < 0.0001f)
                {
                    return Quaternion.identity;
                }

                forward = Vector3.Cross(right.normalized, Vector3.up);
            }

            return Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        private void ClearTransientState()
        {
            if (m_ResetState.Outcome == ResetOutcome.Pending)
            {
                CompleteResetRecord(ResetOutcome.Cancelled);
            }
            ClearPendingCompensation();
            m_CooldownUntilTime = 0f;
        }

        private void ClearPendingCompensation()
        {
            m_PendingStartTime = 0f;
            m_PendingLastSignalTime = float.NegativeInfinity;
            m_StableSinceTime = 0f;
            m_PendingReason = string.Empty;
        }

        #endregion

        #region Reference pose

        private void InitializeReferencePose()
        {
            if (!HasValidPose(m_CenterEyeAnchor))
            {
                WarnInvalidTrackingPose(
                    HasValidPose(m_TrackingSpace),
                    false);
                return;
            }

            Vector3 position = m_CenterEyeAnchor.position;
            Quaternion rotation = m_CenterEyeAnchor.rotation;
            m_LastCenterEyeAnchorPosition = position;
            m_LastCenterEyeAnchorRotation = rotation;
            m_PreviousCenterEyeAnchorPosition = position;
            m_PreviousCenterEyeAnchorRotation = rotation;
            m_LastReferenceSampleTime = Time.unscaledTime;
            m_PreviousReferenceSampleTime = m_LastReferenceSampleTime;
        }

        private void UpdateReferencePose()
        {
            if (!HasValidPose(m_CenterEyeAnchor))
            {
                WarnInvalidTrackingPose(
                    HasValidPose(m_TrackingSpace),
                    false);
                return;
            }

            m_PreviousCenterEyeAnchorPosition = m_LastCenterEyeAnchorPosition;
            m_PreviousCenterEyeAnchorRotation = m_LastCenterEyeAnchorRotation;
            m_PreviousReferenceSampleTime = m_LastReferenceSampleTime;
            m_LastCenterEyeAnchorPosition = m_CenterEyeAnchor.position;
            m_LastCenterEyeAnchorRotation = m_CenterEyeAnchor.rotation;
            m_LastReferenceSampleTime = Time.unscaledTime;
        }

        public void UpdateLastValues(Vector3? position, Quaternion? rotation)
        {
            if (!EnsureReferences())
            {
                return;
            }

            Vector3 nextPosition = position ?? m_CenterEyeAnchor.position;
            Quaternion nextRotation = rotation ?? m_CenterEyeAnchor.rotation;
            if (!IsFinite(nextPosition) || !IsValidRotation(nextRotation))
            {
                WarnInvalidTrackingPose(
                    HasValidPose(m_TrackingSpace),
                    HasValidPose(m_CenterEyeAnchor));
                return;
            }

            m_LastCenterEyeAnchorPosition = nextPosition;
            m_LastCenterEyeAnchorRotation = nextRotation;
            m_PreviousCenterEyeAnchorPosition = m_LastCenterEyeAnchorPosition;
            m_PreviousCenterEyeAnchorRotation = m_LastCenterEyeAnchorRotation;
            m_LastReferenceSampleTime = Time.unscaledTime;
            m_PreviousReferenceSampleTime = m_LastReferenceSampleTime;
        }

        public Vector3 GetLastPosition()
        {
            return m_LastCenterEyeAnchorPosition;
        }

        public Quaternion GetLastRotation()
        {
            return m_LastCenterEyeAnchorRotation;
        }

        #endregion

#endif
    }
}
