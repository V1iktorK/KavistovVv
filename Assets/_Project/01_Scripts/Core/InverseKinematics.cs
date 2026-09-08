using UnityEngine;

/// <summary>
/// Упрощённый CCD-солвер инверсной кинематики для 6-осевого манипулятора.
/// Сначала приводит TCP к целевой позиции, затем доворачивает
/// конечные суставы до целевой ориентации фланца.
/// </summary>
public class InverseKinematics : MonoBehaviour
{
    [Header("Kinematic chain")]
    public Transform[] joints;      // 6 суставов от основания к фланцу
    public Transform target;        // Куда должна прийти TCP (Tool Center Point)
    public Transform baseTransform;
    public Transform endEffector;

    [Header("Tuning")]
    public float positionThreshold = 0.01f;
    public float orientationThreshold = 2f; // градусы
    public int maxIterations = 10;

    private Vector3 desiredPosition;
    private Quaternion desiredRotation;
    private Vector3 basePosition;
    private Quaternion baseRotation;
    private bool initialized;
    private bool hasOrientationTarget;

    private void Awake()
    {
        CacheBase();
    }

    public void SetTarget(Vector3 position)
    {
        desiredPosition = position;
        hasOrientationTarget = false;
    }

    public void SetTarget(Vector3 position, Quaternion rotation)
    {
        desiredPosition = position;
        desiredRotation = rotation;
        hasOrientationTarget = true;
    }

    public void Solve(float deltaTime)
    {
        if (baseTransform == null || endEffector == null)
        {
            return;
        }

        if (!initialized)
        {
            CacheBase();
        }

        baseTransform.SetPositionAndRotation(basePosition, baseRotation);

        Transform[] chain = BuildChain();
        if (chain.Length == 0)
        {
            return;
        }

        float step = Mathf.Clamp(deltaTime * 12f, 0.01f, 1f);

        // 1. Позиционная часть: сводим TCP к желаемой точке.
        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            if (Vector3.Distance(endEffector.position, desiredPosition) <= positionThreshold)
            {
                break;
            }

            for (int i = chain.Length - 1; i >= 0; i--)
            {
                Transform joint = chain[i];
                Vector3 toEnd = endEffector.position - joint.position;
                Vector3 toTarget = desiredPosition - joint.position;
                if (toEnd.sqrMagnitude < 0.000001f || toTarget.sqrMagnitude < 0.000001f)
                {
                    continue;
                }

                Quaternion correction = Quaternion.FromToRotation(toEnd, toTarget);
                joint.rotation = Quaternion.Slerp(joint.rotation, correction * joint.rotation, step);
            }
        }

        // 2. Ориентационная часть: доворачиваем фланец к целевой ориентации.
        if (hasOrientationTarget)
        {
            AlignOrientation(chain, step);
        }

        baseTransform.SetPositionAndRotation(basePosition, baseRotation);
    }

    /// <summary>Сводит текущую ориентацию фланца к целевой, вращая 2-3 конечных сустава.</summary>
    private void AlignOrientation(Transform[] chain, float step)
    {
        Quaternion delta = Quaternion.Inverse(endEffector.rotation) * desiredRotation;

        Vector3 axis = new Vector3(delta.x, delta.y, delta.z);
        float axisLength = axis.magnitude;
        float angle = 2f * Mathf.Atan2(axisLength, Mathf.Abs(delta.w)); // радианы

        if (angle < Mathf.Deg2Rad * orientationThreshold)
        {
            return;
        }

        if (axisLength < 0.000001f)
        {
            axis = endEffector.up;
        }
        else
        {
            axis = axis / axisLength;
        }

        int jointsToRotate = Mathf.Min(3, chain.Length);
        float perJointDegrees = Mathf.Clamp(angle * step * Mathf.Rad2Deg, 0.05f, 90f);

        for (int i = chain.Length - 1; i >= chain.Length - jointsToRotate; i--)
        {
            chain[i].Rotate(axis, perJointDegrees, Space.World);
        }
    }

    /// <summary>Строит цепочку суставов от фланца вверх до фиксированной базы.</summary>
    private Transform[] BuildChain()
    {
        Transform node = endEffector.parent;

        int count = 0;
        while (node != null && node != baseTransform)
        {
            count++;
            node = node.parent;
        }

        if (node != baseTransform)
        {
            return new Transform[0];
        }

        Transform[] chain = new Transform[count];
        node = endEffector.parent;
        for (int i = count - 1; i >= 0 && node != null && node != baseTransform; i--)
        {
            chain[i] = node;
            node = node.parent;
        }
        return chain;
    }

    private void CacheBase()
    {
        if (baseTransform == null)
        {
            return;
        }

        basePosition = baseTransform.position;
        baseRotation = baseTransform.rotation;
        initialized = true;
    }
}