using UnityEngine;

namespace KazistovVvUI
{
    /// <summary>Направление, в котором тянется граница окна (ЭТАП 4).</summary>
    public enum KvResizeEdge
    {
        None = 0,
        Left,
        Right,
        Top,
        Bottom,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    /// <summary>
    /// Курсоры изменения размера окна (ЭТАП 4): «↔», «↕» и две диагонали. Текстуры
    /// рисуются КОДОМ (как иконки), файлов-ассетов нет; курсом управляет
    /// <see cref="Set"/> при наведении на границу и <see cref="Reset"/> при уходе.
    ///
    /// §26: запись формы курсора идёт через <see cref="KvMouseCursor"/> — он ЕДИНСТВЕННЫЙ,
    /// кто пишет в `Cursor.*` (включая `Cursor.SetCursor`). Своя иконка
    /// (<see cref="KvVirtualCursor"/>) для основного режима не используется: если она
    /// включена флагом <see cref="KvMouseCursor.InGameIconInCameraMode"/>, форма меняется
    /// и у неё — поэтому вызов `SetShape` сохранён.
    /// </summary>
    public static class KvCursors
    {
        private const int Size = 32;

        private static Texture2D horizontal;
        private static Texture2D vertical;
        private static Texture2D diagonalUp;     // ↙ ↗
        private static Texture2D diagonalDown;   // ↖ ↘

        /// <summary>Поставить курсор под направление границы.</summary>
        public static void Set(KvResizeEdge edge)
        {
            if (edge == KvResizeEdge.None)
            {
                Reset();
                return;
            }
            if (KvMouseCursor.Captured) return;

            Texture2D tex = TextureFor(edge);
            if (tex == null) return;
            // §26: форма подменяется у ОБЕИХ иконок (какая сейчас видна, та и покажет «↔»),
            // но записи в Cursor.* делает только KvMouseCursor.
            KvVirtualCursor.SetShape(tex);
            KvMouseCursor.SetSystemCursorShape(tex, new Vector2(Size * 0.5f, Size * 0.5f));
        }

        /// <summary>Вернуть обычный курсор.</summary>
        public static void Reset()
        {
            if (KvMouseCursor.Captured) return;
            KvVirtualCursor.SetShape(null);
            KvMouseCursor.ResetSystemCursorShape();
        }

        /// <summary>
        /// Вернуть обычную ФОРМУ, не трогая системный курсор: вызывается при захвате курсора
        /// (телеоперация), чтобы не осталось «залипшей» стрелки изменения размера.
        /// </summary>
        public static void ResetShapeOnly()
        {
            KvVirtualCursor.SetShape(null);
        }

        private static Texture2D TextureFor(KvResizeEdge edge)
        {
            switch (edge)
            {
                case KvResizeEdge.Left:
                case KvResizeEdge.Right:
                    if (horizontal == null) horizontal = Build(1f, 0f);
                    return horizontal;
                case KvResizeEdge.Top:
                case KvResizeEdge.Bottom:
                    if (vertical == null) vertical = Build(0f, 1f);
                    return vertical;
                case KvResizeEdge.BottomLeft:
                case KvResizeEdge.TopRight:
                    if (diagonalUp == null) diagonalUp = Build(0.7071f, 0.7071f);
                    return diagonalUp;
                default:
                    if (diagonalDown == null) diagonalDown = Build(0.7071f, -0.7071f);
                    return diagonalDown;
            }
        }

        /// <summary>Нарисовать двойную стрелку вдоль направления (dx, dy) — белая с чёрной обводкой.</summary>
        private static Texture2D Build(float dx, float dy)
        {
            Color32[] px = new Color32[Size * Size];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, 0);

            float cx = Size * 0.5f - 0.5f, cy = Size * 0.5f - 0.5f;
            float half = Size * 0.34f;

            // Обводка (чёрная, на 1 px шире) — чтобы стрелка читалась на любом фоне.
            DrawArrow(px, cx, cy, dx, dy, half, new Color32(0, 0, 0, 220), 2.6f);
            DrawArrow(px, cx, cy, dx, dy, half, new Color32(255, 255, 255, 255), 1.4f);

            Texture2D tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        private static void DrawArrow(Color32[] px, float cx, float cy, float dx, float dy,
            float half, Color32 color, float thickness)
        {
            // Стержень двойной стрелки.
            Line(px, cx - dx * half, cy - dy * half, cx + dx * half, cy + dy * half, color, thickness);

            // Два наконечника.
            float nx = -dy, ny = dx;
            float head = 5.5f;
            float tipX = cx + dx * half, tipY = cy + dy * half;
            Line(px, tipX, tipY, tipX - dx * head + nx * head * 0.7f,
                tipY - dy * head + ny * head * 0.7f, color, thickness);
            Line(px, tipX, tipY, tipX - dx * head - nx * head * 0.7f,
                tipY - dy * head - ny * head * 0.7f, color, thickness);

            float tailX = cx - dx * half, tailY = cy - dy * half;
            Line(px, tailX, tailY, tailX + dx * head + nx * head * 0.7f,
                tailY + dy * head + ny * head * 0.7f, color, thickness);
            Line(px, tailX, tailY, tailX + dx * head - nx * head * 0.7f,
                tailY + dy * head - ny * head * 0.7f, color, thickness);
        }

        private static void Line(Color32[] px, float x0, float y0, float x1, float y1,
            Color32 color, float thickness)
        {
            float half = Mathf.Max(0.4f, thickness * 0.5f);
            int minX = Mathf.FloorToInt(Mathf.Min(x0, x1) - half - 1f);
            int maxX = Mathf.CeilToInt(Mathf.Max(x0, x1) + half + 1f);
            int minY = Mathf.FloorToInt(Mathf.Min(y0, y1) - half - 1f);
            int maxY = Mathf.CeilToInt(Mathf.Max(y0, y1) + half + 1f);
            float dx = x1 - x0, dy = y1 - y0;
            float len2 = dx * dx + dy * dy;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (x < 0 || y < 0 || x >= Size || y >= Size) continue;
                    float px0 = x + 0.5f, py0 = y + 0.5f;
                    float k = len2 > 0.0001f
                        ? Mathf.Clamp01(((px0 - x0) * dx + (py0 - y0) * dy) / len2) : 0f;
                    float qx = x0 + dx * k, qy = y0 + dy * k;
                    float d = Mathf.Sqrt((px0 - qx) * (px0 - qx) + (py0 - qy) * (py0 - qy));
                    float a = Mathf.Clamp01(half + 0.5f - d);
                    if (a <= 0.01f) continue;
                    int index = y * Size + x;     // y снизу вверх (совпадает с Texture2D)
                    Color32 old = px[index];
                    float na = Mathf.Clamp01(old.a / 255f + a * (color.a / 255f));
                    px[index] = new Color32(color.r, color.g, color.b, (byte)(na * 255f));
                }
            }
        }
    }
}
