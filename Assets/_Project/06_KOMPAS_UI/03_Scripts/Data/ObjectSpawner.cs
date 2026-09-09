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

        /// <summary>Спавнит стол (примитив-куб) в точке.</summary>
        public RegisteredObject SpawnTable(Vector3 position, Vector3? upNormal = null)
        {
            GameObject table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "Стол_" + System.DateTime.Now.ToString("HHmmss");
            table.transform.position = position + Vector3.up * 0.01f;
            table.transform.localScale = new Vector3(1.2f, 0.05f, 0.8f);

            if (upNormal != null && upNormal.Value.sqrMagnitude > 0.01f)
            {
                table.transform.up = upNormal.Value;
            }

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

            GameObject copy = Object.Instantiate(robotTemplates[0].gameObject);
            copy.name = robotTemplates[0].robotName + "_" + System.DateTime.Now.ToString("HHmmss");
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
