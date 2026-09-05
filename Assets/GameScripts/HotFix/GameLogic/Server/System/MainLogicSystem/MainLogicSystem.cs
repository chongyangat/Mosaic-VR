using GameBase;
using GameFramework.Event;
using GameMain;
using UGFExtensions.Await;
using UnityGameFramework.Runtime;
using Mirror;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Mirror.Discovery;
using GameProto;
using System.Collections.Generic;
using cfg;
using System.Linq;
using XNF.BaseFrame.EyeTracking;
using UnityEngine.SceneManagement;


#if MANAGER_SERVER
using ThirdPersonCamera;
using System.Text;
#else
using MetaQuestProEyeGazeUDP;
# endif

namespace GameLogic
{
    /// <summary>
    /// 主逻辑系统。
    /// TODO 项目赶进度关系，没时间拆分内容，导致单个代码文件内容过多
    /// </summary>
    public sealed class MainLogicSystem : BaseLogicSys<MainLogicSystem>
    {
        private const string RemoteQuestRecenterStateName = "RemoteQuestRecenter";

#if MANAGER_SERVER
        private const float RemoteQuestRecenterCooldown = 3f;
        private float m_NextRemoteQuestRecenterTime;
#endif

        #region 视角恢复

        /// <summary>
        /// 根据动捕修正视角
        /// </summary>
        private void OnFixViewByMotionCapture()
        {
#if !UNITY_EDITOR
            // 使用 Launcher 场景中明确的动捕数字人，避免同名对象导致取错目标。
            var motionCaptureRoot = GameObject.Find("MotionCapAvatar");
            var avatarTransform = motionCaptureRoot != null
                ? motionCaptureRoot.transform.Find("avatar_001")
                : null;
            if (avatarTransform == null || !avatarTransform.gameObject.activeInHierarchy)
            {
                Log.Warning("OnFixViewByMotionCapture: Active MotionCapAvatar/avatar_001 was not found");
                return;
            }

            var avatar = avatarTransform.gameObject;
            var animator = avatar.GetComponent<Animator>();
            if (animator == null || !animator.isHuman)
            {
                Log.Warning("OnFixViewByMotionCapture: Avatar has no valid humanoid Animator");
                return;
            }

            var leftEye = animator.GetBoneTransform(HumanBodyBones.LeftEye);
            var rightEye = animator.GetBoneTransform(HumanBodyBones.RightEye);
            if (leftEye == null || rightEye == null)
            {
                Log.Warning("OnFixViewByMotionCapture: Avatar eye bones were not found");
                return;
            }

            // 位置严格采用数字人双眼中点；朝向采用数字人的水平前向，避免动捕
            // 产生的轻微俯仰/翻滚被写入整个 TrackingSpace。
            var eyePos = (leftEye.position + rightEye.position) * 0.5f;
            var avatarForward = Vector3.ProjectOnPlane(avatarTransform.forward, Vector3.up);
            if (avatarForward.sqrMagnitude < 0.0001f)
            {
                Log.Warning("OnFixViewByMotionCapture: Avatar forward direction is invalid");
                return;
            }
            var eyeRot = Quaternion.LookRotation(avatarForward.normalized, Vector3.up);

            // 带目标位姿的事件现在表示“立即精确对齐”，不再受自动异常检测阈值限制。
            GameModule.Event.Fire(SetOVRTrackingRestoreEventArgs.EventId,
                SetOVRTrackingRestoreEventArgs.Create(true, false, eyePos, eyeRot));
            Log.Info(
                $"OnFixViewByMotionCapture: Exact avatar alignment requested, "
                + $"eyePos: {eyePos}, eyeRot: {eyeRot.eulerAngles}");
#endif
        }

        private void OnSystemRecenterAvatarAlignmentRequested(object sender, GameEventArgs e)
        {
            var args = e as RequestAvatarViewAlignmentEventArgs;
            Log.Info(
                $"System recenter avatar alignment requested: "
                + $"{args?.Reason ?? "unspecified"}");
            OnFixViewByMotionCapture();
        }

        #endregion

        #region 任务

        /// <summary>
        /// 任务列表对象预置体（带网络同步）
        /// </summary>
        private GameObject m_TasksListPrefab;

        /// <summary>
        /// 任务列表对象（带网络同步）
        /// </summary>
        private GameObject m_TasksListObj;

        /// <summary>
        /// 创建任务系统
        /// </summary>
        private void CreateTaskSys()
        {
            // 注册
            NetworkClient.RegisterPrefab(m_TasksListPrefab);
            // 实例化
#if MANAGER_SERVER
            m_TasksListObj = Object.Instantiate(m_TasksListPrefab);
            NetworkServer.Spawn(m_TasksListObj);
#endif
        }

        /// <summary>
        /// 销毁任务系统
        /// </summary>
        private void DestroyTaskSys()
        {
            if (null != m_TasksListObj)
            {
                NetworkClient.UnregisterPrefab(m_TasksListObj);
                NetworkServer.Destroy(m_TasksListObj);
            }
        }
#if MANAGER_SERVER

        /// <summary>
        /// 初始化任务
        /// </summary>
        /// <param name="taskListId">任务清单ID</param>
        private void InitTasks(int taskListId)
        {
            // 获取任务清单
            var task = ConfigSystem.Instance.Tables.TbTasks.GetOrDefault(taskListId);
            if (task == null)
            {
                // 输出日志
                Log.Warning($"InitTasks: No task list found with ID {taskListId}");
                return;
            }
            // 根据任务类型创建任务
            switch (task.Type)
            {
                case EObjType.PRODUCT:
                    {
                        var taskDatasList = ConfigSystem.Instance.Tables.TbSupermaketTasks.DataList
                            .FindAll(item => task.GroupIdsList.Contains(item.GroupId));
                        // 创建任务
                        var tasksList = new List<TaskVo>(taskDatasList.Count);

                        // 填充任务信息
                        taskDatasList.ForEach(item =>
                        {
                            // 获取商品名
                            var taskName = ConfigSystem.Instance.Tables.TbProductsList.Get(item.ProductIdsList.First()).Obj.Name;
                            // 列表转字符串，用,隔开
                            var proStr = new StringBuilder();
                            item.ProductIdsList.ForEach(id => proStr.Append($"{id},"));
                            // 去掉最后一个,
                            proStr.Remove(proStr.Length - 1, 1);
                            tasksList.Add(
                                new TaskVo(item.Id, taskName, proStr.ToString(), item.ProImgPath, item.RequiredCounts, 0,
                                item.GroupId));
                        });
                        // 让任务系统创建任务
                        GameEvent.EventMgr.GetInterface<ITaskLogicEvent>().AddList(tasksList);
                    }
                    break;

                case EObjType.OBSTACLE:
                    {
                        var taskDatasList = ConfigSystem.Instance.Tables.TbStreetTasks.DataList
                            .FindAll(item => task.GroupIdsList.Contains(item.GroupId));
                        // 创建任务
                        var tasksList = new List<TaskVo>(taskDatasList.Count);

                        // 填充任务信息
                        taskDatasList.ForEach(item =>
                        {
                            tasksList.Add(
                                new TaskVo(item.Id, item.TargetPoint, string.Empty, string.Empty, 1, 0,
                                item.GroupId));
                        });
                        // 让任务系统创建任务
                        GameEvent.EventMgr.GetInterface<ITaskLogicEvent>().AddList(tasksList);
                    }
                    break;
                default:
                    // 输出日志
                    Log.Warning($"InitTasks: Unsupported task type {task.Type}");
                    break;
            }
        }
#endif

