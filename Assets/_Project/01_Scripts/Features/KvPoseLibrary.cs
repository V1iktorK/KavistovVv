using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using TrajectoryCore;

namespace KazistovVvFeatures
{
    /// <summary>Сохранённая поза робота (ЭТАП 2 ТЗ).</summary>
    [Serializable]
    public class KvPosePreset
    {
        public int version = 1;
        public string id = "";
        public string name = "Поза";
        /// <summary>Имя робота, для которого сохранена поза (у SCARA и робота свои наборы).</summary>
        public string robot = "";
        public string created = "";
        public string notes = "";
        /// <summary>Углы суставов, град (для SCARA ось Z — призматическая, как у робота).</summary>
        public float[] q = new float[0];
        /// <summary>TCP в момент сохранения (справочно, для свойств и подсказки).</summary>
        public float[] tcp = new float[0];

        [NonSerialized] public string filePath = "";

        public Vector3 TcpVector
        {
            get { return tcp != null && tcp.Length >= 3 ? new Vector3(tcp[0], tcp[1], tcp[2]) : Vector3.zero; }
        }

        public double[] ToDoubles()
        {
            if (q == null) return new double[0];
            double[] result = new double[q.Length];
            for (int i = 0; i < q.Length; i++) result[i] = q[i];
            return result;
        }

        public void Normalize()
        {
            if (version <= 0) version = 1;
            if (q == null) q = new float[0];
            if (tcp == null) tcp = new float[0];
            if (string.IsNullOrEmpty(id)) id = Guid.NewGuid().ToString("N");
            if (string.IsNullOrEmpty(name)) name = "Поза";
        }

        public string Tooltip
        {
            get
            {
                return name +
                       "\nРобот: " + (string.IsNullOrEmpty(robot) ? "—" : robot) +
                       "\nСоздана: " + created +
                       "\nTCP: " + TcpVector.x.ToString("0.000") + ", " + TcpVector.y.ToString("0.000") +
                       ", " + TcpVector.z.ToString("0.000") +
                       (string.IsNullOrEmpty(notes) ? "" : "\n" + notes);
            }
        }
    }

    /// <summary>Хранилище поз (по одному файлу на позу — копируются и правятся руками).</summary>
    public static class KvPoseStore
    {
        public const string Extension = ".json";

        /// <summary>Имена, предложенные ТЗ (быстрый выбор при сохранении).</summary>
        public static readonly string[] SuggestedNames =
        {
            "Домашняя", "Инспекция", "Замена инструмента", "Парковка"
        };

        public static List<KvPosePreset> LoadAll()
        {
            List<KvPosePreset> result = new List<KvPosePreset>();
            foreach (string file in FeatureStorage.ListFiles(FeatureStorage.PosesDir, "*" + Extension))
            {
                KvPosePreset pose = FeatureStorage.LoadJson<KvPosePreset>(file);
                if (pose == null) continue;
                pose.filePath = file;
                pose.Normalize();
                if (pose.q.Length == 0) continue;
                result.Add(pose);
            }
            result.Sort(delegate (KvPosePreset a, KvPosePreset b)
            {
                int byRobot = string.CompareOrdinal(a.robot, b.robot);
                return byRobot != 0 ? byRobot : string.CompareOrdinal(a.name, b.name);
            });
            return result;
        }

        public static string Save(KvPosePreset pose)
        {
            if (pose == null) return null;
            pose.Normalize();
            if (string.IsNullOrEmpty(pose.filePath))
            {
                string file = FeatureStorage.SafeName(pose.name, "pose") + "_" +
                              FeatureStorage.TimeStamp() + Extension;
                pose.filePath = Path.Combine(FeatureStorage.PosesDir, file);
            }
            return FeatureStorage.SaveJson(pose.filePath, pose) ? pose.filePath : null;
        }

        public static bool Delete(KvPosePreset pose)
        {
            if (pose == null) return false;
            return FeatureStorage.DeleteFile(pose.filePath);
        }
    }

