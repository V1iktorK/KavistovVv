using UnityEngine;
using UnityEngine.InputSystem;

public class KeyboardMouseInputProvider : InputProvider
{
    public Camera playerCamera;
    public float mouseSensitivity = 1f;

    private bool TryLegacyMouseButton(int button)
    {
        try
        {
            return Input.GetMouseButton(button);
        }
        catch
        {
            return false;
        }
    }

    private bool TryLegacyKey(KeyCode code)
    {
        try
        {
            return Input.GetKey(code);
        }
        catch
        {
            return false;
        }
    }

    private bool TryLegacyKeyDown(KeyCode code)
    {
        try
        {
            return Input.GetKeyDown(code);
        }
        catch
        {
            return false;
        }
    }

    private Vector2 TryLegacyAxisVector(Vector2 fallback)
    {
        try
        {
            return new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        }
        catch
        {
            return fallback;
        }
    }

    private Vector2 TryLegacyMouseDelta()
    {
        try
        {
            return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * mouseSensitivity;
        }
        catch
        {
            return Vector2.zero;
        }
    }

    private Camera ResolveCamera()
    {
        if (playerCamera == null)
            playerCamera = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();

        if (playerCamera == null)
            Debug.LogWarning("[KeyboardMouseInputProvider] Camera not assigned and no camera found.");

        return playerCamera;
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
        if (Mouse.current != null)
            return Mouse.current.leftButton.wasPressedThisFrame;
        return Input.GetMouseButtonDown(0);
    }

    public override bool GetSelectHeld()
    {
        if (Mouse.current != null)
            return Mouse.current.leftButton.isPressed;
        return TryLegacyMouseButton(0);
    }

    public override bool GetGrabDown()
    {
        if (Mouse.current != null)
            return Mouse.current.rightButton.wasPressedThisFrame;
        return Input.GetMouseButtonDown(1);
    }

    public override bool GetGrabHeld()
    {
        if (Mouse.current != null)
            return Mouse.current.rightButton.isPressed;
        return TryLegacyMouseButton(1);
    }

    public override bool GetSwitchRobotDown()
    {
        if (Keyboard.current != null)
            return Keyboard.current.tabKey.wasPressedThisFrame || Keyboard.current.fKey.wasPressedThisFrame;
        return TryLegacyKeyDown(KeyCode.Tab) || TryLegacyKeyDown(KeyCode.F);
    }

    public override bool GetRecordDown()
    {
        if (Keyboard.current != null)
            return Keyboard.current.rKey.wasPressedThisFrame;
        return TryLegacyKeyDown(KeyCode.R);
    }

    public override bool GetPlayDown()
    {
        if (Keyboard.current != null)
            return Keyboard.current.pKey.wasPressedThisFrame;
        return TryLegacyKeyDown(KeyCode.P);
    }

    public override Vector2 GetMovement()
    {
        if (Keyboard.current != null)
        {
            float h = (Keyboard.current.aKey.isPressed ? -1f : 0f) + (Keyboard.current.dKey.isPressed ? 1f : 0f);
            float v = (Keyboard.current.sKey.isPressed ? -1f : 0f) + (Keyboard.current.wKey.isPressed ? 1f : 0f);
            return new Vector2(h, v);
        }
        return TryLegacyAxisVector(Vector2.zero);
    }

    public override Vector2 GetRotation()
    {
        if (Mouse.current != null)
        {
            if (Mouse.current.rightButton.isPressed || Mouse.current.middleButton.isPressed)
                return Mouse.current.delta.ReadValue() * mouseSensitivity;
            return Vector2.zero;
        }

        if (TryLegacyMouseButton(2) || TryLegacyMouseButton(1))
            return TryLegacyMouseDelta();
        return Vector2.zero;
    }

    public override bool IsAvailable() => true;
}