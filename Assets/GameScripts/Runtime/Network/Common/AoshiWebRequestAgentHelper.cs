using GameFramework;
using GameFramework.WebRequest;
using System;
using System.Collections.Generic;
using UGFExtensions.Await;

#if UNITY_5_4_OR_NEWER
using UnityEngine.Networking;
using UnityGameFramework.Runtime;

#else
using UnityEngine.Experimental.Networking;
#endif
using Utility = GameFramework.Utility;

namespace GameMain
{
    /// <summary>
    /// 使用 UnityWebRequest 实现的 Web 请求代理辅助器。
    /// </summary>
    public class AoshiWebRequestAgentHelper : WebRequestAgentHelperBase, IDisposable
    {
        private UnityWebRequest m_UnityWebRequest = null;
        private bool m_Disposed = false;

        private EventHandler<WebRequestAgentHelperCompleteEventArgs> m_WebRequestAgentHelperCompleteEventHandler = null;
        private EventHandler<WebRequestAgentHelperErrorEventArgs> m_WebRequestAgentHelperErrorEventHandler = null;

        #region 奥视产品通讯特殊处理

        /// <summary>
        /// 奥视产品通讯特殊处理
        /// </summary>
        /// <param name="configData"></param>
        private void AoshiProductSpecialProcess(Dictionary<string, object> configData)
        {
            m_UnityWebRequest.SetRequestHeader("Content-Type", "application/json;charset=UTF-8");
            m_UnityWebRequest.SetRequestHeader("Authorization", (string)configData["Authorization"]);
            //m_UnityWebRequest.SetRequestHeader("aoshi-origin", "pc");
        }

        #endregion

        /// <summary>
        /// Web 请求代理辅助器完成事件。
        /// </summary>
        public override event EventHandler<WebRequestAgentHelperCompleteEventArgs> WebRequestAgentHelperComplete
        {
            add
            {
                m_WebRequestAgentHelperCompleteEventHandler += value;
            }
            remove
            {
                m_WebRequestAgentHelperCompleteEventHandler -= value;
            }
        }

        /// <summary>
        /// Web 请求代理辅助器错误事件。
        /// </summary>
        public override event EventHandler<WebRequestAgentHelperErrorEventArgs> WebRequestAgentHelperError
        {
            add
            {
                m_WebRequestAgentHelperErrorEventHandler += value;
            }
            remove
            {
                m_WebRequestAgentHelperErrorEventHandler -= value;
            }
        }

        /// <summary>
        /// 通过 Web 请求代理辅助器发送请求。
        /// </summary>
        /// <param name="webRequestUri">要发送的远程地址。</param>
        /// <param name="userData">用户自定义数据。</param>
        public override void Request(string webRequestUri, object userData)
        {
            if (m_WebRequestAgentHelperCompleteEventHandler == null || m_WebRequestAgentHelperErrorEventHandler == null)
            {
                Log.Fatal("Web request agent helper handler is invalid.");
                return;
            }

            WWWFormInfo wwwFormInfo = (WWWFormInfo)userData;
            if (wwwFormInfo.WWWForm == null)
            {
                m_UnityWebRequest = UnityWebRequest.Get(webRequestUri);
            }
            else
            {
                m_UnityWebRequest = UnityWebRequest.Post(webRequestUri, wwwFormInfo.WWWForm);
            }

            // 如果是Aoshi产品，需要特殊处理
            var configData = (Dictionary<string, object>)wwwFormInfo.UserData;
            if (configData != null && configData.ContainsKey("IsAoshiProduct") && (bool)configData["IsAoshiProduct"])
            {
                AoshiProductSpecialProcess(configData);
            }

#if UNITY_2017_2_OR_NEWER
            m_UnityWebRequest.SendWebRequest();
#else
            m_UnityWebRequest.Send();
#endif
        }

