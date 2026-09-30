using UnityEngine;
using UnityEngine.UI;

namespace KazistovVvUI
{
    /// <summary>
    /// СОБСТВЕННЫЙ КУРСОР — ТОЛЬКО ДЛЯ ОСОБЫХ СЛУЧАЕВ (ФИКС 8 §22; уточнён ФИКСОМ 4 §23 и §26).
    ///
    /// ГДЕ ОН СЕЙЧАС В ОСНОВНОМ РЕЖИМЕ (§26): НИГДЕ. Переключатель режима — Tab, и в обоих
    /// режимах работает системный курсор Unity: в режиме камеры он СКРЫТ (`Cursor.visible = false`),
    /// в режиме интерфейса — ПОКАЗАН. Своя иконка для основного режима не используется;
    /// включить её можно единственным флагом <see cref="KvMouseCursor.InGameIconInCameraMode"/>
    /// (по умолчанию false) — он и оставлен «специальным случаем».
    ///
    /// ЧТО КЛАСС ДЕЛАЕТ ВСЕГДА (и почему не удалён): его `Update` — это ХОЗЯИН кадрового
    /// обслуживания <see cref="KvMouseCursor.Tick"/> (переключение по Tab и самовосстановление
    /// `Cursor.*` работают независимо от того, жив ли контроллер камеры и открыто ли меню),
    /// и он же принимает формы курсора от <see cref="KvCursors"/>.
    ///
    /// ЧТО ДЕЛАЕТ КЛАСС:
    ///   • иконка — `RawImage` на отдельном канвасе `ScreenSpaceOverlay` с сортировкой
    ///     поверх всей оболочки (SortingOrder 900), поэтому она видна и когда панели скрыты;
    ///   • положение читается в `Update` из `Mouse.current.position` (new Input System)
    ///     с откатом на `Input.mousePosition` (legacy) — тот же приём, что во всём проекте;
    ///   • текстура рисуется КОДОМ (файлов-ассетов нет), по эталону §0.6/ЭТАП 10:
    ///     поле 24×24, штрих 1.5 px, белая заливка + тёмная обводка — иконка читается
    ///     и на светлой, и на тёмной сцене;
    ///   • форма может подменяться (курсоры изменения размера окна из <see cref="KvCursors"/>),
    ///     иначе системный курсор-«↔» пропал бы вместе с системной иконкой.
    ///
    /// Текстура создаётся один раз и переиспользуется; объект живёт на служебном объекте
    /// `DontDestroyOnLoad`-независимо — он часть оболочки и умирает вместе со сценой.
    /// </summary>
    public class KvVirtualCursor : MonoBehaviour
    {
        /// <summary>Сортировка «поверх всего»: оболочка использует 100…240.</summary>
        public const int SortingOrder = 900;

        /// <summary>Поле иконки — эталон §0.6 (ЭТАП 10): 24×24, штрих 1.5 px.</summary>
        public const int GridSize = 24;
        private const int TextureSize = 32;      // с запасом под обводку
        private const float Stroke = 1.5f;       // KvIcons.Stroke
        private const float TipX = 0.5f;         // остриё стрелки (совпадает с «горячей точкой»)

        private static KvVirtualCursor instance;

        private Canvas canvas;
        private RawImage image;
        private RectTransform rect;
        private Texture2D arrow;                 // обычная иконка
        private Texture2D shape;                 // текущая форма (null — стрелка)

        /// <summary>Единственный экземпляр (создаётся по требованию).</summary>
        public static KvVirtualCursor Instance { get { return instance; } }

        /// <summary>Создать/получить курсор. Ничего не делает, если уже создан.</summary>
        public static KvVirtualCursor Ensure()
        {
            if (instance != null) return instance;

            GameObject go = new GameObject("KvVirtualCursor", typeof(RectTransform));
            instance = go.AddComponent<KvVirtualCursor>();
            instance.Build();
            return instance;
        }

        private void Build()
        {
            // Канвас — КОРНЕВОЙ объект сцены: у Overlay-канваса вложенность в чужой RectTransform
            // даёт смещение и наследование масштаба, а курсор обязан попадать пиксель в пиксель.
            GameObject canvasGo = new GameObject("KvVirtualCursorCanvas",
                typeof(Canvas), typeof(CanvasScaler));
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            // Пиксель в пиксель: иконка курсора не должна масштабироваться вместе с UI.
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            GameObject imageGo = new GameObject("Pointer", typeof(RawImage));
            imageGo.transform.SetParent(canvasGo.transform, false);
            image = imageGo.GetComponent<RawImage>();
            image.raycastTarget = false;          // курсор не перехватывает клики
            image.texture = Arrow();
            image.color = Color.white;

            rect = (RectTransform)imageGo.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);   // отсчёт от левого верхнего угла экрана
            rect.pivot = new Vector2(0f, 1f);                        // «горячая точка» — остриё
            rect.sizeDelta = new Vector2(TextureSize, TextureSize);

