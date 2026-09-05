using System;
using System.IO;
using GameBase;
using GameMain;
using UnityGameFramework.Runtime;

namespace GameLogic
{
    /// <summary>
    /// 试验方案读取系统
    /// 负责读取并处理试验方案文件
    /// </summary>
    public class SchemeSys : BaseLogicSys<SchemeSys>
    {

        #region 保存

        /// <summary>
        /// 处理开始保存试验方案文件事件
        /// </summary>
        /// <param name="savePath">试验方案文件保存路径</param>
        private void OnSaveStartSchemeFile(string savePath)
        {
            Log.Info("SchemeSys: 开始保存试验方案文件: {0}", savePath);
            // 在m_SavedPathText路径尾部按日期添加一个文件夹
            var folderPath = savePath;
            // 按日期命名
            var folderName = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            // 组合路径
            folderPath = Path.Combine(folderPath, folderName);
            // 创建文件夹
            // 如果不存在
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            // 广播方案文件夹创建完成事件，供其他模块使用
            GameEvent.EventMgr.GetInterface<IActorLogicEvent_Scheme>().OnSchemeFolderCreated(folderPath);

            // 准备好保存路径
            var eyeTrackingFileFullPath = folderPath + "\\" + string.Format(GameMain.Constant.File.EyesTrackingFile, folderName);
            var gazeTrackingFileFullPath = folderPath + "\\" + string.Format(GameBase.Constant.File.GazeTrackingFile, folderName);

            // 让眼动数据开始记录并保存
            GameModule.Event.Fire(GazeSavedEventArgs.EventId, GazeSavedEventArgs.Create(true, eyeTrackingFileFullPath));
            // 让凝视点数据开始记录并保存
            GameEvent.EventMgr.GetInterface<IActorLogicEvent_EyesTracking>().SaveStart(gazeTrackingFileFullPath);

            // 构建试验方案文件
            BuildSchemeFile(folderPath, folderName, eyeTrackingFileFullPath, gazeTrackingFileFullPath);
        }
        
        /// <summary>
        /// 处理停止保存试验方案文件事件
        /// </summary>
        /// <param name="savePath">试验方案文件保存路径</param>
        private void OnSaveCompleteSchemeFile(string savePath)
        {
            Log.Info("SchemeSys: 停止保存试验方案文件: {0}", savePath);
            // 让眼动数据停止记录并保存
            GameModule.Event.Fire(GazeSavedEventArgs.EventId, GazeSavedEventArgs.Create(false, savePath));
            // 让凝视点数据停止记录并保存
            GameEvent.EventMgr.GetInterface<IActorLogicEvent_EyesTracking>().SaveComplete(savePath);
        }

        #endregion

        #region 试验方案文件构建

        /// <summary>
        /// 构建试验方案文件
        /// </summary>
        /// <param name="folderPath">保存文件夹路径</param>
        /// <param name="folderName">文件夹名称</param>
        /// <param name="eyesTrackingFilePath">眼动数据文件完整路径</param>
        /// <param name="gazeTrackingFilePath">凝视点数据文件完整路径</param>
        private void BuildSchemeFile(string folderPath, string folderName, string eyesTrackingFilePath, string gazeTrackingFilePath)
        {
            try
            {
                // 创建试验方案数据对象
                ExperimentSchemeData schemeData = ExperimentSchemeManager.Instance.CreateScheme(folderName, "眼动追踪试验数据采集");

                // 添加眼动数据文件信息
                string eyesTrackingFileName = Path.GetFileName(eyesTrackingFilePath);
                schemeData.AddDataFile("EyesTracking", eyesTrackingFileName, "眼动追踪数据文件");

                // 添加凝视点数据文件信息
                string gazeTrackingFileName = Path.GetFileName(gazeTrackingFilePath);
                schemeData.AddDataFile("GazeTracking", gazeTrackingFileName, "凝视点追踪数据文件");

                // 构建试验方案文件路径
                string schemeFilePath = Path.Combine(folderPath, folderName + "." + GameBase.Constant.File.TrialDataFileExtension);

                // 保存试验方案文件
                bool saveSuccess = ExperimentSchemeManager.Instance.SaveScheme(schemeFilePath, schemeData);
                if (saveSuccess)
                {
                    Log.Info("SchemeSys: 成功创建试验方案文件: {0}", schemeFilePath);
                }
                else
                {
                    Log.Error("SchemeSys: 创建试验方案文件失败: {0}", schemeFilePath);
                }
            }
            catch (Exception ex)
            {
                Log.Error("SchemeSys: 构建试验方案文件异常: {0}", ex.Message);
            }
        }

        #endregion

