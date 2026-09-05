using UnityEngine;

namespace VBSOED
{
    public class TrafficLightWalkLight : MonoBehaviour
    {
        [SerializeField] private MatTextureSet flagSet;
        [SerializeField] private MatTextureSet.TextureSetData flagRedData;
        [SerializeField] private MatTextureSet.TextureSetData flagGreenTexture;

        [SerializeField] private MatTextureSet numberSet1;
        [SerializeField] private MatTextureSet numberSet2;
        [SerializeField] private Texture2D[] redNumbers;
        [SerializeField] private Texture2D[] greenNumbers;

        //public void SetData(TrafficLightObject.LightType lightType)
        //{
        //    if (lightType == TrafficLightObject.LightType.Red)
        //        flagSet.Set(flagRedData);
        //    else if (lightType == TrafficLightObject.LightType.Green)
        //        flagSet.Set(flagGreenTexture);
        //}

        public void SetFlag(TrafficLightObject.LightType lightType)
        {
            if (lightType == TrafficLightObject.LightType.Red)
                flagSet.Set(flagRedData);
            else if (lightType == TrafficLightObject.LightType.Green)
                flagSet.Set(flagGreenTexture);
        }

        public void SetTime(float _time, TrafficLightObject.LightType lightType) 
        {
            int time = Mathf.CeilToInt(_time);
            int value1 = time / 10;
            int value2 = time % 10;
            if (value1 > 9) 
            {
                value1 = 9;
                value2 = 9;
            }
            var data = GetTextureData(value1, lightType);
            numberSet1.Set(data);
            data = GetTextureData(value2, lightType);
            numberSet2.Set(data);
        }

        MatTextureSet.TextureSetData numSetData = new MatTextureSet.TextureSetData();

        private MatTextureSet.TextureSetData GetTextureData(int num, TrafficLightObject.LightType lightType) 
        {
            var texture = lightType == TrafficLightObject.LightType.Red ? redNumbers[num] : greenNumbers[num];
            numSetData.data[0].texture = texture;
            numSetData.data[3].texture =  texture;
            return numSetData;
        }
    }
}
