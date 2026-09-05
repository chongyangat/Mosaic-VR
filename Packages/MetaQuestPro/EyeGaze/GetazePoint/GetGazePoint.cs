using UnityEngine;
using UnityEngine.Events;

namespace MetaQuestProGazeGetPoint
{
    /// <summary>
    /// 凝视点获取（TODO 临时：已直接修改）
    /// </summary>
    public class GetGazePoint : MonoBehaviour
    {
        [Header("Gaze Config")]
        [SerializeField] 
        private LayerMask m_GazeTargetsMask = ~0;

        [SerializeField]
        private float m_GazeMaxDistance = 100;

        [Header("位置更新间隔")]
        public float positionUpdateRate = 0.5f;
        private float _positionUpdateRate = 0f;
        private bool _positionUpdateCooling = false;

        //俩眼球的视线方向
        [SerializeField] private Transform leftEyeViewPoint;
        [SerializeField] private Transform rightEyeviewPoint;
        //俩眼球中心点
        [SerializeField] private Transform eyesCenter;
        private Vector3 viewCenter;
        private Vector3 gazePoint;

        /// <summary>
        /// 发送凝视点数据（左眼、右眼、双眼）
        /// </summary>
        public UnityEvent<Vector3?, Vector3?, Vector3?> onGazePointDateSend;

        ///// <summary>
        ///// 
        ///// </summary>
        //public UnityEvent<Vector3> onGazePointUpdate;

        // Update is called once per frame
        void Update()
        {
            PointUpdateInterval();
            UpdateGazePoint();
        }

        /// <summary>
        /// 调更新时间间隔
        /// </summary>
        /// <param name="value"></param>
        public void SetPointUpdateRate(string value)
        {
            try
            {
                float v = float.Parse(value);
                if (v <= 0)
                    v = 0.1f;
                positionUpdateRate = v;
            }
            catch (System.Exception)
            {
                throw;
            }
        }

        private void UpdateGazePoint()
        {
            //视线中心点
            viewCenter = CalculateCenterPoint(leftEyeViewPoint.position, rightEyeviewPoint.position);

            //算凝视点方向向量
            Vector3 gazeDir = viewCenter - eyesCenter.position;
            gazeDir = gazeDir.normalized;

            //发条射线获取凝视点
            // 左眼
            Vector3? leftGazePoint = GetGazePointByRay(gazeDir, leftEyeViewPoint.position);
            // 右眼
            Vector3? rightGazePoint = GetGazePointByRay(gazeDir, rightEyeviewPoint.position);
            // 双眼
            Vector3? doubleEyesGazePoint = GetGazePointByRay(gazeDir, eyesCenter.position);
            // 只要有一个凝视点就发送
            if (leftGazePoint != null || rightGazePoint != null || doubleEyesGazePoint != null)
            {
                if (!_positionUpdateCooling)
                {
                    //更新凝视点
                    //更新时间间隔
                    _positionUpdateRate = positionUpdateRate;
                    _positionUpdateCooling = true;
                    onGazePointDateSend?.Invoke(leftGazePoint, rightGazePoint, doubleEyesGazePoint);
                }

                //onGazePointUpdate?.Invoke(gazePoint);
            }
        }

        /// <summary>
        /// 根据射线获取凝视点
        /// </summary>
        /// <param name="gazeDir"></param>
        /// <param name="eyePos"></param>
        /// <returns></returns>
        private Vector3? GetGazePointByRay(Vector3 gazeDir, Vector3 eyePos)
        {
            if (Physics.Raycast(eyePos, gazeDir, out RaycastHit hit, m_GazeMaxDistance, m_GazeTargetsMask))
            {
                // 为方便显示，凝视点向摄像机方向靠近一定距离，不要跟物体贴合，保证能够看得见
                gazePoint = hit.point - gazeDir * 0.1f;
                //gazePoint = hit.point;

                return gazePoint;
            }
            // 绘制调试用射线
            Debug.DrawRay(eyePos, gazeDir * m_GazeMaxDistance, Color.red);

            return null;
        }

        /// <summary>
        /// 更新时间间隔
        /// </summary>
        private void PointUpdateInterval()
        {
            if (_positionUpdateRate > 0)
            {
                _positionUpdateRate -= Time.deltaTime;
                if (_positionUpdateRate <= 0)
                {
                    _positionUpdateCooling = false;
                }
            }
        }

        //获取两点的中心点
        Vector3 CalculateCenterPoint(Vector3 point1, Vector3 point2)
        {
            return (point1 + point2) * 0.5f;
        }
    }

}
