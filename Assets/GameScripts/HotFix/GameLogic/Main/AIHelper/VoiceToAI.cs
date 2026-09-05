using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

namespace GameLogic
{
    public class VoiceToAI : UIFormLogic
    {
        [SerializeField]
        private TextMeshProUGUI m_Head;

        #region 内容

        [SerializeField]
        private TextMeshProUGUI m_ChatContent;

        /// <summary>
        /// 语音播放
        /// </summary>
        private AudioSource m_AudioPlayer;

        /// <summary>
        /// AI回复
        /// </summary>
        private string m_AIReply;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="result"></param>
        private void OnGetVoiceRecgResult(string result)
        {
            // 设置头部
            m_Head.text = "你说";
            // 
            m_ChatContent.text = result;
            // 发送给AI
            GameEvent.EventMgr.GetInterface<IActorLogicEvent>().OnSendTextToAI(result);
        }

        private void OnGetAIReply(string reply)
        {
             // 设置头部
            m_Head.text = "指引助手说";
            // 清空文本，准备打字机效果
            // m_ChatContent.text = reply;
            m_AIReply = reply;
            m_ChatContent.text = string.Empty;
            // 发送文本给语音合成
            GameEvent.EventMgr.GetInterface<IActorLogicEvent>().OnSendTextToVoiceSynthesis(reply);
        }

        private void OnGetVoiceSynthesisResult(AudioClip result)
        {
            // 播放语音
            m_AudioPlayer.PlayOneShot(result);
            // TODO 临时 测试
            // GameEvent.EventMgr.GetInterface<IActorLogicEvent>().OnSendVoiceToVoiceRecg(result);
            // 知道语音后，开始打字机效果
            PlayWords(m_AIReply, result.length);
        }

        #endregion

        #region 打字机效果

        private async void PlayWords(string words, float lastTime)
        {
            if (string.IsNullOrEmpty(words))
            {
                // 输出提示
                Log.Warning("words is null or empty");
                return;
            }
            var delayTime = lastTime / words.Length;//计算每次延迟的时间
            for (int i = 0; i < words.Length; i++)//遍历插入字符串的长度
            {
                var currentText = words.Substring(0, i);//看demo1的代码注释
                m_ChatContent.text = currentText;
                // 慢一点点
                await UniTask.Delay((int)delayTime * 2000);//每次延迟的时间 数值越小 延迟越少
            }
        }

        #endregion

        #region 录音

        [SerializeField]
        private Button m_StartStopRec;

        /// <summary>
        /// 是否正在录音
        /// </summary>
        private bool m_IsRecording = false;

        private void OnStartStopRecClick()
        {
            //
            m_IsRecording = !m_IsRecording;
            // 
            if (m_IsRecording)
            {
                m_StartStopRec.GetComponentInChildren<TextMeshProUGUI>().text = "提交说话";
                GameEvent.EventMgr.GetInterface<IActorLogicEvent>().OnStartRecordVoice();
                // 重置
                m_Head.text = "你说";
                m_ChatContent.text = "";
            }
            else
            {
                m_StartStopRec.GetComponentInChildren<TextMeshProUGUI>().text = "开始说话";
                GameEvent.EventMgr.GetInterface<IActorLogicEvent>().OnStopRecordVoice();
            }
        }

        #endregion

        #region 生命周期

        protected override void OnInit(object userData)
        {
            base.OnInit(userData);
            // 初始化语音播放
            if (m_AudioPlayer == null)
            {
                m_AudioPlayer = GameModule.Sound.GetComponentInChildren<AudioSource>();
            }
            // 初始化界面
            m_StartStopRec.onClick.AddListener(OnStartStopRecClick);
            // 监听语音识别结果
            GameEvent.AddEventListener<string>(IActorLogicEvent_Event.OnGetVoiceRecgResult, OnGetVoiceRecgResult);
            // 监听AI反馈结果
            GameEvent.AddEventListener<string>(IActorLogicEvent_Event.OnGetAIReply, OnGetAIReply);
            // 监听语音合成结果
            GameEvent.AddEventListener<AudioClip>(IActorLogicEvent_Event.OnGetVoiceSynthesisResult, OnGetVoiceSynthesisResult);
        }

        protected override void OnRecycle()
        {
            base.OnRecycle();
            // 回收界面
            m_StartStopRec.onClick.RemoveListener(OnStartStopRecClick);
            // 移除监听语音识别结果
            GameEvent.RemoveEventListener<string>(IActorLogicEvent_Event.OnGetVoiceRecgResult, OnGetVoiceRecgResult);
            // 移除监听AI反馈结果
            GameEvent.RemoveEventListener<string>(IActorLogicEvent_Event.OnGetAIReply, OnGetAIReply);
            // 移除监听语音合成结果
            GameEvent.RemoveEventListener<AudioClip>(IActorLogicEvent_Event.OnGetVoiceSynthesisResult, OnGetVoiceSynthesisResult);
        }

        #endregion
    }
}
