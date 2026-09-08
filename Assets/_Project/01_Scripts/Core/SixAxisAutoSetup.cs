using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Автоматически настраивает SixAxisController при запуске:
/// находит суставы по паттерну Axis1..Axis6, добавляет InverseKinematics,
/// удаляет лишние скрипты (CollisionGuard, SpatialAnchorManager).
/// </summary>
[ExecuteInEditMode]
public class SixAxisAutoSetup : MonoBehaviour
{
    [Header("Debug")]
    public bool autoSetupOnStart = true;
    public bool removeExtraComponents = true;

    void Start()
    {
        if (!autoSetupOnStart)
        {
            return;
        }

        Setup();
    }

    public void Setup()
    {
        Debug.Log("[SixAxisAutoSetup] Настройка робота: " + name);

        if (removeExtraComponents)
        {
            RemoveExtraComponents();
        }

        // Получаем или создаём SixAxisController.
        SixAxisController controller = GetComponent<SixAxisController>();
        if (controller == null)
        {
            controller = gameObject.AddComponent<SixAxisController>();
            Debug.Log("[SixAxisAutoSetup] Создан SixAxisController");
        }

        // Добавляем InverseKinematics, если его нет.
        InverseKinematics ik = GetComponent<InverseKinematics>();
        if (ik == null)
        {
            ik = gameObject.AddComponent<InverseKinematics>();
            Debug.Log("[SixAxisAutoSetup] Добавлен InverseKinematics");
        }

        // Находим суставы.
        Transform[] joints = FindJoints();
        controller.jointTransforms = joints;
        ik.joints = joints;

        if (joints.Length > 0)
        {
            Debug.Log("[SixAxisAutoSetup] Найдено " + joints.Length + " суставов:");
            for (int i = 0; i < joints.Length; i++)
            {
                string parentName = joints[i].parent != null ? joints[i].parent.name : "null";
                Debug.Log("   [" + (i + 1) + "] " + joints[i].name + " (parent: " + parentName + ")");
            }
        }
        else
        {
            Debug.LogError("[SixAxisAutoSetup] Суставы НЕ найдены! Проверьте иерархию GameObject-ов под роботом.");
            Debug.LogError("[SixAxisAutoSetup] Суставы должны содержать 'Axis' в имени (Axis1, Axis2, ... Axis6)");
            return;
        }

        // Находим base (первый GameObject без 'Axis' в имени).
        Transform baseTransform = FindBase();
        if (baseTransform != null)
        {
            controller.baseTransform = baseTransform;
            controller.fixedBase = baseTransform;
            ik.baseTransform = baseTransform;
            Debug.Log("[SixAxisAutoSetup] Base: " + baseTransform.name);
        }
        else
        {
            Debug.LogWarning("[SixAxisAutoSetup] Base не найден, используется корень");
            controller.baseTransform = transform;
            controller.fixedBase = transform;
            ik.baseTransform = transform;
        }

        // Находим endEffector (последний сустав или TCP).
        Transform endEffector = FindEndEffector(joints);
        if (endEffector != null)
        {
            controller.endEffector = endEffector;
            ik.endEffector = endEffector;
            Debug.Log("[SixAxisAutoSetup] EndEffector: " + endEffector.name);
        }

        // Создаём IKTarget.
        if (ik.target == null)
        {
            GameObject targetGO = GameObject.Find(name + "_IKTarget");
            if (targetGO == null)
            {
                targetGO = new GameObject(name + "_IKTarget");
                targetGO.transform.SetParent(transform);
            }
            targetGO.transform.position = endEffector != null ? endEffector.position : Vector3.zero;
            ik.target = targetGO.transform;
            Debug.Log("[SixAxisAutoSetup] IKTarget создан");
        }

        // Устанавливаем тестовую цель.
        if (endEffector != null)
        {
            Vector3 testTarget = endEffector.position + Vector3.forward * 0.5f;
            controller.SetTarget(testTarget);
            Debug.Log("[SixAxisAutoSetup] Тестовая цель установлена: " + testTarget);
        }

        Debug.Log("[SixAxisAutoSetup] Настройка завершена для " + name);
    }

