using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace KazistovVvFeatures
{
    /// <summary>Тип события журнала действий (ЭТАП 13 ТЗ) — по нему работает фильтр.</summary>
    public enum KvLogKind
    {
        System,
        Point,
        Trajectory,
        Motion,
        Stop,
        Zone,
        Pose,
        Record,
        Session,
        Scenario,
        Error,
        Ui,
        /// <summary>
        /// Предупреждение (добавлено на этапе 36 ТЗ: уровни «информация / предупреждение / ошибка»).
        /// Значение добавлено В КОНЕЦ списка, поэтому уже сохранённые журналы читаются как раньше.
        /// </summary>
        Warning
    }

    /// <summary>Одна запись журнала.</summary>
    [Serializable]
    public class KvLogEntry
    {
        /// <summary>Секунды от старта приложения (для сортировки).</summary>
        public float time;
        /// <summary>Время суток чч:мм:сс.</summary>
        public string stamp = "";
        /// <summary>Тип события (для фильтра).</summary>
        public KvLogKind kind = KvLogKind.System;
        public string text = "";

        public string Line
        {
            get { return stamp + " [" + KvActionLog.KindLabel(kind) + "] " + text; }
        }
    }

    /// <summary>
    /// ЖУРНАЛ ДЕЙСТВИЙ (ЭТАП 13 ТЗ).
    ///
    /// Прокручиваемый список событий: «точка выбрана», «траектория №3 подтверждена»,
    /// «робот начал движение», «аварийная остановка» и т.п. Хранится в кольцевом буфере
    /// (последние `capacity` записей) И на диске — каждая строка дописывается в файл
    /// сразу (JSON Lines + читаемый текст), поэтому журнал переживает падение/выход.
    ///
    /// Фильтр по типу — свойство <see cref="Filter"/> (null = все типы).
    /// Подписка на новые записи — событие <see cref="Changed"/>.
    /// </summary>
    public class KvActionLog
    {
        /// <summary>Сколько записей держать в памяти.</summary>
        public int capacity = 600;
        /// <summary>Дублировать ли записи в консоль Unity (по умолчанию — только важные).</summary>
        public bool echoToConsole;

        public event Action Changed;

        private readonly List<KvLogEntry> entries = new List<KvLogEntry>();
        private KvLogKind? filter;

        private static KvActionLog instance;

        /// <summary>Общий журнал приложения (создаётся при первом обращении).</summary>
        public static KvActionLog Instance
        {
            get
            {
                if (instance == null) instance = new KvActionLog();
                return instance;
            }
        }

        /// <summary>Все записи (новые в конце).</summary>
        public IReadOnlyList<KvLogEntry> Entries { get { return entries; } }

        public int Count { get { return entries.Count; } }

        /// <summary>Активный фильтр (null — показывать все типы).</summary>
        public KvLogKind? Filter
        {
            get { return filter; }
            set { filter = value; Raise(); }
        }

        /// <summary>Файл журнала (текстовый, дописывается построчно).</summary>
        public string FilePath
        {
            get { return Path.Combine(FeatureStorage.LogsDir, "actions_" + DateTime.Now.ToString("yyyyMMdd") + ".log"); }
        }

        /// <summary>Добавить событие.</summary>
        public void Add(KvLogKind kind, string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            KvLogEntry e = new KvLogEntry
            {
                time = Time.realtimeSinceStartup,
                stamp = DateTime.Now.ToString("HH:mm:ss"),
                kind = kind,
                text = text
            };
            entries.Add(e);
            if (entries.Count > Mathf.Max(20, capacity)) entries.RemoveRange(0, entries.Count - capacity);

            FeatureStorage.AppendLine(FilePath, e.Line);
            if (echoToConsole || kind == KvLogKind.Error)
            {
                if (kind == KvLogKind.Error) Debug.LogWarning("[Log] " + e.text);
                else Debug.Log("[Log] " + e.text);
            }

            // Запись, не проходящую фильтр, всё равно сохранили — панель просто не покажет её.
            Raise();
        }

        public void Info(string text) { Add(KvLogKind.System, text); }
        public void Error(string text) { Add(KvLogKind.Error, text); }

        /// <summary>Предупреждение (этап 36 ТЗ): не ошибка, но требует внимания оператора.</summary>
        public void Warning(string text) { Add(KvLogKind.Warning, text); }

        /// <summary>Записи с учётом фильтра (для панели журнала).</summary>
        public List<KvLogEntry> Filtered()
        {
            if (filter == null) return new List<KvLogEntry>(entries);
            List<KvLogEntry> result = new List<KvLogEntry>();
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].kind == filter.Value) result.Add(entries[i]);
            return result;
        }

        /// <summary>Последние N записей с учётом фильтра (для прокрутки «в конец»).</summary>
        public List<KvLogEntry> Tail(int count)
        {
            List<KvLogEntry> all = Filtered();
            if (all.Count <= count) return all;
            return all.GetRange(all.Count - count, count);
        }

        public void Clear()
        {
            entries.Clear();
            Raise();
        }

        /// <summary>Тип события по строке настроек (для внешней схемы).</summary>
        public static bool TryParseKind(string name, out KvLogKind kind)
        {
            kind = KvLogKind.System;
            if (string.IsNullOrEmpty(name)) return false;
            try
            {
                kind = (KvLogKind)Enum.Parse(typeof(KvLogKind), name, true);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string KindLabel(KvLogKind kind)
        {
            switch (kind)
            {
                case KvLogKind.Point: return "точка";
                case KvLogKind.Trajectory: return "траектория";
                case KvLogKind.Motion: return "движение";
                case KvLogKind.Stop: return "стоп";
                case KvLogKind.Zone: return "зоны";
                case KvLogKind.Pose: return "позы";
                case KvLogKind.Record: return "запись";
                case KvLogKind.Session: return "сессия";
                case KvLogKind.Scenario: return "сценарий";
                case KvLogKind.Error: return "ошибка";
                case KvLogKind.Warning: return "предупреждение";
                case KvLogKind.Ui: return "интерфейс";
                default: return "система";
            }
        }

        /// <summary>Короткое имя типа для колонки фильтра.</summary>
        public static string ShortKind(KvLogKind kind)
        {
            switch (kind)
            {
                case KvLogKind.Point: return "ТЧК";
                case KvLogKind.Trajectory: return "ТРЖ";
                case KvLogKind.Motion: return "ДВЖ";
                case KvLogKind.Stop: return "СТП";
                case KvLogKind.Zone: return "ЗОН";
                case KvLogKind.Pose: return "ПОЗ";
                case KvLogKind.Record: return "ЗПС";
                case KvLogKind.Session: return "СЕС";
                case KvLogKind.Scenario: return "СЦН";
                case KvLogKind.Error: return "ОШБ";
                case KvLogKind.Warning: return "ПРД";
                case KvLogKind.Ui: return "ИНТ";
                default: return "СИС";
            }
        }

        public static Color KindColor(KvLogKind kind)
        {
            switch (kind)
            {
                case KvLogKind.Error: return new Color(1f, 0.28f, 0.32f);
                case KvLogKind.Warning: return new Color(1f, 0.75f, 0.2f);
                case KvLogKind.Stop: return new Color(1f, 0.55f, 0.15f);
                case KvLogKind.Motion: return new Color(0.45f, 0.85f, 1f);
                case KvLogKind.Point: return new Color(0.95f, 0.45f, 0.45f);
                case KvLogKind.Trajectory: return new Color(0.95f, 0.8f, 0.3f);
                case KvLogKind.Zone: return new Color(1f, 0.5f, 0.5f);
                case KvLogKind.Pose: return new Color(0.6f, 1f, 0.6f);
                case KvLogKind.Record: return new Color(0.75f, 0.7f, 1f);
                case KvLogKind.Session: return new Color(0.7f, 0.9f, 0.95f);
                case KvLogKind.Scenario: return new Color(0.95f, 0.75f, 0.95f);
                default: return new Color(0.75f, 0.78f, 0.82f);
            }
        }

        /// <summary>Выгрузить весь буфер в отдельный файл (кнопка «Экспорт журнала»).</summary>
        public string Export()
        {
            string path = Path.Combine(FeatureStorage.LogsDir, "actions_export_" + FeatureStorage.TimeStamp() + ".log");
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < entries.Count; i++) sb.AppendLine(entries[i].Line);
            try
            {
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
                Add(KvLogKind.System, "журнал выгружен: " + path);
                return path;
            }
            catch (Exception e)
            {
                Error("не удалось выгрузить журнал: " + e.Message);
                return null;
            }
        }

        private void Raise()
        {
            if (Changed != null) Changed();
        }
    }
}
