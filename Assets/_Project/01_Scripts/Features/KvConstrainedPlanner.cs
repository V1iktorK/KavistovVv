using System;
using System.Collections.Generic;
using UnityEngine;
using KazistovVvUI;
using TrajectoryCore;

namespace KazistovVvFeatures
{
    /// <summary>
    /// ЭТАП 8 ТЗ: ПЛАНИРОВАНИЕ С ОГРАНИЧЕНИЯМИ (CONSTRAINED PLANNING).
    ///
    /// Задачи из ТЗ: «двигайся, держа инструмент вертикально», «не наклоняйся больше 15°»,
    /// «TCP всегда смотрит на объект X». Реализовано через IK С ДОПОЛНИТЕЛЬНЫМИ УСЛОВИЯМИ
    /// (<see cref="KvToolKinematics"/>), без переписывания ядра:
    ///
    ///   1. ПРОВЕРКА: каждая построенная траектория проверяется по ФАКТИЧЕСКОЙ оси инструмента
    ///      (ось последнего сустава) на КАЖДОМ сэмпле — сколько сэмплов нарушают ограничение
    ///      и каков худший угол. Нарушающие варианты помечаются в дереве и в свойствах,
    ///      а при включённом «отбрасывать» — считаются непригодными (и платформа сама
    ///      предлагает ближайший подходящий вариант).
    ///   2. ПРИВЕДЕНИЕ (проекция): «Привести траекторию к ограничению» пересчитывает позы
    ///      нарушающих сэмплов решателем IK с дополнительным условием, сохраняя цель TCP,
    ///      лимиты суставов и зазоры. Путь остаётся тем же по смыслу, но теперь инструмент
    ///      держится как требуется.
    ///
    /// Включается и выключается в настройках (ТЗ), настройки переживают перезапуск.
    /// Работает и на роботе, и на SCARA (у SCARA ось инструмента задана конструкцией,
    /// поэтому ограничение либо выполнено всегда, либо честно помечается невыполнимым).
    /// </summary>
    public class KvConstrainedPlanner
    {
        public const string EnabledPrefsKey = "KazistovVv.Constr.Enabled";
        public const string ModePrefsKey = "KazistovVv.Constr.Mode";
        public const string TiltPrefsKey = "KazistovVv.Constr.Tilt";
        public const string TolPrefsKey = "KazistovVv.Constr.Tolerance";
        public const string FilterPrefsKey = "KazistovVv.Constr.Filter";

        public event Action<string> Message;

        private TrajectoryFlowController flow;
        private CollisionWorld world;
        private KvMotionLimits limits = new KvMotionLimits();
        private bool loaded;

        private bool enabled;
        private readonly KvOrientConstraint profile = new KvOrientConstraint();
        private bool discardViolating;

        private readonly Dictionary<TrajectoryCandidate, KvToolKinematics.PlanCheck> checks =
            new Dictionary<TrajectoryCandidate, KvToolKinematics.PlanCheck>();
        private string signature = "";
        private int evaluated;
        private int violating;

        // Пороги проекции и перепланирования (ФИКС 2): те же, что были зашиты в вызове Solve.
        private const float minClearance = 0.005f;        // мин. зазор до препятствия, м
        private const float minSelfClearance = 0.004f;    // мин. зазор между своими звеньями, м
        private const float minLimitMargin = 2.5f;        // мин. запас до лимитов суставов, °
        /// <summary>Последний результат проекции (для интерфейса и журнала).</summary>
        public int LastProjected { get; private set; }
        /// <summary>Худший остаток после проекции, °.</summary>
        public float LastResidual { get; private set; }
        /// <summary>Какой вариант проектировался последним.</summary>
        public int LastProjectedIndex { get; private set; } = -1;

        // ------------------------------------------------------------------ настройки

        /// <summary>Ограничение включено (тумблер в настройках/вкладке).</summary>
        public bool Enabled
        {
            get { Load(); return enabled; }
            set
            {
                Load();
                enabled = value;
                PlayerPrefs.SetInt(EnabledPrefsKey, enabled ? 1 : 0);
                PlayerPrefs.Save();
                if (enabled) Invalidate();
            }
        }

