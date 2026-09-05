using UnityGameFramework.Runtime;
using UnityEngine.UI;
using TMPro;
using UnityEngine;
using System.Collections.Generic;
using cfg;

namespace GameLogic
{
    /// <summary>
    /// 管理配置界面
    /// TODO 临时，时间关系，界面临时处理，没有按UI框架来。
    /// </summary>
    public class ManagerSetupForm : UIFormLogic
    {
        #region 模式选择

        [Header("Mode Select")]

        [Tooltip("界面根对象")]
        [SerializeField]
        private GameObject m_ModeSelUIRoot;

        [Tooltip("数据采集按钮")]
        [SerializeField]
        private Button m_DataCollectButton;

        private void OnEnterWithDataCollect()
        {
            m_ModeSelUIRoot.SetActive(false);
            m_CreateUserUIRoot.SetActive(true);
        }

        #endregion

        #region 创建用户

        [Header("Create User")]

        [Tooltip("界面根对象")]
        [SerializeField]
        private GameObject m_CreateUserUIRoot;

        [Tooltip("确定按钮")]
        [SerializeField]
        private Button m_ConfirmButton;

        [Tooltip("返回按钮")]
        [SerializeField]
        private Button m_ReturnLastInCUButton;

        [Tooltip("用户名输入框")]
        [SerializeField]
        private TMP_InputField m_UserNameInput;

        private void OnCreateUser()
        {
#if MANAGER_SERVER
            // 获取并存储用户名
            var userName = m_UserNameInput.text;
            AppData.UserName = userName; 
#endif

            m_CreateUserUIRoot.SetActive(false);
            m_SelectSceneUIRoot.SetActive(true);
        }

        private void OnReturnLastInCU()
        {
            m_CreateUserUIRoot.SetActive(false);
            m_ModeSelUIRoot.SetActive(true);
        }

        #endregion

        #region 选择场景

        [Header("Select Scene")]
        [Tooltip("界面根对象")]
        [SerializeField]
        private GameObject m_SelectSceneUIRoot;

        [Tooltip("返回按钮")]
        [SerializeField]
        private Button m_ReturnLastInSSButton;

        /// <summary>
        /// 场景列表
        /// </summary>
        private List<Scenes> m_ScenesList;

        /// <summary>
        /// 设置场景选择可用
        /// </summary>
        /// <param name="isEnabled"></param>
        private void SetSceneSelectEnable(bool isEnabled)
        {
            SetSupermarketEnable(isEnabled);
            SetStreetEnable(isEnabled);
        }

        /// <summary>
        /// 进入场景进入场景
        /// </summary>
        /// <param name="scenePrefix"></param>
        private void OnEnterScene(string scenePrefix)
        {
            // 获取选择场景的内容
            int? sceneId = null;
            string scenePath = null;
            foreach (var sceneCfg in m_ScenesList)
            {
                foreach (var scene in sceneCfg.ScenesList)
                {
                    if (scene.Path.StartsWith(scenePrefix))
                    {
                        sceneId = sceneCfg.Id;
                        scenePath = scene.Path;
                        break;
                    }
                }
            }
            if (sceneId == null)
            {
                // 输出日志
                Log.Warning("Scene with the prefix '{0}' is not exist.", scenePrefix);
                return;
            }
            // 发送开始请求
            GameEvent.EventMgr.GetInterface<IActorLogicEvent>().OnStartTrial(sceneId.Value, scenePath);
            // 
            GameModule.UI.CloseUIForm(GetComponent<UIForm>());
        }

        private void OnReturnLastInSS()
        {
            m_SelectSceneUIRoot.SetActive(false);
            m_CreateUserUIRoot.SetActive(true);
        }

        /// <summary>
        /// 重置场景选择
        /// </summary>
        private void ResetSceneSelect()
        {
            m_SelectSceneUIRoot.SetActive(false);
            //
            ResetSupermarket();
            ResetStreet();
        }

        #region 超市

        [Header("Supermarket")]
        [Tooltip("超市按钮")]
        [SerializeField]
        private Button m_SupermarButton;

        [Tooltip("布局列表")]
        [SerializeField]
        private TMP_Dropdown m_LayoutList;

        /// <summary>
        /// 进入超市
        /// </summary>
        private void OnEnterSupermarket()
        {
            OnEnterScene("Assets/AssetRaw/Scenes/Supermarket/");
        }

        /// <summary>
        /// 重置超市
        /// </summary>
        private void ResetSupermarket()
        {
            m_SupermarButton.interactable = false;
        }

        /// <summary>
        /// 设置超市可用
        /// </summary>
        /// <param name="isEnabled"></param>
        private void SetSupermarketEnable(bool isEnabled)
        {
            m_SupermarButton.interactable = isEnabled;
        }

        #endregion

        #region 街道

        [Header("Street")]
        [Tooltip("街道按钮")]
        [SerializeField]
        private Button m_StreetButton;

        private const string ENGLISH_SCENE_LABEL_NAME = "EnglishSceneLabel";

        /// <summary>
        /// Covers the Chinese labels baked into the scene-card sprites with
        /// runtime English labels. Keeping this as UI preserves every sprite
        /// state (normal, highlighted, selected, and disabled).
        /// </summary>
        private void EnsureEnglishSceneLabels()
        {
            CreateEnglishSceneLabel(m_SupermarButton, "Supermarket");
            CreateEnglishSceneLabel(m_StreetButton, "Street");
        }

