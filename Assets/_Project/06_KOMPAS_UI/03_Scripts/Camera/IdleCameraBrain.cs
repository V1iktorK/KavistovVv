using UnityEngine;
using UnityEngine.InputSystem;

namespace KompasUI
{
    /// <summary>
    /// Режим простоя камеры («как в Skyrim»): если пользователь ничего не делает
    /// (нет движения мыши, клавиш, стиков) — камера медленно облетает активного
    /// робота на небольшой высоте. Любое действие мгновенно возвращает управление.
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
                }
            }
            else if (!orbiting && Time.time - lastActivityTime > idleDelay)
            {
                orbiting = true;
                userRotation = transform.rotation;
                userPosition = transform.position;
                if (OrbitTarget == null) OrbitTarget = FindActiveRobot();
            }

            if (orbiting)
            {
                DoOrbit(Time.deltaTime);
            }
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

            transform.position = Vector3.Lerp(transform.position, targetPos, blendSpeed * dt);
            Vector3 look = center + Vector3.up * 0.6f;
            Quaternion targetRot = Quaternion.LookRotation(look - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, blendSpeed * dt * 2f);
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
            orbiting = false;
        }
    }
}
