using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using KazistovVvUI;
using TrajectoryCore;

namespace KazistovVvFeatures
{
    /// <summary>
    /// ЭТАП 31 ТЗ: КИНЕМАТОГРАФИЧЕСКИЙ РЕЖИМ (CINEMATIC MODE).
    ///
    /// Что делает:
    ///   • «широкий экран» — чёрные полосы сверху и снизу (настраиваемая высота) и мягкое
    ///     затемнение кадра;
    ///   • автоматическое движение камеры: облёт робота по кругу, слежение за инструментом,
    ///     неподвижный «штатив»; движение плавное (демпфирование), без рывков;
    ///   • замедление съёмки — управляется штатным множителем воспроизведения движения,
    ///     то есть замедляется именно показ робота, а не вся сцена (физика и интерфейс
    ///     работают в обычном темпе);
    ///   • полное восстановление камеры при выходе: положение, поворот и угол обзора
    ///     возвращаются ровно такими, какими были до включения режима.
    ///
    /// Режим не перехватывает клавиши и не мешает управлению: выключили — и управление
    /// камерой снова полностью у оператора.
    /// </summary>
    public class KvCinematicService
    {
        public enum CameraMode { Orbit = 0, FollowTool = 1, Tripod = 2 }

        public event Action<string> Message;

        private TrajectoryFlowController flow;
        private Camera camera;
        private Canvas canvas;
        private RectTransform canvasRect;

        private Image barTop, barBottom, dim;
        private Text modeText;
        private bool enabled;
        private bool barsVisible = true;
        [Range(0f, 0.25f)] private float barHeight = 0.11f;
        private CameraMode mode = CameraMode.Orbit;
        private float orbitRadius = 3.4f;
        private float orbitHeight = 2.1f;
        private float orbitSpeed = 6f;          // градусов в секунду
        private float damping = 2.4f;

        private Vector3 savedPosition;
        private Quaternion savedRotation;
        private float savedFov;
        private bool saved;
        private float orbitAngle;
        private Vector3 smoothTarget;

        public bool Enabled { get { return enabled; } }
        public CameraMode Mode { get { return mode; } }
        public float BarHeight { get { return barHeight; } }

        public void Bind(TrajectoryFlowController controller, Camera mainCamera, Transform canvasParent)
        {
            flow = controller;
            camera = mainCamera;
            Build(canvasParent);
        }

        private void Build(Transform parent)
        {
            if (canvas != null || parent == null) return;
            canvas = KvOverlayKit.CreateCanvas(parent, "KvCinematicCanvas", 48);
            canvasRect = (RectTransform)canvas.transform;

            barTop = KvTheme.CreatePanel(canvasRect, "BarTop", Color.black);
            barBottom = KvTheme.CreatePanel(canvasRect, "BarBottom", Color.black);
            dim = KvTheme.CreatePanel(canvasRect, "Dim", new Color(0f, 0f, 0f, 0.18f));
            KvTheme.Stretch(dim.rectTransform, 0f, 0f, 0f, 0f);
            dim.raycastTarget = false;

            modeText = KvTheme.CreateText(canvasRect, "Mode", "", KvTheme.FontSizeSmall,
                TextAnchor.LowerRight, new Color(1f, 1f, 1f, 0.75f));
            modeText.rectTransform.anchorMin = new Vector2(1f, 0f);
            modeText.rectTransform.anchorMax = new Vector2(1f, 0f);
            modeText.rectTransform.pivot = new Vector2(1f, 0f);
            modeText.rectTransform.sizeDelta = new Vector2(420f, 18f);
            modeText.rectTransform.anchoredPosition = new Vector2(-14f, 10f);

            canvas.gameObject.SetActive(false);
        }

        public void Toggle()
        {
            SetEnabled(!enabled);
        }

        public void SetEnabled(bool value)
        {
            if (enabled == value) return;
            enabled = value;
            if (value)
            {
                SaveCamera();
                orbitAngle = 0f;
                if (Cam != null)
                    smoothTarget = TargetPoint();
            }
            else
            {
                RestoreCamera();
            }
            if (canvas != null) canvas.gameObject.SetActive(value);
            Layout();
            Report(value
                ? "кинематографический режим включён · камера: " + ModeLabel + " · полосы " +
                  (barsVisible ? (barHeight * 100f).ToString("0") + " %" : "выключены")
                : "кинематографический режим выключен, камера возвращена оператору");
        }

        public void SetMode(CameraMode value)
        {
            mode = value;
            if (enabled)
            {
                orbitAngle = 0f;
                smoothTarget = TargetPoint();
            }
            Report("камера: " + ModeLabel);
        }

        public void NextMode()
        {
            SetMode((CameraMode)(((int)mode + 1) % 3));
        }

        public void SetBarHeight(float value)
        {
            barHeight = Mathf.Clamp(value, 0f, 0.25f);
            Layout();
        }

        public void SetBars(bool value)
        {
            barsVisible = value;
            Layout();
        }

        public void SetOrbit(float radius, float height, float speed)
        {
            orbitRadius = Mathf.Clamp(radius, 0.8f, 12f);
            orbitHeight = Mathf.Clamp(height, 0.2f, 8f);
            orbitSpeed = Mathf.Clamp(speed, -40f, 40f);
        }

        private string ModeLabel
        {
            get
            {
                switch (mode)
                {
                    case CameraMode.FollowTool: return "слежение за инструментом";
                    case CameraMode.Tripod: return "штатив (без движения)";
                    default: return "облёт робота";
                }
            }
        }

