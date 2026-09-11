using System.Collections.Generic;
using UnityEngine;

namespace TrajectoryCore
{
    /// <summary>Готовая траектория: сэмплы q(t) + метрики.</summary>
    public class PlannedTrajectory
    {
        public double[][] Path;      // сэмплы конфигураций
        public float[] Times;        // время каждого сэмпла, с
        public string Label = "";
        public double Time;          // полное время, с
        public double Length;        // нормированная длина пути
        public float MinClearance;   // минимальный зазор, м
        public float LimitMargin;    // минимальный запас до лимитов, град
        public double SigmaMin;      // оценка σ_min в цели
        public double Score;         // итоговый (меньше — лучше)

        public double[] GoalQ => Path != null && Path.Length > 0 ? Path[Path.Length - 1] : null;
    }

    /// <summary>
    /// Планировщик (E2–E3 плана): BiRRT-Connect в пространстве обобщённых координат
    /// с проверкой столкновений и лимитов, сглаживание short-cut, параметризация
    /// времени (трапеция по лимитам скоростей) и скоринг нескольких кандидатов.
    /// Детерминирован: один seed → один результат.
    /// </summary>
    public class Planner
    {
        public float clearance = 0.02f;
        public float selfClearance = 0.015f;   // запас между своими звеньями
        public int maxIterations = 2500;
        public double stepSizeDeg = 12.0;
        public float timeStep = 0.02f;      // шаг сэмплирования траектории
        public float accelShare = 0.25f;    // доля времени на разгон/торможение

        // Веса скоринга
        public double wTime = 1.0, wLength = 0.6, wClearance = 2.0, wLimit = 0.5, wSigma = 0.5;
        public double wPosture = 0.05;      // вклад стоимости позы (PostureSelector)

        private PoseValidator v;
        private CollisionWorld world;
        private IkSolver ik;
        private PostureSelector posture;
        private double[] ranges;

        public bool Ready { get; private set; }
        public int LastIterations { get; private set; }
        public string LastDebug { get; private set; } = "";

        public void Init(PoseValidator validator, CollisionWorld collisionWorld)
        {
            v = validator;
            world = collisionWorld;
            Ready = v != null && v.Ready && world != null;
            if (!Ready) return;
            ranges = new double[v.Dof];
            for (int i = 0; i < v.Dof; i++)
            {
                double r = v.Upper[i] - v.Lower[i];
                ranges[i] = r > 1e-6 ? r : 1.0;
            }
            ik = new IkSolver();
            ik.Init(v);
            posture = new PostureSelector();
            posture.Init(v);
        }

