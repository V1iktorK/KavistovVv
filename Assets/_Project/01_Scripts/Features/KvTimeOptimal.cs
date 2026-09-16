using System;
using System.Collections.Generic;
using UnityEngine;
using KazistovVvUI;
using TrajectoryCore;

namespace KazistovVvFeatures
{
    /// <summary>
    /// ЭТАП 5 ТЗ: ВРЕМЯ-ОПТИМАЛЬНЫЕ ТРАЕКТОРИИ.
    ///
    /// Задача: при заданных ограничениях (максимальная скорость суставов, максимальное
    /// ускорение, максимальный jerk) найти САМУЮ БЫСТРУЮ траекторию к той же точке.
    /// Форма пути сохраняется — меняется распределение времени вдоль пути:
    ///   • скорость вдоль пути ограничена лимитами КАЖДОГО сустава;
    ///   • профиль считается интегрированием вперёд-назад (разгон / крейсер / торможение);
    ///   • jerk ограничивается отдельными проходами по профилю ускорений;
    ///   • в начале и в конце — нулевая скорость (как у штатного исполнителя).
    ///
    /// Метрика «время-оптимальная» показывается рядом с исходной: время, выигрыш в
    /// процентах, фактические максимумы скорости/ускорения/jerk. Переключение на эту
    /// траекторию — одна кнопка (и отметка в сравнении траекторий A/B).
    /// </summary>
    public class KvTimeOptimal
    {
        public const string VelPrefsKey = "KazistovVv.Post.MaxVel";
        public const string AccPrefsKey = "KazistovVv.Post.MaxAcc";
        public const string JerkPrefsKey = "KazistovVv.Post.MaxJerk";

        public event Action<string> Message;

        /// <summary>Ограничения, при которых считается время-оптимальная траектория.</summary>
        public class Draft
        {
            public PlannedTrajectory plan;
            public KvTrajStats stats;
            public KvTrajStats original;
            public string key = "";
            public float timeGain;      // % выигрыша по времени
            public bool applied;
            /// <summary>Отчёт о фактическом рывке (ФИКС 1) — честная проверка по сэмплам.</summary>
            public string jerkReport = "";
            /// <summary>Уложился ли фактический рывок в предел.</summary>
            public bool jerkOk = true;
        }

        private TrajectoryFlowController flow;
        private CollisionWorld world;
        private KvMotionLimits limits = new KvMotionLimits();
        private KvEnergyModel energy;
        private bool loaded;

        private readonly Dictionary<TrajectoryCandidate, Draft> drafts =
            new Dictionary<TrajectoryCandidate, Draft>();
        private readonly Dictionary<TrajectoryCandidate, PlannedTrajectory> originals =
            new Dictionary<TrajectoryCandidate, PlannedTrajectory>();
        private Draft last;

        /// <summary>Максимальная скорость суставов, °/с.</summary>
        public float MaxVel { get { Load(); return limits.maxVelDeg; } }
        /// <summary>Максимальное ускорение суставов, °/с².</summary>
        public float MaxAcc { get { Load(); return limits.maxAccDeg; } }
        /// <summary>Максимальный jerk, °/с³.</summary>
        public float MaxJerk { get { Load(); return limits.maxJerkDeg; } }

        public KvMotionLimits Limits { get { Load(); return limits; } }
        public Draft Last { get { return last; } }
        public int DraftCount { get { return drafts.Count; } }
        public TrajectoryFlowController Flow() { return flow; }

        /// <summary>Изменить лимиты (вызывается ползунками вкладки), с сохранением в PlayerPrefs.</summary>
        public void SetLimits(float vel, float acc, float jerk, bool save = true)
        {
            Load();
            limits.maxVelDeg = Mathf.Clamp(vel, 1f, 720f);
            limits.maxAccDeg = Mathf.Clamp(acc, 1f, 5000f);
            limits.maxJerkDeg = Mathf.Clamp(jerk, 10f, 100000f);
            // Призматические оси — те же пределы, переведённые в СИ (10 см ≈ 90°).
            limits.maxVelMps = limits.maxVelDeg / 900f;
            limits.maxAccMps2 = limits.maxAccDeg / 900f;
            limits.maxJerkMps3 = limits.maxJerkDeg / 900f;
            if (save)
            {
                PlayerPrefs.SetFloat(VelPrefsKey, limits.maxVelDeg);
                PlayerPrefs.SetFloat(AccPrefsKey, limits.maxAccDeg);
                PlayerPrefs.SetFloat(JerkPrefsKey, limits.maxJerkDeg);
                PlayerPrefs.Save();
            }
            Invalidate();
        }