        /// <summary>
        /// 打开任务界面
        /// </summary>
        /// <param name="sceneName"></param>
        /// <returns></returns>
        private async System.Threading.Tasks.Task OpenTaskUI(string sceneName)
        {
            // 打开任务界面
#if MANAGER_SERVER
            // 管理端
            // 根据场景名选择界面
            if (sceneName.StartsWith("Assets/AssetRaw/Scenes/Street/"))
            {
                await GameModule.UI.OpenUIFormAsync("Assets/AssetRaw/UI/Server/TaskS/StreetTasksListForm.prefab", "UI", 0, false, null);
            }
#else
            // 客户端
            if (sceneName.StartsWith("Assets/AssetRaw/Scenes/Street/"))
            {
                await GameModule.UI.OpenUIFormAsync("Assets/AssetRaw/UI/Client/TaskC/StreetTasksListForm.prefab", "3DUIInHand", 0, false, null);
            }
            // 先隐藏手部画布，避免手部画布一开始就挂在屏幕上，手都还没有触发。
            // 找到手部画布位置
            var handCanvas = GameObject.Find("UserClient/PalmMenu/Menu/Visuals/CanvasMenu");
            if (handCanvas != null)
            {
                // 隐藏手部画布
                handCanvas.SetActive(false);
            }
            else
            {
                // 输出日志
                Log.Warning("Open TaskUI: No hand canvas found");
            }
#endif
        }

        /// <summary>
        /// 关闭任务界面
        /// </summary>
        /// <param name="sceneName"></param>
        private void CloseTaskUI(string sceneName)
        {
#if MANAGER_SERVER
            // 管理端
            // 根据场景名选择界面
            if (sceneName.StartsWith("Assets/AssetRaw/Scenes/Street/"))
            {
                // 关闭任务界面
                var uiForm = GameModule.UI.GetUIForm("Assets/AssetRaw/UI/Server/TaskS/StreetTasksListForm.prefab");
                if (uiForm != null)
                {
                    GameModule.UI.CloseUIForm(uiForm);
                }
                // 关闭任务结算界面
                uiForm = GameModule.UI.GetUIForm("Assets/AssetRaw/UI/Server/TaskS/StreetTasksResultForm.prefab");
                if (uiForm != null)
                {
                    GameModule.UI.CloseUIForm(uiForm);
                }
            }
#else
            // 客户端
            if (sceneName.StartsWith("Assets/AssetRaw/Scenes/Street/"))
            {
                // 关闭任务界面
                var uiForm = GameModule.UI.GetUIForm("Assets/AssetRaw/UI/Client/TaskC/StreetTasksListForm.prefab");
                if (uiForm != null)
                {
                    GameModule.UI.CloseUIForm(uiForm);
                }
                // 关闭任务结算界面
                uiForm = GameModule.UI.GetUIForm("Assets/AssetRaw/UI/Client/TaskC/StreetTasksResultForm.prefab");
                if (uiForm != null)
                {
                    GameModule.UI.CloseUIForm(uiForm);
                }
            }
#endif
        }

        #endregion

        #region 眼动

#if MANAGER_SERVER


#else
        /// 眼动管理器
        /// </summary>
        private UDPManager m_GazeManager;

        /// <summary>
        /// 初始化眼动模块
        /// </summary>
        /// <param name="revTargetIp">接收端目标IP</param>
        private void InitGaze(string revTargetIp)
        {
            // 启动用户端眼动模块UDP
            m_GazeManager = GameObject.Find("UserClient").transform.Find("MetaQuestProGaze/UDP").GetComponent<UDPManager>();
            // 用户端眼动模块的设置接收端（管理端）IP
            // 管理端只接收眼动数据，故IP随便设置，但不能为空，也不能是127.0.0.1的回环地址，否则会报错，暂时尽量不动眼动模块的外包方代码。
            m_GazeManager.SetIP(revTargetIp, m_GazeManager.m_SendTargetPort);
            // 输出日志，含IP
            Log.Info($"初始化眼动，设置接收IP为： {m_GazeManager.m_SendTargetIP}");
            // 启动眼动模块UDP
            m_GazeManager.gameObject.SetActive(true);
        }

        /// <summary>
        /// 启动眼动模块
        /// </summary>
        private void StartGaze()
        {
            // 输出日志
            Log.Info("启动眼动模块");
            m_GazeManager.gameObject.SetActive(true);
        }

        private void StopGaze()
        {
            // 输出日志
            Log.Info("停止眼动模块");
            m_GazeManager.gameObject.SetActive(false);
        }
#endif

        #endregion

        #region 界面

        /// <summary>
        /// 初始化开始界面
        /// </summary>
        /// <returns></returns>
        private System.Threading.Tasks.Task OpenStartUI()
        {
#if MANAGER_SERVER
            // 打开管理界面
            var uiPrefab = "Assets/AssetRaw/UI/Server/ManagerSetupForm.prefab";
            var data = ConfigSystem.Instance.Tables.TbScenes.DataList;
#else
            // 打开登录界面
            var uiPrefab = "Assets/AssetRaw/UI/Client/LoginForm.prefab";
            object data = null;
#endif
            // 如果已经打开了就不打开了
            if (!GameModule.UI.GetUIForm(uiPrefab))
            {
                return GameModule.UI.OpenUIFormAsync(uiPrefab, "UI", 0, false, data);
            }

            return System.Threading.Tasks.Task.CompletedTask;
        }

#if !MANAGER_SERVER

        /// <summary>
        /// 初始化调试界面
        /// </summary>
        /// <returns></returns>
        private System.Threading.Tasks.Task OpenDebugUI()
        {
            // 仅调试模式或设置显示帧率时才打开
            if (!m_IsShowFPS && GameModule.Debugger.ActiveWindowType != DebuggerActiveWindowType.AlwaysOpen)
            {
                return System.Threading.Tasks.Task.CompletedTask;
            }
            // 输出日志
            Log.Debug("打开帧率与版本界面");
            // 打开帧率与版本界面
            var uiPrefab = "Assets/AssetRaw/UI/Client/DebugInHandForm.prefab";
            // 如果已经打开了就不打开了
            if (!GameModule.UI.GetUIForm(uiPrefab))
            {
                return GameModule.UI.OpenUIFormAsync(uiPrefab, "DebugInHand", 0, false, null);
            }

            return System.Threading.Tasks.Task.CompletedTask;
        }
#endif

