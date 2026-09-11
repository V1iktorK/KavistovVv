using UnityEngine;
using UnityEngine.UI;

namespace KompasUI
{
    /// <summary>
    /// Нижняя панель (статус-бар): по центру внизу висят «болванки-ссылки» —
    /// Название организации, Email, Номер телефона, Сайт.
    /// </summary>
    public class StatusBar : MonoBehaviour
    {
        private Text statusText;

        public void Build(RectTransform parent)
        {
            Image panel = KompasTheme.CreatePanel(parent, "StatusBar", KompasTheme.PanelHeader);
            RectTransform pr = panel.rectTransform;
            pr.anchorMin = new Vector2(0f, 0f);
            pr.anchorMax = new Vector2(1f, 0f);
            pr.pivot = new Vector2(0.5f, 0f);
            pr.sizeDelta = new Vector2(0f, 30f);
            pr.anchoredPosition = new Vector2(0f, 0f);

            // Слева — онлайн-статус прицела (достижимость/запас).
            statusText = KompasTheme.CreateText(panel.rectTransform, "AimStatus", "",
                KompasTheme.FontSizeSmall, TextAnchor.MiddleLeft, KompasTheme.TextDim);
            RectTransform sr = statusText.rectTransform;
            sr.anchorMin = new Vector2(0f, 0f);
            sr.anchorMax = new Vector2(0f, 1f);
            sr.pivot = new Vector2(0f, 0.5f);
            sr.anchoredPosition = new Vector2(12f, 0f);
            sr.sizeDelta = new Vector2(700f, 0f);

            // Центральный блок ссылок (примерно 2px «выше нижнего края» — сам бар у края).
            GameObject centerGo = new GameObject("Links", typeof(HorizontalLayoutGroup));
            centerGo.transform.SetParent(panel.transform, false);
            RectTransform cr = (RectTransform)centerGo.transform;
            cr.anchorMin = new Vector2(0.5f, 0f);
            cr.anchorMax = new Vector2(0.5f, 1f);
            cr.pivot = new Vector2(0.5f, 0.5f);
            cr.sizeDelta = new Vector2(760f, 0f);

            HorizontalLayoutGroup hlg = centerGo.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 18f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            AddLink(cr, "ООО «ПромРобоТех»", "organization");
            AddSeparator(cr);
            AddLink(cr, "info@promrobotec.ru", "email");
            AddSeparator(cr);
            AddLink(cr, "+7 (495) 123-45-67", "phone");
            AddSeparator(cr);
            AddLink(cr, "www.promrobotec.ru", "site");
        }

        /// <summary>Онлайн-статус прицела: текст + цвет (зелёный/жёлтый/красный).</summary>
        public void SetAimStatus(string text, Color color)
        {
            if (statusText == null) return;
            statusText.text = text;
            statusText.color = color;
        }

        private void AddSeparator(RectTransform parent)
        {
            Image sep = KompasTheme.CreatePanel(parent, "Sep", KompasTheme.TextDim);
            sep.rectTransform.sizeDelta = new Vector2(1f, 14f);
        }

        private void AddLink(RectTransform parent, string text, string key)
        {
            Button b = KompasTheme.CreateButton(parent, "Link_" + key, text, () =>
            {
                Debug.Log($"[KompasUI] Ссылка-заглушка '{key}': {text}");
            }, height: 22, small: true);
            LayoutElement le = b.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 22f;
            le.minWidth = text.Length * 6f + 20f;
        }
    }
}
