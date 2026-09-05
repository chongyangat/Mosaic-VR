using UnityEngine;

namespace GameMain
{
    /// <summary>
    /// 跟随目标物体
    /// </summary>
    public class PositionFollower : MonoBehaviour
    {
        private const float DefaultFallbackHeight = 1f;
        private const float DefaultMinTargetHeight = 0.2f;
        private const float DefaultMaxTargetHeight = 2.5f;
        private const float DefaultMaxHorizontalDistance = 5f;

        [Header("跟随设置")]
        [Tooltip("要跟随的目标物体")]
        public Transform target; // 要跟随的目标物体

        [Tooltip("位置偏移量")]
        public Vector3 positionOffset = Vector3.zero; // 位置偏移量

        [Tooltip("跟随平滑度 (0=即时跟随, 1=完全不跟随)")]
        [Range(0f, 1f)] public float smoothFactor = 0.1f; // 跟随平滑度

        [Header("高级设置")]
        [Tooltip("使用固定Y轴高度")]
        public bool useFixedHeight = false; // 是否使用固定高度

        [Tooltip("固定高度值")]
        public float fixedHeight = 1.5f; // 固定高度值

        [Tooltip("启用高度平滑过渡")]
        public bool smoothHeightTransition = true; // 高度平滑过渡

        [Tooltip("是否应用初始偏移量")]
        public bool applyInitialOffset = true; // 是否应用初始偏移量

        private Vector3 _initialOffset; // 初始偏移量
        private Vector3 _targetPosition; // 目标位置
        private bool _isInitialized = false; // 是否已初始化

        void Start()
        {
            if (!_isInitialized)
            {
                Initialize();
            }
        }

        void LateUpdate()
        {
            if (target == null) return;

            if (!_isInitialized)
            {
                Initialize();
                if (!_isInitialized)
                {
                    return;
                }
            }

            // 计算目标位置（忽略目标旋转）
            _targetPosition = target.position + _initialOffset;

            // 应用位置偏移
            _targetPosition += positionOffset;

            // 处理固定高度
            if (useFixedHeight)
            {
                float targetY = fixedHeight;

                if (smoothHeightTransition)
                {
                    // 平滑过渡到固定高度
                    targetY = Mathf.Lerp(transform.position.y, fixedHeight, smoothFactor * 2);
                }

                _targetPosition.y = targetY;
            }

            // 应用平滑移动
            if (smoothFactor > 0)
            {
                transform.position = Vector3.Lerp(
                    transform.position,
                    _targetPosition,
                    Mathf.Clamp01(1f - smoothFactor)
                );
            }
            else
            {
                transform.position = _targetPosition;
            }
        }

        // 初始化跟随器
        public void Initialize()
        {
            if (target == null)
            {
                Debug.LogWarning("PositionFollower: 没有设置目标物体!");
                return;
            }

            // 计算初始偏移量（基于当前位置和目标位置）
            if (applyInitialOffset)
            {
                _initialOffset = transform.position - target.position;
            }

            // 如果使用固定高度，调整初始偏移的Y值
            if (useFixedHeight)
            {
                _initialOffset.y = 0;
            }

            _isInitialized = true;
        }

        // 设置新的跟随目标
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            _isInitialized = false;
            Initialize();
        }

        /// <summary>
        /// 立即把跟随点对齐到指定世界坐标，并重新计算与目标的偏移。
        /// 后续仍会继续跟随目标的位移。
        /// </summary>
        public void SnapTo(Vector3 worldPosition)
        {
            transform.position = worldPosition;

            if (target == null)
            {
                _isInitialized = false;
                Debug.LogWarning("PositionFollower: 对齐后无法继续跟随，因为没有设置目标物体!");
                return;
            }

            _initialOffset = worldPosition - target.position - positionOffset;
            _isInitialized = true;
        }

        /// <summary>
        /// 将跟随点的水平位置对齐到指定锚点，同时保留有效的目标高度。
        /// 目标位置异常时暂停跟随并使用锚点上方的安全高度，
        /// 避免相机落地或飞离场景。下一次对齐读到有效目标后恢复跟随。
        /// </summary>
        public void SnapHorizontalTo(
            Vector3 anchorPosition,
            float fallbackHeight = DefaultFallbackHeight,
            float minTargetHeight = DefaultMinTargetHeight,
            float maxTargetHeight = DefaultMaxTargetHeight,
            float maxHorizontalDistance = DefaultMaxHorizontalDistance)
        {
            Vector3 alignedPosition = anchorPosition;
            float targetHeight = target != null
                ? target.position.y + positionOffset.y - anchorPosition.y
                : float.NaN;
            Vector2 horizontalDelta = target != null
                ? new Vector2(
                    target.position.x - anchorPosition.x,
                    target.position.z - anchorPosition.z)
                : new Vector2(float.NaN, float.NaN);
            bool hasValidTarget = target != null
                && IsFinite(target.position)
                && IsFinite(targetHeight)
                && targetHeight >= minTargetHeight
                && targetHeight <= maxTargetHeight
                && horizontalDelta.sqrMagnitude <= maxHorizontalDistance * maxHorizontalDistance;

            if (hasValidTarget)
            {
                alignedPosition.y = anchorPosition.y + targetHeight;
                enabled = true;
                SnapTo(alignedPosition);
            }
            else
            {
                alignedPosition.y = anchorPosition.y + fallbackHeight;
                transform.position = alignedPosition;
                _isInitialized = false;
                enabled = false;
                Debug.LogWarning(
                    $"PositionFollower: 目标位置 {target?.position.ToString() ?? "None"} 无效，"
                    + $"暂停跟随并使用安全位置 {alignedPosition}。");
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        // 重置偏移量到当前位置
        public void ResetOffset()
        {
            if (target != null)
            {
                _initialOffset = transform.position - target.position;
                _isInitialized = true;
            }
        }
    }
}
