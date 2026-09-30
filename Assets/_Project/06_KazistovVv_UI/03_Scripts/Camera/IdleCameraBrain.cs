using UnityEngine;
using UnityEngine.InputSystem;

namespace KazistovVvUI
{
    /// <summary>
    /// Режим простоя камеры («как в Skyrim»): если пользователь ничего не делает
    /// (нет движения мыши, клавиш, стиков) — камера медленно облетает активного
    /// робота на небольшой высоте. Любое действие мгновенно возвращает управление.
    ///
    /// ФИКС 5 (§23) — ЧТО ЗДЕСЬ БЫЛО НЕ ТАК И ЧТО ИСПРАВЛЕНО:
    ///   • облёт и <see cref="KazistovVvFeatures.FreeFlyCameraController"/> писали в один и
    ///     тот же `transform` в одном кадре: облёт ставил поворот, а контроллер камеры
    ///     в следующем кадре возвращал свой устаревший `yaw/pitch` — камера «колбасила».
    ///     Теперь контроллер камеры на время облёта получает `ExternalCameraControl = true`
    ///     и в камеру не лезет;
    ///   • Lerp/Slerp считались с «сырым» `blendSpeed · dt`: при просадке кадра (а в проекте
    ///     бывают тяжёлые кадры планирования) коэффициент прыгал, и камера дёргалась.
    ///     Теперь сглаживание через `1 − exp(−k·dt)` и <see cref="Mathf.SmoothDamp"/> —
    ///     одинаковое поведение при любом FPS и при любом `Application.targetFrameRate`.
    /// </summary>
    public class IdleCameraBrain : MonoBehaviour
    {
        [Header("Настройки простоя")]
        [Tooltip("Через сколько секунд бездействия включится облёт (600 = 10 минут)")]
        public float idleDelay = 600f;
        [Tooltip("Радиус облёта вокруг робота")]
        public float orbitRadius = 6f;
        [Tooltip("Высота камеры над базой робота")]
        public float orbitHeight = 2.2f;
        [Tooltip("Скорость облёта, град/с")]
        public float orbitSpeed = 12f;
        [Tooltip("Плавность входа/выхода")]
        public float blendSpeed = 1.2f;

        private float lastActivityTime;
        private bool orbiting;
        private float orbitAngle;
        private Quaternion userRotation;
        private Vector3 userPosition;
        private Vector3 positionVelocity;      // ФИКС 5: состояние SmoothDamp

        /// <summary>Облёт идёт прямо сейчас (диагностика).</summary>
        public bool Orbiting { get { return orbiting; } }

        /// <summary>Целевой робот для облёта (ставится UIManager'ом).</summary>
        public Transform OrbitTarget { get; set; }

        void Start()
        {
            lastActivityTime = Time.time;
            userRotation = transform.rotation;
            userPosition = transform.position;
        }

        void Update()
        {
            bool active = HasUserActivity();
            if (active)
            {
                lastActivityTime = Time.time;
                if (orbiting)
                {
                    // Возврат к позиции/повороту пользователя — плавно.
                    orbiting = false;
                    SetExternalControl(false);
                }
            }
            else if (!orbiting && Time.time - lastActivityTime > idleDelay)
            {
                orbiting = true;
                userRotation = transform.rotation;
                userPosition = transform.position;
                orbitAngle = Mathf.Atan2(transform.position.z - Center().z,
                    transform.position.x - Center().x);
                positionVelocity = Vector3.zero;
                if (OrbitTarget == null) OrbitTarget = FindActiveRobot();
                SetExternalControl(true);
            }

            if (orbiting)
            {
                DoOrbit(Time.deltaTime);
            }
        }

        /// <summary>Отдать/вернуть управление камерой контроллеру оператора (ФИКС 5).</summary>
        private void SetExternalControl(bool value)
        {
            if (cameraRig == null) cameraRig = GetComponent<FreeFlyCameraController>();
            if (cameraRig == null)
            {
                KazistovVvUIManager ui = KazistovVvUIManager.Instance;
                if (ui != null) cameraRig = ui.CameraRig;
            }
            if (cameraRig != null) cameraRig.ExternalCameraControl = value;
            if (!value && cameraRig != null) cameraRig.SyncAnglesFromTransform();
        }

        private FreeFlyCameraController cameraRig;

        private Vector3 Center()
        {
            if (OrbitTarget != null) return OrbitTarget.position;
            Transform robot = FindActiveRobot();
            return robot != null ? robot.position : Vector3.zero;
        }

        private void DoOrbit(float dt)
        {
            if (OrbitTarget == null)
            {
                OrbitTarget = FindActiveRobot();
                if (OrbitTarget == null) return;
            }

            orbitAngle += orbitSpeed * dt;

            Vector3 center = OrbitTarget.position;
            float rad = orbitAngle * Mathf.Deg2Rad;
            Vector3 targetPos = center + new Vector3(Mathf.Cos(rad) * orbitRadius, orbitHeight, Mathf.Sin(rad) * orbitRadius);

            // ФИКС 5: SmoothDamp вместо Lerp с «сырым» blendSpeed·dt — движение не зависит
            // от FPS и не дёргается на тяжёлых кадрах.
            transform.position = Vector3.SmoothDamp(transform.position, targetPos,
                ref positionVelocity, Mathf.Max(0.05f, 1f / Mathf.Max(0.2f, blendSpeed)), Mathf.Infinity, dt);

            Vector3 look = center + Vector3.up * 0.6f;
            Quaternion targetRot = Quaternion.LookRotation(look - transform.position, Vector3.up);
            // Кадронезависимое сглаживание поворота: 1 − exp(−k·dt).
            float k = 1f - Mathf.Exp(-Mathf.Max(0.2f, blendSpeed * 2f) * dt);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, k);
        }

        private static Transform FindActiveRobot()
        {
            RobotController[] robots = Object.FindObjectsByType<RobotController>(FindObjectsInactive.Include);
            foreach (RobotController rc in robots)
            {
                if (rc != null && rc.isActive && rc.tcp != null) return rc.tcp;
            }
            foreach (RobotController rc in robots)
            {
                if (rc != null) return rc.transform;
            }
            return null;
        }

        private bool HasUserActivity()
        {
            // Мышь
            if (Mouse.current != null)
            {
                Vector2 delta = Mouse.current.delta.ReadValue();
                if (delta.sqrMagnitude > 0.001f) return true;
                if (Mouse.current.leftButton.isPressed ||
                    Mouse.current.rightButton.isPressed ||
                    Mouse.current.middleButton.isPressed) return true;
            }

            // Клавиатура
            if (Keyboard.current != null)
            {
                if (Keyboard.current.anyKey.isPressed) return true;
            }

            // Геймпад
            if (Gamepad.current != null)
            {
                if (Gamepad.current.leftStick.ReadValue().sqrMagnitude > 0.001f) return true;
                if (Gamepad.current.rightStick.ReadValue().sqrMagnitude > 0.001f) return true;
            }

            return false;
        }

        public void PingActivity()
        {
            lastActivityTime = Time.time;
            if (orbiting)
            {
                orbiting = false;
                SetExternalControl(false);
            }
        }

        private void OnDisable()
        {
            if (orbiting) SetExternalControl(false);
            orbiting = false;
        }
    }
}
