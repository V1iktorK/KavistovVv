using System;
using System.Collections.Generic;
using UnityEngine;

namespace KompasUI
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
                ProjectNode node = CreateNodeFor(rc.transform, rc);
                Roots.Add(node);
            }

            // Прочие «объекты» (столы и т.п.) ищутся по компоненту-маркеру.
            var markers = UnityEngine.Object.FindObjectsByType<RegisteredObject>(
                FindObjectsInactive.Include);
            foreach (RegisteredObject mo in markers)
            {
                if (mo == null || mo.Node != null) continue;
                ProjectNode node = new ProjectNode(
                    Guid.NewGuid().ToString("N"),
                    mo.DisplayName,
                    KompasNodeKind.Table,
                    mo.transform,
                    null);
                mo.Node = node;
                Roots.Add(node);
            }

            NotifyChanged();
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
                robot is SixAxisController || robot is SCARAController ? KompasNodeKind.Robot : KompasNodeKind.Object,
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
                            KompasNodeKind.Axis,
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
        [HideInInspector] public ProjectNode Node;
        public string DisplayName = "Объект";
    }
}
