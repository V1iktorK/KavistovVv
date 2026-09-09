using UnityEngine;
using UnityEngine.UI;

namespace KompasUI
{
    /// <summary>
    /// Цветовая тема и фабрика UI-элементов в стиле КОМПАС-3D:
    /// плотные панели, тёмно-серая гамма, тонкие рамки, мелкие кнопки.
    /// </summary>
    public static class KompasTheme
    {
        // Палитра (тёмная «инженерная» тема)
        public static readonly Color PanelBg = new Color(0.16f, 0.17f, 0.19f, 0.97f);
        public static readonly Color PanelHeader = new Color(0.21f, 0.22f, 0.25f, 1f);
        public static readonly Color ButtonBg = new Color(0.26f, 0.27f, 0.30f, 1f);
        public static readonly Color ButtonHover = new Color(0.33f, 0.35f, 0.39f, 1f);
        public static readonly Color ButtonActive = new Color(0.12f, 0.42f, 0.78f, 1f);
        public static readonly Color Accent = new Color(0.35f, 0.62f, 0.95f, 1f);
        public static readonly Color TextMain = new Color(0.92f, 0.93f, 0.95f, 1f);
        public static readonly Color TextDim = new Color(0.62f, 0.64f, 0.68f, 1f);
        public static readonly Color Border = new Color(0.09f, 0.10f, 0.11f, 1f);

        public const int FontSize = 14;
        public const int FontSizeSmall = 12;

        public static Font Font
        {
            get
            {
                Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return f != null ? f : Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
        }

        public static Image CreatePanel(RectTransform parent, string name, Color color)
        {
            GameObject go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            Image img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        public static Text CreateText(RectTransform parent, string name, string content, int size, TextAnchor anchor, Color color)
        {
            GameObject go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);
            Text t = go.GetComponent<Text>();
            t.font = Font;
            t.fontSize = size;
            t.text = content;
            t.alignment = anchor;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>Плотная кнопка (Image+Button+Text) в стиле КОМПАС.</summary>
        public static Button CreateButton(RectTransform parent, string name, string label,
            System.Action onClick, int height = 26, bool small = false)
        {
            GameObject go = new GameObject(name, typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Image img = go.GetComponent<Image>();
            img.color = ButtonBg;
            img.sprite = UIFactory.GetSprite();

            Button btn = go.GetComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            cb.pressedColor = new Color(0.55f, 0.55f, 0.55f, 1f);
            cb.selectedColor = Color.white;
            btn.colors = cb;

            Text txt = CreateText((RectTransform)go.transform, "Label", label,
                small ? FontSizeSmall : FontSize, TextAnchor.MiddleCenter, TextMain);
            Stretch(txt.rectTransform, 4, 2);

            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }

        /// <summary>Кнопка-«треугольник» для сворачивания ветки дерева.</summary>
        public static Button CreateSmallButton(RectTransform parent, string name, string label,
            System.Action onClick)
        {
            return CreateButton(parent, name, label, onClick, 20, small: true);
        }

        /// <summary>Горизонтальная тонкая линия-разделитель.</summary>
        public static Image CreateDivider(RectTransform parent, bool vertical = false)
        {
            Image div = CreatePanel(parent, "Divider", Border);
            if (vertical)
            {
                div.rectTransform.sizeDelta = new Vector2(1f, 0f);
            }
            else
            {
                div.rectTransform.sizeDelta = new Vector2(0f, 1f);
            }
            return div;
        }

        public static void Stretch(RectTransform rt, float padL = 0, float padR = 0, float padT = 0, float padB = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padL, padB);
            rt.offsetMax = new Vector2(-padR, -padT);
        }
    }

    /// <summary>Разовые фабричные хелперы (спрайт и пр.).</summary>
    public static class UIFactory
    {
        private static Sprite _sprite;

        /// <summary>Белая текстура-спрайт для UI (без файлов ассетов).</summary>
        public static Sprite GetSprite()
        {
            if (_sprite != null) return _sprite;
            Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            Color[] px = new Color[16];
            for (int i = 0; i < px.Length; i++) px[i] = Color.white;
            tex.SetPixels(px);
            tex.Apply();
            _sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            return _sprite;
        }
    }
}
