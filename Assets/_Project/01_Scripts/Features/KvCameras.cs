using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using KazistovVvUI;
using TrajectoryCore;

namespace KazistovVvFeatures
{
    /// <summary>Одно окно «картинки в картинке» (этап 16 ТЗ).</summary>
    public class KvPiPWindow
    {
        public string id = "";
        public string title = "";
        public Camera camera;
        public RenderTexture texture;
        public RawImage image;
        public RectTransform root;
        public bool visible;
        public float width = 260f;
        public float height = 150f;
    }

    /// <summary>
    /// ЭТАП 16 ТЗ: НЕСКОЛЬКО КАМЕР / PiP (КАРТИНКА В КАРТИНКЕ).
    ///
    /// Что делает:
    ///   • создаёт ДОПОЛНИТЕЛЬНЫЕ камеры: вид сверху, вид сбоку и вид «от первого лица»
    ///     (камера у инструмента робота, смотрит его же осью) — основная камера оператора
    ///     при этом работает как обычно;
    ///   • каждая камера рисуется в свою текстуру, а на экране показываются маленькие окна
    ///     PiP (можно включать по одному, менять размер и порядок, показывать одно на весь
    ///     экран) — и для отладки, и для презентаций;
    ///   • окна живут на СВОЁМ канвасе поверх сцены (сортировка ниже подсказок), поэтому
    ///     FreeCAD-интерфейс не перестраивается и не ломается.
    ///
    /// Камеры служебные: они не имеют коллайдеров и не участвуют в расчётах.
    /// </summary>
    public class KvCameraService
    {
        public event Action<string> Message;

        private TrajectoryFlowController flow;
        private Camera main;
        private Canvas canvas;
        private RectTransform canvasRect;
        private readonly List<KvPiPWindow> windows = new List<KvPiPWindow>();
        private string fullscreenId = "";
        private int textureSize = 640;
        private float margin = 8f;

        public IReadOnlyList<KvPiPWindow> Windows { get { return windows; } }

        /// <summary>Строка состояния для дерева моделей и свойств (этап 16 ТЗ).</summary>
        public string Status()
        {
            int shown = 0;
            for (int i = 0; i < windows.Count; i++) if (windows[i].visible) shown++;
            string full = string.IsNullOrEmpty(fullscreenId)
                ? ""
                : " · на весь экран: " + FullscreenTitle();
            return "окон " + windows.Count + ", показано " + shown + full;
        }

        private string FullscreenTitle()
        {
            for (int i = 0; i < windows.Count; i++)
                if (windows[i].id == fullscreenId) return windows[i].title;
            return fullscreenId;
        }
        public bool AnyVisible
        {
            get
            {
                foreach (KvPiPWindow w in windows) if (w.visible) return true;
                return false;
            }
        }

        public string Fullscreen { get { return fullscreenId; } }

        public void Bind(TrajectoryFlowController controller, Camera mainCamera)
        {
            flow = controller;
            main = mainCamera;
        }

        /// <summary>Собрать окна PiP (вызывается один раз при привязке).</summary>
        public void Build(Transform parent)
        {
            if (canvas != null) return;

            canvas = KvOverlayKit.CreateCanvas(parent, "KvPiPCanvas", 40);
            canvasRect = (RectTransform)canvas.transform;

            AddWindow("top", KvLocExtra3.T("cam.top", "Вид сверху"), 1.9f);
            AddWindow("side", KvLocExtra3.T("cam.side", "Вид сбоку"), 1.45f);
            AddWindow("fpv", KvLocExtra3.T("cam.fpv", "Вид от первого лица (глаза робота)"), 0.6f);

            Layout();
            Report("камеры PiP готовы: " + windows.Count + " окна (сверху · сбоку · от первого лица)");
        }

