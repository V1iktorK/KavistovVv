using TrajectoryCore;
using UnityEngine;

/// <summary>
/// Исполнитель траекторий (E4 плана): проигрывает q(t) по времени, каждый шаг
/// проверяется SafetyGate (лимиты/зазор/watchdog). При отказе — немедленный стоп.
/// </summary>
public class TrajectoryExecutor : MonoBehaviour
{
    public float timeScale = 1f;              // можно замедлить (0.5 = вдвое медленнее)

    private PoseValidator validator;
    private CollisionWorld world;
    private SafetyGate gate;
    private PlannedTrajectory active;
    private double playhead;
    private int index;
    private bool running;

    public bool IsRunning => running;
    public string StatusText { get; private set; } = "";

    public void Init(PoseValidator v, CollisionWorld w, SafetyGate g)
    {
        validator = v;
        world = w;
        gate = g;
    }

    public bool Play(PlannedTrajectory t)
    {
        if (validator == null || !validator.Ready || t == null) return false;
        gate?.NotifyState();
        if (gate != null && !gate.Approve(t, validator, out SafetyReason reason))
        {
            StatusText = "Отказ Safety: " + SafetyGate.Describe(reason);
            Debug.LogWarning("[Safety] Траектория отклонена: " + StatusText);
            return false;
        }
        active = t;
        playhead = 0;
        index = 0;
        running = true;
        gate?.ResetPlayhead();
        StatusText = t.Label;
        return true;
    }

    public void Stop(SafetyReason reason = SafetyReason.OperatorStop)
    {
        if (!running) return;
        running = false;
        active = null;
        StatusText = reason == SafetyReason.OperatorStop ? "остановлено" : "СТОП: " + SafetyGate.Describe(reason);
        if (reason != SafetyReason.OperatorStop)
            Debug.LogWarning("[Safety] Аварийный стоп: " + SafetyGate.Describe(reason));
    }

    private void Update()
    {
        if (!running || active == null) return;
        gate?.NotifyState(); // живые данные для watchdog

        playhead += Time.deltaTime * Mathf.Max(0.05f, timeScale);

        // Находим текущий сэмпл.
        float[] times = active.Times;
        while (index + 1 < times.Length && times[index + 1] <= playhead) index++;

        double[] q;
        if (index + 1 >= times.Length)
        {
            q = active.Path[active.Path.Length - 1];
        }
        else
        {
            float t0 = times[index], t1 = times[index + 1];
            float k = t1 > t0 ? Mathf.Clamp01((float)((playhead - t0) / (t1 - t0))) : 0f;
            double[] a = active.Path[index], b = active.Path[index + 1];
            q = new double[a.Length];
            for (int i = 0; i < a.Length; i++) q[i] = a[i] + (b[i] - a[i]) * k;
        }

        // Валидация шага и применение.
        float clearance = validator.ClearanceAt(q, world, out _, out _);
        bool ok = true;
        if (gate != null)
            ok = gate.ApproveStep(q, clearance, playhead, validator, out SafetyReason reason);

        if (!ok)
        {
            Stop(gate != null ? gate.LastReason : SafetyReason.NoData);
            return;
        }

        validator.Apply(q);

        if (playhead >= times[times.Length - 1])
        {
            running = false;
            active = null;
            StatusText = "выполнено";
        }
    }
}
