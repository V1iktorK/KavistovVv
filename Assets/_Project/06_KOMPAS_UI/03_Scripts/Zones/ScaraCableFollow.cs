using UnityEngine;

namespace KompasUI
{
    /// <summary>
    /// «Физика» кабеля SCARA (LS10-B702S_cable_2): нижняя точка кабеля неподвижно
    /// держится за base_1, а сама петля кабеля разворачивается так, чтобы её
    /// «дальний конец» (направление меша от пивота к центру) смотрел на точку
    /// LS10-B702S_J2_4. Кабель следует за рукой, оставаясь закреплённым у базы.
    /// </summary>
    public class ScaraCableFollow : MonoBehaviour
    {
        [Tooltip("Неподвижная база (LS10-B702S_base_1)")]
        public Transform baseAnchor;
        [Tooltip("Точка руки, за которой следует кабель (LS10-B702S_J2_4)")]
        public Transform followTarget;
        [Tooltip("Сам кабель (LS10-B702S_cable_2)")]
        public Transform cable;

        [Header("Точки крепления")]
        [Tooltip("Локально в base: куда крепится низ кабеля (авто = текущая позиция кабеля)")]
        public Vector3 baseAttachLocal;
        [Tooltip("Локально в followTarget: за какую точку тянется кабель")]
        public Vector3 targetAttachLocal = Vector3.zero;
        [Tooltip("Сглаживание поворота (1 = мгновенно)")]
        public float rotationSpeed = 8f;

        private Vector3 meshDirLocal = Vector3.up; // направление «петли» в локальных осях кабеля
        private Quaternion startLocalRotation;
        private bool initialized;

        void Start()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (baseAnchor == null) baseAnchor = FindChild(transform, "LS10-B702S_base_1");
            if (followTarget == null) followTarget = FindChild(transform, "LS10-B702S_J2_4");
            if (cable == null) cable = FindChild(transform, "LS10-B702S_cable_2");
            if (baseAnchor == null || followTarget == null || cable == null) return;

            // Точка крепления к базе = текущее положение кабеля в координатах базы.
            if (baseAttachLocal == Vector3.zero)
                baseAttachLocal = baseAnchor.InverseTransformPoint(cable.position);

            // Направление меша кабеля: от пивота к центру его границ (локально).
            Renderer r = cable.GetComponentInChildren<Renderer>();
            if (r != null)
            {
                Vector3 worldDir = r.bounds.center - cable.position;
                if (worldDir.sqrMagnitude > 0.0001f)
                    meshDirLocal = cable.InverseTransformDirection(worldDir).normalized;
            }
            else
            {
                meshDirLocal = Vector3.up;
            }

            startLocalRotation = cable.localRotation;
            initialized = true;
        }

        void LateUpdate()
        {
            if (!initialized)
            {
                Initialize();
                if (!initialized) return;
            }

            Vector3 a = baseAnchor.TransformPoint(baseAttachLocal);
            Vector3 b = followTarget.TransformPoint(targetAttachLocal);

            // Низ кабеля «стоит» в точке крепления на базе.
            cable.position = a;

            // Куда сейчас смотрит петля кабеля (в мире).
            Vector3 currentDir = cable.TransformDirection(meshDirLocal);
            Vector3 desiredDir = b - a;
            if (desiredDir.sqrMagnitude < 0.0001f || currentDir.sqrMagnitude < 0.0001f) return;
            desiredDir.Normalize();
            currentDir.Normalize();

            Quaternion delta = Quaternion.FromToRotation(currentDir, desiredDir);
            float t = Mathf.Clamp01(rotationSpeed * Time.deltaTime);
            cable.rotation = Quaternion.Slerp(cable.rotation, delta * cable.rotation, t);
        }

        private static Transform FindChild(Transform root, string name)
        {
            if (root == null) return null;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t;
            }
            return null;
        }
    }
}
