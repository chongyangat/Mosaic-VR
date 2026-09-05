using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace VBSOED
{
    public class MoveTo
    {
        public PointPath path { get { return pmd.path; } }
        IMoveObject owner;
        PathMoveData pmd;

        public MoveTo() { }

        public void Release()
        {
            if (pmd != null)
            {
                pmd.Release();
                pmd = null;
            }
            owner = null;
            isCanSetPosition = null;
            currentIndex = 0;
            currentDis = 0f;
        }

        public void Reset(IMoveObject owner, PathMoveData pmd)
        {
            this.owner = owner;
            if(this.pmd != null)
                this.pmd.Release();
            this.pmd = pmd;
            currentIndex = 0;
            currentDis = 0f;
        }

        public void ResetPath(PointPath pp)
        {
            currentIndex = 0;
            currentDis = 0f;
            pmd.SetPP(pp);
        }

        public int currentIndex = 0;
        public float currentDis = 0f;

        public bool isArrivalEnd
        {
            get { return currentDis >= path.totalDis ? true : false; }
        }

        public static int s_index;

        public static float angleOffset = 1f;

        public static void LookAtPoint(IMoveObject obj, Vector3 point)
        {
            Rot(obj, (point - obj.position).normalized);
        }

        public static void Rot(IMoveObject obj, Vector3 dir)
        {
            float angle = Angle(dir);
            if ((angle >= 90 && angle < 180) || (angle >= 270))
                angle += angleOffset;
            else
                angle -= angleOffset;
            obj.SetSmoothRot(angle);
        }

        public static float ObjDir2Angle(Vector3 dir)
        {
            float angle = Angle(dir);
            if ((angle >= 90 && angle < 180) || (angle >= 270))
                angle += angleOffset;
            else
                angle -= angleOffset;
            return angle;
        }

        public static float Angle(Vector3 dir)
        {
            if (dir.x >= 0)
            {
                if (dir.z >= 0)
                {
                    return 90f - Mathf.Rad2Deg * Mathf.Acos(dir.x);
                }
                else
                {
                    return 90f + Mathf.Rad2Deg * Mathf.Acos(dir.x);
                }
            }
            else
            {
                if (dir.z >= 0)
                {
                    return 270f + Mathf.Rad2Deg * Mathf.Acos(-dir.x);
                }
                else
                {
                    return 270 - Mathf.Rad2Deg * Mathf.Acos(-dir.x);
                }
            }
        }

        public class SetPositionData
        {
            public Vector3 position;
            public Vector3 dir;
        }

        static SetPositionData sSetData = new SetPositionData();

        // 是否可设置此位置
        public System.Func<SetPositionData, bool> isCanSetPosition = null;

        private void Move(float dis)
        {
            dis = this.path.CheckDis(dis);
            var path = this.path;
            var ttd = path.GetAvailableTotalDis();
            if (path.IsWaiting()) 
            {
                if (IsArrive(currentDis, ttd)) 
                {
                    path.OnWait(owner.id);
                    return;
                }
            }
            path.OnLeave(owner.id);
            bool isEnd = path.GetPoint(dis, out s_index, out sSetData.position, out sSetData.dir);
            //if (isEnd == false) 
            //{
            //    if (dis >= ttd)
            //        isEnd = true;
            //}
            if (isCanSetPosition != null && !isCanSetPosition(sSetData))
            {
                return; // 位置非法
            }

            if (ttd <= 0)
            {
                return;// 距离非法
            }

            if (dis > ttd)
                dis = ttd;

            currentDis = dis;
            owner.position = sSetData.position;
            Rot(owner, sSetData.dir);
            if (isEnd)
            {
                if (pmd.onEnd != null)
                    pmd.onEnd(true);
            }
            else
            {
                if (currentIndex != s_index)
                {
                    currentIndex = s_index;
                    if (pmd.OnArrivalPoint != null)
                    {
                        pmd.OnArrivalPoint(path.points, currentIndex);
                    }
                }
            }
        }

        public static void RotTo(IMoveObject obj, Vector3 targetPos)
        {
            //this.targetPos = targetPos;
            var dir = targetPos - obj.position;
            dir.Normalize();
            RotTo(obj, Angle(dir));
        }

        public static void RotTo(IMoveObject obj, float angle)
        {
            obj.SetSmoothRot(angle);
        }

        public void Update()
        {
            float dt = Time.deltaTime;
            Update(dt);
        }

        public void Update(float deltaTime)
        {
            float maxDic = owner.speed * deltaTime;

            Move(currentDis + maxDic);
        }

        public bool SimulationUpdate(ref float deltaTime)
        {
            float speed = owner.speed;
            Vector3 current = owner.position;
            var totalDis = path.totalDis - currentDis;
            float tt = totalDis / speed; // 可以移动的距离
            if (deltaTime >= tt)
            {
                // 到时间了
                deltaTime -= tt;
                return true;
            }
            else
            {
                // 还没到时间
                Move(currentDis + deltaTime * owner.speed);
                deltaTime = 0;
                return false;
            }
        }

        public bool Update(ref float deltaTime)
        {
            float speed = owner.speed;
            Vector3 current = owner.position;
            var totalDis = path.totalDis - currentDis;
            float tt = totalDis / speed; // 可以移动的距离
            if (deltaTime >= tt)
            {
                // 到时间了
                Move(path.totalDis);
                deltaTime -= tt;
                return true;
            }
            else
            {
                // 还没到时间
                Move(currentDis + deltaTime * owner.speed);
                deltaTime = 0;
                return false;
            }
        }

        private bool IsArrive(float dis, float target) 
        {
            if (Mathf.Approximately(dis, target) || dis >= target)
                return true;
            return false;
        }
    }



    public class PathMoveData
    {
        public PathMoveData()
        {

        }

        public PointPath path { get; private set; }
        public System.Action<bool> onEnd; // 结束路径移动时的回调
        public System.Action<List<Vector3>, int> OnArrivalPoint; // 到达点的回调
        public object customData;

        public void SetPP(PointPath path)
        {
            this.path = path;
        }

        public void Release()
        {
            if (path != null)
            {
                path.Release();
                path = null;
            }
            onEnd = null;
            OnArrivalPoint = null;
            customData = null;
        }
    }
}
