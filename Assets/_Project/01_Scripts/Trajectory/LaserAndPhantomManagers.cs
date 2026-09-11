using System.Collections.Generic;
using UnityEngine;

namespace TrajectoryCore
{
    /// <summary>
    /// Менеджер лазеров (два луча): красный — выбор точки на поверхности,
    /// зелёный — подтверждение траектории и фантома.
    /// Лучи берутся из позы камеры и латеральных смещений рук — ровно там же,
    /// где рисованы сами лучи, поэтому «куда смотрю, туда и выбираю».
    /// </summary>
    public class LaserManager : MonoBehaviour
    {
        [Header("Геометрия лучей (как у визуальных лучей)")]
        public float handOffset = 0.35f;
        public float downOffset = 0.15f;
        public float maxDistance = 50f;

        [Header("Выбор")]
        public float tubePickRadius = 0.05f;   // радиус «колбаски» (1/20 юнита)
        public float phantomPickRadius = 0.18f;

        public Ray RedRay { get; private set; }
        public Ray GreenRay { get; private set; }
        public bool RedAimValid { get; private set; }
        public bool GreenAimValid { get; private set; }
        public Vector3 RedHitPoint { get; private set; }
        public Vector3 GreenHitPoint { get; private set; }

        private Camera cam;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            if (cam == null) cam = Camera.main;
        }

