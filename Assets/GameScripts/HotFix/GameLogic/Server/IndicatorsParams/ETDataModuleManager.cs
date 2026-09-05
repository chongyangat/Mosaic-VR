using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace XNF.BaseFrame.EyeTracking
{
    public class ETDataModuleManager
    {
        private static ETDataModuleManager _instance;
        public static ETDataModuleManager Instance => _instance ??= new ETDataModuleManager();
        private Dictionary<Type, IEyeTrackingDataModule> m_activeModules;
        private bool m_isInitialized;

        public event Action<IEyeTrackingDataModule> onModuleInitialized;
        public event Action<IEyeTrackingDataModule> onModuleCleared;

        private ETDataModuleManager()
        {
            m_activeModules = new Dictionary<Type, IEyeTrackingDataModule>();
            m_isInitialized = false;
        }

        /// <summary>
        /// 初始化默认数据模块
        /// </summary>
        public void Initialize()
        {
            if (m_isInitialized) return;

            // 可以在这里初始化默认模块
            // InitializeModule<NativeFixationDataModule>();
            // InitializeModule<SaccadeDataModule>();

            m_isInitialized = true;
            //Debug.Log("ETDataModuleManager initialized");
        }

        /// <summary>
        /// 销毁
        /// </summary>
        public void Clear()
        {
            m_isInitialized = false;

            foreach (var module in m_activeModules.Values)
            {
                module.Clear();
            }
        }

        /// <summary>
        /// 初始化指定数据模块
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="initParams"></param>
        /// <returns></returns>
        public T InitializeModule<T>(params object[] initParams) where T : IEyeTrackingDataModule, new()
        {
            if (m_activeModules.ContainsKey(typeof(T)))
            {
                Debug.LogWarning($"Module {typeof(T).Name} is already initialized");
                return (T)m_activeModules[typeof(T)];
            }

            T module = new T();
            module.Initialize(initParams);
            m_activeModules[typeof(T)] = module;

            onModuleInitialized?.Invoke(module);
            return module;
        }

        /// <summary>
        /// 清理指定数据模块
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void ClearModule<T>() where T : IEyeTrackingDataModule
        {
            Type moduleType = typeof(T);
            if (m_activeModules.TryGetValue(moduleType, out IEyeTrackingDataModule module))
            {
                module.Clear();
                m_activeModules.Remove(moduleType);
                onModuleCleared?.Invoke(module);
            }
        }

        /// <summary>
        /// 清理所有数据模块
        /// </summary>
        public void ClearAllModules()
        {
            foreach (var module in m_activeModules.Values)
            {
                module.Clear();
            }
            m_activeModules.Clear();
            m_isInitialized = false;

            Debug.Log("All ET data modules cleared");
        }

        /// <summary>
        /// 获取指定数据模块
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetModule<T>() where T : IEyeTrackingDataModule
        {
            if (m_activeModules.TryGetValue(typeof(T), out IEyeTrackingDataModule module))
            {
                return (T)module;
            }
            return default(T);
        }

        /// <summary>
        /// 收集所有数据模块的数据
        /// </summary>
        /// <param name="currentEyePosition"></param>
        public void CollectDataFromAllModules(Vector3 currentEyePosition)
        {
            foreach (var module in m_activeModules.Values)
            {
                if (module.IsInitialized)
                {
                    module.CollectData(currentEyePosition);
                }
            }
        }

        /// <summary>
        /// 收集某个数据模块的数据
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="currentEyePosition"></param>
        public void CollectData<T>(Vector3 currentEyePosition) where T : IEyeTrackingDataModule
        {
            if (m_activeModules.ContainsKey(typeof(T)))
            {
                m_activeModules[typeof(T)].CollectData(currentEyePosition);
            }
        }

        /// <summary>
        /// 清理某个数据模块的数据
        /// </summary>
        public void ClearData<T>() where T : IEyeTrackingDataModule
        {
            if (m_activeModules.ContainsKey(typeof(T)))
            {
                m_activeModules[typeof(T)].ClearData();
            }
        }

        /// <summary>
        /// 清理所有数据模块的数据
        /// </summary>
        public void ClearDataFromAllModules()
        {
            foreach (var module in m_activeModules.Values)
            {
                module.ClearData();
            }
        }

        /// <summary>
        /// 查找指定数据模块是否存在
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public bool IsModuleActive<T>() where T : IEyeTrackingDataModule
        {
            return m_activeModules.ContainsKey(typeof(T));
        }

        /// <summary>
        /// 获取所有存在的数据模块
        /// </summary>
        /// <returns></returns>
        public IEnumerable<IEyeTrackingDataModule> GetAllActiveModules()
        {
            return m_activeModules.Values;
        }
    }
}
