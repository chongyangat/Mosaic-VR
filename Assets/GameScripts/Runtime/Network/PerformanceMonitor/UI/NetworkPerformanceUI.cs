using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Mirror;

namespace GameMain
{
    public class NetworkPerformanceUI : MonoBehaviour
    {
        [Tooltip("网络性能监控器实例")]
        public NetworkPerformanceMonitor monitor;
        
        [Tooltip("是否启用UI显示")]
        public bool m_IsEnabled = true;
        
        [Tooltip("UI更新间隔（秒）")]
        public float updateInterval = 0.1f;
        
        [Tooltip("图表显示时间范围（秒）")]
        public float chartTimeRange = 60.0f;
        
        // UI组件
        public Text latencyText;
        public Text jitterText;
        public Text packetLossText;
        
        // 新增UI组件
        public Text latencyDistributionText;
        public Text frameArrivalText;
        
        // 链路选择UI
        public Text currentLinkText;
        public Dropdown linkDropdown;
        
        // 优先级设置UI
        public Text priorityText;
        public Dropdown priorityDropdown;
        
        public RectTransform chartContainer;
        public Image chartPrefab;
        
        // 当前选中的链路
        private string currentLink = "PC<->VR 状态同步";
        
        // 图表类型
        public enum ChartType
        {
            Latency,
            Jitter,
            PacketLoss,
            FrameDelay,
            FrameLoss,
        }
        
        public ChartType currentChartType = ChartType.Latency;
        
        // 视图类型
        public enum ViewType
        {
            SingleLink,
            Aggregated
        }
        
        public ViewType currentViewType = ViewType.SingleLink;
        
        // 图表相关
        private List<Image> chartImages = new List<Image>();
        private float lastUpdateTime = 0;
        private const int maxChartPoints = 100;
        
        // 颜色配置
        private Color latencyColor = Color.yellow;
        private Color jitterColor = Color.cyan;
        private Color packetLossColor = Color.red;
        private Color link1Color = Color.yellow;
        private Color link2Color = Color.cyan;
        private Color link3Color = Color.green;
        
        // 链路状态指示灯
        private Dictionary<string, Image> linkStatusIndicators = new Dictionary<string, Image>();
        
        // 视图切换UI
        public Button viewToggleButton;
        public Text viewToggleText;
        
        private void Start()
        {
            if (!m_IsEnabled) return;
            
            if (monitor == null)
            {
                monitor = NetworkPerformanceMonitor.Instance;
                if (monitor == null)
                {
                    Debug.LogError("NetworkPerformanceMonitor not found!");
                    m_IsEnabled = false;
                    return;
                }
            }
            
            // 初始化图表
            InitializeChart();
            
            // 初始化链路选择
            InitializeLinkSelection();
            
            // 初始化优先级设置
            InitializePrioritySelection();
            
            // 初始化视图切换按钮
            InitializeViewToggle();
            
            // 订阅性能更新事件
            monitor.OnPerformanceUpdated += OnPerformanceUpdated;
            
            lastUpdateTime = Time.time;
        }
        
        private void InitializeViewToggle()
        {
            if (viewToggleButton != null)
            {
                viewToggleButton.onClick.AddListener(ToggleView);
            }
            UpdateViewToggleText();
        }
        
        private void UpdateViewToggleText()
        {
            if (viewToggleText != null)
            {
                viewToggleText.text = currentViewType == ViewType.SingleLink ? "切换到聚合视图" : "切换到单链路视图";
            }
        }
        
        private void InitializeLinkSelection()
        {
            if (linkDropdown != null)
            {
                // 清空现有选项
                linkDropdown.ClearOptions();
                
                // 获取所有链路
                List<string> links = monitor.AllLinks;
                linkDropdown.AddOptions(links);
                
                // 设置默认选项
                int defaultIndex = links.IndexOf(currentLink);
                if (defaultIndex >= 0)
                {
                    linkDropdown.value = defaultIndex;
                }
                
                // 添加值变化事件
                linkDropdown.onValueChanged.AddListener(OnLinkChanged);
            }
            
            if (currentLinkText != null)
            {
                currentLinkText.text = "当前链路: " + currentLink;
            }
            
            // 创建链路状态指示灯
            CreateLinkStatusIndicators();
        }
        
