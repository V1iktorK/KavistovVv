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
        public float FloorY = float.NegativeInfinity;
        public uint Version { get; private set; }

        private const float MaxObjectSize = 6f;   // крупнее — считаем фоном (ангар/стены)
        private const float MinObjectSize = 0.02f;

        /// <summary>Пересобрать мир (стробоскопически: раз в N кадров или по изменению сцены).</summary>
        public void Rebuild(RobotController skipRobot, float linkRadius = 0.06f)
        {
            Capsules.Clear();
            FloorY = float.NegativeInfinity;
            Version++;

            // 1. Статика сцены.
            Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
            foreach (Renderer r in renderers)
            {
                if (r == null) continue;
                string n = r.gameObject.name;
                if (n.Contains("Phantom") || n.Contains("Preview") || n.Contains("Laser") ||
                    n.Contains("IlyichLamp") || n.Contains("AimMarker") || n.Contains("TCP"))
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

                Capsules.Add(MakeCapsule(b, n));
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

        /// <summary>Минимальное расстояние от точки до мира (пол + капсулы).</summary>
        public float DistanceToPoint(Vector3 p)
        {
            float best = FloorY > float.NegativeInfinity ? Mathf.Max(0f, p.y - FloorY) : float.MaxValue;
            foreach (ObstacleCapsule c in Capsules)
                best = Mathf.Min(best, DistancePointSegment(p, c.a, c.b) - c.r);
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

            for (int i = 0; i + 1 < nodes.Count; i++)
            {
                for (int k = 0; k < Capsules.Count; k++)
                {
                    ObstacleCapsule c = Capsules[k];
                    float d = SegmentSegmentDistance(nodes[i], nodes[i + 1], c.a, c.b) - linkRadius - c.r;
                    if (d < best) best = d;
                }
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
