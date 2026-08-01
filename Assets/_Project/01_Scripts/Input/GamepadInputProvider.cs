using UnityEngine;

public class GamepadInputProvider : InputProvider
{
    public Camera playerCamera;
    
    public override Vector3 GetPointerPosition() => playerCamera.transform.position;
    public override Vector3 GetPointerDirection() => playerCamera.transform.forward;
    
    public override bool GetSelectDown() => Input.GetButtonDown("Fire1");
    public override bool GetSelectHeld() => Input.GetButton("Fire1");
    public override bool GetGrabDown() => Input.GetButtonDown("Fire2");
    public override bool GetGrabHeld() => Input.GetButton("Fire2");
    public override bool GetSwitchRobotDown() => Input.GetButtonDown("LB");
    public override bool GetRecordDown() => Input.GetButtonDown("X");
    public override bool GetPlayDown() => Input.GetButtonDown("Y");
    
    public override Vector2 GetMovement()
    {
        return new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
    }
    
    public override Vector2 GetRotation()
    {
        return new Vector2(Input.GetAxis("RightStickX"), Input.GetAxis("RightStickY"));
    }
    
    public override bool IsAvailable()
    {
        return Input.GetJoystickNames().Length > 0 && 
               !string.IsNullOrEmpty(Input.GetJoystickNames()[0]);
    }
}