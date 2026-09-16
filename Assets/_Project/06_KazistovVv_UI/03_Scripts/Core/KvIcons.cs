using System.Collections.Generic;
using UnityEngine;

namespace KazistovVvUI
{
    /// <summary>
    /// Библиотека МОНОХРОМНЫХ иконок в стиле FreeCAD: иконки рисуются кодом
    /// (белые штрихи на прозрачном фоне) и тонируются цветом `Image.color`.
    /// Ни одного файла-ассета не нужно, поэтому нет проблем с импортом/meta.
    /// Спрайты кэшируются по (id, размер) — построение идёт один раз на иконку.
    ///
    /// ЭТАП 10 (единый набор): все иконки рисуются в сетке 24×24 с ОДНОЙ толщиной
    /// линии <see cref="Stroke"/> = 1.5 px (и <see cref="StrokeThin"/> = 1.2 px для
    /// мелких деталей), монохромно, с одинаковым отступом от края. Проверяется
    /// диагностикой <c>DshUiStagesDiag</c> (у каждой иконки ненулевая альфа,
    /// одинаковый размер спрайта и штрих одной толщины).
    /// </summary>
    public static class KvIcons
    {
        /// <summary>Штатный размер иконки тулбара (пикселей, в координатах канваса).</summary>
        public const int ToolbarSize = 24;
        /// <summary>Размер иконки строки дерева.</summary>
        public const int RowSize = 14;

        /// <summary>ЭТАП 10: единая толщина линии иконки (px сетки 24×24).</summary>
        public const float Stroke = 1.5f;
        /// <summary>ЭТАП 10: толщина мелких деталей (штриховка, второстепенные линии).</summary>
        public const float StrokeThin = 1.2f;
        /// <summary>ЭТАП 10: базовый размер сетки иконки (спрайт всегда рисуется в ней).</summary>
        public const int GridSize = 24;

        /// <summary>Все id иконок библиотеки (для диагностики единого набора).</summary>
        public static readonly string[] AllIds =
        {
            "point","path","play","pause","stop","reset","robot","swap","workspace","limits","metrics",
            "flashlight","undo","redo","theme-dark","theme-light","theme-system","settings","help","tree",
            "properties","eye","eye-off","axis","joint","tcp","table","node-point","curve","phantom",
            "layers","close","collapse","expand","dock","undock","chevron-right","chevron-down","check",
            "info","record","estop","zone","zone-box","zone-sphere","zone-cylinder","compare","graph",
            "heatmap","clearance","gripper","pickplace","eta","log","scenario","presentation","session",
            "features","lang","screenshot","video","singularity","waypoint","health","obstacle","pendant",
            "more","palette","keyboard","contrast","fontsize","gamepad","layout","lan","error","empty",
            "loading","pin","duplicate","focus","rename","trash","copy","filter"
        };

        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        /// <summary>Спрайт иконки (белый штрих). null — если иконки с таким id нет.</summary>
        public static Sprite Get(string id)
        {
            return Get(id, ToolbarSize);
        }

        /// <summary>Спрайт иконки заданного размера.</summary>
        public static Sprite Get(string id, int size)
        {
            if (string.IsNullOrEmpty(id)) return null;
            string key = id + "#" + size;
            Sprite sp;
            if (cache.TryGetValue(key, out sp) && sp != null) return sp;

            KvIconCanvas c = new KvIconCanvas(size);
            if (!Draw(id, c)) return null;

            sp = c.ToSprite();
            cache[key] = sp;
            return sp;
        }

        /// <summary>Есть ли такая иконка в библиотеке.</summary>
        public static bool Has(string id)
        {
            return Get(id) != null;
        }

        // ------------------------------------------------------------------ рисовка

