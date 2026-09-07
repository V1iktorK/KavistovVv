using UnityEngine;

/// <summary>
/// Автоматически настраивает SixAxisController при запуске.
/// Находит суставы по паттерну Axis1, Axis2, ... Axis6
/// Добавляет InverseKinematics если его нет.
/// </summary>
public class SixAxisAutoSetup : MonoBehaviour
{
    void Start()
    {
        SixAxisController controller = GetComponent<SixAxisController>();
        if (controller == null)
        {
            Debug.LogWarning("[SixAxisAutoSetup] SixAxisController не найден на " + name);
            return;
        }

        // Добавляем InverseKinematics если нет
        InverseKinematics ik = GetComponent<InverseKinematics>();
        if (ik == null)
        {
            ik = gameObject.AddComponent<InverseKinematics>();
            Debug.Log("[SixAxisAutoSetup] Добавлен InverseKinematics на " + name);
        }

        // Находим суставы по паттерну Axis1, Axis2, ...
        Transform[] joints = FindJoints();
        if (joints.Length > 0)
        {
            controller.jointTransforms = joints;
            ik.joints = joints;
            Debug.Log("[SixAxisAutoSetup] Найдено " + joints.Length + " суставов: " + GetJointNames(joints));
        }
        else
        {
            Debug.LogError("[SixAxisAutoSetup] Суставы не найдены! Проверьте иерархию " + name);
        }

        // Находим base и endEffector
        Transform root = transform;
        Transform baseTransform = FindChild(root, "base", "Base", "BASE", "root", "Root", "ROOT");
        if (baseTransform != null)
        {
            controller.baseTransform = baseTransform;
            controller.fixedBase = baseTransform;
            ik.baseTransform = baseTransform;
            Debug.Log("[SixAxisAutoSetup] Base найден: " + baseTransform.name);
        }

        Transform endEffector = FindChild(root, "flange", "Flange", "TCP", "tcp", "gripper", "Gripper");
        if (endEffector != null)
        {
            controller.endEffector = endEffector;
            ik.endEffector = endEffector;
            Debug.Log("[SixAxisAutoSetup] EndEffector найден: " + endEffector.name);
        }
        else if (joints.Length > 0)
        {
            // Если не нашли, используем последний сустав как endEffector
            controller.endEffector = joints[joints.Length - 1];
            ik.endEffector = joints[joints.Length - 1];
            Debug.Log("[SixAxisAutoSetup] EndEffector = последний сустав: " + joints[joints.Length - 1].name);
        }

        // Устанавливаем target для IK
        if (ik.target == null)
        {
            GameObject targetGO = new GameObject("IKTarget");
            targetGO.transform.SetParent(root);
            targetGO.transform.position = controller.endEffector != null ? controller.endEffector.position : Vector3.zero;
            ik.target = targetGO.transform;
            Debug.Log("[SixAxisAutoSetup] Создан IKTarget");
        }

        Debug.Log("[SixAxisAutoSetup] Настройка SixAxisController завершена для " + name);
    }

    Transform[] FindJoints()
    {
        Transform[] allChildren = GetComponentsInChildren<Transform>(true);
        Transform[] joints = new Transform[6];
        int found = 0;

        for (int axis = 1; axis <= 6 && found < 6; axis++)
        {
            string[] patterns = { "Axis" + axis + "_2", "Axis" + axis + "_1", "Axis" + axis };
            foreach (Transform t in allChildren)
            {
                foreach (string pattern in patterns)
                {
                    if (t.name.IndexOf(pattern, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        joints[found++] = t;
                        break;
                    }
                }
                if (found > 0 && joints[found - 1] != null) break;
            }
        }

        // Фильтруем null
        Transform[] result = new Transform[found];
        System.Array.Copy(joints, result, found);
        return result;
    }

    static Transform FindChild(Transform root, params string[] names)
    {
        if (root == null) return null;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            foreach (string name in names)
            {
                if (child.name.IndexOf(name, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return child;
                }
            }
        }
        return null;
    }

    string GetJointNames(Transform[] joints)
    {
        string names = "";
        foreach (Transform t in joints)
        {
            if (t != null) names += t.name + ", ";
        }
        return names.TrimEnd(',', ' ');
    }
}
