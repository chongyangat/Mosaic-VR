using UnityEngine;

namespace GameMain
{
    public static class QuaternionExtensions
    {
        /// <summary>
        /// Quaternion约等于
        /// </summary>
        /// <param name="self"></param>
        /// <param name="rot"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public static bool Approximately(this Quaternion self, Quaternion rot, float precision = 0.001f)
        {
            return Mathf.Abs(self.x - rot.x) < precision && Mathf.Abs(self.y - rot.y) < precision
                && Mathf.Abs(self.z - rot.z) < precision && Mathf.Abs(self.w - rot.w) < precision;
        }
    }
}
