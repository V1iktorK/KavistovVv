using System.Collections.Generic;
using TrajectoryCore;
using UnityEngine;

/// <summary>
/// Поток выбора «два лазера» (MVP-1…MVP-4):
///   красный ЛКМ  — зафиксировать точку (пока не зафиксирована, ничего не считается);
///   красный луч  — перемещение точки (смена точки отменяет выбор траектории);
///   зелёный ПКМ  — выбрать «колбаску» траектории, затем — фантом;
///   Esc          — сброс всего.
/// Реализует детерминированную логику: генерация кандидатов планировщиком,
/// скоринг, отбор ветвей IK «не жадно» (farthest-point + стоимость позы),
/// инициализация движения со скоростью м/с.
/// </summary>
public class TrajectoryFlowController : MonoBehaviour
{
    [Header("Выбор точки (красный лазер)")]
    public float pointMoveThreshold = 0.02f;   // сдвиг прицела, отменяющий выбор, м
    public float tubeRadius = 0.05f;           // радиус «колбаски» (1/20 юнита)
    public int candidateCount = 5;             // сколько траекторий генерируем

    [Header("Фантомы (зелёный лазер)")]
    public int phantomCount = 5;

    [Header("Движение")]
    public float motionSpeed = 0.05f;          // м/с по TCP: 0.05 = 1 м за 20 секунд
    public bool slowMotionEnabled = true;

    [Header("Производительность")]
    public float planningSliceMs = 12f;        // бюджет планирования на кадр (тайм-слайсы)
    public int candidatesPerSlice = 1;         // сколько кандидатов считаем за кадр

    [Header("Стенды (два стола + роботы)")]
    public bool ensureStandsOnStart = true;    // создать стенды, если их нет в сцене

    private readonly System.Collections.Generic.Queue<int> planQueue =
        new System.Collections.Generic.Queue<int>();
    private double[] planningStart;
    private Vector3 planningTarget;
    private int planningWant;
    private readonly List<PlannedTrajectory> plannedSoFar = new List<PlannedTrajectory>();

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

    public SelectionState State => state;

    private void Awake()
    {
        lasers = gameObject.AddComponent<LaserManager>();
        phantoms = gameObject.AddComponent<PhantomManager>();
        motion = gameObject.AddComponent<MotionExecutor>();
    }

    /// <summary>Вызывается контроллером камеры каждый кадр (обновление прицела).</summary>
    public void UpdateAim(Vector3 aimPoint, bool aimHit, bool redConfirm, bool greenConfirm, bool cancel)
    {
        lasers.UpdateRays(aimPoint, aimHit);
        if (!started)
        {
            if (ensureStandsOnStart) StandBuilder.EnsureStands(0.98f);
            Rebind();
            started = true;
        }

        // Привязка к выбранному роботу: пока робот не выбран (F), ничего не делаем.
        RobotController active = FindSelectedRobot();
        if (active != robot)
        {
            robot = active;
            Rebind();
        }
        if (robot == null)
        {
            if (redConfirm || greenConfirm)
                Report("Сначала выберите робота: наведите шарик лазера и нажмите F", new Color(1f, 0.85f, 0.3f));
            return;
        }

        // Планируем порциями по кадрам (тайм-слайсы) — без пиков латентности.
        ProcessPlanningQueue();

        // Мир столкновений обновляем стробоскопически.
        worldTimer -= Time.deltaTime;
        if (worldTimer <= 0f)
        {
            world.Rebuild(robot, 0.06f);
            worldTimer = 0.5f;
        }
        gate.NotifyState();

        if (cancel) { ResetAll("сброшено оператором (Esc)"); return; }

        // Красный луч двигает точку: если она уже была зафиксирована — отменяем выбор.
        if (state.hasPoint && (aimPoint - state.aimAtLock).magnitude > pointMoveThreshold)
        {
            state.ResetTrajectorySelection();
            HideTrajectories();
            phantoms.Hide();
            state.hasPoint = false;
            state.phase = FlowPhase.Idle;
            robot.ClearTarget();   // робот НЕ должен никуда тянуться без подтверждения
            Report("Точка смещена — подтвердите новую (ЛКМ / триггер)", new Color(1f, 0.85f, 0.3f));
        }

        switch (state.phase)
        {
            case FlowPhase.Idle:
                robot.ClearTarget();                 // ключевая гарантия: без точки — стоим
                if (executor != null && executor.IsRunning) executor.Stop(SafetyReason.OperatorStop);
                if (redConfirm && aimHit) LockPoint(aimPoint);
                break;

            case FlowPhase.PointLocked:
            case FlowPhase.TrajectoryHover:
                robot.ClearTarget();
                if (redConfirm && aimHit) LockPoint(aimPoint);
                else UpdateTrajectoryHover();
                if (greenConfirm && state.hoveredTrajectory >= 0) SelectTrajectory(state.hoveredTrajectory);
                break;

            case FlowPhase.TrajectorySelected:
            case FlowPhase.PhantomHover:
                robot.ClearTarget();
                UpdatePhantomHover();
                if (greenConfirm && state.hoveredPhantom >= 0) SelectPhantom(state.hoveredPhantom);
                break;

            case FlowPhase.PhantomSelected:
            case FlowPhase.Executing:
                if (!executor.IsRunning) state.phase = FlowPhase.PhantomSelected;
                break;
        }
    }

