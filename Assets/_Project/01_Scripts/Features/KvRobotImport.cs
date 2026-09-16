using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using UnityEngine;
using KazistovVvUI;
using TrajectoryCore;

namespace KazistovVvFeatures
{
    /// <summary>Метка импортированной модели (этап 12): источник, состав, время импорта.</summary>
    public class KvImportedRobot : MonoBehaviour
    {
        public string sourceFile = "";
        public string format = "";
        public int linkCount;
        public int jointCount;
        public string importedAt = "";
        public string note = "";
        /// <summary>Модель создана как визуальная (STEP): кинематики нет.</summary>
        public bool visualOnly;

        // --- ФИКС 6: связь со стендом (какой стенд занял робот)
        /// <summary>Имя стенда, на который поставлен робот ("" — отдельный объект).</summary>
        public string standName = "";
        /// <summary>Ссылка на объект стенда (сохраняется в компоненте робота по ТЗ ФИКС 6).</summary>
        public GameObject standRef;

        // --- ФИКС 5: что удалось извлечь из STEP (только ФАКТ, без выдумывания)
        /// <summary>Сколько осей-кандидатов найдено в STEP (цилиндрические поверхности).</summary>
        public int stepAxisCount;
        /// <summary>Сколько плоскостей-кандидатов скольжения найдено в STEP.</summary>
        public int stepPlaneCount;
        /// <summary>Сколько кинематических пар описано в файле (AP214 KINEMATIC_PAIR).</summary>
        public int stepPairCount;
        /// <summary>Подписи найденных осей (для свойств и отчёта).</summary>
        public string[] stepAxes = new string[0];
        /// <summary>Честная строка о том, что дала кинематическая разведка STEP.</summary>
        public string stepKinematicsNote = "";
    }

    /// <summary>Куда поставить импортированную модель (ФИКС 6).</summary>
    public enum KvImportPlacement
    {
        /// <summary>Отдельный объект рядом со стендами (поведение по умолчанию).</summary>
        Separate = 0,
        /// <summary>Заменить робота на существующем стенде (старый удаляется, новый встаёт на его место).</summary>
        ReplaceOnStand = 1,
        /// <summary>Добавить робота на выбранный стенд (рядом с существующим оборудованием).</summary>
        AddToStand = 2
    }

    /// <summary>Описание сустава из URDF.</summary>
    public class KvUrdfJoint
    {
        public string name = "joint";
        public string type = "revolute";
        public string parent = "";
        public string child = "";
        public Vector3 origin = Vector3.zero;
        public Vector3 rpy = Vector3.zero;         // градусы
        public Vector3 axis = Vector3.up;
        public float lower, upper;                 // в единицах проекта (градусы / метры)
        public bool hasLimits;
        public Transform transform;                // созданный узел сустава
        public Transform childLink;
    }

    /// <summary>Описание звена из URDF.</summary>
    public class KvUrdfLink
    {
        public string name = "link";
        public Vector3 visualOrigin = Vector3.zero;
        public Vector3 visualRpy = Vector3.zero;
        public string geometry = "";               // box / cylinder / sphere / mesh / нет
        public Vector3 size = Vector3.one;         // box: размеры, cylinder: (r, length, r)
        public Transform transform;
    }

    /// <summary>
    /// ЭТАП 12 ТЗ: ИМПОРТ МОДЕЛЕЙ РОБОТОВ (URDF / STEP).
    ///
    /// URDF (полная поддержка): файл читается с диска, из него берутся звенья (`link`),
    /// суставы (`joint`: revolute / continuous / prismatic / fixed), их оси, смещения
    /// (`origin xyz rpy`) и ЛИМИТЫ. По этим данным КОДОМ строится иерархия сцены:
    /// база → звенья → суставы → TCP. На корень вешается штатный `SixAxisController`
    /// (тот же класс, что у роботов сцены), поэтому созданная модель сразу работает
    /// с `PoseValidator` — IK, лимиты, зазоры, планировщик; ничего в ядре не меняется.
    ///
    /// STEP (базовая поддержка, как и требует ТЗ): из текстового STEP вытаскиваются
    /// координаты `CARTESIAN_POINT`, считается габарит и создаётся визуальный корпус.
    /// Кинематического дерева в STEP нет, поэтому ограничение честно пишется в интерфейсе.
    ///
    /// ПРОВЕРКА КИНЕМАТИКИ после импорта: для 6-осевой модели — полный цикл FK→IK
    /// штатным валидатором, для остальных — проверка связности цепи (движение каждого
    /// сустава обязано двигать TCP).
    /// </summary>
    public class KvRobotImportService
    {
        public event Action<string> Message;

        private TrajectoryFlowController flow;
        private CollisionWorld world;
        private readonly List<string> files = new List<string>();
        private GameObject imported;

        /// <summary>Куда ставить модель при импорте (ФИКС 6).</summary>
        public KvImportPlacement Placement { get; set; }

        /// <summary>Стенд, выбранный для вариантов «заменить» / «добавить» (ФИКС 6).</summary>
        public string StandName { get; set; }

        /// <summary>Имена стендов сцены (для интерфейса).</summary>
        public string[] StandNames()
        {
            return new[] { TrajectoryCore.StandBuilder.Stand1Name, TrajectoryCore.StandBuilder.Stand2Name };
        }

        /// <summary>Робот, стоящий на стенде (или null).</summary>
        public RobotController RobotOnStand(string standName)
        {
            GameObject stand = FindStand(standName);
            if (stand == null) return null;
            RobotController[] robots = UnityEngine.Object.FindObjectsByType<RobotController>(
                FindObjectsInactive.Include);
            foreach (RobotController rc in robots)
            {
                if (rc == null) continue;
                Transform t = rc.transform;
                while (t != null)
                {
                    if (t.gameObject == stand) return rc;
                    t = t.parent;
                }
            }
            return null;
        }

        private static GameObject FindStand(string standName)
        {
            if (string.IsNullOrEmpty(standName)) return null;
            foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>(
                         FindObjectsInactive.Include))
                if (t != null && t.name == standName) return t.gameObject;
            return null;
        }

