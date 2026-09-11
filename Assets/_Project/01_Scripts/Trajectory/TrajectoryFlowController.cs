using System.Collections.Generic;
using TrajectoryCore;
using UnityEngine;

/// <summary>
/// Полный сценарий «два лазера» (этапы 0–4 из ТЗ):
///   Этап 0: только наведение, меняется лишь цвет шарика — ничего не считается.
///   Этап 1: ЛКМ при ВКЛЮЧЁННОМ КРАСНОМ лазере → точка фиксируется, считаются 3–5 траекторий.
///           только зелёный / оба выключены → «Включите красный лазер».
///           недостижимая точка → «Выберите другую точку».
///   Этап 2: показаны оранжевые траектории; движение мыши их НЕ удаляет.
///           ЛКМ красным → пересчёт под новую точку; ЛКМ зелёным по колбаске → выбор траектории.
///   Этап 3: появляются фантомы (2–8) и медленно (1 юнит / 30 с) едут из позы робота в свои позы;
///           ЛКМ зелёным по фантому → старт движения.
///   Этап 4: робот едет (1 юнит / 30 с), нажатия игнорируются.
/// Детерминированно, без LLM.
/// </summary>
public class TrajectoryFlowController : MonoBehaviour
{
    [Header("Выбор точки")]
    public int candidateCount = 5;             // 3–5 кандидатов
    public float tubeRadius = 0.05f;
    public float planningSliceMs = 12f;
    public int candidatesPerSlice = 1;

    [Header("Фантомы")]
    public int phantomCount = 8;               // до 8 (6-осевой), 2–4 (SCARA — ограничится сама)
    public float phantomSpeed = 1f / 30f;      // 1 юнит за 30 секунд

    [Header("Движение робота")]
    public float motionSpeed = 1f / 30f;       // 1 юнит за 30 секунд
    public bool slowMotionEnabled = true;

    [Header("Стенды")]
    public bool ensureStandsOnStart = true;

    private LaserManager lasers;
    private PhantomManager phantoms;
    private TrajectoryExecutor executor;
    private MotionExecutor motion;

    private readonly CollisionWorld world = new CollisionWorld();
    private readonly PoseValidator validator = new PoseValidator();
    private readonly IkSolver ik = new IkSolver();
    private readonly PostureSelector posture = new PostureSelector();
    private readonly Planner planner = new Planner();
    private readonly SafetyGate gate = new SafetyGate();
    private readonly SelectionState state = new SelectionState();
    private readonly PlanMetrics metrics = new PlanMetrics();

    private RobotController robot;
    private float worldTimer;
    private bool started;
    private bool phantomSelectionLatch;    // одно нажатие = одно действие
    private float planStartTime;

    // Очередь тайм-слайсов
    private readonly Queue<int> planQueue = new Queue<int>();
    private readonly List<PlannedTrajectory> plannedSoFar = new List<PlannedTrajectory>();
    private double[] planningStart;
    private Vector3 planningTarget;
    private int planningWant;

    public SelectionState State => state;

    private void Awake()
    {
        lasers = gameObject.AddComponent<LaserManager>();
        phantoms = gameObject.AddComponent<PhantomManager>();
        motion = gameObject.AddComponent<MotionExecutor>();
    }

