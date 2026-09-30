using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using KazistovVvUI;

namespace KazistovVvFeatures
{
    /// <summary>
    /// ВКЛАДКА ВЕРСТАКА ПОСТОБРАБОТКИ. Модуль этапов 4–6 (а затем и последующих)
    /// добавляет свою вкладку регистрацией, поэтому окно не нужно править при
    /// появлении новых функций: как реестр команд у тулбара — здесь реестр вкладок.
    /// </summary>
    public interface IKvWorkbenchTab
    {
        /// <summary>Стабильный ключ вкладки («smooth», «topt», «energy»).</summary>
        string Key { get; }
        /// <summary>Подпись вкладки (уже локализованная).</summary>
        string Title { get; }
        /// <summary>Построить содержимое во время `content`.</summary>
        void Build(KvTabKit kit);
        /// <summary>Кадровое обновление (может быть пустым).</summary>
        void Tick();
        /// <summary>Периодическое обновление подписей (0.2 с) — может быть пустым.</summary>
        void Refresh();
    }

    /// <summary>
    /// НАБОР СТРОИТЕЛЕЙ ДЛЯ ВКЛАДКИ: секции, строки «подпись = значение»,
    /// переключатели, ползунки, таблицы и кнопки — в том же стиле uGUI/FreeCAD,
    /// что и остальные панели проекта. Строки с динамическим текстом обновляются
    /// окном (провайдеры `Func&lt;string&gt;`), поэтому в кадре нет сборки мусора.
    /// </summary>
    public class KvTabKit
    {
        internal readonly List<KeyValuePair<Text, Func<string>>> dynamicRows =
            new List<KeyValuePair<Text, Func<string>>>();
        internal RectTransform content;

        public KvTabKit(RectTransform content)
        {
            this.content = content;
        }

        public RectTransform Content { get { return content; } }

        public void Section(string title)
        {
            KvWidgets.SectionHeader(content, title, 20f);
        }

        public Text Note(string text, Color color)
        {
            return KvWidgets.Note(content, text, color, 0f);
        }

        /// <summary>Строка с динамическим текстом: «подпись: значение».</summary>
        public Text Info(Func<string> provider, Color color)
        {
            Text t = KvWidgets.Label(content, "Info", provider != null ? provider() : "", KvTheme.FontSize,
                color, TextAnchor.MiddleLeft);
            // ФИКС 6 (§23): раньше строка растягивалась по обеим осям (`KvTheme.Stretch`) и в
            // контейнере, который высотой детей НЕ управляет, получала нулевую высоту.
            // Теперь: по горизонтали — на всю ширину панели, по вертикали — ровно одна строка.
            RectTransform rt = t.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(-12f, 17f);
            LayoutElement le = t.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 17f;
            le.preferredHeight = 17f;
            if (provider != null) dynamicRows.Add(new KeyValuePair<Text, Func<string>>(t, provider));
            return t;
        }

        /// <summary>Строка таблицы «подпись | значение | значение» (для метрик).</summary>
        public void Table(string caption, Func<string> left, Func<string> right)
        {
            RectTransform row = KvWidgets.CreateRow(content, "TableRow", 17f, 4f);
            LayoutElement le = row.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 17f;
            le.preferredHeight = 17f;
            if (!string.IsNullOrEmpty(caption))
                AddCell(row, "Caption", caption, KvTheme.TextDim, 190f, null);
            AddCell(row, "Left", "", KvTheme.TextMain, 0f, left);
            AddCell(row, "Right", "", KvTheme.TextMain, 0f, right);
        }

        private void AddCell(RectTransform row, string name, string text, Color color, float width,
            Func<string> provider)
        {
            Text t = KvWidgets.Label(row, name, provider != null ? provider() : text,
                KvTheme.FontSizeSmall, color, TextAnchor.MiddleLeft, width > 0f ? width : 110f);
            LayoutElement le = t.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 16f;
            le.preferredHeight = 16f;
            // ФИКС 6 (§23): высота строки ячейки задаётся явно — родительский контейнер
            // высотой детей не управляет, и без этого текст «плавал» в дефолтных 100 px.
            ((RectTransform)t.transform).sizeDelta =
                new Vector2(((RectTransform)t.transform).sizeDelta.x, 16f);
            if (width > 0f)
            {
                le.minWidth = width;
                le.preferredWidth = width;
            }
            else
            {
                le.flexibleWidth = 1f;
                le.minWidth = 80f;
            }
            if (provider != null) dynamicRows.Add(new KeyValuePair<Text, Func<string>>(t, provider));
        }