    /// <summary>
    /// ПРЕДУСТАНОВЛЕННЫЕ ПОЗЫ (ЭТАП 2 ТЗ).
    ///
    /// «Сохранить текущую позу как…» — снимает углы суставов активного робота и пишет JSON.
    /// «Перейти в позу X» — строит траекторию в пространстве суставов до этой позы
    /// (планировщиком, если он нашёл путь, иначе плавной интерполяцией с проверкой лимитов)
    /// и отдаёт её штатному исполнителю проекта — робот едет плавно, как по обычной траектории.
    ///
    /// Для SCARA работает ровно так же (4 оси), ограничение только на выбор позы: набор поз
    /// привязан к имени робота, чужие позы в дереве не показываются.
    /// </summary>
    public class KvPoseLibrary
    {
        /// <summary>Скорость «плавного» переезда в позу, если планировщик не нашёл путь.</summary>
        public float fallbackSpeedMps = 0.08f;

        public event Action Changed;
        public event Action<string> Failed;

        private readonly List<KvPosePreset> poses = new List<KvPosePreset>();
        private TrajectoryFlowController flow;
        private CollisionWorld world;

        public IReadOnlyList<KvPosePreset> All { get { return poses; } }
        public int Count { get { return poses.Count; } }

        public void Bind(TrajectoryFlowController controller)
        {
            flow = controller;
            Reload();
        }

        public void BindWorld(CollisionWorld collisionWorld)
        {
            world = collisionWorld;
        }

        public void Reload()
        {
            poses.Clear();
            poses.AddRange(KvPoseStore.LoadAll());
            if (Changed != null) Changed();
        }

        /// <summary>
        /// Позы текущего робота потока (для дерева моделей и «Перейти в позу»).
        ///
        /// ФИКС 2. Раньше фильтр был только по ИМЕНИ робота, поэтому в список попадали позы
        /// с ПУСТЫМ именем (сохранённые прежними версиями) — а среди них могли быть снимки
        /// ДРУГОГО робота с другим числом осей. `MoveTo` такую позу честно отклоняет
        /// («снята для робота с N осями»), и в прогоне это выглядело как «переезд не
        /// запустился, поток остался в Idle». Теперь поза показывается, только если она
        /// ПОДХОДИТ текущему роботу по числу осей: имя — главный признак, число осей —
        /// обязательная проверка (у SCARA 3 оси, у робота с кистью 6).
        /// </summary>
        public List<KvPosePreset> ForCurrentRobot()
        {
            List<KvPosePreset> result = new List<KvPosePreset>();
            string robot = flow != null && flow.Validator != null ? flow.Validator.RobotName : "";
            int dof = flow != null && flow.Validator != null && flow.Validator.Ready
                ? flow.Validator.Dof : 0;

            for (int i = 0; i < poses.Count; i++)
            {
                KvPosePreset pose = poses[i];
                if (pose == null || pose.q == null || pose.q.Length == 0) continue;

                // Число осей — обязательное условие: иначе поза физически не применима.
                if (dof > 0 && pose.q.Length != dof) continue;

                // Имя робота: своё — да; пустое (неизвестное) — только если оси совпали.
                if (string.IsNullOrEmpty(pose.robot) || string.IsNullOrEmpty(robot) ||
                    pose.robot == robot)
                    result.Add(pose);
            }
            return result;
        }

        /// <summary>Сохранить текущую позу робота под именем <paramref name="name"/>.</summary>
        public KvPosePreset SaveCurrent(string name, string notes = "")
        {
            if (flow == null || flow.Validator == null || !flow.Validator.Ready)
            {
                Fail("позу сохранить нечем: робот потока не готов");
                return null;
            }
            double[] q = flow.Validator.CopyCurrent();
            if (q == null || q.Length == 0)
            {
                Fail("не удалось прочитать углы суставов");
                return null;
            }

            Vector3 tcpPoint = flow.Validator.TcpAt(q);
            KvPosePreset pose = new KvPosePreset
            {
                id = Guid.NewGuid().ToString("N"),
                name = string.IsNullOrEmpty(name) ? "Поза " + FeatureStorage.TimeStamp() : name,
                robot = flow.Validator.RobotName,
                created = FeatureStorage.IsoNow(),
                notes = notes ?? "",
                q = new float[q.Length],
                tcp = new[] { tcpPoint.x, tcpPoint.y, tcpPoint.z }
            };
            for (int i = 0; i < q.Length; i++) pose.q[i] = (float)q[i];

            string path = KvPoseStore.Save(pose);
            if (string.IsNullOrEmpty(path))
            {
                Fail("не удалось записать позу «" + pose.name + "»");
                return null;
            }

            poses.Add(pose);
            poses.Sort(delegate (KvPosePreset a, KvPosePreset b)
            {
                int byRobot = string.CompareOrdinal(a.robot, b.robot);
                return byRobot != 0 ? byRobot : string.CompareOrdinal(a.name, b.name);
            });

            Debug.Log("[Pose] сохранена поза «" + pose.name + "» · робот " + pose.robot +
                      " · TCP (" + tcpPoint.x.ToString("0.000") + ", " + tcpPoint.y.ToString("0.000") +
                      ", " + tcpPoint.z.ToString("0.000") + ") · " + path);
            if (Changed != null) Changed();
            return pose;
        }

