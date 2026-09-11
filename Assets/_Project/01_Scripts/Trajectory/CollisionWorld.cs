using System.Collections.Generic;
using UnityEngine;

namespace TrajectoryCore
{
    /// <summary>Капсула препятствия: отрезок A–B + радиус.</summary>
    public struct ObstacleCapsule
    {
        public Vector3 a;
        public Vector3 b;
        public float r;
        public string name;
        public bool support;   // опора робота (стол, на котором он стоит) — не считается столкновением
    }

    /// <summary>Осевой бокс препятствия (столешницы, плиты, ящики).</summary>
    public struct ObstacleBox
    {
        public Vector3 center;
        public Vector3 half;
        public string name;
        public bool support;
    }

    /// <summary>
    /// Мир столкновений (E0/E1 плана): упрощённая капсульная модель окружения.
    /// * статика сцены (столы, детали) — капсулы по габаритам мешей;
    /// * пол — горизонтальная плоскость (по самой крупной плоской поверхности);
    /// * другие роботы — капсульные цепочки по их суставам;
    /// * очень крупные объекты (ангар/стены) игнорируются (далеко от рабочей зоны).
    /// Расстояния считаются по отрезкам (segment-segment), без физики Unity.
    /// </summary>
    public class CollisionWorld
    {
        public readonly List<ObstacleCapsule> Capsules = new List<ObstacleCapsule>();
        public readonly List<ObstacleBox> Boxes = new List<ObstacleBox>();
        public float FloorY = float.NegativeInfinity;
        public uint Version { get; private set; }

        private const float MaxObjectSize = 6f;   // крупнее — считаем фоном (ангар/стены)
        private const float MinObjectSize = 0.02f;

        /// <summary>Пересобрать мир. basePos — позиция базы робота (для определения опоры).</summary>
        public void Rebuild(RobotController skipRobot, float linkRadius = 0.06f)
        {
            Capsules.Clear();
            Boxes.Clear();
            FloorY = float.NegativeInfinity;
            Version++;

            Vector3 basePos = skipRobot != null ? skipRobot.transform.position : Vector3.zero;

            // 1. Статика сцены.
            Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
            foreach (Renderer r in renderers)
            {
                if (r == null) continue;
                string n = r.gameObject.name;
                if (n.Contains("Phantom") || n.Contains("Preview") || n.Contains("Laser") ||
                    n.Contains("IlyichLamp") || n.Contains("AimMarker") || n.Contains("TCP") ||
                    n.Contains("GhostRunner") || n.Contains("GhostWorstClearance") ||
                    n.Contains("GhostTrajectory"))
                    continue;
                if (r.GetComponentInParent<RobotController>() != null) continue; // роботы отдельно

                Bounds b = r.bounds;
                Vector3 s = b.size;
                if (s.magnitude < MinObjectSize) continue;
                if (Mathf.Max(s.x, Mathf.Max(s.y, s.z)) > MaxObjectSize)
                {
                    // Пол: низкий и очень широкий — учитываем как плоскость.
                    if (s.y < 1f && s.x > 8f && s.z > 8f)
                        FloorY = Mathf.Max(FloorY, b.max.y);
                    continue; // стены/потолок ангара игнорируем
                }

                // Плиты/столешницы (одна сторона много меньше двух других) — бокс;
                // вытянутые тела — капсула.
                float min = Mathf.Min(s.x, Mathf.Min(s.y, s.z));
                float max = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
                bool slab = min < 0.35f * max;
                if (slab)
                {
                    bool support = b.max.y <= basePos.y + 0.03f && b.max.y >= basePos.y - 0.4f &&
                                   Mathf.Abs(b.center.x - basePos.x) <= b.extents.x + 0.25f &&
                                   Mathf.Abs(b.center.z - basePos.z) <= b.extents.z + 0.25f;
                    Boxes.Add(new ObstacleBox
                    {
                        center = b.center,
                        half = b.extents,
                        name = n,
                        support = support
                    });
                }
                else
                {
                    ObstacleCapsule c = MakeCapsule(b, n);
                    c.support = false;
                    Capsules.Add(c);
                }
            }

            // 2. Другие роботы — цепочки по суставам.
            RobotController[] robots = Object.FindObjectsByType<RobotController>(FindObjectsInactive.Exclude);
            foreach (RobotController rc in robots)
            {
                if (rc == null || rc == skipRobot) continue;
                AddRobotChain(rc, linkRadius);
            }
        }

        private static ObstacleCapsule MakeCapsule(Bounds b, string name)
        {
            Vector3 s = b.size;
            // Ось — самое длинное измерение; радиус — по двум другим (консервативно, по максимуму).
            int axis = 0;
            if (s.y >= s.x && s.y >= s.z) axis = 1;
            else if (s.z >= s.x && s.z >= s.y) axis = 2;

            Vector3 dir = axis == 0 ? Vector3.right : (axis == 1 ? Vector3.up : Vector3.forward);
            float half = s[axis] * 0.5f;
            float rOther = Mathf.Max(0.01f, 0.5f * Mathf.Max(
                axis == 0 ? s.y : s.x,
                axis == 2 ? s.y : s.z));

            return new ObstacleCapsule
            {
                a = b.center - dir * half,
                b = b.center + dir * half,
                r = rOther,
                name = name
            };
        }