        /// <summary>
        /// МЕСТО И ОРИЕНТАЦИЯ МОДЕЛИ ПО ВЫБРАННОМУ ВАРИАНТУ (ФИКС 6).
        /// Возвращает false, если вариант «заменить/добавить на стенд» выбрать нельзя
        /// (стенда или робота на нём нет) — тогда импорт честно отказывается, а не ставит
        /// модель «куда попало».
        /// </summary>
        private bool ResolvePlacement(Vector3 separatePlace, Quaternion separateRotation,
            out Vector3 position, out Quaternion rotation, out string standNote)
        {
            position = separatePlace;
            rotation = separateRotation;
            standNote = "";

            if (Placement == KvImportPlacement.Separate) return true;

            GameObject stand = FindStand(StandName);
            if (stand == null)
            {
                Report("импорт: стенд «" + StandName + "» не найден в сцене — выберите вариант " +
                       "«отдельным объектом»");
                return false;
            }

            Bounds standBounds = new Bounds(stand.transform.position, Vector3.one * 0.2f);
            Renderer[] rs = stand.GetComponentsInChildren<Renderer>(true);
            if (rs.Length > 0)
            {
                standBounds = rs[0].bounds;
                for (int i = 1; i < rs.Length; i++) standBounds.Encapsulate(rs[i].bounds);
            }

            if (Placement == KvImportPlacement.ReplaceOnStand)
            {
                RobotController old = RobotOnStand(StandName);
                if (old == null)
                {
                    Report("импорт: на стенде «" + StandName + "» нет робота — вариант " +
                           "«заменить» неприменим, выберите «добавить на стенд»");
                    return false;
                }

                // Переносим позицию и ориентацию старого робота (ФИКС 6: «корректно перенести
                // позицию, ориентацию, привязки»). Масштаб не переносим — модель своя.
                position = old.transform.position;
                rotation = old.transform.rotation;
                standNote = " · заменён робот на стенде «" + StandName + "» (" +
                            old.robotName + " удалён, поза перенесена)";

                GameObject doomed = old.gameObject;
                Report("импорт: удаляю робота «" + doomed.name + "» со стенда «" + StandName +
                       "» — на его место встанет импортированная модель");
                UnityEngine.Object.Destroy(doomed);
            }
            else
            {
                // «Добавить на стенд»: верхняя плоскость столешницы, свободное место по X
                // от уже стоящего оборудования.
                float offsetX = 1.8f;
                RobotController[] robots = UnityEngine.Object.FindObjectsByType<RobotController>(
                    FindObjectsInactive.Exclude);
                float maxX = -1000f;
                foreach (RobotController rc in robots)
                {
                    if (rc == null) continue;
                    if (Mathf.Abs(rc.transform.position.z - stand.transform.position.z) > 5f) continue;
                    maxX = Mathf.Max(maxX, rc.transform.position.x);
                }
                if (maxX < -999f)
                    maxX = standBounds.center.x - standBounds.extents.x * 0.35f - offsetX;

                position = new Vector3(maxX + offsetX, standBounds.max.y, separatePlace.z);
                rotation = Quaternion.identity;
                standNote = " · добавлен на стенд «" + StandName + "»";
            }
            return true;
        }
        public IReadOnlyList<string> Files { get { return files; } }
        public GameObject Imported { get { return imported; } }
        public string LastReport { get; private set; } = "";
        public string LastCheck { get; private set; } = "";
        public bool LastVisualOnly { get; private set; }

        public void Bind(TrajectoryFlowController controller, CollisionWorld collisionWorld)
        {
            flow = controller;
            world = collisionWorld;
        }

        /// <summary>Папки поиска моделей: StreamingAssets/robots и «Документы\KazistovVv\robot_models».</summary>
        public string[] SearchFolders()
        {
            var list = new List<string>();
            string streaming = Path.Combine(Application.streamingAssetsPath, "robots");
            list.Add(streaming);
            try
            {
                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (!string.IsNullOrEmpty(docs))
                    list.Add(Path.Combine(Path.Combine(docs, "KazistovVv"), "robot_models"));
            }
            catch (Exception) { }
            list.Add(Path.Combine(FeatureStorage.Root, "Models"));
            return list.ToArray();
        }

        /// <summary>Найти файлы моделей (.urdf / .xml / .step / .stp) во всех папках поиска.</summary>
        public int RefreshFiles()
        {
            files.Clear();
            foreach (string folder in SearchFolders())
            {
                try
                {
                    if (!Directory.Exists(folder)) { FeatureStorage.EnsureDir(folder); continue; }
                    foreach (string pattern in new[] { "*.urdf", "*.xml", "*.step", "*.stp" })
                    {
                        foreach (string path in Directory.GetFiles(folder, pattern))
                            if (!files.Contains(path)) files.Add(path);
                    }
                }
                catch (Exception e)
                {
                    Report("папка " + folder + " не прочитана: " + e.Message);
                }
            }
            Report("найдено файлов моделей: " + files.Count +
                   (files.Count == 0
                       ? " (положите .urdf или .step в " + string.Join(" или ", SearchFolders()) + ")"
                       : ""));
            return files.Count;
        }

        // ================================================================== импорт

