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
        [Tooltip("Дистанция канваса от камеры, метры")]
        public float canvasDistance = 1.6f;
        [Tooltip("Физическая высота канваса, метры (1280x720 пропорция)")]
        public float canvasHeight = 0.9f;
        [Tooltip("Физическая ширина канваса, метры")]
        public float canvasWidth = 1.6f;

        [Header("Опции")]
        public bool autoRebuildTree = true;

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
            SelectNode(RuntimeRegistry.Roots.Count > 0 ? RuntimeRegistry.Roots[0] : null);
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

            canvasRect = (RectTransform)canvasGo.transform;
            canvasRect.sizeDelta = new Vector2(1280f, 720f);
            canvasRect.localScale = new Vector3(
                canvasWidth / 1280f,
                canvasHeight / 720f,
                1f);

            // Канвас привязывается К КАМЕРЕ (родитель = камера): UI движется строго
            // вместе с камерой без какого-либо лага/догоняния. Дочерний канвас
            // наследует поворот камеры и всегда остаётся перед ней.
            Camera cam = MainCamera;
            if (cam != null)
            {
                canvasGo.transform.SetParent(cam.transform, true);
                canvasRect.localPosition = new Vector3(0f, -0.12f, canvasDistance);
                canvasRect.localRotation = Quaternion.identity;
            }
            else
            {
                PositionCanvasInFront();
            }
        }

        private void PositionCanvasInFront()
        {
            Camera cam = MainCamera;
            if (cam == null) return;
            Vector3 pos = cam.transform.position + cam.transform.forward * canvasDistance;
            pos.y = cam.transform.position.y - 0.25f; // чуть ниже взгляда
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
            Vector3 desiredPos = cam.transform.position
                                  + cam.transform.forward * canvasDistance
                                  + cam.transform.up * (-0.12f);
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
