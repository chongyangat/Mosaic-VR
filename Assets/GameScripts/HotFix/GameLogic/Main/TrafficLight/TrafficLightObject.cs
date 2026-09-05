using UnityEngine;
using System.Collections.Generic;
using System.Security.Cryptography;
using System;

namespace VBSOED
{
    public partial class TrafficLightObject : MonoBehaviour
    {
        public enum LightType 
        {
            None,
            Red,
            Green,
        }

        [SerializeField] private float delayTime = 0;
        [SerializeField] private float redLightTime = 10;
        [SerializeField] private float greenLightTime = 20;
        [SerializeField] private LightType startLight = LightType.Red;
        [SerializeField] private TrafficLightCarLight carLight;
        [SerializeField] private TrafficLightWalkLight walkLight;

        public LightType lightType { get; private set; }
        private bool enable = false;
        private int _id = -1;
        public int id
        {
            get
            {
                if (_id < 0) _id = GetId();
                return _id;
            }
        }
        public Action<TrafficLightObject> onStateChange;

        private void Start()
        {
            if(enable == false)
                SetEnable(true);
        }

        public void Init(float delayTime, float redLightTime, float greenLightTime, LightType startLight)
        {
            this.delayTime = delayTime;
            this.redLightTime = redLightTime;
            this.greenLightTime = greenLightTime;
            this.startLight = startLight;
            SetEnable(true);
        }

        public void SetEnable(bool enable) 
        {
            this.enable = enable;
            if (enable)
            {
                totalTime = 0;
                if (startLight == LightType.None) startLight = LightType.Red;
                SwitchLight(startLight);
            }
            else
            {
                SwitchLight(LightType.None);
            }
        }

        public void SwitchLight(LightType type)
        {
            time = 0;
            if(carLight != null)
                carLight.SetData(type);
            if(walkLight != null)
                walkLight.SetFlag(type == LightType.Red ? LightType.Green : LightType.Red);
            lightType = type;
            onStateChange?.Invoke(this);
        }

        public bool CanCarThrough() => lightType == LightType.Green;

        private float totalTime;
        private float time;
        private void Update()
        {
            if (lightType == LightType.None) return;
            var deltaTime = Time.deltaTime;
            totalTime += deltaTime;
            if (totalTime < delayTime) return;
            time += deltaTime;
            var stepTime = lightType == LightType.Red ? redLightTime : greenLightTime;
            var remain = Mathf.Max(stepTime - time, 0);
            if (walkLight != null)
                walkLight.SetTime(remain, lightType == LightType.Red ? LightType.Green : LightType.Red);
            if (time > stepTime)
            {
                SwitchLight(lightType == LightType.Red ? LightType.Green : LightType.Red);
            }
        }

        private int waitCount;
        //public void OnEnterWait() 
        //{
        //    waitCount++;
        //}

        //public void OnLeaveWait() 
        //{

        //}

        private float carLength = 4.5f;
        private float carOffset = 1f;
        public float GetWaitDis() 
        {
            if (waitCount <= 0)
            {
                return 0;
            }
            return waitCount * (carLength + carOffset);
        }

        private HashSet<long> waitIdList = new HashSet<long>();
        public bool AddWait(long id) 
        {
            if (waitIdList.Add(id))
            {
                ++waitCount;
                return true;
            }
            return false;
        }

        public bool RemoveWait(long id)
        {
            if (waitIdList.Remove(id))
            {
                --waitCount;
                return true;
            }
            return false;
        }

        static int c_id;
        private int GetId()
        {
            ++c_id;
            return c_id;
        }
    }
}
