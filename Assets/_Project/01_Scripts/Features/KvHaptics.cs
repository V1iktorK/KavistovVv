using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace KazistovVvFeatures
{
    /// <summary>Сила вибрации (ЭТАП 17 ТЗ).</summary>
    public enum KvHapticStrength { Light = 0, Medium = 1, Hard = 2 }

    /// <summary>
    /// ВИБРАЦИЯ КОНТРОЛЛЕРОВ (ЭТАП 17 ТЗ) — ЗАГОТОВКА.
    ///
    /// По ТЗ: «пока что — заготовки, активация при VR/MR». В проекте нет пакета `com.meta.*`
    /// (Meta-контур подключён через OpenXR-слой), а PICO SDK не установлен, поэтому прямой
    /// зависимости от их API здесь нет — вместо неё РЕФЛЕКСИЯ:
    ///   • ищутся компоненты контроллеров (XR Interaction Toolkit: `XRBaseController`,
    ///     `ActionBasedController`, `XRController`, Meta/PICO-контроллеры) по имени типа;
    ///   • у найденного компонента вызывается первый подходящий метод
    ///     `SendHapticImpulse(float amplitude, float duration)`
    ///     (XRIT v3: `SendHapticImpulse(float, float)`; Meta: `SendHapticImpulse(float, float, float)`);
    ///   • если ничего не найдено — вибрация НЕ отправляется, в консоль пишется одна строка
    ///     за сессию (чтобы не спамить), и работа продолжается как обычно.
    ///
    /// Такой «мягкий» мост не ломает десктопный запуск и автоматически заработает, когда
    /// в сцену добавят VR/MR-контроллеры, — правки кода не потребуются.
    /// </summary>
    public class KvHaptics
    {
        /// <summary>Включена ли вибрация (по ТЗ — выключатель в настройках).</summary>
        public bool Enabled = true;
        /// <summary>Писать в консоль, когда вибрация недоступна.</summary>
        public bool logUnavailable = true;

        /// <summary>Длительности импульсов, с.</summary>
        public float lightDuration = 0.04f;
        public float mediumDuration = 0.09f;
        public float hardDuration = 0.16f;

        /// <summary>Амплитуды импульсов 0…1.</summary>
        public float lightAmplitude = 0.20f;
        public float mediumAmplitude = 0.45f;
        public float hardAmplitude = 0.85f;

        private readonly List<MonoBehaviour> controllers = new List<MonoBehaviour>();
        private float controllerScanTimer;
        private bool warned;

        /// <summary>Есть ли найденный контроллер (для диагностики/панели функций).</summary>
        public bool Available { get { return controllers.Count > 0; } }
        public int ControllerCount { get { return controllers.Count; } }

        /// <summary>Есть ли контроллеры XR в сцене (пересканируется раз в 2 с).</summary>
        public bool IsActive
        {
            get
            {
                if (Time.realtimeSinceStartup - controllerScanTimer > 2f)
                {
                    controllerScanTimer = Time.realtimeSinceStartup;
                    Scan();
                }
                return controllers.Count > 0;
            }
        }

        /// <summary>Отправить импульс (light/medium/hard).</summary>
        public void Pulse(KvHapticStrength strength, string source = null)
        {
            if (!Enabled) return;

            float amplitude;
            float duration;
            switch (strength)
            {
                case KvHapticStrength.Hard: amplitude = hardAmplitude; duration = hardDuration; break;
                case KvHapticStrength.Medium: amplitude = mediumAmplitude; duration = mediumDuration; break;
                default: amplitude = lightAmplitude; duration = lightDuration; break;
            }
            Pulse(amplitude, duration, source);
        }

        public void Pulse(float amplitude, float duration, string source = null)
        {
            if (!Enabled) return;

            if (Time.realtimeSinceStartup - controllerScanTimer > 2f)
            {
                controllerScanTimer = Time.realtimeSinceStartup;
                Scan();
            }

            if (controllers.Count == 0)
            {
                if (logUnavailable && !warned)
                {
                    warned = true;
                    Debug.Log("[Haptics] контроллеры VR/MR не найдены — вибрация не отправляется" +
                              (string.IsNullOrEmpty(source) ? "" : " (событие: " + source + ")") +
                              ". Заготовка активна: при добавлении контроллеров XR импульсы уйдут автоматически.");
                }
                return;
            }

            bool sent = false;
            for (int i = 0; i < controllers.Count; i++)
            {
                MonoBehaviour controller = controllers[i];
                if (controller == null) continue;
                if (SendTo(controller, amplitude, duration)) sent = true;
            }

            if (sent && !string.IsNullOrEmpty(source))
                Debug.Log("[Haptics] импульс " + amplitude.ToString("0.00") + " · " +
                          (duration * 1000f).ToString("0") + " мс · " + source);
        }

        // ---- события этапов (ТЗ): выбор точки — слабая, подтверждение — сильнее,
        //      столкновение/ошибка — резкая короткая, аварийный стоп — самая сильная.
        public void OnPointSelected() { Pulse(KvHapticStrength.Light, "выбор точки"); }
        public void OnTrajectoryConfirmed() { Pulse(KvHapticStrength.Medium, "подтверждение траектории"); }
        public void OnMotionStarted() { Pulse(0.35f, 0.12f, "старт движения"); }
        public void OnError() { Pulse(KvHapticStrength.Hard, "ошибка"); }
        public void OnEmergencyStop() { Pulse(1f, 0.30f, "аварийная остановка"); }

        private void Scan()
        {
            controllers.Clear();
            MonoBehaviour[] all;
            try
            {
                all = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
            }
            catch (Exception)
            {
                return;
            }
            for (int i = 0; i < all.Length; i++)
            {
                MonoBehaviour mb = all[i];
                if (mb == null) continue;
                string typeName = mb.GetType().Name;
                if (typeName.IndexOf("Controller", StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (typeName.IndexOf("XR", StringComparison.OrdinalIgnoreCase) < 0 &&
                    typeName.IndexOf("Haptic", StringComparison.OrdinalIgnoreCase) < 0 &&
                    typeName.IndexOf("Pico", StringComparison.OrdinalIgnoreCase) < 0 &&
                    typeName.IndexOf("Meta", StringComparison.OrdinalIgnoreCase) < 0 &&
                    typeName.IndexOf("OVR", StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (FindHapticMethod(mb.GetType()) == null) continue;
                controllers.Add(mb);
            }
        }

        private static MethodInfo FindHapticMethod(Type type)
        {
            MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo m = methods[i];
                if (m.Name != "SendHapticImpulse") continue;
                ParameterInfo[] ps = m.GetParameters();
                if (ps.Length >= 2 &&
                    ps[0].ParameterType == typeof(float) && ps[1].ParameterType == typeof(float))
                    return m;
            }
            return null;
        }

        private static bool SendTo(MonoBehaviour controller, float amplitude, float duration)
        {
            try
            {
                MethodInfo method = FindHapticMethod(controller.GetType());
                if (method == null) return false;
                ParameterInfo[] ps = method.GetParameters();
                object[] args = new object[ps.Length];
                args[0] = Mathf.Clamp01(amplitude);
                args[1] = Mathf.Max(0.001f, duration);
                for (int i = 2; i < ps.Length; i++)
                    args[i] = ps[i].ParameterType == typeof(float) ? (object)0f
                        : ps[i].ParameterType == typeof(uint) ? (object)0u
                        : null;
                method.Invoke(controller, args);
                return true;
            }
            catch (Exception)
            {
                return false;   // несовместимая подпись — молча пропускаем этот контроллер
            }
        }
    }
}
