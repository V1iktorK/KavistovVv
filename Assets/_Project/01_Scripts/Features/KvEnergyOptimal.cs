using System;
using System.Collections.Generic;
using UnityEngine;
using KazistovVvUI;
using TrajectoryCore;

namespace KazistovVvFeatures
{
    /// <summary>
    /// ЭТАП 6 ТЗ: ОПТИМИЗАЦИЯ ПО ЭНЕРГИИ.
    ///
    /// Симуляция ровно по ТЗ: энергия ∝ Σ |момент × угловая скорость| × время
    /// (момент — упрощённая динамика: инерция + вязкое трение + удержание груза, см.
    /// <see cref="KvEnergyModel"/>). Метрика «Энергоэффективность» — Дж и Дж/м плюс
    /// пиковая мощность: по ней видно, какой вариант бережнее к приводам (меньше износ
    /// реального робота).
    ///
    /// Поиск эко-профиля: путь НЕ меняется, подбирается распределение времени по длине
    /// пути — семейство профилей «мягче разгоны / ниже крейсерская скорость» с общим
    /// шагом. Для каждого профиля считается энергия, выбирается минимальная. Такой
    /// перебор честнее «угадывания» коэффициента: результат всегда не хуже исходного
    /// времени, а обычно заметно экономнее (падают пиковые ускорения и мощности).
    /// </summary>
    public class KvEnergyOptimal
    {
        public const string PayloadPrefsKey = "KazistovVv.Post.PayloadKg";

        public event Action<string> Message;

        /// <summary>Черновик эко-профиля: план, метрики и выигрыш по энергии.</summary>
        public class Draft
        {
            public PlannedTrajectory plan;
            public float energy;            // Дж
            public float energyPerMeter;    // Дж/м
            public float peakPower;         // Вт
            public float time;              // с
            public float originalEnergy;    // Дж (исходный профиль)
            public float originalPerMeter;
            public float originalPeakPower;
            public float originalTime;
            public float savings;           // % экономии энергии
            public float accelScale;
            public float velScale;
            public string key = "";
            public bool applied;
            public int trials;
        }

        // Семейство профилей: доля от лимитов ускорения и скорости.
        private static readonly float[] AccelScales = { 0.30f, 0.45f, 0.65f, 0.85f, 1.00f };
        private static readonly float[] VelScales = { 0.55f, 0.75f, 0.90f, 1.00f };

        private TrajectoryFlowController flow;
        private KvMotionLimits limits = new KvMotionLimits();
        private KvEnergyModel model = new KvEnergyModel();
        private bool loaded;

        private readonly Dictionary<TrajectoryCandidate, Draft> drafts =
            new Dictionary<TrajectoryCandidate, Draft>();
        private readonly Dictionary<TrajectoryCandidate, PlannedTrajectory> originals =
            new Dictionary<TrajectoryCandidate, PlannedTrajectory>();
        private Draft last;

        public KvEnergyModel Model { get { Load(); return model; } }
        public Draft Last { get { return last; } }
        public int DraftCount { get { return drafts.Count; } }

        /// <summary>Масса груза, кг (влияет и на энергию, и на поиск эко-профиля).</summary>
        public float PayloadKg
        {
            get { Load(); return model.payloadKg; }
            set
            {
                Load();
                model.payloadKg = Mathf.Clamp(value, 0f, 50f);
                PlayerPrefs.SetFloat(PayloadPrefsKey, model.payloadKg);
                PlayerPrefs.Save();
                Invalidate();
            }
        }

        public TrajectoryFlowController Flow() { return flow; }

        public void Bind(TrajectoryFlowController controller, KvMotionLimits motionLimits)
        {
            flow = controller;
            if (motionLimits != null) limits = motionLimits;
            Load();
        }

        private void Load()
        {
            if (loaded) return;
            loaded = true;
            model.payloadKg = Mathf.Clamp(PlayerPrefs.GetFloat(PayloadPrefsKey, model.payloadKg),
                0f, 50f);
        }

        public void Invalidate()
        {
            drafts.Clear();
            originals.Clear();
            last = null;
        }

