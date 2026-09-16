using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using KazistovVvUI;
using TrajectoryCore;

namespace KazistovVvFeatures
{
    /// <summary>
    /// ЭТАП 1 ТЗ: ГЛАВНОЕ МЕНЮ ПРИ ЗАПУСКЕ.
    ///
    /// Что делает:
    ///   • показывает экран запуска поверх сцены: «Новый проект» · «Открыть сессию» ·
    ///     «Показать демо» (этап 3) · «Обучение» (этап 2) · «Настройки» · «Выход»;
    ///   • фон — МЕДЛЕННЫЙ КИНЕМАТОГРАФИЧЕСКИЙ ОБЛЁТ стенда: камера сама идёт по кругу,
    ///     плавно меняет высоту и держит робота в кадре; управление оператора на это время
    ///     отключается так же, как в презентационном режиме (скорости обнуляются, лазеры
    ///     выключаются) — состояние возвращается при выходе из меню;
    ///   • кнопки — ТОЛЬКО иконки в стиле FreeCAD: подсказка всплывает у кнопки и
    ///     дублируется в строке-подсказке внизу экрана;
    ///   • все тексты локализованы (см. <see cref="KvLocExtra"/>, 7 языков).
    ///
    /// Меню переживает перезапуск (PlayerPrefs): если оператор выключил показ при запуске,
    /// приложение открывается сразу в рабочей области, а вернуть экран можно клавишей F1
    /// или командой «Главное меню».
    ///
    /// Сцена и существующая оболочка НЕ меняются: меню — отдельный канвас, который
    /// показывается вместо оболочки (оболочка гасится штатным `SetUiVisible`).
    /// </summary>
    public class KvStartMenu : MonoBehaviour
    {
        /// <summary>Ключ PlayerPrefs: показывать меню при запуске.</summary>
        public const string PrefsKey = "KazistovVv.StartMenu.Enabled";

        public int sortingOrder = 200;
        public float panelWidth = 330f;

        [Header("Кинематографический облёт")]
        public float orbitRadius = 3.6f;
        public float orbitHeight = 1.75f;
        public float orbitSpeed = 6.5f;         // градусов в секунду
        public float heightWave = 0.35f;        // амплитуда плавного подъёма/спуска, м
        public float heightWavePeriod = 26f;    // секунд на полный цикл
        public float lookSmoothing = 3.2f;
        public bool cinematic = true;

        public event Action<string> Message;

        // ------------------------------------------------------------------ состояние
        private Canvas canvas;
        private RectTransform canvasRect;
        private RectTransform root;
        private Text hintText;
        private Text captionText;
        private KvMiniTip tip;
        private readonly List<Button> buttons = new List<Button>();
        private readonly List<Image> buttonFrames = new List<Image>();
        private readonly List<string> buttonIcons = new List<string>();
        private readonly List<Action> buttonActions = new List<Action>();
        private readonly List<Text> buttonCaptions = new List<Text>();
        private int selected;

        private bool visible;
        private bool rigSaved;
        private Camera camera;
        private TrajectoryFlowController flow;
        private FreeFlyCameraController rig;

        private Vector3 savedPosition;
        private Quaternion savedRotation;
        private float savedMoveSpeed, savedSprintSpeed, savedVerticalSpeed, savedLook, savedGamepadLook;
        private bool savedLeftLaser, savedRightLaser, savedSuppress;
        private float angle;
        private float timeInMenu;

        /// <summary>Действие кнопки «Показать демо» (этап 3) — ставит хаб.</summary>
        public Action demoAction;
        /// <summary>Действие кнопки «Обучение» (этап 2) — ставит хаб.</summary>
        public Action tutorialAction;
        /// <summary>Действие «Новый проект» (дополнительная очистка у хаба).</summary>
        public Action newProjectAction;

        public bool Visible { get { return visible; } }
        public float TimeInMenu { get { return timeInMenu; } }
        /// <summary>Сколько пунктов в меню (диагностика).</summary>
        public int ButtonCount { get { return buttons.Count; } }
        /// <summary>Иконки пунктов меню по порядку (диагностика).</summary>
        public IReadOnlyList<string> IconIds { get { return buttonIcons; } }
        public string SelectedLabel
        {
            get { return selected >= 0 && selected < buttonCaptions.Count && buttonCaptions[selected] != null
                ? buttonCaptions[selected].text : ""; }
        }

