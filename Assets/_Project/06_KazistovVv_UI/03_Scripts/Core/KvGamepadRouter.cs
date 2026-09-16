using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KazistovVvUI
{
    /// <summary>
    /// УПРАВЛЕНИЕ ГЕЙМПАДОМ (ЭТАП 9). Раскладка (расставлена «с нуля», геймпад у
    /// оператора первый — поэтому подписи продублированы в виртуальном геймпаде на экране):
    ///
    ///   ЛЕВЫЙ СТИК      — движение камеры (вперёд/назад/влево/вправо, как ходьба в FPS)
    ///   ПРАВЫЙ СТИК     — поворот камеры (обзор)
    ///   D-Pad ↑ / ↓     — глубина шарика прицела (аналог колеса мыши)
    ///   D-Pad ← / →     — переключение между роботом и SCARA
    ///   A (нижняя)      — подтверждение (аналог ЛКМ): выбор точки / траектории
    ///   B (правая)      — отмена / Esc
    ///   X (левая)       — красный лазер (выбор точки)
    ///   Y (верхняя)     — зелёный лазер (выбор траектории)
    ///   LB              — аварийная остановка
    ///   RB              — домой (preset-поза)
    ///   LT              — вход/подтверждение режима перемещения точки
    ///   RT              — запуск траектории (то же, что ЛКМ зелёным лазером)
    ///   Start           — Command Palette (Ctrl+P)
    ///   Select (Back)   — список горячих клавиш (F12)
    ///   R3              — ручной режим стиков (устаревший тумблер, оставлен для совместимости)
    ///
    /// Если геймпада НЕТ — управление не активируется вовсе (<see cref="Present"/> = false),
    /// клавиатура и мышь работают как раньше. Подключение определяется автоматически
    /// (каждый кадр + событие `InputSystem.onDeviceChange`).
    ///
    /// Интеграция — по тем же принципам, что VR/клавиатура: проект уже имеет
    /// `GamepadInputProvider`/`InputManager`; роутер читает геймпад через new Input System
    /// и отдаёт действия существующим системам (поток этапов, контроллер камеры, реестр
    /// команд), не меняя сами эти системы.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class KvGamepadRouter : MonoBehaviour
    {
        /// <summary>Активный роутер (для виртуального геймпада и диагностики).</summary>
        public static KvGamepadRouter Instance { get; private set; }

        [Tooltip("Мастер-выключатель управления геймпадом (правится в инспекторе)")]
        public bool enableGamepad = true;

        [Tooltip("Мёртвая зона стиков")]
        public float stickDeadzone = 0.18f;

        [Tooltip("Писать в консоль подключение/отключение геймпада")]
        public bool logDeviceChanges = true;

        /// <summary>Геймпад подключён (управление активно).</summary>
        public bool Present { get; private set; }
        /// <summary>Имя устройства («Xbox Controller», «DualSense»…).</summary>
        public string DeviceName { get; private set; }
        /// <summary>Сколько действий геймпада выполнено за сессию (диагностика).</summary>
        public int ActionCount { get; private set; }
        /// <summary>Последнее действие (диагностика).</summary>
        public string LastAction { get; private set; }
        /// <summary>Сколько раз геймпад подключался (диагностика автоопределения).</summary>
        public int ConnectCount { get; private set; }

        /// <summary>Текущее состояние кнопок (для виртуального геймпада на экране).</summary>
        public readonly HashSet<string> Pressed = new HashSet<string>();
        /// <summary>Положение левого стика (для индикатора).</summary>
        public Vector2 LeftStick { get; private set; }
        /// <summary>Положение правого стика (для индикатора).</summary>
        public Vector2 RightStick { get; private set; }
        /// <summary>Значения триггеров 0..1 (для индикатора).</summary>
        public float LeftTrigger { get; private set; }
        /// <summary>Правый триггер 0..1.</summary>
        public float RightTrigger { get; private set; }

        private readonly HashSet<string> pressedThisFrame = new HashSet<string>();
        private const float DpadRepeatDelay = 0.22f;
        private float dpadNextTime;

        void Awake()
        {
            Instance = this;
            InputSystem.onDeviceChange += OnDeviceChange;
        }

        void OnDestroy()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
            if (Instance == this) Instance = null;
            KvGamepadBridge.Reset();
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (!(device is Gamepad)) return;
            if (change == InputDeviceChange.Added && logDeviceChanges)
                Debug.Log("[KazistovVv] Геймпад подключён: " + device.displayName);
            if ((change == InputDeviceChange.Removed || change == InputDeviceChange.Disconnected) &&
                logDeviceChanges)
            {
                Debug.Log("[KazistovVv] Геймпад отключён: " + device.displayName +
                          " — управление геймпадом выключено, работает клавиатура");
                KvGamepadBridge.Reset();
            }
            Detect();
        }

        /// <summary>Пересчитать признак «геймпад есть» (автоопределение, ТЗ ЭТАПА 9).</summary>
        public bool Detect()
        {
            bool wasPresent = Present;
            Gamepad pad = Gamepad.current;
            Present = enableGamepad && pad != null;
            DeviceName = Present ? pad.displayName : "";
            if (Present && !wasPresent)
            {
                ConnectCount++;
                if (logDeviceChanges)
                    Debug.Log("[KazistovVv] Управление геймпадом активно · устройство: " +
                              DeviceName + " · раскладка — Start: палитра команд, Select: горячие клавиши, " +
                              "LB: аварийный стоп, RB: домой, LT: режим точки, RT: запуск");
            }
            if (!Present) KvGamepadBridge.Reset();
            else KvGamepadBridge.Active = true;
            return Present;
        }

        void Update()
        {
            KvGamepadBridge.Active = enableGamepad && Gamepad.current != null;
            Present = KvGamepadBridge.Active;
            if (!Present)
            {
                if (Pressed.Count > 0) Pressed.Clear();
                LeftStick = Vector2.zero;
                RightStick = Vector2.zero;
                LeftTrigger = 0f;
                RightTrigger = 0f;
                return;
            }

            Gamepad pad = Gamepad.current;
            DeviceName = pad.displayName;

            // --- стики (движение/обзор камеры читает сам контроллер камеры — как раньше,
            //     но теперь без тумблера R3; здесь только для индикатора).
            LeftStick = Deadzone(pad.leftStick.ReadValue());
            RightStick = Deadzone(pad.rightStick.ReadValue());
            LeftTrigger = pad.leftTrigger.ReadValue();
            RightTrigger = pad.rightTrigger.ReadValue();

            Pressed.Clear();
            pressedThisFrame.Clear();
            if (LeftStick.sqrMagnitude > 0.0001f) Pressed.Add("L");
            if (RightStick.sqrMagnitude > 0.0001f) Pressed.Add("R");

            KazistovVvUIManager ui = KazistovVvUIManager.Instance;
            if (ui == null) return;

            // --- A (нижняя кнопка) и RT: подтверждение (ТЗ: «аналог ЛКМ»)
            if (Down(pad.buttonSouth, "A") || Down(pad.rightTrigger, "RT"))
                KvGamepadBridge.Confirm = true;

            // --- B (правая кнопка): отмена/Esc
            if (Down(pad.buttonEast, "B"))
                KvGamepadBridge.Cancel = true;

            // --- LT: вход/подтверждение режима перемещения точки (аналог Enter)
            if (Down(pad.leftTrigger, "LT"))
                KvGamepadBridge.Enter = true;

            // --- X: красный лазер, Y: зелёный лазер (тумблеры тулбара)
            if (Down(pad.buttonWest, "X")) Invoke("point.select", "красный лазер");
            if (Down(pad.buttonNorth, "Y")) Invoke("path.select", "зелёный лазер");

            // --- LB: аварийная остановка, RB: домой (preset-поза)
            if (Down(pad.leftShoulder, "LB")) Invoke("estop", "аварийная остановка");
            if (Down(pad.rightShoulder, "RB")) Invoke("pose.goto", "домой (preset-поза)");

            // --- Start: палитра команд, Select: горячие клавиши
            if (Down(pad.startButton, "Start"))
            {
                ui.OpenCommandPalette();
                Note("палитра команд");
            }
            if (Down(pad.selectButton, "Select"))
            {
                ui.ToggleHotkeys();
                Note("список горячих клавиш");
            }

            // --- D-Pad влево/вправо: переключение робота / SCARA
            if (Repeat(pad.dpad.left, "DLeft")) ui.SwitchRobotFromGamepad();
            if (Repeat(pad.dpad.right, "DRight")) ui.SwitchRobotFromGamepad();

            // --- D-Pad вверх/вниз: глубина шарика прицела (аналог колеса мыши)
            FreeFlyCameraController cam = ui.CameraRig;
            if (cam != null)
            {
                if (Repeat(pad.dpad.up, "DUp"))
                {
                    cam.AddScrollInput(1f);
                    Note("шарик глубже");
                }
                if (Repeat(pad.dpad.down, "DDown"))
                {
                    cam.AddScrollInput(-1f);
                    Note("шарик ближе");
                }
            }

            // --- R3: устаревший тумблер стиков (совместимость) — просто отметка в индикаторе
            if (Down(pad.rightStickButton, "R3")) Note("стики: ручной режим (R3)");
        }

        void LateUpdate()
        {
            // Флаги живут ровно один кадр: и контроллер камеры, и поток этапов
            // успевают их увидеть (оба Update идут до LateUpdate).
            KvGamepadBridge.ClearFrame();
        }

        private Vector2 Deadzone(Vector2 value)
        {
            return value.magnitude < stickDeadzone ? Vector2.zero : value;
        }

        private bool Down(UnityEngine.InputSystem.Controls.ButtonControl button, string label)
        {
            if (button == null || !button.wasPressedThisFrame) return false;
            pressedThisFrame.Add(label);
            Pressed.Add(label);
            return true;
        }

        /// <summary>D-Pad с автоповтором (иначе удержание не работало бы как колесо/переключатель).</summary>
        private bool Repeat(UnityEngine.InputSystem.Controls.ButtonControl button, string label)
        {
            if (button == null) return false;
            if (button.wasPressedThisFrame)
            {
                dpadNextTime = Time.unscaledTime + DpadRepeatDelay;
                pressedThisFrame.Add(label);
                Pressed.Add(label);
                return true;
            }
            if (button.isPressed && Time.unscaledTime >= dpadNextTime)
            {
                dpadNextTime = Time.unscaledTime + 0.09f;
                Pressed.Add(label);
                return true;
            }
            return false;
        }

        private void Invoke(string commandId, string label)
        {
            if (KvCommands.Get(commandId) == null) return;
            KvCommands.Invoke(commandId);
            Note(label);
        }

        private void Note(string action)
        {
            ActionCount++;
            LastAction = action;
        }
    }
}