        public void ResetCache()
        {
            Invalidate();
        }

        private string Signature(TrajectoryCandidate candidate)
        {
            return candidate.GetHashCode() + "|" + model.payloadKg.ToString("0.00") + "|" +
                   limits.maxVelDeg.ToString("0.0") + "|" + limits.maxAccDeg.ToString("0.0") + "|" +
                   limits.maxJerkDeg.ToString("0.0");
        }

        /// <summary>Рассчитать эко-профиль варианта (кэшируется по лимитам, грузу и варианту).</summary>
        public Draft Compute(TrajectoryCandidate candidate, bool quiet = false)
        {
            if (candidate == null || candidate.plan == null)
            {
                if (!quiet) Report(KvVariantKit.SceneStatus(flow));
                return null;
            }
            if (flow == null || flow.Validator == null || !flow.Validator.Ready)
            {
                if (!quiet) Report("расчёт невозможен: валидатор робота не готов");
                return null;
            }

            string key = Signature(candidate);
            Draft cached;
            if (drafts.TryGetValue(candidate, out cached) && cached != null && cached.key == key)
            {
                last = cached;
                return cached;
            }

            PoseValidator v = flow.Validator;
            PlannedTrajectory source = candidate.plan;
            if (!originals.ContainsKey(candidate))
                originals[candidate] = KvTrajMath.Clone(source, source.Label);

            Vector3 basePosition = KvVariantKit.RobotBase(flow);
            KvTrajMath.EnsureTimes(source);
            float[] levers = KvTrajMath.LeverArms(v, source.Path, basePosition);

            float peak;
            float baseEnergy = KvTrajMath.Energy(v, source, model, levers, out peak);
            float baseLength = KvTrajMath.PathLength(v, source.Path);

            Draft best = null;
            int trials = 0;

            for (int a = 0; a < AccelScales.Length; a++)
            {
                for (int s = 0; s < VelScales.Length; s++)
                {
                    PlannedTrajectory trial = KvTrajMath.Retime(v, source, limits,
                        AccelScales[a], VelScales[s],
                        (source.Label ?? "вариант") + " · " + KvLocExtra.T("energy.eco", "эко-профиль"));
                    if (trial == null) continue;
                    trials++;

                    float trialPeak;
                    float energy = KvTrajMath.Energy(v, trial, model, levers, out trialPeak);
                    if (best == null || energy < best.energy)
                    {
                        if (best == null) best = new Draft();
                        best.plan = trial;
                        best.energy = energy;
                        best.peakPower = trialPeak;
                        best.time = trial.Time > 0f ? (float)trial.Time
                            : (trial.Times != null ? trial.Times[trial.Times.Length - 1] : 0f);
                        best.accelScale = AccelScales[a];
                        best.velScale = VelScales[s];
                    }
                }
            }

            if (best == null)
            {
                if (!quiet) Report("эко-профиль не рассчитан: планировщик не дал пути");
                return null;
            }

            best.key = key;
            best.trials = trials;
            best.originalEnergy = baseEnergy;
            best.originalPeakPower = peak;
            best.originalTime = source.Time > 0f ? (float)source.Time
                : (source.Times != null ? source.Times[source.Times.Length - 1] : 0f);
            float length = KvTrajMath.PathLength(v, best.plan.Path);
            if (length < 1e-4f) length = baseLength;
            best.energyPerMeter = length > 1e-4f ? best.energy / length : 0f;
            best.originalPerMeter = baseLength > 1e-4f ? baseEnergy / baseLength : 0f;
            best.savings = baseEnergy > 1e-4f ? (baseEnergy - best.energy) / baseEnergy * 100f : 0f;
            best.applied = false;

            drafts[candidate] = best;
            last = best;
            return best;
        }

