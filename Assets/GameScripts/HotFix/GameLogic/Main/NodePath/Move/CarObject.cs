using UnityEngine;
using System;
using GameLogic;

namespace VBSOED
{
    public class CarObject : MonoBehaviour, IMoveObject
    {
        #region 车轮

        [Header("Wheel Settings")]
        [Tooltip("车轮检查间隔，单位秒")]
        [SerializeField]
        private float m_WheelCheckInterval = 0.01f;

        /// <summary>
        /// 车轮列表
        /// </summary>
        private WheelAniSim[] m_Wheels;

        /// <summary>
        /// 是否在行车
        /// </summary>
        private bool m_IsMoving = false;

        /// <summary>
        /// 初始化车轮
        /// </summary>
        private void InitWheel()
        {
            m_Wheels = GetComponentsInChildren<WheelAniSim>();
        }

        /// <summary>
        /// 车轮开始旋转
        /// </summary>
        private void StartWheel()
        {
            foreach (var wheel in m_Wheels)
            {
                wheel.StartRotation();
            }
            //
            m_IsMoving = true;
        }

        /// <summary>
        /// 车轮停止旋转
        /// </summary>
        private void StopWheel()
        {
            foreach (var wheel in m_Wheels)
            {
                wheel.StopRotation();
            }
            //
            m_IsMoving = false;
        }

        /// <summary>
        /// 记录上一次位置
        /// </summary>
        private Vector3 m_LastPos;

        /// <summary>
        /// 上一次检查时间
        /// </summary>
        private float m_LastCheckTime;

        /// <summary>
        /// 检查停车
        /// </summary>
        private void CheckStop()
        {
            if (Time.time - m_LastCheckTime < m_WheelCheckInterval)
            {
                return;
            }
            // 检查两次位置是否有变化
            if (Vector3.Distance(transform.position, m_LastPos) < 0.01f)
            {
                if (m_IsMoving)
                {
                    // 停车
                    StopWheel();
                    // 输出日志
                    Debug.Log($"{gameObject.name}等红灯，停车");
                }
            }
            else
            {
                if (!m_IsMoving)
                {
                    // 开始移动
                    StartWheel();
                    // 输出日志
                    Debug.Log($"{gameObject.name}绿灯，开始移动");
                }

            }
            //
            m_LastPos = transform.position;
            m_LastCheckTime = Time.time;
        }

        #endregion

        public long id
        {
            get
            {
                if (_id < 0) _id = GetId();
                return _id;
            }
        }
        public Vector3 position { get => transform.position; set => transform.position = value; }
        public float speed { get => _speed; set => _speed = value; }
        public float angle
        {
            get { return Angle; }
            set
            {
                if (Angle == value)
                    return;

                SetAngle(value);
            }
        }
        Vector3 direction
        {
            set
            {
                //value.y += 90;
                root.localEulerAngles = value;
            }
        }

        private Transform root=> transform;

        public void SetSmoothRot(float angle)
        {
            rotSmooth = true;
            setAngle = angle;
        }
        public TrafficLightObject.LightType lightType => TrafficLightObject.LightType.Green;
        public int waitIndex { get; set; }

        [SerializeField] private float _speed;
        private long _id = -1;
        private MoveTo moveTo;

        #region U3D

        void Start()
        {
            InitWheel();
        }

        private void Update()
        {
            if (moveTo != null)
                moveTo.Update();
            UpdateRot();
            // 
            CheckStop();
        }

        private void OnDestroy()
        {
            Release();
        }

        #endregion

        public void Release()
        {
            if (moveTo != null)
                moveTo.Release();
            moveTo = null;
        }
        private float Angle;
        private float setAngle;
        float currentVelocity;
        float maxRotSpeed = 270f;
        bool rotSmooth = false;
        float rotSpeed = 70;
        private void SetAngle(float value)
        {
            Angle = value;
            direction = new Vector3(0, value, 0);
        }
        private void UpdateRot() 
        {
            if (rotSmooth == false) return;
            var currentAngle = NormalizeAngle(angle);
            var targetAngle = NormalizeAngle(setAngle);
            float smoothTime = Mathf.Abs(targetAngle - currentAngle) / rotSpeed;
            var value = Mathf.SmoothDampAngle(angle, setAngle, ref currentVelocity, smoothTime, maxRotSpeed);
            if (Mathf.Abs(Mathf.DeltaAngle(value, setAngle)) <= 0.01f)
            {
                // 结束了
                currentVelocity = 0f;
                value = setAngle;
                rotSmooth = false;
            }
            angle = value;
        }

        private float NormalizeAngle(float angle)
        {
            while (angle > 180f) angle -= 360f;
            while (angle < -180f) angle += 360f;
            return angle;
        }

        static long c_id;
        private long GetId() 
        {
            ++c_id;
            return c_id;
        }


        public void Move(NodeLine nodeLine, Action onend)
        {
            if (moveTo != null)
                moveTo.Release();
            if (moveTo == null) moveTo = new MoveTo();
            int count = nodeLine.pathList.Count;
            if (count == 0) return;
            int index = -1;
            void OnMoveEnd(bool r) 
            {
                ++index;
                if (index >= count) 
                {
                    onend?.Invoke();
                    return;
                }
                PathMoveData pmd = new PathMoveData();
                pmd.onEnd = OnMoveEnd;
                var tp = nodeLine.pathList[index];
                PointPath path = PointPath.GetOrCreate();
                path.Init(tp, true);
                pmd.SetPP(path);
                if (moveTo != null)
                    moveTo.Release();
                moveTo.Reset(this, pmd);
            }
            {
                var tp = nodeLine.pathList[0];
                if (tp.pointCount > 2) 
                {
                    position = tp.localPointToWorld(tp.path.points[0]);
                    var dir = tp.localPointToWorld(tp.path.points[1]) - position;
                    float angle = MoveTo.ObjDir2Angle(dir.normalized);
                    this.angle = angle;
                }
            }
            OnMoveEnd(true);
            //PathMoveData pmd = new PathMoveData();
            //pmd.onEnd = OnMoveEnd;
            //var tp = nodeLine.pathList[index];
            //PointPath path = PointPath.GetOrCreate();
            //path.Init(tp, true);
            //pmd.SetPP(path);
            //if (moveTo != null)
            //    moveTo.Release();
            //moveTo.Reset(this, pmd);
        }
    }
}
