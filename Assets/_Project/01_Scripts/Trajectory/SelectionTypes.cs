using System.Collections.Generic;
using UnityEngine;

namespace TrajectoryCore
{
    /// <summary>Состояния сценария (по ТЗ): этапы 0–4.</summary>
    public enum FlowState
    {
        Idle,                // этап 0/1: точка не подтверждена — ничего не считаем и не двигаем
        PointSelected,       // точка подтверждена, кандидаты ещё считаются (тайм-слайсы)
        TrajectoriesShown,   // этап 2: показаны оранжевые траектории, ждём зелёный выбор
        PhantomsMoving,      // этап 3: фантомы едут из позы робота в свои конечные позы
        RobotMoving          // этап 4: реальный робот едет (нажатия игнорируются)
    }

    /// <summary>Кандидат-траектория: геометрия для «колбаски», метрики и стоимость.</summary>
    public class TrajectoryCandidate
    {
        public int id;
        public string label = "";
        public PlannedTrajectory plan;      // q(t) + времена
        public Vector3[] tube;              // полилиния TCP для визуала и выбора
        public float lengthM;               // длина пути TCP, м
        public float timeS;                 // время по параметризации, с
        public float minClearance;          // минимальный зазор, м
        public float limitMarginDeg;        // запас до лимитов, град
        public double score;                // S — меньше лучше
        public bool safe;                   // прошла SafetyGate
        public string why = "";             // причина отказа/предупреждения
        public TrajectoryTube view;         // визуал (создаётся TrajectoryTube)
    }

    /// <summary>Вариант конфигурации робота (решение IK) для фантома.</summary>
    public struct PhantomConfig
    {
        public int index;
        public double[] q;
        public IkBranchTag tag;
        public double cost;        // стоимость позы (PostureSelector)
        public float sigmaMin;     // м/град
        public float fkError;      // ошибка прямой задачи, м
        public GameObject ghost;   // визуал
        public string Label => "Фантом " + (index + 1) + " [" + tag + "]";
    }

    /// <summary>Текущее состояние выбора (единая точка правды для UI и логики).</summary>
    public class SelectionState
    {
        public FlowState phase = FlowState.Idle;
        public Vector3 point;                 // зафиксированная точка
        public bool hasPoint;
        public Vector3 aimAtLock;             // прицел в момент фиксации точки
        public int hoveredTrajectory = -1;
        public int selectedTrajectory = -1;
        public int hoveredPhantom = -1;
        public int selectedPhantom = -1;
        public readonly List<TrajectoryCandidate> candidates = new List<TrajectoryCandidate>();
        public readonly List<PhantomConfig> phantoms = new List<PhantomConfig>();

        public void ResetTrajectorySelection()
        {
            selectedTrajectory = -1;
            hoveredTrajectory = -1;
            selectedPhantom = -1;
            hoveredPhantom = -1;
            phantoms.Clear();
        }

        public void ClearAll()
        {
            ResetTrajectorySelection();
            candidates.Clear();
            hasPoint = false;
            phase = FlowState.Idle;
        }
    }

    /// <summary>Геометрия выбора: расстояние «луч ↔ трубка траектории» (без коллайдеров).</summary>
    public static class TubeMath
    {
        /// <summary>
        /// Минимальное расстояние от луча до полилинии; возвращает также параметр вдоль луча
        /// (для проверки перекрытия по глубине) и индекс ближайшего сегмента.
        /// </summary>
        public static float DistanceRayPolyline(Ray ray, Vector3[] poly, out float rayParam, out int segIndex)
        {
            rayParam = 0f;
            segIndex = -1;
            if (poly == null || poly.Length < 2) return float.MaxValue;

            float best = float.MaxValue;
            Vector3 far = ray.origin + ray.direction * 1000f;
            for (int i = 0; i + 1 < poly.Length; i++)
            {
                float d = SegmentSegmentDistance(ray.origin, far, poly[i], poly[i + 1]);
                if (d < best)
                {
                    best = d;
                    segIndex = i;
                    // проекция ближайшей точки сегмента на луч — грубая, но достаточная оценка
                    Vector3 mid = (poly[i] + poly[i + 1]) * 0.5f;
                    rayParam = Vector3.Dot(mid - ray.origin, ray.direction);
                }
            }
            return best;
        }

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
            if (tN < 0f) { tN = 0f; if (-d < 0f) sN = 0f; else if (-d > a) sN = sD; else { sN = -d; sD = a; } }
            else if (tN > tD) { tN = tD; if (-d + b < 0f) sN = 0f; else if (-d + b > a) sN = sD; else { sN = -d + b; sD = a; } }
            float sc = Mathf.Abs(sN) < 1e-6f ? 0f : sN / sD;
            float tc = Mathf.Abs(tN) < 1e-6f ? 0f : tN / tD;
            return (w + sc * u - tc * v).magnitude;
        }