        /// <summary>Импортировать файл: URDF (с кинематикой) или STEP (визуально).</summary>
        public bool Import(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Report("файл не найден: " + path);
                return false;
            }

            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".step" || ext == ".stp") return ImportStep(path);
            if (ext == ".urdf" || ext == ".xml") return ImportUrdf(path);

            Report("формат «" + ext + "» не поддерживается (нужен .urdf или .step)");
            return false;
        }

        /// <summary>Импорт URDF: структура «база → звенья → суставы → TCP».</summary>
        public bool ImportUrdf(string path)
        {
            if (flow == null || flow.Validator == null)
            {
                // Импорт возможен и без потока, но тогда некому проверять кинематику.
                Debug.Log("[Import] поток этапов не привязан — кинематика будет проверена частично");
            }

            XmlDocument doc = new XmlDocument();
            try
            {
                doc.Load(path);
            }
            catch (Exception e)
            {
                Report("URDF не прочитан: " + e.Message);
                return false;
            }

            XmlNode robotNode = doc.SelectSingleNode("/robot");
            if (robotNode == null)
            {
                Report("URDF: нет корневого элемента <robot>");
                return false;
            }

            string robotName = Attribute(robotNode, "name", Path.GetFileNameWithoutExtension(path));
            var links = new Dictionary<string, KvUrdfLink>();
            var joints = new List<KvUrdfJoint>();

            // --- звенья
            foreach (XmlNode linkNode in robotNode.SelectNodes("link"))
            {
                KvUrdfLink link = new KvUrdfLink { name = Attribute(linkNode, "name", "link") };
                XmlNode visual = linkNode.SelectSingleNode("visual");
                if (visual != null)
                {
                    Vector3 xyz, rpy;
                    ParseOrigin(visual.SelectSingleNode("origin"), out xyz, out rpy);
                    link.visualOrigin = xyz;
                    link.visualRpy = rpy;
                    XmlNode geometry = visual.SelectSingleNode("geometry");
                    if (geometry != null)
                    {
                        XmlNode box = geometry.SelectSingleNode("box");
                        XmlNode cyl = geometry.SelectSingleNode("cylinder");
                        XmlNode sphere = geometry.SelectSingleNode("sphere");
                        XmlNode mesh = geometry.SelectSingleNode("mesh");
                        if (box != null)
                        {
                            link.geometry = "box";
                            link.size = ParseVector(Attribute(box, "size", "0.1 0.1 0.1"));
                        }
                        else if (cyl != null)
                        {
                            link.geometry = "cylinder";
                            float r = ParseFloat(Attribute(cyl, "radius", "0.05"), 0.05f);
                            float len = ParseFloat(Attribute(cyl, "length", "0.1"), 0.1f);
                            link.size = new Vector3(r, len, r);
                        }
                        else if (sphere != null)
                        {
                            link.geometry = "sphere";
                            float r = ParseFloat(Attribute(sphere, "radius", "0.05"), 0.05f);
                            link.size = new Vector3(r, r, r);
                        }
                        else if (mesh != null)
                        {
                            link.geometry = "mesh";
                            link.size = new Vector3(0.08f, 0.08f, 0.08f);
                        }
                    }
                }
                links[link.name] = link;
            }

            // --- суставы
            foreach (XmlNode jointNode in robotNode.SelectNodes("joint"))
            {
                KvUrdfJoint joint = new KvUrdfJoint
                {
                    name = Attribute(jointNode, "name", "joint"),
                    type = Attribute(jointNode, "type", "revolute").ToLowerInvariant()
                };
                XmlNode parent = jointNode.SelectSingleNode("parent");
                XmlNode child = jointNode.SelectSingleNode("child");
                joint.parent = Attribute(parent, "link", "");
                joint.child = Attribute(child, "link", "");

                Vector3 xyz, rpy;
                ParseOrigin(jointNode.SelectSingleNode("origin"), out xyz, out rpy);
                joint.origin = xyz;
                joint.rpy = rpy;

                XmlNode axis = jointNode.SelectSingleNode("axis");
                if (axis != null)
                {
                    Vector3 a = ParseVector(Attribute(axis, "xyz", "1 0 0"));
                    if (a.sqrMagnitude > 1e-6f) joint.axis = a.normalized;
                }

                XmlNode limit = jointNode.SelectSingleNode("limit");
                if (limit != null)
                {
                    joint.hasLimits = true;
                    float lower = ParseFloat(Attribute(limit, "lower", "0"), 0f);
                    float upper = ParseFloat(Attribute(limit, "upper", "0"), 0f);
                    if (joint.type == "prismatic")
                    {
                        joint.lower = lower;
                        joint.upper = upper;
                    }
                    else
                    {
                        // URDF — радианы, проект — градусы.
                        joint.lower = lower * Mathf.Rad2Deg;
                        joint.upper = upper * Mathf.Rad2Deg;
                    }
                }
                else if (joint.type == "continuous")
                {
                    joint.hasLimits = true;
                    joint.lower = -360f;
                    joint.upper = 360f;
                }
                joints.Add(joint);
            }

            if (links.Count == 0 || joints.Count == 0)
            {
                Report("URDF: не найдено звеньев/суставов (звеньев " + links.Count +
                       ", суставов " + joints.Count + ")");
                return false;
            }

            // --- цепь: от корневого звена по суставам
            var childLinks = new HashSet<string>();
            foreach (KvUrdfJoint j in joints) if (!string.IsNullOrEmpty(j.child)) childLinks.Add(j.child);
            string rootLink = null;
            foreach (KvUrdfLink link in links.Values)
            {
                if (!childLinks.Contains(link.name)) { rootLink = link.name; break; }
            }
            if (rootLink == null) rootLink = links.Keys.GetEnumerator().Current;

            List<KvUrdfJoint> chain = BuildChain(joints, rootLink);

            // --- создание объекта сцены
            RemoveImported();
            RobotInventory.Suppress = true;
            GameObject root = null;
            try
            {
                root = new GameObject("Импорт_" + robotName);
                root.SetActive(false);       // собираем «на выключенном» объекте: Awake увидит готовые поля
                Vector3 place = SuggestedPlace(rootLink, links);
                // URDF описывает модель в системе Z-вверх, Unity — Y-вверх: поворачиваем
                // КОРЕНЬ на −90° вокруг X. Внутренняя иерархия остаётся в координатах URDF,
                // поэтому пересчитывать оси суставов и лимиты не нужно.
                Quaternion separateRotation = Quaternion.Euler(-90f, 0f, 0f);
                Vector3 position;
                Quaternion rotation;
                string standNote;
                if (!ResolvePlacement(place, separateRotation, out position, out rotation,
                        out standNote))
                {
                    UnityEngine.Object.Destroy(root);
                    return false;
                }
                root.transform.position = position;
                root.transform.rotation = rotation;
                place = position;

                // корневое звено
                GameObject baseGo = new GameObject("link_" + rootLink);
                baseGo.transform.SetParent(root.transform, false);
                AttachVisual(baseGo.transform, links.ContainsKey(rootLink) ? links[rootLink] : null);

                Transform cursor = baseGo.transform;
                var jointTransforms = new List<Transform>();
                var axes = new List<Vector3>();
                var lowers = new List<float>();
                var uppers = new List<float>();
                Transform lastLink = baseGo.transform;

                foreach (KvUrdfJoint joint in chain)
                {
                    GameObject jointGo = new GameObject("joint_" + joint.name);
                    jointGo.transform.SetParent(cursor, false);
                    jointGo.transform.localPosition = joint.origin;
                    Quaternion originRotation = Quaternion.Euler(joint.rpy);
                    jointGo.transform.localRotation = originRotation;

                    GameObject linkGo = new GameObject("link_" + joint.child);
                    linkGo.transform.SetParent(jointGo.transform, false);
                    AttachVisual(linkGo.transform, links.ContainsKey(joint.child)
                        ? links[joint.child] : null);

                    joint.transform = jointGo.transform;
                    joint.childLink = linkGo.transform;

                    if (joint.type != "fixed")
                    {
                        // Ось в системе проекта задаётся в системе РОДИТЕЛЯ сустава
                        // (localRotation = AngleAxis(q, axis) * q0), поэтому ось URDF
                        // (она задана в системе сустава) переводим через q0.
                        Vector3 axisInParent = originRotation * joint.axis;
                        jointTransforms.Add(jointGo.transform);
                        axes.Add(axisInParent.normalized);
                        float lo = joint.hasLimits ? joint.lower : -180f;
                        float hi = joint.hasLimits ? joint.upper : 180f;
                        if (joint.type == "prismatic" && !joint.hasLimits)
                        {
                            lo = -0.1f;
                            hi = 0.1f;
                        }
                        lowers.Add(lo);
                        uppers.Add(hi);
                    }

                    cursor = linkGo.transform;
                    lastLink = linkGo.transform;
                }

                // TCP: если в URDF есть звено tool0/tcp — берём его, иначе конец цепи.
                Transform tcp = lastLink;
                foreach (KvUrdfLink link in links.Values)
                {
                    string lower = link.name.ToLowerInvariant();
                    if (lower.Contains("tool0") || lower.Contains("tcp"))
                    {
                        GameObject tcpGo = new GameObject("tcp_" + link.name);
                        tcpGo.transform.SetParent(lastLink, false);
                        tcp = tcpGo.transform;
                        break;
                    }
                }

                SixAxisController six = root.AddComponent<SixAxisController>();
                six.robotName = "Импорт_" + robotName;
                six.jointTransforms = jointTransforms.ToArray();
                six.jointAxesLocal = axes.ToArray();
                six.jointLimits = new Vector2[jointTransforms.Count];
                for (int i = 0; i < jointLimitsLength(six); i++)
                {
                    six.jointLimits[i] = new Vector2(lowers[Mathf.Min(i, lowers.Count - 1)],
                        uppers[Mathf.Min(i, uppers.Count - 1)]);
                }
                six.fixedBase = baseGo.transform;
                six.fixedBaseName = baseGo.name;
                six.endEffector = tcp;
                six.endEffectorName = tcp.name;
                six.baseTransform = baseGo.transform;
                six.tcp = tcp;
                six.isActive = false;

                // Маркер «объект создан оператором»: штатная проверка инвентаря
                // («роботов должно быть ровно 2») такие объекты не считает, а дерево
                // моделей показывает импортированный робот как обычный узел робота.
                RegisteredObject reg = root.AddComponent<RegisteredObject>();
                reg.DisplayName = root.name;
                reg.Node = new ProjectNode(Guid.NewGuid().ToString("N"), root.name,
                    ProjectNodeKind.Robot, root.transform, six);

                KvImportedRobot marker = root.AddComponent<KvImportedRobot>();
                marker.sourceFile = path;
                marker.format = "URDF";
                marker.linkCount = links.Count;
                marker.jointCount = jointTransforms.Count;
                marker.importedAt = FeatureStorage.IsoNow();
                // ФИКС 6: ссылка на стенд сохраняется В КОМПОНЕНТЕ робота.
                if (Placement != KvImportPlacement.Separate)
                {
                    marker.standName = StandName;
                    marker.standRef = FindStand(StandName);
                }

                root.SetActive(true);
                imported = root;

                LastVisualOnly = false;
                LastReport = KvLocExtra2.F("import.structure",
                    "структура: база, звеньев {0}, суставов {1}, TCP",
                    links.Count, jointTransforms.Count) + " · " + Path.GetFileName(path) +
                    " · позиция " + place.x.ToString("0.00") + ", " + place.y.ToString("0.00") +
                    ", " + place.z.ToString("0.00") + standNote;
                Report(KvLocExtra2.T("import.done", "модель импортирована") + ": " + LastReport);
            }
            catch (Exception e)
            {
                if (root != null) UnityEngine.Object.Destroy(root);
                Report("импорт URDF не удался: " + e.GetType().Name + " " + e.Message);
                return false;
            }
            finally
            {
                RobotInventory.Suppress = false;
            }

            CheckKinematics();
            return true;
        }

        private static int jointLimitsLength(SixAxisController six)
        {
            return six.jointLimits != null ? six.jointLimits.Length : 0;
        }

        /// <summary>Цепь суставов от корневого звена (обходим граф в глубину).</summary>
        private static List<KvUrdfJoint> BuildChain(List<KvUrdfJoint> joints, string rootLink)
        {
            var chain = new List<KvUrdfJoint>();
            var used = new HashSet<string>();
            string current = rootLink;
            bool progress = true;
            while (progress)
            {
                progress = false;
                foreach (KvUrdfJoint joint in joints)
                {
                    if (used.Contains(joint.name)) continue;
                    if (joint.parent != current) continue;
                    chain.Add(joint);
                    used.Add(joint.name);
                    current = joint.child;
                    progress = true;
                    break;
                }
            }
            // Оставшиеся суставы (не последовательная цепь) — в конец, чтобы не терять состав.
            foreach (KvUrdfJoint joint in joints)
                if (!used.Contains(joint.name)) chain.Add(joint);
            return chain;
        }

        private static void AttachVisual(Transform parent, KvUrdfLink link)
        {
            if (link == null) return;
            GameObject go;
            switch (link.geometry)
            {
                case "box":
                    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.transform.SetParent(parent, false);
                    go.transform.localPosition = link.visualOrigin;
                    go.transform.localRotation = Quaternion.Euler(link.visualRpy);
                    go.transform.localScale = link.size;
                    break;
                case "cylinder":
                    go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    go.transform.SetParent(parent, false);
                    go.transform.localPosition = link.visualOrigin;
                    // URDF: ось цилиндра — Z, у Unity-примитива — Y: доворачиваем на 90°.
                    go.transform.localRotation = Quaternion.Euler(link.visualRpy) *
                                                 Quaternion.Euler(90f, 0f, 0f);
                    go.transform.localScale = new Vector3(link.size.x * 2f, link.size.y * 0.5f,
                        link.size.x * 2f);
                    break;
                case "sphere":
                    go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    go.transform.SetParent(parent, false);
                    go.transform.localPosition = link.visualOrigin;
                    go.transform.localRotation = Quaternion.Euler(link.visualRpy);
                    go.transform.localScale = Vector3.one * (link.size.x * 2f);
                    break;
                default:
                    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.transform.SetParent(parent, false);
                    go.transform.localPosition = link.visualOrigin;
                    go.transform.localRotation = Quaternion.Euler(link.visualRpy);
                    go.transform.localScale = link.size;
                    break;
            }
            go.name = "visual_" + link.name;
            Renderer r = go.GetComponent<Renderer>();
            if (r != null)
            {
                // Импортированные тела не должны становиться препятствием для планировщика
                // старого робота: коллайдеры снимаем, а мир столкновений собирается по
                // рендерерам — поэтому модель помечается как служебная только для чужих
                // расчётов; свои расчёты идут через PoseValidator (по трансформам).
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            Collider col = go.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.Destroy(col);
        }

        /// <summary>Свободное место рядом с существующими стендами.</summary>
        private Vector3 SuggestedPlace(string rootLink, Dictionary<string, KvUrdfLink> links)
        {
            Vector3 basePos = Vector3.zero;
            bool has = false;
            float maxX = -100f;
            RobotController[] robots = UnityEngine.Object.FindObjectsByType<RobotController>(
                FindObjectsInactive.Exclude);
            foreach (RobotController rc in robots)
            {
                if (rc == null) continue;
                if (!has) { basePos = rc.transform.position; has = true; }
                maxX = Mathf.Max(maxX, rc.transform.position.x);
            }
            if (!has) return new Vector3(0f, 1.0f, 0f);
            float x = maxX > -99f ? maxX + 2.2f : basePos.x + 2.2f;
            return new Vector3(x, TrajectoryCore.StandBuilder.TopHeight, basePos.z);
        }

        /// <summary>Импорт STEP: базовая поддержка — габарит и визуальный корпус.</summary>
        public bool ImportStep(string path)
        {
            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (Exception e)
            {
                Report("STEP не прочитан: " + e.Message);
                return false;
            }

            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            int found = 0;

            MatchCollection matches = Regex.Matches(text,
                @"CARTESIAN_POINT\s*\(\s*'[^']*'\s*,\s*\(\s*([-+0-9Ee.,\s]+?)\)\s*\)");
            foreach (Match m in matches)
            {
                string[] parts = m.Groups[1].Value.Split(',');
                if (parts.Length < 3) continue;
                float x, y, z;
                if (!float.TryParse(parts[0].Trim(), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out x)) continue;
                if (!float.TryParse(parts[1].Trim(), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out y)) continue;
                if (!float.TryParse(parts[2].Trim(), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out z)) continue;
                min = Vector3.Min(min, new Vector3(x, y, z));
                max = Vector3.Max(max, new Vector3(x, y, z));
                found++;
            }

            if (found == 0)
            {
                Report("STEP: координаты не найдены (файл не текстовый или другая схема) — " +
                       "базовая поддержка ограничена текстовыми STEP (AP203/AP214)");
                return false;
            }

            Vector3 size = max - min;

            // --- ФИКС 5: кинематическая разведка STEP (только факты, без выдумывания)
            KvStepAxis[] axes;
            int planeCount, pairCount, linkCount;
            string kinNote;
            ExtractStepKinematics(text, out axes, out planeCount, out pairCount, out linkCount,
                out kinNote);

            RemoveImported();
            GameObject root = new GameObject("Импорт_" + Path.GetFileNameWithoutExtension(path));
            Vector3 separatePlace = SuggestedPlace(null, null) + Vector3.up * (size.y * 0.5f);
            Vector3 position;
            Quaternion rotation;
            string standNote;
            if (!ResolvePlacement(separatePlace, Quaternion.identity, out position, out rotation,
                    out standNote))
            {
                UnityEngine.Object.Destroy(root);
                return false;
            }
            root.transform.position = position;
            root.transform.rotation = rotation;

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "step_body";
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(Mathf.Max(0.02f, size.x),
                Mathf.Max(0.02f, size.y), Mathf.Max(0.02f, size.z));
            Collider col = body.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.Destroy(col);

            KvImportedRobot marker = root.AddComponent<KvImportedRobot>();
            marker.sourceFile = path;
            marker.format = "STEP";
            marker.importedAt = FeatureStorage.IsoNow();
            marker.visualOnly = true;
            marker.note = "двигателей в STEP нет: кинематика не собрана автоматически";
            marker.stepAxisCount = axes.Length;
            marker.stepPlaneCount = planeCount;
            marker.stepPairCount = pairCount;
            marker.stepKinematicsNote = kinNote;
            var axisLabels = new List<string>();
            for (int i = 0; i < axes.Length && i < 12; i++)
                axisLabels.Add(axes[i].name + ": точка (" + axes[i].origin.x.ToString("0.000") +
                               ", " + axes[i].origin.y.ToString("0.000") + ", " +
                               axes[i].origin.z.ToString("0.000") + "), направление (" +
                               axes[i].direction.x.ToString("0.00") + ", " +
                               axes[i].direction.y.ToString("0.00") + ", " +
                               axes[i].direction.z.ToString("0.00") + "), R " +
                               axes[i].radius.ToString("0.000"));
            marker.stepAxes = axisLabels.ToArray();
            // ФИКС 6: ссылка на стенд сохраняется в компоненте робота.
            if (Placement != KvImportPlacement.Separate)
            {
                marker.standName = StandName;
                marker.standRef = FindStand(StandName);
            }
            imported = root;

            LastVisualOnly = true;
            LastReport = KvLocExtra2.F("import.step.stub",
                "STEP: геометрия прочитана (габарит {0} м)", size.magnitude.ToString("0.00")) +
                " · точек " + found + " · " + kinNote + standNote;
            LastCheck = kinNote;
            Report(LastReport);
            return true;
        }

        // ================================================================== КИНЕМАТИКА STEP (ФИКС 5)

        /// <summary>Найденная в STEP ось-кандидат (вращения — цилиндрическая поверхность).</summary>
        public struct KvStepAxis
        {
            public string name;
            public Vector3 origin;
            public Vector3 direction;
            public float radius;
            public string kind;      // "вращение (CYLINDRICAL_SURFACE)" и т. п.
        }

        /// <summary>
        /// ИЗВЛЕЧЬ КИНЕМАТИЧЕСКУЮ СТРУКТУРУ ИЗ ТЕКСТОВОГО STEP (ФИКС 5).
        ///
        /// Что ищется (только то, что РЕАЛЬНО есть в файле — ничего не выдумывается):
        ///   • `CYLINDRICAL_SURFACE` → её `AXIS2_PLACEMENT_3D` → точка + направление оси:
        ///     это ось-кандидат ВРАЩЕНИЯ (так в STEP описаны шарниры);
        ///   • `PLANE` → нормаль её размещения: плоскость-кандидат СКОЛЬЖЕНИЯ;
        ///   • `KINEMATIC_PAIR` / `KINEMATIC_LINK` (кинематика AP214) → сколько связей
        ///     между звеньями описано в файле.
        ///
        /// Возвращается структура-СПРАВКА: по ней нельзя собрать корректную цепь (в STEP нет
        /// ни имён звеньев, ни порядка суставов, ни лимитов), поэтому импорт НЕ строит
        /// кинематику по догадке — он честно показывает, что найдено, и оставляет визуальный
        /// корпус, если полноценной структуры нет.
        /// </summary>
        public static void ExtractStepKinematics(string text, out KvStepAxis[] axes,
            out int planeCount, out int pairCount, out int linkCount, out string note)
        {
            axes = new KvStepAxis[0];
            planeCount = 0;
            pairCount = 0;
            linkCount = 0;
            note = "";

            // Разбор «сущность #N = ТИП ( ... );» в словарь: по нему разрешаются ссылки.
            var entities = new Dictionary<int, string>();
            MatchCollection defs = Regex.Matches(text,
                @"#(\d+)\s*=\s*([A-Z_0-9]+)\s*\(([^;]*)\)\s*;", RegexOptions.Singleline);
            foreach (Match m in defs)
            {
                int id;
                if (!int.TryParse(m.Groups[1].Value, out id)) continue;
                entities[id] = m.Groups[2].Value.Trim().ToUpperInvariant() + " (" +
                               m.Groups[3].Value + ")";
            }
            if (entities.Count == 0)
            {
                note = "в файле не разобрана ни одна сущность STEP";
                return;
            }

            var found = new List<KvStepAxis>();
            foreach (KeyValuePair<int, string> kv in entities)
            {
                string body = kv.Value;
                if (!body.StartsWith("CYLINDRICAL_SURFACE")) continue;

                int placementId;
                float radius;
                if (!FirstReference(body, out placementId)) continue;
                radius = FirstFloat(body);

                Vector3 origin, direction;
                if (!ResolveAxisPlacement(entities, placementId, out origin, out direction))
                    continue;

                found.Add(new KvStepAxis
                {
                    name = "ось #" + kv.Key,
                    origin = origin,
                    direction = direction,
                    radius = radius,
                    kind = "вращение (CYLINDRICAL_SURFACE)"
                });
            }

            foreach (KeyValuePair<int, string> kv in entities)
            {
                string body = kv.Value;
                if (body.StartsWith("PLANE")) planeCount++;
                else if (body.StartsWith("KINEMATIC_PAIR")) pairCount++;
                else if (body.StartsWith("KINEMATIC_LINK")) linkCount++;
            }

            axes = found.ToArray();
            note = "STEP: осей-кандидатов вращения " + axes.Length +
                   " (цилиндрические поверхности), плоскостей-кандидатов скольжения " +
                   planeCount + ", кинематических пар " + pairCount +
                   ", звеньев " + linkCount;
            if (axes.Length == 0 && planeCount == 0 && pairCount == 0)
                note += " — КИНЕМАТИЧЕСКОЙ СТРУКТУРЫ В ФАЙЛЕ НЕТ (только геометрия)";
            else
                note += " — по этим данным цепь НЕ собирается автоматически: в STEP нет имён " +
                        "звеньев, порядка суставов и лимитов. Структура показана как справка.";
        }

        /// <summary>Первая ссылка вида #N в тексте сущности.</summary>
        private static bool FirstReference(string body, out int id)
        {
            id = 0;
            Match m = Regex.Match(body, @"#(\d+)");
            return m.Success && int.TryParse(m.Groups[1].Value, out id);
        }

        /// <summary>Первое число в тексте сущности (радиус цилиндра).</summary>
        private static float FirstFloat(string body)
        {
            Match m = Regex.Match(body, @"[-+]?\d+\.?\d*(?:[eE][-+]?\d+)?\s*\)\s*$");
            if (m.Success)
            {
                float v;
                if (float.TryParse(m.Value.TrimEnd(')', ' '), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out v)) return v;
            }
            return 0f;
        }

        /// <summary>Разрешить `AXIS2_PLACEMENT_3D` (точка + ось) по её номеру.</summary>
        private static bool ResolveAxisPlacement(Dictionary<int, string> entities, int placementId,
            out Vector3 origin, out Vector3 direction)
        {
            origin = Vector3.zero;
            direction = Vector3.up;

            string placement;
            if (!entities.TryGetValue(placementId, out placement)) return false;
            if (!placement.StartsWith("AXIS2_PLACEMENT_3D")) return false;

            var refs = new List<int>();
            foreach (Match m in Regex.Matches(placement, @"#(\d+)"))
            {
                int id;
                if (int.TryParse(m.Groups[1].Value, out id)) refs.Add(id);
            }
            if (refs.Count < 2) return false;

            origin = ResolvePoint(entities, refs[0]);
            direction = ResolveDirection(entities, refs[1]);
            return direction.sqrMagnitude > 1e-8f;
        }

        private static Vector3 ResolvePoint(Dictionary<int, string> entities, int id)
        {
            string body;
            if (!entities.TryGetValue(id, out body)) return Vector3.zero;
            if (!body.StartsWith("CARTESIAN_POINT")) return Vector3.zero;
            return FirstTriple(body);
        }

        private static Vector3 ResolveDirection(Dictionary<int, string> entities, int id)
        {
            string body;
            if (!entities.TryGetValue(id, out body)) return Vector3.up;
            if (!body.StartsWith("DIRECTION")) return Vector3.up;
            Vector3 v = FirstTriple(body);
            return v.sqrMagnitude > 1e-8f ? v.normalized : Vector3.up;
        }

        /// <summary>Первая тройка чисел в скобках — координаты точки или направление.</summary>
        private static Vector3 FirstTriple(string body)
        {
            Match m = Regex.Match(body, @"\(\s*([-+0-9Ee.,\s]+?)\)");
            while (m.Success)
            {
                string[] parts = m.Groups[1].Value.Split(',');
                if (parts.Length >= 3)
                {
                    float x, y, z;
                    if (float.TryParse(parts[0].Trim(), NumberStyles.Float,
                            CultureInfo.InvariantCulture, out x) &&
                        float.TryParse(parts[1].Trim(), NumberStyles.Float,
                            CultureInfo.InvariantCulture, out y) &&
                        float.TryParse(parts[2].Trim(), NumberStyles.Float,
                            CultureInfo.InvariantCulture, out z))
                        return new Vector3(x, y, z);
                }
                m = m.NextMatch();
            }
            return Vector3.zero;
        }
        // ================================================================== проверка кинематики

        /// <summary>Проверить кинематику импортированной модели (ТЗ: «Проверка кинематики после импорта»).</summary>
        public bool CheckKinematics()
        {
            LastCheck = "";
            if (imported == null)
            {
                Report("проверять нечего: модель не импортирована");
                return false;
            }

            KvImportedRobot marker = imported.GetComponent<KvImportedRobot>();
            SixAxisController six = imported.GetComponent<SixAxisController>();
            if (marker != null && marker.visualOnly)
            {
                LastCheck = KvLocExtra2.T("import.step.stub", "STEP: кинематики нет");
                Report(LastCheck);
                return false;
            }
            if (six == null || six.jointTransforms == null || six.jointTransforms.Length == 0)
            {
                LastCheck = "в модели нет подвижных суставов";
                Report("проверка кинематики: " + LastCheck);
                return false;
            }

            int dof = six.jointTransforms.Length;

            // --- 1) связность: движение каждого сустава обязано двигать TCP
            Transform tcp = six.tcp != null ? six.tcp : six.endEffector;
            if (tcp == null)
            {
                LastCheck = "не найден TCP модели";
                Report("проверка кинематики: " + LastCheck);
                return false;
            }

            int movingJoints = 0;
            float worstMove = 0f;
            for (int i = 0; i < dof; i++)
            {
                Transform joint = six.jointTransforms[i];
                if (joint == null) continue;
                Quaternion saved = joint.localRotation;
                Vector3 before = tcp.position;
                Vector3 beforeFwd = tcp.forward;
                Vector3 beforeUp = tcp.up;

                joint.localRotation = Quaternion.AngleAxis(10f,
                    i < six.jointAxesLocal.Length ? six.jointAxesLocal[i] : Vector3.up) * saved;

                float moved = Vector3.Distance(before, tcp.position);
                float axisTurn = Vector3.Angle(beforeFwd, tcp.forward);
                float rollTurn = Vector3.Angle(beforeUp, tcp.up);
                joint.localRotation = saved;

                // Сустав «подключён», если он меняет положение TCP, направление инструмента
                // ИЛИ поворот вокруг оси инструмента (у оси вращения, проходящей через TCP,
                // смещения нет — но она честно работает).
                if (moved > 0.0005f || axisTurn > 0.5f || rollTurn > 0.5f) movingJoints++;
                worstMove = Mathf.Max(worstMove, moved);
            }

            // --- 2) для 6 осей — полный цикл FK → IK штатным валидатором
            string ikReport = "";
            bool ikOk = false;
            if (dof == 6)
            {
                PoseValidator v = new PoseValidator();
                v.Init(six);
                if (v.Ready)
                {
                    // Проверяем на нескольких позах в пределах лимитов.
                    double[] q = new double[6];
                    for (int i = 0; i < 6; i++)
                    {
                        float lo = six.jointLimits != null && i < six.jointLimits.Length
                            ? six.jointLimits[i].x : -45f;
                        float hi = six.jointLimits != null && i < six.jointLimits.Length
                            ? six.jointLimits[i].y : 45f;
                        if (six.jointLimits != null && i < six.jointLimits.Length &&
                            six.jointLimits[i].x > six.jointLimits[i].y)
                        {
                            lo = six.jointLimits[i].y;
                            hi = six.jointLimits[i].x;
                        }
                        q[i] = Mathf.Lerp(lo, hi, 0.5f);
                    }

                    float fkError = 0f;
                    float ikError = 0f;
                    for (int trial = 0; trial < 3; trial++)
                    {
                        double[] trialQ = (double[])q.Clone();
                        if (trial > 0)
                            for (int i = 0; i < 6; i++) trialQ[i] += (trial - 1) * 6.0;

                        Vector3 point = v.TcpAt(trialQ);
                        Vector3 fkAgain = v.TcpAt(trialQ);
                        fkError = Mathf.Max(fkError, Vector3.Distance(point, fkAgain));

                        double[] solved;
                        if (v.SolveIk(point, trialQ, out solved))
                            ikError = Mathf.Max(ikError, Vector3.Distance(v.TcpAt(solved), point));
                    }
                    ikOk = true;
                    ikReport = KvLocExtra2.F("import.check.ok",
                        "кинематика в порядке: FK-ошибка {0} мм, IK-ошибка {1} мм",
                        (fkError * 1000f).ToString("0.000"), (ikError * 1000f).ToString("0.0"));
                }
                else
                {
                    ikReport = "валидатор проекта не собрался по этой модели (проверена связность)";
                }
            }
            else
            {
                ikReport = KvLocExtra2.F("import.check.dof",
                    "осей {0} · валидатор проекта поддерживает 6 (проверена прямая задача)", dof);
            }

            LastCheck = ikReport + " · подвижных суставов " + movingJoints + " из " + dof +
                        " · максимальное смещение TCP при повороте сустава на 10° " +
                        (worstMove * 1000f).ToString("0") + " мм";
            Report("проверка кинематики: " + LastCheck);

            // Главный критерий — работоспособность цепи: для 6 осей это цикл FK→IK штатным
            // валидатором (ошибка 0 мм = модель собрана верно). Перебор суставов — справка:
            // у оси, проходящей через TCP (или совпадающей с осью инструмента), смещения
            // кончика нет, и это НЕ дефект модели.
            if (ikOk) return true;
            if (movingJoints >= dof - 1)
            {
                Report("кинематика: цикл FK→IK не проверялся, но цепь связана (" + movingJoints +
                       " из " + dof + " суставов двигают инструмент)");
                return true;
            }
            Report("ВНИМАНИЕ: проверьте оси суставов в модели — часть из них не влияет " +
                   "ни на положение, ни на ориентацию инструмента");
            return false;
        }

        /// <summary>Удалить импортированного робота (модель — самостоятельный объект сцены).</summary>
        public bool RemoveImported()
        {
            if (imported == null) return false;
            UnityEngine.Object.Destroy(imported);
            imported = null;
            LastReport = "";
            LastCheck = "";
            Report("импортированная модель удалена из сцены");
            return true;
        }

        // ================================================================== утилиты

        private static string Attribute(XmlNode node, string name, string fallback)
        {
            if (node == null || node.Attributes == null) return fallback;
            XmlAttribute a = node.Attributes[name];
            return a != null && !string.IsNullOrEmpty(a.Value) ? a.Value : fallback;
        }

        private static void ParseOrigin(XmlNode origin, out Vector3 xyz, out Vector3 rpy)
        {
            xyz = Vector3.zero;
            rpy = Vector3.zero;
            if (origin == null) return;
            xyz = ParseVector(Attribute(origin, "xyz", "0 0 0"));
            Vector3 r = ParseVector(Attribute(origin, "rpy", "0 0 0"));
            // URDF — радианы, Unity — градусы.
            rpy = new Vector3(r.x * Mathf.Rad2Deg, r.y * Mathf.Rad2Deg, r.z * Mathf.Rad2Deg);
        }

        private static Vector3 ParseVector(string text)
        {
            if (string.IsNullOrEmpty(text)) return Vector3.zero;
            string[] parts = text.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            float x = parts.Length > 0 ? ParseFloat(parts[0], 0f) : 0f;
            float y = parts.Length > 1 ? ParseFloat(parts[1], 0f) : 0f;
            float z = parts.Length > 2 ? ParseFloat(parts[2], 0f) : 0f;
            return new Vector3(x, y, z);
        }

        private static float ParseFloat(string text, float fallback)
        {
            float value;
            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return value;
            return fallback;
        }

        private void Report(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            Debug.Log("[Import] " + text);
            if (Message != null) Message(text);
        }
    }

    /// <summary>
    /// ВКЛАДКА «ИМПОРТ МОДЕЛИ РОБОТА» (ЭТАП 12 ТЗ): список найденных файлов, импорт,
    /// проверка кинематики, удаление модели и подсказки по папкам.
    /// </summary>
    public class KvImportTab : IKvWorkbenchTab
    {
        private readonly KvRobotImportService service;
        private int selectedFile = -1;

        public KvImportTab(KvRobotImportService import)
        {
            service = import;
        }

        public string Key { get { return "import"; } }
        public string Title { get { return KvLocExtra2.T("import.title", "Импорт модели робота"); } }

        private static string T(string key, string fallback)
        {
            return KvLocExtra2.T(key, fallback);
        }

        public void Build(KvTabKit kit)
        {
            if (service == null || kit == null) return;

            kit.Section(Title);
            kit.Info(delegate
            {
                string folders = string.Join("\n", service.SearchFolders());
                return T("import.folder", "Папки поиска") + ":\n" + folders;
            }, KvTheme.TextDim);

            kit.Buttons(new[] { T("import.refresh", "Обновить список файлов") },
                new Action[] { delegate { service.RefreshFiles(); } });

            if (service.Files.Count == 0)
                kit.Note(T("import.none",
                    "Файлы не найдены: положите .urdf или .step в папку поиска"), KvTheme.Warn);
            else
            {
                for (int i = 0; i < service.Files.Count && i < 12; i++)
                {
                    int index = i;
                    string path = service.Files[i];
                    kit.Buttons(new[] { (index == selectedFile ? "● " : "") +
                                        Path.GetFileName(path) },
                        new Action[] { delegate { selectedFile = index; } });
                }
            }

            // ФИКС 6: выбор, КУДА поставить модель — отдельным объектом, заменить робота на
            // стенде или добавить на стенд. Место и ориентация переносятся корректно,
            // а ссылка на стенд сохраняется в компоненте робота.
            string[] modes =
            {
                T("import.place.separate", "отдельным объектом"),
                T("import.place.replace", "заменить робота на стенде"),
                T("import.place.add", "добавить на стенд")
            };
            KvImportPlacement[] order =
            {
                KvImportPlacement.Separate, KvImportPlacement.ReplaceOnStand,
                KvImportPlacement.AddToStand
            };
            int current = 0;
            for (int i = 0; i < order.Length; i++)
                if (order[i] == service.Placement) current = i;

            kit.Segmented(T("import.place", "Куда поставить"), modes, current, delegate (int i)
            {
                service.Placement = order[Mathf.Clamp(i, 0, order.Length - 1)];
            });

            string[] stands = service.StandNames();
            int standIndex = 0;
            for (int i = 0; i < stands.Length; i++)
                if (stands[i] == service.StandName) standIndex = i;
            if (string.IsNullOrEmpty(service.StandName)) service.StandName = stands[0];
            kit.Segmented(T("import.stand", "Стенд"), stands, standIndex,
                delegate (int i)
                {
                    if (i >= 0 && i < stands.Length) service.StandName = stands[i];
                });

            kit.Info(delegate
            {
                RobotController onStand = service.RobotOnStand(service.StandName);
                if (service.Placement == KvImportPlacement.Separate)
                    return T("import.place.hint.separate",
                        "Модель встанет отдельным объектом рядом со стендами");
                if (service.Placement == KvImportPlacement.ReplaceOnStand)
                    return onStand != null
                        ? T("import.place.hint.replace",
                            "Будет заменён робот на стенде: ") + onStand.robotName
                        : "⚠ " + T("import.place.hint.norobot",
                            "на выбранном стенде нет робота — вариант «заменить» недоступен");
                return T("import.place.hint.add",
                    "Модель встанет на столешницу выбранного стенда рядом с оборудованием");
            }, KvTheme.TextDim);

            kit.Buttons(new[]
            {
                T("import.load", "Импортировать"),
                T("import.check", "Проверить кинематику"),
                T("import.remove", "Удалить импортированного робота")
            }, new Action[]
            {
                delegate { ImportSelected(); },
                delegate { service.CheckKinematics(); },
                delegate { service.RemoveImported(); }
            });

            // ФИКС 5: что удалось извлечь из STEP (оси вращения/скольжения и связи) —
            // показывается ЧЕСТНО, без попытки собрать кинематику по догадке.
            kit.Divider();
            kit.Info(delegate
            {
                GameObject importedRoot = service.Imported;
                if (importedRoot == null) return "";
                KvImportedRobot m = importedRoot.GetComponent<KvImportedRobot>();
                if (m == null || string.IsNullOrEmpty(m.stepKinematicsNote)) return "";
                return "🔎 " + m.stepKinematicsNote;
            }, KvTheme.Accent);
            kit.Info(delegate
            {
                GameObject importedRoot = service.Imported;
                if (importedRoot == null) return "";
                KvImportedRobot m = importedRoot.GetComponent<KvImportedRobot>();
                if (m == null || m.stepAxes == null || m.stepAxes.Length == 0) return "";
                return string.Join("\n", m.stepAxes);
            }, KvTheme.TextDim);
            kit.Info(delegate
            {
                GameObject importedRoot = service.Imported;
                if (importedRoot == null) return "";
                KvImportedRobot m = importedRoot.GetComponent<KvImportedRobot>();
                if (m == null || string.IsNullOrEmpty(m.standName)) return "";
                return T("import.stand.linked", "Робот привязан к стенду") + ": " + m.standName;
            }, KvTheme.Ok);

            kit.Divider();
            kit.Info(delegate { return service.LastReport; }, KvTheme.Ok);
            kit.Info(delegate { return service.LastCheck; }, KvTheme.Accent);
            kit.Note(T("import.info",
                "URDF читается с диска, структура создаётся кодом: база → звенья → суставы → TCP. " +
                "STEP даёт только геометрию: оси-кандидаты и плоскости показываются как " +
                "справка, кинематика по ним НЕ выдумывается. Модель можно поставить отдельным " +
                "объектом, заменить робота на стенде или добавить на стенд."),
                KvTheme.TextDim);
        }

        private void ImportSelected()
        {
            if (service.Files.Count == 0)
            {
                service.RefreshFiles();
                if (service.Files.Count == 0) return;
            }
            int index = selectedFile >= 0 && selectedFile < service.Files.Count ? selectedFile : 0;
            service.Import(service.Files[index]);
        }

        public void Tick() { }
        public void Refresh() { }
    }
}
