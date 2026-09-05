﻿using UnityGameFramework.Runtime;
using UnityEngine.UI;
using UnityEngine;
using TMPro;
using System;
using System.IO;
using GameMain;
using Crosstales.FB;
using Mirror;

namespace GameLogic
{
    /// <summary>
    /// 管理主界面
    /// </summary>
    public class ManagerMainForm : UIFormLogic
    {
        #region 界面通用

        [Header("Common")]
        [Tooltip("返回按钮")]
        [SerializeField]
        private Button m_ReturnLastButton;

        [Tooltip("用户名")]
        [SerializeField]
        private TextMeshProUGUI m_UserName;

        private void OnReturnLastInSS()
        {
            // 停止试验
            GameEvent.EventMgr.GetInterface<IActorLogicEvent>().OnStopTrial();
        }

        private void OnStopTrial()
        {
            if (TryGetComponent<UIForm>(out var uiForm))
            {
                // 关闭自身
                GameModule.UI.CloseUIForm(uiForm);
            }
        }

        #endregion

        #region 采集

        [Header("Collection")]

        [Tooltip("界面根对象")]
        [SerializeField]
        private GameObject m_CollectUIRoot;

        [Tooltip("采集按钮")]
        [SerializeField]
        private Button m_CollectButton;

        private void OnEnterCollectMode()
        {
            // 输出日志
            Log.Warning("采集开发中");
        }

        #region 选择保存路径

        [Header("Save Path")]
        [Tooltip("选择保存路径按钮")]
        [SerializeField]
        private Button m_SelectSavePathButton;

        /// <summary>
        /// 保存路径的PlayerPrefs键
        /// </summary>
        private const string SAVED_PATH_KEY = "LastSavedPath";

        /// <summary>
        /// Placeholder shown until the user selects a save folder.
        /// </summary>
        private const string SAVE_PATH_PLACEHOLDER = "Select a save folder";

        /// <summary>
        /// 当前方案文件夹路径（由SchemeSys创建时广播）
        /// </summary>
        private string m_CurrentSchemeFolderPath = string.Empty;

        /// <summary>
        /// 选择保存路径
        /// </summary>
        private void OnSelectSavePath()
        {
            string initialPath = HasConfiguredSavePath()
                ? m_SavedPathText.text
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string path = FileBrowser.Instance.OpenSingleFolder("Select Save Folder", initialPath);
            // 输出日志
            Log.Info("选择保存路径：" + path);

            if (!string.IsNullOrEmpty(path))
            {
                m_SavedPathText.text = path;
                // 保存路径到PlayerPrefs
                PlayerPrefs.SetString(SAVED_PATH_KEY, path);
                PlayerPrefs.Save();
                // Keep the performance option visible at all times, and only
                // unlock the recording control after a valid path is selected.
                m_RecordFunRoot.SetActive(true);
                m_RecordToggle.interactable = true;
            }
        }

        #endregion

        #region 录制

        [Header("Record")]
        [Tooltip("录制区根对象")]
        [SerializeField]
        private GameObject m_RecordFunRoot;

        [Tooltip("录制按钮")]
        [SerializeField]
        private Toggle m_RecordToggle;

        [Tooltip("开始录制按钮文本")]
        [SerializeField]
        private GameObject m_StartRecordText;

        [Tooltip("停止录制按钮文本")]
        [SerializeField]
        private GameObject m_StopRecordText;

        [Tooltip("保存路径")]
        [SerializeField]
        private TextMeshProUGUI m_SavedPathText;

        [Tooltip("录制时间")]
        [SerializeField]
        private TextMeshProUGUI m_RecordTime;

        [Header("Performance Monitor")]
        [Tooltip("性能监控Toggle")]
        [SerializeField]
        private Toggle m_PerformanceMonitorToggle;

        [Tooltip("性能监控器实例")]
        private NetworkPerformanceMonitor m_PerformanceMonitor;

        private bool m_PerformanceRecordingPending;
        private string m_PendingPerformanceFolderPath = string.Empty;

        /// <summary>
        /// 当前实验结束时是否需要归档 Unity 日志。
        /// </summary>
        private bool m_UnityLogArchivePending;

        /// <summary>
        /// 当前实验开始时间，用于生成唯一的日志文件名。
        /// </summary>
        private DateTime m_UnityLogRecordingStartedAt;

        [Tooltip("是否已初始化性能监控")]
        private bool m_PerformanceMonitorInitialized = false;
        
        [Tooltip("商品运动数据录制器实例")]
        private ProductMovementRecorder m_ProductMovementRecorder;