        /// <summary>Переключиться на эко-профиль (подменить время варианта).</summary>
        public bool ApplySelected()
        {
            int index;
            TrajectoryCandidate candidate = KvVariantKit.Selected(flow, out index);
            Draft draft = Compute(candidate);
            if (draft == null || candidate == null) return false;

            KvVariantKit.ApplyPlan(flow, candidate, draft.plan);
            draft.applied = true;
            Report(KvLocExtra.T("energy.title", "Оптимизация по энергии") + ": " +
                   KvVariantKit.Label(candidate, index) + " · " +
                   KvLocExtra.T("energy.value", "Энергия, Дж") + " " +
                   draft.originalEnergy.ToString("0.00") + " → " + draft.energy.ToString("0.00") +
                   " (" + KvLocExtra.F("energy.lower", "ниже на {0} %", draft.savings.ToString("0.0")) +
                   ") · " + KvLocExtra.T("energy.per.meter", "Удельная энергия, Дж/м") + " " +
                   draft.energyPerMeter.ToString("0.00") + " · " +
                   KvLocExtra.T("energy.peak", "Пиковая мощность, Вт") + " " +
                   draft.peakPower.ToString("0.0") + " · " +
                   KvLocExtra.T("smooth.time", "Время, с") + " " + draft.time.ToString("0.000") +
                   " · груз " + model.payloadKg.ToString("0.0") + " кг · профилей проверено " +
                   draft.trials);
            return true;
        }

        /// <summary>Вернуть исходный профиль времени.</summary>
        public bool ResetSelected()
        {
            int index;
            TrajectoryCandidate candidate = KvVariantKit.Selected(flow, out index);
            if (candidate == null) return false;

            PlannedTrajectory original;
            if (!originals.TryGetValue(candidate, out original) || original == null)
            {
                Report("возвращать нечего: эко-профиль не считался");
                return false;
            }
            KvVariantKit.ApplyPlan(flow, candidate, KvTrajMath.Clone(original, original.Label));
            Draft draft;
            if (drafts.TryGetValue(candidate, out draft) && draft != null) draft.applied = false;
            Report("вариант " + KvVariantKit.Label(candidate, index) +
                   ": возвращён исходный профиль времени (" + original.Time.ToString("0.000") + " с)");
            return true;
        }

        /// <summary>Энергия текущего (уже применённого) плана варианта — для свойств и дерева.</summary>
        public float CurrentEnergy(TrajectoryCandidate candidate, out float peakPower)
        {
            peakPower = 0f;
            if (candidate == null || candidate.plan == null) return 0f;
            if (flow == null || flow.Validator == null || !flow.Validator.Ready) return 0f;
            return KvTrajMath.Energy(flow.Validator, candidate.plan, model,
                KvVariantKit.RobotBase(flow), out peakPower);
        }

        /// <summary>Строка метрики «энергоэффективность» для статуса/свойств.</summary>
        public string MetricLine(TrajectoryCandidate candidate)
        {
            if (candidate == null || candidate.plan == null) return "—";
            float peak;
            float energy = CurrentEnergy(candidate, out peak);
            float length = KvTrajMath.PathLength(flow != null ? flow.Validator : null, candidate.plan.Path);
            string perMeter = length > 1e-4f ? (energy / length).ToString("0.00") : "—";
            return energy.ToString("0.00") + " Дж · " + perMeter + " Дж/м · " +
                   peak.ToString("0.0") + " Вт · груз " + model.payloadKg.ToString("0.0") + " кг";
        }

        private void Report(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            Debug.Log("[Energy] " + text);
            if (Message != null) Message(text);
        }
    }

    /// <summary>
    /// ВКЛАДКА «ЭНЕРГИЯ» окна-верстака (этап 6): масса груза, расчёт эко-профиля
    /// и таблица «исходная / эко» по энергии, удельной энергии, мощности и времени.
    /// </summary>
    public class KvEnergyTab : IKvWorkbenchTab
    {
        private readonly KvEnergyOptimal service;
        private float payload;

        public KvEnergyTab(KvEnergyOptimal energy)
        {
            service = energy;
        }

        public string Key { get { return "energy"; } }
        public string Title { get { return KvLocExtra.T("energy.title", "Оптимизация по энергии"); } }

