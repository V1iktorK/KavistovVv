using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Добавляет коллайдеры к каждому звену 6-осного манипулятора и
/// отключает столкновения между соседними звеньями (чтобы робот
/// не сталкивался сам с собой, но при этом взаимодействовал с
/// окружающей средой: стол, стены, другие объекты).
/// </summary>
/// <remarks>
/// 6-осный манипулятор приводит к перекрытию соседних звеньев при больших
/// углах поворота (особенно вблизи стыков J2-J3, J4-J5). Этот скрипт решает
/// задачу «самостолкновений» путём:
/// - присвоения каждому Transform звена простого коллайдера (Capsule);
/// - игнорирования столкновений между соседними и «дальними» звеньями
///   (см. RobotConfig.IgnoredCollisionPairs);
/// - включения столкновений с окружающей средой (рабочая поверхность, стол).
/// </remarks>
[RequireComponent(typeof(Collider))]
public class RobotSelfCollision : MonoBehaviour
{
    [Header("Joint chain (filled by SixAxisController)")]
    public Transform[] jointTransforms;

    [Header("Collision settings")]
    [Tooltip("Отключить столкновения между соседними суставами и известными парами")]
    public bool disableSelfCollisions = true;

    [Tooltip("Радиус цилиндров для коллайдеров суставов")]
    public float jointColliderRadius = 0.08f;

    [Tooltip("Слой для коллайдеров робота (обязательно отличный от стола и стен)")]
    public int robotColliderLayer = 15; // "Robot" по умолчанию

    private readonly List<GameObject> spawnedColliders = new List<GameObject>();

    private void Awake()
    {
        SetupColliders();
        SetupCollisionMatrix();
    }

    private void SetupColliders()
    {
        if (jointTransforms == null || jointTransforms.Length == 0)
            return;

        for (int i = 0; i < jointTransforms.Length; i++)
        {
            if (jointTransforms[i] == null)
                continue;

            GameObject holder = new GameObject($"Collider_{jointTransforms[i].name}");
            holder.transform.SetParent(jointTransforms[i], false);
            holder.layer = robotColliderLayer;

            CapsuleCollider col = holder.AddComponent<CapsuleCollider>();
            col.center = Vector3.zero;
            col.radius = jointColliderRadius;
            col.height = jointColliderRadius * 2f;
            col.direction = 2; // Y-axis

            Rigidbody rb = holder.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            spawnedColliders.Add(holder);
        }

        // Коллайдер для всего робота (контейнер)
        Collider baseCol = GetComponent<Collider>();
        if (baseCol == null)
            baseCol = gameObject.AddComponent<CapsuleCollider>();
        baseCol.isTrigger = false;
    }

    private void SetupCollisionMatrix()
    {
        if (!disableSelfCollisions || jointTransforms == null || jointTransforms.Length == 0)
            return;

        // Список пар, между которыми нужно отключить столкновения
        HashSet<(int, int)> ignoredPairs = GetIgnoredPairs();

        Collider[] allColliders = GetComponentsInChildren<Collider>();
        Dictionary<Transform, Collider> transformToCollider = new Dictionary<Transform, Collider>();

        foreach (var col in allColliders)
        {
            Transform parentJoint = FindParentJoint(col.transform);
            if (parentJoint != null)
                transformToCollider[parentJoint] = col;
        }

        var jointList = new List<Transform>(jointTransforms);
        for (int i = 0; i < jointList.Count; i++)
        {
            for (int j = i + 1; j < jointList.Count; j++)
            {
                if (ignoredPairs.Contains((i, j)))
                {
                    if (transformToCollider.TryGetValue(jointList[i], out Collider colA) &&
                        transformToCollider.TryGetValue(jointList[j], out Collider colB))
                    {
                        Physics.IgnoreCollision(colA, colB, true);
                    }
                }
            }
        }

        // Отключаем столкновения между родительским и дочерним суставами (всегда)
        for (int i = 0; i < allColliders.Length; i++)
        {
            for (int j = i + 1; j < allColliders.Length; j++)
            {
                Transform tA = allColliders[i].transform;
                Transform tB = allColliders[j].transform;

                if (IsParentOrChildOf(tA, tB) || IsParentOrChildOf(tB, tA))
                {
                    Physics.IgnoreCollision(allColliders[i], allColliders[j], true);
                }
            }
        }
    }

    private HashSet<(int, int)> GetIgnoredPairs()
    {
        var pairs = new HashSet<(int, int)>();

        // Соседние суставы J1-J2, J2-J3, J3-J4, J4-J5, J5-J6
        for (int i = 0; i < 5; i++)
            pairs.Add((i, i + 1));

        // Дальние звенья: J1-J3 (основание + первая часть манипулятора)
        pairs.Add((0, 2));

        // J2-J4 могут перекрываться при больших углах
        pairs.Add((1, 3));

        // J3-J5
        pairs.Add((2, 4));

        return pairs;
    }

    private Transform FindParentJoint(Transform t)
    {
        while (t != null && t != transform)
        {
            foreach (var jt in jointTransforms)
            {
                if (jt == t)
                    return t;
            }
            t = t.parent;
        }
        return transform;
    }

    private static bool IsParentOrChildOf(Transform child, Transform parent)
    {
        Transform current = child;
        while (current != null)
        {
            if (current == parent)
                return true;
            current = current.parent;
        }
        return false;
    }

    private void OnDestroy()
    {
        foreach (var obj in spawnedColliders)
        {
            if (obj != null) Destroy(obj);
        }
    }
}
