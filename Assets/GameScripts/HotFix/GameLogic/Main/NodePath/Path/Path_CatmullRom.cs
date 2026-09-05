using System;
using UnityEngine;
using System.Collections.Generic;

namespace VBSOED
{
    public partial class Path
    {
        Vector3[] controlPoints = null;

        float[] timesTable; // 每个点占用的长度百分比
        float[] lengthsTable; // 长度
        // 每个线段再细分的线段
        public int subdivisionsXSegment = 10;

        [NonSerialized]
        public float[] wpLengths; // 长度

#if UNITY_EDITOR
        [NonSerialized]
        public Vector3[] nonLinearDrawWps;
#endif
        // 长度
        static float[] ToWPLength(List<Vector3> points, Vector3[] controlPoints, bool isClosed)
        {
            if (!isClosed)
                return ToWPLength(points, controlPoints);

            int cnt = points.Count;
            try
            {
                points.Add(points[0]);
                return ToWPLength(points, controlPoints);
            }
            finally
            {
                points.RemoveAt(cnt);
            }
        }

        static readonly Vector3[] _PartialControlPs = new Vector3[2];
        static readonly Vector3[] _PartialWps = new Vector3[2];

        static float[] ToWPLength(IList<Vector3> wps, Vector3[] controlPoints, int subdivisions = 10)
        {
            int count = wps.Count;
            float[] wpLengths = new float[count];
            wpLengths[0] = 0;
            for (int i = 1; i < count; ++i)
            {
                // Create partial path
                _PartialControlPs[0] = i == 1 ? controlPoints[0] : wps[i - 2];
                _PartialWps[0] = wps[i - 1];
                _PartialWps[1] = wps[i];
                _PartialControlPs[1] = i == count - 1 ? controlPoints[1] : wps[i + 1];

                // Calculate length of partial path
                float partialLen = 0;
                float incr = 1f / subdivisions;
                Vector3 prevP = GetPoint2(0, _PartialWps, _PartialControlPs);
                for (int c = 1; c < subdivisions + 1; ++c)
                {
                    float perc = incr * c;
                    Vector3 currP = GetPoint2(perc, _PartialWps, _PartialControlPs);
                    partialLen += Vector3.Distance(currP, prevP);
                    prevP = currP;
                }
                wpLengths[i] = partialLen;
            }

            return wpLengths;
        }

        static Vector3 GetPoint2(float perc, Vector3[] wps, Vector3[] controlPoints)
        {
            float u = perc;
            ref Vector3 a = ref controlPoints[0];
            ref Vector3 b = ref wps[0];
            ref Vector3 c = ref wps[1];
            ref Vector3 d = ref controlPoints[1];

            return .5f * (
                (-a + 3f * b - 3f * c + d) * (u * u * u)
                + (2f * a - 5f * b + 4f * c - d) * (u * u)
                + (-a + c) * u
                + 2f * b
            );
        }

#if UNITY_EDITOR
        void RefreshNonLinearDrawWps()
        {
            int cnt = points.Count;
            int gizmosSubdivisions = cnt * subdivisionsXSegment;
            if (nonLinearDrawWps == null || nonLinearDrawWps.Length != gizmosSubdivisions + 1)
                nonLinearDrawWps = new Vector3[gizmosSubdivisions + 1];

            for (int i = 0; i <= gizmosSubdivisions; ++i)
            {
                float perc = i / (float)gizmosSubdivisions;
                Vector3 wp = GetPoint(perc);
                nonLinearDrawWps[i] = wp;
            }
        }
#endif

        void RefreshLength()
        {
            int subdivisions = subdivisionsXSegment * points.Count;
            percs = new float[subdivisions];

            {
                float pathLen = 0;
                float incr = 1f / subdivisions;
                timesTable = new float[subdivisions];
                lengthsTable = new float[subdivisions];
                Vector3 prevP = GetPoint(0, points, isClosed, controlPoints);
                for (int i = 1; i < subdivisions + 1; ++i)
                {
                    float perc = incr * i;
                    Vector3 currP = GetPoint(perc, points, isClosed, controlPoints);
                    pathLen += Vector3.Distance(currP, prevP);
                    prevP = currP;
                    timesTable[i - 1] = perc;
                    lengthsTable[i - 1] = pathLen;
                }

                // Assign
                length = pathLen;
            }
        }
    }
}