        #endregion

        #region 网络

        /// <summary>
        /// 网络发现模块
        /// </summary>
        private NetworkDiscovery m_NetworkDiscovery;

#if !MANAGER_SERVER
        /// <summary>
        /// Current PC endpoint on the EYE-VRLab WLAN. LAN discovery remains the primary
        /// connection path; this address is used only when discovery receives no reply.
        /// </summary>
        private const string ClientFallbackServerIp = "192.168.51.4";

        private const int ClientDiscoveryFallbackDelayMs = 6000;
        private int m_ClientDiscoveryGeneration;

        private void StartClientDiscoveryWithFallback()
        {
            if (NetworkClient.active || NetworkClient.isConnected)
            {
                return;
            }

            var roomManager = ASUNetworkRoomManager.singleton;
            if (m_NetworkDiscovery == null && roomManager != null)
            {
                m_NetworkDiscovery = roomManager.GetComponent<NetworkDiscovery>();
            }

            if (m_NetworkDiscovery != null)
            {
                m_NetworkDiscovery.StartDiscovery();
            }
            else
            {
                Log.Warning("LAN discovery is unavailable; the client will use the direct fallback endpoint.");
            }

            int generation = ++m_ClientDiscoveryGeneration;
            TryConnectFallbackServer(generation).Forget();
        }

        private async UniTaskVoid TryConnectFallbackServer(int generation)
        {
            await UniTask.Delay(ClientDiscoveryFallbackDelayMs, ignoreTimeScale: true);

            if (generation != m_ClientDiscoveryGeneration
                || NetworkClient.active
                || NetworkClient.isConnected)
            {
                return;
            }

            if (m_NetworkDiscovery != null)
            {
                m_NetworkDiscovery.StopDiscovery();
            }

            var roomManager = ASUNetworkRoomManager.singleton;
            if (roomManager == null)
            {
                Log.Warning("Direct fallback connection was cancelled because the network manager is no longer available.");
                return;
            }

            roomManager.networkAddress = ClientFallbackServerIp;
            Log.Warning(
                $"No LAN discovery reply after {ClientDiscoveryFallbackDelayMs / 1000}s; "
                + $"connecting directly to EYE-VRLab PC {ClientFallbackServerIp}:7777.");
            roomManager.StartClient();
            InitGaze(ClientFallbackServerIp);
            GameModule.Event.Fire(ServerDiscoveredEventArgs.EventId,
                ServerDiscoveredEventArgs.Create(ClientFallbackServerIp));
        }
#endif

        private void OnRoomDisOrConnectedChanged(object sender, GameEventArgs e)
        {
            var ne = (RoomDisOrConnectedChangedEventArgs)e;
            if (ne == null)
            {
                return;
            }
            if (ne.IsConnected)
            {
                Log.Info("OnClientConnected");
                // 发送客户端连接事件
                GameEvent.EventMgr.GetInterface<IActorLogicEvent>().OnClientConnected(NetworkClient.ready);
#if MANAGER_SERVER
                // 如果主机客户端已连接并且未准备好，则其自动准备。
                if (!NetworkClient.ready)
                {
                    NetworkClient.Ready();
                    if (NetworkClient.localPlayer == null)
                        NetworkClient.AddPlayer();
                }
#endif
            }
            else
            {
                Log.Info("OnClientDisconnected");
                // 重启启动网络发现模块
                // 输出日志
                Log.Info("Restart NetworkDiscovery");
#if MANAGER_SERVER
                m_NetworkDiscovery.StartDiscovery();
#else
                StartClientDiscoveryWithFallback();
#endif
                // 发送客户端断开连接事件
                GameEvent.EventMgr.GetInterface<IActorLogicEvent>().OnClientDisconnected();
            }

        }

        private void OnRoomAllPlayerChanged(object sender, GameEventArgs e)
        {
            var ne = (RoomAllPlayerReadyChangedEventArgs)e;
            if (ne == null)
            {
                return;
            }
            // 发送房间所有玩家变化事件
            GameEvent.EventMgr.GetInterface<IActorLogicEvent>().OnAllUserReadyChanged(ne.IsReady);
        }

#if !MANAGER_SERVER
        /// <summary>
        /// 准备就绪状态改变
        /// </summary>
        /// <param name="isReady"></param>
        private void OnUserReadyChange(bool isReady)
        {
            // 设置客户端准备就绪状态
            var roomPlayersList = GameObject.FindObjectsByType<NetworkRoomPlayer>(FindObjectsSortMode.None);
            foreach (var roomPlayer in roomPlayersList)
            {
                if (roomPlayer.isLocalPlayer)
                {
                    roomPlayer.CmdChangeReadyState(isReady);
                    break;
                }
            }
        }

        /// <summary>
        /// 找到服务端
        /// </summary>
        /// <param name="info"></param>
        private void OnDiscoveredServer(ServerResponse info)
        {
            Log.Info($"Discovered Server: {info.serverId} | {info.EndPoint} | {info.uri}");
            ++m_ClientDiscoveryGeneration;
            // 停止搜索服务端
            m_NetworkDiscovery.StopDiscovery();
            // 连接服务端
            ASUNetworkRoomManager.singleton.StartClient(info.uri);
            // 初始化眼动模块
            InitGaze(info.EndPoint.Address.ToString());
            // 触发服务器发现事件
            GameModule.Event.Fire(ServerDiscoveredEventArgs.EventId,
                ServerDiscoveredEventArgs.Create(info.EndPoint.Address.ToString()));
        }

#endif

