using System.Collections.Generic;
using UnityEngine;

namespace TrajectoryCore
{
    /// <summary>
    /// «Колбаска» вокруг траектории (MVP-1/2): тонкая линия + процедурный tube-меш
    /// радиусом ~0.05 (1/20 юнита) + штрих-пунктирный слой для подсветки.
    /// Выбор — геометрический (расстояние луч↔полилиния), коллайдеры и слои не нужны:
    /// это устойчивее и работает с любым источником луча (мышь, контроллер, геймпад).
    /// </summary>
    public class TrajectoryTube : MonoBehaviour
    {
        public float radius = 0.05f;          // радиус «колбаски» (захват лучом с ~1 юнита)
        public Color baseColor = new Color(1f, 0.45f, 0.03f, 0.9f);    // ярко-оранжевый (HDRP)
        public Color hoverColor = new Color(1f, 0.68f, 0.15f, 1f);     // подсветка наведения
        public Color selectedColor = new Color(1f, 0.85f, 0.3f, 1f);   // выбранная
        public float tubeAlpha = 0.07f;       // «колбаска» почти невидима (только для захвата)

        private Vector3[] points = new Vector3[0];
        private LineRenderer line;            // основная линия
        private LineRenderer dashLine;        // штрих-пунктир (виден при наведении/выборе)
        private MeshFilter tubeFilter;
        private Mesh tubeMesh;
        private MeshRenderer tubeRenderer;
        private bool highlighted;
        private bool selected;
        private float pulse;

        public Vector3[] Points => points;
        public bool Contains(Ray ray) => Contains(ray, radius);

        /// <summary>Попадание луча в «колбаску» (расстояние до полилинии ≤ радиуса).</summary>
        public bool Contains(Ray ray, float pickRadius)
        {
            if (points.Length < 2) return false;
            float d = TubeMath.DistanceRayPolyline(ray, points, out float t, out int seg);
            _ = t; _ = seg;
            return d <= pickRadius;
        }

        public float DistanceToRay(Ray ray)
        {
            if (points.Length < 2) return float.MaxValue;
            return TubeMath.DistanceRayPolyline(ray, points, out _, out _);
        }

        public void Build(Vector3[] path, Color color, bool dashed = true)
        {
            points = path != null ? path : new Vector3[0];
            baseColor = color;
            EnsureRenderers();
            BuildTubeMesh();
            UpdateLine(dashed);
            SetHighlight(false, false);
        }

        public void SetHighlight(bool hover, bool isSelected)
        {
            highlighted = hover;
            selected = isSelected;
            Color c = selected ? selectedColor : (hover ? hoverColor : baseColor);
            if (line != null)
            {
                line.startColor = c;
                line.endColor = new Color(c.r, c.g, c.b, c.a * 0.35f);
                line.widthMultiplier = selected ? 0.022f : (hover ? 0.016f : 0.010f);
            }
            if (dashLine != null) dashLine.enabled = hover || selected;
            if (tubeRenderer != null)
            {
                Material m = tubeRenderer.material;
                if (m != null && m.HasProperty("_BaseColor"))
                    m.SetColor("_BaseColor", new Color(c.r, c.g, c.b,
                        selected ? tubeAlpha * 2.2f : (hover ? tubeAlpha * 1.6f : tubeAlpha)));
            }
        }

        private void Update()
        {
            // Лёгкая пульсация подсветки — визуальная подсказка «выбор активен».
            if (!highlighted && !selected) return;
            pulse += Time.deltaTime * 3f;
            float k = 0.75f + 0.25f * Mathf.Sin(pulse);
            if (line != null)
                line.widthMultiplier = (selected ? 0.022f : 0.016f) * k;
        }

