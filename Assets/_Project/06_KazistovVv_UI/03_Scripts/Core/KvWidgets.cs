using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KazistovVvUI
{
    /// <summary>
    /// Виджеты десктопного интерфейса в стиле FreeCAD: кнопки-иконки, разделители,
    /// заголовки секций, строки «свойство = значение», переключатели и сегментные
    /// переключатели (радио-строка). Всё создаётся кодом — файлов-ассетов нет.
    /// </summary>
    public static class KvWidgets
    {
        // ------------------------------------------------------------------ контейнеры

        public static RectTransform CreateRow(RectTransform parent, string name, float height,
            float spacing = 4f, TextAnchor align = TextAnchor.MiddleLeft, bool expandHeight = false)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            HorizontalLayoutGroup h = go.GetComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = align;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = expandHeight;
            h.childControlWidth = false;
            h.childControlHeight = false;
            if (height > 0f)
            {
                LayoutElement le = go.AddComponent<LayoutElement>();
                le.minHeight = height;
                le.preferredHeight = height;
            }
            return rt;
        }

        public static RectTransform CreateColumn(RectTransform parent, string name, float height,
            float spacing = 2f)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            VerticalLayoutGroup v = go.GetComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.childAlignment = TextAnchor.UpperLeft;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            v.childControlWidth = true;
            v.childControlHeight = false;
            if (height > 0f)
            {
                LayoutElement le = go.AddComponent<LayoutElement>();
                le.minHeight = height;
                le.preferredHeight = height;
            }
            return rt;
        }

        public static LayoutElement Fit(GameObject go, float width, float height)
        {
            LayoutElement le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            if (width > 0f) { le.minWidth = width; le.preferredWidth = width; }
            if (height > 0f) { le.minHeight = height; le.preferredHeight = height; }
            return le;
        }

        // ------------------------------------------------------------------ текст

        public static Text Label(RectTransform parent, string name, string text, int size,
            Color color, TextAnchor anchor = TextAnchor.MiddleLeft, float width = 0f)
        {
            Text t = KvTheme.CreateText(parent, name, text, size, anchor, color);
            if (width > 0f) Fit(t.gameObject, width, 0f);
            return t;
        }

        /// <summary>Заголовок секции панели (как «Tasks»/«Properties» в FreeCAD).</summary>
        public static RectTransform SectionHeader(RectTransform parent, string title, float height = 20f)
        {
            RectTransform row = CreateRow(parent, "Header_" + title, height, 6f);
            Image bg = KvTheme.CreatePanel(row, "Bg", KvTheme.PanelHeader);
            KvTheme.Stretch(bg.rectTransform);
            bg.transform.SetAsFirstSibling();
            LayoutElement le = row.gameObject.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;

            Text t = KvTheme.CreateText(row, "Title", title.ToUpperInvariant(), KvTheme.FontSizeSmall,
                TextAnchor.MiddleLeft, KvTheme.TextDim);
            Fit(t.gameObject, 0f, height);
            t.rectTransform.offsetMin = new Vector2(6f, 0f);
            return row;
        }

        // ------------------------------------------------------------------ кнопка-иконка

        /// <summary>
        /// Кнопка ТОЛЬКО с иконкой (без подписи) — основной элемент тулбара FreeCAD.
        /// </summary>
        public static KvIconButton IconButton(RectTransform parent, string name, string iconId,
            float size, Action onClick)
        {
            GameObject go = new GameObject(name, typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            Image bg = go.GetComponent<Image>();
            bg.sprite = KvTheme.WhiteSprite;
            bg.color = KvTheme.ButtonBg;
            bg.raycastTarget = true;

            LayoutElement le = go.GetComponent<LayoutElement>();
            le.minWidth = size;
            le.minHeight = size;
            le.preferredWidth = size;
            le.preferredHeight = size;

            Button btn = go.GetComponent<Button>();
            btn.targetGraphic = bg;
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = KvTheme.Ratio(KvTheme.ButtonHover, KvTheme.ButtonBg);
            cb.pressedColor = KvTheme.Ratio(KvTheme.ButtonPressed, KvTheme.ButtonBg);
            cb.selectedColor = Color.white;
            cb.disabledColor = new Color(1f, 1f, 1f, 0.5f);
            cb.fadeDuration = 0.05f;
            btn.colors = cb;

            GameObject iconGo = new GameObject("Icon", typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            Image icon = iconGo.GetComponent<Image>();
            icon.sprite = KvIcons.Get(iconId, Mathf.RoundToInt(size * 0.78f));
            icon.color = KvTheme.IconTint;
            icon.raycastTarget = false;
            RectTransform ir = icon.rectTransform;
            ir.anchorMin = ir.anchorMax = new Vector2(0.5f, 0.5f);
            ir.pivot = new Vector2(0.5f, 0.5f);
            float iconSize = size * 0.78f;
            ir.sizeDelta = new Vector2(iconSize, iconSize);
            ir.anchoredPosition = Vector2.zero;
            if (icon.sprite == null) iconGo.SetActive(false);

            KvIconButton view = go.AddComponent<KvIconButton>();
            view.Bind(btn, bg, icon, iconId);
            if (onClick != null) btn.onClick.AddListener(delegate { onClick(); });
            return view;
        }

        /// <summary>Вертикальный разделитель тулбара.</summary>
        public static Image ToolbarSeparator(RectTransform parent)
        {
            Image sep = KvTheme.CreatePanel(parent, "Sep", KvTheme.Separator);
            LayoutElement le = sep.gameObject.AddComponent<LayoutElement>();
            le.minWidth = 1f;
            le.preferredWidth = 1f;
            return sep;
        }

        // ------------------------------------------------------------------ строка «свойство»

        /// <summary>Строка свойств: слева подпись, справа ЗНАЧЕНИЕ (только чтение).</summary>
        public static Text PropertyRow(RectTransform parent, string label, string value,
            float height = 17f, float labelWidth = 116f)
        {
            RectTransform row = CreateRow(parent, "Prop_" + label, height, 4f);
            Text l = KvTheme.CreateText(row, "Label", label, KvTheme.FontSizeSmall,
                TextAnchor.MiddleLeft, KvTheme.TextDim);
            Fit(l.gameObject, labelWidth, height);
            l.horizontalOverflow = HorizontalWrapMode.Wrap;

            Text v = KvTheme.CreateText(row, "Value", value, KvTheme.FontSizeSmall,
                TextAnchor.MiddleRight, KvTheme.TextMain);
            v.horizontalOverflow = HorizontalWrapMode.Wrap;
            LayoutElement le = v.gameObject.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
            le.flexibleWidth = 1f;
            return v;
        }

        // ------------------------------------------------------------------ переключатель

        /// <summary>
        /// Галка-переключатель в стиле FreeCAD (квадрат с «✓» + подпись).
        /// Кликабельна ВСЯ строка: Button живёт на корне строки, возвращаемый
        /// <see cref="KvSwitch"/> — на том же объекте (удобно навешивать колбэки).
        /// </summary>
        public static KvSwitch Switch(RectTransform parent, string label, bool initial, Action<bool> onChanged)
        {
            GameObject rowGo = new GameObject("Switch_" + label, typeof(Image), typeof(Button),
                typeof(LayoutElement));
            rowGo.transform.SetParent(parent, false);
            RectTransform row = (RectTransform)rowGo.transform;

            Image bg = rowGo.GetComponent<Image>();
            bg.sprite = KvTheme.WhiteSprite;
            bg.color = new Color(0f, 0f, 0f, 0.16f);
            bg.raycastTarget = true;

            HorizontalLayoutGroup h = rowGo.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 6f;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            h.childControlWidth = false;
            h.childControlHeight = false;
            h.padding = new RectOffset(3, 3, 1, 1);

            LayoutElement rle = rowGo.GetComponent<LayoutElement>();
            rle.minHeight = 18f;
            rle.preferredHeight = 18f;

            Button rowButton = rowGo.GetComponent<Button>();
            rowButton.targetGraphic = bg;
            ColorBlock cb = rowButton.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = KvTheme.Ratio(KvTheme.ButtonHover, KvTheme.PanelDark);
            cb.pressedColor = KvTheme.Ratio(KvTheme.ButtonPressed, KvTheme.PanelDark);
            cb.disabledColor = Color.white;
            cb.fadeDuration = 0.05f;
            rowButton.colors = cb;

            GameObject boxGo = new GameObject("Box", typeof(Image), typeof(LayoutElement));
            boxGo.transform.SetParent(rowGo.transform, false);
            Image box = boxGo.GetComponent<Image>();
            box.sprite = KvTheme.WhiteSprite;
            box.color = initial ? KvTheme.ButtonChecked : KvTheme.ButtonBg;
            box.raycastTarget = false;
            Fit(boxGo, 14f, 14f);

            GameObject checkGo = new GameObject("Check", typeof(Image));
            checkGo.transform.SetParent(boxGo.transform, false);
            Image check = checkGo.GetComponent<Image>();
            check.sprite = KvIcons.Get("check", 11);
            check.color = KvTheme.IsLight ? Color.white : KvTheme.TextMain;
            check.raycastTarget = false;
            KvTheme.Stretch(check.rectTransform, 1f, 1f, 1f, 1f);
            checkGo.SetActive(initial);

            Text labelText = KvTheme.CreateText(row, "Label", label, KvTheme.FontSizeSmall,
                TextAnchor.MiddleLeft, KvTheme.TextMain);
            LayoutElement le = labelText.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.minHeight = 16f;

            KvSwitch view = rowGo.AddComponent<KvSwitch>();
            view.Bind(box, checkGo, initial);
            rowButton.onClick.AddListener(delegate
            {
                if (view.Locked) return;
                view.Set(!view.Value);
                onChanged?.Invoke(view.Value);
            });
            return view;
        }

        // ------------------------------------------------------------------ сегментный выбор

        /// <summary>Сегментный переключатель (Тёмная | Светлая | Системная).</summary>
        public static KvSegmented Segmented(RectTransform parent, string name, string[] options,
            int initial, Action<int> onChanged, float height = 20f)
        {
            RectTransform row = CreateRow(parent, "Seg_" + name, height, 2f);
            Image bg = KvTheme.CreatePanel(row, "Bg", KvTheme.ButtonPressed);
            KvTheme.Stretch(bg.rectTransform);
            bg.transform.SetAsFirstSibling();

            KvSegmented view = row.gameObject.AddComponent<KvSegmented>();
            List<Button> buttons = new List<Button>();
            List<Image> images = new List<Image>();

            for (int i = 0; i < options.Length; i++)
            {
                int index = i;
                Button b = KvTheme.CreateButton(row, "Seg" + i, options[i], null,
                    Mathf.RoundToInt(height), true);
                Fit(b.gameObject, 0f, height);
                LayoutElement le = b.GetComponent<LayoutElement>();
                le.flexibleWidth = 1f;
                images.Add(b.GetComponent<Image>());
                buttons.Add(b);
                b.onClick.AddListener(delegate
                {
                    view.Set(index);
                    onChanged?.Invoke(index);
                });
            }

            view.Bind(buttons, images, initial);
            return view;
        }

        /// <summary>Информационная строка (подсказка/пометка) в панели настроек.</summary>
        public static Text Note(RectTransform parent, string text, Color color, float height = 0f)
        {
            Text t = KvTheme.CreateText(parent, "Note", text, KvTheme.FontSizeSmall,
                TextAnchor.UpperLeft, color);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            if (height > 0f) Fit(t.gameObject, 0f, height);
            return t;
        }
    }

    /// <summary>Кнопка-иконка: состояние (обычная/включена/недоступна) и подсказка.</summary>
    public class KvIconButton : MonoBehaviour
    {
        public Button Button { get; private set; }
        public Image Background { get; private set; }
        public Image Icon { get; private set; }
        public string IconId { get; private set; }
        public KvTooltipTarget Tooltip { get; private set; }

        private bool enabledState = true;

        internal void Bind(Button button, Image background, Image icon, string iconId)
        {
            Button = button;
            Background = background;
            Icon = icon;
            IconId = iconId;
            Tooltip = gameObject.GetComponent<KvTooltipTarget>();
            if (Tooltip == null) Tooltip = gameObject.AddComponent<KvTooltipTarget>();
        }

        /// <summary>Сменить иконку (например, play ⟷ pause, тема).</summary>
        public void SetIcon(string iconId)
        {
            if (IconId == iconId) return;
            IconId = iconId;
            if (Icon == null) return;
            Sprite sp = KvIcons.Get(iconId, Mathf.RoundToInt(Icon.rectTransform.sizeDelta.x));
            Icon.sprite = sp;
            Icon.gameObject.SetActive(sp != null);
        }

        /// <summary>Подсветить как «включено» (тумблер) и задать оттенок иконки.</summary>
        public void SetChecked(bool value, Color? iconTint = null)
        {
            if (Background != null)
                Background.color = value ? KvTheme.ButtonChecked : KvTheme.ButtonBg;
            if (Icon != null)
                Icon.color = iconTint.HasValue ? iconTint.Value
                    : (value && !KvTheme.IsLight ? Color.white : KvTheme.IconTint);
        }

        /// <summary>Доступна ли кнопка (недоступные — «заглушки»).</summary>
        public void SetEnabled(bool value)
        {
            enabledState = value;
            if (Button != null) Button.interactable = value;
            if (Icon != null)
                Icon.color = value ? KvTheme.IconTint : KvTheme.TextDisabled;
            if (Background != null && !value) Background.color = KvTheme.PanelDark;
        }

        /// <summary>Пометка «в разработке» для подсказки.</summary>
        public void SetStub(bool stub)
        {
            if (Tooltip != null) Tooltip.stub = stub;
        }

        public bool IsEnabled { get { return enabledState; } }
    }

    /// <summary>Переключатель (галка) панели настроек.</summary>
    public class KvSwitch : MonoBehaviour
    {
        public bool Value { get; private set; }

        /// <summary>Заблокирован (нет обработчика — пункт «в разработке»).</summary>
        public bool Locked { get; set; }

        private Image box;
        private GameObject check;

        internal void Bind(Image boxImage, GameObject checkMark, bool initial)
        {
            box = boxImage;
            check = checkMark;
            Value = initial;
            Paint();
        }

        public void Set(bool value)
        {
            Value = value;
            Paint();
        }

        private void Paint()
        {
            if (box != null) box.color = Value ? KvTheme.ButtonChecked : KvTheme.ButtonBg;
            if (check != null) check.SetActive(Value);
        }
    }

    /// <summary>Сегментный переключатель (используется для темы и масштаба).</summary>
    public class KvSegmented : MonoBehaviour
    {
        public int Index { get; private set; }

        private List<Button> buttons;
        private List<Image> images;

        internal void Bind(List<Button> btns, List<Image> imgs, int initial)
        {
            buttons = btns;
            images = imgs;
            Index = initial;
            Paint();
        }

        public void Set(int index)
        {
            Index = index;
            Paint();
        }

        private void Paint()
        {
            if (images == null) return;
            for (int i = 0; i < images.Count; i++)
            {
                if (images[i] == null) continue;
                images[i].color = i == Index ? KvTheme.ButtonChecked : KvTheme.ButtonBg;
                Text t = images[i].GetComponentInChildren<Text>();
                if (t != null)
                    t.color = i == Index && !KvTheme.IsLight ? Color.white : KvTheme.TextMain;
            }
        }
    }
}