        [Tooltip("是否已初始化商品运动数据录制器")]
        private bool m_ProductMovementRecorderInitialized = false;

        private void OnStartStopRecord(bool isOn)
        {
            string savePath = string.Empty;

            // 开始录制
            if (isOn)
            {
                // Resolve a writable path. The old implementation disabled this
                // toggle while the placeholder was visible, so clicks were silently
                // ignored and no recording callback was ever invoked.
                if (!TryResolveSavePath(out savePath))
                {
                    m_RecordToggle.SetIsOnWithoutNotify(false);
                    return;
                }

                m_CurrentSchemeFolderPath = string.Empty;
                m_PerformanceRecordingPending = false;
                m_PendingPerformanceFolderPath = string.Empty;
                // 重置时间
                ResetRecordData();

                // 如果性能监控Toggle勾选，则启动性能监控
                bool shouldRecordPerformance =
                    m_PerformanceMonitorToggle != null && m_PerformanceMonitorToggle.isOn;
                if (shouldRecordPerformance && !StartPerformanceMonitor())
                {
                    Log.Error("Performance recording could not start because the network monitor is unavailable.");
                    m_RecordToggle.SetIsOnWithoutNotify(false);
                    return;
                }

                if (shouldRecordPerformance)
                {
                    // Start a fresh low-RTT synchronization burst. Directional
                    // recording is deferred until UDPClockSync reports Ready.
                    UDPClockSync.RequestImmediateSyncBurst();
                    Log.Info("Clock synchronization started; VR-to-PC delay remains invalid until sync state is Ready.");
                }

                PrepareUnityLogArchive();
                
                // 启动商品运动数据录制
                StartProductMovementRecorder();
            }
            else
            {
                // 停止性能监控并导出数据
                StopPerformanceMonitorAndExport();
                // 停止商品运动数据录制
                StopProductMovementRecorderAndExport();
            }

            //
            m_StartRecordText.SetActive(!isOn);
            m_StopRecordText.SetActive(isOn);

            // 发送事件
            var schemeEvent = GameEvent.EventMgr.GetInterface<IActorLogicEvent_Scheme>();
            if (isOn)
            {
                schemeEvent.SaveStart(m_SavedPathText.text);

                // SchemeSys normally creates a timestamped session folder and
                // synchronously invokes OnSchemeFolderCreated. Keep a direct
                // fallback path, but defer opening the performance CSV until clock
                // synchronization is measurement-ready.
                if (m_PerformanceMonitorToggle != null &&
                    m_PerformanceMonitorToggle.isOn &&
                    m_PerformanceMonitor != null &&
                    !m_PerformanceMonitor.IsRecording &&
                    string.IsNullOrEmpty(m_PendingPerformanceFolderPath))
                {
                    SchedulePerformanceRecording(savePath, "scheme-folder event was not received");
                }
            }
            else
            {
                schemeEvent.SaveComplete(m_SavedPathText.text);
                ArchiveUnityLog();
            }
        }

        /// <summary>
        /// 为本次实验准备 Unity 日志归档，并在原日志中写入清晰的开始标记。
        /// </summary>
        private void PrepareUnityLogArchive()
        {
            m_UnityLogRecordingStartedAt = DateTime.Now;
            m_UnityLogArchivePending = true;
            Log.Info($"[ExperimentRecording] Unity log capture started at " +
                     $"{m_UnityLogRecordingStartedAt:yyyy-MM-dd HH:mm:ss.fff}.");
        }

        /// <summary>
        /// 将当前 Unity Editor/Player 日志快照复制到当前实验文件夹。
        /// 日志文件在 Unity 运行时会被占用，因此使用共享读方式复制。
        /// </summary>
        private void ArchiveUnityLog()
        {
            if (!m_UnityLogArchivePending)
            {
                return;
            }

            m_UnityLogArchivePending = false;
            Log.Info($"[ExperimentRecording] Recording stopped at " +
                     $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}; archiving Unity log.");

            string sourcePath = ResolveUnityLogPath();
            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
            {
                Log.Warning($"Unity log archive skipped: source log was not found at '{sourcePath}'.");
                return;
            }

            string folderPath = string.IsNullOrEmpty(m_CurrentSchemeFolderPath)
                ? m_SavedPathText.text.Trim()
                : m_CurrentSchemeFolderPath;

            try
            {
                Directory.CreateDirectory(folderPath);
                string destinationPath = Path.Combine(
                    folderPath,
                    $"UnityLog_{m_UnityLogRecordingStartedAt:yyyyMMdd_HHmmss_fff}.log");

                using (var sourceStream = new FileStream(
                           sourcePath,
                           FileMode.Open,
                           FileAccess.Read,
                           FileShare.ReadWrite | FileShare.Delete))
                using (var destinationStream = new FileStream(
                           destinationPath,
                           FileMode.Create,
                           FileAccess.Write,
                           FileShare.Read))
                {
                    sourceStream.CopyTo(destinationStream);
                    destinationStream.Flush();
                }

                Log.Info($"Unity log archived to: {destinationPath}");
            }
            catch (Exception exception)
            {
                Log.Error($"Unable to archive Unity log '{sourcePath}': {exception.Message}");
            }
        }