        private void Load()
        {
            if (loaded) return;
            loaded = true;
            limits.maxVelDeg = PlayerPrefs.GetFloat(VelPrefsKey, limits.maxVelDeg);
            limits.maxAccDeg = PlayerPrefs.GetFloat(AccPrefsKey, limits.maxAccDeg);
            limits.maxJerkDeg = PlayerPrefs.GetFloat(JerkPrefsKey, limits.maxJerkDeg);
            limits.maxVelMps = limits.maxVelDeg / 900f;
            limits.maxAccMps2 = limits.maxAccDeg / 900f;
            limits.maxJerkMps3 = limits.maxJerkDeg / 900f;
        }

        public void Bind(TrajectoryFlowController controller, CollisionWorld collisionWorld,
            KvEnergyModel energyModel)
        {
            flow = controller;
            world = collisionWorld;
            energy = energyModel;
            Load();
        }

        /// <summary>Лимиты изменились — прежние расчёты больше не действительны.</summary>
        public void Invalidate()
        {
            drafts.Clear();
            originals.Clear();
            last = null;
        }

        /// <summary>Сбросить при смене робота.</summary>
        public void ResetCache()
        {
            Invalidate();
        }

        private string Signature(TrajectoryCandidate candidate)
        {
            return candidate.GetHashCode() + "|" + limits.maxVelDeg.ToString("0.0") + "|" +
                   limits.maxAccDeg.ToString("0.0") + "|" + limits.maxJerkDeg.ToString("0.0");
        }

        /// <summary>Рассчитать время-оптимальный профиль для варианта (кэшируется по лимитам).</summary>
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
            Draft draft;
            if (drafts.TryGetValue(candidate, out draft) && draft != null && draft.key == key)
            {
                last = draft;
                return draft;
            }

            // Исходная геометрия: если вариант уже сглажен, время-оптимальный профиль
            // считается по ТЕКУЩЕМУ пути (сглаженному) — это то, что видит оператор.
            PlannedTrajectory source = candidate.plan;
            if (!originals.ContainsKey(candidate))
                originals[candidate] = KvTrajMath.Clone(source, source.Label);

            PlannedTrajectory plan = KvTrajMath.Retime(flow.Validator, source, limits, 1f, 1f,
                (source.Label ?? "вариант") + " · " + KvLocExtra.T("topt.title", "время-оптимальная"));
            if (plan == null)
            {
                if (!quiet) Report("время-оптимальная траектория не рассчитана");
                return null;
            }

            draft = new Draft();
            draft.plan = plan;
            draft.key = key;
            draft.applied = false;
            draft.original = KvTrajMath.Analyze(flow.Validator, source, world, null,
                KvVariantKit.RobotBase(flow));
            draft.stats = KvTrajMath.Analyze(flow.Validator, plan, world, energy,
                KvVariantKit.RobotBase(flow));
            draft.timeGain = draft.original.time > 1e-4f
                ? (draft.original.time - draft.stats.time) / draft.original.time * 100f
                : 0f;
            // ФИКС 1: аналитический S-профиль гарантирует рывок ВДОЛЬ пути; фактический
            // рывок по сэмплам проверяется отдельно и показывается честно.
            draft.jerkOk = KvTrajMath.VerifyJerk(flow.Validator, plan, limits, null,
                out draft.jerkReport);

            drafts[candidate] = draft;
            last = draft;
            return draft;
        }

        /// <summary>Переключиться на время-оптимальную траекторию (подменить план варианта).</summary>
        public bool ApplySelected()
        {
            int index;
            TrajectoryCandidate candidate = KvVariantKit.Selected(flow, out index);
            Draft draft = Compute(candidate);
            if (draft == null || candidate == null) return false;

            KvVariantKit.ApplyPlan(flow, candidate, draft.plan);
            draft.applied = true;
            Report(KvLocExtra.T("topt.metric", "Метрика «время-оптимальная»") + ": " +
                   KvVariantKit.Label(candidate, index) + " · " +
                   KvLocExtra.F("topt.faster", "быстрее на {0} %", draft.timeGain.ToString("0.0")) +
                   " · " + draft.original.time.ToString("0.000") + " с → " +
                   draft.stats.time.ToString("0.000") + " с · v " +
                   draft.stats.maxVel.ToString("0.0") + " °/с · a " +
                   draft.stats.maxAcc.ToString("0.0") + " °/с² · jerk " +
                   draft.stats.maxJerk.ToString("0") + " °/с³ (лимиты " +
                   MaxVel.ToString("0") + " / " + MaxAcc.ToString("0") + " / " +
                   MaxJerk.ToString("0") + ")");
            if (!draft.jerkOk && !string.IsNullOrEmpty(draft.jerkReport))
                Report("внимание: " + draft.jerkReport);
            return true;
        }