        /// <summary>Длина полилинии, м.</summary>
        public static float PolylineLength(Vector3[] poly)
        {
            if (poly == null || poly.Length < 2) return 0f;
            float len = 0f;
            for (int i = 0; i + 1 < poly.Length; i++) len += Vector3.Distance(poly[i], poly[i + 1]);
            return len;
        }
    }

    /// <summary>Немного геометрии для фантомов: выбор «максимально разных» ветвей.</summary>
    public static class PhantomMath
    {
        /// <summary>
        /// Жадный выбор МАКСИМАЛЬНО РАЗНЕСЁННЫХ по конфигурации ветвей (farthest-point sampling):
        /// первая — лучшая по стоимости, далее — та, что дальше всех от уже выбранных.
        /// Даёт визуально различимые фантомы (elbow up/down, shoulder left/right, wrist flip).
        /// </summary>
        public static List<int> PickDistinct(List<IkSolution> solutions, double[] ranges, int count)
        {
            var result = new List<int>();
            if (solutions == null || solutions.Count == 0) return result;

            int bestStart = 0;
            for (int i = 1; i < solutions.Count; i++)
                if (solutions[i].withinLimits && !solutions[bestStart].withinLimits) bestStart = i;
            result.Add(bestStart);

            while (result.Count < Mathf.Min(count, solutions.Count))
            {
                int next = -1;
                double bestDist = -1;
                for (int i = 0; i < solutions.Count; i++)
                {
                    if (result.Contains(i)) continue;
                    double minDist = double.MaxValue;
                    foreach (int r in result)
                        minDist = System.Math.Min(minDist, ConfigDistance(solutions[i].q, solutions[r].q, ranges));
                    // предпочитаем далёкие, но валидные
                    double score = minDist + (solutions[i].withinLimits ? 0.0 : -10.0);
                    if (score > bestDist) { bestDist = score; next = i; }
                }
                if (next < 0) break;
                result.Add(next);
            }
            return result;
        }

        public static double ConfigDistance(double[] a, double[] b, double[] ranges)
        {
            if (a == null || b == null) return 0;
            double s = 0;
            int n = System.Math.Min(a.Length, b.Length);
            for (int i = 0; i < n; i++)
            {
                double r = ranges != null && i < ranges.Length && ranges[i] > 1e-6 ? ranges[i] : 1.0;
                double d = (a[i] - b[i]) / r;
                s += d * d;
            }
            return System.Math.Sqrt(s);
        }
    }

    /// <summary>Таймирование движения под заданную скорость инструмента (м/с).</summary>
    public static class MotionTiming
    {
        /// <summary>
        /// Пересчитывает времена траектории так, чтобы средняя скорость TCP была speedMps
        /// (например 0.05 м/с = 1 м за 20 с), сохраняя форму профиля.
        /// </summary>
        public static void RescaleToSpeed(PlannedTrajectory t, float lengthM, float speedMps)
        {
            if (t == null || t.Times == null || t.Times.Length == 0) return;
            float total = t.Times[t.Times.Length - 1];
            if (total <= 1e-4f || lengthM <= 1e-4f) return;
            float desired = lengthM / Mathf.Max(0.005f, speedMps);
            float k = desired / total;
            for (int i = 0; i < t.Times.Length; i++) t.Times[i] *= k;
            t.Time = t.Times[t.Times.Length - 1];
        }
    }
}
