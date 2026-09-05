using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameMain
{
    /// <summary>
    /// 网络性能监控核心类，负责监控延迟、抖动、丢包率等网络性能指标
    /// 支持多链路监控，每个链路有独立的性能数据
    /// </summary>
    public partial class NetworkPerformanceMonitor : MonoBehaviour
    {
        #region 链路管理

        /// <summary>
        /// 链路数据访问锁
        /// </summary>
        private readonly object _linkLock = new object();

        /// <summary>
        /// 初始化指定链路
        /// </summary>
        /// <param name="link">链路名称</param>
        public void InitializeLink(string link)
        {
            if (string.IsNullOrEmpty(link))
                return;

            lock (_linkLock)
            {
                if (!_linkDataDict.ContainsKey(link))
                {
                    _linkDataDict[link] = new LinkData(_bufferSize, _jitterWindowSize);
                    Debug.Log($"[NetworkPerformanceMonitor] 初始化链路: {link}");
                }
            }
        }

        /// <summary>
        /// 设置当前链路
        /// </summary>
        /// <param name="link">链路名称</param>
        public void SetCurrentLink(string link)
        {
            if (string.IsNullOrEmpty(link))
            {
                Debug.LogError("[NetworkPerformanceMonitor] 设置当前链路为空");
                return;
            }

            lock (_linkLock)
            {
                // 如果当前链路与指定链路相同，直接返回
                if (_currentLink == link)
                {
                    return;
                }
                // 初始化链路数据
                InitializeLink(link);
                _currentLink = link;
                if (IsShowLog)
                {
                    Debug.Log($"[NetworkPerformanceMonitor] 切换到链路: {link}");
                }
            }
        }

        /// <summary>
        /// 重置指定链路的统计数据
        /// </summary>
        /// <param name="link">链路名称（null 表示所有链路）</param>
        public void ResetStatistics(string link = null)
        {
            lock (_linkLock)
            {
                if (string.IsNullOrEmpty(link))
                {
                    // 重置所有链路
                    foreach (var kvp in _linkDataDict)
                    {
                        kvp.Value.Reset();
                    }
                    Debug.Log("[NetworkPerformanceMonitor] 重置所有链路统计数据");
                }
                else
                {
                    // 重置指定链路
                    if (_linkDataDict.TryGetValue(link, out var linkData))
                    {
                        linkData.Reset();
                        Debug.Log($"[NetworkPerformanceMonitor] 重置链路统计数据: {link}");
                    }
                }
            }
        }

        /// <summary>
        /// 获取链路数据
        /// </summary>
        /// <param name="link">链路名称</param>
        /// <returns>链路数据</returns>
        private LinkData GetLinkData(string link)
        {
            lock (_linkLock)
            {
                InitializeLink(link);
                return _linkDataDict[link];
            }
        }

        /// <summary>
        /// 设置链路活跃状态
        /// </summary>
        /// <param name="link">链路名称</param>
        /// <param name="active">是否活跃</param>
        public void SetLinkActive(string link, bool active)
        {
            var linkData = GetLinkData(link);
            linkData.isActive = active;
            linkData.lastActiveDateTime = DateTime.Now;
        }

        /// <summary>
        /// 获取链路活跃状态
        /// </summary>
        /// <param name="link">链路名称</param>
        /// <returns>是否活跃</returns>
        public bool IsLinkActive(string link)
        {
            lock (_linkLock)
            {
                if (_linkDataDict.TryGetValue(link, out var linkData))
                {
                    // 检查链路是否在最近5秒内有活动
                    // 使用 DateTime.Now 计算时间差，避免在非主线程中调用 Time.time
                    double secondsSinceLastActive = (DateTime.Now - linkData.lastActiveDateTime).TotalSeconds;
                    if (linkData.isActive || secondsSinceLastActive < 5.0f)
                    {
                        return true;
                    }
                    return linkData.isActive;
                }
                return false;
            }
        }

        /// <summary>
        /// 更新链路活动状态
        /// </summary>
        /// <param name="link">链路名称</param>
        public void UpdateLinkActivity(string link)
        {
            var linkData = GetLinkData(link);
            linkData.isActive = true;
            linkData.lastActiveDateTime = DateTime.Now;
        }

        /// <summary>
        /// 设置链路优先级
        /// </summary>
        /// <param name="link">链路名称</param>
        /// <param name="priority">优先级</param>
        public void SetLinkPriority(string link, LinkPriority priority)
        {
            var linkData = GetLinkData(link);
            linkData.priority = priority;
            Debug.Log($"[NetworkPerformanceMonitor] 设置链路 {link} 优先级为: {priority}");
        }

        /// <summary>
        /// 获取链路优先级
        /// </summary>
        /// <param name="link">链路名称</param>
        /// <returns>优先级</returns>
        public LinkPriority GetLinkPriority(string link)
        {
            lock (_linkLock)
            {
                if (_linkDataDict.TryGetValue(link, out var linkData))
                {
                    return linkData.priority;
                }
                return LinkPriority.Medium; // 默认中等优先级
            }
        }

        /// <summary>
        /// 获取按优先级排序的链路列表
        /// </summary>
        /// <returns>按优先级从高到低排序的链路列表</returns>
        public List<string> GetLinksByPriority()
        {
            lock (_linkLock)
            {
                var sortedLinks = _linkDataDict.OrderByDescending(kvp => kvp.Value.priority).Select(kvp => kvp.Key).ToList();
                return sortedLinks;
            }
        }

        /// <summary>
        /// 基于优先级采样所有链路的性能数据
        /// 高优先级链路优先采样
        /// </summary>
        public void SampleAllLinksByPriority()
        {
            var linksByPriority = GetLinksByPriority();
            foreach (string link in linksByPriority)
            {
                SamplePerformanceData(link);
            }
        }

        #endregion
    }
}