        public void Build(KvTabKit kit)
        {
            if (service == null) return;
            payload = service.PayloadKg;

            kit.Section(Title);
            kit.Info(delegate { return KvVariantKit.SceneStatus(service.Flow()); }, KvTheme.Warn);

            kit.Slider(KvLocExtra.T("energy.payload", "Масса груза, кг"), 0f, 25f,
                payload, "0.0", delegate (float v)
                {
                    payload = v;
                    service.PayloadKg = v;
                });

            kit.Buttons(new[]
            {
                KvLocExtra.T("energy.compute", "Рассчитать эко-профиль"),
                KvLocExtra.T("energy.apply", "Переключиться на эко-профиль"),
                KvLocExtra.T("smooth.reset", "Вернуть исходную")
            }, new Action[]
            {
                delegate { service.Compute(null, false); },
                delegate { service.ApplySelected(); },
                delegate { service.ResetSelected(); }
            });

            kit.Section(KvLocExtra.T("wb.metric", "Метрика"));
            kit.Table("", delegate
            {
                return KvLocExtra.T("smooth.before", "До");
            }, delegate
            {
                return KvLocExtra.T("energy.eco", "эко-профиль");
            });

            AddRow(kit, KvLocExtra.T("energy.value", "Энергия, Дж"),
                delegate (KvEnergyOptimal.Draft d) { return d.originalEnergy.ToString("0.00"); },
                delegate (KvEnergyOptimal.Draft d) { return d.energy.ToString("0.00"); });
            AddRow(kit, KvLocExtra.T("energy.per.meter", "Удельная энергия, Дж/м"),
                delegate (KvEnergyOptimal.Draft d) { return d.originalPerMeter.ToString("0.00"); },
                delegate (KvEnergyOptimal.Draft d) { return d.energyPerMeter.ToString("0.00"); });
            AddRow(kit, KvLocExtra.T("energy.peak", "Пиковая мощность, Вт"),
                delegate (KvEnergyOptimal.Draft d) { return d.originalPeakPower.ToString("0.0"); },
                delegate (KvEnergyOptimal.Draft d) { return d.peakPower.ToString("0.0"); });
            AddRow(kit, KvLocExtra.T("smooth.time", "Время, с"),
                delegate (KvEnergyOptimal.Draft d) { return d.originalTime.ToString("0.000"); },
                delegate (KvEnergyOptimal.Draft d) { return d.time.ToString("0.000"); });
            AddRow(kit, KvLocExtra.T("energy.savings", "Экономия"),
                delegate (KvEnergyOptimal.Draft d) { return "—"; },
                delegate (KvEnergyOptimal.Draft d)
                {
                    return KvLocExtra.F("energy.lower", "ниже на {0} %", d.savings.ToString("0.0")) +
                           " · разгонов " + (d.accelScale * 100f).ToString("0") + " % · " +
                           "скорость " + (d.velScale * 100f).ToString("0") + " %";
                });

            kit.Divider();
            kit.Note(KvLocExtra.T("energy.info",
                "Симуляция по ТЗ: энергия ∝ Σ |момент × угловая скорость| × время. Момент считается " +
                "по упрощённой динамике, поэтому метрика годится для сравнения вариантов и оценки " +
                "износа, а не для паспортных расчётов."), KvTheme.TextDim);
        }

        private void AddRow(KvTabKit kit, string caption,
            Func<KvEnergyOptimal.Draft, string> before, Func<KvEnergyOptimal.Draft, string> after)
        {
            kit.Table(caption,
                delegate
                {
                    KvEnergyOptimal.Draft d = Current();
                    return d != null ? before(d) : "—";
                },
                delegate
                {
                    KvEnergyOptimal.Draft d = Current();
                    return d != null ? after(d) : "—";
                });
        }

        private KvEnergyOptimal.Draft Current()
        {
            if (service == null) return null;
            int index;
            TrajectoryCandidate candidate = KvVariantKit.Selected(service.Flow(), out index);
            if (candidate == null) return null;
            return service.Compute(candidate, true);
        }

        public void Tick() { }
        public void Refresh() { }
    }
}
