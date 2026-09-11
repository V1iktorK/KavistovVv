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
///  4) CAPS LOCK — переключение ВИДИМОГО КУРСОРА (режим работы с UI): курсор виден и
///     свободен (можно нажимать кнопки панелей). Клик по UI курсор НЕ прячет;
///     клик по рабочему пространству (миру) — возврат к захваченному курсору
///     (положение «до нажатия»).
///  5) G — ФОНАРИК (spot-свет на камере), включение/выключение.
///  6) Управление геймпадом (стики) активно, когда геймпад подключён и курсор
///     захвачен (телеоперация); тумблер — R3 (нажатие правого стика).
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

    [Header("Gamepad control (toggle R3)")]
    public float gamepadLookSensitivity = 1.5f;

    [Header("Flashlight (G)")]
    public bool enableFlashlight = true;
    public bool flashlightEnabled = false;
    public float flashlightIntensity = 11000f;
    public float flashlightRange = 20f;
    public float flashlightAngle = 150f;     // внешний конус
    public float flashlightInnerAngle = 140f; // внутренний конус

    private float yaw;
    private float pitch;
    private LineRenderer leftLaser;
    private LineRenderer rightLaser;
    private bool primaryButtonWasPressed;
    private CharacterController body;
    private bool gamepadMove;
    private Light flashlight;
    private AimIndicator aimIndicator;
    private TrajectoryPlannerController plannerController;
    private TrajectoryFlowController flowController;

    /// <summary>Подавить старую прямую телеоперацию кликом (движение — только через поток выбора).</summary>
    public bool suppressDirectTeleop = true;

    public Vector3 AimPointPublic => aimPoint;
    public bool AimHitPublic => aimHitSurface;

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
                case KeyCode.CapsLock: return Keyboard.current.capsLockKey.wasPressedThisFrame;
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
                case KeyCode.CapsLock: return Input.GetKeyDown(KeyCode.CapsLock);
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

    private bool IsPointerOverUI()
    {
        try
        {
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return true;
            }
        }
        catch
        {
        }
        return false;
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

        leftLaser = CreateLaser("Laser_LeftHand");
        rightLaser = CreateLaser("Laser_RightHand");

        CreateFlashlight();
        aimIndicator = gameObject.AddComponent<AimIndicator>(); // оракул достижимости (E1/E5)
        plannerController = gameObject.AddComponent<TrajectoryPlannerController>(); // планировщик (P/1-2-3/F9)
        flowController = gameObject.AddComponent<TrajectoryFlowController>();       // поток «два лазера»

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

    /// <summary>
    /// Фонарик на камере (G). Только источник света, без модельки.
    /// </summary>
    private void CreateFlashlight()
    {
        if (!enableFlashlight) return;

        GameObject holder = new GameObject("Flashlight");
        holder.transform.SetParent(transform, false);
        holder.transform.localPosition = new Vector3(0f, -0.05f, 0.05f);
        holder.transform.localRotation = Quaternion.identity;

        var light = holder.AddComponent<Light>();
        light.type = LightType.Spot;
        light.intensity = flashlightIntensity;
        light.range = flashlightRange;
        light.spotAngle = flashlightAngle;
        light.innerSpotAngle = flashlightInnerAngle;
        light.colorTemperature = 5200f;
        light.useColorTemperature = true;
        light.shadows = LightShadows.Soft;
        light.enabled = false; // выключен до первого G
        flashlight = light;
    }

    /// <summary>Переключить фонарик (G).</summary>
    private void ToggleFlashlight()
    {
        if (flashlight == null) return;
        flashlightEnabled = !flashlightEnabled;
        flashlight.enabled = flashlightEnabled;
        Debug.Log("[FreeFlyCamera] Фонарик: " + (flashlightEnabled ? "ВКЛ" : "ВЫКЛ"));
    }

    /// <summary>
    /// Создаёт LineRenderer на ОТДЕЛЬНОМ дочернем GameObject.
    /// ВАЖНО: Unity 6 не позволяет добавить второй LineRenderer на тот же GameObject
    /// (AddComponent возвращает null) — поэтому у каждой руки свой носитель.
    /// </summary>
    private LineRenderer CreateLaser(string childName)
    {
        var holder = new GameObject(childName);
        holder.transform.SetParent(transform, false);
        holder.transform.localPosition = Vector3.zero;
        holder.transform.localRotation = Quaternion.identity;

        var lr = holder.AddComponent<LineRenderer>();
        if (lr == null)
        {
            Debug.LogError("[FreeFlyCamera] Не удалось создать LineRenderer на '" + childName + "'.");
            return null;
        }

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

        // KOMPAS-UI самосоздаётся: если в сцене нет менеджера (объект удалили) —
        // создаём (канвас/панели строятся автоматически, дубликаты гасятся).
        EnsureKompasUi();

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

    private void EnsureKompasUi()
    {
        if (KompasUI.KompasUIManager.Instance != null) return;
        if (Object.FindFirstObjectByType<KompasUI.KompasUIManager>() != null) return;
        var go = new GameObject("KOMPAS_UI");
        go.AddComponent<KompasUI.KompasUIManager>();
        Debug.Log("[FreeFlyCamera] KOMPAS-UI создан автоматически (менеджера в сцене не было).");
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

        // CAPS LOCK — переключение РЕЖИМА UI: курсор виден + панели KOMPAS показаны
        // (телеоперация: курсор захвачен + панели скрыты).
        // Клик по UI курсор не прячет; клик по миру — возврат к телеоперации.
        if (IsKeyPressed(KeyCode.CapsLock))
        {
            ToggleUiMode();
        }

        // G — фонарик (toggle).
        if (IsKeyPressed(KeyCode.G))
        {
            ToggleFlashlight();
        }

        // Геймпад: стики активны только в телеоперации (курсор захвачен).
        // R3 (нажатие правого стика) включает/выключает геймпад-управление.
        if (gamepadMode && Gamepad.current != null && Gamepad.current.rightStickButton.wasPressedThisFrame)
        {
            gamepadMove = !gamepadMove;
            Debug.Log("[FreeFlyCamera] Управление геймпадом (стик): " + (gamepadMove ? "ВКЛ" : "ВЫКЛ"));
        }

        // Переключение рук-лазеров (toggle) клавишами Z / X.
        if (IsKeyPressed(KeyCode.Z)) leftHandEnabled = !leftHandEnabled;
        if (IsKeyPressed(KeyCode.X)) rightHandEnabled = !rightHandEnabled;

        // Выбор робота по F (или LB на геймпаде): контекстный — см. SelectRobotContextual.
        if (IsKeyPressed(KeyCode.F)) SelectRobotContextual();
        else if (gamepadMode && Gamepad.current != null && Gamepad.current.leftShoulder.wasPressedThisFrame)
            SelectRobotContextual();

        bool lmbDown = IsMouseButtonDownThisFrame(0);
        bool rmbDown = IsMouseButtonDownThisFrame(1);
        bool overUI = IsPointerOverUI();

        // Esc освобождает курсор и показывает панели (как TAB в сторону UI).
        bool escDown = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                       || (Keyboard.current == null && TryLegacyKeyDown(KeyCode.Escape));
        if (escDown && Cursor.lockState == CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            KompasUI.KompasUIManager.SetUiVisible(true);
        }

        // Клик по окну Game при свободном курсоре → захват мыши (FPS-режим).
        // Если клик пришёлся на UI-канвас — не захватываем (работают кнопки).
        bool justCaptured = false;
        if (Cursor.lockState != CursorLockMode.Locked && (lmbDown || rmbDown) && !overUI && Application.isFocused)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            // UI НЕ скрываем: панели пропадают только по Caps Lock.
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

        // Поворот геймпадом (правый стик): только в телеоперации (курсор захвачен).
        bool teleopMode = Cursor.lockState == CursorLockMode.Locked;
        if (teleopMode && gamepadMove && gamepadMode && Gamepad.current != null)
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

        // Геймпад: левый стик — горизонтальное движение (только телеоперация).
        if (teleopMode && gamepadMove && gamepadMode && Gamepad.current != null)
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
        // Гравитации/прижима к полу нет — камера остаётся на своей высоте.
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

        // Онлайн-вердикт по точке прицела (зелёный/жёлтый/красный маркер).
        if (aimIndicator != null)
            aimIndicator.UpdateAim(aimPoint, aimHitSurface);

        // Планировщик траекторий (P — план, 1/2/3 — исполнить, F9 — автотест).
        if (plannerController != null)
            plannerController.UpdateAim(aimPoint, aimHitSurface);

        // Поток выбора «два лазера»: красный (ЛКМ) — точка, зелёный (ПКМ) — траектория/фантом.
        if (flowController != null)
        {
            bool redConfirm = Mouse.current != null
                ? Mouse.current.leftButton.wasPressedThisFrame
                : Input.GetMouseButtonDown(0);
            bool greenConfirm = Mouse.current != null
                ? Mouse.current.rightButton.wasPressedThisFrame
                : Input.GetMouseButtonDown(1);
            bool cancel = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            flowController.UpdateAim(aimPoint, aimHitSurface, redConfirm, greenConfirm, cancel);
        }

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

    /// <summary>
    /// ЛКМ ведёт ВЫБРАННОГО (активного) робота к точке прицеливания.
    /// Если активного нет — выбирается робот под прицелом.
    /// Лазеры (Z/X) — только визуальные указатели, от цвета лазера управление не зависит.
    /// </summary>
    private void HandleClickActions()
    {
        bool currentlyPressed = IsMouseButtonPressed(0);
        bool clicked = currentlyPressed && !primaryButtonWasPressed;
        primaryButtonWasPressed = currentlyPressed;
        if (!clicked) return;

        // Клик по кнопке UI — не телеоперация.
        if (IsPointerOverUI()) return;

        // 0) Клик ПО РОБОТУ (прицел над его моделью/коллайдером) — просто выбираем его основным.
        RobotController hitRobot = FindRobotUnderAim();
        if (hitRobot != null)
        {
            SelectRobotAsPrimary(hitRobot);
            return;
        }

        // 1) Иначе — активный (выбранный F / деревом) робот.
        RobotController selectedRobot = FindActiveRobot();

        // 2) Затем — робот, на которого смотрит прицел.
        if (selectedRobot == null)
        {
            selectedRobot = aimHitSurface
                ? FindPreferredRobotController(FindAnyRobotTransformNearAim())
                : null;
        }
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

        SelectRobotAsPrimary(selectedRobot);

        Vector3 posTarget = aimHitSurface
            ? aimPoint
            : GetRobotTcpPosition(selectedRobot);

        selectedRobot.SetTarget(posTarget);
        Debug.Log($"[DesktopTeleoperation] Target pos={posTarget} assigned to {selectedRobot.name}.");
    }

    /// <summary>Делает робота основным (единственным активным).</summary>
    private static void SelectRobotAsPrimary(RobotController robot)
    {
        if (robot == null) return;
        RobotController[] all = Object.FindObjectsByType<RobotController>(FindObjectsInactive.Include);
        foreach (RobotController rc in all)
        {
            if (rc != null) rc.SetActive(rc == robot);
        }
    }

    /// <summary>Робот, чья модель/коллайдер находится под прицелом (по попаданию луча).</summary>
    private RobotController FindRobotUnderAim()
    {
        Camera cam = GetComponent<Camera>();
        if (cam == null) return null;

        Ray ray = new Ray(transform.position + transform.up * 0.1f, transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, laserLength,
                laserLayers, QueryTriggerInteraction.Ignore))
        {
            RobotController rc = FindPreferredRobotController(hit.collider.transform);
            if (rc != null) return rc;
        }
        return null;
    }

    private static RobotController FindActiveRobot()
    {
        RobotController[] robots = Object.FindObjectsByType<RobotController>(FindObjectsInactive.Include);
        foreach (RobotController rc in robots)
        {
            if (rc != null && rc.isActive) return rc;
        }
        return null;
    }

    /// <summary>
    /// Безопасная точка TCP робота: tcp → endEffector → корень.
    /// Не падает, если поле tcp не назначено в инспекторе.
    /// </summary>
    private static Vector3 GetRobotTcpPosition(RobotController robot)
    {
        if (robot == null) return Vector3.zero;
        if (robot.tcp != null) return robot.tcp.position;
        if (robot.endEffector != null) return robot.endEffector.position;
        return robot.transform.position;
    }

    /// <summary>
    /// F: контекстный выбор робота.
    ///   * робот ПОД ПРИЦЕЛОМ → он становится основным (и запоминается в истории);
    ///   * робота под прицелом НЕТ и основной выбран → переключение на
    ///     «последнего выбранного» (история, при повторе F — возврат);
    ///   * ничего не выбрано → ближайший к прицелу / первый в сцене.
    /// </summary>
    private void SelectRobotContextual()
    {
        RobotController[] robots = Object.FindObjectsByType<RobotController>(FindObjectsInactive.Include);
        if (robots == null || robots.Length == 0) return;

        RobotController aimed = FindRobotUnderAim();
        RobotController active = FindActiveRobot();

        if (aimed != null)
        {
            // Робот в прицеле — выбираем его; запоминаем предыдущего основного.
            PushRobotHistory(active);
            SelectRobotAsPrimary(aimed);
            LogRobotSelected(aimed);
            return;
        }

        if (active != null)
        {
            // В прицеле пусто: переключаемся на «последнего выбранного».
            RobotController previous = PeekRobotHistory(active);
            if (previous != null)
            {
                PushRobotHistory(active);
                SelectRobotAsPrimary(previous);
                LogRobotSelected(previous);
                return;
            }
            // Истории ещё нет — циклический проход по списку (запасной вариант).
            RobotController next = null;
            for (int i = 0; i < robots.Length; i++)
            {
                if (robots[i] == active)
                {
                    for (int k = 1; k <= robots.Length; k++)
                    {
                        RobotController cand = robots[(i + k) % robots.Length];
                        if (cand != null && cand != active) { next = cand; break; }
                    }
                    break;
                }
            }
            if (next != null)
            {
                PushRobotHistory(active);
                SelectRobotAsPrimary(next);
                LogRobotSelected(next);
            }
            return;
        }

        // Активного нет: ближайший к прицелу или первый.
        RobotController fallback = FindRobotNearRay(transform.position, transform.forward, laserLength);
        if (fallback == null) fallback = robots[0];
        PushRobotHistory(null);
        SelectRobotAsPrimary(fallback);
        LogRobotSelected(fallback);
    }

    /// <summary>История выбранных роботов: [0] — предыдущий основной (для F-переключения).</summary>
    private readonly System.Collections.Generic.List<RobotController> robotHistory =
        new System.Collections.Generic.List<RobotController>();

    private void PushRobotHistory(RobotController robot)
    {
        if (robot == null) return;
        robotHistory.Remove(robot);
        robotHistory.Insert(0, robot);
        while (robotHistory.Count > 4) robotHistory.RemoveAt(robotHistory.Count - 1);
    }

    /// <summary>Возвращает предыдущего основного робота (не equal текущему active).</summary>
    private RobotController PeekRobotHistory(RobotController active)
    {
        if (robotHistory.Count == 0) return null;
        for (int i = 0; i < robotHistory.Count; i++)
        {
            if (robotHistory[i] != null && robotHistory[i] != active) return robotHistory[i];
        }
        return null;
    }

    private static void LogRobotSelected(RobotController robot)
    {
        if (robot != null)
            Debug.Log("[FreeFlyCamera] Основной робот: " + robot.robotName);
    }

    /// <summary>
    /// CAPS LOCK: режим UI (курсор виден + панели KOMPAS видны) ⟷ телеоперация
    /// (курсор захвачен, панели скрыты). UI в Screen Space Overlay — виден
    /// всегда и не «режется» геометрией сцены.
    /// </summary>
    private void ToggleUiMode()
    {
        bool uiMode = Cursor.lockState == CursorLockMode.Locked;
        if (uiMode)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Debug.Log("[FreeFlyCamera] Режим UI: курсор + панели (CapsLock/клик по миру — обратно).");
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Debug.Log("[FreeFlyCamera] Телеоперация: курсор захвачен, панели скрыты.");
        }
        KompasUI.KompasUIManager.SetUiVisible(uiMode);
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
