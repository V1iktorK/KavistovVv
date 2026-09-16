using System;
using System.Collections.Generic;
using UnityEngine;
using TrajectoryCore;
namespace KazistovVvFeatures
{
    /// <summary>ТИП ОГРАНИЧЕНИЯ ОРИЕНТАЦИИ ИНСТРУМЕНТА (этапы 7 и 8 ТЗ).</summary>
    public enum KvOrientMode
    {
        /// <summary>Ограничения нет.</summary>
        None = 0,
        /// <summary>Ось инструмента должна смотреть в заданном направлении мира.</summary>
        ToolDirection = 1,
        /// <summary>Инструмент вертикально (вниз или вверх) — частный случай ToolDirection.</summary>
        ToolVertical = 2,
        /// <summary>Наклон от опорного направления не больше предела.</summary>
        MaxTilt = 3,
        /// <summary>TCP «смотрит» на объект: ось инструмента направлена в целевую точку.</summary>
        LookAt = 4
    }

    /// <summary>
    /// ОПИСАНИЕ ОГРАНИЧЕНИЯ ОРИЕНТАЦИИ: используется и промежуточными точками (этап 7),
    /// и планированием с ограничениями (этап 8), и калибровкой (этап 9 — там важна
    /// ориентация при обучении точек).
    /// </summary>
    public class KvOrientConstraint
    {
        public KvOrientMode mode = KvOrientMode.None;
        /// <summary>Требуемое направление оси инструмента в мире (ToolDirection).</summary>
        public Vector3 direction = Vector3.down;
        /// <summary>Опорное направление (ToolVertical/MaxTilt): вертикаль мира.</summary>
        public Vector3 reference = Vector3.down;
        /// <summary>Предел наклона, ° (MaxTilt).</summary>
        public float tiltLimitDeg = 15f;
        /// <summary>Точка, на которую смотрит инструмент (LookAt).</summary>
        public Vector3 targetPoint = Vector3.zero;
        /// <summary>Допуск соблюдения, °.</summary>
        public float toleranceDeg = 6f;

        public bool Active { get { return mode != KvOrientMode.None; } }

        public KvOrientConstraint Clone()
        {
            return new KvOrientConstraint
            {
                mode = mode,
                direction = direction,
                reference = reference,
                tiltLimitDeg = tiltLimitDeg,
                targetPoint = targetPoint,
                toleranceDeg = toleranceDeg
            };
        }

        /// <summary>Текстовое описание для интерфейса, журнала и свойств.</summary>
        public string Describe()
        {
            switch (mode)
            {
                case KvOrientMode.ToolDirection:
                    return "ось инструмента → (" + direction.x.ToString("0.00") + ", " +
                           direction.y.ToString("0.00") + ", " + direction.z.ToString("0.00") +
                           "), допуск " + toleranceDeg.ToString("0.0") + "°";
                case KvOrientMode.ToolVertical:
                    return "инструмент вертикально (" +
                           (reference.y >= 0f ? "вверх" : "вниз") + "), допуск " +
                           toleranceDeg.ToString("0.0") + "°";
                case KvOrientMode.MaxTilt:
                    return "наклон ≤ " + tiltLimitDeg.ToString("0.0") + "° от (" +
                           reference.x.ToString("0.00") + ", " + reference.y.ToString("0.00") + ", " +
                           reference.z.ToString("0.00") + ")";
                case KvOrientMode.LookAt:
                    return "TCP смотрит в точку (" + targetPoint.x.ToString("0.00") + ", " +
                           targetPoint.y.ToString("0.00") + ", " + targetPoint.z.ToString("0.00") + ")";
                default:
                    return "нет";
            }
        }
    }