        private void Layout()
        {
            if (canvasRect == null) return;
            float height = barsVisible ? Screen.height * barHeight : 0f;

            barTop.rectTransform.anchorMin = new Vector2(0f, 1f);
            barTop.rectTransform.anchorMax = new Vector2(1f, 1f);
            barTop.rectTransform.pivot = new Vector2(0.5f, 1f);
            barTop.rectTransform.offsetMin = new Vector2(0f, 0f);
            barTop.rectTransform.offsetMax = new Vector2(0f, 0f);
            barTop.rectTransform.sizeDelta = new Vector2(0f, height);
            barTop.rectTransform.anchoredPosition = Vector2.zero;

            barBottom.rectTransform.anchorMin = new Vector2(0f, 0f);
            barBottom.rectTransform.anchorMax = new Vector2(1f, 0f);
            barBottom.rectTransform.pivot = new Vector2(0.5f, 0f);
            barBottom.rectTransform.sizeDelta = new Vector2(0f, height);
            barBottom.rectTransform.anchoredPosition = Vector2.zero;

            barTop.gameObject.SetActive(barsVisible && enabled);
            barBottom.gameObject.SetActive(barsVisible && enabled);
            dim.gameObject.SetActive(enabled);
        }

        private void SaveCamera()
        {
            if (Cam == null || saved) return;
            savedPosition = Cam.transform.position;
            savedRotation = Cam.transform.rotation;
            savedFov = Cam.fieldOfView;
            saved = true;
        }

        private void RestoreCamera()
        {
            if (Cam == null || !saved) return;
            Cam.transform.position = savedPosition;
            Cam.transform.rotation = savedRotation;
            Cam.fieldOfView = savedFov;
            saved = false;
        }

        /// <summary>
        /// Камера оператора. Берётся ЛЕНИВО: в момент привязки сервисов главная камера может быть
        /// ещё не создана (например, в пакетном режиме без графики или при позднем старте XR),
        /// поэтому при первом обращении она ищется заново — иначе режим молча ничего не делал бы.
        /// </summary>
        private Camera Cam
        {
            get
            {
                if (camera == null) camera = Camera.main;
                return camera;
            }
        }

        private Vector3 TargetPoint()
        {
            if (flow != null && flow.Validator != null && flow.Validator.Ready)
                return flow.Validator.TcpAt(flow.Validator.CopyCurrent());
            return Vector3.zero;
        }

        private Vector3 BasePoint()
        {
            if (flow != null && flow.Robot != null)
                return new Vector3(flow.Robot.transform.position.x, 0f, flow.Robot.transform.position.z);
            return Vector3.zero;
        }

        /// <summary>Кадровое движение камеры. Вызывать из LateUpdate, после управления оператором.</summary>
        public void Tick(float deltaTime)
        {
            if (!enabled || Cam == null) return;

            Vector3 basePoint = BasePoint();
            Vector3 target;
            Vector3 desired;

            switch (mode)
            {
                case CameraMode.FollowTool:
                {
                    target = Vector3.Lerp(smoothTarget, TargetPoint(), Mathf.Clamp01(damping * deltaTime));
                    smoothTarget = target;
                    desired = target + new Vector3(orbitRadius * 0.55f, orbitHeight * 0.55f, -orbitRadius * 0.75f);
                    break;
                }
                case CameraMode.Tripod:
                {
                    target = Vector3.Lerp(smoothTarget, TargetPoint(), Mathf.Clamp01(damping * deltaTime));
                    smoothTarget = target;
                    desired = saved ? savedPosition : target + new Vector3(2f, 1.6f, -3f);
                    break;
                }
                default:
                {
                    orbitAngle = Mathf.Repeat(orbitAngle + orbitSpeed * deltaTime, 360f);
                    float radians = orbitAngle * Mathf.Deg2Rad;
                    target = Vector3.Lerp(smoothTarget, TargetPoint(), Mathf.Clamp01(damping * deltaTime * 0.6f));
                    smoothTarget = target;
                    Vector3 center = Vector3.Lerp(basePoint, target, 0.35f);
                    desired = center + new Vector3(Mathf.Sin(radians) * orbitRadius, orbitHeight,
                        Mathf.Cos(radians) * orbitRadius);
                    break;
                }
            }

            float k = Mathf.Clamp01(damping * deltaTime);
            Transform camTransform = Cam.transform;
            camTransform.position = Vector3.Lerp(camTransform.position, desired, k);
            Vector3 look = smoothTarget - camTransform.position;
            if (look.sqrMagnitude > 1e-5f)
                camTransform.rotation = Quaternion.Slerp(camTransform.rotation,
                    Quaternion.LookRotation(look.normalized, Vector3.up), k);

            if (modeText != null)
                modeText.text = "кинорежим · " + ModeLabel + " · FOV " +
                                Cam.fieldOfView.ToString("0") + "° · полосы " +
                                (barHeight * 100f).ToString("0") + " %";
        }

        public string Status()
        {
            if (!enabled) return "выключен";
            return "включён · " + ModeLabel + " · радиус " + orbitRadius.ToString("0.0") +
                   " м, высота " + orbitHeight.ToString("0.0") + " м, скорость " +
                   orbitSpeed.ToString("0") + " °/с";
        }

        private void Report(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            Debug.Log("[Cinematic] " + text);
            if (Message != null) Message(text);
        }
    }

