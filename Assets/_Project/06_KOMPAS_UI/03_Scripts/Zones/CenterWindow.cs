using UnityEngine;
using UnityEngine.UI;

namespace KompasUI
{
    /// <summary>
    /// Центральное окно (главная сцена): не перекрывает обзор.
    /// Показывает подсказки режима и предпросмотр размещаемого объекта.
    /// </summary>
    public class CenterWindow : MonoBehaviour
    {
        private Text hintText;
        private GameObject preview;
        private Renderer previewRenderer;
        private SpawnKind activeKind = SpawnKind.None;

        public SpawnKind ActiveKind => activeKind;

        public void Build(RectTransform parent)
        {
            // Небольшая плашка-подсказка вверху по центру (не мешает обзору).
            hintText = KompasTheme.CreateText(parent, "CenterHint", "",
                KompasTheme.FontSizeSmall, TextAnchor.MiddleCenter, KompasTheme.TextDim);
            RectTransform hr = hintText.rectTransform;
            hr.anchorMin = new Vector2(0.5f, 1f);
            hr.anchorMax = new Vector2(0.5f, 1f);
            hr.pivot = new Vector2(0.5f, 1f);
            hr.anchoredPosition = new Vector2(0f, -52f);
            hr.sizeDelta = new Vector2(500f, 24f);
        }

        public void SetHint(string text)
        {
            if (hintText != null) hintText.text = text;
        }

        public void StartPlacement(SpawnKind kind)
        {
            activeKind = kind;
            if (kind == SpawnKind.None)
            {
                SetHint("");
                DestroyPreview();
                return;
            }

            SetHint(kind == SpawnKind.Table
                ? "Укажите место для стола: наведите на поверхность и нажмите ЛКМ. Esc — отмена."
                : "Укажите место для робота: наведите на пол и нажмите ЛКМ. Esc — отмена.");

            CreatePreview(kind);
        }

        public void UpdatePreview(Vector3 position, bool valid)
        {
            if (preview == null) return;
            preview.transform.position = position;
            if (previewRenderer != null)
            {
                Color c = valid
                    ? new Color(0.3f, 1f, 0.4f, 0.45f)
                    : new Color(1f, 0.3f, 0.3f, 0.45f);
                previewRenderer.material.color = c;
            }
        }

        private void CreatePreview(SpawnKind kind)
        {
            DestroyPreview();

            if (kind == SpawnKind.Table)
            {
                preview = GameObject.CreatePrimitive(PrimitiveType.Cube);
                preview.name = "Preview_Table";
                preview.transform.localScale = new Vector3(1.2f, 0.05f, 0.8f);
            }
            else if (kind == SpawnKind.Robot)
            {
                preview = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                preview.name = "Preview_Robot";
                preview.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            }

            if (preview == null) return;
            previewRenderer = preview.GetComponent<Renderer>();
            if (previewRenderer != null)
            {
                previewRenderer.material = new Material(Shader.Find("Sprites/Default"));
            }
        }

        public void DestroyPreview()
        {
            if (preview != null) Destroy(preview);
            preview = null;
            previewRenderer = null;
        }
    }
}
