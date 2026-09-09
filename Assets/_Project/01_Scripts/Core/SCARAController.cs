using UnityEngine;

/// <summary>
/// Контроллер SCARA-робота LS10-B702S на базе честной DH-кинематики.
///
/// Кинематическая схема SCARA (DH: две вращательные оси Z1||Z2 — вертикали,
/// затем призматическая ось Z3 по вертикали):
///   * Прямая задача — параметры звеньев (a1,a2) измеряются автоматически
///     из геометрии (горизонтальные проекции отрезков J1→J2 и J2→z_5);
///   * Обратная задача — АНАЛИТИЧЕСКОЕ планарное решение 2R (закон косинусов):
///     угол локтя выбирается по ближайшей ветви к текущей позе, далее доворот
///     базы — ровно в целевую горизонтальную проекцию точки;
///   * Призматическая ось Z — плавный вертикальный ход z_5 (ZMin..ZMax),
///     как и раньше.
/// Вращения выполняются строго вокруг вертикали (ось Z DH), поэтому робот
/// не «выкручивается» в 3D — только по своей реальной кинематике.
/// </summary>
public class SCARAController : RobotController
{
    [Header("LS10 planar joints")]
    public Transform joint1;   // LS10-B702S_J1_3
    public Transform joint2;   // LS10-B702S_J2_4
    public Transform joint3;   // LS10-B702S_z_5 — призматическая ось (вертикальный ход)
    public Transform baseTransform;

    [SerializeField] private string baseName = "LS10-B702S_base_1";
    [SerializeField] private string joint1Name = "LS10-B702S_J1_3";
    [SerializeField] private string joint2Name = "LS10-B702S_J2_4";
    [SerializeField] private string joint3Name = "LS10-B702S_z_5";

    [Header("Vertical travel (Z prismatic)")]
    public float ZMin = -0.3f;
    public float ZMax = 0.3f;

    // --- DH-параметры (замеренные) ---
    private Vector3 verticalAxis;      // ось Z DH (вертикаль базы)
    private float link1Len;            // a1: плечо J1→J2 (горизонтальная проекция)
    private float link2Len;            // a2: предплечье J2→z_5 (горизонтальная проекция)
    private float initialHeight;       // стартовая высота z_5 над базой
    private Vector3 refDir1;           // стартовое направление звена 1 (для замера текущих углов)
    private Vector3 refDir2;           // стартовое направление звена 2
    private bool geometryCached;
    private bool referencesReported;
    private float lastAngle1;
    private float lastAngle2;

    protected override void Awake()
    {
        base.Awake();
        ResolveJoints();
        CacheGeometry();
        EnsureCableFollow();
    }

    /// <summary>Автоматически подключает кабель (LS10-B702S_cable_2) к точке J2_4.</summary>
    private void EnsureCableFollow()
    {
        Transform cable = FindChild(new string[] { "LS10-B702S_cable_2", "cable_2" });
        if (cable == null) return;
        if (cable.GetComponent<KompasUI.ScaraCableFollow>() != null) return;

        var follow = cable.gameObject.AddComponent<KompasUI.ScaraCableFollow>();
        follow.baseAnchor = baseTransform != null ? baseTransform : transform;
        follow.followTarget = joint2; // LS10-B702S_J2_4
        follow.cable = cable;
        Debug.Log("[SCARA] Кабель подключён к точке " + (joint2 != null ? joint2.name : "J2_4"));
    }

    private void Update()
    {
        MoveToTarget(Time.deltaTime);
        UpdateTelemetry(Time.deltaTime);
    }

    public override void SetTarget(Vector3 position)
    {
        targetPosition = position;
        hasTarget = true;
    }

    public override void SetTarget(Vector3 position, Quaternion rotation)
    {
        // SCARA задаётся точкой; ориентация фланца не управляется (ось Z фиксирована).
        SetTarget(position);
        targetRotation = rotation;
    }

