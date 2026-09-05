using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityGameFramework.Editor;
using zFramework.Extension;

namespace GameScripts.Editor
{
    public static class ProductsExporter
    {
        [MenuItem("Tools/VBSOED/场景商品数据导出/所有到CSV")]
        public static void AllProductsToExcel()
        {
            // 用Tag“Good”获取所有场景商品对象
            var goods = GameObject.FindGameObjectsWithTag("Good");
            ExportToExcel(goods);
        }

        [MenuItem("Tools/VBSOED/场景商品数据导出/选择部分至CSV")]
        public static void SelectedProductsToExcel()
        {
            // 如果没有选择对象，则不导出，输出日志
            if (Selection.gameObjects.Length == 0)
            {
                Debug.LogWarning("没有选择对象，不导出");
                return;
            }
            // 筛选出Tag为“Good”的对象
            var goods = Selection.gameObjects.Where(g =>
            {
                // 如果选择对象没有Tag“Good”，则不导出，输出日志
                if (!g.CompareTag("Good"))
                {
                    Debug.LogWarning($"{g.name}没有Tag“Good”，不导出");
                    return false;
                }
                return true;
            }).ToArray();
            ExportToExcel(goods);
        }

        [MenuItem("Tools/VBSOED/场景商品数据导出/打开表格目录", priority = 20)]
        public static void OpenConfigFolder()
        {
            // 目录不存在，则输出日志
            if (!System.IO.Directory.Exists(Application.dataPath + SAVE_PATH))
            {
                Debug.LogWarning("目录不存在，请先导出数据");
                return;
            }
            // 打开保存路径
            OpenFolder.Execute(Application.dataPath + SAVE_PATH);
        }

        /// <summary>
        /// 保存路径
        /// </summary>
        private const string SAVE_PATH = "/../Cache/ProjectTools/Export/";

        /// <summary>
        /// 导出商品数据到Excel
        /// </summary>
        /// <param name="goods"></param>
        private static void ExportToExcel(GameObject[] goods)
        {
            var sortedGoods = goods.OrderBy(g => g.name).ToArray();
            // 输出日志，开始导出
            Debug.Log("商品数据-开始导出");
            // 组装每个商品数据
            var productsList = sortedGoods.Select(good =>
            {
                // 获取商品预置体路径
                // 通过场景中选择的GameObject获取在AssetDatabase中的预置体路径
                var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(good);
                // 如是果没有获取到预置体路径，则输出警告
                if (string.IsNullOrEmpty(path))
                {
                    Debug.LogWarning($"{good.name}没有获取到预置体路径，可能其不是预置体，忽略处理。");
                }
                // 输出找到商品的路径
                Debug.Log($"商品数据-找到商品：{path}");
                // 获取商品的位置
                var position = good.transform.position;
                // 获取商品的角度
                var rotation = good.transform.rotation;
                // 返回商品数据
                return new ProductData
                {
                    Name = good.name,
                    PrefabPath = path,
                    Position = Vector3ToString(position),
                    Rotation = Vector3ToString(rotation.eulerAngles)
                };
            }).ToList();
            // 利用Unity的临时目录清理文件
            var savePath = Application.dataPath + SAVE_PATH + "Product.csv";
            // 输出日志，商品数量，保存位置
            Debug.Log($"商品数据-正在导出：商品数量：{productsList.Count}，保存位置：{savePath}");
            // 导出商品数据到Excel
            // 如果场景中没有商品，则不导出，输出日志
            if (productsList.Count == 0)
            {
                Debug.LogWarning("场景中没有商品，不导出");
                return;
            }
            else
            {
                try
                {
                    CsvUtility.Write(productsList, savePath);
                }
                catch (System.Exception ex)
                {
                    // 输出异常信息
                    Debug.LogError(ex.Message);
                    // 输出日志，导出失败
                    Debug.LogError($"商品数据-导出失败：兄弟，你大概率没关前一个导出文件，被锁定了，关掉再试试。");
                    return;
                }
            }
            // 输出日志，导出完成
            Debug.Log($"商品数据-导出完成：商品数量：{productsList.Count}，保存位置(菜单可快捷打开)：{savePath}");
        }

        /// <summary>
        /// 标准化输出三维向量至文本
        /// </summary>
        /// <param name="v"></param>
        private static string Vector3ToString(Vector3 v)
        {
            return $"{v.x},{v.y},{v.z}";
        }

        /// <summary>
        /// 商品数据
        /// </summary>
        private class ProductData
        {
            /// <summary>
            /// 商品名
            /// </summary>
            public string Name;

            /// <summary>
            /// 商品预置体路径
            /// </summary>
            public string PrefabPath;

            /// <summary>
            /// 商品位置
            /// </summary>
            public string Position;

            /// <summary>
            /// 商品朝向
            /// </summary>
            public string Rotation;

        }
    }
}