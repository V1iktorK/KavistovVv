using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using KazistovVvUI;
using TrajectoryCore;

namespace KazistovVvFeatures
{
    /// <summary>Вариант оформления пульта (ТЗ: минимум два).</summary>
    public enum KvPendantVariant
    {
        Industrial = 0,   // Вариант A — как у KUKA / ABB / FANUC
        Addon = 1         // Вариант B — стиль проекта АДДОН (эталон ждём от заказчика)
    }

    /// <summary>Как пульт закреплён в сцене.</summary>
    public enum KvPendantAttach
    {
        Floating = 0,     // парит в сцене в заданной точке
        FollowCamera = 1, // «в руке»: следует за оператором
        AboveRobot = 2    // над стойкой активного робота
    }

    /// <summary>Режим ручного управления джойстиком.</summary>
    public enum KvPendantJog
    {
        Joint = 0,        // покачивание выбранного сустава
        World = 1         // сдвиг TCP в горизонтальной плоскости
    }

    /// <summary>
    /// ЭТАП 8 ТЗ — ВИРТУАЛЬНЫЙ ПУЛЬТ (TEACH PENDANT).
    ///
    /// ВНЕШНИЙ ВИД (ТЗ): прямоугольная панель на World Space Canvas — корпус, ЭКРАН,
    /// ДЖОЙСТИК и крупные кнопки, плюс индикаторы состояния (LED). Два варианта:
    ///   * ВАРИАНТ A (`Industrial`) — в стиле промышленных пультов: широкий корпус, тёмный
    ///     экран сверху, три ряда кнопок, красный грибок СТОП справа, LED-линейка;
    ///   * ВАРИАНТ B (`Addon`) — стиль проекта АДДОН: скруглённый корпус, крупный экран,
    ///     джойстик справа, ряд цветных кнопок под экраном. ЭТАЛОН ЖДЁТ ФОТО — оформление
    ///     сделано «заглушкой с базовым функционалом» (ТЗ), логика у вариантов общая.
    ///
    /// ФУНКЦИОНАЛ (ТЗ «заглушка с базовым функционалом»):
    ///   * ЭКРАН — координаты TCP (мир и от базы робота), текущее состояние State Machine,
    ///     имя робота, углы суставов и значение джойстика;
    ///   * ДЖОЙСТИК — выдаёт вектор (`JoystickVector`) и РЕАЛЬНО двигает робота: в режиме
    ///     `Joint` покачивает выбранный сустав, в режиме `World` сдвигает TCP по плоскости.
    ///     Движение идёт через `PoseValidator` (та же проверка лимитов и зазоров), только
    ///     когда робот свободен — план не выполняется;
    ///   * КНОПКИ: ПУСК, СТОП (это та же АВАРИЙНАЯ ОСТАНОВКА проекта), ДОМОЙ (переезд в
    ///     нулевую позу штатным планировщиком), ЗАПИСАТЬ ПОЗУ (та же библиотека поз, что в
    ///     панели функций);
    ///   * LED: ПИТАНИЕ, ДВИЖЕНИЕ, СВЯЗЬ, АВАРИЯ — перекрашиваются по фактическому состоянию.
    ///
    /// РАСПОЛОЖЕНИЕ: World Space Canvas — `Floating` (парит в заданной точке),
    /// `FollowCamera` (прикреплён к оператору) или `AboveRobot` (над стойкой робота).
    /// Объект интерфейса не скрывается из иерархии: это часть рабочего места, а не служебный
    /// визуал; коллайдеров у него нет, в `CollisionWorld` он не попадает.
    /// </summary>
    public class KvTeachPendant : MonoBehaviour
    {
        // ------------------------------------------------------------------ параметры
        [Header("Оформление")]
        public KvPendantVariant variant = KvPendantVariant.Industrial;
        public KvPendantAttach attach = KvPendantAttach.Floating;
        public Vector3 floatingPosition = new Vector3(1.05f, 1.32f, -24.6f);
        public float worldScale = 0.0016f;          // пиксель UI → метр
        public bool faceCamera = true;
        public float followDistance = 0.62f;
        public float followDrop = -0.16f;

        [Header("Ручное управление")]
        public KvPendantJog jogMode = KvPendantJog.Joint;
        public int jogAxis = 0;                     // выбранный сустав (0 — первый)
        public float jogSpeedDeg = 22f;             // град/с в режиме Joint
        public float jogSpeedMps = 0.18f;           // м/с в режиме World
        public bool jogEnabled = true;

