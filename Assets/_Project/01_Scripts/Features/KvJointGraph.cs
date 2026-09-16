using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using KazistovVvUI;
using TrajectoryCore;

namespace KazistovVvFeatures
{
    /// <summary>
    /// ГРАФИКИ УГЛОВ СУСТАВОВ (ЭТАП 7 ТЗ).
    ///
    /// Рисуется в отдельной панели (собственный `RawImage` + `Texture2D`, без шейдеров и
    /// ассетов): по выбранной траектории строится график изменения КАЖДОГО угла во времени,
    /// у каждого сустава своя линия своего цвета, по горизонтали — время, по вертикали — угол.
    /// Резкие скачки видны как почти вертикальные участки, плавные — как наклонные.
    ///
    /// Дополнительно (опция ТЗ): НАЛОЖЕНИЕ графиков двух траекторий — вторая траектория
    /// рисуется приглушённо/пунктиром в той же системе координат, поэтому разница видна глазом.
    /// Полная перерисовка идёт только при смене данных (не в кадре) — на FPS не влияет.
    /// </summary>
    public class KvJointGraph : MonoBehaviour
    {
        public int textureWidth = 620;
        public int textureHeight = 240;
        public int fontSize = 11;

        /// <summary>Цвета линий суставов (J1…J6).</summary>
        public static readonly Color[] JointColors =
        {
            new Color(1.00f, 0.35f, 0.30f),   // J1
            new Color(1.00f, 0.70f, 0.20f),   // J2
            new Color(0.55f, 0.95f, 0.30f),   // J3
            new Color(0.30f, 0.85f, 0.95f),   // J4
            new Color(0.55f, 0.60f, 1.00f),   // J5
            new Color(0.90f, 0.45f, 0.95f)    // J6
        };

        private RectTransform root;
        private RawImage plot;
        private Texture2D texture;
        private Text titleText;
        private Text legendText;
        private Text axisText;

        // данные
        private List<float[]> seriesA = new List<float[]>();
        private List<float[]> seriesB = new List<float[]>();
        private float[] timesA;
        private float[] timesB;
        private string titleA = "";
        private string titleB = "";

        public bool HasData { get { return seriesA.Count > 0; } }
        public RectTransform Root { get { return root; } }

        /// <summary>Собрать панель (создаётся кодом, ассетов нет).</summary>
        public void Build(RectTransform parent, float width, float height)
        {
            GameObject go = new GameObject("KvJointGraph", typeof(Image));
            go.transform.SetParent(parent, false);
            root = (RectTransform)go.transform;
            Image bg = go.GetComponent<Image>();
            bg.sprite = KvTheme.WhiteSprite;
            bg.color = KvTheme.PanelDark;
            bg.raycastTarget = false;
            KvWidgets.Fit(go, width, height);
            root.sizeDelta = new Vector2(width, height);

            titleText = KvTheme.CreateText(root, "Title", "ГРАФИКИ УГЛОВ СУСТАВОВ",
                fontSize + 1, TextAnchor.UpperLeft, KvTheme.TextDim);
            KvWidgets.Fit(titleText.gameObject, width - 8f, 16f);
            titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            titleText.rectTransform.pivot = new Vector2(0.5f, 1f);
            titleText.rectTransform.anchoredPosition = new Vector2(0f, -2f);

            GameObject plotGo = new GameObject("Plot", typeof(RawImage));
            plotGo.transform.SetParent(root, false);
            plot = plotGo.GetComponent<RawImage>();
            plot.raycastTarget = false;
            RectTransform pr = plot.rectTransform;
            pr.anchorMin = new Vector2(0f, 0f);
            pr.anchorMax = new Vector2(1f, 1f);
            pr.offsetMin = new Vector2(6f, 44f);
            pr.offsetMax = new Vector2(-6f, -20f);

            legendText = KvTheme.CreateText(root, "Legend", "", fontSize,
                TextAnchor.LowerLeft, KvTheme.TextMain);
            legendText.horizontalOverflow = HorizontalWrapMode.Wrap;
            KvWidgets.Fit(legendText.gameObject, width - 12f, 28f);
            legendText.rectTransform.anchorMin = new Vector2(0f, 0f);
            legendText.rectTransform.anchorMax = new Vector2(1f, 0f);
            legendText.rectTransform.pivot = new Vector2(0.5f, 0f);
            legendText.rectTransform.anchoredPosition = new Vector2(0f, 15f);

            axisText = KvTheme.CreateText(root, "Axis", "время →", fontSize - 1,
                TextAnchor.LowerRight, KvTheme.TextDim);
            KvWidgets.Fit(axisText.gameObject, width - 12f, 14f);
            axisText.rectTransform.anchorMin = new Vector2(0f, 0f);
            axisText.rectTransform.anchorMax = new Vector2(1f, 0f);
            axisText.rectTransform.pivot = new Vector2(0.5f, 0f);
            axisText.rectTransform.anchoredPosition = new Vector2(0f, 2f);
        }