        private void AddWindow(string id, string title, float height)
        {
            KvPiPWindow window = new KvPiPWindow
            {
                id = id,
                title = title,
                height = height * 100f
            };

            // Без графики (пакетный режим) камеры и текстуры не создаются: попытка отрисовать
            // кадр в таком режиме ломает весь цикл кадров. Окна остаются в списке как «пустые»,
            // поэтому интерфейс и статус работают одинаково в обоих случаях.
            if (KvGraphics.Available)
            {
                GameObject camGo = new GameObject("KvCam_" + id, typeof(Camera));
                camGo.transform.SetParent(canvas.transform, false);
                Camera cam = camGo.GetComponent<Camera>();
                cam.fieldOfView = 55f;
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 120f;
                cam.enabled = false;
                cam.depth = -10;                  // рисуется в текстуру, не в экран
                cam.clearFlags = CameraClearFlags.Skybox;
                cam.cullingMask = ~0;

                window.camera = cam;
                window.texture = new RenderTexture(textureSize, Mathf.RoundToInt(textureSize * 9f / 16f), 16);
                window.texture.name = "KvPiP_" + id;
                cam.targetTexture = window.texture;
            }
            else
            {
                Report("камера «" + title + "»: нет графики — окно создано без изображения");
            }

            GameObject frameGo = new GameObject("Window_" + id, typeof(Image));
            frameGo.transform.SetParent(canvasRect, false);
            RectTransform rt = (RectTransform)frameGo.transform;
            window.root = rt;
            Image bg = frameGo.GetComponent<Image>();
            bg.sprite = KvTheme.WhiteSprite;
            bg.color = new Color(KvTheme.WindowBg.r, KvTheme.WindowBg.g, KvTheme.WindowBg.b, 0.92f);
            bg.raycastTarget = true;

            GameObject imgGo = new GameObject("Image", typeof(RawImage));
            imgGo.transform.SetParent(rt, false);
            RawImage raw = imgGo.GetComponent<RawImage>();
            raw.texture = window.texture;
            raw.raycastTarget = false;
            RectTransform rawRect = (RectTransform)imgGo.transform;
            rawRect.anchorMin = new Vector2(0f, 0f);
            rawRect.anchorMax = new Vector2(1f, 1f);
            rawRect.offsetMin = new Vector2(2f, 2f);
            rawRect.offsetMax = new Vector2(-2f, -2f);
            window.image = raw;

            Text label = KvTheme.CreateText(rt, "Title", title, KvTheme.FontSizeSmall,
                TextAnchor.UpperLeft, KvTheme.TextMain);
            label.rectTransform.anchorMin = new Vector2(0f, 1f);
            label.rectTransform.anchorMax = new Vector2(1f, 1f);
            label.rectTransform.pivot = new Vector2(0.5f, 1f);
            label.rectTransform.offsetMin = new Vector2(6f, 0f);
            label.rectTransform.offsetMax = new Vector2(-6f, 0f);
            label.rectTransform.sizeDelta = new Vector2(-12f, 16f);
            label.rectTransform.anchoredPosition = Vector2.zero;

            window.visible = false;
            rt.gameObject.SetActive(false);
            windows.Add(window);
        }

        /// <summary>Показать/скрыть окно по id ("" — переключить все).</summary>
        public void SetVisible(string id, bool visible)
        {
            if (string.IsNullOrEmpty(id))
            {
                foreach (KvPiPWindow w in windows) SetVisible(w.id, visible);
                return;
            }
            KvPiPWindow window = Find(id);
            if (window == null) return;
            window.visible = visible;
            if (window.root != null) window.root.gameObject.SetActive(visible);
            if (window.camera != null) window.camera.enabled = visible || fullscreenId == id;
            if (visible) Report("камера «" + window.title + "» включена");
            Layout();
        }

        public void Toggle(string id)
        {
            KvPiPWindow window = Find(id);
            if (window == null) return;
            SetVisible(id, !window.visible);
        }

        /// <summary>Переключить весь набор окон (кнопка/клавиша).</summary>
        public void ToggleAll()
        {
            SetVisible("", !AnyVisible);
        }

        /// <summary>Показать одно окно на весь экран (повторный вызов возвращает PiP).</summary>
        public void ToggleFullscreen(string id)
        {
            fullscreenId = fullscreenId == id ? "" : id;
            foreach (KvPiPWindow w in windows)
            {
                if (w.camera != null)
                    w.camera.enabled = w.visible || w.id == fullscreenId;
                if (w.root != null)
                    w.root.gameObject.SetActive(w.visible || w.id == fullscreenId);
            }
            Layout();
            Report(string.IsNullOrEmpty(fullscreenId)
                ? "окна PiP возвращены к обычному размеру"
                : "камера «" + (Find(fullscreenId) != null ? Find(fullscreenId).title : fullscreenId) +
                  "» показана на весь экран");
        }

        private KvPiPWindow Find(string id)
        {
            foreach (KvPiPWindow w in windows) if (w.id == id) return w;
            return null;
        }

        /// <summary>Расставить окна по правому краю (или одно на весь экран).</summary>
        private void Layout()
        {
            if (canvasRect == null) return;

            if (!string.IsNullOrEmpty(fullscreenId))
            {
                KvPiPWindow big = Find(fullscreenId);
                if (big != null && big.root != null)
                {
                    big.root.anchorMin = new Vector2(0f, 0f);
                    big.root.anchorMax = new Vector2(1f, 1f);
                    big.root.offsetMin = new Vector2(0f, 0f);
                    big.root.offsetMax = new Vector2(0f, 0f);
                }
                return;
            }

            float y = -margin;
            foreach (KvPiPWindow w in windows)
            {
                if (w.root == null) continue;
                w.root.anchorMin = new Vector2(1f, 1f);
                w.root.anchorMax = new Vector2(1f, 1f);
                w.root.pivot = new Vector2(1f, 1f);
                float width = w.height * 16f / 9f;
                w.root.sizeDelta = new Vector2(width, w.height + 18f);
                w.root.anchoredPosition = new Vector2(-margin, y);
                y -= w.height + 18f + margin;
            }
        }