        public void Toggle(string text, bool value, Action<bool> onChanged)
        {
            KvSwitch sw = KvWidgets.Switch(content, text, value, onChanged);
            LayoutElement le = sw.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = sw.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 20f;
            le.preferredHeight = 20f;
        }

        /// <summary>Ползунок с подписью и текущим значением (для «уровня сглаживания» и лимитов).</summary>
        public Slider Slider(string caption, float min, float max, float value, string format,
            Action<float> onChanged)
        {
            RectTransform row = KvWidgets.CreateRow(content, "SliderRow", 20f, 6f);
            LayoutElement rowLe = row.gameObject.AddComponent<LayoutElement>();
            rowLe.minHeight = 20f;
            rowLe.preferredHeight = 20f;

            Text label = KvWidgets.Label(row, "Caption", caption, KvTheme.FontSizeSmall,
                KvTheme.TextDim, TextAnchor.MiddleLeft, 190f);
            LayoutElement labelLe = label.gameObject.AddComponent<LayoutElement>();
            labelLe.minWidth = 190f;
            labelLe.preferredWidth = 190f;
            labelLe.minHeight = 18f;

            GameObject sliderGo = new GameObject("Slider", typeof(UnityEngine.UI.Slider));
            sliderGo.transform.SetParent(row, false);
            RectTransform sliderRect = (RectTransform)sliderGo.transform;
            sliderRect.sizeDelta = new Vector2(220f, 14f);
            LayoutElement sliderLe = sliderGo.AddComponent<LayoutElement>();
            sliderLe.minWidth = 150f;
            sliderLe.preferredWidth = 220f;
            sliderLe.minHeight = 18f;

            Image track = KvTheme.CreatePanel(sliderRect, "Track", KvTheme.InputBg);
            KvTheme.Stretch(track.rectTransform, 0f, 0f, 6f, 6f);
            Image fill = KvTheme.CreatePanel(track.rectTransform, "Fill", KvTheme.Accent);
            fill.rectTransform.anchorMin = new Vector2(0f, 0f);
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            fill.rectTransform.sizeDelta = new Vector2(0f, 0f);

            GameObject handle = new GameObject("Handle", typeof(Image));
            handle.transform.SetParent(sliderRect, false);
            Image handleImage = handle.GetComponent<Image>();
            handleImage.sprite = KvTheme.WhiteSprite;
            handleImage.color = KvTheme.ButtonBg;
            RectTransform handleRect = (RectTransform)handle.transform;
            handleRect.sizeDelta = new Vector2(12f, 18f);

            Text valueText = KvWidgets.Label(row, "Value", "", KvTheme.FontSizeSmall,
                KvTheme.TextMain, TextAnchor.MiddleRight, 84f);
            LayoutElement valueLe = valueText.gameObject.AddComponent<LayoutElement>();
            valueLe.minWidth = 84f;
            valueLe.preferredWidth = 84f;
            valueLe.minHeight = 18f;

            // ВАЖНО: тип слайдера указывается ПОЛНОСТЬЮ — иначе `Slider` внутри этого
            // класса разрешается в сам метод `KvTabKit.Slider`, а не в UnityEngine.UI.Slider.
            UnityEngine.UI.Slider slider = sliderGo.GetComponent<UnityEngine.UI.Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = false;

            string fmt = string.IsNullOrEmpty(format) ? "0.00" : format;
            slider.value = Mathf.Clamp(value, min, max);
            valueText.text = slider.value.ToString(fmt);
            dynamicRows.Add(new KeyValuePair<Text, Func<string>>(valueText,
                delegate { return slider != null ? slider.value.ToString(fmt) : ""; }));

            slider.onValueChanged.AddListener(delegate (float v)
            {
                valueText.text = v.ToString(fmt);
                if (onChanged != null) onChanged(v);
            });
            return slider;
        }

