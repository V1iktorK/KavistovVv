using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace KazistovVvFeatures
{
    /// <summary>Точка в JSON-виде (JsonUtility не пишет Vector3 «как есть» в списках — надёжнее свой тип).</summary>
    [Serializable]
    public class KvVec3
    {
        public float x, y, z;

        public KvVec3() { }

        public KvVec3(Vector3 v)
        {
            x = v.x;
            y = v.y;
            z = v.z;
        }

        public Vector3 Vector { get { return new Vector3(x, y, z); } }
    }

    /// <summary>Состояние одного робота в сессии (ЭТАП 20 ТЗ).</summary>
    [Serializable]
    public class KvSessionRobot
    {
        public string name = "";
        /// <summary>Углы суставов, град (у SCARA ось Z — призматическая).</summary>
        public float[] q;
        public KvVec3 position = new KvVec3();
        public KvVec3 euler = new KvVec3();
        public bool active;
    }

    /// <summary>Состояние переключателей новых функций (этапы 1–20) в сессии.</summary>
    [Serializable]
    public class KvSessionFlags
    {
        public bool heatmapReachability = true;
        public bool heatmapClearance;
        public bool zonesVisible = true;
        public bool jointsPanel;
        public bool logPanel;
        public bool scenariosPanel;
        public bool autoRecordPose = true;
    }

    /// <summary>
    /// СЕССИЯ (ЭТАП 20 ТЗ): позиции роботов, выбранные точки, зоны запрета, ссылки на
    /// записанные траектории и позы, состояние переключателей — в одном JSON-файле.
    ///
    /// РАСШИРЯЕМОСТЬ (требование ТЗ «новые поля добавляются без ломки старых файлов»):
    /// `JsonUtility` заполняет только ПРИСУТСТВУЮЩИЕ поля, отсутствующие остаются значениями
    /// инициализаторов, а `version` + `Normalize()` доводят старый файл до рабочего вида.
    /// Поэтому в новую версию можно добавлять поля, не трогая уже сохранённые сессии.
    /// </summary>
    [Serializable]
    public class KvSession
    {
        public int version = 1;
        public string id = "";
        public string name = "Сессия";
        public string created = "";
        public string notes = "";
        public string sceneName = "";

        public List<KvSessionRobot> robots = new List<KvSessionRobot>();
        public List<KvVec3> points = new List<KvVec3>();
        public KvVec3 currentPoint = new KvVec3();
        public bool hasPoint;

        /// <summary>Файлы записанных траекторий (пути), которые были в работе.</summary>
        public List<string> recordFiles = new List<string>();
        /// <summary>Файлы сохранённых поз.</summary>
        public List<string> poseFiles = new List<string>();
        /// <summary>Зоны запрета целиком (их можно восстановить в сцене).</summary>
        public List<KvZoneData> zones = new List<KvZoneData>();
        public KvSessionFlags flags = new KvSessionFlags();

        [NonSerialized] public string filePath = "";

        public void Normalize()
        {
            if (version <= 0) version = 1;
            if (robots == null) robots = new List<KvSessionRobot>();
            if (points == null) points = new List<KvVec3>();
            if (recordFiles == null) recordFiles = new List<string>();
            if (poseFiles == null) poseFiles = new List<string>();
            if (zones == null) zones = new List<KvZoneData>();
            if (flags == null) flags = new KvSessionFlags();
            if (currentPoint == null) currentPoint = new KvVec3();
            for (int i = 0; i < zones.Count; i++) if (zones[i] != null) zones[i].Normalize();
            if (string.IsNullOrEmpty(id)) id = Guid.NewGuid().ToString("N");
            if (string.IsNullOrEmpty(name)) name = "Сессия";
        }

        public string Summary
        {
            get
            {
                return name + " · " + created + " · роботов " + robots.Count +
                       " · точек " + points.Count + " · зон " + zones.Count;
            }
        }
    }

    /// <summary>Хранилище сессий (файл на сессию).</summary>
    public static class KvSessionStore
    {
        public const string Extension = ".session.json";

        public static List<KvSession> LoadAll()
        {
            List<KvSession> result = new List<KvSession>();
            foreach (string file in FeatureStorage.ListFiles(FeatureStorage.SessionsDir, "*" + Extension))
            {
                KvSession session = FeatureStorage.LoadJson<KvSession>(file);
                if (session == null) continue;
                session.filePath = file;
                session.Normalize();
                result.Add(session);
            }
            result.Sort(delegate (KvSession a, KvSession b)
            {
                return string.CompareOrdinal(b.created, a.created);
            });
            return result;
        }

        public static KvSession Load(string path)
        {
            KvSession session = FeatureStorage.LoadJson<KvSession>(path);
            if (session == null) return null;
            session.filePath = path;
            session.Normalize();
            return session;
        }

        public static string Save(KvSession session)
        {
            if (session == null) return null;
            session.Normalize();
            if (string.IsNullOrEmpty(session.filePath))
            {
                session.filePath = Path.Combine(FeatureStorage.SessionsDir,
                    FeatureStorage.SafeName(session.name, "session") + "_" +
                    FeatureStorage.TimeStamp() + Extension);
            }
            return FeatureStorage.SaveJson(session.filePath, session) ? session.filePath : null;
        }

        public static bool Delete(KvSession session)
        {
            if (session == null) return false;
            return FeatureStorage.DeleteFile(session.filePath);
        }
    }

    /// <summary>
    /// СОХРАНЕНИЕ / ЗАГРУЗКА СЕССИИ (ЭТАП 20 ТЗ).
    ///
    /// Сборка сессии — из СЦЕНЫ (позы роботов, активный робот, точки) и из сервисов новых
    /// функций (зоны, записи, позы, флаги). Загрузка — обратная операция: зоны создаются
    /// заново, позы роботов применяются напрямую через валидатор (с проверкой лимитов),
    /// точка возвращается в поток, флаги применяются хабом.
    /// </summary>
    public class KvSessionManager
    {
        public event Action<string> Message;
        public event Action Changed;

        private readonly List<KvSession> sessions = new List<KvSession>();
        private TrajectoryFlowController flow;
        private KvZoneService zones;
        private KvRecordingService recordings;
        private KvPoseLibrary poses;
        private Func<KvSessionFlags> captureFlags;
        private Action<KvSessionFlags> applyFlags;

        public IReadOnlyList<KvSession> All { get { return sessions; } }
        public int Count { get { return sessions.Count; } }

        public void Bind(TrajectoryFlowController controller, KvZoneService zoneService,
            KvRecordingService recordingService, KvPoseLibrary poseLibrary,
            Func<KvSessionFlags> flagsCapture, Action<KvSessionFlags> flagsApply)
        {
            flow = controller;
            zones = zoneService;
            recordings = recordingService;
            poses = poseLibrary;
            captureFlags = flagsCapture;
            applyFlags = flagsApply;
            Reload();
        }

        public void Reload()
        {
            sessions.Clear();
            sessions.AddRange(KvSessionStore.LoadAll());
            if (Changed != null) Changed();
        }

        /// <summary>Собрать сессию из текущего состояния (без записи на диск).</summary>
        public KvSession Capture(string name)
        {
            KvSession session = new KvSession
            {
                id = Guid.NewGuid().ToString("N"),
                name = string.IsNullOrEmpty(name) ? "Сессия " + FeatureStorage.TimeStamp() : name,
                created = FeatureStorage.IsoNow(),
                sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
            };

            RobotController[] robots = UnityEngine.Object.FindObjectsByType<RobotController>(
                FindObjectsInactive.Include);
            for (int i = 0; i < robots.Length; i++)
            {
                RobotController robot = robots[i];
                if (robot == null || KazistovVvUI.RuntimeRegistry.IsHidden(robot.gameObject)) continue;

                KvSessionRobot entry = new KvSessionRobot
                {
                    name = robot.robotName,
                    position = new KvVec3(robot.transform.position),
                    euler = new KvVec3(robot.transform.eulerAngles),
                    active = robot.isActive
                };

                if (flow != null && flow.Validator != null && flow.Validator.Ready &&
                    flow.Robot == robot)
                {
                    double[] q = flow.Validator.CopyCurrent();
                    entry.q = new float[q.Length];
                    for (int j = 0; j < q.Length; j++) entry.q[j] = (float)q[j];
                }
                else
                {
                    float[] angles = robot.GetJointAngles();
                    entry.q = angles != null ? (float[])angles.Clone() : new float[0];
                }
                session.robots.Add(entry);
            }

            if (flow != null && flow.State.hasPoint)
            {
                session.hasPoint = true;
                session.currentPoint = new KvVec3(flow.State.point);
            }

            if (zones != null)
            {
                for (int i = 0; i < zones.Zones.Count; i++)
                {
                    KvZone zone = zones.Zones[i];
                    if (zone == null || zone.Data == null) continue;
                    zone.Data.filePath = "";                 // в сессию пишем сами данные, не путь
                    session.zones.Add(zone.Data);
                }
            }

            if (recordings != null && recordings.Playing != null)
                session.recordFiles.Add(recordings.Playing.filePath ?? recordings.Playing.name);
            else
            {
                List<KvTrajectoryRecord> all = KvRecordStore.LoadAll();
                for (int i = 0; i < all.Count && i < 32; i++)
                    session.recordFiles.Add(all[i].filePath);
            }

            if (poses != null)
            {
                for (int i = 0; i < poses.All.Count && i < 64; i++)
                    session.poseFiles.Add(poses.All[i].filePath);
            }

            if (captureFlags != null) session.flags = captureFlags();
            session.Normalize();
            return session;
        }

        /// <summary>Сохранить текущее состояние как сессию.</summary>
        public KvSession Save(string name)
        {
            KvSession session = Capture(name);
            string path = KvSessionStore.Save(session);
            if (string.IsNullOrEmpty(path))
            {
                Report("не удалось сохранить сессию «" + session.name + "»");
                return null;
            }
            sessions.Insert(0, session);
            Report("сессия «" + session.name + "» сохранена: " + path);
            if (Changed != null) Changed();
            return session;
        }

        /// <summary>Загрузить сессию (роботы, точка, зоны, флаги).</summary>
        public bool Load(KvSession session)
        {
            if (session == null) return false;
            session.Normalize();

            int applied = 0;
            int skipped = 0;

            // 1) позы роботов
            for (int i = 0; i < session.robots.Count; i++)
            {
                KvSessionRobot entry = session.robots[i];
                RobotController robot = FindRobot(entry.name);
                if (robot == null || entry.q == null || entry.q.Length == 0) { skipped++; continue; }

                if (flow != null && flow.Validator != null && flow.Validator.Ready &&
                    flow.Robot == robot && entry.q.Length == flow.Validator.Dof)
                {
                    double[] q = new double[entry.q.Length];
                    for (int j = 0; j < q.Length; j++) q[j] = entry.q[j];
                    if (flow.Validator.WithinLimits(q))
                    {
                        flow.Validator.Apply(q);
                        applied++;
                    }
                    else
                    {
                        skipped++;
                        Report("поза робота «" + entry.name + "» вне лимитов — пропущена");
                    }
                }
                else
                {
                    skipped++;
                }
            }

            // 2) точка
            if (session.hasPoint && flow != null && flow.Validator != null && flow.Validator.Ready &&
                !flow.Motion.IsRunning)
            {
                Vector3 point = session.currentPoint.Vector;
                if (flow.LockPointFromUi(point, point, Vector3.up, false))
                    Report("точка сессии возвращена в поток: " + point);
            }

            // 3) зоны запрета
            int created = 0;
            if (zones != null)
            {
                for (int i = 0; i < session.zones.Count; i++)
                {
                    KvZoneData data = session.zones[i];
                    if (data == null) continue;
                    data.filePath = "";                        // заново сохранится в папку зон
                    if (zones.Find(data.id) != null) continue;
                    if (zones.Create(data) != null) created++;
                }
            }

            // 4) флаги функций
            if (applyFlags != null && session.flags != null) applyFlags(session.flags);

            Report("сессия «" + session.name + "» загружена · роботов применено " + applied +
                   " (пропущено " + skipped + ") · зон создано " + created +
                   " · заметок " + (string.IsNullOrEmpty(session.notes) ? "нет" : session.notes));
            if (Changed != null) Changed();
            return true;
        }

        public bool Delete(KvSession session)
        {
            if (session == null) return false;
            bool ok = KvSessionStore.Delete(session) && sessions.Remove(session);
            if (ok)
            {
                Report("сессия «" + session.name + "» удалена");
                if (Changed != null) Changed();
            }
            return ok;
        }

        /// <summary>Свежайшая сессия (для «Загрузить последнюю»).</summary>
        public KvSession Newest
        {
            get { return sessions.Count > 0 ? sessions[0] : null; }
        }

        private static RobotController FindRobot(string name)
        {
            RobotController[] robots = UnityEngine.Object.FindObjectsByType<RobotController>(
                FindObjectsInactive.Include);
            for (int i = 0; i < robots.Length; i++)
            {
                if (robots[i] == null) continue;
                if (KazistovVvUI.RuntimeRegistry.IsHidden(robots[i].gameObject)) continue;
                if (robots[i].robotName == name || robots[i].gameObject.name == name) return robots[i];
            }
            return null;
        }

        private void Report(string text)
        {
            Debug.Log("[Session] " + text);
            if (Message != null) Message(text);
        }
    }
}
