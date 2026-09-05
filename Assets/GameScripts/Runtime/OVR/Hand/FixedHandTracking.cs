using UnityEngine;

namespace GameMain
{
    public class FixedHandTracking : MonoBehaviour {
    [SerializeField] private OVRCameraRig _cameraRig;
    [SerializeField] private OVRHand _leftHand, _rightHand;
    [SerializeField] 
    private GameObject _leftVirtualHand, _rightVirtualHand;
    private Vector3 _fixedOrigin;

    void Update() {
        // 更新头显位置
        _fixedOrigin = _cameraRig.transform.position;
        
        // 更新虚拟手
        UpdateVirtualHand(_leftHand, _leftVirtualHand);
        UpdateVirtualHand(_rightHand, _rightVirtualHand);
    }

    void UpdateVirtualHand(OVRHand sourceHand, GameObject virtualHand) {
        if (sourceHand.IsTracked) {
            Vector3 handPos = sourceHand.PointerPose.position;
            Vector3 localPos = _cameraRig.centerEyeAnchor.InverseTransformPoint(handPos);
            virtualHand.transform.position = _cameraRig.centerEyeAnchor.TransformPoint(localPos);
            virtualHand.transform.rotation = sourceHand.PointerPose.rotation;
        }
        virtualHand.SetActive(sourceHand.IsTracked);
    }
}
}