        /// <summary>
        /// 网络同步状态改变后
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnNetSyncStateChanged(object sender, GameEventArgs e)
        {
            var ne = (NetSyncStateChangedEventArgs)e;
            if (ne == null)
            {
                return;
            }

            if (ne.StateName == RemoteQuestRecenterStateName)
            {
#if !MANAGER_SERVER
                string reason = string.IsNullOrEmpty(ne.State)
                    ? "manager remote recenter"
                    : ne.State;
                Log.Info($"Remote Quest recenter received: {reason}");
                GameModule.Event.Fire(
                    RequestOVRSystemRecenterEventArgs.EventId,
                    RequestOVRSystemRecenterEventArgs.Create(reason));
#endif
                return;
            }

            if (ne.StateName != "SyncScene")
            {
                return;
            }
            // 提取数据
            var data = ne.UserData as string;
            if (data == null)
            {
                // 输出日志
                Log.Warning("OnNetSyncStateChanged: data is null");
                return;
            }
            // 转换Json
            SceneSyncData sceneSyncData;
            try
            {
                sceneSyncData = JsonUtility.FromJson<SceneSyncData>(data);
                if (sceneSyncData == null)
                {
                    // 输出日志
                    Log.Warning("OnNetSyncStateChanged: sceneSyncData is null");
                    return;
                }
            }
            catch (System.Exception)
            {
                // 输出日志
                Log.Warning($"无法转换场景同步Json数据: {data}");
                return;
            }
            // 检查场景ID
            // 设置选择的场景
            SetSelScene(sceneSyncData.SceneId, sceneSyncData.ScenePath);
#if MANAGER_SERVER
            // 切换至游戏场景，让服务端开启试验。
            NetworkManager.singleton.ServerChangeScene(((ASUNetworkRoomManager)NetworkManager.singleton).GameplayScene); 
#endif
        }

        /// <summary>
        /// 初始化网络玩家对象
        /// </summary>
        private void InitNetPlayer()
        {
            if (NetworkClient.connection.isAuthenticated && NetworkClient.localPlayer == null)
            {
                // add player if existing one is null
                NetworkClient.AddPlayer();
            }
        }

        #endregion

        #region 开始与结束试验

        /// <summary>
        /// 当前选择的场景id
        /// </summary>
        private int m_CurSelSceneId;

        /// <summary>
        /// 当前选择的场景数据
        /// </summary>
        private SceneData m_CurSelScene;

        /// <summary>
        /// 场景同步数据
        /// </summary>
        class SceneSyncData
        {
            /// <summary>
            /// 场景ID
            /// </summary>
            public int SceneId;

            /// <summary>
            /// 场景路径
            /// </summary>
            public string ScenePath;
        }

        /// <summary>
        /// 设置选择的场景
        /// </summary>
        /// <param name="sceneId"></param>
        /// <param name="scenePath"></param>
        private void SetSelScene(int sceneId, string scenePath)
        {
            m_CurSelSceneId = sceneId;
            var sceneConfig = ConfigSystem.Instance.Tables.TbScenes.Get(sceneId);
            m_CurSelScene = sceneConfig.ScenesList.Find(scene => scene.Path.Equals(scenePath));
        }

#if MANAGER_SERVER

        /// <summary>
        /// 开始试验
        /// </summary>
        private void OnStartTrial(int sceneId, string scenePath)
        {
            // 清理所有模块
            ETDataModuleManager.Instance.Clear();
            // 同步场景
            GameModule.Event.Fire(NetSyncStateWillChangeEventArgs.EventId, 
                NetSyncStateWillChangeEventArgs.Create("SyncScene", null, ECallState.CLIENT_RPC, false, 
                new SceneSyncData()
                {
                    SceneId = sceneId,
                    ScenePath = scenePath
                }));
        }

        private void OnStopTrial()
        {
            // 切换至房间场景，让服务端停止试验。
            NetworkManager.singleton.ServerChangeScene(((ASUNetworkRoomManager)NetworkManager.singleton).RoomScene);
        }
#endif
        /// <summary>
        /// 试验是否开始
        /// </summary>
        private bool m_IsTrialStarted = false;

        /// <summary>
        /// 开始试验
        /// </summary>
        /// <returns></returns>
        private async void StartTrial()
        {
            // 输出日志
            Log.Info($"Start trail:SceneID:{m_CurSelSceneId}.");
            // 打开加载界面
            await GameModule.UI.OpenUIFormAsync("Assets/AssetRaw/UI/Loading/LoadingForm.prefab", "Top", 0, false, null);
            // 生成任务系统
            CreateTaskSys();
#if MANAGER_SERVER
            // GetSpawnObjsForScene(m_CurSelScene.ObjsSpawnListId);
            // 网络管理已经修改为游戏场景加载完不会自动就绪了
            NetworkClient.Ready();
            InitNetPlayer();
            // 服务端在别的地方注册
#else
            // 关闭登录界面
            GameModule.UI.CloseUIForm(GameModule.UI.GetUIForm("Assets/AssetRaw/UI/Client/LoginForm.prefab"));
            // 客户端在这里注册
            // TODO 临时，先手动赋值，后面需要从服务器获取。
            //m_CurSelSceneId = 10001;
            //var sceneConfig = ConfigSystem.Instance.Tables.TbScenes.Get(m_CurSelSceneId);
            //m_CurSelScene = sceneConfig.ScenesList[0];
            var objsList = GetSpawnObjsForScene(m_CurSelScene.ObjsSpawnListId);
            await ClientRegisterNetObjs(objsList);
#endif
            // 读取选择的场景，并加载场景
            await GameModule.Scene.LoadSceneAsync(m_CurSelScene.Path);
            // 设置试验开始标志
            m_IsTrialStarted = true;
#if !UNITY_EDITOR
            // 先暂停头显重置维持
            GameModule.Event.Fire(SetOVRTrackingRestoreEventArgs.EventId, SetOVRTrackingRestoreEventArgs.Create(false)); 
#endif
            // 传送角色至场景初始点
            var data = new Dictionary<string, object> { { "SceneName", m_CurSelScene.Path } };
            TeleportPlayerToStartPos(m_CurSelScene.Path, data);
        }

        /// <summary>
        /// 停止试验
        /// </summary>
        private void StopTrial()
        {
            if (!m_IsTrialStarted)
            {
                return;
            }
            // 输出日志
            Log.Info("Main Logic Stop");
#if !MANAGER_SERVER
            // 停止眼动模块
            StopGaze();
#endif
            // 注销网络物品
            ClientUnregisterNetObjs();
            // 关闭任务界面
            CloseTaskUI(m_CurSelScene.Path);
            // 卸载场景
            if (GameModule.Scene.SceneIsLoaded(m_CurSelScene.Path))
            {
                GameModule.Scene.UnloadScene(m_CurSelScene.Path);
                // 输出日志
                Log.Info($"Scene '{m_CurSelScene.Path}' unloaded");
            }
            // 销毁任务系统
            DestroyTaskSys();
            // 设置试验开始标志
            m_IsTrialStarted = false;
        }

        #endregion

        #region 生成物品

        /// <summary>
        /// 生成的网络对象列表
        /// </summary>
        private List<NetworkSpawnObj> m_SpawnedNetObjsList = new();

