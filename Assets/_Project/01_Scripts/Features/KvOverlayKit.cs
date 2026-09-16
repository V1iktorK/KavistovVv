using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using KazistovVvUI;

namespace KazistovVvFeatures
{
    /// <summary>
    /// ИНСТРУМЕНТЫ ЭКРАННЫХ СЛОЁВ НОВЫХ ФУНКЦИЙ (этапы 1–3 ТЗ): собственный канвас,
    /// всплывающие подсказки, пульсирующая рамка подсветки и карточка-подсказка
    /// (туториал / демонстрация).
    ///
    /// ПОЧЕМУ СВОИ СЛОИ, А НЕ ОБЩИЕ: стартовое меню показывается ДО/ВМЕСТО рабочей
    /// оболочки (она на это время скрыта), а подсказки туториала обязаны быть видны
    /// даже когда оболочка погашена (презентация, демонстрация). Общий слой подсказок
    /// живёт на канвасе оболочки и вместе с ним скрывается, поэтому здесь —
    /// самостоятельные канвасы с бо́льшим sortingOrder. Всё создаётся кодом, сцена
    /// не меняется, файлов-ассетов не добавляется.
    /// </summary>
    public static class KvOverlayKit
    {
        /// <summary>Создать отдельный экранный канвас (Screen Space Overlay) поверх оболочки.</summary>
        public static Canvas CreateCanvas(Transform parent, string name, int sortingOrder)
        {
            GameObject go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);

            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            float scale = Mathf.Clamp(KvSettings.UiScale, 0.6f, 1.6f);
            scaler.referenceResolution = new Vector2(1366f / scale, 768f / scale);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        /// <summary>Прямоугольник элемента на экране (все канвасы проекта — Screen Space Overlay).</summary>
        public static bool ScreenRect(RectTransform target, out Vector2 center, out Vector2 size)
        {
            center = Vector2.zero;
            size = Vector2.zero;
            if (target == null) return false;

            Vector3[] corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector3 bl = corners[0];
            Vector3 tr = corners[2];
            center = new Vector2((bl.x + tr.x) * 0.5f, (bl.y + tr.y) * 0.5f);
            size = new Vector2(Mathf.Abs(tr.x - bl.x), Mathf.Abs(tr.y - bl.y));
            return size.x > 0.5f && size.y > 0.5f;
        }

