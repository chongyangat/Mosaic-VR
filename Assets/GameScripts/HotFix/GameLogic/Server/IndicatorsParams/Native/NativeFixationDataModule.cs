using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace XNF.BaseFrame.EyeTracking
{
    /// <summary>
    /// 注视点新增事件委托（与 FixationDataModule 保持一致）
    /// </summary>
    public delegate void FixationPointAddedEventHandler(Vector2 fixationPoint, int stayDuration, int saccadeDuration);

    /// <summary>
    /// 基于 C++ 原生 DLL 的注视眼动数据模块, 通过 P/Invoke 调用 EyeTrackingNative.dll。
    /// 算法与 FixationDataModule 完全一致, 计算逻辑在原生层执行。
    /// 用法: 替换 FixationDataModule 即可, 接口完全兼容 ETDataModuleManager。
    /// </summary>
    public class NativeFixationDataModule : EyeTrackingDataModuleBase
    {
        // ==================== P/Invoke 声明 ====================

        private const string DLL_NAME = "EyeTrackingNative";

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void FixationPointCallback(IntPtr userData, float x, float y, int stayDuration, int saccadeDuration);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr fixation_create(float distanceThreshold, int gazeTimeMs);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void fixation_destroy(IntPtr handle);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void fixation_collect_data(IntPtr handle, float x, float y, float z, float deltaTimeMs);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void fixation_clear_data(IntPtr handle);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int fixation_get_count(IntPtr handle);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int fixation_get_avg_gaze_time(IntPtr handle);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int fixation_get_avg_saccade_time(IntPtr handle);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int fixation_get_avg_saccade_speed(IntPtr handle);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int fixation_get_max_saccade_speed(IntPtr handle);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int fixation_get_avg_amplitude(IntPtr handle);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int fixation_get_backward_gaze_count(IntPtr handle);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int fixation_get_gaze_points(IntPtr handle, float[] outX, float[] outY, int maxCount);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void fixation_set_callback(IntPtr handle, FixationPointCallback callback, IntPtr userData);

        // ==================== IL2CPP 安全的静态回调 ====================

        /// <summary>
        /// 静态回调委托 — 必须作为静态字段持有, 防止 GC 回收
        /// </summary>
        private static readonly FixationPointCallback s_staticCallback = new FixationPointCallback(StaticFixationCallback);

        /// <summary>
        /// IL2CPP 安全的静态回调入口, 通过 userData(GCHandle) 路由到实例
        /// </summary>
        [AOT.MonoPInvokeCallback(typeof(FixationPointCallback))]
        private static void StaticFixationCallback(IntPtr userData, float x, float y, int stayDuration, int saccadeDuration)
        {
            if (userData == IntPtr.Zero) return;
            GCHandle gcHandle = GCHandle.FromIntPtr(userData);
            if (gcHandle.Target is NativeFixationDataModule instance)
            {
                instance.OnFixationPointAdded?.Invoke(new Vector2(x, y), stayDuration, saccadeDuration);
            }
        }

        // ==================== 字段 ====================

        private IntPtr m_nativeHandle = IntPtr.Zero;
        private GCHandle m_gcHandle;

        private float m_distanceThreshold = 40f;
        private int m_gazeTime = 100;

        // ==================== 公共属性 ====================

        /// <summary>
        /// 注视点新增事件, 当检测到新的注视点时触发
        /// </summary>
        public event FixationPointAddedEventHandler OnFixationPointAdded;

        /// <summary>
        /// 注视点集合
        /// </summary>
        public List<Vector2> GazePoints
        {
            get
            {
                if (m_nativeHandle == IntPtr.Zero) return new List<Vector2>();
                int count = fixation_get_count(m_nativeHandle);
                if (count <= 0) return new List<Vector2>();
                float[] xs = new float[count];
                float[] ys = new float[count];
                fixation_get_gaze_points(m_nativeHandle, xs, ys, count);
                List<Vector2> points = new List<Vector2>(count);
                for (int i = 0; i < count; i++)
                    points.Add(new Vector2(xs[i], ys[i]));
                return points;
            }
        }

        // ==================== 生命周期 ====================

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="_objs">[0] 距离阈值(像素, float), [1] 注视时长阈值(毫秒, int)</param>
        public override void Initialize(params object[] _objs)
        {
            base.Initialize(_objs);

            ClearData();
            m_distanceThreshold = _objs.Length > 0 ? (float)_objs[0] : 40f;
            m_gazeTime = _objs.Length > 1 ? (int)_objs[1] : 100;

            // 创建原生实例
            if (m_nativeHandle != IntPtr.Zero)
            {
                fixation_destroy(m_nativeHandle);
            }
            m_nativeHandle = fixation_create(m_distanceThreshold, m_gazeTime);

            // 创建 GCHandle 用于回调路由, 并注册静态回调
            if (m_gcHandle.IsAllocated) m_gcHandle.Free();
            m_gcHandle = GCHandle.Alloc(this, GCHandleType.Normal);
            fixation_set_callback(m_nativeHandle, s_staticCallback, GCHandle.ToIntPtr(m_gcHandle));
        }

        /// <summary>
        /// 销毁
        /// </summary>
        public override void Clear()
        {
            base.Clear();
            ClearData();

            if (m_nativeHandle != IntPtr.Zero)
            {
                // 先取消回调, 防止销毁后回调被调用
                fixation_set_callback(m_nativeHandle, s_staticCallback, IntPtr.Zero);
                fixation_destroy(m_nativeHandle);
                m_nativeHandle = IntPtr.Zero;
            }
            if (m_gcHandle.IsAllocated)
            {
                m_gcHandle.Free();
            }
        }

        /// <summary>
        /// 数据重置
        /// </summary>
        public override void ClearData()
        {
            base.ClearData();
            if (m_nativeHandle != IntPtr.Zero)
            {
                fixation_clear_data(m_nativeHandle);
            }
        }

        /// <summary>
        /// 实时计算注视模块数据(需每一帧都调用)
        /// </summary>
        public override void CollectData(Vector3 _currentEyePosition)
        {
            base.CollectData(_currentEyePosition);

            if (m_nativeHandle == IntPtr.Zero) return;

            float deltaTimeMs = Time.deltaTime * 1000f;
            fixation_collect_data(m_nativeHandle,
                _currentEyePosition.x, _currentEyePosition.y, _currentEyePosition.z,
                deltaTimeMs);
        }

        // ==================== 结果查询 (与 FixationDataModule 接口一致) ====================

        /// <summary>
        /// 获取平均注视时间(毫秒ms)
        /// </summary>
        public int GetAvgGazeTime()
        {
            if (m_nativeHandle == IntPtr.Zero) return 0;
            return fixation_get_avg_gaze_time(m_nativeHandle);
        }

        /// <summary>
        /// 获取平均扫视时间(毫秒ms)
        /// </summary>
        public int GetAvgSacadeTime()
        {
            if (m_nativeHandle == IntPtr.Zero) return 0;
            return fixation_get_avg_saccade_time(m_nativeHandle);
        }

        /// <summary>
        /// 获取扫视速度的平均值(像素/毫秒ms)
        /// </summary>
        public int GetAvgSacadeSpeed()
        {
            if (m_nativeHandle == IntPtr.Zero) return 0;
            return fixation_get_avg_saccade_speed(m_nativeHandle);
        }

        /// <summary>
        /// 获取扫视速度的峰值(像素/毫秒ms)
        /// </summary>
        public int GetMaxSacadeSpeed()
        {
            if (m_nativeHandle == IntPtr.Zero) return 0;
            return fixation_get_max_saccade_speed(m_nativeHandle);
        }

        /// <summary>
        /// 获取扫视幅度(像素)
        /// </summary>
        public int GetSacadeAvgAmplitude()
        {
            if (m_nativeHandle == IntPtr.Zero) return 0;
            return fixation_get_avg_amplitude(m_nativeHandle);
        }

        /// <summary>
        /// 获取回视次数
        /// </summary>
        public int GetBackwardGazeCount()
        {
            if (m_nativeHandle == IntPtr.Zero) return 0;
            return fixation_get_backward_gaze_count(m_nativeHandle);
        }

        // ==================== 析构安全 ====================

        ~NativeFixationDataModule()
        {
            if (m_nativeHandle != IntPtr.Zero)
            {
                fixation_destroy(m_nativeHandle);
                m_nativeHandle = IntPtr.Zero;
            }
            if (m_gcHandle.IsAllocated)
            {
                m_gcHandle.Free();
            }
        }
    }
}
