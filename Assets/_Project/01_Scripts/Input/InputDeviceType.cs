using UnityEngine;

/// <summary>
/// Отдельный enum для устройства ввода (не зависит от InputManager MonoBehaviour).
/// Используется SettingsData и InputManager.
/// </summary>
public enum InputDeviceType
{
    KeyboardMouse = 0,
    VR = 1,
    Gamepad = 2,
    MR = 3,
    AR = 4
}

/// <summary>
/// Конвертация между InputDeviceType и InputManager.InputDevice.
/// </summary>
public static class InputDeviceExtensions
{
    public static InputManager.InputDevice ToInputManagerDevice(InputDeviceType type)
    {
        return (InputManager.InputDevice)type;
    }

    public static InputDeviceType ToInputDeviceType(InputManager.InputDevice type)
    {
        return (InputDeviceType)type;
    }
}
