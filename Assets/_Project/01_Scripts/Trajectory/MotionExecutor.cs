using KazistovVvFeatures;
using TrajectoryCore;
using UnityEngine;

/// <summary>
/// Исполнитель движения с настраиваемой скоростью (MVP: 1 м за 20 с).
/// Оборачивает TrajectoryExecutor: пересчитывает времена траектории под заданную
/// скорость TCP (м/с) и при необходимости доводит робота до выбранной ветви IK.
///
/// ФИКС §27 — ПЛАВНОСТЬ ЛЮБОГО ДВИЖЕНИЯ (жалоба оператора: «роботы не плавно достигают
/// конечной точки, а резко дёргаются где-то до середины траектории, а далее ведут себя
/// плавно»). Обёртка — ЕДИНСТВЕННАЯ точка входа для ЛЮБОГО движения робота: и шаг 5
/// основного потока (`TrajectoryFlowController.SelectPhantom`), и все внешние планы
/// (`PlayExternalPlan`: «перейти в позу», «домой», Pick&amp;Place, макросы, маршрут через
/// waypoints, пульт/планшет/голос/жест).
///
/// ДО ФИКСА: у планов планировщика профиль времени уже был аналитическим S-профилем
/// (`Planner.ApplySProfile`), а у планов «кнопок» — НЕТ. `KvPlanKit.MakeJointPlan` и
/// `KvWaypoints.Merge` задавали времена ЛИНЕЙНО по номеру сэмпла (`Times[i] = u · T`),
/// а `RescaleToSpeed` только растягивает время, форму профиля не меняя. В итоге исполнитель
/// (`TrajectoryExecutor`) интерполировал между сэмплами ЛИНЕЙНО, то есть внутри сэмпла
/// скорость постоянна, а на КАЖДОЙ границе сэмплов мгновенно меняется. При 40 сэмплах
/// (`MakeJointPlan`) и smoothstep-геометрии пути скорость растёт как 6u(1−u): у начала и
/// конца парабола крутая, поэтому скачок скорости на первых границах — до ×2 (например,
/// 10 → 20 °/с за один сэмпл) — это и есть видимый «дёрг». Ближе к середине парабола
/// выполаживается, скачки становятся малыми — «а далее ведут себя плавно». Ровно то,
/// что описал оператор.
///
/// СТАЛО: перед проигрыванием ЛЮБОЙ план перепараметризуется тем же ядром, что и
/// планировщик с постобработкой — `KvTrajMath.Retime` (внутри аналитический
/// `KvTrajMath.SProfileBuild`: разгон → крейсер → торможение с ограничением скорости,
/// ускорения И рывка). Путь не меняется — меняется только распределение времени, поэтому
/// кинематика, зазоры и SafetyGate остаются ровно теми же. Для планов планировщика
/// операция ИДЕМПОТЕНТНА (Retime — чистая функция пути и лимитов, исходные `Times` не
/// читаются), поэтому поведение основного потока не меняется; для планов «кнопок»
/// движение становится таким же плавным, как у планировщика.
/// </summary>
public class MotionExecutor : MonoBehaviour
{
    public float speedMps = 0.05f;      // 0.05 м/с = 1 метр за 20 секунд
    public float maxSpeedMps = 0.5f;

    [Tooltip("Пределы скорости/ускорения/рывка для перепараметризации плана. Пусто — берётся " +
             "общий набор этапов (KvStageHub2.Limits), а если и его нет — типовые значения " +
             "KvMotionLimits (90 °/с · 180 °/с² · 1200 °/с³).")]
    [System.NonSerialized]        // KvMotionLimits — обычный класс, а не ассет: сериализовать нечего
    public KvMotionLimits motionLimits;

    [Tooltip("Шаг уплотнения пути перед проигрыванием, в нормированных единицах (1 единица ≈ 90° " +
             "вращательного сустава или 10 см призматического). 0.004 ≈ 0.36° на сэмпл: «ступенька» " +
             "скорости на границе сэмплов становится невидимой. 0 — уплотнение выключено.")]
    public double densifyStep = KazistovVvFeatures.KvTrajMath.DefaultDensifyStep;

    [Tooltip("Верхний предел числа сэмплов после уплотнения (защита от гигантских путей).")]
    public int densifyMaxSamples = 2400;

    private TrajectoryExecutor executor;
    private PoseValidator validator;
    private CollisionWorld world;
    private SafetyGate gate;

