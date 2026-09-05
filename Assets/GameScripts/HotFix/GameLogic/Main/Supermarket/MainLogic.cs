using UGFExtensions.Await;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace GameLogic.Supermarket
{
    public class MainLogic : MonoBehaviour
    {
        #region U3D

        private async void OnEnable()
        {
#if MANAGER_SERVER
            // 打开任务界面
            await GameModule.UI.OpenUIFormAsync("Assets/AssetRaw/UI/Server/TaskS/SupermarketTasksListForm.prefab", "UI", 0, false, null);
            // 打开光照设置界面
            await GameModule.UI.OpenUIFormAsync("Assets/AssetRaw/UI/Server/ManagerLightSettingForm.prefab", "UI", 0, false, null);
#else
            // 打开任务界面
            await GameModule.UI.OpenUIFormAsync("Assets/AssetRaw/UI/Client/TaskC/SupermarketTasksListForm.prefab", "3DUIInHand", 0, false, null);
#endif
        }

        private void OnDisable()
        {
            UIForm form;
#if MANAGER_SERVER
            // 关闭任务界面
            form = GameModule.UI.GetUIForm("Assets/AssetRaw/UI/Server/TaskS/SupermarketTasksListForm.prefab");
            if (form != null)
            {
                GameModule.UI.CloseUIForm(form);
            }
            // 关闭任务结算界面
            form = GameModule.UI.GetUIForm("Assets/AssetRaw/UI/Server/TaskS/SupermarketTasksResultForm.prefab");
            if (form != null)
            {
                GameModule.UI.CloseUIForm(form);
            }
            // 关闭光照设置界面
            form = GameModule.UI.GetUIForm("Assets/AssetRaw/UI/Server/ManagerLightSettingForm.prefab");
            if (form != null)
            {
                GameModule.UI.CloseUIForm(form);
            }
#else
            // 关闭任务界面
            form = GameModule.UI.GetUIForm("Assets/AssetRaw/UI/Client/TaskC/SupermarketTasksListForm.prefab");
            if (form != null)
            {
                GameModule.UI.CloseUIForm(form);
            }
            // 关闭任务结算界面
            form = GameModule.UI.GetUIForm("Assets/AssetRaw/UI/Client/TaskC/SupermarketTasksResultForm.prefab");
            if (form != null)
            {
                GameModule.UI.CloseUIForm(form);
            }
#endif

        }
        #endregion
    }

}