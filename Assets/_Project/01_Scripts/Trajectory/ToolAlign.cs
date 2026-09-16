using UnityEngine;

namespace TrajectoryCore
{
    /// <summary>
    /// Выравнивание концевой плоскости робота («пятака») по плоскости поверхности.
    ///
    /// Нормаль «пятака» — ось вращения последнего сустава: если она совпала с нормалью
    /// поверхности, концевая плоскость ПАРАЛЛЕЛЬНА столу/полу/стене (и наклонной поверхности).
    ///
    /// Решается численно и НЕ трогает кинематику, IK и планировщик: берётся уже готовая поза
    /// цели и подстраивается покоординатным спуском по стоимости
    /// «ошибка TCP по позиции + ошибка оси по углу». Каждый шаг проверяется лимитами суставов;
    /// результат принимается только после полной проверки позы (позиция, лимиты с запасом,
    /// зазор до мира и до своих звеньев, ограничение на величину перестроения).
    /// При любой неудаче возвращается ИСХОДНАЯ поза (свободная ориентация, как было).
    /// </summary>
    public static class ToolAlign
    {
        /// <summary>Итог выравнивания. q — выровненная поза, либо исходная при ok == false.</summary>
        public struct Outcome
        {
            public bool ok;
            public double[] q;
            public float angleDeg;      // остаточный угол между нормалью «пятака» и нормалью поверхности
            public float posErrM;       // ошибка TCP по позиции, м
            public bool intoSurface;    // true — инструмент развёрнут «в поверхность» (−нормаль)
            public string why;          // почему не удалось (для лога)
        }

        /// <summary>
        /// Подгоняет позу цели так, чтобы TCP остался в target, а «пятак» встал параллельно
        /// плоскости с нормалью surfaceNormal. Сначала пробуется направление «в поверхность»
        /// (−нормаль), затем разворот оси (+нормаль): «пятак» параллелен в обоих случаях.
        /// </summary>
        public static Outcome AlignGoal(PoseValidator v, CollisionWorld world, double[] qGoal,
            Vector3 tcpTarget, Vector3 surfaceNormal, float minClearance, float minSelfClearance,
            float minLimitMarginDeg, float toleranceM, float angleTolDeg)
        {
            var none = new Outcome { ok = false, q = qGoal };
            if (v == null || !v.Ready || qGoal == null) { none.why = "нет данных о роботе"; return none; }
            if (v.Dof < 6) { none.why = "у этого робота нет 6 вращательных осей"; return none; }
            if (surfaceNormal.sqrMagnitude < 1e-6f) { none.why = "нормаль поверхности не задана"; return none; }

            Vector3 n = surfaceNormal.normalized;

            Outcome into = Solve(v, world, qGoal, tcpTarget, -n, minClearance, minSelfClearance,
                                 minLimitMarginDeg, toleranceM, angleTolDeg);
            if (into.ok) { into.intoSurface = true; return into; }

            Outcome flipped = Solve(v, world, qGoal, tcpTarget, n, minClearance, minSelfClearance,
                                    minLimitMarginDeg, toleranceM, angleTolDeg);
            if (flipped.ok) { flipped.intoSurface = false; return flipped; }

            into.why = into.why + "; разворот оси: " + flipped.why;
            return into;
        }

        // ------------------------------------------------------------------ численная подгонка

        private static Outcome Solve(PoseValidator v, CollisionWorld world, double[] qGoal,
            Vector3 target, Vector3 aDes, float minClearance, float minSelfClearance,
            float minLimitMarginDeg, float toleranceM, float angleTolDeg)
        {
            const int maxSweeps = 24;      // проходов «по всем суставам»
            const float stepDeg = 4f;      // максимальный доворот сустава за шаг
            const float wPos = 25f;        // вес позиции: 1 см ошибки TCP ≈ 5° ошибки оси

            var q = (double[])qGoal.Clone();
            var res = new Outcome { q = q };

            double[] bestQ = (double[])qGoal.Clone();
            float bestPos, bestAng;
            Measure(v, q, target, aDes, out bestPos, out bestAng);
            float bestScore = bestPos / toleranceM + bestAng / angleTolDeg;

            float cost = Cost(v, q, target, aDes, wPos);
            for (int sweep = 0; sweep < maxSweeps; sweep++)
            {
                Measure(v, q, target, aDes, out res.posErrM, out res.angleDeg);
                if (res.posErrM <= toleranceM && res.angleDeg <= angleTolDeg) break;

                // Лучшая увиденная поза: нормализованная сумма ошибок (позиция/допуск + угол/допуск).
                float score = res.posErrM / toleranceM + res.angleDeg / angleTolDeg;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestPos = res.posErrM;
                    bestAng = res.angleDeg;
                    bestQ = (double[])q.Clone();
                }

                bool moved = false;
                for (int i = 0; i < v.Dof; i++)
                {
                    if (v.IsPrismatic(i)) continue;              // призму SCARA не трогаем
                    float g = Gradient(v, q, i, target, aDes, wPos);
                    if (Mathf.Abs(g) < 1e-6f) continue;
                    float dir = g > 0f ? -1f : 1f;               // спуск против роста стоимости

                    for (int t = 0; t < 3; t++)                  // шаг с дроблением (полушаг, четверть)
                    {
                        float dq = dir * stepDeg / (1 << t);
                        double save = q[i];
                        q[i] = save + dq;
                        if (!v.WithinLimits(q)) { q[i] = save; continue; }
                        float next = Cost(v, q, target, aDes, wPos);
                        if (next < cost - 1e-6f) { cost = next; moved = true; break; }
                        q[i] = save;
                    }
                }
                if (!moved) break;                                // локальный минимум
            }