        /// <summary>Планирование до точки; возвращает до maxCandidates вариантов (отсортированы).</summary>
        public List<PlannedTrajectory> Plan(double[] start, Vector3 goalPoint, int maxCandidates, int seed)
        {
            var result = new List<PlannedTrajectory>();
            if (!Ready || start == null) return result;

            // 1. Конфигурации цели: сначала ВСЕ аналитические ветви IK (≤8), затем CCD-резерв.
            var goals = new List<double[]>();
            var goalTags = new List<string>();
            List<IkSolution> branches = ik != null && ik.Ready
                ? ik.SolveAll(goalPoint, start)
                : new List<IkSolution>();

            // Ранжируем ветви селектором позы (лимиты + home + сингулярность + непрерывность).
            branches.Sort((a, b) => PostureCost(a, start).CompareTo(PostureCost(b, start)));
            foreach (IkSolution s in branches)
            {
                if (!s.withinLimits) continue;
                bool dup = false;
                foreach (double[] ex in goals)
                    if (ConfigDistance(ex, s.q) < 0.02) { dup = true; break; }
                if (dup) continue;
                goals.Add(s.q);
                goalTags.Add(s.tag.ToString());
                if (goals.Count >= maxCandidates + 1) break;
            }
            LastBranchInfo = goalTags.Count > 0 ? string.Join(" | ", goalTags.ToArray()) : "нет ветвей";

            // CCD-резерв, если аналитика не дала решений (или робот SCARA).
            if (goals.Count == 0)
            {
                for (int k = 0; k < 4; k++)
                {
                    double[] s = (double[])start.Clone();
                    var rng = new System.Random(seed + k * 977);
                    for (int i = 0; i < v.Dof; i++)
                    {
                        if (k == 0) continue;
                        s[i] += (rng.NextDouble() * 2.0 - 1.0) * ranges[i] * 0.25;
                    }
                    if (!v.WithinLimits(s)) continue;
                    if (v.SolveIk(goalPoint, s, out double[] qg, 120, 0.008f) && v.WithinLimits(qg))
                    {
                        bool dup = false;
                        foreach (double[] ex in goals)
                            if (ConfigDistance(ex, qg) < 0.02) { dup = true; break; }
                        if (!dup) { goals.Add(qg); goalTags.Add("CCD"); }
                    }
                    if (goals.Count >= maxCandidates + 1) break;
                }
            }
            if (goals.Count == 0)
            {
                LastDebug = "IK: решения не найдены (ошибка " + (v.LastIkError * 1000f).ToString("0") + " мм)";
                return result;
            }

            int pathsFound = 0, filteredOut = 0;
            // 2. BiRRT для каждой конфигурации цели.
            foreach (double[] goal in goals)
            {
                List<double[]> path = PlanBiRrt(start, goal, seed);
                if (path == null || path.Count < 2) continue;
                pathsFound++;
                path = Shortcut(path, seed);
                PlannedTrajectory t = BuildTrajectory(path, goalPoint);
                if (t == null) continue;
                if (t.MinClearance < 0f) { filteredOut++; continue; } // столкновение
                result.Add(t);
                if (result.Count >= maxCandidates * 2) break;
            }
            LastDebug = "IK-решений=" + goals.Count + ", путей=" + pathsFound +
                        ", отброшено по коллизии=" + filteredOut + ", кандидатов=" + result.Count;

            // 3. Скоринг и отбор.
            foreach (PlannedTrajectory t in result) t.Score = Score(t);
            result.Sort((a, b) => a.Score.CompareTo(b.Score));
            for (int i = 0; i < result.Count && i < maxCandidates; i++)
                result[i].Label = "Вариант " + (i + 1) + (i == 0 ? " (оптимальный)" : "");
            if (result.Count > maxCandidates) result.RemoveRange(maxCandidates, result.Count - maxCandidates);
            return result;
        }

        private double Score(PlannedTrajectory t)
        {
            double clearPenalty = t.MinClearance <= 0.001 ? 1000.0 : wClearance / t.MinClearance;
            double limitPenalty = t.LimitMargin <= 0.5 ? 500.0 : wLimit * (30.0 / t.LimitMargin);
            double sigmaPenalty = t.SigmaMin <= 1e-6 ? 200.0 : wSigma * (0.05 / t.SigmaMin);
            double posturePenalty = posture != null && posture.Ready && t.GoalQ != null
                ? wPosture * posture.Cost(t.GoalQ, null) : 0.0;
            return wTime * t.Time + wLength * t.Length + clearPenalty + limitPenalty +
                   sigmaPenalty + posturePenalty;
        }

        private double PostureCost(IkSolution s, double[] qPrev)
        {
            return posture != null && posture.Ready ? posture.Cost(s.q, qPrev) : 0.0;
        }

        public string LastBranchInfo { get; private set; } = "";

        private double ConfigDistance(double[] a, double[] b)
        {
            double s = 0;
            for (int i = 0; i < v.Dof; i++)
            {
                double d = (a[i] - b[i]) / ranges[i];
                s += d * d;
            }
            return System.Math.Sqrt(s);
        }

