// VRInputProvider.cs
using UnityEngine;

public class VRInputProvider : InputProvider
{
    public Transform rightController;
    public Transform leftController;
    
    public override Vector3 GetPointerPosition() => rightController.position;
    public override Vector3 GetPointerDirection() => rightController.forward;
    
    public override bool GetSelectDown() => OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger);
    public override bool GetSelectHeld() => OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger);
    public override bool GetGrabDown() => OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger);
    public override bool GetGrabHeld() => OVRInput.Get(OVRInput.Button.PrimaryHandTrigger);
    public override bool GetSwitchRobotDown() => OVRInput.GetDown(OVRInput.Button.One);
    public override bool GetRecordDown() => OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger);
    public override bool GetPlayDown() => OVRInput.GetDown(OVRInput.Button.Two);
    
    public override Vector2 GetMovement()
    {
        return OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);
    }
    
    public override Vector2 GetRotation()
    {
        return OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick);
    }
    
    public override bool IsAvailable() => OVRInput.IsControllerConnected(OVRInput.Controller.Touch);
}