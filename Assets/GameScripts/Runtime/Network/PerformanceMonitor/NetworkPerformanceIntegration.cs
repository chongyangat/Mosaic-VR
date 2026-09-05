using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Mirror;

namespace GameMain
{
    /// <summary>
    /// 网络性能监控集成助手
    /// </summary>
    public static class NetworkPerformanceIntegration
    {
        /// <summary>
        /// 自动设置网络性能监控
        /// </summary>
        /// <param name="networkManager">网络管理器实例</param>
        /// <param name="enableUI">是否启用UI显示</param>
        /// <returns>创建的网络性能监控器实例</returns>
        public static NetworkPerformanceMonitor SetupNetworkPerformanceMonitor(NetworkManager networkManager, bool enableUI = true)
        {
            if (networkManager == null)
            {
                Debug.LogError("NetworkManager is null!");
                return null;
            }

            // 检查是否已经存在NetworkPerformanceMonitor
            NetworkPerformanceMonitor existingMonitor = NetworkPerformanceMonitor.Instance;
            if (existingMonitor != null)
            {
                Debug.Log("NetworkPerformanceMonitor already exists.");
                return existingMonitor;
            }

            // 创建NetworkPerformanceMonitor
            GameObject monitorObj = new GameObject("NetworkPerformanceMonitor");
            NetworkPerformanceMonitor monitor = monitorObj.AddComponent<NetworkPerformanceMonitor>();

            // 如果启用UI，创建UI
            if (enableUI)
            {
                CreatePerformanceUI(monitor);
            }

            Debug.Log("NetworkPerformanceMonitor setup completed.");
            return monitor;
        }

        /// <summary>
        /// 设置MoCap监控
        /// </summary>
        /// <param name="monitor">网络性能监控器实例</param>
        /// <param name="mocapSystem">MoCap系统实例</param>
        public static void SetupMoCapMonitoring(NetworkPerformanceMonitor monitor, object mocapSystem)
        {
            if (monitor == null)
            {
                Debug.LogError("NetworkPerformanceMonitor is null!");
                return;
            }

            // 检查MoCap系统类型并进行相应的集成
            if (mocapSystem != null)
            {
                string systemType = mocapSystem.GetType().Name;
                Debug.Log($"Setting up MoCap monitoring for: {systemType}");

                // 这里可以根据不同的MoCap系统类型进行具体的集成
                // 例如：OptiTrack、Vicon、Xsens等

                // 示例：订阅MoCap系统的帧数据事件
                // 实际项目中需要根据具体MoCap系统的API进行集成
            }

            Debug.Log("MoCap monitoring setup completed.");
        }

        /// <summary>
        /// 处理MoCap帧数据
        /// </summary>
        /// <param name="monitor">网络性能监控器实例</param>
        /// <param name="sequence">帧序列号</param>
        /// <param name="timestamp">帧时间戳</param>
        /// <param name="dataSize">数据大小（字节）</param>
        public static void ProcessMoCapFrame(NetworkPerformanceMonitor monitor, uint sequence, float timestamp, int dataSize = 0)
        {
            if (monitor == null)
            {
                Debug.LogError("NetworkPerformanceMonitor is null!");
                return;
            }

            // 记录MoCap帧
            monitor.OnMoCapFrameReceived(sequence, timestamp);

            // 可以在这里添加额外的MoCap数据处理逻辑
            // 例如：数据大小分析、传输时间计算等
        }

        /// <summary>
        /// 模拟MoCap数据（用于测试）
        /// </summary>
        /// <param name="monitor">网络性能监控器实例</param>
        /// <param name="frameRate">帧率</param>
        /// <param name="duration">持续时间（秒）</param>
        public static void SimulateMoCapData(NetworkPerformanceMonitor monitor, int frameRate = 30, float duration = 60.0f)
        {
            if (monitor == null)
            {
                Debug.LogError("NetworkPerformanceMonitor is null!");
                return;
            }

            Debug.Log($"Simulating MoCap data at {frameRate} FPS for {duration} seconds");

            // 创建模拟协程
            MonoBehaviour mono = monitor.gameObject.GetComponent<MonoBehaviour>();
            if (mono != null)
            {
                mono.StartCoroutine(SimulateMoCapFramesCoroutine(monitor, frameRate, duration));
            }
        }

        /// <summary>
        /// 模拟MoCap帧数据的协程
        /// </summary>
        private static System.Collections.IEnumerator SimulateMoCapFramesCoroutine(NetworkPerformanceMonitor monitor, int frameRate, float duration)
        {
            uint sequence = 0;
            float startTime = Time.time;
            float interval = 1.0f / frameRate;

            while (Time.time - startTime < duration)
            {
                // 模拟MoCap帧到达
                float timestamp = Time.time;
                monitor.OnMoCapFrameReceived(sequence, timestamp);
                sequence++;

                // 模拟帧间隔
                yield return new WaitForSeconds(interval);
            }

            Debug.Log($"MoCap simulation completed. Total frames: {sequence}");
        }