        private void OnLinkChanged(int index)
        {
            if (linkDropdown != null && index >= 0 && index < linkDropdown.options.Count)
            {
                currentLink = linkDropdown.options[index].text;
                if (currentLinkText != null)
                {
                    currentLinkText.text = "当前链路: " + currentLink;
                }
                // 更新优先级下拉菜单
                UpdatePriorityDropdown();
                // 更新UI显示
                UpdateUI();
                UpdateChart();
            }
        }
        
        /// <summary>
        /// 初始化优先级设置下拉菜单
        /// </summary>
        private void InitializePrioritySelection()
        {
            if (priorityDropdown != null)
            {
                // 清空现有选项
                priorityDropdown.ClearOptions();
                
                // 添加优先级选项
                List<string> priorities = new List<string>
                {
                    "低 (Low)",
                    "中 (Medium)",
                    "高 (High)",
                    "关键 (Critical)"
                };
                priorityDropdown.AddOptions(priorities);
                
                // 添加值变化事件
                priorityDropdown.onValueChanged.AddListener(OnPriorityChanged);
            }
            
            // 更新优先级显示
            UpdatePriorityDropdown();
        }
        
        /// <summary>
        /// 更新优先级下拉菜单为当前链路的优先级
        /// </summary>
        private void UpdatePriorityDropdown()
        {
            if (priorityDropdown != null && monitor != null)
            {
                var priority = monitor.GetLinkPriority(currentLink);
                priorityDropdown.value = (int)priority;
                if (priorityText != null)
                {
                    priorityText.text = "优先级: " + priority.ToString();
                }
            }
        }
        
        /// <summary>
        /// 处理优先级变更事件
        /// </summary>
        /// <param name="index">选中的优先级索引</param>
        private void OnPriorityChanged(int index)
        {
            if (monitor != null)
            {
                var priority = (NetworkPerformanceMonitor.LinkPriority)index;
                monitor.SetLinkPriority(currentLink, priority);
                if (priorityText != null)
                {
                    priorityText.text = "优先级: " + priority.ToString();
                }
            }
        }
        
        private void OnDestroy()
        {
            if (monitor != null)
            {
                monitor.OnPerformanceUpdated -= OnPerformanceUpdated;
            }
        }
        
        private void Update()
        {
            if (!m_IsEnabled) return;
            
            if (Time.time - lastUpdateTime >= updateInterval)
            {
                UpdateUI();
                UpdateChart();
                UpdateLinkStatusIndicators();
                lastUpdateTime = Time.time;
            }
        }
        
        private void InitializeChart()
        {
            if (chartContainer == null || chartPrefab == null) return;
            
            // 清空现有图表
            foreach (var image in chartImages)
            {
                Destroy(image.gameObject);
            }
            chartImages.Clear();
            
            // 创建新的图表点
            for (int i = 0; i < maxChartPoints; i++)
            {
                Image point = Instantiate(chartPrefab, chartContainer);
                point.transform.localPosition = new Vector3(i * 5, 0, 0);
                point.gameObject.SetActive(false);
                chartImages.Add(point);
            }
        }
        
        private void OnPerformanceUpdated(NetworkPerformanceMonitor.PerformanceData data)
        {
            // 性能数据更新时的回调，根据当前视图类型更新UI
            if (currentViewType == ViewType.SingleLink)
            {
                if (data.link == currentLink)
                {
                    UpdateUI();
                    UpdateChart();
                }
            }
            else
            {
                UpdateUI();
                UpdateChart();
            }
        }
        