            name = "KvVirtualCursor";
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void Update()
        {
            // ФИКСЫ 1/3/4 (§23) + §26: кадровое обслуживание режима курсора живёт ЗДЕСЬ, потому
            // что этот компонент существует всегда (его создаёт `KvMouseCursor.Apply`).
            // Один вызов — и переключение по Tab, и самовосстановление `Cursor.*` работают
            // независимо от того, жив ли контроллер камеры и открыто ли стартовое меню.
            KvMouseCursor.Tick();

            if (rect == null) return;
            Vector2 p = ReadMousePosition();
            // Мышь в координатах экрана (0,0 — левый НИЖНИЙ угол), RectTransform —
            // с отсчётом от левого ВЕРХНЕГО: переводим.
            rect.anchoredPosition = new Vector2(p.x - TipX, p.y - Screen.height);
        }

        /// <summary>Показать/скрыть иконку (управляет <see cref="KvMouseCursor"/>).</summary>
        public void SetVisible(bool value)
        {
            if (image != null) image.enabled = value;
        }

        /// <summary>
        /// Сменить форму курсора (курсоры изменения размера окна — <see cref="KvCursors"/>).
        /// null — вернуть обычную стрелку. Формы не кэшируются здесь: их рисует KvCursors.
        /// </summary>
        public static void SetShape(Texture2D texture)
        {
            Ensure().ApplyShape(texture);
        }

        /// <summary>Сменить форму у уже созданной иконки (см. статическую обёртку <see cref="SetShape"/>).</summary>
        public void ApplyShape(Texture2D texture)
        {
            shape = texture;
            if (image == null) return;
            image.texture = shape != null ? shape : Arrow();
        }

        /// <summary>Текущая форма (диагностика).</summary>
        public bool HasCustomShape { get { return shape != null; } }
        /// <summary>Иконка сейчас видима (диагностика).</summary>
        public bool IconVisible { get { return image != null && image.enabled; } }

        private static Vector2 ReadMousePosition()
        {
            try
            {
                if (UnityEngine.InputSystem.Mouse.current != null)
                    return UnityEngine.InputSystem.Mouse.current.position.ReadValue();
            }
            catch (System.Exception) { }
            return Input.mousePosition;
        }

        /// <summary>Иконка-стрелка: белая заливка, тёмная обводка, поле 24×24 (эталон §0.6).</summary>
        private Texture2D Arrow()
        {
            if (arrow != null) return arrow;

            // Классическая стрелка-указатель. Координаты — в пикселях поля 24×24, Y снизу вверх;
            // остриё (0.5, 23.5) совпадает с pivot (0,1) — курсор «попадает» точно в точку клика.
            float[][] outline =
            {
                new[] { 0.5f, 23.5f },
                new[] { 0.5f, 4.5f },
                new[] { 6.0f, 10.0f },
                new[] { 9.5f, 1.5f },
                new[] { 12.5f, 3.0f },
                new[] { 9.0f, 11.5f },
                new[] { 15.5f, 11.5f },
            };

            Color32[] px = new Color32[TextureSize * TextureSize];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, 0);

            // 1) обводка (тёмная, шире заливки) — иконка видна и на светлом фоне;
            for (int i = 0; i < outline.Length; i++)
            {
                float[] a = outline[i];
                float[] b = outline[(i + 1) % outline.Length];
                Line(px, a[0], a[1], b[0], b[1], new Color32(15, 15, 18, 235), Stroke + 1.1f);
            }
            // 2) заливка (белая, чуть уже) поверх внутренней половины обводки.
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    if (!Inside(outline, x + 0.5f, y + 0.5f)) continue;
                    px[y * TextureSize + x] = new Color32(246, 246, 248, 255);
                }
            }

            arrow = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
            arrow.hideFlags = HideFlags.HideAndDontSave;
            arrow.filterMode = FilterMode.Bilinear;
            arrow.wrapMode = TextureWrapMode.Clamp;
            arrow.name = "KvVirtualCursorArrow";
            arrow.SetPixels32(px);
            arrow.Apply();
            return arrow;
        }

        /// <summary>Внутри ли точка многоугольника (правило чётности, без учёта выпуклости).</summary>
        private static bool Inside(float[][] poly, float x, float y)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                float xi = poly[i][0], yi = poly[i][1];
                float xj = poly[j][0], yj = poly[j][1];
                if ((yi > y) == (yj > y)) continue;
                float cross = (xj - xi) * (y - yi) / (yj - yi) + xi;
                if (x < cross) inside = !inside;
            }
            return inside;
        }

        /// <summary>Линия с покрытием (сглаживание по краю) — тот же приём, что в KvCursors.</summary>
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
                    if (x < 0 || y < 0 || x >= TextureSize || y >= TextureSize) continue;
                    float px0 = x + 0.5f, py0 = y + 0.5f;
                    float k = len2 > 0.0001f
                        ? Mathf.Clamp01(((px0 - x0) * dx + (py0 - y0) * dy) / len2) : 0f;
                    float qx = x0 + dx * k, qy = y0 + dy * k;
                    float d = Mathf.Sqrt((px0 - qx) * (px0 - qx) + (py0 - qy) * (py0 - qy));
                    float a = Mathf.Clamp01(half + 0.5f - d);
                    if (a <= 0.01f) continue;
                    int index = y * TextureSize + x;
                    Color32 old = px[index];
                    float na = Mathf.Clamp01(old.a / 255f + a * (color.a / 255f));
                    px[index] = new Color32(color.r, color.g, color.b, (byte)(na * 255f));
                }
            }
        }
    }
}
