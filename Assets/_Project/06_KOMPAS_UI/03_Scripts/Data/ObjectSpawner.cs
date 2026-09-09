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
        [Header("Робот для копирования (если пусто — ищется в сцене)")]
        public RobotController robotTemplate;

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

        /// <summary>Спавнит копию робота (шаблон — первый найденный в сцене).</summary>
        public RegisteredObject SpawnRobot(Vector3 position)
        {
            if (robotTemplate == null)
            {
                RobotController[] robots = Object.FindObjectsByType<RobotController>(
                    FindObjectsInactive.Include);
                if (robots.Length == 0)
                {
                    Debug.LogWarning("[KompasUI] Нет робота-шаблона для копирования.");
                    return null;
                }
                robotTemplate = robots[0];
            }

            GameObject copy = Object.Instantiate(robotTemplate.gameObject);
            copy.name = robotTemplate.robotName + "_копия_" + System.DateTime.Now.ToString("HHmmss");
            copy.transform.position = position;
            copy.transform.rotation = Quaternion.identity;

            // Подчищаем лишний автоконфигуратор, чтобы он не перетирал настройки копии.
            SixAxisAutoSetup auto = copy.GetComponent<SixAxisAutoSetup>();
            if (auto != null) Object.Destroy(auto);

            RegisteredObject reg = copy.AddComponent<RegisteredObject>();
            reg.DisplayName = copy.name;
            return reg;
        }
    }
}
