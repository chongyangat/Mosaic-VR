using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace VBSOED
{
    public enum HandlesDrawMode
    {
        Orthographic = 0,
        Perspective = 1
    }

    public enum HandlesType
    {
        Free = 0,
        Full = 1
    }

    [ExecuteAlways]
    public class TweenPath : MonoBehaviour
    {
        public Path path; // 路径点
        public TrafficLightObject trafficLight;

        public int pointCount { get { return path.points.Count; } }

        public bool isClosedPath { get { return path.isClosed; } }

        public List<Vector3> wps { get { return path.points; } }

        public PathType pathType { get { return path.pathType; } }

        public void SaveToWorldPosition()
        {
            var wps = this.wps;
            int cnt = wps.Count;
            for (int i = 0; i < cnt; ++i)
                wps[i] = transform.TransformPoint(wps[i]);
            transform.localPosition = Vector3.zero;
            transform.localEulerAngles = Vector3.zero;
            transform.localScale = Vector3.one;
        }

        public Vector3 localPointToWorld(Vector3 local)
        {
            return transform.TransformPoint(local);
        }

        public Vector3 worldPointToLocal(Vector3 worldPos)
        {
            return transform.InverseTransformPoint(worldPos);
        }

        public List<Vector3> toWorldPoint(bool isClosed)
        {
            var points = new List<Vector3>(path.points);
            if (isClosed)
                points.Add(points[0]);

            Matrix4x4 localToWorld = transform.localToWorldMatrix;
            int cnt = points.Count;
            for (int i = 0; i < cnt; ++i)
                points[i] = localToWorld.MultiplyPoint(points[i]);

            return points;
        }

        public void toWorldPoint(bool isClosed, List<Vector2> points)
        {
            int index = points.Count;
            var wps = this.wps;
            int cnt = wps.Count;
            var t = transform;
            for (int i = 0; i < cnt; ++i)
                points.Add(t.TransformPoint(wps[i]));

            if (isClosed)
                points.Add(points[index]);
        }

        public void toWorldPoint(bool isClosed, List<Vector3> points)
        {
            int index = points.Count;
            var wps = this.wps;
            int cnt = wps.Count;
            var t = transform;
            for (int i = 0; i < cnt; ++i)
                points.Add(t.TransformPoint(wps[i]));

            if (isClosed)
                points.Add(points[index]);
        }

        public Vector3 GetPoint(float perc, bool lengthPerc)
        {
            path.Check();

            if (lengthPerc && pathType == PathType.CatmullRom)
                perc = path.ConvertToConstantPathPerc(perc);

            return path.GetPoint(perc);
        }

#if UNITY_EDITOR
        public bool livePreview = true;
        public bool onDrawGizmos = false;

        public bool wpsDropdown { get; set; }

        public Vector3 lastSrcPosition { get; set; }
        public Quaternion lastSrcRotation { get; set; }
        public bool relative { get; set; }
        public HandlesDrawMode handlesDrawMode { get; set; }
        public float perspectiveHandleSize { get; set; } = 0.5f;

        public HandlesType handlesType { get; set; }

        public bool showIndexes;

        public bool showWpLength;

        [SerializeField]
        Color gizmoColor = Color.white;

        public Color pathColor = Color.white;

        public void SetDirty()
        {
            path.SetDirty();
        }

        static void DrawLines(IList<Vector3> wps, System.Func<Vector3, Vector3> func)
        {
            int cnt = wps.Count;
            if (cnt == 0)
                return;

            if (func == null)
            {
                Vector3 first = wps[0];
                for (int i = 1; i < cnt; ++i)
                {
                    var b = wps[i];
                    Gizmos.DrawLine(first, b);
                    first = b;
                }
            }
            else
            {
                Vector3 first = func(wps[0]);
                for (int i = 1; i < cnt; ++i)
                {
                    var b = func(wps[i]);
                    Gizmos.DrawLine(first, b);
                    first = b;
                }
            }
        }

        private void OnDrawGizmos()
        {
            if (!onDrawGizmos)
                return;

            if (UnityEditor.Selection.activeGameObject == gameObject)
                return;

            if (pathType == PathType.Linear)
            {
                int count = this.wps.Count;
                var wps = toWorldPoint(false);

                DrawLines(wps, null);
            }
            else
            {
                var points = path.nonLinearDrawWps;
                if (points == null)
                    return;

                DrawLines(points, localPointToWorld);
            }
        }
#endif
    }
}