    /// <summary>Пошаговое планирование: один прогон планировщика за кадр (тайм-слайс).</summary>
    private void ProcessPlanningQueue()
    {
        if (planQueue.Count == 0) return;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int done = 0;
        while (planQueue.Count > 0 && done < Mathf.Max(1, candidatesPerSlice) &&
               sw.Elapsed.TotalMilliseconds < planningSliceMs)
        {
            int seedIndex = planQueue.Dequeue();
            done++;
            List<PlannedTrajectory> c = planner.Plan(planningStart, planningTarget, 2, 1000 + seedIndex * 7919);
            if (c != null)
            {
                foreach (PlannedTrajectory t in c)
                {
                    bool dup = false;
                    foreach (PlannedTrajectory ex in plannedSoFar)
                        if (System.Math.Abs(ex.Time - t.Time) < 1e-3 &&
                            System.Math.Abs(ex.Length - t.Length) < 1e-3) { dup = true; break; }
                    if (!dup) plannedSoFar.Add(t);
                }
            }
            if (plannedSoFar.Count >= planningWant) planQueue.Clear();
        }
        sw.Stop();
        metrics.RecordPlan(sw.Elapsed.TotalMilliseconds, plannedSoFar.Count > 0,
            plannedSoFar.Count > 0 ? plannedSoFar[0] : null);

        // Обновляем визуал по мере готовности (пользователь видит, как появляются варианты).
        if (done > 0) RebuildCandidateVisuals();
        if (planQueue.Count == 0 && plannedSoFar.Count > 0)
            Report("Кандидатов: " + state.candidates.Count + " · наведите ЗЕЛЁНЫЙ луч на колбаску",
                new Color(1f, 0.75f, 0.35f));
    }

    // ------------------------------------------------------------------ шаги сценария

    private void LockPoint(Vector3 point)
    {
        state.point = point;
        state.aimAtLock = point;
        state.hasPoint = true;
        state.ResetTrajectorySelection();
        phantoms.Hide();
        HideTrajectories();

        // Планирование — ПОРЦИЯМИ по кадрам: ставим очередь сидов.
        planningStart = validator.CopyCurrent();
        planningTarget = point;
        planningWant = Mathf.Clamp(candidateCount, 1, 8);
        plannedSoFar.Clear();
        planQueue.Clear();
        for (int k = 0; k < planningWant; k++) planQueue.Enqueue(k);
        state.phase = FlowPhase.PointLocked;
        Report("Точка подтверждена · считаю траектории…", new Color(1f, 0.8f, 0.4f));
    }

