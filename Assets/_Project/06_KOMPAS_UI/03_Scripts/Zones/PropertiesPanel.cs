using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace KompasUI
{
    /// <summary>
    /// Правая панель «Свойства»: показывает параметры выбранного узла.
    /// Сверху — кнопка-переключатель, полностью скрывающая панель (и возвращающая её).
    /// </summary>
    public class PropertiesPanel : MonoBehaviour
    {
        private RectTransform panelRect;
        private Text headerText;
        private Text propsText;
        private readonly List<GameObject> rows = new List<GameObject>();

        public bool IsVisible { get; private set; } = true;

        private const float OpenWidth = 300f;
        private const float ClosedWidth = 28f;

        public void Build(RectTransform parent)
        {
            Image panel = KompasTheme.CreatePanel(parent, "PropertiesPanel", KompasTheme.PanelBg);
            panelRect = panel.rectTransform;
            panelRect.anchorMin = new Vector2(1f, 0f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(1f, 0.5f);
            panelRect.sizeDelta = new Vector2(OpenWidth, -88f);
            panelRect.anchoredPosition = new Vector2(0f, -6f);

            // Кнопка-переключатель (стрелка) — всегда видна у края.
            Button toggle = KompasTheme.CreateSmallButton(
                (RectTransform)panel.transform,
                "Toggle",
                "❮",
                ToggleVisibility);
            RectTransform tr = (RectTransform)toggle.transform;
            tr.anchorMin = new Vector2(0f, 0.5f);
            tr.anchorMax = new Vector2(0f, 0.5f);
            tr.pivot = new Vector2(0f, 0.5f);
            tr.sizeDelta = new Vector2(28f, 60f);
            tr.anchoredPosition = new Vector2(0f, 0f);
            toggle.GetComponentInChildren<Text>().text = "❮";

            // Заголовок
            headerText = KompasTheme.CreateText((RectTransform)panel.transform, "Header", "Свойства",
                KompasTheme.FontSize, TextAnchor.MiddleLeft, KompasTheme.TextMain);
            RectTransform hr = headerText.rectTransform;
            hr.anchorMin = new Vector2(0f, 1f);
            hr.anchorMax = new Vector2(1f, 1f);
            hr.pivot = new Vector2(0.5f, 1f);
            hr.sizeDelta = new Vector2(0f, 30f);
            hr.offsetMin = new Vector2(10f, 0f);
            hr.offsetMax = new Vector2(-10f, 0f);

            // Область текста свойств
            propsText = KompasTheme.CreateText((RectTransform)panel.transform, "Props", "Выберите объект",
                KompasTheme.FontSizeSmall, TextAnchor.UpperLeft, KompasTheme.TextMain);
            RectTransform pr = propsText.rectTransform;
            pr.anchorMin = new Vector2(0f, 0f);
            pr.anchorMax = new Vector2(1f, 1f);
            pr.offsetMin = new Vector2(10f, 10f);
            pr.offsetMax = new Vector2(-10f, -36f);
            propsText.horizontalOverflow = HorizontalWrapMode.Wrap;
            propsText.verticalOverflow = VerticalWrapMode.Truncate;
        }

        public void ToggleVisibility()
        {
            IsVisible = !IsVisible;
            ApplyVisibility();
        }

        private void ApplyVisibility()
        {
            float target = IsVisible ? OpenWidth : ClosedWidth;
            // Плавность опустим — мгновенно, чтобы не плодить корутины в каркасе.
            panelRect.sizeDelta = new Vector2(target, panelRect.sizeDelta.y);

            // В свёрнутом виде прячем содержимое, оставляя кнопку-стрелку.
            if (headerText != null) headerText.gameObject.SetActive(IsVisible);
            if (propsText != null) propsText.gameObject.SetActive(IsVisible);
        }

        public void ShowNode(ProjectNode node)
        {
            if (headerText != null)
                headerText.text = node != null ? node.DisplayName : "Свойства";
            if (propsText != null)
                propsText.text = node != null ? BuildProps(node) : "Выберите объект в дереве или кликните по роботу.";
        }

        private static string BuildProps(ProjectNode node)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Тип: " + KindName(node.Kind));
            if (node.Robot != null)
            {
                sb.AppendLine("Управление: " + node.Robot.GetType().Name);
                sb.AppendLine("Скорость: " + node.Robot.maxSpeed.ToString("0.00"));
                sb.AppendLine("Активен: " + node.Robot.isActive);
                sb.AppendLine("Температура: " + node.Robot.jointTemperature.ToString("0.0") + " °C");
                sb.AppendLine("Наработка: " + node.Robot.operatingHours.ToString("0.00") + " ч");
            }
            if (node.WorldTransform != null)
            {
                Vector3 p = node.WorldTransform.position;
                sb.AppendLine("Позиция: " + p.x.ToString("0.00") + ", " + p.y.ToString("0.00") + ", " + p.z.ToString("0.00"));
                Vector3 r = node.WorldTransform.eulerAngles;
                sb.AppendLine("Поворот: " + r.x.ToString("0") + "°, " + r.y.ToString("0") + "°, " + r.z.ToString("0") + "°");
            }
            return sb.ToString();
        }

        private static string KindName(KompasNodeKind kind)
        {
            switch (kind)
            {
                case KompasNodeKind.Robot: return "Робот";
                case KompasNodeKind.Table: return "Стол / объект";
                case KompasNodeKind.Axis: return "Ось";
                default: return kind.ToString();
            }
        }
    }
}
