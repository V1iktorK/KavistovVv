using UnityEngine;
using UnityEngine.UI;

namespace KazistovVvUI
{
    /// <summary>
    /// Нижняя панель — СТАТУС-БАР в стиле FreeCAD: слева состояние State Machine,
    /// выбранный робот и координаты луча; в центре — статусные сообщения (те же,
    /// что идут в лог/подсказки потока); справа — индикатор темы и FPS.
    /// Обновление текста — по вызову менеджера (10 раз в секунду), поэтому панель
    /// не влияет на производительность.
    /// </summary>
    public class KvStatusBar : MonoBehaviour
    {
        /// <summary>Высота статус-бара.</summary>
        public const float Height = 22f;

        private RectTransform root;
        private Image bg;
        private Text stateText;
        private Text robotText;
        private Text cursorText;
        private Text messageText;
        private Text themeText;
        private Text fpsText;
        private Text hintText;

        /// <summary>Собрать статус-бар. Возвращает высоту.</summary>
        public float Build(RectTransform canvas, Canvas owner)
        {
            GameObject go = new GameObject("KvStatusBar", typeof(Image));
            go.transform.SetParent(canvas, false);
            root = (RectTransform)go.transform;
            root.anchorMin = new Vector2(0f, 0f);
            root.anchorMax = new Vector2(1f, 0f);
            root.pivot = new Vector2(0.5f, 0f);
            root.sizeDelta = new Vector2(0f, Height);
            root.anchoredPosition = Vector2.zero;
            bg = go.GetComponent<Image>();
            bg.sprite = KvTheme.WhiteSprite;
            bg.color = KvTheme.PanelHeader;
            bg.raycastTarget = true;

            stateText = AddField("State", 6f, 158f, TextAnchor.MiddleLeft, KvTheme.TextMain);
            AddSeparator(166f);
            robotText = AddField("Robot", 174f, 132f, TextAnchor.MiddleLeft, KvTheme.TextDim);
            AddSeparator(308f);
            cursorText = AddField("Cursor", 316f, 250f, TextAnchor.MiddleLeft, KvTheme.TextDim);
            AddSeparator(568f);

            messageText = AddField("Message", 576f, 0f, TextAnchor.MiddleLeft, KvTheme.TextDim);
            messageText.rectTransform.anchorMax = new Vector2(1f, 1f);
            messageText.rectTransform.offsetMax = new Vector2(-232f, 0f);

            // Правая группа: тема и FPS (как индикаторы рабочей среды).
            fpsText = AddRightField("Fps", -6f, 58f, TextAnchor.MiddleRight, KvTheme.TextDim);
            themeText = AddRightField("Theme", -68f, 158f, TextAnchor.MiddleRight, KvTheme.TextDim);
            hintText = AddRightField("Hint", -232f, 158f, TextAnchor.MiddleRight, KvTheme.TextDisabled);

            SetTheme(KvTheme.ModeLabel);
            return Height;
        }

        private Text AddField(string name, float x, float width, TextAnchor anchor, Color color)
        {
            Text t = KvTheme.CreateText(root, name, "", KvTheme.FontSizeSmall, anchor, color);
            t.rectTransform.anchorMin = new Vector2(0f, 0f);
            t.rectTransform.anchorMax = new Vector2(0f, 1f);
            t.rectTransform.pivot = new Vector2(0f, 0.5f);
            t.rectTransform.sizeDelta = new Vector2(width, 0f);
            t.rectTransform.anchoredPosition = new Vector2(x, 0f);
            return t;
        }

        private Text AddRightField(string name, float x, float width, TextAnchor anchor, Color color)
        {
            Text t = KvTheme.CreateText(root, name, "", KvTheme.FontSizeSmall, anchor, color);
            t.rectTransform.anchorMin = new Vector2(1f, 0f);
            t.rectTransform.anchorMax = new Vector2(1f, 1f);
            t.rectTransform.pivot = new Vector2(1f, 0.5f);
            t.rectTransform.sizeDelta = new Vector2(width, 0f);
            t.rectTransform.anchoredPosition = new Vector2(x, 0f);
            return t;
        }

        private void AddSeparator(float x)
        {
            Image sep = KvTheme.CreatePanel(root, "Sep", KvTheme.Separator);
            sep.rectTransform.anchorMin = new Vector2(0f, 0f);
            sep.rectTransform.anchorMax = new Vector2(0f, 1f);
            sep.rectTransform.pivot = new Vector2(0f, 0.5f);
            sep.rectTransform.sizeDelta = new Vector2(1f, -8f);
            sep.rectTransform.anchoredPosition = new Vector2(x, 0f);
        }

        // ------------------------------------------------------------------ значения

        /// <summary>Состояние State Machine (Idle / PointSelected / …).</summary>
        public void SetState(string text, Color color)
        {
            if (stateText == null) return;
            stateText.text = text;
            stateText.color = color;
        }

        /// <summary>Выбранный/активный робот.</summary>
        public void SetRobot(string text)
        {
            if (robotText != null) robotText.text = text;
        }

        /// <summary>Координаты курсора/луча.</summary>
        public void SetCursor(string text)
        {
            if (cursorText != null) cursorText.text = text;
        }

        /// <summary>Статусное сообщение (то же, что в логе потока).</summary>
        public void SetMessage(string text, Color color)
        {
            if (messageText == null) return;
            messageText.text = text;
            messageText.color = color;
        }

        /// <summary>Индикатор темы («Тёмная», «Светлая», «Системная»).</summary>
        public void SetTheme(string text)
        {
            if (themeText != null) themeText.text = KvLoc.T("status.theme", "Тема") + ": " + text;
        }

        /// <summary>FPS-счётчик (проверка «UI не влияет на FPS»).</summary>
        public void SetFps(float fps)
        {
            if (fpsText != null) fpsText.text = Mathf.RoundToInt(fps) + " FPS";
        }

        /// <summary>Служебная подсказка справа (TAB, режим UI и т.п.).</summary>
        public void SetHint(string text)
        {
            if (hintText != null) hintText.text = text;
        }

        /// <summary>Корень панели.</summary>
        public RectTransform Root { get { return root; } }

        /// <summary>Текущий текст состояния (диагностика).</summary>
        public string StateText { get { return stateText != null ? stateText.text : ""; } }
        /// <summary>Текущий текст сообщения (диагностика).</summary>
        public string MessageText { get { return messageText != null ? messageText.text : ""; } }
        /// <summary>Текущий текст координат (диагностика).</summary>
        public string CursorText { get { return cursorText != null ? cursorText.text : ""; } }
        /// <summary>Текущий текст робота (диагностика).</summary>
        public string RobotText { get { return robotText != null ? robotText.text : ""; } }
        /// <summary>Текущий текст темы (диагностика).</summary>
        public string ThemeText { get { return themeText != null ? themeText.text : ""; } }

        /// <summary>Перекрасить статус-бар под текущую тему.</summary>
        public void Repaint()
        {
            if (bg != null) bg.color = KvTheme.PanelHeader;
            if (robotText != null) robotText.color = KvTheme.TextDim;
            if (cursorText != null) cursorText.color = KvTheme.TextDim;
            if (fpsText != null) fpsText.color = KvTheme.TextDim;
            if (themeText != null) themeText.color = KvTheme.TextDim;
            if (hintText != null) hintText.color = KvTheme.TextDisabled;
        }
    }
}
