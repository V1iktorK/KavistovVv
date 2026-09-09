using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Свободная камера-«оператор» для телeoперации роботами.
/// Управление: WASD + Q/E (вертикаль), ПКМ — осмотр.
///
/// Функции, добавленные в этой версии:
///  1) Двухрукавные лазерные указки (левая — КРАСНАЯ, правая — ЗЕЛЁНАЯ).
///     Каждая включается/выключается ПЕРЕКЛЮЧЕНИЕМ (toggle):
///        Z — левая рука (красная), X — правая рука (зелёная).
///     Лучи сходятся в точке прицеливания на поверхности; чем ближе объект,
///     тем больше угол лучей относительно корпуса камеры.
///  2) У рук разный функционал:
///        Левая (красная) — наведение позиции TCP активного робота.
///        Правая (зелёная) — наведение ориентации TCP (куда «смотрит» инструмент).
///  3) Маленький коллайдер (CharacterController) на камере: камера врезается
///     в стены/текстуры, но не проходит сквозь них.
///  4) Движение/поворот геймпадом, когда активен Gamepad-провайдер (F3),
///     переключаемое клавишей G (или правым стиком при активном геймпаде).
/// </summary>
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

    [Header("Desktop teleoperation - laser pointers (two hands)")]
    public bool enableLaserPointer = true;            // мастер-выключатель обеих указок
    public float laserLength = 100f;
    public float laserHandOffset = 0.35f;             // латеральное смещение рук от центра
    public float laserDownOffset = 0.15f;             // опускание рук относительно центра
    public LayerMask laserLayers = Physics.DefaultRaycastLayers;

    [Header("Left hand (RED) - position target, toggle Z")]
    public bool leftHandEnabled = true;
    public float leftHandOffset = 0.35f;
    public Color leftColor = new Color(1f, 0.1f, 0.1f);

    [Header("Right hand (GREEN) - orientation target, toggle X")]
    public bool rightHandEnabled = false;
    public float rightHandOffset = 0.35f;
    public Color rightColor = new Color(0.1f, 1f, 0.3f);

    [Header("Camera collision (bump, not pass-through)")]
    public bool enableCameraCollision = true;
    public float bodyRadius = 0.25f;
    public float bodyHeight = 1.6f;
    public float bodySkin = 0.02f;

    [Header("Gamepad control (toggle G / right stick)")]
    public float gamepadLookSensitivity = 1.5f;

    private float yaw;
    private float pitch;
    private LineRenderer leftLaser;
    private LineRenderer rightLaser;
    private bool primaryButtonWasPressed;
    private CharacterController body;
    private bool gamepadMove;

    // Точка прицеливания (куда смотрит оператор) — на поверхности.
    private Vector3 aimPoint;
    private bool aimHitSurface;

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
                case KeyCode.Z: return Keyboard.current.zKey.wasPressedThisFrame;
                case KeyCode.X: return Keyboard.current.xKey.wasPressedThisFrame;
                case KeyCode.G: return Keyboard.current.gKey.wasPressedThisFrame;
                case KeyCode.F: return Keyboard.current.fKey.wasPressedThisFrame;
            }
        }

        try
        {
            switch (code)
            {
                case KeyCode.W: return Input.GetKey(KeyCode.W);
                case KeyCode.S: return Input.GetKey(KeyCode.S);
                case KeyCode.A: return Input.GetKey(KeyCode.A);
                case KeyCode.D: return Input.GetKey(KeyCode.D);
                case KeyCode.Q: return Input.GetKey(KeyCode.Q);
                case KeyCode.E: return Input.GetKey(KeyCode.E);
                case KeyCode.LeftShift: return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                case KeyCode.Escape: return Input.GetKeyDown(KeyCode.Escape);
                case KeyCode.Z: return Input.GetKeyDown(KeyCode.Z);
                case KeyCode.X: return Input.GetKeyDown(KeyCode.X);
                case KeyCode.G: return Input.GetKeyDown(KeyCode.G);
                case KeyCode.F: return Input.GetKeyDown(KeyCode.F);
            }
        }
        catch
        {
        }
        return false;
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

    private bool IsMouseButtonDownThisFrame(int button)
    {
        try
        {
            if (Input.GetMouseButtonDown(button))
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
                    if (Mouse.current.leftButton.wasPressedThisFrame) return true;
                    break;
                case 1:
                    if (Mouse.current.rightButton.wasPressedThisFrame) return true;
                    break;
                case 2:
                    if (Mouse.current.middleButton.wasPressedThisFrame) return true;
                    break;
            }
        }
        return false;
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

        leftLaser = CreateLaser();
        rightLaser = CreateLaser();

        if (enableCameraCollision)
        {
            body = GetComponent<CharacterController>();
            if (body == null)
            {
                body = gameObject.AddComponent<CharacterController>();
            }
            body.radius = bodyRadius;
            body.height = bodyHeight;
            body.skinWidth = bodySkin;
            body.center = new Vector3(0f, bodyHeight * 0.5f, 0f);
        }
    }

    /// <summary>Создаёт новый, гарантированно отдельный LineRenderer для руки.</summary>
    private LineRenderer CreateLaser()
    {
        var lr = gameObject.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.useWorldSpace = true;
        lr.startWidth = 0.018f;
        lr.endWidth = 0.006f;
        lr.enabled = false;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            lr.material = new Material(shader);
        }
        else
        {
            Debug.LogWarning("[FreeFlyCamera] Шейдер Sprites/Default не найден — лазер без материала.");
        }
        return lr;
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

    private bool IsGamepadActive()
    {
        // Геймпад считается активным, если он подключён (через new Input System).
        return Gamepad.current != null;
    }

    void Update()
    {
        bool gamepadMode = IsGamepadActive();

        // Переключаемое управление геймпадом: клавиша G (на клавиатуре),
        // либо при активном геймпаде — правый стик нажатый (R3). При включении
        // управления камера начинает слушать стики геймпада.
        if (IsKeyPressed(KeyCode.G))
        {
            gamepadMove = !gamepadMove;
            Debug.Log("[FreeFlyCamera] Управление геймпадом: " + (gamepadMove ? "ВКЛ" : "ВЫКЛ"));
        }
        if (gamepadMode && Gamepad.current != null && Gamepad.current.rightStickButton.wasPressedThisFrame)
        {
            gamepadMove = !gamepadMove;
            Debug.Log("[FreeFlyCamera] Управление геймпадом (стик): " + (gamepadMove ? "ВКЛ" : "ВЫКЛ"));
        }

        // Переключение рук-лазеров (toggle) клавишами Z / X.
        if (IsKeyPressed(KeyCode.Z)) leftHandEnabled = !leftHandEnabled;
        if (IsKeyPressed(KeyCode.X)) rightHandEnabled = !rightHandEnabled;

        // Выбор активного робота по клавише F (или кнопке смены устройства).
        if (IsKeyPressed(KeyCode.F)) SelectNextRobot();
        else if (gamepadMode && Gamepad.current != null && Gamepad.current.leftShoulder.wasPressedThisFrame)
            SelectNextRobot();

        bool lmbDown = IsMouseButtonDownThisFrame(0);
        bool rmbDown = IsMouseButtonDownThisFrame(1);

        // Esc освобождает курсор (возврат в UI/меню).
        bool escDown = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                       || (Keyboard.current == null && TryLegacyKeyDown(KeyCode.Escape));
        if (escDown && Cursor.lockState == CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // Клик по окну Game при свободном курсоре → захват мыши (FPS-режим).
        bool justCaptured = false;
        if (Cursor.lockState != CursorLockMode.Locked && (lmbDown || rmbDown) && Application.isFocused)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            justCaptured = true;
            primaryButtonWasPressed = true; // этот клик не считаем телеоперацией
        }

        // В захваченном состоянии мышь вращает камеру (без удержания ПКМ).
        bool lookFromMouse = Cursor.lockState == CursorLockMode.Locked && !justCaptured;
        if (lookFromMouse)
        {
            Vector2 delta = ReadMouseDelta();
            float mouseX = delta.x;
            float mouseY = delta.y * (invertY ? 1f : -1f);

            yaw += mouseX;
            pitch -= mouseY;
            pitch = Mathf.Clamp(pitch, -89f, 89f);

            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        // Поворот геймпадом (правый стик), переключаемо.
        if (gamepadMove && gamepadMode && Gamepad.current != null)
        {
            Vector2 rot = Gamepad.current.rightStick.ReadValue();
            float lookX = rot.x * gamepadLookSensitivity * 100f * Time.deltaTime;
            float lookY = rot.y * gamepadLookSensitivity * 100f * Time.deltaTime * (invertY ? 1f : -1f);
            yaw += lookX;
            pitch = Mathf.Clamp(pitch - lookY, -89f, 89f);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        UpdateLaserPointers();
        HandleClickActions();

        // --- Движение ---
        float speed = IsKeyPressed(KeyCode.LeftShift) ? sprintSpeed * boostMultiplier : moveSpeed;
        Vector3 move = Vector3.zero;

        if (IsKeyPressed(KeyCode.W)) move += transform.forward;
        if (IsKeyPressed(KeyCode.S)) move -= transform.forward;
        if (IsKeyPressed(KeyCode.D)) move += transform.right;
        if (IsKeyPressed(KeyCode.A)) move -= transform.right;
        if (IsKeyPressed(KeyCode.E)) move += Vector3.up * verticalSpeed;
        if (IsKeyPressed(KeyCode.Q)) move -= Vector3.up * verticalSpeed;

        // Геймпад: левый стик — горизонтальное движение (переключаемо).
        if (gamepadMove && gamepadMode && Gamepad.current != null)
        {
            Vector2 stick = Gamepad.current.leftStick.ReadValue();
            Vector3 planar = transform.forward * stick.y + transform.right * stick.x;
            if (planar.sqrMagnitude > 0f)
            {
                planar.y = 0f;
                move += planar.normalized * speed;
            }
        }

        if (move.sqrMagnitude > 0f)
        {
            move = move.normalized * speed * Time.deltaTime;
            if (body != null)
            {
                // CharacterController даёт «врезание»: скользит вдоль стен, не проходит сквозь них.
                body.Move(move);
            }
            else
            {
                transform.position += move;
            }
        }
        // Если есть коллайдер — слегка прижимаем камеру к поверхности (не проходим сквозь пол).
        if (body != null && enableCameraCollision)
        {
            body.Move(Vector3.down * 0.05f * Time.deltaTime);
        }
    }

    /// <summary>Обновляет лучи обеих рук так, чтобы они сходились в точке прицеливания на поверхности.</summary>
    private void UpdateLaserPointers()
    {
        if (!enableLaserPointer)
        {
            if (leftLaser != null) leftLaser.enabled = false;
            if (rightLaser != null) rightLaser.enabled = false;
            return;
        }

        ComputeAimPoint();

        if (leftHandEnabled) DrawHandLaser(leftLaser, -1f, leftHandOffset, leftColor);
        else if (leftLaser != null) leftLaser.enabled = false;

        if (rightHandEnabled) DrawHandLaser(rightLaser, 1f, rightHandOffset, rightColor);
        else if (rightLaser != null) rightLaser.enabled = false;
    }

    /// <summary>Считает центральную точку прицеливания (куда направлен корпус оператора).</summary>
    private void ComputeAimPoint()
    {
        Vector3 center = transform.position + transform.up * 0.1f;
        aimPoint = center + transform.forward * laserLength;
        aimHitSurface = false;

        Ray ray = new Ray(center, transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, laserLength, laserLayers, QueryTriggerInteraction.Ignore))
        {
            aimPoint = hit.point;
            aimHitSurface = true;
        }
        else
        {
            Plane workPlane = new Plane(Vector3.up, Vector3.zero);
            if (workPlane.Raycast(ray, out float planeDistance) && planeDistance >= 0f && planeDistance <= laserLength)
            {
                aimPoint = center + transform.forward * planeDistance;
                aimHitSurface = true;
            }
        }
    }

    /// <summary>Рисует луч от руки к цели; при близкой цели угол луча больше (сходимость).</summary>
    private void DrawHandLaser(LineRenderer laser, float side, float offset, Color color)
    {
        if (laser == null) return;

        // Рука смещена вбок и немного вниз от центра камеры.
        Vector3 hand = transform.position
                       + transform.right * (side * offset)
                       - transform.up * laserDownOffset;

        Vector3 endpoint;
        if (aimHitSurface && Vector3.Distance(hand, aimPoint) > 0.001f)
        {
            // Луч идёт от руки к целевой точке поверхности.
            Vector3 dir = (aimPoint - hand).normalized;
            Ray ray = new Ray(hand, dir);
            float maxDist = Mathf.Max(Vector3.Distance(hand, aimPoint), 0.01f);
            if (Physics.Raycast(ray, out RaycastHit hit, maxDist, laserLayers, QueryTriggerInteraction.Ignore))
            {
                endpoint = hit.point;
            }
            else
            {
                endpoint = aimPoint;
            }
        }
        else
        {
            endpoint = hand + transform.forward * laserLength;
        }

        laser.startColor = color;
        laser.endColor = new Color(color.r, color.g, color.b, 0.15f);
        laser.enabled = true;
        laser.SetPosition(0, hand);
        laser.SetPosition(1, endpoint);
    }

    /// <summary>Применяет функции рук по нажатию ЛКМ: левая рука задаёт позицию, правая — ориентацию.</summary>
    private void HandleClickActions()
    {
        bool currentlyPressed = IsMouseButtonPressed(0);
        bool clicked = currentlyPressed && !primaryButtonWasPressed;
        primaryButtonWasPressed = currentlyPressed;
        if (!clicked) return;

        RobotController selectedRobot = aimHitSurface
            ? FindPreferredRobotController(FindAnyRobotTransformNearAim())
            : null;
        if (selectedRobot == null)
        {
            selectedRobot = FindRobotNearRay(transform.position, transform.forward, laserLength);
        }
        if (selectedRobot == null)
        {
            RobotController[] robots = Object.FindObjectsByType<RobotController>(FindObjectsInactive.Include);
            if (robots.Length > 0) selectedRobot = robots[0];
        }

        if (selectedRobot == null) return;

        selectedRobot.SetActive(true);

        Vector3 posTarget = selectedRobot.tcp != null
            ? selectedRobot.tcp.position
            : selectedRobot.transform.position;

        // Левая рука (красная) — позиция TCP.
        if (leftHandEnabled && aimHitSurface)
        {
            posTarget = aimPoint;
        }

        Quaternion rotTarget = Quaternion.LookRotation(transform.forward, Vector3.up);
        // Правая рука (зелёная) — ориентация TCP (куда смотрит инструмент).
        if (rightHandEnabled)
        {
            Vector3 orientPoint = aimHitSurface ? aimPoint : transform.position + transform.forward * laserLength;
            Vector3 lookDir = (orientPoint - selectedRobot.tcp.position).normalized;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                rotTarget = Quaternion.LookRotation(lookDir, Vector3.up);
            }
        }

        selectedRobot.SetTarget(posTarget, rotTarget);
        Debug.Log($"[DesktopTeleoperation] Target pos={posTarget} rot={rotTarget.eulerAngles} assigned to {selectedRobot.name} (leftHand={leftHandEnabled}, rightHand={rightHandEnabled}).");
    }

    /// <summary>Циклически выбирает следующего робота в сцене (клавиша F).</summary>
    private void SelectNextRobot()
    {
        RobotController[] robots = Object.FindObjectsByType<RobotController>(FindObjectsInactive.Include);
        if (robots == null || robots.Length == 0) return;

        // Ищем текущего «активного» (с подсветкой) как точку отсчёта.
        int activeIdx = -1;
        for (int i = 0; i < robots.Length; i++)
        {
            if (robots[i] != null && robots[i].isActive)
            {
                activeIdx = i;
                break;
            }
        }

        int next = (activeIdx + 1) % robots.Length;
        for (int i = 0; i < robots.Length; i++)
        {
            if (robots[i] != null) robots[i].SetActive(i == next);
        }

        if (robots[next] != null)
            Debug.Log("[FreeFlyCamera] Активный робот: " + robots[next].robotName);
    }

    private Transform FindAnyRobotTransformNearAim()
    {
        RobotController c = FindRobotNearRay(transform.position, transform.forward, laserLength);
        return c != null ? c.transform : null;
    }

    private static RobotController FindRobotNearRay(Vector3 origin, Vector3 direction, float maxDistance)
    {
        RobotController closestRobot = null;
        float closestRayDistance = float.PositiveInfinity;
        foreach (RobotController candidate in Object.FindObjectsByType<RobotController>(FindObjectsInactive.Include))
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
        if (source == null) return null;
        SCARAController scara = source.GetComponentInParent<SCARAController>();
        if (scara != null) return scara;

        SixAxisController sixAxis = source.GetComponentInParent<SixAxisController>();
        if (sixAxis != null) return sixAxis;

        return source.GetComponentInParent<RobotController>();
    }
}