        /// <summary>
        /// 提取场景生成对象
        /// </summary>
        /// <param name="objsSpawnListId"></param>
        /// <returns></returns>
        private Dictionary<int, cfg.ObjBase> GetSpawnObjsForScene(int objsSpawnListId)
        {
            if (!ConfigSystem.Instance.Tables.TbObjSpawnList.DataMap
                .TryGetValue(objsSpawnListId, out var ObjsSpawnConfig))
            {
                Log.Warning($"无法找到网络物品生成列表ID: {objsSpawnListId}");

                return null;
            }
            var objsInGroup = new Dictionary<int, cfg.ObjBase>();
            switch (ObjsSpawnConfig.Type)
            {
                case EObjType.PRODUCT:
                    // 提取指定组的物品
                    foreach (var product in ConfigSystem.Instance.Tables.TbProductsList.DataList)
                    {
                        // 提取指定组的物品
                        if (product.GroupId == ObjsSpawnConfig.GroupId)
                        {
                            objsInGroup.Add(product.Id, product.Obj);
                        }
                    }
                    return objsInGroup;
                case EObjType.OBSTACLE:
                    // 提取指定组的物品
                    foreach (var obstacle in ConfigSystem.Instance.Tables.TbObstaclesList.DataList)
                    {
                        // 提取指定组的物品
                        if (obstacle.GroupId == ObjsSpawnConfig.GroupId)
                        {
                            objsInGroup.Add(obstacle.Id, obstacle.Obj);
                        }
                    }
                    return objsInGroup;
                default:
                    // 输出错误
                    Log.Error($"Invalid object type: {ObjsSpawnConfig.Type}");
                    return null;
            }
        }

#if MANAGER_SERVER

        /// <summary>
        /// 客户端注册网络对象
        /// </summary>
        /// <param name="objConfigsList">网络对象配置列表</param>
        /// <returns></returns>
        private async System.Threading.Tasks.Task HostRegisterNetObjs(Dictionary<int, cfg.ObjBase> objConfigsList)
        {
            if(objConfigsList == null)
            {
                // 输出日志
                Log.Info($"网络对象列表为空，不能生成");
                return;
            }
            // 遍历配置列表
            var count = objConfigsList.Count;
            for (int index = 0; index < count; index++)
            {
                var objConfig = objConfigsList.ElementAt(index);
                // 服务端的客户端可以直接生成
                var objPrefab = await GameModule.Resource.LoadAssetAsync<GameObject>(objConfig.Value.PrefabPath);
                // 生成网络对象
                var cloneObj = Instantiate(objConfig.Key, objPrefab, objConfig.Value);
                // 生成网络对象
                NetworkServer.Spawn(cloneObj);
                // 记录
                m_SpawnedNetObjsList.Add(new NetworkSpawnObj
                {
                    id = objConfig.Key,
                    m_Config = objConfig.Value,
                    m_IsLoadAsAssetOnly = true,
                    m_Prefab = objPrefab
                });
                // 更新进度
                GameEvent.Send(StringId.StringToHash("Progress"), index / (float)count);
            }
        }
#else
        /// <summary>
        /// 客户端注册网络对象
        /// </summary>
        /// <param name="objConfigsList">网络对象配置列表</param>
        /// <returns></returns>
        private async System.Threading.Tasks.Task ClientRegisterNetObjs(Dictionary<int, cfg.ObjBase> objConfigsList)
        {
            if (objConfigsList == null)
            {
                // 输出日志
                Log.Warning($"网络对象列表为空，不能生成");
                return;
            }
            // 遍历配置列表
            var count = objConfigsList.Count;
            for (int index = 0; index < count; index++)
            {
                var objConfig = objConfigsList.ElementAt(index);
                // 客户端先注册，等服务端统一生成。
                var objPrefab = await GameModule.Resource.LoadAssetAsync<GameObject>(objConfig.Value.PrefabPath);
                // 注册网络对象
                NetworkClient.RegisterPrefab(objPrefab, (_, _) =>
                {
                    return Instantiate(objConfig.Key, objPrefab, objConfig.Value);
                },
                spawndObj =>
                {
                    Object.Destroy(spawndObj);
                });
                // 记录
                m_SpawnedNetObjsList.Add(new NetworkSpawnObj
                {
                    id = objConfig.Key,
                    m_Config = objConfig.Value,
                    m_IsLoadAsAssetOnly = true,
                    m_Prefab = objPrefab
                });
                // 更新进度
                GameEvent.Send(StringId.StringToHash("Progress"), index / (float)count);
            }
        }

#endif

        /// <summary>
        /// 实例化对象
        /// </summary>
        /// <param name="id"></param>
        /// <param name="objPrefab"></param>
        /// <param name="objConfig"></param>
        /// <returns></returns>
        private GameObject Instantiate(int id, GameObject objPrefab, ObjBase objConfig)
        {
            // 生成对象
            var cloneObj = Object.Instantiate(objPrefab,
                        new Vector3(objConfig.Position.X, objConfig.Position.Y, objConfig.Position.Z),
                        Quaternion.Euler(objConfig.Rotation.X, objConfig.Rotation.Y, objConfig.Rotation.Z));
            // 根据配置文件重新命名
            objPrefab.name = objConfig.Name;
            // 根据对象类型处理
            var ObjsSpawnConfig = ConfigSystem.Instance.Tables.TbObjSpawnList.Get(m_CurSelScene.ObjsSpawnListId);
            switch (ObjsSpawnConfig.Type)
            {
                case EObjType.PRODUCT:
                    // 设置商品
                    var price = ConfigSystem.Instance.Tables.TbProductsList.Get(id).Price;
                    cloneObj.GetComponentInChildren<Product>().Setup(id, price);
#if MANAGER_SERVER
                    // 管理端需要取消刚体的重力，并设置刚体为运动学
                    var rig = cloneObj.GetComponentInChildren<Rigidbody>(); ;
                    rig.useGravity = false;
                    rig.isKinematic = true;
                    // 输出日志
                    Log.Info($"The spawned product is {cloneObj.name}");
#endif

                    break;
            }
            return cloneObj;

        }

        /// <summary>
        /// 客户端注销网络对象
        /// </summary>
        private void ClientUnregisterNetObjs()
        {
            foreach (var spawnedNetObj in m_SpawnedNetObjsList)
            {
                // 空对象跳过
                if (spawnedNetObj.m_Prefab == null)
                {
                    // 输出日志
                    Log.Error($"The spawned net object is null and skip to unregister. ({spawnedNetObj.m_Config.PrefabPath})");
                    continue;
                }
                // 注销网络对象
                NetworkClient.UnregisterPrefab(spawnedNetObj.m_Prefab);
                if (spawnedNetObj.m_IsLoadAsAssetOnly)
                {
                    try
                    {
                        GameModule.Resource.UnloadAsset(spawnedNetObj.m_Prefab);
                    }
                    catch (System.Exception ex)
                    {
                        // TODO 临时，有时间去排查为什么会失败。
                        Log.Warning($"Failed to unload asset: {spawnedNetObj.m_Prefab.name}, {ex.Message}");
                    }
                }
                else
                {
                    Object.Destroy(spawnedNetObj.m_Prefab);
                }
            }
        }

