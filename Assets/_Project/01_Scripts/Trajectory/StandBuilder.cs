using UnityEngine;

namespace TrajectoryCore
{
    /// <summary>
    /// Стенды (две «сцены» с оборудованием):
    ///   Стенд 1: стол в (0,0,-24) rot(0,0,0) scale(1,1,1) → 6-осевой робот на нём;
    ///   Стенд 2: стол в (0,0,-28) rot(0,0,0) scale(1,1,1) → SCARA на нём.
    /// Процедура идемпотентна (по именам объектов), масштаб корней = 1, высота стола
    /// задаётся габаритами столешницы (роботы ставятся на верхнюю плоскость).
    /// </summary>
    public static class StandBuilder
    {
        public const string Stand1Name = "Стенд_1_Стол";
        public const string Stand2Name = "Стенд_2_Стол";
        public static readonly Vector3 Stand1Pos = new Vector3(0f, 0f, -24f);
        public static readonly Vector3 Stand2Pos = new Vector3(0f, 0f, -28f);

        /// <summary>Создать оба стенда, если их ещё нет (вызывается из меню редактора и при старте Play).</summary>
        public static void EnsureStands(float tableTopHeight = 0.98f)
        {
            GameObject stand1 = FindByName(Stand1Name);
            if (stand1 == null) stand1 = BuildTable(Stand1Name, Stand1Pos, tableTopHeight);
            GameObject stand2 = FindByName(Stand2Name);
            if (stand2 == null) stand2 = BuildTable(Stand2Name, Stand2Pos, tableTopHeight);

            PlaceRobotOnStand(stand1, typeof(SixAxisController), "Робот_6ос_Стенд1");
            PlaceRobotOnStand(stand2, typeof(SCARAController), "SCARA_Стенд2");
        }

        /// <summary>Стол: столешница + 4 ножки. Корень scale = (1,1,1), размеры заданы деталями.</summary>
        private static GameObject BuildTable(string name, Vector3 position, float topHeight)
        {
            GameObject root = new GameObject(name);
            root.transform.position = position;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            const float topThickness = 0.05f;
            float topY = Mathf.Max(0.2f, topHeight) - topThickness * 0.5f;

            GameObject top = GameObject.CreatePrimitive(PrimitiveType.Cube);
            top.name = "Столешница";
            top.transform.SetParent(root.transform, false);
            top.transform.localScale = new Vector3(1.2f, topThickness, 0.8f);
            top.transform.localPosition = new Vector3(0f, topY, 0f);

            float legHeight = Mathf.Max(0.1f, topHeight - topThickness);
            float legX = 0.5f, legZ = 0.3f;
            for (int i = 0; i < 4; i++)
            {
                GameObject leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leg.name = "Ножка_" + (i + 1);
                leg.transform.SetParent(root.transform, false);
                leg.transform.localScale = new Vector3(0.06f, legHeight, 0.06f);
                leg.transform.localPosition = new Vector3(
                    (i % 2 == 0 ? -legX : legX), legHeight * 0.5f, (i < 2 ? -legZ : legZ));
            }

            root.AddComponent<KompasUI.RegisteredObject>().DisplayName = name;
            return root;
        }

        /// <summary>Поставить копию робота нужного типа на верхнюю плоскость стенда.</summary>
        private static void PlaceRobotOnStand(GameObject stand, System.Type robotType, string copyName)
        {
            if (stand == null) return;
            if (FindByName(copyName) != null) return;   // уже стоит

            RobotController template = FindTemplate(robotType);
            if (template == null)
            {
                Debug.LogWarning("[StandBuilder] Нет шаблона робота типа " + robotType.Name);
                return;
            }

            GameObject copy = Object.Instantiate(template.gameObject);
            copy.name = copyName;
            Bounds standBounds = GetBounds(stand);
            Vector3 pos = new Vector3(stand.transform.position.x, standBounds.max.y, stand.transform.position.z);
            copy.transform.position = pos;
            copy.transform.rotation = Quaternion.identity;
            copy.transform.localScale = Vector3.one;

            SixAxisAutoSetup auto = copy.GetComponent<SixAxisAutoSetup>();
            if (auto != null) Object.Destroy(auto);
            copy.AddComponent<KompasUI.RegisteredObject>().DisplayName = copyName;

            RobotController rc = copy.GetComponent<RobotController>();
            if (rc != null) rc.ClearTarget();
        }

        private static RobotController FindTemplate(System.Type robotType)
        {
            foreach (RobotController rc in Object.FindObjectsByType<RobotController>(
                         FindObjectsInactive.Include))
            {
                if (rc == null) continue;
                if (!robotType.IsInstanceOfType(rc)) continue;
                string n = rc.name;
                if (n.StartsWith("Робот_") || n.StartsWith("SCARA_")) continue; // это копии стендов
                if (rc.GetComponent<KompasUI.RegisteredObject>() != null) continue;
                return rc;
            }
            return null;
        }

        private static GameObject FindByName(string name)
        {
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (t != null && t.name == name) return t.gameObject;
            return null;
        }

        private static Bounds GetBounds(GameObject go)
        {
            Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.2f);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }
    }
}