        /// <summary>Ряд кнопок. `labels` и `actions` — одинаковой длины.</summary>
        public void Buttons(string[] labels, Action[] actions)
        {
            if (labels == null || labels.Length == 0) return;
            RectTransform row = KvWidgets.CreateRow(content, "Buttons", 24f, 6f);
            LayoutElement rowLe = row.gameObject.AddComponent<LayoutElement>();
            rowLe.minHeight = 24f;
            rowLe.preferredHeight = 24f;

            for (int i = 0; i < labels.Length; i++)
            {
                Action action = actions != null && i < actions.Length ? actions[i] : null;
                Button button = KvTheme.CreateSmallButton(row, "Btn" + i, labels[i],
                    delegate { if (action != null) action(); });
                // ФИКС 6 (§22): ширина кнопки задаётся ЯВНО (и в LayoutElement, и в sizeDelta).
                // Строка KvWidgets.CreateRow управление размером детей НЕ включает, поэтому
                // одного LayoutElement было мало: кнопка брала дефолтный sizeDelta нового
                // RectTransform (100×100) и рисовалась огромным квадратом — на это и жаловался
                // оператор во вкладках «Сглаживание траекторий» и «Деревья поведения».
                float width = Mathf.Max(70f, labels[i].Length * 7.6f + 16f);
                LayoutElement le = button.gameObject.AddComponent<LayoutElement>();
                le.minHeight = 22f;
                le.preferredHeight = 22f;
                le.minWidth = width;
                le.preferredWidth = width;
                ((RectTransform)button.transform).sizeDelta = new Vector2(width, 22f);
            }
        }

        /// <summary>Сегментный выбор (радио-строка) — например метод сглаживания.</summary>
        public KvSegmented Segmented(string caption, string[] options, int index, Action<int> onChanged)
        {
            RectTransform row = KvWidgets.CreateRow(content, "SegRow", 22f, 6f);
            LayoutElement rowLe = row.gameObject.AddComponent<LayoutElement>();
            rowLe.minHeight = 22f;
            rowLe.preferredHeight = 22f;

            if (!string.IsNullOrEmpty(caption))
            {
                Text label = KvWidgets.Label(row, "Caption", caption, KvTheme.FontSizeSmall,
                    KvTheme.TextDim, TextAnchor.MiddleLeft, 190f);
                LayoutElement le = label.gameObject.AddComponent<LayoutElement>();
                le.minWidth = 190f;
                le.preferredWidth = 190f;
                le.minHeight = 20f;
            }

            KvSegmented seg = KvWidgets.Segmented(row, "Seg", options,
                Mathf.Clamp(index, 0, Mathf.Max(0, options.Length - 1)),
                delegate (int i) { if (onChanged != null) onChanged(i); }, 20f);
            LayoutElement segLe = seg.gameObject.GetComponent<LayoutElement>();
            if (segLe == null) segLe = seg.gameObject.AddComponent<LayoutElement>();
            segLe.minHeight = 20f;
            segLe.preferredHeight = 20f;
            return seg;
        }

        /// <summary>Разделитель между блоками.</summary>
        public void Divider()
        {
            Image div = KvTheme.CreateDivider(content, false);
            LayoutElement le = div.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 1f;
            le.preferredHeight = 1f;
        }
    }

    /// <summary>
    /// ОКНО-ВЕРСТАК ПОСТОБРАБОТКИ ТРАЕКТОРИЙ (этапы 4–6 ТЗ: сглаживание,
    /// время-оптимальная траектория, энергия). Собственный канвас, перетаскивание за
    /// заголовок, вкладки из реестра — ничего в существующей оболочке не меняется.
    /// </summary>
    public class KvWorkbenchWindow : MonoBehaviour
    {
        public int sortingOrder = 46;
        public float windowWidth = 560f;
        public float windowHeight = 430f;
        public float refreshInterval = 0.2f;

        private static readonly List<IKvWorkbenchTab> tabs = new List<IKvWorkbenchTab>();
        private static KvWorkbenchWindow instance;

        private Canvas canvas;
        private RectTransform canvasRect;
        private RectTransform root;
        private RectTransform content;
        private Text titleText;
        private KvSegmented tabStrip;
        private KvTabKit kit;
        private readonly List<Text> tabNotes = new List<Text>();
        private int activeTab;
        private bool visible;
        private float timer;