        [Header("Диагностика")]
        public bool logEvents = true;

        public event Action<string> Message;

        // --- внешние действия (привязывает хаб: кнопки делают ровно то же, что интерфейс)
        public Action stopRequest;
        public Action homeRequest;
        public Action recordPoseRequest;
        public Func<bool> startRequest;

        // ------------------------------------------------------------------ состояние
        private TrajectoryFlowController flow;
        private PoseValidator v;
        private RobotController robot;
        private Canvas canvas;
        private RectTransform panel;
        private Camera cam;

        private Text screenTitle, screenTcp, screenState, screenRobot, screenJoints, screenStick;
        private Image ledPower, ledMotion, ledLink, ledAlarm;
        private RectTransform stickBase, stickKnob;
        private Image stickKnobImage;
        private Text modeLabel;
        private readonly Button[] axisButtons = new Button[8];
        private readonly Image[] axisButtonImages = new Image[8];

        private Vector2 stick;
        private bool stickHeld;
        private bool visible;
        private float uiTimer;

        private Color bodyColor, screenColor, buttonColor, accentColor;

        public bool Visible { get { return visible; } }
        public Vector2 JoystickVector { get { return stick; } }
        public KvPendantVariant Variant { get { return variant; } }
        public KvPendantAttach Attach { get { return attach; } }
        public KvPendantJog JogMode { get { return jogMode; } }
        public int JogAxis { get { return jogAxis; } }
        public string ScreenTcpText { get { return screenTcp != null ? screenTcp.text : ""; } }
        public string ScreenStateText { get { return screenState != null ? screenState.text : ""; } }

        // ================================================================== сборка

