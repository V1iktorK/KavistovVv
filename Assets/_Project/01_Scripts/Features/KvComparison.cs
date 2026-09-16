using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TrajectoryCore;

namespace KazistovVvFeatures
{
    /// <summary>Метрики одной траектории для сравнения (ЭТАП 6 ТЗ).</summary>
    public struct KvTrajMetrics
    {
        public int index;
        public string label;
        public float lengthM;
        public float timeS;
        public float curvatureDeg;
        public float clearanceMm;
        public float limitMarginDeg;
        public double score;
        public int samples;
        public bool safe;
        public bool dangerous;      // пересекает зону запрета
        public string why;
    }

    /// <summary>
    /// СРАВНЕНИЕ ТРАЕКТОРИЙ (ЭТАП 6 ТЗ).
    ///
    /// Из восьми вариантов выбираются ДВЕ траектории (слоты A и B) — по ТЗ «например,
    /// чекбоксами в дереве»: дерево проекта не поддерживает чекбоксы и мультивыбор,
    /// поэтому отметка выполняется командой (кнопка в панели сравнения и пункт меню)
    /// на ВЫБРАННОМ в дереве узле траектории, а в дереве видно «A»/«B» рядом с длиной.
    ///
    /// Панель показывает метрики бок о бок: длина, время, кривизна, зазор, запас до лимитов,
    /// оценка; в сцене линии уже разного цвета (у каждого варианта свой оттенок из потока),
    /// кнопка «Переключиться» выбирает траекторию как обычный клик зелёным лазером.
    /// </summary>
    public class KvComparison
    {
        /// <summary>Индекс траектории в слоте A (-1 — пусто).</summary>
        public int SlotA = -1;
        /// <summary>Индекс траектории в слоте B (-1 — пусто).</summary>
        public int SlotB = -1;

        public event Action Changed;

        private TrajectoryFlowController flow;

        public void Bind(TrajectoryFlowController controller)
        {
            flow = controller;
        }

        private List<TrajectoryCandidate> Candidates
        {
            get { return flow != null ? flow.State.candidates : null; }
        }

        public int CandidateCount { get { return Candidates != null ? Candidates.Count : 0; } }

        /// <summary>Сбросить отметки (новая точка — новые траектории).</summary>
        public void Reset()
        {
            SlotA = -1;
            SlotB = -1;
            Raise();
        }

        /// <summary>Отметить траекторию в свободный слот (A, затем B, затем замена A).</summary>
        public string Mark(int index)
        {
            if (index < 0 || index >= CandidateCount) return "траектория не выбрана";
            if (SlotA == index)
            {
                SlotA = -1;
                Raise();
                return "метка A снята";
            }
            if (SlotB == index)
            {
                SlotB = -1;
                Raise();
                return "метка B снята";
            }
            if (SlotA < 0)
            {
                SlotA = index;
                Raise();
                return "траектория " + (index + 1) + " отмечена как A";
            }
            if (SlotB < 0)
            {
                SlotB = index;
                Raise();
                return "траектория " + (index + 1) + " отмечена как B";
            }
            SlotA = index;
            Raise();
            return "метка A перенесена на траекторию " + (index + 1) + " (B = " + (SlotB + 1) + ")";
        }

        /// <summary>Буква метки для узла дерева ("" — не отмечена).</summary>
        public string MarkOf(int index)
        {
            if (SlotA == index && SlotB == index) return "A/B";
            if (SlotA == index) return "A";
            if (SlotB == index) return "B";
            return "";
        }

        public bool Ready { get { return SlotA >= 0 && SlotB >= 0 && SlotA != SlotB; } }

        /// <summary>Метрики варианта по индексу.</summary>
        public KvTrajMetrics Metrics(int index)
        {
            KvTrajMetrics m = new KvTrajMetrics();
            m.index = index;
            List<TrajectoryCandidate> list = Candidates;
            if (list == null || index < 0 || index >= list.Count) return m;

            TrajectoryCandidate c = list[index];
            if (c == null) return m;
            m.label = string.IsNullOrEmpty(c.label) ? "Траектория " + (index + 1) : c.label;
            m.lengthM = c.lengthM;
            m.timeS = c.timeS;
            m.clearanceMm = c.minClearance * 1000f;
            m.limitMarginDeg = c.limitMarginDeg;
            m.score = c.score;
            m.safe = c.safe;
            m.why = c.why;
            m.samples = c.plan != null && c.plan.Path != null ? c.plan.Path.Length : 0;
            m.curvatureDeg = c.plan != null ? (float)c.plan.CurvatureTotal : 0f;
            if (KvZoneMarks.IsMarked(c))
            {
                m.dangerous = true;
                m.why = string.IsNullOrEmpty(m.why) ? KvZoneMarks.ReasonOf(c) : m.why;
            }
            return m;
        }

