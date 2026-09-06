using UnityEngine;

public class SixAxisController : RobotController
{
    [Header("6-Axis IK")]
    public InverseKinematics ik;
    public Transform[] jointTransforms;
    public Transform baseTransform;
    public string baseName = "LS10-B702S_base_1";
    
    void Update()
    {
        ResolveRobotReferences();
        MoveToTarget(Time.deltaTime);
        UpdateTelemetry(Time.deltaTime);
    }

    private void ResolveRobotReferences()
    {
        if (ik == null)
        {
            ik = GetComponent<InverseKinematics>();
        }

        Transform root = transform;
        if (baseTransform == null)
        {
            baseTransform = FindChild(root, baseName) ??
                            FindChild(root, "Root") ??
                            root;
        }
        fixedBase = baseTransform;

        if (endEffector == null)
        {
            endEffector = FindChild(root, endEffectorName) ??
                          FindChild(root, alternativeEndEffectorName) ??
                          FindDeepestDescendant(root);
        }

        if (jointTransforms == null || jointTransforms.Length == 0)
        {
            jointTransforms = new[]
            {
                FindChild(root, "Axis1_2") ?? FindChild(root, "Axis1"),
                FindChild(root, "Axis2_2") ?? FindChild(root, "Axis2"),
                FindChild(root, "Axis3_2") ?? FindChild(root, "Axis3"),
                FindChild(root, "Axis4_2") ?? FindChild(root, "Axis4"),
                FindChild(root, "Axis5_2") ?? FindChild(root, "Axis5"),
                FindChild(root, "Axis6_2") ?? FindChild(root, "Axis6")
            };
        }

        if (ik != null)
        {
            ik.baseTransform = baseTransform;
            ik.endEffector = endEffector;
            if (ik.joints == null || ik.joints.Length == 0)
            {
                ik.joints = jointTransforms;
            }
        }
    }

    private static Transform FindChild(Transform root, string objectName)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == objectName) return child;
        }

        return null;
    }

    private static Transform FindDeepestDescendant(Transform root)
    {
        Transform deepest = null;
        int deepestLevel = -1;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child == root) continue;
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
    
    public override void SetTarget(Vector3 position, Quaternion? rotation = null)
    {
        targetPosition = position;
        targetRotation = rotation ?? Quaternion.identity;
        hasTarget = true;
        if (ik != null && ik.target != null)
        {
            ik.target.position = targetPosition;
            ik.target.rotation = targetRotation;
        }
    }
    
    public override void MoveToTarget(float deltaTime)
    {
        if (ik != null && hasTarget)
        {
            ik.SetTarget(targetPosition, targetRotation);
            ik.Solve(deltaTime);
        }
        else
        {
            base.MoveToTarget(deltaTime);
        }
    }
    
    public override float[] GetJointAngles()
    {
        if (jointTransforms == null) return new float[0];
        float[] angles = new float[jointTransforms.Length];
        for (int i = 0; i < jointTransforms.Length; i++)
            angles[i] = jointTransforms[i] != null ? jointTransforms[i].localEulerAngles.y : 0f;
        return angles;
    }
}