        /// <summary>Вернуть исходное время (отменить время-оптимальный профиль).</summary>
        public bool ResetSelected()
        {
            int index;
            TrajectoryCandidate candidate = KvVariantKit.Selected(flow, out index);
            if (candidate == null) return false;

            PlannedTrajectory original;
            if (!originals.TryGetValue(candidate, out original) || original == null)
            {
                Report("возвращать нечего: время-оптимальная траектория не считалась");
                return false;
            }
            KvVariantKit.ApplyPlan(flow, candidate, KvTrajMath.Clone(original, original.Label));
            Draft draft;
            if (drafts.TryGetValue(candidate, out draft) && draft != null) draft.applied = false;
            Report("вариант " + KvVariantKit.Label(candidate, index) +
                   ": возвращено исходное время (" + original.Time.ToString("0.000") + " с)");
            return true;
        }

        /// <summary>Отметить вариант в сравнении траекторий (A/B) — «переключиться на неё» в сравнении.</summary>
        public bool MarkInComparison()
        {
            FeatureHub hub = FeatureHub.Current;
            if (hub == null || hub.Comparison == null)
            {
                Report("сравнение траекторий недоступно");
                return false;
            }
            int index;
            KvVariantKit.Selected(flow, out index);
            if (index < 0)
            {
                Report("сравнение: сначала выберите вариант");
                return false;
            }
            // Тот же путь, что у кнопки «Отметить выбранную (A/B)»: слоты занимаются
            // по кругу, событие изменения поднимает сама модель сравнения.
            string result = hub.Comparison.Mark(index);
            Report("сравнение траекторий: " + result + " · вкладка «Сравнение» (F7) покажет " +
                   "разницу по времени, длине, кривизне и зазору");
            return true;
        }