        /// <summary>Строки «метрика слева · значение A · значение B» для панели.</summary>
        public List<string[]> SideBySide()
        {
            List<string[]> rows = new List<string[]>();
            KvTrajMetrics a = Metrics(SlotA);
            KvTrajMetrics b = Metrics(SlotB);

            rows.Add(new[] { "Траектория", Label(a), Label(b) });
            rows.Add(new[] { "Длина, м", F(a.lengthM, "0.000"), F(b.lengthM, "0.000") });
            rows.Add(new[] { "Время, с", F(a.timeS, "0.0"), F(b.timeS, "0.0") });
            rows.Add(new[] { "Кривизна, °", F(a.curvatureDeg, "0"), F(b.curvatureDeg, "0") });
            rows.Add(new[] { "Зазор, мм", F(a.clearanceMm, "0"), F(b.clearanceMm, "0") });
            rows.Add(new[] { "Запас лимитов, °", F(a.limitMarginDeg, "0.0"), F(b.limitMarginDeg, "0.0") });
            rows.Add(new[] { "Оценка (меньше лучше)", F(a.score, "0.000"), F(b.score, "0.000") });
            rows.Add(new[] { "Сэмплов", a.samples.ToString(), b.samples.ToString() });
            rows.Add(new[] { "Зона запрета", a.dangerous ? "ПЕРЕСЕКАЕТ" : "нет", b.dangerous ? "ПЕРЕСЕКАЕТ" : "нет" });
            rows.Add(new[] { "Лучше по длине", a.lengthM <= b.lengthM ? "✓" : "", b.lengthM < a.lengthM ? "✓" : "" });
            rows.Add(new[] { "Лучше по времени", a.timeS <= b.timeS ? "✓" : "", b.timeS < a.timeS ? "✓" : "" });
            rows.Add(new[] { "Лучше по зазору", a.clearanceMm >= b.clearanceMm ? "✓" : "", b.clearanceMm > a.clearanceMm ? "✓" : "" });
            rows.Add(new[] { "Лучше по лимитам", a.limitMarginDeg >= b.limitMarginDeg ? "✓" : "", b.limitMarginDeg > a.limitMarginDeg ? "✓" : "" });
            return rows;
        }

        /// <summary>Короткий вывод «чем отличаются» (для журнала и подсказки).</summary>
        public string Verdict()
        {
            if (!Ready) return "отметьте две траектории (A и B)";
            KvTrajMetrics a = Metrics(SlotA);
            KvTrajMetrics b = Metrics(SlotB);
            StringBuilder sb = new StringBuilder();
            sb.Append("A «").Append(Label(a)).Append("» против B «").Append(Label(b)).Append("»: ");
            sb.Append("длина ").Append((b.lengthM - a.lengthM >= 0 ? "+" : "")).Append((b.lengthM - a.lengthM).ToString("0.000")).Append(" м");
            sb.Append(" · время ").Append((b.timeS - a.timeS >= 0 ? "+" : "")).Append((b.timeS - a.timeS).ToString("0.0")).Append(" с");
            sb.Append(" · зазор ").Append((b.clearanceMm - a.clearanceMm >= 0 ? "+" : "")).Append((b.clearanceMm - a.clearanceMm).ToString("0")).Append(" мм");
            sb.Append(" · запас ").Append((b.limitMarginDeg - a.limitMarginDeg >= 0 ? "+" : "")).Append((b.limitMarginDeg - a.limitMarginDeg).ToString("0.0")).Append("°");
            return sb.ToString();
        }

        /// <summary>Переключиться на одну из сравниваемых траекторий (как выбор зелёным лучом).</summary>
        public bool Activate(int index)
        {
            if (flow == null) return false;
            bool ok = flow.SelectCandidateByIndex(index);
            if (ok) Raise();
            return ok;
        }

        private static string Label(KvTrajMetrics m)
        {
            if (m.index < 0) return "—";
            string l = m.label;
            int cut = l.IndexOf(" · ", StringComparison.Ordinal);
            if (cut > 0) l = l.Substring(0, cut);
            return l;
        }

        private static string F(float value, string format)
        {
            return value.ToString(format);
        }

        private static string F(double value, string format)
        {
            return value.ToString(format);
        }

        private void Raise()
        {
            if (Changed != null) Changed();
        }
    }

    /// <summary>
    /// Отметка «траектория пересекает зону запрета» (ЭТАП 5 ТЗ).
    /// Живёт в `ConditionalWeakTable`-подобном виде: отметка привязана к САМОМУ объекту
    /// кандидата, поэтому не путается между пересчётами и не требует правок
    /// `TrajectoryCandidate` (файл ядра не меняется).
    /// </summary>
    public static class KvZoneMarks
    {
        private class Mark
        {
            public bool dangerous;
            public string reason = "";
            public float penetration;
        }

        private static readonly Dictionary<TrajectoryCandidate, Mark> marks =
            new Dictionary<TrajectoryCandidate, Mark>();

        public static void Set(TrajectoryCandidate candidate, bool dangerous, string reason, float penetration)
        {
            if (candidate == null) return;
            Mark m;
            if (!marks.TryGetValue(candidate, out m))
            {
                m = new Mark();
                marks[candidate] = m;
            }
            m.dangerous = dangerous;
            m.reason = reason ?? "";
            m.penetration = penetration;
        }

        public static bool IsMarked(TrajectoryCandidate candidate)
        {
            Mark m;
            return candidate != null && marks.TryGetValue(candidate, out m) && m.dangerous;
        }

        public static string ReasonOf(TrajectoryCandidate candidate)
        {
            Mark m;
            return candidate != null && marks.TryGetValue(candidate, out m) ? m.reason : "";
        }

        public static float PenetrationOf(TrajectoryCandidate candidate)
        {
            Mark m;
            return candidate != null && marks.TryGetValue(candidate, out m) ? m.penetration : 0f;
        }

        /// <summary>Убрать отметки, которых уже нет в текущем списке кандидатов (без утечки словаря).</summary>
        public static void Prune(List<TrajectoryCandidate> alive)
        {
            if (marks.Count == 0) return;
            List<TrajectoryCandidate> dead = new List<TrajectoryCandidate>();
            foreach (KeyValuePair<TrajectoryCandidate, Mark> pair in marks)
            {
                if (pair.Key == null || (alive != null && !alive.Contains(pair.Key))) dead.Add(pair.Key);
            }
            for (int i = 0; i < dead.Count; i++) marks.Remove(dead[i]);
        }

        public static void Clear()
        {
            marks.Clear();
        }
    }
}
