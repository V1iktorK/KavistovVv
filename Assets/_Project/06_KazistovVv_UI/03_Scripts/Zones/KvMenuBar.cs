using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KazistovVvUI
{
    /// <summary>
    /// Строка меню в стиле FreeCAD (сверху, тонкая): пункты строятся ИЗ РЕЕСТРА
    /// команд по их <see cref="KvCommand.MenuPath"/> («Вид/Зона достижимости»).
    /// Поэтому новое меню/пункт добавляется регистрацией команды — код этой
    /// панели трогать не нужно.
    /// </summary>
    public class KvMenuBar : MonoBehaviour
    {
        /// <summary>Высота строки меню.</summary>
        public const float Height = 20f;

        private static readonly string[] Menus = { "Файл", "Правка", "Вид", "Робот", "Сервис", "Справка" };

        private RectTransform root;
        private RectTransform canvasRect;
        private RectTransform dropdown;
        private RectTransform blocker;
        private string openMenu = "";
        private readonly List<GameObject> dropdownRows = new List<GameObject>();
        private readonly List<Image> menuButtons = new List<Image>();
        private readonly List<Text> menuTexts = new List<Text>();
        private Image dropdownBg;
        private Text infoText;

        /// <summary>Собрать строку меню. Возвращает её высоту.</summary>
        public float Build(RectTransform canvas, Canvas owner)
        {
            canvasRect = canvas;

            GameObject go = new GameObject("KvMenuBar", typeof(Image));
            go.transform.SetParent(canvas, false);
            root = (RectTransform)go.transform;
            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.sizeDelta = new Vector2(0f, Height);
            root.anchoredPosition = Vector2.zero;
            Image bg = go.GetComponent<Image>();
            bg.sprite = KvTheme.WhiteSprite;
            bg.color = KvTheme.PanelHeader;
            bg.raycastTarget = true;

            // Кнопки меню — слева, тонкие подписи.
            float x = 4f;
            foreach (string menu in Menus)
            {
                string captured = menu;
                GameObject bgo = new GameObject("Menu_" + menu, typeof(Image), typeof(Button));
                bgo.transform.SetParent(root, false);
                Image bimg = bgo.GetComponent<Image>();
                bimg.sprite = KvTheme.WhiteSprite;
                bimg.color = new Color(0f, 0f, 0f, 0f);
                bimg.raycastTarget = true;
                RectTransform brt = (RectTransform)bgo.transform;
                brt.anchorMin = new Vector2(0f, 0f);
                brt.anchorMax = new Vector2(0f, 1f);
                brt.pivot = new Vector2(0f, 0.5f);
                float w = 10f + menu.Length * 7.2f;
                brt.sizeDelta = new Vector2(w, -1f);
                brt.anchoredPosition = new Vector2(x, 0f);
                x += w + 1f;

                // ЭТАП 2: подпись пункта строки меню берётся из словаря
                // («Файл» → menu.file → «File»), длина кнопки считается по русскому
                // названию, поэтому геометрия меню не «прыгает» при смене языка.
                Text t = KvTheme.CreateText(brt, "Label", KvLoc.Menu(menu), KvTheme.FontSizeSmall,
                    TextAnchor.MiddleCenter, KvTheme.TextMain);
                KvTheme.Stretch(t.rectTransform);

                Button btn = bgo.GetComponent<Button>();
                btn.targetGraphic = bimg;
                ColorBlock cb = btn.colors;
                cb.normalColor = Color.white;
                cb.highlightedColor = KvTheme.Ratio(KvTheme.ButtonHover, KvTheme.PanelHeader);
                cb.pressedColor = KvTheme.Ratio(KvTheme.ButtonPressed, KvTheme.PanelHeader);
                btn.colors = cb;
                btn.onClick.AddListener(delegate { ToggleMenu(captured); });

                menuButtons.Add(bimg);
                menuTexts.Add(t);
            }

            // Правая часть — «имя проекта» (как заголовок рабочей среды FreeCAD).
            infoText = KvTheme.CreateText(root, "Info", "", KvTheme.FontSizeSmall,
                TextAnchor.MiddleRight, KvTheme.TextDim);
            infoText.rectTransform.anchorMin = new Vector2(1f, 0f);
            infoText.rectTransform.anchorMax = new Vector2(1f, 1f);
            infoText.rectTransform.pivot = new Vector2(1f, 0.5f);
            infoText.rectTransform.sizeDelta = new Vector2(520f, 0f);
            infoText.rectTransform.anchoredPosition = new Vector2(-6f, 0f);

            BuildBlocker(canvas);
            return Height;
        }

        /// <summary>Текст справа в строке меню (например «KazistovVv · робот: 6-осевой»).</summary>
        public void SetInfo(string text)
        {
            if (infoText != null) infoText.text = text;
        }

        private void BuildBlocker(RectTransform canvas)
        {
            GameObject go = new GameObject("KvMenuBlocker", typeof(Image), typeof(Button));
            go.transform.SetParent(canvas, false);
            blocker = (RectTransform)go.transform;
            KvTheme.Stretch(blocker);
            Image img = go.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f);
            img.raycastTarget = true;
            Button b = go.GetComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(CloseMenu);
            blocker.gameObject.SetActive(false);
            blocker.SetAsLastSibling();
        }

        /// <summary>Открыть/закрыть меню по имени.</summary>
        public void ToggleMenu(string menu)
        {
            if (openMenu == menu)
            {
                CloseMenu();
                return;
            }
            CloseMenu();
            openMenu = menu;
            BuildDropdown(menu);
            if (dropdown != null)
            {
                dropdown.gameObject.SetActive(true);
                dropdown.SetAsLastSibling();
            }
            if (blocker != null)
            {
                blocker.gameObject.SetActive(true);
                blocker.SetAsLastSibling();
                if (dropdown != null) dropdown.SetAsLastSibling();
            }
        }

        /// <summary>Закрыть открытое меню.</summary>
        public void CloseMenu()
        {
            openMenu = "";
            if (dropdown != null) dropdown.gameObject.SetActive(false);
            if (blocker != null) blocker.gameObject.SetActive(false);
        }

        private void BuildDropdown(string menu)
        {
            if (dropdown == null)
            {
                GameObject go = new GameObject("KvMenuDropdown", typeof(Image));
                go.transform.SetParent(canvasRect, false);
                dropdown = (RectTransform)go.transform;
                dropdown.anchorMin = dropdown.anchorMax = new Vector2(0f, 1f);
                dropdown.pivot = new Vector2(0f, 1f);
                dropdownBg = go.GetComponent<Image>();
                dropdownBg.sprite = KvTheme.WhiteSprite;
                dropdownBg.color = KvTheme.PanelBg;
                dropdownBg.raycastTarget = true;
                Outline o = go.AddComponent<Outline>();
                o.effectColor = KvTheme.Border;
                o.effectDistance = new Vector2(1f, -1f);
                o.useGraphicAlpha = false;
            }

            foreach (GameObject row in dropdownRows)
                if (row != null) Destroy(row);
            dropdownRows.Clear();

            // Позиция — под кнопкой меню.
            float x = 0f;
            float width = 240f;
            foreach (string m in Menus)
            {
                float w = 10f + m.Length * 7.2f + 1f;
                if (m == menu) break;
                x += w;
            }
            dropdown.anchoredPosition = new Vector2(x, -Height);

            float y = 3f;
            string lastGroup = null;
            bool any = false;

            foreach (KvCommand c in KvCommands.All)
            {
                if (string.IsNullOrEmpty(c.MenuPath)) continue;
                string[] parts = c.MenuPath.Split('/');
                if (parts.Length < 2 || parts[0] != menu) continue;
                string group = parts.Length > 2 ? parts[1] : null;

                if (group != null && group != lastGroup)
                {
                    if (lastGroup != null) y = AddSeparator(y, width);
                    Text gt = KvTheme.CreateText(dropdown, "Group",
                        KvLoc.MenuGroup(group).ToUpperInvariant(),
                        KvTheme.FontSizeSmall - 1, TextAnchor.MiddleLeft, KvTheme.TextDisabled);
                    gt.rectTransform.anchorMin = new Vector2(0f, 1f);
                    gt.rectTransform.anchorMax = new Vector2(1f, 1f);
                    gt.rectTransform.pivot = new Vector2(0f, 1f);
                    gt.rectTransform.sizeDelta = new Vector2(0f, 14f);
                    gt.rectTransform.anchoredPosition = new Vector2(8f, -y);
                    dropdownRows.Add(gt.gameObject);
                    y += 14f;
                    lastGroup = group;
                }
                else if (group == null)
                {
                    lastGroup = null;
                }

                y = AddItem(c, c.LocalizedTitle, y, width);
                any = true;
            }

            if (!any)
            {
                y = AddItem(null, KvLoc.T("menu.empty", "Нет доступных пунктов"), y, width);
            }

            dropdown.sizeDelta = new Vector2(width, y + 4f);
        }

        private float AddSeparator(float y, float width)
        {
            Image sep = KvTheme.CreatePanel(dropdown, "Sep", KvTheme.Separator);
            sep.rectTransform.anchorMin = new Vector2(0f, 1f);
            sep.rectTransform.anchorMax = new Vector2(1f, 1f);
            sep.rectTransform.pivot = new Vector2(0f, 1f);
            sep.rectTransform.sizeDelta = new Vector2(-10f, 1f);
            sep.rectTransform.anchoredPosition = new Vector2(5f, -(y + 2f));
            dropdownRows.Add(sep.gameObject);
            return y + 5f;
        }

        private float AddItem(KvCommand command, string label, float y, float width)
        {
            const float rowH = 18f;
            bool enabled = command != null && command.Enabled;
            bool isStub = command != null && command.Stub;

            GameObject row = new GameObject("Item_" + label, typeof(Image), typeof(Button));
            row.transform.SetParent(dropdown, false);
            RectTransform rt = (RectTransform)row.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(-6f, rowH);
            rt.anchoredPosition = new Vector2(3f, -y);
            Image bg = row.GetComponent<Image>();
            bg.sprite = KvTheme.WhiteSprite;
            bg.color = new Color(0f, 0f, 0f, 0f);
            bg.raycastTarget = true;

            Color textColor = enabled ? KvTheme.TextMain : KvTheme.TextDisabled;

            // Галка состояния слева.
            if (command != null && command.IsChecked != null)
            {
                GameObject checkGo = new GameObject("Check", typeof(Image));
                checkGo.transform.SetParent(row.transform, false);
                Image check = checkGo.GetComponent<Image>();
                check.sprite = KvIcons.Get("check", 10);
                check.color = textColor;
                check.raycastTarget = false;
                check.rectTransform.anchorMin = check.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                check.rectTransform.pivot = new Vector2(0f, 0.5f);
                check.rectTransform.sizeDelta = new Vector2(10f, 10f);
                check.rectTransform.anchoredPosition = new Vector2(6f, 0f);
                checkGo.SetActive(command.Checked);
                row.AddComponent<KvMenuCheck>().Bind(checkGo);
            }

            Text t = KvTheme.CreateText(rt, "Label", label, KvTheme.FontSizeSmall,
                TextAnchor.MiddleLeft, textColor);
            t.rectTransform.anchorMin = new Vector2(0f, 0f);
            t.rectTransform.anchorMax = new Vector2(1f, 1f);
            t.rectTransform.offsetMin = new Vector2(22f, 0f);
            t.rectTransform.offsetMax = new Vector2(-46f, 0f);

            if (command != null && !string.IsNullOrEmpty(command.Hotkey))
            {
                Text hk = KvTheme.CreateText(rt, "Hotkey", command.Hotkey, KvTheme.FontSizeSmall,
                    TextAnchor.MiddleRight, KvTheme.TextDim);
                hk.rectTransform.anchorMin = new Vector2(1f, 0f);
                hk.rectTransform.anchorMax = new Vector2(1f, 1f);
                hk.rectTransform.pivot = new Vector2(1f, 0.5f);
                hk.rectTransform.sizeDelta = new Vector2(44f, 0f);
                hk.rectTransform.anchoredPosition = new Vector2(-4f, 0f);
            }

            Button btn = row.GetComponent<Button>();
            btn.targetGraphic = bg;
            btn.interactable = enabled;
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = KvTheme.Ratio(KvTheme.ButtonHover, KvTheme.PanelBg);
            cb.pressedColor = KvTheme.Ratio(KvTheme.ButtonPressed, KvTheme.PanelBg);
            cb.disabledColor = Color.white;
            btn.colors = cb;

            if (command != null)
            {
                string id = command.Id;
                btn.onClick.AddListener(delegate
                {
                    CloseMenu();
                    KvCommands.Invoke(id);
                    Refresh();
                });
            }
            else
            {
                btn.interactable = false;
            }

            if (isStub)
            {
                KvTooltipTarget tip = row.AddComponent<KvTooltipTarget>();
                tip.Set(command.LocalizedTitle, KvLoc.T("common.stub", "в разработке"), "");
                tip.stub = true;
            }

            dropdownRows.Add(row);
            return y + rowH + 1f;
        }

        /// <summary>Перерисовать галки и доступность пунктов меню.</summary>
        public void Refresh()
        {
            if (openMenu.Length > 0) BuildDropdown(openMenu);

            for (int i = 0; i < menuButtons.Count; i++)
            {
                bool open = i < Menus.Length && Menus[i] == openMenu;
                if (menuButtons[i] != null)
                    menuButtons[i].color = open ? KvTheme.ButtonHover : new Color(0f, 0f, 0f, 0f);
                if (menuTexts[i] != null)
                    menuTexts[i].color = open ? KvTheme.TextMain : KvTheme.TextDim;
            }
        }

        /// <summary>Перекрасить под текущую тему.</summary>
        public void Repaint()
        {
            if (root == null) return;
            Image bg = root.GetComponent<Image>();
            if (bg != null) bg.color = KvTheme.PanelHeader;
            if (dropdownBg != null) dropdownBg.color = KvTheme.PanelBg;
            if (infoText != null) infoText.color = KvTheme.TextDim;
            for (int i = 0; i < menuTexts.Count; i++)
                if (menuTexts[i] != null) menuTexts[i].color = KvTheme.TextDim;
            Outline o = dropdown != null ? dropdown.GetComponent<Outline>() : null;
            if (o != null) o.effectColor = KvTheme.Border;
        }

        /// <summary>Открытое меню (пусто — закрыто).</summary>
        public string OpenMenu { get { return openMenu; } }
        /// <summary>Сколько строк в открытом выпадающем списке (диагностика).</summary>
        public int DropdownRowCount { get { return dropdownRows.Count; } }

        private void Update()
        {
            // Клик по миру/панели закрывает меню (страховка, если блокер не поймал).
            if (openMenu.Length == 0) return;
            if (Input.GetKeyDown(KeyCode.Escape)) CloseMenu();
        }
    }

    /// <summary>Галка состояния пункта меню.</summary>
    internal class KvMenuCheck : MonoBehaviour
    {
        private GameObject check;

        public void Bind(GameObject checkGo)
        {
            check = checkGo;
        }

        public void Set(bool value)
        {
            if (check != null) check.SetActive(value);
        }
    }
}
