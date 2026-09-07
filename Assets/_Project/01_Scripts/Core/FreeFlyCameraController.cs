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
        try
        {
            if (Input.GetMouseButton(button))
            {
                return true;
            }
        }
        catch
        {
        }

        if (Mouse.current != null)
        {
            switch (button)
            {
                case 0:
                    if (Mouse.current.leftButton.isPressed) return true;
                    break;
                case 1:
                    if (Mouse.current.rightButton.isPressed) return true;
                    break;
                case 2:
                    if (Mouse.current.middleButton.isPressed) return true;
                    break;
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
        try
        {
            Vector2 legacyDelta = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
            if (legacyDelta.sqrMagnitude > 0f)
            {
                return legacyDelta * lookSensitivity;
            }
        }
        catch
        {
        }

        return Mouse.current != null
            ? Mouse.current.delta.ReadValue() * lookSensitivity
            : Vector2.zero;
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

        if (lockCursorOnStart && Application.isFocused)
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

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && lockCursorOnStart)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else if (!hasFocus)
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

        bool rightMouseHeld = IsMouseButtonPressed(1);
        if (rightMouseHeld)
        {
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

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

        bool hasHit = Physics.Raycast(
            origin,
            direction,
            out RaycastHit hit,
            laserLength,
            laserLayers,
            QueryTriggerInteraction.Ignore);
        if (hasHit)
        {
            endpoint = hit.point;
        }
        else
        {
            Plane workPlane = new Plane(Vector3.up, Vector3.zero);
            if (workPlane.Raycast(new Ray(origin, direction), out float planeDistance) &&
                planeDistance >= 0f && planeDistance <= laserLength)
            {
                endpoint = origin + direction * planeDistance;
            }
        }

        if (IsMouseButtonPressed(0) && !primaryButtonWasPressed)
        {
            RobotController selectedRobot = hasHit
                ? FindPreferredRobotController(hit.collider.transform)
                : null;
            if (selectedRobot == null)
            {
                selectedRobot = FindRobotNearRay(origin, direction, laserLength);
            }
            if (selectedRobot == null)
            {
                RobotController[] robots = Object.FindObjectsByType<RobotController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (robots.Length > 0)
                {
                    selectedRobot = robots[0];
                }
            }

            if (selectedRobot != null)
            {
                selectedRobot.SetActive(true);
                selectedRobot.SetTarget(endpoint, Quaternion.LookRotation(transform.forward, Vector3.up));
                Debug.Log($"[DesktopTeleoperation] Target {endpoint} assigned to {selectedRobot.name}.");
            }
        }

        laser.SetPosition(0, origin);
        laser.SetPosition(1, endpoint);
        primaryButtonWasPressed = IsMouseButtonPressed(0);
    }

    private static RobotController FindRobotNearRay(Vector3 origin, Vector3 direction, float maxDistance)
    {
        RobotController closestRobot = null;
        float closestRayDistance = float.PositiveInfinity;
        foreach (RobotController candidate in Object.FindObjectsByType<RobotController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            RobotController robot = FindPreferredRobotController(candidate.transform);
            if (robot == null || robot != candidate && candidate is SCARAController) continue;
            Vector3 toRobot = robot.transform.position - origin;
            float rayDistance = Vector3.Dot(toRobot, direction);
            if (rayDistance < 0f || rayDistance > maxDistance) continue;

            float perpendicularDistance = Vector3.Cross(direction, toRobot).magnitude;
            if (perpendicularDistance < closestRayDistance)
            {
                closestRayDistance = perpendicularDistance;
                closestRobot = robot;
            }
        }

        return closestRobot;
    }

    private static RobotController FindPreferredRobotController(Transform source)
    {
        SCARAController scara = source.GetComponentInParent<SCARAController>();
        if (scara != null) return scara;

        SixAxisController sixAxis = source.GetComponentInParent<SixAxisController>();
        if (sixAxis != null) return sixAxis;

        return source.GetComponentInParent<RobotController>();
    }
}