            // Итог — лучшая из увиденных поз (спуск мог «качнуться» в сторону в конце).
            Measure(v, q, target, aDes, out res.posErrM, out res.angleDeg);
            if (bestScore < res.posErrM / toleranceM + res.angleDeg / angleTolDeg)
            {
                q = bestQ;
                res.posErrM = bestPos;
                res.angleDeg = bestAng;
            }
            res.q = q;

            if (res.posErrM > toleranceM)
            {
                res.why = "TCP не удержан в цели (" + (res.posErrM * 1000f).ToString("0") + " мм)";
                return res;
            }
            if (res.angleDeg > angleTolDeg)
            {
                res.why = "ось не довернулась (остаток " + res.angleDeg.ToString("0.0") + "°)";
                return res;
            }
            if (!v.WithinLimits(q)) { res.why = "поза вне лимитов суставов"; return res; }
            if (v.LimitMargin(q) < minLimitMarginDeg) { res.why = "мал запас до лимитов"; return res; }

            float self = v.SelfClearance(q, out _, out _);
            if (self < minSelfClearance) { res.why = "сближение своих звеньев"; return res; }

            float clear = v.ClearanceAt(q, world, out _, out _);
            if (clear < minClearance) { res.why = "мал зазор до препятствия"; return res; }

            for (int i = 0; i < v.Dof && i < qGoal.Length; i++)
            {
                if (v.IsPrismatic(i)) continue;
                if (Mathf.Abs(Mathf.DeltaAngle((float)qGoal[i], (float)q[i])) > 120f)
                {
                    res.why = "слишком большой доворот сустава " + (i + 1);
                    return res;
                }
            }

            res.ok = true;
            return res;
        }

        /// <summary>Ось «пятака» — ось вращения последнего сустава (нормаль концевой плоскости).</summary>
        private static Vector3 ToolAxis(PoseValidator v, double[] q)
        {
            Vector3 a = v.AxisWorld(v.Dof - 1, q);
            return a.sqrMagnitude > 1e-8f ? a.normalized : Vector3.up;
        }

        private static void Measure(PoseValidator v, double[] q, Vector3 target, Vector3 aDes,
            out float posErrM, out float angleDeg)
        {
            posErrM = Vector3.Distance(v.TcpAt(q), target);
            angleDeg = Vector3.Angle(ToolAxis(v, q), aDes);
        }

        /// <summary>Стоимость: ошибка позиции TCP (см) + ошибка угла оси (град), с весом позиции.</summary>
        private static float Cost(PoseValidator v, double[] q, Vector3 target, Vector3 aDes, float wPos)
        {
            float e = Vector3.Distance(v.TcpAt(q), target) * 100f;
            float ang = Vector3.Angle(ToolAxis(v, q), aDes);
            return wPos * e * e + ang * ang;
        }

        /// <summary>Производная стоимости по суставу i (центральная разность, на градус).</summary>
        private static float Gradient(PoseValidator v, double[] q, int i, Vector3 target,
            Vector3 aDes, float wPos)
        {
            const float h = 1.5f;
            double save = q[i];
            q[i] = save + h; float plus = Cost(v, q, target, aDes, wPos);
            q[i] = save - h; float minus = Cost(v, q, target, aDes, wPos);
            q[i] = save;
            return (plus - minus) / (2f * h);
        }
    }
}
