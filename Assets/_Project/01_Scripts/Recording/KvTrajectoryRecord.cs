using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace KazistovVvFeatures
{
    /// <summary>
    /// Один сэмпл записи движения (ЭТАП 1 ТЗ).
    /// Хранит ровно то, что требует ТЗ: углы суставов, время (timestamp) и TCP-координаты.
    /// Массивы, а не Vector3/double[] — так <see cref="JsonUtility"/> пишет их без обёрток,
    /// а файл остаётся читаемым человеком.
    /// </summary>
    [Serializable]
    public class KvRecordSample
    {
        /// <summary>Время от начала записи, с.</summary>
        public float t;
        /// <summary>Углы суставов, град (у SCARA ось Z — призматическая, в тех же единицах, что робот).</summary>
        public float[] q;
        /// <summary>TCP в МИРОВЫХ координатах: x, y, z.</summary>
        public float[] tcp;
        /// <summary>TCP-поворот (кватернион xyzw) — для полноты записи; в расчётах не участвует.</summary>
        public float[] rot;

        public Vector3 TcpVector
        {
            get
            {
                return tcp != null && tcp.Length >= 3
                    ? new Vector3(tcp[0], tcp[1], tcp[2]) : Vector3.zero;
            }
        }

        public double[] CloneQ()
        {
            if (q == null) return null;
            double[] result = new double[q.Length];
            for (int i = 0; i < q.Length; i++) result[i] = q[i];
            return result;
        }
    }

    /// <summary>
    /// Записанная траектория целиком (ЭТАП 1 ТЗ). Формат — JSON (`FeatureStorage`),
    /// поэтому новые поля можно добавлять без ломки уже сохранённых файлов:
    /// <see cref="JsonUtility"/> заполняет только присутствующие поля, а
    /// <see cref="Normalize"/> чинит старые/битые записи.
    /// </summary>
    [Serializable]
    public class KvTrajectoryRecord
    {
        /// <summary>Версия схемы файла (растёт при несовместимых изменениях).</summary>
        public int version = 1;
        public string id = "";
        public string name = "Запись";
        /// <summary>Имя робота, на котором писали (чтобы не проигрывать чужую кинематику).</summary>
        public string robot = "";
        /// <summary>Когда создана (yyyy-MM-dd HH:mm:ss).</summary>
        public string created = "";
        /// <summary>Источник: live (реальный робот), phantom (фантом), plan (вариант траектории).</summary>
        public string source = "live";
        /// <summary>Частота сэмплирования при записи, Гц.</summary>
        public float rate = 20f;
        /// <summary>Длительность записи, с.</summary>
        public float duration;
        /// <summary>Длина пути TCP, м.</summary>
        public float length;
        /// <summary>Множитель скорости воспроизведения, сохранённый вместе с записью.</summary>
        public float speed = 1f;
        /// <summary>Свободная заметка оператора.</summary>
        public string notes = "";
        public List<KvRecordSample> samples = new List<KvRecordSample>();

        /// <summary>Файл, из которого запись прочитана (пусто для только что созданной).</summary>
        [NonSerialized] public string filePath = "";

        public int SampleCount { get { return samples != null ? samples.Count : 0; } }

        /// <summary>Число суставов в записи (по первому сэмплу).</summary>
        public int Dof
        {
            get
            {
                if (samples == null) return 0;
                for (int i = 0; i < samples.Count; i++)
                    if (samples[i] != null && samples[i].q != null) return samples[i].q.Length;
                return 0;
            }
        }

        /// <summary>Починить запись после чтения: поля, списки, монотонное время, длины.</summary>
        public void Normalize()
        {
            if (version <= 0) version = 1;
            if (samples == null) samples = new List<KvRecordSample>();
            samples.RemoveAll(delegate (KvRecordSample s) { return s == null || s.q == null || s.q.Length == 0; });
            if (string.IsNullOrEmpty(id)) id = Guid.NewGuid().ToString("N");
            if (string.IsNullOrEmpty(name)) name = "Запись";
            if (rate <= 0f) rate = 20f;
            if (speed <= 0f) speed = 1f;

            float t = 0f;
            for (int i = 0; i < samples.Count; i++) samples[i].t = t = Mathf.Max(t, samples[i].t);

            if (duration <= 0f) duration = samples.Count > 0 ? samples[samples.Count - 1].t : 0f;
            if (length <= 0f) length = ComputeLength();
        }

        /// <summary>Длина TCP-пути по сэмплам, м (пересчитывается, если не сохранена).</summary>
        public float ComputeLength()
        {
            float len = 0f;
            if (samples == null) return 0f;
            for (int i = 1; i < samples.Count; i++)
            {
                if (samples[i - 1].tcp == null || samples[i].tcp == null) continue;
                len += Vector3.Distance(samples[i - 1].TcpVector, samples[i].TcpVector);
            }
            return len;
        }

        /// <summary>Подпись для дерева моделей и списков.</summary>
        public string ShortLabel
        {
            get
            {
                return name + " · " + SampleCount + " т. · " + duration.ToString("0.0") + " с";
            }
        }

        /// <summary>Полное описание для подсказки.</summary>
        public string Tooltip
        {
            get
            {
                return name +
                       "\nРобот: " + (string.IsNullOrEmpty(robot) ? "—" : robot) +
                       "\nИсточник: " + SourceLabel +
                       "\nСоздана: " + created +
                       "\nСэмплов: " + SampleCount + " · частота " + rate.ToString("0") + " Гц" +
                       "\nДлительность: " + duration.ToString("0.00") + " с" +
                       "\nПуть TCP: " + length.ToString("0.000") + " м" +
                       (string.IsNullOrEmpty(notes) ? "" : "\n" + notes);
            }
        }

        public string SourceLabel
        {
            get
            {
                switch (source)
                {
                    case "phantom": return "фантом";
                    case "plan": return "вариант траектории";
                    case "manual": return "ручное ведение суставов";
                    default: return "реальный робот";
                }
            }
        }

        /// <summary>
        /// Превратить запись в траекторию ядра (`TrajectoryCore.PlannedTrajectory`) —
        /// так запись можно отдать фантому и планировщику «как обычную траекторию» (ТЗ этап 1).
        /// Времена масштабируются множителем скорости (1 — как записано).
        /// </summary>
        public TrajectoryCore.PlannedTrajectory ToPlannedTrajectory(float speedMultiplier = 1f)
        {
            int n = SampleCount;
            if (n < 2) return null;

            float k = Mathf.Clamp(speedMultiplier <= 0f ? 1f : speedMultiplier, 0.05f, 20f);

            TrajectoryCore.PlannedTrajectory plan = new TrajectoryCore.PlannedTrajectory();
            plan.Label = name;
            plan.Path = new double[n][];
            plan.Times = new float[n];
            for (int i = 0; i < n; i++)
            {
                plan.Path[i] = samples[i].CloneQ();
                plan.Times[i] = samples[i].t / k;
            }
            plan.Time = plan.Times[n - 1];
            plan.Length = length;
            plan.MinClearance = 0.02f;   // пересчитывается исполнителем/heatmap; не блокирует запуск
            plan.LimitMargin = 180f;
            return plan;
        }
    }

    /// <summary>
    /// Хранилище записей на диске (папка `Recordings` внутри данных функций).
    /// Записи читаются лениво (по требованию дерева/списка), файлы можно копировать руками.
    /// </summary>
    public static class KvRecordStore
    {
        public const string Extension = ".json";

        /// <summary>Прочитать все записи каталога (битые пропускаются с предупреждением).</summary>
        public static List<KvTrajectoryRecord> LoadAll()
        {
            List<KvTrajectoryRecord> result = new List<KvTrajectoryRecord>();
            foreach (string file in FeatureStorage.ListFiles(FeatureStorage.RecordingsDir, "*" + Extension))
            {
                KvTrajectoryRecord rec = Load(file);
                if (rec != null) result.Add(rec);
            }
            result.Sort(delegate (KvTrajectoryRecord a, KvTrajectoryRecord b)
            {
                return string.CompareOrdinal(b.created, a.created);   // новые сверху
            });
            return result;
        }

        public static KvTrajectoryRecord Load(string path)
        {
            KvTrajectoryRecord rec = FeatureStorage.LoadJson<KvTrajectoryRecord>(path);
            if (rec == null) return null;
            rec.filePath = path;
            rec.Normalize();
            if (rec.SampleCount < 2)
            {
                Debug.LogWarning("[Record] «" + FeatureStorage.FileStem(path) + "» пропущена: сэмплов " +
                                 rec.SampleCount + " (нужно ≥ 2)");
                return null;
            }
            return rec;
        }

        /// <summary>Сохранить запись; возвращает путь файла или null.</summary>
        public static string Save(KvTrajectoryRecord rec)
        {
            if (rec == null || rec.SampleCount < 2) return null;
            rec.Normalize();
            if (string.IsNullOrEmpty(rec.filePath))
            {
                string file = FeatureStorage.SafeName(rec.name, "record") + "_" +
                              FeatureStorage.TimeStamp() + Extension;
                rec.filePath = Path.Combine(FeatureStorage.RecordingsDir, file);
            }
            return FeatureStorage.SaveJson(rec.filePath, rec) ? rec.filePath : null;
        }

        public static bool Delete(KvTrajectoryRecord rec)
        {
            if (rec == null) return false;
            bool ok = FeatureStorage.DeleteFile(rec.filePath);
            if (ok) Debug.Log("[Record] удалена запись «" + rec.name + "» (" + rec.filePath + ")");
            return ok;
        }

        /// <summary>Переименовать запись (файл тоже переименовывается — иначе имя «уедет»).</summary>
        public static bool Rename(KvTrajectoryRecord rec, string newName)
        {
            if (rec == null || string.IsNullOrEmpty(newName)) return false;
            string old = rec.filePath;
            rec.name = newName;
            if (!string.IsNullOrEmpty(old) && File.Exists(old))
            {
                string dir = Path.GetDirectoryName(old);
                string target = Path.Combine(dir, FeatureStorage.SafeName(newName, "record") + Extension);
                if (!string.Equals(target, old, StringComparison.OrdinalIgnoreCase))
                {
                    if (File.Exists(target)) target = Path.Combine(dir,
                        FeatureStorage.SafeName(newName, "record") + "_" + FeatureStorage.TimeStamp() + Extension);
                    try
                    {
                        File.Move(old, target);
                        rec.filePath = target;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("[Record] файл не переименован: " + e.Message);
                    }
                }
            }
            return Save(rec) != null;
        }
    }
}