        private void CreateEnglishSceneLabel(Button button, string labelText)
        {
            if (button == null)
            {
                return;
            }

            Transform existingLabel = button.transform.Find(ENGLISH_SCENE_LABEL_NAME);
            if (existingLabel != null)
            {
                existingLabel.SetAsLastSibling();
                return;
            }

            var backgroundObject = new GameObject(
                ENGLISH_SCENE_LABEL_NAME,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            backgroundObject.transform.SetParent(button.transform, false);

            var backgroundRect = (RectTransform)backgroundObject.transform;
            backgroundRect.anchorMin = new Vector2(1f, 0f);
            backgroundRect.anchorMax = new Vector2(1f, 0f);
            backgroundRect.pivot = new Vector2(1f, 0f);
            backgroundRect.anchoredPosition = new Vector2(-35f, 62f);
            backgroundRect.sizeDelta = new Vector2(210f, 52f);

            var background = backgroundObject.GetComponent<Image>();
            background.color = new Color(0.055f, 0.065f, 0.08f, 1f);
            background.raycastTarget = false;

            var textObject = new GameObject(
                "Text",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(backgroundObject.transform, false);

            var textRect = (RectTransform)textObject.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var label = textObject.GetComponent<TextMeshProUGUI>();
            label.text = labelText;
            if (m_VersionShower != null && m_VersionShower.font != null)
            {
                label.font = m_VersionShower.font;
            }
            label.fontSize = 25f;
            label.fontStyle = FontStyles.Bold;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.raycastTarget = false;
        }

        /// <summary>
        /// 进入街道
        /// </summary>
        private void OnEnterStreet()
        {
            OnEnterScene("Assets/AssetRaw/Scenes/Street/");
        }

        /// <summary>
        /// 重置街道
        /// </summary>
        private void ResetStreet()
        {
            m_StreetButton.interactable = false;
        }

        /// <summary>
        /// 设置街道可用
        /// </summary>
        /// <param name="isEnabled"></param>
        private void SetStreetEnable(bool isEnabled) {
            m_StreetButton.interactable = isEnabled;
        }

        #endregion

        #endregion

        #region 界面通用

        /// <summary>
        /// 重置界面
        /// </summary>
        private void ResetUI()
        {
            ResetSceneSelect();
            m_CreateUserUIRoot.SetActive(false);
            m_ModeSelUIRoot.SetActive(true);
        }

        #endregion

        #region 版本

        [Header("Version")]
        [Tooltip("版本显示")]
        [SerializeField]
        private TextMeshProUGUI m_VersionShower;

        #endregion

        #region 网络

        /// <summary>
        /// 准备就绪状态改变
        /// </summary>
        /// <param name="isReady"></param>
        private void OnUserReadyChange(bool isReady)
        {
            SetSceneSelectEnable(isReady);
        }

        #endregion

        #region 生命周期

        protected override void OnInit(object userData)
        {
            base.OnInit(userData);
            // 初始化版本
            m_VersionShower.text = $"v{Application.version}";
        }

        protected override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            EnsureEnglishSceneLabels();
            // 先重置界面
            ResetUI();
            // 接收数据
            m_ScenesList = (List<Scenes>)userData;
            // 添加监听
            m_SupermarButton.onClick.AddListener(OnEnterSupermarket);
            m_StreetButton.onClick.AddListener(OnEnterStreet);
            m_DataCollectButton.onClick.AddListener(OnEnterWithDataCollect);
            m_ConfirmButton.onClick.AddListener(OnCreateUser);
            m_ReturnLastInCUButton.onClick.AddListener(OnReturnLastInCU);
            m_ReturnLastInSSButton.onClick.AddListener(OnReturnLastInSS);
            // 注册准备就绪状态
            GameEvent.AddEventListener<bool>(IActorLogicEvent_Event.OnAllUserReadyChanged, OnUserReadyChange);
        }

        //public override void OnUpdate(float elapseSeconds, float realElapseSeconds)
        //{
        //    base.OnUpdate(elapseSeconds, realElapseSeconds);
        //    // 
        //    //if (Input.GetKeyUp(KeyCode.Q))
        //    //{
        //    //    GameEvent.EventMgr.GetInterface<ILoginUI>().OnUserReady();
        //    //}
        //    // 
            
        //}

        public override void OnClose(bool isShutdown, object userData)
        {
            base.OnClose(isShutdown, userData);
            
            // 移除监听
            m_SupermarButton.onClick.RemoveListener(OnEnterSupermarket);
            m_StreetButton.onClick.RemoveListener(OnEnterStreet);
            m_DataCollectButton.onClick.RemoveListener(OnEnterWithDataCollect);
            m_ConfirmButton.onClick.RemoveListener(OnCreateUser);
            m_ReturnLastInCUButton.onClick.RemoveListener(OnReturnLastInCU);
            m_ReturnLastInSSButton.onClick.RemoveListener(OnReturnLastInSS);
            // 注销准备就绪状态
            GameEvent.RemoveEventListener<bool>(IActorLogicEvent_Event.OnAllUserReadyChanged, OnUserReadyChange);
        }

        #endregion
    } 
}
