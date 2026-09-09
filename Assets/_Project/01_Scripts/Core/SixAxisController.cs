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

    [Header("Self-collision guard")]
    [Tooltip("Разрешить кинематический самоколлизионный запрет движения")]
    public bool enableSelfCollisionGuard = true;
    [Tooltip("Радиус звеньев для проверки самопересечения звеньев")]
    public float linkRadius = 0.07f;
    [Tooltip("При коллизии откатывать только конфликтующие суставы (иначе стоп всего движения)")]
    public bool selectiveRollback = true;

    private Vector3 smoothedTargetPosition;
    private Quaternion smoothedTargetRotation;
    private bool smoothingInitialized;
    private bool selfCollisionSetup;
    private bool jointLimitsInitialized;
    private readonly Quaternion[] rollbackPose = new Quaternion[6];

    public bool SelfCollisionBlocked { get; private set; }

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

        // tcp не назначен в инспекторе → берём фланец (endEffector),
        // чтобы телеоперация/телеметрия не падали с UnassignedReferenceException.
        if (tcp == null)
        {
            tcp = endEffector;
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
            int n = jointTransforms != null ? jointTransforms.Length : 0;
            SnapshotPose(rollbackPose, n);

            ik.SetTarget(targetPosition, targetRotation);
            ik.Solve(deltaTime);

            if (enableSelfCollisionGuard)
                ResolveSelfCollision(deltaTime, rollbackPose, n);
        }
        else
            base.MoveToTarget(deltaTime);
    }

    private void SnapshotPose(Quaternion[] buffer, int count)
    {
        for (int i = 0; i < count && i < jointTransforms.Length && i < buffer.Length; i++)
        {
            buffer[i] = jointTransforms[i] != null ? jointTransforms[i].localRotation : Quaternion.identity;
        }
    }

    /// <summary>
    /// Проверяет самопересечение звеньев робота (капсульные отрезки между осями).
    /// Если пересечение есть — откатывает суставы в безопасную позу, чтобы корпус
    /// не проходил сквозь себя, либо (селективно) только конфликтующие.
    /// </summary>
    private void ResolveSelfCollision(float deltaTime, Quaternion[] pose, int count)
    {
        if (count < 2) return;

        // Ищем пару конфликтующих (не соседних) звеньев.
        bool collision = TryFindSelfCollision(out int a, out int b);

        if (!collision)
        {
            SelfCollisionBlocked = false;
            return;
        }

        // Откат: возвращаем суставы в позу до IK (полный или селективный).
        int lo = selectiveRollback ? Mathf.Max(0, a - 1) : 0;
        int hi = selectiveRollback ? Mathf.Min(count - 1, b + 1) : count - 1;
        for (int i = lo; i <= hi; i++)
        {
            if (i < 0 || i >= count) continue;
            var j = jointTransforms[i];
            if (j == null) continue;
            float blend = Mathf.Clamp01(deltaTime * 12f);
            j.localRotation = Quaternion.Slerp(j.localRotation, pose[i], blend);
        }

        if (!SelfCollisionBlocked)
        {
            SelfCollisionBlocked = true;
            Debug.LogWarning($"[SixAxis] Самостолкновение звеньев {a} и {b} — движение приостановлено, выполнен откат. " +
                             "Попробуйте другую цель/траекторию.");
        }
    }

    /// <summary>
    /// Ищет первую пару НЕ-соседних физических звеньев, капсулы которых пересекаются.
    /// Совпадающие оси (Axis4 == Axis5 в запястье 6-осевого робота) схлопываются
    /// в один узел — иначе сегменты, сходящиеся в одной точке, всегда «пересекаются».
    /// </summary>
    private bool TryFindSelfCollision(out int indexA, out int indexB)
    {
        indexA = -1;
        indexB = -1;
        if (jointTransforms == null) return false;

        // 1. Строим цепочку уникальных узлов (пропускаем оси, совпадающие с предыдущей).
        var nodes = new System.Collections.Generic.List<Vector3>();
        var nodeJoints = new System.Collections.Generic.List<int>(); // исходный индекс для узла
        const float mergeEpsilonSqr = 1e-8f;
        for (int i = 0; i < jointTransforms.Length; i++)
        {
            if (jointTransforms[i] == null) continue;
            Vector3 p = jointTransforms[i].position;
            if (nodes.Count > 0 && (p - nodes[nodes.Count - 1]).sqrMagnitude < mergeEpsilonSqr)
                continue; // та же точка (совпадающие оси запястья) — один узел
            nodes.Add(p);
            nodeJoints.Add(i);
        }

        if (nodes.Count < 3) return false;

        // 2. Звенья = отрезки между соседними уникальными узлами.
        // Проверяем только пары звеньев, которые НЕ делят узел (b >= a+3).
        for (int a = 0; a < nodes.Count - 2; a++)
        {
            for (int b = a + 3; b < nodes.Count; b++)
            {
                if (SegmentsOverlap(nodes[a], nodes[a + 1], nodes[b - 1], nodes[b], linkRadius))
                {
                    indexA = nodeJoints[a];
                    indexB = nodeJoints[b];
                    return true;
                }
            }
        }
        return false;
    }

    private static bool SegmentsOverlap(Vector3 a1, Vector3 a2, Vector3 b1, Vector3 b2, float radius)
    {
        return ClosestDistanceBetweenSegments(a1, a2, b1, b2) < radius * 2f;
    }

    /// <summary>Минимальное расстояние между двумя отрезками (3D).</summary>
    public static float ClosestDistanceBetweenSegments(
        Vector3 p1, Vector3 p2, Vector3 q1, Vector3 q2)
    {
        Vector3 u = p2 - p1;
        Vector3 v = q2 - q1;
        Vector3 w = p1 - q1;

        float a = Vector3.Dot(u, u);
        float b = Vector3.Dot(u, v);
        float c = Vector3.Dot(v, v);
        float d = Vector3.Dot(u, w);
        float e = Vector3.Dot(v, w);

        float denom = a * c - b * b;
        float sN, sD = denom;
        float tN, tD = denom;

        if (denom < 1e-6f)
        {
            sN = 0f;
            sD = 1f;
            tN = e;
            tD = c;
        }
        else
        {
            sN = b * e - c * d;
            tN = a * e - b * d;
            if (sN < 0f) { sN = 0f; tN = e; tD = c; }
            else if (sN > sD) { sN = sD; tN = e + b; tD = c; }
        }

        if (tN < 0f)
        {
            tN = 0f;
            if (-d < 0f) sN = 0f;
            else if (-d > a) sN = sD;
            else { sN = -d; sD = a; }
        }
        else if (tN > tD)
        {
            tN = tD;
            if (-d + b < 0f) sN = 0f;
            else if (-d + b > a) sN = sD;
            else { sN = (-d + b); sD = a; }
        }

        float sc = Mathf.Abs(sN) < 1e-6f ? 0f : sN / sD;
        float tc = Mathf.Abs(tN) < 1e-6f ? 0f : tN / tD;

        Vector3 dP = w + (sc * u) - (tc * v);
        return dP.magnitude;
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