        /// <summary>
        /// 标记事件开始
        /// </summary>
        /// <param name="eventId">事件ID</param>
        public static void MarkEventStart(string eventId)
        {
            NetworkPerformanceMonitor monitor = NetworkPerformanceMonitor.Instance;
            if (monitor != null)
            {
                monitor.MarkEventStart(eventId);
            }
        }

        /// <summary>
        /// 标记事件结束
        /// </summary>
        /// <param name="eventId">事件ID</param>
        /// <returns>事件同步延迟</returns>
        public static float MarkEventEnd(string eventId)
        {
            NetworkPerformanceMonitor monitor = NetworkPerformanceMonitor.Instance;
            if (monitor != null)
            {
                return monitor.MarkEventEnd(eventId);
            }
            return -1;
        }

        /// <summary>
        /// 为自定义INetwork实现添加性能监控
        /// </summary>
        /// <param name="networkImplementation">自定义网络实现</param>
        /// <returns>包装后的网络实现</returns>
        public static INetwork WrapNetworkWithPerformanceMonitor(INetwork networkImplementation)
        {
            if (networkImplementation == null)
            {
                Debug.LogError("Network implementation is null!");
                return null;
            }

            return new NetworkPerformanceWrapper(networkImplementation);
        }

        /// <summary>
        /// 创建性能监控UI
        /// </summary>
        /// <param name="monitor">网络性能监控器实例</param>
        /// <returns>创建的UI实例</returns>
        private static NetworkPerformanceUI CreatePerformanceUI(NetworkPerformanceMonitor monitor)
        {
            // 创建UI画布
            GameObject canvasObj = new GameObject("NetworkPerformanceUI");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // 确保画布在场景加载时不被销毁
            UnityEngine.Object.DontDestroyOnLoad(canvasObj);
            canvas.sortingOrder = 11002;

            // 添加CanvasScaler
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // 添加GraphicRaycaster
            canvasObj.AddComponent<GraphicRaycaster>();

            // 创建主面板
            GameObject panelObj = new GameObject("Panel");
            panelObj.transform.SetParent(canvasObj.transform);

            RectTransform panelRect = panelObj.AddComponent<RectTransform>();
            panelRect.anchoredPosition = new Vector2(20, -20);
            panelRect.sizeDelta = new Vector2(500, 350); // 增加高度以容纳链路选择组件
            panelRect.pivot = new Vector2(0, 1);
            panelRect.anchorMin = new Vector2(0, 1);
            panelRect.anchorMax = new Vector2(0, 1);

            // 添加背景
            Image background = panelObj.AddComponent<Image>();
            background.color = new Color(0, 0, 0, 0.7f);

            // 链路选择组件
            CreateText(panelObj, "CurrentLinkText", new Vector2(10, -20), "当前链路: PC<->VR 状态同步");

            // 创建链路选择下拉菜单
            Dropdown dropdown = CreateDropdown(panelObj, "LinkDropdown", new Vector2(150, -20), new Vector2(200, 20));

            // 为Dropdown添加默认选项
            List<string> defaultOptions = new List<string> { "PC<->VR 状态同步", "MoCap→PC", "MoCap→VR" };
            dropdown.AddOptions(defaultOptions);
            dropdown.RefreshShownValue();

            // 创建文本对象
            CreateText(panelObj, "LatencyText", new Vector2(10, -40), "延时: 0.0ms");
            CreateText(panelObj, "JitterText", new Vector2(10, -60), "抖动: 0.0ms");
            CreateText(panelObj, "PacketLossText", new Vector2(10, -80), "丢包: 0.0%");
            CreateText(panelObj, "LatencyDistributionText", new Vector2(10, -120), "延迟分布: Min 0.0ms, Max 0.0ms, P50 0.0ms, P95 0.0ms");
            CreateText(panelObj, "FrameArrivalText", new Vector2(10, -140), "帧到达: 0.0ms, 间隔: 0.0ms");

            // 创建图表容器
            GameObject chartObj = new GameObject("ChartContainer");
            chartObj.transform.SetParent(panelObj.transform);
            RectTransform chartRect = chartObj.AddComponent<RectTransform>();
            chartRect.anchoredPosition = new Vector2(150, -160);
            chartRect.sizeDelta = new Vector2(280, 80);

            // 创建图表预制体
            GameObject chartPrefabObj = new GameObject("ChartPoint");
            Image chartPrefab = chartPrefabObj.AddComponent<Image>();
            chartPrefab.color = Color.yellow;

            // 添加NetworkPerformanceUI组件
            NetworkPerformanceUI ui = panelObj.AddComponent<NetworkPerformanceUI>();
            ui.monitor = monitor;
            ui.currentLinkText = panelObj.transform.Find("CurrentLinkText").GetComponent<Text>();
            ui.linkDropdown = panelObj.transform.Find("LinkDropdown").GetComponent<Dropdown>();
            ui.latencyText = panelObj.transform.Find("LatencyText").GetComponent<Text>();
            ui.jitterText = panelObj.transform.Find("JitterText").GetComponent<Text>();
            ui.packetLossText = panelObj.transform.Find("PacketLossText").GetComponent<Text>();
            ui.latencyDistributionText = panelObj.transform.Find("LatencyDistributionText").GetComponent<Text>();
            ui.frameArrivalText = panelObj.transform.Find("FrameArrivalText").GetComponent<Text>();
            ui.chartContainer = chartRect;
            ui.chartPrefab = chartPrefab;

            // 增加面板高度以容纳链路状态指示器
            panelRect.sizeDelta = new Vector2(500, 450);

            return ui;
        }

