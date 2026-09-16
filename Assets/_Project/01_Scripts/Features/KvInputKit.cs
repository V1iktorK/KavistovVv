using System;
using UnityEngine;
using UnityEngine.UI;
using KazistovVvUI;

namespace KazistovVvFeatures
{
    /// <summary>
    /// ПОЛЯ ВВОДА ТЕКСТА для новых вкладок (этапы 23, 26, 27, 32, 36 ТЗ).
    ///
    /// Во встроенном наборе виджетов (`KvWidgets`) поля ввода не было — до этих этапов
    /// интерфейс был чисто «кнопочным». Чтобы не менять общий стиль, поле рисуется теми же
    /// материалами и шрифтом `KvTheme`: фон `InputBg`, рамка отсутствует, текст `TextMain`,
    /// подсказка `TextDim`. Возвращается штатный `UnityEngine.UI.InputField`, поэтому в
    /// вызывающем коде можно читать `field.text` в любой момент.
    /// </summary>
    public static class KvInputKit
    {
        /// <summary>Однострочное поле. `onSubmit` вызывается при нажатии Enter/уходе фокуса.</summary>
        public static InputField Single(RectTransform parent, string caption, string value,
            string hint, Action<string> onSubmit)
        {
            return Build(parent, caption, value, hint, 22f, false, onSubmit);
        }

        /// <summary>Многострочное поле (скрипты, титры, списки команд).</summary>
        public static InputField Multi(RectTransform parent, string caption, string value,
            string hint, float height, Action<string> onSubmit)
        {
            return Build(parent, caption, value, hint, Mathf.Max(48f, height), true, onSubmit);
        }

        private static InputField Build(RectTransform parent, string caption, string value,
            string hint, float height, bool multiline, Action<string> onSubmit)
        {
            RectTransform row = KvWidgets.CreateRow(parent, "InputRow", height, 6f);
            LayoutElement rowLe = row.gameObject.AddComponent<LayoutElement>();
            rowLe.minHeight = height;
            rowLe.preferredHeight = height;

            if (!string.IsNullOrEmpty(caption))
            {
                Text label = KvWidgets.Label(row, "Caption", caption, KvTheme.FontSizeSmall,
                    KvTheme.TextDim, TextAnchor.UpperLeft, 190f);
                LayoutElement labelLe = label.gameObject.AddComponent<LayoutElement>();
                labelLe.minWidth = 190f;
                labelLe.preferredWidth = 190f;
                labelLe.minHeight = height;
                labelLe.preferredHeight = height;
            }

            Image bg = KvTheme.CreatePanel(row, "InputBg", KvTheme.InputBg);
            LayoutElement bgLe = bg.gameObject.AddComponent<LayoutElement>();
            bgLe.flexibleWidth = 1f;
            bgLe.minWidth = 120f;
            bgLe.minHeight = height;
            bgLe.preferredHeight = height;

            InputField field = bg.gameObject.AddComponent<InputField>();
            field.targetGraphic = bg;
            field.lineType = multiline ? InputField.LineType.MultiLineNewline
                                       : InputField.LineType.SingleLine;
            field.characterLimit = multiline ? 20000 : 512;

            Text text = KvTheme.CreateText(bg.rectTransform, "Text", "", KvTheme.FontSizeSmall,
                multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft, KvTheme.TextMain);
            KvTheme.Stretch(text.rectTransform, 6f, 6f, 3f, 3f);
            text.supportRichText = false;
            text.horizontalOverflow = multiline ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;

            Text placeholder = KvTheme.CreateText(bg.rectTransform, "Hint",
                string.IsNullOrEmpty(hint) ? "" : hint, KvTheme.FontSizeSmall,
                multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft, KvTheme.TextDim);
            KvTheme.Stretch(placeholder.rectTransform, 6f, 6f, 3f, 3f);

            field.textComponent = text;
            field.placeholder = placeholder;
            field.text = string.IsNullOrEmpty(value) ? "" : value;
            if (onSubmit != null)
                field.onEndEdit.AddListener(delegate (string v) { onSubmit(v); });
            return field;
        }

        /// <summary>Переключатель-«радио» для набора вариантов одной строкой (для фильтров).</summary>
        public static KvSegmented Filter(RectTransform parent, string caption, string[] options,
            int index, Action<int> onChanged)
        {
            RectTransform row = KvWidgets.CreateRow(parent, "FilterRow", 22f, 6f);
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
            }
            return KvWidgets.Segmented(row, "Seg", options,
                Mathf.Clamp(index, 0, Mathf.Max(0, options.Length - 1)),
                delegate (int i) { if (onChanged != null) onChanged(i); }, 20f);
        }
    }
}