        #region 生命周期

        /// <summary>
        /// 初始化眼动数据读取系统
        /// </summary>
        public override bool OnInit()
        {
            if (!base.OnInit())
                return false;

            Log.Info("SchemeSys OnInit");

            // 注册事件监听
            RegisterEvents();

            return true;
        }

        /// <summary>
        /// 取消初始化眼动数据读取系统
        /// </summary>
        public override void OnUnInit()
        {
            // 注销事件监听
            UnRegisterEvents();

            base.OnUnInit();
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 注册事件监听
        /// </summary>
        private void RegisterEvents()
        {
            // 注册读取试验方案文件事件
            GameEvent.AddEventListener<string>(IActorLogicEvent_Scheme_Event.OnRead, OnReadSchemeFile);
            // 注册保存试验方案文件事件
            GameEvent.AddEventListener<string>(IActorLogicEvent_Scheme_Event.OnSaveStart, OnSaveStartSchemeFile);
            // 注册停止保存试验方案文件事件
            GameEvent.AddEventListener<string>(IActorLogicEvent_Scheme_Event.OnSaveComplete, OnSaveCompleteSchemeFile);
        }

        /// <summary>
        /// 注销事件监听
        /// </summary>
        private void UnRegisterEvents()
        {
            // 注销读取试验方案文件事件
            GameEvent.RemoveEventListener<string>(IActorLogicEvent_Scheme_Event.OnRead, OnReadSchemeFile);
            // 注销保存试验方案文件事件
            GameEvent.RemoveEventListener<string>(IActorLogicEvent_Scheme_Event.OnSaveStart, OnSaveStartSchemeFile);
            // 注销停止保存试验方案文件事件
            GameEvent.RemoveEventListener<string>(IActorLogicEvent_Scheme_Event.OnSaveComplete, OnSaveCompleteSchemeFile);
        }

        /// <summary>
        /// 处理读取试验方案文件事件
        /// </summary>
        /// <param name="schemeFilePath">试验方案文件完整路径（.vbsoedtd）</param>
        public void OnReadSchemeFile(string schemeFilePath)
        {
            Log.Info("SchemeSys: 开始读取试验方案文件: {0}", schemeFilePath);

            // 使用试验方案管理器加载方案
            ExperimentSchemeData schemeData = ExperimentSchemeManager.Instance.LoadScheme(schemeFilePath);
            if (schemeData == null)
            {
                Log.Error("SchemeSys: 加载试验方案文件失败: {0}", schemeFilePath);
                return;
            }

            Log.Info("SchemeSys: 成功加载试验方案 '{0}', 包含 {1} 个数据文件",
                schemeData.ExperimentName, schemeData.GetDataFileCount());

            // 获取方案文件所在目录
            string baseDirectory = ExperimentSchemeManager.Instance.GetSchemeDirectory(schemeFilePath);

            // 优先加载 GazeTracking 类型数据文件
            DataFileInfo gazeTrackingFile = schemeData.GetDataFileByType("GazeTracking");
            if (gazeTrackingFile != null)
            {
                string gazeTrackingPath = gazeTrackingFile.GetFullPath(baseDirectory);
                if (!string.IsNullOrEmpty(gazeTrackingPath) && File.Exists(gazeTrackingPath))
                {
                    Log.Info("SchemeSys: 读取 GazeTracking 数据文件: {0}", gazeTrackingPath);
                    // 发送读取注视数据事件
                    //GameEvent.EventMgr.GetInterface<IActorLogicEvent_Gaze>().OnRead(gazeTrackingPath);
                }
                else
                {
                    Log.Warning("SchemeSys: GazeTracking 数据文件不存在: {0}", gazeTrackingPath);
                }
            }

            // 如果没有 GazeTracking，尝试加载 EyesTracking 类型数据文件
            DataFileInfo eyesTrackingFile = schemeData.GetDataFileByType("EyesTracking");
            if (eyesTrackingFile != null)
            {
                string eyesTrackingPath = eyesTrackingFile.GetFullPath(baseDirectory);
                if (!string.IsNullOrEmpty(eyesTrackingPath) && File.Exists(eyesTrackingPath))
                {
                    Log.Info("SchemeSys: 读取 EyesTracking 数据文件: {0}", eyesTrackingPath);
                    // 发送读取眼动数据事件
                    //GameEvent.EventMgr.GetInterface<IActorLogicEvent_EyesTracking>().OnRead(eyesTrackingPath);
                }
                else
                {
                    Log.Warning("SchemeSys: EyesTracking 数据文件不存在: {0}", eyesTrackingPath);
                }
            }
        }

        #endregion

    }
}