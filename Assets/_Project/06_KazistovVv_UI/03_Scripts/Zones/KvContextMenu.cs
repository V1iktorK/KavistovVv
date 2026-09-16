using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KazistovVvUI
{
    /// <summary>Строка контекстного меню (ЭТАП 6).</summary>
    public class KvContextMenuItem
    {
        /// <summary>Подпись пункта.</summary>
        public string Label = "";
        /// <summary>Id иконки (может быть пустым).</summary>
        public string Icon = "";
        /// <summary>Действие пункта.</summary>
        public Action Action;
        /// <summary>Пункт доступен.</summary>
        public bool Enabled = true;
        /// <summary>Пункт отмечен галочкой (для «Скрыть/Показать»).</summary>
        public bool Checked;
        /// <summary>Горячая клавиша справа (необязательно).</summary>
        public string Hotkey = "";
        /// <summary>Пункт-разделитель (остальные поля не используются).</summary>
        public bool Separator;

        public KvContextMenuItem(string label, Action action, string icon = "", bool enabled = true,
            bool checkable = false, bool isChecked = false, string hotkey = "")
        {
            Label = label;
            Action = action;
            Icon = icon;
            Enabled = enabled;
            Checked = isChecked;
            Hotkey = hotkey;
        }

        public KvContextMenuItem() { }

        /// <summary>Разделитель.</summary>
        public static KvContextMenuItem Sep()
        {
            return new KvContextMenuItem { Separator = true };
        }
    }

    /// <summary>
    /// КОНТЕКСТНОЕ МЕНЮ (ЭТАП 6): всплывающий список действий по правому клику.
    /// Показывается в точке курсора, закрывается по клику вне, по Esc и после выбора.
    /// Готового решения в проекте нет, поэтому меню сделано локально — по тому же
    /// принципу, что выпадающие списки меню-бара и групп тулбара.
    /// </summary>
    public class KvContextMenu : MonoBehaviour
    {
        private const float RowHeight = 20f;
        private const float PanelWidth = 236f;
        private const int RowIconSize = 14;

        private static KvContextMenu instance;

        private RectTransform canvasRect;
        private RectTransform panel;
        private RectTransform blocker;
        private readonly List<GameObject> rows = new List<GameObject>();
        private Action onClosed;

        /// <summary>Меню сейчас открыто.</summary>
        public bool IsOpen { get { return panel != null && panel.gameObject.activeSelf; } }
        /// <summary>Сколько строк в меню (диагностика).</summary>
        public int ItemCount { get { return rows.Count; } }
        /// <summary>Текущее (последнее открытое) меню.</summary>
        public static KvContextMenu Current { get { return instance; } }

        /// <summary>
        /// Показать меню в точке экрана. <paramref name="canvas"/> — канвас интерфейса,
        /// <paramref name="screenPosition"/> — позиция курсора (обычно `PointerEventData.position`).
        /// </summary>
        public static KvContextMenu Show(RectTransform canvas, Vector2 screenPosition, string title,
            List<KvContextMenuItem> items, Action closed = null)
        {
            if (canvas == null || items == null || items.Count == 0) return null;
            if (instance != null && instance.canvasRect != canvas)
            {
                Destroy(instance.gameObject);
                instance = null;
            }
            if (instance == null)
            {
                GameObject go = new GameObject("KvContextMenuLayer", typeof(RectTransform));
                go.transform.SetParent(canvas, false);
                RectTransform rt = (RectTransform)go.transform;
                KvTheme.Stretch(rt);
                instance = go.AddComponent<KvContextMenu>();
                instance.canvasRect = canvas;
                instance.Build();
            }
            instance.Open(screenPosition, title, items, closed);
            return instance;
        }

        /// <summary>Закрыть текущее меню (если открыто).</summary>
        public static void CloseCurrent()
        {
            if (instance != null) instance.Close();
        }

        private void Build()
        {
            GameObject blockGo = new GameObject("Blocker", typeof(Image), typeof(Button));
            blockGo.transform.SetParent(transform, false);
            blocker = (RectTransform)blockGo.transform;
            KvTheme.Stretch(blocker);
            Image bi = blockGo.GetComponent<Image>();
            bi.color = new Color(0f, 0f, 0f, 0f);
            bi.raycastTarget = true;
            Button bb = blockGo.GetComponent<Button>();
            bb.transition = Selectable.Transition.None;
            bb.onClick.AddListener(Close);

            GameObject go = new GameObject("KvContextMenu", typeof(Image));
            go.transform.SetParent(transform, false);
            panel = (RectTransform)go.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 1f);
            panel.sizeDelta = new Vector2(PanelWidth, 0f);
            Image bg = go.GetComponent<Image>();
            bg.sprite = KvTheme.WhiteSprite;
            bg.color = KvTheme.PanelBg;
            bg.raycastTarget = true;
            Outline o = go.AddComponent<Outline>();
            o.effectColor = KvTheme.Border;
            o.effectDistance = new Vector2(1f, -1f);
            o.useGraphicAlpha = false;

            panel.gameObject.SetActive(false);
            blocker.gameObject.SetActive(false);
        }

        private void Open(Vector2 screenPosition, string title, List<KvContextMenuItem> items,
            Action closed)
        {
            onClosed = closed;
            RebuildRows(title, items);
            Position(screenPosition);

            blocker.gameObject.SetActive(true);
            blocker.SetAsLastSibling();
            panel.gameObject.SetActive(true);
            panel.SetAsLastSibling();
        }

        /// <summary>Закрыть меню.</summary>
        public void Close()
        {
            if (panel != null) panel.gameObject.SetActive(false);
            if (blocker != null) blocker.gameObject.SetActive(false);
            Action cb = onClosed;
            onClosed = null;
            if (cb != null) cb();
        }

        private void Update()
        {
            if (!IsOpen) return;
            if (Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        private void RebuildRows(string title, List<KvContextMenuItem> items)
        {
            foreach (GameObject go in rows)
                if (go != null) Destroy(go);
            rows.Clear();

            float y = 3f;
            if (!string.IsNullOrEmpty(title))
            {
                Text t = KvTheme.CreateText(panel, "CtxTitle", title, KvTheme.FontSizeSmall - 1,
                    TextAnchor.MiddleLeft, KvTheme.TextDisabled);
                RectTransform tr = t.rectTransform;
                tr.anchorMin = new Vector2(0f, 1f);
                tr.anchorMax = new Vector2(1f, 1f);
                tr.pivot = new Vector2(0f, 1f);
                tr.sizeDelta = new Vector2(-8f, 15f);
                tr.anchoredPosition = new Vector2(6f, -y);
                rows.Add(t.gameObject);
                y += 16f;
            }

            foreach (KvContextMenuItem item in items)
            {
                if (item == null) continue;
                if (item.Separator)
                {
                    Image sep = KvTheme.CreatePanel(panel, "CtxSep", KvTheme.Separator);
                    sep.rectTransform.anchorMin = new Vector2(0f, 1f);
                    sep.rectTransform.anchorMax = new Vector2(1f, 1f);
                    sep.rectTransform.pivot = new Vector2(0f, 1f);
                    sep.rectTransform.sizeDelta = new Vector2(-10f, 1f);
                    sep.rectTransform.anchoredPosition = new Vector2(5f, -(y + 2f));
                    rows.Add(sep.gameObject);
                    y += 5f;
                    continue;
                }
                y = AddRow(item, y);
            }

            panel.sizeDelta = new Vector2(PanelWidth, y + 3f);
        }

        private float AddRow(KvContextMenuItem item, float y)
        {
            GameObject row = new GameObject("Ctx_" + item.Label, typeof(Image), typeof(Button));
            row.transform.SetParent(panel, false);
            RectTransform rt = (RectTransform)row.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(-6f, RowHeight);
            rt.anchoredPosition = new Vector2(3f, -y);

            Image bg = row.GetComponent<Image>();
            bg.sprite = KvTheme.WhiteSprite;
            bg.color = new Color(0f, 0f, 0f, 0f);
            bg.raycastTarget = true;

            Button btn = row.GetComponent<Button>();
            btn.targetGraphic = bg;
            btn.interactable = item.Enabled;
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = KvTheme.Ratio(KvTheme.ButtonHover, KvTheme.PanelBg);
            cb.pressedColor = KvTheme.Ratio(KvTheme.ButtonPressed, KvTheme.PanelBg);
            cb.disabledColor = Color.white;
            cb.fadeDuration = 0.05f;
            btn.colors = cb;

            Color textColor = item.Enabled ? KvTheme.TextMain : KvTheme.TextDisabled;

            if (!string.IsNullOrEmpty(item.Icon))
            {
                GameObject iconGo = new GameObject("Icon", typeof(Image));
                iconGo.transform.SetParent(row.transform, false);
                Image icon = iconGo.GetComponent<Image>();
                icon.sprite = KvIcons.Get(item.Icon, RowIconSize);
                icon.color = item.Enabled ? KvTheme.IconTint : KvTheme.TextDisabled;
                icon.raycastTarget = false;
                RectTransform ir = icon.rectTransform;
                ir.anchorMin = ir.anchorMax = new Vector2(0f, 0.5f);
                ir.pivot = new Vector2(0f, 0.5f);
                ir.sizeDelta = new Vector2(RowIconSize, RowIconSize);
                ir.anchoredPosition = new Vector2(6f, 0f);
                if (icon.sprite == null) iconGo.SetActive(false);
            }

            Text text = KvTheme.CreateText(rt, "Label", item.Label, KvTheme.FontSizeSmall,
                TextAnchor.MiddleLeft, textColor);
            text.rectTransform.anchorMin = new Vector2(0f, 0f);
            text.rectTransform.anchorMax = new Vector2(1f, 1f);
            text.rectTransform.offsetMin = new Vector2(26f, 0f);
            text.rectTransform.offsetMax = new Vector2(-8f, 0f);

            if (item.Checked)
            {
                GameObject checkGo = new GameObject("Check", typeof(Image));
                checkGo.transform.SetParent(row.transform, false);
                Image check = checkGo.GetComponent<Image>();
                check.sprite = KvIcons.Get("check", 10);
                check.color = KvTheme.Accent;
                check.raycastTarget = false;
                RectTransform cr = check.rectTransform;
                cr.anchorMin = cr.anchorMax = new Vector2(1f, 0.5f);
                cr.pivot = new Vector2(1f, 0.5f);
                cr.sizeDelta = new Vector2(10f, 10f);
                cr.anchoredPosition = new Vector2(-6f, 0f);
            }

            if (!string.IsNullOrEmpty(item.Hotkey))
            {
                Text hk = KvTheme.CreateText(rt, "Hotkey", item.Hotkey, KvTheme.FontSizeSmall - 1,
                    TextAnchor.MiddleRight, KvTheme.TextDim);
                hk.rectTransform.anchorMin = new Vector2(1f, 0f);
                hk.rectTransform.anchorMax = new Vector2(1f, 1f);
                hk.rectTransform.pivot = new Vector2(1f, 0.5f);
                hk.rectTransform.sizeDelta = new Vector2(60f, 0f);
                hk.rectTransform.anchoredPosition = new Vector2(-6f, 0f);
            }

            Action action = item.Action;
            btn.onClick.AddListener(delegate
            {
                Close();
                if (action != null) action();
            });

            rows.Add(row);
            return y + RowHeight + 1f;
        }

        /// <summary>Поставить меню в точке курсора, не выпуская за пределы канваса.</summary>
        private void Position(Vector2 screenPosition)
        {
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition,
                null, out local);

            float w = canvasRect.rect.width, h = canvasRect.rect.height;
            float pw = panel.sizeDelta.x, ph = panel.sizeDelta.y;
            float x = local.x;
            float y = local.y;
            if (x + pw > w * 0.5f) x = w * 0.5f - pw - 4f;
            if (x < -w * 0.5f + 4f) x = -w * 0.5f + 4f;
            if (y - ph < -h * 0.5f + 4f) y = -h * 0.5f + ph + 4f;
            panel.anchoredPosition = new Vector2(x, y);
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }
}
