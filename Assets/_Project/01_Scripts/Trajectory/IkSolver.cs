using System.Collections.Generic;
using UnityEngine;

namespace TrajectoryCore
{
    /// <summary>Метки ветви решения IK (для «posture locking» и гистерезиса).</summary>
    public struct IkBranchTag
    {
        public bool shoulderFar;   // база повёрнута на 180°
        public bool elbowDown;     // локоть «вниз» (вторая ветвь)
        public bool wristFlip;     // запястье перевёрнуто
        public override string ToString()
        {
            return (shoulderFar ? "S+" : "S-") + "," + (elbowDown ? "E+" : "E-") +
                   "," + (wristFlip ? "W+" : "W-");
        }
    }

    public struct IkSolution
    {
        public double[] q;
        public IkBranchTag tag;
        public double fkError;      // ошибка прямой задачи по TCP, м
        public bool withinLimits;
    }

    /// <summary>
    /// Аналитическая IK (MVP, Проблема 1): для 6-осевого робота с «сферическим»
    /// запястьем (оси 4-5-6 пересекаются) даём ВСЕ ветви: 2 (плечо) × 2 (локоть) ×
    /// 2 (переворот запястья) = до 8 решений, плюс резервный многостартовый CCD.
    ///
    /// Вывод формул: позиционная часть — 2R в плоскости руки (теорема косинусов),
    /// база — доворот плоскости на цель (q1 = atan2 в плоскости ⊥ вертикали),
    /// запястье — roll-pitch-roll, переворот: (q4,q5,q6) → (q4+π, −q5, q6+π).
    /// Нулевые углы модели учитываются через смещения q2_off/q3_off, замеренные
    /// в стартовой позе.
    /// </summary>
    public class IkSolver
    {
        public float positionTolerance = 0.004f;  // 4 мм
        public int ccdIterations = 120;
        public int ccdSeeds = 6;

        private PoseValidator v;
        public bool Ready { get; private set; }

        // Замеренная геометрия 6-осевого
        private Vector3 baseOrigin, up, rRef, nRef;
        private float a2, a3;
        private float q2OffsetDeg, q3OffsetDeg;

        public void Init(PoseValidator validator)
        {
            v = validator;
            Ready = false;
            if (v == null || !v.Ready || v.Dof < 6) return;

            double[] q0 = new double[v.Dof]; // стартовая (нулевая) поза
            Vector3 baseP = v.PivotAt(0, q0);
            Vector3 shoulder = v.PivotAt(1, q0);
            Vector3 elbow = v.PivotAt(2, q0);
            Vector3 wrist = v.PivotAt(3, q0);

            baseOrigin = baseP;
            up = v.AxisWorld(0, q0);                       // ось J1 — вертикаль
            Vector3 armDir = Vector3.ProjectOnPlane(wrist - elbow, up);
            if (armDir.sqrMagnitude < 1e-6f) return;
            rRef = armDir.normalized;                      // радиальное направление покоя
            nRef = Vector3.Cross(up, rRef).normalized;     // нормаль плоскости руки

            a2 = Vector3.Distance(shoulder, elbow);        // плечо
            a3 = Vector3.Distance(elbow, wrist);           // предплечье

            // Смещения нулей: считаем формулы в стартовой позе и запоминаем разницу.
            Vector3 d = elbow - shoulder;
            float rho = new Vector2(Vector3.Dot(d, rRef), Vector3.Dot(d, nRef)).magnitude;
            float sigma = Vector3.Dot(d, up);
            float phi1Rest = Mathf.Atan2(sigma, rho) * Mathf.Rad2Deg;
            Vector3 f = wrist - elbow;
            float phi2Rest = Mathf.Atan2(Vector3.Dot(f, up),
                new Vector2(Vector3.Dot(f, rRef), Vector3.Dot(f, nRef)).magnitude) * Mathf.Rad2Deg;
            q2OffsetDeg = 0f - (phi1Rest - 90f);
            q3OffsetDeg = 0f - (phi2Rest - phi1Rest);
            Ready = true;
        }

        /// <summary>Все ветви аналитической IK; при неудаче — многостартовый CCD.</summary>
        public List<IkSolution> SolveAll(Vector3 target, double[] seed)
        {
            var result = new List<IkSolution>();
            if (v == null || seed == null) return result;
            if (v.Dof == 3) return SolveAllScara(target, seed);   // SCARA: 2R + призма
            if (!Ready || seed.Length < 6) return result;

            Vector3 shoulder = v.PivotAt(1, new double[v.Dof]);
            Vector3 d = target - shoulder;
            Vector3 planar = Vector3.ProjectOnPlane(d, up);
            float sigma = Vector3.Dot(d, up);

            // Азимут цели относительно плоскости покоя.
            float az = Mathf.Atan2(Vector3.Dot(planar, nRef), Vector3.Dot(planar, rRef)) * Mathf.Rad2Deg;

            for (int s = 0; s < 2; s++)               // плечо: 0 — «ближняя», 1 — разворот на 180°
            {
                bool shoulderFar = s == 1;
                float q1 = az + (shoulderFar ? 180f : 0f);
                float rho = planar.magnitude * (shoulderFar ? -1f : 1f);

                float r = Mathf.Sqrt(rho * rho + sigma * sigma);
                float cosRel = (r * r - a2 * a2 - a3 * a3) / (2f * a2 * a3);
                if (Mathf.Abs(cosRel) > 1f) continue;  // вне рабочей зоны

                float relAbs = Mathf.Acos(Mathf.Clamp(cosRel, -1f, 1f)) * Mathf.Rad2Deg;
                for (int e = 0; e < 2; e++)
                {
                    bool elbowDown = e == 1;
                    float rel = elbowDown ? -relAbs : relAbs;
                    float psi = Mathf.Atan2(sigma, rho) * Mathf.Rad2Deg;
                    float phi1 = psi - Mathf.Atan2(a3 * Mathf.Sin(rel * Mathf.Deg2Rad),
                                                   a2 + a3 * Mathf.Cos(rel * Mathf.Deg2Rad)) * Mathf.Rad2Deg;

                    double q2 = phi1 - 90f + q2OffsetDeg;
                    double q3 = rel + q3OffsetDeg;

                    for (int w = 0; w < 2; w++)         // запястье: базовая / перевёрнутая
                    {
                        bool wristFlip = w == 1;
                        var q = (double[])seed.Clone();
                        q[0] = q1; q[1] = q2; q[2] = q3;
                        if (wristFlip)
                        {
                            q[3] += 180.0;
                            q[4] = -q[4];
                            q[5] += 180.0;
                        }

                        var sol = new IkSolution
                        {
                            q = q,
                            tag = new IkBranchTag { shoulderFar = shoulderFar, elbowDown = elbowDown, wristFlip = wristFlip }
                        };
                        sol.withinLimits = v.WithinLimits(q, 0f);
                        sol.fkError = (v.TcpAt(q) - target).magnitude;
                        if (sol.fkError <= positionTolerance * 5f) result.Add(sol);
                    }
                }
            }

            if (result.Count == 0) result.AddRange(CcdFallback(target, seed));
            return result;
        }

