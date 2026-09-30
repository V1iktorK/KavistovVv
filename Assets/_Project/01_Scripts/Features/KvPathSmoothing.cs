using System;
using System.Collections.Generic;
using UnityEngine;
using KazistovVvUI;
using TrajectoryCore;

namespace KazistovVvFeatures
{
    /// <summary>Метрики одного варианта: исходная траектория и результат постобработки.</summary>
    public class KvSmoothEntry
    {
        public PlannedTrajectory original;      // копия плана ДО обработки
        public PlannedTrajectory smoothed;      // план ПОСЛЕ обработки
        public KvTrajStats before;              // метрики плана как его построил планировщик
        public KvTrajStats baseline;            // тот же путь, но перепараметризованный по лимитам
        public KvTrajStats after;
        public float level;
        public KvSmoothMethod method;
        public bool applied;

        /// <summary>
        /// Метрики, с которыми сравнивается результат: тот же путь, время пересчитано по лимитам.
        ///
        /// ПОЧЕМУ ЭТО ВСЁ ЕЩЁ НУЖНО (ФИКС 2 §20 это не отменяет): до §20 планировщик отдавал
        /// путь с ПОСТОЯННОЙ скоростью внутри сегментов (нулевые ускорение и рывок, но мгновенные
        /// скачки скорости в узлах), поэтому «до» по сырому плану выглядело нулевым. Теперь
        /// планировщик сам строит S-профиль, и `baseline` совпадает с ним по времени — сравнение
        /// остаётся честным: оно показывает выигрыш ОТ ФОРМЫ пути (сглаживания), а не от пересчёта.
        /// </summary>
        public KvTrajStats Reference { get { return baseline.valid ? baseline : before; } }

        public float JerkGain
        {
            get
            {
                float reference = Reference.maxJerk;
                if (reference < 1e-3f) return 0f;
                return (reference - after.maxJerk) / reference * 100f;
            }
        }

        public float AccGain
        {
            get
            {
                float reference = Reference.maxAcc;
                if (reference < 1e-3f) return 0f;
                return (reference - after.maxAcc) / reference * 100f;
            }
        }

        public float CurvatureGain
        {
            get
            {
                float reference = Reference.curvature;
                if (reference < 1e-6f) return 0f;
                return (reference - after.curvature) / reference * 100f;
            }
        }

        public float TimeDelta { get { return after.time - Reference.time; } }
    }

    /// <summary>
    /// ЭТАП 4 ТЗ: СГЛАЖИВАНИЕ ТРАЕКТОРИЙ (POST-PROCESSING ПОСЛЕ ПЛАНИРОВАНИЯ).
    ///
    /// Что делает:
    ///   • после того как планировщик построил варианты, путь каждого варианта
    ///     сглаживается выбранным методом (B-сплайн / Безье / фильтр Гаусса) —
    ///     ползунок «Уровень сглаживания» 0…100 % задаёт силу обработки;
    ///   • время пересчитывается по лимитам скорости и ускорения (та же схема, что у
    ///     время-оптимальной траектории этапа 5), поэтому сглаженный путь сразу годен
    ///     к исполнению и не выходит за пределы суставов;
    ///   • считается метрика «До / После»: jerk, ускорения, кривизна, длина и время;
    ///   • включается тумблером «Применять автоматически» (настройки/вкладка, значение
    ///     переживает перезапуск), тогда сглаживание идёт само сразу после планирования.
    ///
    /// Исходный план СОХРАНЯЕТСЯ, поэтому «Вернуть исходную» — мгновенная отмена.
    /// </summary>
    public class KvPathSmoothing
    {
        public const string LevelPrefsKey = "KazistovVv.Post.SmoothLevel";
        public const string MethodPrefsKey = "KazistovVv.Post.SmoothMethod";
        public const string AutoPrefsKey = "KazistovVv.Post.SmoothAuto";
        /// <summary>ФИКС 7: «сглаживать только выбранный вариант» (0/1).</summary>
        public const string AutoSelectedPrefsKey = "KazistovVv.Post.SmoothAutoSelected";
        /// <summary>ФИКС 3 (§20): «сглаживать только изломы» (0/1).</summary>
        public const string CornersOnlyPrefsKey = "KazistovVv.Post.SmoothCornersOnly";

        public event Action<string> Message;

