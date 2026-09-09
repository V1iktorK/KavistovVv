using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace KompasUI
{
    /// <summary>
    /// Главный менеджер KOMPAS-интерфейса: единый OVERLAY-канвас (Screen Space —
    /// как окно настольного приложения: всегда поверх сцены, не «режется»
    /// геометрией, чёткий текст при любом разрешении) и 5 зон:
    ///  1) TopBar (сверху), 2) TreePanel (слева), 3) PropertiesPanel (справа),
    ///  4) CenterWindow (подсказки/выбор типа робота), 5) StatusBar (снизу).
    /// Управление: TAB — показать/скрыть UI (курсор мыши тоже переключается).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class KompasUIManager : MonoBehaviour
    {
        /// <summary>Активный (не-дубликат) менеджер UI.</summary>
        public static KompasUIManager Instance { get; private set; }

        [Header("Разрешение UI (16:9, 1920×1080)")]
        [Tooltip("Опорное разрешение канваса по горизонтали")]
        public float uiReferenceWidth = 1920f;
        [Tooltip("Опорное разрешение канваса по вертикали")]
        public float uiReferenceHeight = 1080f;

        [Header("Опции")]
        public bool autoRebuildTree = true;
        [Tooltip("UI виден (TAB переключает вместе с курсором)")]
        public bool uiVisible = true;

        [Header("«Лампочка Ильича»")]
        [Tooltip("Тёплая точечная лампа над столом (только источник света)")]
        public bool enableWorkLamp = true;
        [Tooltip("Высота лампы над столешницей, метры")]
        public float lampHeight = 1.25f;
        [Tooltip("Яркость (HDRP, кандела)")]
        public float lampIntensity = 900f;
        [Tooltip("Дальность света, метры")]
        public float lampRange = 7f;
        [Tooltip("Тёплая температура, K")]
        public float lampTemperature = 2700f;

        private Canvas canvas;
        private RectTransform canvasRect;

        private TopBar topBar;
        private TreePanel treePanel;
        private PropertiesPanel propertiesPanel;
        private CenterWindow centerWindow;
        private StatusBar statusBar;
        private SettingsWindow settingsWindow;

        private ObjectSpawner spawner;
        private IdleCameraBrain idleBrain;

        private ProjectNode selectedNode;
        private RobotController lastActiveRobot;
        private GameObject workLamp;

        // Размещение роботов.
        private RobotController pendingRobotTemplate;   // выбранный тип (SCARA/6-осевой)
        private Transform pendingTable;                 // стол, на который ставим

        private Camera MainCamera
        {
            get { return Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>(); }
        }

        /// <summary>Показать/скрыть весь KOMPAS-UI (TAB).</summary>
        public static void SetUiVisible(bool visible)
        {
            if (Instance == null) return;
            Instance.SetVisibleInternal(visible);
        }

        private void SetVisibleInternal(bool visible)
        {
            uiVisible = visible;
            if (canvas != null) canvas.gameObject.SetActive(visible);
        }

        void Awake()
        {
            if (FindObjectsByType<KompasUIManager>(FindObjectsInactive.Include).Length > 1)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            EnsureEventSystem();
            spawner = gameObject.AddComponent<ObjectSpawner>();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            RuntimeRegistry.Changed -= OnRegistryChanged;
        }

        private bool built;

        void Start()
        {
            if (built) return;
            built = true;

            BuildCanvas();
            BuildZonesIfNeeded();

            Camera cam = MainCamera;
            if (cam != null)
            {
                idleBrain = cam.GetComponent<IdleCameraBrain>();
                if (idleBrain == null) idleBrain = cam.gameObject.AddComponent<IdleCameraBrain>();
            }

            RuntimeRegistry.RebuildFromScene();
            RuntimeRegistry.Changed += OnRegistryChanged;

            // Раскрываем роботов по умолчанию, чтобы дерево выглядело как в Unity.
            foreach (ProjectNode root in RuntimeRegistry.Roots)
            {
                if (root.Kind == KompasNodeKind.Robot && treePanel != null)
                    treePanel.Expand(root.Id);
            }

            if (spawner != null) spawner.RefreshRobotTemplates();
            EnsureWorkLamp();
            SelectNode(RuntimeRegistry.Roots.Count > 0 ? RuntimeRegistry.Roots[0] : null);
        }

        /// <summary>
        /// «Лампочка Ильича»: тёплый точечный источник над столом (без модельки).
        /// Ставится над ПЕРВЫМ столом — уже существующим в сцене либо размещённым
        /// через UI (на повторные вызовы не реагирует, пока лампа не создана).
        /// </summary>
        private void EnsureWorkLamp()
        {
            if (!enableWorkLamp || workLamp != null) return;

            Transform table = FindFirstTableSurface();
            if (table == null) return;

            Bounds b = GetRenderBounds(table);
            if (b.size.y < 0.001f) return;

            Vector3 pos = new Vector3(b.center.x, b.max.y + lampHeight, b.center.z);
            GameObject go = new GameObject("IlyichLamp");
            go.transform.position = pos;

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.intensity = lampIntensity;
            light.range = lampRange;
            light.color = Color.white;
            light.colorTemperature = lampTemperature; // 2700 K — тёплая «лампочка»
            light.useColorTemperature = true;
            light.shadows = LightShadows.Soft;

            workLamp = go;
            Debug.Log("[KompasUI] «Лампочка Ильича» над столом '" + table.name +
                      "' (" + pos.ToString("0.00") + ")");
        }

        /// <summary>Первый «стол»: объект с именем стол/desk/table (не робот).</summary>
        private static Transform FindFirstTableSurface()
        {
            Transform best = null;
            float bestTop = float.NegativeInfinity;
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (t.GetComponent<Renderer>() == null) continue;
                if (t.GetComponentInParent<RobotController>() != null) continue;
                if (!NameLooksLikeTable(t.name)) continue;
                Renderer r = t.GetComponent<Renderer>();
                if (r == null) continue;
                float top = r.bounds.max.y; // берём самый «верхний» кусок стола
                if (top > bestTop)
                {
                    bestTop = top;
                    best = t;
                }
            }
            return best;
        }

        private static bool NameLooksLikeTable(string name)
        {
            string n = name.ToLowerInvariant();
            return n.IndexOf("стол", System.StringComparison.Ordinal) >= 0 ||
                   n.IndexOf("desk", System.StringComparison.Ordinal) >= 0 ||
                   n.IndexOf("table", System.StringComparison.Ordinal) >= 0;
        }

        /// <summary>Объединённые границы мешей объекта (для столешницы).</summary>
        private static Bounds GetRenderBounds(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
                return new Bounds(root.position, Vector3.one);
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);
            return b;
        }

        // ------------------------------------------------------------------ UI построение

        private void EnsureEventSystem()
        {
            if (EventSystem.current == null)
            {
                GameObject es = new GameObject("EventSystem", typeof(EventSystem));
                es.transform.SetParent(transform, false);
                var module = es.AddComponent<InputSystemUIInputModule>();
                module.AssignDefaultActions();
            }
        }

        private void BuildCanvas()
        {
            GameObject canvasGo = new GameObject("KompasCanvas", typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            canvas = canvasGo.GetComponent<Canvas>();
            // OVERLAY: UI всегда поверх сцены — не режется геометрией, чёткий текст,
            // автоматически следует за разрешением экрана (TAB может скрыть).
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(uiReferenceWidth, uiReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasRect = (RectTransform)canvasGo.transform;
            canvasRect.sizeDelta = new Vector2(uiReferenceWidth, uiReferenceHeight);
        }

        // ------------------------------------------------------------------ Зоны

        private void BuildZonesIfNeeded()
        {
            if (topBar != null) return;

            // Слой-контейнер с «подложкой» для зон в координатах канваса
            // Вся канва 1280x720: верх 44px, низ 30px, центр между ними.
            // Каждая зона сама себя позиционирует через anchors.

            topBar = gameObject.AddComponent<TopBar>();
            topBar.Build(canvasRect, StartPlacement, ToggleSettings);

            settingsWindow = gameObject.AddComponent<SettingsWindow>();
            settingsWindow.Build(canvasRect);

            treePanel = gameObject.AddComponent<TreePanel>();
            treePanel.Build(canvasRect, SelectNode);
            treePanel.BindRootProvider(RebuildTree);

            propertiesPanel = gameObject.AddComponent<PropertiesPanel>();
            propertiesPanel.Build(canvasRect);

            centerWindow = gameObject.AddComponent<CenterWindow>();
            centerWindow.Build(canvasRect);

            statusBar = gameObject.AddComponent<StatusBar>();
            statusBar.Build(canvasRect);
        }

        // ------------------------------------------------------------------ Реестр/дерево

        private void OnRegistryChanged()
        {
            // Если стол появился/размещён — вешаем «лампочку Ильича» (один раз).
            EnsureWorkLamp();
            RebuildTree();
        }

        private void RebuildTree()
        {
            if (treePanel == null) return;
            treePanel.Rebuild(RuntimeRegistry.Roots, selectedNode);
        }

        public void SelectNode(ProjectNode node)
        {
            selectedNode = node;
            if (propertiesPanel != null) propertiesPanel.ShowNode(node);
            RebuildTree();

            // Выбор узла-робота активирует его подсветку.
            if (node != null && node.Robot != null)
            {
                node.Robot.SetActive(true);
            }
        }

        // ------------------------------------------------------------------ Размещение

        public void StartPlacement(SpawnKind kind)
        {
            BuildZonesIfNeeded();
            if (kind == SpawnKind.Robot)
            {
                // Сначала выбор типа робота (SCARA или 6-осевой).
                pendingRobotTemplate = null;
                centerWindow.StartPlacement(SpawnKind.None); // убрать старое размещение
                centerWindow.ShowRobotChooser(OnRobotTypeChosen);
                return;
            }
            if (kind == SpawnKind.None)
            {
                pendingRobotTemplate = null;
            }
            centerWindow.StartPlacement(kind);
        }

        /// <summary>Выбран тип робота (0 — 6-осевой, 1 — SCARA).</summary>
        private void OnRobotTypeChosen(int typeIndex)
        {
            centerWindow.HideRobotChooser();

            if (spawner == null) return;
            spawner.RefreshRobotTemplates();
            RobotController template = null;
            foreach (RobotController rc in spawner.robotTemplates)
            {
                if (rc == null) continue;
                bool isSix = rc is SixAxisController;
                bool isScara = rc is SCARAController;
                if ((typeIndex == 0 && isSix) || (typeIndex == 1 && isScara))
                {
                    template = rc;
                    break;
                }
            }
            if (template == null)
            {
                Debug.LogWarning("[KompasUI] Нет шаблона робота нужного типа (0=6-осевой, 1=SCARA).");
                return;
            }

            pendingRobotTemplate = template;
            Debug.Log("[KompasUI] Размещение робота: " + template.robotName +
                      " (наведите на стол, ←/→ направление, ЛКМ/Enter — поставить)");
            centerWindow.StartPlacement(SpawnKind.Robot);
        }

        /// <summary>Открыть/закрыть окно «Настройки» (кнопка TopBar).</summary>
        public void ToggleSettings()
        {
            BuildZonesIfNeeded();
            if (settingsWindow != null) settingsWindow.Toggle();
        }

        // ------------------------------------------------------------------ Ввод / луч

        void Update()
        {
            if (!built) return; // зоны ещё не построены (Start не отработал)

            HandleRobotChooserKeys();
            HandlePlacementInput();
            TrackActiveRobotForProperties();
        }

        /// <summary>Выбор типа робота клавишами 1/2, пока открыт выборщик; Esc — отмена.</summary>
        private void HandleRobotChooserKeys()
        {
            if (centerWindow == null || !centerWindow.RobotChooserOpen) return;

            bool escDown = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                           || (Keyboard.current == null && TryLegacyKeyDown(KeyCode.Escape));
            if (escDown)
            {
                centerWindow.HideRobotChooser();
                return;
            }

            int pick = -1;
            if (Keyboard.current != null)
            {
                if (Keyboard.current.digit1Key.wasPressedThisFrame) pick = 0;
                else if (Keyboard.current.digit2Key.wasPressedThisFrame) pick = 1;
            }
            if (pick < 0)
            {
                try
                {
                    if (Input.GetKeyDown(KeyCode.Alpha1)) pick = 0;
                    else if (Input.GetKeyDown(KeyCode.Alpha2)) pick = 1;
                }
                catch { }
            }
            if (pick >= 0) OnRobotTypeChosen(pick);
        }

        /// <summary>Следит за «активным» роботом: клик по нему открывает свойства справа.</summary>
        private void TrackActiveRobotForProperties()
        {
            if (propertiesPanel == null) return;

            RobotController current = null;
            RobotController[] robots = Object.FindObjectsByType<RobotController>(FindObjectsInactive.Include);
            foreach (RobotController rc in robots)
            {
                if (rc != null && rc.isActive) { current = rc; break; }
            }

            if (current != null && current != lastActiveRobot)
            {
                lastActiveRobot = current;
                ProjectNode node = RuntimeRegistry.FindRobotNode(current);
                if (node == null)
                {
                    node = RuntimeRegistry.CreateNodeFor(current.transform, current);
                    RuntimeRegistry.Roots.Add(node);
                    RuntimeRegistry.NotifyChanged();
                }
                SelectNode(node);
                idleBrain?.PingActivity();
            }
            else if (current == null && lastActiveRobot != null)
            {
                lastActiveRobot = null;
            }
        }

        // ------------------------------------------------------------------ Размещение: ввод

        private void HandlePlacementInput()
        {
            if (centerWindow == null || centerWindow.ActiveKind == SpawnKind.None) return;

            // Esc — отмена размещения.
            bool escDown = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                           || (Keyboard.current == null && TryLegacyKeyDown(KeyCode.Escape));
            if (escDown)
            {
                centerWindow.StartPlacement(SpawnKind.None);
                return;
            }

            // Вращение направления робота: ←/→, A/D, геймпад (левый стик X).
            if (centerWindow.ActiveKind == SpawnKind.Robot)
            {
                float rot = 0f;
                if (Keyboard.current != null)
                {
                    if (Keyboard.current.leftArrowKey.isPressed || Keyboard.current.aKey.isPressed) rot -= 90f;
                    if (Keyboard.current.rightArrowKey.isPressed || Keyboard.current.dKey.isPressed) rot += 90f;
                }
                else
                {
                    try
                    {
                        if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) rot -= 90f;
                        if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) rot += 90f;
                    }
                    catch { }
                }
                if (rot != 0f)
                    centerWindow.RotateRobot(rot * Time.deltaTime);
            }

            Camera cam = MainCamera;
            if (cam == null) return;

            Ray ray = GetAimRay(cam);
            bool hit = Physics.Raycast(ray, out RaycastHit hitInfo, 200f,
                ~0, QueryTriggerInteraction.Ignore);

            // Для робота цель — ЦЕНТР стола (робот ставится только на стол).
            Vector3 placePoint = hit ? hitInfo.point : ray.origin + ray.direction * 10f;
            bool valid;
            pendingTable = null;
            if (centerWindow.ActiveKind == SpawnKind.Robot)
            {
                Transform tableRoot = null;
                bool onTable = false;
                if (hit)
                    onTable = TryFindTableSurface(hitInfo, out placePoint, out tableRoot);
                valid = onTable;
                if (onTable) pendingTable = tableRoot;
            }
            else
            {
                valid = hit && hitInfo.normal.y > 0.3f;
            }

            centerWindow.UpdatePreview(placePoint, valid);

            bool confirm = (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                || (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
                || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
            if (!confirm || !valid) return;

            ConfirmPlacement(placePoint);
        }

        /// <summary>
        /// Ищет «стол» под точкой попадания (RegisteredObject или имя стол/desk/table,
        /// но не пол/level/plane). Возвращает ЦЕНТР ВЕРХА стола (по мешам корня)
        /// и корень стола.
        /// </summary>
        private static bool TryFindTableSurface(RaycastHit hit, out Vector3 center,
            out Transform tableRoot)
        {
            center = hit.point;
            tableRoot = null;
            Collider col = hit.collider;
            if (col == null) return false;

            bool looksLikeTable = false;
            RegisteredObject reg = col.GetComponentInParent<RegisteredObject>();
            string name = col.transform.root.name.ToLowerInvariant();

            if (reg != null) looksLikeTable = true;
            else if (name.Contains("стол") || name.Contains("table") || name.Contains("desk"))
                looksLikeTable = true;

            // Не считаем столом пол/уровень/стены (слишком большие плоскости).
            if (name.Contains("plane") || name.Contains("floor") || name.Contains("level") ||
                name.Contains("hangar") || name.Contains("wall"))
                looksLikeTable = false;

            if (!looksLikeTable) return false;

            Transform root = col.transform.root;
            tableRoot = root;
            Bounds b = GetRenderBounds(root);
            if (b.size.y < 0.001f) return false;
            center = new Vector3(b.center.x, b.max.y, b.center.z);
            return true;
        }

        /// <summary>Удаляет размещённых через UI роботов, стоящих на этом столе.</summary>
        private void RemoveRobotsOnTable(Transform tableRoot)
        {
            if (tableRoot == null) return;
            Bounds b = GetRenderBounds(tableRoot);
            float minX = b.min.x - 0.15f, maxX = b.max.x + 0.15f;
            float minZ = b.min.z - 0.15f, maxZ = b.max.z + 0.15f;
            float minY = b.min.y - 0.2f, maxY = b.max.y + 2.5f;

            var robots = Object.FindObjectsByType<RobotController>(FindObjectsInactive.Include);
            foreach (RobotController rc in robots)
            {
                if (rc == null) continue;
                if (rc.GetComponent<RegisteredObject>() == null) continue; // только копии UI
                Vector3 p = rc.transform.position;
                if (p.x >= minX && p.x <= maxX && p.z >= minZ && p.z <= maxZ &&
                    p.y >= minY && p.y <= maxY)
                {
                    Debug.Log("[KompasUI] На столе '" + tableRoot.name +
                              "' был робот '" + rc.robotName + "' — удалён (1 робот на стол).");
                    Object.Destroy(rc.gameObject);
                }
            }
        }

        /// <summary>Делает робота единственным основным (подсветка/телеметрия).</summary>
        private static void ActivateRobotOnly(RobotController keep)
        {
            if (keep == null) return;
            var robots = Object.FindObjectsByType<RobotController>(FindObjectsInactive.Include);
            foreach (RobotController rc in robots)
            {
                if (rc != null) rc.SetActive(rc == keep);
            }
        }

        private void ConfirmPlacement(Vector3 point)
        {
            SpawnKind kind = centerWindow.ActiveKind;
            float yaw = kind == SpawnKind.Robot ? centerWindow.RobotYaw : 0f;
            centerWindow.StartPlacement(SpawnKind.None);

            if (spawner == null) return;

            RobotController placedRobot = null;
            RegisteredObject spawned;
            if (kind == SpawnKind.Table)
            {
                spawned = spawner.SpawnTable(point);
            }
            else
            {
                // На стол ставится ОДИН робот: старых с этого стола убираем.
                RemoveRobotsOnTable(pendingTable);
                if (pendingRobotTemplate == null)
                {
                    Debug.LogWarning("[KompasUI] Не выбран тип робота — размещение отменено.");
                    return;
                }
                spawned = spawner.SpawnRobot(point, yaw, pendingRobotTemplate);
                if (spawned != null)
                {
                    placedRobot = spawned.GetComponent<RobotController>();
                    ActivateRobotOnly(placedRobot); // новый робот становится основным
                }
            }

            if (spawned == null) return;

            // Реестр/дерево: пересобираем (удалённые старые роботы исчезают).
            RuntimeRegistry.RebuildFromScene();
            RuntimeRegistry.NotifyChanged();

            if (placedRobot != null)
            {
                ProjectNode node = RuntimeRegistry.FindRobotNode(placedRobot);
                if (node != null)
                {
                    if (treePanel != null) treePanel.Expand(node.Id);
                    SelectNode(node);
                }
            }
        }

        private static bool TryLegacyKeyDown(KeyCode code)
        {
            try { return Input.GetKeyDown(code); }
            catch { return false; }
        }

        private static Ray GetAimRay(Camera cam)
        {
            // В VR/геймпаде — луч из руки (InputManager), иначе центр экрана.
            if (InputManager.Instance != null &&
                InputManager.Instance.ActiveProvider is GamepadInputProvider)
            {
                return new Ray(InputManager.Instance.PointerPosition,
                    InputManager.Instance.PointerDirection);
            }

            Vector3 screen = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
            return cam.ScreenPointToRay(screen);
        }

        // ------------------------------------------------------------------ Публичные хелперы

        public void Refresh()
        {
            RuntimeRegistry.RebuildFromScene();
            RebuildTree();
        }
    }
}