        #region 创建下拉列表

        /// <summary>
        /// 创建完整的Dropdown对象
        /// </summary>
        private static Dropdown CreateDropdown(GameObject parent, string name, Vector2 position, Vector2 size)
        {
            GameObject dropdownObj = new GameObject(name);
            dropdownObj.transform.SetParent(parent.transform);

            RectTransform dropdownRect = dropdownObj.AddComponent<RectTransform>();
            dropdownRect.anchoredPosition = position;
            dropdownRect.sizeDelta = size;

            Image backgroundImage = dropdownObj.AddComponent<Image>();
            backgroundImage.color = new Color(0.3f, 0.3f, 0.3f, 1f);

            Dropdown dropdown = dropdownObj.AddComponent<Dropdown>();

            // 创建Label
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(dropdownObj.transform);
            RectTransform labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0, 0);
            labelRect.anchorMax = new Vector2(1, 1);
            labelRect.offsetMin = new Vector2(10, 2);
            labelRect.offsetMax = new Vector2(-30, -2);

            Text labelText = labelObj.AddComponent<Text>();
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.fontSize = 12;
            labelText.color = Color.white;
            labelText.alignment = TextAnchor.MiddleLeft;

            dropdown.captionText = labelText;

            // 创建箭头
            GameObject arrowObj = new GameObject("Arrow");
            arrowObj.transform.SetParent(dropdownObj.transform);
            RectTransform arrowRect = arrowObj.AddComponent<RectTransform>();
            arrowRect.anchorMin = new Vector2(1, 0.5f);
            arrowRect.anchorMax = new Vector2(1, 0.5f);
            arrowRect.anchoredPosition = new Vector2(-15, 0);
            arrowRect.sizeDelta = new Vector2(20, 20);

            Text arrowText = arrowObj.AddComponent<Text>();
            arrowText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            arrowText.fontSize = 14;
            arrowText.text = "▼";
            arrowText.color = Color.white;
            arrowText.alignment = TextAnchor.MiddleCenter;

            // 创建Template
            GameObject templateObj = new GameObject("Template");
            templateObj.SetActive(false);
            templateObj.transform.SetParent(dropdownObj.transform);
            RectTransform templateRect = templateObj.AddComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0, 0);
            templateRect.anchorMax = new Vector2(1, 0);
            templateRect.pivot = new Vector2(0.5f, 1);
            templateRect.anchoredPosition = new Vector2(0, 0);
            templateRect.sizeDelta = new Vector2(0, 100);

            Image templateImage = templateObj.AddComponent<Image>();
            templateImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            ScrollRect scrollRect = templateObj.AddComponent<ScrollRect>();

            // 创建Viewport
            GameObject viewportObj = new GameObject("Viewport");
            viewportObj.transform.SetParent(templateObj.transform);
            RectTransform viewportRect = viewportObj.AddComponent<RectTransform>();
            viewportRect.anchorMin = new Vector2(0, 0);
            viewportRect.anchorMax = new Vector2(1, 1);
            viewportRect.offsetMin = new Vector2(0, 0);
            viewportRect.offsetMax = new Vector2(0, 0);

            Image viewportImage = viewportObj.AddComponent<Image>();
            viewportImage.color = new Color(0, 0, 0, 0.5f);

            Mask mask = viewportObj.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            scrollRect.viewport = viewportRect;

