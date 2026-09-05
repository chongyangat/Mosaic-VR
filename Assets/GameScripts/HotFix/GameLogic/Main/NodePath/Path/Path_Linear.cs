using UnityEngine;
using System.Collections.Generic;

namespace VBSOED
{
    public partial class Path
    {
        float[] percs = null; // 每个点所占的百分比

        void ResetPercs()
        {
            if (!isClosed)
            {
#if UNITY_EDITOR
                wpLengths =
#endif
                LinearPoint(points, ref percs, out length);
                return;
            }

            int cnt = points.Count;
            try
            {
                points.Add(points[0]);

#if UNITY_EDITOR
                wpLengths =
#endif
                LinearPoint(points, ref percs, out length);
            }
            finally
            {
                points.RemoveAt(cnt);
            }
        }

        // 线性点
        // percs，每个点占总长度的百分比
        // length，总长度
        // 返回每个线段之间的长度
        static float[] LinearPoint(IList<Vector3> points, ref float[] percs, out float length)
        {
            int cnt = points.Count;
            if (cnt <= 1)
            {
                length = 0;
                percs = null;
                return null;
            }

#if UNITY_EDITOR
            float[] wpLengths = new float[cnt];
            wpLengths[0] = 0;
#endif
            length = 0f;
            float distance;
            percs = new float[cnt - 2];
            for (int i = 1; i < cnt; ++i)
            {
                distance = Vector3.Distance(points[i], points[i - 1]);
#if UNITY_EDITOR
                wpLengths[i] = distance;
#endif
                if (i != cnt - 1)
                    percs[i - 1] = distance;
                length += distance;
            }

            cnt -= 2;
            distance = 0f;
            for (int i = 0; i < cnt; ++i)
            {
                distance += percs[i];
                percs[i] = distance / length;
            }

#if UNITY_EDITOR
            return wpLengths;
#else
            return null;
#endif
        }
    }
}