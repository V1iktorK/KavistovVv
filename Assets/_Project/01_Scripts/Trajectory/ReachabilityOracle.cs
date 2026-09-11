using System.Collections.Generic;
using KompasKinematics;
using UnityEngine;

namespace TrajectoryCore
{
    public enum ReachVerdict
    {
        Safe,          // зелёный: достижимо и есть запас
        Marginal,      // жёлтый: достижимо, но малый запас / лимит / близко к сингулярности
        Collision,     // красный: столкновение по пути/в позе
        Unreachable    // красный: вне рабочей зоны / IK не сходится
    }

    public struct ReachResult
    {
        public ReachVerdict verdict;
        public float clearance;   // минимальный зазор, м (минус = пересечение)
        public Vector3 tcp;       // где окажется инструмент
        public string reason;     // человекочитаемая причина
        public long stampMs;
    }

    /// <summary>
    /// Reachability Oracle (E1 плана): онлайн-вердикт по точке прицела.
    /// * SCARA — аналитически: кольцо рабочей зоны + ход z + капсулы корпуса (2 ветви локтя);
    /// * 6-осевой — численно: seeded CCD в текущей позе, затем проверка капсульной цепочки.
    /// Результат кэшируется по вокселю 1 см и сбрасывается при смене версии мира/позы.
    /// </summary>
    public class ReachabilityOracle
    {
        public float clearance = 0.02f;    // требуемый запас, м
        public float linkRadius = 0.06f;   // радиус звеньев, м
        public int ccdIterations = 24;
        public float ccdTolerance = 0.01f;

        private CollisionWorld world;
        private SixAxisController six;
        private SCARAController scara;

        // SCARA-геометрия (кэш)
        private float a1, a2, baseY, initialHeight, zMin, zMax;
        private Vector3 basePos;
        private Vector3 baseUp = Vector3.up;

        private readonly Dictionary<long, ReachResult> cache = new Dictionary<long, ReachResult>();
        private uint cachedWorldVersion;
        private float poseSignature = -1f;
        private float cacheTime;

        public bool Ready { get; private set; }
        public string RobotName { get; private set; }

        public void Init(RobotController robot, CollisionWorld collisionWorld)
        {
            world = collisionWorld;
            six = robot as SixAxisController;
            scara = robot as SCARAController;
            Ready = six != null || scara != null;
            RobotName = robot != null ? robot.robotName : "-";
            cache.Clear();
            cachedWorldVersion = 0;
            poseSignature = -1f;

            if (scara != null) CacheScaraGeometry();
        }

        private void CacheScaraGeometry()
        {
            Transform j1 = scara.joint1, j2 = scara.joint2;
            if (j1 == null || j2 == null) return;
            basePos = scara.baseTransform != null ? scara.baseTransform.position : j1.position;
            baseUp = scara.baseTransform != null ? scara.baseTransform.up : Vector3.up;
            baseY = Vector3.Dot(j1.position, baseUp);
            Vector3 p1 = Vector3.ProjectOnPlane(j1.position - basePos, baseUp);
            Vector3 p2 = Vector3.ProjectOnPlane(j2.position - basePos, baseUp);
            a1 = p1.magnitude;
            a2 = Vector3.Distance(p1, p2);
            initialHeight = Vector3.Dot(j2.position - basePos, baseUp);
            zMin = scara.ZMin;
            zMax = scara.ZMax;
        }

        /// <summary>Вердикт по точке прицела (мир).</summary>
        public ReachResult Query(Vector3 point)
        {
            ReachResult r = new ReachResult { verdict = ReachVerdict.Unreachable, reason = "нет робота" };
            if (!Ready || world == null) return r;

            float sig = PoseSignature();
            if (cachedWorldVersion != world.Version || Mathf.Abs(sig - poseSignature) > 0.0001f)
            {
                cache.Clear();
                cachedWorldVersion = world.Version;
                poseSignature = sig;
                if (scara != null) CacheScaraGeometry();
            }

            long key = VoxelKey(point);
            if (cache.TryGetValue(key, out ReachResult cached) &&
                Time.realtimeSinceStartup - cacheTime < 1.5f)
                return cached;

            r = scara != null ? QueryScara(point) : QuerySixAxis(point);
            r.stampMs = (long)(Time.realtimeSinceStartup * 1000f);
            cache[key] = r;
            cacheTime = Time.realtimeSinceStartup;
            return r;
        }