        public void ToggleView()
        {
            currentViewType = currentViewType == ViewType.SingleLink ? ViewType.Aggregated : ViewType.SingleLink;
            UpdateViewToggleText();
            UpdateUI();
            UpdateChart();
        }
        
        private void UpdateUI()
        {
            if (monitor == null) return;
            
            if (currentViewType == ViewType.SingleLink)
            {
                var data = monitor.GetLatestData(currentLink);
                bool isLinkActive = monitor.IsLinkActive(currentLink);
                
                // 检查网络连接状态
                bool networkActive = false;
                if (NetworkServer.active)
                {
                    // 在服务器上，检查是否有客户端连接
                    networkActive = NetworkServer.connections.Count > 0;
                }
                else
                {
                    // 在客户端上，检查是否连接到服务器
                    networkActive = NetworkClient.isConnected && NetworkClient.ready;
                }
                
                // 综合判断链路活跃状态
                if (networkActive)
                {
                    isLinkActive = true;
                }
                else
                {
                    isLinkActive = false;
                }
                
                // 更新文本显示
                if (latencyText != null)
                {
                    if (isLinkActive && data.latency >= 0)
                    {
                        latencyText.text = string.Format("延时: {0:F1}ms", data.latency);
                        latencyText.color = GetLatencyColor(data.latency);
                    }
                    else
                    {
                        latencyText.text = "延时: --ms";
                        latencyText.color = Color.gray;
                    }
                }
                
                if (jitterText != null)
                {
                    if (isLinkActive && data.jitter >= 0)
                    {
                        jitterText.text = string.Format("抖动: {0:F1}ms", data.jitter);
                        jitterText.color = GetJitterColor(data.jitter);
                    }
                    else
                    {
                        jitterText.text = "抖动: --ms";
                        jitterText.color = Color.gray;
                    }
                }
                
                if (packetLossText != null)
                {
                    if (isLinkActive && data.packetLoss >= 0)
                    {
                        packetLossText.text = string.Format("丢包: {0:F1}%", data.packetLoss);
                        packetLossText.color = GetPacketLossColor(data.packetLoss);
                    }
                    else
                    {
                        packetLossText.text = "丢包: --%";
                        packetLossText.color = Color.gray;
                    }
                }
                
                // 更新延迟分布显示
                if (latencyDistributionText != null)
                {
                    if (isLinkActive && data.latencyMin >= 0 && data.latencyMax >= 0 && data.latencyMedian >= 0 && data.latency95th >= 0)
                    {
                        latencyDistributionText.text = string.Format("延迟分布: Min {0:F1}ms, Max {1:F1}ms, P50 {2:F1}ms, P95 {3:F1}ms", 
                            data.latencyMin, data.latencyMax, data.latencyMedian, data.latency95th);
                    }
                    else
                    {
                        latencyDistributionText.text = "延迟分布: --";
                        latencyDistributionText.color = Color.gray;
                    }
                }
                
                // 更新MoCap帧数据显示
                if (frameArrivalText != null)
                {
                    if (isLinkActive && data.frameArrivalDelay >= 0 && data.frameInterval >= 0)
                    {
                        frameArrivalText.text = string.Format("帧到达: {0:F1}ms, 间隔: {1:F1}ms", 
                            data.frameArrivalDelay, data.frameInterval);
                    }
                    else
                    {
                        frameArrivalText.text = "帧到达: --ms, 间隔: --ms";
                        frameArrivalText.color = Color.gray;
                    }
                }

            }
            else
            {
                // 聚合视图，显示所有链路的性能数据对比
                var allLinksData = monitor.GetAllLinksLatestData();
                
                if (latencyText != null)
                {
                    StringBuilder latencyBuilder = new StringBuilder("延时对比:\n");
                    foreach (var kvp in allLinksData)
                    {
                        string link = kvp.Key;
                        float latency = kvp.Value.latency;
                        string latencyStr = latency >= 0 ? string.Format("{0:F1}ms", latency) : "--ms";
                        latencyBuilder.AppendFormat("{0}: {1}\n", link, latencyStr);
                    }
                    latencyText.text = latencyBuilder.ToString();
                    latencyText.color = Color.white;
                }
                
                if (jitterText != null)
                {
                    StringBuilder jitterBuilder = new StringBuilder("抖动对比:\n");
                    foreach (var kvp in allLinksData)
                    {
                        string link = kvp.Key;
                        float jitter = kvp.Value.jitter;
                        string jitterStr = jitter >= 0 ? string.Format("{0:F1}ms", jitter) : "--ms";
                        jitterBuilder.AppendFormat("{0}: {1}\n", link, jitterStr);
                    }
                    jitterText.text = jitterBuilder.ToString();
                    jitterText.color = Color.white;
                }
                
                if (packetLossText != null)
                {
                    StringBuilder packetLossBuilder = new StringBuilder("丢包对比:\n");
                    foreach (var kvp in allLinksData)
                    {
                        string link = kvp.Key;
                        float packetLoss = kvp.Value.packetLoss;
                        string packetLossStr = packetLoss >= 0 ? string.Format("{0:F1}%", packetLoss) : "--%";
                        packetLossBuilder.AppendFormat("{0}: {1}\n", link, packetLossStr);
                    }
                    packetLossText.text = packetLossBuilder.ToString();
                    packetLossText.color = Color.white;
                }
                
                if (latencyDistributionText != null)
                {
                    latencyDistributionText.text = "聚合视图: 显示所有链路性能对比";
                    latencyDistributionText.color = Color.white;
                }
                
                if (frameArrivalText != null)
                {
                    frameArrivalText.text = "";
                }
            }
        }
        
