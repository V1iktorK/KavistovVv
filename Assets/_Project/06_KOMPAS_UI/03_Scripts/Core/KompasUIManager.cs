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
        public float canvasDistance = 2.2f;
        public float canvasHeight = 1.35f;
        public float canvasWidth = 2.6f;

        [Header("Опции")]
        public bool autoRebuildTree = true;

        private Canvas canvas;
        private RectTransform canvasRect;

        private TopBar topBar;
        private TreePanel treePanel;
        private PropertiesPanel propertiesPanel;
        private CenterWindow centerWindow;
        private StatusBar statusBar;

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
            canvasGo.transform.SetParent(transform, false);

            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            canvasRect = (RectTransform)canvasGo.transform;
            canvasRect.sizeDelta = new Vector2(1280f, 720f);
            canvasRect.localScale = new Vector3(
                canvasWidth / 1280f,
                canvasHeight / 720f,
                1f);

            PositionCanvasInFront();
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
            topBar.Build(canvasRect, StartPlacement);

            treePanel = gameObject.AddComponent<TreePanel>();
            treePanel.Build(canvasRect, SelectNode);

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
            Camera cam = MainCamera;
            if (cam == null || canvasRect == null) return;

            // Не «приклеиваем» жёстко: канва остаётся на месте, только мягко
            // доворачивается к камере, если пользователь далеко отошёл.
            Vector3 toCanvas = canvasRect.position - cam.transform.position;
            if (toCanvas.sqrMagnitude > (canvasDistance * 1.5f) * (canvasDistance * 1.5f))
            {
                Vector3 pos = cam.transform.position + cam.transform.forward * canvasDistance;
                pos.y = cam.transform.position.y - 0.25f;
                canvasRect.position = Vector3.Lerp(canvasRect.position, pos, Time.deltaTime * 2f);
            }
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
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                centerWindow.StartPlacement(SpawnKind.None);
                return;
            }

            Camera cam = MainCamera;
            if (cam == null) return;

            Ray ray = GetAimRay(cam);
            bool hit = Physics.Raycast(ray, out RaycastHit hitInfo, 200f,
                ~0, QueryTriggerInteraction.Ignore);
            Vector3 point = hit ? hitInfo.point : ray.origin + ray.direction * 10f;
            bool valid = hit && hitInfo.normal.y > 0.3f;

            centerWindow.UpdatePreview(point, valid);

            bool confirm = (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                || (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame);
            if (!confirm || !valid) return;

            ConfirmPlacement(point);
        }

        private void ConfirmPlacement(Vector3 point)
        {
            SpawnKind kind = centerWindow.ActiveKind;
            centerWindow.StartPlacement(SpawnKind.None);

            if (spawner == null) return;

            RegisteredObject spawned = kind == SpawnKind.Table
                ? spawner.SpawnTable(point)
                : spawner.SpawnRobot(point);

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