    /// <summary>Перестроить «колбаски» по уже готовым кандидатам (вызывается по мере планирования).</summary>
    private void RebuildCandidateVisuals()
    {
        HideTrajectories();
        var plans = new List<PlannedTrajectory>(plannedSoFar);
        plans.Sort((a, b) => a.Score.CompareTo(b.Score));
        int want = Mathf.Clamp(candidateCount, 1, 8);
        if (plans.Count > want) plans.RemoveRange(want, plans.Count - want);

        int id = 0;
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
            int stride = Mathf.Max(1, n / 60);
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
            cand.view.Build(cand.tube, new Color(1f, 0.45f, 0.03f, 0.9f)); // ярко-оранжевые
            state.candidates.Add(cand);
        }
    }

    private void GenerateCandidates(Vector3 point)
    {
        double[] start = validator.CopyCurrent();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Несколько прогонов планировщика с разными seed → разные гомотопии пути.
        var plans = new List<PlannedTrajectory>();
        int want = Mathf.Clamp(candidateCount, 1, 8);
        for (int k = 0; k < want; k++)
        {
            List<PlannedTrajectory> c = planner.Plan(start, point, 2, 1000 + k * 7919);
            if (c == null) continue;
            foreach (PlannedTrajectory t in c)
            {
                bool dup = false;
                foreach (PlannedTrajectory ex in plans)
                    if (System.Math.Abs(ex.Time - t.Time) < 1e-3 &&
                        System.Math.Abs(ex.Length - t.Length) < 1e-3) { dup = true; break; }
                if (!dup) plans.Add(t);
            }
            if (plans.Count >= want) break;
        }
        sw.Stop();
        metrics.RecordPlan(sw.Elapsed.TotalMilliseconds, plans.Count > 0, plans.Count > 0 ? plans[0] : null);

        // Скоринг (меньше — лучше), сортировка и обрезка.
        plans.Sort((a, b) => a.Score.CompareTo(b.Score));
        if (plans.Count > want) plans.RemoveRange(want, plans.Count - want);

        int id = 0;
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

            // Точки «колбаски»: TCP по сэмплам (прореживание до ~60 точек).
            int n = t.Path.Length;
            int stride = Mathf.Max(1, n / 60);
            var pts = new List<Vector3>();
            for (int i = 0; i < n; i += stride) pts.Add(validator.TcpAt(t.Path[i]));
            if (pts.Count < 2) continue;
            cand.tube = pts.ToArray();
            cand.lengthM = TubeMath.PolylineLength(cand.tube);

            // Тормозим движение: 1 м за 20 с (по TCP), настраивается.
            if (slowMotionEnabled) MotionTiming.RescaleToSpeed(t, cand.lengthM, motionSpeed);

            GameObject go = new GameObject(cand.label);
            go.transform.SetParent(transform, false);
            cand.view = go.AddComponent<TrajectoryTube>();
            cand.view.radius = tubeRadius;
            cand.view.Build(cand.tube, new Color(1f, 0.45f, 0.03f, 0.9f)); // ярко-оранжевые линии
            state.candidates.Add(cand);
        }
    }

    private void UpdateTrajectoryHover()
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
                state.candidates[i].view.SetHighlight(i == best && best >= 0, i == state.selectedTrajectory && state.selectedTrajectory >= 0);

        if (best >= 0)
        {
            TrajectoryCandidate c = state.candidates[best];
            state.phase = FlowPhase.TrajectoryHover;
            ShowMetrics(c, "наведение");
        }
        else if (state.phase != FlowPhase.PointLocked)
        {
            state.phase = FlowPhase.PointLocked;
        }
    }

    private void ShowMetrics(TrajectoryCandidate c, string mode)
    {
        string txt = string.Format("{0} ({1}): длина {2:F2} м · время {3:F1} с · зазор {4:F0} мм · запас лимитов {5:F1}°",
            c.label, mode, c.lengthM, c.timeS, c.minClearance * 1000f, c.limitMarginDeg);
        if (!c.safe) txt += " · " + c.why;
        KompasUI.KompasUIManager.SetPlanStatus(txt, c.safe ? new Color(0.5f, 0.95f, 1f) : new Color(1f, 0.6f, 0.4f));
        KompasUI.KompasUIManager.SetTooltip(txt, c.tube.Length > 0 ? c.tube[c.tube.Length / 2] : state.point);
    }

    private void SelectTrajectory(int index)
    {
        if (index < 0 || index >= state.candidates.Count) return;
        state.selectedTrajectory = index;
        state.phase = FlowPhase.TrajectorySelected;
        state.candidates[index].view?.SetHighlight(true, true);
        BuildPhantoms(state.candidates[index]);
        Report("Траектория выбрана · выберите ФАНТОМ зелёным лучом (ПКМ) — " +
               phantoms.Count + " вариантов", new Color(0.4f, 1f, 0.55f));
    }

    private void BuildPhantoms(TrajectoryCandidate cand)
    {
        phantoms.Init(robot, validator);
        double[] goalQ = cand.plan.GoalQ;
        Vector3 goal = state.point;

        // Все ветви IK для цели (аналитика + мультисид) + «не жадный» отбор разных конфигураций.
        var solutions = ik.SolveAllSeeded(goal, goalQ ?? validator.CopyCurrent(), Mathf.Max(4, phantomCount));
        var ranges = new double[validator.Dof];
        for (int i = 0; i < validator.Dof; i++) ranges[i] = validator.Upper[i] - validator.Lower[i];

        // Ранжируем по стоимости позы (лимиты/home/сингулярность/непрерывность).
        solutions.Sort((a, b) => posture.Cost(a.q, goalQ).CompareTo(posture.Cost(b.q, goalQ)));
        List<int> picked = PhantomMath.PickDistinct(solutions, ranges, phantomCount);
        phantoms.Show(solutions, picked, goal, ranges);

        state.phantoms.Clear();
        foreach (PhantomConfig pc in phantoms.Configs) state.phantoms.Add(pc);
    }

    private void UpdatePhantomHover()
    {
        int hover = phantoms.HoverIndex(lasers.GreenRay);
        state.hoveredPhantom = hover;
        phantoms.SetHighlight(hover);
        if (hover >= 0)
        {
            state.phase = FlowPhase.PhantomHover;
            var list = phantoms.Configs;
            if (hover < list.Count)
            {
                string txt = list[hover].Label + " · ошибка FK " + (list[hover].fkError * 1000f).ToString("0.0") +
                             " мм · ветвь " + list[hover].tag;
                KompasUI.KompasUIManager.SetPlanStatus(txt, new Color(0.6f, 1f, 0.7f));
                KompasUI.KompasUIManager.SetTooltip(txt, list[hover].ghost != null ? list[hover].ghost.transform.position + Vector3.up * 1.2f : state.point);
            }
        }
        else if (state.phase != FlowPhase.TrajectorySelected)
        {
            state.phase = FlowPhase.TrajectorySelected;
        }
    }

    private void SelectPhantom(int index)
    {
        if (index < 0 || index >= state.phantoms.Count) return;
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

        // Ставим робота в выбранную конфигурацию (без рывка: через планировщик до этой же точки).
        TrajectoryCandidate cand = state.candidates[state.selectedTrajectory];
        PlannedTrajectory plan = cand.plan;

        if (motion.Play(plan, cfg.q))
        {
            state.phase = FlowPhase.Executing;
            // Контроллер IK не должен бороться с исполнителем.
            robot.enabled = false;
            Report("Движение: " + cfg.Label + " · скорость " + motionSpeed.ToString("0.000") +
                   " м/с (1 м за " + (1f / Mathf.Max(0.005f, motionSpeed)).ToString("0") + " с)", new Color(0.4f, 1f, 0.6f));
        }
        else
        {
            Report("Safety отклонила движение: " + SafetyGate.Describe(gate.LastReason), new Color(1f, 0.4f, 0.35f));
        }
    }

    private void ResetAll(string why)
    {
        phantoms.Hide();
        HideTrajectories();
        executor?.Stop(SafetyReason.OperatorStop);
        if (robot != null) robot.enabled = true;
        state.ClearAll();
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
        if (robot == null) return;   // нет выбранного робота — ждём F
        validator.Init(robot);
        validator.linkRadius = 0.06f;
        ik.Init(validator);
        posture.Init(validator);
        world.Rebuild(robot, 0.06f);
        planner.Init(validator, world);
        planner.clearance = 0.02f;
        planner.maxIterations = 700;
        gate.NotifyState();
        phantoms.Init(robot, validator);
        robot.ClearTarget();   // движение начнётся только после выбора фантома
    }

    /// <summary>Активный (выбранный по F) робот; если выбор обнулён — null.</summary>
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

    private static RobotController FindActiveRobot()
    {
        RobotController[] robots = Object.FindObjectsByType<RobotController>(FindObjectsInactive.Exclude);
        foreach (RobotController rc in robots)
            if (rc != null && rc.isActive) return rc;
        return robots.Length > 0 ? robots[0] : null;
    }
}
