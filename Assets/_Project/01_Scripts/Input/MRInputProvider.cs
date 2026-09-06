using UnityEngine;

// Desktop build does not include the Meta/Oculus SDK.
public class MRInputProvider : InputProvider
{
    public override Vector3 GetPointerPosition() =>
        Camera.main != null ? Camera.main.transform.position : Vector3.zero;

    public override Vector3 GetPointerDirection() =>
        Camera.main != null ? Camera.main.transform.forward : Vector3.forward;

    public override bool GetSelectDown() => false;
    public override bool GetGrabDown() => false;
    public override bool GetSwitchRobotDown() => false;
    public override bool GetRecordDown() => false;
    public override bool GetPlayDown() => false;
    public override Vector2 GetMovement() => Vector2.zero;
    public override Vector2 GetRotation() => Vector2.zero;
    public override bool IsAvailable() => false;
}
