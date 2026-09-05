using UnityEngine;

namespace GameLogic
{
    public class WheelAniSim : MonoBehaviour
    {

        [Header("Rotation Settings")]
        [Tooltip("Rotation speed in degrees per second，>0 for clockwise")]
        public float rotationSpeed = 500; // 默认90度/秒

        [Tooltip("Rotation axis (normalized)")]
        public Vector3 rotationAxis = Vector3.forward; // 默认绕Z轴旋转

        [Tooltip("Should the rotation be smooth (unchecked for better performance)")]
        public bool smoothRotation = false;

        [Tooltip("自动开始旋转")]
        public bool m_IsAutoStartRotation = true;

        /// <summary>
        /// 是否正在旋转
        /// </summary>
        public bool IsRotating { get; private set; } = false;

        /// <summary>
        /// 启动车轮旋转动画
        /// </summary>
        public void StartRotation()
        {
            IsRotating = true;
        }

        /// <summary>
        /// 停止车轮旋转动画
        /// </summary>
        public void StopRotation()
        {
            IsRotating = false;
        }

        #region U3D

        void OnEnable()
        {
            if (m_IsAutoStartRotation)
            {
                StartRotation();
            }
        }

        /// <summary>
        /// 每帧更新车轮旋转动画
        /// </summary>
        /// <remarks>
        /// 根据rotationSpeed和smoothRotation设置，采用平滑或非平滑方式旋转车轮
        /// 平滑旋转使用Rotate方法，非平滑旋转直接修改localEulerAngles以获得更好性能
        /// </remarks>
        private void Update()
        {
            if (!IsRotating)
            {
                return;
            }
            // 计算这一帧应该旋转的角度
            float rotationAmount = rotationSpeed * Time.deltaTime;

            if (smoothRotation)
            {
                // 平滑旋转
                transform.Rotate(rotationAxis, -rotationAmount, Space.Self);
            }
            else
            {
                // 非平滑旋转，性能更好
                transform.localEulerAngles += rotationAxis * rotationAmount;
            }
        }
        #endregion
    }
}