            // 创建Content
            GameObject contentObj = new GameObject("Content");
            contentObj.transform.SetParent(viewportObj.transform);
            RectTransform contentRect = contentObj.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = new Vector2(0, 0);
            contentRect.sizeDelta = new Vector2(0, 0);

            VerticalLayoutGroup layoutGroup = contentObj.AddComponent<VerticalLayoutGroup>();
            layoutGroup.padding = new RectOffset(0, 0, 0, 0);
            layoutGroup.spacing = 0;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = false;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;

            ContentSizeFitter sizeFitter = contentObj.AddComponent<ContentSizeFitter>();
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            // 创建Item
            GameObject itemObj = new GameObject("Item");
            itemObj.transform.SetParent(contentObj.transform);
            RectTransform itemRect = itemObj.AddComponent<RectTransform>();
            itemRect.sizeDelta = new Vector2(0, 24);

            Toggle toggle = itemObj.AddComponent<Toggle>();
            toggle.isOn = false;

            // Item Background
            GameObject itemBgObj = new GameObject("Item Background");
            itemBgObj.transform.SetParent(itemObj.transform);
            RectTransform itemBgRect = itemBgObj.AddComponent<RectTransform>();
            itemBgRect.anchorMin = new Vector2(0, 0);
            itemBgRect.anchorMax = new Vector2(1, 1);
            itemBgRect.offsetMin = new Vector2(0, 0);
            itemBgRect.offsetMax = new Vector2(0, 0);

            Image itemBgImage = itemBgObj.AddComponent<Image>();
            itemBgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            toggle.targetGraphic = itemBgImage;

            // Item Checkmark
            GameObject itemCheckObj = new GameObject("Item Checkmark");
            itemCheckObj.transform.SetParent(itemObj.transform);
            RectTransform itemCheckRect = itemCheckObj.AddComponent<RectTransform>();
            itemCheckRect.anchorMin = new Vector2(0, 0.5f);
            itemCheckRect.anchorMax = new Vector2(0, 0.5f);
            itemCheckRect.anchoredPosition = new Vector2(10, 0);
            itemCheckRect.sizeDelta = new Vector2(12, 12);

            Text itemCheckText = itemCheckObj.AddComponent<Text>();
            itemCheckText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            itemCheckText.fontSize = 12;
            itemCheckText.text = "✓";
            itemCheckText.color = Color.white;
            itemCheckText.alignment = TextAnchor.MiddleCenter;

            toggle.graphic = itemCheckText;

            // Item Label
            GameObject itemLabelObj = new GameObject("Item Label");
            itemLabelObj.transform.SetParent(itemObj.transform);
            RectTransform itemLabelRect = itemLabelObj.AddComponent<RectTransform>();
            itemLabelRect.anchorMin = new Vector2(0, 0);
            itemLabelRect.anchorMax = new Vector2(1, 1);
            itemLabelRect.offsetMin = new Vector2(25, 2);
            itemLabelRect.offsetMax = new Vector2(-10, -2);

            Text itemLabelText = itemLabelObj.AddComponent<Text>();
            itemLabelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            itemLabelText.fontSize = 12;
            itemLabelText.color = Color.white;
            itemLabelText.alignment = TextAnchor.MiddleLeft;

            dropdown.itemText = itemLabelText;
            dropdown.template = templateRect;
            
            return dropdown;
        }

        #endregion
        /// <summary>
        /// 创建文本对象
        /// </summary>
        private static Text CreateText(GameObject parent, string name, Vector2 position, string text)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent.transform);

            RectTransform rect = textObj.AddComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(280, 20);

            Text textComponent = textObj.AddComponent<Text>();
            textComponent.text = text;
            textComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textComponent.fontSize = 12;
            textComponent.color = Color.white;
            textComponent.alignment = TextAnchor.MiddleLeft;

            return textComponent;
        }
    }

    /// <summary>
    /// 网络性能监控包装器
    /// </summary>
    public class NetworkPerformanceWrapper : INetwork
    {
        private INetwork wrappedNetwork;
        private NetworkPerformanceMonitor monitor;

        public event System.Action<string> OnReceive
        {
            add { wrappedNetwork.OnReceive += value; }
            remove { wrappedNetwork.OnReceive -= value; }
        }

        public NetworkPerformanceWrapper(INetwork network)
        {
            wrappedNetwork = network;
            monitor = NetworkPerformanceMonitor.Instance ?? new GameObject("NetworkPerformanceMonitor").AddComponent<NetworkPerformanceMonitor>();
        }

        public void Send(string msg)
        {
            // 记录发送前的时间
            float sendTime = Time.time;

            // 发送消息
            wrappedNetwork.Send(msg);

            // 模拟发送事件（实际项目中应该在真正发送时触发）
            // 这里只是一个示例
        }
    }
}
