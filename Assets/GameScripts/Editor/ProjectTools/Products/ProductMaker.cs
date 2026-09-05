// ===================================================
// Copyright ©2025. All rights reserved.
// Author：Aovis Vision
// CreateTime：2025/4/1 9:17:11
// Version: v1.8 (命名已还原，逻辑按新需求)
// Description：商品制作工具
// ===================================================

using Meta.XR.BuildingBlocks;
using Mirror;
using Oculus.Interaction;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using GameLogic;

namespace GameScripts.Editor
{
    public static class ProductMaker
    {
        #region 菜单

        [MenuItem("Tools/VBSOED/商品配置/移除商品属性（所选商品对象）（修改场景对象，非预置体）")]
        public static void RemoveSelectedProductsProperties()
        {
            var count = Selection.gameObjects.Length;
            if (count == 0)
            {
                Debug.LogWarning("没有选择对象，请先选择一个商品对象。");
                return;
            }
            Debug.Log($"##########移除所选商品对象的商品属性开始（总计{count}个）##########");
            var productsToHandle = new List<Product>();
            for (int index = 0; index < count; index++)
            {
                var product = Selection.gameObjects[index].GetComponentInChildren<Product>();
                if (product == null)
                {
                    Debug.LogWarning($"商品对象 {Selection.gameObjects[index].name} 下没有商品对象，跳过。");
                    continue;
                }
                productsToHandle.Add(product);
                Debug.Log($"商品对象 {Selection.gameObjects[index].name} 添加了1个商品对象。");
            }
            var finishedCnt = RemoveProductsProperties(productsToHandle.ToArray());
            if (finishedCnt > 0)
            {
                EditorUtility.SetDirty(Selection.gameObjects[0]);
            }
            Debug.Log($"##########移除所选商品对象的商品属性结束：完成（{finishedCnt}/{productsToHandle.Count}）##########");
        }

        [MenuItem("Tools/VBSOED/商品配置/移除商品属性（所选对象的所有子商品）（修改场景对象，非预置体）")]
        public static void RemoveSelectedProductsPropertiesInAllChildren()
        {
            var count = Selection.gameObjects.Length;
            if (count == 0)
            {
                Debug.LogWarning("没有选择对象，请先选择一个子对象中包含商品对象的对象。");
                return;
            }
            Debug.Log($"##########移除所选对象下所有子商品的商品属性开始（总计{count}个）##########");
            var productsToHandle = new List<Product>();
            for (int index = 0; index < count; index++)
            {
                var productsRoot = Selection.gameObjects[index];
                var products = productsRoot.GetComponentsInChildren<Product>();
                if (products.Length == 0)
                {
                    Debug.LogWarning($"商品对象 {productsRoot.name} 下没有商品对象，跳过。");
                    continue;
                }
                productsToHandle.AddRange(products);
                Debug.Log($"商品对象 {productsRoot.name} 添加了{products.Length}个商品对象。");
            }
            var finishedCnt = RemoveProductsProperties(productsToHandle.ToArray());
            if (finishedCnt > 0)
            {
                EditorUtility.SetDirty(Selection.gameObjects[0]);
            }
            Debug.Log($"##########移除所选对象下所有子商品的商品属性结束：完成（{finishedCnt}/{productsToHandle.Count}）##########");
        }