        /// <summary>Кадровое сопровождение: камеры ставятся в нужные точки.</summary>
        public void Tick(float deltaTime)
        {
            if (!AnyVisible && string.IsNullOrEmpty(fullscreenId)) return;
            if (flow == null) return;

            Vector3 center = flow.Robot != null ? flow.Robot.transform.position : Vector3.zero;
            PoseValidator v = flow.Validator;
            if (v == null || !v.Ready) return;

            double[] q = v.CopyCurrent();
            Vector3 tcp = v.TcpAt(q);

            KvPiPWindow top = Find("top");
            if (top != null && top.camera != null)
            {
                top.camera.transform.position = center + Vector3.up * 3.2f;
                top.camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                top.camera.fieldOfView = 55f;
            }

            KvPiPWindow side = Find("side");
            if (side != null && side.camera != null)
            {
                side.camera.transform.position = center + new Vector3(-2.2f, 1.1f, 0f);
                side.camera.transform.rotation = Quaternion.LookRotation((tcp - side.camera.transform.position)
                    .normalized, Vector3.up);
                side.camera.fieldOfView = 45f;
            }

            KvPiPWindow fpv = Find("fpv");
            if (fpv != null && fpv.camera != null)
            {
                // «Глаза робота»: камера стоит в TCP и смотрит вдоль оси инструмента.
                Vector3 axis = KvToolKinematics.ToolAxis(v, q);
                fpv.camera.transform.position = tcp - axis * 0.06f;
                Vector3 forward = axis.sqrMagnitude > 1e-6f ? axis : Vector3.down;
                fpv.camera.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
                fpv.camera.fieldOfView = 70f;
            }
        }

        public void Dispose()
        {
            foreach (KvPiPWindow w in windows)
            {
                if (w.camera != null && w.camera.targetTexture != null) w.camera.targetTexture = null;
                if (w.texture != null) UnityEngine.Object.Destroy(w.texture);
                if (w.camera != null) UnityEngine.Object.Destroy(w.camera.gameObject);
            }
            windows.Clear();
        }

        private void Report(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            Debug.Log("[Cameras] " + text);
            if (Message != null) Message(text);
        }
    }

    /// <summary>ВКЛАДКА «КАМЕРЫ И PiP» (ЭТАП 16 ТЗ).</summary>
    public class KvCameraTab : IKvWorkbenchTab
    {
        private readonly KvCameraService service;

        public KvCameraTab(KvCameraService cameras)
        {
            service = cameras;
        }

        public string Key { get { return "cameras"; } }
        public string Title { get { return KvLocExtra3.T("cam.title", "Камеры и PiP"); } }

        private static string T(string key, string fallback)
        {
            return KvLocExtra3.T(key, fallback);
        }

        public void Build(KvTabKit kit)
        {
            if (service == null || kit == null) return;

            kit.Section(Title);
            kit.Buttons(new[]
            {
                T("cam.pip", "Картинка в картинке"),
                T("cam.top", "Вид сверху"),
                T("cam.side", "Вид сбоку"),
                T("cam.fpv", "Вид от первого лица")
            }, new Action[]
            {
                delegate { service.ToggleAll(); },
                delegate { service.Toggle("top"); },
                delegate { service.Toggle("side"); },
                delegate { service.Toggle("fpv"); }
            });
            kit.Buttons(new[]
            {
                T("cam.top", "Вид сверху") + " ⛶",
                T("cam.side", "Вид сбоку") + " ⛶",
                T("cam.fpv", "Вид от первого лица") + " ⛶"
            }, new Action[]
            {
                delegate { service.ToggleFullscreen("top"); },
                delegate { service.ToggleFullscreen("side"); },
                delegate { service.ToggleFullscreen("fpv"); }
            });

            kit.Table("", delegate { return T("cam.title", "Камеры и PiP"); },
                delegate { return T("wb.value", "Значение"); });
            for (int i = 0; i < service.Windows.Count && i < 3; i++)
            {
                int index = i;
                kit.Table("#" + (index + 1),
                    delegate { return service.Windows[index].title; },
                    delegate
                    {
                        KvPiPWindow w = service.Windows[index];
                        return (w.visible ? "вкл" : "выкл") +
                               (service.Fullscreen == w.id ? " · на весь экран" : "");
                    });
            }

            kit.Note(T("cam.info",
                "Дополнительные камеры рисуются в отдельные текстуры и показываются окнами поверх сцены; " +
                "основная камера оператора продолжает работать как обычно."), KvTheme.TextDim);
        }

        public void Tick() { }
        public void Refresh() { }
    }
}