        private void EnsureRenderers()
        {
            if (line == null)
            {
                GameObject go = new GameObject("Line");
                go.transform.SetParent(transform, false);
                line = go.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.numCapVertices = 2;
                Shader sh = Shader.Find("Sprites/Default");
                if (sh != null) line.material = new Material(sh);
            }
            if (dashLine == null)
            {
                GameObject go = new GameObject("Dash");
                go.transform.SetParent(transform, false);
                dashLine = go.AddComponent<LineRenderer>();
                dashLine.useWorldSpace = true;
                dashLine.numCapVertices = 0;
                Shader sh = Shader.Find("Sprites/Default");
                if (sh != null) dashLine.material = new Material(sh);
            }
            if (tubeFilter == null)
            {
                GameObject go = new GameObject("Tube");
                go.transform.SetParent(transform, false);
                tubeFilter = go.AddComponent<MeshFilter>();
                tubeRenderer = go.AddComponent<MeshRenderer>();
                Shader sh = Shader.Find("HDRP/Lit");
                if (sh == null) sh = Shader.Find("Standard");
                if (sh == null) sh = Shader.Find("Sprites/Default");
                Material m = new Material(sh);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(baseColor.r, baseColor.g, baseColor.b, 0.12f));
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.2f);
                tubeRenderer.material = m;
                tubeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                tubeRenderer.receiveShadows = false;
            }
        }

        private void UpdateLine(bool dashed)
        {
            if (line != null)
            {
                line.positionCount = points.Length;
                if (points.Length > 0) line.SetPositions(points);
            }
            if (dashLine == null) return;
            if (!dashed || points.Length < 2)
            {
                dashLine.positionCount = 0;
                return;
            }
            // Штрих-пунктир: короткие отрезки вдоль полилинии (шаг = dash + gap).
            const float dash = 0.08f, gap = 0.05f;
            var seg = new List<Vector3>();
            float acc = 0f;
            for (int i = 0; i + 1 < points.Length; i++)
            {
                Vector3 a = points[i], b = points[i + 1];
                float len = Vector3.Distance(a, b);
                if (len < 1e-5f) continue;
                float t = 0f;
                while (t < len)
                {
                    float t0 = t, t1 = Mathf.Min(len, t + dash);
                    if (acc <= 0f) { seg.Add(Vector3.Lerp(a, b, t0 / len)); acc = dash; }
                    seg.Add(Vector3.Lerp(a, b, t1 / len));
                    acc -= (t1 - t0);
                    if (acc <= 0f) acc = -gap;
                    t = t1 + (acc < 0f ? -acc : 0f);
                    acc = acc < 0f ? 0f : acc;
                }
            }
            dashLine.positionCount = seg.Count;
            if (seg.Count > 0) dashLine.SetPositions(seg.ToArray());
            dashLine.startColor = new Color(hoverColor.r, hoverColor.g, hoverColor.b, 0.9f);
            dashLine.endColor = new Color(hoverColor.r, hoverColor.g, hoverColor.b, 0.3f);
            dashLine.widthMultiplier = 0.008f;
        }

        /// <summary>Процедурная «колбаска»: трубчатый меш вдоль полилинии (для визуала и объёма).</summary>
        private void BuildTubeMesh()
        {
            if (tubeFilter == null) return;
            const int sides = 8;
            int n = points.Length;
            if (n < 2)
            {
                tubeMesh = null;
                tubeFilter.sharedMesh = null;
                return;
            }
            var verts = new List<Vector3>(n * sides);
            var tris = new List<int>((n - 1) * sides * 6);
            for (int i = 0; i < n; i++)
            {
                Vector3 dir = i == 0 ? points[1] - points[0]
                    : (i == n - 1 ? points[n - 1] - points[n - 2] : points[i + 1] - points[i - 1]);
                if (dir.sqrMagnitude < 1e-8f) dir = Vector3.up;
                dir.Normalize();
                Vector3 right = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                Vector3 upv = Vector3.Cross(dir, right).normalized;
                for (int s = 0; s < sides; s++)
                {
                    float a = (float)s / sides * Mathf.PI * 2f;
                    verts.Add(points[i] + (right * Mathf.Cos(a) + upv * Mathf.Sin(a)) * radius);
                }
            }
            for (int i = 0; i + 1 < n; i++)
            {
                for (int s = 0; s < sides; s++)
                {
                    int s1 = (s + 1) % sides;
                    int a = i * sides + s, b = i * sides + s1, c = (i + 1) * sides + s, d = (i + 1) * sides + s1;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            }
            tubeMesh = new Mesh { name = "TrajectoryTube" };
            tubeMesh.SetVertices(verts);
            tubeMesh.SetTriangles(tris, 0);
            tubeMesh.RecalculateNormals();
            tubeMesh.RecalculateBounds();
            tubeFilter.sharedMesh = tubeMesh;
        }
    }
}
