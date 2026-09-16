using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using KazistovVvUI;
using TrajectoryCore;

namespace KazistovVvFeatures
{
    /// <summary>Язык робота для экспорта траектории (ЭТАП 11 ТЗ).</summary>
    public enum KvRobotLanguage
    {
        /// <summary>KUKA KRL — файл trajectory.src.</summary>
        Krl = 0,
        /// <summary>FANUC KAREL — файл trajectory.kl.</summary>
        Karel = 1,
        /// <summary>ABB RAPID — файл trajectory.mod.</summary>
        Rapid = 2
    }

    /// <summary>
    /// ЭТАП 11 ТЗ: ЭКСПОРТ ТРАЕКТОРИИ В ЯЗЫКИ РОБОТОВ (KUKA KRL / FANUC KAREL / ABB RAPID).
    ///
    /// Это НЕ языки интерфейса: пользователь выбирает язык робота в настройках, а платформа
    /// выгружает КОД ДВИЖЕНИЯ по выбранной траектории в файл нужного формата
    /// (`trajectory.src`, `trajectory.kl`, `trajectory.mod`).
    ///
    /// Что попадает в файл:
    ///   • заголовок с датой, именем робота, числом осей и метриками траектории;
    ///   • ДВЕ процедуры — PTP (движение по суставам) и LIN (линейные участки с ориентацией):
    ///     это и есть «шаблоны базовых команд MOVE / LIN / PTP» из ТЗ;
    ///   • точки берутся из ФАКТИЧЕСКОЙ кинематики: угол каждого сустава, положение TCP и
    ///     ориентация фланца (для RAPID — кватернион, для KRL — углы A/B/C, для KAREL — XYZWPR).
    ///
    /// Единицы — как принято у производителя: KRL/KAREL — миллиметры и градусы,
    /// RAPID — миллиметры и кватернион. Призматические оси выводятся в миллиметрах.
    /// </summary>
    public class KvRobotExporter
    {
        public const string LanguagePrefsKey = "KazistovVv.Export.Language";

        public event Action<string> Message;

        /// <summary>Сколько точек максимум выгружается (файл не должен быть нечитаемым).</summary>
        public int maxPoints = 160;

        private TrajectoryFlowController flow;
        private KvRobotLanguage language = KvRobotLanguage.Krl;
        private bool loaded;
        private string lastFile = "";
        private int lastLines;
        private string lastPreview = "";

        public KvRobotLanguage Language
        {
            get { Load(); return language; }
            set
            {
                Load();
                language = value;
                PlayerPrefs.SetInt(LanguagePrefsKey, (int)language);
                PlayerPrefs.Save();
            }
        }

        public string LanguageLabel
        {
            get
            {
                switch (Language)
                {
                    case KvRobotLanguage.Karel:
                        return KvLocExtra2.T("export.lang.karel", "FANUC KAREL");
                    case KvRobotLanguage.Rapid:
                        return KvLocExtra2.T("export.lang.rapid", "ABB RAPID");
                    default:
                        return KvLocExtra2.T("export.lang.krl", "KUKA KRL");
                }
            }
        }

        /// <summary>Расширение файла по ТЗ.</summary>
        public string Extension
        {
            get
            {
                switch (Language)
                {
                    case KvRobotLanguage.Karel: return ".kl";
                    case KvRobotLanguage.Rapid: return ".mod";
                    default: return ".src";
                }
            }
        }

        public string LastFile { get { return lastFile; } }
        public int LastLines { get { return lastLines; } }
        public string LastPreview { get { return lastPreview; } }
        public TrajectoryFlowController Flow() { return flow; }

        /// <summary>Папка экспорта: «Документы\KazistovVv\robot_export» (резерв — данные приложения).</summary>
        public string FolderPath
        {
            get
            {
                try
                {
                    string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    if (!string.IsNullOrEmpty(docs))
                        return FeatureStorage.EnsureDir(Path.Combine(
                            Path.Combine(docs, "KazistovVv"), "robot_export"));
                }
                catch (Exception) { }
                return FeatureStorage.EnsureDir(Path.Combine(FeatureStorage.Root, "Export"));
            }
        }

        private void Load()
        {
            if (loaded) return;
            loaded = true;
            language = (KvRobotLanguage)Mathf.Clamp(PlayerPrefs.GetInt(LanguagePrefsKey, 0), 0, 2);
        }

        public void Bind(TrajectoryFlowController controller)
        {
            flow = controller;
            Load();
        }