    /// <summary>Одна надпись на экране: титр, подзаголовок или пояснение (этап 32 ТЗ).</summary>
    public class KvTitleCue
    {
        public float start;
        public float end = 2f;
        public string text = "";
        public int kind;              // 0 — титр, 1 — подзаголовок, 2 — нижняя треть
        public KvTitleCue() { }
        public KvTitleCue(float start, float end, string text, int kind)
        {
            this.start = start;
            this.end = end;
            this.text = text;
            this.kind = kind;
        }
    }

    /// <summary>Пояснение, привязанное к точке сцены (этап 32 ТЗ).</summary>
    public class KvAnnotation
    {
        public Vector3 point;
        public string text = "";
        public Color color = Color.white;
    }

    /// <summary>
    /// ЭТАП 32 ТЗ: ТИТРЫ, ПОДПИСИ И ПОЯСНЕНИЯ (плюс редактор субтитров).
    ///
    /// Три вида надписей: крупный титр (по центру сверху), подзаголовок (внизу по центру) и
    /// «нижняя треть» (плашка слева внизу, как в новостях). Каждая надпись живёт свой отрезок
    /// времени; список отрезков редактируется во вкладке и выгружается в файл субтитров `.srt`,
    /// который понимают видеоредакторы и плееры.
    ///
    /// Отдельно — ПОЯСНЕНИЯ К ТОЧКАМ СЦЕНЫ: текст привязывается к месту (например, «зона захвата»)
    /// и каждый кадр проецируется на экран, оставаясь на месте при движении камеры.
    /// </summary>
    public class KvTitlesService
    {
        public event Action<string> Message;

        private Camera camera;
        private Canvas canvas;
        private RectTransform canvasRect;

        private Text titleText, subtitleText, lowThirdText, clockText;
        private Image lowThirdPanel;
        private readonly List<KvTitleCue> cues = new List<KvTitleCue>();
        private readonly List<KvAnnotation> annotations = new List<KvAnnotation>();
        private readonly List<Text> annotationViews = new List<Text>(16);

        private bool playing;
        private float playStart;
        private float playOffset;
        private float duration = 30f;
        private bool visible = true;
        private string lastShown = "";

        public IList<KvTitleCue> Cues { get { return cues; } }
        public IList<KvAnnotation> Annotations { get { return annotations; } }
        public bool Playing { get { return playing; } }
        public float Time
        {
            get { return playing ? playOffset + (UnityEngine.Time.realtimeSinceStartup - playStart) : playOffset; }
        }
        public float Duration { get { return duration; } set { duration = Mathf.Clamp(value, 1f, 3600f); } }
        public bool Visible { get { return visible; } }

        public string CurrentText
        {
            get
            {
                KvTitleCue cue = CueAt(Time);
                return cue == null ? "" : cue.text;
            }
        }

        public void Bind(Camera mainCamera, Transform canvasParent)
        {
            camera = mainCamera;
            Build(canvasParent);
        }

        private void Build(Transform parent)
        {
            if (canvas != null || parent == null) return;
            canvas = KvOverlayKit.CreateCanvas(parent, "KvTitlesCanvas", 45);
            canvasRect = (RectTransform)canvas.transform;

            titleText = KvTheme.CreateText(canvasRect, "Title", "", 34, TextAnchor.MiddleCenter,
                Color.white);
            titleText.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            titleText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            titleText.rectTransform.pivot = new Vector2(0.5f, 1f);
            titleText.rectTransform.sizeDelta = new Vector2(1100f, 52f);
            titleText.rectTransform.anchoredPosition = new Vector2(0f, -70f);
            AddShadow(titleText);

            subtitleText = KvTheme.CreateText(canvasRect, "Subtitle", "", 22, TextAnchor.MiddleCenter,
                Color.white);
            subtitleText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            subtitleText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            subtitleText.rectTransform.pivot = new Vector2(0.5f, 0f);
            subtitleText.rectTransform.sizeDelta = new Vector2(1100f, 62f);
            subtitleText.rectTransform.anchoredPosition = new Vector2(0f, 44f);
            subtitleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            subtitleText.verticalOverflow = VerticalWrapMode.Overflow;
            AddShadow(subtitleText);

            lowThirdPanel = KvTheme.CreatePanel(canvasRect, "LowThirdBg", new Color(0f, 0f, 0f, 0.55f));
            lowThirdPanel.rectTransform.anchorMin = new Vector2(0f, 0f);
            lowThirdPanel.rectTransform.anchorMax = new Vector2(0f, 0f);
            lowThirdPanel.rectTransform.pivot = new Vector2(0f, 0f);
            lowThirdPanel.rectTransform.sizeDelta = new Vector2(520f, 42f);
            lowThirdPanel.rectTransform.anchoredPosition = new Vector2(40f, 120f);
            lowThirdPanel.raycastTarget = false;

            lowThirdText = KvTheme.CreateText(lowThirdPanel.rectTransform, "LowThird", "", 20,
                TextAnchor.MiddleLeft, Color.white);
            KvTheme.Stretch(lowThirdText.rectTransform, 12f, 12f);

            clockText = KvTheme.CreateText(canvasRect, "Clock", "", KvTheme.FontSizeSmall,
                TextAnchor.UpperRight, new Color(1f, 1f, 1f, 0.55f));
            clockText.rectTransform.anchorMin = new Vector2(1f, 1f);
            clockText.rectTransform.anchorMax = new Vector2(1f, 1f);
            clockText.rectTransform.pivot = new Vector2(1f, 1f);
            clockText.rectTransform.sizeDelta = new Vector2(300f, 18f);
            clockText.rectTransform.anchoredPosition = new Vector2(-16f, -16f);

            ApplyVisible();
        }