        private void UpdateChart()
        {
            if (chartContainer == null || chartImages.Count == 0) return;
            
            if (currentViewType == ViewType.SingleLink)
            {
                // 单链路视图，显示当前链路的性能数据
                var historyData = monitor.GetHistoryData(Time.time - chartTimeRange, Time.time, currentLink);
                
                // 计算图表数据
                float[] chartData = new float[maxChartPoints];
                Color chartColor = latencyColor;
                float maxValue = 100; // 最大显示值
                
                // 根据图表类型选择数据
                switch (currentChartType)
                {
                    case ChartType.Latency:
                        chartColor = latencyColor;
                        maxValue = 100;
                        break;
                    case ChartType.Jitter:
                        chartColor = jitterColor;
                        maxValue = 50;
                        break;
                    case ChartType.PacketLoss:
                        chartColor = packetLossColor;
                        maxValue = 20;
                        break;
                    case ChartType.FrameDelay:
                        chartColor = new Color(0.0f, 1.0f, 0.5f); // 青绿色
                        maxValue = 50;
                        break;
                    case ChartType.FrameLoss:
                        chartColor = new Color(1.0f, 0.5f, 0.0f); // 橙色
                        maxValue = 20;
                        break;
                }
                
                // 填充数据
                int dataIndex = 0;
                int step = Mathf.Max(1, historyData.Count / maxChartPoints);
                
                for (int i = 0; i < maxChartPoints; i++)
                {
                    if (dataIndex < historyData.Count)
                    {
                        switch (currentChartType)
                        {
                            case ChartType.Latency:
                                chartData[i] = historyData[dataIndex].latency;
                                break;
                            case ChartType.Jitter:
                                chartData[i] = historyData[dataIndex].jitter;
                                break;
                            case ChartType.PacketLoss:
                                chartData[i] = historyData[dataIndex].packetLoss;
                                break;
                            case ChartType.FrameDelay:
                                chartData[i] = historyData[dataIndex].frameArrivalDelay;
                                break;
                            case ChartType.FrameLoss:
                                chartData[i] = historyData[dataIndex].frameLossRate;
                                break;
                        }
                        dataIndex += step;
                    }
                }
                
                // 更新图表显示
                float chartHeight = chartContainer.rect.height;
                
                for (int i = 0; i < chartImages.Count; i++)
                {
                    Image point = chartImages[i];
                    if (i < historyData.Count)
                    {
                        float height = (chartData[i] / maxValue) * chartHeight;
                        point.rectTransform.sizeDelta = new Vector2(2, height);
                        point.color = chartColor;
                        point.gameObject.SetActive(true);
                    }
                    else
                    {
                        point.gameObject.SetActive(false);
                    }
                }
            }
            else
            {
                // 聚合视图，显示所有链路的性能数据对比
                float chartHeight = chartContainer.rect.height;
                float maxValue = 100; // 默认最大显示值
                
                // 根据图表类型设置最大显示值
                switch (currentChartType)
                {
                    case ChartType.Latency:
                        maxValue = 100;
                        break;
                    case ChartType.Jitter:
                        maxValue = 50;
                        break;
                    case ChartType.PacketLoss:
                        maxValue = 20;
                        break;
                    case ChartType.FrameDelay:
                        maxValue = 50;
                        break;
                    case ChartType.FrameLoss:
                        maxValue = 20;
                        break;
                }
                
                // 清空所有图表点
                foreach (var point in chartImages)
                {
                    point.gameObject.SetActive(false);
                }
                
                // 获取所有链路的历史数据
                var allLinks = monitor.AllLinks;
                int linkIndex = 0;
                Color[] linkColors = { link1Color, link2Color, link3Color };
                
                foreach (string link in allLinks)
                {
                    if (linkIndex >= linkColors.Length)
                        break;
                    
                    var historyData = monitor.GetHistoryData(Time.time - chartTimeRange, Time.time, link);
                    if (historyData.Count == 0)
                        continue;
                    
                    Color linkColor = linkColors[linkIndex];
                    int dataIndex = 0;
                    int step = Mathf.Max(1, historyData.Count / maxChartPoints);
                    
                    for (int i = 0; i < maxChartPoints; i++)
                    {
                        if (dataIndex < historyData.Count && i < chartImages.Count)
                        {
                            float value = 0;
                            switch (currentChartType)
                            {
                                case ChartType.Latency:
                                    value = historyData[dataIndex].latency;
                                    break;
                                case ChartType.Jitter:
                                    value = historyData[dataIndex].jitter;
                                    break;
                                case ChartType.PacketLoss:
                                    value = historyData[dataIndex].packetLoss;
                                    break;
                                case ChartType.FrameDelay:
                                    value = historyData[dataIndex].frameArrivalDelay;
                                    break;
                                case ChartType.FrameLoss:
                                    value = historyData[dataIndex].frameLossRate;
                                    break;
                            }
                            
                            // 为不同链路设置不同的X轴偏移，避免重叠
                            float xOffset = i * 5 + linkIndex * 2;
                            Image point = chartImages[i];
                            point.rectTransform.localPosition = new Vector3(xOffset, 0, 0);
                            float height = (value / maxValue) * chartHeight;
                            point.rectTransform.sizeDelta = new Vector2(2, height);
                            point.color = linkColor;
                            point.gameObject.SetActive(true);
                            
                            dataIndex += step;
                        }
                    }
                    
                    linkIndex++;
                }
            }
        }
        
