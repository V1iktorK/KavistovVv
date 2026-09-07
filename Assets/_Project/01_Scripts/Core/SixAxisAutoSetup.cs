using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Автоматически настраивает SixAxisController при запуске.
/// Находит суставы по паттерну Axis1, Axis2, ... Axis6
/// Добавляет InverseKinematics если его нет.
/// Удаляет лишние скрипты (CollisionGuard, SpatialAnchorManager).
/// </summary>
[ExecuteInEditMode]
public class SixAxisAutoSetup : MonoBehaviour
{
    [Header("Debug")]
    public bool autoSetupOnStart = true;
    public bool removeExtraComponents = true;

    void Start()
    {
        if (!autoSetupOnStart) return;
        Setup();
    }

    void Update()
    {
        // Для отладки в редакторе
        #if UNITY_EDITOR
        if (UnityEditor.EditorApplication.isPlaying) return;
        #endif
    }

    public void Setup()
    {
        Debug.Log("[SixAxisAutoSetup] Настройка робота: " + name);

        // Удаляем лишние компоненты
        if (removeExtraComponents)
        {
            RemoveExtraComponents();
        }

        // Получаем или создаём SixAxisController
        SixAxisController controller = GetComponent<SixAxisController>();
        if (controller == null)
        {
            controller = gameObject.AddComponent<SixAxisController>();
            Debug.Log("[SixAxisAutoSetup] Создан SixAxisController");
        }

        // Добавляем InverseKinematics если нет
        InverseKinematics ik = GetComponent<InverseKinematics>();
        if (ik == null)
        {
            ik = gameObject.AddComponent<InverseKinematics>();
            Debug.Log("[SixAxisAutoSetup] Добавлен InverseKinematics");
        }

        // Находим суставы
        Transform[] joints = FindJoints();
        controller.jointTransforms = joints;
        ik.joints = joints;

        if (joints.Length > 0)
        {
            Debug.Log("[SixAxisAutoSetup] ✅ Найдено " + joints.Length + " суставов:");
            for (int i = 0; i < joints.Length; i++)
            {
                Debug.Log("   [" + (i + 1) + "] " + joints[i].name + " (parent: " + (joints[i].parent != null ? joints[i].parent.name : "null") + ")");
            }
        }
        else
        {
            Debug.LogError("[SixAxisAutoSetup] ❌ Суставы НЕ найдены! Проверьте иерархию GameObject-ов под роботом.");
            Debug.LogError("[SixAxisAutoSetup] Суставы должны содержать 'Axis' в имени (Axis1, Axis2, ... Axis6)");
            return;
        }

        // Находим base (первый GameObject без 'Axis' в имени)
        Transform baseTransform = FindBase();
        if (baseTransform != null)
        {
            controller.baseTransform = baseTransform;
            controller.fixedBase = baseTransform;
            ik.baseTransform = baseTransform;
            Debug.Log("[SixAxisAutoSetup] ✅ Base: " + baseTransform.name);
        }
        else
        {
            Debug.LogWarning("[SixAxisAutoSetup] ⚠️ Base не найден, используется корень");
            controller.baseTransform = transform;
            controller.fixedBase = transform;
            ik.baseTransform = transform;
        }

        // Находим endEffector (последний сустав или TCP)
        Transform endEffector = FindEndEffector(joints);
        if (endEffector != null)
        {
            controller.endEffector = endEffector;
            ik.endEffector = endEffector;
            Debug.Log("[SixAxisAutoSetup] ✅ EndEffector: " + endEffector.name);
        }

        // Создаём IKTarget
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
            Debug.Log("[SixAxisAutoSetup] ✅ IKTarget создан");
        }

        // Устанавливаем target position для теста
        if (endEffector != null)
        {
            Vector3 testTarget = endEffector.position + Vector3.forward * 0.5f;
            controller.SetTarget(testTarget);
            Debug.Log("[SixAxisAutoSetup] Тестовая цель установлена: " + testTarget);
        }

        Debug.Log("[SixAxisAutoSetup] ✅ Настройка завершена для " + name);
    }

    Transform[] FindJoints()
    {
        Transform[] allTransforms = GetComponentsInChildren<Transform>(true);
        Transform[] joints = new Transform[6];
        int found = 0;

        // Ищем суставы по паттерну Axis1, Axis2, ... Axis6
        // Поддерживаем варианты: Axis1, Axis1_1, Axis1_2, axis1, AXIS1
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
                if (t == transform) continue; // пропускаем корень

                foreach (string pattern in patterns)
                {
                    if (t.name.IndexOf(pattern, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        // Проверяем, что это не родитель
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
        }

        // Фильтруем null
        Transform[] result = new Transform[found];
        System.Array.Copy(joints, result, found);
        return result;
    }

    Transform FindBase()
    {
        Transform[] allTransforms = GetComponentsInChildren<Transform>(true);

        // Ищем base по именам
        string[] basePatterns = { "base", "Base", "BASE", "root", "Root", "ROOT", "stand", "Stand", "STAND" };

        foreach (Transform t in allTransforms)
        {
            if (t == transform) continue;

            foreach (string pattern in basePatterns)
            {
                if (t.name.IndexOf(pattern, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // Проверяем, что это не сустав
                    bool isJoint = false;
                    for (int i = 1; i <= 6; i++)
                    {
                        if (t.name.IndexOf("Axis" + i, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            isJoint = true;
                            break;
                        }
                    }

                    if (!isJoint)
                    {
                        return t;
                    }
                }
            }
        }

        // Если не нашли, используем первый GameObject без 'Axis' в имени
        foreach (Transform t in allTransforms)
        {
            if (t == transform) continue;

            bool isJoint = false;
            for (int i = 1; i <= 6; i++)
            {
                if (t.name.IndexOf("Axis" + i, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    isJoint = true;
                    break;
                }
            }

            if (!isJoint)
            {
                return t;
            }
        }

        return null;
    }

    Transform FindEndEffector(Transform[] joints)
    {
        Transform[] allTransforms = GetComponentsInChildren<Transform>(true);

        // Ищем TCP, flange, gripper
        string[] eePatterns = { "TCP", "tcp", "Flange", "flange", "Gripper", "gripper", "EndEffector", "endEffector" };

        foreach (Transform t in allTransforms)
        {
            if (t == transform) continue;

            foreach (string pattern in eePatterns)
            {
                if (t.name.IndexOf(pattern, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return t;
                }
            }
        }

        // Если не нашли, используем последний сустав
        if (joints.Length > 0)
        {
            return joints[joints.Length - 1];
        }

        return null;
    }

    bool IsDescendantOf(Transform potentialParent, Transform root)
    {
        Transform current = potentialParent;
        while (current != null)
        {
            if (current == root) return true;
            current = current.parent;
        }
        return false;
    }

    void RemoveExtraComponents()
    {
        // Удаляем CollisionGuard
        CollisionGuard collisionGuard = GetComponent<CollisionGuard>();
        if (collisionGuard != null)
        {
            DestroyImmediate(collisionGuard);
            Debug.Log("[SixAxisAutoSetup] Удалён CollisionGuard");
        }

        // Удаляем SpatialAnchorManager
        SpatialAnchorManager spatialAnchor = GetComponent<SpatialAnchorManager>();
        if (spatialAnchor != null)
        {
            DestroyImmediate(spatialAnchor);
            Debug.Log("[SixAxisAutoSetup] Удалён SpatialAnchorManager");
        }

        // Удаляем старый RobotController
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
            UnityEditor.Selection.activeGameObject = setup.gameObject;
        }
    }
    #endif
}