    /// <summary>Кадровый вход: прицел, подтверждения (ЛКМ/ПКМ/триггеры), состояние лазеров.</summary>
    public void UpdateAim(Vector3 aimPoint, bool aimHit, bool confirmRed, bool confirmGreen,
                          bool cancel, bool redLaserOn, bool greenLaserOn)
    {
        lasers.UpdateRays(aimPoint, aimHit);
        if (!started)
        {
            if (ensureStandsOnStart) StandBuilder.EnsureStands(0.98f);
            Rebind();
            started = true;
        }

        RobotController selected = FindSelectedRobot();
        if (selected != robot) { robot = selected; Rebind(); }

        bool anyConfirm = confirmRed || confirmGreen;

        if (robot == null)
        {
            if (anyConfirm) Report("Наведите шарик лазера на робота и нажмите F", Palette.Warn);
            return;
        }

        ProcessPlanningSlices();
        phantoms.Tick(Time.deltaTime);

        worldTimer -= Time.deltaTime;
        if (worldTimer <= 0f) { world.Rebuild(robot, 0.06f); worldTimer = 0.5f; }
        gate.NotifyState();

        if (cancel) { ResetAll("Сброшено (Esc)"); return; }

        switch (state.phase)
        {
            case FlowState.Idle:
                robot.ClearTarget();
                if (anyConfirm) Stage1_TryLockPoint(aimPoint, aimHit, redLaserOn, greenLaserOn);
                break;

            case FlowState.PointSelected:
                robot.ClearTarget();
                UpdateTrajectoryHover(greenLaserOn);
                break;

            case FlowState.TrajectoriesShown:
                robot.ClearTarget();
                UpdateTrajectoryHover(greenLaserOn);
                if (anyConfirm) Stage2_Confirm(confirmRed, confirmGreen, redLaserOn, greenLaserOn, aimPoint, aimHit);
                break;

            case FlowState.PhantomsMoving:
                robot.ClearTarget();
                int hover = phantoms.HoverIndex(lasers.GreenRay);
                state.hoveredPhantom = hover;
                phantoms.SetHighlight(hover);
                if (hover >= 0) ShowPhantomMetrics(hover);
                if (anyConfirm) Stage3_Confirm(confirmRed, confirmGreen, redLaserOn, greenLaserOn);
                break;

            case FlowState.RobotMoving:
                // этап 4: нажатия игнорируются полностью
                if (!executor.IsRunning) FinishMotion();
                break;
        }
    }

    // ------------------------------------------------------------------ этапы

    private void Stage1_TryLockPoint(Vector3 aimPoint, bool aimHit, bool redOn, bool greenOn)
    {
        if (!redOn)
        {
            Report(greenOn ? "Включите красный лазер (Z)" : "Включите лазер (Z — красный)", Palette.Warn);
            return;
        }
        if (!aimHit)
        {
            Report("Наведите красный лазер на поверхность", Palette.Warn);
            return;
        }

        ReachResult verdict = oracleQuery(aimPoint);
        if (verdict.verdict == ReachVerdict.Unreachable || verdict.verdict == ReachVerdict.Collision)
        {
            Report("Выберите другую точку — " + verdict.reason, Palette.Bad);
            return;
        }

        LockPoint(aimPoint);
    }

    private void Stage2_Confirm(bool confirmRed, bool confirmGreen, bool redOn, bool greenOn,
        Vector3 aimPoint, bool aimHit)
    {
        if (redOn && greenOn)
        {
            Report("Выберите один лазер: красный (Z) — точка, зелёный (X) — траектория", Palette.Warn);
            return;
        }
        if (!redOn && !greenOn)
        {
            Report("Включите лазер: Z — красный (точка), X — зелёный (выбор)", Palette.Warn);
            return;
        }

        if (redOn)
        {
            // Красный: пересчёт под новую точку (траектории НЕ исчезают от движения мыши).
            if (!aimHit) { Report("Наведите красный лазер на поверхность", Palette.Warn); return; }
            LockPoint(aimPoint);
            return;
        }

        // Зелёный: выбор траектории по «колбаске».
        if (state.hoveredTrajectory < 0)
        {
            Report("Наведите зелёный лазер на траекторию", Palette.Warn);
            return;
        }
        SelectTrajectory(state.hoveredTrajectory);
    }

    private void Stage3_Confirm(bool confirmRed, bool confirmGreen, bool redOn, bool greenOn)
    {
        if (redOn && greenOn)
        {
            Report("Выберите один лазер: зелёный (X) — выбор фантома", Palette.Warn);
            return;
        }
        if (greenOn || (!redOn && !greenOn))
        {
            if (!greenOn) { Report("Включите зелёный лазер (X)", Palette.Warn); return; }
            if (state.hoveredPhantom < 0)
            {
                Report("Наведите зелёный лазер на фантом", Palette.Warn);
                return;
            }
            if (!phantoms.AllArrived)
            {
                Report("Фантомы ещё едут…", Palette.Warn);
                return;
            }
            SelectPhantom(state.hoveredPhantom);
            return;
        }
        Report("Наведите зелёный лазер на фантом (красный пока не нужен)", Palette.Warn);
    }

    // ------------------------------------------------------------------ планирование