        /// <summary>Показывать ли меню при следующем запуске (настройка переживает перезапуск).</summary>
        public static bool ShowOnStart
        {
            get { return PlayerPrefs.GetInt(PrefsKey, 1) != 0; }
            set { PlayerPrefs.SetInt(PrefsKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        // ================================================================== сборка

        public void Build(TrajectoryFlowController controller, FreeFlyCameraController cameraRig)
        {
            flow = controller;
            rig = cameraRig;

            canvas = KvOverlayKit.CreateCanvas(transform, "KvStartMenuCanvas", sortingOrder);
            canvasRect = (RectTransform)canvas.transform;
            tip = KvMiniTip.Create(canvasRect);

            GameObject rootGo = new GameObject("Root", typeof(RectTransform));
            rootGo.transform.SetParent(canvasRect, false);
            root = (RectTransform)rootGo.transform;
            KvTheme.Stretch(root);

            // --- затемнение по краям: сцена видна, но текст читается (кинематографическая рамка)
            Image vignette = KvTheme.CreatePanel(root, "Vignette", new Color(0f, 0f, 0f, 0.34f));
            KvTheme.Stretch(vignette.rectTransform);

            // --- левая панель меню
            GameObject panelGo = new GameObject("Panel", typeof(Image));
            panelGo.transform.SetParent(root, false);
            RectTransform panel = (RectTransform)panelGo.transform;
            panel.anchorMin = new Vector2(0f, 0f);
            panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 0.5f);
            panel.sizeDelta = new Vector2(panelWidth, 0f);
            panel.anchoredPosition = Vector2.zero;

            Image panelBg = panelGo.GetComponent<Image>();
            panelBg.sprite = KvTheme.WhiteSprite;
            panelBg.color = new Color(KvTheme.WindowBg.r, KvTheme.WindowBg.g, KvTheme.WindowBg.b, 0.90f);
            panelBg.raycastTarget = true;

            Image strip = KvTheme.CreatePanel(panel, "Accent", KvTheme.Accent);
            strip.rectTransform.anchorMin = new Vector2(1f, 0f);
            strip.rectTransform.anchorMax = new Vector2(1f, 1f);
            strip.rectTransform.pivot = new Vector2(1f, 0.5f);
            strip.rectTransform.sizeDelta = new Vector2(2f, 0f);
            strip.rectTransform.anchoredPosition = Vector2.zero;

            Text title = KvTheme.CreateText(panel, "Title", KvLocExtra.T("start.title", "KazistovVv"),
                26, TextAnchor.LowerLeft, KvTheme.TextMain);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0f, 1f);
            title.rectTransform.offsetMin = new Vector2(22f, 0f);
            title.rectTransform.offsetMax = new Vector2(-16f, 0f);
            title.rectTransform.sizeDelta = new Vector2(-38f, 32f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -28f);

            Text subtitle = KvTheme.CreateText(panel, "Subtitle",
                KvLocExtra.T("start.subtitle", "Платформа управления роботами · VR / MR / ПК"),
                KvTheme.FontSizeSmall, TextAnchor.UpperLeft, KvTheme.TextDim);
            subtitle.horizontalOverflow = HorizontalWrapMode.Wrap;
            subtitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            subtitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            subtitle.rectTransform.pivot = new Vector2(0f, 1f);
            subtitle.rectTransform.offsetMin = new Vector2(22f, 0f);
            subtitle.rectTransform.offsetMax = new Vector2(-16f, 0f);
            subtitle.rectTransform.sizeDelta = new Vector2(-38f, 32f);
            subtitle.rectTransform.anchoredPosition = new Vector2(0f, -62f);

            // --- кнопки-иконки (вертикальный столбец, как панель инструментов FreeCAD)
            float y = -112f;
            AddButton(panel, "start.new", "reset", "start.new", "start.new.desc", 0, ref y,
                delegate { NewProject(); });
            AddButton(panel, "start.open", "session", "start.open", "start.open.desc", 1, ref y,
                delegate { OpenSession(); });
            AddButton(panel, "start.demo", "presentation", "start.demo", "start.demo.desc", 2, ref y,
                delegate { PlayDemo(); });
            AddButton(panel, "start.tutorial", "help", "start.tutorial", "start.tutorial.desc", 3, ref y,
                delegate { StartTutorial(); });
            AddButton(panel, "start.settings", "settings", "start.settings", "start.settings.desc", 4, ref y,
                delegate { OpenSettings(); });
            AddButton(panel, "start.exit", "close", "start.exit", "start.exit.desc", 5, ref y,
                delegate { ExitApplication(); });

            // --- строка-подсказка внизу (как строка состояния FreeCAD)
            Image hintBg = KvTheme.CreatePanel(panel, "HintBg", new Color(0f, 0f, 0f, 0.35f));
            hintBg.rectTransform.anchorMin = new Vector2(0f, 0f);
            hintBg.rectTransform.anchorMax = new Vector2(1f, 0f);
            hintBg.rectTransform.pivot = new Vector2(0.5f, 0f);
            hintBg.rectTransform.sizeDelta = new Vector2(0f, 46f);
            hintBg.rectTransform.anchoredPosition = Vector2.zero;

            hintText = KvTheme.CreateText(hintBg.rectTransform, "Hint",
                KvLocExtra.T("start.hint", "Наведите курсор на кнопку — подсказка появится в этой строке"),
                KvTheme.FontSizeSmall, TextAnchor.MiddleLeft, KvTheme.TextDim);
            hintText.horizontalOverflow = HorizontalWrapMode.Wrap;
            KvTheme.Stretch(hintText.rectTransform, 22f, 16f, 6f, 6f);

            // --- подпись кинематографического режима (снизу справа)
            captionText = KvTheme.CreateText(root, "Caption",
                KvLocExtra.T("start.cinematic", "Кинематографический облёт стенда") + "   ·   " +
                KvLocExtra.T("start.esc", "Esc — войти в рабочую область без изменений"),
                KvTheme.FontSizeSmall, TextAnchor.LowerRight, KvTheme.TextDim);
            captionText.rectTransform.anchorMin = new Vector2(0f, 0f);
            captionText.rectTransform.anchorMax = new Vector2(1f, 0f);
            captionText.rectTransform.pivot = new Vector2(0.5f, 0f);
            captionText.rectTransform.offsetMin = new Vector2(panelWidth + 20f, 14f);
            captionText.rectTransform.offsetMax = new Vector2(-20f, 0f);
            captionText.rectTransform.sizeDelta = new Vector2(-(panelWidth + 40f), 20f);

            root.gameObject.SetActive(false);
            Select(0);
        }

        private void AddButton(RectTransform panel, string id, string iconId, string titleKey,
            string descKey, int index, ref float y, Action action)
        {
            string title = KvLocExtra.T(titleKey, id);
            string desc = KvLocExtra.T(descKey, "");

            GameObject go = new GameObject("Menu_" + id, typeof(Image), typeof(Button));
            go.transform.SetParent(panel, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(54f, 54f);
            rt.anchoredPosition = new Vector2(20f, y);
            y -= 62f;

            Image bg = go.GetComponent<Image>();
            bg.sprite = KvTheme.WhiteSprite;
            bg.color = KvTheme.ButtonBg;
            bg.raycastTarget = true;
            buttonFrames.Add(bg);

            Button button = go.GetComponent<Button>();
            button.targetGraphic = bg;
            ColorBlock cb = button.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = KvTheme.Ratio(KvTheme.ButtonHover, KvTheme.ButtonBg);
            cb.pressedColor = KvTheme.Ratio(KvTheme.ButtonPressed, KvTheme.ButtonBg);
            cb.selectedColor = Color.white;
            cb.fadeDuration = 0.05f;
            button.colors = cb;
            int captured = index;
            button.onClick.AddListener(delegate
            {
                Select(captured);
                if (action != null) action();
            });

            GameObject iconGo = new GameObject("Icon", typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            Image icon = iconGo.GetComponent<Image>();
            Sprite sprite = KvIcons.Get(iconId, 30);
            if (sprite == null) sprite = KvIcons.Get("info", 30);
            icon.sprite = sprite;
            icon.color = KvTheme.IconTint;
            icon.raycastTarget = false;
            RectTransform iconRect = (RectTransform)iconGo.transform;
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 1f);
            iconRect.pivot = new Vector2(0.5f, 1f);
            iconRect.sizeDelta = new Vector2(30f, 30f);
            iconRect.anchoredPosition = new Vector2(0f, -9f);

            Text caption = KvTheme.CreateText((RectTransform)go.transform, "Caption", title,
                KvTheme.FontSizeSmall, TextAnchor.LowerCenter, KvTheme.TextMain);
            caption.horizontalOverflow = HorizontalWrapMode.Wrap;
            caption.rectTransform.anchorMin = new Vector2(0f, 0f);
            caption.rectTransform.anchorMax = new Vector2(1f, 0f);
            caption.rectTransform.pivot = new Vector2(0.5f, 0f);
            caption.rectTransform.offsetMin = new Vector2(3f, 3f);
            caption.rectTransform.offsetMax = new Vector2(-3f, 0f);
            caption.rectTransform.sizeDelta = new Vector2(-6f, 14f);
            caption.raycastTarget = false;

            KvHoverHint hint = go.AddComponent<KvHoverHint>();
            hint.title = title;
            hint.body = desc;
            hint.tip = tip;
            int hoverIndex = index;
            hint.onHover = delegate (string t, string b)
            {
                Select(t == null ? -1 : hoverIndex);
                if (t == null) SetHint(KvLocExtra.T("start.hint",
                    "Наведите курсор на кнопку — подсказка появится в этой строке"));
            };

            buttons.Add(button);
            buttonIcons.Add(iconId);
            buttonActions.Add(action);
            buttonCaptions.Add(caption);
        }

        // ================================================================== выбор и подсказки

        private void Select(int index)
        {
            selected = index;
            for (int i = 0; i < buttonFrames.Count; i++)
            {
                bool on = i == index;
                buttonFrames[i].color = on
                    ? KvTheme.Ratio(KvTheme.ButtonHover, KvTheme.ButtonBg)
                    : KvTheme.ButtonBg;
            }
            if (index >= 0 && index < buttons.Count)
                SetHint(buttonCaptions[index] != null ? buttonCaptions[index].text : "",
                    index);
        }

        private void SetHint(string text)
        {
            SetHint(text, -1);
        }

        private void SetHint(string text, int index)
        {
            if (hintText == null) return;
            string suffix = "";
            if (index >= 0 && index < buttonCaptions.Count)
            {
                // Пояснение к выбранному пункту — во второй части строки подсказки.
                string[] keys = { "start.new.desc", "start.open.desc", "start.demo.desc",
                    "start.tutorial.desc", "start.settings.desc", "start.exit.desc" };
                if (index < keys.Length) suffix = " — " + KvLocExtra.T(keys[index], "");
            }
            hintText.text = (text ?? "") + suffix;
        }

        /// <summary>Обновить подписи после смены языка (вызывает хаб).</summary>
        public void RefreshLanguage()
        {
            if (root == null) return;
            string[] keys = { "start.new", "start.open", "start.demo", "start.tutorial",
                "start.settings", "start.exit" };
            for (int i = 0; i < buttonCaptions.Count && i < keys.Length; i++)
            {
                if (buttonCaptions[i] == null) continue;
                buttonCaptions[i].text = KvLocExtra.T(keys[i], buttonCaptions[i].text);
                KvHoverHint hint = buttons[i] != null ? buttons[i].GetComponent<KvHoverHint>() : null;
                if (hint != null)
                {
                    hint.title = buttonCaptions[i].text;
                    hint.body = KvLocExtra.T(keys[i] + ".desc", hint.body);
                }
            }
            SetHint(KvLocExtra.T("start.hint",
                "Наведите курсор на кнопку — подсказка появится в этой строке"));
            if (captionText != null)
                captionText.text = KvLocExtra.T("start.cinematic", "Кинематографический облёт стенда") +
                                   "   ·   " + KvLocExtra.T("start.esc",
                                       "Esc — войти в рабочую область без изменений");
        }

        // ================================================================== показ/скрытие

        public void Show()
        {
            if (root == null) return;
            if (visible) return;

            visible = true;
            timeInMenu = 0f;
            SaveRig();

            if (flow == null || rig == null)
            {
                KazistovVvUIManager ui = KazistovVvUIManager.Instance;
                if (ui != null)
                {
                    if (flow == null) flow = ui.Flow;
                    if (rig == null) rig = ui.CameraRig;
                }
            }
            if (camera == null) camera = Camera.main;

            SelectedRig(true);
            KazistovVvUIManager.SetUiVisible(false);
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
            if (tip != null) tip.transform.SetAsLastSibling();

            angle = StartAngle();
            Select(0);
            Report(KvLocExtra.T("start.booted", "стартовое меню показано при запуске"));
        }

        public void Hide()
        {
            if (!visible) return;
            visible = false;

            if (root != null) root.gameObject.SetActive(false);
            if (tip != null) tip.Hide();
            RestoreRig();
            KazistovVvUIManager.SetUiVisible(true);
            Report(KvLocExtra.T("start.closed", "стартовое меню закрыто — рабочая область"));
        }

        public void Toggle()
        {
            if (visible) Hide();
            else Show();
        }

        private float StartAngle()
        {
            if (camera == null) return 0.6f;
            Vector3 dir = camera.transform.position - Center();
            return Mathf.Atan2(dir.z, dir.x);
        }

        private void SaveRig()
        {
            if (camera == null) camera = Camera.main;
            if (camera != null)
            {
                savedPosition = camera.transform.position;
                savedRotation = camera.transform.rotation;
            }
            if (rig != null && !rigSaved)
            {
                savedMoveSpeed = rig.moveSpeed;
                savedSprintSpeed = rig.sprintSpeed;
                savedVerticalSpeed = rig.verticalSpeed;
                savedLook = rig.lookSensitivity;
                savedGamepadLook = rig.gamepadLookSensitivity;
                savedLeftLaser = rig.leftHandEnabled;
                savedRightLaser = rig.rightHandEnabled;
                savedSuppress = rig.suppressDirectTeleop;
                rigSaved = true;
            }
        }

        private void SelectedRig(bool locked)
        {
            if (rig == null) return;
            if (locked)
            {
                rig.moveSpeed = 0f;
                rig.sprintSpeed = 0f;
                rig.verticalSpeed = 0f;
                rig.lookSensitivity = 0f;
                rig.gamepadLookSensitivity = 0f;
                rig.leftHandEnabled = false;
                rig.rightHandEnabled = false;
                rig.suppressDirectTeleop = true;
            }
        }

        private void RestoreRig()
        {
            if (rig != null && rigSaved)
            {
                rig.moveSpeed = savedMoveSpeed;
                rig.sprintSpeed = savedSprintSpeed;
                rig.verticalSpeed = savedVerticalSpeed;
                rig.lookSensitivity = savedLook;
                rig.gamepadLookSensitivity = savedGamepadLook;
                rig.leftHandEnabled = savedLeftLaser;
                rig.rightHandEnabled = savedRightLaser;
                rig.suppressDirectTeleop = savedSuppress;
                rigSaved = false;
            }
            if (camera != null)
            {
                camera.transform.position = savedPosition;
                camera.transform.rotation = savedRotation;
            }
        }

        // ================================================================== камера (кинематографический облёт)

        /// <summary>Вести камеру — вызывать из LateUpdate хаба (после логики оператора).</summary>
        public void Tick(float deltaTime)
        {
            if (!visible) return;
            timeInMenu += deltaTime;
            HandleKeys();

            if (!cinematic) return;
            if (camera == null) camera = Camera.main;
            if (camera == null) return;

            Vector3 center = Center();
            angle += Mathf.Deg2Rad * Mathf.Max(0.5f, orbitSpeed) * deltaTime;

            // Медленный «дыхательный» подъём/спуск + лёгкое изменение радиуса: кадр живой,
            // но движение остаётся медленным и предсказуемым (кинематографично).
            float wave = Mathf.Sin(timeInMenu / Mathf.Max(4f, heightWavePeriod) * Mathf.PI * 2f);
            float radius = orbitRadius * (1f + 0.06f * wave);
            Vector3 pos = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            pos.y = center.y + orbitHeight + heightWave * wave;

            float k = 1f - Mathf.Exp(-Mathf.Max(0.5f, lookSmoothing) * deltaTime);
            camera.transform.position = Vector3.Lerp(camera.transform.position, pos, k);
            Quaternion look = Quaternion.LookRotation(
                (center + Vector3.up * 0.45f) - camera.transform.position, Vector3.up);
            camera.transform.rotation = Quaternion.Slerp(camera.transform.rotation, look, k);
        }

        private Vector3 Center()
        {
            if (flow != null && flow.Robot != null) return flow.Robot.transform.position + Vector3.up * 0.5f;
            RobotController[] robots = UnityEngine.Object.FindObjectsByType<RobotController>(
                FindObjectsInactive.Exclude);
            if (robots != null && robots.Length > 0)
            {
                Vector3 sum = Vector3.zero;
                int count = 0;
                for (int i = 0; i < robots.Length; i++)
                {
                    if (robots[i] == null) continue;
                    sum += robots[i].transform.position;
                    count++;
                }
                if (count > 0) return sum / count + Vector3.up * 0.5f;
            }
            return Vector3.up * 1.0f;
        }

        private void HandleKeys()
        {
            int move = 0;
            if (Down(KeyCode.DownArrow) || Down(KeyCode.S)) move = 1;
            if (Down(KeyCode.UpArrow) || Down(KeyCode.W)) move = -1;
            if (move != 0 && buttons.Count > 0)
                Select((selected + move + buttons.Count) % buttons.Count);

            if (Down(KeyCode.Return) || Down(KeyCode.KeypadEnter))
            {
                int index = Mathf.Clamp(selected, 0, buttonActions.Count - 1);
                if (index >= 0 && index < buttonActions.Count && buttonActions[index] != null)
                    buttonActions[index]();
            }
            if (Down(KeyCode.Escape)) Hide();
        }

        private static bool Down(KeyCode code)
        {
            try
            {
                UnityEngine.InputSystem.Keyboard k = UnityEngine.InputSystem.Keyboard.current;
                if (k != null)
                {
                    UnityEngine.InputSystem.Controls.KeyControl key = KeyOf(k, code);
                    if (key != null) return key.wasPressedThisFrame;
                }
            }
            catch (Exception) { }
            return Input.GetKeyDown(code);
        }

        private static UnityEngine.InputSystem.Controls.KeyControl KeyOf(
            UnityEngine.InputSystem.Keyboard k, KeyCode code)
        {
            switch (code)
            {
                case KeyCode.UpArrow: return k.upArrowKey;
                case KeyCode.DownArrow: return k.downArrowKey;
                case KeyCode.S: return k.sKey;
                case KeyCode.W: return k.wKey;
                case KeyCode.Return: return k.enterKey;
                case KeyCode.KeypadEnter: return k.numpadEnterKey;
                case KeyCode.Escape: return k.escapeKey;
                default: return null;
            }
        }

        // ================================================================== действия меню

        /// <summary>НОВЫЙ ПРОЕКТ: чистая рабочая область (точка, траектории, зоны, маршрут).</summary>
        public void NewProject()
        {
            try
            {
                if (newProjectAction != null) newProjectAction();
                if (flow == null) flow = KazistovVvUIManager.Instance != null
                    ? KazistovVvUIManager.Instance.Flow : null;
                if (flow != null) flow.ResetFlow("Новый проект");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[StartMenu] Новый проект: " + e.Message);
            }
            KazistovVvUIManager.SetPlanStatus(
                KvLocExtra.T("start.new", "Новый проект") + " — рабочая область очищена", KvTheme.Accent);
            KvActionLog log = KvActionLog.Instance;
            if (log != null) log.Add(KvLogKind.System, "главное меню: НОВЫЙ ПРОЕКТ — точка, траектории, " +
                                                       "зоны и маршрут очищены");
            Hide();
        }

        /// <summary>ОТКРЫТЬ СЕССИЮ: последняя сохранённая сессия (точка, зоны, позы).</summary>
        public void OpenSession()
        {
            FeatureHub hub = FeatureHub.Current;
            if (hub == null || hub.Sessions == null)
            {
                Report("сессии недоступны: модуль функций ещё не поднят");
                return;
            }
            hub.Sessions.Reload();
            KvSession newest = hub.Sessions.Newest;
            if (newest == null)
            {
                KazistovVvUIManager.SetPlanStatus("Сохранённых сессий нет — создайте новую", KvTheme.Warn);
                Report("сессий нет");
                return;
            }
            bool ok = hub.Sessions.Load(newest);
            KazistovVvUIManager.SetPlanStatus(ok
                    ? "Сессия загружена: «" + newest.name + "»"
                    : "Сессию загрузить не удалось: «" + newest.name + "»",
                ok ? KvTheme.Ok : KvTheme.Error);
            if (ok) Hide();
        }

        /// <summary>НАСТРОЙКИ: вернуть оболочку и открыть панель настроек.</summary>
        public void OpenSettings()
        {
            Hide();
            KazistovVvUIManager ui = KazistovVvUIManager.Instance;
            if (ui != null) ui.ShowSettings(0);
        }

        /// <summary>ПОКАЗАТЬ ДЕМО (этап 3).</summary>
        public void PlayDemo()
        {
            if (demoAction == null)
            {
                Report("демонстрация недоступна");
                return;
            }
            Hide();
            demoAction();
        }

        /// <summary>ОБУЧЕНИЕ (этап 2).</summary>
        public void StartTutorial()
        {
            if (tutorialAction == null)
            {
                Report("туториал недоступен");
                return;
            }
            Hide();
            tutorialAction();
        }

        /// <summary>ВЫХОД: закрыть приложение (в редакторе — остановить Play).</summary>
        public void ExitApplication()
        {
            Report("выход из приложения");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void Report(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            Debug.Log("[StartMenu] " + text);
            if (Message != null) Message(text);
        }
    }
}