        public bool Delete(KvPosePreset pose)
        {
            if (pose == null) return false;
            bool ok = KvPoseStore.Delete(pose) && poses.Remove(pose);
            if (ok)
            {
                Debug.Log("[Pose] удалена поза «" + pose.name + "»");
                if (Changed != null) Changed();
            }
            return ok;
        }

        /// <summary>
        /// «Перейти в позу X»: построить траекторию до позы и запустить движение.
        /// Возвращает описание результата (для журнала/статуса).
        /// </summary>
        public string MoveTo(KvPosePreset pose)
        {
            if (pose == null) return Refuse("поза не выбрана");
            if (flow == null || flow.Validator == null || !flow.Validator.Ready)
                return Refuse("робот потока не готов");
            if (flow.Motion == null) return Refuse("исполнитель движения недоступен");
            if (flow.Motion.IsRunning) return Refuse("робот уже едет — сначала остановите движение");

            double[] goal = pose.ToDoubles();
            if (goal.Length == 0) return Refuse("в позе нет углов");
            // ФИКС 2: причина отказа называется ПРЯМО и попадает в консоль, а не только в
            // строку, которую вызывающий код может проигнорировать (именно так «переезд не
            // запустился» и выглядел в пакетном прогоне).
            if (goal.Length != flow.Validator.Dof)
                return Refuse("поза «" + pose.name + "» снята для робота с " + goal.Length +
                              " осями (робот «" + pose.robot + "»), активный — «" +
                              flow.Validator.RobotName + "» с " + flow.Validator.Dof + " осями");

            if (!flow.Validator.WithinLimits(goal))
                return Refuse("поза «" + pose.name + "» вне лимитов активного робота «" +
                              flow.Validator.RobotName + "»");

            double[] start = flow.Validator.CopyCurrent();
            start = flow.Validator.ContinueFrom(start, goal);   // кратчайшие довороты, без «полного оборота»

            TrajectoryCore.PlannedTrajectory plan = null;
            string how = "интерполяция по суставам";

            if (world != null)
            {
                TrajectoryCore.Planner planner = new TrajectoryCore.Planner();
                planner.Init(flow.Validator, world);
                try
                {
                    plan = planner.PlanToGoal(flow.Validator.CopyCurrent(), start, 11,
                        "Поза «" + pose.name + "»");
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Pose] планировщик отказал: " + e.Message);
                    plan = null;
                }
                if (plan != null && plan.Path != null && plan.Path.Length >= 2)
                    how = "планировщик (зазор " + (plan.MinClearance * 1000f).ToString("0") + " мм)";
                else
                    plan = null;
            }

            // Путь через планировщик может быть отклонён SafetyGate (запас лимитов/зазор) —
            // тогда едем плавной интерполяцией по суставам: цель-то гарантированно в лимитах.
            if (plan != null && flow.PlayExternalPlan(plan, goal, "Переезд в позу «" + pose.name + "»"))
            {
                Debug.Log("[Pose] переезд в позу «" + pose.name + "» · " + how +
                          " · сэмплов " + plan.Path.Length + " · время " + plan.Time.ToString("0.0") + " с");
                return "переезд в позу «" + pose.name + "» · " + how;
            }