        /// <summary>Переключить язык робота (циклически) — команда/кнопка настроек.</summary>
        public KvRobotLanguage CycleLanguage()
        {
            Language = (KvRobotLanguage)(((int)Language + 1) % 3);
            Report("язык робота для экспорта: " + LanguageLabel + " (" + Extension + ")");
            return Language;
        }

        // ================================================================== экспорт

        /// <summary>
        /// Экспортировать выбранную траекторию. Возвращает путь к файлу или "" при отказе
        /// (причина всегда пишется в журнал).
        /// </summary>
        public string Export(TrajectoryCandidate candidate, int index)
        {
            if (candidate == null || candidate.plan == null || candidate.plan.Path == null ||
                candidate.plan.Path.Length < 2)
            {
                Report(KvLocExtra2.T("export.noplan",
                    "Нет траектории: выберите точку (Z + ЛКМ) и подтвердите вариант (X + ЛКМ)"));
                return "";
            }
            if (flow == null || flow.Validator == null || !flow.Validator.Ready)
            {
                Report(KvLocExtra2.T("export.fail", "экспорт не выполнен") +
                       ": робот не определён");
                return "";
            }

            PoseValidator v = flow.Validator;
            List<KvExportPoint> points = Sample(v, candidate.plan);
            if (points.Count < 2)
            {
                Report(KvLocExtra2.T("export.fail", "экспорт не выполнен") +
                       ": точек траектории недостаточно");
                return "";
            }

            string robot = flow.Robot != null ? flow.Robot.robotName : "robot";
            string code;
            switch (Language)
            {
                case KvRobotLanguage.Karel: code = BuildKarel(points, robot, candidate, index); break;
                case KvRobotLanguage.Rapid: code = BuildRapid(points, robot, candidate, index); break;
                default: code = BuildKrl(points, robot, candidate, index); break;
            }

            string path = Path.Combine(FolderPath, "trajectory" + Extension);
            try
            {
                File.WriteAllText(path, code, new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                Report(KvLocExtra2.T("export.fail", "экспорт не выполнен") + ": " + e.Message);
                return "";
            }

            lastFile = path;
            lastLines = code.Split('\n').Length;
            lastPreview = FirstLines(code, 8);
            Report(KvLocExtra2.T("export.done", "файл создан") + ": " + path + " · " +
                   LanguageLabel + " · точек " + points.Count + " · строк " + lastLines);
            return path;
        }

        /// <summary>Экспорт выбранного варианта (кнопка/команда).</summary>
        public string ExportSelected()
        {
            int index;
            TrajectoryCandidate candidate = KvVariantKit.Selected(flow, out index);
            return Export(candidate, index);
        }

        private static string FirstLines(string code, int count)
        {
            string[] lines = code.Split('\n');
            var sb = new StringBuilder();
            for (int i = 0; i < lines.Length && i < count; i++) sb.Append(lines[i]).Append('\n');
            return sb.ToString();
        }

        /// <summary>Одна точка выгрузки: суставы, TCP и ориентация фланца.</summary>
        private class KvExportPoint
        {
            public double[] q;
            public Vector3 tcp;
            public Quaternion flange;
            public float time;
        }

        /// <summary>Прореживание плана до `maxPoints` точек (равномерно по сэмплам).</summary>
        private List<KvExportPoint> Sample(PoseValidator v, PlannedTrajectory plan)
        {
            var list = new List<KvExportPoint>();
            int n = plan.Path.Length;
            int step = Mathf.Max(1, Mathf.CeilToInt(n / (float)Mathf.Max(2, maxPoints)));

            for (int i = 0; i < n; i += step) list.Add(MakePoint(v, plan, i));
            if ((n - 1) % step != 0) list.Add(MakePoint(v, plan, n - 1));
            return list;
        }

        private static KvExportPoint MakePoint(PoseValidator v, PlannedTrajectory plan, int i)
        {
            Vector3 pos;
            Quaternion rot;
            KvCalibrationService.FlangeFrame(v, plan.Path[i], out pos, out rot);
            return new KvExportPoint
            {
                q = plan.Path[i],
                tcp = v.TcpAt(plan.Path[i]),
                flange = rot,
                time = plan.Times != null && i < plan.Times.Length ? plan.Times[i] : 0f
            };
        }

        // ================================================================== KUKA KRL

        private string BuildKrl(List<KvExportPoint> pts, string robot, TrajectoryCandidate candidate,
            int index)
        {
            var sb = new StringBuilder();
            string stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            sb.Append("&ACCESS RVP\n");
            sb.Append("&REL 1\n");
            sb.Append("DEF trajectory ( )\n");
            sb.Append("; ================================================================\n");
            sb.Append("; KazistovVv · экспорт траектории в KUKA KRL\n");
            sb.Append("; робот: ").Append(robot).Append(" · вариант №").Append(index + 1)
              .Append(" · точек: ").Append(pts.Count).Append('\n');
            sb.Append("; создано: ").Append(stamp).Append('\n');
            sb.Append("; траектория: ").Append(candidate.label).Append(" · время ")
              .Append(candidate.timeS.ToString("0.000")).Append(" с · длина ")
              .Append(candidate.lengthM.ToString("0.000")).Append(" м\n");
            sb.Append("; ВНИМАНИЕ: номера инструмента и базы (TOOL_DATA/BASE_DATA) задайте\n");
            sb.Append("; под свою ячейку — здесь используется 1.\n");
            sb.Append("; ================================================================\n");
            sb.Append("  DECL E6AXIS qStart\n");
            sb.Append("  DECL E6POS  pStart\n");
            sb.Append("  INI\n");
            sb.Append("  $TOOL = TOOL_DATA[1]\n");
            sb.Append("  $BASE = BASE_DATA[1]\n\n");

            // --- PTP: движение по суставам
            sb.Append("  ; --- PTP: движение по суставам (MOVE) ---\n");
            sb.Append("  qStart = {").Append(Joints(pts[0].q)).Append("}\n");
            sb.Append("  PTP qStart\n");
            for (int i = 1; i < pts.Count; i++)
            {
                sb.Append("  PTP {").Append(Joints(pts[i].q)).Append("} C_PTP\n");
            }
            sb.Append('\n');

            // --- LIN: линейные участки с ориентацией
            sb.Append("  ; --- LIN: линейное движение с ориентацией (LIN) ---\n");
            sb.Append("  pStart = {").Append(Cartesian(pts[0].tcp, pts[0].flange)).Append("}\n");
            sb.Append("  PTP pStart\n");
            for (int i = 1; i < pts.Count; i++)
            {
                sb.Append("  LIN {").Append(Cartesian(pts[i].tcp, pts[i].flange))
                  .Append("} C_DIS\n");
            }
            sb.Append("  LIN {").Append(Cartesian(pts[pts.Count - 1].tcp, pts[pts.Count - 1].flange))
              .Append("}\n");
            sb.Append("END\n");
            return sb.ToString();
        }

        private static string Joints(double[] q)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < q.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append("A").Append(i + 1).Append(' ')
                  .Append(q[i].ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        /// <summary>KRL: миллиметры + углы A/B/C (градусы) из ориентации фланца.</summary>
        private static string Cartesian(Vector3 tcp, Quaternion flange)
        {
            Vector3 euler = flange.eulerAngles;
            return "X " + Mm(tcp.x) + ", Y " + Mm(tcp.y) + ", Z " + Mm(tcp.z) +
                   ", A " + Deg(euler.x) + ", B " + Deg(euler.y) + ", C " + Deg(euler.z);
        }

        // ================================================================== FANUC KAREL

        private string BuildKarel(List<KvExportPoint> pts, string robot, TrajectoryCandidate candidate,
            int index)
        {
            var sb = new StringBuilder();
            string stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            sb.Append("PROGRAM trajectory\n");
            sb.Append("-- ================================================================\n");
            sb.Append("-- KazistovVv · экспорт траектории в FANUC KAREL\n");
            sb.Append("-- робот: ").Append(robot).Append(" · вариант №").Append(index + 1)
              .Append(" · точек: ").Append(pts.Count).Append('\n');
            sb.Append("-- создано: ").Append(stamp).Append('\n');
            sb.Append("-- траектория: ").Append(candidate.label).Append(" · время ")
              .Append(candidate.timeS.ToString("0.000")).Append(" s · длина ")
              .Append(candidate.lengthM.ToString("0.000")).Append(" m\n");
            sb.Append("-- ВНИМАНИЕ: номера UTOOL/UTFRAME задайте под свою ячейку.\n");
            sb.Append("-- ================================================================\n");
            sb.Append("VAR\n");
            sb.Append("  jStart : JOINT_POS\n");
            sb.Append("  pStart : XYZWPR\n");
            sb.Append("  vSpeed : INTEGER\n");
            sb.Append("BEGIN\n");
            sb.Append("  -- PTP: движение по суставам (MOVE TO JOINT_POS)\n");
            for (int i = 0; i < pts.Count; i++)
            {
                if (i == 0)
                {
                    sb.Append("  jStart = JOINT_POS(").Append(JointsKarel(pts[i].q)).Append(")\n");
                    sb.Append("  MOVE TO jStart\n");
                }
                else
                {
                    sb.Append("  JMP L").Append(i).Append("  -- через точку ").Append(i)
                      .Append(" (суставы)\n");
                    sb.Append("  L").Append(i).Append(" :\n");
                    sb.Append("  MOVE TO JOINT_POS(").Append(JointsKarel(pts[i].q)).Append(")\n");
                }
            }
            sb.Append('\n');
            sb.Append("  -- LIN: линейное движение с ориентацией (MOVE TO XYZWPR)\n");
            sb.Append("  pStart = POSITION(").Append(CartesianKarel(pts[0])).Append(")\n");
            sb.Append("  MOVE TO pStart\n");
            for (int i = 1; i < pts.Count; i++)
            {
                sb.Append("  MOVE TO POSITION(").Append(CartesianKarel(pts[i])).Append(")\n");
            }
            sb.Append("END trajectory\n");
            return sb.ToString();
        }

        /// <summary>KAREL: JOINT_POS принимает градусы; призматические оси — в миллиметрах.</summary>
        private static string JointsKarel(double[] q)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < q.Length && i < 9; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(q[i].ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        /// <summary>KAREL: POSITION(x, y, z, w, p, r) — миллиметры и градусы.</summary>
        private static string CartesianKarel(KvExportPoint p)
        {
            Vector3 euler = p.flange.eulerAngles;
            return Mm(p.tcp.x) + ", " + Mm(p.tcp.y) + ", " + Mm(p.tcp.z) + ", " +
                   Deg(euler.z) + ", " + Deg(euler.y) + ", " + Deg(euler.x);
        }

        // ================================================================== ABB RAPID

        private string BuildRapid(List<KvExportPoint> pts, string robot, TrajectoryCandidate candidate,
            int index)
        {
            var sb = new StringBuilder();
            string stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            sb.Append("MODULE trajectory\n");
            sb.Append("  ! ================================================================\n");
            sb.Append("  ! KazistovVv · экспорт траектории в ABB RAPID\n");
            sb.Append("  ! робот: ").Append(robot).Append(" · вариант №").Append(index + 1)
              .Append(" · точек: ").Append(pts.Count).Append('\n');
            sb.Append("  ! создано: ").Append(stamp).Append('\n');
            sb.Append("  ! траектория: ").Append(candidate.label).Append(" · время ")
              .Append(candidate.timeS.ToString("0.000")).Append(" s · длина ")
              .Append(candidate.lengthM.ToString("0.000")).Append(" m\n");
            sb.Append("  ! ВНИМАНИЕ: tool0/wobj0 задайте под свою ячейку.\n");
            sb.Append("  ! ================================================================\n");
            sb.Append("  CONST jointtarget jStart := [[").Append(JointsRapid(pts[0].q))
              .Append("],[9E9,9E9,9E9,9E9,9E9,9E9]];\n");
            for (int i = 1; i < pts.Count; i++)
            {
                sb.Append("  CONST jointtarget j").Append(i).Append(" := [[")
                  .Append(JointsRapid(pts[i].q)).Append("],[9E9,9E9,9E9,9E9,9E9,9E9]];\n");
            }
            for (int i = 0; i < pts.Count; i++)
            {
                sb.Append("  CONST robtarget p").Append(i).Append(" := [[")
                  .Append(CartesianRapid(pts[i])).Append("],[0,0,0,0],[0,0,0,0],[9E9,9E9,9E9,9E9,9E9,9E9]];\n");
            }
            sb.Append('\n');
            sb.Append("  PROC trajectory_ptp()\n");
            sb.Append("    ! PTP: движение по суставам\n");
            sb.Append("    MoveAbsJ jStart, v500, z50, tool0;\n");
            for (int i = 1; i < pts.Count; i++)
                sb.Append("    MoveAbsJ j").Append(i).Append(", v500, z10, tool0;\n");
            sb.Append("  ENDPROC\n\n");
            sb.Append("  PROC trajectory_lin()\n");
            sb.Append("    ! LIN: линейные участки с ориентацией\n");
            sb.Append("    MoveJ p0, v500, z50, tool0;\n");
            for (int i = 1; i < pts.Count; i++)
                sb.Append("    MoveL p").Append(i).Append(", v200, z5, tool0;\n");
            sb.Append("  ENDPROC\n");
            sb.Append("ENDMODULE\n");
            return sb.ToString();
        }

        private static string JointsRapid(double[] q)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < 6; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(i < q.Length
                    ? q[i].ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)
                    : "0");
            }
            return sb.ToString();
        }