        public static KvWorkbenchWindow Instance { get { return instance; } }
        public bool Visible { get { return visible; } }
        public int ActiveTab { get { return activeTab; } }
        public static int TabCount { get { return tabs.Count; } }
        public string ActiveKey
        {
            get
            {
                if (tabs.Count == 0) return "";
                return tabs[Mathf.Clamp(activeTab, 0, tabs.Count - 1)].Key;
            }
        }

        /// <summary>Зарегистрировать вкладку (повторная регистрация с тем же ключом заменяет её).</summary>
        public static void RegisterTab(IKvWorkbenchTab tab)
        {
            if (tab == null || string.IsNullOrEmpty(tab.Key)) return;
            for (int i = 0; i < tabs.Count; i++)
            {
                if (tabs[i].Key == tab.Key)
                {
                    tabs[i] = tab;
                    if (instance != null) instance.RebuildTabs();
                    return;
                }
            }
            tabs.Add(tab);
            if (instance != null) instance.RebuildTabs();
        }

        public static IKvWorkbenchTab FindTab(string key)
        {
            for (int i = 0; i < tabs.Count; i++)
                if (tabs[i].Key == key) return tabs[i];
            return null;
        }

        public static KvWorkbenchWindow Create(Transform parent)
        {
            if (instance != null) return instance;
            GameObject go = new GameObject("KvWorkbenchWindow", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            instance = go.AddComponent<KvWorkbenchWindow>();
            instance.Build();
            return instance;
        }

        private void Build()
        {
            canvas = KvOverlayKit.CreateCanvas(transform, "Canvas", sortingOrder);
            canvasRect = (RectTransform)canvas.transform;

            GameObject rootGo = new GameObject("Window", typeof(Image));
            rootGo.transform.SetParent(canvasRect, false);
            root = (RectTransform)rootGo.transform;
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(windowWidth, windowHeight);
            root.anchoredPosition = new Vector2(0f, -30f);

            Image bg = rootGo.GetComponent<Image>();
            bg.sprite = KvTheme.WhiteSprite;
            bg.color = KvTheme.WindowBg;
            bg.raycastTarget = true;
            Outline outline = rootGo.AddComponent<Outline>();
            outline.effectColor = KvTheme.Border;
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = false;

            // --- заголовок (перетаскивание — тем же компонентом, что у панели функций)
            RectTransform header = KvWidgets.CreateRow(root, "Header", 24f, 6f);
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.sizeDelta = new Vector2(0f, 24f);
            header.anchoredPosition = Vector2.zero;

            Image headerBg = KvTheme.CreatePanel(header, "Bg", KvTheme.PanelHeader);
            KvTheme.Stretch(headerBg.rectTransform);
            headerBg.transform.SetAsFirstSibling();
            headerBg.raycastTarget = true;

            titleText = KvTheme.CreateText(header, "Title",
                KvLocExtra.T("wb.title", "Верстак: постобработка траекторий").ToUpperInvariant(),
                KvTheme.FontSizeSmall, TextAnchor.MiddleLeft, KvTheme.TextMain);
            KvTheme.Stretch(titleText.rectTransform, 8f, 30f);
            LayoutElement titleLe = titleText.gameObject.AddComponent<LayoutElement>();
            titleLe.flexibleWidth = 1f;
            titleLe.minWidth = 200f;

            KvFeatureDrag drag = headerBg.gameObject.AddComponent<KvFeatureDrag>();
            drag.Target = root;

            Button close = KvTheme.CreateSmallButton(header, "Close", "✕", delegate { Hide(); });
            KvWidgets.Fit(close.gameObject, 24f, 20f);

            // --- вкладки
            // ФИКС 6 (§23): высота полосы вкладок — эталон KvWidgets.TabHeight (26 px, в
            // требуемом диапазоне 24…28), сегменты ниже её на 2 px. Раньше было 22/20.
            RectTransform tabBar = KvWidgets.CreateRow(root, "Tabs", KvWidgets.TabHeight, 4f);
            tabBar.anchorMin = new Vector2(0f, 1f);
            tabBar.anchorMax = new Vector2(1f, 1f);
            tabBar.pivot = new Vector2(0.5f, 1f);
            tabBar.sizeDelta = new Vector2(0f, KvWidgets.TabHeight);
            tabBar.anchoredPosition = new Vector2(0f, -25f);

            // --- содержимое
            GameObject contentGo = new GameObject("Content", typeof(RectTransform),
                typeof(VerticalLayoutGroup));
            contentGo.transform.SetParent(root, false);
            content = (RectTransform)contentGo.transform;
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(8f, 8f);
            content.offsetMax = new Vector2(-8f, -53f);   // ФИКС 6: полоса вкладок стала 26 px
            VerticalLayoutGroup vlg = contentGo.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 3f;
            vlg.padding = new RectOffset(4, 4, 4, 4);
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;

            kit = new KvTabKit(content);
            root.gameObject.SetActive(false);
            RebuildTabs();
        }

        /// <summary>Пересобрать полосу вкладок (после регистрации новой вкладки).</summary>
        public void RebuildTabs()
        {
            if (root == null) return;
            if (tabStrip != null) Destroy(tabStrip.gameObject);

            RectTransform tabBar = (RectTransform)root.Find("Tabs");
            if (tabs.Count == 0)
            {
                tabStrip = null;
                BuildContent();
                return;
            }

            string[] titles = new string[tabs.Count];
            for (int i = 0; i < tabs.Count; i++) titles[i] = tabs[i].Title;
            activeTab = Mathf.Clamp(activeTab, 0, tabs.Count - 1);
            tabStrip = KvWidgets.Segmented(tabBar, "WorkbenchTabs", titles, activeTab,
                delegate (int index)
                {
                    activeTab = index;
                    BuildContent();
                }, KvWidgets.TabHeight - 2f);
        }

        private void BuildContent()
        {
            if (content == null) return;
            for (int i = content.childCount - 1; i >= 0; i--)
                Destroy(content.GetChild(i).gameObject);
            kit.dynamicRows.Clear();

            if (tabs.Count == 0 || activeTab < 0 || activeTab >= tabs.Count) return;
            try
            {
                tabs[activeTab].Build(kit);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Workbench] вкладка «" + tabs[activeTab].Key + "» не собрана: " + e.Message);
            }
            if (titleText != null)
            {
                titleText.text = (KvLocExtra.T("wb.title", "Верстак") + " · " + tabs[activeTab].Title)
                    .ToUpperInvariant() + "   (F2)";
            }
            Refresh();
        }

