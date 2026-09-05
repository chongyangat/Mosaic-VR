using UnityEngine;
using System.Collections.Generic;
using UnityGameFramework.Runtime;
using VBSOED;

namespace VBSOED
{
    public class PointPath
    {
        public PointPath()
        {

        }

        public static PointPath GetOrCreate()
        {
            var pp = new PointPath();
            return pp;
        }

        public string debugInfo
        {
            get
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.Append($"Count:{points.Count} total:{totalDis}");
                foreach (var ator in points)
                {
                    sb.Append(ator);
                    sb.Append(",");
                }

                return sb.ToString();
            }
        }

        public List<Vector3> points = new List<Vector3>();
        public List<float> lengths = new List<float>(); // 长度
        public TweenPath tp;

        public void Init(TweenPath tp, bool isWorld = false)
        {
            this.tp = tp;
            var points = tp.path.points;
            int cnt = points.Count;
            if (isWorld)
            {
                Matrix4x4 matrix = tp.transform.localToWorldMatrix;
                for (int i = 0; i < cnt; ++i)
                    this.points.Add(matrix.MultiplyPoint(points[i]));
            }
            else
            {
                for (int i = 0; i < cnt; ++i)
                    this.points.Add(points[i]);
            }

            ResetLength();
        }
        int GetIndexByLength(float distance)
        {
            float totalDis = 0f;
            int count = lengths.Count;
            for (int i = 0; i < count; ++i)
            {
                totalDis += lengths[i];
                if (totalDis >= distance)
                    return i;
            }

            return count;
        }

        public float totalDis = 0f;

        public Vector3 GetLastPoint()
        {
            return points[points.Count - 1];
        }

        public void ResetLength()
        {
            lengths.Clear();
            totalDis = 0f;

            int cnt = points.Count;
            for (int i = 1; i < cnt; ++i)
            {
                var dis = (points[i] - points[i - 1]).magnitude;
                totalDis += dis;
                lengths.Add(dis);
            }
        }

        public bool IsWaiting()
        {
            if (tp.trafficLight != null && tp.trafficLight.CanCarThrough() == false)
                return true;
            return false;
        }

        public float GetAvailableTotalDis() 
        {
            if (tp.trafficLight == null || tp.trafficLight.CanCarThrough())
                return totalDis;
            return totalDis - tp.trafficLight.GetWaitDis();
        }

        public float CheckDis(float dis)
        {
            if (tp.trafficLight == null || tp.trafficLight.CanCarThrough())
                return dis;
            var ttd = totalDis - tp.trafficLight.GetWaitDis();
            dis = Mathf.Min(dis, ttd);
            return dis;
        }

        public void OnWait(long id)
        {
            if (tp.trafficLight == null) return;
            tp.trafficLight.AddWait(id);
        }
        public void OnLeave(long id)
        {
            if (tp.trafficLight == null) return;
            tp.trafficLight.RemoveWait(id);
        }

        float GetLength(int endIndex)
        {
            float total = 0f;
            if (endIndex >= lengths.Count)
                return totalDis;

            for (int i = 0; i < endIndex; ++i)
                total += lengths[i];
            return total;
        }

        public void Release()
        {
            totalDis = 0f;
            points.Clear();
            lengths.Clear();
        }

        public void Reverse()
        {
            points.Reverse();
            lengths.Reverse();
        }

        // 得到点
        public bool GetPoint(float distance, out int startIndex, out Vector3 point, out Vector3 dir)
        {
            int count = lengths.Count;
            startIndex = -1;
            float totalDis = 0f;
            for (int i = 0; i < count; ++i)
            {
                totalDis += lengths[i];
                if (totalDis >= distance)
                {
                    totalDis -= lengths[i];
                    startIndex = i;
                    break;
                }
            }

            if (startIndex <= -1)
            {
                int pc = points.Count;

                if (pc >= 2)
                {
                    point = points[pc - 1];
                    dir = (point - points[pc - 2]).normalized;
                }
                else
                {
                    if (pc <= 0)
                    {
                        Log.Error($"PointPath points count {pc}");
                        point = Vector3.zero;
                    }
                    else
                    {
                        point = points[pc - 1];
                    }
                    dir = Vector3.zero;
                }
                return true;
            }
            else
                point = Vector3.zero;

            startIndex += 1;

            var sp = points[startIndex - 1];
            var tp = points[startIndex];
            dir = (tp - sp).normalized;

            distance = distance - totalDis;
            point.x = sp.x + dir.x * distance;
            point.z = sp.z + dir.z * distance;
            point.y = sp.y + dir.y * distance;

            return false;
        }
    }
}