        private float level = 60f;
        private KvSmoothMethod method = KvSmoothMethod.BSpline;
        private bool auto = true;
        private bool autoSelectedOnly;      // ФИКС 7 (по умолчанию выключено — как было)
        private bool cornersOnly;           // ФИКС 3 §20 (по умолчанию выключено — как было)
        private bool loaded;

        private TrajectoryFlowController flow;
        private CollisionWorld world;
        private KvMotionLimits limits = new KvMotionLimits();
        private KvEnergyModel energy;

        private readonly Dictionary<TrajectoryCandidate, KvSmoothEntry> entries =
            new Dictionary<TrajectoryCandidate, KvSmoothEntry>();
        private readonly List<TrajectoryCandidate> prune = new List<TrajectoryCandidate>();

        /// <summary>Подпись состояния сцены, для которой сглаживание уже применено (авторежим).</summary>
        private string appliedSignature = "";
        private int appliedCount;

        // ------------------------------------------------------------------ настройки

        /// <summary>Уровень сглаживания, % (0 — выключено, 100 — максимальная обработка).</summary>
        public float Level
        {
            get { Load(); return level; }
            set
            {
                Load();
                level = Mathf.Clamp(value, 0f, 100f);
                PlayerPrefs.SetFloat(LevelPrefsKey, level);
                PlayerPrefs.Save();
            }
        }

        public KvSmoothMethod Method
        {
            get { Load(); return method; }
            set
            {
                Load();
                method = value;
                PlayerPrefs.SetInt(MethodPrefsKey, (int)method);
                PlayerPrefs.Save();
            }
        }