    public bool IsRunning => executor != null && executor.IsRunning;

    /// <summary>Движение на паузе (робот стоит, траектория НЕ завершается) — кнопка UI.</summary>
    public bool Paused => executor != null && executor.Paused;

    /// <summary>Поставить/снять паузу движения (State Machine это не затрагивает).</summary>
    public void SetPaused(bool value)
    {
        if (executor != null) executor.SetPaused(value);
    }

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

        // 1. Финальная точка траектории — выбранная ветвь (чтобы поза совпала с фантомом).
        //    ВАЖНО (ФИКС §27): подмена делается ДО расчёта длины и профиля времени.
        //    Раньше конечный сэмпл подменялся ПОСЛЕ `RescaleToSpeed`, то есть последний
        //    участок пути получал время, посчитанное по СТАРОЙ длине пути.
        if (branchQ != null && plan.Path != null && plan.Path.Length > 0)
            plan.Path[plan.Path.Length - 1] = (double[])branchQ.Clone();

        // 2. Профиль времени: аналитический S-профиль (см. описание класса).
        ApplySProfile(plan);

        // 3. Приводим времена к заданной скорости инструмента (общий масштаб,
        //    форму профиля из шага 2 не меняет).
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

        gate?.NotifyState();
        return executor.Play(plan);
    }

    /// <summary>
    /// Перепараметризовать время плана и УПЛОТНИТЬ его путь — так, чтобы исполнитель
    /// (`TrajectoryExecutor`) вёл робота плавно. Два шага, оба ничего не меняют в геометрии:
    ///
    ///   1) УПЛОТНЕНИЕ (`KvTrajMath.Densify`). Исполнитель интерполирует позу между сэмплами
    ///      ЛИНЕЙНО, поэтому на каждой границе сэмплов мгновенная скорость меняется скачком.
    ///      При 40 сэмплах (`KvPlanKit.MakeJointPlan`, `KvWaypoints.Merge`) скачок читается
    ///      глазом как дёрг. Уплотнение вставляет сэмплы ВДОЛЬ ТОЙ ЖЕ полилинии — путь не
    ///      меняется, а скачок уменьшается пропорционально.
    ///   2) S-ПРОФИЛЬ (`KvTrajMath.Retime` → аналитический `SProfileBuild`, как у планировщика
    ///      и постобработки): ограничены скорость, ускорение И рывок.
    ///
    /// Любая неудача (валидатор не готов, путь короче трёх сэмплов, профиль не построился) —
    /// поведение прежнее: играем с теми временами, что пришли.
    /// </summary>
    private void ApplySProfile(PlannedTrajectory plan)
    {
        if (plan == null || plan.Path == null || validator == null || !validator.Ready) return;
        if (plan.Path.Length < 3) return;                  // S-профиль строится от трёх сэмплов

        // 1. Уплотнение пути (геометрия не меняется — та же полилиния, больше сэмплов).
        PlannedTrajectory dense = KvTrajMath.Densify(validator, plan, densifyStep, densifyMaxSamples);
        if (dense != null && dense.Path != null && dense.Path.Length > plan.Path.Length)
        {
            plan.Path = dense.Path;
            plan.Times = dense.Times;
            plan.Time = dense.Time;
        }

        // 2. Аналитический S-профиль по уплотнённому пути.
        KvMotionLimits limits = motionLimits;
        if (limits == null)
        {
            // Тот же набор, что у планировщика и постобработки (единый источник правды).
            KvStageHub2 hub = KvStageHub2.Current;
            if (hub != null && hub.Limits != null) limits = hub.Limits;
        }
        if (limits == null) limits = new KvMotionLimits();

        PlannedTrajectory timed = KvTrajMath.Retime(validator, plan, limits, 1f, 1f,
            plan.Label, true, false);
        if (timed == null || timed.Times == null) return;
        if (timed.Times.Length != plan.Path.Length) return;

        plan.Times = timed.Times;
        plan.Time = timed.Time;
        plan.BranchTag = AppendTag(plan.BranchTag, "S-профиль (рывок ограничен)");
    }

    private static string AppendTag(string tag, string add)
    {
        if (string.IsNullOrEmpty(tag)) return add;
        if (tag.Contains(add)) return tag;
        return tag + " · " + add;
    }

    public void Stop() => executor?.Stop(SafetyReason.OperatorStop);

    public void SetSpeed(float mps) => speedMps = Mathf.Clamp(mps, 0.005f, maxSpeedMps);
}