        /// <summary>
        /// 通过 Web 请求代理辅助器发送请求。
        /// </summary>
        /// <param name="webRequestUri">要发送的远程地址。</param>
        /// <param name="postData">要发送的数据流。</param>
        /// <param name="userData">用户自定义数据。</param>
        public override void Request(string webRequestUri, byte[] postData, object userData)
        {
            if (m_WebRequestAgentHelperCompleteEventHandler == null || m_WebRequestAgentHelperErrorEventHandler == null)
            {
                Log.Fatal("Web request agent helper handler is invalid.");
                return;
            }

            // 提取用户自定义数据
            object realUserData = null;
            if (userData is Dictionary<string, object>)
            {
                realUserData = userData;                
            }
            else if(userData is WWWFormInfo)
            {
                var formUserData = (userData as WWWFormInfo).UserData;
                if (formUserData is Dictionary<string, object>)
                {
                    realUserData = formUserData;
                }else if (formUserData is AwaitDataWrap<WebResult>)
                {
                    realUserData = (formUserData as AwaitDataWrap<WebResult>).UserData;
                }
                else
                {
                    // 输出错误日志
                    Log.Fatal("Web request agent helper user data is invalid.");
                }
            }

            // 如果是Aoshi产品，需要特殊处理
            if(realUserData != null)
            {
                var configData = (Dictionary<string, object>)realUserData;
                if (configData != null && configData.ContainsKey("IsAoshiProduct") && (bool)configData["IsAoshiProduct"])
                {
                    //m_UnityWebRequest = UnityWebRequest.Post(webRequestUri, "POST");
                    m_UnityWebRequest = UnityWebRequest.Post(webRequestUri, "POST", "application/json");
                    m_UnityWebRequest.uploadHandler.Dispose();
                    m_UnityWebRequest.uploadHandler = new UploadHandlerRaw(postData);
                    AoshiProductSpecialProcess(configData);
                }
                else
                {
                    m_UnityWebRequest = UnityWebRequest.PostWwwForm(webRequestUri, Utility.Converter.GetString(postData));
                }
            }

#if UNITY_2017_2_OR_NEWER
            m_UnityWebRequest.SendWebRequest();
#else
            m_UnityWebRequest.Send();
#endif
        }

        /// <summary>
        /// 重置 Web 请求代理辅助器。
        /// </summary>
        public override void Reset()
        {
            if (m_UnityWebRequest != null)
            {
                m_UnityWebRequest.Dispose();
                m_UnityWebRequest = null;
            }
        }

        /// <summary>
        /// 释放资源。
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源。
        /// </summary>
        /// <param name="disposing">释放资源标记。</param>
        protected virtual void Dispose(bool disposing)
        {
            if (m_Disposed)
            {
                return;
            }

            if (disposing)
            {
                if (m_UnityWebRequest != null)
                {
                    m_UnityWebRequest.Dispose();
                    m_UnityWebRequest = null;
                }
            }

            m_Disposed = true;
        }

        private void Update()
        {
            if (m_UnityWebRequest == null || !m_UnityWebRequest.isDone)
            {
                return;
            }

            bool isError = false;
#if UNITY_2020_2_OR_NEWER
            isError = m_UnityWebRequest.result != UnityWebRequest.Result.Success;
#elif UNITY_2017_1_OR_NEWER
            isError = m_UnityWebRequest.isNetworkError || m_UnityWebRequest.isHttpError;
#else
            isError = m_UnityWebRequest.isError;
#endif
            if (isError)
            {
                WebRequestAgentHelperErrorEventArgs webRequestAgentHelperErrorEventArgs = WebRequestAgentHelperErrorEventArgs.Create(m_UnityWebRequest.error);
                m_WebRequestAgentHelperErrorEventHandler(this, webRequestAgentHelperErrorEventArgs);
                ReferencePool.Release(webRequestAgentHelperErrorEventArgs);
            }
            else if (m_UnityWebRequest.downloadHandler.isDone)
            {
                WebRequestAgentHelperCompleteEventArgs webRequestAgentHelperCompleteEventArgs = WebRequestAgentHelperCompleteEventArgs.Create(m_UnityWebRequest.downloadHandler.data);
                m_WebRequestAgentHelperCompleteEventHandler(this, webRequestAgentHelperCompleteEventArgs);
                ReferencePool.Release(webRequestAgentHelperCompleteEventArgs);
            }
        }
    }
}