    private void LockPoint(Vector3 point)
    {
        state.point = point;
        state.aimAtLock = point;
        state.hasPoint = true;
        state.ResetTrajectorySelection();
        phantoms.Hide();
        HideTrajectories();

        planningStart = validator.CopyCurrent();
        planningTarget = point;
        planningWant = Mathf.Clamp(candidateCount, 3, 5);
        plannedSoFar.Clear();
        planQueue.Clear();
        for (int k = 0; k < 6; k++) planQueue.Enqueue(k);
        state.phase = FlowState.PointSelected;
        planStartTime = Time.realtimeSinceStartup;
        Report("Точка принята · считаю варианты траекторий…", Palette.Info);
    }

    private void ProcessPlanningSlices()
    {
        if (planQueue.Count == 0) return;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int done = 0;
        // За один срез — не больше candidatesPerSlice прогонов И не больше бюджета мс.
        while (planQueue.Count > 0 && done < Mathf.Max(1, candidatesPerSlice) &&
               sw.Elapsed.TotalMilliseconds < planningSliceMs)
        {
            int seedIndex = planQueue.Dequeue();
            done++;
            var list = planner.Plan(planningStart, planningTarget, 4, 1000 + seedIndex * 7919);
            if (list != null)
            {
                foreach (PlannedTrajectory t in list)
                {
                    bool dup = false;
                    foreach (PlannedTrajectory ex in plannedSoFar)
                        if (IsSamePath(ex, t)) { dup = true; break; }
                    if (!dup) plannedSoFar.Add(t);
                }
            }
            if (plannedSoFar.Count >= planningWant) planQueue.Clear();
        }
        sw.Stop();
        metrics.RecordPlan(sw.Elapsed.TotalMilliseconds, plannedSoFar.Count > 0,
            plannedSoFar.Count > 0 ? plannedSoFar[0] : null);

        if (done > 0) RebuildCandidateVisuals();

        if (planQueue.Count == 0)
        {
            if (state.candidates.Count == 0)
            {
                state.phase = FlowState.Idle;
                Report("Выберите другую точку — траектория не найдена", Palette.Bad);
            }
            else
            {
                state.phase = FlowState.TrajectoriesShown;
                Report("Вариантов: " + state.candidates.Count +
                       " · зелёным лучом (X) наведите на колбаску и нажмите ЛКМ", Palette.Info);
            }
        }
    }

    private static bool IsSamePath(PlannedTrajectory a, PlannedTrajectory b)
    {
        if (a == null || b == null) return false;
        if (System.Math.Abs(a.Length - b.Length) > 0.02) return false;
        if (System.Math.Abs(a.Time - b.Time) > 0.05) return false;
        double[] qa = a.GoalQ, qb = b.GoalQ;
        if (qa == null || qb == null || qa.Length != qb.Length) return false;
        double d = 0;
        for (int i = 0; i < qa.Length; i++) d += System.Math.Abs(qa[i] - qb[i]);
        return d < 3.0;   // почти одинаковый путь — считаем дубликатом
    }

    private void RebuildCandidateVisuals()
    {
        HideTrajectories();
        var plans = new List<PlannedTrajectory>(plannedSoFar);
        plans.Sort((a, b) => a.Score.CompareTo(b.Score));
        int want = Mathf.Clamp(candidateCount, 3, 5);
        if (plans.Count > want) plans.RemoveRange(want, plans.Count - want);

        int id = 0;
        Color[] palette =
        {
            new Color(1f, 0.45f, 0.03f), new Color(1f, 0.55f, 0.10f),
            new Color(1f, 0.38f, 0.02f), new Color(1f, 0.62f, 0.18f),
            new Color(1f, 0.50f, 0.05f)
        };

        foreach (PlannedTrajectory t in plans)
        {
            var cand = new TrajectoryCandidate
            {
                id = id,
                label = "Траектория " + (++id),
                plan = t,
                timeS = (float)t.Time,
                minClearance = t.MinClearance,
                limitMarginDeg = t.LimitMargin,
                score = t.Score,
                safe = t.MinClearance >= gate.minClearance && t.LimitMargin >= gate.minLimitMarginDeg
            };
            if (!cand.safe) cand.why = "запас ниже порога";

            int n = t.Path.Length;
            int stride = Mathf.Max(1, n / 70);
            var pts = new List<Vector3>();
            for (int i = 0; i < n; i += stride) pts.Add(validator.TcpAt(t.Path[i]));
            if (pts.Count < 2) continue;
            cand.tube = pts.ToArray();
            cand.lengthM = TubeMath.PolylineLength(cand.tube);
            if (slowMotionEnabled) MotionTiming.RescaleToSpeed(t, cand.lengthM, motionSpeed);

            GameObject go = new GameObject(cand.label);
            go.transform.SetParent(transform, false);
            cand.view = go.AddComponent<TrajectoryTube>();
            cand.view.radius = tubeRadius;
            cand.view.Build(cand.tube, palette[(id - 1) % palette.Length]);
            state.candidates.Add(cand);
        }
        _ = planStartTime;
    }