        private static void AddShadow(Text text)
        {
            Shadow shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
        }

        public void SetVisible(bool value)
        {
            visible = value;
            ApplyVisible();
        }

        private void ApplyVisible()
        {
            if (canvas != null) canvas.gameObject.SetActive(visible);
        }

        // ------------------------------------------------------------------ надписи

        public KvTitleCue AddCue(float start, float end, string text, int kind)
        {
            KvTitleCue cue = new KvTitleCue(Mathf.Max(0f, start), Mathf.Max(start + 0.3f, end), text, kind);
            cues.Add(cue);
            cues.Sort(delegate (KvTitleCue a, KvTitleCue b) { return a.start.CompareTo(b.start); });
            duration = Mathf.Max(duration, cue.end + 1f);
            Report("надпись добавлена: " + Stamp(cue.start) + "–" + Stamp(cue.end) + " · " + text);
            return cue;
        }

        public bool RemoveCue(int index)
        {
            if (index < 0 || index >= cues.Count) return false;
            cues.RemoveAt(index);
            return true;
        }

        public void ClearCues()
        {
            cues.Clear();
            Report("список надписей очищен");
        }

        public KvAnnotation AddAnnotation(Vector3 point, string text, Color color)
        {
            KvAnnotation annotation = new KvAnnotation { point = point, text = text, color = color };
            annotations.Add(annotation);
            return annotation;
        }

        public void ClearAnnotations()
        {
            annotations.Clear();
            Report("пояснения к точкам сцены убраны");
        }

        public void Play(float from = 0f)
        {
            playOffset = Mathf.Max(0f, from);
            playStart = UnityEngine.Time.realtimeSinceStartup;
            playing = true;
            Report("показ титров запущен с " + Stamp(playOffset));
        }

        public void Stop()
        {
            if (!playing) return;
            playOffset = Time;
            playing = false;
            Report("показ титров остановлен на " + Stamp(playOffset));
        }

        public void Reset()
        {
            playing = false;
            playOffset = 0f;
        }

        public void Tick(float deltaTime)
        {
            if (!visible) return;
            float time = Time;
            KvTitleCue cue = CueAt(time);

            string title = "", subtitle = "", low = "";
            if (cue != null)
            {
                if (cue.kind == 0) title = cue.text;
                else if (cue.kind == 1) subtitle = cue.text;
                else low = cue.text;
                if (lastShown != cue.text)
                {
                    lastShown = cue.text;
                    Report("на экране: " + cue.text);
                }
            }

            if (titleText != null) titleText.text = title;
            if (subtitleText != null) subtitleText.text = subtitle;
            if (lowThirdText != null) lowThirdText.text = low;
            if (lowThirdPanel != null) lowThirdPanel.gameObject.SetActive(low.Length > 0);
            if (clockText != null)
                clockText.text = "титры " + Stamp(time) + " / " + Stamp(duration) +
                                 (playing ? "" : " (пауза)");

            UpdateAnnotations();
        }

        private KvTitleCue CueAt(float time)
        {
            for (int i = 0; i < cues.Count; i++)
                if (time >= cues[i].start && time <= cues[i].end) return cues[i];
            return null;
        }

        private void UpdateAnnotations()
        {
            if (camera == null) return;
            while (annotationViews.Count < annotations.Count)
            {
                Text text = KvTheme.CreateText(canvasRect, "Note" + annotationViews.Count, "", 17,
                    TextAnchor.MiddleLeft, Color.white);
                text.rectTransform.anchorMin = new Vector2(0f, 0f);
                text.rectTransform.anchorMax = new Vector2(0f, 0f);
                text.rectTransform.pivot = new Vector2(0f, 0.5f);
                text.rectTransform.sizeDelta = new Vector2(420f, 22f);
                AddShadow(text);
                annotationViews.Add(text);
            }
            for (int i = 0; i < annotationViews.Count; i++)
            {
                Text view = annotationViews[i];
                if (i >= annotations.Count) { view.gameObject.SetActive(false); continue; }
                KvAnnotation annotation = annotations[i];
                Vector3 screen = camera.WorldToScreenPoint(annotation.point);
                bool inFront = screen.z > 0.05f;
                if (!inFront)
                {
                    view.gameObject.SetActive(false);
                    continue;
                }
                view.gameObject.SetActive(true);
                view.text = "● " + annotation.text;
                view.color = annotation.color;
                Vector2 local;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,
                    new Vector2(screen.x, screen.y), null, out local);
                view.rectTransform.anchoredPosition = local + new Vector2(14f, 14f);
            }
        }

        public static string Stamp(float seconds)
        {
            if (seconds < 0f) seconds = 0f;
            int total = Mathf.FloorToInt(seconds);
            int minutes = total / 60;
            int secs = total % 60;
            int millis = Mathf.FloorToInt((seconds - total) * 1000f);
            return minutes.ToString("00") + ":" + secs.ToString("00") + "." + millis.ToString("000");
        }

