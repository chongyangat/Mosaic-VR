using Mirror;
using UnityEngine;

namespace GameLogic
{
    public class ManagerLightController : NetworkBehaviour
    {
        [Header("受控Light列表")]
        public Light[] lights;

        [SyncVar(hook = nameof(OnIntensityChanged))]
        public float globalIntensity = 1f;

        void OnIntensityChanged(float oldValue, float newValue)
        {
            ApplyIntensity(newValue);
        }

        public void SetIntensity(float intensity)
        {
            globalIntensity = intensity;
        }

        void ApplyIntensity(float intensity)
        {
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null)
                {
                    lights[i].intensity = intensity;
                }
            }
        }
    }
}
