using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KompasUI
{
    /// <summary>
    /// Левая панель «Дерево проекта» в стиле Unity/Hierarchy:
    /// — роботы — корневые узлы со стрелкой ▸/▾ (разворачивание осей);
    /// — объекты/столы — листья;
    /// — содержимое обрезается маской (не вываливается за панель), со скроллом.
    /// Клик по строке выбирает узел → свойства справа.
    /// </summary>
    public class TreePanel : MonoBehaviour
    {
        private RectTransform viewport;
        private RectTransform listContent;
        private Action<ProjectNode> onNodeSelected;
        private Action rootProvider;
        private readonly List<GameObject> rowPool = new List<GameObject>();
        private readonly HashSet<string> expanded = new HashSet<string>();
        private ProjectNode currentSelected;

        public void Build(RectTransform parent, Action<ProjectNode> selectNode)
        {
            onNodeSelected = selectNode;

            Image panel = KompasTheme.CreatePanel(parent, "TreePanel", KompasTheme.PanelBg);
            RectTransform pr = panel.rectTransform;
            pr.anchorMin = new Vector2(0f, 0f);
            pr.anchorMax = new Vector2(0f, 1f);
            pr.pivot = new Vector2(0f, 0.5f);
            pr.offsetMin = new Vector2(0f, 30f);      // над статус-баром
            pr.offsetMax = new Vector2(250f, -44f);   // под верхней панелью
            pr.anchoredPosition = Vector2.zero;

            // Заголовок
            Text header = KompasTheme.CreateText(pr, "Header", "Проект",
                KompasTheme.FontSize, TextAnchor.MiddleLeft, KompasTheme.TextDim);
            RectTransform hr = header.rectTransform;
            hr.anchorMin = new Vector2(0f, 1f);
            hr.anchorMax = new Vector2(1f, 1f);
            hr.pivot = new Vector2(0.5f, 1f);
            hr.sizeDelta = new Vector2(0f, 24f);
            hr.offsetMin = new Vector2(10f, 0f);
            hr.offsetMax = new Vector2(-6f, 0f);

            // ScrollRect + маска (контент НЕ вылезает за панель)
            GameObject scrollGo = new GameObject("Scroll", typeof(ScrollRect), typeof(RectMask2D), typeof(Image));
            scrollGo.transform.SetParent(panel.transform, false);
            Image bg = scrollGo.GetComponent<Image>();
            bg.color = new Color(0.115f, 0.125f, 0.145f, 1f);

            viewport = (RectTransform)scrollGo.transform;
            viewport.anchorMin = new Vector2(0f, 0f);
            viewport.anchorMax = new Vector2(1f, 1f);
            viewport.offsetMin = new Vector2(4f, 4f);
            viewport.offsetMax = new Vector2(-4f, -28f);

            ScrollRect scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 24f;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.viewport = viewport;

            GameObject contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewport, false);
            listContent = (RectTransform)contentGo.transform;
            listContent.anchorMin = new Vector2(0f, 1f);
            listContent.anchorMax = new Vector2(1f, 1f);
            listContent.pivot = new Vector2(0.5f, 1f);
            listContent.offsetMin = Vector2.zero;
            listContent.offsetMax = Vector2.zero;
            listContent.sizeDelta = new Vector2(0f, 0f);

            VerticalLayoutGroup vlg = contentGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 1f;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.padding = new RectOffset(2, 2, 2, 2);

            ContentSizeFitter csf = contentGo.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            scroll.content = listContent;
        }

        public void BindRootProvider(Action rebuild) { rootProvider = rebuild; }

        /// <summary>Разворачивает узел по Id (например, робота при старте).</summary>
        public void Expand(string nodeId)
        {
            if (!string.IsNullOrEmpty(nodeId)) expanded.Add(nodeId);
        }

        public void Rebuild(List<ProjectNode> roots, ProjectNode selected)
        {
            ClearRows();
            currentSelected = selected;
            if (roots == null) return;

            foreach (ProjectNode root in roots)
            {
                bool hasChildren = root.Children != null && root.Children.Count > 0;
                bool isOpen = expanded.Contains(root.Id);
                CreateRow(root, 0, hasChildren, isOpen, selected);

                if (hasChildren && isOpen)
                {
                    foreach (ProjectNode child in root.Children)
                        CreateRow(child, 1, false, false, selected);
                }
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(listContent);
        }

        private void CreateRow(ProjectNode node, int depth, bool expandable, bool isOpen, ProjectNode selected)
        {
            GameObject row = new GameObject("Row", typeof(Image), typeof(Button), typeof(LayoutElement));
            row.transform.SetParent(listContent, false);

            Image bg = row.GetComponent<Image>();
            bg.color = node == selected ? KompasTheme.ButtonActive : new Color(0.16f, 0.17f, 0.19f, 0.85f);
            bg.sprite = UIFactory.GetSprite();

            LayoutElement le = row.GetComponent<LayoutElement>();
            le.minHeight = 22f;
            le.preferredHeight = 22f;

            Button btn = row.GetComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            btn.colors = cb;
            ProjectNode captured = node;
            btn.onClick.AddListener(() => onNodeSelected?.Invoke(captured));

            float indent = 6f + depth * 16f;

            if (expandable)
            {
                Button arrow = KompasTheme.CreateSmallButton((RectTransform)row.transform, "Arrow",
                    isOpen ? "▾" : "▸", () =>
                    {
                        if (isOpen) expanded.Remove(node.Id);
                        else expanded.Add(node.Id);
                        rootProvider?.Invoke();
                    });
                RectTransform ar = (RectTransform)arrow.transform;
                ar.anchorMin = new Vector2(0f, 0.5f);
                ar.anchorMax = new Vector2(0f, 0.5f);
                ar.pivot = new Vector2(0f, 0.5f);
                ar.sizeDelta = new Vector2(20f, 18f);
                ar.anchoredPosition = new Vector2(4f + depth * 16f, 0f);
                arrow.GetComponentInChildren<Text>().fontSize = KompasTheme.FontSizeSmall;
                arrow.GetComponentInChildren<Text>().raycastTarget = false;
            }

            string icon = IconFor(node.Kind);
            Text label = KompasTheme.CreateText((RectTransform)row.transform, "Label",
                icon + " " + node.DisplayName,
                KompasTheme.FontSizeSmall, TextAnchor.MiddleLeft,
                node == selected ? Color.white : KompasTheme.TextMain);
            RectTransform lr = label.rectTransform;
            lr.anchorMin = new Vector2(0f, 0f);
            lr.anchorMax = new Vector2(1f, 1f);
            lr.offsetMin = new Vector2(indent + 20f, 0f);
            lr.offsetMax = new Vector2(-4f, 0f);
            label.raycastTarget = false;

            rowPool.Add(row);
        }

        private static string IconFor(KompasNodeKind kind)
        {
            switch (kind)
            {
                case KompasNodeKind.Robot: return "◉";
                case KompasNodeKind.Axis: return "◯";
                case KompasNodeKind.Table: return "▦";
                default: return "•";
            }
        }

        private void ClearRows()
        {
            foreach (GameObject go in rowPool)
            {
                if (go != null) Destroy(go);
            }
            rowPool.Clear();
        }
    }
}