    // ------------------------------------------------------------------ наведение и выбор

    private void UpdateTrajectoryHover(bool greenOn)
    {
        int best = -1;
        float bestDist = tubeRadius;
        for (int i = 0; i < state.candidates.Count; i++)
        {
            TrajectoryCandidate c = state.candidates[i];
            if (c.view == null) continue;
            float d = c.view.DistanceToRay(lasers.GreenRay);
            if (d < bestDist) { bestDist = d; best = i; }
        }
        state.hoveredTrajectory = best;
        for (int i = 0; i < state.candidates.Count; i++)
            if (state.candidates[i].view != null)
                state.candidates[i].view.SetHighlight(i == best && best >= 0,
                    i == state.selectedTrajectory && state.selectedTrajectory >= 0);

        if (best >= 0 && greenOn) ShowTrajectoryMetrics(state.candidates[best]);
    }

    private void ShowTrajectoryMetrics(TrajectoryCandidate c)
    {
        string txt = string.Format("{0}: длина {1:F2} юнита · время {2:F0} с · зазор {3:F0} мм · запас лимитов {4:F1}°",
            c.label, c.lengthM, c.timeS, c.minClearance * 1000f, c.limitMarginDeg);
        KompasUI.KompasUIManager.SetPlanStatus(txt, Palette.Info);
        KompasUI.KompasUIManager.SetTooltip(txt, c.tube.Length > 0 ? c.tube[c.tube.Length / 2] : state.point);
    }

    private void ShowPhantomMetrics(int index)
    {
        var list = phantoms.Configs;
        if (index < 0 || index >= list.Count) return;
        string txt = list[index].Label + " · ветвь " + list[index].tag +
                     " · ошибка FK " + (list[index].fkError * 1000f).ToString("0.0") + " мм";
        KompasUI.KompasUIManager.SetPlanStatus(txt, Palette.Info);
        KompasUI.KompasUIManager.SetTooltip(txt,
            list[index].ghost != null ? list[index].ghost.transform.position + Vector3.up * 1.1f : state.point);
    }

    private void SelectTrajectory(int index)
    {
        if (index < 0 || index >= state.candidates.Count) return;
        state.selectedTrajectory = index;
        state.candidates[index].view?.SetHighlight(true, true);
        BuildPhantoms(state.candidates[index]);
        state.phase = FlowState.PhantomsMoving;
        Report("Траектория выбрана · показываю " + phantoms.Count +
               " вариантов позы — наведите зелёный луч на фантом и нажмите ЛКМ", Palette.Info);
    }

    private void BuildPhantoms(TrajectoryCandidate cand)
    {
        phantoms.Init(robot, validator);
        double[] startQ = validator.CopyCurrent();
        double[] goalQ = cand.plan.GoalQ ?? startQ;

        int want = validator.Dof == 3 ? Mathf.Clamp(phantomCount, 2, 4)     // SCARA: 2–4
                                      : Mathf.Clamp(phantomCount, 2, 8);    // 6-осевой: до 8
        var solutions = ik.SolveAllSeeded(state.point, goalQ, want);
        var ranges = new double[validator.Dof];
        for (int i = 0; i < validator.Dof; i++) ranges[i] = validator.Upper[i] - validator.Lower[i];
        solutions.Sort((a, b) => posture.Cost(a.q, startQ).CompareTo(posture.Cost(b.q, startQ)));
        List<int> picked = PhantomMath.PickDistinct(solutions, ranges, want);
        phantoms.Show(solutions, picked, startQ, phantomSpeed);

        state.phantoms.Clear();
        foreach (PhantomConfig pc in phantoms.Configs) state.phantoms.Add(pc);
        Debug.Log("[Flow] Фантомов: " + phantoms.Count + " (ветвей найдено " + solutions.Count + ")");
    }

