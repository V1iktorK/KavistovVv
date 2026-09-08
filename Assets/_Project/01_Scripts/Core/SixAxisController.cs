using UnityEngine;

/// <summary>
/// Контроллер 6-осного манипулятора на базе инверсной кинематики (InverseKinematics).
/// Реализует CCD-IK для позиции, выравнивание ориентации TCP, плавность движения,
/// ограничения суставов и систему самостолкновений.
/// </summary>
public class SixAxisController : RobotController
{
    [Header("6-Axis IK")]
    public InverseKinematics ik;
    public Transform[] jointTransforms;
    public Transform baseTransform;
    public string baseName = "LS10-B702S_base_1";

    [Header("Smoothing")]
    [Range(0f, 0.99f)] public float positionSmoothing = 0.85f;
    [Range(0f, 0.99f)] public float rotationSmoothing = 0.9f;

    [Header("Joint Limits")]
    public Vector2[] jointLimits = new Vector2[6];

    private Vector3 smoothedTargetPosition;
    private Quaternion smoothedTargetRotation;
    private bool smoothingInitialized;
    private bool selfCollisionSetup;
    private bool jointLimitsInitialized;

    void Update()
    {
        ResolveRobotReferences();
        SetupSelfCollision();
        MoveToTarget(Time.deltaTime);
        ApplyJointLimits();
        UpdateTelemetry(Time.deltaTime);
    }

    private void SetupSelfCollision()
    {
        if (selfCollisionSetup)
            return;

        if (jointTransforms == null || jointTransforms.Length == 0)
            return;

        var collision = GetComponent<RobotSelfCollision>();
        if (collision == null)
            collision = gameObject.AddComponent<RobotSelfCollision>();

        collision.jointTransforms = jointTransforms;
        collision.disableSelfCollisions = true;
        collision.jointColliderRadius = 0.08f;
        selfCollisionSetup = true;
    }

    private void ResolveRobotReferences()
    {
        if (ik == null)
            ik = GetComponent<InverseKinematics>();

        Transform root = transform;
        if (baseTransform == null)
        {
            baseTransform = FindChild(root, baseName);
            if (baseTransform == null)
                baseTransform = FindChild(root, "Root");
            if (baseTransform == null)
                baseTransform = root;
        }
        fixedBase = baseTransform;

        if (endEffector == null)
        {
            endEffector = FindChild(root, endEffectorName);
            if (endEffector == null)
                endEffector = FindChild(root, alternativeEndEffectorName);
            if (endEffector == null)
                endEffector = FindDeepestDescendant(root);
        }

        if (jointTransforms == null || jointTransforms.Length == 0)
        {
            jointTransforms = new Transform[]
            {
                FirstNonNull(FindChild(root, "Axis1_2"), FindChild(root, "Axis1")),
                FirstNullOr(FindChild(root, "Axis2_2"), FindChild(root, "Axis2")),
                FirstNullOr(FindChild(root, "Axis3_2"), FindChild(root, "Axis3")),
                FirstNullOr(FindChild(root, "Axis4_2"), FindChild(root, "Axis4")),
                FirstNullOr(FindChild(root, "Axis5_2"), FindChild(root, "Axis5")),
                FirstNullOr(FindChild(root, "Axis6_2"), FindChild(root, "Axis6"))
            };
        }

        if (ik != null)
        {
            ik.baseTransform = baseTransform;
            ik.endEffector = endEffector;
            if (ik.joints == null || ik.joints.Length == 0)
                ik.joints = jointTransforms;
            ik.positionThreshold = ikTolerance;
            ik.orientationThreshold = 1f;
            ik.maxIterations = ikIterations;
        }

        if (!jointLimitsInitialized)
        {
            InitializeJointLimits();
            jointLimitsInitialized = true;
        }
    }

    private void InitializeJointLimits()
    {
        if (jointLimits == null || jointLimits.Length < 6)
            jointLimits = new Vector2[6];

        jointLimits[0] = new Vector2(-170f, 170f);
        jointLimits[1] = new Vector2(-90f, 150f);
        jointLimits[2] = new Vector2(-70f, 225f);
        jointLimits[3] = new Vector2(-180f, 180f);
        jointLimits[4] = new Vector2(-120f, 120f);
        jointLimits[5] = new Vector2(-180f, 180f);
    }

    public override void SetTarget(Vector3 position)
    {
        if (!smoothingInitialized)
        {
            smoothedTargetPosition = position;
            smoothedTargetRotation = Quaternion.identity;
            smoothingInitialized = true;
        }

        smoothedTargetPosition = Vector3.Lerp(smoothedTargetPosition, position, 1f - positionSmoothing);

        if (ik != null && ik.target != null)
            ik.target.position = smoothedTargetPosition;

        targetPosition = smoothedTargetPosition;
        hasTarget = true;
    }

    public override void SetTarget(Vector3 position, Quaternion rotation)
    {
        if (!smoothingInitialized)
        {
            smoothedTargetPosition = position;
            smoothedTargetRotation = rotation;
            smoothingInitialized = true;
        }

        smoothedTargetPosition = Vector3.Lerp(smoothedTargetPosition, position, 1f - positionSmoothing);
        smoothedTargetRotation = Quaternion.Slerp(smoothedTargetRotation, rotation, 1f - rotationSmoothing);

        targetPosition = smoothedTargetPosition;
        targetRotation = smoothedTargetRotation;
        hasTarget = true;

        if (ik != null && ik.target != null)
        {
            ik.target.position = smoothedTargetPosition;
            ik.target.rotation = smoothedTargetRotation;
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
            base.MoveToTarget(deltaTime);
    }

    private void ApplyJointLimits()
    {
        if (jointTransforms == null || jointTransforms.Length == 0 || jointLimits == null || jointLimits.Length == 0)
            return;

        for (int i = 0; i < jointTransforms.Length && i < jointLimits.Length; i++)
        {
            if (jointTransforms[i] == null)
                continue;

            Vector3 angles = jointTransforms[i].localEulerAngles;
            float rawY = angles.y;
            float normalized = rawY > 180f ? rawY - 360f : rawY;
            float clamped = Mathf.Clamp(normalized, jointLimits[i].x, jointLimits[i].y);
            float smoothed = Mathf.Lerp(normalized, clamped, 0.5f);
            jointTransforms[i].localEulerAngles = new Vector3(angles.x, smoothed, angles.z);
        }
    }

    public override float[] GetJointAngles()
    {
        if (jointTransforms == null)
            return new float[0];

        float[] angles = new float[jointTransforms.Length];
        for (int i = 0; i < jointTransforms.Length; i++)
            angles[i] = jointTransforms[i] != null ? jointTransforms[i].localEulerAngles.y : 0f;
        return angles;
    }

    private static Transform FirstNonNull(Transform first, Transform second)
    {
        return first != null ? first : second;
    }

    private static Transform FirstNullOr(Transform first, Transform second)
    {
        return first != null ? first : second;
    }
}
