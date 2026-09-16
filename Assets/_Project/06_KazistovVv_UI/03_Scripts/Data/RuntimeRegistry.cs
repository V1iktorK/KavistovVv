using System;
using System.Collections.Generic;
using UnityEngine;

namespace KazistovVvUI
{
    /// <summary>
    /// Единый реестр «проекта»: все роботы и объекты, которые показывает
    /// дерево слева. Наполняется автоматически из сцены и при спавне.
    /// </summary>
    public static class RuntimeRegistry
    {
        public static readonly List<ProjectNode> Roots = new List<ProjectNode>();

        /// <summary>Событие изменения реестра (после добавления/удаления).</summary>
        public static event Action Changed;

        public static void NotifyChanged()
        {
            Changed?.Invoke();
        }

        public static void Clear()
        {
            Roots.Clear();
        }

        /// <summary>Полное перестроение реестра из сцены.</summary>
        public static void RebuildFromScene()
        {
            Clear();

            var robots = UnityEngine.Object.FindObjectsByType<RobotController>(
                FindObjectsInactive.Include);

            foreach (RobotController rc in robots)
            {
                if (rc == null) continue;
                if (IsHidden(rc.gameObject)) continue;   // служебные копии (фантомы) — не в дереве
                ProjectNode node = CreateNodeFor(rc.transform, rc);
                Roots.Add(node);
            }

            // Прочие «объекты» (столы и т.п.) ищутся по компоненту-маркеру.
            var markers = UnityEngine.Object.FindObjectsByType<RegisteredObject>(
                FindObjectsInactive.Include);
            foreach (RegisteredObject mo in markers)
            {
                if (mo == null || mo.Node != null) continue;
                if (IsHidden(mo.gameObject)) continue;
                ProjectNode node = new ProjectNode(
                    Guid.NewGuid().ToString("N"),
                    mo.DisplayName,
                    ProjectNodeKind.Table,
                    mo.transform,
                    null);
                mo.Node = node;
                Roots.Add(node);
            }

            // Порядок узлов — как в иерархии сцены: выбор «первого» узла при старте
            // (и, значит, активного робота) становится детерминированным.
            Roots.Sort((a, b) => TrajectoryCore.HierarchyOrder.Compare(
                a != null ? a.WorldTransform : null,
                b != null ? b.WorldTransform : null));

            NotifyChanged();
        }

        /// <summary>Объект скрыт из иерархии (HideFlags.HideInHierarchy) — в дерево не попадает.</summary>
        public static bool IsHidden(GameObject go)
        {
            return go != null && (go.hideFlags & HideFlags.HideInHierarchy) != 0;
        }

        public static ProjectNode FindRobotNode(RobotController robot)
        {
            if (robot == null) return null;
            foreach (ProjectNode root in Roots)
            {
                if (root.Robot == robot) return root;
            }
            return null;
        }

        public static ProjectNode CreateNodeFor(Transform world, RobotController robot)
        {
            string id = Guid.NewGuid().ToString("N");
            ProjectNode node = new ProjectNode(
                id,
                robot != null && !string.IsNullOrEmpty(robot.robotName) ? robot.robotName : world.name,
                robot is SixAxisController || robot is SCARAController ? ProjectNodeKind.Robot : ProjectNodeKind.Object,
                world,
                robot);

            // Дочерние «оси» для 6-осевого — как ветви дерева.
            if (robot is SixAxisController six)
            {
                if (six.jointTransforms != null)
                {
                    foreach (Transform jt in six.jointTransforms)
                    {
                        if (jt == null) continue;
                        node.Children.Add(new ProjectNode(
                            Guid.NewGuid().ToString("N"),
                            "Ось: " + jt.name,
                            ProjectNodeKind.Axis,
                            jt,
                            robot));
                    }
                }
            }

            return node;
        }
    }

    /// <summary>
    /// Маркер на произвольных объектах сцены (столы и т.п.), чтобы реестр
    /// мог их найти и показать в дереве. Вешается автоматически при спавне.
    /// </summary>
    public class RegisteredObject : MonoBehaviour
    {
        // Runtime-only cache: заполняется при спавне (KvRobotImport/ObjectSpawner/StandBuilder)
        // и пересобирается RuntimeRegistry.RebuildFromScene(), поэтому сериализация ему НЕ нужна.
        // UAC1001 («Public field skipped by serialization…») гасится документированным способом —
        // [System.NonSerialized] (Unity Manual: Serialization rules analyzer reference, UAC1001).
        // Ставить сюда [System.Serializable] НЕЛЬЗЯ: ProjectNode держит рекурсивное поле
        // List<ProjectNode> Children (это deep self-reference cycle -> UAC1007/UAC1008),
        // нетипизируемый object Tag и bool? canHide (Unity их не сериализует), а также
        // не имеет конструктора без параметров. Это дало бы новые предупреждения и
        // раздуло бы сцену сериализованной копией дерева на каждом маркере.
        [System.NonSerialized, HideInInspector] public ProjectNode Node;
        public string DisplayName = "Объект";
    }
}
