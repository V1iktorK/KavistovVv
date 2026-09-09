using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace KompasUI
{
    /// <summary>
    /// Главный менеджер KOMPAS-интерфейса: строит единый World Space Canvas
    /// перед пользователем и связывает все 5 зон:
    ///  1) TopBar (сверху), 2) TreePanel (слева), 3) PropertiesPanel (справа,
    ///     скрываемая тумблером), 4) CenterWindow (главная сцена, режим размещения),
    ///  5) StatusBar (снизу, ссылки).
    /// Управление: луч руки/мыши — клик по объекту открывает свойства.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class KompasUIManager : MonoBehaviour
    {
        [Header("Положение канваса перед пользователем")]
        [Tooltip("Дистанция канваса от камеры, метры (приближено к камере)")]
        public float canvasDistance = 1.2f;
        [Tooltip("Размер канваса подстраивается под экран (FOV камеры × формат 16:9 = 1920×1080)")]
        public bool canvasFitToScreen = true;
        [Tooltip("Эталонный формат экрана (ширина/высота). Сейчас 16:9")]
        public float uiAspect = 16f / 9f;
        [Tooltip("Опорное разрешение по горизонтали (для масштаба в пикселях канваса)")]
        public float uiReferenceWidth = 1920f;
        [Tooltip("Опорное разрешение по вертикали")]
        public float uiReferenceHeight = 1080f;

        [Header("Опции")]
        public bool autoRebuildTree = true;

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

        private Camera MainCamera
        {
            get { return Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>(); }
        }

        void Awake()
        {
            if (FindObjectsByType<KompasUIManager>(FindObjectsInactive.Include).Length > 1)
            {
                Destroy(gameObject);
                return;
            }

            EnsureEventSystem();
            spawner = gameObject.AddComponent<ObjectSpawner>();
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

        void OnDestroy()
        {
            RuntimeRegistry.Changed -= OnRegistryChanged;
        }

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

        // ------------------------------------------------------------------ UI построение

        private void BuildCanvas()
        {
            GameObject canvasGo = new GameObject("KompasCanvas", typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            // Канвас «в пикселях»: 1920×1080 (опорное разрешение), 16:9.
            canvasRect = (RectTransform)canvasGo.transform;
            canvasRect.sizeDelta = new Vector2(uiReferenceWidth, uiReferenceHeight);

            // Канвас привязывается К КАМЕРЕ (родитель = камера): UI движется строго
            // вместе с камерой без какого-либо лага/догоняния.
            Camera cam = MainCamera;
            if (cam != null)
            {
                canvasGo.transform.SetParent(cam.transform, true);
                canvasRect.localPosition = new Vector3(0f, 0f, canvasDistance);
                canvasRect.localRotation = Quaternion.identity;

                // Масштаб зависит от РАЗРЕШЕНИЯ экрана: канвас занимает весь кадр
                // камеры (высота = 2·d·tan(FOV/2), ширина = высота × 16/9).
                float worldHeight = canvasFitToScreen && cam.orthographic == false
                    ? 2f * canvasDistance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad)
                    : 2f * canvasDistance * Mathf.Tan(30f * Mathf.Deg2Rad);
                if (worldHeight < 0.01f) worldHeight = 1.2f;
                float worldWidth = worldHeight * uiAspect;
                float scaleX = worldWidth / uiReferenceWidth;
                float scaleY = worldHeight / uiReferenceHeight;
                canvasRect.localScale = new Vector3(scaleX, scaleY, 1f);
            }
            else
            {
                canvasRect.localScale = Vector3.one;
                PositionCanvasInFront();
            }
        }

        private void PositionCanvasInFront()
        {
            Camera cam = MainCamera;
            if (cam == null) return;
            Vector3 pos = cam.transform.position + cam.transform.forward * canvasDistance;
            canvasRect.position = pos;
            canvasRect.rotation = cam.transform.rotation;
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
            centerWindow.StartPlacement(kind);
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

            KeepCanvasFacingCamera();
            HandlePlacementInput();
            TrackActiveRobotForProperties();
        }

        private void KeepCanvasFacingCamera()
        {
            if (canvasRect == null) return;

            // Канвас привязан к камере как дочерний объект — догонять/повторять
            // позицию не нужно, он движется строго с камерой (без лага).
            Transform parent = canvasRect.parent;
            if (parent != null && parent.GetComponent<Camera>() != null)
                return;

            Camera cam = MainCamera;
            if (cam == null) return;

            // Fallback (камера появилась позже): копируем позицию/поворот каждый кадр.
            Vector3 desiredPos = cam.transform.position + cam.transform.forward * canvasDistance;
            canvasRect.position = desiredPos;
            canvasRect.rotation = cam.transform.rotation;
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

            // Для робота цель — ЦЕНТР стола, если под прицелом стол.
            Vector3 placePoint;
            bool valid;
            if (hit && centerWindow.ActiveKind == SpawnKind.Robot && TryFindTableCenter(hitInfo, out Vector3 tableCenter))
            {
                placePoint = tableCenter;
                valid = true;
            }
            else
            {
                placePoint = hit ? hitInfo.point : ray.origin + ray.direction * 10f;
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
        /// Ищет «стол» под точкой попадания: объект-стол (RegisteredObject/имя содержит стол)
        /// либо плоская горизонтальная поверхность. Возвращает ЦЕНТР верхней плоскости стола.
        /// </summary>
        private static bool TryFindTableCenter(RaycastHit hit, out Vector3 center)
        {
            center = hit.point;
            Collider col = hit.collider;
            if (col == null) return false;

            bool looksLikeTable = false;
            RegisteredObject reg = col.GetComponentInParent<RegisteredObject>();
            string name = col.transform.root.name.ToLowerInvariant();

            if (reg != null) looksLikeTable = true;
            else if (name.Contains("стол") || name.Contains("table") || name.Contains("desk")) looksLikeTable = true;

            // Не считаем столом пол/уровень (слишком большие плоскости).
            if (name.Contains("plane") || name.Contains("floor") || name.Contains("level"))
                looksLikeTable = false;

            if (!looksLikeTable) return false;

            Bounds b = col.bounds;
            center = new Vector3(b.center.x, b.max.y, b.center.z);
            return true;
        }

        private void ConfirmPlacement(Vector3 point)
        {
            SpawnKind kind = centerWindow.ActiveKind;
            float yaw = kind == SpawnKind.Robot ? centerWindow.RobotYaw : 0f;
            centerWindow.StartPlacement(SpawnKind.None);

            if (spawner == null) return;

            RegisteredObject spawned = kind == SpawnKind.Table
                ? spawner.SpawnTable(point)
                : spawner.SpawnRobot(point, yaw);

            if (spawned == null) return;

            ProjectNode node = new ProjectNode(
                System.Guid.NewGuid().ToString("N"),
                spawned.DisplayName,
                kind == SpawnKind.Table ? KompasNodeKind.Table : KompasNodeKind.Robot,
                spawned.transform,
                spawned.GetComponent<RobotController>());
            spawned.Node = node;
            RuntimeRegistry.Roots.Add(node);
            RuntimeRegistry.NotifyChanged();
            SelectNode(node);
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
