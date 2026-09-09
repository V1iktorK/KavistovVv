using UnityEngine;

/// <summary>
/// Контроллер плоского (planar) SCARA-робота LS10-B702S:
/// два вращающихся сустава J1/J2 и призматическая ось Z.
/// </summary>
public class SCARAController : RobotController
{
    [Header("LS10 planar joints")]
    public Transform joint1;
    public Transform joint2;
    public Transform joint3; // LS10-B702S_z_5, prismatic Z axis
    public Transform baseTransform;

    [SerializeField] private string baseName = "LS10-B702S_base_1";
    [SerializeField] private string joint1Name = "LS10-B702S_J1_3";
    [SerializeField] private string joint2Name = "LS10-B702S_J2_4";
    [SerializeField] private string joint3Name = "LS10-B702S_z_5";

    [Header("Vertical travel")]
    public float ZMin = -0.3f;
    public float ZMax = 0.3f;

    private Vector3 verticalAxis;
    private float initialHeight;
    private bool geometryCached;
    private bool referencesReported;

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
        Transform resolvedBase = baseTransform != null ? baseTransform : FindChild(new string[] { baseName });
        Transform resolvedJoint1 = joint1 != null ? joint1 : FindChild(new string[] { joint1Name, "J1" });
        Transform resolvedJoint2 = joint2 != null ? joint2 : FindChild(new string[] { joint2Name, "J2" });
        Transform resolvedJoint3 = joint3 != null ? joint3 : FindChild(new string[] { joint3Name, "_z_", "z_5" });

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

        baseTransform = resolvedBase;
        joint1 = resolvedJoint1;
        joint2 = resolvedJoint2;
        joint3 = resolvedJoint3;
        if (!referencesReported)
        {
            Debug.Log("[" + name + "] SCARA joints resolved.", this);
            referencesReported = true;
        }
        if (!geometryCached)
        {
            CacheGeometry();
        }

        Vector3 axis = verticalAxis.sqrMagnitude > 0.001f ? verticalAxis : baseTransform.up;
        Vector3 basePosition = baseTransform.position;
        float settingsSpeed = SettingsData.Instance != null ? SettingsData.Instance.robotSpeed : 1f;
        float speed = Mathf.Clamp01(maxSpeed * settingsSpeed * deltaTime * 5f);

        Vector3 planarTarget = basePosition + Vector3.ProjectOnPlane(targetPosition - basePosition, axis);
        for (int iteration = 0; iteration < 4; iteration++)
        {
            RotateAroundAxisTowards(
                joint2,
                joint3.position - joint2.position,
                planarTarget - joint2.position,
                axis,
                speed);
            RotateAroundAxisTowards(
                joint1,
                joint3.position - joint1.position,
                planarTarget - joint1.position,
                axis,
                speed);
        }

        float currentHeight = Vector3.Dot(joint3.position - basePosition, axis);
        float targetHeight = Vector3.Dot(targetPosition - basePosition, axis);
        float desiredHeight = Mathf.Clamp(targetHeight, initialHeight + ZMin, initialHeight + ZMax);
        float heightOffset = Mathf.Clamp(desiredHeight - currentHeight, -ZMax, ZMax);
        Vector3 desiredZPosition = joint3.position + axis * heightOffset;

        Transform zParent = joint3.parent;
        if (zParent != null)
        {
            joint3.localPosition = Vector3.Lerp(
                joint3.localPosition,
                zParent.InverseTransformPoint(desiredZPosition),
                speed);
        }
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

        initialHeight = Vector3.Dot(joint3.position - baseTransform.position, verticalAxis);
        geometryCached = true;
    }

    /// <summary>Поворачивает сустав вокруг вертикальной оси к желаемому направлению.</summary>
    private static void RotateAroundAxisTowards(
        Transform joint,
        Vector3 currentDirection,
        Vector3 desiredDirection,
        Vector3 axis,
        float amount)
    {
        currentDirection = Vector3.ProjectOnPlane(currentDirection, axis);
        desiredDirection = Vector3.ProjectOnPlane(desiredDirection, axis);
        if (joint == null || currentDirection.sqrMagnitude < 0.001f || desiredDirection.sqrMagnitude < 0.001f)
        {
            return;
        }

        float angle = Vector3.SignedAngle(currentDirection, desiredDirection, axis);
        joint.Rotate(axis, angle * amount, Space.World);
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
            joint1 != null ? joint1.localEulerAngles.y : 0f,
            joint2 != null ? joint2.localEulerAngles.y : 0f,
            joint3 != null ? joint3.localPosition.y : 0f
        };
    }
}