        /// <summary>Обновить оба луча по текущей позе камеры (вызывается каждый кадр).</summary>
        public void UpdateRays(Vector3 aimPoint, bool aimHit)
        {
            Transform t = cam != null ? cam.transform : transform;
            Vector3 redOrigin = t.position - t.right * handOffset - t.up * downOffset;
            Vector3 greenOrigin = t.position + t.right * handOffset - t.up * downOffset;
            Vector3 dir = aimHit ? (aimPoint - redOrigin).normalized : t.forward;

            RedRay = new Ray(redOrigin, dir);
            RedAimValid = aimHit;
            RedHitPoint = aimPoint;

            Vector3 gdir = aimHit ? (aimPoint - greenOrigin).normalized : t.forward;
            GreenRay = new Ray(greenOrigin, gdir);
            GreenAimValid = aimHit;
            GreenHitPoint = aimPoint;
        }
    }

    /// <summary>
    /// Менеджер фантомов (MVP-3): полупрозрачные копии модели в разных конфигурациях IK,
    /// пространственно разнесённые, с выбором зелёным лучом.
    /// </summary>
    public class PhantomManager : MonoBehaviour
    {
        public float explode = 0f;          // 0 = фантомы на своей базе (корректная поза); >0 — «разлёт»
        public float pickRadius = 0.18f;
        public Color ghostTint = new Color(0.55f, 1f, 0.65f, 1f);

        private readonly List<PhantomConfig> configs = new List<PhantomConfig>();
        private RobotController template;
        private PoseValidator validator;
        private Transform root;

        public IReadOnlyList<PhantomConfig> Configs => configs;
        public int Count => configs.Count;

        public void Init(RobotController robotTemplate, PoseValidator poseValidator)
        {
            template = robotTemplate;
            validator = poseValidator;
            if (root == null)
            {
                GameObject go = new GameObject("Phantoms");
                root = go.transform;
            }
        }

        /// <summary>Показать фантомы для набора конфигураций (уже выбранных «максимально разными»).</summary>
        public void Show(List<IkSolution> solutions, List<int> indices, Vector3 goal, double[] ranges)
        {
            Hide();
            if (template == null || solutions == null) return;
            Vector3 center = goal;
            for (int k = 0; k < indices.Count; k++)
            {
                IkSolution s = solutions[indices[k]];
                var cfg = new PhantomConfig
                {
                    index = configs.Count,
                    q = s.q,
                    tag = s.tag,
                    cost = 0,
                    sigmaMin = 0,
                    fkError = (float)s.fkError
                };
                cfg.ghost = CreateGhost(cfg, center, k, indices.Count);
                configs.Add(cfg);
            }
        }

        private GameObject CreateGhost(PhantomConfig cfg, Vector3 goal, int index, int total)
        {
            GameObject ghost = Instantiate(template.gameObject);
            ghost.name = "Phantom_" + index;
            ghost.transform.SetParent(root, false);

            // Гасим логику и коллайдеры — это только визуал.
            foreach (MonoBehaviour mb in ghost.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb != null) mb.enabled = false;
            foreach (Collider c in ghost.GetComponentsInChildren<Collider>(true))
                Destroy(c);
            SixAxisAutoSetup auto = ghost.GetComponent<SixAxisAutoSetup>();
            if (auto != null) Destroy(auto);

            // Поза по конфигурации + (опционально) разлёт по кругу вокруг базы.
            float ang = total > 1 ? (float)index / total * Mathf.PI * 2f : 0f;
            Vector3 offset = explode > 1e-4f
                ? new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * explode
                : Vector3.zero;
            ghost.transform.position = template.transform.position + offset;
            ghost.transform.rotation = template.transform.rotation;

            // Копии задаём ту же позу, что и у выбранной ветви IK (через суставы копии).
            if (validator != null && validator.Ready)
            {
                Transform[] copyJoints = validator.FindCopyJoints(ghost.transform);
                if (copyJoints.Length > 0) validator.ApplyToCopy(copyJoints, cfg.q);
            }

            // Полупрозрачный «призрачный» материал.
            foreach (Renderer r in ghost.GetComponentsInChildren<Renderer>(true))
            {
                Material m = r.material;
                if (m == null) continue;
                if (m.HasProperty("_BaseColor"))
                    m.SetColor("_BaseColor", new Color(ghostTint.r, ghostTint.g, ghostTint.b, 0.45f));
                if (m.HasProperty("_EmissiveColor"))
                {
                    m.SetColor("_EmissiveColor", new Color(ghostTint.r, ghostTint.g, ghostTint.b, 1f) * 0.4f);
                    m.EnableKeyword("_EMISSION");
                }
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.35f);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return ghost;
        }

        public void Hide()
        {
            foreach (PhantomConfig c in configs)
                if (c.ghost != null) Destroy(c.ghost);
            configs.Clear();
        }

        /// <summary>Ближайший фантом к лучу (с учётом радиуса выбора).</summary>
        public int HoverIndex(Ray ray)
        {
            int best = -1;
            float bestDist = pickRadius;
            for (int i = 0; i < configs.Count; i++)
            {
                if (configs[i].ghost == null) continue;
                Bounds b = GetBounds(configs[i].ghost);
                float d = DistanceRaySphere(ray, b.center, Mathf.Max(b.extents.magnitude, pickRadius));
                if (d < bestDist) { bestDist = d; best = i; }
            }
            return best;
        }

        public void SetHighlight(int index)
        {
            for (int i = 0; i < configs.Count; i++)
            {
                if (configs[i].ghost == null) continue;
                bool on = i == index;
                foreach (Renderer r in configs[i].ghost.GetComponentsInChildren<Renderer>(true))
                {
                    Material m = r.material;
                    if (m == null) continue;
                    if (m.HasProperty("_EmissiveColor"))
                        m.SetColor("_EmissiveColor", on
                            ? new Color(0.4f, 1f, 0.5f, 1f) * 1.2f
                            : new Color(ghostTint.r, ghostTint.g, ghostTint.b, 1f) * 0.4f);
                }
            }
        }

        private static Bounds GetBounds(GameObject go)
        {
            Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        private static float DistanceRaySphere(Ray ray, Vector3 center, float radius)
        {
            Vector3 m = center - ray.origin;
            float t = Vector3.Dot(m, ray.direction);
            if (t < 0f) t = 0f;
            return Vector3.Distance(center, ray.origin + ray.direction * t) - radius;
        }
    }
}