    public override void MoveToTarget(float deltaTime)
    {
        if (!hasTarget)
        {
            return;
        }

        ResolveJoints();
        Transform resolvedBase = baseTransform;
        Transform resolvedJoint1 = joint1;
        Transform resolvedJoint2 = joint2;
        Transform resolvedJoint3 = joint3;

        if (resolvedBase == null || resolvedJoint1 == null || resolvedJoint2 == null || resolvedJoint3 == null)
        {
            if (!referencesReported)
            {
                Debug.LogError(
                    "[" + name + "] SCARA references missing. Assign base, J1, J2 and Z joints in the Inspector.",
                    this);
                referencesReported = true;
            }
            return;
        }

        if (!geometryCached)
        {
            CacheGeometry();
        }

        Vector3 axis = verticalAxis.sqrMagnitude > 0.001f ? verticalAxis : resolvedBase.up;
        Vector3 basePos = resolvedBase.position;
        float settingsSpeed = SettingsData.Instance != null ? SettingsData.Instance.robotSpeed : 1f;
        // Плавность: экспоненциальный шаг к аналитическому решению.
        float step = 1f - Mathf.Exp(-deltaTime * Mathf.Max(1f, maxSpeed * settingsSpeed * 5f));
        step = Mathf.Clamp01(step);

        // Планарная часть: приводим проекцию точки z_5 к проекции цели.
        Vector3 p1 = ProjectToPlane(resolvedJoint1.position, basePos, axis);
        Vector3 p2 = ProjectToPlane(resolvedJoint2.position, basePos, axis);
        Vector3 p3 = ProjectToPlane(resolvedJoint3.position, basePos, axis);
        Vector3 pTarget = ProjectToPlane(targetPosition, basePos, axis);
        Vector3 pT = p1 + (pTarget - p1).normalized * Mathf.Min(
            Vector3.Distance(pTarget, p1), link1Len + link2Len - 0.002f);

        SolvePlanar2R(p1, p2, p3, pT, axis, step);

        // Призматическая ось: вертикальный ход z_5.
        float currentHeight = Vector3.Dot(resolvedJoint3.position - basePos, axis);
        float targetHeight = Vector3.Dot(targetPosition - basePos, axis);
        float desiredHeight = Mathf.Clamp(targetHeight, initialHeight + ZMin, initialHeight + ZMax);
        float heightOffset = Mathf.Clamp(desiredHeight - currentHeight, -ZMax, ZMax);
        Vector3 desiredZPosition = resolvedJoint3.position + axis * heightOffset;

        Transform zParent = resolvedJoint3.parent;
        if (zParent != null)
        {
            float zStep = 1f - Mathf.Exp(-deltaTime * Mathf.Max(1f, maxSpeed * settingsSpeed * 6f));
            resolvedJoint3.localPosition = Vector3.Lerp(
                resolvedJoint3.localPosition,
                zParent.InverseTransformPoint(desiredZPosition),
                Mathf.Clamp01(zStep));
        }
    }

    /// <summary>
    /// Аналитическое решение плоской 2R-кинематики (DH a1, a2):
    /// q2 (угол локтя) — по закону косинусов, ветвь выбирается ближайшей к текущей;
    /// q1 (база) — доворот так, чтобы точка z_5 встала над целью.
    /// Углы суставов здесь — ПРИРАЩЕНИЯ от текущей позы (DeltaAngle), поэтому
    /// солвер корректен из любого положения, а не только из «нулевого».
    /// </summary>
    private void SolvePlanar2R(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 pT, Vector3 axis, float step)
    {
        // Текущие «абсолютные» углы звеньев (от стартовых направлений) и угол локтя.
        float angle1 = MeasureAngle(refDir1, p2 - p1, axis);
        float angle2 = MeasureAngle(refDir2, p3 - p2, axis);
        float bend = Mathf.DeltaAngle(angle1, angle2); // текущий угол локтя, ±
        lastAngle1 = angle1;
        lastAngle2 = bend;

        float r = Mathf.Clamp(Vector3.Distance(pT, p1), 0.001f, link1Len + link2Len - 0.002f);
        float cosQ2 = Mathf.Clamp(
            (r * r - link1Len * link1Len - link2Len * link2Len) / (2f * link1Len * link2Len),
            -1f, 1f);
        float q2AbsDeg = Mathf.Acos(cosQ2) * Mathf.Rad2Deg; // [0..180°] — модуль нужного угла локтя

        // Две ветви локтя: выбираем ту, что ближе к текущему углу.
        float q2 = Mathf.Abs(bend - q2AbsDeg) <= Mathf.Abs(bend + q2AbsDeg)
            ? q2AbsDeg
            : -q2AbsDeg;

        // Приращение локтя и куда встанет звено 2 после доворота.
        float d2 = Mathf.DeltaAngle(bend, q2);
        Vector3 arm2 = RotateAround(p3 - p2, axis, d2 * Mathf.Deg2Rad);
        Vector3 w = (p2 - p1) + arm2;

        // Доворот базы: совместить (звено1 + новое звено2) с целью.
        float d1 = Vector3.SignedAngle(
            Vector3.ProjectOnPlane(w, axis),
            Vector3.ProjectOnPlane(pT - p1, axis),
            axis);

        if (float.IsNaN(d1)) return;

        // Плавные довороты (частичный шаг каждый кадр — сходится к решению).
        if (joint2 != null && Mathf.Abs(d2) > 0.01f)
            joint2.Rotate(axis, d2 * step, Space.World);
        if (joint1 != null && Mathf.Abs(d1) > 0.01f)
            joint1.Rotate(axis, d1 * step, Space.World);
    }