    Transform[] FindJoints()
    {
        Transform[] allTransforms = GetComponentsInChildren<Transform>(true);
        Transform[] joints = new Transform[6];
        int found = 0;

        // Ищем суставы по паттерну Axis1..Axis6 (поддерживаем Axis1_1/Axis1_2/axis1 и т.д.).
        for (int axis = 1; axis <= 6 && found < 6; axis++)
        {
            string[] patterns =
            {
                "Axis" + axis + "_2",
                "Axis" + axis + "_1",
                "Axis" + axis,
                "axis" + axis + "_2",
                "axis" + axis + "_1",
                "axis" + axis,
                "AXIS" + axis + "_2",
                "AXIS" + axis + "_1",
                "AXIS" + axis
            };

            foreach (Transform t in allTransforms)
            {
                if (t == transform)
                {
                    continue;
                }

                if (MatchesAny(t.name, patterns))
                {
                    // Проверяем, что это не родитель.
                    bool isChild = t.parent != null && IsDescendantOf(t.parent, transform);
                    if (isChild || t.parent == transform)
                    {
                        joints[found++] = t;
                        Debug.Log("[SixAxisAutoSetup]    Найден сустав " + axis + ": " + t.name);
                        break;
                    }
                }
            }
        }

        // Обрезаем пустые слоты.
        Transform[] result = new Transform[found];
        for (int i = 0; i < found; i++)
        {
            result[i] = joints[i];
        }
        return result;
    }

    Transform FindBase()
    {
        Transform[] allTransforms = GetComponentsInChildren<Transform>(true);
        string[] basePatterns = { "base", "Base", "BASE", "root", "Root", "ROOT", "stand", "Stand", "STAND" };

        foreach (Transform t in allTransforms)
        {
            if (t == transform)
            {
                continue;
            }

            if (!MatchesAny(t.name, basePatterns))
            {
                continue;
            }

            // Проверяем, что это не сустав.
            if (IsJointLike(t.name))
            {
                continue;
            }

            return t;
        }

        // Если не нашли, используем первый GameObject без 'Axis' в имени.
        foreach (Transform t in allTransforms)
        {
            if (t == transform)
            {
                continue;
            }

            if (!IsJointLike(t.name))
            {
                return t;
            }
        }

        return null;
    }

    Transform FindEndEffector(Transform[] joints)
    {
        Transform[] allTransforms = GetComponentsInChildren<Transform>(true);
        string[] eePatterns = { "TCP", "Flange", "Gripper", "EndEffector" };

        foreach (Transform t in allTransforms)
        {
            if (t == transform)
            {
                continue;
            }

            if (MatchesAny(t.name, eePatterns))
            {
                return t;
            }
        }

        // Если не нашли, используем последний сустав.
        if (joints.Length > 0)
        {
            return joints[joints.Length - 1];
        }

        return null;
    }

    private static bool MatchesAny(string name, string[] patterns)
    {
        string lowerName = name.ToLower();
        for (int i = 0; i < patterns.Length; i++)
        {
            if (name == patterns[i] || lowerName.IndexOf(patterns[i].ToLower()) >= 0)
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsJointLike(string name)
    {
        string lowerName = name.ToLower();
        for (int i = 1; i <= 6; i++)
        {
            if (lowerName.IndexOf("axis" + i) >= 0)
            {
                return true;
            }
        }
        return false;
    }

    bool IsDescendantOf(Transform potentialParent, Transform root)
    {
        Transform current = potentialParent;
        while (current != null)
        {
            if (current == root)
            {
                return true;
            }
            current = current.parent;
        }
        return false;
    }

    void RemoveExtraComponents()
    {
        CollisionGuard collisionGuard = GetComponent<CollisionGuard>();
        if (collisionGuard != null)
        {
            DestroyImmediate(collisionGuard);
            Debug.Log("[SixAxisAutoSetup] Удалён CollisionGuard");
        }

        SpatialAnchorManager spatialAnchor = GetComponent<SpatialAnchorManager>();
        if (spatialAnchor != null)
        {
            DestroyImmediate(spatialAnchor);
            Debug.Log("[SixAxisAutoSetup] Удалён SpatialAnchorManager");
        }

        RobotController oldController = GetComponent<RobotController>();
        if (oldController != null && !oldController.GetType().Name.Contains("SixAxis"))
        {
            DestroyImmediate(oldController);
            Debug.Log("[SixAxisAutoSetup] Удалён старый RobotController");
        }
    }

    #if UNITY_EDITOR
    [UnityEditor.MenuItem("GameObject/Robot/Setup SixAxis Auto")]
    static void MenuSetupSixAxis()
    {
        if (Selection.activeGameObject != null)
        {
            SixAxisAutoSetup setup = Selection.activeGameObject.GetComponent<SixAxisAutoSetup>();
            if (setup == null)
            {
                setup = Selection.activeGameObject.AddComponent<SixAxisAutoSetup>();
            }
            setup.Setup();
        }
    }
    #endif
}