        private float PoseSignature()
        {
            if (scara != null && scara.joint1 != null)
                return scara.joint1.localEulerAngles.y + (scara.joint3 != null ? scara.joint3.localPosition.y * 10f : 0f);
            if (six != null && six.jointTransforms != null)
            {
                float s = 0f;
                foreach (Transform j in six.jointTransforms)
                    if (j != null) s += j.localEulerAngles.y + j.localEulerAngles.x * 0.01f;
                return s;
            }
            return 0f;
        }

        private static long VoxelKey(Vector3 p)
        {
            long x = (long)Mathf.Round(p.x * 100f);
            long y = (long)Mathf.Round(p.y * 100f);
            long z = (long)Mathf.Round(p.z * 100f);
            return (x * 73856093L) ^ (y * 19349663L) ^ (z * 83492791L);
        }

        // ------------------------------------------------------------- SCARA

        private ReachResult QueryScara(Vector3 point)
        {
            var res = new ReachResult();
            Transform j1 = scara.joint1, j2 = scara.joint2, j3 = scara.joint3;
            if (j1 == null || j2 == null || j3 == null)
            {
                res.verdict = ReachVerdict.Unreachable;
                res.reason = "SCARA: нет суставов";
                return res;
            }

            Vector3 toTarget = Vector3.ProjectOnPlane(point - basePos, baseUp);
            float r = toTarget.magnitude;
            float reachMax = a1 + a2 - 0.002f;
            float reachMin = Mathf.Abs(a1 - a2) + 0.002f;
            if (r > reachMax || r < reachMin)
            {
                res.verdict = ReachVerdict.Unreachable;
                res.reason = r > reachMax ? "вне вылета руки" : "ближе мертвой зоны";
                res.tcp = basePos + toTarget.normalized * Mathf.Clamp(r, reachMin, reachMax);
                return res;
            }

            // Ход Z: насколько цель ниже/выше допустимого положения стержня.
            float targetHeight = Vector3.Dot(point - basePos, baseUp);
            float desired = Mathf.Clamp(targetHeight, initialHeight + zMin, initialHeight + zMax);
            float zError = targetHeight - desired;

            // Поза: 2 ветви локтя (для проверки коллизий).
            float cosQ2 = Mathf.Clamp((r * r - a1 * a1 - a2 * a2) / (2f * a1 * a2), -1f, 1f);
            float q2 = Mathf.Acos(cosQ2);
            float baseAngle = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg; // азимут цели (град, ось Z)
            float phi1 = Mathf.Atan2(a2 * Mathf.Sin(q2), a1 + a2 * Mathf.Cos(q2)) * Mathf.Rad2Deg;

            float bestClearance = float.NegativeInfinity;
            Vector3 bestTcp = point;
            int branches = 0;
            for (int s = 0; s < 2; s++)
            {
                float sign = s == 0 ? 1f : -1f;
                float q2b = q2 * sign;
                float q1 = baseAngle - Mathf.Atan2(a2 * Mathf.Sin(q2b), a1 + a2 * Mathf.Cos(q2b)) * Mathf.Rad2Deg;
                _ = phi1;

                Vector3 elbow = basePos + (Quaternion.AngleAxis(q1, baseUp) * Vector3.forward) * a1 +
                                baseUp * (initialHeight - Vector3.Dot(j2.position - basePos, baseUp));
                Vector3 wrist = elbow;
                float wristR = Mathf.Sqrt(Mathf.Max(0f, a1 * a1 + a2 * a2 + 2f * a1 * a2 * Mathf.Cos(q2b * Mathf.Deg2Rad)));
                Vector3 wristDir = Quaternion.AngleAxis(q1 + Mathf.Atan2(a2 * Mathf.Sin(q2b), a1 + a2 * Mathf.Cos(q2b)) * Mathf.Rad2Deg, baseUp) * Vector3.forward;
                wrist = basePos + wristDir * wristR + baseUp * (initialHeight - Vector3.Dot(j2.position - basePos, baseUp));
                wrist += baseUp * desired; // стержень в допущенном положении

                var nodes = new List<Vector3> { j1.position, elbow, wrist };
                float clearanceNow = world.MinDistanceChain(nodes, linkRadius);
                if (clearanceNow > bestClearance)
                {
                    bestClearance = clearanceNow;
                    bestTcp = wrist;
                }
                branches++;
            }
            _ = branches;

            res.clearance = bestClearance;
            res.tcp = bestTcp;

            if (bestClearance < 0f)
            {
                res.verdict = ReachVerdict.Collision;
                res.reason = "пересечение со сценой";
            }
            else if (bestClearance < clearance)
            {
                res.verdict = ReachVerdict.Marginal;
                res.reason = "малый запас " + (bestClearance * 1000f).ToString("0") + " мм";
            }
            else if (Mathf.Abs(zError) > 0.005f)
            {
                res.verdict = ReachVerdict.Marginal;
                res.reason = "Z-ход ограничен (" + (zError * 1000f).ToString("0") + " мм)";
            }
            else
            {
                res.verdict = ReachVerdict.Safe;
                res.reason = "запас " + (bestClearance * 1000f).ToString("0") + " мм";
            }
            return res;
        }

