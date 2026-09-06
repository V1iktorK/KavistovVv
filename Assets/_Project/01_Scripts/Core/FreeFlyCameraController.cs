using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class FreeFlyCameraController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float sprintSpeed = 10f;
    public float boostMultiplier = 2f;
    public float verticalSpeed = 4f;

    [Header("Look")]
    public float lookSensitivity = 2f;
    public bool invertY = false;

    [Header("Startup")]
    public bool lockCursorOnStart = false;
    public Vector3 startupPosition = new Vector3(0f, 2f, -28f);
    public Vector3 startupLookAt = new Vector3(12f, -8f, 0f);

    [Header("Desktop teleoperation")]
    public bool enableLaserPointer = true;
    public float laserLength = 100f;
    public float laserHandOffset = 0.35f;
    public LayerMask laserLayers = Physics.DefaultRaycastLayers;

    private float yaw;
    private float pitch;
    private LineRenderer laser;
    private bool primaryButtonWasPressed;

    private bool IsKeyPressed(KeyCode code)
    {
        if (Keyboard.current != null)
        {
            switch (code)
            {
                case KeyCode.W: return Keyboard.current.wKey.isPressed;
                case KeyCode.S: return Keyboard.current.sKey.isPressed;
                case KeyCode.A: return Keyboard.current.aKey.isPressed;
                case KeyCode.D: return Keyboard.current.dKey.isPressed;
                case KeyCode.Q: return Keyboard.current.qKey.isPressed;
                case KeyCode.E: return Keyboard.current.eKey.isPressed;
                case KeyCode.LeftShift: return Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
                case KeyCode.Escape: return Keyboard.current.escapeKey.wasPressedThisFrame;
            }
        }

        try
        {
            return Input.GetKey(code);
        }
        catch
        {
            return false;
        }
    }

    private bool IsMouseButtonPressed(int button)
    {
        if (Mouse.current != null)
        {
            switch (button)
            {
                case 0: return Mouse.current.leftButton.isPressed;
                case 1: return Mouse.current.rightButton.isPressed;
                case 2: return Mouse.current.middleButton.isPressed;
            }
        }

        try
        {
            return Input.GetMouseButton(button);
        }
        catch
        {
            return false;
        }
    }

    private Vector2 ReadMouseDelta()
    {
        if (Mouse.current != null)
        {
            return Mouse.current.delta.ReadValue() * lookSensitivity;
        }

        try
        {
            return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * lookSensitivity;
        }
        catch
        {
            return Vector2.zero;
        }
    }

    void Awake()
    {
        transform.position = startupPosition;
        Vector3 lookDirection = startupLookAt - startupPosition;
        if (lookDirection.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        }

        if (enableLaserPointer)
        {
            laser = gameObject.GetComponent<LineRenderer>();
            if (laser == null)
            {
                laser = gameObject.AddComponent<LineRenderer>();
            }

            laser.positionCount = 2;
            laser.useWorldSpace = true;
            laser.startWidth = 0.018f;
            laser.endWidth = 0.006f;
            laser.startColor = Color.red;
            laser.endColor = new Color(1f, 0.1f, 0.1f, 0.15f);
            laser.material = new Material(Shader.Find("Sprites/Default"));
        }
    }

    void Start()
    {
        yaw = transform.eulerAngles.y;
        pitch = transform.eulerAngles.x;

        if (lockCursorOnStart)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = Cursor.lockState == CursorLockMode.Locked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = Cursor.lockState == CursorLockMode.None;
        }
        else if (Keyboard.current == null)
        {
            try
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    Cursor.lockState = Cursor.lockState == CursorLockMode.Locked ? CursorLockMode.None : CursorLockMode.Locked;
                    Cursor.visible = Cursor.lockState == CursorLockMode.None;
                }
            }
            catch
            {
            }
        }

        if (IsMouseButtonPressed(1))
        {
            Vector2 delta = ReadMouseDelta();
            float mouseX = delta.x;
            float mouseY = delta.y * (invertY ? 1f : -1f);

            yaw += mouseX;
            pitch -= mouseY;
            pitch = Mathf.Clamp(pitch, -89f, 89f);

            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        UpdateLaserPointer();

        float speed = IsKeyPressed(KeyCode.LeftShift) ? sprintSpeed * boostMultiplier : moveSpeed;
        Vector3 move = Vector3.zero;

        if (IsKeyPressed(KeyCode.W)) move += transform.forward;
        if (IsKeyPressed(KeyCode.S)) move -= transform.forward;
        if (IsKeyPressed(KeyCode.D)) move += transform.right;
        if (IsKeyPressed(KeyCode.A)) move -= transform.right;
        if (IsKeyPressed(KeyCode.E)) move += Vector3.up * verticalSpeed;
        if (IsKeyPressed(KeyCode.Q)) move -= Vector3.up * verticalSpeed;

        if (move.sqrMagnitude > 0f)
        {
            move = move.normalized * speed * Time.deltaTime;
            transform.position += move;
        }
    }

    private void UpdateLaserPointer()
    {
        if (!enableLaserPointer || laser == null)
        {
            return;
        }

        Vector3 origin = transform.position - transform.right * laserHandOffset - transform.up * 0.15f;
        Vector3 direction = transform.forward;
        Vector3 endpoint = origin + direction * laserLength;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, laserLength, laserLayers, QueryTriggerInteraction.Ignore))
        {
            endpoint = hit.point;
            if (IsMouseButtonPressed(0) && !primaryButtonWasPressed)
            {
                RobotController[] robots = FindObjectsByType<RobotController>(FindObjectsSortMode.None);
                RobotController selectedRobot = null;
                float closestDistance = float.PositiveInfinity;
                foreach (RobotController robot in robots)
                {
                    float distance = Vector3.Distance(robot.transform.position, hit.point);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        selectedRobot = robot;
                    }
                }

                if (selectedRobot != null)
                {
                    selectedRobot.SetActive(true);
                    selectedRobot.SetTarget(hit.point, Quaternion.LookRotation(transform.forward, Vector3.up));
                }
            }
        }

        laser.SetPosition(0, origin);
        laser.SetPosition(1, endpoint);
        primaryButtonWasPressed = IsMouseButtonPressed(0);
    }
}
