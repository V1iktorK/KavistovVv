using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace KazistovVvUI
{
    /// <summary>
    /// НАВИГАЦИЯ ПО ИНТЕРФЕЙСУ С КЛАВИАТУРЫ (ЭТАП 11): Tab / Shift+Tab — вперёд-назад,
    /// стрелки — по соседним кнопкам (геометрически, как в панелях инструментов),
    /// Enter или пробел — нажать, Esc — снять фокус. Фокус обводится рамкой.
    ///
    /// Работает ТОЛЬКО в режиме интерфейса (курсор свободен, панели видны) и только когда
    /// настройка «Навигация с клавиатуры» включена — в телеоперации клавиши принадлежат
    /// роботу, как и раньше.
    ///
    /// ВАЖНО: пока навигация включена, Tab занят фокусом, поэтому ПЕРЕКЛЮЧЕНИЕ ИНТЕРФЕЙСА
    /// переезжает на Esc (показать) и на команду «Показать / скрыть интерфейс» (тулбар/меню,
    /// горячая клавиша TAB указана в подсказке команды). Это единственное изменение
    /// поведения клавиши, и оно выключается вместе с настройкой.
    /// </summary>
    public class KvKeyboardNav : MonoBehaviour
    {
        /// <summary>
        /// Навигация забирает Tab себе. Пока true, контроллер камеры НЕ переключает
        /// режим интерфейса по Tab (иначе одно нажатие делало бы два дела).
        /// </summary>
        public static bool TabHandledByNavigation
        {
            get { return instance != null && instance.enabled && KvSettings.KeyboardNav; }
        }

        private static KvKeyboardNav instance;

        private KazistovVvUIManager ui;
        private KvToolbar toolbar;
        private RectTransform layer;
        private Image frame;
        private readonly List<RectTransform> focusables = new List<RectTransform>();
        private readonly List<KvIconButton> focusButtons = new List<KvIconButton>();
        private int index = -1;
        private float rebuildTimer;

        /// <summary>Индекс кнопки в фокусе (−1 — фокуса нет).</summary>
        public int FocusIndex { get { return index; } }
        /// <summary>Сколько элементов доступно навигации (диагностика).</summary>
        public int FocusableCount { get { return focusables.Count; } }
        /// <summary>Фокус есть.</summary>
        public bool HasFocus { get { return index >= 0 && index < focusables.Count; } }
        /// <summary>Подпись кнопки в фокусе (диагностика).</summary>
        public string FocusLabel
        {
            get
            {
                if (!HasFocus) return "";
                KvIconButton b = focusButtons[index];
                if (b == null || b.Tooltip == null) return "";
                return b.Tooltip.title;
            }
        }

        /// <summary>Создать навигацию (один раз на оболочку).</summary>
        public static KvKeyboardNav Create(RectTransform canvas, KazistovVvUIManager manager,
            KvToolbar bar)
        {
            GameObject go = new GameObject("KvKeyboardNav", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            RectTransform rt = (RectTransform)go.transform;
            KvTheme.Stretch(rt);
            instance = go.AddComponent<KvKeyboardNav>();
            instance.ui = manager;
            instance.toolbar = bar;
            instance.layer = rt;
            instance.Build();
            instance.enabled = KvSettings.KeyboardNav;
            return instance;
        }

        /// <summary>Включить/выключить навигацию (настройка «Навигация с клавиатуры»).</summary>
        public void SetEnabled(bool value)
        {
            enabled = value;
            if (value) RebuildFocusables();
            else ClearFocus();
        }

        private void Build()
        {
            GameObject go = new GameObject("FocusFrame", typeof(Image));
            go.transform.SetParent(transform, false);
            frame = go.GetComponent<Image>();
            frame.sprite = KvTheme.WhiteSprite;
            frame.color = new Color(KvTheme.Accent.r, KvTheme.Accent.g, KvTheme.Accent.b, 0.22f);
            frame.raycastTarget = false;
            Outline o = go.AddComponent<Outline>();
            o.effectColor = KvTheme.Accent;
            o.effectDistance = new Vector2(2f, -2f);
            o.useGraphicAlpha = false;
            go.SetActive(false);
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void Update()
        {
            if (ui == null || toolbar == null) return;

            // Навигация нужна только в режиме интерфейса: в телеоперации клавиши — роботу.
            bool uiMode = Cursor.lockState != CursorLockMode.Locked && ui != null && ui.uiVisible;
            if (!uiMode)
            {
                if (HasFocus) ClearFocus();
                return;
            }
            // Пока текст вводится (палитра, поиск биндов, переименование) — не мешаем.
            if (IsTypingInField()) return;

            rebuildTimer -= Time.unscaledDeltaTime;
            if (rebuildTimer <= 0f)
            {
                rebuildTimer = 0.5f;
                RebuildFocusables();
            }

            bool tab = KeyDown(KeyCode.Tab);
            bool shift = KeyHeld(KeyCode.LeftShift) || KeyHeld(KeyCode.RightShift);
            if (tab)
            {
                if (focusables.Count > 0) Move(shift ? -1 : 1);
                return;
            }
            if (!HasFocus) return;

            if (KeyDown(KeyCode.RightArrow)) MoveGeometric(new Vector2(1f, 0f));
            else if (KeyDown(KeyCode.LeftArrow)) MoveGeometric(new Vector2(-1f, 0f));
            else if (KeyDown(KeyCode.UpArrow)) MoveGeometric(new Vector2(0f, 1f));
            else if (KeyDown(KeyCode.DownArrow)) MoveGeometric(new Vector2(0f, -1f));
            else if (KeyDown(KeyCode.Return) || KeyDown(KeyCode.KeypadEnter) || KeyDown(KeyCode.Space))
                Activate();
            else if (KeyDown(KeyCode.Escape)) ClearFocus();
        }

        private static bool IsTypingInField()
        {
            EventSystem es = EventSystem.current;
            if (es == null) return false;
            GameObject selected = es.currentSelectedGameObject;
            if (selected == null) return false;
            InputField field = selected.GetComponent<InputField>();
            return field != null && field.isFocused;
        }

        private bool KeyDown(KeyCode code)
        {
            try
            {
                if (Keyboard.current != null)
                {
                    var control = Keyboard.current[KeyCodeToKey(code)];
                    if (control != null) return control.wasPressedThisFrame;
                }
                return Input.GetKeyDown(code);
            }
            catch
            {
                return false;
            }
        }

        private bool KeyHeld(KeyCode code)
        {
            try
            {
                if (Keyboard.current != null)
                {
                    var control = Keyboard.current[KeyCodeToKey(code)];
                    if (control != null) return control.isPressed;
                }
                return Input.GetKey(code);
            }
            catch
            {
                return false;
            }
        }

        private static UnityEngine.InputSystem.Key KeyCodeToKey(KeyCode code)
        {
            switch (code)
            {
                case KeyCode.Tab: return UnityEngine.InputSystem.Key.Tab;
                case KeyCode.Return: return UnityEngine.InputSystem.Key.Enter;
                case KeyCode.KeypadEnter: return UnityEngine.InputSystem.Key.NumpadEnter;
                case KeyCode.Space: return UnityEngine.InputSystem.Key.Space;
                case KeyCode.Escape: return UnityEngine.InputSystem.Key.Escape;
                case KeyCode.LeftArrow: return UnityEngine.InputSystem.Key.LeftArrow;
                case KeyCode.RightArrow: return UnityEngine.InputSystem.Key.RightArrow;
                case KeyCode.UpArrow: return UnityEngine.InputSystem.Key.UpArrow;
                case KeyCode.DownArrow: return UnityEngine.InputSystem.Key.DownArrow;
                case KeyCode.LeftShift: return UnityEngine.InputSystem.Key.LeftShift;
                case KeyCode.RightShift: return UnityEngine.InputSystem.Key.RightShift;
                default: return UnityEngine.InputSystem.Key.None;
            }
        }

        /// <summary>Собрать список элементов, доступных навигации (кнопки тулбара).</summary>
        private void RebuildFocusables()
        {
            focusables.Clear();
            focusButtons.Clear();
            if (toolbar == null) return;
            for (int i = 0; i < toolbar.ButtonCount; i++)
            {
                KvIconButton button = toolbar.ButtonAt(i);
                if (button == null || !button.IsEnabled) continue;
                RectTransform rt = button.transform as RectTransform;
                if (rt == null || !rt.gameObject.activeInHierarchy) continue;
                focusables.Add(rt);
                focusButtons.Add(button);
            }
        }

        private void Move(int delta)
        {
            if (focusables.Count == 0) return;
            index = (index + delta) % focusables.Count;
            if (index < 0) index += focusables.Count;
            Paint();
        }

        /// <summary>Перейти к ближайшей кнопке в заданном направлении (по геометрии).</summary>
        private void MoveGeometric(Vector2 direction)
        {
            if (!HasFocus) { Move(1); return; }
            Vector2 origin = focusables[index].TransformPoint(focusables[index].rect.center);
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < focusables.Count; i++)
            {
                if (i == index) continue;
                Vector2 point = focusables[i].TransformPoint(focusables[i].rect.center);
                Vector2 delta = point - origin;
                float forward = Vector2.Dot(delta, direction);
                if (forward <= 4f) continue;
                float lateral = Mathf.Abs(Vector2.Dot(delta, new Vector2(-direction.y, direction.x)));
                float score = forward + lateral * 2f;
                if (score >= bestDistance) continue;
                bestDistance = score;
                best = i;
            }
            if (best < 0) return;
            index = best;
            Paint();
        }

        /// <summary>Нажать кнопку в фокусе.</summary>
        public bool Activate()
        {
            if (!HasFocus) return false;
            KvIconButton button = focusButtons[index];
            if (button == null || button.Button == null) return false;
            button.Button.onClick.Invoke();
            return true;
        }

        /// <summary>Снять фокус.</summary>
        public void ClearFocus()
        {
            index = -1;
            if (frame != null) frame.gameObject.SetActive(false);
        }

        /// <summary>Поставить фокус на кнопку по id команды (диагностика/тесты).</summary>
        public bool FocusCommand(string commandId)
        {
            if (toolbar == null) return false;
            RebuildFocusables();      // список мог устареть (пересборка панели/смена раскладки)
            IReadOnlyList<string> ids = toolbar.ButtonIds;
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] != commandId) continue;
                for (int f = 0; f < focusButtons.Count; f++)
                {
                    if (focusButtons[f] != toolbar.ButtonAt(i)) continue;
                    index = f;
                    Paint();
                    return true;
                }
            }
            return false;
        }

        private void Paint()
        {
            if (!HasFocus || frame == null) return;
            RectTransform target = focusables[index];
            RectTransform parent = (RectTransform)frame.transform.parent;
            Vector3 world = target.TransformPoint(target.rect.min);
            Vector3 worldMax = target.TransformPoint(target.rect.max);
            Vector2 localMin = parent.InverseTransformPoint(world);
            Vector2 localMax = parent.InverseTransformPoint(worldMax);

            frame.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            frame.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            frame.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            frame.rectTransform.sizeDelta = new Vector2(Mathf.Abs(localMax.x - localMin.x) + 2f,
                Mathf.Abs(localMax.y - localMin.y) + 2f);
            frame.rectTransform.anchoredPosition = (localMin + localMax) * 0.5f;
            frame.gameObject.SetActive(true);
            frame.transform.SetAsLastSibling();
        }

        /// <summary>Перекрасить рамку под текущую тему.</summary>
        public void Repaint()
        {
            if (frame == null) return;
            frame.color = new Color(KvTheme.Accent.r, KvTheme.Accent.g, KvTheme.Accent.b, 0.22f);
            Outline o = frame.GetComponent<Outline>();
            if (o != null) o.effectColor = KvTheme.Accent;
        }
    }
}
