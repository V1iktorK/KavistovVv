using TrajectoryCore;
using UnityEngine;

/// <summary>
/// Индикатор прицела (E5 плана, упрощённый онлайн-вариант):
/// каждый кадр спрашивает Reachability Oracle про точку прицела активного робота
/// и красит маркер: зелёный — достижимо с запасом, жёлтый — предельно/лимит,
/// красный — столкновение или недостижимо. Мир столкновений пересобирается
/// периодически (стробоскопически), чтобы не тратить кадр на полный обход сцены.
/// </summary>
public class AimIndicator : MonoBehaviour
{
    [Header("Маркер цели")]
    public float markerSize = 0.045f;          // «шарик» на конце лазера
    public float markerEmission = 3.2f;        // яркость шарика (на уровне лазера или выше)
    public float worldRebuildInterval = 0.5f;

    [Header("Выбор ветви IK (только при подтверждённой цели)")]
    [Tooltip("Разрешить агенту выбора ветви влиять на робота. Включает поток траекторий, а не сам прицел")]
    public bool allowSeedSelection = false;

    [Header("Запасы")]
    public float clearance = 0.02f;
    public float linkRadius = 0.06f;

    private readonly CollisionWorld world = new CollisionWorld();
    private readonly ReachabilityOracle oracle = new ReachabilityOracle();
    private readonly PoseValidator validator = new PoseValidator();
    private readonly IkSolver ik = new IkSolver();
    private readonly PostureSelector posture = new PostureSelector();
    private GameObject marker;
    private Renderer markerRenderer;
    private Material markerMaterial;
    private float nextRebuild;
    private RobotController lastRobot;
    private Vector3 lastSelectionPoint = new Vector3(9999f, 9999f, 9999f);

    public ReachResult Last { get; private set; }
    public bool HasResult { get; private set; }
    public string LastBranch { get; private set; } = "";

    private void Awake()
    {
        oracle.clearance = clearance;
        oracle.linkRadius = linkRadius;
        CreateMarker();
    }

    private void CreateMarker()
    {
        marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = "AimMarker";
        Collider col = marker.GetComponent<Collider>();
        if (col != null) Destroy(col);
        marker.transform.localScale = Vector3.one * markerSize;

        Shader shader = Shader.Find("HDRP/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        markerMaterial = new Material(shader);
        markerRenderer = marker.GetComponent<Renderer>();
        if (markerRenderer != null)
        {
            markerRenderer.material = markerMaterial;
            markerRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            markerRenderer.receiveShadows = false;
        }
        marker.SetActive(false);
    }

    private void Update()
    {
        RobotController robot = FindActiveRobot();
        if (robot != lastRobot)
        {
            lastRobot = robot;
            oracle.Init(robot, world);
            validator.Init(robot);
            ik.Init(validator);
            posture.Init(validator);
            world.Rebuild(robot, linkRadius);
            nextRebuild = 0f;
            HasResult = false;
        }

        if (robot == null) { if (marker != null) marker.SetActive(false); return; }

        if (Time.realtimeSinceStartup >= nextRebuild)
        {
            world.Rebuild(robot, linkRadius);
            nextRebuild = Time.realtimeSinceStartup + worldRebuildInterval;
        }
    }

    /// <summary>Вызывается контроллером камеры: точка прицела и попадание в поверхность.</summary>
    public void UpdateAim(Vector3 aimPoint, bool hitSurface)
    {
        if (lastRobot == null || !oracle.Ready)
        {
            if (marker != null) marker.SetActive(false);
            HasResult = false;
            return;
        }

        if (!hitSurface)
        {
            if (marker != null) marker.SetActive(false);
            HasResult = false;
            return;
        }

        Last = oracle.Query(aimPoint);
        HasResult = true;
        if (marker != null)
        {
            marker.SetActive(true);
            marker.transform.position = aimPoint;
            ApplyColor(Last.verdict);
        }

        // Выбор «удобной» ветви IK (posture locking). ВАЖНО: влияет на робота только
    // когда это разрешено (allowSeedSelection) — иначе без подтверждённой точки
    // робот не должен никуда тянуться.
        if (allowSeedSelection && lastRobot is SixAxisController six && ik.Ready && validator.Ready)
        {
            if ((aimPoint - lastSelectionPoint).sqrMagnitude > 0.0004f) // > 2 см
            {
                lastSelectionPoint = aimPoint;
                double[] qNow = validator.CopyCurrent();
                var branches = ik.SolveAll(aimPoint, qNow);
                if (branches.Count > 0)
                {
                    IkSolution best = posture.Select(branches, qNow);
                    if (best.q != null && best.withinLimits)
                    {
                        six.SetPreferredSeed(best.q);
                        LastBranch = best.tag + " (ветвей " + branches.Count + ")";
                    }
                }
            }
        }
    }

    private void ApplyColor(ReachVerdict verdict)
    {
        if (markerMaterial == null) return;
        Color c = verdict == ReachVerdict.Safe ? new Color(0.15f, 1f, 0.3f)
            : verdict == ReachVerdict.Marginal ? new Color(1f, 0.85f, 0.1f)
            : new Color(1f, 0.15f, 0.1f);

        if (markerMaterial.HasProperty("_BaseColor")) markerMaterial.SetColor("_BaseColor", c);
        if (markerMaterial.HasProperty("_EmissiveColor"))
        {
            markerMaterial.SetColor("_EmissiveColor", c * markerEmission);   // ярче линии лазера
            markerMaterial.EnableKeyword("_EMISSION");
        }
        if (markerMaterial.HasProperty("_Color")) markerMaterial.SetColor("_Color", c);
    }

    private static RobotController FindActiveRobot()
    {
        RobotController[] robots = Object.FindObjectsByType<RobotController>(FindObjectsInactive.Exclude);
        foreach (RobotController rc in robots)
            if (rc != null && rc.isActive) return rc;
        return robots.Length > 0 ? robots[0] : null;
    }
}
