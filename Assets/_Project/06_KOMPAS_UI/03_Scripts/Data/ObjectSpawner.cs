using UnityEngine;

namespace KompasUI
{
    /// <summary>Какой объект пользователь хочет разместить в центре сцены.</summary>
    public enum SpawnKind
    {
        None,
        Table,
        Robot
    }

    /// <summary>
    /// Спавн объектов «как кубик в Unity»: выбор типа → указание места лучом → создание.
    /// Созданные объекты регистрируются в RuntimeRegistry и попадают в дерево.
    /// </summary>
    public class ObjectSpawner : MonoBehaviour
    {
        [Header("Шаблоны роботов (заполняются из сцены)")]
        public RobotController[] robotTemplates;

        /// <summary>Находит все шаблоны роботов в сцене (оригиналы, не RegisteredObject-копии).</summary>
        public RobotController[] RefreshRobotTemplates()
        {
            var all = Object.FindObjectsByType<RobotController>(FindObjectsInactive.Include);
            var list = new System.Collections.Generic.List<RobotController>();
            foreach (RobotController rc in all)
            {
                if (rc == null) continue;
                // Не берём уже размещённые через UI копии (у них есть RegisteredObject).
                if (rc.GetComponent<RegisteredObject>() != null) continue;
                list.Add(rc);
            }
            robotTemplates = list.ToArray();
            return robotTemplates;
        }

        /// <summary>
        /// Спавнит стол. point — точка ПОВЕРХНОСТИ (пол/стол): низ стола
        /// «приклеивается» к ней (без утопленного по центру размещения).
        /// </summary>
        public RegisteredObject SpawnTable(Vector3 surfacePoint, Vector3? upNormal = null)
        {
            GameObject table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "Стол_" + System.DateTime.Now.ToString("HHmmss");

            Vector3 scale = new Vector3(1.2f, 0.05f, 0.8f);
            table.transform.localScale = scale;
            Vector3 up = upNormal != null && upNormal.Value.sqrMagnitude > 0.01f
                ? upNormal.Value.normalized
                : Vector3.up;
            if (upNormal != null && upNormal.Value.sqrMagnitude > 0.01f)
            {
                table.transform.up = up;
            }
            // Центр куба поднимаем на половину высоты: низ стоит на поверхности.
            table.transform.position = surfacePoint + up * (scale.y * 0.5f + 0.002f);

            RegisteredObject reg = table.AddComponent<RegisteredObject>();
            reg.DisplayName = table.name;
            return reg;
        }

        /// <summary>Спавнит копию робота-шаблона с поворотом вокруг вертикали (yaw, градусы).</summary>
        public RegisteredObject SpawnRobot(Vector3 position, float yawDegrees)
        {
            if (robotTemplates == null || robotTemplates.Length == 0)
                RefreshRobotTemplates();
            if (robotTemplates == null || robotTemplates.Length == 0)
            {
                Debug.LogWarning("[KompasUI] Нет робота-шаблона для копирования.");
                return null;
            }
            return SpawnRobot(position, yawDegrees, robotTemplates[0]);
        }

        /// <summary>Спавнит копию конкретного шаблона (SCARA/6-осевой).</summary>
        public RegisteredObject SpawnRobot(Vector3 position, float yawDegrees,
            RobotController template)
        {
            if (template == null) return null;

            GameObject copy = Object.Instantiate(template.gameObject);
            copy.name = template.robotName + "_" + System.DateTime.Now.ToString("HHmmss");
            copy.transform.position = position;
            copy.transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);

            // Подчищаем автоконфигуратор, чтобы он не перетирал настройки копии.
            SixAxisAutoSetup auto = copy.GetComponent<SixAxisAutoSetup>();
            if (auto != null) Object.Destroy(auto);

            RegisteredObject reg = copy.AddComponent<RegisteredObject>();
            reg.DisplayName = copy.name;
            return reg;
        }
    }
}
