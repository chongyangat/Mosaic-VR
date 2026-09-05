// ===================================================
// Copyright ©2025. All rights reserved.
// Author：Aovis Vision
// CreateTime：2025/6/26 10:06:11
// Version: v1.0
// Description：商品选择工具
// ===================================================

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using GameLogic;

namespace GameScripts.Editor
{
    /// <summary>
    /// 商品选择工具
    /// </summary>
    public static class ProductSelect
    {

        #region 菜单

        /// <summary>
        /// 选择所选对象的所有子商品
        /// </summary>
        [MenuItem("Tools/VBSOED/商品选择/选择所选对象的所有子商品")]
        public static void SelectAllChildProducts()
        {
            // 如果没有选择对象，则输出日志
            var count = Selection.gameObjects.Length;
            if (count == 0)
            {
                Debug.Log("没有选择对象");
                return;
            }
            // 创建一个商品对象列表
            var productsList = new List<Product>();
            // 一个个寻找子对象中的商品对象
            for (int index = 0; index < count; index++)
            {
                var productsBelow = Selection.gameObjects[index].GetComponentsInChildren<Product>(true);
                // 添加到商品列表
                productsList.AddRange(productsBelow);
            }
            // 多选
            if (productsList.Count > 0)
            {
                Selection.objects = productsList.ToArray();
            }
            else
            {
                Debug.Log("所有所选对象下均没有商品对象");
            }
        }

        #endregion

        #region 工具


        #endregion

    }
}