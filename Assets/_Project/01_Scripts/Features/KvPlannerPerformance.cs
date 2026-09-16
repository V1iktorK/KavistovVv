using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace KazistovVvFeatures
{
    /// <summary>Итог одного прогона генерации вариантов (ЭТАП 14 ТЗ).</summary>
    [Serializable]
    public class KvPlanRun
    {
        public string created = "";
        public string robot = "";
        public int requested;          // сколько вариантов запрошено (trajectoryCount)
        public int produced;           // сколько получилось на самом деле
        public float seconds;          // сколько заняла генерация
        public int attempts;           // попыток планирования
        public int failures;           // попыток без пути
        public int duplicates;         // отфильтровано дубликатов
        public int ikFound;            // конфигураций IK найдено
        public int ikUsable;           // из них прошли лимиты
        public int plannerIterations;  // итераций последней попытки BiRRT
        public float pointX, pointY, pointZ;
    }

    /// <summary>
    /// ЗАМЕР ПРОИЗВОДИТЕЛЬНОСТИ ПЛАНИРОВЩИКА (ЭТАП 14 ТЗ).
    ///
    /// Считает и показывает по КАЖДОМУ прогону генерации:
    ///   • сколько времени ушло на генерацию вариантов траекторий;
    ///   • сколько было попыток планирования и сколько итераций у планировщика (IK/RRT);
    ///   • сколько конфигураций IK нашлось и сколько из них годны;
    ///   • сколько вариантов отфильтровано как дубликаты.
    ///
    /// Данные берутся из уже существующих счётчиков потока (добавлены только читающие
    /// свойства — планировщик, IK и оракул не менялись). История прогонов пишется в
    /// `Logs/planner_runs.json` и в журнал действий; в `PROJECT_CONTEXT.md` результаты
    /// переносятся вручную (по ТЗ — «после каждого теста»).
    /// </summary>
    public class KvPlannerPerformance
    {
        /// <summary>Сколько прогонов держать в истории.</summary>
        public int historySize = 24;

        public event Action<KvPlanRun> RunFinished;

        private readonly List<KvPlanRun> history = new List<KvPlanRun>();
        private TrajectoryFlowController flow;

        private bool wasGenerating;
        private float startedAt;
        private int attemptsAtStart;
        private int failuresAtStart;
        private int duplicatesAtStart;
        private Vector3 pointAtStart;

        public IReadOnlyList<KvPlanRun> History { get { return history; } }
        public bool Generating { get { return flow != null && flow.Generating; } }

        /// <summary>Сколько секунд уже идёт генерация (0 — не идёт).</summary>
        public float CurrentSeconds
        {
            get { return Generating ? Time.realtimeSinceStartup - startedAt : 0f; }
        }

        public KvPlanRun Last { get { return history.Count > 0 ? history[history.Count - 1] : null; } }

        public void Bind(TrajectoryFlowController controller)
        {
            flow = controller;
            try
            {
                KvPlanRunList stored = FeatureStorage.LoadJson<KvPlanRunList>(
                    System.IO.Path.Combine(FeatureStorage.LogsDir, "planner_runs.json"));
                if (stored != null && stored.runs != null)
                {
                    history.Clear();
                    history.AddRange(stored.runs);
                }
            }
            catch (Exception)
            {
                // история — необязательная: пустой список тоже корректен
            }
        }

        /// <summary>Кадровое наблюдение за генерацией (вызывает хаб).</summary>
        public void Tick()
        {
            if (flow == null) return;

            bool generating = flow.Generating;
            if (generating && !wasGenerating)
            {
                startedAt = Time.realtimeSinceStartup;
                attemptsAtStart = flow.PlanAttempts;
                failuresAtStart = flow.PlanFailures;
                duplicatesAtStart = flow.DuplicatesFiltered;
                pointAtStart = flow.State.hasPoint ? flow.State.point : Vector3.zero;
            }
            else if (!generating && wasGenerating)
            {
                KvPlanRun run = new KvPlanRun
                {
                    created = FeatureStorage.IsoNow(),
                    robot = flow.Validator != null ? flow.Validator.RobotName : "",
                    requested = flow.PlanTargetCount,
                    produced = flow.State.candidates.Count,
                    seconds = Time.realtimeSinceStartup - startedAt,
                    attempts = flow.PlanAttempts - attemptsAtStart,
                    failures = flow.PlanFailures - failuresAtStart,
                    duplicates = flow.DuplicatesFiltered - duplicatesAtStart,
                    ikFound = flow.IkBranchesFound,
                    ikUsable = flow.IkBranchesUsable,
                    plannerIterations = flow.PlannerIterations,
                    pointX = pointAtStart.x,
                    pointY = pointAtStart.y,
                    pointZ = pointAtStart.z
                };

                history.Add(run);
                while (history.Count > Mathf.Max(2, historySize)) history.RemoveAt(0);
                Save();

                Debug.Log("[Perf] " + Summary(run));
                if (RunFinished != null) RunFinished(run);
            }
            wasGenerating = generating;
        }

        /// <summary>Строка отчёта по прогону (для панели и журнала).</summary>
        public static string Summary(KvPlanRun run)
        {
            if (run == null) return "прогонов планирования ещё не было";
            StringBuilder sb = new StringBuilder();
            sb.Append("генерация ").Append(run.seconds.ToString("0.000")).Append(" с");
            sb.Append(" · вариантов ").Append(run.produced).Append('/').Append(run.requested);
            sb.Append(" · попыток ").Append(run.attempts);
            sb.Append(" (без пути ").Append(run.failures).Append(')');
            sb.Append(" · дубликатов отброшено ").Append(run.duplicates);
            sb.Append(" · IK найдено ").Append(run.ikFound).Append(", годных ").Append(run.ikUsable);
            sb.Append(" · итераций планировщика ").Append(run.plannerIterations);
            return sb.ToString();
        }

        /// <summary>Строки для панели метрик/производительности.</summary>
        public List<string> Lines()
        {
            List<string> lines = new List<string>();
            if (flow != null)
            {
                lines.Add("Идёт генерация: " + (Generating ? "да · " + CurrentSeconds.ToString("0.00") + " с" : "нет"));
                lines.Add("Запрошено вариантов: " + flow.PlanTargetCount);
                lines.Add("Готово вариантов: " + flow.State.candidates.Count +
                          " · в очереди: " + flow.PlanQueueLength);
                lines.Add("Попыток планирования: " + flow.PlanAttempts +
                          " · без пути: " + flow.PlanFailures);
                lines.Add("Отброшено дубликатов: " + flow.DuplicatesFiltered);
                lines.Add("Конфигураций IK: найдено " + flow.IkBranchesFound +
                          " · годных " + flow.IkBranchesUsable);
                lines.Add("Итераций планировщика (последняя попытка): " + flow.PlannerIterations);
                if (!string.IsNullOrEmpty(flow.PlannerDebug))
                    lines.Add("Отчёт планировщика: " + flow.PlannerDebug);
                if (!string.IsNullOrEmpty(flow.PlannerBranchInfo))
                    lines.Add("Ветви IK: " + flow.PlannerBranchInfo);
            }

            lines.Add("— последние прогоны —");
            int from = Mathf.Max(0, history.Count - 8);
            for (int i = history.Count - 1; i >= from; i--)
                lines.Add(history[i].created.Substring(Mathf.Max(0, history[i].created.Length - 8)) + " · " +
                          Summary(history[i]));
            return lines;
        }

        /// <summary>Среднее время генерации по истории (0 — данных нет).</summary>
        public float AverageSeconds()
        {
            if (history.Count == 0) return 0f;
            float sum = 0f;
            for (int i = 0; i < history.Count; i++) sum += history[i].seconds;
            return sum / history.Count;
        }

        private void Save()
        {
            KvPlanRunList list = new KvPlanRunList();
            list.runs = new List<KvPlanRun>(history);
            FeatureStorage.SaveJson(System.IO.Path.Combine(FeatureStorage.LogsDir, "planner_runs.json"), list);
        }

        [Serializable]
        public class KvPlanRunList
        {
            public int version = 1;
            public List<KvPlanRun> runs = new List<KvPlanRun>();
        }
    }
}
