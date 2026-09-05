using UnityEngine;

namespace VBSOED
{
    public class TrafficLightCarLight : MonoBehaviour
    {
        [SerializeField] private MatTextureSet textureSet;
        [SerializeField] private MatTextureSet.TextureSetData redData;
        [SerializeField] private MatTextureSet.TextureSetData greenData;

        public void SetData(TrafficLightObject.LightType lightType) 
        {
            if (lightType == TrafficLightObject.LightType.Red)
                textureSet.Set(redData);
            else if(lightType == TrafficLightObject.LightType.Green)
                textureSet.Set(greenData);
        }
    }
}