        /// <summary>
        /// SCARA (q = (θ1, θ2, z)): аналитика 2R + призматическая ось.
        /// θ1 — азимут на цель, θ2 = ±acos(...) (2 ветви локтя), z — кламп по ходу.
        /// </summary>
        private List<IkSolution> SolveAllScara(Vector3 target, double[] seed)
        {
            var list = new List<IkSolution>();
            if (v.Dof != 3) return list;

            // Геометрия: плечо (J1) → локоть (J2) → z_5.
            double[] zero = new double[3];
            Vector3 shoulder = v.PivotAt(0, zero);
            Vector3 elbow0 = v.PivotAt(1, zero);
            Vector3 wrist0 = v.PivotAt(2, zero);
            Vector3 up = v.AxisWorld(0, zero);
            Vector3 ref1 = Vector3.ProjectOnPlane(elbow0 - shoulder, up);   // направление звена 1 в покое
            Vector3 ref2 = Vector3.ProjectOnPlane(wrist0 - elbow0, up);     // направление звена 2 в покое
            float a1 = ref1.magnitude, a2 = ref2.magnitude;
            if (a1 < 1e-4f || a2 < 1e-4f) return list;
            ref1.Normalize(); ref2.Normalize();
            float restAngle2 = Vector3.SignedAngle(ref1, ref2, up);        // нулевой угол локтя

            Vector3 d = Vector3.ProjectOnPlane(target - shoulder, up);
            float r = d.magnitude;
            float cosQ2 = (r * r - a1 * a1 - a2 * a2) / (2f * a1 * a2);
            if (Mathf.Abs(cosQ2) > 1f) return list;
            float q2Abs = Mathf.Acos(Mathf.Clamp(cosQ2, -1f, 1f)) * Mathf.Rad2Deg;
            float az = Vector3.SignedAngle(ref1, d, up);

            float height = Vector3.Dot(target - shoulder, up)
                         + Vector3.Dot(shoulder - v.PivotAt(0, zero), up); // высота относительно базы
            double zTarget = System.Math.Min(System.Math.Max(height, v.Lower[2]), v.Upper[2]);

            for (int e = 0; e < 2; e++)
            {
                bool elbowDown = e == 1;
                float q2rel = elbowDown ? -q2Abs : q2Abs;
                float phi = Mathf.Atan2(a2 * Mathf.Sin(q2rel * Mathf.Deg2Rad),
                                        a1 + a2 * Mathf.Cos(q2rel * Mathf.Deg2Rad)) * Mathf.Rad2Deg;
                double q1 = az - phi;
                double q2 = q2rel - restAngle2;

                var q = (double[])seed.Clone();
                q[0] = q1; q[1] = q2; q[2] = zTarget;
                var sol = new IkSolution
                {
                    q = q,
                    tag = new IkBranchTag { shoulderFar = false, elbowDown = elbowDown, wristFlip = false },
                    withinLimits = v.WithinLimits(q, 0f)
                };
                sol.fkError = (v.TcpAt(q) - target).magnitude;
                list.Add(sol);
            }
            return list;
        }

        /// <summary>Резерв: CCD из нескольких сидов (текущая поза, «дом», зеркала по знаку).</summary>
        private List<IkSolution> CcdFallback(Vector3 target, double[] seed)
        {
            var list = new List<IkSolution>();
            for (int k = 0; k < ccdSeeds; k++)
            {
                var s = (double[])seed.Clone();
                if (k == 1) { s[1] -= 40; s[2] += 40; }
                if (k == 2) { s[1] += 40; s[2] -= 40; }
                if (k == 3) { s[0] += 180; }
                if (k == 4) { s[4] = -s[4]; s[3] += 180; s[5] += 180; }
                if (k == 5) { s[1] -= 70; s[2] += 70; s[4] = -20; }

                if (!v.SolveIk(target, s, out double[] q, ccdIterations, positionTolerance))
                    continue;
                list.Add(new IkSolution
                {
                    q = q,
                    tag = new IkBranchTag { shoulderFar = k == 3, elbowDown = k == 5, wristFlip = k == 4 },
                    withinLimits = v.WithinLimits(q, 0f),
                    fkError = (v.TcpAt(q) - target).magnitude
                });
                break; // первого успешного достаточно как запасной вариант
            }
            return list;
        }
    }
}