    /// <summary>
    /// КИНЕМАТИКА ИНСТРУМЕНТА И IK С ДОПОЛНИТЕЛЬНЫМИ ОГРАНИЧЕНИЯМИ (этапы 7–8 ТЗ).
    ///
    /// ЧТО ЗДЕСЬ:
    ///   • ось инструмента считается по ФАКТИЧЕСКОЙ кинематике — это ось вращения последнего
    ///     сустава (`PoseValidator.AxisWorld`), ровно тот же критерий, что и у `ToolAlign`
    ///     и у оракула достижимости. Ничего в ядре не меняется;
    ///   • `Error` возвращает НАРУШЕНИЕ ограничения в градусах (0 — соблюдено), поэтому одна
    ///     и та же функция служит и проверкой траектории, и целевой функцией решателя;
    ///   • `Solve` — спуск по суставам (численный градиент), который держит TCP в заданной
    ///     точке и одновременно уменьшает нарушение ориентации; призматические оси двигаются
    ///     в МЕТРАХ, вращательные — в градусах (как принято в проекте);
    ///   • `AnalyzePlan` проверяет ограничение на КАЖДОМ сэмпле готового плана: сколько
    ///     сэмплов нарушают, худший угол и доля соблюдения.
    /// </summary>
    public static class KvToolKinematics
    {
        /// <summary>Ось инструмента: ось вращения последнего сустава (нормаль концевой плоскости).</summary>
        public static Vector3 ToolAxis(PoseValidator v, double[] q)
        {
            if (v == null || !v.Ready || q == null) return Vector3.up;
            Vector3 a = v.AxisWorld(v.Dof - 1, q);
            if (a.sqrMagnitude < 1e-8f) return Vector3.up;
            a.Normalize();
            // У робота с ПРИЗМАТИЧЕСКОЙ последней осью (SCARA: J1, J2, Z) ось призмы
            // направлена «вверх», а инструмент смотрит ПРОТИВ её хода — то есть вниз.
            // Так ограничение «инструмент вертикально» для SCARA выполняется по построению
            // (её инструмент действительно всегда вертикален), а не помечается нарушением.
            return v.IsPrismatic(v.Dof - 1) ? -a : a;
        }

        /// <summary>Фактический наклон инструмента от опорного направления, °.</summary>
        public static float TiltDeg(PoseValidator v, double[] q, Vector3 reference)
        {
            Vector3 r = reference.sqrMagnitude > 1e-8f ? reference.normalized : Vector3.down;
            return Vector3.Angle(ToolAxis(v, q), r);
        }

        /// <summary>
        /// Куда должна смотреть ось инструмента в данной конфигурации (для LookAt направление
        /// зависит от фактического TCP, поэтому считается каждый раз).
        /// </summary>
        public static Vector3 DesiredAxis(PoseValidator v, double[] q, KvOrientConstraint c)
        {
            if (c == null) return Vector3.up;
            switch (c.mode)
            {
                case KvOrientMode.ToolDirection:
                    return c.direction.sqrMagnitude > 1e-8f ? c.direction.normalized : Vector3.down;
                case KvOrientMode.ToolVertical:
                    return c.reference.sqrMagnitude > 1e-8f ? c.reference.normalized : Vector3.down;
                case KvOrientMode.LookAt:
                    if (v != null && v.Ready && q != null)
                    {
                        Vector3 dir = c.targetPoint - v.TcpAt(q);
                        if (dir.sqrMagnitude > 1e-8f) return dir.normalized;
                    }
                    return Vector3.down;
                case KvOrientMode.MaxTilt:
                    return c.reference.sqrMagnitude > 1e-8f ? c.reference.normalized : Vector3.down;
                default:
                    return Vector3.up;
            }
        }

        /// <summary>
        /// НАРУШЕНИЕ ОГРАНИЧЕНИЯ В ГРАДУСАХ: 0 — соблюдено. Для MaxTilt превышение предела,
        /// для остальных — угол между фактической осью и требуемой.
        /// </summary>
        public static float Error(PoseValidator v, double[] q, KvOrientConstraint c)
        {
            if (c == null || !c.Active || v == null || !v.Ready || q == null) return 0f;

            if (c.mode == KvOrientMode.MaxTilt)
            {
                float tilt = TiltDeg(v, q, c.reference);
                return Mathf.Max(0f, tilt - Mathf.Max(0f, c.tiltLimitDeg));
            }

            Vector3 want = DesiredAxis(v, q, c);
            return Vector3.Angle(ToolAxis(v, q), want);
        }