        private static bool Draw(string id, KvIconCanvas c)
        {
            float s = c.Size;
            float m = s * 0.5f;                 // центр
            float r = s * 0.5f - s * 0.085f;    // рабочий радиус (единый отступ от края, ЭТАП 10)

            switch (id)
            {
                // --- выбор точки: прицел -------------------------------------
                case "point":
                    c.Circle(m, m, r * 0.62f, KvIcons.Stroke, false);
                    c.Line(m, m - r, m, m - r * 0.45f);
                    c.Line(m, m + r * 0.45f, m, m + r);
                    c.Line(m - r, m, m - r * 0.45f, m);
                    c.Line(m + r * 0.45f, m, m + r, m);
                    c.Dot(m, m, s * 0.10f);
                    return true;

                // --- выбор траектории: ломаная с узлами ----------------------
                case "path":
                    c.Dot(s * 0.18f, s * 0.20f, s * 0.09f);
                    c.Dot(s * 0.52f, s * 0.58f, s * 0.09f);
                    c.Dot(s * 0.84f, s * 0.84f, s * 0.09f);
                    c.Line(s * 0.18f, s * 0.20f, s * 0.52f, s * 0.58f, KvIcons.Stroke);
                    c.Line(s * 0.52f, s * 0.58f, s * 0.84f, s * 0.84f, KvIcons.Stroke);
                    return true;

                // --- транспорт движения -------------------------------------
                case "play":
                    c.Poly(true, m - r * 0.55f, m - r, m - r * 0.55f, m + r, m + r * 0.9f, m);
                    return true;
                case "pause":
                    c.Rect(m - r * 0.66f, m - r, s * 0.22f, r * 2f, 0f, true);
                    c.Rect(m + r * 0.44f, m - r, s * 0.22f, r * 2f, 0f, true);
                    return true;
                case "stop":
                    c.Rect(m - r * 0.62f, m - r * 0.62f, r * 1.24f, r * 1.24f, 0f, true);
                    return true;
                case "reset":
                    c.Arc(m, m, r * 0.80f, 30f, 330f, KvIcons.Stroke);
                    c.Poly(true, m + r * 0.80f * Mathf.Cos(Mathf.Deg2Rad * 20f),
                        m + r * 0.80f * Mathf.Sin(Mathf.Deg2Rad * 20f),
                        m + r * 0.80f * Mathf.Cos(Mathf.Deg2Rad * 45f) + s * 0.16f,
                        m + r * 0.80f * Mathf.Sin(Mathf.Deg2Rad * 45f) - s * 0.02f,
                        m + r * 0.80f * Mathf.Cos(Mathf.Deg2Rad * 30f) - s * 0.02f,
                        m + r * 0.80f * Mathf.Sin(Mathf.Deg2Rad * 30f) - s * 0.16f);
                    return true;

                // --- роботы --------------------------------------------------
                case "robot":
                    c.Rect(m - r * 0.75f, s * 0.12f, r * 1.5f, s * 0.14f, 0f, true);
                    c.Line(m - r * 0.45f, s * 0.26f, m + r * 0.10f, m + r * 0.15f, KvIcons.Stroke);
                    c.Line(m + r * 0.10f, m + r * 0.15f, m + r * 0.70f, m + r * 0.62f, KvIcons.Stroke);
                    c.Dot(m - r * 0.45f, s * 0.26f, s * 0.075f);
                    c.Dot(m + r * 0.10f, m + r * 0.15f, s * 0.075f);
                    c.Circle(m + r * 0.70f, m + r * 0.62f, s * 0.14f, KvIcons.Stroke, false);
                    return true;
                case "swap":
                    c.Line(s * 0.16f, s * 0.66f, s * 0.84f, s * 0.66f, KvIcons.Stroke);
                    c.Poly(true, s * 0.84f, s * 0.66f, s * 0.66f, s * 0.78f, s * 0.66f, s * 0.54f);
                    c.Line(s * 0.84f, s * 0.34f, s * 0.16f, s * 0.34f, KvIcons.Stroke);
                    c.Poly(true, s * 0.16f, s * 0.34f, s * 0.34f, s * 0.46f, s * 0.34f, s * 0.22f);
                    return true;

                // --- визуализации -------------------------------------------
                case "workspace":
                    c.Arc(m, s * 0.30f, r * 0.90f, 20f, 160f, KvIcons.Stroke);
                    c.Line(m - r * 0.85f, s * 0.30f, m + r * 0.85f, s * 0.30f, KvIcons.Stroke);
                    c.Arc(m, s * 0.30f, r * 0.45f, 25f, 155f, 1f);
                    c.Dot(m, s * 0.30f, s * 0.07f);
                    return true;
                case "limits":
                    c.Arc(m, s * 0.26f, r * 0.88f, 10f, 170f, KvIcons.Stroke);
                    c.Line(m, s * 0.26f, m + r * 0.70f, s * 0.26f + r * 0.70f, KvIcons.Stroke);
                    c.Line(m - r * 0.88f, s * 0.26f, m - r * 0.88f, s * 0.26f + r * 0.22f, KvIcons.Stroke);
                    c.Line(m + r * 0.88f, s * 0.26f, m + r * 0.88f, s * 0.26f + r * 0.22f, KvIcons.Stroke);
                    return true;
                case "metrics":
                    c.Rect(s * 0.16f, s * 0.16f, s * 0.16f, s * 0.36f, 0f, true);
                    c.Rect(s * 0.42f, s * 0.16f, s * 0.16f, s * 0.62f, 0f, true);
                    c.Rect(s * 0.68f, s * 0.16f, s * 0.16f, s * 0.48f, 0f, true);
                    c.Line(s * 0.10f, s * 0.14f, s * 0.90f, s * 0.14f, KvIcons.Stroke);
                    return true;
                case "flashlight":
                    c.Rect(m - s * 0.10f, s * 0.14f, s * 0.20f, s * 0.22f, 0f, true);
                    c.Poly(false, m - s * 0.10f, s * 0.36f, m + s * 0.10f, s * 0.36f,
                        m + r * 0.72f, s * 0.88f, m - r * 0.72f, s * 0.88f);
                    c.Line(m - r * 0.40f, s * 0.58f, m + r * 0.40f, s * 0.58f, 1f);
                    return true;

                // --- правка --------------------------------------------------
                case "undo":
                    c.Arc(m + r * 0.10f, m - r * 0.05f, r * 0.78f, 60f, 300f, KvIcons.Stroke);
                    c.Poly(true, m - r * 0.68f, m - r * 0.05f, m - r * 0.30f, m - r * 0.42f,
                        m - r * 0.30f, m + r * 0.32f);
                    return true;
                case "redo":
                    c.Arc(m - r * 0.10f, m - r * 0.05f, r * 0.78f, 240f, 480f, KvIcons.Stroke);
                    c.Poly(true, m + r * 0.68f, m - r * 0.05f, m + r * 0.30f, m + r * 0.32f,
                        m + r * 0.30f, m - r * 0.42f);
                    return true;

                // --- тема ----------------------------------------------------
                case "theme-dark":
                    c.Circle(m, m, r * 0.80f, KvIcons.Stroke, false);
                    c.Arc(m + r * 0.34f, m + r * 0.10f, r * 0.72f, 70f, 290f, KvIcons.Stroke);
                    return true;
                case "theme-light":
                    c.Circle(m, m, r * 0.44f, KvIcons.Stroke, false);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * 45f * Mathf.Deg2Rad;
                        float cx = Mathf.Cos(a), cy = Mathf.Sin(a);
                        c.Line(m + cx * r * 0.66f, m + cy * r * 0.66f,
                            m + cx * r * 0.98f, m + cy * r * 0.98f, KvIcons.Stroke);
                    }
                    return true;
                case "theme-system":
                    c.Rect(m - r * 0.95f, m - r * 0.62f, r * 1.90f, r * 1.30f, KvIcons.Stroke, false);
                    c.Line(m - r * 0.34f, m - r * 0.62f, m - r * 0.34f, m - r * 0.90f, KvIcons.Stroke);
                    c.Line(m + r * 0.34f, m - r * 0.62f, m + r * 0.34f, m - r * 0.90f, KvIcons.Stroke);
                    c.Line(m - r * 0.70f, m - r * 0.90f, m + r * 0.70f, m - r * 0.90f, KvIcons.Stroke);
                    return true;

                // --- сервис --------------------------------------------------
                case "settings":
                    c.Circle(m, m, r * 0.42f, KvIcons.Stroke, false);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * 45f * Mathf.Deg2Rad;
                        float cx = Mathf.Cos(a), cy = Mathf.Sin(a);
                        c.Line(m + cx * r * 0.52f, m + cy * r * 0.52f,
                            m + cx * r * 0.98f, m + cy * r * 0.98f, KvIcons.Stroke);
                    }
                    return true;
                case "help":
                    c.Circle(m, m, r * 0.92f, KvIcons.Stroke, false);
                    c.Arc(m + r * 0.02f, m + r * 0.30f, r * 0.36f, 190f, 350f, KvIcons.Stroke);
                    c.Line(m + r * 0.02f, m + r * 0.28f, m + r * 0.02f, m - r * 0.05f, KvIcons.Stroke);
                    c.Dot(m + r * 0.02f, m - r * 0.46f, s * 0.075f);
                    return true;

