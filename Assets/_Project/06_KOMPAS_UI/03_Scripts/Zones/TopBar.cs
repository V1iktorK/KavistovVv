using System;
using UnityEngine;
using UnityEngine.UI;

namespace KompasUI
{
    /// <summary>
    /// Верхняя панель (Toolbar) в стиле КОМПАС: плотный ряд кнопок-заглушек.
    /// Кнопки «Добавить стол» и «Добавить робота» запускают режим размещения.
    /// </summary>
    public class TopBar : MonoBehaviour
    {
        public RectTransform Container { get; private set; }

        private Action<SpawnKind> onPlacementRequested;

        public void Build(RectTransform parent, Action<SpawnKind> placementRequested,
            Action settingsRequested = null)
        {
            onPlacementRequested = placementRequested;

            // Подложка всей верхней зоны.
            Image panel = KompasTheme.CreatePanel(parent, "TopBar", KompasTheme.PanelHeader);
            panel.rectTransform.anchorMin = new Vector2(0f, 1f);
            panel.rectTransform.anchorMax = new Vector2(1f, 1f);
            panel.rectTransform.pivot = new Vector2(0.5f, 1f);
            panel.rectTransform.sizeDelta = new Vector2(0f, 44f);
            panel.rectTransform.anchoredPosition = Vector2.zero;

            // Горизонтальный плотный ряд кнопок.
            GameObject rowGo = new GameObject("Row", typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            rowGo.transform.SetParent(panel.transform, false);
            Container = (RectTransform)rowGo.transform;
            KompasTheme.Stretch(Container, 8, 8, 4, 4);

            HorizontalLayoutGroup hlg = rowGo.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            ContentSizeFitter csf = rowGo.GetComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            // --- Кнопки-заглушки (плотный ряд как в КОМПАС) ---
            AddToolButton("Файл");
            AddToolButton("Правка");
            AddToolButton("Вид");
            AddToolButton("Вставка");
            AddToolButton("Операции");
            AddToolButton("Сервис");
            AddToolButton("Окно");
            AddToolButton("Справка");

            AddSeparator();

            Button addTable = AddToolButton("Добавить стол", accent: true);
            addTable.onClick.AddListener(() => onPlacementRequested?.Invoke(SpawnKind.Table));

            Button addRobot = AddToolButton("Добавить робота", accent: true);
            addRobot.onClick.AddListener(() => onPlacementRequested?.Invoke(SpawnKind.Robot));

            AddSeparator();

            Button settings = AddToolButton("Настройки");
            settings.onClick.AddListener(() => settingsRequested?.Invoke());
        }

        private void AddSeparator()
        {
            Image sep = KompasTheme.CreatePanel(Container, "Sep", KompasTheme.Border);
            sep.rectTransform.sizeDelta = new Vector2(2f, 26f);
        }

        private Button AddToolButton(string label, bool accent = false)
        {
            Button b = KompasTheme.CreateButton(Container, "Btn_" + label, label, null, 26);
            LayoutElement le = b.gameObject.AddComponent<LayoutElement>();
            le.minWidth = 92f;
            le.minHeight = 26f;
            if (accent)
            {
                Image img = b.GetComponent<Image>();
                img.color = KompasTheme.ButtonActive;
            }
            return b;
        }
    }
}
