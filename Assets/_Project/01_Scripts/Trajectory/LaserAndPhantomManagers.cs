using System.Collections.Generic;
using UnityEngine;

namespace TrajectoryCore
{
    /// <summary>
    /// Материалы-«призраки» и неоновые материалы (HDRP): настоящая прозрачность
    /// переключается на surface type Transparent + альфа-ключи, иначе — тонировка.
    /// </summary>
    public static class GhostMaterial
    {
        public static void MakeGhost(Material m, Color tint, float alpha)
        {
            if (m == null) return;
            if (m.HasProperty("_SurfaceType")) m.SetFloat("_SurfaceType", 1f);        // Transparent
            if (m.HasProperty("_BlendMode")) m.SetFloat("_BlendMode", 0f);           // Alpha
            if (m.HasProperty("_AlphaCutoffEnable")) m.SetFloat("_AlphaCutoffEnable", 0f);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.EnableKeyword("_BLENDMODE_ALPHA");
            m.EnableKeyword("_ALPHATEST_OFF");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(tint.r, tint.g, tint.b, alpha));
            if (m.HasProperty("_Color")) m.SetColor("_Color", new Color(tint.r, tint.g, tint.b, alpha));
            if (m.HasProperty("_EmissiveColor"))
            {
                m.SetColor("_EmissiveColor", new Color(tint.r, tint.g, tint.b, 1f) * 0.8f);
                m.EnableKeyword("_EMISSION");
            }
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.5f);
        }

        /// <summary>«Ядовитый» неоновый материал (для шарика и линий траекторий).</summary>
        public static void MakeNeon(Material m, Color color, float emission)
        {
            if (m == null) return;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_UnlitColor")) m.SetColor("_UnlitColor", color * emission);
            if (m.HasProperty("_EmissiveColor"))
            {
                m.SetColor("_EmissiveColor", color * emission);
                m.EnableKeyword("_EMISSION");
            }
        }
    }

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
        public float explode = 0f;          // 0 = фантомы на своей базе; >0 — «разлёт»
        public float pickRadius = 0.25f;
        public Color ghostTint = new Color(0.55f, 1f, 0.65f, 1f);
        public float moveSpeed = 1f / 30f;  // 1 юнит за 30 секунд (как движение робота)

        private readonly List<PhantomConfig> configs = new List<PhantomConfig>();
        private readonly List<double[]> startPoses = new List<double[]>();
        private readonly List<double[]> targetPoses = new List<double[]>();
        private readonly List<float> progress = new List<float>();
        private readonly List<float> durations = new List<float>();
        private RobotController template;
        private PoseValidator validator;
        private Transform root;

        public IReadOnlyList<PhantomConfig> Configs => configs;
        public int Count => configs.Count;

        /// <summary>Все фантомы доехали до своих конечных поз?</summary>
        public bool AllArrived
        {
            get
            {
                if (configs.Count == 0) return false;
                for (int i = 0; i < progress.Count; i++)
                    if (progress[i] < 1f) return false;
                return true;
            }
        }

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

        /// <summary>
        /// Показать фантомы: создаются в ТЕКУЩЕЙ позе робота и плавно едут в свои позы
        /// (1 юнит за 30 секунд), каждый со своим оттенком.
        /// </summary>
        public void Show(List<IkSolution> solutions, List<int> indices, double[] startQ, float speedUnitsPerSec)
        {
            Hide();
            if (template == null || solutions == null) return;

            for (int k = 0; k < indices.Count; k++)
            {
                IkSolution s = solutions[indices[k]];
                var cfg = new PhantomConfig
                {
                    index = configs.Count,
                    q = s.q,
                    tag = s.tag,
                    fkError = (float)s.fkError
                };
                float hue = indices.Count > 1 ? (float)k / indices.Count : 0f;
                cfg.ghost = CreateGhost(cfg, hue, k, indices.Count);
                configs.Add(cfg);

                // Старт — из позы реального робота (плавный переезд), финиш — целевая поза.
                ApplyPose(cfg.ghost, startQ);
                startPoses.Add((double[])startQ.Clone());
                targetPoses.Add((double[])s.q.Clone());

                double len = PhantomMath.ConfigDistance(startQ, s.q, null);
                durations.Add(Mathf.Max(0.05f, (float)len / Mathf.Max(0.001f, speedUnitsPerSec)));
                progress.Add(0f);
            }
        }

        /// <summary>Анимация переезда фантомов (вызывается потоком каждый кадр).</summary>
        public void Tick(float deltaTime)
        {
            for (int i = 0; i < configs.Count; i++)
            {
                if (configs[i].ghost == null || i >= progress.Count) continue;
                if (progress[i] >= 1f) continue;
                progress[i] = Mathf.Clamp01(progress[i] + deltaTime / Mathf.Max(0.05f, durations[i]));
                ApplyPose(configs[i].ghost, LerpPose(startPoses[i], targetPoses[i], progress[i]));
            }
        }

        private static double[] LerpPose(double[] a, double[] b, float t)
        {
            var q = new double[a.Length];
            for (int i = 0; i < a.Length; i++) q[i] = a[i] + (b[i] - a[i]) * t;
            return q;
        }

        private void ApplyPose(GameObject ghost, double[] q)
        {
            if (ghost == null || validator == null || !validator.Ready || q == null) return;
            if (validator.Dof == 3) validator.ApplyScaraToCopy(ghost.transform, q);
            else
            {
                Transform[] joints = validator.FindCopyJoints(ghost.transform);
                if (joints.Length > 0) validator.ApplyToCopy(joints, q);
            }
        }

        private GameObject CreateGhost(PhantomConfig cfg, float hue, int index, int total)
        {
            GameObject ghost = Instantiate(template.gameObject);
            ghost.name = "Phantom_" + index;
            ghost.transform.SetParent(root, false);
            ghost.transform.position = template.transform.position;
            ghost.transform.rotation = template.transform.rotation;
            ghost.transform.localScale = Vector3.one;

            foreach (MonoBehaviour mb in ghost.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb != null) mb.enabled = false;
            foreach (Collider c in ghost.GetComponentsInChildren<Collider>(true))
                Destroy(c);
            SixAxisAutoSetup auto = ghost.GetComponent<SixAxisAutoSetup>();
            if (auto != null) Destroy(auto);
            Rigidbody rb = ghost.GetComponent<Rigidbody>();
            if (rb != null) Destroy(rb);

            // Разный оттенок на каждый фантом — визуально различимы.
            Color tint = Color.HSVToRGB(Mathf.Repeat(0.28f + hue * 0.5f, 1f), 0.85f, 1f);
            foreach (Renderer r in ghost.GetComponentsInChildren<Renderer>(true))
            {
                Material m = r.material;
                if (m == null) continue;
                GhostMaterial.MakeGhost(m, tint, 0.45f);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            return ghost;
        }

        public void Hide()
        {
            foreach (PhantomConfig c in configs)
                if (c.ghost != null) Destroy(c.ghost);
            configs.Clear();
            startPoses.Clear();
            targetPoses.Clear();
            progress.Clear();
            durations.Clear();
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