        /// <summary>
        /// 网络生成对象
        /// </summary>
        private class NetworkSpawnObj
        {
            /// <summary>
            /// ID
            /// </summary>
            public int id;

            /// <summary>
            /// 对象配置
            /// </summary>
            public cfg.ObjBase m_Config;

            /// <summary>
            /// 是否仅加载为资产
            /// </summary>
            public bool m_IsLoadAsAssetOnly = false;

            /// <summary>
            /// 对象预置体
            /// </summary>
            public GameObject m_Prefab;
        }

        #endregion

        #region 场景切换

#if MANAGER_SERVER

        private void OnServerSceneChange(object sender, GameEventArgs e)
        {
            ServerSceneChangeEventArgs ne = (ServerSceneChangeEventArgs)e;
            if (ne == null)
            {
                return;
            }
            // 输出场景名称
            Log.Info($"ServerSceneChange: {ne.NewSceneName}");
            // 如果不是主场景，就停止上一次试验（如果有）。
            if (ne.NewSceneName != ((ASUNetworkRoomManager)NetworkManager.singleton).GameplayScene)
            {
                StopTrial();
            }
        }

        private async void OnServerSceneChanged(object sender, GameEventArgs e)
        {
            ServerSceneChangedEventArgs ne = (ServerSceneChangedEventArgs)e;
            if (ne == null)
            {
                return;
            }
            // 输出场景名称
            Log.Info($"ServerSceneChanged: {ne.NewSceneName}");
            // 如果是主场景就开始游戏
            var networkManager = NetworkManager.singleton as ASUNetworkRoomManager;
            if (ne.NewSceneName == networkManager.GameplayScene)
            {
                StartTrial();
            }
            // 如果场景是房间场景，就显示初始界面
            else if (ne.NewSceneName == networkManager.RoomScene)
            {
                await OpenStartUI();
                // 发送事件
                GameEvent.EventMgr.GetInterface<IActorLogicEvent>().OnEnteredRoomScene();
            }
        }

#else
        private void OnClientSceneChange(object sender, GameEventArgs e)
        {
            ClientSceneChangeEventArgs ne = (ClientSceneChangeEventArgs)e;
            if (ne == null)
            {
                return;
            }
            // 输出场景名称
            Log.Info($"ClientSceneChange: {ne.NewSceneName}");
            // 如果不是主场景，就停止上一次试验（如果有）。
            if (ne.NewSceneName != ((ASUNetworkRoomManager)NetworkManager.singleton).GameplayScene)
            {
                StopTrial();
            }
        }

        private async void OnClientSceneChanged(object sender, GameEventArgs e)
        {
            ClientSceneChangedEventArgs ne = (ClientSceneChangedEventArgs)e;
            if (ne == null)
            {
                return;
            }
            // 输出场景名称
            Log.Info($"ClientSceneChanged: {ne.NewSceneName}");
            // 如果是主场景就开始游戏
            var networkManager = NetworkManager.singleton as ASUNetworkRoomManager;
            if (ne.NewSceneName == networkManager.GameplayScene)
            {
                StartTrial();
            }
            // 如果场景是房间场景，就显示初始界面
            else if (ne.NewSceneName == networkManager.RoomScene)
            {
                await OpenStartUI();
                // 发送事件
                GameEvent.EventMgr.GetInterface<IActorLogicEvent>().OnEnteredRoomScene();
            }
        }

        /// <summary>
        /// 进入大厅房间场景
        /// </summary>
        private void OnEnteredRoomScene()
        {
            var networkManager = NetworkManager.singleton as ASUNetworkRoomManager;
            // 输出日志
            Log.Info($"进入了大厅房间：{networkManager.RoomScene}");
            // 传送角色至初始点，以看到登录界面
            var data = new Dictionary<string, object> { { "SceneName", networkManager.RoomScene } };
            TeleportPlayerToStartPos(networkManager.RoomScene, data);
        }

#endif

        #endregion

        #region 角色传送

        /// <summary>
        /// 传送角色至初始点
        /// </summary>
        /// <param name="sceneName">场景名</param>
        /// <param name="data">传递的数据</param>
        private void TeleportPlayerToStartPos(string sceneName = null, object data = null)
        {
            // 试验内容以 Additive 方式加载，大厅和内容场景里都可能存在 StartPos。
            // 必须在指定场景内查找，避免 GameObject.Find 随机取得大厅的同名对象。
            var initPos = FindStartPosition(sceneName);
            // 如果没有找到，给出错误提示
            if (initPos == null)
            {
                // 输出错误，场景中没有找到初始点
                Log.Error($"Scene '{sceneName}' does not have a start position");
                return;
            }
            // 传送角色到初始点
            // 输出日志，包含初始点的位置和旋转
            Log.Info($"Teleporting player to start position: " +
                $"\nPos:{initPos.transform.position}" +
                $"\nRot:{initPos.transform.rotation}");
            // 发送传送事件
            GameModule.Event.Fire(TeleportPlayerEventArgs.EventId,
                TeleportPlayerEventArgs.Create(initPos.transform.position, initPos.transform.rotation, true, data));
        }

        private GameObject FindStartPosition(string sceneAssetName)
        {
            if (!string.IsNullOrEmpty(sceneAssetName))
            {
                string unitySceneName = SceneComponent.GetSceneName(sceneAssetName);
                Scene scene = SceneManager.GetSceneByName(unitySceneName);
                if (scene.IsValid() && scene.isLoaded)
                {
                    foreach (GameObject root in scene.GetRootGameObjects())
                    {
                        Transform startPosition = root.GetComponentsInChildren<Transform>(true)
                            .FirstOrDefault(item => item.name == "StartPos");
                        if (startPosition != null)
                        {
                            return startPosition.gameObject;
                        }
                    }

                    Log.Error($"Scene '{sceneAssetName}' is loaded but does not have a StartPos object");
                    return null;
                }

                Log.Warning($"Scene '{sceneAssetName}' is not loaded; falling back to global StartPos lookup");
            }

            return GameObject.Find("StartPos");
        }