        private static string ResolveUnityLogPath()
        {
            string logPath = Application.consoleLogPath;
            if (!string.IsNullOrEmpty(logPath) && File.Exists(logPath))
            {
                return logPath;
            }

#if UNITY_EDITOR_WIN
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(localAppData))
            {
                return Path.Combine(localAppData, "Unity", "Editor", "Editor.log");
            }
#endif

            return logPath;
        }

        /// <summary>
        /// 初始化性能监控
        /// </summary>
        private bool InitializePerformanceMonitor()
        {
            if (m_PerformanceMonitorInitialized && m_PerformanceMonitor != null)
            {
                return true;
            }

            m_PerformanceMonitor = NetworkPerformanceMonitor.Instance;
            if (m_PerformanceMonitor != null)
            {
                m_PerformanceMonitorInitialized = true;
                return true;
            }

            // 获取NetworkManager实例
            NetworkManager networkManager = NetworkManager.singleton;
            if (networkManager == null)
            {
                Log.Error("NetworkManager not found; performance monitor initialization failed.");
                return false;
            }

            // 使用集成助手设置性能监控
#if UNITY_EDITOR
            m_PerformanceMonitor = NetworkPerformanceIntegration.SetupNetworkPerformanceMonitor(networkManager);
#else
            m_PerformanceMonitor = NetworkPerformanceIntegration.SetupNetworkPerformanceMonitor(networkManager, false);
#endif
            m_PerformanceMonitorInitialized = m_PerformanceMonitor != null;
            if (m_PerformanceMonitorInitialized)
            {
                Log.Info("Performance monitor initialized.");
            }

            return m_PerformanceMonitorInitialized;
        }

        /// <summary>
        /// 启动性能监控
        /// </summary>
        private bool StartPerformanceMonitor()
        {
            // 确保性能监控已初始化
            if (!InitializePerformanceMonitor())
            {
                return false;
            }

            // 启用性能监控
            if (m_PerformanceMonitor != null)
            {
                m_PerformanceMonitor.IsEnabled = true;
                m_PerformanceMonitor.ResetStatistics();

                // The monitor may be created after Mirror's OnStartServer callback,
                // so synchronize the PC/VR link state when recording begins.
                // Packet-driven eye/mocap links reactivate themselves.
                if (NetworkServer.active || NetworkClient.active)
                {
                    m_PerformanceMonitor.UpdateLinkActivity("PC<->VR 状态同步");
                }

                Log.Info("Performance monitor started.");
                return true;
            }

            return false;
        }
        
        /// <summary>
        /// 初始化商品运动数据录制器
        /// </summary>
        private void InitializeProductMovementRecorder()
        {
            if (m_ProductMovementRecorderInitialized) return;
            
            // 检查是否已存在
            m_ProductMovementRecorder = ProductMovementRecorder.Instance;
            m_ProductMovementRecorderInitialized = true;
            Log.Info("Product movement recorder initialized.");
        }

        /// <summary>
        /// 启动商品运动数据录制
        /// </summary>
        private void StartProductMovementRecorder()
        {
            // 确保已初始化
            if (!m_ProductMovementRecorderInitialized)
            {
                InitializeProductMovementRecorder();
            }
        }