                // --- панели/дерево -------------------------------------------
                case "tree":
                    c.Dot(s * 0.22f, s * 0.80f, s * 0.09f);
                    c.Dot(s * 0.22f, s * 0.50f, s * 0.09f);
                    c.Dot(s * 0.22f, s * 0.20f, s * 0.09f);
                    c.Line(s * 0.22f, s * 0.80f, s * 0.22f, s * 0.20f, 1f);
                    c.Line(s * 0.26f, s * 0.80f, s * 0.80f, s * 0.80f, 1f);
                    c.Line(s * 0.26f, s * 0.50f, s * 0.66f, s * 0.50f, 1f);
                    c.Line(s * 0.26f, s * 0.20f, s * 0.80f, s * 0.20f, 1f);
                    return true;
                case "properties":
                    c.Line(s * 0.12f, s * 0.78f, s * 0.88f, s * 0.78f, KvIcons.Stroke);
                    c.Line(s * 0.12f, s * 0.50f, s * 0.88f, s * 0.50f, KvIcons.Stroke);
                    c.Line(s * 0.12f, s * 0.22f, s * 0.88f, s * 0.22f, KvIcons.Stroke);
                    c.Dot(s * 0.38f, s * 0.78f, s * 0.095f);
                    c.Dot(s * 0.66f, s * 0.50f, s * 0.095f);
                    c.Dot(s * 0.28f, s * 0.22f, s * 0.095f);
                    return true;
                case "eye":
                    c.Poly(false, s * 0.08f, m, s * 0.50f, s * 0.80f, s * 0.92f, m, s * 0.50f, s * 0.20f);
                    c.Dot(m, m, s * 0.13f);
                    return true;
                case "eye-off":
                    c.Poly(false, s * 0.08f, m, s * 0.50f, s * 0.80f, s * 0.92f, m, s * 0.50f, s * 0.20f);
                    c.Dot(m, m, s * 0.11f);
                    c.Line(s * 0.12f, s * 0.12f, s * 0.88f, s * 0.88f, KvIcons.Stroke);
                    return true;
                case "axis":
                    c.Line(s * 0.20f, s * 0.18f, s * 0.20f, s * 0.84f, KvIcons.Stroke);
                    c.Poly(true, s * 0.20f, s * 0.92f, s * 0.13f, s * 0.76f, s * 0.27f, s * 0.76f);
                    c.Rect(s * 0.10f, s * 0.10f, s * 0.20f, s * 0.09f, 0f, true);
                    return true;
                case "joint":
                    c.Circle(m, m, r * 0.88f, KvIcons.Stroke, false);
                    c.Circle(m, m, r * 0.34f, KvIcons.Stroke, false);
                    return true;
                case "tcp":
                    c.Circle(m, m, r * 0.72f, KvIcons.Stroke, false);
                    c.Line(m, m - r * 0.46f, m, m + r * 0.46f, KvIcons.Stroke);
                    c.Line(m - r * 0.46f, m, m + r * 0.46f, m, KvIcons.Stroke);
                    return true;
                case "table":
                    c.Rect(s * 0.10f, s * 0.64f, s * 0.80f, s * 0.14f, 0f, true);
                    c.Line(s * 0.20f, s * 0.64f, s * 0.20f, s * 0.14f, KvIcons.Stroke);
                    c.Line(s * 0.80f, s * 0.64f, s * 0.80f, s * 0.14f, KvIcons.Stroke);
                    return true;
                case "node-point":
                    c.Circle(m, m, r * 0.62f, KvIcons.Stroke, false);
                    c.Dot(m, m, s * 0.11f);
                    return true;
                case "curve":
                    c.Line(s * 0.12f, s * 0.22f, s * 0.38f, s * 0.62f, KvIcons.Stroke);
                    c.Line(s * 0.38f, s * 0.62f, s * 0.62f, s * 0.32f, KvIcons.Stroke);
                    c.Line(s * 0.62f, s * 0.32f, s * 0.88f, s * 0.78f, KvIcons.Stroke);
                    return true;
                case "phantom":
                    c.Arc(m, m, r * 0.82f, 0f, 360f, KvIcons.Stroke);
                    c.Dot(m - r * 0.30f, m + r * 0.18f, s * 0.07f);
                    c.Dot(m + r * 0.30f, m + r * 0.18f, s * 0.07f);
                    c.Arc(m, m - r * 0.10f, r * 0.42f, 200f, 340f, KvIcons.Stroke);
                    return true;
                case "layers":
                    c.Poly(false, m, s * 0.82f, s * 0.86f, s * 0.56f, m, s * 0.30f, s * 0.14f, s * 0.56f);
                    c.Line(s * 0.14f, s * 0.40f, m, s * 0.66f, KvIcons.Stroke);
                    c.Line(m, s * 0.66f, s * 0.86f, s * 0.40f, KvIcons.Stroke);
                    return true;

                // --- служебные (кнопки панелей) -------------------------------
                case "close":
                    c.Line(s * 0.24f, s * 0.24f, s * 0.76f, s * 0.76f, KvIcons.Stroke);
                    c.Line(s * 0.76f, s * 0.24f, s * 0.24f, s * 0.76f, KvIcons.Stroke);
                    return true;
                case "collapse":
                    c.Line(s * 0.24f, m + s * 0.06f, s * 0.76f, m + s * 0.06f, KvIcons.Stroke);
                    return true;
                case "expand":
                    c.Line(s * 0.24f, m, s * 0.76f, m, KvIcons.Stroke);
                    c.Line(m, s * 0.24f, m, s * 0.76f, KvIcons.Stroke);
                    return true;
                case "dock":
                    c.Rect(s * 0.12f, s * 0.16f, s * 0.76f, s * 0.68f, KvIcons.Stroke, false);
                    c.Rect(s * 0.60f, s * 0.16f, s * 0.28f, s * 0.68f, 0f, true);
                    return true;
                case "chevron-right":
                    c.Line(s * 0.36f, s * 0.20f, s * 0.68f, m, KvIcons.Stroke);
                    c.Line(s * 0.68f, m, s * 0.36f, s * 0.80f, KvIcons.Stroke);
                    return true;
                case "chevron-down":
                    c.Line(s * 0.20f, s * 0.64f, m, s * 0.32f, KvIcons.Stroke);
                    c.Line(m, s * 0.32f, s * 0.80f, s * 0.64f, KvIcons.Stroke);
                    return true;
                case "check":
                    c.Line(s * 0.20f, s * 0.52f, s * 0.42f, s * 0.26f, KvIcons.Stroke);
                    c.Line(s * 0.42f, s * 0.26f, s * 0.82f, s * 0.76f, KvIcons.Stroke);
                    return true;
                case "info":
                    c.Circle(m, m, r * 0.92f, KvIcons.Stroke, false);
                    c.Dot(m, s * 0.74f, s * 0.08f);
                    c.Line(m, s * 0.58f, m, s * 0.24f, KvIcons.Stroke);
                    return true;

