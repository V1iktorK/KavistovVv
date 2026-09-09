using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KompasUI
{
    /// <summary>
    /// Левая панель «Дерево проекта»: иерархия роботов и объектов,
    /// как дерево в КОМПАС-3D. Клик по узлу → выбор → свойства справа.
    /// </summary>
    public class TreePanel : MonoBehaviour
    {
        private RectTransform listContent;
        private Action<ProjectNode> onNodeSelected;
        private readonly List<GameObject> rowPool = new List<GameObject>();

        public void Build(RectTransform parent, Action<ProjectNode> selectNode)
        {
            onNodeSelected = selectNode;

            Image panel = KompasTheme.CreatePanel(parent, "TreePanel", KompasTheme.PanelBg);
            panel.rectTransform.anchorMin = new Vector2(0f, 0f);
            panel.rectTransform.anchorMax = new Vector2(0f, 1f);
            panel.rectTransform.pivot = new Vector2(0f, 0.5f);
            panel.rectTransform.sizeDelta = new Vector2(250f, -88f); // под верхней панелью и над статус-баром
            panel.rectTransform.anchoredPosition = new Vector2(0f, -6f);

            // Заголовок панели
            Text header = KompasTheme.CreateText((RectTransform)panel.transform, "Header", "Дерево проекта",
                KompasTheme.FontSize, TextAnchor.MiddleLeft, KompasTheme.TextMain);
            KompasTheme.Stretch(header.rectTransform, 10, 0, 0, 0);
            header.rectTransform.sizeDelta = new Vector2(0f, 28f);
            header.rectTransform.anchorMin = new Vector2(0f, 1f);
            header.rectTransform.anchorMax = new Vector2(1f, 1f);
            header.rectTransform.pivot = new Vector2(0.5f, 1f);

            // Scroll (список узлов)
            GameObject scrollGo = new GameObject("Scroll", typeof(ScrollRect), typeof(Image));
            scrollGo.transform.SetParent(panel.transform, false);
            Image scrollBg = scrollGo.GetComponent<Image>();
            scrollBg.color = new Color(0.12f, 0.13f, 0.15f, 1f);

            RectTransform scrollRt = (RectTransform)scrollGo.transform;
            scrollRt.anchorMin = new Vector2(0f, 0f);
            scrollRt.anchorMax = new Vector2(1f, 1f);
            scrollRt.offsetMin = new Vector2(4f, 4f);
            scrollRt.offsetMax = new Vector2(-4f, -30f);

            ScrollRect scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 24f;

            // Content с VerticalLayoutGroup
            GameObject contentGo = new GameObject("Content", typeof(RectTransform),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(scrollRt, false);
            listContent = (RectTransform)contentGo.transform;
            listContent.anchorMin = new Vector2(0f, 1f);
            listContent.anchorMax = new Vector2(1f, 1f);
            listContent.pivot = new Vector2(0.5f, 1f);
            listContent.offsetMin = new Vector2(4f, 4f);
            listContent.offsetMax = new Vector2(-4f, 0f);

            VerticalLayoutGroup vlg = contentGo.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 1f;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlHeight = true;

            ContentSizeFitter csf = contentGo.GetComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = listContent;
        }

        public void Rebuild(List<ProjectNode> roots, ProjectNode selected)
        {
            ClearRows();
            if (roots == null) return;

            foreach (ProjectNode root in roots)
            {
                CreateRow(root, 0, selected);
                if (root.Children != null && root.Children.Count > 0)
                {
                    foreach (ProjectNode child in root.Children)
                    {
                        CreateRow(child, 1, selected);
                    }
                }
            }
        }

        private void CreateRow(ProjectNode node, int depth, ProjectNode selected)
        {
            GameObject row = new GameObject("Row_" + node.DisplayName,
                typeof(Image), typeof(Button), typeof(LayoutElement));
            row.transform.SetParent(listContent, false);

            Image bg = row.GetComponent<Image>();
            bg.color = node == selected ? KompasTheme.ButtonActive : KompasTheme.ButtonBg;
            bg.sprite = UIFactory.GetSprite();

            LayoutElement le = row.GetComponent<LayoutElement>();
            le.minHeight = 24f;

            Button btn = row.GetComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            btn.colors = cb;
            ProjectNode captured = node;
            btn.onClick.AddListener(() => onNodeSelected?.Invoke(captured));

            Text label = KompasTheme.CreateText((RectTransform)row.transform, "Label",
                (depth > 0 ? "   ⤷ " : "▸ ") + node.DisplayName,
                KompasTheme.FontSizeSmall, TextAnchor.MiddleLeft, KompasTheme.TextMain);
            KompasTheme.Stretch(label.rectTransform, 8 + depth * 14, 4, 0, 0);
            label.raycastTarget = false;

            rowPool.Add(row);
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
