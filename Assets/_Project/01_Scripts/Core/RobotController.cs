using UnityEngine;

/// <summary>
/// Абстрактная основа всех контроллеров роботов.
/// Реализует позиционный CCD (Cyclic Coordinate Descent) для суставов,
/// разрешение ссылок на геометрию по именам и базовую телеметрию.
/// </summary>
public class RobotController : MonoBehaviour
{
    [Header("Base")]
    public string robotName = "Robot";
    public Transform tcp;
    [Range(0.1f, 5f)] public float maxSpeed = 1f;
    [HideInInspector] public bool isActive = false;

    [Header("Industry 4.0 - Telemetry")]
    public bool telemetryEnabled = true;
    public float jointTemperature = 25f;
    public float operatingHours = 0f;

    [Header("Automatic kinematics")]
    public Transform fixedBase;
    public Transform endEffector;
    public string fixedBaseName = "LS10-B702S_base_1";
    public string endEffectorName = "Axis6_2";
    public string alternativeEndEffectorName = "LS10-B702S_z_5";
    [Range(1, 32)] public int ikIterations = 12;
    [Min(0.001f)] public float ikTolerance = 0.01f;

    protected Vector3 targetPosition;
    protected Quaternion targetRotation;
    protected bool hasTarget;

    protected virtual void Awake()
    {
        targetPosition = transform.position;
        targetRotation = transform.rotation;
        ResolveKinematicReferences();
    }

    /// <summary>Задать цель только по позиции (ориентация не меняется).</summary>
    public virtual void SetTarget(Vector3 position)
    {
        targetPosition = position;
        hasTarget = true;
    }

    /// <summary>Задать цель по позиции и ориентации.</summary>
    public virtual void SetTarget(Vector3 position, Quaternion rotation)
    {
        SetTarget(position);
        targetRotation = rotation;
    }

    public virtual void MoveToTarget(float deltaTime)
    {
        if (!hasTarget)
        {
            return;
        }

        ResolveKinematicReferences();
        if (fixedBase == null || endEffector == null)
        {
            return;
        }

        Transform[] joints = BuildJointChain();
        if (joints.Length == 0)
        {
            return;
        }

        Vector3 basePosition = fixedBase.position;
        Quaternion baseRotation = fixedBase.rotation;
        float step = Mathf.Clamp(deltaTime * maxSpeed * 12f, 0.01f, 1f);

        for (int iteration = 0; iteration < ikIterations; iteration++)
        {
            if (Vector3.Distance(endEffector.position, targetPosition) <= ikTolerance)
            {
                break;
            }

            for (int i = joints.Length - 1; i >= 0; i--)
            {
                Transform joint = joints[i];
                Vector3 toEnd = endEffector.position - joint.position;
                Vector3 toTarget = targetPosition - joint.position;
                if (toEnd.sqrMagnitude < 0.000001f || toTarget.sqrMagnitude < 0.000001f)
                {
                    continue;
                }

                Quaternion correction = Quaternion.FromToRotation(toEnd, toTarget);
                joint.rotation = Quaternion.Slerp(joint.rotation, correction * joint.rotation, step);
            }
        }

        fixedBase.SetPositionAndRotation(basePosition, baseRotation);
    }

    private void ResolveKinematicReferences()
    {
        Transform root = transform;
        fixedBase = fixedBase != null ? fixedBase : FindChild(root, fixedBaseName);
        if (fixedBase == null)
        {
            fixedBase = root;
        }

        endEffector = endEffector != null
            ? endEffector
            : FindChild(root, endEffectorName);
        if (endEffector == null)
        {
            endEffector = FindChild(root, alternativeEndEffectorName);
        }
        if (endEffector == null)
        {
            endEffector = tcp;
        }
        if (endEffector == null)
        {
            endEffector = FindDeepestDescendant(root);
        }

        // Если tcp не назначен в инспекторе — берём endEffector, чтобы
        // телеметрия и телеоперация не падали с UnassignedReferenceException.
        if (tcp == null)
        {
            tcp = endEffector;
        }
    }

    /// <summary>Строит цепочку суставов от фланца вверх до фиксированной базы.</summary>
    private Transform[] BuildJointChain()
    {
        Transform node = endEffector != null ? endEffector.parent : null;

        int count = 0;
        while (node != null && node != fixedBase)
        {
            count++;
            node = node.parent;
        }

        if (node != fixedBase)
        {
            return new Transform[0];
        }

        Transform[] chain = new Transform[count];
        node = endEffector != null ? endEffector.parent : null;
        for (int i = count - 1; i >= 0 && node != null && node != fixedBase; i--)
        {
            chain[i] = node;
            node = node.parent;
        }
        return chain;
    }

    /// <summary>Максимально глубокий потомок в иерархии (запасной end effector).</summary>
    protected static Transform FindDeepestDescendant(Transform root)
    {
        Transform deepest = null;
        int deepestLevel = -1;

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child == root)
            {
                continue;
            }

            int level = 0;
            Transform parent = child.parent;
            while (parent != null && parent != root)
            {
                level++;
                parent = parent.parent;
            }

            if (parent == root && level > deepestLevel)
            {
                deepest = child;
                deepestLevel = level;
            }
        }

        return deepest;
    }

    /// <summary>Прямой поиск потомка по точному имени.</summary>
    protected static Transform FindChild(Transform root, string objectName)
    {
        if (root == null || objectName == null || objectName.Length == 0)
        {
            return null;
        }

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == objectName)
            {
                return child;
            }
        }

        return null;
    }

    public virtual float[] GetJointAngles()
    {
        return new float[0];
    }

    public virtual void SetActive(bool active)
    {
        isActive = active;
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.material.HasProperty("_EmissionColor"))
            {
                renderer.material.SetColor(
                    "_EmissionColor",
                    active ? Color.green * 0.3f : Color.black);
                renderer.material.EnableKeyword("_EMISSION");
            }
        }
    }

    public virtual void UpdateTelemetry(float deltaTime)
    {
        if (!telemetryEnabled)
        {
            return;
        }

        operatingHours += deltaTime / 3600f;
        Vector3 reference = tcp != null ? tcp.position : transform.position;
        float movement = Vector3.Distance(reference, targetPosition);
        jointTemperature = Mathf.Lerp(jointTemperature, 25f + movement * 50f, deltaTime * 0.05f);
    }
}