            TrajectoryCore.PlannedTrajectory safe = BuildJointPlan(start, goal, pose.name);
            if (safe == null) return "не удалось построить путь до позы «" + pose.name + "»";
            if (!flow.PlayExternalPlan(safe, goal, "Переезд в позу «" + pose.name + "»"))
                return "исполнитель отклонил путь до позы «" + pose.name + "»";

            Debug.Log("[Pose] переезд в позу «" + pose.name + "» · интерполяция по суставам" +
                      " · сэмплов " + safe.Path.Length + " · время " + safe.Time.ToString("0.0") + " с");
            return "переезд в позу «" + pose.name + "» · интерполяция по суставам";
        }

        /// <summary>
        /// Плавный путь в пространстве суставов (страховка, когда планировщик не нашёл путь):
        /// линейная интерполяция с профилем разгона/торможения, проверка лимитов на каждом сэмпле.
        /// </summary>
        public TrajectoryCore.PlannedTrajectory BuildJointPlan(double[] start, double[] goal,
            string label, int samples = 40)
        {
            return KvPlanKit.MakeJointPlan(flow != null ? flow.Validator : null, world, start, goal,
                label, fallbackSpeedMps, samples);
        }

        private void Fail(string message)
        {
            Debug.LogWarning("[Pose] " + message);
            if (Failed != null) Failed(message);
        }

