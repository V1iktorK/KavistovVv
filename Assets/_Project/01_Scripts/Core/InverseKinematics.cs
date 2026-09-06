using UnityEngine;

// InverseKinematics.cs — упрощённый CCD для 6-осевого
public class InverseKinematics : MonoBehaviour
{
    public Transform[] joints;      // 6 суставов от основания к фланцу
    public Transform target;        // Куда должна прийти TCP (Tool Center Point)
    public Transform baseTransform;
    public Transform endEffector;
    public float threshold = 0.01f;
    public int maxIterations = 10;

    private Vector3 desiredPosition;
    private Quaternion desiredRotation;
    private Vector3 basePosition;
    private Quaternion baseRotation;
    private bool initialized;

    private void Awake()
    {
        if (baseTransform != null)
        {
            basePosition = baseTransform.position;
            baseRotation = baseTransform.rotation;
            initialized = true;
        }
    }

    public void SetTarget(Vector3 position, Quaternion rotation)
    {
        desiredPosition = position;
        desiredRotation = rotation;
    }

    public void Solve(float deltaTime)
    {
        if (baseTransform == null || endEffector == null)
        {
            return;
        }

        if (!initialized)
        {
            basePosition = baseTransform.position;
            baseRotation = baseTransform.rotation;
            initialized = true;
        }

        baseTransform.SetPositionAndRotation(basePosition, baseRotation);
        Transform[] chain = BuildChain();
        if (chain.Length == 0) return;

        float step = Mathf.Clamp(deltaTime * 12f, 0.01f, 1f);
        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            if (Vector3.Distance(endEffector.position, desiredPosition) <= threshold)
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

        baseTransform.SetPositionAndRotation(basePosition, baseRotation);
    }

    private Transform[] BuildChain()
    {
        var result = new System.Collections.Generic.List<Transform>();
        Transform current = endEffector.parent;
        while (current != null && current != baseTransform)
        {
            result.Add(current);
            current = current.parent;
        }

        result.Reverse();
        return result.ToArray();
    }
}