        private bool Free(double[] q, out float clearanceOut)
        {
            clearanceOut = -1f;
            if (!v.WithinLimits(q)) return false;
            // самоколлизия собственных звеньев
            float self = v.SelfClearance(q, out _, out _);
            if (self < selfClearance) { clearanceOut = self; return false; }
            clearanceOut = v.ClearanceAt(q, world, out _, out _);
            return clearanceOut >= clearance;
        }

        private double[] Steer(double[] from, double[] to, double stepNorm)
        {
            double dist = ConfigDistance(from, to);
            if (dist <= stepNorm) return (double[])to.Clone();
            double k = stepNorm / dist;
            var q = new double[v.Dof];
            for (int i = 0; i < v.Dof; i++) q[i] = from[i] + (to[i] - from[i]) * k;
            return q;
        }

        private List<double[]> PlanBiRrt(double[] start, double[] goal, int seed)
        {
            var rng = new System.Random(seed * 31 + 7);
            var treeA = new List<double[]> { (double[])start.Clone() };
            var treeB = new List<double[]> { (double[])goal.Clone() };
            var parentA = new List<int> { -1 };
            var parentB = new List<int> { -1 };
            double step = stepSizeDeg / 90.0; // нормированный шаг

            LastIterations = 0;
            for (int it = 0; it < maxIterations; it++)
            {
                LastIterations = it;
                double[] qRand = Sample(rng);
                if (it % 4 == 0) qRand = (double[])treeB[treeB.Count - 1].Clone(); // goal bias

                int ia = Nearest(treeA, qRand);
                double[] qNew = Steer(treeA[ia], qRand, step);
                if (!Free(qNew, out _)) continue;
                treeA.Add(qNew); parentA.Add(ia);

                int ib = Nearest(treeB, qNew);
                double[] qConn = Steer(treeB[ib], qNew, step);
                if (!Free(qConn, out _)) continue;
                treeB.Add(qConn); parentB.Add(ib);

                if (ConfigDistance(qNew, qConn) < step * 1.5)
                {
                    // Сшиваем: A → qNew → qConn → B
                    var pathA = Extract(treeA, parentA, treeA.Count - 1);
                    var pathB = Extract(treeB, parentB, treeB.Count - 1);
                    pathB.Reverse();
                    pathA.AddRange(pathB);
                    return pathA;
                }
            }
            return null;
        }

        private double[] Sample(System.Random rng)
        {
            var q = new double[v.Dof];
            for (int i = 0; i < v.Dof; i++)
                q[i] = v.Lower[i] + rng.NextDouble() * (v.Upper[i] - v.Lower[i]);
            return q;
        }