    private static Vector3 ProjectToPlane(Vector3 point, Vector3 origin, Vector3 normal)
    {
        return origin + Vector3.ProjectOnPlane(point - origin, normal);
    }

    private static float MeasureAngle(Vector3 referenceDir, Vector3 currentDir, Vector3 axis)
    {
        Vector3 a = Vector3.ProjectOnPlane(referenceDir, axis);
        Vector3 b = Vector3.ProjectOnPlane(currentDir, axis);
        if (a.sqrMagnitude < 1e-8f || b.sqrMagnitude < 1e-8f) return 0f;
        return Vector3.SignedAngle(a, b, axis);
    }

    private static Vector3 RotateAround(Vector3 v, Vector3 axis, float radians)
    {
        return Quaternion.AngleAxis(radians * Mathf.Rad2Deg, axis) * v;
    }

    private void ResolveJoints()
    {
        if (baseTransform == null)
        {
            baseTransform = FindChild(new string[] { baseName });
        }

        if (baseTransform == null)
        {
            baseTransform = transform;
        }

        if (joint1 == null)
        {
            joint1 = FindChild(new string[] { joint1Name, "J1" });
        }
        if (joint2 == null)
        {
            joint2 = FindChild(new string[] { joint2Name, "J2" });
        }
        if (joint3 == null)
        {
            joint3 = FindChild(new string[] { joint3Name, "_z_", "z_5" });
        }

        if (verticalAxis.sqrMagnitude < 0.001f && baseTransform != null)
        {
            verticalAxis = baseTransform.up;
        }
    }

    private void CacheGeometry()
    {
        if (baseTransform == null || joint1 == null || joint2 == null || joint3 == null)
        {
            return;
        }

        if (verticalAxis.sqrMagnitude < 0.001f)
        {
            verticalAxis = baseTransform.up;
        }

        Vector3 basePos = baseTransform.position;
        Vector3 p1 = ProjectToPlane(joint1.position, basePos, verticalAxis);
        Vector3 p2 = ProjectToPlane(joint2.position, basePos, verticalAxis);
        Vector3 p3 = ProjectToPlane(joint3.position, basePos, verticalAxis);

        link1Len = Vector3.Distance(p1, p2);
        link2Len = Vector3.Distance(p2, p3);
        refDir1 = (p2 - p1).normalized;
        refDir2 = (p3 - p2).normalized;
        initialHeight = Vector3.Dot(joint3.position - basePos, verticalAxis);
        geometryCached = true;

        Debug.Log("[SCARA] DH: a1=" + link1Len.ToString("0.000") + " a2=" +
                  link2Len.ToString("0.000") + ", вертикаль=" +
                  verticalAxis.ToString("0.00"));
    }

    /// <summary>
    /// Ищет потомка, имя которого целиком совпадает с одним из переданных
    /// вариантов либо содержит его (без учёта регистра).
    /// </summary>
    private Transform FindChild(string[] names)
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            foreach (string objectName in names)
            {
                if (objectName != null &&
                    (child.name == objectName || ContainsIgnoreCase(child.name, objectName)))
                {
                    return child;
                }
            }
        }

        return null;
    }

    private static bool ContainsIgnoreCase(string text, string substring)
    {
        return text.ToLower().IndexOf(substring.ToLower()) >= 0;
    }

    public override float[] GetJointAngles()
    {
        return new float[]
        {
            lastAngle1,
            lastAngle2,
            joint3 != null ? joint3.localPosition.y : 0f
        };
    }
}
