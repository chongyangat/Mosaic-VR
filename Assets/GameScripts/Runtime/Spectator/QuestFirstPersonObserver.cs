#if UNITY_EDITOR || UNITY_STANDALONE
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameMain
{
    /// <summary>
    /// PC-only observer that renders the already-networked Quest HMD pose.
    ///
    /// The component is created at runtime, is disabled by default, and is
    /// excluded from Android players. It therefore requires no Quest APK
    /// rebuild and does not modify scene or prefab assets.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class QuestFirstPersonObserver : MonoBehaviour
    {
        private const string ObserverObjectName = "[PC] Quest First-Person Observer";
        private const string ObserverCameraName = "Quest First-Person Observer Camera";
        private const float TargetSearchInterval = 0.5f;
        private const float PositionSharpness = 18f;
        private const float RotationSharpness = 22f;
        private const float DefaultFieldOfView = 85f;
        private const float GazeReceiverSearchInterval = 0.5f;
        private const float GazeTrailSampleInterval = 1f / 30f;
        private const float GazeTrailLifetime = 1.5f;
        private const float GazeFreshnessTimeout = 0.25f;
        private const float DisplayFrameSearchInterval = 0.5f;
        private const int MaxGazeTrailSamples = 48;
        private const float GazeMarkerSize = 30f;
        private const string DisplayFrameName = "Frame";
        private const string DisplayFormName = "ManagerMainForm";
        private const string OutputSurfaceName = "[PC] Quest First-Person Surface";

        private struct GazeTrailSample
        {
            public Vector3 WorldPosition;
            public float ReceiveTime;
        }

        private static QuestFirstPersonObserver s_Instance;

        private Camera m_ObserverCamera;
        private Camera m_SourceCamera;
        private RenderTexture m_OutputTexture;
        private RectTransform m_DisplayFrame;
        private RectTransform m_OutputSurfaceTransform;
        private RawImage m_OutputSurface;
        private VirtualPlayerControllor m_TargetPlayer;
        private GameLogic.GazeDataReceiver m_GazeReceiver;
        private float m_NextTargetSearchTime;
        private float m_NextGazeReceiverSearchTime;
        private float m_LastGazePacketReceiveTime = float.NegativeInfinity;
        private float m_LastGazeReceiveTime = float.NegativeInfinity;
        private float m_LastGazeTrailSampleTime = float.NegativeInfinity;
        private float m_NextDisplayFrameSearchTime;
        private bool m_IsObserving;
        private bool m_HasReceivedPose;
        private bool m_HasReceivedGazePacket;
        private bool m_HasValidGaze;
        private bool m_HasLoggedFirstGazePacket;
        private bool m_HasLoggedFirstValidGaze;
        private bool m_ShowGazeOverlay = true;
        private bool m_IsControlPanelExpanded;
        private Vector3 m_LatestGazeWorldPosition;
        private string m_GazeSource = "both eyes";
        private readonly List<GazeTrailSample> m_GazeTrail = new List<GazeTrailSample>(MaxGazeTrailSamples);
        private Texture2D m_GazeRingTexture;
        private Texture2D m_GazeDotTexture;
        private GUIStyle m_StatusStyle;
        private GUIStyle m_ButtonStyle;

        public static QuestFirstPersonObserver Instance => s_Instance;
        public bool IsObserving => m_IsObserving;
        public bool HasTarget => m_TargetPlayer != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateObserver()
        {
            if (Application.isBatchMode || s_Instance != null)
            {
                return;
            }

            QuestFirstPersonObserver existing = FindObjectOfType<QuestFirstPersonObserver>();
            if (existing != null)
            {
                s_Instance = existing;
                return;
            }

            var observerObject = new GameObject(ObserverObjectName);
            DontDestroyOnLoad(observerObject);
            s_Instance = observerObject.AddComponent<QuestFirstPersonObserver>();
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
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnbindGazeReceiver();
            ResetGazeState();
            ReleaseOutputSurface();
            ReleaseOutputTexture();
            ReleaseGazeTextures();

            if (s_Instance == this)
            {
                s_Instance = null;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            UnbindGazeReceiver();
            ReleaseOutputSurface();
            m_SourceCamera = null;
            m_DisplayFrame = null;
            m_TargetPlayer = null;
            m_HasReceivedPose = false;
            m_NextTargetSearchTime = 0f;
            m_NextGazeReceiverSearchTime = 0f;
            m_NextDisplayFrameSearchTime = 0f;
            ResetGazeState();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F8))
            {
                SetObservationActive(!m_IsObserving);
            }

            if (Input.GetKeyDown(KeyCode.F9))
            {
                QuestCompleteViewMirror.Toggle();
            }

            if (Input.GetKeyDown(KeyCode.F7))
            {
                SetGazeOverlayVisible(!m_ShowGazeOverlay);
            }

            if (!m_IsObserving)
            {
                return;
            }

            ResolveTargetPlayer();
            if (m_ShowGazeOverlay)
            {
                ResolveGazeReceiver();
                RemoveExpiredGazeTrailSamples();
            }
            ResolveSourceCamera();
            EnsureObserverCamera();
            ResolveDisplayFrame();
            EnsureOutputTexture();
            EnsureOutputSurface();
        }

        private void LateUpdate()
        {
            if (!m_IsObserving || m_TargetPlayer == null || m_ObserverCamera == null)
            {
                if (m_ObserverCamera != null)
                {
                    m_ObserverCamera.enabled = false;
                }
                SetOutputSurfaceVisible(false);
                return;
            }

            Vector3 targetPosition = m_TargetPlayer.transform.position;
            Quaternion targetRotation = Quaternion.Euler(m_TargetPlayer.PlayerDir);

            if (!m_HasReceivedPose)
            {
                m_ObserverCamera.transform.SetPositionAndRotation(targetPosition, targetRotation);
                m_HasReceivedPose = true;
            }
            else
            {
                float positionT = 1f - Mathf.Exp(-PositionSharpness * Time.unscaledDeltaTime);
                float rotationT = 1f - Mathf.Exp(-RotationSharpness * Time.unscaledDeltaTime);
                m_ObserverCamera.transform.position = Vector3.Lerp(
                    m_ObserverCamera.transform.position,
                    targetPosition,
                    positionT);
                m_ObserverCamera.transform.rotation = Quaternion.Slerp(
                    m_ObserverCamera.transform.rotation,
                    targetRotation,
                    rotationT);
            }

            m_ObserverCamera.enabled = m_OutputTexture != null;
            SetOutputSurfaceVisible(m_OutputTexture != null);
        }

        public void SetObservationActive(bool active)
        {
            m_IsObserving = active;
            m_HasReceivedPose = false;
            m_NextTargetSearchTime = 0f;
            m_NextGazeReceiverSearchTime = 0f;

            if (!active && m_ObserverCamera != null)
            {
                m_ObserverCamera.enabled = false;
            }

            SetOutputSurfaceVisible(active && m_HasReceivedPose && m_OutputTexture != null);

            if (!active)
            {
                UnbindGazeReceiver();
                ResetGazeState();
            }

            Debug.Log($"[QuestObserver] First-person observation {(active ? "enabled" : "disabled")}. Press F8 to toggle.");
        }

        public void SetGazeOverlayVisible(bool visible)
        {
            m_ShowGazeOverlay = visible;
            m_NextGazeReceiverSearchTime = 0f;

            if (!visible)
            {
                UnbindGazeReceiver();
                ResetGazeState();
            }

            Debug.Log($"[QuestObserver] F8 gaze overlay {(visible ? "enabled" : "disabled")}. Press F7 to toggle.");
        }

        private void ResolveTargetPlayer()
        {
            if (m_TargetPlayer != null && m_TargetPlayer.isActiveAndEnabled && m_TargetPlayer.isServer)
            {
                return;
            }

            if (Time.unscaledTime < m_NextTargetSearchTime)
            {
                return;
            }

            m_NextTargetSearchTime = Time.unscaledTime + TargetSearchInterval;
            m_TargetPlayer = null;

            VirtualPlayerControllor[] players = FindObjectsByType<VirtualPlayerControllor>(FindObjectsSortMode.None);
            foreach (VirtualPlayerControllor player in players)
            {
                if (player != null && player.isActiveAndEnabled && player.isServer && !player.isLocalPlayer)
                {
                    m_TargetPlayer = player;
                    m_HasReceivedPose = false;
                    Debug.Log($"[QuestObserver] Following remote Quest player '{player.name}' (netId={player.netId}).");
                    return;
                }
            }
        }

        private void ResolveSourceCamera()
        {
            Camera currentMainCamera = Camera.main;
            if (currentMainCamera == null || currentMainCamera == m_ObserverCamera || currentMainCamera == m_SourceCamera)
            {
                return;
            }

            m_SourceCamera = currentMainCamera;
            ApplySourceCameraSettings();
        }

        private void ResolveGazeReceiver()
        {
            if (m_GazeReceiver != null)
            {
                return;
            }

            if (Time.unscaledTime < m_NextGazeReceiverSearchTime)
            {
                return;
            }

            m_NextGazeReceiverSearchTime = Time.unscaledTime + GazeReceiverSearchInterval;
            GameLogic.GazeDataReceiver[] receivers = FindObjectsByType<GameLogic.GazeDataReceiver>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (GameLogic.GazeDataReceiver receiver in receivers)
            {
                if (receiver == null || !receiver.isActiveAndEnabled)
                {
                    continue;
                }

                m_GazeReceiver = receiver;
                m_GazeReceiver.OnReceivedGazePointInUpdate.AddListener(OnGazePointReceived);
                Debug.Log($"[QuestObserver] Bound F8 gaze overlay to '{receiver.name}'.");
                return;
            }
        }

        private void UnbindGazeReceiver()
        {
            if (m_GazeReceiver == null)
            {
                return;
            }

            m_GazeReceiver.OnReceivedGazePointInUpdate.RemoveListener(OnGazePointReceived);
            m_GazeReceiver = null;
        }

        private void OnGazePointReceived(
            MetaQuestProEyeGazeUDP.GazeNetData gazeData,
            Vector3? leftEyeGazePosition,
            Vector3? rightEyeGazePosition,
            Vector3? doubleEyesGazePosition)
        {
            float receiveTime = Time.unscaledTime;
            m_LastGazePacketReceiveTime = receiveTime;
            m_HasReceivedGazePacket = true;

            if (!m_HasLoggedFirstGazePacket)
            {
                m_HasLoggedFirstGazePacket = true;
                Debug.Log($"[QuestObserver] First gaze packet received (sequence={gazeData.sequence}).");
            }

            Vector3? selectedPosition = doubleEyesGazePosition;
            m_GazeSource = "both eyes";

            if (!selectedPosition.HasValue && leftEyeGazePosition.HasValue && rightEyeGazePosition.HasValue)
            {
                selectedPosition = (leftEyeGazePosition.Value + rightEyeGazePosition.Value) * 0.5f;
                m_GazeSource = "averaged eyes";
            }
            else if (!selectedPosition.HasValue && leftEyeGazePosition.HasValue)
            {
                selectedPosition = leftEyeGazePosition;
                m_GazeSource = "left eye";
            }
            else if (!selectedPosition.HasValue && rightEyeGazePosition.HasValue)
            {
                selectedPosition = rightEyeGazePosition;
                m_GazeSource = "right eye";
            }

            if (!selectedPosition.HasValue)
            {
                // Keep the last valid point alive until the freshness timeout.
                // A single invalid eye-tracking frame should not make the F8
                // marker flicker when the user blinks.
                return;
            }

            m_LatestGazeWorldPosition = selectedPosition.Value;
            m_LastGazeReceiveTime = receiveTime;
            m_HasValidGaze = true;

            if (!m_HasLoggedFirstValidGaze)
            {
                m_HasLoggedFirstValidGaze = true;
                Debug.Log($"[QuestObserver] Live gaze acquired from {m_GazeSource} (sequence={gazeData.sequence}).");
            }

            if (receiveTime - m_LastGazeTrailSampleTime < GazeTrailSampleInterval)
            {
                return;
            }

            m_LastGazeTrailSampleTime = receiveTime;
            m_GazeTrail.Add(new GazeTrailSample
            {
                WorldPosition = selectedPosition.Value,
                ReceiveTime = receiveTime
            });

            if (m_GazeTrail.Count > MaxGazeTrailSamples)
            {
                m_GazeTrail.RemoveAt(0);
            }
        }

        private void RemoveExpiredGazeTrailSamples()
        {
            float oldestAllowedTime = Time.unscaledTime - GazeTrailLifetime;
            int removeCount = 0;
            while (removeCount < m_GazeTrail.Count && m_GazeTrail[removeCount].ReceiveTime < oldestAllowedTime)
            {
                removeCount++;
            }

            if (removeCount > 0)
            {
                m_GazeTrail.RemoveRange(0, removeCount);
            }

            if (m_HasValidGaze && Time.unscaledTime - m_LastGazeReceiveTime > GazeFreshnessTimeout)
            {
                m_HasValidGaze = false;
            }
        }

        private void ResetGazeState()
        {
            m_HasReceivedGazePacket = false;
            m_HasValidGaze = false;
            m_HasLoggedFirstGazePacket = false;
            m_HasLoggedFirstValidGaze = false;
            m_LastGazePacketReceiveTime = float.NegativeInfinity;
            m_LastGazeReceiveTime = float.NegativeInfinity;
            m_LastGazeTrailSampleTime = float.NegativeInfinity;
            m_GazeTrail.Clear();
        }

        private void EnsureObserverCamera()
        {
            if (m_ObserverCamera == null)
            {
                var cameraObject = new GameObject(ObserverCameraName);
                cameraObject.transform.SetParent(transform, false);
                m_ObserverCamera = cameraObject.AddComponent<Camera>();
                m_ObserverCamera.enabled = false;
                m_ObserverCamera.stereoTargetEye = StereoTargetEyeMask.None;
                ApplySourceCameraSettings();
            }
        }

        private void ApplySourceCameraSettings()
        {
            if (m_ObserverCamera == null)
            {
                return;
            }

            bool wasEnabled = m_ObserverCamera.enabled;
            RenderTexture previousTarget = m_OutputTexture;

            if (m_SourceCamera != null)
            {
                m_ObserverCamera.CopyFrom(m_SourceCamera);
            }
            else
            {
                m_ObserverCamera.clearFlags = CameraClearFlags.Skybox;
                m_ObserverCamera.nearClipPlane = 0.03f;
                m_ObserverCamera.farClipPlane = 1000f;
            }

            m_ObserverCamera.fieldOfView = DefaultFieldOfView;
            m_ObserverCamera.rect = new Rect(0f, 0f, 1f, 1f);
            m_ObserverCamera.stereoTargetEye = StereoTargetEyeMask.None;
            int gazeLayer = LayerMask.NameToLayer("GazeLine");
            if (gazeLayer >= 0)
            {
                // The existing third-person gaze sprites face Camera.main and
                // can be offset in F8. Hide them only from the observer camera;
                // the dedicated screen-space overlay below replaces them.
                m_ObserverCamera.cullingMask &= ~(1 << gazeLayer);
            }
            m_ObserverCamera.targetTexture = previousTarget;
            m_ObserverCamera.enabled = wasEnabled;
        }

        private void ResolveDisplayFrame()
        {
            if (m_DisplayFrame != null && m_DisplayFrame.gameObject.activeInHierarchy)
            {
                return;
            }

            if (Time.unscaledTime < m_NextDisplayFrameSearchTime)
            {
                return;
            }

            m_NextDisplayFrameSearchTime = Time.unscaledTime + DisplayFrameSearchInterval;
            RectTransform bestMatch = null;
            float bestArea = 0f;
            RectTransform[] candidates = FindObjectsByType<RectTransform>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            foreach (RectTransform candidate in candidates)
            {
                if (candidate == null || candidate.name != DisplayFrameName ||
                    !HasAncestorNamed(candidate, DisplayFormName))
                {
                    continue;
                }

                float area = candidate.rect.width * candidate.rect.height;
                if (area > bestArea)
                {
                    bestMatch = candidate;
                    bestArea = area;
                }
            }

            if (bestMatch == m_DisplayFrame)
            {
                return;
            }

            ReleaseOutputSurface();
            m_DisplayFrame = bestMatch;
            if (m_DisplayFrame != null)
            {
                Debug.Log("[QuestObserver] First-person output attached to ManagerMainForm/Main/Frame.");
            }
        }

        private static bool HasAncestorNamed(Transform transformToCheck, string ancestorName)
        {
            Transform current = transformToCheck;
            while (current != null)
            {
                if (current.name.StartsWith(ancestorName))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private void EnsureOutputSurface()
        {
            if (m_DisplayFrame == null)
            {
                return;
            }

            if (m_OutputSurface == null)
            {
                var surfaceObject = new GameObject(
                    OutputSurfaceName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(RawImage));
                m_OutputSurfaceTransform = surfaceObject.GetComponent<RectTransform>();
                m_OutputSurface = surfaceObject.GetComponent<RawImage>();
                surfaceObject.layer = m_DisplayFrame.gameObject.layer;
                m_OutputSurface.raycastTarget = false;
                m_OutputSurface.maskable = true;
                m_OutputSurface.color = Color.white;
                m_OutputSurfaceTransform.SetParent(m_DisplayFrame.parent, false);
                m_OutputSurfaceTransform.SetSiblingIndex(m_DisplayFrame.GetSiblingIndex());
            }

            SyncOutputSurfaceLayout();
            m_OutputSurface.texture = m_OutputTexture;
            m_OutputSurface.uvRect = CalculateOutputUvRect();
            SetOutputSurfaceVisible(m_IsObserving && m_HasReceivedPose && m_OutputTexture != null);
        }

        private void SyncOutputSurfaceLayout()
        {
            if (m_DisplayFrame == null || m_OutputSurfaceTransform == null)
            {
                return;
            }

            m_OutputSurfaceTransform.anchorMin = m_DisplayFrame.anchorMin;
            m_OutputSurfaceTransform.anchorMax = m_DisplayFrame.anchorMax;
            m_OutputSurfaceTransform.anchoredPosition = m_DisplayFrame.anchoredPosition;
            m_OutputSurfaceTransform.sizeDelta = m_DisplayFrame.sizeDelta;
            m_OutputSurfaceTransform.pivot = m_DisplayFrame.pivot;
            m_OutputSurfaceTransform.localRotation = m_DisplayFrame.localRotation;
            m_OutputSurfaceTransform.localScale = m_DisplayFrame.localScale;
        }

        private Rect CalculateOutputUvRect()
        {
            if (m_OutputTexture == null || m_DisplayFrame == null ||
                m_OutputTexture.height <= 0 || m_DisplayFrame.rect.height <= 0f)
            {
                return new Rect(0f, 0f, 1f, 1f);
            }

            float sourceAspect = (float)m_OutputTexture.width / m_OutputTexture.height;
            float targetAspect = m_DisplayFrame.rect.width / m_DisplayFrame.rect.height;
            if (sourceAspect > targetAspect)
            {
                float visibleWidth = targetAspect / sourceAspect;
                return new Rect((1f - visibleWidth) * 0.5f, 0f, visibleWidth, 1f);
            }

            float visibleHeight = sourceAspect / targetAspect;
            return new Rect(0f, (1f - visibleHeight) * 0.5f, 1f, visibleHeight);
        }

        private void SetOutputSurfaceVisible(bool visible)
        {
            if (m_OutputSurface != null)
            {
                m_OutputSurface.enabled = visible;
            }
        }

        private void ReleaseOutputSurface()
        {
            if (m_OutputSurfaceTransform != null)
            {
                Destroy(m_OutputSurfaceTransform.gameObject);
            }

            m_OutputSurface = null;
            m_OutputSurfaceTransform = null;
        }

        private void EnsureOutputTexture()
        {
            Rect displayRect = GetObservationDisplayRect();
            int width = Mathf.Max(1, Mathf.RoundToInt(displayRect.width));
            int height = Mathf.Max(1, Mathf.RoundToInt(displayRect.height));
            float minimumScale = Mathf.Max(1f, 640f / width, 360f / height);
            width = Mathf.RoundToInt(width * minimumScale);
            height = Mathf.RoundToInt(height * minimumScale);
            if (m_OutputTexture != null && m_OutputTexture.width == width && m_OutputTexture.height == height)
            {
                return;
            }

            ReleaseOutputTexture();
            m_OutputTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "Quest First-Person Observer Output",
                antiAliasing = 1,
                useMipMap = false,
                autoGenerateMips = false
            };
            m_OutputTexture.Create();

            if (m_ObserverCamera != null)
            {
                m_ObserverCamera.targetTexture = m_OutputTexture;
            }

            if (m_OutputSurface != null)
            {
                m_OutputSurface.texture = m_OutputTexture;
                m_OutputSurface.uvRect = CalculateOutputUvRect();
            }
        }

        private void ReleaseOutputTexture()
        {
            if (m_OutputTexture == null)
            {
                return;
            }

            if (m_ObserverCamera != null && m_ObserverCamera.targetTexture == m_OutputTexture)
            {
                m_ObserverCamera.targetTexture = null;
            }

            if (m_OutputSurface != null && m_OutputSurface.texture == m_OutputTexture)
            {
                m_OutputSurface.texture = null;
            }

            m_OutputTexture.Release();
            Destroy(m_OutputTexture);
            m_OutputTexture = null;
        }

        private void OnGUI()
        {
            if (m_IsObserving && m_HasReceivedPose && m_OutputTexture != null && Event.current.type == EventType.Repaint)
            {
                if (m_OutputSurface == null)
                {
                    GUI.DrawTexture(
                        GetObservationDisplayRect(),
                        m_OutputTexture,
                        ScaleMode.ScaleAndCrop,
                        false);
                }

                if (m_ShowGazeOverlay)
                {
                    DrawGazeOverlay();
                }
            }

            EnsureGuiStyles();

            const float collapsedWidth = 138f;
            const float menuButtonHeight = 34f;
            const float rightMargin = 16f;
            Rect menuButtonRect = new Rect(
                Screen.width - collapsedWidth - rightMargin,
                16f,
                collapsedWidth,
                menuButtonHeight);
            string activeView = QuestCompleteViewMirror.IsRunning
                ? "Full"
                : m_IsObserving ? "1P" : "3P";
            string menuButtonText = m_IsControlPanelExpanded
                ? $"View Menu · {activeView}  ▲"
                : $"View Menu · {activeView}  ▼";
            if (GUI.Button(menuButtonRect, menuButtonText, m_ButtonStyle))
            {
                m_IsControlPanelExpanded = !m_IsControlPanelExpanded;
            }

            if (!m_IsControlPanelExpanded)
            {
                return;
            }

            const float panelWidth = 310f;
            const float panelHeight = 226f;
            Rect panelRect = new Rect(
                Screen.width - panelWidth - rightMargin,
                menuButtonRect.yMax + 6f,
                panelWidth,
                panelHeight);
            GUI.Box(panelRect, GUIContent.none);

            string status = !m_IsObserving
                ? "Quest First-Person: OFF"
                : m_TargetPlayer == null
                    ? "Quest First-Person: waiting for remote player"
                    : $"Quest First-Person: LIVE  netId={m_TargetPlayer.netId}";

            GUI.Label(new Rect(panelRect.x + 12f, panelRect.y + 9f, panelWidth - 24f, 24f), status, m_StatusStyle);

            string buttonText = m_IsObserving ? "Stop Quest View  [F8]" : "Start Quest View  [F8]";
            if (GUI.Button(
                    new Rect(panelRect.x + 12f, panelRect.y + 40f, panelWidth - 24f, 34f),
                    buttonText,
                    m_ButtonStyle))
            {
                SetObservationActive(!m_IsObserving);
                m_IsControlPanelExpanded = false;
            }

            bool hasFreshPacket = m_HasReceivedGazePacket &&
                                  Time.unscaledTime - m_LastGazePacketReceiveTime <= 1f;
            bool hasFreshValidGaze = m_HasValidGaze &&
                                     Time.unscaledTime - m_LastGazeReceiveTime <= GazeFreshnessTimeout;
            string gazeStatus = !m_ShowGazeOverlay
                ? "Gaze Overlay: OFF"
                : !m_IsObserving
                    ? "Gaze Overlay: ready (starts with F8)"
                    : m_GazeReceiver == null
                        ? "Gaze Overlay: waiting for receiver"
                        : !m_HasReceivedGazePacket
                            ? "Gaze Overlay: waiting for UDP gaze data"
                            : !hasFreshPacket
                                ? "Gaze Overlay: gaze stream stale"
                                : !hasFreshValidGaze
                                    ? "Gaze Overlay: waiting for valid gaze hit"
                                    : $"Gaze Overlay: LIVE ({m_GazeSource})";
            GUI.Label(
                new Rect(panelRect.x + 12f, panelRect.y + 79f, panelWidth - 24f, 24f),
                gazeStatus,
                m_StatusStyle);

            string gazeButtonText = m_ShowGazeOverlay
                ? "Hide Gaze Trail  [F7]"
                : "Show Gaze Trail  [F7]";
            if (GUI.Button(
                    new Rect(panelRect.x + 12f, panelRect.y + 107f, panelWidth - 24f, 30f),
                    gazeButtonText,
                    m_ButtonStyle))
            {
                SetGazeOverlayVisible(!m_ShowGazeOverlay);
                m_IsControlPanelExpanded = false;
            }

            string completeStatus = QuestCompleteViewMirror.IsRunning
                ? "Complete Quest View: LIVE (hands + palm menu)"
                : QuestCompleteViewMirror.Status;
            GUI.Label(
                new Rect(panelRect.x + 12f, panelRect.y + 145f, panelWidth - 24f, 24f),
                completeStatus,
                m_StatusStyle);

            string completeButtonText = QuestCompleteViewMirror.IsRunning
                ? "Stop Complete Quest View  [F9]"
                : "Open Complete Quest View  [F9]";
            if (GUI.Button(
                    new Rect(panelRect.x + 12f, panelRect.y + 178f, panelWidth - 24f, 34f),
                    completeButtonText,
                    m_ButtonStyle))
            {
                QuestCompleteViewMirror.Toggle();
                m_IsControlPanelExpanded = false;
            }
        }

        private Rect GetObservationDisplayRect()
        {
            if (TryGetDisplayFrameGuiRect(out Rect displayRect))
            {
                return displayRect;
            }

            // Safe fallback used while the server UI is changing scenes.
            return new Rect(
                Screen.width * 0.055f,
                Screen.height * 0.16f,
                Screen.width * 0.89f,
                Screen.height * 0.52f);
        }

        private bool TryGetDisplayFrameGuiRect(out Rect guiRect)
        {
            guiRect = default;
            if (m_DisplayFrame == null)
            {
                return false;
            }

            Canvas canvas = m_DisplayFrame.GetComponentInParent<Canvas>();
            Camera canvasCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            var corners = new Vector3[4];
            m_DisplayFrame.GetWorldCorners(corners);

            float minX = float.PositiveInfinity;
            float minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float maxY = float.NegativeInfinity;
            foreach (Vector3 corner in corners)
            {
                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(canvasCamera, corner);
                minX = Mathf.Min(minX, screenPoint.x);
                minY = Mathf.Min(minY, screenPoint.y);
                maxX = Mathf.Max(maxX, screenPoint.x);
                maxY = Mathf.Max(maxY, screenPoint.y);
            }

            minX = Mathf.Clamp(minX, 0f, Screen.width);
            maxX = Mathf.Clamp(maxX, 0f, Screen.width);
            minY = Mathf.Clamp(minY, 0f, Screen.height);
            maxY = Mathf.Clamp(maxY, 0f, Screen.height);
            if (maxX - minX < 32f || maxY - minY < 32f)
            {
                return false;
            }

            guiRect = new Rect(minX, Screen.height - maxY, maxX - minX, maxY - minY);
            return true;
        }

        private void DrawGazeOverlay()
        {
            if (m_ObserverCamera == null || m_OutputTexture == null)
            {
                return;
            }

            EnsureGazeTextures();
            float now = Time.unscaledTime;
            Vector2? previousScreenPosition = null;
            float previousAlpha = 0f;

            foreach (GazeTrailSample sample in m_GazeTrail)
            {
                if (!TryWorldToObserverScreen(sample.WorldPosition, out Vector2 screenPosition))
                {
                    previousScreenPosition = null;
                    continue;
                }

                float age = now - sample.ReceiveTime;
                float alpha = Mathf.Clamp01(1f - age / GazeTrailLifetime);
                if (previousScreenPosition.HasValue)
                {
                    DrawGuiLine(
                        previousScreenPosition.Value,
                        screenPosition,
                        new Color(1f, 0.9f, 0f, Mathf.Min(previousAlpha, alpha) * 0.42f),
                        2f);
                }

                float dotSize = Mathf.Lerp(3f, 7f, alpha);
                DrawCenteredTexture(
                    m_GazeDotTexture,
                    screenPosition,
                    dotSize,
                    new Color(1f, 0.9f, 0f, alpha * 0.68f));
                previousScreenPosition = screenPosition;
                previousAlpha = alpha;
            }

            if (!m_HasValidGaze || now - m_LastGazeReceiveTime > GazeFreshnessTimeout ||
                !TryWorldToObserverScreen(m_LatestGazeWorldPosition, out Vector2 liveScreenPosition))
            {
                return;
            }

            float pulse = 1f + Mathf.Sin(now * 8f) * 0.08f;
            DrawCenteredTexture(
                m_GazeRingTexture,
                liveScreenPosition,
                GazeMarkerSize * pulse,
                new Color(1f, 0.92f, 0f, 0.96f));
            DrawCenteredTexture(
                m_GazeDotTexture,
                liveScreenPosition,
                5f,
                new Color(1f, 0.96f, 0.15f, 1f));
        }

        private bool TryWorldToObserverScreen(Vector3 worldPosition, out Vector2 screenPosition)
        {
            screenPosition = default;
            Vector3 viewportPosition = m_ObserverCamera.WorldToViewportPoint(worldPosition);
            if (viewportPosition.z <= m_ObserverCamera.nearClipPlane ||
                viewportPosition.x < 0f || viewportPosition.x > 1f ||
                viewportPosition.y < 0f || viewportPosition.y > 1f)
            {
                return false;
            }

            Rect displayRect = GetObservationDisplayRect();
            Rect visibleUv = CalculateOutputUvRect();
            if (visibleUv.width <= 0f || visibleUv.height <= 0f ||
                viewportPosition.x < visibleUv.xMin || viewportPosition.x > visibleUv.xMax ||
                viewportPosition.y < visibleUv.yMin || viewportPosition.y > visibleUv.yMax)
            {
                return false;
            }

            float displayX = (viewportPosition.x - visibleUv.xMin) / visibleUv.width;
            float displayY = (viewportPosition.y - visibleUv.yMin) / visibleUv.height;
            screenPosition = new Vector2(
                displayRect.x + displayX * displayRect.width,
                displayRect.y + (1f - displayY) * displayRect.height);
            return displayRect.Contains(screenPosition);
        }

        private void EnsureGazeTextures()
        {
            if (m_GazeRingTexture == null)
            {
                m_GazeRingTexture = CreateRadialTexture(64, true);
            }

            if (m_GazeDotTexture == null)
            {
                m_GazeDotTexture = CreateRadialTexture(32, false);
            }
        }

        private static Texture2D CreateRadialTexture(int size, bool ring)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = ring ? "F8 Gaze Ring" : "F8 Gaze Dot",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[size * size];
            float radius = (size - 1) * 0.5f;
            Vector2 center = new Vector2(radius, radius);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float normalizedDistance = Vector2.Distance(new Vector2(x, y), center) / radius;
                    float outerAlpha = Mathf.Clamp01((1f - normalizedDistance) * 8f);
                    float alpha = outerAlpha;
                    if (ring)
                    {
                        float innerAlpha = Mathf.Clamp01((normalizedDistance - 0.58f) * 12f);
                        alpha *= innerAlpha;
                    }

                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static void DrawCenteredTexture(Texture texture, Vector2 center, float size, Color color)
        {
            Color previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size), texture);
            GUI.color = previousColor;
        }

        private static void DrawGuiLine(Vector2 start, Vector2 end, Color color, float width)
        {
            Vector2 difference = end - start;
            float length = difference.magnitude;
            if (length <= 0.01f)
            {
                return;
            }

            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            GUI.color = color;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(difference.y, difference.x) * Mathf.Rad2Deg, start);
            GUI.DrawTexture(new Rect(start.x, start.y - width * 0.5f, length, width), Texture2D.whiteTexture);
            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
        }

        private void ReleaseGazeTextures()
        {
            if (m_GazeRingTexture != null)
            {
                Destroy(m_GazeRingTexture);
                m_GazeRingTexture = null;
            }

            if (m_GazeDotTexture != null)
            {
                Destroy(m_GazeDotTexture);
                m_GazeDotTexture = null;
            }
        }

        private void EnsureGuiStyles()
        {
            if (m_StatusStyle != null && m_ButtonStyle != null)
            {
                return;
            }

            m_StatusStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            m_ButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
        }
    }
}
#endif
