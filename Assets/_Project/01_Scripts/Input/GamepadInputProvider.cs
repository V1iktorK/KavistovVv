using UnityEngine;
using UnityEngine.InputSystem;

public class GamepadInputProvider : InputProvider
{
    public Camera playerCamera;

    private Camera ResolveCamera()
    {
        if (playerCamera == null)
            playerCamera = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();

        if (playerCamera == null)
            Debug.LogWarning("[GamepadInputProvider] Camera not assigned and no camera found.");

        return playerCamera;
    }

    private Gamepad GetGamepad()
    {
        return Gamepad.current;
    }

    public override Vector3 GetPointerPosition()
    {
        var camera = ResolveCamera();
        return camera != null ? camera.transform.position : Vector3.zero;
    }

    public override Vector3 GetPointerDirection()
    {
        var camera = ResolveCamera();
        return camera != null ? camera.transform.forward : Vector3.forward;
    }

    public override bool GetSelectDown()
    {
        var gamepad = GetGamepad();
        if (gamepad != null)
            return gamepad.buttonSouth.wasPressedThisFrame || gamepad.buttonEast.wasPressedThisFrame;
        return Input.GetButtonDown("Fire1");
    }

    public override bool GetSelectHeld()
    {
        var gamepad = GetGamepad();
        if (gamepad != null)
            return gamepad.buttonSouth.isPressed || gamepad.buttonEast.isPressed;
        return Input.GetButton("Fire1");
    }

    public override bool GetGrabDown()
    {
        var gamepad = GetGamepad();
        if (gamepad != null)
            return gamepad.buttonWest.wasPressedThisFrame || gamepad.buttonNorth.wasPressedThisFrame;
        return Input.GetButtonDown("Fire2");
    }

    public override bool GetGrabHeld()
    {
        var gamepad = GetGamepad();
        if (gamepad != null)
            return gamepad.buttonWest.isPressed || gamepad.buttonNorth.isPressed;
        return Input.GetButton("Fire2");
    }

    public override bool GetSwitchRobotDown()
    {
        var gamepad = GetGamepad();
        if (gamepad != null)
            return gamepad.leftShoulder.wasPressedThisFrame;
        return Input.GetButtonDown("LB");
    }

    public override bool GetRecordDown()
    {
        var gamepad = GetGamepad();
        if (gamepad != null)
            return gamepad.buttonWest.wasPressedThisFrame;
        return Input.GetButtonDown("X");
    }

    public override bool GetPlayDown()
    {
        var gamepad = GetGamepad();
        if (gamepad != null)
            return gamepad.buttonNorth.wasPressedThisFrame;
        return Input.GetButtonDown("Y");
    }

    public override Vector2 GetMovement()
    {
        var gamepad = GetGamepad();
        if (gamepad != null)
            return gamepad.leftStick.ReadValue();

        return new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
    }

    public override Vector2 GetRotation()
    {
        var gamepad = GetGamepad();
        if (gamepad != null)
            return gamepad.rightStick.ReadValue();

        return new Vector2(Input.GetAxis("RightStickX"), Input.GetAxis("RightStickY"));
    }

    public override bool IsAvailable()
    {
        var gamepad = GetGamepad();
        return gamepad != null || Input.GetJoystickNames().Length > 0 && !string.IsNullOrEmpty(Input.GetJoystickNames()[0]);
    }
}