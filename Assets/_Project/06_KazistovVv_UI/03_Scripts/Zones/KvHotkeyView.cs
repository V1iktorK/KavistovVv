using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KazistovVvUI
{
    /// <summary>
    /// ОКНО СПИСКА ГОРЯЧИХ КЛАВИШ (ЭТАП 7). Открывается по F12 и через меню
    /// «Справка → Горячие клавиши»; живёт в ПЛАВАЮЩЕЙ dock-панели, поэтому его можно
    /// тащить за заголовок, пристыковать к краю, растянуть за границы/углы
    /// (этапы 3–4) — раскладка сохраняется в PlayerPrefs.
    ///
    /// Содержимое: поле поиска (фильтр по названию, клавише, разделу, пояснению),
    /// список биндов с группировкой по разделам и ПОДСВЕТКОЙ КОНФЛИКТОВ (одна клавиша
    /// на два разных действия). Кнопка «Переназначить» — заглушка на будущее.
    /// </summary>
    public class KvHotkeyView : MonoBehaviour
    {
        private RectTransform root;
        private RectTransform content;
        private InputField field;
        private Image bg;
        private readonly List<GameObject> rows = new List<GameObject>();

        /// <summary>Сколько строк построено (диагностика).</summary>
        public int RowCount { get { return rows.Count; } }
        /// <summary>Сколько конфликтов подсвечено в текущем списке (диагностика).</summary>
        public int ConflictCount { get; private set; }
        /// <summary>Текущая строка поиска (диагностика).</summary>
        public string Query { get { return field != null ? field.text : ""; } }

        public void Build(RectTransform parent)
        {
            root = parent;
            bg = parent.GetComponent<Image>();
            if (bg == null)
            {
                bg = parent.gameObject.AddComponent<Image>();
                bg.sprite = KvTheme.WhiteSprite;
                bg.color = KvTheme.PanelDark;
            }

            // --- строка поиска
            GameObject inputGo = new GameObject("Search", typeof(Image), typeof(InputField));
            inputGo.transform.SetParent(root, false);
            RectTransform ir = (RectTransform)inputGo.transform;
            ir.anchorMin = new Vector2(0f, 1f);
            ir.anchorMax = new Vector2(1f, 1f);
            ir.pivot = new Vector2(0.5f, 1f);
            ir.sizeDelta = new Vector2(-8f, 22f);
            ir.anchoredPosition = new Vector2(0f, -3f);
            Image inputBg = inputGo.GetComponent<Image>();
            inputBg.sprite = KvTheme.WhiteSprite;
            inputBg.color = KvTheme.InputBg;
            inputBg.raycastTarget = true;

            Text placeholder = KvTheme.CreateText(ir, "Placeholder",
                KvLoc.T("hotkeys.search", "Поиск: название действия или клавиша…"),
                KvTheme.FontSizeSmall, TextAnchor.MiddleLeft, KvTheme.TextDisabled);
            KvTheme.Stretch(placeholder.rectTransform, 6f, 6f, 2f, 2f);

            Text text = KvTheme.CreateText(ir, "Text", "", KvTheme.FontSizeSmall,
                TextAnchor.MiddleLeft, KvTheme.TextMain);
            KvTheme.Stretch(text.rectTransform, 6f, 6f, 2f, 2f);
            text.supportRichText = false;

            field = inputGo.GetComponent<InputField>();
            field.targetGraphic = inputBg;
            field.textComponent = text;
            field.placeholder = placeholder;
            field.lineType = InputField.LineType.SingleLine;
            field.caretColor = KvTheme.TextMain;
            field.selectionColor = KvTheme.SelectionBg;
            field.onValueChanged.AddListener(delegate { Rebuild(); });

            // --- прокручиваемый список
            GameObject scrollGo = new GameObject("Scroll", typeof(ScrollRect), typeof(RectMask2D),
                typeof(Image));
            scrollGo.transform.SetParent(root, false);
            Image scrollBg = scrollGo.GetComponent<Image>();
            scrollBg.sprite = KvTheme.WhiteSprite;
            scrollBg.color = KvTheme.PanelDark;
            RectTransform viewport = (RectTransform)scrollGo.transform;
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(2f, 2f);
            viewport.offsetMax = new Vector2(-2f, -28f);

            ScrollRect scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 26f;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.viewport = viewport;

            GameObject contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewport, false);
            content = (RectTransform)contentGo.transform;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            VerticalLayoutGroup vlg = contentGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 1f;
            vlg.padding = new RectOffset(4, 4, 3, 3);
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            ContentSizeFitter csf = contentGo.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            scroll.content = content;

            Rebuild();
        }

        /// <summary>Задать строку поиска программно (диагностика/тесты).</summary>
        public void SetQuery(string query)
        {
            if (field != null) field.text = query ?? "";
            Rebuild();
        }

        /// <summary>Перестроить список биндов с учётом фильтра и конфликтов.</summary>
        public void Rebuild()
        {
            if (content == null) return;
            foreach (GameObject go in rows)
                if (go != null) Destroy(go);
            rows.Clear();
            ConflictCount = 0;

            HashSet<string> conflicts = KvBindings.Conflicts();
            List<KvBindEntry> list = KvBindings.Filter(field != null ? field.text : "");

            if (list.Count == 0)
            {
                rows.Add(Header(KvLoc.T("hotkeys.empty", "Ничего не найдено")).gameObject);
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
                return;
            }

            string device = null;
            string group = null;
            foreach (KvBindEntry e in list)
            {
                if (e == null) continue;
                if (e.Device != device)
                {
                    device = e.Device;
                    group = null;
                    rows.Add(Header(device.ToUpperInvariant()).gameObject);
                }
                if (e.Group != group)
                {
                    group = e.Group;
                    rows.Add(SubHeader(group).gameObject);
                }
                bool conflict = KvBindings.IsConflict(e, conflicts);
                if (conflict) ConflictCount++;
                rows.Add(BindRow(e, conflict));
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }

        private Text Header(string title)
        {
            Text t = KvWidgets.Label(content, "HK_" + title, title, KvTheme.FontSizeSmall,
                KvTheme.Accent, TextAnchor.MiddleLeft);
            KvWidgets.Fit(t.gameObject, 0f, 18f);
            return t;
        }

        private Text SubHeader(string title)
        {
            Text t = KvWidgets.Label(content, "HKG_" + title, title, KvTheme.FontSizeSmall - 1,
                KvTheme.TextDim, TextAnchor.MiddleLeft);
            KvWidgets.Fit(t.gameObject, 0f, 15f);
            t.rectTransform.offsetMin = new Vector2(6f, 0f);
            return t;
        }

        private GameObject BindRow(KvBindEntry entry, bool conflict)
        {
            RectTransform row = KvWidgets.CreateRow(content, "HKRow_" + entry.Action, 18f, 6f);
            if (conflict)
            {
                Image mark = KvTheme.CreatePanel(row, "ConflictBg", new Color(
                    KvTheme.Error.r, KvTheme.Error.g, KvTheme.Error.b, 0.22f));
                KvTheme.Stretch(mark.rectTransform);
                mark.transform.SetAsFirstSibling();
            }

            Text action = KvTheme.CreateText(row, "Action", entry.Action, KvTheme.FontSizeSmall,
                TextAnchor.MiddleLeft, conflict ? KvTheme.Error : KvTheme.TextMain);
            LayoutElement le = action.gameObject.AddComponent<LayoutElement>();
            le.minWidth = 250f;
            le.preferredWidth = 250f;
            le.minHeight = 16f;

            Text keys = KvTheme.CreateText(row, "Keys", entry.Keys, KvTheme.FontSizeSmall,
                TextAnchor.MiddleLeft, conflict ? KvTheme.Error : KvTheme.Accent);
            LayoutElement le2 = keys.gameObject.AddComponent<LayoutElement>();
            le2.minWidth = 150f;
            le2.preferredWidth = 150f;
            le2.minHeight = 16f;

            string note = entry.Note;
            if (conflict) note = KvLoc.T("hotkeys.conflict", "КОНФЛИКТ: эта клавиша занята другим действием") +
                (string.IsNullOrEmpty(note) ? "" : " · " + note);
            if (!string.IsNullOrEmpty(note))
            {
                Text noteText = KvTheme.CreateText(row, "Note", note, KvTheme.FontSizeSmall - 1,
                    TextAnchor.MiddleLeft, conflict ? KvTheme.Error : KvTheme.TextDisabled);
                LayoutElement le3 = noteText.gameObject.AddComponent<LayoutElement>();
                le3.flexibleWidth = 1f;
                le3.minHeight = 16f;
            }

            // Заглушка переназначения (ТЗ ЭПАТА 7: «пока заглушка»).
            Button rebind = KvTheme.CreateSmallButton(row, "Rebind",
                KvLoc.T("hotkeys.rebind", "Переназначить"), delegate
                {
                    KazistovVvUIManager.SetPlanStatus(
                        KvLoc.T("hotkeys.rebind.stub",
                            "Переназначение клавиш появится позже — бинды не меняются"),
                        KvTheme.Warn);
                });
            LayoutElement le4 = rebind.gameObject.AddComponent<LayoutElement>();
            le4.minWidth = 104f;
            le4.preferredWidth = 104f;
            le4.minHeight = 16f;
            KvTooltipTarget tip = rebind.gameObject.AddComponent<KvTooltipTarget>();
            tip.Set(KvLoc.T("hotkeys.rebind", "Переназначить"),
                KvLoc.T("common.stub", "в разработке"), "");
            tip.stub = true;

            return row.gameObject;
        }

        /// <summary>Перекрасить окно под текущую тему.</summary>
        public void Repaint()
        {
            if (bg != null) bg.color = KvTheme.PanelDark;
            Rebuild();
        }
    }
}