        private int Nearest(List<double[]> tree, double[] q)
        {
            int best = 0;
            double bestD = double.MaxValue;
            for (int i = 0; i < tree.Count; i++)
            {
                double d = ConfigDistance(tree[i], q);
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        private static List<double[]> Extract(List<double[]> tree, List<int> parent, int idx)
        {
            var path = new List<double[]>();
            while (idx >= 0)
            {
                path.Add(tree[idx]);
                idx = parent[idx];
            }
            path.Reverse();
            return path;
        }

        /// <summary>Short-cut сглаживание: выкидываем промежуточные точки, если прямая свободна.</summary>
        private List<double[]> Shortcut(List<double[]> path, int seed)
        {
            var rng = new System.Random(seed * 131 + 17);
            for (int attempt = 0; attempt < path.Count * 3; attempt++)
            {
                if (path.Count < 3) break;
                int i = rng.Next(0, path.Count - 1);
                int j = rng.Next(i + 1, path.Count);
                if (j - i < 2) continue;
                if (SegmentFree(path[i], path[j])) path.RemoveRange(i + 1, j - i - 1);
            }
            return path;
        }

        /// <summary>
        /// Проверка ребра с АДАПТИВНЫМ шагом: Δq ≤ δ / (2·max‖J‖∞), т.е. смещение
        /// любой точки звена на шаге не превышает половины требуемого зазора —
        /// это исключает «проскок» сквозь препятствие (дискретизация по расстоянию).
        /// </summary>
        private bool SegmentFree(double[] a, double[] b)
        {
            float jn = v.MaxJacobianNorm(a);
            double maxStepNorm = Mathf.Max(0.002f, clearance * 0.5f / Mathf.Max(1e-6f, jn));
            int steps = Mathf.Max(2, Mathf.CeilToInt((float)(ConfigDistance(a, b) / maxStepNorm)));
            steps = Mathf.Min(steps, 400);
            for (int s = 1; s < steps; s++)
            {
                double k = (double)s / steps;
                var q = new double[v.Dof];
                for (int i = 0; i < v.Dof; i++) q[i] = a[i] + (b[i] - a[i]) * k;
                if (!Free(q, out _)) return false;
            }
            return true;
        }

        /// <summary>Параметризация времени (трапеция) + ресемплирование с фиксированным шагом.</summary>
        private PlannedTrajectory BuildTrajectory(List<double[]> path, Vector3 goalPoint)
        {
            var durations = new double[path.Count - 1];
            double total = 0;
            for (int i = 0; i + 1 < path.Count; i++)
            {
                double seg = 0;
                for (int j = 0; j < v.Dof; j++)
                {
                    double dq = System.Math.Abs(path[i + 1][j] - path[i][j]);
                    double vmax = System.Math.Max(1e-3, v.VelMax[j]);
                    double t = dq / vmax + vmax * accelShare / (vmax * 2.0);
                    seg = System.Math.Max(seg, t);
                }
                durations[i] = System.Math.Max(seg, timeStep);
                total += durations[i];
            }

            var samples = new List<double[]>();
            var times = new List<float>();
            double tAcc = 0;
            for (int i = 0; i + 1 < path.Count; i++)
            {
                int steps = Mathf.Max(1, Mathf.RoundToInt((float)(durations[i] / timeStep)));
                for (int s = 0; s < steps; s++)
                {
                    double k = (double)s / steps;
                    var q = new double[v.Dof];
                    for (int j = 0; j < v.Dof; j++)
                        q[j] = path[i][j] + (path[i + 1][j] - path[i][j]) * k;
                    samples.Add(q);
                    times.Add((float)(tAcc + durations[i] * k));
                }
                tAcc += durations[i];
            }
            samples.Add((double[])path[path.Count - 1].Clone());
            times.Add((float)tAcc);

            var t2 = new PlannedTrajectory
            {
                Path = samples.ToArray(),
                Times = times.ToArray(),
                Time = tAcc
            };

            // Метрики: длина, минимальный зазор, запас лимитов, σ_min в цели.
            double len = 0;
            float minClear = float.MaxValue, minLimit = float.MaxValue;
            for (int i = 0; i < samples.Count; i++)
            {
                if (i > 0) len += ConfigDistance(samples[i - 1], samples[i]);
                float c = v.ClearanceAt(samples[i], world, out _, out _);
                float self = v.SelfClearance(samples[i], out _, out _);
                minClear = Mathf.Min(minClear, Mathf.Min(c, self));
                minLimit = Mathf.Min(minLimit, v.LimitMargin(samples[i]));
            }
            t2.Length = len;
            t2.MinClearance = minClear == float.MaxValue ? 0f : minClear;
            t2.LimitMargin = minLimit == float.MaxValue ? 0f : minLimit;

            double[][] jac = KinematicsJacobian.Allocate(v.Dof);
            KinematicsJacobian.Compute(v, path[path.Count - 1], jac, out _);
            // σ_min в м/град (нормируем радианы → градусы, чтобы порог 0.05 был осмысленным)
            t2.SigmaMin = KinematicsJacobian.SigmaMin(jac, v.Dof) * 57.29578;
            _ = goalPoint;
            return t2;
        }
    }
}
