
using GameLogic;
using NG.AISpeech;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace GameBase
{
    public class RecordVoice : MonoBehaviour
    {
        [SerializeField]
        private VoiceRecorder m_VoiceRecorder;

        private void OnVoiceRecordStart()
        {
            m_VoiceRecorder.StartRecordingWithPermission();
        }

        private void OnStopRecordVoice()
        {
            m_VoiceRecorder.StopRecording();
            // TODO 临时 测试
            //var audioPlayer = gameObject.GetOrAddComponent<AudioSource>();
            //audioPlayer.clip = m_VoiceRecorder.RecordedClip;
            //audioPlayer.loop = true;
            //audioPlayer.Play();
            //// 保存录音文件
            //var resampleAudioClip = AudioRecorder.ResampleAudioClip(m_VoiceRecorder.RecordedClip, AudioRecorder.outputSampleRate);
            //var audioData = AudioRecorder.ConvertAudioClipToWav(resampleAudioClip);
            //System.IO.File.WriteAllBytes(Application.dataPath + "/../voice.mp3", audioData);
            // 停止后，将录音文件发送给语音识别服务
            GameEvent.EventMgr.GetInterface<IActorLogicEvent>().OnSendVoiceToVoiceRecg(m_VoiceRecorder.RecordedClip);
        }

        #region U3D

        private void OnEnable()
        {
            GameEvent.AddEventListener(IActorLogicEvent_Event.OnStartRecordVoice, OnVoiceRecordStart);
            GameEvent.AddEventListener(IActorLogicEvent_Event.OnStopRecordVoice, OnStopRecordVoice);
        }

        private void OnDisable()
        {
            GameEvent.RemoveEventListener(IActorLogicEvent_Event.OnStartRecordVoice, OnVoiceRecordStart);
            GameEvent.RemoveEventListener(IActorLogicEvent_Event.OnStopRecordVoice, OnStopRecordVoice);
        }

        #endregion
    }
}
