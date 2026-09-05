using System;
using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VBSOED
{
    [System.Serializable]
    public partial class Path
    {
        public List<Vector3> points; // 所有的点
        public PathType pathType; // 路径点类型
        public bool isClosed; // 是否闭合的点
        float length;
        public float Length { get { return length; } }

        public Path Clone()
        {
            return new Path()
            {
                points = new List<Vector3>(points),
                pathType = pathType,
                isClosed = isClosed,
            };
        }

        void ResetControlPoint()
        {
            int cnt = points.Count;
            controlPoints = new Vector3[2];
            if (isClosed)
            {
                controlPoints[0] = points[cnt - 2];
                controlPoints[1] = points[1];
            }
            else
            {
                controlPoints[0] = points[1];
                Vector3 lastP = points[cnt - 1];
                Vector3 diffV = lastP - points[cnt - 2];
                controlPoints[1] = lastP + diffV;
            }
        }

        public float ConvertToConstantPathPerc(float perc)
        {
            if (pathType == PathType.Linear) 
                return perc;

            if (perc >= 1) return 1f;
            if (perc <= 0) return 0f;

            if (length <= 0) 
                return perc; // Fix bug in case of 0-length path

            float tLen = length * perc;
            // Find point in time/length table
            float t0 = 0, l0 = 0, t1 = 0, l1 = 0;
            int count = lengthsTable.Length;
            for (int i = 0; i < count; ++i)
            {
                if (lengthsTable[i] > tLen)
                {
                    t1 = timesTable[i];
                    l1 = lengthsTable[i];
                    if (i > 0) l0 = lengthsTable[i - 1];
                    break;
                }
                t0 = timesTable[i];
            }

            // Find correct time
            perc = t0 + ((tLen - l0) / (l1 - l0)) * (t1 - t0);
            return perc;
        }

        public Vector3 GetPoint(float perc)
        {
            switch (pathType)
            {
                case PathType.Linear:
                    {
                        if (perc <= 0)
                            return points[0];
                        else if (perc >= 1f)
                        {
                            if (isClosed)
                                return points[0];

                            return points[points.Count - 1];
                        }

                        int cnt = points.Count;
                        if (cnt <= 2 && !isClosed)
                            return Vector3.Lerp(points[0], points[1], perc);

                        if (!isClosed)
                            return GetPoint(points, percs, perc, length);

                        try
                        {
                            points.Add(points[0]);
                            return GetPoint(points, percs, perc, length);
                        }
                        finally
                        {
                            points.RemoveAt(cnt);
                        }
                    }
                case PathType.CatmullRom:
                    {
                        if (points.Count <= 1)
                            return Vector3.zero;

                        return GetPoint(perc, points, isClosed, controlPoints);
                    }
            }

            return Vector3.zero;
        }

        public static Vector3 GetPoint(IList<Vector3> points, float[] percs, float perc, float length)
        {
            int cnt = percs.Length;
            int startIndex = cnt;
            float startPerc = percs[cnt - 1];
            for (int i = 0; i < cnt; ++i)
            {
                if (percs[i] > perc)
                {
                    startIndex = i;
                    if (i == 0)
                        startPerc = 0;
                    else
                        startPerc = percs[i - 1];
                    break;
                }
            }

            Vector3 wp0 = points[startIndex];
            Vector3 wp1 = points[startIndex + 1];

            return wp0 + Vector3.ClampMagnitude(wp1 - wp0, length * (perc - startPerc));
        }

        public static Vector3 GetPoint(float perc, IList<Vector3> points, bool isClosed, Vector3[] controlPoints)
        {
            if (!isClosed)
                return GetPoint(perc, points, controlPoints);

            int cnt = points.Count;
            try
            {
                points.Add(points[0]);
                return GetPoint(perc, points, controlPoints);
            }
            finally
            {
                points.RemoveAt(cnt);
            }
        }

        public static Vector3 GetPoint(float perc, IList<Vector3> wps, Vector3[] controlPoints)
        {
            int cnt = wps.Count;
            int numSections = cnt - 1; // Considering also control points
            int tSec = (int)Math.Floor(perc * numSections);
            int currPt = numSections - 1;
            if (currPt > tSec) currPt = tSec;
            float u = perc * numSections - currPt;

            Vector3 a = currPt == 0 ? controlPoints[0] : wps[currPt - 1];
            Vector3 b = wps[currPt];
            Vector3 c = wps[currPt + 1];
            Vector3 d = currPt + 2 > cnt - 1 ? controlPoints[1] : wps[currPt + 2];

            return .5f * (
                (-a + 3f * b - 3f * c + d) * (u * u * u)
                + (2f * a - 5f * b + 4f * c - d) * (u * u)
                + (-a + c) * u
                + 2f * b
            );
        }

        void ResetWPLengths()
        {
            if (pathType != PathType.CatmullRom)
                return;

            wpLengths = ToWPLength(points, controlPoints, isClosed);
        }

        public void Check()
        {
            if (pathType != PathType.CatmullRom)
            {
                if (controlPoints == null || controlPoints.Length == 0)
                    SetDirty();
            }
            else
            {
                if (percs == null || percs.Length == 0)
                    SetDirty();
            }
        }

        public void GetAllPoints(List<Vector3> points)
        {
            Check();
            if (pathType == PathType.Linear)
            {
                int cnt = this.points.Count;
                for (int i = 0; i < cnt; ++i)
                    points.Add(this.points[i]);
            }
            else
            {
                int cnt = this.points.Count;
                int gizmosSubdivisions = cnt * subdivisionsXSegment;
                for (int i = 0; i <= gizmosSubdivisions; ++i)
                {
                    float perc = i / (float)gizmosSubdivisions;
                    Vector3 wp = GetPoint(perc);
                    points.Add(wp);
                }
            }
        }

        public void SetDirty()
        {
            RefreshCache();
        }

        // 刷新缓存的数据
        void RefreshCache()
        {
            if (points == null)
                points = new List<Vector3>();

            if (pathType == PathType.Linear)
                ResetPercs();
            else
            {
                ResetControlPoint();
#if UNITY_EDITOR
                RefreshNonLinearDrawWps();
#endif
                ResetWPLengths();
                RefreshLength();
            }
        }

#if UNITY_EDITOR
        public static void InspectorGUI(SerializedProperty path_sp)
        {
            SerializedProperty pathType_sp = path_sp.FindPropertyRelative("pathType");
            EditorGUILayout.PropertyField(pathType_sp);

            SerializedProperty isClosed_sp = path_sp.FindPropertyRelative("isClosed");
            EditorGUILayout.PropertyField(isClosed_sp);

            if (pathType_sp.intValue == (int)PathType.CatmullRom)
            {
                SerializedProperty subdivisionsXSegment_sp = path_sp.FindPropertyRelative("subdivisionsXSegment");
                ++EditorGUI.indentLevel;
                EditorGUILayout.PropertyField(subdivisionsXSegment_sp);
                --EditorGUI.indentLevel;
                if (subdivisionsXSegment_sp.intValue < 2)
                    subdivisionsXSegment_sp.intValue = 2;
            }
        }
#endif
    }
}