using UnityEngine;

namespace GameMain
{
    public static class VectorExtensions
    {
        /// <summary>
        /// Are two points within <see cref="Vector3.kEpsilon"/>
        /// distance of each other
        /// </summary>
        public static bool Approximately(this Vector3 self, Vector3 target, float precision = Vector3.kEpsilon)
        {
            return (self - target).sqrMagnitude <= precision * precision;
        }

        /// <summary>
        /// Vector3 XZ轴约等于
        /// </summary>
        /// <param name="self"></param>
        /// <param name="target"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public static bool ApproximatelyXZ(this Vector3 self, Vector3 target, float precision = 0.001f)
        {
            return Mathf.Abs(self.x - target.x) < precision && Mathf.Abs(self.z - target.z) < precision;
        }
    }
}