        /// <summary>
        /// 停止性能监控并结束录制
        /// </summary>
        private void StopPerformanceMonitorAndExport()
        {
            if (m_PerformanceMonitor == null) return;

            bool wasPending = m_PerformanceRecordingPending;
            m_PerformanceRecordingPending = false;
            m_PendingPerformanceFolderPath = string.Empty;

            // 禁用性能监控
            m_PerformanceMonitor.IsEnabled = false;
            
            // 停止实时录制
            if (m_PerformanceMonitor.IsRecording)
            {
                string filePath = m_PerformanceMonitor.StopRecording();
                Log.Info($"Performance monitor stopped, recording saved to: {filePath}");
            }
            else if (!wasPending)
            {
                // 如果没有正在录制，回退到普通导出
                string exportFolderPath = string.IsNullOrEmpty(m_CurrentSchemeFolderPath) 
                    ? m_SavedPathText.text 
                    : m_CurrentSchemeFolderPath;

                string csvPath = System.IO.Path.Combine(exportFolderPath,
                    $"NetworkPerformance_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                m_PerformanceMonitor.ExportToCsv(csvPath);

                Log.Info($"Performance monitor stopped, data exported to: {csvPath}");
            }
            else
            {
                Log.Warning("Performance recording stopped before clock synchronization became ready; no directional latency CSV was created.");
            }
        }
        
        /// <summary>
        /// 停止商品运动数据录制并导出
        /// </summary>
        private void StopProductMovementRecorderAndExport()
        {
            if (m_ProductMovementRecorder == null) return;
            
            // 停止实时录制
            if (m_ProductMovementRecorder.IsRecording)
            {
                string filePath = m_ProductMovementRecorder.StopRecording();
                Log.Info($"Product movement recorder stopped, recording saved to: {filePath}");
            }
            else
            {
                // 如果没有正在录制，也需要处理
                Log.Info("Product movement recorder was not recording.");
            }
        }

        /// <summary>
        /// 处理方案文件夹创建事件
        /// </summary>
        /// <param name="folderPath">方案文件夹完整路径</param>
        private void OnSchemeFolderCreated(string folderPath)
        {
            m_CurrentSchemeFolderPath = folderPath;
            Log.Info($"ManagerMainForm: 方案文件夹已创建: {folderPath}");
            
            // 实时录制开始录制到方案文件夹
            if (m_PerformanceMonitorToggle != null &&
                m_PerformanceMonitorToggle.isOn &&
                m_PerformanceMonitor != null &&
                m_PerformanceMonitor.IsEnabled)
            {
                SchedulePerformanceRecording(folderPath, "scheme folder created");
            }
            
            // 商品运动数据录制开始录制到方案文件夹
            // 确保已初始化
            if (!m_ProductMovementRecorderInitialized)
            {
                InitializeProductMovementRecorder();
            }
            
            if (m_ProductMovementRecorder != null)
            {
                string csvPath = System.IO.Path.Combine(folderPath,
                    $"ProductMovement_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                m_ProductMovementRecorder.StartRecording(csvPath);
                Log.Info($"ManagerMainForm: 开始商品运动数据录制: {csvPath}");
            }
        }

        private void ResetRecordData()
        {
            m_RecordTime.text = "00:00:00";
            m_RecordedTimeSec = 0f;
        }

        /// <summary>
        /// 加载上一次保存的路径
        /// </summary>
        private void LoadLastSavedPath()
        {
            if (PlayerPrefs.HasKey(SAVED_PATH_KEY))
            {
                string lastPath = PlayerPrefs.GetString(SAVED_PATH_KEY);
                m_SavedPathText.text = lastPath;
                Log.Info("加载上次保存路径：" + lastPath);
            }
            else
            {
                m_SavedPathText.text = SAVE_PATH_PLACEHOLDER;
            }
        }

        private bool HasConfiguredSavePath()
        {
            return m_SavedPathText != null &&
                   !string.IsNullOrWhiteSpace(m_SavedPathText.text) &&
                   !m_SavedPathText.text.Trim().Equals(
                       SAVE_PATH_PLACEHOLDER,
                       StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Resolves a writable recording root. If the operator has not selected a
        /// folder, use a visible folder under Documents instead of leaving the
        /// Start Recording control disabled without feedback.
        /// </summary>
        private bool TryResolveSavePath(out string savePath)
        {
            savePath = HasConfiguredSavePath() ? m_SavedPathText.text.Trim() : string.Empty;
            if (string.IsNullOrEmpty(savePath))
            {
                string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (string.IsNullOrEmpty(documentsPath))
                {
                    documentsPath = Application.persistentDataPath;
                }

                savePath = Path.Combine(documentsPath, "VBSOED", "Recordings");
            }

            try
            {
                Directory.CreateDirectory(savePath);
                m_SavedPathText.text = savePath;
                PlayerPrefs.SetString(SAVED_PATH_KEY, savePath);
                PlayerPrefs.Save();
                return true;
            }
            catch (Exception exception)
            {
                Log.Error($"Unable to use recording folder '{savePath}': {exception.Message}");
                savePath = string.Empty;
                return false;
            }
        }

        private bool TryStartPerformanceRecording(string folderPath, string source)
        {
            if (m_PerformanceMonitor == null)
            {
                Log.Error($"Performance recording failed ({source}): monitor is unavailable.");
                return false;
            }

            if (m_PerformanceMonitor.IsRecording)
            {
                return true;
            }

            try
            {
                Directory.CreateDirectory(folderPath);
                string csvPath = Path.Combine(
                    folderPath,
                    $"NetworkPerformance_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                bool started = m_PerformanceMonitor.StartRecording(csvPath);
                if (!started || !m_PerformanceMonitor.IsRecording)
                {
                    Log.Error($"Performance recording failed ({source}): {csvPath}");
                    return false;
                }

                Log.Info($"Performance recording started ({source}): {csvPath}");
                return true;
            }
            catch (Exception exception)
            {
                Log.Error($"Performance recording failed ({source}): {exception.Message}");
                return false;
            }
        }

        private void SchedulePerformanceRecording(string folderPath, string source)
        {
            m_PendingPerformanceFolderPath = folderPath;
            m_PerformanceRecordingPending = true;

            if (UDPClockSync.IsMeasurementReady)
            {
                TryStartPendingPerformanceRecording(source);
            }
            else
            {
                Log.Info($"Performance recording is waiting for clock synchronization ({source}).");
            }
        }

        private void TryStartPendingPerformanceRecording(string source)
        {
            if (!m_PerformanceRecordingPending ||
                string.IsNullOrEmpty(m_PendingPerformanceFolderPath) ||
                !UDPClockSync.IsMeasurementReady)
            {
                return;
            }

            if (TryStartPerformanceRecording(m_PendingPerformanceFolderPath, source))
            {
                m_PerformanceRecordingPending = false;
                Log.Info($"Clock synchronization ready; performance recording started. " +
                         $"RTT={UDPClockSync.LastRTT:F2}ms, offset={UDPClockSync.ClockDiff:F2}ms, " +
                         $"uncertainty<={UDPClockSync.OffsetUncertaintyMs:F2}ms.");
            }
        }

        private void ResetRecordUI()
        {
            ResetRecordData();
            m_UnityLogArchivePending = false;
            // Keep recording available. TryResolveSavePath chooses a safe default
            // when the user starts without first browsing for a folder.
            m_RecordFunRoot.SetActive(true);
            m_RecordToggle.interactable = true;
            m_RecordToggle.SetIsOnWithoutNotify(false);
        }

        /// <summary>
        /// 录制时间
        /// </summary>
        private float m_RecordedTimeSec = 0f;

        /// <summary>
        /// 录制时间开始计时
        /// </summary>
        private void OnRecordTimingUpdate()
        {
            // 每帧更新1秒
            m_RecordedTimeSec += Time.deltaTime;
            // 格式化时间
            m_RecordTime.text = TimeSpan.FromSeconds(m_RecordedTimeSec).ToString(@"hh\:mm\:ss");
        }

        #endregion

        #endregion

        #region 生命周期

        protected override void OnOpen(object userData)
        {
            base.OnOpen(userData);
#if MANAGER_SERVER
            // 设置用户名
            m_UserName.text = AppData.UserName;
#endif
            // 添加监听
            m_CollectButton.onClick.AddListener(OnEnterCollectMode);
            m_ReturnLastButton.onClick.AddListener(OnReturnLastInSS);
            m_RecordToggle.onValueChanged.AddListener(OnStartStopRecord);
            m_SelectSavePathButton.onClick.AddListener(OnSelectSavePath);
            GameEvent.AddEventListener(IActorLogicEvent_Event.OnStopTrial, OnStopTrial);
            GameEvent.AddEventListener<string>(IActorLogicEvent_Scheme_Event.OnSchemeFolderCreated, OnSchemeFolderCreated);

            // 加载上一次保存的路径
            LoadLastSavedPath();
            // 重置录制UI状态
            ResetRecordUI();
        }

        public override void OnClose(bool isShutdown, object userData)
        {
            base.OnClose(isShutdown, userData);
            // 移除监听
            m_CollectButton.onClick.RemoveListener(OnEnterCollectMode);
            m_ReturnLastButton.onClick.RemoveListener(OnReturnLastInSS);
            m_RecordToggle.onValueChanged.RemoveListener(OnStartStopRecord);
            m_SelectSavePathButton.onClick.RemoveListener(OnSelectSavePath);
            GameEvent.RemoveEventListener(IActorLogicEvent_Event.OnStopTrial, OnStopTrial);
            GameEvent.RemoveEventListener<string>(IActorLogicEvent_Scheme_Event.OnSchemeFolderCreated, OnSchemeFolderCreated);
        }

        public override void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
            if (m_PerformanceRecordingPending && m_RecordToggle.isOn)
            {
                TryStartPendingPerformanceRecording("clock synchronization became ready");
            }

            if (m_RecordToggle.isOn)
            {
                OnRecordTimingUpdate();
            }
        }

        #endregion
    }
}