        /// <summary>
        /// 创建链路状态指示灯
        /// </summary>
        private void CreateLinkStatusIndicators()
        {
            // 清空现有指示灯
            foreach (var indicator in linkStatusIndicators.Values)
            {
                if (indicator != null)
                {
                    Destroy(indicator.gameObject);
                }
            }
            linkStatusIndicators.Clear();
            
            // 获取所有链路
            List<string> links = monitor.AllLinks;
            
            // 创建状态指示灯容器
            GameObject statusContainer = new GameObject("LinkStatusContainer");
            statusContainer.transform.SetParent(transform);
            
            RectTransform containerRect = statusContainer.AddComponent<RectTransform>();
            containerRect.anchoredPosition = new Vector2(360, -20);
            containerRect.sizeDelta = new Vector2(120, 60);
            containerRect.pivot = new Vector2(0, 1);
            containerRect.anchorMin = new Vector2(0, 1);
            containerRect.anchorMax = new Vector2(0, 1);
            
            // 为每个链路创建状态指示灯
            for (int i = 0; i < links.Count; i++)
            {
                string link = links[i];
                
                // 创建指示灯对象
                GameObject indicatorObj = new GameObject($"{link}_StatusIndicator");
                indicatorObj.transform.SetParent(statusContainer.transform);
                
                RectTransform indicatorRect = indicatorObj.AddComponent<RectTransform>();
                indicatorRect.anchoredPosition = new Vector2(10, -20 - i * 20);
                indicatorRect.sizeDelta = new Vector2(10, 10);
                
                Image indicatorImage = indicatorObj.AddComponent<Image>();
                indicatorImage.color = Color.red; // 初始为红色（非活跃）
                
                // 创建链路名称文本
                GameObject textObj = new GameObject($"{link}_StatusText");
                textObj.transform.SetParent(statusContainer.transform);
                
                RectTransform textRect = textObj.AddComponent<RectTransform>();
                textRect.anchoredPosition = new Vector2(30, -20 - i * 20);
                textRect.sizeDelta = new Vector2(80, 20);
                
                Text textComponent = textObj.AddComponent<Text>();
                textComponent.text = link;
                textComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                textComponent.fontSize = 10;
                textComponent.color = Color.white;
                textComponent.alignment = TextAnchor.MiddleLeft;
                
                // 添加到字典中
                linkStatusIndicators[link] = indicatorImage;
            }
        }
        