        /// <summary>
        /// 传送角色完成
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void OnTeleportPlayer(object sender, GameEventArgs e)
        {
            TeleportPlayerEventArgs ne = (TeleportPlayerEventArgs)e;
            if (ne == null)
            {
                return;
            }
            // 检查附加数据
            if (ne.UserData == null)
            {
                // 输出日志
                Log.Warning("此次传送无附加数据，本模块忽略。");
                return;
            }
            // 检查附加数据是否为字典
            var userData = ne.UserData as Dictionary<string, object>;
            if (userData == null)
            {
                // 输出日志
                Log.Warning("此次传送附加数据非字典，本模块忽略。");
                return;
            }
            // 检查附加数据是否包含SceneName
            if (!userData.ContainsKey("SceneName"))
            {
                // 输出日志
                Log.Warning("此次传送附加数据不包含SceneName，本模块忽略。");
                return;
            }
            var sceneName = userData["SceneName"] as string;
#if MANAGER_SERVER
            // 相机跟随的是数字人的 Center，而不是下面被传送的网络 Player。
            // 将 Center 的 X/Z 对齐到本次传送点，Y 轴保留有效的 Hips 高度，
            // 避免相机落到 StartPos 的地面或被异常动捕坐标带离场景。
            var motionCaptureRoot = GameObject.Find("MotionCapAvatar");
            var cameraTarget = motionCaptureRoot != null
                ? motionCaptureRoot.transform.Find("avatar_001/Center")
                : null;
            if (cameraTarget == null)
            {
                Log.Error("Manager camera target 'MotionCapAvatar/avatar_001/Center' was not found");
            }
            else
            {
                var positionFollower = cameraTarget.GetComponent<PositionFollower>();
                if (positionFollower != null)
                {
                    positionFollower.SnapHorizontalTo(ne.Position);
                }
                else
                {
                    cameraTarget.position = ne.Position + Vector3.up;
                }

                var mainCamera = Camera.main;
                var cameraController = mainCamera != null
                    ? mainCamera.GetComponent<CameraController>()
                    : null;
                if (cameraController == null)
                {
                    Log.Error("Manager main camera does not have a CameraController");
                }
                else
                {
                    cameraController.InitFromTarget(cameraTarget);
                    cameraController.enabled = true;
                }
            }

            // 把虚拟玩家放到初始点
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                player.transform.position = ne.Position;
            }
            else
            {
                Log.Warning("Manager network Player was not found during teleport");
            }
            // 角色传送完成后，打开主界面
            // 如果已经打开了就不打开了
            var uiPrefab = "Assets/AssetRaw/UI/Server/ManagerMainForm.prefab";
            if (!GameModule.UI.GetUIForm(uiPrefab))
            {
                await GameModule.UI.OpenUIFormAsync(uiPrefab, "UI", 0, false, null);
            }
            // 生成带网络同步的物品
            var objsList = GetSpawnObjsForScene(m_CurSelScene.ObjsSpawnListId);
            await HostRegisterNetObjs(objsList);
            // 初始化任务
            InitTasks(ConfigSystem.Instance.Tables.TbScenes.Get(m_CurSelSceneId).TasksListId);
            // 等一会，等任务初始化完成
            await UniTask.Delay(1000);
#else
            // 如果场景非房间场景，就正常进入主场景
            var networkManager = NetworkManager.singleton as ASUNetworkRoomManager;
            if (sceneName == networkManager.RoomScene)
            {
                return;
            }

            // 场景加载完成，设置客户端就绪
            // 输出日志
            // 网络管理已经修改为游戏场景加载完不会自动就绪了
            Log.Info("Client scene is ready now, set the client to ready.");
            NetworkClient.Ready();
            InitNetPlayer();
            // 等一会，等传送到位
            await UniTask.Delay(1000);
            // 开启眼动模块
            StartGaze();
#if !UNITY_EDITOR
            // 启动头显重置维持
            //GameModule.Event.Fire(SetOVRTrackingRestoreEventArgs.EventId, SetOVRTrackingRestoreEventArgs.Create(true));
            GameModule.Event.Fire(SetOVRTrackingRestoreEventArgs.EventId,
                SetOVRTrackingRestoreEventArgs.Create(true, true, null, null, false, null)); 
#endif

#if DEBUG_MAIN_LOGIC_SYSTEM
            // TODO 临时 测试，跳过网络模块
            // 生成对象
            foreach(var obj in m_SpawnedNetObjsList)
            {
                // 实例化
                Instantiate(obj.id, obj.m_Prefab, EObjType.PRODUCT, obj.m_Config);
            }
#endif
#endif
            // 打开任务界面
            await OpenTaskUI(sceneName);
            // 关闭加载界面
            GameModule.UI.CloseUIForm(GameModule.UI.GetUIForm("Assets/AssetRaw/UI/Loading/LoadingForm.prefab"));
            // 发送事件告诉试验准备就绪
            GameEvent.EventMgr.GetInterface<IActorLogicEvent>().OnTrialReady();

        }

        #endregion

        #region 生命周期

        public override bool OnInit()
        {
            Log.Info("OnInit Main Logic System");

            base.OnInit();

            OnEnterDebug();

            // 初始化网络发现模块
            m_NetworkDiscovery = ASUNetworkRoomManager.singleton.GetComponent<NetworkDiscovery>();

            // 注册事件（公共）
            GameModule.Event.Subscribe(TeleportPlayerEventArgs.EventId, OnTeleportPlayer);
            GameModule.Event.Subscribe(RoomAllPlayerReadyChangedEventArgs.EventId, OnRoomAllPlayerChanged);
            GameModule.Event.Subscribe(RoomDisOrConnectedChangedEventArgs.EventId, OnRoomDisOrConnectedChanged);
#if MANAGER_SERVER
            // 服务端
            GameModule.Event.Subscribe(ServerSceneChangedEventArgs.EventId, OnServerSceneChanged);
            GameModule.Event.Subscribe(ServerSceneChangeEventArgs.EventId, OnServerSceneChange);
            GameEvent.AddEventListener<int, string>(IActorLogicEvent_Event.OnStartTrial, OnStartTrial);
            GameEvent.AddEventListener(IActorLogicEvent_Event.OnStopTrial, OnStopTrial);
#else
            // 客户端
            GameModule.Event.Subscribe(ClientSceneChangedEventArgs.EventId, OnClientSceneChanged);
            GameModule.Event.Subscribe(ClientSceneChangeEventArgs.EventId, OnClientSceneChange);
            GameModule.Event.Subscribe(
                RequestAvatarViewAlignmentEventArgs.EventId,
                OnSystemRecenterAvatarAlignmentRequested);
            // 注册准备就绪状态
            GameEvent.AddEventListener<bool>(ILoginUI_Event.OnUserReadyChange, OnUserReadyChange);
            // 注册发现服务端事件
            m_NetworkDiscovery.OnServerFound.AddListener(OnDiscoveredServer);
            // 注册根据动捕修正视角事件
            GameEvent.AddEventListener(IActorLogicEvent_Event.FixViewByMotionCapture, OnFixViewByMotionCapture);
            // 注册进入大厅房间事件
            GameEvent.AddEventListener(IActorLogicEvent_Event.OnEnteredRoomScene, OnEnteredRoomScene);
#endif
            // 注册网络同步状态改变后事件
            GameModule.Event.Subscribe(NetSyncStateChangedEventArgs.EventId, OnNetSyncStateChanged);

            // 初始化眼动指标数据模块
            // ETDataModuleManager.Instance.Initialize();
            // ETDataModuleManager.Instance.InitializeModule<NativeFixationDataModule>(40f, 100);// 初始化需要的模块（可以带参数）

            return true;
        }

