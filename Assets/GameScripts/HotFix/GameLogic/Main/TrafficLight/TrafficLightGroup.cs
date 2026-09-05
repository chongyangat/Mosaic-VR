using UnityEngine;
using System.Collections.Generic;
using System;

namespace VBSOED
{
    public class TrafficLightGroup : MonoBehaviour
    {
        [Serializable]
        public class TrafficLightData 
        {
            public TrafficLightObject.LightType startType;
            public TrafficLightObject lightObject;
        }
        [SerializeField] private float delayTime = 0;
        [SerializeField] private float redLightTime = 30;
        [SerializeField] private float greenLightTime = 30;
        [SerializeField] private List<TrafficLightData> trafficLightObjects = new List<TrafficLightData>();


        private void Start()
        {
            int count = trafficLightObjects.Count;
            for (int i = 0; i < count; i++) 
            {
                var itor = trafficLightObjects[i];
                if (itor.lightObject == null) continue;
                itor.lightObject.Init(delayTime, redLightTime, greenLightTime, itor.startType);
            }
        }
    }
}