        // ------------------------------------------------------------- 6-осевой

        private ReachResult QuerySixAxis(Vector3 point)
        {
            var res = new ReachResult();
            if (six == null || six.jointTransforms == null || six.jointTransforms.Length < 6)
            {
                res.verdict = ReachVerdict.Unreachable;
                res.reason = "6-осевой: нет суставов";
                return res;
            }

            // Быстрый отсев по габариту рабочей зоны (относительно базы).
            Transform b = six.baseTransform != null ? six.baseTransform : six.transform;
            float dist = Vector3.Distance(point, b.position);
            if (dist > 1.6f)
            {
                res.verdict = ReachVerdict.Unreachable;
                res.reason = "вне рабочей зоны (далеко)";
                return res;
            }
            if (dist < 0.15f)
            {
                res.verdict = ReachVerdict.Unreachable;
                res.reason = "внутри корпуса робота";
                return res;
            }

            Transform tip = six.tcp != null ? six.tcp : six.endEffector;
            if (tip == null)
            {
                res.verdict = ReachVerdict.Unreachable;
                res.reason = "нет TCP";
                return res;
            }

            // Сохраняем позу, решаем IK в реальных трансформах, затем возвращаем.
            Quaternion[] saved = new Quaternion[six.jointTransforms.Length];
            for (int i = 0; i < saved.Length; i++)
                saved[i] = six.jointTransforms[i] != null ? six.jointTransforms[i].localRotation : Quaternion.identity;

            bool ok = DHInverse.SolveCCD(six.jointTransforms, six.jointAxesLocal, tip, point,
                ccdIterations, ccdTolerance);
            float err = Vector3.Distance(tip.position, point);
            res.tcp = tip.position;

            // Капсульная цепочка по суставам + инструмент.
            var nodes = new List<Vector3>();
            foreach (Transform j in six.jointTransforms)
            {
                if (j == null) continue;
                Vector3 p = j.position;
                if (nodes.Count > 0 && (p - nodes[nodes.Count - 1]).sqrMagnitude < 1e-6f) continue;
                nodes.Add(p);
            }
            nodes.Add(tip.position);
            float clr = world.MinDistanceChain(nodes, linkRadius);

            // Лимиты и сингулярности по фактическим углам.
            float[] angles = six.GetJointAngles();
            bool nearLimit = false;
            if (angles != null && six.jointLimits != null)
            {
                for (int i = 0; i < angles.Length && i < six.jointLimits.Length; i++)
                {
                    if (angles[i] < six.jointLimits[i].x + 5f || angles[i] > six.jointLimits[i].y - 5f)
                    { nearLimit = true; break; }
                }
            }
            bool wristSingular = angles != null && angles.Length > 4 && Mathf.Abs(angles[4]) < 8f;

            for (int i = 0; i < saved.Length; i++)
                if (six.jointTransforms[i] != null) six.jointTransforms[i].localRotation = saved[i];

            res.clearance = clr;
            if (err > 0.03f || !ok && err > 0.03f)
            {
                res.verdict = ReachVerdict.Unreachable;
                res.reason = "IK не сходится (ошибка " + (err * 1000f).ToString("0") + " мм)";
            }
            else if (clr < 0f)
            {
                res.verdict = ReachVerdict.Collision;
                res.reason = "пересечение со сценой";
            }
            else if (clr < clearance)
            {
                res.verdict = ReachVerdict.Marginal;
                res.reason = "малый запас " + (clr * 1000f).ToString("0") + " мм";
            }
            else if (nearLimit)
            {
                res.verdict = ReachVerdict.Marginal;
                res.reason = "близко к лимиту сустава";
            }
            else if (wristSingular)
            {
                res.verdict = ReachVerdict.Marginal;
                res.reason = "близко к сингулярности запястья";
            }
            else
            {
                res.verdict = ReachVerdict.Safe;
                res.reason = "запас " + (clr * 1000f).ToString("0") + " мм";
            }
            return res;
        }
    }
}