        /// <summary>Показать график одной траектории.</summary>
        public void SetPlan(PlannedTrajectory plan, string title)
        {
            seriesA.Clear();
            seriesB.Clear();
            timesA = null;
            timesB = null;
            titleA = title ?? "";
            titleB = "";

            if (plan != null && plan.Path != null && plan.Path.Length > 1)
            {
                timesA = plan.Times != null ? (float[])plan.Times.Clone() : Uniform(plan.Path.Length, 1f);
                int dof = plan.Path[0] != null ? plan.Path[0].Length : 0;
                for (int j = 0; j < dof; j++)
                {
                    float[] s = new float[plan.Path.Length];
                    for (int i = 0; i < plan.Path.Length; i++)
                        s[i] = plan.Path[i] != null && j < plan.Path[i].Length ? (float)plan.Path[i][j] : 0f;
                    seriesA.Add(s);
                }
            }
            Redraw();
        }

        /// <summary>Показать два графика наложенно (опция ТЗ).</summary>
        public void SetPlanPair(PlannedTrajectory a, string titleFirst, PlannedTrajectory b, string titleSecond)
        {
            SetPlan(a, titleFirst);
            if (b != null && b.Path != null && b.Path.Length > 1)
            {
                timesB = b.Times != null ? (float[])b.Times.Clone() : Uniform(b.Path.Length, 1f);
                int dof = b.Path[0] != null ? b.Path[0].Length : 0;
                for (int j = 0; j < dof; j++)
                {
                    float[] s = new float[b.Path.Length];
                    for (int i = 0; i < b.Path.Length; i++)
                        s[i] = b.Path[i] != null && j < b.Path[i].Length ? (float)b.Path[i][j] : 0f;
                    seriesB.Add(s);
                }
                titleB = titleSecond ?? "";
            }
            Redraw();
        }

        public void Clear()
        {
            seriesA.Clear();
            seriesB.Clear();
            timesA = null;
            timesB = null;
            titleA = "";
            titleB = "";
            Redraw();
        }