        public bool Auto
        {
            get { Load(); return auto; }
            set
            {
                Load();
                auto = value;
                PlayerPrefs.SetInt(AutoPrefsKey, auto ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// ФИКС 7. «Сглаживать ТОЛЬКО выбранный вариант»: в авторежиме сглаживание применяется
        /// к тому варианту, который выбрал оператор, а не ко всем восьми сразу. По умолчанию
        /// ВЫКЛЮЧЕНО — поведение остаётся прежним (обрабатываются все 8). Значение хранится
        /// в PlayerPrefs.
        /// </summary>
        public bool AutoSelectedOnly
        {
            get { Load(); return autoSelectedOnly; }
            set
            {
                Load();
                autoSelectedOnly = value;
                PlayerPrefs.SetInt(AutoSelectedPrefsKey, autoSelectedOnly ? 1 : 0);
                PlayerPrefs.Save();
                appliedSignature = "";        // режим сменился — пересчитать под новый режим
            }
        }

        /// <summary>
        /// ФИКС 3 (§20). «ТОЧЕЧНО СГЛАЖИВАТЬ ИЗЛОМЫ»: сглаживание применяется только
        /// к участкам с изломом пути (угол между соседними сегментами больше
        /// <see cref="KvTrajMath.SharpCornerDeg"/>), остальной путь остаётся как у планировщика.
        /// По умолчанию ВЫКЛЮЧЕНО — поведение прежнее (сглаживается весь путь).
        /// </summary>
        public bool CornersOnly
        {
            get { Load(); return cornersOnly; }
            set
            {
                Load();
                cornersOnly = value;
                PlayerPrefs.SetInt(CornersOnlyPrefsKey, cornersOnly ? 1 : 0);
                PlayerPrefs.Save();
                previewKey = "";              // режим сменился — пересчитать предпросмотр
            }
        }

        /// <summary>Порог излома для подсветки и точечного сглаживания, градусы.</summary>
        public float CornerThresholdDeg = KvTrajMath.SharpCornerDeg;

        /// <summary>Индексы сэмплов-изломов последней проверки (ФИКС 3).</summary>
        public int[] LastSharpCorners { get; private set; } = new int[0];
        /// <summary>Самый острый излом последней проверки, градусы.</summary>
        public float LastSharpAngleDeg { get; private set; }
        /// <summary>Была ли уже выдана подсказка «сгладьте излом» для текущей точки.</summary>
        private string cornersWarnedSignature = "";
        /// <summary>Подпись состояния, для которого изломы уже искали (ФИКС 3).</summary>
        private string cornersCheckedSignature = "";

        /// <summary>Длительность последнего автоматического сглаживания, мс (ФИКС 7, замер).</summary>
        public float LastAutoMs { get; private set; }
        /// <summary>Сколько вариантов обработало последнее автоматическое сглаживание.</summary>
        public int LastAutoCount { get; private set; }
        /// <summary>Режим последнего автоматического сглаживания (true — только выбранный).</summary>
        public bool LastAutoSelectedOnly { get; private set; }

        public KvMotionLimits Limits
        {
            get { return limits; }
            set { if (value != null) limits = value; }
        }

        public int AppliedCount { get { return appliedCount; } }
        public int ComputedCount { get { return entries.Count; } }

        public string MethodLabel
        {
            get
            {
                switch (Method)
                {
                    case KvSmoothMethod.Bezier: return KvLocExtra.T("smooth.method.bezier", "Безье");
                    case KvSmoothMethod.Gauss: return KvLocExtra.T("smooth.method.gauss", "Фильтр Гаусса");
                    default: return KvLocExtra.T("smooth.method.bspline", "B-сплайн");
                }
            }
        }

        private void Load()
        {
            if (loaded) return;
            loaded = true;
            level = Mathf.Clamp(PlayerPrefs.GetFloat(LevelPrefsKey, 60f), 0f, 100f);
            method = (KvSmoothMethod)Mathf.Clamp(PlayerPrefs.GetInt(MethodPrefsKey, 0), 0, 2);
            auto = PlayerPrefs.GetInt(AutoPrefsKey, 1) != 0;
            autoSelectedOnly = PlayerPrefs.GetInt(AutoSelectedPrefsKey, 0) != 0;   // по умолчанию — все 8
            cornersOnly = PlayerPrefs.GetInt(CornersOnlyPrefsKey, 0) != 0;         // по умолчанию — весь путь
        }

        public void Bind(TrajectoryFlowController controller, CollisionWorld collisionWorld,
            KvMotionLimits motionLimits, KvEnergyModel energyModel)
        {
            flow = controller;
            world = collisionWorld;
            if (motionLimits != null) limits = motionLimits;
            energy = energyModel;
            Load();
        }

        // ------------------------------------------------------------------ применение

        /// <summary>Поток этапов, к которому привязан сервис (для вкладок верстака).</summary>
        public TrajectoryFlowController Flow() { return flow; }

        /// <summary>
        /// Метрики выбранного варианта для интерфейса: опорный вариант («тот же путь,
        /// время по лимитам») считается здесь и только для ВЫБРАННОГО варианта —
        /// в авторежиме он не нужен, а стоит он лишних расчётов на каждый из 8 вариантов.
        /// </summary>
        public KvSmoothEntry CurrentEntry(out int index)
        {
            index = -1;
            TrajectoryCandidate candidate = KvVariantKit.Selected(flow, out index);
            if (candidate == null) return null;

            KvSmoothEntry entry = EntryOf(candidate);
            EnsureBaseline(candidate, entry);
            if (entry == null || entry.applied) return entry;
            if (flow == null || flow.Validator == null || !flow.Validator.Ready) return entry;

            string key = candidate.GetHashCode() + "|" + Level.ToString("0") + "|" + (int)Method;
            if (previewKey != key || Time.unscaledTime - previewTime > 0.6f)
            {
                previewKey = key;
                previewTime = Time.unscaledTime;
                PlannedTrajectory draft = Preview(candidate);
                if (draft != null)
                    entry.after = KvTrajMath.Analyze(flow.Validator, draft, null, energy,
                        KvVariantKit.RobotBase(flow));
            }
            return entry;
        }

        /// <summary>
        /// Посчитать ОПОРНЫЙ вариант («тот же путь, время пересчитано по лимитам»), с которым
        /// сравнивается сглаживание. Считается один раз на вариант и только по запросу
        /// интерфейса/свойств — в авторежиме лишней работы не делается.
        /// </summary>
        public void EnsureBaseline(TrajectoryCandidate candidate, KvSmoothEntry entry = null)
        {
            if (candidate == null) return;
            if (entry == null) entry = EntryOf(candidate);
            if (entry == null || entry.baseline.valid || entry.original == null) return;
            if (flow == null || flow.Validator == null || !flow.Validator.Ready) return;

            PlannedTrajectory retimed = KvTrajMath.Retime(flow.Validator, entry.original,
                limits, 1f, 1f, null, true);
            if (retimed != null)
                entry.baseline = KvTrajMath.Analyze(flow.Validator, retimed, null, energy,
                    KvVariantKit.RobotBase(flow));
        }

        private string previewKey = "";
        private float previewTime = -10f;

        /// <summary>Метрики варианта (кэшируются; исходные считаются один раз).</summary>
        public KvSmoothEntry EntryOf(TrajectoryCandidate candidate)
        {
            if (candidate == null || candidate.plan == null) return null;
            KvSmoothEntry entry;
            if (!entries.TryGetValue(candidate, out entry))
            {
                entry = new KvSmoothEntry();
                entry.original = KvTrajMath.Clone(candidate.plan, candidate.plan.Label);
                entry.before = KvTrajMath.Analyze(flow != null ? flow.Validator : null, candidate.plan,
                    null, energy, KvVariantKit.RobotBase(flow));
                // Опорный вариант («тот же путь по лимитам») считается по запросу —
                // см. EnsureBaseline: в авторежиме он не нужен.
                entries[candidate] = entry;
            }
            return entry;
        }

        /// <summary>
        /// Рассчитать сглаженный план варианта БЕЗ применения (для предпросмотра метрик)
        /// и заполнить «после».
        /// </summary>
        public PlannedTrajectory Preview(TrajectoryCandidate candidate)
        {
            if (candidate == null || candidate.plan == null) return null;
            if (flow == null || flow.Validator == null || !flow.Validator.Ready) return null;

            KvSmoothEntry entry = EntryOf(candidate);
            PlannedTrajectory basis = entry != null && entry.original != null ? entry.original : candidate.plan;

            // ФИКС 3 (§20): точечный режим — сглаживаем ТОЛЬКО окрестности изломов, найденных
            // в нормированном пространстве суставов. Полный режим (по умолчанию) — как раньше.
            double[][] path;
            if (CornersOnly)
            {
                int[] corners = KvTrajMath.FindSharpCorners(flow.Validator, basis, CornerThresholdDeg);
                path = KvTrajMath.SmoothCorners(basis.Path, corners, Method, Level / 100f);
            }
            else
            {
                path = KvTrajMath.Smooth(basis.Path, Method, Level / 100f);
            }
            PlannedTrajectory draft = KvTrajMath.Clone(basis, basis.Label);
            draft.Path = path;
            PlannedTrajectory result = KvTrajMath.Retime(flow.Validator, draft, limits, 1f, 1f,
                (basis.Label ?? "вариант") + " · " + KvLocExtra.T("smooth.done", "сглажено"));
            if (result == null)
            {
                result = draft;
                KvTrajMath.EnsureTimes(result);
            }
            // ФИКС 1: аналитический S-профиль задаёт рывок ВДОЛЬ пути, но сглаженный путь
            // (Безье, Гаусс, B-сплайн) добавляет вклад формы q''·v². Проверяем факт честно.
            VerifyJerkOf(result);
            return result;
        }

        /// <summary>
        /// ЧЕСТНАЯ ПРОВЕРКА РЫВКА для сглаженного пути (ФИКС 1): измеряется фактический
        /// максимум по сэмплам; при превышении пишется предупреждение с причиной.
        /// Результат доступен в <see cref="LastJerkReport"/> и в «Свойствах».
        /// </summary>
        private void VerifyJerkOf(PlannedTrajectory plan)
        {
            string report;
            bool ok = KvTrajMath.VerifyJerk(flow != null ? flow.Validator : null, plan, limits,
                MethodLabel, out report);
            LastJerkReport = report;
            LastJerkOk = ok;
            if (!ok && !quietWarned)
            {
                quietWarned = true;
                Report("сглаживание («" + MethodLabel + "», уровень " + Level.ToString("0") +
                       " %) — " + report);
            }
        }

        private bool quietWarned;

        /// <summary>
        /// ФИКС 3 (§20). ПОИСК ИЗЛОМОВ у текущего (выбранного, иначе первого) варианта.
        /// Обновляет <see cref="LastSharpCorners"/> и <see cref="LastSharpAngleDeg"/> для интерфейса
        /// и ОДИН РАЗ на точку сообщает оператору, что участок стоит сгладить.
        ///
        /// Геометрия пути здесь НЕ правится: метод только сообщает о проблемном участке
        /// и предлагает инструмент (вкладка «Сглаживание траектории», опция «только изломы»).
        /// Излом — это следствие формы пути (признак §14.10/§16.1): предел скорости вдоль пути
        /// в такой точке близок к нулю, поэтому траектория получается медленной.
        /// </summary>
        public int DetectCorners()
        {
            LastSharpCorners = new int[0];
            LastSharpAngleDeg = 0f;
            if (flow == null || flow.State == null || flow.State.candidates.Count == 0) return 0;
            if (flow.Validator == null || !flow.Validator.Ready) return 0;

            int index;
            TrajectoryCandidate candidate = KvVariantKit.Selected(flow, out index);
            if (candidate == null) candidate = flow.State.candidates[0];
            if (candidate == null || candidate.plan == null) return 0;

            KvSmoothEntry entry = EntryOf(candidate);
            PlannedTrajectory basis = entry != null && entry.original != null ? entry.original : candidate.plan;

            LastSharpCorners = KvTrajMath.FindSharpCorners(flow.Validator, basis, CornerThresholdDeg);
            LastSharpAngleDeg = KvTrajMath.MaxCornerDeg(flow.Validator, basis);
            if (LastSharpCorners.Length == 0) return 0;

            // Ключ подсказки — фаза + точка + число вариантов (уровень/метод сюда НЕ входят:
            // смена ползунка не должна повторять одно и то же сообщение).
            string warnKey = flow.State.phase + "|" + flow.State.candidates.Count + "|" +
                             flow.State.point.x.ToString("0.000") + "," +
                             flow.State.point.y.ToString("0.000") + "," +
                             flow.State.point.z.ToString("0.000");
            if (cornersWarnedSignature != warnKey)
            {
                cornersWarnedSignature = warnKey;
                Report(KvLocExtra.F("smooth.corners.suggest",
                    "Излом пути {0}° (участков: {1}) — такой участок стоит сгладить: " +
                    "вкладка «Сглаживание траектории», опция «только изломы»",
                    LastSharpAngleDeg.ToString("0"), LastSharpCorners.Length));
            }
            return LastSharpCorners.Length;
        }

        /// <summary>Отчёт о фактическом рывке последнего сглаженного пути (ФИКС 1).</summary>
        public string LastJerkReport { get; private set; } = "";
        /// <summary>Уложился ли фактический рывок в предел.</summary>
        public bool LastJerkOk { get; private set; } = true;

        /// <summary>Применить сглаживание к выбранному варианту (или к лучшему).</summary>
        public bool ApplySelected(bool quiet = false)
        {
            int index;
            TrajectoryCandidate candidate = KvVariantKit.Selected(flow, out index);
            if (candidate == null)
            {
                if (!quiet) Report(KvVariantKit.SceneStatus(flow));
                return false;
            }
            return Apply(candidate, index, quiet);
        }

        /// <summary>Применить сглаживание ко ВСЕМ построенным вариантам (авторежим).</summary>
        public int ApplyAll(bool quiet = true)
        {
            if (flow == null || flow.State == null) return 0;
            int done = 0;
            for (int i = 0; i < flow.State.candidates.Count; i++)
                if (Apply(flow.State.candidates[i], i, quiet)) done++;
            return done;
        }

        private bool Apply(TrajectoryCandidate candidate, int index, bool quiet)
        {
            if (candidate == null || candidate.plan == null) return false;
            if (flow == null || flow.Validator == null || !flow.Validator.Ready)
            {
                if (!quiet) Report("сглаживание невозможно: валидатор робота не готов");
                return false;
            }
            if (Level <= 0.5f)
            {
                if (!quiet) Report("уровень сглаживания 0 % — обработка не нужна");
                return false;
            }

            KvSmoothEntry entry = EntryOf(candidate);
            PlannedTrajectory result = Preview(candidate);
            if (result == null || entry == null)
            {
                if (!quiet) Report("сглаживание не выполнено: план не рассчитан");
                return false;
            }

            entry.smoothed = result;
            entry.level = Level;
            entry.method = Method;
            entry.after = KvTrajMath.Analyze(flow.Validator, result, null, energy,
                KvVariantKit.RobotBase(flow));
            entry.applied = true;

            // Геометрия копируется В ТЕ ЖЕ массивы, что и у фантомов в полёте,
            // поэтому картинка не «отрывается» от расчёта.
            bool ok = KvVariantKit.ApplyPlan(flow, candidate, result);
            if (!ok)
            {
                if (!quiet) Report("сглаживание: план не применён");
                return false;
            }

            if (!quiet)
            {
                Report(KvLocExtra.T("smooth.pickone", "Вариант") + " " +
                       KvVariantKit.Label(candidate, index) + ": " +
                       KvLocExtra.T("smooth.done", "сглажено") + " (" + MethodLabel + ", " +
                       Level.ToString("0") + " %) · jerk " + entry.before.maxJerk.ToString("0") +
                       " → " + entry.after.maxJerk.ToString("0") + " °/с³ (" +
                       entry.JerkGain.ToString("+0.0;-0.0") + " %), кривизна " +
                       entry.before.curvature.ToString("0.00") + " → " +
                       entry.after.curvature.ToString("0.00") + " 1/м");
            }
            return true;
        }

        /// <summary>Вернуть исходный (несглаженный) план выбранного варианта.</summary>
        public bool ResetSelected()
        {
            int index;
            TrajectoryCandidate candidate = KvVariantKit.Selected(flow, out index);
            if (candidate == null || !entries.ContainsKey(candidate))
            {
                Report("возвращать нечего: вариант не обрабатывался");
                return false;
            }
            KvSmoothEntry entry = entries[candidate];
            if (entry.original == null)
            {
                Report("исходный план не сохранён");
                return false;
            }
            KvVariantKit.ApplyPlan(flow, candidate, KvTrajMath.Clone(entry.original, entry.original.Label));
            entry.applied = false;
            Report("вариант " + KvVariantKit.Label(candidate, index) + ": возвращена исходная траектория " +
                   "(jerk " + entry.before.maxJerk.ToString("0") + " °/с³)");
            return true;
        }

        // ------------------------------------------------------------------ авторежим

        /// <summary>
        /// Кадровое обслуживание: если включено «применять автоматически», сразу после
        /// планирования (появление новых вариантов) все они сглаживаются один раз.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (flow == null) return;

            // Чистим записи по исчезнувшим вариантам (иначе словарь растёт за смену).
            if (flow.State != null && entries.Count > flow.State.candidates.Count + 8)
            {
                prune.Clear();
                foreach (KeyValuePair<TrajectoryCandidate, KvSmoothEntry> kv in entries)
                    if (!flow.State.candidates.Contains(kv.Key)) prune.Add(kv.Key);
                for (int i = 0; i < prune.Count; i++) entries.Remove(prune[i]);
            }

            if (!KvVariantKit.Ready(flow)) return;

            string signature = Signature();

            // ФИКС 3 (§20): изломы ищем ДО выхода по авторежиму — подсказка «участок стоит
            // сгладить» нужна как раз тогда, когда сглаживание выключено. Стоимость — один
            // проход по сэмплам и только при смене состояния сцены.
            if (signature != cornersCheckedSignature)
            {
                cornersCheckedSignature = signature;
                DetectCorners();
            }

            if (!Auto || Level <= 0.5f) return;
            if (signature == appliedSignature) return;
            appliedSignature = signature;

            // ФИКС 7. Замер стоимости автосглаживания: видно, сколько миллисекунд ушло на
            // обработку и сколько вариантов она затронула. Режим «только выбранный» касается
            // ОДНОГО варианта вместо восьми — разница в этой строке и видна.
            System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
            int done = autoSelectedOnly ? (ApplySelected(true) ? 1 : 0) : ApplyAll(true);
            watch.Stop();

            LastAutoMs = (float)watch.Elapsed.TotalMilliseconds;
            LastAutoCount = done;
            LastAutoSelectedOnly = autoSelectedOnly;
            appliedCount += done;
            if (done > 0)
                Report(KvLocExtra.T("smooth.auto.on",
                           "Автоматическое сглаживание включено: новая точка — сразу сглаженные варианты") +
                       " → вариантов " + done + (autoSelectedOnly ? " (только выбранный)" : " (все)") +
                       ", метод " + MethodLabel + ", уровень " +
                       Level.ToString("0") + " % · " + LastAutoMs.ToString("0.0") + " мс");
        }

        /// <summary>Подпись «какие варианты уже обработаны» (точка + число вариантов + фаза).</summary>
        private string Signature()
        {
            if (flow == null || flow.State == null) return "";
            SelectionState s = flow.State;
            // ФИКС 7: в режиме «только выбранный» номер выбранного варианта входит в подпись —
            // иначе смена выбора не запускала бы сглаживание нового варианта.
            return s.phase + "|" + s.candidates.Count + "|" +
                   s.point.x.ToString("0.000") + "," + s.point.y.ToString("0.000") + "," +
                   s.point.z.ToString("0.000") + "|" + Level.ToString("0") + "|" + (int)Method +
                   "|" + (autoSelectedOnly ? s.selectedTrajectory.ToString() : "all");
        }

        /// <summary>Сбросить кэш метрик (после смены робота).</summary>
        public void ResetCache()
        {
            entries.Clear();
            appliedSignature = "";
            cornersCheckedSignature = "";
            cornersWarnedSignature = "";
            LastSharpCorners = new int[0];
            LastSharpAngleDeg = 0f;
        }

        private void Report(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            Debug.Log("[Smooth] " + text);
            if (Message != null) Message(text);
        }
    }

    /// <summary>
    /// ВКЛАДКА «СГЛАЖИВАНИЕ» окна-верстака (этап 4): ползунок уровня, выбор метода,
    /// тумблер авторежима и таблица метрик «До / После».
    /// </summary>
    public class KvSmoothTab : IKvWorkbenchTab
    {
        private readonly KvPathSmoothing service;
        private KvSegmented methodSegment;
        private bool toggleValue;

        public KvSmoothTab(KvPathSmoothing smoothing)
        {
            service = smoothing;
        }

        public string Key { get { return "smooth"; } }
        public string Title { get { return KvLocExtra.T("smooth.title", "Сглаживание траектории"); } }

        public void Build(KvTabKit kit)
        {
            if (service == null) return;

            kit.Section(Title);
            kit.Info(delegate { return KvVariantKit.SceneStatus(service.Flow()); }, KvTheme.Warn);

            kit.Slider(KvLocExtra.T("smooth.level", "Уровень сглаживания, %"), 0f, 100f,
                service.Level, "0", delegate (float v) { service.Level = v; });

            string[] methods =
            {
                KvLocExtra.T("smooth.method.bspline", "B-сплайн"),
                KvLocExtra.T("smooth.method.bezier", "Безье"),
                KvLocExtra.T("smooth.method.gauss", "Фильтр Гаусса")
            };
            methodSegment = kit.Segmented(KvLocExtra.T("smooth.method", "Метод"), methods,
                (int)service.Method, delegate (int i) { service.Method = (KvSmoothMethod)i; });

            toggleValue = service.Auto;
            kit.Toggle(KvLocExtra.T("smooth.auto", "Применять автоматически после планирования"),
                service.Auto, delegate (bool v)
                {
                    toggleValue = v;
                    service.Auto = v;
                });

            // ФИКС 7: стоимость автосглаживания. Включённая опция обрабатывает ОДИН вариант
            // (выбранный), а не все 8 сразу — появление траекторий заметно быстрее.
            kit.Toggle(KvLocExtra.T("smooth.auto.selected",
                    "Сглаживать только выбранный вариант (быстрее)"),
                service.AutoSelectedOnly, delegate (bool v) { service.AutoSelectedOnly = v; });

            // ФИКС 3 (§20): точечное сглаживание изломов. Ползунок уровня при этом задаёт
            // силу обработки, но трогается только окрестность найденных изломов.
            kit.Toggle(KvLocExtra.T("smooth.corners.only",
                    "Точечно сглаживать изломы (только проблемные участки)"),
                service.CornersOnly, delegate (bool v) { service.CornersOnly = v; });

            // ФИКС 3: подсветка изломов текущего варианта. Излом — это форма пути:
            // предел скорости вдоль пути в такой точке близок к нулю, поэтому участок «вязнет».
            kit.Info(delegate
            {
                int count = service.LastSharpCorners != null ? service.LastSharpCorners.Length : 0;
                if (count == 0)
                    return KvLocExtra.F("smooth.corners.none",
                        "Изломов нет (порог {0}°): путь гладкий, точечное сглаживание не нужно",
                        service.CornerThresholdDeg.ToString("0"));
                return KvLocExtra.F("smooth.corners.found",
                    "Изломов: {0} · самый острый {1}° (порог {2}°) — предлагается сгладить эти участки",
                    count, service.LastSharpAngleDeg.ToString("0"),
                    service.CornerThresholdDeg.ToString("0"));
            }, KvTheme.Warn);

            kit.Info(delegate
            {
                if (!service.Auto) return KvLocExtra.T("smooth.auto.off",
                    "Автосглаживание выключено — сглаживание применяется только по кнопке.");
                if (service.LastAutoCount <= 0) return KvLocExtra.T("smooth.auto.wait",
                    "Ждём первую точку: после планирования здесь появится время обработки.");
                return KvLocExtra.T("smooth.auto.cost", "Последнее автосглаживание: ") +
                       service.LastAutoMs.ToString("0.0") + " мс · вариантов " +
                       service.LastAutoCount +
                       (service.LastAutoSelectedOnly ? " (только выбранный)" : " (все 8)");
            }, KvTheme.Accent);

            kit.Buttons(new[]
            {
                KvLocExtra.T("smooth.apply", "Сгладить выбранную"),
                KvLocExtra.T("smooth.reset", "Вернуть исходную")
            }, new Action[]
            {
                delegate { service.ApplySelected(false); },
                delegate { service.ResetSelected(); }
            });

            kit.Section(KvLocExtra.T("wb.variant", "Вариант"));
            kit.Table(KvLocExtra.T("wb.metric", "Метрика"),
                delegate { return KvLocExtra.T("smooth.before", "До"); },
                delegate { return KvLocExtra.T("smooth.after", "После"); });

            AddRow(kit, KvLocExtra.T("smooth.jerk", "Jerk (рывок), °/с³"),
                delegate (KvSmoothEntry e) { return e.Reference.maxJerk.ToString("0"); },
                delegate (KvSmoothEntry e) { return e.after.maxJerk.ToString("0") + "  (" +
                    e.JerkGain.ToString("+0.0;-0.0") + " %)"; });
            AddRow(kit, KvLocExtra.T("smooth.accel", "Ускорение, °/с²"),
                delegate (KvSmoothEntry e) { return e.Reference.maxAcc.ToString("0"); },
                delegate (KvSmoothEntry e) { return e.after.maxAcc.ToString("0") + "  (" +
                    e.AccGain.ToString("+0.0;-0.0") + " %)"; });
            AddRow(kit, KvLocExtra.T("smooth.curvature", "Кривизна, 1/м"),
                delegate (KvSmoothEntry e) { return e.Reference.curvature.ToString("0.000"); },
                delegate (KvSmoothEntry e) { return e.after.curvature.ToString("0.000") + "  (" +
                    e.CurvatureGain.ToString("+0.0;-0.0") + " %)"; });
            AddRow(kit, KvLocExtra.T("smooth.length", "Длина пути TCP, м"),
                delegate (KvSmoothEntry e) { return e.Reference.length.ToString("0.000"); },
                delegate (KvSmoothEntry e) { return e.after.length.ToString("0.000"); });
            AddRow(kit, KvLocExtra.T("smooth.time", "Время, с"),
                delegate (KvSmoothEntry e) { return e.Reference.time.ToString("0.000"); },
                delegate (KvSmoothEntry e) { return e.after.time.ToString("0.000") + "  (" +
                    e.TimeDelta.ToString("+0.000;-0.000") + " с)"; });

            kit.Divider();
            kit.Table(KvLocExtra.T("smooth.planner", "План планировщика (как построен)"),
                delegate
                {
                    KvSmoothEntry e = Current();
                    return e != null
                        ? "jerk " + e.before.maxJerk.ToString("0") + " · a " +
                          e.before.maxAcc.ToString("0") + " · t " + e.before.time.ToString("0.000")
                        : "—";
                },
                delegate
                {
                    KvSmoothEntry e = Current();
                    return e != null
                        ? "v " + e.before.maxVel.ToString("0") + " °/с · L " +
                          e.before.length.ToString("0.000") + " м"
                        : "—";
                });
            kit.Divider();
            kit.Note(KvLocExtra.T("smooth.info",
                "Постобработка после планирования: путь сглаживается в пространстве суставов, " +
                "затем время пересчитывается по лимитам скорости и ускорения."), KvTheme.TextDim);
            kit.Note(KvLocExtra.T("smooth.retime.note",
                "Время пересчитывается по лимитам: у планировщика оно с большим запасом, поэтому " +
                "после постобработки траектория становится заметно быстрее."), KvTheme.Warn);
        }

        private void AddRow(KvTabKit kit, string caption, Func<KvSmoothEntry, string> before,
            Func<KvSmoothEntry, string> after)
        {
            kit.Table(caption,
                delegate
                {
                    KvSmoothEntry e = Current();
                    return e != null ? before(e) : "—";
                },
                delegate
                {
                    KvSmoothEntry e = Current();
                    return e != null && e.applied ? after(e) : "—";
                });
        }

        private KvSmoothEntry Current()
        {
            if (service == null) return null;
            int index;
            return service.CurrentEntry(out index);
        }

        public void Tick() { }

        public void Refresh()
        {
            if (methodSegment != null && methodSegment.Index != (int)service.Method)
                methodSegment.Set((int)service.Method);
        }
    }
}
