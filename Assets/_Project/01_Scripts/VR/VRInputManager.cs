using UnityEngine;

public class VRInputManager : MonoBehaviour
{
    [Header("OVR Performance")]
    public bool useFoveatedRendering = true;
    
    void Start()
    {
        #if OCULUS_SDK
        if (useFoveatedRendering)
        {
            OVRManager.foveatedRenderingLevel = OVRManager.FoveatedRenderingLevel.High;
            OVRManager.useDynamicFoveatedRendering = true;
        }
        #endif
    }
    
    void Update()
    {
        #if OCULUS_SDK
        if (OVRInput.GetDown(OVRInput.Button.Start))
            OVRManager.display.RecenterPose();
        #endif
    }
}