                // ---------------------------------------------------------- новые функции (этапы 1–20)
                // --- этап 1: запись (кружок «rec») / воспроизведение
                case "record":
                    c.Circle(m, m, r * 0.72f, KvIcons.Stroke, false);
                    c.Dot(m, m, r * 0.36f);
                    return true;

                // --- этап 4: аварийная остановка (круг с крестом)
                case "estop":
                    c.Circle(m, m, r * 0.92f, KvIcons.Stroke, false);
                    c.Line(m - r * 0.45f, m - r * 0.45f, m + r * 0.45f, m + r * 0.45f, KvIcons.Stroke);
                    c.Line(m + r * 0.45f, m - r * 0.45f, m - r * 0.45f, m + r * 0.45f, KvIcons.Stroke);
                    return true;

                // --- этап 5: зоны запрета
                case "zone":
                    c.Rect(s * 0.16f, s * 0.16f, s * 0.68f, s * 0.68f, KvIcons.Stroke, false);
                    c.Line(s * 0.20f, s * 0.80f, s * 0.80f, s * 0.20f, KvIcons.Stroke);
                    return true;
                case "zone-box":
                    c.Poly(false, s * 0.18f, s * 0.24f, s * 0.62f, s * 0.16f, s * 0.82f, s * 0.34f,
                        s * 0.38f, s * 0.44f);
                    c.Line(s * 0.18f, s * 0.24f, s * 0.18f, s * 0.60f, KvIcons.Stroke);
                    c.Line(s * 0.62f, s * 0.16f, s * 0.62f, s * 0.52f, KvIcons.Stroke);
                    c.Line(s * 0.82f, s * 0.34f, s * 0.82f, s * 0.70f, KvIcons.Stroke);
                    c.Line(s * 0.38f, s * 0.44f, s * 0.38f, s * 0.80f, KvIcons.Stroke);
                    return true;
                case "zone-sphere":
                    c.Circle(m, m, r * 0.82f, KvIcons.Stroke, false);
                    c.Arc(m, m, r * 0.82f, 200f, 340f, KvIcons.Stroke);
                    return true;
                case "zone-cylinder":
                    c.Rect(s * 0.24f, s * 0.20f, s * 0.52f, s * 0.56f, KvIcons.Stroke, false);
                    c.Arc(m, s * 0.20f, s * 0.26f, 0f, 360f, KvIcons.Stroke);
                    c.Arc(m, s * 0.76f, s * 0.26f, 0f, 360f, KvIcons.Stroke);
                    return true;

                // --- этап 6: сравнение (две линии рядом)
                case "compare":
                    c.Line(s * 0.16f, s * 0.30f, s * 0.44f, s * 0.72f, KvIcons.Stroke);
                    c.Line(s * 0.56f, s * 0.30f, s * 0.84f, s * 0.72f, KvIcons.Stroke);
                    c.Line(s * 0.16f, s * 0.18f, s * 0.84f, s * 0.18f, KvIcons.Stroke);
                    return true;

                // --- этап 7: график (оси + ломаная)
                case "graph":
                    c.Line(s * 0.18f, s * 0.16f, s * 0.18f, s * 0.84f, KvIcons.Stroke);
                    c.Line(s * 0.18f, s * 0.16f, s * 0.86f, s * 0.16f, KvIcons.Stroke);
                    c.Line(s * 0.24f, s * 0.34f, s * 0.42f, s * 0.58f, KvIcons.Stroke);
                    c.Line(s * 0.42f, s * 0.58f, s * 0.60f, s * 0.42f, KvIcons.Stroke);
                    c.Line(s * 0.60f, s * 0.42f, s * 0.82f, s * 0.76f, KvIcons.Stroke);
                    return true;

                // --- этап 8: тепловая карта достижимости (сфера с градиентом-штриховкой)
                case "heatmap":
                    c.Circle(m, m, r * 0.86f, KvIcons.Stroke, false);
                    c.Circle(m, m, r * 0.52f, KvIcons.Stroke, false);
                    c.Dot(m, m, r * 0.20f);
                    return true;

                // --- этап 9: тепловая карта зазоров (линия с «опасным» участком)
                case "clearance":
                    c.Line(s * 0.14f, s * 0.34f, s * 0.86f, s * 0.34f, KvIcons.Stroke);
                    c.Dot(s * 0.30f, s * 0.34f, s * 0.11f);
                    c.Dot(s * 0.70f, s * 0.34f, s * 0.11f);
                    c.Line(s * 0.14f, s * 0.66f, s * 0.86f, s * 0.66f, KvIcons.Stroke);
                    c.Line(s * 0.44f, s * 0.20f, s * 0.56f, s * 0.20f, KvIcons.Stroke);
                    return true;

                // --- этап 10: гриппер (две «губки»)
                case "gripper":
                    c.Rect(s * 0.22f, s * 0.44f, s * 0.14f, s * 0.42f, KvIcons.Stroke, true);
                    c.Rect(s * 0.64f, s * 0.44f, s * 0.14f, s * 0.42f, KvIcons.Stroke, true);
                    c.Line(s * 0.36f, s * 0.30f, s * 0.64f, s * 0.30f, KvIcons.Stroke);
                    c.Line(m, s * 0.30f, m, s * 0.12f, KvIcons.Stroke);
                    return true;

                // --- этап 11: pick-and-place (куб + стрелка)
                case "pickplace":
                    c.Rect(s * 0.14f, s * 0.16f, s * 0.30f, s * 0.30f, KvIcons.Stroke, false);
                    c.Rect(s * 0.58f, s * 0.54f, s * 0.28f, s * 0.28f, KvIcons.Stroke, false);
                    c.Line(s * 0.44f, s * 0.34f, s * 0.68f, s * 0.58f, KvIcons.Stroke);
                    return true;

