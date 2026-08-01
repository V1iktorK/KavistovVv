using UnityEngine;

public class KeyboardMouseInputProvider : InputProvider
{
    public Camera playerCamera;
    public float mouseSensitivity = 1f;
    
    private float yaw = 0f;
    private float pitch = 0f;
    
    void Start()
    {
        yaw = playerCamera.transform.eulerAngles.y;
        pitch = playerCamera.transform.eulerAngles.x;
    }
    
    public override Vector3 GetPointerPosition() => playerCamera.transform.position;
    
    public override Vector3 GetPointerDirection()
    {
        return playerCamera.transform.forward;
    }
    
    public override bool GetSelectDown() => Input.GetMouseButtonDown(0);
    public override bool GetSelectHeld() => Input.GetMouseButton(0);
    public override bool GetGrabDown() => Input.GetMouseButtonDown(1);
    public override bool GetGrabHeld() => Input.GetMouseButton(1);
    public override bool GetSwitchRobotDown() => Input.GetKeyDown(KeyCode.Tab);
    public override bool GetRecordDown() => Input.GetKeyDown(KeyCode.R);
    public override bool GetPlayDown() => Input.GetKeyDown(KeyCode.P);
    
    public override Vector2 GetMovement()
    {
        return new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
    }
    
    public override Vector2 GetRotation()
    {
        if (Input.GetMouseButton(2) || Input.GetMouseButton(1))
        {
            return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * mouseSensitivity;
        }
        return Vector2.zero;
    }
    
    public override bool IsAvailable() => true;
}