        /// <summary>
        /// 批量配置商品：同步 + 可拾取（修改预置体）
        /// 最外层父物体（根）仅设置 Tag = "Good"，不加任何组件
        /// 所有自身含 MeshFilter + MeshRenderer 的子物体添加全部功能组件，但不设 Tag
        /// 其他对象完全忽略
        /// </summary>
        [MenuItem("Tools/VBSOED/商品配置/同步+可拾取（修改预置体）")]
        public static void SetupSelectedProducts()
        {
            var count = Selection.gameObjects.Length;
            if (count == 0)
            {
                Debug.LogWarning("没有选择对象，请先选择一个商品对象预置体。");
                return;
            }
            Debug.Log($"##########批量配置商品预置体开始（总计{count}个）##########");

            var finishedCnt = 0;
            var noMeshSkipCnt = 0;
            var notPrefabCnt = 0;
            var unmodifiedCnt = 0;

            // 判断 GameObject 自身是否包含有效网格（不递归子物体）
            bool HasValidMeshOnSelf(GameObject go)
            {
                var mf = go.GetComponent<MeshFilter>();
                var mr = go.GetComponent<MeshRenderer>();
                return mf != null && mr != null && mf.sharedMesh != null;
            }

            for (int index = 0; index < count; index++)
            {
                var prefabSrc = Selection.gameObjects[index];
                Debug.Log($"---------正在处理预置体：{prefabSrc.name}({(index + 1)}/{count})---------");

                // 必须是 Regular Prefab
                if (PrefabUtility.GetPrefabAssetType(prefabSrc) != PrefabAssetType.Regular)
                {
                    Debug.LogWarning($"预置体 {prefabSrc.name} 不是本体，跳过。");
                    notPrefabCnt++;
                    continue;
                }

                // 实例化临时副本用于修改
                var tempInstance = PrefabUtility.InstantiatePrefab(prefabSrc) as GameObject;
                bool isModified = false;

                // ✅【关键1】给最外层父物体（根）设置 Tag，仅此操作
                if (!tempInstance.CompareTag("Good"))
                {
                    tempInstance.tag = "Good";
                    isModified = true;
                }

                // 收集所有自身包含有效网格的子物体（包括根自己，如果它有网格）
                var validMeshObjects = new List<GameObject>();
                var allObjects = tempInstance.GetComponentsInChildren<Transform>(includeInactive: true);
                foreach (var t in allObjects)
                {
                    if (HasValidMeshOnSelf(t.gameObject))
                    {
                        validMeshObjects.Add(t.gameObject);
                    }
                }

                if (validMeshObjects.Count == 0)
                {
                    Debug.LogWarning($"预置体 {prefabSrc.name} 中没有任何对象包含有效的 MeshFilter + MeshRenderer，跳过。");
                    noMeshSkipCnt++;
                    Object.DestroyImmediate(tempInstance);
                    continue;
                }

                // ✅【关键2】对每个有效网格对象添加全部功能组件，但不设置 Tag
                foreach (var obj in validMeshObjects)
                {
                    bool objModified = false;

                    // Collider
                    var collider = obj.GetComponent<Collider>();
                    if (collider == null)
                    {
                        var meshCollider = obj.AddComponent<MeshCollider>();
                        meshCollider.convex = true;
                        objModified = true;
                    }
                    else if (collider is MeshCollider mc && !mc.convex)
                    {
                        mc.convex = true;
                        objModified = true;
                    }

                    // Rigidbody
                    if (obj.GetComponent<Rigidbody>() == null)
                    {
                        var rb = obj.AddComponent<Rigidbody>();
                        rb.interpolation = RigidbodyInterpolation.Interpolate;
                        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                        objModified = true;
                    }

                    // NetworkIdentity + NetworkTransformReliable
                    if (obj.GetComponent<NetworkIdentity>() == null)
                    {
                        obj.AddComponent<NetworkIdentity>();
                        var nt = obj.AddComponent<NetworkTransformReliable>();
                        nt.syncScale = true;
                        nt.target = obj.transform;
                        objModified = true;
                    }
                    else
                    {
                        var nt = obj.GetComponent<NetworkTransformReliable>();
                        if (nt == null)
                        {
                            obj.AddComponent<NetworkTransformReliable>();
                            objModified = true;
                        }
                        else if (nt.target != obj.transform)
                        {
                            nt.target = obj.transform;
                            objModified = true;
                        }
                    }

                    // Product
                    if (obj.GetComponentInChildren<Product>() == null)
                    {
                        obj.AddComponent<Product>();
                        objModified = true;
                    }

                    // BuildingBlock + TouchHandGrab 子结构
                    if (obj.GetComponentInChildren<BuildingBlock>() == null)
                    {
                        obj.AddComponent<BuildingBlock>();
                        AddProductChildComps(obj);
                        objModified = true;
                    }
                    else
                    {
                        AddProductChildComps(obj);
                    }

                    if (objModified) isModified = true;
                }

                // 应用修改回预置体
                if (isModified)
                {
                    EditorUtility.SetDirty(tempInstance);
                    PrefabUtility.ApplyPrefabInstance(tempInstance, InteractionMode.UserAction);
                    finishedCnt++;
                }
                else
                {
                    unmodifiedCnt++;
                }

                Object.DestroyImmediate(tempInstance);
            }

            string logMsg = $"##########批量配置商品完成：完成（{finishedCnt}/{count}）";
            if (noMeshSkipCnt > 0) logMsg += $"，无网格（{noMeshSkipCnt}）";
            if (notPrefabCnt > 0) logMsg += $"，非本体预置体（{notPrefabCnt}）";
            if (unmodifiedCnt > 0) logMsg += $"，未修改（{unmodifiedCnt}）";
            logMsg += "##########";
            Debug.Log(logMsg);
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 移除商品相关组件和配置
        /// </summary>
        private static int RemoveProductsProperties(Product[] productsList)
        {
            var count = productsList.Length;
            Debug.Log($"##########移除商品对象的商品属性开始（总计{count}个）##########");
            var finishedCnt = 0;

            for (int index = 0; index < count; index++)
            {
                var productObj = productsList[index].gameObject;
                Debug.Log($"---------正在处理商品对象：{productObj.name}({(index + 1)}/{count})---------");

                // 清除 Tag（恢复为 Untagged）
                productObj.tag = "Untagged";

                // 移除功能组件
                Object.DestroyImmediate(productObj.GetComponentInChildren<Rigidbody>());
                Object.DestroyImmediate(productObj.GetComponentInChildren<NetworkTransformReliable>());
                Object.DestroyImmediate(productObj.GetComponentInChildren<NetworkIdentity>());
                Object.DestroyImmediate(productObj.GetComponentInChildren<Product>());
                Object.DestroyImmediate(productObj.GetComponentInChildren<BuildingBlock>());

                // 移除自动生成的子对象
                var touchHandGrab = productObj.transform.Find("[BuildingBlock] TouchHandGrab");
                if (touchHandGrab != null)
                    Object.DestroyImmediate(touchHandGrab.gameObject);

                Debug.Log($"---------商品对象 {productObj.name} 配置移除完成---------");
                finishedCnt++;
            }

            Debug.Log($"##########移除商品对象的商品属性完成：完成（{finishedCnt}/{count}）##########");
            return finishedCnt;
        }

        /// <summary>
        /// 为有网格的对象添加交互子结构（TouchHandGrab 等）
        /// 注意：函数名严格保持为 AddProductChildComps（未改动！）
        /// </summary>
        private static void AddProductChildComps(GameObject parentWithMesh)
        {
            var touchHandGrab = parentWithMesh.transform.Find("[BuildingBlock] TouchHandGrab")?.gameObject;
            if (touchHandGrab == null)
            {
                touchHandGrab = new GameObject("[BuildingBlock] TouchHandGrab");
                touchHandGrab.transform.SetParent(parentWithMesh.transform, worldPositionStays: false);
                touchHandGrab.transform.localPosition = Vector3.zero;
                touchHandGrab.transform.localRotation = Quaternion.identity;
                touchHandGrab.transform.localScale = Vector3.one;

                touchHandGrab.AddComponent<BuildingBlock>();
                var grabbable = touchHandGrab.AddComponent<Grabbable>();
                grabbable.InjectOptionalTargetTransform(parentWithMesh.transform);

                var handGrabInteractable = touchHandGrab.AddComponent<TouchHandGrabInteractable>();
                handGrabInteractable.InjectOptionalPointableElement(grabbable);

                var itemCollider = parentWithMesh.GetComponent<Collider>();
                if (itemCollider != null)
                {
                    handGrabInteractable.InjectAllTouchHandGrabInteractable(itemCollider, new List<Collider> { itemCollider });
                }
            }
            else
            {
                var grabbable = touchHandGrab.GetComponent<Grabbable>();
                var rigidbody = parentWithMesh.GetComponent<Rigidbody>();
                if (grabbable != null && rigidbody != null)
                {
                    grabbable.InjectOptionalRigidbody(rigidbody);
                }
            }
        }

        #endregion
    }
}