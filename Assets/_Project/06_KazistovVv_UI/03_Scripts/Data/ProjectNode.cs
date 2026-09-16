using System.Collections.Generic;
using UnityEngine;

namespace KazistovVvUI
{
    /// <summary>Тип узла дерева моделей.</summary>
    public enum ProjectNodeKind
    {
        Group,        // папка («Столы», «Точки», «Траектории»)
        Robot,        // робот
        Axis,         // ось/сустав робота
        Tcp,          // концевая точка (TCP)
        Table,        // стол/стенд
        Object,       // прочий объект сцены
        Point,        // выбранная пользователем точка
        Trajectory,   // сгенерированная траектория
        Phantom       // фантом (маркер конечной позы)
    }

    /// <summary>
    /// Узел дерева моделей: любая сущность, которую пользователь видит слева
    /// и свойства которой показывает правая панель.
    /// </summary>
    public class ProjectNode
    {
        public string Id;
        /// <summary>Стабильный ключ (переживает пересборку дерева) — по нему хранится
        /// состояние «раскрыт/свёрнут» и пользовательское имя узла.</summary>
        public string Key;
        public string DisplayName;
        /// <summary>Короткая дополнительная строка справа в дереве (например «2.65 ю · 5.3 с»).</summary>
        public string Details = "";
        /// <summary>Полное описание для подсказки.</summary>
        public string Tooltip = "";
        public ProjectNodeKind Kind;
        public Transform WorldTransform;   // объект в сцене (null для виртуальных узлов)
        public RobotController Robot;      // если узел — робот
        public List<ProjectNode> Children;
        /// <summary>Произвольные данные узла (например индекс траектории или Vector3 точки).</summary>
        public object Tag;

        public ProjectNode(string id, string name, ProjectNodeKind kind, Transform world,
            RobotController robot)
        {
            Id = id;
            Key = id;
            DisplayName = name;
            Kind = kind;
            WorldTransform = world;
            Robot = robot;
            Children = new List<ProjectNode>();
        }

        /// <summary>Есть ли у узла связанный объект сцены.</summary>
        public bool HasObject { get { return WorldTransform != null; } }

        /// <summary>Можно ли скрывать/показывать объект узла («глазик»).
        /// Результат кэшируется: обход рендереров делается один раз на узел, а не при
        /// каждой пересборке дерева.</summary>
        public bool CanHide
        {
            get
            {
                if (canHide.HasValue) return canHide.Value;
                if (WorldTransform == null || !WorldTransform.gameObject.activeInHierarchy)
                {
                    canHide = false;
                    return false;
                }
                canHide = WorldTransform.GetComponentsInChildren<Renderer>(true).Length > 0;
                return canHide.Value;
            }
        }

        /// <summary>Сбросить кэш «глазика» (например, после смены объекта узла).</summary>
        public void ResetVisibilityCache()
        {
            canHide = null;
        }

        private bool? canHide;

        /// <summary>Идентификатор иконки узла.</summary>
        public string IconId
        {
            get
            {
                switch (Kind)
                {
                    case ProjectNodeKind.Robot: return "robot";
                    case ProjectNodeKind.Axis: return "joint";
                    case ProjectNodeKind.Tcp: return "tcp";
                    case ProjectNodeKind.Table: return "table";
                    case ProjectNodeKind.Point: return "node-point";
                    case ProjectNodeKind.Trajectory: return "curve";
                    case ProjectNodeKind.Phantom: return "phantom";
                    case ProjectNodeKind.Group: return "layers";
                    default: return "axis";
                }
            }
        }
    }
}
