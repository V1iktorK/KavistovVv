using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance;

    [SerializeField] private InputProvider vrProvider;
    [SerializeField] private InputProvider keyboardMouseProvider;
    [SerializeField] private InputProvider gamepadProvider;
    [SerializeField] private InputProvider mrProvider;
    [SerializeField] private InputProvider arProvider;

    private InputProvider activeProvider;

    public enum InputDevice { KeyboardMouse, VR, Gamepad, MR, AR }

    public InputProvider ActiveProvider
    {
        get { return activeProvider; }
    }

    private void EnsureProvidersExist()
    {
        if (keyboardMouseProvider == null)
        {
            GameObject go = new GameObject("KeyboardMouseInputProvider");
            go.transform.SetParent(transform);
            keyboardMouseProvider = go.AddComponent<KeyboardMouseInputProvider>();
        }

        if (gamepadProvider == null)
        {
            GameObject go = new GameObject("GamepadInputProvider");
            go.transform.SetParent(transform);
            gamepadProvider = go.AddComponent<GamepadInputProvider>();
        }

        if (vrProvider == null)
        {
            GameObject go = new GameObject("VRInputProvider");
            go.transform.SetParent(transform);
            vrProvider = go.AddComponent<VRInputProvider>();
        }

        if (mrProvider == null)
        {
            GameObject go = new GameObject("MRInputProvider");
            go.transform.SetParent(transform);
            mrProvider = go.AddComponent<MRInputProvider>();
        }

        if (arProvider == null)
        {
            GameObject go = new GameObject("ARInputProvider");
            go.transform.SetParent(transform);
            arProvider = go.AddComponent<ARInputProvider>();
        }
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            return;
        }

        Destroy(gameObject);
    }

    void Start()
    {
        EnsureProvidersExist();
        LoadPreferredDevice();
    }

    void Update()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.f1Key.wasPressedThisFrame)
            {
                SetInputDevice(InputDevice.KeyboardMouse);
            }
            if (Keyboard.current.f2Key.wasPressedThisFrame)
            {
                SetInputDevice(InputDevice.VR);
            }
            if (Keyboard.current.f3Key.wasPressedThisFrame)
            {
                SetInputDevice(InputDevice.Gamepad);
            }
            return;
        }

        if (Input.GetKeyDown(KeyCode.F1)) SetInputDevice(InputDevice.KeyboardMouse);
        if (Input.GetKeyDown(KeyCode.F2)) SetInputDevice(InputDevice.VR);
        if (Input.GetKeyDown(KeyCode.F3)) SetInputDevice(InputDevice.Gamepad);
    }

    public void SetInputDevice(InputDevice device)
    {
        if (activeProvider != null)
            activeProvider.enabled = false;

        switch (device)
        {
            case InputDevice.VR:
                activeProvider = vrProvider;
                break;
            case InputDevice.KeyboardMouse:
                activeProvider = keyboardMouseProvider;
                break;
            case InputDevice.Gamepad:
                activeProvider = gamepadProvider;
                break;
            case InputDevice.MR:
                activeProvider = mrProvider;
                break;
            case InputDevice.AR:
                activeProvider = arProvider;
                break;
            default:
                activeProvider = keyboardMouseProvider;
                break;
        }

        if (activeProvider == null)
        {
            Debug.LogError("[InputManager] No valid provider selected.");
            return;
        }

        activeProvider.enabled = true;
        PlayerPrefs.SetInt("InputDevice", (int)device);
        PlayerPrefs.Save();
    }
    
    void LoadPreferredDevice()
    {
        int saved = PlayerPrefs.GetInt("InputDevice", -1);
        if (saved != -1)
        {
            SetInputDevice((InputDevice)saved);
            return;
        }

        if (vrProvider != null && vrProvider.IsAvailable())
        {
            SetInputDevice(InputDevice.VR);
        }
        else if (gamepadProvider != null && gamepadProvider.IsAvailable())
        {
            SetInputDevice(InputDevice.Gamepad);
        }
        else
        {
            SetInputDevice(InputDevice.KeyboardMouse);
        }
    }

    // --- Прокси-свойства активного провайдера ввода ---

    public Vector3 PointerPosition
    {
        get { return activeProvider != null ? activeProvider.GetPointerPosition() : Vector3.zero; }
    }

    public Vector3 PointerDirection
    {
        get { return activeProvider != null ? activeProvider.GetPointerDirection() : Vector3.forward; }
    }

    public bool SelectDown
    {
        get { return activeProvider != null && activeProvider.GetSelectDown(); }
    }

    public bool SelectHeld
    {
        get { return activeProvider != null && activeProvider.GetSelectHeld(); }
    }

    public bool GrabDown
    {
        get { return activeProvider != null && activeProvider.GetGrabDown(); }
    }

    public bool GrabHeld
    {
        get { return activeProvider != null && activeProvider.GetGrabHeld(); }
    }

    public bool SwitchRobotDown
    {
        get { return activeProvider != null && activeProvider.GetSwitchRobotDown(); }
    }

    public bool RecordDown
    {
        get { return activeProvider != null && activeProvider.GetRecordDown(); }
    }

    public bool PlayDown
    {
        get { return activeProvider != null && activeProvider.GetPlayDown(); }
    }

    public Vector2 Movement
    {
        get { return activeProvider != null ? activeProvider.GetMovement() : Vector2.zero; }
    }

    public Vector2 Rotation
    {
        get { return activeProvider != null ? activeProvider.GetRotation() : Vector2.zero; }
    }
}