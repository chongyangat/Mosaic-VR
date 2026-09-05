using UnityEngine;

namespace GameMain
{
    /// <summary>
    /// 启动器
    /// </summary>
    public class Launcher : MonoBehaviour
    {
        #region U3D

        private void Start()
        {
            // 初始化所有时间戳提供器
            TimestampProviderInitializer.InitializeAll();
        }

        #endregion
    }
}
