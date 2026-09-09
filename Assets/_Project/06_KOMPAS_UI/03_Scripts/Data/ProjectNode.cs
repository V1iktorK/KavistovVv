using UnityEngine;

namespace KompasUI
{
    /// <summary>Тип узла дерева проекта.</summary>
    public enum KompasNodeKind
    {
        Robot,
        Axis,   // дочерний узел (ось робота)
        Table,
        Object
    }

    /// <summary>
    /// Узел «дерева проекта»: любая сущность, которую пользователь видит слева
    /// и свойства которой показывает правая панель.
    /// </summary>
    public class ProjectNode
    {
        public string Id;
        public string DisplayName;
        public KompasNodeKind Kind;
        public Transform WorldTransform;   // объект в сцене (может быть null для виртуальных узлов)
        public RobotController Robot;      // если узел — робот
        public System.Collections.Generic.List<ProjectNode> Children;

        public ProjectNode(string id, string name, KompasNodeKind kind, Transform world, RobotController robot)
        {
            Id = id;
            DisplayName = name;
            Kind = kind;
            WorldTransform = world;
            Robot = robot;
            Children = new System.Collections.Generic.List<ProjectNode>();
        }

        public bool IsSelectable => Kind != KompasNodeKind.Axis || true;
    }
}
