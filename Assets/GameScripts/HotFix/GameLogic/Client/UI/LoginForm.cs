using UnityGameFramework.Runtime;
using UnityEngine.UI;
using GameLogic;
using UnityEngine;

public class LoginForm : UIFormLogic
{
    /// <summary>
    /// 登录按钮
    /// </summary>
    public Button LoginButton;

    /// <summary>
    /// 等待按钮
    /// </summary>
    public Button WaitButton;

    private void OnLoginButtonClick()
    {
        // 发送登录请求
        GameEvent.EventMgr.GetInterface<ILoginUI>().OnUserReadyChange(true);
        // 关闭自身，由外部关闭。
        // GameModule.UI.CloseUIForm(GetComponent<UIForm>());
        // 显示等待按钮
        OnShowStart(false);
    }

    /// <summary>
    /// 通知准备好了的事件状态
    /// </summary>
    private void OnShowStart(bool isReady)
    {
        //按钮显示
        LoginButton.gameObject.SetActive(isReady);
        //等待按钮
        WaitButton.gameObject.SetActive(!isReady);
    }

    private void OnEnterRoomScene()
    {
        OnShowStart(true);
    }

    private void OnClientDisconnected()
    {
        OnShowStart(false);
    }

    /// <summary>
    /// 注销后的状态还原成初始状态
    /// </summary>
    private void OnInitialState()
    {
        //按钮显示
        LoginButton.gameObject.SetActive(false);
        //等待按钮
        WaitButton.enabled = false;
        WaitButton.gameObject.SetActive(true);
    }

    #region 生命周期

    protected override void OnOpen(object userData)
    {
        base.OnOpen(userData);
        // 添加监听
        LoginButton.onClick.AddListener(OnLoginButtonClick);
        // 注册进入房间状态
        GameEvent.AddEventListener(IActorLogicEvent_Event.OnEnteredRoomScene, OnEnterRoomScene);
        // 注册断开连接状态
        GameEvent.AddEventListener(IActorLogicEvent_Event.OnClientDisconnected, OnClientDisconnected);
        // 
        OnInitialState();
    }

#if DEBUG_LOGIN_FORM
    public override void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
        base.OnUpdate(elapseSeconds, realElapseSeconds);
        // 
        if (Input.GetKeyUp(KeyCode.Q))
        {
            //GameEvent.EventMgr.GetInterface<ILoginUI>().OnUserReady();
            OnLoginButtonClick();
        }

        //if (Input.GetKeyUp(KeyCode.W))
        //{
        //    GameEvent.EventMgr.GetInterface<IActorLogicEvent>().OnStart("超市");
        //}
    } 
#endif

    public override void OnClose(bool isShutdown, object userData)
    {
        base.OnClose(isShutdown, userData);
        // 移除监听
        LoginButton.onClick.RemoveListener(OnLoginButtonClick);
        // 注销进入房间状态
        GameEvent.RemoveEventListener(IActorLogicEvent_Event.OnEnteredRoomScene, OnEnterRoomScene);
        // 注销断开连接状态
        GameEvent.RemoveEventListener(IActorLogicEvent_Event.OnClientDisconnected, OnClientDisconnected);
    }

    #endregion
}