        /// <summary>RAPID: robtarget = [[x,y,z],[q1,q2,q3,q4],[cfx,cfy,cfz,cf4],…], мм и кватернион.</summary>
        private static string CartesianRapid(KvExportPoint p)
        {
            Quaternion q = p.flange;
            // RAPID использует кватернион (q1..q4) в порядке (w, x, y, z).
            return Mm(p.tcp.x) + "," + Mm(p.tcp.y) + "," + Mm(p.tcp.z) + "],[" +
                   q.w.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture) + "," +
                   q.x.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture) + "," +
                   q.y.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture) + "," +
                   q.z.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture);
        }

        // ================================================================== мелочи

        private static string Mm(float meters)
        {
            return (meters * 1000f).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string Deg(float degrees)
        {
            return degrees.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
        }

        private void Report(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            Debug.Log("[RobotExport] " + text);
            if (Message != null) Message(text);
        }
    }

    /// <summary>
    /// ВКЛАДКА «ЭКСПОРТ В ЯЗЫК РОБОТА» (ЭТАП 11 ТЗ): выбор языка, папка экспорта,
    /// кнопка выгрузки и предпросмотр начала файла.
    /// </summary>
    public class KvExportTab : IKvWorkbenchTab
    {
        private readonly KvRobotExporter exporter;
        private KvSegmented languageSegment;

        public KvExportTab(KvRobotExporter service)
        {
            exporter = service;
        }

        public string Key { get { return "export"; } }
        public string Title { get { return KvLocExtra2.T("export.title", "Экспорт траектории"); } }

        private static string T(string key, string fallback)
        {
            return KvLocExtra2.T(key, fallback);
        }

        public void Build(KvTabKit kit)
        {
            if (exporter == null || kit == null) return;

            kit.Section(Title);
            string[] langs =
            {
                T("export.lang.krl", "KUKA KRL"),
                T("export.lang.karel", "FANUC KAREL"),
                T("export.lang.rapid", "ABB RAPID")
            };
            languageSegment = kit.Segmented(T("export.language", "Язык робота"), langs,
                (int)exporter.Language, delegate (int i)
                {
                    exporter.Language = (KvRobotLanguage)Mathf.Clamp(i, 0, 2);
                });

            kit.Info(delegate
            {
                return T("export.language", "Язык робота") + ": " + exporter.LanguageLabel +
                       " · trajectory" + exporter.Extension;
            }, KvTheme.Accent);
            kit.Info(delegate { return T("export.folder", "Папка экспорта") + ": " + exporter.FolderPath; },
                KvTheme.TextDim);
            kit.Info(delegate
            {
                TrajectoryFlowController f = exporter.Flow();
                if (f == null || f.State == null || f.State.candidates.Count == 0)
                    return T("export.noplan",
                        "Нет траектории: выберите точку (Z + ЛКМ) и подтвердите вариант (X + ЛКМ)");
                int index = f.State.selectedTrajectory >= 0 ? f.State.selectedTrajectory : 0;
                TrajectoryCandidate c = index < f.State.candidates.Count
                    ? f.State.candidates[index] : null;
                return c == null ? "—" : T("wb.variant", "Вариант") + " " + KvVariantKit.Label(c, index);
            }, KvTheme.TextMain);

            kit.Buttons(new[] { T("export.button", "Экспорт траектории") },
                new Action[] { delegate { exporter.ExportSelected(); } });

            kit.Divider();
            kit.Info(delegate
            {
                return string.IsNullOrEmpty(exporter.LastFile)
                    ? T("export.info", "Генерируется код движения по выбранной траектории")
                    : T("export.done", "файл создан") + ": " + exporter.LastFile + " · строк " +
                      exporter.LastLines;
            }, KvTheme.Ok);

            foreach (string line in (exporter.LastPreview ?? "").Split('\n'))
            {
                string captured = line;
                if (string.IsNullOrEmpty(captured)) continue;
                kit.Info(delegate { return captured; }, KvTheme.TextDim);
            }

            kit.Divider();
            kit.Note(T("export.info",
                "Генерируется код движения по выбранной траектории: PTP (по суставам) и LIN " +
                "(линейные участки с ориентацией из прямой задачи). Файлы: trajectory.src (KUKA), " +
                "trajectory.kl (FANUC), trajectory.mod (ABB)."), KvTheme.TextDim);
        }

        public void Tick() { }

        public void Refresh()
        {
            if (languageSegment != null && languageSegment.Index != (int)exporter.Language)
                languageSegment.Set((int)exporter.Language);
        }
    }
}