        /// <summary>
        /// 更新链路状态指示灯
        /// </summary>
        private void UpdateLinkStatusIndicators()
        {
            foreach (var kvp in linkStatusIndicators)
            {
                string link = kvp.Key;
                Image indicator = kvp.Value;
                
                if (indicator != null)
                {
                    bool isActive = monitor.IsLinkActive(link);
                    indicator.color = isActive ? Color.green : Color.red;
                }
            }
        }
        
        private Color GetLatencyColor(float latency)
        {
            if (latency < 50) return Color.green;
            if (latency < 100) return Color.yellow;
            if (latency < 200) return new Color(1.0f, 0.647f, 0.0f); // 橙色
            return Color.red;
        }
        
        private Color GetJitterColor(float jitter)
        {
            if (jitter < 10) return Color.green;
            if (jitter < 20) return Color.yellow;
            if (jitter < 50) return new Color(1.0f, 0.647f, 0.0f); // 橙色
            return Color.red;
        }
        
        private Color GetPacketLossColor(float packetLoss)
        {
            if (packetLoss < 1) return Color.green;
            if (packetLoss < 5) return Color.yellow;
            if (packetLoss < 10) return new Color(1.0f, 0.647f, 0.0f); // 橙色
            return Color.red;
        }
        
        public void ToggleUI()
        {
            m_IsEnabled = !m_IsEnabled;
            gameObject.SetActive(m_IsEnabled);
        }
        
        public void ExportData()
        {
            if (monitor == null) return;
            
            string filePath = Application.persistentDataPath + "/NetworkPerformance_" + currentLink + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";
            monitor.ExportData(filePath, currentLink);
            Debug.Log("Network performance data exported to: " + filePath);
        }
        
        public void SetChartTimeRange(float seconds)
        {
            chartTimeRange = seconds;
        }
        
        public void SetChartType(ChartType type)
        {
            currentChartType = type;
            UpdateChart();
        }
        
        public void ToggleChartType()
        {
            int currentIndex = (int)currentChartType;
            int nextIndex = (currentIndex + 1) % Enum.GetValues(typeof(ChartType)).Length;
            currentChartType = (ChartType)nextIndex;
            UpdateChart();
        }
    }
}
