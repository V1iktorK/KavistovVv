using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace KompasUI
{
    /// <summary>
    /// Модальное окно «Настройки» (кнопка в TopBar): популярные параметры
    /// применяются СРАЗУ:
    ///   * Скорость движения роботов (SettingsData.robotSpeed);
    ///   * Чувствительность мыши (FreeFlyCameraController.lookSensitivity);
    ///   * Инвертировать Y (камера);
    ///   * Сброс к значениям по умолчанию; закрытие — кнопка или Esc.
    /// </summary>
    public class SettingsWindow : MonoBehaviour
    {
        private RectTransform overlay;
        private bool open;
        private bool built;

        private SettingsData data;
        private FreeFlyCameraController camControl;

        private readonly List<System.Action> resetters = new List<System.Action>();

        public bool IsOpen { get { return open; } }

        public void Build(RectTransform canvas)
        {
            if (built) return;
            built = true;

            overlay = (RectTransform)KompasTheme.CreatePanel(canvas, "SettingsOverlay",
                new Color(0f, 0f, 0f, 0.35f)).transform;
            KompasTheme.Stretch(overlay, 0, 0, 0, 0);

            BuildWindow();

            // Оверлей поверх всех панелей (последний sibling).
            overlay.SetAsLastSibling();
            Close();
        }

        // ------------------------------------------------------------- построение

        private void BuildWindow()
        {
            Image window = KompasTheme.CreatePanel(overlay, "SettingsWindow", KompasTheme.PanelHeader);
            RectTransform w = window.rectTransform;
            w.anchorMin = new Vector2(0.5f, 0.5f);
            w.anchorMax = new Vector2(0.5f, 0.5f);
            w.pivot = new Vector2(0.5f, 0.5f);
            w.sizeDelta = new Vector2(560f, 230f);
            w.anchoredPosition = Vector2.zero;

            // Заголовок.
            Text title = KompasTheme.CreateText(w, "Title", "Настройки", 16, TextAnchor.MiddleLeft,
                KompasTheme.TextMain);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0f, 1f);
            title.rectTransform.offsetMin = new Vector2(14f, -30f);
            title.rectTransform.offsetMax = new Vector2(-14f, -4f);

            float y = 42f;
            float robotSpeed = Data != null ? Data.robotSpeed : 1f;
            float sens = CamControl != null ? CamControl.lookSensitivity : 2f;

            y += AddSliderRow(w, "Скорость робота", 0.2f, 3f, robotSpeed, "0.00", y,
                v => { if (Data != null) Data.robotSpeed = v; });
            y += AddSliderRow(w, "Чувствительность мыши", 0.2f, 5f, sens, "0.00", y,
                v =>
                {
                    if (CamControl != null) CamControl.lookSensitivity = v;
                    if (Data != null) Data.mouseSensitivity = v;
                });
            y += AddToggleRow(w, "Инвертировать Y", y, v =>
            {
                if (CamControl != null) CamControl.invertY = v;
                if (Data != null) Data.invertY = v;
            });

            // Подсказка.
            Text hint = KompasTheme.CreateText(w, "Hint",
                "Параметры применяются сразу", KompasTheme.FontSizeSmall,
                TextAnchor.MiddleCenter, KompasTheme.TextDim);
            hint.rectTransform.anchorMin = new Vector2(0f, 0f);
            hint.rectTransform.anchorMax = new Vector2(1f, 0f);
            hint.rectTransform.pivot = new Vector2(0.5f, 0f);
            hint.rectTransform.offsetMin = new Vector2(10f, 44f);
            hint.rectTransform.offsetMax = new Vector2(-10f, 64f);

            // Кнопки: Сброс | Закрыть (справа внизу).
            Button reset = KompasTheme.CreateButton(w, "Btn_Reset", "Сброс", ResetDefaults, 26);
            RectTransform br = (RectTransform)reset.transform;
            br.anchorMin = new Vector2(1f, 0f);
            br.anchorMax = new Vector2(1f, 0f);
            br.pivot = new Vector2(1f, 0f);
            br.sizeDelta = new Vector2(90f, 26f);
            br.anchoredPosition = new Vector2(-116f, 12f);

            Button close = KompasTheme.CreateButton(w, "Btn_Close", "Закрыть", Close, 26);
            RectTransform cr = (RectTransform)close.transform;
            cr.anchorMin = new Vector2(1f, 0f);
            cr.anchorMax = new Vector2(1f, 0f);
            cr.pivot = new Vector2(1f, 0f);
            cr.sizeDelta = new Vector2(90f, 26f);
            cr.anchoredPosition = new Vector2(-14f, 12f);
            close.GetComponent<Image>().color = KompasTheme.ButtonActive;
        }

        private float AddSliderRow(RectTransform parent, string label, float min, float max,
            float initial, string fmt, float topY, System.Action<float> onChanged)
        {
            RectTransform row = (RectTransform)KompasTheme.CreatePanel(parent, "Row_" + label,
                new Color(0f, 0f, 0f, 0f)).transform;
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.offsetMin = new Vector2(14f, -topY - 30f);
            row.offsetMax = new Vector2(-14f, -topY);

            Text lbl = KompasTheme.CreateText(row, "Label", label, KompasTheme.FontSize,
                TextAnchor.MiddleLeft, KompasTheme.TextMain);
            lbl.rectTransform.anchorMin = Vector2.zero;
            lbl.rectTransform.anchorMax = new Vector2(0.42f, 1f);

            Slider slider = CreateSlider(row, label, min, max, out Text valueText);
            valueText.text = initial.ToString(fmt);
            slider.SetValueWithoutNotify(initial);
            slider.onValueChanged.AddListener(v =>
            {
                valueText.text = v.ToString(fmt);
                onChanged?.Invoke(v);
            });
            resetters.Add(() =>
            {
                slider.SetValueWithoutNotify(initial);
                valueText.text = initial.ToString(fmt);
                onChanged?.Invoke(initial);
            });
            return 34f;
        }

        /// <summary>Создаёт слайдер (0.42..0.88 ширины строки) + текстовое значение (0.88..1).</summary>
        private static Slider CreateSlider(RectTransform parent, string name, float min, float max,
            out Text valueText)
        {
            GameObject valueGo = new GameObject("Value_" + name, typeof(Text));
            valueGo.transform.SetParent(parent, false);
            valueText = valueGo.GetComponent<Text>();
            valueText.font = KompasTheme.Font;
            valueText.fontSize = KompasTheme.FontSize;
            valueText.alignment = TextAnchor.MiddleRight;
            valueText.color = KompasTheme.Accent;
            valueText.horizontalOverflow = HorizontalWrapMode.Overflow;
            valueText.verticalOverflow = VerticalWrapMode.Overflow;
            valueText.raycastTarget = false;
            RectTransform vrt = (RectTransform)valueGo.transform;
            vrt.anchorMin = new Vector2(0.88f, 0f);
            vrt.anchorMax = new Vector2(1f, 1f);

            GameObject sliderGo = new GameObject("Slider_" + name, typeof(Image), typeof(Slider));
            sliderGo.transform.SetParent(parent, false);
            Image bg = sliderGo.GetComponent<Image>();
            bg.sprite = UIFactory.GetSprite();
            bg.color = KompasTheme.ButtonBg;

            Slider slider = sliderGo.GetComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;

            GameObject fillGo = new GameObject("Fill", typeof(Image));
            fillGo.transform.SetParent(sliderGo.transform, false);
            Image fill = fillGo.GetComponent<Image>();
            fill.sprite = UIFactory.GetSprite();
            fill.color = new Color(KompasTheme.Accent.r, KompasTheme.Accent.g,
                KompasTheme.Accent.b, 0.9f);
            RectTransform frt = (RectTransform)fillGo.transform;
            frt.anchorMin = new Vector2(0f, 0f);
            frt.anchorMax = new Vector2(1f, 1f);
            frt.offsetMin = new Vector2(2f, 2f);
            frt.offsetMax = new Vector2(-2f, -2f);
            slider.fillRect = frt;
            slider.targetGraphic = bg;

            RectTransform srt = (RectTransform)sliderGo.transform;
            srt.anchorMin = new Vector2(0.42f, 0f);
            srt.anchorMax = new Vector2(0.88f, 1f);
            srt.offsetMin = new Vector2(0f, 4f);
            srt.offsetMax = new Vector2(-2f, -4f);
            return slider;
        }

        private float AddToggleRow(RectTransform parent, string label, float topY,
            System.Action<bool> onChanged)
        {
            RectTransform row = (RectTransform)KompasTheme.CreatePanel(parent, "Row_" + label,
                new Color(0f, 0f, 0f, 0f)).transform;
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.offsetMin = new Vector2(14f, -topY - 30f);
            row.offsetMax = new Vector2(-14f, -topY);

            Text lbl = KompasTheme.CreateText(row, "Label", label, KompasTheme.FontSize,
                TextAnchor.MiddleLeft, KompasTheme.TextMain);
            lbl.rectTransform.anchorMin = Vector2.zero;
            lbl.rectTransform.anchorMax = new Vector2(0.42f, 1f);

            GameObject toggleGo = new GameObject("Toggle_" + label, typeof(Image), typeof(Toggle));
            toggleGo.transform.SetParent(row, false);
            Image tgImg = toggleGo.GetComponent<Image>();
            tgImg.sprite = UIFactory.GetSprite();
            Toggle tgl = toggleGo.GetComponent<Toggle>();
            tgl.targetGraphic = tgImg;
            RectTransform trt = (RectTransform)toggleGo.transform;
            trt.anchorMin = new Vector2(0.42f, 0.5f);
            trt.anchorMax = new Vector2(0.42f, 0.5f);
            trt.pivot = new Vector2(0f, 0.5f);
            trt.sizeDelta = new Vector2(64f, 24f);
            trt.anchoredPosition = Vector2.zero;

            bool initial = CamControl != null && CamControl.invertY;
            tgl.SetIsOnWithoutNotify(initial);
            tgImg.color = initial ? KompasTheme.ButtonActive : KompasTheme.ButtonBg;
            tgl.onValueChanged.AddListener(v =>
            {
                tgImg.color = v ? KompasTheme.ButtonActive : KompasTheme.ButtonBg;
                onChanged?.Invoke(v);
            });
            resetters.Add(() =>
            {
                tgl.SetIsOnWithoutNotify(initial);
                tgImg.color = initial ? KompasTheme.ButtonActive : KompasTheme.ButtonBg;
                onChanged?.Invoke(initial);
            });
            return 34f;
        }

        // ------------------------------------------------------------- состояние

        private SettingsData Data
        {
            get
            {
                if (data == null)
                {
                    data = SettingsData.Instance != null
                        ? SettingsData.Instance
                        : ScriptableObject.CreateInstance<SettingsData>(); // runtime, до конца сессии
                }
                return data;
            }
        }

        private FreeFlyCameraController CamControl
        {
            get
            {
                if (camControl == null)
                {
                    Camera cam = Camera.main != null ? Camera.main
                        : Object.FindAnyObjectByType<Camera>();
                    if (cam != null) camControl = cam.GetComponent<FreeFlyCameraController>();
                }
                return camControl;
            }
        }

        private void ResetDefaults()
        {
            foreach (System.Action reset in resetters)
                reset();
        }

        public void Toggle()
        {
            if (open) Close(); else Open();
        }

        public void Open()
        {
            if (overlay == null) return;
            overlay.SetAsLastSibling();
            open = true;
            overlay.gameObject.SetActive(true);
        }

        public void Close()
        {
            if (overlay == null) return;
            open = false;
            overlay.gameObject.SetActive(false);
        }

        void Update()
        {
            if (!open) return;

            bool escDown = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                           || (Keyboard.current == null && LegacyEsc());
            if (escDown) Close();
        }

        private static bool LegacyEsc()
        {
            try { return Input.GetKeyDown(KeyCode.Escape); }
            catch { return false; }
        }
    }
}
