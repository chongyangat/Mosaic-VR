using UnityEngine;
using UnityEngine.Events;
using UnityGameFramework.Runtime;

namespace GameMain
{
    /// <summary>
    /// 根据平台激活对象
    /// </summary>
    public class ActiveByPlatform : MonoBehaviour
    {
        #region 用户端

        [Header("User Client")]
        [Tooltip("用户端需要激活的对象列表")]
        [SerializeField]
        private UnityEvent OnUserClientActive;

        [Tooltip("用户端在框架初始化后需要激活的对象列表")]
        [SerializeField]
        private UnityEvent OnUserClientActiveAfterGFInit;

        #endregion

        #region 管理端

        [Header("Manager Server")]
        [Tooltip("管理端需要激活的对象列表")]
        [SerializeField]
        private UnityEvent OnManagerServerActive;

        [Tooltip("管理端在框架初始化后需要激活的对象列表")]
        [SerializeField]
        private UnityEvent OnManagerServerActiveAfterGFInit;

        #endregion

        public void ActiveAfterGFInit()
        {
            // 激活
#if MANAGER_SERVER
            OnManagerServerActiveAfterGFInit?.Invoke();
#else
            OnUserClientActiveAfterGFInit?.Invoke();
#endif
        }

        #region U3D

        private void OnEnable()
        {
            // 激活 
#if MANAGER_SERVER
            OnManagerServerActive?.Invoke();
#else
            OnUserClientActive?.Invoke();
#endif
        }

        #endregion
    }
}