        /// <summary>Перевести точку экрана в локальные координаты канваса.</summary>
        public static Vector2 ToCanvas(RectTransform canvasRect, Vector2 screenPoint)
        {
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, null, out local);
            return local;
        }
    }

    /// <summary>Наведение на элемент: показывает всплывающую подсказку и (необязательно)
    /// отдаёт текст наружу — для строки-подсказки в стартовом меню.</summary>
    public class KvHoverHint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public string title = "";
        public string body = "";
        /// <summary>Внешний обработчик (title, body); null — только всплывающая подсказка.</summary>
        public Action<string, string> onHover;
        /// <summary>Общая всплывающая подсказка слоя (ставится при создании канваса).</summary>
        public KvMiniTip tip;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (tip != null) tip.Show(title, body, transform as RectTransform);
            if (onHover != null) onHover(title, body);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (tip != null) tip.Hide();
            if (onHover != null) onHover(null, null);
        }

        private void OnDisable()
        {
            if (tip != null) tip.Hide();
        }
    }

    /// <summary>
    /// ВСПЛЫВАЮЩАЯ ПОДСКАЗКА СВОЕГО СЛОЯ (в стиле FreeCAD): заголовок + пояснение,
    /// появляется рядом с элементом, целиком в пределах канваса.
    /// </summary>
    public class KvMiniTip : MonoBehaviour
    {
        public float maxWidth = 320f;

        private RectTransform canvasRect;
        private RectTransform root;
        private Image frame;
        private Text titleText;
        private Text bodyText;
        private Image line;

        public static KvMiniTip Create(RectTransform canvasRect)
        {
            GameObject go = new GameObject("KvMiniTip", typeof(RectTransform));
            go.transform.SetParent(canvasRect, false);
            KvMiniTip tip = go.AddComponent<KvMiniTip>();
            tip.Build(canvasRect);
            return tip;
        }

        private void Build(RectTransform canvas)
        {
            canvasRect = canvas;
            root = (RectTransform)transform;
            root.anchorMin = root.anchorMax = new Vector2(0f, 0f);
            root.pivot = new Vector2(0f, 1f);
            root.sizeDelta = new Vector2(280f, 52f);

            frame = KvTheme.CreatePanel(root, "Frame", KvTheme.PanelHeader);
            KvTheme.Stretch(frame.rectTransform);
            Outline o = frame.gameObject.AddComponent<Outline>();
            o.effectColor = KvTheme.Border;
            o.effectDistance = new Vector2(1f, -1f);
            o.useGraphicAlpha = false;

            line = KvTheme.CreatePanel(root, "Accent", KvTheme.Accent);
            line.rectTransform.anchorMin = new Vector2(0f, 0f);
            line.rectTransform.anchorMax = new Vector2(0f, 1f);
            line.rectTransform.pivot = new Vector2(0f, 0.5f);
            line.rectTransform.sizeDelta = new Vector2(3f, 0f);
            line.rectTransform.anchoredPosition = Vector2.zero;

            titleText = KvTheme.CreateText(root, "Title", "", KvTheme.FontSize,
                TextAnchor.UpperLeft, KvTheme.TextMain);
            titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            titleText.rectTransform.pivot = new Vector2(0f, 1f);
            titleText.rectTransform.offsetMin = new Vector2(10f, 0f);
            titleText.rectTransform.offsetMax = new Vector2(-8f, 0f);
            titleText.rectTransform.sizeDelta = new Vector2(-18f, 18f);
            titleText.rectTransform.anchoredPosition = new Vector2(0f, -5f);

            bodyText = KvTheme.CreateText(root, "Body", "", KvTheme.FontSizeSmall,
                TextAnchor.UpperLeft, KvTheme.TextDim);
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyText.rectTransform.anchorMin = new Vector2(0f, 1f);
            bodyText.rectTransform.anchorMax = new Vector2(1f, 1f);
            bodyText.rectTransform.pivot = new Vector2(0f, 1f);
            bodyText.rectTransform.offsetMin = new Vector2(10f, 0f);
            bodyText.rectTransform.offsetMax = new Vector2(-8f, 0f);
            bodyText.rectTransform.sizeDelta = new Vector2(-18f, 20f);
            bodyText.rectTransform.anchoredPosition = new Vector2(0f, -25f);

            root.SetAsLastSibling();
            Hide();
        }

        public bool Visible { get { return root != null && root.gameObject.activeSelf; } }

        public void Show(string title, string body, RectTransform anchor)
        {
            if (root == null) return;
            if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(body)) { Hide(); return; }

            titleText.text = title;
            bodyText.text = body;
            bool hasBody = !string.IsNullOrEmpty(body);
            bodyText.gameObject.SetActive(hasBody);

            float width = Mathf.Min(maxWidth, Mathf.Max(180f, titleText.preferredWidth + 40f));
            float height = hasBody ? 52f : 28f;
            if (hasBody)
            {
                float bodyHeight = Mathf.Min(120f, bodyText.preferredHeight + 4f);
                height = 30f + bodyHeight;
                bodyText.rectTransform.sizeDelta = new Vector2(-18f, bodyHeight);
            }
            root.sizeDelta = new Vector2(width, height);

            Vector2 center, size;
            if (KvOverlayKit.ScreenRect(anchor, out center, out size))
            {
                Vector2 local = KvOverlayKit.ToCanvas(canvasRect, center);
                float x = local.x - width * 0.5f;
                float y = local.y - size.y * 0.5f - 8f;
                // Не выходим за пределы канваса.
                float halfW = canvasRect.rect.width * 0.5f;
                float halfH = canvasRect.rect.height * 0.5f;
                x = Mathf.Clamp(x, -halfW + 4f, halfW - width - 4f);
                if (y - height < -halfH) y = local.y + size.y * 0.5f + height + 8f;
                root.anchoredPosition = new Vector2(x, y);
            }
            else
            {
                root.anchoredPosition = new Vector2(12f, -12f);
            }

            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
        }

        public void Hide()
        {
            if (root != null) root.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// ПУЛЬСИРУЮЩАЯ РАМКА ПОДСВЕТКИ (этап 2 ТЗ: «подсветка нужных элементов»).
    /// Ставится поверх любого элемента интерфейса (в том числе на другом канвасе):
    /// положение берётся из его экранного прямоугольника.
    /// </summary>
    public class KvHighlightFrame : MonoBehaviour
    {
        public Color color = new Color(1f, 0.78f, 0.2f, 1f);
        public float pulseSpeed = 3.4f;
        public float thickness = 2f;
        public float padding = 3f;

        private RectTransform canvasRect;
        private RectTransform root;
        private readonly List<Image> bars = new List<Image>();
        private RectTransform target;
        private bool visible;

        public static KvHighlightFrame Create(RectTransform canvasRect)
        {
            GameObject go = new GameObject("KvHighlightFrame", typeof(RectTransform));
            go.transform.SetParent(canvasRect, false);
            KvHighlightFrame frame = go.AddComponent<KvHighlightFrame>();
            frame.Build(canvasRect);
            return frame;
        }

        private void Build(RectTransform canvas)
        {
            canvasRect = canvas;
            root = (RectTransform)transform;
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(40f, 40f);

            for (int i = 0; i < 4; i++)
            {
                Image bar = KvTheme.CreatePanel(root, "Bar" + i, color);
                bar.raycastTarget = false;
                bars.Add(bar);
            }
            root.gameObject.SetActive(false);
        }

        public void SetTarget(RectTransform value)
        {
            target = value;
            visible = value != null;
            if (root != null) root.gameObject.SetActive(visible && isActiveAndEnabled);
            Update();
        }

        public void SetVisible(bool value)
        {
            visible = value && target != null;
            if (root != null) root.gameObject.SetActive(visible);
        }

        public bool IsVisible { get { return visible && root != null && root.gameObject.activeSelf; } }

        private void Update()
        {
            if (!visible || root == null || target == null) return;

            Vector2 center, size;
            if (!KvOverlayKit.ScreenRect(target, out center, out size))
            {
                root.gameObject.SetActive(false);
                return;
            }
            root.gameObject.SetActive(true);

            Vector2 local = KvOverlayKit.ToCanvas(canvasRect, center);
            root.anchoredPosition = local;
            root.sizeDelta = size + new Vector2(padding * 2f, padding * 2f);

            float w = root.sizeDelta.x, h = root.sizeDelta.y;
            float t = thickness;
            Place(bars[0], new Vector2(0f, h * 0.5f - t * 0.5f), new Vector2(w, t));    // верх
            Place(bars[1], new Vector2(0f, -h * 0.5f + t * 0.5f), new Vector2(w, t));   // низ
            Place(bars[2], new Vector2(-w * 0.5f + t * 0.5f, 0f), new Vector2(t, h));   // лево
            Place(bars[3], new Vector2(w * 0.5f - t * 0.5f, 0f), new Vector2(t, h));    // право

            float k = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * pulseSpeed);
            Color c = new Color(color.r, color.g, color.b, Mathf.Clamp01(0.35f + 0.65f * k));
            for (int i = 0; i < bars.Count; i++) bars[i].color = c;
        }

        private static void Place(Image bar, Vector2 position, Vector2 size)
        {
            RectTransform rt = bar.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }
    }

    /// <summary>
    /// КАРТОЧКА-ПОДСКАЗКА (туториал и демонстрация): заголовок, счётчик шагов,
    /// полоса прогресса, текст и ряд кнопок. Живёт на своём канвасе, поэтому видна
    /// и при скрытой оболочке интерфейса.
    /// </summary>
    public class KvHintCard : MonoBehaviour
    {
        public float width = 520f;

        private RectTransform canvasRect;
        private RectTransform root;
        private Text titleText;
        private Text stepText;
        private Text bodyText;
        private Image progressBg;
        private Image progressFill;
        private Image progressFrame;
        private Text footerText;
        private RectTransform buttonRow;
        private readonly List<Button> buttons = new List<Button>();
        private readonly List<Text> buttonLabels = new List<Text>();

        public static KvHintCard Create(RectTransform canvasRect)
        {
            GameObject go = new GameObject("KvHintCard", typeof(RectTransform));
            go.transform.SetParent(canvasRect, false);
            KvHintCard card = go.AddComponent<KvHintCard>();
            card.Build(canvasRect);
            return card;
        }

        private void Build(RectTransform canvas)
        {
            canvasRect = canvas;
            root = (RectTransform)transform;
            root.anchorMin = new Vector2(0.5f, 0f);
            root.anchorMax = new Vector2(0.5f, 0f);
            root.pivot = new Vector2(0.5f, 0f);
            root.sizeDelta = new Vector2(width, 150f);
            root.anchoredPosition = new Vector2(0f, 96f);

            Image bg = KvTheme.CreatePanel(root, "Bg", KvTheme.WindowBg);
            KvTheme.Stretch(bg.rectTransform);
            Outline o = bg.gameObject.AddComponent<Outline>();
            o.effectColor = KvTheme.Border;
            o.effectDistance = new Vector2(1f, -1f);
            o.useGraphicAlpha = false;

            Image accent = KvTheme.CreatePanel(root, "Accent", KvTheme.Accent);
            accent.rectTransform.anchorMin = new Vector2(0f, 1f);
            accent.rectTransform.anchorMax = new Vector2(1f, 1f);
            accent.rectTransform.pivot = new Vector2(0.5f, 1f);
            accent.rectTransform.sizeDelta = new Vector2(0f, 3f);
            accent.rectTransform.anchoredPosition = Vector2.zero;

            titleText = KvTheme.CreateText(root, "Title", "", KvTheme.FontSizeTitle + 1,
                TextAnchor.UpperLeft, KvTheme.TextMain);
            titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            titleText.rectTransform.pivot = new Vector2(0f, 1f);
            titleText.rectTransform.offsetMin = new Vector2(14f, 0f);
            titleText.rectTransform.offsetMax = new Vector2(-150f, 0f);
            titleText.rectTransform.sizeDelta = new Vector2(-164f, 20f);
            titleText.rectTransform.anchoredPosition = new Vector2(0f, -8f);

            stepText = KvTheme.CreateText(root, "Step", "", KvTheme.FontSizeSmall,
                TextAnchor.UpperRight, KvTheme.Accent);
            stepText.rectTransform.anchorMin = new Vector2(1f, 1f);
            stepText.rectTransform.anchorMax = new Vector2(1f, 1f);
            stepText.rectTransform.pivot = new Vector2(1f, 1f);
            stepText.rectTransform.sizeDelta = new Vector2(140f, 18f);
            stepText.rectTransform.anchoredPosition = new Vector2(-12f, -9f);

            progressFrame = KvTheme.CreatePanel(root, "ProgressFrame", KvTheme.PanelDark);
            progressFrame.rectTransform.anchorMin = new Vector2(0f, 1f);
            progressFrame.rectTransform.anchorMax = new Vector2(1f, 1f);
            progressFrame.rectTransform.pivot = new Vector2(0.5f, 1f);
            progressFrame.rectTransform.offsetMin = new Vector2(14f, 0f);
            progressFrame.rectTransform.offsetMax = new Vector2(-14f, 0f);
            progressFrame.rectTransform.sizeDelta = new Vector2(-28f, 4f);
            progressFrame.rectTransform.anchoredPosition = new Vector2(0f, -30f);

            progressBg = KvTheme.CreatePanel(progressFrame.rectTransform, "Bg", KvTheme.PanelDark);
            KvTheme.Stretch(progressBg.rectTransform);

            progressFill = KvTheme.CreatePanel(progressFrame.rectTransform, "Fill", KvTheme.Accent);
            progressFill.rectTransform.anchorMin = new Vector2(0f, 0f);
            progressFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            progressFill.rectTransform.pivot = new Vector2(0f, 0.5f);
            progressFill.rectTransform.sizeDelta = new Vector2(0f, 0f);
            progressFill.rectTransform.anchoredPosition = Vector2.zero;

            bodyText = KvTheme.CreateText(root, "Body", "", KvTheme.FontSize,
                TextAnchor.UpperLeft, KvTheme.TextMain);
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyText.rectTransform.anchorMin = new Vector2(0f, 1f);
            bodyText.rectTransform.anchorMax = new Vector2(1f, 1f);
            bodyText.rectTransform.pivot = new Vector2(0f, 1f);
            bodyText.rectTransform.offsetMin = new Vector2(14f, 0f);
            bodyText.rectTransform.offsetMax = new Vector2(-14f, 0f);
            bodyText.rectTransform.sizeDelta = new Vector2(-28f, 46f);
            bodyText.rectTransform.anchoredPosition = new Vector2(0f, -40f);

            footerText = KvTheme.CreateText(root, "Footer", "", KvTheme.FontSizeSmall,
                TextAnchor.UpperLeft, KvTheme.TextDim);
            footerText.horizontalOverflow = HorizontalWrapMode.Wrap;
            footerText.rectTransform.anchorMin = new Vector2(0f, 0f);
            footerText.rectTransform.anchorMax = new Vector2(1f, 0f);
            footerText.rectTransform.pivot = new Vector2(0f, 0f);
            footerText.rectTransform.offsetMin = new Vector2(14f, 8f);
            footerText.rectTransform.offsetMax = new Vector2(-300f, 0f);
            footerText.rectTransform.sizeDelta = new Vector2(-314f, 16f);
            footerText.rectTransform.anchoredPosition = new Vector2(0f, 8f);

            buttonRow = KvWidgets.CreateRow(root, "Buttons", 24f, 6f, TextAnchor.MiddleRight);
            buttonRow.anchorMin = new Vector2(1f, 0f);
            buttonRow.anchorMax = new Vector2(1f, 0f);
            buttonRow.pivot = new Vector2(1f, 0f);
            buttonRow.sizeDelta = new Vector2(width - 28f, 24f);
            buttonRow.anchoredPosition = new Vector2(-14f, 8f);

            gameObject.SetActive(false);
        }

        public bool Visible { get { return root != null && root.gameObject.activeSelf; } }

        public void SetTitle(string title)
        {
            if (titleText != null) titleText.text = title ?? "";
        }

        public void SetBody(string body)
        {
            if (bodyText == null) return;
            bodyText.text = body ?? "";
            float h = Mathf.Min(160f, Mathf.Max(18f, bodyText.preferredHeight + 4f));
            bodyText.rectTransform.sizeDelta = new Vector2(-28f, h);
            float height = 60f + h + 34f;
            root.sizeDelta = new Vector2(width, height);
            if (stepText != null)
                stepText.rectTransform.anchoredPosition = new Vector2(-12f, -9f);
        }

        public void SetStep(int index, int total)
        {
            if (stepText == null) return;
            stepText.text = total > 0
                ? KvLocExtra.F("tut.step", "Шаг {0} из {1}", index + 1, total)
                : "";
            float k = total > 0 ? Mathf.Clamp01((index + 1) / (float)total) : 0f;
            if (progressFill != null)
                progressFill.rectTransform.sizeDelta = new Vector2(progressFrame.rectTransform.rect.width * k, 0f);
        }

        public void SetFooter(string footer)
        {
            if (footerText != null) footerText.text = footer ?? "";
        }

        /// <summary>Кнопки карточки. `labels` и `actions` — одинаковой длины.</summary>
        public void SetButtons(string[] labels, Action[] actions)
        {
            while (buttons.Count < (labels != null ? labels.Length : 0))
            {
                int index = buttons.Count;
                Button button = KvTheme.CreateSmallButton(buttonRow, "CardBtn" + index, "—",
                    delegate { });
                KvWidgets.Fit(button.gameObject, 0f, 22f);
                buttons.Add(button);
                buttonLabels.Add(button.GetComponentInChildren<Text>());
            }
            for (int i = 0; i < buttons.Count; i++)
            {
                bool used = labels != null && i < labels.Length;
                buttons[i].gameObject.SetActive(used);
                if (!used) continue;
                buttons[i].onClick.RemoveAllListeners();
                int captured = i;
                buttons[i].onClick.AddListener(delegate
                {
                    if (actions != null && captured < actions.Length && actions[captured] != null)
                        actions[captured]();
                });
                if (buttonLabels[i] != null) buttonLabels[i].text = labels[i];
            }
        }

        public void Show()
        {
            if (root == null) return;
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
        }

        public void Hide()
        {
            if (root != null) root.gameObject.SetActive(false);
        }
    }
}