        /// <summary>Соблюдено ли ограничение с учётом допуска.</summary>
        public static bool Satisfies(PoseValidator v, double[] q, KvOrientConstraint c)
        {
            if (c == null || !c.Active) return true;
            return Error(v, q, c) <= Mathf.Max(0.1f, c.toleranceDeg);
        }

        // ================================================================== проверка плана

        /// <summary>Результат проверки ограничения на плане.</summary>
        public struct PlanCheck
        {
            public int samples;
            public int violations;
            public float worstAngle;      // худшее нарушение, °
            public float worstLimit;      // наибольший наклон фактически, °
            public float compliance;      // доля соблюдения 0..1
            public int firstViolation;    // индекс первого нарушающего сэмпла

            public bool Ok { get { return violations == 0; } }

            public string Describe(string what)
            {
                if (samples == 0) return what + ": нет данных";
                return what + ": " + (violations == 0
                    ? "соблюдено на всех " + samples + " сэмплах"
                    : "нарушений " + violations + " из " + samples + " · худший угол " +
                      worstAngle.ToString("0.0") + "°");
            }
        }

        /// <summary>Проверить ограничение на всех сэмплах готового плана.</summary>
        public static PlanCheck AnalyzePlan(PoseValidator v, PlannedTrajectory plan,
            KvOrientConstraint c)
        {
            PlanCheck result = new PlanCheck();
            if (plan == null || plan.Path == null || plan.Path.Length == 0) return result;
            if (c == null || !c.Active || v == null || !v.Ready) return result;

            result.samples = plan.Path.Length;
            result.firstViolation = -1;
            float tol = Mathf.Max(0.1f, c.toleranceDeg);

            for (int i = 0; i < plan.Path.Length; i++)
            {
                float err = Error(v, plan.Path[i], c);
                float tilt = TiltDeg(v, plan.Path[i], c.reference);
                if (tilt > result.worstLimit) result.worstLimit = tilt;
                if (err > tol)
                {
                    result.violations++;
                    if (result.firstViolation < 0) result.firstViolation = i;
                }
                if (err > result.worstAngle) result.worstAngle = err;
            }
            result.compliance = result.samples > 0
                ? 1f - result.violations / (float)result.samples
                : 0f;
            return result;
        }

        // ================================================================== решатель IK с ограничением