                // --- этап 12: ETA (часы)
                case "eta":
                    c.Circle(m, m, r * 0.86f, KvIcons.Stroke, false);
                    c.Line(m, m, m, s * 0.70f, KvIcons.Stroke);
                    c.Line(m, m, s * 0.70f, m, KvIcons.Stroke);
                    return true;

                // --- этап 13: журнал (строки текста)
                case "log":
                    c.Rect(s * 0.16f, s * 0.14f, s * 0.68f, s * 0.72f, KvIcons.Stroke, false);
                    c.Line(s * 0.26f, s * 0.70f, s * 0.74f, s * 0.70f, KvIcons.Stroke);
                    c.Line(s * 0.26f, s * 0.52f, s * 0.74f, s * 0.52f, KvIcons.Stroke);
                    c.Line(s * 0.26f, s * 0.34f, s * 0.58f, s * 0.34f, KvIcons.Stroke);
                    return true;

                // --- этап 18: сценарий (список с галочками)
                case "scenario":
                    c.Line(s * 0.14f, s * 0.72f, s * 0.26f, s * 0.60f, KvIcons.Stroke);
                    c.Line(s * 0.26f, s * 0.60f, s * 0.44f, s * 0.84f, KvIcons.Stroke);
                    c.Line(s * 0.54f, s * 0.72f, s * 0.86f, s * 0.72f, KvIcons.Stroke);
                    c.Line(s * 0.14f, s * 0.40f, s * 0.26f, s * 0.28f, KvIcons.Stroke);
                    c.Line(s * 0.26f, s * 0.28f, s * 0.44f, s * 0.52f, KvIcons.Stroke);
                    c.Line(s * 0.54f, s * 0.40f, s * 0.86f, s * 0.40f, KvIcons.Stroke);
                    return true;

                // --- этап 19: презентационный режим (кинокамера)
                case "presentation":
                    c.Rect(s * 0.14f, s * 0.30f, s * 0.46f, s * 0.36f, KvIcons.Stroke, false);
                    c.Poly(true, s * 0.60f, s * 0.38f, s * 0.60f, s * 0.58f, s * 0.84f, s * 0.68f,
                        s * 0.84f, s * 0.28f);
                    return true;

                // --- этап 20: сессия (дискета)
                case "session":
                    c.Rect(s * 0.16f, s * 0.16f, s * 0.68f, s * 0.68f, KvIcons.Stroke, false);
                    c.Rect(s * 0.32f, s * 0.54f, s * 0.36f, s * 0.30f, KvIcons.Stroke, true);
                    c.Rect(s * 0.30f, s * 0.20f, s * 0.40f, s * 0.22f, KvIcons.Stroke, false);
                    return true;

                // --- панель функций новых возможностей (шестерёнка + плюс)
                case "features":
                    c.Circle(m, m, r * 0.58f, KvIcons.Stroke, false);
                    c.Dot(m, m, r * 0.18f);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = Mathf.Deg2Rad * (i * 60f);
                        float x0 = m + Mathf.Cos(a) * r * 0.58f, y0 = m + Mathf.Sin(a) * r * 0.58f;
                        float x1 = m + Mathf.Cos(a) * r * 0.92f, y1 = m + Mathf.Sin(a) * r * 0.92f;
                        c.Line(x0, y0, x1, y1, KvIcons.Stroke);
                    }
                    return true;

                // ================= ЭТАПЫ 1–8 СЕССИИ 15.09.2026: новые иконки =================

                // --- ЭТАП 2: язык интерфейса (глобус с меридианами)
                case "lang":
                    c.Circle(m, m, r * 0.88f, KvIcons.Stroke, false);
                    c.Line(m - r * 0.88f, m, m + r * 0.88f, m, KvIcons.Stroke);
                    c.Arc(m, m, r * 0.44f, -90f, 90f, KvIcons.Stroke);
                    c.Arc(m, m, r * 0.44f, 90f, 270f, KvIcons.Stroke);
                    c.Line(m, m - r * 0.88f, m, m + r * 0.88f, KvIcons.Stroke);
                    return true;

                // --- ЭТАП 3: скриншот (фотоаппарат)
                case "screenshot":
                    c.Rect(s * 0.12f, s * 0.24f, s * 0.76f, s * 0.52f, KvIcons.Stroke, false);
                    c.Rect(s * 0.34f, s * 0.76f, s * 0.32f, s * 0.12f, KvIcons.Stroke, true);
                    c.Circle(s * 0.50f, s * 0.50f, r * 0.30f, KvIcons.Stroke, false);
                    c.Dot(s * 0.76f, s * 0.66f, s * 0.05f);
                    return true;

                // --- ЭТАП 3: видеозапись (кинокамера + красная точка)
                case "video":
                    c.Rect(s * 0.12f, s * 0.30f, s * 0.52f, s * 0.42f, KvIcons.Stroke, false);
                    c.Poly(true, s * 0.64f, s * 0.42f, s * 0.64f, s * 0.60f, s * 0.88f, s * 0.70f,
                        s * 0.88f, s * 0.32f);
                    c.Circle(s * 0.38f, s * 0.51f, r * 0.16f, KvIcons.Stroke, true);
                    return true;