        private void Report(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            Debug.Log("[TimeOptimal] " + text);
            if (Message != null) Message(text);
        }
    }

    /// <summary>
    /// ВКЛАДКА «ВРЕМЯ-ОПТИМАЛЬНАЯ ТРАЕКТОРИЯ» окна-верстака (этап 5):
    /// ползунки лимитов, кнопки расчёта/переключения и таблица метрик.
    /// </summary>
    public class KvTimeOptimalTab : IKvWorkbenchTab
    {
        private readonly KvTimeOptimal service;
        private float lastVel, lastAcc, lastJerk;

        public KvTimeOptimalTab(KvTimeOptimal timeOptimal)
        {
            service = timeOptimal;
        }

        public string Key { get { return "topt"; } }
        public string Title { get { return KvLocExtra.T("topt.title", "Время-оптимальная траектория"); } }

        public void Build(KvTabKit kit)
        {
            if (service == null) return;
            lastVel = service.MaxVel;
            lastAcc = service.MaxAcc;
            lastJerk = service.MaxJerk;

            kit.Section(Title);
            kit.Info(delegate { return KvVariantKit.SceneStatus(service.Flow()); }, KvTheme.Warn);

            kit.Section(KvLocExtra.T("topt.metric", "Метрика «время-оптимальная»"));
            kit.Slider(KvLocExtra.T("topt.vel", "Макс. скорость суставов, °/с"), 5f, 360f,
                lastVel, "0", delegate (float v) { service.SetLimits(v, lastAcc, lastJerk); lastVel = v; });
            kit.Slider(KvLocExtra.T("topt.acc", "Макс. ускорение, °/с²"), 10f, 2000f,
                lastAcc, "0", delegate (float v) { service.SetLimits(lastVel, v, lastJerk); lastAcc = v; });
            kit.Slider(KvLocExtra.T("topt.jerk", "Макс. jerk, °/с³"), 50f, 50000f,
                lastJerk, "0", delegate (float v) { service.SetLimits(lastVel, lastAcc, v); lastJerk = v; });

            kit.Buttons(new[]
            {
                KvLocExtra.T("topt.compute", "Рассчитать время-оптимальную"),
                KvLocExtra.T("topt.apply", "Переключиться на неё"),
                KvLocExtra.T("smooth.reset", "Вернуть исходную")
            }, new Action[]
            {
                delegate { service.Compute(null, false); },
                delegate { service.ApplySelected(); },
                delegate { service.ResetSelected(); }
            });

            kit.Buttons(new[]
            {
                KvLocExtra.T("wb.variant", "Вариант") + " → A/B",
                KvLocExtra.T("wb.metric", "Метрика") + " → " + KvLocExtra.T("smooth.reset", "Сброс")
            }, new Action[]
            {
                delegate { service.MarkInComparison(); },
                delegate { service.Invalidate(); }
            });

            kit.Section(KvLocExtra.T("wb.metric", "Метрика"));
            kit.Table("", delegate { return KvLocExtra.T("smooth.before", "До"); },
                delegate { return KvLocExtra.T("topt.title", "Время-оптимальная"); });

            AddRow(kit, KvLocExtra.T("smooth.time", "Время, с"),
                delegate (KvTimeOptimal.Draft d) { return d.original.time.ToString("0.000"); },
                delegate (KvTimeOptimal.Draft d) { return d.stats.time.ToString("0.000"); });
            AddRow(kit, KvLocExtra.T("topt.gain", "Выигрыш по времени"),
                delegate (KvTimeOptimal.Draft d) { return "—"; },
                delegate (KvTimeOptimal.Draft d)
                {
                    return KvLocExtra.F("topt.faster", "быстрее на {0} %", d.timeGain.ToString("0.0"));
                });
            AddRow(kit, KvLocExtra.T("topt.vel", "Макс. скорость, °/с"),
                delegate (KvTimeOptimal.Draft d) { return d.original.maxVel.ToString("0.0"); },
                delegate (KvTimeOptimal.Draft d) { return d.stats.maxVel.ToString("0.0"); });
            AddRow(kit, KvLocExtra.T("smooth.accel", "Ускорение, °/с²"),
                delegate (KvTimeOptimal.Draft d) { return d.original.maxAcc.ToString("0.0"); },
                delegate (KvTimeOptimal.Draft d) { return d.stats.maxAcc.ToString("0.0"); });
            AddRow(kit, KvLocExtra.T("smooth.jerk", "Jerk, °/с³"),
                delegate (KvTimeOptimal.Draft d) { return d.original.maxJerk.ToString("0"); },
                delegate (KvTimeOptimal.Draft d) { return d.stats.maxJerk.ToString("0"); });
            AddRow(kit, KvLocExtra.T("smooth.length", "Длина пути TCP, м"),
                delegate (KvTimeOptimal.Draft d) { return d.original.length.ToString("0.000"); },
                delegate (KvTimeOptimal.Draft d) { return d.stats.length.ToString("0.000"); });
            AddRow(kit, KvLocExtra.T("topt.clears", "Мин. зазор, м"),
                delegate (KvTimeOptimal.Draft d) { return d.original.clearance.ToString("0.000"); },
                delegate (KvTimeOptimal.Draft d) { return d.stats.clearance.ToString("0.000"); });

            kit.Divider();
            kit.Info(delegate
            {
                KvTimeOptimal.Draft d = Current();
                return d != null && !string.IsNullOrEmpty(d.jerkReport)
                    ? (d.jerkOk ? "" : "⚠ ") + d.jerkReport
                    : KvTrajMath.LastProfileNote;
            }, KvTheme.TextDim);
            kit.Note(KvLocExtra.T("topt.info",
                "Скорость вдоль пути ограничена лимитами суставов: профиль строится АНАЛИТИЧЕСКИ " +
                "(S-профиль: разгон → крейсер → торможение), рывок — управляющий сигнал, поэтому " +
                "он не превышает предел по построению. Форма пути не меняется — меняется только " +
                "распределение времени."), KvTheme.TextDim);
        }

        private void AddRow(KvTabKit kit, string caption, Func<KvTimeOptimal.Draft, string> before,
            Func<KvTimeOptimal.Draft, string> after)
        {
            kit.Table(caption,
                delegate
                {
                    KvTimeOptimal.Draft d = Current();
                    return d != null ? before(d) : "—";
                },
                delegate
                {
                    KvTimeOptimal.Draft d = Current();
                    return d != null ? after(d) : "—";
                });
        }

        /// <summary>Черновик для текущего варианта: считается один раз и кэшируется в сервисе.</summary>
        private KvTimeOptimal.Draft Current()
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