        /// <summary>
        /// ФИКС 2. Отказ переезда в позу: причина идёт и в консоль, и в подписчика (`Failed`),
        /// и возвращается строкой. Раньше она только возвращалась — при вызове из диагностики
        /// отказ выглядел как «переезд не запустился» без объяснения.
        /// </summary>
        private string Refuse(string reason)
        {
            Debug.LogWarning("[Pose] переезд в позу не запущен: " + reason);
            if (Failed != null) Failed(reason);
            return reason;
        }
    }

    /// <summary>
    /// Общий «конструктор планов» для новых функций (позы, pick-and-place, сценарии):
    ///   • <see cref="MakeJointPlan"/> — плавный путь в пространстве суставов с честными
    ///     зазором и запасом лимитов (его проверяет штатный `SafetyGate`);
    ///   • <see cref="SolvePoseForPoint"/> — поза для точки TCP с выравниванием инструмента
    ///     через публичный `ToolAlign` (для 6-осевого робота; у SCARA ось Z вертикальна сама).
    /// Ничего в ядре не меняется: используются только публичные методы валидатора и ToolAlign.
    /// </summary>
    public static class KvPlanKit
    {
        /// <summary>Плавный план «из текущей позы в целевую» (smoothstep по суставам).</summary>
        public static TrajectoryCore.PlannedTrajectory MakeJointPlan(TrajectoryCore.PoseValidator v,
            TrajectoryCore.CollisionWorld world, double[] start, double[] goal, string label,
            float speedMps = 0.08f, int samples = 40)
        {
            if (v == null || !v.Ready || start == null || goal == null) return null;
            if (start.Length != goal.Length || start.Length == 0) return null;

            samples = Mathf.Clamp(samples, 4, 200);
            TrajectoryCore.PlannedTrajectory plan = new TrajectoryCore.PlannedTrajectory();
            plan.Label = label;
            plan.Path = new double[samples][];
            plan.Times = new float[samples];

            float length = 0f;
            Vector3 prev = Vector3.zero;
            float limitMargin = float.MaxValue;
            float clearance = float.MaxValue;

            for (int i = 0; i < samples; i++)
            {
                float u = i / (float)(samples - 1);
                float k = u * u * (3f - 2f * u);
                double[] q = new double[start.Length];
                for (int j = 0; j < q.Length; j++) q[j] = start[j] + (goal[j] - start[j]) * k;
                plan.Path[i] = q;

                Vector3 tcpPoint = v.TcpAt(q);
                if (i > 0) length += Vector3.Distance(prev, tcpPoint);
                prev = tcpPoint;
                limitMargin = Mathf.Min(limitMargin, v.LimitMargin(q));

                if (world != null)
                {
                    Vector3 hitTcp;
                    Vector3[] nodes;
                    clearance = Mathf.Min(clearance, v.ClearanceAt(q, world, out hitTcp, out nodes));
                }
                plan.Times[i] = u;
            }

            float total = Mathf.Max(0.5f, length / Mathf.Max(0.005f, speedMps));
            for (int i = 0; i < samples; i++) plan.Times[i] *= total;

            plan.Time = total;
            plan.Length = length;
            plan.MinClearance = clearance == float.MaxValue ? 0.05f : clearance;
            plan.LimitMargin = limitMargin == float.MaxValue ? 180f : limitMargin;
            plan.BranchTag = "Интерполяция по суставам";
            return plan;
        }

        /// <summary>
        /// Поза, при которой TCP стоит в точке <paramref name="point"/>, а инструмент —
        /// вдоль <paramref name="toolNormal"/> (для 6-осевого — через `ToolAlign`, у SCARA
        /// ось инструмента вертикальна сама). Возвращает false, если IK не сошлась.
        /// </summary>
        public static bool SolvePoseForPoint(TrajectoryCore.PoseValidator v,
            TrajectoryCore.CollisionWorld world, Vector3 point, Vector3 toolNormal,
            double[] seed, out double[] q, out string note)
        {
            q = null;
            note = "";
            if (v == null || !v.Ready) { note = "валидатор не готов"; return false; }

            double[] start = seed != null ? seed : v.CopyCurrent();
            // CCD — локальный метод: из «неудобной» позы (например, после ручных тестов)
            // он может застрять в далёком локальном минимуме и вернуть ошибку в сотни мм.
            // Поэтому пробуем НЕСКОЛЬКО стартовых приближений, прежде чем объявить точку
            // недостижимой: текущую позу, «нулевую» позу и середину диапазонов лимитов.
            q = null;
            if (!v.SolveIk(point, start, out q, 160) || q == null)
            {
                double[] alt = new double[start.Length];
                if (!v.SolveIk(point, alt, out q, 160) || q == null)
                {
                    for (int i = 0; i < alt.Length; i++)
                        alt[i] = v.IsPrismatic(i)
                            ? v.Lower[i] + (v.Upper[i] - v.Lower[i]) * 0.5
                            : 0.0;
                    if (!v.SolveIk(point, alt, out q, 160) || q == null)
                    {
                        note = "IK не сошлась (ошибка " + (v.LastIkError * 1000f).ToString("0") + " мм)";
                        return false;
                    }
                }
            }

            if (!v.WithinLimits(q))
            {
                note = "поза вне лимитов";
                return false;
            }

            if (v.Dof >= 6 && toolNormal.sqrMagnitude > 1e-6f)
            {
                // Выравнивание инструмента вниз требует доворота запястья почти на 180°, из
                // «обычной» позы оно часто не сходится (измерено 14.09.2026: просили −up,
                // получили +0.90, то есть пальцы остались вверху). Поэтому пробуем ещё и
                // ЗЕРКАЛЬНУЮ конфигурацию запястья — тот же приём, что в `IkSolver.SolveAllSeeded`.
                double[] flipped = (double[])q.Clone();
                if (flipped.Length >= 6)
                {
                    flipped[3] += 180.0;
                    flipped[4] = -flipped[4];
                    flipped[5] += 180.0;
                    double[] qf;
                    if (v.WithinLimits(flipped) && v.SolveIk(point, flipped, out qf, 160) &&
                        qf != null && v.LastIkError <= 0.02f)
                        q = qf;
                }

                TrajectoryCore.ToolAlign.Outcome outcome = TrajectoryCore.ToolAlign.AlignGoal(
                    v, world, q, point, toolNormal.normalized,
                    0.02f, 0.015f, 3f, 0.012f, 35f);
                if (outcome.ok && outcome.q != null && v.WithinLimits(outcome.q))
                {
                    q = outcome.q;
                    note = "инструмент выровнен (остаток " + outcome.angleDeg.ToString("0.0") + "°)";
                }
                else
                {
                    note = "выравнивание не удалось (" + outcome.why + ") — свободная ориентация";
                }
            }
            else
            {
                note = "свободная ориентация" + (v.Dof < 6 ? " (осей " + v.Dof + ")" : "");
            }
            return true;
        }
    }
}
