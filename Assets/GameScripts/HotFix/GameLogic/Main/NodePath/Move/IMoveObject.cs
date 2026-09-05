using UnityEngine;
using System.Collections.Generic;
using UnityGameFramework.Runtime;
using VBSOED;

namespace VBSOED
{
    public interface IMoveObject
    {
        long id { get; }
        Vector3 position { get; set; }
        float speed { get; set; }
        float angle { get; set; }
        void SetSmoothRot(float angle);
        TrafficLightObject.LightType lightType { get; }
        int waitIndex { get; set; }
    }
}
