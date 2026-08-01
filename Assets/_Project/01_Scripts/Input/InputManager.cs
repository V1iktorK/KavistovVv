using UnityEngine;

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
    
    public enum InputDevice { VR, KeyboardMouse, Gamepad, MR, AR }
    
    void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    
    void Start()
    {
        LoadPreferredDevice();
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
                // EnablePassthrough(true); // временно закомментировано
                break;
            case InputDevice.AR:
                activeProvider = arProvider;
                // EnablePassthrough(false);
                // var arSession = FindObjectOfType<ARSession>();
                // if (arSession != null) arSession.enabled = true;
                break;
        }
        
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
    
    // Добавьте остальные по необходимости
}