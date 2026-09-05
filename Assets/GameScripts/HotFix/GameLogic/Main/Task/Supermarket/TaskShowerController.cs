using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using System.Threading.Tasks;

namespace GameLogic.Supermarket
{
    public class TaskShowerController : MonoBehaviour
    {
        [Tooltip("任务根节点")]
        public GameObject m_Root;

        /// <summary>
        /// 任务ID
        /// </summary>
        public int m_TaskID { get; set; }

        #region 进度

        [Tooltip("任务进度显示")]
        public TextMeshProUGUI m_CurrentProgressShower; // 当前完成个数

        /// <summary>
        /// 当前进度
        /// </summary>
        //[SyncVar(hook = nameof(OnCurProgressChanged))]
        int m_CurProgressValue = 0;

        /// <summary>
        /// 当前进度变化时
        /// </summary>
        /// <param name="oldValue"></param>
        /// <param name="newValue"></param>
        private void OnCurProgressChanged(int oldValue, int newValue)
        {
            if (Mathf.Abs(newValue - oldValue) > Mathf.Epsilon)
            {
                SetProgress(m_CurProgressValue, m_TotalProgressValue);
            }
        }

        /// <summary>
        /// 总进度
        /// </summary>
        //[SyncVar(hook = nameof(OnTotalProgressChanged))]
        int m_TotalProgressValue = 0;

        /// <summary>
        /// 总进度变化时
        /// </summary>
        private void OnTotalProgressChanged(int oldValue, int newValue)
        {
            if (Mathf.Abs(newValue - oldValue) > Mathf.Epsilon)
            {
                SetProgress(m_CurProgressValue, m_TotalProgressValue);
            }
        }

        /// <summary>
        /// 设置任务进度
        /// </summary>
        public void SetProgress(int currentValue, int totalValue)
        {
            // 记录进度
            m_CurProgressValue = currentValue;
            m_TotalProgressValue = totalValue;

#if MANAGER_SERVER
            // 显示进度
            m_CurrentProgressShower.text = $"{currentValue}/ {totalValue}";

            // 样式调整
            if (currentValue == 0)
            {
                m_CurrentProgressShower.color = Color.white;
                m_CurrentProgressShower.fontStyle = FontStyles.Normal;
            }
            else if (currentValue == totalValue)
            {
                // 设置为绿色
                m_CurrentProgressShower.color = Color.green;
                m_CurrentProgressShower.fontStyle = FontStyles.Normal;
            }
            else
            {
                m_CurrentProgressShower.color = Color.blue;
                m_CurrentProgressShower.fontStyle = FontStyles.Bold;
            }
#else
            // 客户端不显示进度文本
            m_CurrentProgressShower.text = "";
#endif
        }

        #endregion

        #region 状态

        /// <summary>
        /// 任务状态图片
        /// </summary>
        public ETaskStatus Status { get; set; }

        #endregion

        #region 完成状态

        [Tooltip("任务完成状态图片")]
        [SerializeField]
        private Toggle m_CompletedStatus;   // 任务已完成（显示时，未完成隐藏）

        /// <summary>
        /// 任务完成状态
        /// </summary>
        //[SyncVar(hook = nameof(OnCompletedStatusChanged))]
        bool m_IsCompleted = false;

        /// <summary>
        /// 任务完成状态变化时
        /// </summary>
        /// <param name="oldValue"></param>
        /// <param name="newValue"></param>
        private void OnCompletedStatusChanged(bool oldValue, bool newValue)
        {
            if (oldValue != newValue)
            {
                SetCompletedStatus(newValue);
            }
        }

        /// <summary>
        /// 设置任务完成状态
        /// </summary>
        /// <param name="isCompleted"></param>
        public void SetCompletedStatus(bool isCompleted)
        {
            m_IsCompleted = isCompleted;

#if MANAGER_SERVER
            // 设置显示
            m_CompletedStatus.isOn = isCompleted;
#endif
        }

        /// <summary>
        /// 完成状态
        /// </summary>
#if MANAGER_SERVER
        public bool IsCompleted => m_CompletedStatus.isOn;
#else
        public bool IsCompleted => m_IsCompleted;
#endif

        #endregion

        #region 图片

        [Tooltip("任务图片显示")]
        public Image m_ProductImgShower;

        /// <summary>
        /// 图片URL
        /// </summary>
        //[SyncVar(hook = nameof(OnImageURLChanged))]
        private string m_ImageURL;

        /// <summary>
        /// 图片
        /// </summary>
        private Sprite m_Image;

        /// <summary>
        /// 图片URL变化时
        /// </summary>
        /// <param name="oldValue"></param>
        /// <param name="newValue"></param>
        private void OnImageURLChanged(string oldValue, string newValue)
        {
            if (oldValue != newValue)
            {
                _ = SetImage(newValue);
            }
        }

        /// <summary>
        /// 加载任务图片
        public async Task SetImage(string url)
        /// </summary>
        {
            if (m_ImageURL == url)
            {
                Debug.Log("相同URL的图片已加载，无需重复加载");
                return;
            }
            // 记录URL
            m_ImageURL = url;
            // 清理上一次的图片
            var lastImg = m_ProductImgShower.sprite;
            if (lastImg != null)
            {
                GameModule.Resource.UnloadAsset(lastImg);
                lastImg = null;
            }
            // 加载图片
            m_Image = await GameModule.Resource.LoadAssetAsync<Sprite>(url);
            // 有可能任务单个更新频繁，旧的对象已经清理，await后，对象已经不存在。
            if (m_ProductImgShower != null)
            {
                m_ProductImgShower.sprite = m_Image;
            }
        }

        #endregion

        #region U3D

        private void OnDestroy()
        {
            GameModule.Resource.UnloadAsset(m_Image);
        }

        #endregion
    }
}