        /// <summary>Активное ограничение (тип, направление, допуск, цель).</summary>
        public KvOrientConstraint Profile { get { Load(); return profile; } }

        /// <summary>Отбрасывать варианты, нарушающие ограничение.</summary>
        public bool DiscardViolating
        {
            get { Load(); return discardViolating; }
            set
            {
                Load();
                discardViolating = value;
                PlayerPrefs.SetInt(FilterPrefsKey, discardViolating ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public int EvaluatedCount { get { return evaluated; } }
        public int ViolatingCount { get { return violating; } }
        public TrajectoryFlowController Flow() { return flow; }

        /// <summary>Ограничение, действующее прямо сейчас (null — выключено).</summary>
        public KvOrientConstraint Active
        {
            get
            {
                Load();
                if (!enabled || profile.mode == KvOrientMode.None) return null;
                return profile;
            }
        }

        private void Load()
        {
            if (loaded) return;
            loaded = true;
            enabled = PlayerPrefs.GetInt(EnabledPrefsKey, 0) != 0;
            profile.mode = (KvOrientMode)Mathf.Clamp(PlayerPrefs.GetInt(ModePrefsKey, 2), 0, 4);
            profile.tiltLimitDeg = PlayerPrefs.GetFloat(TiltPrefsKey, 15f);
            profile.toleranceDeg = PlayerPrefs.GetFloat(TolPrefsKey, 6f);
            profile.reference = Vector3.down;
            profile.direction = Vector3.down;
            discardViolating = PlayerPrefs.GetInt(FilterPrefsKey, 0) != 0;
        }

        public void Save()
        {
            Load();
            PlayerPrefs.SetInt(ModePrefsKey, (int)profile.mode);
            PlayerPrefs.SetFloat(TiltPrefsKey, profile.tiltLimitDeg);
            PlayerPrefs.SetFloat(TolPrefsKey, profile.toleranceDeg);
            PlayerPrefs.Save();
        }

        public void Bind(TrajectoryFlowController controller, CollisionWorld collisionWorld,
            KvMotionLimits motionLimits)
        {
            flow = controller;
            world = collisionWorld;
            if (motionLimits != null) limits = motionLimits;
            Load();
        }

        public void Invalidate()
        {
            checks.Clear();
            signature = "";
            evaluated = 0;
            violating = 0;
        }

        // ------------------------------------------------------------------ проверка вариантов

        /// <summary>Проверка ограничения для варианта (кэшируется по подписи сцены).</summary>
        public KvToolKinematics.PlanCheck Check(TrajectoryCandidate candidate)
        {
            KvToolKinematics.PlanCheck result = new KvToolKinematics.PlanCheck();
            if (candidate == null || candidate.plan == null) return result;
            if (flow == null || flow.Validator == null || !flow.Validator.Ready) return result;

            KvOrientConstraint c = Active;
            if (c == null) return result;

            checks.TryGetValue(candidate, out result);
            if (result.samples > 0 && signature == CurrentSignature()) return result;

            result = KvToolKinematics.AnalyzePlan(flow.Validator, candidate.plan, c);
            checks[candidate] = result;
            return result;
        }

        private string CurrentSignature()
        {
            if (flow == null || flow.State == null) return "";
            return flow.State.candidates.Count + "|" + profile.mode + "|" +
                   profile.tiltLimitDeg.ToString("0.0") + "|" + profile.toleranceDeg.ToString("0.0") +
                   "|" + profile.direction.x.ToString("0.00") + profile.direction.y.ToString("0.00") +
                   profile.direction.z.ToString("0.00") + "|" + profile.targetPoint.x.ToString("0.000") +
                   profile.targetPoint.z.ToString("0.000") + "|" + enabled;
        }

        /// <summary>Вариант нарушает ограничение (для дерева, свойств и фильтра).</summary>
        public bool Violates(TrajectoryCandidate candidate)
        {
            if (Active == null) return false;
            KvToolKinematics.PlanCheck check = Check(candidate);
            return check.samples > 0 && check.violations > 0;
        }

        /// <summary>Подпись состояния для дерева/свойств ("" — ограничение выключено).</summary>
        public string StatusLine()
        {
            KvOrientConstraint c = Active;
            if (c == null) return KvLocExtra2.T("constr.mode.none", "нет");
            if (evaluated == 0) return c.Describe();
            return c.Describe() + " · " + KvLocExtra2.F("constr.violated",
                       "нарушений: {0} из {1} сэмплов · худший угол {2}°",
                       violating, evaluated, "—");
        }

        /// <summary>
        /// Кадровое обслуживание: при появлении новых вариантов они проверяются один раз,
        /// в журнал идёт понятная строка. Ничего не пересчитывается каждый кадр.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (flow == null || flow.State == null) return;
            if (Active == null) return;
            if (flow.State.candidates.Count == 0)
            {
                if (evaluated != 0) Invalidate();
                return;
            }

            string current = CurrentSignature();
            if (current == signature) return;
            signature = current;

            evaluated = 0;
            violating = 0;
            int bestCompliant = -1;
            for (int i = 0; i < flow.State.candidates.Count; i++)
            {
                TrajectoryCandidate candidate = flow.State.candidates[i];
                if (candidate == null || candidate.plan == null) continue;
                KvToolKinematics.PlanCheck check =
                    KvToolKinematics.AnalyzePlan(flow.Validator, candidate.plan, Active);
                checks[candidate] = check;
                evaluated += check.samples;
                violating += check.violations;
                if (check.violations == 0 && bestCompliant < 0) bestCompliant = i;
            }

            string message = KvLocExtra2.T("constr.status", "Состояние ограничения") + ": " +
                             Active.Describe() + " · " + (violating == 0
                                 ? KvLocExtra2.T("constr.ok",
                                     "выбранная траектория удовлетворяет ограничению")
                                 : KvLocExtra2.F("constr.violated",
                                     "нарушений: {0} из {1} сэмплов · худший угол {2}°",
                                     violating, evaluated,
                                     WorstAngle().ToString("0.0")));
            Report(message);

            if (discardViolating && violating > 0)
            {
                int selected = flow.State.selectedTrajectory;
                bool selectedViolates = selected >= 0 &&
                                        Violates(flow.State.candidates[selected]);
                if (selectedViolates && bestCompliant >= 0)
                {
                    flow.SelectCandidateByIndex(bestCompliant);
                    Report("ограничение: выбранный вариант нарушает ограничение — " +
                           "переключился на вариант №" + (bestCompliant + 1) +
                           " («отбрасывать нарушающие» включено)");
                }
                else if (bestCompliant < 0)
                {
                    Report("ограничение: ни один из " + flow.State.candidates.Count +
                           " вариантов не соблюдает ограничение — включите «Привести к ограничению» " +
                           "или ослабьте допуск");
                }
            }
        }

        private float WorstAngle()
        {
            float worst = 0f;
            foreach (KeyValuePair<TrajectoryCandidate, KvToolKinematics.PlanCheck> kv in checks)
                if (kv.Value.worstAngle > worst) worst = kv.Value.worstAngle;
            return worst;
        }

        // ------------------------------------------------------------------ приведение к ограничению

        /// <summary>
        /// ПРИВЕСТИ ТРАЕКТОРИЮ К ОГРАНИЧЕНИЮ (проекция): нарушающие сэмплы пересчитываются
        /// решателем IK с дополнительным условием, лимиты и зазоры проверяются на каждом шаге.
        ///
        /// ФИКС 2 (сходимость): для КАЖДОГО нарушающего сэмпла пробуются РАЗНЫЕ стартовые
        /// приближения — исправленный сосед, сам сэмпл, зеркальное запястье (лечит ориентацию
        /// «на 180°»), нулевая поза, середина лимитов и ветви планировщика. Если ни одна проба
        /// не сошлась, сэмпл честно помечается НЕПРИВЕДЁННЫМ: число неприведённых сэмплов и
        /// худший остаток показываются в журнале и в свойствах, а оператору предлагается
        /// «Перепланировать с ограничением».
        ///
        /// Возвращает число исправленных сэмплов; причина отказа — в журнале.
        /// </summary>
        public int Project(TrajectoryCandidate candidate, int index)
        {
            LastProjected = 0;
            LastResidual = 0f;
            LastProjectedIndex = index;
            LastFailed = 0;
            LastAttempts = 0;

            if (candidate == null || candidate.plan == null)
            {
                Report(KvLocExtra2.T("export.noplan",
                    "Нет траектории: выберите точку (Z + ЛКМ) и подтвердите вариант (X + ЛКМ)"));
                return 0;
            }
            KvOrientConstraint c = Active;
            if (c == null)
            {
                Report("ограничение выключено — включать проекцию нечего");
                return 0;
            }
            if (flow == null || flow.Validator == null || !flow.Validator.Ready)
            {
                Report("проекция невозможна: валидатор робота не готов");
                return 0;
            }

            PoseValidator v = flow.Validator;
            Planner planner = flow.Planner;
            PlannedTrajectory plan = KvTrajMath.Clone(candidate.plan, candidate.plan.Label);
            int fixedSamples = 0;
            int failed = 0;
            float worstLeft = 0f;
            double[] prev = null;

            for (int i = 0; i < plan.Path.Length; i++)
            {
                double[] q = plan.Path[i];
                float err = KvToolKinematics.Error(v, q, c);
                if (err <= Mathf.Max(0.1f, c.toleranceDeg))
                {
                    prev = q;
                    continue;
                }

                // --- перебор ветвей (ФИКС 2): сосед → сам сэмпл → зеркальное запястье → …
                List<double[]> seeds = KvToolKinematics.Seeds(v, planner, v.TcpAt(q), c, q, prev);
                double[] repaired = null;
                for (int attempt = 0; attempt < seeds.Count; attempt++)
                {
                    LastAttempts++;
                    double[] trial;
                    float posErr, angErr;
                    string why;
                    // Доворот сустава ограничиваем только для пробы «от соседа» (ради
                    // непрерывности пути); у остальных ветвей доворот может быть большим —
                    // в этом и смысл перезапуска из другой ветви.
                    float maxTurn = attempt == 0 ? 150f : 0f;
                    if (KvToolKinematics.Solve(v, world, v.TcpAt(q), c, seeds[attempt],
                            minClearance, minSelfClearance, minLimitMargin, 0.004f, maxTurn,
                            out trial, out posErr, out angErr, out why))
                    {
                        repaired = trial;
                        break;
                    }
                }

                if (repaired != null)
                {
                    plan.Path[i] = repaired;
                    fixedSamples++;
                    prev = repaired;
                }
                else
                {
                    failed++;
                    if (err > worstLeft) worstLeft = err;
                    prev = q;
                }
            }

            LastFailed = failed;
            if (fixedSamples == 0)
            {
                Report("привести к ограничению не удалось: НИ ОДИН из " + failed +
                       " нарушающих сэмплов не приведён (худший остаток " +
                       worstLeft.ToString("0.0") + "°, проб IK " + LastAttempts +
                       "). Помогает «Перепланировать с ограничением» — маршрут строится с нуля");
                return 0;
            }

            PlannedTrajectory retimed = KvTrajMath.Retime(v, plan, limits, 1f, 1f,
                (plan.Label ?? "вариант") + " · ограничение", true);
            PlannedTrajectory result = retimed != null ? retimed : plan;
            if (retimed == null) KvTrajMath.EnsureTimes(result);

            if (!KvVariantKit.ApplyPlan(flow, candidate, result))
            {
                Report("привести к ограничению не удалось: план не применён");
                return 0;
            }

            KvToolKinematics.PlanCheck after = KvToolKinematics.AnalyzePlan(v, result, c);
            checks[candidate] = after;
            LastProjected = fixedSamples;
            LastResidual = after.worstAngle;
            signature = CurrentSignature();

            Report(KvLocExtra2.F("constr.projected", "спроектировано сэмплов: {0} · остаток {1}°",
                       fixedSamples, after.worstAngle.ToString("0.0")) +
                   " · вариант " + KvVariantKit.Label(candidate, index) +
                   (failed > 0 ? " · НЕ приведено " + failed + " сэмплов (худший остаток " +
                                 worstLeft.ToString("0.0") + "°) — доступно «Перепланировать с ограничением»" : "") +
                   (after.violations == 0
                       ? " · " + KvLocExtra2.T("constr.ok",
                           "выбранная траектория удовлетворяет ограничению")
                       : ""));
            return fixedSamples;
        }

        /// <summary>Сколько сэмплов последней проекции привести не удалось (ФИКС 2).</summary>
        public int LastFailed { get; private set; }
        /// <summary>Сколько проб IK выполнено в последней проекции (диагностика).</summary>
        public int LastAttempts { get; private set; }

        /// <summary>
        /// ПЕРЕПЛАНИРОВАТЬ С ОГРАНИЧЕНИЕМ (ФИКС 2): сегмент строится С НУЛЯ под ограничение,
        /// а не проектируется существующий путь.
        ///
        /// Схема (ядро не меняется — только публичные методы планировщика):
        ///   1. берутся ветви целевой точки (`Planner.SolveGoalConfigs`), среди них выбирается
        ///      та, что соблюдает ограничение и держит запас до лимитов;
        ///   2. путь строится штатным BiRRT в найденную позу (`Planner.PlanViaWaypoint`);
        ///   3. если в заданную позу пути нет — пробуются остальные ветви;
        ///   4. если подходящих поз нет вообще, честно сообщается, что ограничение в цели
        ///      не выполняется, и маршрут помечается соответствующей строкой.
        /// </summary>
        public int Replan(TrajectoryCandidate candidate, int index)
        {
            LastProjected = 0;
            LastResidual = 0f;
            LastFailed = 0;
            LastAttempts = 0;
            LastProjectedIndex = index;

            KvOrientConstraint c = Active;
            if (candidate == null || candidate.plan == null || candidate.plan.Path == null ||
                candidate.plan.Path.Length < 2)
            {
                Report(KvLocExtra2.T("export.noplan",
                    "Нет траектории: выберите точку (Z + ЛКМ) и подтвердите вариант (X + ЛКМ)"));
                return 0;
            }
            if (c == null)
            {
                Report("ограничение выключено — перепланировать не под чем");
                return 0;
            }
            if (flow == null || flow.Validator == null || !flow.Validator.Ready ||
                flow.Planner == null || !flow.Planner.Ready)
            {
                Report("перепланирование невозможно: планировщик робота не готов");
                return 0;
            }

            PoseValidator v = flow.Validator;
            Planner planner = flow.Planner;
            double[] start = candidate.plan.Path[0];
            Vector3 goal = v.TcpAt(candidate.plan.Path[candidate.plan.Path.Length - 1]);
            float tol = Mathf.Max(0.1f, c.toleranceDeg);

            List<Planner.GoalConfig> branches = planner.SolveGoalConfigs(start, goal, 8, 4242);
            if (branches == null || branches.Count == 0)
            {
                Report("перепланирование: планировщик не дал ни одной позы для точки");
                return 0;
            }

            var usable = new List<Planner.GoalConfig>();
            var fallback = new List<Planner.GoalConfig>();
            float bestErr = float.MaxValue;
            for (int i = 0; i < branches.Count; i++)
            {
                Planner.GoalConfig b = branches[i];
                if (b.q == null || !v.WithinLimits(b.q)) continue;
                LastAttempts++;
                float err = KvToolKinematics.Error(v, b.q, c);
                if (err < bestErr) bestErr = err;
                if (err <= tol && v.LimitMargin(b.q) >= minLimitMargin) usable.Add(b);
                else fallback.Add(b);
            }
            if (usable.Count == 0 && fallback.Count == 0)
            {
                Report("перепланирование: все позы точки — вне лимитов суставов");
                return 0;
            }

            List<Planner.GoalConfig> order = usable.Count > 0 ? usable : fallback;
            string note = usable.Count > 0
                ? ""
                : " (огибание: ограничение в цели не выполняется, худший угол " +
                  bestErr.ToString("0.0") + "°)";

            PlannedTrajectory built = null;
            double[] usedGoal = null;
            for (int i = 0; i < order.Count && built == null; i++)
            {
                PlannedTrajectory via = planner.PlanViaWaypoint(start, order[i].q, 4242 + i * 17,
                    "ограничение · перепланирование");
                if (via != null && via.Path != null && via.Path.Length > 1)
                {
                    built = via;
                    usedGoal = order[i].q;
                }
            }
            if (built == null)
            {
                Report("перепланирование не удалось: путь в подходящую позу не найден " +
                       "(" + order.Count + " поз проверено)" + note);
                return 0;
            }

            PlannedTrajectory retimed = KvTrajMath.Retime(v, built, limits, 1f, 1f,
                (candidate.plan.Label ?? "вариант") + " · перепланировано под ограничение", true);
            PlannedTrajectory result = retimed != null ? retimed : built;
            if (retimed == null) KvTrajMath.EnsureTimes(result);
            if (!KvVariantKit.ApplyPlan(flow, candidate, result))
            {
                Report("перепланирование: план не применён");
                return 0;
            }

            KvToolKinematics.PlanCheck after = KvToolKinematics.AnalyzePlan(v, result, c);
            checks[candidate] = after;
            LastProjected = result.Path.Length - after.violations;
            LastFailed = after.violations;
            LastResidual = after.worstAngle;
            signature = CurrentSignature();

            Report("перепланировано под ограничение: вариант " +
                   KvVariantKit.Label(candidate, index) + " · сэмплов " + result.Path.Length +
                   " · запас лимитов " +
                   (usedGoal != null ? v.LimitMargin(usedGoal) : 0f).ToString("0.0") + "°" +
                   (after.violations == 0
                       ? " · ограничение соблюдено на всех сэмплах"
                       : " · нарушений " + after.violations + " из " + after.samples +
                         " (худший угол " + after.worstAngle.ToString("0.0") + "°)") + note);
            return LastProjected;
        }

        /// <summary>Перепланировать выбранный вариант (кнопка/команда).</summary>
        public int ReplanSelected()
        {
            int index;
            TrajectoryCandidate candidate = KvVariantKit.Selected(flow, out index);
            return Replan(candidate, index);
        }
        /// <summary>Привести выбранный вариант (кнопка/команда).</summary>
        public int ProjectSelected()
        {
            int index;
            TrajectoryCandidate candidate = KvVariantKit.Selected(flow, out index);
            return Project(candidate, index);
        }

        /// <summary>Задать цель «смотреть на объект» из точки прицела.</summary>
        public void SetLookAtFromAim(Vector3 point)
        {
            Load();
            profile.mode = KvOrientMode.LookAt;
            profile.targetPoint = point;
            Save();
            Invalidate();
        }

        private void Report(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            Debug.Log("[Constrained] " + text);
            if (Message != null) Message(text);
        }
    }

    /// <summary>
    /// ВКЛАДКА «ПЛАНИРОВАНИЕ С ОГРАНИЧЕНИЯМИ» (ЭТАП 8 ТЗ): выбор типа ограничения,
    /// предела наклона, допуска, цели «смотреть на объект», кнопки проверки и приведения.
    /// </summary>
    public class KvConstrainedTab : IKvWorkbenchTab
    {
        private readonly KvConstrainedPlanner service;
        private readonly Func<Vector3> aimProvider;
        private KvSegmented modeSegment;

        public KvConstrainedTab(KvConstrainedPlanner planner, Func<Vector3> aimPoint)
        {
            service = planner;
            aimProvider = aimPoint;
        }

        public string Key { get { return "constraints"; } }
        public string Title { get { return KvLocExtra2.T("constr.title", "Планирование с ограничениями"); } }

        private static string T(string key, string fallback)
        {
            return KvLocExtra2.T(key, fallback);
        }

        public void Build(KvTabKit kit)
        {
            if (service == null || kit == null) return;

            kit.Section(Title);
            kit.Toggle(T("constr.enable", "Включить ограничения планирования"),
                service.Enabled, delegate (bool v)
                {
                    service.Enabled = v;
                    if (v) service.Invalidate();
                });

            string[] modes =
            {
                T("constr.mode.none", "нет"),
                T("constr.mode.vertical", "инструмент вертикально"),
                T("constr.mode.tilt", "наклон не больше N°"),
                T("constr.mode.lookat", "TCP смотрит на объект")
            };
            // В интерфейсе четыре понятных режима: None / Vertical / MaxTilt / LookAt.
            KvOrientMode[] order =
            {
                KvOrientMode.None, KvOrientMode.ToolVertical, KvOrientMode.MaxTilt,
                KvOrientMode.LookAt
            };
            int current = 0;
            for (int i = 0; i < order.Length; i++)
                if (order[i] == service.Profile.mode) current = i;

            modeSegment = kit.Segmented(T("constr.mode", "Тип ограничения"), modes, current,
                delegate (int i)
                {
                    service.Profile.mode = order[Mathf.Clamp(i, 0, order.Length - 1)];
                    if (service.Profile.mode == KvOrientMode.ToolVertical)
                        service.Profile.reference = Vector3.down;
                    service.Save();
                    service.Invalidate();
                });

            kit.Info(delegate
            {
                return T("constr.status", "Состояние ограничения") + ": " +
                       service.Profile.Describe();
            }, KvTheme.Accent);

            kit.Slider(T("constr.tilt", "Предел наклона, °"), 1f, 90f,
                service.Profile.tiltLimitDeg, "0.0", delegate (float v)
                {
                    service.Profile.tiltLimitDeg = v;
                    service.Save();
                    service.Invalidate();
                });
            kit.Slider(T("constr.tolerance", "Допуск, °"), 0.5f, 45f,
                service.Profile.toleranceDeg, "0.0", delegate (float v)
                {
                    service.Profile.toleranceDeg = v;
                    service.Save();
                    service.Invalidate();
                });

            kit.Section(T("constr.axis.ref", "Опорное направление"));
            kit.Buttons(new[]
            {
                T("constr.axis.down", "вниз (−Y)"), T("constr.axis.up", "вверх (+Y)")
            }, new Action[]
            {
                delegate
                {
                    service.Profile.reference = Vector3.down;
                    service.Profile.direction = Vector3.down;
                    service.Save();
                    service.Invalidate();
                },
                delegate
                {
                    service.Profile.reference = Vector3.up;
                    service.Profile.direction = Vector3.up;
                    service.Save();
                    service.Invalidate();
                }
            });

            kit.Buttons(new[]
            {
                T("constr.target", "Объект наблюдения (из точки прицела)")
            }, new Action[] { delegate { SetTargetFromAim(); } });
            kit.Info(delegate
            {
                Vector3 p = service.Profile.targetPoint;
                return T("constr.mode.lookat", "TCP смотрит на объект") + ": (" +
                       p.x.ToString("0.000") + ", " + p.y.ToString("0.000") + ", " +
                       p.z.ToString("0.000") + ")";
            }, KvTheme.TextDim);

            kit.Toggle(T("constr.filter", "Отбрасывать варианты, нарушающие ограничение"),
                service.DiscardViolating, delegate (bool v) { service.DiscardViolating = v; });

            kit.Buttons(new[]
            {
                T("constr.check", "Проверить выбранную траекторию"),
                T("constr.project", "Привести траекторию к ограничению"),
                T("constr.replan", "Перепланировать с ограничением")
            }, new Action[]
            {
                delegate { CheckSelected(); },
                delegate { service.ProjectSelected(); },
                delegate { service.ReplanSelected(); }
            });

            // ФИКС 2: остаток проекции НЕ скрывается — оператор видит, сколько сэмплов
            // привести не удалось и каков худший угол, и может перепланировать сегмент с нуля.
            kit.Info(delegate
            {
                if (service.LastFailed <= 0) return "";
                return "⚠ " + T("constr.unreduced", "не приведено сэмплов") + ": " +
                       service.LastFailed + " · " + T("constr.worst", "худший остаток") + " " +
                       service.LastResidual.ToString("0.0") + "° · " +
                       (service.Active != null && service.Active.mode != KvOrientMode.None
                           ? T("constr.hint.replan",
                               "нажмите «Перепланировать с ограничением» — маршрут будет построен заново")
                           : "");
            }, KvTheme.Warn);

            kit.Divider();
            kit.Table("", delegate
            {
                return T("wb.variant", "Вариант");
            }, delegate
            {
                return T("constr.status", "Ограничение");
            });
            for (int i = 0; i < 8; i++)
            {
                int index = i;
                kit.Table("#" + (index + 1),
                    delegate { return VariantSummary(index); },
                    delegate { return VariantViolation(index); });
            }

            kit.Divider();
            kit.Note(T("constr.info",
                "Ограничение проверяется на КАЖДОМ сэмпле траектории по фактической оси инструмента. " +
                "«Привести к ограничению» пересчитывает позы методом IK с дополнительным условием — " +
                "цель остаётся той же."), KvTheme.TextDim);
        }

        private void SetTargetFromAim()
        {
            Vector3 p = aimProvider != null ? aimProvider() : Vector3.zero;
            TrajectoryFlowController f = service.Flow();
            if (p.sqrMagnitude < 1e-6f && f != null && f.State != null && f.State.hasPoint)
                p = f.State.point;
            service.SetLookAtFromAim(p);
        }

        private void CheckSelected()
        {
            TrajectoryFlowController f = service.Flow();
            if (f == null || f.State == null) return;
            int index = f.State.selectedTrajectory >= 0 ? f.State.selectedTrajectory : 0;
            if (index < 0 || index >= f.State.candidates.Count) return;
            KvToolKinematics.PlanCheck check = service.Check(f.State.candidates[index]);
            Debug.Log("[Constrained] проверка варианта №" + (index + 1) + ": " +
                      check.Describe(KvLocExtra2.T("constr.status", "Состояние ограничения")));
        }

        private string VariantSummary(int index)
        {
            TrajectoryFlowController f = service.Flow();
            if (f == null || f.State == null || index >= f.State.candidates.Count) return "—";
            TrajectoryCandidate c = f.State.candidates[index];
            if (c == null) return "—";
            return c.timeS.ToString("0.00") + " с · " + c.lengthM.ToString("0.00") + " м";
        }

        private string VariantViolation(int index)
        {
            TrajectoryFlowController f = service.Flow();
            if (f == null || f.State == null || index >= f.State.candidates.Count) return "—";
            TrajectoryCandidate c = f.State.candidates[index];
            if (c == null) return "—";
            if (service.Active == null) return "—";
            KvToolKinematics.PlanCheck check = service.Check(c);
            if (check.samples == 0) return "—";
            return check.violations == 0
                ? "✓ " + check.compliance.ToString("P0")
                : "⚠ " + check.violations + "/" + check.samples + " · " +
                  check.worstAngle.ToString("0.0") + "°";
        }

        public void Tick() { }

        public void Refresh()
        {
            if (modeSegment == null || service == null) return;
            int current = 0;
            switch (service.Profile.mode)
            {
                case KvOrientMode.ToolVertical: current = 1; break;
                case KvOrientMode.MaxTilt: current = 2; break;
                case KvOrientMode.LookAt: current = 3; break;
                default: current = 0; break;
            }
            if (modeSegment.Index != current) modeSegment.Set(current);
        }
    }
}