        /// <summary>
        /// СТАРТОВЫЕ ПРИБЛИЖЕНИЯ ДЛЯ IK С ОГРАНИЧЕНИЕМ (ФИКС 2).
        ///
        /// Спуск по градиенту — локальный метод: у ориентации «на 180°» (инструмент должен
        /// смотреть вниз, а смотрит вверх) градиент в окрестности почти нулевой, и из
        /// «обычной» позы решение не находится, хотя оно существует. Поэтому для нарушающих
        /// сэмплов перебираются РАЗНЫЕ ветви:
        ///   • уже исправленный соседний сэмпл (путь остаётся непрерывным);
        ///   • сам сэмпл;
        ///   • ЗЕРКАЛЬНАЯ КОНФИГУРАЦИЯ ЗАПЯСТЬЯ (J4+180, J5→−J5, J6+180) — приём, которым
        ///     пользуются и `KvPlanKit.SolvePoseForPoint`, и `IkSolver.SolveAllSeeded`;
        ///   • «нулевая» поза и середина диапазонов лимитов;
        ///   • ветви штатного планировщика для этой точки (они уже проверены по лимитам).
        ///
        /// Возвращает список проб в порядке приоритета (дубликаты отбрасываются).
        /// </summary>
        public static List<double[]> Seeds(PoseValidator v, Planner planner, Vector3 point,
            KvOrientConstraint c, double[] current, double[] previous)
        {
            var seeds = new List<double[]>();
            if (v == null || !v.Ready || current == null) return seeds;
            int dof = v.Dof;

            Action<double[]> add = delegate (double[] q)
            {
                if (q == null || q.Length < dof) return;
                for (int i = 0; i < seeds.Count; i++)
                {
                    double dist = 0.0;
                    for (int j = 0; j < dof; j++)
                    {
                        double d = seeds[i][j] - q[j];
                        dist += d * d;
                    }
                    if (dist < 0.25) return;              // та же ветвь (в градусах²)
                }
                seeds.Add((double[])q.Clone());
            };

            if (previous != null) add(previous);
            add(current);

            if (dof >= 6)
            {
                double[] flipped = (double[])current.Clone();
                flipped[3] += 180.0;
                flipped[4] = -flipped[4];
                flipped[5] += 180.0;
                if (v.WithinLimits(flipped)) add(flipped);

                if (previous != null)
                {
                    double[] flippedPrev = (double[])previous.Clone();
                    flippedPrev[3] += 180.0;
                    flippedPrev[4] = -flippedPrev[4];
                    flippedPrev[5] += 180.0;
                    if (v.WithinLimits(flippedPrev)) add(flippedPrev);
                }
            }

            double[] zero = new double[dof];
            add(zero);

            double[] middle = new double[dof];
            for (int i = 0; i < dof; i++)
                middle[i] = v.IsPrismatic(i)
                    ? v.Lower[i] + (v.Upper[i] - v.Lower[i]) * 0.5
                    : 0.0;
            add(middle);

            if (planner != null && planner.Ready)
            {
                List<Planner.GoalConfig> branches =
                    planner.SolveGoalConfigs(previous != null ? previous : current, point, 8, 4242);
                if (branches != null)
                    foreach (Planner.GoalConfig branch in branches)
                        if (branch.q != null && v.WithinLimits(branch.q)) add(branch.q);
            }
            return seeds;
        }

        /// <summary>Шаг по суставу: призматическая ось — метры, вращательная — градусы.</summary>
        private static float StepFor(PoseValidator v, int joint, float baseDeg)
        {
            return v.IsPrismatic(joint) ? baseDeg * 0.001f : baseDeg;
        }

