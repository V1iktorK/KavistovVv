using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;                  // LightUnit (единицы света) для фонарика

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
///  4) TAB — переключение ВИДИМОГО КУРСОРА (режим работы с UI): курсор виден и
///     свободен (можно нажимать кнопки панелей). Клик по UI курсор НЕ прячет;
///     клик по рабочему пространству (миру) — возврат к захваченному курсору
///     (положение «до нажатия»). Раньше это был CAPS LOCK — бинд перенесён на TAB
///     (CAPS LOCK больше ничего не вызывает).
///  5) G — ФОНАРИК (spot-свет на камере), включение/выключение. Все параметры света
///     (углы конуса, поток в люменах, дальность, радиус, температура) — в инспекторе.
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

    [Header("Глубина шарика прицела (колесо мыши)")]
    [Tooltip("Шаг перемещения шарика вдоль луча за один щелчок колеса мыши, юниты.")]
    public float scrollStep = 0.08f;
    [Tooltip("Максимальная дистанция от оператора до шарика, юниты (дальше колесо его не ведёт).")]
    public float maxDistance = 20f;
    [Tooltip("Минимальная дистанция от оператора до шарика, юниты (ближе колесо его не подпускает).")]
    public float minDistance = 0.25f;
    [Tooltip("Прилипание к поверхностям: прокрутка вперёд останавливает шарик на первой поверхности " +
             "по лучу и сквозь неё не пропускает. Выключено — шарик проходит сквозь геометрию до maxDistance.")]
    public bool stickyToSurface = true;
    [Tooltip("Слои, к которым прилипает шарик (и по которым средняя кнопка ищет ближайшую поверхность). " +
             "Пустое значение = слои лучей (laserLayers).")]
    public LayerMask surfaceLayer = Physics.DefaultRaycastLayers;
    [Tooltip("Плавность хода шарика по колесу, 1/с: больше — резче, меньше — мягче.")]
    public float scrollSmoothSpeed = 16f;
    [Tooltip("Допуск «шарик на поверхности», юниты: ближе этого шарик считается прилипшим к ней " +
             "(визуал «сплющен»; смещение целевой точки задаётся потоком: toolOffset, по умолчанию 0).")]
    public float surfaceStickTolerance = 0.005f;

    [Header("Camera collision (bump, not pass-through)")]
    public bool enableCameraCollision = true;
    public float bodyRadius = 0.25f;
    public float bodyHeight = 1.6f;
    public float bodySkin = 0.02f;

    [Header("Gamepad control (toggle R3)")]
    public float gamepadLookSensitivity = 1.5f;

    [Header("Фонарик (G)")]
    [Tooltip("Мастер-выключатель: без него объект Flashlight не создаётся и G ничего не делает.")]
    public bool enableFlashlight = true;
    [Tooltip("Состояние фонарика. Переключается клавишей G; значение поля — стартовое " +
             "состояние в PlayMode (в сцене 0 = на старте выключен, как и было).")]
    public bool flashlightEnabled = false;
    [Tooltip("Сила света, ЛЮМЕНЫ (световой поток). Инженерный ориентир для рабочей зоны: " +
             "1500 лм в конусе 60° дают ≈200 лк на 3 м и ≈450 лк на 2 м. " +
             "Больше ~3000 лм в узком конусе начинает «выжигать» поверхность и блики.")]
    public float flashlightIntensity = 1500f;
    [Tooltip("Дальность действия, юниты (м). Ход луча: 12–15 достаточно для рабочей зоны " +
             "1.5–5 м; больше — свет засвечивает весь ангар.")]
    public float flashlightRange = 14f;
    [Tooltip("ВНЕШНИЙ угол конуса, градусы. Фонарик — 55–65°; 103° (было) слишком широко, свет размывается.")]
    public float flashlightAngle = 60f;
    [Tooltip("ВНУТРЕННИЙ угол конуса (ядро луча), градусы. Держится меньше внешнего: " +
             "30° даёт мягкую границу без резкой кромки. Значение автоматически зажимается " +
             "до 0.95 от внешнего, даже если в инспекторе задано больше.")]
    public float flashlightInnerAngle = 30f;
    [Tooltip("Радиус источника света, юниты. Маленький (0.025) — «точечный» фонарик: " +
             "резкая тень с лёгким смягчением, как у настоящей лампы.")]
    public float flashlightRadius = 0.025f;
    [Tooltip("Цветовая температура, Кельвины. 4500 K — нейтрально-тёплый инженерный свет: " +
             "цвета материалов не искажаются, глаза в VR меньше устают, чем на 5200 K.")]
    public float flashlightColorTemperature = 4500f;
    [Tooltip("Мягкие тени от фонарика.")]
    public bool flashlightSoftShadows = true;
    [Tooltip("Плавное зажигание/затухание фонарика, с. 0 (по умолчанию) — включается мгновенно, " +
             "как было: значения углов/люменов в инспекторе остаются ровно теми, что заданы. " +
             "0.15 — мягкое появление света без «щелчка» (яркость плавно идёт к заданной).")]
    public float flashlightFadeTime = 0f;

    private float yaw;
    private float pitch;
    private LineRenderer leftLaser;
    private LineRenderer rightLaser;
    private bool primaryButtonWasPressed;
    private CharacterController body;
    private bool gamepadMove;
    private Light flashlight;
    // Последние применённые значения фонарика (живое применение правок инспектора в PlayMode).
    private float lastFlashIntensity, lastFlashRange, lastFlashOuter, lastFlashInner;
    private float lastFlashRadius, lastFlashTemp;
    private bool lastFlashSoft, lastFlashEnabled, lastFlashMaster;
    // Плавность фонарика (ТЗ «без резких скачков»): текущая «яркость» 0..1. При
    // flashlightFadeTime = 0 всегда 1/0 — поведение прежнее, значения не искажаются.
    private float flashFade = 1f;
    // Защёлка фронта клавиши G: одно физическое нажатие = ровно одно переключение (см. ToggleEdge).
    private bool gKeyWasDown;
    // Кадр последнего переключения фонарика — второе срабатывание в том же кадре игнорируется.
    private int lastFlashToggleFrame = -1;
    private AimIndicator aimIndicator;
    private TrajectoryPlannerController plannerController;
    private TrajectoryFlowController flowController;
    private RobotController selectedRobot;

    /// <summary>Подавить старую прямую телеоперацию кликом (движение — только через поток выбора).</summary>
    public bool suppressDirectTeleop = true;

    public Vector3 AimPointPublic => aimPoint;
    public bool AimHitPublic => aimHitSurface;
    /// <summary>Нормаль поверхности в точке прицела (для смещения TCP и выравнивания «пятака»).</summary>
    public Vector3 AimNormalPublic => aimNormal;
    /// <summary>Прицел попал в РЕАЛЬНУЮ геометрию (RaycastHit): нормаль поверхности настоящая.</summary>
    public bool AimOnSurfacePublic => aimOnRealSurface;

    /// <summary>Глубина шарика вдоль луча (от оператора), юниты — «колесо мыши».</summary>
    public float AimBallDepth => ballDepth;
    /// <summary>Шарик «прилип»: стоит РОВНО на поверхности (нормаль поверхности настоящая).</summary>
    public bool AimBallOnSurface => ballOnSurface;
    /// <summary>У шарика есть опорная поверхность под лучом (реальная геометрия или рабочая плоскость y = 0).</summary>
    public bool AimBallHasSurface => ballHasBase;
    /// <summary>Шарик отведён колесом от поверхности (идёт «в воздухе» к оператору или за поверхность).</summary>
    public bool AimBallDetached => ballHasBase && !ballOnSurface;

    // Точка прицеливания = позиция ШАРИКА (куда смотрит оператор и куда сходятся лучи).
    private Vector3 aimPoint;
    private bool aimHitSurface;
    private Vector3 aimNormal = Vector3.up;
    private bool aimOnRealSurface;

    // --- глубина шарика (колесо мыши) ---
    // Опорная точка — первая поверхность по лучу (или рабочая плоскость y = 0). Шарик стоит на ней,
    // пока колесо не трогали (поведение прицела прежнее). Колесо отводит шарик ВДОЛЬ ЛУЧА:
    // depthOffset > 0 — к оператору («назад»), < 0 — за поверхность (только при stickyToSurface = false).
    private float ballDepth;              // фактическая дистанция шарика от центра камеры
    private float depthOffset;            // текущий сдвиг от опорной точки
    private float depthOffsetTarget;      // цель сдвига (накопитель колеса)
    private float freeDepth;              // абсолютная глубина, когда луч ни во что не попал (пустота)
    private float freeDepthTarget;
    private float injectedScroll;         // прокрутка «извне» (VR-контроллеры, автотесты), щелчки
    private bool ballDepthInit;
    private bool ballHasBase;             // у шарика есть опорная точка
    private bool ballHadBase;             // была ли опора в прошлом кадре (переходы «поверхность ↔ пустота»)
    private bool ballOnSurface;           // шарик стоит на поверхности (прилип)
    private float ballSurfaceDist = -1f;  // дистанция до поверхности, к которой липнет шарик (surfaceLayer)
    private bool ballSurfaceHit;

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
                case KeyCode.Tab: return Keyboard.current.tabKey.wasPressedThisFrame;
                case KeyCode.Return: return Keyboard.current.enterKey.wasPressedThisFrame;
                case KeyCode.KeypadEnter: return Keyboard.current.numpadEnterKey.wasPressedThisFrame;
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
                case KeyCode.Tab: return Input.GetKeyDown(KeyCode.Tab);
                case KeyCode.Return: return Input.GetKeyDown(KeyCode.Return);
                case KeyCode.KeypadEnter: return Input.GetKeyDown(KeyCode.KeypadEnter);
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

    /// <summary>
    /// Клавиша нажата ИМЕННО в этом кадре (одиночное действие: подтвердить/выбрать).
    /// Отдельно от <see cref="IsKeyPressed"/> (там удержание нужно для движения точки)
    /// и с тем же откатом на legacy Input.
    /// </summary>
    private bool IsKeyDownThisFrame(KeyCode code)
    {
        if (Keyboard.current != null)
        {
            switch (code)
            {
                case KeyCode.W: return Keyboard.current.wKey.wasPressedThisFrame;
                case KeyCode.A: return Keyboard.current.aKey.wasPressedThisFrame;
                case KeyCode.S: return Keyboard.current.sKey.wasPressedThisFrame;
                case KeyCode.D: return Keyboard.current.dKey.wasPressedThisFrame;
                case KeyCode.Q: return Keyboard.current.qKey.wasPressedThisFrame;
                case KeyCode.E: return Keyboard.current.eKey.wasPressedThisFrame;
            }
        }
        return TryLegacyKeyDown(code);
    }

    /// <summary>
    /// СОСТОЯНИЕ клавиши (удержание) — надёжный источник для переключателей.
    /// В отличие от `wasPressedThisFrame` (это «нажатие с момента последнего ОБНОВЛЕНИЯ ВВОДА»,
    /// а не «в этом кадре»), состояние не повторяется в соседних кадрах.
    /// </summary>
    private bool IsKeyHeld(KeyCode code)
    {
        if (Keyboard.current != null)
        {
            switch (code)
            {
                case KeyCode.G: return Keyboard.current.gKey.isPressed;
                case KeyCode.Z: return Keyboard.current.zKey.isPressed;
                case KeyCode.X: return Keyboard.current.xKey.isPressed;
                case KeyCode.F: return Keyboard.current.fKey.isPressed;
                case KeyCode.Tab: return Keyboard.current.tabKey.isPressed;
            }
        }
        try
        {
            switch (code)
            {
                case KeyCode.G: return Input.GetKey(KeyCode.G);
                case KeyCode.Z: return Input.GetKey(KeyCode.Z);
                case KeyCode.X: return Input.GetKey(KeyCode.X);
                case KeyCode.F: return Input.GetKey(KeyCode.F);
                case KeyCode.Tab: return Input.GetKey(KeyCode.Tab);
            }
        }
        catch
        {
        }
        return false;
    }

    /// <summary>
    /// ФРОНТ клавиши с защёлкой: одно ФИЗИЧЕСКОЕ нажатие = РОВНО одно действие.
    ///
    /// Почему не `wasPressedThisFrame` (причина «фонарик включается со второго нажатия»):
    /// в Input System 1.20 это условие `InputUpdate.s_UpdateStepCount == m_UpdateCountLastPressed`,
    /// то есть «нажатие с момента последнего обновления ввода», а не «в этом кадре». Если между
    /// двумя кадрами Unity обновления ввода не было (в редакторе это штатная ситуация: первый кадр
    /// после фокуса окна, тяжёлый кадр, только что стартовавший PlayMode), условие истинно ДВА
    /// кадра подряд: фонарик включался и в том же мгновении гас, а следующее нажатие снова его
    /// включало — ровно «вкл → выкл → вкл».
    /// Собственный фронт по СОСТОЯНИЮ клавиши даёт ровно одно срабатывание: повтор возможен
    /// только после ОТПУСКАНИЯ клавиши, поэтому дребезг и автоповтор клавиатуры не влияют.
    /// </summary>
    private bool KeyToggleEdge(KeyCode code, ref bool wasDown)
    {
        bool down = IsKeyHeld(code);
        bool edge = down && !wasDown;
        wasDown = down;
        return edge;
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

    /// <summary>Нажатие «в этом кадре» через legacy Input (откат, если клавиатуру не отдал Input System).</summary>
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
        aimIndicator = gameObject.AddComponent<AimIndicator>(); // оракул достижимости
        flowController = gameObject.AddComponent<TrajectoryFlowController>(); // поток «два лазера»

        // Оракул (шарик прицела) проверяет ТУ ЖЕ точку, что зафиксирует поток: точка на
        // поверхности + toolOffset вдоль нормали (по умолчанию toolOffset = 0 — то есть
        // ровно hit.point). Значение берём из потока — один источник правды.
        aimIndicator.toolOffset = flowController.toolOffset;
        aimIndicator.offsetAlongNormal = flowController.offsetMode == TrajectoryFlowController.ToolOffsetMode.SurfaceNormal;

        // Ни один робот не должен двигаться до подтверждения точки.
        foreach (RobotController rc in Object.FindObjectsByType<RobotController>(FindObjectsInactive.Include))
            if (rc != null) rc.ClearTarget();

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
    ///
    /// ВСЕ параметры — сериализуемые поля (см. раздел «Фонарик (G)» выше): их можно
    /// менять в инспекторе `Main Camera → FreeFlyCameraController` в PlayMode,
    /// без перекомпиляции — значения применяются на лету (см. UpdateFlashlightLive).
    ///
    /// Настройка под инженерный CAD в тёмном помещении (ТЗ сессии 13.09.2026):
    ///   * конус 30° / 60° — фонарик, а не «прожектор»: 103° размывали свет;
    ///   * 1500 лм — рабочая зона 1.5–5 м (≈450 лк на 2 м, ≈200 лк на 3 м) без «выжигания»;
    ///   * дальность 14 юнитов — свет не заливает весь ангар;
    ///   * радиус источника 0.025 — точечный свет с лёгким смягчением тени;
    ///   * 4500 K — нейтрально-тёплый инженерный свет (цвета материалов не врут).
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

        // Единица измерения — ЛЮМЕНЫ (световой поток): значение в инспекторе означает
        // то, что написано в подсказке поля, и не зависит от «spot reflector» HDRP.
        light.lightUnit = LightUnit.Lumen;
        light.enableSpotReflector = false;
        flashlight = light;                      // дальше всё ставит ApplyFlashlightSettings
        flashFade = 0f;                          // старт «погашен»; при fade = 0 ставится 1/0 сразу

        ApplyFlashlightSettings();
        CacheFlashlightFields();
        Debug.Log("[FreeFlyCamera] Фонарик настроен: " + light.spotAngle.ToString("0.#") + "°/" +
                  light.innerSpotAngle.ToString("0.#") + "° · " + light.intensity.ToString("0") +
                  " лм · дальность " + light.range.ToString("0.#") +
                  " · радиус " + light.shapeRadius.ToString("0.###") + " · " +
                  light.colorTemperature.ToString("0") + " K · тени " + light.shadows +
                  " · на старте " + (light.enabled ? "ВКЛ" : "ВЫКЛ"));
    }

    /// <summary>
    /// Применить сериализуемые параметры фонарика к компоненту Light. Вызывается при создании
    /// и КАЖДЫЙ раз, когда значение в инспекторе изменилось (`UpdateFlashlightLive`) —
    /// поэтому параметры крутятся в PlayMode без перекомпиляции и сразу видны в сцене.
    ///
    /// Инварианты (защита от «произвольных» чисел в инспекторе HDRP и от мерцания):
    ///   * внутренний угол ВСЕГДА ≤ 0.95 × внешнего (пара 38°/140° раньше ломала конус);
    ///   * интенсивность/дальность/радиус не отрицательны, дальность ≥ 0.1;
    ///   * температура в диапазоне 1000…20000 K, `useColorTemperature` включён;
    ///   * состояние `enabled` — из `flashlightEnabled` и мастер-флага `enableFlashlight`.
    /// </summary>
    public void ApplyFlashlightSettings()
    {
        if (flashlight == null) return;

        float outer = Mathf.Clamp(flashlightAngle, 1f, 179f);
        float inner = Mathf.Clamp(flashlightInnerAngle, 1f, Mathf.Max(1f, outer * 0.95f));

        bool want = enableFlashlight && flashlightEnabled;
        // Без сглаживания (flashlightFadeTime = 0, значение по умолчанию) яркость — ВСЕГДА ровно
        // flashlightIntensity: числа в инспекторе не искажаются ни на кадр, включён свет или нет
        // (состояние задаёт только `enabled`). Коэффициент появляется лишь при плавном зажигании.
        if (flashlightFadeTime <= 0.0001f) flashFade = want ? 1f : 0f;
        float k = flashlightFadeTime <= 0.0001f ? 1f : flashFade;

        flashlight.intensity = Mathf.Max(0f, flashlightIntensity) * k;
        flashlight.range = Mathf.Max(0.1f, flashlightRange);
        flashlight.spotAngle = outer;
        flashlight.innerSpotAngle = inner;
        flashlight.shapeRadius = Mathf.Max(0f, flashlightRadius);
        flashlight.colorTemperature = Mathf.Clamp(flashlightColorTemperature, 1000f, 20000f);
        flashlight.useColorTemperature = true;
        flashlight.shadows = flashlightSoftShadows ? LightShadows.Soft : LightShadows.None;
        flashlight.enabled = want && k > 0.001f;
    }

    /// <summary>
    /// Плавное зажигание/затухание (только при flashlightFadeTime > 0; по умолчанию выключено).
    /// Считается по unscaledDeltaTime и не трогает Light, пока яркость не изменилась, —
    /// ни мерцания, ни лишних пересчётов теней у HDRP.
    /// </summary>
    private void UpdateFlashlightFade()
    {
        if (flashlight == null || flashlightFadeTime <= 0.0001f) return;
        bool want = enableFlashlight && flashlightEnabled;
        float next = Mathf.MoveTowards(flashFade, want ? 1f : 0f, Time.unscaledDeltaTime / flashlightFadeTime);
        if (Mathf.Abs(next - flashFade) < 1e-4f) return;
        flashFade = next;
        flashlight.intensity = Mathf.Max(0f, flashlightIntensity) * flashFade;
        if (flashFade <= 0.001f) flashlight.enabled = false;
        else if (want) flashlight.enabled = true;
    }

    /// <summary>Снимок применённых значений — чтобы в кадре не дёргать Light без надобности.</summary>
    private void CacheFlashlightFields()
    {
        lastFlashIntensity = flashlightIntensity;
        lastFlashRange = flashlightRange;
        lastFlashOuter = flashlightAngle;
        lastFlashInner = flashlightInnerAngle;
        lastFlashRadius = flashlightRadius;
        lastFlashTemp = flashlightColorTemperature;
        lastFlashSoft = flashlightSoftShadows;
        lastFlashEnabled = flashlightEnabled;
        lastFlashMaster = enableFlashlight;
    }

    /// <summary>
    /// Живое применение параметров фонарика из инспектора (PlayMode, без перекомпиляции).
    /// Проверка — сравнение с последними применёнными значениями (без аллокаций и без
    /// обращения к Light, пока ничего не менялось).
    /// </summary>
    private void UpdateFlashlightLive()
    {
        if (flashlight == null) return;

        // ИНВАРИАНТ (антимерцание): источник света обязан соответствовать флагам.
        // Если состояние фонарика кто-то сбросил (другой код, выгрузка домена, правка
        // flashlightEnabled в инспекторе) — оно восстанавливается в том же кадре,
        // и «фонарик сам погас» больше не выглядит как второе нажатие.
        // Пока идёт ПЛАВНОЕ затухание (flashlightFadeTime > 0), светом распоряжается
        // UpdateFlashlightFade — инвариант ему не мешает.
        bool want = enableFlashlight && flashlightEnabled;
        bool fadingOut = !want && flashlightFadeTime > 0.0001f && flashFade > 0.001f;
        bool expectedOn = want && (flashlightFadeTime <= 0.0001f || flashFade > 0.001f);
        if (!fadingOut && flashlight.enabled != expectedOn)
        {
            if (flashlightFadeTime > 0.0001f) flashFade = expectedOn ? 1f : 0f;
            ApplyFlashlightSettings();
            CacheFlashlightFields();
            return;
        }

        if (Mathf.Approximately(lastFlashIntensity, flashlightIntensity) &&
            Mathf.Approximately(lastFlashRange, flashlightRange) &&
            Mathf.Approximately(lastFlashOuter, flashlightAngle) &&
            Mathf.Approximately(lastFlashInner, flashlightInnerAngle) &&
            Mathf.Approximately(lastFlashRadius, flashlightRadius) &&
            Mathf.Approximately(lastFlashTemp, flashlightColorTemperature) &&
            lastFlashSoft == flashlightSoftShadows &&
            lastFlashEnabled == flashlightEnabled &&
            lastFlashMaster == enableFlashlight) return;

        ApplyFlashlightSettings();
        CacheFlashlightFields();
        Debug.Log("[FreeFlyCamera] Фонарик: параметры применены на лету — " +
                  flashlight.spotAngle.ToString("0.#") + "°/" + flashlight.innerSpotAngle.ToString("0.#") +
                  "° · " + flashlight.intensity.ToString("0") + " лм · дальность " +
                  flashlight.range.ToString("0.#") + " · " + flashlight.colorTemperature.ToString("0") +
                  " K · тени " + flashlight.shadows + " · " + (flashlight.enabled ? "ВКЛ" : "ВЫКЛ"));
    }

    /// <summary>Переключить фонарик (G). Два срабатывания в одном кадре невозможны.</summary>
    private void ToggleFlashlight()
    {
        if (lastFlashToggleFrame == Time.frameCount) return;   // одно нажатие = одно переключение
        lastFlashToggleFrame = Time.frameCount;
        SetFlashlight(!flashlightEnabled);
    }

    /// <summary>
    /// Включить/выключить фонарик программно (VR-контроллер, автотесты, UI).
    /// G и это метод — один и тот же путь: состояние пишется в сериализуемое поле и в Light.
    /// Повторный вызов с тем же значением ничего не меняет и не пишет в лог (идемпотентно).
    /// </summary>
    public void SetFlashlight(bool on)
    {
        if (flashlight == null)
        {
            Debug.LogWarning("[FreeFlyCamera] Фонарика нет: enableFlashlight = " + enableFlashlight);
            return;
        }
        bool changed = flashlightEnabled != on || flashlight.enabled != on;
        flashlightEnabled = on;
        // Применяем СРАЗУ (а не только кэшируем): если оператор поправил параметры в инспекторе
        // и в том же кадре нажал G, правки не должны потеряться.
        ApplyFlashlightSettings();
        CacheFlashlightFields();
        if (changed) Debug.Log("[FreeFlyCamera] Фонарик: " + (on ? "ВКЛ" : "ВЫКЛ") +
                               " (первое нажатие включает, повторное выключает)");
    }

    /// <summary>
    /// Фонарик ВКЛЮЧЁН по состоянию (мастер-флаг + флаг состояния). При `flashlightFadeTime > 0`
    /// свет в первые миллисекунды ещё разгорается, но логическое состояние уже «включён» —
    /// поэтому проверки «нажатие включает фонарик» смотрят именно сюда.
    /// </summary>
    public bool FlashlightOn => flashlight != null && enableFlashlight && flashlightEnabled;
    /// <summary>Фонарик реально светит в этом кадре (Light.enabled и ненулевая яркость).</summary>
    public bool FlashlightLit => flashlight != null && flashlight.enabled && flashlight.intensity > 0.01f;
    /// <summary>Компонент Light фонарика (диагностика/VR-контур). null, если фонарика нет.</summary>
    public Light FlashlightLight => flashlight;

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
        lr.startWidth = 0.010f;   // тонкий, но заметный
        lr.endWidth = 0.004f;
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

        // KazistovVv-UI самосоздаётся: если в сцене нет менеджера (объект удалили) —
        // создаём (канвас/панели строятся автоматически, дубликаты гасятся).
        EnsureKazistovVvUi();

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

    private void EnsureKazistovVvUi()
    {
        if (KazistovVvUI.KazistovVvUIManager.Instance != null) return;
        if (Object.FindAnyObjectByType<KazistovVvUI.KazistovVvUIManager>() != null) return;
        var go = new GameObject("KazistovVv_UI");
        go.AddComponent<KazistovVvUI.KazistovVvUIManager>();
        Debug.Log("[FreeFlyCamera] KazistovVv-UI создан автоматически (менеджера в сцене не было).");
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

    /// <summary>
    /// НАВЕСТИ КАМЕРУ НА ОБЪЕКТ (ЭТАП 6, пункт «Фокус камеры на объекте» контекстного меню
    /// дерева). Меняются ТОЛЬКО позиция оператора и его yaw/pitch — те же две переменные,
    /// которыми управляет мышь, поэтому после фокуса обзор продолжает работать как обычно.
    /// Логика роботов, лазеров, потока и планировщика не затрагивается.
    /// </summary>
    public void FocusOn(Vector3 worldPoint, float distance = 2.4f)
    {
        Vector3 direction = transform.forward;
        if (direction.sqrMagnitude < 0.0001f) direction = Vector3.back;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) direction = Vector3.back;
        direction.Normalize();

        Vector3 position = worldPoint - direction * Mathf.Max(0.5f, distance);
        position.y = Mathf.Max(position.y + Mathf.Max(0.35f, distance * 0.35f), 0.25f);

        // CharacterController «держит» позицию: на время телепорта его надо выключить.
        bool hadBody = body != null;
        if (hadBody) body.enabled = false;
        transform.position = position;
        if (hadBody) body.enabled = true;

        Vector3 look = worldPoint - transform.position;
        if (look.sqrMagnitude > 0.00001f)
        {
            Vector3 euler = Quaternion.LookRotation(look.normalized, Vector3.up).eulerAngles;
            float x = euler.x > 180f ? euler.x - 360f : euler.x;
            pitch = Mathf.Clamp(x, -89f, 89f);
            yaw = euler.y;
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }
        aimPoint = worldPoint;
    }

    private bool IsGamepadActive()
    {
        // Геймпад считается активным, если он подключён (через new Input System).
        return Gamepad.current != null;
    }

    void Update()
    {
        bool gamepadMode = IsGamepadActive();

        // TAB — переключение РЕЖИМА UI: курсор виден + панели KazistovVv показаны
        // (телеоперация: курсор захвачен + панели скрыты).
        // Клик по UI курсор не прячет; клик по миру — возврат к телеоперации.
        // (Раньше здесь был CAPS LOCK — по ТЗ бинд перенесён на TAB; CAPS LOCK
        //  теперь не читается в проекте вообще.)
        // ЭТАП 11: если включена навигация по интерфейсу с клавиатуры, Tab ЗАНЯТ фокусом
        // (ТЗ: «Tab, стрелки, Enter»), поэтому режим UI переключают Esc (показать) и
        // команда «Показать / скрыть интерфейс» (тулбар/меню). Настройка выключена —
        // поведение ровно прежнее.
        if (IsKeyPressed(KeyCode.Tab) && !KazistovVvUI.KvKeyboardNav.TabHandledByNavigation)
        {
            ToggleUiMode();
        }

        // G — фонарик (toggle). Бинт НЕ менялся: та же клавиша G.
        // Фронт берётся по состоянию клавиши с защёлкой (KeyToggleEdge): одно нажатие =
        // одно переключение. Раньше здесь стоял IsKeyPressed → wasPressedThisFrame, который
        // в кадре без обновления ввода истинен ДВА кадра подряд: первый наскок включал
        // фонарик и в том же мгновении гасил его — «включается со второго нажатия».
        if (KeyToggleEdge(KeyCode.G, ref gKeyWasDown))
        {
            ToggleFlashlight();
        }
        // Правки параметров фонарика в инспекторе применяются на лету (PlayMode, без компиляции)
        // + страховка «свет соответствует флагам».
        UpdateFlashlightLive();
        UpdateFlashlightFade();

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

        // F: смотрит ли «шарик» лазера (точка прицела) на робота.
        //   попал в робота  → он становится выбранным;
        //   не попал ни в кого → выбор обнуляется.
        // ЭТАП 9: когда работает роутер геймпада, LB занят АВАРИЙНОЙ ОСТАНОВКОЙ (по ТЗ),
        // поэтому старый путь «LB = выбор робота по прицелу» отключается — иначе одно
        // нажатие сделало бы два разных дела. Переключение робота на геймпаде — D-Pad ← / →.
        if (IsKeyPressed(KeyCode.F)) SelectRobotByAim();
        else if (gamepadMode && Gamepad.current != null &&
                 Gamepad.current.leftShoulder.wasPressedThisFrame &&
                 !KazistovVvUI.KvGamepadBridge.SuppressLegacyGamepad)
            SelectRobotByAim();

        bool lmbDown = IsMouseButtonDownThisFrame(0);
        bool rmbDown = IsMouseButtonDownThisFrame(1);
        bool overUI = IsPointerOverUI();

        // Esc освобождает курсор и показывает панели (как TAB в сторону UI).
        // КРОМЕ РЕЖИМА ПЕРЕМЕЩЕНИЯ ТОЧКИ: там Esc принадлежит режиму (отмена перемещения, ТЗ шаг 4)
        // и курсор с панелями не трогает — для UI в этом режиме остаётся TAB.
        bool pointMoveNow = flowController != null && flowController.IsPointMoveMode;
        bool escDown = IsKeyPressed(KeyCode.Escape) || KazistovVvUI.KvGamepadBridge.Cancel;
        if (escDown && !pointMoveNow && Cursor.lockState == CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            KazistovVvUI.KazistovVvUIManager.SetUiVisible(true);
        }

        // Клик по окну Game при свободном курсоре → захват мыши (FPS-режим).
        // Если клик пришёлся на UI-канвас — не захватываем (работают кнопки).
        bool justCaptured = false;
        if (Cursor.lockState != CursorLockMode.Locked && (lmbDown || rmbDown) && !overUI && Application.isFocused)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            // UI НЕ скрываем: панели пропадают только по TAB.
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
        // ЭТАП 9: стики работают и БЕЗ ручного тумблера R3 (левая ось — ходьба, правая — обзор),
        // тумблер оставлен для совместимости.
        bool stickControl = gamepadMove || KazistovVvUI.KvGamepadBridge.SticksAlwaysActive;
        bool teleopMode = Cursor.lockState == CursorLockMode.Locked;
        if (teleopMode && stickControl && gamepadMode && Gamepad.current != null)
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
        // В РЕЖИМЕ ПЕРЕМЕЩЕНИЯ ТОЧКИ клавиши QWEASD принадлежат ТОЧКЕ, а не камере:
        // камера стоит на месте, оператор двигает цель (ТЗ, этап 3).
        bool cameraMoveAllowed = !pointMoveNow;
        float speed = IsKeyPressed(KeyCode.LeftShift) ? sprintSpeed * boostMultiplier : moveSpeed;
        Vector3 move = Vector3.zero;

        if (cameraMoveAllowed)
        {
            if (IsKeyPressed(KeyCode.W)) move += transform.forward;
            if (IsKeyPressed(KeyCode.S)) move -= transform.forward;
            if (IsKeyPressed(KeyCode.D)) move += transform.right;
            if (IsKeyPressed(KeyCode.A)) move -= transform.right;
            if (IsKeyPressed(KeyCode.E)) move += Vector3.up * verticalSpeed;
            if (IsKeyPressed(KeyCode.Q)) move -= Vector3.up * verticalSpeed;

            // Геймпад: левый стик — горизонтальное движение (только телеоперация).
            if (teleopMode && stickControl && gamepadMode && Gamepad.current != null)
            {
                Vector2 stick = Gamepad.current.leftStick.ReadValue();
                Vector3 planar = transform.forward * stick.y + transform.right * stick.x;
                if (planar.sqrMagnitude > 0f)
                {
                    planar.y = 0f;
                    move += planar.normalized * speed;
                }
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

        // РЕЖИМ ПЕРЕМЕЩЕНИЯ ТОЧКИ: точку ведёт HUD/маркер (PointMoveHud), шарик прицела
        // и наведение мышью в этом режиме не работают — индикатор приостанавливаем,
        // а колесо мыши глубину НЕ трогает (ТЗ: там точка ходит по QWEASD).
        bool pointMoveMode = flowController != null && flowController.IsPointMoveMode;

        // Точка прицела = ШАРИК: он стоит на своей глубине по лучу (колесо мыши, см. UpdateAimBallDepth).
        ComputeAimPoint(!pointMoveMode);

        // Онлайн-вердикт по точке прицела (зелёный/жёлтый/красный маркер).
        if (aimIndicator != null)
        {
            aimIndicator.SetSuspended(pointMoveMode);
            if (!pointMoveMode)
                aimIndicator.UpdateAim(aimPoint, true, aimHitSurface, aimNormal, ballOnSurface);
        }

        // Планировщик траекторий (P — план, 1/2/3 — исполнить, F9 — автотест).
        if (plannerController != null && !pointMoveMode)
            plannerController.UpdateAim(aimPoint, aimHitSurface);

        // Поток выбора «два лазера»: ОДНА кнопка действия — ЛКМ (её смысл определяется
        // тем, какой лазер включён: Z — красный, X — зелёный).
        // Альтернативы для VR/геймпада: левый триггер, клавиша E.
        // Enter — вход в режим перемещения точки и подтверждение позиции в нём.
        // Ввод читается через IsKeyPressed: у него есть откат на legacy Input, поэтому Enter/Esc
        // доходят даже если new Input System не отдал клавиатуру (иначе «Enter не переключает режимы»).
        if (flowController != null)
        {
            // ЛКМ читается через IsMouseButtonDownThisFrame: у него есть откат на legacy Input.
            // Раньше ЛКМ бралась ТОЛЬКО из Mouse.current — при пустом new Input System
            // (Mouse.current == null) не работало бы вообще ничего: ни точка, ни траектория,
            // ни фантом (ровно тот же класс отказа, что был у Enter до правки 09:00).
            bool confirm =
                IsMouseButtonDownThisFrame(0) ||
                (Gamepad.current != null && Gamepad.current.leftTrigger.wasPressedThisFrame &&
                 !KazistovVvUI.KvGamepadBridge.SuppressLegacyGamepad) ||
                KazistovVvUI.KvGamepadBridge.Confirm ||                          // ЭТАП 9: A / RT
                (!pointMoveMode && IsKeyDownThisFrame(KeyCode.E));   // в режиме E — «точка вверх», а не ЛКМ
            bool cancel = IsKeyPressed(KeyCode.Escape) || KazistovVvUI.KvGamepadBridge.Cancel; // ЭТАП 9: B
            bool enter = IsKeyPressed(KeyCode.Return) || IsKeyPressed(KeyCode.KeypadEnter) ||
                         KazistovVvUI.KvGamepadBridge.Enter;                 // ЭТАП 9: LT
            bool shift = IsKeyPressed(KeyCode.LeftShift);

            flowController.UpdateAim(aimPoint, aimHitSurface, confirm, cancel,
                leftHandEnabled, rightHandEnabled, aimNormal, aimOnRealSurface,
                enter, PointMoveAxis(), shift);
        }

        // Цель лучей: обычная (ШАРИК на его глубине) либо ПЕРЕМЕЩАЕМАЯ ТОЧКА — тогда и цвет лучей
        // показывает её достижимость (ТЗ: в режиме лазеры — только индикация точки и её цвета).
        // Шарик существует всегда (даже когда луч ушёл в пустоту) — лучи сходятся в нём.
        Vector3 laserTarget = aimPoint;
        bool laserTargetValid = true;
        bool laserThroughGeometry = false;
        Color leftTint = leftColor;
        Color rightTint = rightColor;
        if (pointMoveMode && flowController != null &&
            flowController.TryGetPointMoveTarget(out Vector3 movePoint, out Color moveTint))
        {
            laserTarget = movePoint;
            laserTargetValid = true;
            laserThroughGeometry = true;    // точка может быть внутри стены — луч не обрезаем геометрией
            leftTint = moveTint;
            rightTint = moveTint;
        }

        if (leftHandEnabled) DrawHandLaser(leftLaser, -1f, leftHandOffset, leftTint, laserTarget, laserTargetValid, laserThroughGeometry);
        else if (leftLaser != null) leftLaser.enabled = false;

        if (rightHandEnabled) DrawHandLaser(rightLaser, 1f, rightHandOffset, rightTint, laserTarget, laserTargetValid, laserThroughGeometry);
        else if (rightLaser != null) rightLaser.enabled = false;
    }

    /// <summary>
    /// Направление перемещения ТОЧКИ по QWEASD — в МИРОВЫХ координатах (ТЗ, этап 3):
    /// W/S — горизонтальная проекция взгляда оператора, A/D — вправо/влево, Q/E — мировая вертикаль.
    /// Горизонталь берётся намеренно: так «вперёд/назад» не «втыкает» точку в стол, если
    /// оператор смотрит вниз (вертикаль — отдельные клавиши Q/E).
    /// Чтение — через IsKeyPressed (есть откат на legacy Input): движение не должно пропадать,
    /// если new Input System не отдал клавиатуру.
    /// </summary>
    private Vector3 PointMoveAxis()
    {
        Vector3 dir = Vector3.zero;

        Vector3 fwd = transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward; else fwd.Normalize();
        Vector3 right = transform.right;
        right.y = 0f;
        if (right.sqrMagnitude < 1e-6f) right = Vector3.right; else right.Normalize();

        if (IsKeyPressed(KeyCode.W)) dir += fwd;
        if (IsKeyPressed(KeyCode.S)) dir -= fwd;
        if (IsKeyPressed(KeyCode.D)) dir += right;
        if (IsKeyPressed(KeyCode.A)) dir -= right;
        if (IsKeyPressed(KeyCode.E)) dir += Vector3.up;
        if (IsKeyPressed(KeyCode.Q)) dir -= Vector3.up;
        return dir;
    }

    /// <summary>
    /// Считает точку прицеливания (позицию ШАРИКА) и его состояние.
    ///
    /// Луч исходит из позиции оператора (камера — «текущий источник лазера») в направлении взгляда.
    /// Правило по умолчанию ПРЕЖНЕЕ: если колесо не трогали, шарик стоит ровно на первой поверхности
    /// по лучу (или на рабочей плоскости y = 0). Колесо мыши двигает шарик ВДОЛЬ луча
    /// (см. <see cref="UpdateAimBallDepth"/>), средняя кнопка возвращает его на ближайшую поверхность.
    /// </summary>
    private void ComputeAimPoint(bool allowDepthControl)
    {
        Vector3 center = transform.position + transform.up * 0.1f;
        Vector3 dir = transform.forward;
        Ray ray = new Ray(center, dir);

        // 1) Опорная поверхность — ровно та же геометрия, что была: реальная коллизия,
        //    а если её нет — горизонтальная рабочая плоскость y = 0 (она «поверхностью» не считается).
        float hitDist = -1f;
        Vector3 hitNormal = Vector3.up;
        aimNormal = Vector3.up;
        aimOnRealSurface = false;
        if (Physics.Raycast(ray, out RaycastHit hit, laserLength, laserLayers, QueryTriggerInteraction.Ignore))
        {
            hitDist = hit.distance;
            hitNormal = hit.normal;
            aimOnRealSurface = true;
        }
        else
        {
            Plane workPlane = new Plane(Vector3.up, Vector3.zero);
            if (workPlane.Raycast(ray, out float planeDistance) && planeDistance >= 0f && planeDistance <= laserLength)
            {
                hitDist = planeDistance;
                hitNormal = workPlane.normal;
            }
        }

        // 2) Поверхность, к которой ЛИПНЕТ шарик (свой слой — surfaceLayer).
        ballSurfaceHit = Physics.Raycast(ray, out RaycastHit stickHit, laserLength, SurfaceMask(),
                                         QueryTriggerInteraction.Ignore);
        ballSurfaceDist = ballSurfaceHit ? stickHit.distance : -1f;

        // 3) Колесо мыши / средняя кнопка: глубина шарика вдоль луча и прилипание к поверхности.
        UpdateAimBallDepth(hitDist, allowDepthControl);

        // 4) Итог: точка прицела = шарик, нормаль — нормаль поверхности под ним.
        aimNormal = hitNormal;
        ballHasBase = hitDist >= 0f;
        aimHitSurface = ballHasBase;                 // семантика прежняя: «луч встретил поверхность»
        ballOnSurface = aimOnRealSurface && ballHasBase &&
                        Mathf.Abs(ballDepth - hitDist) <= Mathf.Max(0.0005f, surfaceStickTolerance);
        aimPoint = center + dir * ballDepth;
    }

    /// <summary>
    /// ГЛУБИНА ШАРИКА (колесо мыши) и ПРИЛИПАНИЕ к поверхностям.
    ///   * прокрутка ВПЕРЁД (Scroll Up) — шарик идёт по лучу ОТ оператора и останавливается
    ///     на первой поверхности (прилипание: дальше вперёд он не двигается);
    ///   * прокрутка НАЗАД (Scroll Down) — шарик отходит от поверхности К оператору (до minDistance);
    ///   * луч ни во что не попал (смотрит в пустоту) — шарик движется свободно (minDistance…maxDistance);
    ///   * СРЕДНЯЯ КНОПКА — мгновенный возврат к ближайшей поверхности по лучу (<see cref="SnapAimBallToSurface"/>).
    /// Движение плавное (scrollSmoothSpeed), шаг — scrollStep за щелчок.
    /// </summary>
    private void UpdateAimBallDepth(float baseDist, bool allowControl)
    {
        float dt = Mathf.Max(1e-4f, Time.deltaTime);
        float smooth = 1f - Mathf.Exp(-Mathf.Max(1f, scrollSmoothSpeed) * dt);
        float minD = Mathf.Max(0.01f, minDistance);
        float maxD = Mathf.Max(minD, maxDistance);
        float step = Mathf.Max(0.001f, scrollStep);
        bool hasBase = baseDist >= 0f;

        if (!ballDepthInit)
        {
            // Первый кадр: шарик стоит РОВНО на опорной поверхности (поведение прицела прежнее).
            ballDepth = hasBase ? baseDist : minD;
            depthOffset = depthOffsetTarget = 0f;
            freeDepth = freeDepthTarget = ballDepth;
            ballDepthInit = true;
            ballHadBase = hasBase;
        }

        // --- ввод: колесо мыши (только в телеоперации и не над панелями UI) + средняя кнопка ---
        // «Внешнюю» прокрутку (VR/автотесты) снимаем ВСЕГДА: в режиме перемещения точки она
        // не копится, иначе выплеснулась бы в момент выхода из режима.
        float wheel = TakeInjectedScroll();
        if (allowControl)
        {
            if (AimDepthInputAllowed())
            {
                wheel += ReadScrollNotches();
                // Средняя кнопка — мгновенный возврат к ближайшей поверхности по лучу: состояние
                // (глубина, сдвиг) уже выставлено, дальше кадр его не пересчитывает.
                if (IsMouseButtonDownThisFrame(2) && SnapAimBallToSurface())
                {
                    ballHadBase = true;
                    return;
                }
            }
        }
        else
        {
            wheel = 0f;      // РЕЖИМ ПЕРЕМЕЩЕНИЯ ТОЧКИ: колесо глубину не трогает (ТЗ)
        }

        if (hasBase)
        {
            // Опора появилась (шарик вышел из пустоты на поверхность) — переносим глубину в сдвиг
            // без прыжка: шарик остаётся там, где был, а «прилипание» работает уже от новой опоры.
            if (!ballHadBase)
                depthOffset = depthOffsetTarget = Mathf.Clamp(baseDist - freeDepth, -(maxD - minD), maxD);

            depthOffsetTarget -= wheel * step;

            // Прилипание: вперёд — не дальше поверхности, к которой шарик липнет (surfaceLayer).
            // Предел maxDistance — «мягкий»: поверхность под прицелом важнее лимита, иначе на дальней
            // геометрии шарик сошёл бы с поверхности, хотя колесо не трогали. Назад — не ближе minDistance.
            float minOff = Mathf.Min(0f, baseDist - maxD);
            if (stickyToSurface && ballSurfaceHit)
                minOff = Mathf.Max(minOff, baseDist - ballSurfaceDist);
            float maxOff = Mathf.Max(minOff, baseDist - minD);

            depthOffsetTarget = Mathf.Clamp(depthOffsetTarget, minOff, maxOff);
            depthOffset = Mathf.Clamp(Mathf.Lerp(depthOffset, depthOffsetTarget, smooth), minOff, maxOff);
            ballDepth = baseDist - depthOffset;            // границы уже заложены в [minOff, maxOff]
            freeDepth = freeDepthTarget = ballDepth;
        }
        else
        {
            // Пустота: опоры нет — шарик ходит свободно по абсолютной глубине.
            if (ballHadBase) freeDepth = freeDepthTarget = ballDepth;
            freeDepthTarget = Mathf.Clamp(freeDepthTarget + wheel * step, minD, maxD);
            freeDepth = Mathf.Clamp(Mathf.Lerp(freeDepth, freeDepthTarget, smooth), minD, maxD);
            ballDepth = freeDepth;
            depthOffset = depthOffsetTarget = 0f;
        }

        ballHadBase = hasBase;
    }

    /// <summary>
    /// Средняя кнопка мыши: шарик МГНОВЕННО возвращается к ближайшей поверхности по текущему лучу
    /// (первое попадание Raycast). Если луч ни во что не попал (и рабочей плоскости под ним нет) —
    /// шарик ОСТАЁТСЯ НА МЕСТЕ: решение зафиксировано в PROJECT_CONTEXT (никаких «телепортов» вслепую).
    /// </summary>
    public bool SnapAimBallToSurface()
    {
        float minD = Mathf.Max(0.01f, minDistance);
        Vector3 center = transform.position + transform.up * 0.1f;
        Vector3 dir = transform.forward;
        Ray ray = new Ray(center, dir);

        float target = -1f;
        string what = "";
        if (Physics.Raycast(ray, out RaycastHit hit, laserLength, SurfaceMask(), QueryTriggerInteraction.Ignore))
        {
            target = hit.distance;
            what = "поверхность «" + hit.collider.name + "»";
        }
        else
        {
            Plane workPlane = new Plane(Vector3.up, Vector3.zero);
            if (workPlane.Raycast(ray, out float planeDistance) && planeDistance >= 0f && planeDistance <= laserLength)
            {
                target = planeDistance;
                what = "рабочая плоскость y = 0";
            }
        }

        if (target < 0f)
        {
            KazistovVvUI.KazistovVvUIManager.SetPlanStatus("Под прицелом нет поверхности — шарик остался на месте",
                new Color(1f, 0.72f, 0f));
            Debug.Log("[AimDepth] средняя кнопка: поверхность по лучу не найдена — шарик остался на глубине " +
                      ballDepth.ToString("F3"));
            return false;
        }

        // Шарик ставится РОВНО на поверхность: прицел важнее «мягкого» предела maxDistance,
        // а ближе minDistance оператор шарик не подпускает (это защита от «шарика в голове»).
        ballDepth = Mathf.Max(minD, target);
        depthOffset = depthOffsetTarget = 0f;      // на поверхности — сдвига нет
        freeDepth = freeDepthTarget = ballDepth;
        ballDepthInit = true;
        ballHadBase = true;
        ballHasBase = true;
        ballSurfaceHit = true;
        ballSurfaceDist = target;
        aimHitSurface = true;
        aimPoint = center + dir * ballDepth;
        Debug.Log("[AimDepth] средняя кнопка: шарик возвращён к ближайшей поверхности (" + what + ") · глубина " +
                  ballDepth.ToString("F3"));
        return true;
    }

    /// <summary>
    /// Прокрутка колеса «извне» (VR-контроллеры, автотесты), в щелчках: + = вперёд (от оператора).
    /// Складывается с мышью в том же кадре; в режиме перемещения точки игнорируется.
    /// </summary>
    public void AddScrollInput(float notches)
    {
        injectedScroll += notches;
    }

    private float TakeInjectedScroll()
    {
        float n = injectedScroll;
        injectedScroll = 0f;
        return n;
    }

    /// <summary>
    /// Слои для прилипания шарика: surfaceLayer, а если он пуст — слои лучей (страховка от «ничего не липнет»).
    /// </summary>
    private int SurfaceMask()
    {
        int m = surfaceLayer.value;
        return m != 0 ? m : laserLayers.value;
    }

    /// <summary>
    /// Колесо и средняя кнопка принадлежат ШАРИКУ только когда оператор работает с миром:
    /// курсор захвачен (телеоперация) либо курсор свободен, но НЕ над панелями KazistovVv —
    /// иначе колесо прокручивает списки UI (ScrollRect дерева KazistovVv), а средняя кнопка — UI-событие.
    /// </summary>
    private bool AimDepthInputAllowed()
    {
        if (Cursor.lockState == CursorLockMode.Locked) return true;
        // Панели KazistovVv СКРЫТЫ (телеоперация без захвата курсора): прокручивать нечего —
        // колесо и средняя кнопка принадлежат шарику. Без этой проверки при свободном курсоре
        // колесо работало бы только там, где EventSystem не видит UI, и казалось «мёртвым».
        KazistovVvUI.KazistovVvUIManager ui = KazistovVvUI.KazistovVvUIManager.Instance;
        if (ui != null && !ui.uiVisible) return true;
        return !IsPointerOverUI();
    }

    /// <summary>
    /// Прокрутка колеса в «щелчках» (+ = вперёд, от оператора). За кадр читается ОДИН источник:
    /// legacy-ось (Input.GetAxis, если доступна), иначе new Input System (Mouse.current.scroll) —
    /// иначе в режиме «Both» один щелчок посчитался бы дважды.
    /// </summary>
    private float ReadScrollNotches()
    {
        float legacy = 0f;
        try { legacy = Input.GetAxis("Mouse ScrollWheel") * 10f; }   // 0.1 за щелчок → 1
        catch { legacy = 0f; }
        if (Mathf.Abs(legacy) > 0.001f) return legacy;

        if (Mouse.current != null) return NormalizeScrollRaw(Mouse.current.scroll.ReadValue().y);
        return 0f;
    }

    /// <summary>Разные устройства отдают разный «щелчок»: Windows — 120, часть мышей — ±1, тачпады — ~10.</summary>
    private static float NormalizeScrollRaw(float raw)
    {
        float a = Mathf.Abs(raw);
        if (a < 0.001f) return 0f;
        if (a > 8f) return raw / 120f;
        if (a > 1.5f) return raw / 10f;
        return raw;
    }

    /// <summary>
    /// Рисует луч от руки к цели; при близкой цели угол луча больше (сходимость).
    /// `target` — куда сходятся лучи (точка прицела или перемещаемая точка),
    /// `throughGeometry` — не обрезать луч геометрией («рентген»: точка может быть внутри стены).
    /// </summary>
    private void DrawHandLaser(LineRenderer laser, float side, float offset, Color color,
                               Vector3 target, bool targetValid, bool throughGeometry)
    {
        if (laser == null) return;

        // Рука смещена вбок и немного вниз от центра камеры.
        Vector3 hand = transform.position
                       + transform.right * (side * offset)
                       - transform.up * laserDownOffset;

        Vector3 endpoint;
        if (targetValid && Vector3.Distance(hand, target) > 0.001f)
        {
            if (throughGeometry)
            {
                endpoint = target;
            }
            else
            {
                // Луч идёт от руки к целевой точке поверхности.
                Vector3 dir = (target - hand).normalized;
                Ray ray = new Ray(hand, dir);
                float maxDist = Mathf.Max(Vector3.Distance(hand, target), 0.01f);
                if (Physics.Raycast(ray, out RaycastHit hit, maxDist, laserLayers, QueryTriggerInteraction.Ignore))
                {
                    endpoint = hit.point;
                }
                else
                {
                    endpoint = target;
                }
            }
        }
        else
        {
            endpoint = hand + transform.forward * laserLength;
        }

        laser.startColor = new Color(color.r, color.g, color.b, 0.38f);   // ~25–50 %: заметно, но прозрачно
        laser.endColor = new Color(color.r, color.g, color.b, 0.12f);
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
        // В РЕЖИМЕ ПЕРЕМЕЩЕНИЯ ТОЧКИ мышь не действует вовсе (ТЗ, шаг 2): даже если прямая
        // телеоперация в проекте включена, ЛКМ в режиме не должна двигать робота.
        if (flowController != null && flowController.IsPointMoveMode) return;

        // КРИТИЧНО: при включённом новом потоке («два лазера») прямая телеоперация
        // кликом ОТКЛЮЧЕНА — робот не двигается, пока позиция не подтверждена красным.
        if (suppressDirectTeleop) return;

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
    /// F: «шарик» лазера (точка прицела) на роботе → выбираем его;
    /// ни на кого не смотрит → обнуляем выбор (никакого перебора списка).
    /// Работает и для SCARA, и для 6-осевого: ищем контроллер в иерархии попадания,
    /// а если у мешей нет коллайдеров — по габаритам модели (bounds).
    /// </summary>
    private void SelectRobotByAim()
    {
        RobotController aimed = FindRobotUnderAim();
        if (aimed == null && aimHitSurface) aimed = FindRobotByAimVolume(aimPoint);

        RobotController[] robots = Object.FindObjectsByType<RobotController>(FindObjectsInactive.Include);

        if (aimed != null)
        {
            foreach (RobotController rc in robots)
                if (rc != null) rc.SetActive(rc == aimed);
            selectedRobot = aimed;
            Debug.Log("[FreeFlyCamera] Выбран робот: " + aimed.robotName);
            KazistovVvUI.KazistovVvUIManager.SetPlanStatus("Выбран робот: " + aimed.robotName,
                new Color(0.6f, 0.9f, 1f));
        }
        else
        {
            foreach (RobotController rc in robots)
                if (rc != null) rc.SetActive(false);
            selectedRobot = null;
            Debug.Log("[FreeFlyCamera] Выбор робота сброшен (луч не на роботе)");
            KazistovVvUI.KazistovVvUIManager.SetPlanStatus("Выбор робота сброшен", Color.gray);
        }
    }

    /// <summary>Робот, чья модель накрывает точку прицела (fallback без коллайдеров).</summary>
    private static RobotController FindRobotByAimVolume(Vector3 point)
    {
        RobotController best = null;
        float bestDist = float.MaxValue;
        foreach (RobotController rc in Object.FindObjectsByType<RobotController>(FindObjectsInactive.Include))
        {
            if (rc == null) continue;
            Bounds b = GetRobotBounds(rc);
            b.Expand(0.05f);
            if (!b.Contains(point)) continue;
            float d = Vector3.Distance(b.center, point);
            if (d < bestDist) { bestDist = d; best = rc; }
        }
        return best;
    }

    private static Bounds GetRobotBounds(RobotController robot)
    {
        Renderer[] rs = robot.GetComponentsInChildren<Renderer>(true);
        if (rs == null || rs.Length == 0) return new Bounds(robot.transform.position, Vector3.one * 0.3f);
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    /// <summary>Текущий выбранный робот (для потока траекторий).</summary>
    public RobotController SelectedRobot => selectedRobot;

    /// <summary>Точка прицела (шарик лазера) в мире.</summary>
    public Vector3 AimPosition => aimPoint;

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
    /// TAB: режим UI (курсор виден + панели KazistovVv видны) ⟷ телеоперация
    /// (курсор захвачен, панели скрыты). UI в Screen Space Overlay — виден
    /// всегда и не «режется» геометрией сцены.
    /// Раньше переключалось CAPS LOCK — бинд перенесён на TAB (ТЗ сессии 13.09.2026).
    /// </summary>
    private void ToggleUiMode()
    {
        bool uiMode = Cursor.lockState == CursorLockMode.Locked;
        if (uiMode)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Debug.Log("[FreeFlyCamera] Режим UI: курсор + панели (Tab/клик по миру — обратно).");
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Debug.Log("[FreeFlyCamera] Телеоперация: курсор захвачен, панели скрыты.");
        }
        KazistovVvUI.KazistovVvUIManager.SetUiVisible(uiMode);
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