        public override async void OnStart()
        {
            // 加载任务系统
            m_TasksListPrefab = await GameModule.Resource.LoadAssetAsync<GameObject>("Assets/AssetRaw/Actor/Sys/TaskList.prefab");
            // 初始化UI
            await OpenStartUI();
            // 等界面初始化好后，再启动网络。
#if MANAGER_SERVER
            // 管理端以主机模式启动
            ASUNetworkRoomManager.singleton.StartHost();
            m_NetworkDiscovery.AdvertiseServer();
#else
            // 设置服务端地址
            //ASUNetworkRoomManager.singleton.networkAddress = ConfigSystem.Instance.Tables.Tbconstant.GetOrDefault(0).Param2;
            //NetworkManager.singleton.StartClient();
            // 客户端开始寻找服务端
            StartClientDiscoveryWithFallback();
            // 
            await OpenDebugUI();
#endif

#if DEBUG_MAIN_LOGIC_SYSTEM
            // TODO 临时 测试，跳过网络模块
            StartTrial(); 
#endif
        }

        public override void OnUpdate()
        {
#if MANAGER_SERVER
            if (Input.GetKeyDown(KeyCode.F6))
            {
                RequestRemoteQuestRecenter();
            }
#endif

#if DEBUG_MAIN_LOGIC_SYSTEM
            if (Input.GetKeyUp(KeyCode.S))
            {
                // 生成任务列表
                m_TasksListObj = Object.Instantiate(m_TasksListPrefab);
                NetworkServer.Spawn(m_TasksListObj);
                // 输出日志
                Debug.Log("[测试]管理端生成任务对象");
            }
            if (Input.GetKeyUp(KeyCode.I))
            {
                InitTasks(ConfigSystem.Instance.Tables.TbScenes.Get(m_CurSelSceneId).TasksListId);
                // 输出日志
                Debug.Log("[测试]管理端初始化任务");
            }
#endif
        }

#if MANAGER_SERVER
        private void RequestRemoteQuestRecenter()
        {
            float now = Time.unscaledTime;
            if (now < m_NextRemoteQuestRecenterTime)
            {
                Log.Warning("Remote Quest recenter ignored: command is cooling down");
                return;
            }

            if (!m_IsTrialStarted)
            {
                Log.Warning("Remote Quest recenter ignored: no trial is currently running");
                return;
            }

            if (!NetworkServer.active)
            {
                Log.Warning("Remote Quest recenter ignored: Mirror server is not active");
                return;
            }

            m_NextRemoteQuestRecenterTime = now + RemoteQuestRecenterCooldown;
            GameModule.Event.Fire(
                NetSyncStateWillChangeEventArgs.EventId,
                NetSyncStateWillChangeEventArgs.Create(
                    RemoteQuestRecenterStateName,
                    "manager F6 key",
                    ECallState.CLIENT_RPC,
                    true));
            Log.Info("Remote Quest recenter sent from manager (F6)");
        }
#endif

        public override void OnDestroy()
        {
            Log.Info("OnDestroy Main Logic System");
#if MANAGER_SERVER
            // 管理端停止主机
            if(ASUNetworkRoomManager.singleton != null)
            {
                ASUNetworkRoomManager.singleton.StopHost();
            }
            if (m_NetworkDiscovery != null)
            {
                m_NetworkDiscovery.StopDiscovery();
            }
#else
            // Invalidate delayed fallback tasks before their continuation can touch
            // objects destroyed during shutdown or a scene/module reload.
            ++m_ClientDiscoveryGeneration;
            if (m_NetworkDiscovery != null)
            {
                m_NetworkDiscovery.StopDiscovery();
            }
#endif
            // 注册事件（公共）
            GameModule.Event.Unsubscribe(TeleportPlayerEventArgs.EventId, OnTeleportPlayer);
            GameModule.Event.Unsubscribe(RoomAllPlayerReadyChangedEventArgs.EventId, OnRoomAllPlayerChanged);
            GameModule.Event.Unsubscribe(RoomDisOrConnectedChangedEventArgs.EventId, OnRoomDisOrConnectedChanged);
#if MANAGER_SERVER
            // 服务端
            GameModule.Event.Unsubscribe(ServerSceneChangedEventArgs.EventId, OnServerSceneChanged);
            GameModule.Event.Unsubscribe(ServerSceneChangeEventArgs.EventId, OnServerSceneChange);
            GameEvent.RemoveEventListener<int, string>(IActorLogicEvent_Event.OnStartTrial, OnStartTrial);
            GameEvent.RemoveEventListener(IActorLogicEvent_Event.OnStopTrial, OnStopTrial);
#else
            // 客户端
            GameModule.Event.Unsubscribe(ClientSceneChangedEventArgs.EventId, OnClientSceneChanged);
            GameModule.Event.Unsubscribe(ClientSceneChangeEventArgs.EventId, OnClientSceneChange);
            GameModule.Event.Unsubscribe(
                RequestAvatarViewAlignmentEventArgs.EventId,
                OnSystemRecenterAvatarAlignmentRequested);
            // 注销准备就绪状态
            GameEvent.RemoveEventListener<bool>(ILoginUI_Event.OnUserReadyChange, OnUserReadyChange);
            // 注销发现服务端事件
            if (m_NetworkDiscovery != null)
            {
                m_NetworkDiscovery.OnServerFound.RemoveListener(OnDiscoveredServer);
            }
            // 注销根据动捕修正视角事件
            GameEvent.RemoveEventListener(IActorLogicEvent_Event.FixViewByMotionCapture, OnFixViewByMotionCapture);
            // 注销进入大厅房间事件
            GameEvent.RemoveEventListener(IActorLogicEvent_Event.OnEnteredRoomScene, OnEnteredRoomScene);
#endif
            // 注销网络同步状态改变后事件监听
            GameModule.Event.Unsubscribe(NetSyncStateChangedEventArgs.EventId, OnNetSyncStateChanged);
            // 卸载任务列表
            if (m_TasksListPrefab != null)
            {
                GameModule.Resource.UnloadAsset(m_TasksListPrefab);
            }

            base.OnDestroy();
        }

        #endregion

        #region 调试

        /// <summary>
        /// 是否显示FPS
        /// </summary>
        private bool m_IsShowFPS = false;

        void OnEnterDebug()
        {
#if DEBUG_SHOW_FPS
            m_IsShowFPS = true;
            // 取消FPS限制
            Application.targetFrameRate = -1;
            // 输出警告日志
            Debug.LogWarning("取消FPS限制");
#else
            m_IsShowFPS = false;
#endif
        }

        #endregion
    }
}