                // --- ЭТАП 4: сингулярность (звезда-«вспышка» у сустава)
                case "singularity":
                    c.Circle(m, m, r * 0.30f, KvIcons.Stroke, false);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = Mathf.Deg2Rad * (i * 45f);
                        float cx = Mathf.Cos(a), cy = Mathf.Sin(a);
                        c.Line(m + cx * r * 0.46f, m + cy * r * 0.46f,
                            m + cx * r * 0.96f, m + cy * r * 0.96f, KvIcons.Stroke);
                    }
                    return true;

                // --- ЭТАП 5: waypoint (ломаная с промежуточным узлом)
                case "waypoint":
                    c.Line(s * 0.12f, s * 0.24f, s * 0.46f, s * 0.66f, KvIcons.Stroke);
                    c.Line(s * 0.46f, s * 0.66f, s * 0.88f, s * 0.34f, KvIcons.Stroke);
                    c.Dot(s * 0.12f, s * 0.24f, s * 0.08f);
                    c.Dot(s * 0.88f, s * 0.34f, s * 0.08f);
                    c.Circle(s * 0.46f, s * 0.66f, r * 0.24f, KvIcons.Stroke, false);
                    c.Dot(s * 0.46f, s * 0.66f, s * 0.06f);
                    return true;

                // --- ЭТАП 6: мониторинг состояния (кардиограмма)
                case "health":
                    c.Line(s * 0.10f, s * 0.46f, s * 0.30f, s * 0.46f, KvIcons.Stroke);
                    c.Line(s * 0.30f, s * 0.46f, s * 0.38f, s * 0.74f, KvIcons.Stroke);
                    c.Line(s * 0.38f, s * 0.74f, s * 0.50f, s * 0.20f, KvIcons.Stroke);
                    c.Line(s * 0.50f, s * 0.20f, s * 0.60f, s * 0.56f, KvIcons.Stroke);
                    c.Line(s * 0.60f, s * 0.56f, s * 0.70f, s * 0.46f, KvIcons.Stroke);
                    c.Line(s * 0.70f, s * 0.46f, s * 0.90f, s * 0.46f, KvIcons.Stroke);
                    return true;

                // --- ЭТАП 7: динамическое препятствие (тележка со стрелками)
                case "obstacle":
                    c.Rect(s * 0.16f, s * 0.34f, s * 0.68f, s * 0.30f, KvIcons.Stroke, false);
                    c.Circle(s * 0.30f, s * 0.26f, r * 0.14f, KvIcons.Stroke, true);
                    c.Circle(s * 0.70f, s * 0.26f, r * 0.14f, KvIcons.Stroke, true);
                    c.Line(s * 0.30f, s * 0.80f, s * 0.70f, s * 0.80f, KvIcons.Stroke);
                    c.Poly(true, s * 0.78f, s * 0.80f, s * 0.64f, s * 0.90f, s * 0.64f, s * 0.70f);
                    return true;

                // --- ЭТАП 8: виртуальный пульт (панель с экраном и джойстиком)
                case "pendant":
                    c.Rect(s * 0.16f, s * 0.12f, s * 0.68f, s * 0.76f, KvIcons.Stroke, false);
                    c.Rect(s * 0.24f, s * 0.60f, s * 0.52f, s * 0.20f, KvIcons.Stroke, false);
                    c.Rect(s * 0.28f, s * 0.40f, s * 0.18f, s * 0.12f, KvIcons.Stroke, true);
                    c.Rect(s * 0.54f, s * 0.40f, s * 0.18f, s * 0.12f, KvIcons.Stroke, true);
                    c.Dot(s * 0.36f, s * 0.24f, s * 0.07f);
                    return true;

                default:
                    return false;

                // ================= ЭТАПЫ 1–11 СЕССИИ UX/UI: новые иконки того же стиля =====

                // --- ЭТАП 1: «прочее» и «ещё» — три точки
                case "more":
                    c.Dot(s * 0.24f, m, s * 0.075f);
                    c.Dot(s * 0.50f, m, s * 0.075f);
                    c.Dot(s * 0.76f, m, s * 0.075f);
                    return true;

                // --- ЭТАП 3: открепить окно (стрелка из рамки вверх-вправо)
                case "undock":
                    c.Rect(s * 0.10f, s * 0.10f, s * 0.52f, s * 0.52f, KvIcons.Stroke, false);
                    c.Line(s * 0.44f, s * 0.56f, s * 0.90f, s * 0.90f, KvIcons.Stroke);
                    c.Poly(true, s * 0.92f, s * 0.92f, s * 0.60f, s * 0.86f, s * 0.86f, s * 0.60f);
                    return true;

                // --- ЭТАП 5: палитра команд (строка ввода + список)
                case "palette":
                    c.Rect(s * 0.10f, s * 0.56f, s * 0.80f, s * 0.32f, KvIcons.Stroke, false);
                    c.Line(s * 0.20f, s * 0.44f, s * 0.80f, s * 0.44f, KvIcons.Stroke);
                    c.Line(s * 0.20f, s * 0.30f, s * 0.62f, s * 0.30f, KvIcons.Stroke);
                    c.Line(s * 0.20f, s * 0.16f, s * 0.72f, s * 0.16f, KvIcons.Stroke);
                    return true;

                // --- ЭТАП 7: горячие клавиши (клавиатура)
                case "keyboard":
                    c.Rect(s * 0.08f, s * 0.26f, s * 0.84f, s * 0.48f, KvIcons.Stroke, false);
                    c.Line(s * 0.18f, s * 0.62f, s * 0.82f, s * 0.62f, KvIcons.StrokeThin);
                    c.Line(s * 0.18f, s * 0.50f, s * 0.46f, s * 0.50f, KvIcons.StrokeThin);
                    c.Line(s * 0.54f, s * 0.50f, s * 0.82f, s * 0.50f, KvIcons.StrokeThin);
                    c.Line(s * 0.18f, s * 0.38f, s * 0.82f, s * 0.38f, KvIcons.StrokeThin);
                    return true;

                // --- ЭТАП 11: высокий контраст (половина круга залита)
                case "contrast":
                    c.Circle(m, m, r * 0.86f, KvIcons.Stroke, false);
                    c.Poly(true, m, m - r * 0.86f, m, m + r * 0.86f, m + r * 0.86f, m);
                    return true;

                // --- ЭТАП 11: размер шрифта («A» большого и малого кегля)
                case "fontsize":
                    c.Line(s * 0.16f, s * 0.20f, s * 0.32f, s * 0.80f, KvIcons.Stroke);
                    c.Line(s * 0.48f, s * 0.20f, s * 0.32f, s * 0.80f, KvIcons.Stroke);
                    c.Line(s * 0.22f, s * 0.58f, s * 0.42f, s * 0.58f, KvIcons.Stroke);
                    c.Line(s * 0.60f, s * 0.44f, s * 0.72f, s * 0.80f, KvIcons.Stroke);
                    c.Line(s * 0.84f, s * 0.44f, s * 0.72f, s * 0.80f, KvIcons.Stroke);
                    c.Line(s * 0.65f, s * 0.66f, s * 0.79f, s * 0.66f, KvIcons.StrokeThin);
                    return true;

                // --- ЭТАП 9: геймпад (корпус, стики, крестовина)
                case "gamepad":
                    c.Rect(s * 0.08f, s * 0.30f, s * 0.84f, s * 0.40f, KvIcons.Stroke, false);
                    c.Circle(s * 0.30f, s * 0.50f, r * 0.14f, KvIcons.Stroke, false);
                    c.Circle(s * 0.70f, s * 0.50f, r * 0.14f, KvIcons.Stroke, false);
                    c.Line(s * 0.24f, s * 0.22f, s * 0.36f, s * 0.22f, KvIcons.StrokeThin);
                    c.Line(s * 0.64f, s * 0.22f, s * 0.76f, s * 0.22f, KvIcons.StrokeThin);
                    return true;

                // --- ЭТАП 3: раскладка окон (рамка + полосы панелей)
                case "layout":
                    c.Rect(s * 0.10f, s * 0.14f, s * 0.80f, s * 0.72f, KvIcons.Stroke, false);
                    c.Rect(s * 0.10f, s * 0.14f, s * 0.24f, s * 0.72f, 0f, true);
                    c.Line(s * 0.46f, s * 0.62f, s * 0.82f, s * 0.62f, KvIcons.StrokeThin);
                    return true;

                // --- ЭТАП 1: сеть (три узла, соединённые линиями)
                case "lan":
                    c.Dot(s * 0.50f, s * 0.78f, s * 0.10f);
                    c.Dot(s * 0.18f, s * 0.26f, s * 0.10f);
                    c.Dot(s * 0.82f, s * 0.26f, s * 0.10f);
                    c.Line(s * 0.50f, s * 0.74f, s * 0.22f, s * 0.30f, KvIcons.Stroke);
                    c.Line(s * 0.50f, s * 0.74f, s * 0.78f, s * 0.30f, KvIcons.Stroke);
                    return true;

                // --- ЭТАП 8: ошибка (треугольник с восклицательным знаком)
                case "error":
                    c.Poly(false, m, s * 0.86f, s * 0.90f, s * 0.16f, s * 0.10f, s * 0.16f);
                    c.Line(m, s * 0.68f, m, s * 0.38f, KvIcons.Stroke);
                    c.Dot(m, s * 0.28f, s * 0.07f);
                    return true;

                // --- ЭТАП 8: пустое состояние (пустая рамка)
                case "empty":
                    c.Rect(s * 0.14f, s * 0.22f, s * 0.72f, s * 0.56f, KvIcons.Stroke, false);
                    c.Line(s * 0.14f, s * 0.66f, s * 0.86f, s * 0.66f, KvIcons.StrokeThin);
                    return true;

                // --- ЭТАП 8: загрузка (круговой индикатор с разрывом)
                case "loading":
                    c.Arc(m, m, r * 0.84f, 30f, 320f, KvIcons.Stroke);
                    c.Dot(m + r * 0.84f * Mathf.Cos(Mathf.Deg2Rad * 20f),
                        m + r * 0.84f * Mathf.Sin(Mathf.Deg2Rad * 20f), s * 0.08f);
                    return true;

                // --- ЭТАП 3: закрепить (канцелярская кнопка)
                case "pin":
                    c.Line(m, s * 0.20f, m, s * 0.56f, KvIcons.Stroke);
                    c.Line(s * 0.30f, s * 0.56f, s * 0.70f, s * 0.56f, KvIcons.Stroke);
                    c.Line(s * 0.38f, s * 0.56f, s * 0.62f, s * 0.30f, KvIcons.Stroke);
                    c.Line(s * 0.38f, s * 0.30f, s * 0.62f, s * 0.56f, KvIcons.StrokeThin);
                    return true;

                // --- ЭТАП 6: дублировать (две наложенные рамки)
                case "duplicate":
                    c.Rect(s * 0.12f, s * 0.34f, s * 0.54f, s * 0.54f, KvIcons.Stroke, false);
                    c.Rect(s * 0.34f, s * 0.12f, s * 0.54f, s * 0.54f, KvIcons.Stroke, false);
                    return true;

                // --- ЭТАП 6: фокус камеры (центр + углы)
                case "focus":
                    c.Circle(m, m, r * 0.30f, KvIcons.Stroke, false);
                    c.Line(s * 0.10f, s * 0.26f, s * 0.10f, s * 0.10f, KvIcons.Stroke);
                    c.Line(s * 0.10f, s * 0.10f, s * 0.26f, s * 0.10f, KvIcons.Stroke);
                    c.Line(s * 0.74f, s * 0.90f, s * 0.90f, s * 0.90f, KvIcons.Stroke);
                    c.Line(s * 0.90f, s * 0.90f, s * 0.90f, s * 0.74f, KvIcons.Stroke);
                    return true;

                // --- ЭТАП 6: переименовать (карандаш)
                case "rename":
                    c.Line(s * 0.18f, s * 0.30f, s * 0.62f, s * 0.74f, KvIcons.Stroke);
                    c.Line(s * 0.30f, s * 0.22f, s * 0.74f, s * 0.66f, KvIcons.Stroke);
                    c.Line(s * 0.18f, s * 0.30f, s * 0.30f, s * 0.22f, KvIcons.Stroke);
                    c.Line(s * 0.14f, s * 0.86f, s * 0.30f, s * 0.82f, KvIcons.Stroke);
                    return true;

                // --- ЭТАП 6: удалить (корзина)
                case "trash":
                    c.Line(s * 0.14f, s * 0.74f, s * 0.86f, s * 0.74f, KvIcons.Stroke);
                    c.Line(s * 0.38f, s * 0.86f, s * 0.62f, s * 0.86f, KvIcons.Stroke);
                    c.Line(s * 0.36f, s * 0.86f, s * 0.36f, s * 0.74f, KvIcons.StrokeThin);
                    c.Line(s * 0.64f, s * 0.86f, s * 0.64f, s * 0.74f, KvIcons.StrokeThin);
                    c.Line(s * 0.24f, s * 0.74f, s * 0.30f, s * 0.18f, KvIcons.Stroke);
                    c.Line(s * 0.76f, s * 0.74f, s * 0.70f, s * 0.18f, KvIcons.Stroke);
                    c.Line(s * 0.30f, s * 0.18f, s * 0.70f, s * 0.18f, KvIcons.Stroke);
                    return true;

                // --- ЭТАП 6: копировать имя (два листа)
                case "copy":
                    c.Rect(s * 0.12f, s * 0.16f, s * 0.48f, s * 0.56f, KvIcons.Stroke, false);
                    c.Rect(s * 0.40f, s * 0.30f, s * 0.48f, s * 0.56f, KvIcons.Stroke, false);
                    return true;

                // --- ЭТАП 2: фильтр (воронка)
                case "filter":
                    c.Line(s * 0.12f, s * 0.80f, s * 0.88f, s * 0.80f, KvIcons.Stroke);
                    c.Line(s * 0.12f, s * 0.80f, s * 0.42f, s * 0.46f, KvIcons.Stroke);
                    c.Line(s * 0.88f, s * 0.80f, s * 0.58f, s * 0.46f, KvIcons.Stroke);
                    c.Line(s * 0.42f, s * 0.46f, s * 0.58f, s * 0.46f, KvIcons.Stroke);
                    c.Line(m, s * 0.46f, m, s * 0.16f, KvIcons.Stroke);
                    return true;
            }
        }
    }

    /// <summary>
    /// Мини-растеризатор иконок: белые штрихи на прозрачном фоне.
    /// Координаты — в пикселях, начало в левом НИЖНЕМ углу (иконки рисуются «вверх»).
    /// </summary>
    internal class KvIconCanvas
    {
        private readonly int w;
        private readonly Color[] px;

        public int Size { get { return w; } }

        public KvIconCanvas(int size)
        {
            w = Mathf.Max(8, size);
            px = new Color[w * w];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(1f, 1f, 1f, 0f);
        }

        private void Blend(int x, int y, float a)
        {
            if (x < 0 || y < 0 || x >= w || y >= w || a <= 0f) return;
            int i = y * w + x;                       // y — снизу вверх
            float na = Mathf.Clamp01(px[i].a + a);
            px[i] = new Color(1f, 1f, 1f, na);
        }

        /// <summary>Отрезок с полутолщиной t/2 и сглаживанием.</summary>
        public void Line(float x0, float y0, float x1, float y1, float t = KvIcons.Stroke)
        {
            float half = Mathf.Max(0.35f, t * 0.5f);
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
                    float cx = x + 0.5f, cy = y + 0.5f;
                    float k = 0f;
                    if (len2 > 0.0001f)
                        k = Mathf.Clamp01(((cx - x0) * dx + (cy - y0) * dy) / len2);
                    float px0 = x0 + dx * k, py0 = y0 + dy * k;
                    float d = Mathf.Sqrt((cx - px0) * (cx - px0) + (cy - py0) * (cy - py0));
                    float a = Mathf.Clamp01(half + 0.5f - d);
                    Blend(x, y, a);
                }
            }
        }

        /// <summary>Прямоугольник (контур или заливка).</summary>
        public void Rect(float x, float y, float width, float height, float t, bool filled)
        {
            if (filled)
            {
                int minX = Mathf.FloorToInt(x), maxX = Mathf.CeilToInt(x + width) - 1;
                int minY = Mathf.FloorToInt(y), maxY = Mathf.CeilToInt(y + height) - 1;
                for (int iy = minY; iy <= maxY; iy++)
                    for (int ix = minX; ix <= maxX; ix++)
                        Blend(ix, iy, 1f);
                return;
            }
            Line(x, y, x + width, y, t);
            Line(x + width, y, x + width, y + height, t);
            Line(x + width, y + height, x, y + height, t);
            Line(x, y + height, x, y, t);
        }

        /// <summary>Окружность (контур или заливка).</summary>
        public void Circle(float cx, float cy, float r, float t, bool filled)
        {
            float half = Mathf.Max(0.35f, t * 0.5f);
            int minX = Mathf.FloorToInt(cx - r - half - 1f);
            int maxX = Mathf.CeilToInt(cx + r + half + 1f);
            int minY = Mathf.FloorToInt(cy - r - half - 1f);
            int maxY = Mathf.CeilToInt(cy + r + half + 1f);
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                    if (filled) Blend(x, y, Mathf.Clamp01(r + 0.5f - d));
                    else Blend(x, y, Mathf.Clamp01(half + 0.5f - Mathf.Abs(d - r)));
                }
            }
        }

        /// <summary>Залитая точка (узел).</summary>
        public void Dot(float cx, float cy, float r)
        {
            Circle(cx, cy, r, 0f, true);
        }

        /// <summary>Дуга от угла a0 до a1 (градусы, против часовой).</summary>
        public void Arc(float cx, float cy, float r, float a0, float a1, float t)
        {
            int steps = Mathf.Max(6, Mathf.CeilToInt(Mathf.Abs(a1 - a0) / 8f));
            float px0 = 0f, py0 = 0f;
            for (int i = 0; i <= steps; i++)
            {
                float a = Mathf.Deg2Rad * Mathf.Lerp(a0, a1, i / (float)steps);
                float x = cx + Mathf.Cos(a) * r, y = cy + Mathf.Sin(a) * r;
                if (i > 0) Line(px0, py0, x, y, t);
                px0 = x; py0 = y;
            }
        }

        /// <summary>Многоугольник: контур или заливка (алгоритм «точка внутри»).</summary>
        public void Poly(bool filled, params float[] xy)
        {
            int n = xy.Length / 2;
            if (n < 2) return;
            if (!filled)
            {
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    Line(xy[i * 2], xy[i * 2 + 1], xy[j * 2], xy[j * 2 + 1], KvIcons.Stroke);
                }
                return;
            }

            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                minX = Mathf.Min(minX, xy[i * 2]); maxX = Mathf.Max(maxX, xy[i * 2]);
                minY = Mathf.Min(minY, xy[i * 2 + 1]); maxY = Mathf.Max(maxY, xy[i * 2 + 1]);
            }
            for (int y = Mathf.FloorToInt(minY); y <= Mathf.CeilToInt(maxY); y++)
            {
                for (int x = Mathf.FloorToInt(minX); x <= Mathf.CeilToInt(maxX); x++)
                {
                    float cx = x + 0.5f, cy = y + 0.5f;
                    bool inside = false;
                    for (int i = 0, j = n - 1; i < n; j = i++)
                    {
                        float xi = xy[i * 2], yi = xy[i * 2 + 1];
                        float xj = xy[j * 2], yj = xy[j * 2 + 1];
                        if (((yi > cy) != (yj > cy)) &&
                            (cx < (xj - xi) * (cy - yi) / (yj - yi + 1e-6f) + xi))
                            inside = !inside;
                    }
                    if (inside) Blend(x, y, 1f);
                }
            }
        }

        public Sprite ToSprite()
        {
            Texture2D tex = new Texture2D(w, w, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels(px);
            tex.Apply();
            Sprite sp = Sprite.Create(tex, new Rect(0, 0, w, w), new Vector2(0.5f, 0.5f), w);
            sp.hideFlags = HideFlags.HideAndDontSave;
            return sp;
        }
    }
}
