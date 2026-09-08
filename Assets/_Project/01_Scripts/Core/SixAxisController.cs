using UnityEngine;

/// <summary>
/// Контроллер 6-осевого манипулятора на базе инверсной кинематики (InverseKinematics).
/// </summary>
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
            baseTransform = FindChild(root, baseName);
            if (baseTransform == null)
            {
                baseTransform = FindChild(root, "Root");
            }
            if (baseTransform == null)
            {
                baseTransform = root;
            }
        }
        fixedBase = baseTransform;

        if (endEffector == null)
        {
            endEffector = FindChild(root, endEffectorName);
            if (endEffector == null)
            {
                endEffector = FindChild(root, alternativeEndEffectorName);
            }
            if (endEffector == null)
            {
                endEffector = FindDeepestDescendant(root);
            }
        }

        if (jointTransforms == null || jointTransforms.Length == 0)
        {
            jointTransforms = new Transform[]
            {
                FirstNonNull(FindChild(root, "Axis1_2"), FindChild(root, "Axis1")),
                FirstNonNull(FindChild(root, "Axis2_2"), FindChild(root, "Axis2")),
                FirstNonNull(FindChild(root, "Axis3_2"), FindChild(root, "Axis3")),
                FirstNonNull(FindChild(root, "Axis4_2"), FindChild(root, "Axis4")),
                FirstNonNull(FindChild(root, "Axis5_2"), FindChild(root, "Axis5")),
                FirstNonNull(FindChild(root, "Axis6_2"), FindChild(root, "Axis6"))
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

    public override void SetTarget(Vector3 position)
    {
        targetPosition = position;
        hasTarget = true;
        if (ik != null && ik.target != null)
        {
            ik.target.position = targetPosition;
        }
    }

    public override void SetTarget(Vector3 position, Quaternion rotation)
    {
        SetTarget(position);
        targetRotation = rotation;
        if (ik != null && ik.target != null)
        {
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
        if (jointTransforms == null)
        {
            return new float[0];
        }

        float[] angles = new float[jointTransforms.Length];
        for (int i = 0; i < jointTransforms.Length; i++)
        {
            angles[i] = jointTransforms[i] != null ? jointTransforms[i].localEulerAngles.y : 0f;
        }
        return angles;
    }

    private static Transform FirstNonNull(Transform first, Transform second)
    {
        return first != null ? first : second;
    }
}