        public void Show(int index)
        {
            if (root == null) return;
            if (index >= 0 && index < tabs.Count && index != activeTab)
            {
                activeTab = index;
                if (tabStrip != null) tabStrip.Set(activeTab);
                BuildContent();
            }
            else if (!root.gameObject.activeSelf)
            {
                BuildContent();
            }
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
            visible = true;
        }

        public void Show(string key)
        {
            for (int i = 0; i < tabs.Count; i++)
                if (tabs[i].Key == key) { Show(i); return; }
            Show(-1);
        }

        public void Hide()
        {
            if (root != null) root.gameObject.SetActive(false);
            visible = false;
            KvTooltip.Release();
        }

        public void Toggle()
        {
            if (visible) Hide();
            else Show(-1);
        }

        /// <summary>Перерисовать содержимое активной вкладки (после внешних изменений).</summary>
        public void RebuildContent()
        {
            BuildContent();
        }

        private void Update()
        {
            if (!visible) return;
            for (int i = 0; i < tabs.Count; i++) tabs[i].Tick();

            timer -= Time.unscaledDeltaTime;
            if (timer > 0f) return;
            timer = Mathf.Max(0.05f, refreshInterval);

            Refresh();
            for (int i = 0; i < tabs.Count; i++) tabs[i].Refresh();
        }

        private void Refresh()
        {
            for (int i = 0; i < kit.dynamicRows.Count; i++)
            {
                KeyValuePair<Text, Func<string>> row = kit.dynamicRows[i];
                if (row.Key == null) continue;
                string text = "";
                try { text = row.Value != null ? row.Value() : ""; }
                catch (Exception) { }
                if (row.Key.text != text) row.Key.text = text;
            }
        }
    }
}