        private void AddRobotChain(RobotController rc, float linkRadius)
        {
            Transform[] joints = null;
            if (rc is SixAxisController six) joints = six.jointTransforms;
            else if (rc is SCARAController sc) joints = new[] { sc.joint1, sc.joint2, sc.joint3 };

            if (joints == null) return;
            var nodes = new List<Vector3>();
            foreach (Transform j in joints)
            {
                if (j == null) continue;
                Vector3 p = j.position;
                if (nodes.Count > 0 && (p - nodes[nodes.Count - 1]).sqrMagnitude < 1e-6f) continue;
                nodes.Add(p);
            }
            for (int i = 0; i + 1 < nodes.Count; i++)
            {
                Capsules.Add(new ObstacleCapsule
                {
                    a = nodes[i],
                    b = nodes[i + 1],
                    r = linkRadius,
                    name = rc.robotName + "_link" + i
                });
            }
        }

        /// <summary>Минимальное расстояние от точки до мира (пол + капсулы + боксы).</summary>
        public float DistanceToPoint(Vector3 p)
        {
            float best = FloorY > float.NegativeInfinity ? Mathf.Max(0f, p.y - FloorY) : float.MaxValue;
            foreach (ObstacleCapsule c in Capsules)
            {
                if (c.support) continue;
                best = Mathf.Min(best, DistancePointSegment(p, c.a, c.b) - c.r);
            }
            foreach (ObstacleBox b in Boxes)
            {
                if (b.support) continue;
                best = Mathf.Min(best, PointBoxDistance(p, b));
            }
            return best;
        }

        /// <summary>Минимальный зазор между капсульной цепочкой робота и миром.</summary>
        public float MinDistanceChain(IList<Vector3> nodes, float linkRadius)
        {
            float best = float.MaxValue;

            // против пола
            if (FloorY > float.NegativeInfinity)
                for (int i = 0; i + 1 < nodes.Count; i++)
                    best = Mathf.Min(best, Mathf.Min(nodes[i].y, nodes[i + 1].y) - FloorY - linkRadius);

            // первое звено (база) крепится к опоре — его столкновения считаются нормой
            int firstSegment = nodes.Count > 2 ? 1 : 0;

            for (int i = firstSegment; i + 1 < nodes.Count; i++)
            {
                for (int k = 0; k < Capsules.Count; k++)
                {
                    ObstacleCapsule c = Capsules[k];
                    if (c.support) continue;
                    float d = SegmentSegmentDistance(nodes[i], nodes[i + 1], c.a, c.b) - linkRadius - c.r;
                    if (d < best) best = d;
                }
                for (int k = 0; k < Boxes.Count; k++)
                {
                    ObstacleBox bx = Boxes[k];
                    if (bx.support) continue;
                    float d = SegmentBoxDistance(nodes[i], nodes[i + 1], bx) - linkRadius;
                    if (d < best) best = d;
                }
            }
            return best;
        }

        /// <summary>Точное расстояние точка–осевой бокс.</summary>
        public static float PointBoxDistance(Vector3 p, ObstacleBox b)
        {
            Vector3 d = new Vector3(
                Mathf.Abs(p.x - b.center.x) - b.half.x,
                Mathf.Abs(p.y - b.center.y) - b.half.y,
                Mathf.Abs(p.z - b.center.z) - b.half.z);
            Vector3 outside = new Vector3(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f), Mathf.Max(d.z, 0f));
            float outsideDist = outside.magnitude;
            float insideDist = Mathf.Min(Mathf.Max(d.x, Mathf.Max(d.y, d.z)), 0f);
            return outsideDist + insideDist;
        }

        /// <summary>Расстояние отрезок–бокс (сэмплирование отрезка).</summary>
        public static float SegmentBoxDistance(Vector3 a, Vector3 b, ObstacleBox box)
        {
            const int samples = 7;
            float best = float.MaxValue;
            for (int i = 0; i <= samples; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, (float)i / samples);
                best = Mathf.Min(best, PointBoxDistance(p, box));
            }
            return best;
        }

        // ------------------------------------------------------------- геометрия

        public static float DistancePointSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-9f) return (p - a).magnitude;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2);
            return (p - (a + ab * t)).magnitude;
        }

        /// <summary>Минимальное расстояние между двумя отрезками (3D).</summary>
        public static float SegmentSegmentDistance(Vector3 p1, Vector3 p2, Vector3 q1, Vector3 q2)
        {
            Vector3 u = p2 - p1, v = q2 - q1, w = p1 - q1;
            float a = Vector3.Dot(u, u), b = Vector3.Dot(u, v), c = Vector3.Dot(v, v);
            float d = Vector3.Dot(u, w), e = Vector3.Dot(v, w);
            float denom = a * c - b * b;
            float sN, sD = denom, tN, tD = denom;

            if (denom < 1e-6f) { sN = 0f; sD = 1f; tN = e; tD = c; }
            else
            {
                sN = b * e - c * d;
                tN = a * e - b * d;
                if (sN < 0f) { sN = 0f; tN = e; tD = c; }
                else if (sN > sD) { sN = sD; tN = e + b; tD = c; }
            }

            if (tN < 0f)
            {
                tN = 0f;
                if (-d < 0f) sN = 0f;
                else if (-d > a) sN = sD;
                else { sN = -d; sD = a; }
            }
            else if (tN > tD)
            {
                tN = tD;
                if (-d + b < 0f) sN = 0f;
                else if (-d + b > a) sN = sD;
                else { sN = (-d + b); sD = a; }
            }

            float sc = Mathf.Abs(sN) < 1e-6f ? 0f : sN / sD;
            float tc = Mathf.Abs(tN) < 1e-6f ? 0f : tN / tD;
            return (w + sc * u - tc * v).magnitude;
        }
    }
}