    private void SelectPhantom(int index)
    {
        if (index < 0 || index >= state.phantoms.Count) return;
        if (phantomSelectionLatch) return;
        phantomSelectionLatch = true;
        state.selectedPhantom = index;
        PhantomConfig cfg = state.phantoms[index];

        if (executor == null)
        {
            executor = robot.GetComponent<TrajectoryExecutor>();
            if (executor == null) executor = robot.gameObject.AddComponent<TrajectoryExecutor>();
        }
        executor.Init(validator, world, gate);
        executor.timeScale = 1f;
        motion.Bind(executor, validator, world, gate);

        PlannedTrajectory plan = state.candidates[state.selectedTrajectory].plan;
        plan.Path[plan.Path.Length - 1] = (double[])cfg.q.Clone();  // финальная поза = выбранный фантом

        if (motion.Play(plan, cfg.q))
        {
            state.phase = FlowState.RobotMoving;
            robot.enabled = false;
            Report("Движение: " + cfg.Label + " · скорость 1 юнит за " +
                   Mathf.RoundToInt(1f / Mathf.Max(0.001f, motionSpeed)) + " с", Palette.Ok);
        }
        else
        {
            phantomSelectionLatch = false;
            Report("Safety отклонила движение: " + SafetyGate.Describe(gate.LastReason), Palette.Bad);
        }
    }

    private void FinishMotion()
    {
        if (state.phase != FlowState.RobotMoving) return;
        if (robot != null) robot.enabled = true;
        phantoms.Hide();
        HideTrajectories();
        state.ClearAll();
        phantomSelectionLatch = false;
        Report("Готово. Задайте новую точку красным лазером", Palette.Ok);
    }

    // ------------------------------------------------------------------ служебное

    private ReachResult oracleQuery(Vector3 point)
    {
        return oracle.Ready ? oracle.Query(point)
                            : new ReachResult { verdict = ReachVerdict.Safe, reason = "оракул не готов" };
    }

    private readonly ReachabilityOracle oracle = new ReachabilityOracle();

    private void ResetAll(string why)
    {
        phantoms.Hide();
        HideTrajectories();
        planQueue.Clear();
        plannedSoFar.Clear();
        executor?.Stop(SafetyReason.OperatorStop);
        if (robot != null) robot.enabled = true;
        state.ClearAll();
        phantomSelectionLatch = false;
        Report(why, Color.gray);
    }

    private void HideTrajectories()
    {
        foreach (TrajectoryCandidate c in state.candidates)
            if (c.view != null) Object.Destroy(c.view.gameObject);
        state.candidates.Clear();
    }

    private void Rebind()
    {
        if (robot == null) return;
        validator.Init(robot);
        validator.linkRadius = 0.06f;
        ik.Init(validator);
        posture.Init(validator);
        world.Rebuild(robot, 0.06f);
        planner.Init(validator, world);
        planner.clearance = 0.02f;
        planner.maxIterations = 700;
        oracle.Init(robot, world);
        gate.NotifyState();
        phantoms.Init(robot, validator);
        robot.ClearTarget();
    }

    private static RobotController FindSelectedRobot()
    {
        RobotController[] robots = Object.FindObjectsByType<RobotController>(FindObjectsInactive.Exclude);
        foreach (RobotController rc in robots)
            if (rc != null && rc.isActive) return rc;
        return null;
    }

    private static void Report(string text, Color color)
    {
        KompasUI.KompasUIManager.SetPlanStatus(text, color);
        Debug.Log("[Flow] " + text);
    }

    /// <summary>Палитра подсказок и шарика (ядовитые неоновые цвета).</summary>
    public static class Palette
    {
        public static readonly Color Ok = new Color(0.55f, 1f, 0.05f);      // кислотно-зелёный
        public static readonly Color Info = new Color(0.35f, 1f, 0.75f);    // неоновый циан
        public static readonly Color Warn = new Color(1f, 0.72f, 0f);       // ядовито-оранжевый
        public static readonly Color Bad = new Color(1f, 0.05f, 0.45f);     // неоновый маджента-красный
    }
}