        /// <summary>
        /// РЕШИТЬ IK С ДОПОЛНИТЕЛЬНЫМ ОГРАНИЧЕНИЕМ: TCP остаётся в точке `point`,
        /// нарушение ориентации уменьшается до допуска. Возвращает false с причиной,
        /// если поза не найдена (лимиты, зазор, самозазор, сближение звеньев).
        /// </summary>
        public static bool Solve(PoseValidator v, CollisionWorld world, Vector3 point,
            KvOrientConstraint c, double[] seed, float minClearance, float minSelfClearance,
            float minLimitMarginDeg, float posTolM, float maxJointTurnDeg,
            out double[] q, out float posErrM, out float angleErrDeg, out string why)
        {
            q = seed != null ? (double[])seed.Clone() : null;
            posErrM = float.MaxValue;
            angleErrDeg = float.MaxValue;
            why = "";

            if (v == null || !v.Ready) { why = "робот не определён"; return false; }
            if (q == null || q.Length < v.Dof) { why = "нет начальной позы"; return false; }

            const int maxSweeps = 30;
            const float wPos = 25f;
            float posTol = Mathf.Max(0.0005f, posTolM);
            double[] best = (double[])q.Clone();
            float bestScore = float.MaxValue;

            float cost = Cost(v, q, point, c, wPos);
            for (int sweep = 0; sweep < maxSweeps; sweep++)
            {
                posErrM = Vector3.Distance(v.TcpAt(q), point);
                angleErrDeg = Error(v, q, c);

                float score = posErrM / posTol + angleErrDeg / Mathf.Max(0.1f, c.toleranceDeg);
                if (score < bestScore) { bestScore = score; best = (double[])q.Clone(); }
                if (posErrM <= posTol && angleErrDeg <= Mathf.Max(0.1f, c.toleranceDeg)) break;

                bool moved = false;
                for (int i = 0; i < v.Dof; i++)
                {
                    float h = StepFor(v, i, 1.5f);
                    float g = Gradient(v, q, i, point, c, wPos, h);
                    if (Mathf.Abs(g) < 1e-7f) continue;
                    float dir = g > 0f ? -1f : 1f;

                    for (int t = 0; t < 3; t++)
                    {
                        float dq = StepFor(v, i, 5f) * dir / (1 << t);
                        double save = q[i];
                        q[i] = save + dq;
                        if (!v.WithinLimits(q)) { q[i] = save; continue; }
                        float next = Cost(v, q, point, c, wPos);
                        if (next < cost - 1e-7f) { cost = next; moved = true; break; }
                        q[i] = save;
                    }
                }
                if (!moved) break;
            }

            posErrM = Vector3.Distance(v.TcpAt(best), point);
            angleErrDeg = Error(v, best, c);
            q = best;

            if (posErrM > posTol)
            {
                why = "TCP не удержан в точке (" + (posErrM * 1000f).ToString("0") + " мм)";
                return false;
            }
            if (angleErrDeg > Mathf.Max(0.1f, c.toleranceDeg))
            {
                why = "ограничение не выполнено (остаток " + angleErrDeg.ToString("0.0") + "°)";
                return false;
            }
            if (!v.WithinLimits(q)) { why = "поза вне лимитов суставов"; return false; }
            if (v.LimitMargin(q) < minLimitMarginDeg)
            {
                why = "мал запас до лимитов (" + v.LimitMargin(q).ToString("0.0") + "° < " +
                      minLimitMarginDeg.ToString("0.0") + "°)";
                return false;
            }
            if (v.SelfClearance(q, out _, out _) < minSelfClearance)
            {
                why = "сближение своих звеньев";
                return false;
            }
            if (world != null && v.ClearanceAt(q, world, out _, out _) < minClearance)
            {
                why = "мал зазор до препятствия";
                return false;
            }
            if (maxJointTurnDeg > 0f && seed != null)
            {
                for (int i = 0; i < v.Dof && i < seed.Length; i++)
                {
                    if (v.IsPrismatic(i)) continue;
                    if (Mathf.Abs(Mathf.DeltaAngle((float)seed[i], (float)q[i])) > maxJointTurnDeg)
                    {
                        why = "слишком большой доворот сустава " + (i + 1) +
                              " (" + Mathf.Abs(Mathf.DeltaAngle((float)seed[i], (float)q[i]))
                                  .ToString("0") + "°)";
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>Стоимость: ошибка позиции TCP (см) + нарушение ограничения (град).</summary>
        private static float Cost(PoseValidator v, double[] q, Vector3 point, KvOrientConstraint c,
            float wPos)
        {
            float e = Vector3.Distance(v.TcpAt(q), point) * 100f;
            float ang = Error(v, q, c);
            return wPos * e * e + ang * ang;
        }

        /// <summary>Производная стоимости по суставу (центральная разность).</summary>
        private static float Gradient(PoseValidator v, double[] q, int i, Vector3 point,
            KvOrientConstraint c, float wPos, float h)
        {
            double save = q[i];
            q[i] = save + h; float plus = Cost(v, q, point, c, wPos);
            q[i] = save - h; float minus = Cost(v, q, point, c, wPos);
            q[i] = save;
            return (plus - minus) / (2f * h);
        }

        // ================================================================== поза прохода с ограничением

        /// <summary>
        /// Поза прохода через точку с учётом ограничения: сначала пробуются ветви IK
        /// планировщика (как в waypoint-редакторе), среди них выбирается САМАЯ «свободная»
        /// по ориентации и запасу лимитов; если ни одна ветвь не подошла — численный
        /// решатель с ограничением из текущей позы.
        /// </summary>
        public static double[] SolveWaypointPose(PoseValidator v, CollisionWorld world, Planner planner,
            Vector3 point, KvOrientConstraint c, double[] seed, float minClearance,
            float minSelfClearance, float minLimitMarginDeg, float posTolM,
            out float margin, out float angleErr, out string note)
        {
            margin = 0f;
            angleErr = 0f;
            note = "";
            if (v == null || !v.Ready) { note = "робот не определён"; return null; }

            double[] best = null;
            float bestScore = float.MaxValue;

            // --- 1) ветви IK планировщика: годятся, если соблюдают ограничение
            if (planner != null && planner.Ready)
            {
                List<Planner.GoalConfig> branches = planner.SolveGoalConfigs(seed, point, 8, 4242);
                if (branches != null)
                {
                    foreach (Planner.GoalConfig branch in branches)
                    {
                        if (branch.q == null || branch.q.Length < v.Dof) continue;
                        if (!v.WithinLimits(branch.q)) continue;
                        float err = Error(v, branch.q, c);
                        if (err > Mathf.Max(0.1f, c.toleranceDeg)) continue;
                        float m = v.LimitMargin(branch.q);
                        if (m < minLimitMarginDeg) continue;
                        if (world != null && v.ClearanceAt(branch.q, world, out _, out _) < minClearance)
                            continue;
                        float score = -m;
                        if (score < bestScore)
                        {
                            bestScore = score;
                            best = branch.q;
                            margin = m;
                            angleErr = err;
                        }
                    }
                }
            }

            if (best != null)
            {
                note = "поза из ветвей IK с ограничением";
                return best;
            }

            // --- 2) численный решатель с ограничением
            double[] q;
            float posErr, angErr;
            string why;
            if (Solve(v, world, point, c, seed, minClearance, minSelfClearance, minLimitMarginDeg,
                    posTolM, 120f, out q, out posErr, out angErr, out why))
            {
                margin = v.LimitMargin(q);
                angleErr = angErr;
                note = "IK с ограничением";
                return q;
            }

            // --- 3) без ограничения вообще (для диагностики: точка достижима, но не так)
            double[] plain;
            string plainWhy;
            if (KvPlanKit.SolvePoseForPoint(v, world, point, c != null && c.Active
                    ? DesiredAxis(v, seed, c) : Vector3.down, seed, out plain, out plainWhy))
            {
                margin = v.LimitMargin(plain);
                angleErr = Error(v, plain, c);
                note = "точка достижима, но ограничение не выполнено (" + why + ")";
                return plain;
            }

            // --- 4) последний резерв: обычные ветви IK планировщика с максимальным запасом.
            //     Точка остаётся рабочей, но ограничение честно помечается невыполненным —
            //     по ТЗ ограничение обязательно, поэтому такая точка помечается недостижимой
            //     в контексте ограничения (см. KvWaypoints.EvaluateAll).
            if (planner != null && planner.Ready)
            {
                List<Planner.GoalConfig> branches = planner.SolveGoalConfigs(seed, point, 8, 4242);
                double[] bestPlain = null;
                float bestMargin = -1f;
                if (branches != null)
                {
                    foreach (Planner.GoalConfig branch in branches)
                    {
                        if (branch.q == null || branch.q.Length < v.Dof) continue;
                        if (!v.WithinLimits(branch.q)) continue;
                        float m = v.LimitMargin(branch.q);
                        if (m > bestMargin) { bestMargin = m; bestPlain = branch.q; }
                    }
                }
                if (bestPlain != null)
                {
                    margin = bestMargin;
                    angleErr = Error(v, bestPlain, c);
                    note = KvLocExtra2.T("wp.orient.fail", "ориентация не достигнута") + " (" +
                           angleErr.ToString("0.0") + "° > " +
                           Mathf.Max(0.1f, c.toleranceDeg).ToString("0.0") + "°)";
                    return bestPlain;
                }
            }

            note = string.IsNullOrEmpty(why) ? plainWhy : why;
            return null;
        }
    }
}
