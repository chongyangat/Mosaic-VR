using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace GameMain.Editor.Assets.GameScripts.Editor.ProjectTools.Scenes
{
    public static class SceneClearer
    {
        [MenuItem("Tools/VBSOED/场景清理/清理所有未激活子对象")]
        public static void ClearInactiveObjs()
        {
            // 获取当前选中的对象
            GameObject[] selectedObjects = Selection.gameObjects;

            if (selectedObjects.Length == 0)
            {
                Debug.LogWarning("请先选择一个或多个父对象！");
                return;
            }

            // 收集所有未激活对象
            List<GameObject> inactiveObjects = new List<GameObject>();
            foreach (GameObject parent in selectedObjects)
            {
                CollectInactiveChildren(parent.transform, inactiveObjects);
            }

            if (inactiveObjects.Count == 0)
            {
                Debug.Log("所选对象下没有找到未激活的子对象");
                return;
            }

            // 显示确认对话框
            if (!EditorUtility.DisplayDialog("确认清理",
                $"即将删除 {inactiveObjects.Count} 个未激活对象，此操作不可撤销！",
                "确定", "取消"))
            {
                return;
            }

            // 记录删除数量
            int deletedCount = 0;
            List<string> deletedNames = new List<string>();

            // 删除未激活对象
            foreach (GameObject obj in inactiveObjects)
            {
                if (obj != null)
                {
                    deletedNames.Add(obj.name);
                    Undo.DestroyObjectImmediate(obj);
                    deletedCount++;
                }
            }

            // 输出结果
            Debug.Log($"<color=#ff0000>已清理未激活对象: {deletedCount} 个</color>");
            Debug.Log($"<color=#00ff00>清理对象列表:</color>");
            foreach (string name in deletedNames)
            {
                Debug.Log($"- {name}");
            }
        }

        private static void CollectInactiveChildren(Transform parent, List<GameObject> inactiveObjects)
        {
            foreach (Transform child in parent)
            {
                // 检查对象是否自身未激活（activeSelf为false）
                // 排除因父级未激活导致的未激活状态
                if (!child.gameObject.activeSelf)
                {
                    inactiveObjects.Add(child.gameObject);
                }

                // 递归检查子对象
                if (child.childCount > 0)
                {
                    CollectInactiveChildren(child, inactiveObjects);
                }
            }
        }
    }
}