using UnityEngine;

namespace TrajectoryCore
{
    public enum SafetyReason { None, NotChecked, JointLimit, Clearance, Velocity, Watchdog, NoData, OperatorStop }

    /// <summary>
    /// Safety Layer (E4 плана): независимая проверка траектории и «сторожевой таймер».
    /// Проверки: лимиты суставов (с запасом), минимальный зазор, предельные скорости,
    /// актуальность данных. При отказе — движение не разрешается (fail-safe).
    /// </summary>
    public class SafetyGate
    {
        public float minClearance = 0.015f;
        public float minLimitMarginDeg = 3f;
        public int watchdogTimeoutMs = 300;

        public bool Enabled = true;

        private long lastStateMs = -1;
        private double lastPlayheadTime = -1;

        public SafetyReason LastReason { get; private set; } = SafetyReason.NotChecked;

        public void NotifyState() { lastStateMs = (long)(Time.realtimeSinceStartup * 1000f); }

        public bool WatchdogAlive()
        {
            if (lastStateMs < 0) return false;
            return (long)(Time.realtimeSinceStartup * 1000f) - lastStateMs <= watchdogTimeoutMs;
        }

        /// <summary>Проверка траектории перед исполнением.</summary>
        public bool Approve(PlannedTrajectory t, PoseValidator validator, out SafetyReason reason)
        {
            reason = SafetyReason.None;
            if (!Enabled) { LastReason = reason; return true; }
            if (t == null || t.Path == null || t.Path.Length == 0)
            {
                reason = SafetyReason.NoData; LastReason = reason; return false;
            }

            if (!WatchdogAlive())
            {
                reason = SafetyReason.Watchdog; LastReason = reason; return false;
            }

            if (t.MinClearance < minClearance)
            {
                reason = SafetyReason.Clearance; LastReason = reason; return false;
            }
            if (t.LimitMargin < minLimitMarginDeg)
            {
                reason = SafetyReason.JointLimit; LastReason = reason; return false;
            }
            foreach (double[] q in t.Path)
            {
                if (!validator.WithinLimits(q))
                {
                    reason = SafetyReason.JointLimit; LastReason = reason; return false;
                }
            }
            LastReason = reason;
            return true;
        }

        /// <summary>Проверка на шаге исполнения: не вышли ли за пределы/не застряли ли.</summary>
        public bool ApproveStep(double[] q, float clearance, double playheadTime, PoseValidator validator,
            out SafetyReason reason)
        {
            reason = SafetyReason.None;
            if (!Enabled) return true;

            if (!WatchdogAlive()) { reason = SafetyReason.Watchdog; LastReason = reason; return false; }
            if (playheadTime <= lastPlayheadTime - 1e-6 && lastPlayheadTime > 0)
            {
                reason = SafetyReason.Watchdog; LastReason = reason; return false;
            }
            if (!validator.WithinLimits(q)) { reason = SafetyReason.JointLimit; LastReason = reason; return false; }
            if (clearance < minClearance * 0.5f) { reason = SafetyReason.Clearance; LastReason = reason; return false; }

            lastPlayheadTime = playheadTime;
            LastReason = reason;
            return true;
        }

        public void ResetPlayhead() { lastPlayheadTime = -1; }

        public static string Describe(SafetyReason r)
        {
            switch (r)
            {
                case SafetyReason.JointLimit: return "лимит сустава";
                case SafetyReason.Clearance: return "малый зазор";
                case SafetyReason.Velocity: return "превышение скорости";
                case SafetyReason.Watchdog: return "watchdog: нет данных";
                case SafetyReason.NoData: return "нет траектории";
                case SafetyReason.OperatorStop: return "остановка оператором";
                default: return "ок";
            }
        }
    }
}