        /// <summary>Полная перерисовка текстуры графика.</summary>
        public void Redraw()
        {
            if (plot == null) return;

            int w = Mathf.Clamp(textureWidth, 64, 2048);
            int h = Mathf.Clamp(textureHeight, 64, 1024);

            if (texture == null || texture.width != w || texture.height != h)
            {
                if (texture != null) Destroy(texture);
                texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
                texture.hideFlags = HideFlags.HideAndDontSave;
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;
                plot.texture = texture;
            }

            Color bg = KvTheme.IsLight ? new Color(0.97f, 0.97f, 0.98f, 1f) : new Color(0.06f, 0.07f, 0.09f, 1f);
            Color grid = KvTheme.IsLight ? new Color(0.80f, 0.80f, 0.83f, 1f) : new Color(0.22f, 0.24f, 0.28f, 1f);
            Color axis = KvTheme.IsLight ? new Color(0.45f, 0.45f, 0.50f, 1f) : new Color(0.55f, 0.58f, 0.64f, 1f);

            Color[] px = new Color[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = bg;

            // сетка 8 × 4
            for (int i = 1; i < 8; i++)
            {
                int x = Mathf.RoundToInt(i * (w - 1) / 8f);
                for (int y = 0; y < h; y++) px[y * w + x] = grid;
            }
            for (int i = 1; i < 4; i++)
            {
                int y = Mathf.RoundToInt(i * (h - 1) / 4f);
                for (int x = 0; x < w; x++) px[y * w + x] = grid;
            }
            for (int x = 0; x < w; x++) px[x] = axis;                    // низ
            for (int y = 0; y < h; y++) px[y * w] = axis;                // лево

            if (seriesA.Count == 0)
            {
                if (titleText != null) titleText.text = "ГРАФИКИ УГЛОВ СУСТАВОВ · нет данных";
                if (legendText != null) legendText.text = "Выберите траекторию в дереве (ветка «Траектории»)";
                if (axisText != null) axisText.text = "";
                texture.SetPixels(px);
                texture.Apply(false);
                return;
            }

            // Общий диапазон времени и углов (для наложения двух графиков — единая шкала).
            float tMax = 0.0001f;
            if (timesA != null && timesA.Length > 0) tMax = Mathf.Max(tMax, timesA[timesA.Length - 1]);
            if (timesB != null && timesB.Length > 0) tMax = Mathf.Max(tMax, timesB[timesB.Length - 1]);

            float min = float.MaxValue, max = float.MinValue;
            Accumulate(seriesA, ref min, ref max);
            Accumulate(seriesB, ref min, ref max);
            if (max - min < 1e-3f) { max += 1f; min -= 1f; }
            float pad = (max - min) * 0.08f;
            min -= pad;
            max += pad;

            DrawSeries(px, w, h, seriesA, timesA, tMax, min, max, null);
            DrawSeries(px, w, h, seriesB, timesB, tMax, min, max, new Color(1f, 1f, 1f, 0.55f));

            texture.SetPixels(px);
            texture.Apply(false);

            if (titleText != null)
            {
                titleText.text = string.IsNullOrEmpty(titleB)
                    ? "ГРАФИКИ УГЛОВ · " + Trim(titleA)
                    : "ГРАФИКИ УГЛОВ · " + Trim(titleA) + "  ⟷  " + Trim(titleB) + " (наложение)";
            }
            if (legendText != null) legendText.text = Legend();
            if (axisText != null)
                axisText.text = "0 … " + tMax.ToString("0.0") + " с · угол " +
                                min.ToString("0") + "…" + max.ToString("0") + "°";
        }

        private void DrawSeries(Color[] px, int w, int h, List<float[]> series, float[] times,
            float tMax, float min, float max, Color? overrideColor)
        {
            if (series == null || series.Count == 0 || times == null || times.Length < 2) return;

            for (int j = 0; j < series.Count; j++)
            {
                Color color = overrideColor.HasValue
                    ? overrideColor.Value
                    : JointColors[j % JointColors.Length];

                float[] values = series[j];
                for (int i = 0; i + 1 < values.Length && i + 1 < times.Length; i++)
                {
                    float x0 = Mathf.Clamp01(times[i] / tMax) * (w - 2) + 1f;
                    float x1 = Mathf.Clamp01(times[i + 1] / tMax) * (w - 2) + 1f;
                    float y0 = Mathf.InverseLerp(min, max, values[i]) * (h - 3) + 1f;
                    float y1 = Mathf.InverseLerp(min, max, values[i + 1]) * (h - 3) + 1f;

                    int steps = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(x1 - x0)) + 1, 1, 64);
                    for (int s = 0; s <= steps; s++)
                    {
                        float k = s / (float)steps;
                        int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, k));
                        int y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, k));
                        Plot(px, w, h, x, y, color);
                    }
                }
            }
        }

        private static void Plot(Color[] px, int w, int h, int x, int y, Color color)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int px0 = x + dx, py0 = y + dy;
                    if (px0 < 0 || py0 < 0 || px0 >= w || py0 >= h) continue;
                    int index = py0 * w + px0;
                    if (Mathf.Abs(dx) + Mathf.Abs(dy) == 1)
                    {
                        px[index] = Color.Lerp(px[index], color, 0.55f);
                    }
                    else if (dx == 0 && dy == 0)
                    {
                        px[index] = color;
                    }
                }
            }
        }

        private static void Accumulate(List<float[]> series, ref float min, ref float max)
        {
            for (int j = 0; j < series.Count; j++)
            {
                float[] values = series[j];
                for (int i = 0; i < values.Length; i++)
                {
                    if (values[i] < min) min = values[i];
                    if (values[i] > max) max = values[i];
                }
            }
        }

        private string Legend()
        {
            if (seriesA.Count == 0) return "";
            string result = "";
            for (int j = 0; j < seriesA.Count; j++)
            {
                if (j > 0) result += "   ";
                result += "■ J" + (j + 1) + " " + ColorUtility.ToHtmlStringRGB(
                    JointColors[j % JointColors.Length]);
            }
            return "<color=#888888>СУСТАВЫ:</color> " + result.Replace("■", "■");
        }

        private static string Trim(string label)
        {
            if (string.IsNullOrEmpty(label)) return "—";
            int cut = label.IndexOf(" · ", StringComparison.Ordinal);
            return cut > 0 ? label.Substring(0, cut) : label;
        }

        private static float[] Uniform(int count, float total)
        {
            float[] t = new float[count];
            for (int i = 0; i < count; i++) t[i] = count > 1 ? total * i / (count - 1f) : 0f;
            return t;
        }

        private void OnDestroy()
        {
            if (texture != null) Destroy(texture);
        }
    }
}
