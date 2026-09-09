using UnityEngine;
using UnityEngine.UI;

namespace KompasUI
{
    /// <summary>
    /// Центральное окно (главная сцена): подсказки режимов и предпросмотр размещения.
    /// Для робота поддерживается выбор направления в горизонтали:
    ///   ←/→ или A/D (мышь/клавиатура), левый стик X (геймпад) — вращение;
    ///   при приближении к углу, кратному 90°, направление «примагничивается».
    /// </summary>
    public class CenterWindow : MonoBehaviour
    {
        private Text hintText;
        private GameObject preview;
        private Renderer previewRenderer;
        private SpawnKind activeKind = SpawnKind.None;

        public SpawnKind ActiveKind => activeKind;

        // Режим робота: угол поворота в горизонтали
        public float RobotYaw { get; private set; }

        private float yawSnapThreshold = 6f; // градусов, в пределах которых «прилипаем» к 90°

        public void Build(RectTransform parent)
        {
            hintText = KompasTheme.CreateText(parent, "CenterHint", "",
                KompasTheme.FontSizeSmall, TextAnchor.MiddleCenter, KompasTheme.TextDim);
            RectTransform hr = hintText.rectTransform;
            hr.anchorMin = new Vector2(0.5f, 1f);
            hr.anchorMax = new Vector2(0.5f, 1f);
            hr.pivot = new Vector2(0.5f, 1f);
            hr.anchoredPosition = new Vector2(0f, -52f);
            hr.sizeDelta = new Vector2(640f, 24f);
        }

        public void SetHint(string text)
        {
            if (hintText != null) hintText.text = text;
        }

        public void StartPlacement(SpawnKind kind)
        {
            activeKind = kind;
            RobotYaw = 0f;
            if (kind == SpawnKind.None)
            {
                SetHint("");
                DestroyPreview();
                return;
            }

            SetHint(kind == SpawnKind.Table
                ? "Стол: наведите на поверхность, ЛКМ/Enter — поставить. Esc — отмена."
                : "Робот: ←/→ или A/D — направление (магнит к 90°), ЛКМ/Enter — поставить в центр стола. Esc — отмена.");

            CreatePreview(kind);
        }

        /// <summary>Вращение робота в горизонтали (вызывается UIManager'ом из ввода).</summary>
        public void RotateRobot(float deltaDegrees)
        {
            if (activeKind != SpawnKind.Robot) return;
            RobotYaw = (RobotYaw + deltaDegrees) % 360f;
            if (RobotYaw < 0f) RobotYaw += 360f;
            ApplySnap();
        }

        private void ApplySnap()
        {
            // Магнит: если угол близок к кратному 90 — прилипаем.
            float nearest = Mathf.Round(RobotYaw / 90f) * 90f;
            if (Mathf.Abs(RobotYaw - nearest) <= yawSnapThreshold)
            {
                RobotYaw = nearest;
            }
        }

        public void UpdatePreview(Vector3 position, bool valid)
        {
            if (preview == null) return;
            preview.transform.position = position;
            // Направление робота — вращение вокруг вертикали
            Vector3 euler = preview.transform.eulerAngles;
            euler.y = RobotYaw;
            preview.transform.eulerAngles = euler;

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
                // Тело робота
                preview = new GameObject("Preview_Robot");
                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                body.transform.SetParent(preview.transform, false);
                body.transform.localPosition = Vector3.up * 0.4f;
                body.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
                // «Нос» — направление робота
                GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
                nose.transform.SetParent(preview.transform, false);
                nose.transform.localPosition = new Vector3(0f, 0.4f, 0.75f);
                nose.transform.localScale = new Vector3(0.18f, 0.18f, 0.6f);
            }

            if (preview == null) return;
            foreach (Renderer r in preview.GetComponentsInChildren<Renderer>())
            {
                r.material = new Material(Shader.Find("Sprites/Default"));
            }
            previewRenderer = preview.GetComponentInChildren<Renderer>();
        }

        public void DestroyPreview()
        {
            if (preview != null) Destroy(preview);
            preview = null;
            previewRenderer = null;
        }
    }
}
