// RobotController.cs — абстрактный базовый класс
using UnityEngine;

public class RobotController : MonoBehaviour // Убрали abstract
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
    
    protected Vector3 targetPosition;
    protected Quaternion targetRotation;
    protected bool hasTarget;
    [Header("Automatic kinematics")]
    public Transform fixedBase;
    public Transform endEffector;
    public string fixedBaseName = "LS10-B702S_base_1";
    public string endEffectorName = "Axis6_2";
    public string alternativeEndEffectorName = "LS10-B702S_z_5";
    [Range(1, 32)] public int ikIterations = 12;
    [Min(0.001f)] public float ikTolerance = 0.01f;

    protected virtual void Awake()
    {
        targetPosition = transform.position;
        targetRotation = transform.rotation;
        ResolveKinematicReferences();
    }
    
    // Заменили abstract на virtual и добавили пустые реализации
    public virtual void SetTarget(Vector3 position, Quaternion? rotation = null)
    {
        targetPosition = position;
        if (rotation.HasValue) targetRotation = rotation.Value;
        hasTarget = true;
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
        fixedBase = fixedBase != null ? fixedBase : FindChild(root, fixedBaseName) ?? root;
        endEffector = endEffector != null
            ? endEffector
            : FindChild(root, endEffectorName) ??
              FindChild(root, alternativeEndEffectorName) ??
              tcp ??
              FindDeepestDescendant(root);
    }

    private Transform[] BuildJointChain()
    {
        var joints = new System.Collections.Generic.List<Transform>();
        Transform current = endEffector != null ? endEffector.parent : null;
        while (current != null && current != fixedBase)
        {
            joints.Add(current);
            current = current.parent;
        }

        if (current != fixedBase)
        {
            return System.Array.Empty<Transform>();
        }

        joints.Reverse();
        return joints.ToArray();
    }

    private static Transform FindDeepestDescendant(Transform root)
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

    private static Transform FindChild(Transform root, string objectName)
    {
        if (root == null || string.IsNullOrEmpty(objectName))
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
        return new float[0]; // Возвращаем пустой массив по умолчанию
    }
    
    public virtual void SetActive(bool active)
    {
        isActive = active;
        foreach (var rend in GetComponentsInChildren<Renderer>(true))
        {
            if (rend.material.HasProperty("_EmissionColor"))
            {
                rend.material.SetColor("_EmissionColor", active ? Color.green * 0.3f : Color.black);
                rend.material.EnableKeyword("_EMISSION");
            }
        }
    }
    
    public virtual void UpdateTelemetry(float deltaTime)
    {
        if (!telemetryEnabled) return;
        operatingHours += deltaTime / 3600f;
        float movement = Vector3.Distance(tcp != null ? tcp.position : transform.position, targetPosition);
        jointTemperature = Mathf.Lerp(jointTemperature, 25f + movement * 50f, deltaTime * 0.05f);
    }
}