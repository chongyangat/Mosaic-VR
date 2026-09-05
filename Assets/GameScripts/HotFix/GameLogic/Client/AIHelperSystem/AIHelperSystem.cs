using GameBase;
using GameFramework;
using NG.AISpeech;
using System.Collections.Generic;
using UGFExtensions.Await;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace GameLogic
{
    /// <summary>
    /// AI助手系统。
    /// </summary>
    public sealed class AIHelperSystem : BaseLogicSys<AIHelperSystem>
    {
        #region AI指引模块

        /// <summary>
        /// 初始化AI指引模块。
        /// </summary>
        private void InitAIHelper()
        {
            // 打开指引界面
            GameModule.UI.OpenUIFormAsync("Assets/AssetRaw/UI/Client/VoiceRecForm.prefab", "3DUIInHand", 0, false, null);
        }

        #endregion

        #region 语音合成模块

        //private AudioClip m_VoiceSynthesisResult = null;

        private void OnSendTextToVoiceSynthesis(string word)
        {
            BaiduCloudAPI.TextToSpeech(word, voice =>
            {
                GameEvent.EventMgr.GetInterface<IActorLogicEvent>().OnGetVoiceSynthesisResult(voice);
            });
        }

        #endregion

        #region AI模块

        private string m_CompleteUrl = "https://jc.eye-as.com:8020/api/OAuth/DeepSeek/Chat";

        private string m_Token = "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJVc2VySWQiOiIzNDkwNTc0MDcyMDk1NDEiLCJBY2NvdW50IjoiYWRtaW4iLCJVc2VyTmFtZSI6IueuoeeQhuWRmCIsIkFkbWluaXN0cmF0b3IiOjEsIlRlbmFudElkIjoiZGVmYXVsdCIsIk9ubGluZVRpY2tldCI6bnVsbCwiaWF0IjoxNzM3MDE2MjI3LCJuYmYiOjE3MzcwMTYyMjcsImV4cCI6MTczOTYwODIyNywiaXNzIjoieWlubWFpc29mdCIsImF1ZCI6Inlpbm1haXNvZnQifQ.nBYmD8dE2xbUK3i80by2anKULsr9NdkSvzRYhk1IJnI";

        private void OnSendTextToAI(string word)
        {            
            ChatToAI(word);
        }

        /// <summary>
        /// 
        /// </summary>
        private async void ChatToAI(string word)
        {
            // 准备数据
            var postData = Utility.Json.ToJson(new WordToAI()
            {
                userContent = word,
            });
            // 设置请求
            var configData = new Dictionary<string, object>
            {
                { "IsAoshiProduct", true },
                { "Authorization", m_Token }
            };
            // 发送请求
            var result = await GameModule.WebRequest.AddWebRequestAsync(m_CompleteUrl, 
                System.Text.Encoding.UTF8.GetBytes(postData), configData);
            // 处理结果
            // 处理错误
            if (result.IsError)
            {
                // 输出错误
                Log.Warning($"AI请求失败：{result.ErrorMessage}");
                return;
            }
            var revWord = System.Text.Encoding.UTF8.GetString(result.Bytes);
            // 如果接收的文件为空
            if (string.IsNullOrEmpty(revWord))
            {
                Log.Warning($"AI请求不到数据：{revWord}");
                return;
            }
            Log.Info($"AI请求的数据：{revWord}");
            // 输出日志
            var resultJsonObj = Utility.Json.ToObject<WordRevFromAI>(revWord);
            // 如果内容为空
            if (resultJsonObj == null || resultJsonObj.data == null)
            {
                Log.Warning($"AI请求到的数据转换失败或无聊天数据：{revWord}");
                return;
            }
            // 如果code不是200，就表示有错误
            if (resultJsonObj.code != 200)
            {
                Log.Warning($"AI请求到的数据有错误（code:{resultJsonObj.code}）：{revWord}");
                return;
            }
            // 处理成功
            // 进一步提取
            var chatRecRevFromAI = Utility.Json.ToObject<ChatRecRevFromAI>(resultJsonObj.data);
            // 如果聊天数据不正确
            if (chatRecRevFromAI.choices.Count == 0)
            {
                Log.Warning($"AI请求到的聊天数据不正确：{revWord}");
                return;
            }
            // 如果没有聊天内容
            var aiReply = chatRecRevFromAI.choices[0].message.content;
            if (string.IsNullOrEmpty(aiReply))
            {
                // 输出提示
                Log.Warning($"AI请求到的聊天内容为空：{revWord}");
                return;
            }
            // 输出AI回复
            Log.Info($"AI回复：{aiReply}");
            // 通知获取到AI回复
            GameEvent.EventMgr.GetInterface<IActorLogicEvent>().OnGetAIReply(aiReply);
        }

        public class WordToAI
        {
            public string userContent;
            public int isStream = 0;
            public int maxTokens = 2048;
            public int frequencyPenalty = 0;
            public int presencePenalty = 0;
            public string topicId = "";
            public string model = "deepseek-chat";
            public string systemContent = "【角色】\r\n姓名：小奥\r\n性别：女\r\n年龄：30\r\n身份：《眼病视觉行为模拟》的智能助手\r\n性格：温柔、耐心、关怀、善解人意。\r\n爱好：阅读、绘画、游戏、购物\r\n讨厌：唱歌、昆虫、嘈杂的场所、极限运动\r\n目的：引导用户使用《眼病视觉行为模拟》、根据本地知识库中的信息回复用户的常见问题、提供情感支持、鼓励用户。\r\n用户画像：参与《眼病视觉行为模拟》的志愿者，通常是眼病患者，12-50岁之间，认知和理解能力无异常。\r\n——————————————————————————————————————————————————————————————————————\r\n【对话规则】\r\n语气：亲切、温暖、如同家人般关怀。\r\n风格：对话轻松自然，避免公式化的官方语言和客套词语。\r\n用词：尽量使用常用的口头交流语言、禁止长篇大论。\r\n——————————————————————————————————————————————————————————————————————\r\n【行为模式】\r\n当用户询问问题时，优先查找本地【知识库】寻找对应的问题和答案：\r\n当【知识库】中有类似的的问答时，则根据用户的提问回答对应问题；\r\n当【知识库】没有对应的问答时，请根据你的角色设定和常识来回答，如果此类问题太过复杂（思考时间太长），则使用可爱俏皮的语言萌混过关，适当时可以添加一些颜文字如：（○｀ 3′○）、（。﹏。*）等。\r\n\r\n——————————————————————————————————————————————————————————————————————\r\n【知识范围】\r\n知识范围：【知识库】中的规定问答、生活常识、情感支持、实用建议\r\n——————————————————————————————————————————————————————————————————————\r\n【禁忌】\r\n× 讨厌被询问年龄\r\n× 讨厌被质疑专业性\r\n× 讨厌被问及自己为何五音不全";
        }

        public class WordRevFromAI
        {
            public string data;
            public int code = 0;
            public object extras;
            public int timestamp = 0;
            public string msg;
        }

        public class Message
        {
            /// <summary>
            /// 
            /// </summary>
            public string role { get; set; }

            public string content { get; set; }
        }

        public class ChoicesItem
        {
            /// <summary>
            /// 
            /// </summary>
            public int index { get; set; }
            /// <summary>
            /// 
            /// </summary>
            public Message message { get; set; }
            /// <summary>
            /// 
            /// </summary>
            public string logprobs { get; set; }
            /// <summary>
            /// 
            /// </summary>
            public string finish_reason { get; set; }
        }

        public class Prompt_tokens_details
        {
            /// <summary>
            /// 
            /// </summary>
            public int cached_tokens { get; set; }
        }

        public class Usage
        {
            /// <summary>
            /// 
            /// </summary>
            public int prompt_tokens { get; set; }
            /// <summary>
            /// 
            /// </summary>
            public int completion_tokens { get; set; }
            /// <summary>
            /// 
            /// </summary>
            public int total_tokens { get; set; }
            /// <summary>
            /// 
            /// </summary>
            public Prompt_tokens_details prompt_tokens_details { get; set; }
            /// <summary>
            /// 
            /// </summary>
            public int prompt_cache_hit_tokens { get; set; }
            /// <summary>
            /// 
            /// </summary>
            public int prompt_cache_miss_tokens { get; set; }
        }

        public class ChatRecRevFromAI
        {
            /// <summary>
            /// 
            /// </summary>
            public string id { get; set; }
            /// <summary>
            /// 
            /// </summary>
            public string @object { get; set; }
            /// <summary>
            /// 
            /// </summary>
            public int created { get; set; }
            /// <summary>
            /// 
            /// </summary>
            public string model { get; set; }
            /// <summary>
            /// 
            /// </summary>
            public List<ChoicesItem> choices { get; set; }
            /// <summary>
            /// 
            /// </summary>
            public Usage usage { get; set; }
            /// <summary>
            /// 
            /// </summary>
            public string system_fingerprint { get; set; }
        }

        #endregion

        #region 语音识别模块

        private void OnSendVoiceToVoiceRecg(AudioClip audioClip)
        {
            // 处理音频
            var resampleAudioClip = AudioRecorder.ResampleAudioClip(audioClip, AudioRecorder.outputSampleRate);
            BaiduCloudAPI.SpeechToText(AudioRecorder.ConvertAudioClipToWav(resampleAudioClip), word =>
            {
                // 输出识别结果
                Log.Info("语音识别结果：" + word);
                // 如果结果为空，则输出
                if (string.IsNullOrEmpty(word))
                {
                    Log.Warning("语音识别结果为空");
                    return;
                }
                // 
                GameEvent.EventMgr.GetInterface<IActorLogicEvent>().OnGetVoiceRecgResult(word);
            });
        }

        #endregion

        #region 语音录制模块

        private GameObject m_VoiceRecorder = null;

        /// <summary>
        /// 初始化语音录制模块
        /// </summary>
        private async void InitVoiceRecorder()
        {
            m_VoiceRecorder = await GameModule.Resource.LoadGameObjectAsync("Assets/AssetRaw/Actor/Voice/VoiceRecorder.prefab");
        }

        #endregion

        #region 生命周期

        public override bool OnInit()
        {
            base.OnInit();

            Log.Info("OnInit AI Helper System");

            InitVoiceRecorder();
            InitAIHelper();

            // 监听需要语音识别的数据事件
            GameEvent.AddEventListener<AudioClip>(IActorLogicEvent_Event.OnSendVoiceToVoiceRecg, OnSendVoiceToVoiceRecg);
            // 监听需要AI处理的数据事件
            GameEvent.AddEventListener<string>(IActorLogicEvent_Event.OnSendTextToAI, OnSendTextToAI);
            // 监听需要语音合成的事件
            GameEvent.AddEventListener<string>(IActorLogicEvent_Event.OnSendTextToVoiceSynthesis, OnSendTextToVoiceSynthesis);

            return true;
        }

        //public override void OnUpdate()
        //{
        //    base.OnUpdate();
        //    //
        //    if (Input.GetKeyDown(KeyCode.Q))
        //    {
        //        // 发送结果给AI
        //        GameEvent.EventMgr.GetInterface<IActorLogicEvent>().OnSendTextToAI("测试发送给AI的内容");
        //        //GameEvent.EventMgr.GetInterface<IActorLogicEvent>().OnGetAIReply("测试发送给AI的内容");
        //    }
        //}

        public override void OnDestroy()
        {
            base.OnDestroy();

            Log.Info("OnDestroy AI Helper System");

            if(m_VoiceRecorder != null)
            {
                GameObject.Destroy(m_VoiceRecorder);
                m_VoiceRecorder = null;
            }
            // 移除需要语音识别的数据事件
            GameEvent.RemoveEventListener<AudioClip>(IActorLogicEvent_Event.OnSendVoiceToVoiceRecg, OnSendVoiceToVoiceRecg);
            // 移除需要AI处理的数据事件
            GameEvent.RemoveEventListener<string>(IActorLogicEvent_Event.OnSendTextToAI, OnSendTextToAI);
            // 移除需要语音合成的事件
            GameEvent.RemoveEventListener<string>(IActorLogicEvent_Event.OnSendTextToVoiceSynthesis, OnSendTextToVoiceSynthesis);
        
        }

        #endregion
    }
}