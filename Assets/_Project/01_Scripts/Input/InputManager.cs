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
    public InputProvider ActiveProvider => activeProvider;
    
    public enum InputDevice { KeyboardMouse, VR, Gamepad, MR, AR }

    private void EnsureProvidersExist()
    {
        if (keyboardMouseProvider == null)
        {
            var go = new GameObject("KeyboardMouseInputProvider");
            go.transform.SetParent(transform);
            keyboardMouseProvider = go.AddComponent<KeyboardMouseInputProvider>();
        }

        if (gamepadProvider == null)
        {
            var go = new GameObject("GamepadInputProvider");
            go.transform.SetParent(transform);
            gamepadProvider = go.AddComponent<GamepadInputProvider>();
        }

        if (vrProvider == null)
        {
            var go = new GameObject("VRInputProvider");
            go.transform.SetParent(transform);
            vrProvider = go.AddComponent<VRInputProvider>();
        }

        if (mrProvider == null)
        {
            var go = new GameObject("MRInputProvider");
            go.transform.SetParent(transform);
            mrProvider = go.AddComponent<MRInputProvider>();
        }

        if (arProvider == null)
        {
            var go = new GameObject("ARInputProvider");
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
            if (Keyboard.current.f1Key.wasPressedThisFrame) SetInputDevice(InputDevice.KeyboardMouse);
            if (Keyboard.current.f2Key.wasPressedThisFrame) SetInputDevice(InputDevice.VR);
            if (Keyboard.current.f3Key.wasPressedThisFrame) SetInputDevice(InputDevice.Gamepad);
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
    
    // Метод временно отключён, чтобы не требовать Oculus SDK
    /*
    void EnablePassthrough(bool enable)
    {
        var pt = FindObjectOfType<OVRPassthroughLayer>();
        if (pt != null) 
        {
            pt.enabled = enable;
            pt.textureOpacity = 0.8f;
        }
    }
    */
    
    void LoadPreferredDevice()
    {
        int saved = PlayerPrefs.GetInt("InputDevice", -1);
        if (saved != -1)
        {
            SetInputDevice((InputDevice)saved);
            return;
        }
        
        if (vrProvider != null && vrProvider.IsAvailable())
            SetInputDevice(InputDevice.VR);
        else if (gamepadProvider != null && gamepadProvider.IsAvailable())
            SetInputDevice(InputDevice.Gamepad);
        else
            SetInputDevice(InputDevice.KeyboardMouse);
    }
    
    // Прокси-методы
    public Vector3 PointerPosition => activeProvider?.GetPointerPosition() ?? Vector3.zero;
    public Vector3 PointerDirection => activeProvider?.GetPointerDirection() ?? Vector3.forward;
    public bool SelectDown => activeProvider?.GetSelectDown() ?? false;
    public bool GrabDown => activeProvider?.GetGrabDown() ?? false;
    public bool SwitchRobotDown => activeProvider?.GetSwitchRobotDown() ?? false;
    public bool RecordDown => activeProvider?.GetRecordDown() ?? false;
    public bool PlayDown => activeProvider?.GetPlayDown() ?? false;
    public bool SelectHeld => activeProvider?.GetSelectHeld() ?? false;
    public bool GrabHeld => activeProvider?.GetGrabHeld() ?? false;
    public Vector2 Movement => activeProvider?.GetMovement() ?? Vector2.zero;
    public Vector2 Rotation => activeProvider?.GetRotation() ?? Vector2.zero;
    
    // Добавьте остальные по необходимости
}