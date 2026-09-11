using UnityEngine;

namespace TrajectoryCore
{
    /// <summary>
    /// Селектор позы (Проблема 1): выбирает «удобную» конфигурацию из всех ветвей IK
    /// по функции стоимости + гистерезис (posture locking).
    ///
    /// S(q) = w_home·||q−q_home||²_W + w_lim·Σφ_lim(q_i) + w_sing·φ_sing(q)
    ///      + w_jump·||q−q_prev||²_W + w_fk·(ошибка FK)²
    ///   φ_lim = ((qmax−qmin)²)/((qmax−q)(q−qmin))           (барьер, Chan–Dubey)
    ///   φ_sing = (σ_th/σ_min)² при σ_min<σ_th                (SVD/степенной метод)
    ///   W = diag(1/range_i²)                                (нормировка единиц)
    ///
    /// Переключение ветви — только если новая лучше более чем на Δ (гистерезис),
    /// иначе робот «дёргается» между ветвями.
    /// </summary>
    public class PostureSelector
    {
        public float wHome = 0.35f;
        public float wLimit = 1.0f;
        public float wSing = 0.6f;
        public float wJump = 1.0f;
        public float wFk = 500f;
        public float sigmaThreshold = 0.05f;   // м/град (после нормировки Якобиана)
        public float hysteresis = 0.15f;       // 15 % — порог смены ветви

        private PoseValidator v;
        private double[] home;
        private double[] ranges;
        private double[][] jac;
        private IkBranchTag lockedTag;
        private bool hasLock;

        public bool Ready { get; private set; }

        public void Init(PoseValidator validator, double[] homePose = null)
        {
            v = validator;
            Ready = v != null && v.Ready;
            if (!Ready) return;
            home = homePose != null ? (double[])homePose.Clone() : new double[v.Dof];
            ranges = new double[v.Dof];
            for (int i = 0; i < v.Dof; i++)
            {
                double r = v.Upper[i] - v.Lower[i];
                ranges[i] = r > 1e-6 ? r : 1.0;
            }
            jac = KinematicsJacobian.Allocate(v.Dof);
            hasLock = false;
        }

        public void ResetLock() { hasLock = false; }

        /// <summary>Стоимость конфигурации; меньше — лучше.</summary>
        public double Cost(double[] q, double[] qPrev)
        {
            if (!Ready || q == null) return double.MaxValue;
            double s = 0;

            // Нормированное отклонение и барьеры лимитов
            for (int i = 0; i < v.Dof; i++)
            {
                double lo = (q[i] - v.Lower[i]) / ranges[i];   // 0..1 от нижнего лимита
                double hi = (v.Upper[i] - q[i]) / ranges[i];   // 0..1 от верхнего
                s += wLimit * 1.0 / Mathf.Max(1e-6f, (float)(lo * hi));   // барьер: в центре ~4
                double dh = (q[i] - home[i]) / ranges[i];
                s += wHome * dh * dh;
                if (qPrev != null)
                {
                    double dj = (q[i] - qPrev[i]) / ranges[i];
                    s += wJump * dj * dj;
                }
            }

            // Сингулярность
            KinematicsJacobian.Compute(v, q, jac, out _);
            double sigmaMin = KinematicsJacobian.SigmaMin(jac, v.Dof) * 57.29578; // → м/град
            if (sigmaMin < sigmaThreshold)
            {
                double ratio = sigmaThreshold / Mathf.Max(1e-6f, (float)sigmaMin);
                s += wSing * ratio * ratio;
            }
            LastSigmaMin = (float)sigmaMin;
            LastManipulability = (float)KinematicsJacobian.Manipulability(jac, v.Dof);
            return s;
        }

        public float LastSigmaMin { get; private set; }
        public float LastManipulability { get; private set; }

        /// <summary>Выбор решения с гистерезисом: держимся за ветвь, пока она адекватна.</summary>
        public IkSolution Select(System.Collections.Generic.List<IkSolution> candidates, double[] qPrev)
        {
            IkSolution best = default;
            double bestCost = double.MaxValue;
            double lockedCost = double.MaxValue;
            bool foundLocked = false;

            foreach (IkSolution c in candidates)
            {
                if (!c.withinLimits) continue;
                double cost = Cost(c.q, qPrev) + wFk * c.fkError * c.fkError;
                if (cost < bestCost) { bestCost = cost; best = c; }
                if (hasLock && SameBranch(c.tag, lockedTag)) { lockedCost = cost; foundLocked = true; }
            }

            if (bestCost == double.MaxValue) return best;             // ничего валидного
            if (foundLocked && bestCost > lockedCost * (1.0 - hysteresis))
                return best;                                          // смена ветви оправдана
            if (foundLocked && !SameBranch(best.tag, lockedTag))
            {
                // держимся за прежнюю ветвь (ищем её в списке)
                foreach (IkSolution c in candidates)
                    if (c.withinLimits && SameBranch(c.tag, lockedTag)) return c;
            }
            lockedTag = best.tag;
            hasLock = true;
            return best;
        }

        private static bool SameBranch(IkBranchTag a, IkBranchTag b)
        {
            return a.shoulderFar == b.shoulderFar && a.elbowDown == b.elbowDown && a.wristFlip == b.wristFlip;
        }
    }
}
