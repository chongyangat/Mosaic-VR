using UnityEngine;
using UnityGameFramework.Runtime;

namespace GameLogic
{
    /// <summary>
    /// 逻辑事件
    /// </summary>
    [EventInterface(EEventGroup.GroupLogic)]
    interface IActorLogicEvent
    {

        #region 网络

        /// <summary>
        /// 客户端已连接
        /// </summary>
        /// <param name="isReady">是否就绪</param>
        void OnClientConnected(bool isReady);

        /// <summary>
        /// 客户端断开连接
        /// </summary>
        void OnClientDisconnected();

        /// <summary>
        /// 进入房间场景后
        /// </summary>
        void OnEnteredRoomScene();

        /// <summary>
        /// 所有用户就绪
        /// </summary>
        /// <param name="isReady">是否全部就绪</param>
        void OnAllUserReadyChanged(bool isReady); 

        #endregion

        #region 训练

        /// <summary>
        /// 开始试验
        /// </summary>
        /// <param name="sceneId"></param>
        /// <param name="scenePath"></param>
        void OnStartTrial(int sceneId, string scenePath);

        /// <summary>
        /// 结束试验
        /// </summary>
        void OnStopTrial(); 

        /// <summary>
        /// 试验就绪
        /// </summary>
        void OnTrialReady();

        #endregion

        #region AI

        /// <summary>
        /// 开始录制语音
        /// </summary>
        void OnStartRecordVoice();

        /// <summary>
        /// 停止录制语音
        /// </summary>
        void OnStopRecordVoice();

        /// <summary>
        /// 发送语音给语音识别
        /// </summary>
        /// <param name="audioClip"></param>
        void OnSendVoiceToVoiceRecg(AudioClip audioClip);

        /// <summary>
        /// 获得语音识别的结果
        /// </summary>
        /// <param name="result"></param>
        void OnGetVoiceRecgResult(string result);

        /// <summary>
        /// 发送文本给AI
        /// </summary>
        /// <param name="text"></param>
        void OnSendTextToAI(string text);

        /// <summary>
        /// 发送文本给语音合成
        /// </summary>
        /// <param name="text"></param>
        void OnSendTextToVoiceSynthesis(string text);

        /// <summary>
        /// 获得语音合成结果
        /// </summary>
        /// <param name="result"></param>
        void OnGetVoiceSynthesisResult(AudioClip result);

        /// <summary>
        /// 获得AI返回的文本
        /// </summary>
        /// <param name="reply"></param>
        void OnGetAIReply(string reply); 

        #endregion

        #region 其他

        /// <summary>
        /// 根据动捕修正视角
        /// </summary>
        void FixViewByMotionCapture();

        /// <summary>
        /// 清空购物篮
        /// </summary>
        void ClearShoppingBasket();

        #endregion

        #region 结算

        /// <summary>
        /// 点击结算按钮
        /// </summary>
        void OnSettlementClicked();

        /// <summary>
        /// 结算完成
        /// </summary>
        void OnSettlementCompleted();

        #endregion
    }
}