        public void Build(TrajectoryFlowController controller)
        {
            flow = controller;
            cam = Camera.main;
            if (panel != null) { ApplyPalette(); return; }

            ApplyPalette();

            GameObject canvasGo = new GameObject("KvPendantCanvas", typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;
            canvas.sortingOrder = 30;

            RectTransform canvasRect = (RectTransform)canvasGo.transform;
            canvasRect.sizeDelta = new Vector2(560f, 760f);

            GameObject panelGo = new GameObject("PendantBody", typeof(Image));
            panelGo.transform.SetParent(canvasRect, false);
            panel = (RectTransform)panelGo.transform;
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            Image body = panelGo.GetComponent<Image>();
            body.sprite = KvTheme.WhiteSprite;
            body.color = bodyColor;

            if (variant == KvPendantVariant.Industrial) BuildIndustrial();
            else BuildAddon();

            canvasRect.localScale = Vector3.one * worldScale;
            canvasRect.position = floatingPosition;
            canvasRect.rotation = Quaternion.Euler(0f, 180f, 0f);
            PlaceImmediate();

            SetVisible(false);
            Debug.Log("[Pendant] пульт собран: " +
                      (variant == KvPendantVariant.Industrial ? "вариант A (промышленный)" : "вариант B (АДДОН)") +
                      " · джойстик: " + (jogMode == KvPendantJog.Joint ? "по суставам" : "по TCP"));
        }

        private void ApplyPalette()
        {
            bodyColor = new Color(0.16f, 0.17f, 0.19f);
            screenColor = new Color(0.05f, 0.11f, 0.08f);
            buttonColor = new Color(0.30f, 0.32f, 0.35f);
            accentColor = new Color(0.30f, 0.62f, 0.95f);
        }

        // --- ВАРИАНТ A: промышленный пульт (KUKA/ABB/FANUC): экран, 3 ряда кнопок, грибок СТОП
        private void BuildIndustrial()
        {
            AddTitle("KAZISTOVV · TEACH PENDANT", "A");

            RectTransform screen = AddScreen(24f, 96f, 512f, 190f);
            BuildScreenTexts(screen);

            // LED-линейка под экраном
            RectTransform ledRow = AddRow(24f, 300f, 512f, 40f, new Color(0.10f, 0.11f, 0.13f));
            ledPower = AddLed(ledRow, 20f, "POWER", new Color(0.25f, 0.85f, 0.35f));
            ledMotion = AddLed(ledRow, 130f, "MOTION", new Color(1f, 0.80f, 0.20f));
            ledLink = AddLed(ledRow, 240f, "LINK", new Color(0.35f, 0.70f, 1f));
            ledAlarm = AddLed(ledRow, 350f, "ALARM", new Color(1f, 0.25f, 0.22f));

            // Ряд 1 — управление программой
            RectTransform row1 = AddRow(24f, 352f, 512f, 74f, bodyColor);
            AddButton(row1, "ПУСК", 6f, new Color(0.18f, 0.55f, 0.25f), delegate
            {
                if (startRequest != null) startRequest();
            });
            AddButton(row1, "СТОП", 176f, new Color(0.62f, 0.16f, 0.14f), delegate
            {
                if (stopRequest != null) stopRequest();
            });
            AddButton(row1, "ДОМОЙ", 346f, new Color(0.20f, 0.35f, 0.60f), delegate
            {
                if (homeRequest != null) homeRequest();
            });

            // Ряд 2 — поза и режимы
            RectTransform row2 = AddRow(24f, 436f, 512f, 74f, bodyColor);
            AddButton(row2, "ЗАПИСАТЬ\nПОЗУ", 6f, new Color(0.60f, 0.50f, 0.15f), delegate
            {
                if (recordPoseRequest != null) recordPoseRequest();
            });
            AddButton(row2, "РЕЖИМ\nJOINT/TCP", 176f, buttonColor, delegate { ToggleJogMode(); });
            AddButton(row2, "ВАРИАНТ\nA / B", 346f, buttonColor, delegate { ToggleVariant(); });

            // Ряд 3 — выбор сустава (кнопки 1..6)
            RectTransform row3 = AddRow(24f, 520f, 512f, 60f, bodyColor);
            for (int i = 0; i < 6; i++)
            {
                int index = i;
                Button b = AddButton(row3, (i + 1).ToString(), 6f + i * 86f,
                    new Color(0.26f, 0.28f, 0.31f), delegate { SetJogAxis(index); });
                b.GetComponent<RectTransform>().sizeDelta = new Vector2(74f, 48f);
                axisButtons[i] = b;
                axisButtonImages[i] = b.targetGraphic as Image;
            }

            // Джойстик слева внизу + грибок СТОП справа
            RectTransform joyRow = AddRow(24f, 590f, 512f, 150f, bodyColor);
            stickBase = AddCircle(joyRow, 30f, 20f, 130f, new Color(0.11f, 0.12f, 0.14f));
            stickKnob = AddCircle(stickBase, 40f, 30f, 64f, new Color(0.55f, 0.58f, 0.62f));
            stickKnobImage = stickKnob.GetComponent<Image>();
            AttachStick(stickBase);

            RectTransform mushroom = AddCircle(joyRow, 210f, 8f, 132f, new Color(0.75f, 0.16f, 0.12f));
            Button stopButton = mushroom.gameObject.AddComponent<Button>();
            stopButton.targetGraphic = mushroom.GetComponent<Image>();
            stopButton.onClick.AddListener(delegate
            {
                if (stopRequest != null) stopRequest();
            });
            AddCaption(joyRow, "АВАРИЙНЫЙ СТОП", 210f, 142f, 132f, new Color(1f, 0.75f, 0.7f));

            modeLabel = AddCaption(joyRow, "", 380f, 8f, 130f, KvTheme.TextDim);
        }

        // --- ВАРИАНТ B: «стиль проекта АДДОН» — заглушка: скруглённый корпус, крупный экран,
        //     джойстик справа, ряд цветных кнопок. Ждём фото-эталон для точной отрисовки.
        private void BuildAddon()
        {
            AddTitle("KAZISTOVV · ADDON STYLE", "B");

            RectTransform screen = AddScreen(20f, 92f, 520f, 236f);
            BuildScreenTexts(screen);

            RectTransform ledRow = AddRow(20f, 344f, 520f, 36f, new Color(0.10f, 0.11f, 0.13f));
            ledPower = AddLed(ledRow, 16f, "PWR", new Color(0.25f, 0.85f, 0.35f));
            ledMotion = AddLed(ledRow, 142f, "MOVE", new Color(1f, 0.80f, 0.20f));
            ledLink = AddLed(ledRow, 268f, "LINK", new Color(0.35f, 0.70f, 1f));
            ledAlarm = AddLed(ledRow, 394f, "ALARM", new Color(1f, 0.25f, 0.22f));

            // Цветной ряд кнопок под экраном
            RectTransform row1 = AddRow(20f, 394f, 520f, 66f, bodyColor);
            AddButton(row1, "▶", 8f, new Color(0.20f, 0.60f, 0.28f), delegate
            {
                if (startRequest != null) startRequest();
            }).GetComponent<RectTransform>().sizeDelta = new Vector2(118f, 54f);
            AddButton(row1, "■", 136f, new Color(0.70f, 0.20f, 0.16f), delegate
            {
                if (stopRequest != null) stopRequest();
            }).GetComponent<RectTransform>().sizeDelta = new Vector2(118f, 54f);
            AddButton(row1, "⌂", 264f, new Color(0.22f, 0.40f, 0.68f), delegate
            {
                if (homeRequest != null) homeRequest();
            }).GetComponent<RectTransform>().sizeDelta = new Vector2(118f, 54f);
            AddButton(row1, "●", 392f, new Color(0.66f, 0.55f, 0.16f), delegate
            {
                if (recordPoseRequest != null) recordPoseRequest();
            }).GetComponent<RectTransform>().sizeDelta = new Vector2(118f, 54f);

            // Джойстик справа, кнопки осей слева
            RectTransform joyRow = AddRow(20f, 474f, 520f, 250f, bodyColor);
            stickBase = AddCircle(joyRow, 300f, 30f, 210f, new Color(0.11f, 0.12f, 0.14f));
            stickKnob = AddCircle(stickBase, 60f, 52f, 100f, new Color(0.60f, 0.63f, 0.68f));
            stickKnobImage = stickKnob.GetComponent<Image>();
            AttachStick(stickBase);

            for (int i = 0; i < 6; i++)
            {
                int index = i;
                int col = i % 2, row = i / 2;
                Button b = AddButton(joyRow, (i + 1).ToString(), 12f + col * 132f, 18f + row * 74f,
                    new Color(0.26f, 0.28f, 0.31f), delegate { SetJogAxis(index); });
                b.GetComponent<RectTransform>().sizeDelta = new Vector2(122f, 66f);
                axisButtons[i] = b;
                axisButtonImages[i] = b.targetGraphic as Image;
            }

            AddCaption(joyRow, "ЭТАЛОН ОФОРМЛЕНИЯ ЖДЁМ (фото АДДОН)", 12f, 232f, 260f,
                KvTheme.TextDim);
            modeLabel = AddCaption(joyRow, "", 300f, 242f, 210f, KvTheme.TextDim);
        }

        private void AddTitle(string text, string tag)
        {
            GameObject go = new GameObject("Title", typeof(Image));
            go.transform.SetParent(panel, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(-24f, 62f);
            rt.anchoredPosition = new Vector2(0f, -16f);
            Image bg = go.GetComponent<Image>();
            bg.sprite = KvTheme.WhiteSprite;
            bg.color = new Color(0.10f, 0.11f, 0.13f);

            Text t = KvTheme.CreateText(rt, "Label", text, 22, TextAnchor.MiddleCenter,
                new Color(0.85f, 0.90f, 0.95f));
            KvTheme.Stretch(t.rectTransform, 8f, 4f);

            Text badge = KvTheme.CreateText(rt, "Badge", "ВАРИАНТ " + tag, 18,
                TextAnchor.MiddleRight, accentColor);
            KvTheme.Stretch(badge.rectTransform, 8f, 4f);
        }

        private RectTransform AddScreen(float x, float y, float w, float h)
        {
            GameObject go = new GameObject("Screen", typeof(Image));
            go.transform.SetParent(panel, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, -y);
            Image img = go.GetComponent<Image>();
            img.sprite = KvTheme.WhiteSprite;
            img.color = screenColor;
            return rt;
        }

        private void BuildScreenTexts(RectTransform screen)
        {
            screenTitle = KvTheme.CreateText(screen, "ScreenTitle", "TCP", 17,
                TextAnchor.UpperLeft, new Color(0.55f, 0.95f, 0.65f));
            screenTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            screenTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            screenTitle.rectTransform.pivot = new Vector2(0.5f, 1f);
            screenTitle.rectTransform.sizeDelta = new Vector2(-20f, 22f);
            screenTitle.rectTransform.anchoredPosition = new Vector2(0f, -8f);

            screenTcp = MakeLine(screen, "Tcp", -32f, 24, new Color(0.70f, 1f, 0.80f));
            screenRobot = MakeLine(screen, "Robot", -78f, 17, new Color(0.60f, 0.85f, 0.75f));
            screenState = MakeLine(screen, "State", -102f, 17, new Color(0.95f, 0.85f, 0.45f));
            screenJoints = MakeLine(screen, "Joints", -124f, 15, new Color(0.55f, 0.85f, 0.65f));
            screenStick = MakeLine(screen, "Stick", -168f, 15, new Color(0.50f, 0.80f, 0.95f));
        }

        private static Text MakeLine(RectTransform parent, string name, float y, int size, Color color)
        {
            Text t = KvTheme.CreateText(parent, name, "", size, TextAnchor.UpperLeft, color);
            t.rectTransform.anchorMin = new Vector2(0f, 1f);
            t.rectTransform.anchorMax = new Vector2(1f, 1f);
            t.rectTransform.pivot = new Vector2(0.5f, 1f);
            t.rectTransform.sizeDelta = new Vector2(-20f, size + 8f);
            t.rectTransform.anchoredPosition = new Vector2(0f, y);
            return t;
        }

        private RectTransform AddRow(float x, float y, float w, float h, Color color)
        {
            GameObject go = new GameObject("Row", typeof(Image));
            go.transform.SetParent(panel, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, -y);
            Image img = go.GetComponent<Image>();
            img.sprite = KvTheme.WhiteSprite;
            img.color = color;
            img.raycastTarget = false;
            return rt;
        }

        /// <summary>Кнопка с явной вертикальной позицией (вариант B — сетка кнопок осей).</summary>
        private Button AddButton(RectTransform parent, string label, float x, float y, Color color,
            Action onClick)
        {
            Button b = AddButton(parent, label, x, color, onClick);
            RectTransform rt = b.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(x, -y);
            return b;
        }

        private Button AddButton(RectTransform parent, string label, float x, Color color, Action onClick)
        {
            GameObject go = new GameObject("Btn_" + label.Replace('\n', ' '), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(160f, 62f);
            rt.anchoredPosition = new Vector2(x, -6f);

            Image img = go.GetComponent<Image>();
            img.sprite = KvTheme.WhiteSprite;
            img.color = color;
            img.raycastTarget = true;

            Button b = go.GetComponent<Button>();
            b.targetGraphic = img;
            ColorBlock cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            b.colors = cb;
            b.onClick.AddListener(delegate { if (onClick != null) onClick(); });

            Text t = KvTheme.CreateText(rt, "Label", label, 20, TextAnchor.MiddleCenter,
                new Color(0.95f, 0.96f, 0.98f));
            KvTheme.Stretch(t.rectTransform, 6f, 4f);
            t.raycastTarget = false;
            return b;
        }

        private RectTransform AddCircle(RectTransform parent, float x, float y, float size, Color color)
        {
            GameObject go = new GameObject("Circle", typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = new Vector2(x, -y);
            Image img = go.GetComponent<Image>();
            img.sprite = KvTheme.WhiteSprite;      // квадрат со скруглением роли не играет в World Space
            img.color = color;
            img.raycastTarget = true;
            return rt;
        }

        private Text AddCaption(RectTransform parent, string text, float x, float y, float w, Color color)
        {
            Text t = KvTheme.CreateText(parent, "Caption", text, 15, TextAnchor.UpperLeft, color);
            t.rectTransform.anchorMin = new Vector2(0f, 1f);
            t.rectTransform.anchorMax = new Vector2(0f, 1f);
            t.rectTransform.pivot = new Vector2(0f, 1f);
            t.rectTransform.sizeDelta = new Vector2(w, 26f);
            t.rectTransform.anchoredPosition = new Vector2(x, -y);
            return t;
        }

        private Image AddLed(RectTransform parent, float x, string name, Color color)
        {
            RectTransform holder = AddRow(x, 0f, 110f, 34f, new Color(0f, 0f, 0f, 0f));
            holder.SetParent(parent, false);
            holder.anchoredPosition = new Vector2(0f, -4f);

            RectTransform lamp = AddCircle(holder, 2f, 4f, 24f, color);
            Image lampImage = lamp.GetComponent<Image>();
            lampImage.raycastTarget = false;
            Text t = KvTheme.CreateText(holder, "Led" + name, name, 13, TextAnchor.UpperLeft,
                new Color(0.70f, 0.72f, 0.75f));
            t.rectTransform.anchorMin = new Vector2(0f, 1f);
            t.rectTransform.anchorMax = new Vector2(0f, 1f);
            t.rectTransform.pivot = new Vector2(0f, 1f);
            t.rectTransform.sizeDelta = new Vector2(86f, 20f);
            t.rectTransform.anchoredPosition = new Vector2(30f, -8f);
            t.raycastTarget = false;
            return lampImage;
        }

        private void AttachStick(RectTransform baseRect)
        {
            KvPendantStick handler = baseRect.gameObject.AddComponent<KvPendantStick>();
            handler.Init(this, baseRect);
        }

        // ================================================================== управление

        public void SetVisible(bool value)
        {
            visible = value;
            if (canvas != null) canvas.gameObject.SetActive(value);
            if (value) { PlaceImmediate(); Refresh(); }
            Say("пульт: " + (value ? "показан" : "скрыт") +
                (variant == KvPendantVariant.Industrial ? " (вариант A)" : " (вариант B)"));
        }

        public bool Toggle()
        {
            SetVisible(!visible);
            return visible;
        }

        /// <summary>Переключить вариант оформления ON-THE-FLY (ТЗ: минимум два варианта).</summary>
        public void ToggleVariant()
        {
            KvPendantVariant next = variant == KvPendantVariant.Industrial
                ? KvPendantVariant.Addon : KvPendantVariant.Industrial;
            SetVariant(next);
        }

        public void SetVariant(KvPendantVariant value)
        {
            if (value == variant && panel != null) return;
            variant = value;
            if (panel != null)
            {
                // Полная пересборка тела пульта (корпус тот же Canvas, меняется начинка).
                for (int i = panel.childCount - 1; i >= 0; i--)
                    Destroy(panel.GetChild(i).gameObject);
                if (variant == KvPendantVariant.Industrial) BuildIndustrial();
                else BuildAddon();
                PlaceImmediate();
                Refresh();
            }
            Say("вариант пульта: " + (variant == KvPendantVariant.Industrial
                ? "A — промышленный (KUKA/ABB/FANUC)"
                : "B — стиль проекта АДДОН (заглушка, ждём эталон)"));
        }

        public void SetAttach(KvPendantAttach value)
        {
            attach = value;
            PlaceImmediate();
            Say("пульт закреплён: " + (attach == KvPendantAttach.Floating ? "парит в сцене"
                : attach == KvPendantAttach.FollowCamera ? "в руке оператора" : "над стойкой робота"));
        }

        public void SetJogMode(KvPendantJog value)
        {
            jogMode = value;
            Say("джойстик: " + (jogMode == KvPendantJog.Joint ? "по суставам" : "по TCP (плоскость)"));
        }

        public void ToggleJogMode()
        {
            SetJogMode(jogMode == KvPendantJog.Joint ? KvPendantJog.World : KvPendantJog.Joint);
        }

        public void SetJogAxis(int index)
        {
            jogAxis = Mathf.Max(0, index);
            Say("джойстик управляет суставом " + (jogAxis + 1));
        }

        /// <summary>Внешний ввод стика (VR-контроллер, автотест, геймпад).</summary>
        public void SetStick(Vector2 value)
        {
            stick = Vector2.ClampMagnitude(value, 1f);
            UpdateKnob();
        }

        // ================================================================== кадровое обслуживание

        public void Tick(float dt)
        {
            if (!visible) return;
            if (flow != null && flow.Validator != null) v = flow.Validator;

            Place(dt);
            ApplyJog(dt);

            uiTimer -= dt;
            if (uiTimer > 0f) return;
            uiTimer = 0.1f;
            Refresh();
        }

        /// <summary>Размещение пульта: парение / в руке / над роботом.</summary>
        private void Place(float dt)
        {
            if (canvas == null) return;
            RectTransform rect = (RectTransform)canvas.transform;

            Vector3 target = floatingPosition;
            Quaternion rot = rect.rotation;

            switch (attach)
            {
                case KvPendantAttach.FollowCamera:
                    if (cam == null) cam = Camera.main;
                    if (cam != null)
                    {
                        target = cam.transform.position + cam.transform.forward * followDistance +
                                 Vector3.up * followDrop;
                        rot = Quaternion.LookRotation(target - cam.transform.position, Vector3.up);
                    }
                    break;
                case KvPendantAttach.AboveRobot:
                    if (flow != null && flow.Robot != null)
                        target = flow.Robot.transform.position + new Vector3(0.85f, 1.25f, 0.55f);
                    break;
            }

            rect.position = Vector3.Lerp(rect.position, target, Mathf.Clamp01(dt * 8f));
            if (faceCamera && cam != null && attach != KvPendantAttach.FollowCamera)
            {
                Vector3 dir = rect.position - cam.transform.position;
                if (dir.sqrMagnitude > 1e-4f)
                    rect.rotation = Quaternion.Slerp(rect.rotation,
                        Quaternion.LookRotation(dir, Vector3.up), Mathf.Clamp01(dt * 6f));
            }
        }

        private void PlaceImmediate()
        {
            if (canvas == null) return;
            RectTransform rect = (RectTransform)canvas.transform;
            switch (attach)
            {
                case KvPendantAttach.FollowCamera:
                    if (cam == null) cam = Camera.main;
                    if (cam != null)
                        rect.position = cam.transform.position + cam.transform.forward * followDistance +
                                        Vector3.up * followDrop;
                    break;
                case KvPendantAttach.AboveRobot:
                    if (flow != null && flow.Robot != null)
                        rect.position = flow.Robot.transform.position + new Vector3(0.85f, 1.25f, 0.55f);
                    else rect.position = floatingPosition;
                    break;
                default:
                    rect.position = floatingPosition;
                    break;
            }
        }

        /// <summary>
        /// Ручное управление джойстиком. Робот двигается ТОЛЬКО когда он свободен
        /// (не выполняет план), с проверкой лимитов и зазоров тем же `PoseValidator`.
        /// </summary>
        private void ApplyJog(float dt)
        {
            if (!jogEnabled || stick.sqrMagnitude < 0.02f) return;
            if (v == null || !v.Ready) return;
            if (flow == null) return;
            if (flow.ExternalMotionRunning || flow.State.phase == FlowState.RobotMoving) return;

            if (jogMode == KvPendantJog.Joint)
            {
                int axis = Mathf.Clamp(jogAxis, 0, v.Dof - 1);
                double[] q = new double[v.Dof];
                v.CopyCurrentInto(q);

                double delta = v.IsPrismatic(axis)
                    ? stick.y * jogSpeedMps * dt
                    : stick.y * jogSpeedDeg * dt;
                q[axis] += delta;
                q = v.ContinueFrom(v.CopyCurrent(), q);
                if (!v.WithinLimits(q, 0f)) return;
                if (!Safe(q)) return;
                v.Apply(q);
            }
            else
            {
                double[] q = new double[v.Dof];
                v.CopyCurrentInto(q);
                Vector3 tcp = v.TcpAt(q);
                Vector3 move = new Vector3(stick.x, 0f, stick.y) * jogSpeedMps * dt;
                double[] solved;
                if (!v.SolveIk(tcp + move, q, out solved, 30) || solved == null) return;
                if (!v.WithinLimits(solved, 0f)) return;
                if (!Safe(solved)) return;
                v.Apply(solved);
            }
        }

        /// <summary>Проверка позы «без столкновений» (мир столкновений потока не трогаем).</summary>
        private bool Safe(double[] q)
        {
            if (v == null || !v.Ready) return false;
            int a, b;
            float self = v.SelfClearance(q, out a, out b);
            return self > 0.01f;
        }

        // ================================================================== экран и LED

        public void Refresh()
        {
            if (flow == null || flow.Validator == null || !flow.Validator.Ready) return;
            v = flow.Validator;
            robot = flow.Robot;

            double[] q = new double[v.Dof];
            v.CopyCurrentInto(q);
            Vector3 tcp = v.TcpAt(q);
            Vector3 local = robot != null ? robot.transform.InverseTransformPoint(tcp) : tcp;

            if (screenTitle != null)
                screenTitle.text = "TCP  ·  " + (robot != null ? robot.robotName : "—");
            if (screenTcp != null)
                screenTcp.text = "МИР   X " + tcp.x.ToString("+0.000;-0.000") +
                                 "  Y " + tcp.y.ToString("+0.000;-0.000") +
                                 "  Z " + tcp.z.ToString("+0.000;-0.000") + "\n" +
                                 "БАЗА  X " + local.x.ToString("+0.000;-0.000") +
                                 "  Y " + local.y.ToString("+0.000;-0.000") +
                                 "  Z " + local.z.ToString("+0.000;-0.000");
            if (screenRobot != null)
                screenRobot.text = "ОСЕЙ: " + v.Dof + " · деталь: " +
                                   (flow.Motion != null && flow.Motion.IsRunning ? "ДВИЖЕНИЕ" : "СТОП");
            if (screenState != null)
                screenState.text = "СОСТОЯНИЕ: " + flow.State.phase;
            if (screenJoints != null)
            {
                string text = "УГЛЫ: ";
                for (int i = 0; i < q.Length; i++)
                {
                    if (i > 0) text += "  ";
                    text += "J" + (i + 1) + " " + (v.IsPrismatic(i)
                        ? (q[i] * 1000.0).ToString("0") + " мм"
                        : q[i].ToString("0.0") + "°");
                }
                screenJoints.text = text;
            }
            if (screenStick != null)
                screenStick.text = "ДЖОЙСТИК: " + (jogMode == KvPendantJog.Joint ? "JOINT" : "TCP") +
                                   " · ось " + (jogAxis + 1) +
                                   " · стик (" + stick.x.ToString("0.00") + ", " + stick.y.ToString("0.00") + ")" +
                                   (stickHeld ? " · УДЕРЖИВАЕТСЯ" : "");

            if (modeLabel != null)
                modeLabel.text = "Режим: " + (jogMode == KvPendantJog.Joint ? "JOINT" : "TCP") +
                                 " · ось " + (jogAxis + 1) + " · " +
                                 (attach == KvPendantAttach.Floating ? "в сцене"
                                     : attach == KvPendantAttach.FollowCamera ? "в руке" : "над роботом");

            // LED — по фактическому состоянию робота и потока.
            bool moving = (flow.Motion != null && flow.Motion.IsRunning) || flow.ExternalMotionRunning;
            bool alarm = !v.WithinLimits(q, 1f);
            SetLed(ledPower, new Color(0.25f, 0.85f, 0.35f), true);
            SetLed(ledMotion, new Color(1f, 0.80f, 0.20f), moving);
            SetLed(ledLink, new Color(0.35f, 0.70f, 1f), robot != null);
            SetLed(ledAlarm, new Color(1f, 0.25f, 0.22f), alarm);

            for (int i = 0; i < axisButtons.Length; i++)
            {
                if (axisButtons[i] == null) continue;
                bool active = i == jogAxis && i < v.Dof;
                axisButtons[i].interactable = i < v.Dof;
                if (axisButtonImages[i] != null)
                    axisButtonImages[i].color = active
                        ? new Color(0.25f, 0.55f, 0.85f)
                        : (i < v.Dof ? new Color(0.26f, 0.28f, 0.31f) : new Color(0.18f, 0.19f, 0.21f));
            }
        }

        private static void SetLed(Image led, Color color, bool on)
        {
            if (led == null) return;
            led.color = on ? color : new Color(color.r * 0.22f, color.g * 0.22f, color.b * 0.22f, 1f);
        }

        private void UpdateKnob()
        {
            if (stickKnob == null) return;
            stickKnob.anchoredPosition = new Vector2(stick.x * 26f, -stick.y * 26f);
            if (stickKnobImage != null)
                stickKnobImage.color = stickHeld
                    ? new Color(0.80f, 0.85f, 0.95f)
                    : new Color(0.55f, 0.58f, 0.62f);
        }

        // ================================================================== API для стика

        internal void OnStickDrag(Vector2 local, float radius)
        {
            stick = Vector2.ClampMagnitude(local / Mathf.Max(1f, radius), 1f);
            UpdateKnob();
        }

        internal void OnStickRelease()
        {
            stick = Vector2.zero;
            stickHeld = false;
            UpdateKnob();
            Say("джойстик отпущен — вектор сброшен");
        }

        internal void OnStickPress()
        {
            stickHeld = true;
            UpdateKnob();
            Say("джойстик захвачен: " + (jogMode == KvPendantJog.Joint
                ? "покачивание сустава " + (jogAxis + 1)
                : "сдвиг TCP в плоскости"));
        }

        public string Status
        {
            get
            {
                return KvLoc.T("pendant.title", "Виртуальный пульт") + ": " +
                       (visible ? "показан" : "скрыт") + " · " +
                       (variant == KvPendantVariant.Industrial ? "вариант A" : "вариант B") +
                       " · " + (jogMode == KvPendantJog.Joint ? "JOINT" : "TCP") +
                       " · ось " + (jogAxis + 1) +
                       " · " + (attach == KvPendantAttach.Floating ? "в сцене"
                           : attach == KvPendantAttach.FollowCamera ? "в руке" : "над роботом");
            }
        }

        private void Say(string message)
        {
            if (logEvents) Debug.Log("[Pendant] " + message);
            if (Message != null) Message(message);
        }

        private void OnDestroy()
        {
            if (canvas != null) Destroy(canvas.gameObject);
        }
    }

    /// <summary>Обработчик захвата/перетаскивания джойстика (uGUI, работает и в World Space).</summary>
    public class KvPendantStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private KvTeachPendant owner;
        private RectTransform baseRect;

        public void Init(KvTeachPendant pendant, RectTransform rect)
        {
            owner = pendant;
            baseRect = rect;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (owner == null) return;
            owner.OnStickPress();
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (owner == null || baseRect == null) return;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(baseRect, eventData.position,
                eventData.pressEventCamera, out local)) return;
            owner.OnStickDrag(local, baseRect.sizeDelta.x * 0.36f);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (owner == null) return;
            owner.OnStickRelease();
        }
    }
}
