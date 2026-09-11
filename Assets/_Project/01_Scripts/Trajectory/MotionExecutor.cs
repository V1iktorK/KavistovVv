using TrajectoryCore;
using UnityEngine;

/// <summary>
/// Исполнитель движения с настраиваемой скоростью (MVP: 1 м за 20 с).
/// Оборачивает TrajectoryExecutor: пересчитывает времена траектории под заданную
/// скорость TCP (м/с) и при необходимости доводит робота до выбранной ветви IK.
/// </summary>
public class MotionExecutor : MonoBehaviour
{
    public float speedMps = 0.05f;      // 0.05 м/с = 1 метр за 20 секунд
    public float maxSpeedMps = 0.5f;

    private TrajectoryExecutor executor;
    private PoseValidator validator;
    private CollisionWorld world;
    private SafetyGate gate;

    public bool IsRunning => executor != null && executor.IsRunning;

    public void Bind(TrajectoryExecutor exec, PoseValidator v, CollisionWorld w, SafetyGate g)
    {
        executor = exec;
        validator = v;
        world = w;
        gate = g;
    }

    /// <summary>Запустить движение по траектории; branchQ — выбранная конфигурация (ветвь IK).</summary>
    public bool Play(PlannedTrajectory plan, double[] branchQ)
    {
        if (executor == null || plan == null) return false;

        // Приводим времена к заданной скорости инструмента.
        float length = 0f;
        if (plan.Path != null && validator != null && validator.Ready)
        {
            Vector3 prev = validator.TcpAt(plan.Path[0]);
            for (int i = 1; i < plan.Path.Length; i++)
            {
                Vector3 cur = validator.TcpAt(plan.Path[i]);
                length += Vector3.Distance(prev, cur);
                prev = cur;
            }
        }
        MotionTiming.RescaleToSpeed(plan, length, Mathf.Clamp(speedMps, 0.005f, maxSpeedMps));

        // Финальная точка траектории — выбранная ветвь (чтобы поза совпала с фантомом).
        if (branchQ != null && plan.Path != null && plan.Path.Length > 0)
            plan.Path[plan.Path.Length - 1] = (double[])branchQ.Clone();

        gate?.NotifyState();
        return executor.Play(plan);
    }

    public void Stop() => executor?.Stop(SafetyReason.OperatorStop);

    public void SetSpeed(float mps) => speedMps = Mathf.Clamp(mps, 0.005f, maxSpeedMps);
}