        /// <summary>Формат субтитров SRT: часы:минуты:секунды,запятая,миллисекунды.</summary>
        public static string SrtStamp(float seconds)
        {
            if (seconds < 0f) seconds = 0f;
            int total = Mathf.FloorToInt(seconds);
            int hours = total / 3600;
            int minutes = (total % 3600) / 60;
            int secs = total % 60;
            int millis = Mathf.FloorToInt((seconds - total) * 1000f);
            return hours.ToString("00") + ":" + minutes.ToString("00") + ":" + secs.ToString("00") +
                   "," + millis.ToString("000");
        }

        /// <summary>Выгрузка субтитров в файл `.srt` (понимают видеоредакторы и плееры).</summary>
        public string ExportSrt(string folder, string baseName)
        {
            try
            {
                if (string.IsNullOrEmpty(folder)) folder = FeatureStorage.Root;
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, baseName + ".srt");
                StringBuilder sb = new StringBuilder();
                int number = 1;
                for (int i = 0; i < cues.Count; i++)
                {
                    KvTitleCue cue = cues[i];
                    if (string.IsNullOrEmpty(cue.text)) continue;
                    sb.AppendLine(number.ToString());
                    sb.AppendLine(SrtStamp(cue.start) + " --> " + SrtStamp(cue.end));
                    sb.AppendLine(cue.text);
                    sb.AppendLine();
                    number++;
                }
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
                Report("субтитры выгружены: " + path + " (" + (number - 1) + " надписей)");
                return path;
            }
            catch (Exception e)
            {
                Report("субтитры не выгружены: " + e.Message);
                return "";
            }
        }

        public string Status()
        {
            return (visible ? "показаны" : "скрыты") + " · надписей " + cues.Count +
                   " · пояснений " + annotations.Count + " · время " + Stamp(Time) +
                   (playing ? " (идёт)" : " (пауза)");
        }

        private void Report(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            Debug.Log("[Titles] " + text);
            if (Message != null) Message(text);
        }
    }

    /// <summary>Запись голоса диктора в WAV (этап 33 ТЗ).</summary>
    public class KvVoiceOverService
    {
        public const int MaxSeconds = 300;

        public event Action<string> Message;

        private string device = "";
        private AudioClip micClip;
        private AudioClip savedClip;
        private AudioSource source;
        private bool recording;
        private int recordedSamples;
        private int frequency = 44100;
        private float recordStart;
        private float videoOffset = -1f;     // смещение относительно начала видеозаписи, с
        private string lastPath = "";
        private float[] waveform = new float[0];
        private float peak;
        private float startedAtSameTimeAsVideo = -1f;

        public bool Recording { get { return recording; } }
        public bool HasRecording { get { return savedClip != null && recordedSamples > 0; } }
        public float Duration { get { return savedClip != null ? (float)recordedSamples / frequency : 0f; } }
        public float RecordingTime
        {
            get
            {
                if (!recording || micClip == null) return 0f;
                int position = Microphone.GetPosition(device);
                return (float)position / frequency;
            }
        }
        public string LastPath { get { return lastPath; } }
        public float VideoOffset { get { return videoOffset; } }
        public float Peak { get { return peak; } }
        public bool Playing { get { return source != null && source.isPlaying; } }
        public float PlaybackTime { get { return source != null ? source.time : 0f; } }
        public float[] Waveform { get { return waveform; } }

        public string Folder
        {
            get
            {
                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (string.IsNullOrEmpty(docs)) docs = FeatureStorage.Root;
                return Path.Combine(Path.Combine(docs, "KazistovVv"), "voiceover");
            }
        }

        public void Bind(Transform parent)
        {
            if (source != null) return;
            GameObject go = new GameObject("KvVoiceOver", typeof(AudioSource));
            go.hideFlags = HideFlags.HideInHierarchy;
            if (parent != null) go.transform.SetParent(parent, false);
            source = go.GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 1f;
        }

        public bool Start()
        {
            if (recording) return true;
            if (Microphone.devices == null || Microphone.devices.Length == 0)
            {
                Report("микрофон не найден — запись голоса невозможна");
                return false;
            }
            try
            {
                device = Microphone.devices[0];
                micClip = Microphone.Start(device, false, MaxSeconds, frequency);
                if (micClip == null) { Report("микрофон не отдал поток"); return false; }
                recording = true;
                recordStart = UnityEngine.Time.realtimeSinceStartup;

                KvCaptureService capture = KvStageHub.Current != null ? KvStageHub.Current.Capture : null;
                if (capture != null && capture.IsRecording && capture.RecordStartedAt > 0f)
                {
                    videoOffset = recordStart - capture.RecordStartedAt;
                    startedAtSameTimeAsVideo = recordStart;
                    Report("запись голоса начата: видео уже пишется, смещение " +
                           videoOffset.ToString("0.00") + " с");
                }
                else
                {
                    videoOffset = -1f;
                    Report("запись голоса начата (видео не пишется — смещения нет)");
                }
                return true;
            }
            catch (Exception e)
            {
                Report("запись голоса не начата: " + e.Message);
                recording = false;
                return false;
            }
        }

        /// <summary>Остановить запись и сохранить файл WAV.</summary>
        public string Stop()
        {
            if (!recording) return lastPath;
            int position = 0;
            try
            {
                position = Microphone.GetPosition(device);
                Microphone.End(device);
            }
            catch (Exception) { }
            recording = false;
            recordedSamples = Mathf.Clamp(position, 0, micClip != null ? micClip.samples : 0);
            if (recordedSamples < frequency / 4)
            {
                Report("запись слишком короткая — файл не сохранён");
                return "";
            }

            float[] data = new float[recordedSamples];
            try
            {
                micClip.GetData(data, 0);
            }
            catch (Exception e)
            {
                Report("данные записи недоступны: " + e.Message);
                return "";
            }

            savedClip = AudioClip.Create("Голос диктора", recordedSamples, 1, frequency, false);
            savedClip.SetData(data, 0);
            BuildWaveform(data);
            lastPath = WriteWav(data);
            SaveMarkers();

            if (source != null)
            {
                source.clip = savedClip;
                source.Stop();
            }
            Report("запись голоса сохранена: " + lastPath + " · " + Duration.ToString("0.0") + " с" +
                   (videoOffset >= 0f ? " · смещение к видео " + videoOffset.ToString("0.00") + " с" : ""));
            return lastPath;
        }

        public void Play(float from = 0f)
        {
            if (source == null || savedClip == null) { Report("записи нет"); return; }
            source.clip = savedClip;
            source.time = Mathf.Clamp(from, 0f, Mathf.Max(0f, Duration - 0.05f));
            source.Play();
            Report("воспроизведение голоса с " + KvTitlesService.Stamp(from));
        }

        public void StopPlayback()
        {
            if (source != null) source.Stop();
        }

        /// <summary>Воспроизведение, синхронизированное с видеорядом: время видео → время голоса.</summary>
        public void PlaySyncedToVideo(float videoSeconds)
        {
            float offset = videoOffset > 0f ? videoOffset : 0f;
            float target = videoSeconds - offset;
            if (target < 0f)
            {
                Report("в этот момент видео голос ещё не начался (смещение " +
                       offset.ToString("0.00") + " с)");
                return;
            }
            Play(target);
        }

        private void BuildWaveform(float[] data)
        {
            const int buckets = 240;
            waveform = new float[buckets];
            peak = 0f;
            int step = Mathf.Max(1, data.Length / buckets);
            for (int i = 0; i < buckets; i++)
            {
                int from = i * step;
                int to = Mathf.Min(data.Length, from + step);
                float sum = 0f;
                for (int k = from; k < to; k++) sum += data[k] * data[k];
                float rms = Mathf.Sqrt(sum / Mathf.Max(1, to - from));
                waveform[i] = rms;
                if (rms > peak) peak = rms;
            }
        }

        private string WriteWav(float[] data)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                string path = Path.Combine(Folder, "KazistovVv_voice_" + FeatureStorage.TimeStamp() + ".wav");
                using (FileStream stream = new FileStream(path, FileMode.Create))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    int dataBytes = data.Length * 2;
                    writer.Write(new[] { 'R', 'I', 'F', 'F' });
                    writer.Write(36 + dataBytes);
                    writer.Write(new[] { 'W', 'A', 'V', 'E' });
                    writer.Write(new[] { 'f', 'm', 't', ' ' });
                    writer.Write(16);
                    writer.Write((short)1);            // PCM
                    writer.Write((short)1);            // моно
                    writer.Write(frequency);
                    writer.Write(frequency * 2);       // байт в секунду
                    writer.Write((short)2);            // выравнивание блока
                    writer.Write((short)16);           // бит на отсчёт
                    writer.Write(new[] { 'd', 'a', 't', 'a' });
                    writer.Write(dataBytes);
                    for (int i = 0; i < data.Length; i++)
                    {
                        short value = (short)Mathf.Clamp(Mathf.RoundToInt(data[i] * 32767f),
                            short.MinValue, short.MaxValue);
                        writer.Write(value);
                    }
                }
                return path;
            }
            catch (Exception e)
            {
                Report("WAV не сохранён: " + e.Message);
                return "";
            }
        }

        /// <summary>Рядом с записью — текстовый список меток (титры и время) для монтажа.</summary>
        public void SaveMarkers()
        {
            try
            {
                if (string.IsNullOrEmpty(lastPath)) return;
                string path = Path.ChangeExtension(lastPath, ".markers.txt");
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Запись голоса KazistovVv (этап 33 ТЗ)");
                sb.AppendLine("файл: " + Path.GetFileName(lastPath));
                sb.AppendLine("длительность: " + Duration.ToString("0.000", CultureInfo.InvariantCulture) + " с");
                sb.AppendLine("смещение к видеозаписи: " +
                              (videoOffset >= 0f
                                  ? videoOffset.ToString("0.000", CultureInfo.InvariantCulture) + " с"
                                  : "видео не писалось"));
                if (startedAtSameTimeAsVideo > 0f) sb.AppendLine("старт совпал с видеозаписью");
                KvTitlesService titles = KvStageHub4.Current != null ? KvStageHub4.Current.Titles : null;
                if (titles != null && titles.Cues.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("метки титров:");
                    for (int i = 0; i < titles.Cues.Count; i++)
                        sb.AppendLine(KvTitlesService.Stamp(titles.Cues[i].start) + "  " +
                                      titles.Cues[i].text);
                }
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
                Report("метки сохранены: " + path);
            }
            catch (Exception e)
            {
                Report("метки не сохранены: " + e.Message);
            }
        }

        public string Status()
        {
            if (recording)
                return "идёт запись · " + RecordingTime.ToString("0.0") + " с из " + MaxSeconds + " с";
            if (!HasRecording) return "записи нет";
            return "запись " + Duration.ToString("0.0") + " с · пик " + peak.ToString("0.000") +
                   (videoOffset >= 0f ? " · смещение к видео " + videoOffset.ToString("0.00") + " с"
                                      : " · без привязки к видео") +
                   (Playing ? " · воспроизведение " + PlaybackTime.ToString("0.0") + " с" : "");
        }

        private void Report(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            Debug.Log("[VoiceOver] " + text);
            if (Message != null) Message(text);
        }
    }

    /// <summary>ВКЛАДКА «КИНОРЕЖИМ» (ЭТАП 31 ТЗ).</summary>
    public class KvCinemaTab : IKvWorkbenchTab
    {
        private readonly KvCinematicService cinema;
        private KvSegmented modeSegment;

        public KvCinemaTab(KvCinematicService cinematic) { cinema = cinematic; }

        public string Key { get { return "cine"; } }
        public string Title { get { return KvLocExtra3.T("cine.title", "Кинематографический режим"); } }
        private static string T(string k, string f) { return KvLocExtra3.T(k, f); }

        public void Build(KvTabKit kit)
        {
            if (kit == null) return;
            kit.Section(T("cine.title", "Кинематографический режим"));
            kit.Toggle(T("cine.enable", "Включить кинорежим"), cinema.Enabled, cinema.SetEnabled);
            modeSegment = kit.Segmented(T("cine.camera", "Камера"),
                new[]
                {
                    T("cine.cam.orbit", "облёт"),
                    T("cine.cam.follow", "за инструментом"),
                    T("cine.cam.tripod", "штатив")
                }, (int)cinema.Mode, delegate (int i) { cinema.SetMode((KvCinematicService.CameraMode)i); });
            kit.Toggle(T("cine.bars", "Чёрные полосы (широкий экран)"), true, cinema.SetBars);
            kit.Slider(T("cine.barheight", "Высота полос"), 0f, 0.25f, cinema.BarHeight, "0.00",
                cinema.SetBarHeight);
            kit.Slider(T("cine.radius", "Радиус облёта, м"), 0.8f, 12f, 3.4f, "0.0",
                delegate (float v) { cinema.SetOrbit(v, 2.1f, 6f); });
            kit.Info(delegate { return cinema.Status(); }, KvTheme.Accent);
            kit.Note(T("cine.info",
                "Камера движется плавно, положение и угол обзора восстанавливаются при выходе. " +
                "Клавиши не перехватываются: управление камерой снова у оператора."), KvTheme.TextDim);
        }

        public void Tick() { }
        public void Refresh()
        {
            if (modeSegment != null && modeSegment.Index != (int)cinema.Mode)
                modeSegment.Set((int)cinema.Mode);
        }
    }

    /// <summary>ВКЛАДКА «ТИТРЫ И СУБТИТРЫ» (ЭТАП 32 ТЗ).</summary>
    public class KvTitlesTab : IKvWorkbenchTab
    {
        private readonly KvTitlesService titles;
        private readonly Func<Vector3> aimProvider;
        private InputField startField, endField, textField;

        public KvTitlesTab(KvTitlesService service, Func<Vector3> aim)
        {
            titles = service;
            aimProvider = aim;
        }

        public string Key { get { return "titles"; } }
        public string Title { get { return KvLocExtra3.T("titles.title", "Титры и подписи"); } }
        private static string T(string k, string f) { return KvLocExtra3.T(k, f); }

        public void Build(KvTabKit kit)
        {
            if (kit == null) return;
            kit.Section(T("titles.title", "Титры, подписи, пояснения"));
            kit.Toggle(T("titles.show", "Показывать надписи"), titles.Visible, titles.SetVisible);

            startField = KvInputKit.Single(kit.Content, T("titles.start", "Начало, с"), "0", "0.0", null);
            endField = KvInputKit.Single(kit.Content, T("titles.end", "Конец, с"), "3", "3.0", null);
            textField = KvInputKit.Single(kit.Content, T("titles.text", "Текст"), "", "например: Вид сверху",
                null);

            kit.Buttons(new[]
            {
                T("titles.add.title", "Добавить титр"),
                T("titles.add.sub", "Добавить подзаголовок"),
                T("titles.add.low", "Добавить нижнюю треть")
            }, new Action[]
            {
                delegate { AddCue(0); },
                delegate { AddCue(1); },
                delegate { AddCue(2); }
            });

            kit.Buttons(new[]
            {
                T("titles.play", "Показ"),
                T("titles.stop", "Пауза"),
                T("titles.reset", "В начало"),
                T("titles.delete", "Удалить последнюю"),
                T("titles.clear", "Очистить")
            }, new Action[]
            {
                delegate { titles.Play(); },
                delegate { titles.Stop(); },
                delegate { titles.Reset(); },
                delegate { titles.RemoveCue(titles.Cues.Count - 1); },
                delegate { titles.ClearCues(); }
            });

            kit.Info(delegate { return titles.Status(); }, KvTheme.Accent);
            kit.Info(delegate
            {
                if (titles.Cues.Count == 0) return T("titles.none", "надписей нет");
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < titles.Cues.Count && i < 4; i++)
                {
                    KvTitleCue cue = titles.Cues[i];
                    if (i > 0) sb.Append(" · ");
                    sb.Append(KvTitlesService.Stamp(cue.start)).Append(' ').Append(cue.text);
                }
                return sb.ToString();
            }, KvTheme.TextMain);

            kit.Divider();
            kit.Section(T("titles.notes", "Пояснения к точкам сцены"));
            kit.Buttons(new[]
            {
                T("titles.note.add", "Пояснение в точку взгляда"),
                T("titles.note.clear", "Убрать пояснения")
            }, new Action[]
            {
                delegate
                {
                    Vector3 point = aimProvider != null ? aimProvider() : Vector3.zero;
                    titles.AddAnnotation(point, T("titles.note.default", "пояснение"),
                        new Color(0.55f, 0.85f, 1f));
                },
                delegate { titles.ClearAnnotations(); }
            });
            kit.Info(delegate
            {
                return T("titles.notes.count", "Пояснений") + ": " + titles.Annotations.Count;
            }, KvTheme.TextDim);

            kit.Divider();
            kit.Section(T("titles.srt", "Субтитры (SRT)"));
            kit.Buttons(new[] { T("titles.srt.export", "Выгрузить .srt") },
                new Action[] { delegate { titles.ExportSrt(FeatureStorage.Root, "KazistovVv_subtitles"); } });
            kit.Note(T("titles.info",
                "Файл .srt открывается в видеоредакторах и плеерах: надписи подхватятся по времени. " +
                "Пояснения привязаны к точкам сцены и двигаются вместе с камерой."), KvTheme.TextDim);
        }

        private void AddCue(int kind)
        {
            float start = Parse(startField, 0f);
            float end = Parse(endField, start + 3f);
            string text = textField != null ? textField.text : "";
            if (string.IsNullOrEmpty(text))
                text = kind == 0 ? T("titles.sample.title", "KazistovVv · робот в работе")
                                 : T("titles.sample.sub", "траектория, лимиты и зазоры");
            titles.AddCue(start, end, text, kind);
            if (textField != null) textField.text = "";
            if (endField != null) endField.text = (end + 3f).ToString("0.0");
            if (startField != null) startField.text = (end + 0.2f).ToString("0.0");
        }

        private static float Parse(InputField field, float fallback)
        {
            if (field == null) return fallback;
            float value;
            return float.TryParse(field.text.Replace(',', '.'), NumberStyles.Float,
                CultureInfo.InvariantCulture, out value) ? value : fallback;
        }

        public void Tick() { }
        public void Refresh() { }
    }

    /// <summary>ВКЛАДКА «ГОЛОС ДИКТОРА» (ЭТАП 33 ТЗ).</summary>
    public class KvVoiceOverTab : IKvWorkbenchTab
    {
        private readonly KvVoiceOverService voice;
        private KvMiniPlot plot;
        private InputField syncField;

        public KvVoiceOverTab(KvVoiceOverService service) { voice = service; }

        public string Key { get { return "voiceover"; } }
        public string Title { get { return KvLocExtra3.T("vo.title", "Голос диктора"); } }
        private static string T(string k, string f) { return KvLocExtra3.T(k, f); }

        public void Build(KvTabKit kit)
        {
            if (kit == null) return;
            kit.Section(T("vo.title", "Запись голоса диктора"));
            kit.Buttons(new[]
            {
                voice.Recording ? T("vo.stop", "Остановить запись") : T("vo.start", "Начать запись"),
                T("vo.play", "Прослушать"),
                T("vo.stopplay", "Стоп")
            }, new Action[]
            {
                delegate { if (voice.Recording) voice.Stop(); else voice.Start(); },
                delegate { voice.Play(); },
                delegate { voice.StopPlayback(); }
            });
            kit.Info(delegate { return voice.Status(); },
                voice.Recording ? KvTheme.Error : KvTheme.Accent);
            kit.Info(delegate
            {
                return T("vo.folder", "Папка записей") + ": " + voice.Folder;
            }, KvTheme.TextDim);

            plot = KvMiniPlot.Create(kit.Content, "VoicePlot", 420f, 110f);
            kit.Slider(T("vo.timeline", "Время видеоряда, с"), 0f, 120f, 0f, "0.0", delegate (float v)
            {
                voice.PlaySyncedToVideo(v);
            });
            syncField = KvInputKit.Single(kit.Content, T("vo.sync", "Перейти к моменту видео, с"),
                "0", "0.0", delegate (string text)
                {
                    float value;
                    if (float.TryParse(text.Replace(',', '.'), NumberStyles.Float,
                            CultureInfo.InvariantCulture, out value))
                        voice.PlaySyncedToVideo(value);
                });
            kit.Buttons(new[] { T("vo.markers", "Обновить метки титров") },
                new Action[] { delegate { voice.SaveMarkers(); } });
            kit.Note(T("vo.info",
                "Голос пишется в WAV (16 бит, 44,1 кГц) в папку «Документы\\KazistovVv\\voiceover». " +
                "Если видео уже пишется, сохраняется СМЕЩЕНИЕ начала голоса относительно начала " +
                "видеозаписи — по нему голос выставляется на монтаже одним движением. Рядом " +
                "сохраняется список меток титров."), KvTheme.TextDim);
        }

        public void Tick() { }

        public void Refresh()
        {
            if (plot == null) return;
            float[] data = voice.Waveform;
            if (data == null || data.Length == 0) return;
            float duration = Mathf.Max(0.1f, voice.Duration);
            float[] xs = new float[data.Length];
            for (int i = 0; i < data.Length; i++) xs[i] = duration * i / Mathf.Max(1, data.Length - 1);
            plot.SetData(xs, data, T("vo.x", "время, с"), T("vo.y", "громкость"));
        }
    }
}
