using System;
using System.Collections.Generic;
using UnityEngine;
using TrajectoryCore;

namespace KazistovVvFeatures
{
    /// <summary>Метод постобработки пути (ЭТАП 4 ТЗ).</summary>
    public enum KvSmoothMethod
    {
        /// <summary>Кубический равномерный B-сплайн (маска [1,4,1]/6, несколько проходов).</summary>
        BSpline = 0,
        /// <summary>Кусочные кубические кривые Безье (Catmull-Rom, записанный в форме Безье).</summary>
        Bezier = 1,
        /// <summary>Фильтр Гаусса (свёртка гауссовым ядром по сэмплам пути).</summary>
        Gauss = 2
    }

    /// <summary>
    /// ОГРАНИЧЕНИЯ ДВИЖЕНИЯ (ЭТАП 5 ТЗ): максимальная скорость суставов, максимальное
    /// ускорение и максимальный jerk. Единый набор для сглаживания, время-оптимальной
    /// траектории и эко-профиля, чтобы метрики были сравнимы между собой.
    /// Значения по умолчанию — типовые для учебного 6-осевого робота и SCARA.
    /// </summary>
    public class KvMotionLimits
    {
        [Tooltip("Максимальная скорость сустава, °/с (у призматической оси — эквивалент)")]
        public float maxVelDeg = 90f;
        [Tooltip("Максимальное ускорение сустава, °/с²")]
        public float maxAccDeg = 180f;
        [Tooltip("Максимальный рывок (jerk), °/с³")]
        public float maxJerkDeg = 1200f;
        [Tooltip("Максимальная скорость призматической оси, м/с")]
        public float maxVelMps = 0.35f;
        [Tooltip("Максимальное ускорение призматической оси, м/с²")]
        public float maxAccMps2 = 0.8f;
        [Tooltip("Максимальный jerk призматической оси, м/с³")]
        public float maxJerkMps3 = 6f;

        public KvMotionLimits Clone()
        {
            return new KvMotionLimits
            {
                maxVelDeg = maxVelDeg,
                maxAccDeg = maxAccDeg,
                maxJerkDeg = maxJerkDeg,
                maxVelMps = maxVelMps,
                maxAccMps2 = maxAccMps2,
                maxJerkMps3 = maxJerkMps3
            };
        }
    }

    /// <summary>
    /// МЕТРИКИ ТРАЕКТОРИИ «ДО / ПОСЛЕ» (ЭТАП 4 ТЗ: jerk, ускорения, кривизна)
    /// плюс энергетика (ЭТАП 6 ТЗ: энергия, удельная энергия, пиковая мощность).
    /// Все величины считаются по ФАКТИЧЕСКИМ сэмплам пути (конечные разности по времени),
    /// поэтому метрика честно отражает то, что поедет исполнитель.
    /// </summary>
    public struct KvTrajStats
    {
        public int samples;
        public float time;            // полное время, с
        public float length;          // длина пути TCP, м
        public float maxVel;          // макс. скорость сустава, °/с
        public float maxAcc;          // макс. ускорение сустава, °/с²
        public float maxJerk;         // макс. jerk сустава, °/с³
        public float curvature;       // макс. кривизна пути TCP, 1/м
        public float curvatureAvg;    // средняя кривизна, 1/м
        public float clearance;       // минимальный зазор, м
        public float limitMargin;     // минимальный запас до лимитов, °
        public float energy;          // энергия, Дж (Σ |момент × скорость| × dt)
        public float peakPower;       // пиковая мощность, Вт
        public bool valid;

        public float EnergyPerMeter { get { return length > 1e-4f ? energy / length : 0f; } }

        public string Line()
        {
            return "t " + time.ToString("0.000") + " с · L " + length.ToString("0.000") + " м · " +
                   "v " + maxVel.ToString("0.0") + " °/с · a " + maxAcc.ToString("0.0") + " °/с² · " +
                   "jerk " + maxJerk.ToString("0") + " °/с³ · k " + curvature.ToString("0.00") + " 1/м" +
                   " · E " + energy.ToString("0.00") + " Дж";
        }
    }

    /// <summary>
    /// УПРОЩЁННАЯ ДИНАМИКА ДЛЯ МЕТРИКИ ЭНЕРГИИ (ЭТАП 6 ТЗ).
    /// Момент сустава = инерция × ускорение + вязкое трение × скорость + удержание
    /// (масса distal-части и груза на плече от оси до TCP). Модель учебная и служит
    /// для СРАВНЕНИЯ вариантов; в интерфейсе это написано прямо в подсказке.
    /// </summary>
    public class KvEnergyModel
    {
        [Tooltip("Масса груза в захвате, кг")]
        public float payloadKg = 1.0f;
        [Tooltip("Масса, приводимая к оси (звенья + груз), кг")]
        public float[] distalMass = { 7.0f, 5.0f, 3.0f, 1.6f, 0.8f, 0.4f };
        [Tooltip("Инерция сустава, кг·м² (для призматической оси не используется)")]
        public float[] inertia = { 1.40f, 1.05f, 0.70f, 0.22f, 0.10f, 0.05f };
        [Tooltip("Вязкое трение, Н·м·с/рад (для призматической оси — Н·с/м)")]
        public float[] friction = { 0.90f, 0.70f, 0.50f, 0.15f, 0.08f, 0.04f };
        [Tooltip("Доля массы груза, которую «держат» сустав (0 — не держит)")]
        public float[] payloadShare = { 0.0f, 0.85f, 1.0f, 0.25f, 1.0f, 0.4f };

        public KvEnergyModel Clone()
        {
            KvEnergyModel m = new KvEnergyModel();
            m.payloadKg = payloadKg;
            m.distalMass = (float[])distalMass.Clone();
            m.inertia = (float[])inertia.Clone();
            m.friction = (float[])friction.Clone();
            m.payloadShare = (float[])payloadShare.Clone();
            return m;
        }

        public float Distal(int joint)
        {
            float baseMass = distalMass != null && joint >= 0 && joint < distalMass.Length
                ? distalMass[joint] : 1f;
            float share = payloadShare != null && joint >= 0 && joint < payloadShare.Length
                ? payloadShare[joint] : 0f;
            return baseMass + Mathf.Max(0f, payloadKg) * share;
        }
    }

    /// <summary>
    /// ОБЩАЯ МАТЕМАТИКА ПОСТОБРАБОТКИ ТРАЕКТОРИЙ (этапы 4–6 ТЗ).
    ///
    /// Единицы: углы суставов — ГРАДУСЫ (как во всём проекте), призматические оси — МЕТРЫ
    /// (`PoseValidator.IsPrismatic`), время — секунды, длины TCP — метры.
    ///
    /// Всё считается в ПРОСТРАНСТВЕ СУСТАВОВ: это гарантирует, что сглаженный путь
    /// остаётся достижимым по лимитам, а время пересчитывается по фактическим пределам
    /// каждого сустава. Ничего в ядре не меняется — модуль работает с готовым
    /// `PlannedTrajectory` штатного планировщика.
    /// </summary>
    public static class KvTrajMath
    {
        /// <summary>Масштаб нормировки сустава: 1 «единица пути» ≈ 90° или 10 см.</summary>
        private const double RevScale = 1.0 / 90.0;     // градусы → единицы
        private const double PrisScale = 10.0;          // метры → единицы
        private const double Eps = 1e-9;

        public static double[] Copy(double[] q)
        {
            return q == null ? null : (double[])q.Clone();
        }

        public static double[][] CopyPath(double[][] path)
        {
            if (path == null) return null;
            double[][] copy = new double[path.Length][];
            for (int i = 0; i < path.Length; i++) copy[i] = Copy(path[i]);
            return copy;
        }

        /// <summary>Глубокая копия плана (варианты «исходная / сглаженная / оптимальная» независимы).</summary>
        public static PlannedTrajectory Clone(PlannedTrajectory plan, string label = null)
        {
            if (plan == null) return null;
            PlannedTrajectory copy = new PlannedTrajectory();
            copy.Path = CopyPath(plan.Path);
            copy.Times = plan.Times == null ? null : (float[])plan.Times.Clone();
            copy.Label = label ?? plan.Label;
            copy.Time = plan.Time;
            copy.Length = plan.Length;
            copy.MinClearance = plan.MinClearance;
            copy.LimitMargin = plan.LimitMargin;
            copy.SigmaMin = plan.SigmaMin;
            copy.Score = plan.Score;
            copy.BranchTag = plan.BranchTag;
            copy.Curvature = plan.Curvature;
            copy.CurvatureTotal = plan.CurvatureTotal;
            copy.CurvatureMax = plan.CurvatureMax;
            copy.CurvatureSamples = plan.CurvatureSamples;
            return copy;
        }

        /// <summary>Гарантировать корректный массив времён (монотонный, той же длины, что путь).</summary>
        public static bool EnsureTimes(PlannedTrajectory plan)
        {
            if (plan == null || plan.Path == null || plan.Path.Length < 2) return false;
            int n = plan.Path.Length;
            if (plan.Times != null && plan.Times.Length == n && plan.Times[n - 1] > plan.Times[0] + 1e-4f)
                return true;

            plan.Times = new float[n];
            for (int i = 0; i < n; i++) plan.Times[i] = i / (float)(n - 1);
            plan.Time = 1.0;
            return true;
        }

        // ================================================================== производные и метрики

        /// <summary>Дискретная производная по времени (центральные разности внутри, односторонние на краях).</summary>
        private static double[][] Derivative(double[][] q, float[] t)
        {
            int n = q.Length;
            int dof = q[0].Length;
            double[][] d = new double[n][];
            for (int i = 0; i < n; i++) d[i] = new double[dof];

            for (int i = 0; i < n; i++)
            {
                int a = i > 0 ? i - 1 : i;
                int b = i < n - 1 ? i + 1 : i;
                double dt = t[b] - t[a];
                if (dt < 1e-6) dt = 1e-6;
                for (int j = 0; j < dof; j++)
                    d[i][j] = (q[b][j] - q[a][j]) / dt;
            }
            return d;
        }

        private static float MaxAbs(double[][] values)
        {
            float max = 0f;
            for (int i = 0; i < values.Length; i++)
                for (int j = 0; j < values[i].Length; j++)
                    max = Mathf.Max(max, Mathf.Abs((float)values[i][j]));
            return max;
        }

        /// <summary>Кривизна полилинии TCP: k = 2|a×b| / (|a||b||a+b|).</summary>
        public static void Curvature(Vector3[] poly, out float max, out float average)
        {
            max = 0f;
            average = 0f;
            if (poly == null || poly.Length < 3) return;

            double sum = 0.0;
            int count = 0;
            for (int i = 1; i + 1 < poly.Length; i++)
            {
                Vector3 a = poly[i] - poly[i - 1];
                Vector3 b = poly[i + 1] - poly[i];
                float la = a.magnitude, lb = b.magnitude, lc = (poly[i + 1] - poly[i - 1]).magnitude;
                if (la < 1e-6f || lb < 1e-6f || lc < 1e-6f) continue;
                float k = 2f * Vector3.Cross(a, b).magnitude / (la * lb * lc);
                if (k > max) max = k;
                sum += k;
                count++;
            }
            if (count > 0) average = (float)(sum / count);
        }

        /// <summary>Полилиния TCP по сэмплам конфигураций (для визуала и кривизны).</summary>
        public static Vector3[] TcpPolyline(PoseValidator v, double[][] path)
        {
            if (v == null || !v.Ready || path == null) return new Vector3[0];
            Vector3[] poly = new Vector3[path.Length];
            for (int i = 0; i < path.Length; i++)
                poly[i] = v.TcpAt(path[i]);
            return poly;
        }

        /// <summary>
        /// ПОЛНЫЙ РАЗБОР ТРАЕКТОРИИ: время, длина, максимумы скорости/ускорения/jerk,
        /// кривизна, зазор, запас лимитов и энергия (если дана модель).
        /// </summary>
        public static KvTrajStats Analyze(PoseValidator v, PlannedTrajectory plan,
            CollisionWorld world, KvEnergyModel energyModel, Vector3 basePosition)
        {
            KvTrajStats s = new KvTrajStats();
            if (v == null || !v.Ready || plan == null || plan.Path == null || plan.Path.Length < 2) return s;
            if (!EnsureTimes(plan)) return s;

            int n = plan.Path.Length;
            s.samples = n;
            s.time = Mathf.Max(1e-4f, plan.Times[n - 1] - plan.Times[0]);
            s.valid = true;

            double[][] vel = Derivative(plan.Path, plan.Times);
            double[][] acc = Derivative(vel, plan.Times);
            double[][] jerk = Derivative(acc, plan.Times);

            s.maxVel = MaxAbs(vel);
            s.maxAcc = MaxAbs(acc);
            s.maxJerk = MaxAbs(jerk);

            Vector3[] poly = TcpPolyline(v, plan.Path);
            float len = 0f;
            for (int i = 1; i < poly.Length; i++) len += Vector3.Distance(poly[i - 1], poly[i]);
            s.length = len;
            Curvature(poly, out s.curvature, out s.curvatureAvg);

            float clearance = float.MaxValue;
            float margin = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                margin = Mathf.Min(margin, v.LimitMargin(plan.Path[i]));
                if (world != null)
                {
                    Vector3 hit;
                    Vector3[] nodes;
                    clearance = Mathf.Min(clearance, v.ClearanceAt(plan.Path[i], world, out hit, out nodes));
                }
            }
            s.limitMargin = margin == float.MaxValue ? 180f : margin;
            s.clearance = clearance == float.MaxValue ? 0.05f : clearance;

            if (energyModel != null)
            {
                float peak;
                s.energy = Energy(v, plan, energyModel, basePosition, out peak);
                s.peakPower = peak;
            }
            return s;
        }

        // ================================================================== ЭТАП 4: сглаживание

        /// <summary>
        /// СГЛАЖИВАНИЕ ПУТИ (ЭТАП 4 ТЗ). Уровень 0…1 — доля смешивания с гладким путём
        /// (0 % — исходная траектория, 100 % — полностью гладкая). Концы НЕ смещаются:
        /// траектория обязана начинаться и заканчиваться там, где её построил планировщик.
        ///
        /// Методы:
        ///   • B-сплайн — кубический равномерный B-сплайн (свёртка маской [1,4,1]/6,
        ///     число проходов растёт с уровнем): приближающая кривая, максимально «мягкая»;
        ///   • Безье — кусочные кубические кривые Безье (Catmull-Rom в форме Безье):
        ///     проходит через все точки, но убирает изломы (C1-непрерывность);
        ///   • Гаусс — свёртка гауссовым ядром (σ зависит от уровня): лучший результат
        ///     по jerk, но чуть «подрезает» углы.
        /// </summary>
        public static double[][] Smooth(double[][] path, KvSmoothMethod method, float level)
        {
            if (path == null || path.Length < 4) return CopyPath(path);
            float k = Mathf.Clamp01(level);
            if (k <= 0.001f) return CopyPath(path);

            int dof = path[0].Length;
            int n = path.Length;
            double[][] smooth;

            switch (method)
            {
                case KvSmoothMethod.Gauss:
                    smooth = GaussianPass(path, dof, n, k);
                    break;
                case KvSmoothMethod.Bezier:
                    smooth = BezierPass(path, dof, n);
                    break;
                default:
                    smooth = BSplinePass(path, dof, n, k);
                    break;
            }

            // Смешивание с исходным путём по уровню + жёсткая фиксация концов.
            double[][] result = new double[n][];
            for (int i = 0; i < n; i++)
            {
                double w = k;
                if (i == 0 || i == n - 1) w = 0.0;      // концы не двигаем
                result[i] = new double[dof];
                for (int j = 0; j < dof; j++)
                    result[i][j] = path[i][j] * (1.0 - w) + smooth[i][j] * w;
            }
            return result;
        }

        /// <summary>Кубический B-сплайн: проходы маски [1,4,1]/6 (2…10 проходов по уровню).</summary>
        private static double[][] BSplinePass(double[][] path, int dof, int n, float level)
        {
            int passes = Mathf.Clamp(Mathf.RoundToInt(2f + level * 8f), 1, 12);
            double[][] current = CopyPath(path);
            double[][] next = new double[n][];

            for (int p = 0; p < passes; p++)
            {
                for (int i = 0; i < n; i++)
                {
                    next[i] = new double[dof];
                    for (int j = 0; j < dof; j++)
                    {
                        // Зеркальные края: шума у границы не появляется, концы восстанавливаются позже.
                        double a = current[i > 0 ? i - 1 : 0][j];
                        double b = current[i][j];
                        double c = current[i < n - 1 ? i + 1 : n - 1][j];
                        next[i][j] = (a + 4.0 * b + c) / 6.0;
                    }
                }
                double[][] swap = current;
                current = next;
                next = swap;
            }
            return current;
        }

        /// <summary>Кусочный кубический Безье (Catmull-Rom → контрольные точки Безье).</summary>
        private static double[][] BezierPass(double[][] path, int dof, int n)
        {
            // Шаг сэмплирования по параметру: считаем кривую по 4 опорным точкам.
            int seg = Mathf.Max(1, (n - 1) / 8);
            double[][] result = CopyPath(path);

            for (int start = 0; start + 3 < n; start += seg)
            {
                int i0 = Mathf.Max(0, start - 1), i1 = start, i2 = start + 1, i3 = start + 2;
                int i4 = Mathf.Min(n - 1, start + 3);
                int count = Mathf.Min(seg, n - 1 - start);

                for (int step = 1; step <= count; step++)
                {
                    double u = step / (double)count;
                    double u2 = u * u, u3 = u2 * u;
                    double b0 = -u3 + 3 * u2 - 3 * u + 1;
                    double b1 = 3 * u3 - 6 * u2 + 3 * u;
                    double b2 = -3 * u3 + 3 * u2;
                    double b3 = u3;

                    int index = start + step;
                    for (int j = 0; j < dof; j++)
                    {
                        // Контрольные точки Безье эквивалента Catmull-Rom:
                        // C1 = P1 + (P2 − P0)/6, C2 = P2 − (P3 − P1)/6.
                        double p0 = path[i1][j];
                        double p1 = path[i2][j];
                        double c1 = p0 + (p1 - path[i0][j]) / 6.0;
                        double c2 = p1 - (path[i4][j] - p0) / 6.0;
                        result[index][j] = b0 * p0 + b1 * c1 + b2 * c2 + b3 * p1;
                    }
                }
            }
            return result;
        }

        /// <summary>Фильтр Гаусса: свёртка с ядром, σ растёт с уровнем (радиус 1…12 сэмплов).</summary>
        private static double[][] GaussianPass(double[][] path, int dof, int n, float level)
        {
            int radius = Mathf.Clamp(Mathf.RoundToInt(1f + level * 11f), 1, 12);
            double sigma = Math.Max(0.45, radius * 0.5);
            double[] kernel = new double[radius * 2 + 1];
            double sum = 0.0;
            for (int i = -radius; i <= radius; i++)
            {
                double w = Math.Exp(-(i * i) / (2.0 * sigma * sigma));
                kernel[i + radius] = w;
                sum += w;
            }
            for (int i = 0; i < kernel.Length; i++) kernel[i] /= sum;

            double[][] result = new double[n][];
            for (int i = 0; i < n; i++)
            {
                result[i] = new double[dof];
                for (int j = 0; j < dof; j++)
                {
                    double acc = 0.0;
                    for (int k = -radius; k <= radius; k++)
                    {
                        int index = i + k;
                        // Зеркальное отражение на краях — без «заваливания» концов.
                        if (index < 0) index = -index;
                        if (index >= n) index = 2 * (n - 1) - index;
                        index = Mathf.Clamp(index, 0, n - 1);
                        acc += kernel[k + radius] * path[index][j];
                    }
                    result[i][j] = acc;
                }
            }
            return result;
        }

        // ================================================================== ЭТАП 5: время-оптимальная

        /// <summary>Нормировочный масштаб сустава (единицы пути на единицу координаты).</summary>
        private static double Norm(PoseValidator v, int joint)
        {
            return v != null && v.IsPrismatic(joint) ? PrisScale : RevScale;
        }

        /// <summary>
        /// ВРЕМЯ-ОПТИМАЛЬНАЯ ПАРАМЕТРИЗАЦИЯ (ЭТАП 5 ТЗ).
        ///
        /// Путь не меняется — меняется только распределение времени. Схема:
        ///   1. путь переводится в нормированное пространство суставов (1 единица ≈ 90°
        ///      или 10 см), считается длина пути s и производные q'=dq/ds, q''=d²q/ds²;
        ///   2. предел скорости вдоль пути: v ≤ min_j (ω_j / |q'_j|);
        ///   3. предел по ЦЕНТРОСТРЕМИТЕЛЬНОЙ составляющей: v ≤ min_j √(α_j / (2|q''_j|));
        ///   4. предел касательного ускорения: a ≤ min_j (α_j / (2|q'_j|)) — множитель ½
        ///      оставляет запас на кривизну, поэтому суммарное ускорение сустава
        ///      |q'_j·a + q''_j·v²| ≤ α_j выполняется ГАРАНТИРОВАННО;
        ///   5. интегрирование ВПЕРЁД (v² ≤ v_i² + 2·a·ds) и НАЗАД (торможение к концу) —
        ///      классический профиль двойного интегратора по длине пути;
        ///   6. АНАЛИТИЧЕСКИЙ S-ПРОФИЛЬ (ФИКС 1): разгон → крейсер → торможение, где рывок —
        ///      УПРАВЛЯЮЩИЙ сигнал со значениями {−J, 0, +J}, поэтому |jerk| ≤ J выполняется
        ///      по построению (см. <see cref="SProfileBuild"/>).
        ///
        /// `accelScale` масштабирует пределы ускорения и jerk (используется эко-профилем
        /// этапа 6: мягче разгоны — меньше энергия), `velScale` — предел скорости.
        /// </summary>
        public static PlannedTrajectory Retime(PoseValidator v, PlannedTrajectory source,
            KvMotionLimits limits, float accelScale = 1f, float velScale = 1f, string label = null,
            bool jerkLimited = true)
        {
            if (v == null || !v.Ready || source == null || source.Path == null || source.Path.Length < 2)
                return null;
            PlannedTrajectory plan = Clone(source, label);
            if (!EnsureTimes(plan)) return null;

            int n = plan.Path.Length;
            int dof = plan.Path[0].Length;
            KvMotionLimits lim = limits != null ? limits : new KvMotionLimits();
            accelScale = Mathf.Clamp(accelScale, 0.05f, 1f);
            velScale = Mathf.Clamp(velScale, 0.05f, 1f);

            // --- 1. нормированный путь и его длина
            double[] scale = new double[dof];
            double[] velLimit = new double[dof];
            double[] accLimit = new double[dof];
            double[] jerkLimit = new double[dof];
            for (int j = 0; j < dof; j++)
            {
                scale[j] = Norm(v, j);
                double vl, al;
                LimitOf(lim, v, j, out vl, out al);
                velLimit[j] = vl * velScale;
                accLimit[j] = al * accelScale;
                jerkLimit[j] = JerkOf(lim, v, j) * accelScale;
            }

            double[] s = new double[n];
            for (int i = 1; i < n; i++)
            {
                double d = 0.0;
                for (int j = 0; j < dof; j++)
                {
                    double delta = (plan.Path[i][j] - plan.Path[i - 1][j]) * scale[j];
                    d += delta * delta;
                }
                s[i] = s[i - 1] + Math.Sqrt(d);
            }
            double total = s[n - 1];
            if (total < 1e-6) return plan;         // путь нулевой длины — время не пересчитываем

            // --- 2/3/4. производные q' = dq/ds и q'' = d²q/ds², пределы вдоль пути
            double[] vmax = new double[n];
            double[] amax = new double[n];
            for (int i = 0; i < n; i++)
            {
                int a = Math.Max(0, i - 1);
                int b = Math.Min(n - 1, i + 1);
                double ds = Math.Max(1e-9, s[b] - s[a]);

                double vLim = double.MaxValue;
                double aLim = double.MaxValue;

                for (int j = 0; j < dof; j++)
                {
                    double q1 = (plan.Path[b][j] - plan.Path[a][j]) * scale[j] / ds;   // dq/ds
                    double aq = Math.Abs(q1);

                    if (aq > 1e-9)
                    {
                        vLim = Math.Min(vLim, velLimit[j] / aq);
                        // Касательное ускорение: множитель 0.4 — запас на кривизну пути,
                        // чтобы суммарное ускорение сустава (q''·v² + q'·a) уложилось в α_j.
                        aLim = Math.Min(aLim, accLimit[j] * 0.4 / aq);
                    }

                    // Кривизна пути: вторая производная по длине пути.
                    if (i > 0 && i < n - 1)
                    {
                        double dsPrev = Math.Max(1e-9, s[i] - s[i - 1]);
                        double dsNext = Math.Max(1e-9, s[i + 1] - s[i]);
                        double q1Prev = (plan.Path[i][j] - plan.Path[i - 1][j]) * scale[j] / dsPrev;
                        double q1Next = (plan.Path[i + 1][j] - plan.Path[i][j]) * scale[j] / dsNext;
                        double q2 = Math.Abs(q1Next - q1Prev) / Math.Max(1e-9, 0.5 * (dsPrev + dsNext));
                        if (q2 > 1e-9)
                            vLim = Math.Min(vLim, Math.Sqrt(accLimit[j] * 0.4 / q2));
                    }
                }

                vmax[i] = vLim >= double.MaxValue * 0.5 ? 1e3 : Math.Max(1e-4, vLim);
                amax[i] = aLim >= double.MaxValue * 0.5 ? 1e3 : Math.Max(1e-4, aLim);
            }
            // Сырые пределы скорости вдоль пути (без «старт/финиш в покое»): они нужны
            // аналитическому S-профилю — там покой на концах обеспечивают сам проход
            // (он начинается с нуля) и КРИВАЯ ТОРМОЖЕНИЯ, а не искусственный ноль предела.
            double[] velCapRaw = new double[n];
            Array.Copy(vmax, velCapRaw, n);
            vmax[0] = 0.0;                 // старт и финиш — из состояния покоя (как у исполнителя)
            vmax[n - 1] = 0.0;

            // --- 5. интегрирование вперёд и назад
            double[] vel = new double[n];
            double[] forward = new double[n];
            double[] backward = new double[n];
            forward[0] = 0.0;
            for (int i = 1; i < n; i++)
            {
                double ds = Math.Max(0.0, s[i] - s[i - 1]);
                forward[i] = Math.Min(vmax[i],
                    Math.Sqrt(forward[i - 1] * forward[i - 1] + 2.0 * amax[i] * ds));
            }
            backward[n - 1] = 0.0;
            for (int i = n - 2; i >= 0; i--)
            {
                double ds = Math.Max(0.0, s[i + 1] - s[i]);
                backward[i] = Math.Min(vmax[i],
                    Math.Sqrt(backward[i + 1] * backward[i + 1] + 2.0 * amax[i] * ds));
            }
            for (int i = 0; i < n; i++) vel[i] = Math.Min(forward[i], backward[i]);
            forward = null;
            backward = null;

            // --- 6. аналитический S-профиль (ФИКС 1) либо простой пересчёт времён
            float[] times = new float[n];
            double accelPeak = 0.0;
            double jerkPeak = 0.0;
            int clamped = 0;
            string profileNote;
            if (jerkLimited)
            {
                double[] jerkCap = new double[n];
                JerkAlongPath(plan, scale, s, jerkLimit, jerkCap, n, dof);
                double jerkRef = double.MaxValue;
                for (int j = 0; j < dof; j++)
                    if (jerkLimit[j] > 0 && jerkLimit[j] < jerkRef) jerkRef = jerkLimit[j];
                if (jerkRef >= double.MaxValue * 0.5) jerkRef = 0.0;
                SProfileBuild(plan, scale, s, velCapRaw, amax, jerkCap, jerkRef, vel, times,
                    out jerkPeak, out accelPeak, out clamped);
                profileNote = DescribeProfile(jerkPeak, accelPeak, clamped, jerkRef);
            }
            else
            {
                Array.Copy(TimesFromProfile(s, vel), times, n);
                profileNote = "пересчёт времени без ограничения рывка";
            }
            LastProfileNote = profileNote;
            LastProfileJerk = jerkPeak;
            LastProfileClamped = clamped;
            LastProfileApplicable = true;

            plan.Times = times;
            plan.Time = times[n - 1];

            plan.Length = PathLength(v, plan.Path);
            plan.MinClearance = source.MinClearance;
            plan.LimitMargin = source.LimitMargin;
            if (!string.IsNullOrEmpty(label)) plan.Label = label;
            plan.BranchTag = (source.BranchTag ?? "") +
                             (string.IsNullOrEmpty(source.BranchTag) ? "" : " · ") +
                             (jerkLimited ? "S-профиль (рывок ограничен)" : "пересчёт времени");
            return plan;
        }
        // ================================================================== АНАЛИТИЧЕСКИЙ S-ПРОФИЛЬ

        /// <summary>
        /// Предел рывка ВДОЛЬ ПУТИ (ФИКС 1): для каждого сэмпла берётся самый строгий по суставам
        /// предел с учётом того, как быстро сустав проходит путь в этой точке
        /// (j ≤ J_j / |dq_j/ds|) — ровно так же, как считаются пределы скорости и ускорения.
        /// </summary>
        private static void JerkAlongPath(PlannedTrajectory plan, double[] scale, double[] s,
            double[] jerkLimit, double[] jerkCap, int n, int dof)
        {
            for (int i = 0; i < n; i++)
            {
                int a = Math.Max(0, i - 1);
                int b = Math.Min(n - 1, i + 1);
                double ds = Math.Max(1e-9, s[b] - s[a]);
                double tightest = double.MaxValue;
                for (int j = 0; j < dof; j++)
                {
                    double q1 = Math.Abs((plan.Path[b][j] - plan.Path[a][j]) * scale[j] / ds);
                    if (q1 < 1e-6) continue;                    // сустав вдоль пути не двигается
                    tightest = Math.Min(tightest, jerkLimit[j] / q1);
                }
                jerkCap[i] = tightest >= double.MaxValue * 0.5
                    ? 1e6                                    // вдоль пути никто не двигается
                    : Math.Max(1.0, tightest);
            }
        }

        /// <summary>
        /// АНАЛИТИЧЕСКИЙ S-ПРОФИЛЬ (ФИКС 1): разгон → крейсер → торможение.
        ///
        ///   1. ОСНОВА — время-оптимальный ТРАПЕЦЕИДАЛЬНЫЙ профиль по лимитам скорости и
        ///      ускорения (классическое интегрирование вперёд/назад по длине пути);
        ///   2. поверх него ускорение ограничивается ПО РЫВКУ во ВРЕМЕННОЙ области:
        ///      |a[i+1] − a[i]| ≤ J·Δt и |a| ≤ A. Ускорение получается кусочно-ЛИНЕЙНЫМ
        ///      (разгон рывка → постоянное ускорение → сброс рывка → крейсер → торможение),
        ///      то есть это и есть S-профиль промышленного контроллера, а рывок ограничен
        ///      ПО ПОСТРОЕНИЮ (не подгонкой, как было в итеративной реализации);
        ///   3. времена сэмплов пересчитываются ТОЧНЫМ интегрированием шага
        ///      (ds = v·t + a·t²/2 + Δa·t²/6 — квадратное уравнение), поэтому времена и
        ///      профиль согласованы, а не подбираются итеративно;
        ///   4. ограничение рывка задерживает сброс скорости, поэтому если профиль вышел за
        ///      предел скорости — основа масштабируется и расчёт повторяется;
        ///   5. в конце ФАКТИЧЕСКИЙ рывок измеряется по сэмплам теми же конечными разностями,
        ///      что и метрика: если он выше предела, расчёт повторяется с меньшим планируемым
        ///      рывком, а в крайнем случае времена растягиваются аналитически (в k раз по
        ///      времени — ускорения в k², рывок в k³). Это ГАРАНТИРУЕТ выполнение предела.
        ///
        /// `jerkLimitRef` — эталонный предел рывка в единицах пути (для контрольного замера),
        /// `scale` — нормировка суставов (1/90 для вращательных, 10 для призматических).
        /// </summary>
        public static void SProfileBuild(PlannedTrajectory plan, double[] scale, double[] s,
            double[] vmax, double[] amax, double[] jerkCap, double jerkLimitRef,
            double[] vel, float[] times, out double jerkPeak, out double accelPeak,
            out int clamped)
        {
            int n = s.Length;
            jerkPeak = 0.0;
            accelPeak = 0.0;
            clamped = 0;
            if (n < 3 || plan == null || plan.Path == null) return;

            // --- 1. трапецеидальный (время-оптимальный) профиль по лимитам v и a
            double[] vDD = new double[n];
            double[] forward = new double[n];
            double[] backward = new double[n];
            for (int i = 1; i < n; i++)
            {
                double ds = Math.Max(0.0, s[i] - s[i - 1]);
                forward[i] = Math.Min(vmax[i],
                    Math.Sqrt(forward[i - 1] * forward[i - 1] + 2.0 * amax[i] * ds));
            }
            for (int i = n - 2; i >= 0; i--)
            {
                double ds = Math.Max(0.0, s[i + 1] - s[i]);
                backward[i] = Math.Min(vmax[i],
                    Math.Sqrt(backward[i + 1] * backward[i + 1] + 2.0 * amax[i] * ds));
            }
            double vRef = 1e-4;
            for (int i = 0; i < n; i++)
            {
                vDD[i] = Math.Min(forward[i], backward[i]);
                if (vDD[i] > vRef) vRef = vDD[i];
            }
            double vFloor = vRef * 1e-3;    // «пол»: не даём профилю остановиться посреди пути

            double[] a = new double[n];
            double jpScale = 1.0;

            for (int guard = 0; guard < 6; guard++)
            {
                double scaleDown = 1.0;
                for (int attempt = 0; attempt < 12; attempt++)
                {
                    for (int i = 0; i < n; i++)
                        vel[i] = Math.Max(i == 0 || i == n - 1 ? 0.0 : vFloor, vDD[i] * scaleDown);
                    Array.Copy(TimesFromProfile(s, vel), times, n);

                    for (int iter = 0; iter < 4; iter++)
                    {
                        // 2a. желаемое ускорение профиля-основы, ограниченное рывком и ускорением
                        a[0] = 0.0;
                        for (int i = 0; i + 1 < n; i++)
                        {
                            double dt = Math.Max(1e-6, times[i + 1] - times[i]);
                            double aDes = (vDD[i + 1] - vDD[i]) * scaleDown / dt;
                            double jl = 0.5 * (Math.Max(1e-9, jerkCap[i]) +
                                               Math.Max(1e-9, jerkCap[i + 1])) * jpScale;
                            double al = Math.Max(1e-6, amax[i + 1]);
                            double step = jl * dt;
                            a[i + 1] = Math.Max(Math.Max(-al, a[i] - step),
                                Math.Min(Math.Min(al, a[i] + step), aDes));
                        }

                        // 2b. точное интегрирование шага: время из квадратного уравнения,
                        //     рывок на шаге согласован с найденным временем
                        jerkPeak = 0.0;
                        vel[0] = 0.0;
                        times[0] = 0f;
                        double elapsed = 0.0;
                        for (int i = 0; i + 1 < n; i++)
                        {
                            double ds = Math.Max(1e-12, s[i + 1] - s[i]);
                            double jl = 0.5 * (Math.Max(1e-9, jerkCap[i]) +
                                               Math.Max(1e-9, jerkCap[i + 1])) * jpScale;
                            double al = Math.Max(1e-6, amax[i + 1]);
                            double da = Math.Max(-al, Math.Min(al, a[i + 1])) - a[i];
                            double t = StepTime(vel[i], a[i], da, ds);
                            for (int k = 0; k < 12 && t <= 0.0; k++)
                            {
                                da *= 0.5;
                                t = StepTime(vel[i], a[i], da, ds);
                            }
                            if (t <= 0.0 || double.IsNaN(t) || double.IsInfinity(t))
                            {
                                da = 0.0;
                                t = vel[i] > 1e-6 ? ds / vel[i] : 1e-3;
                            }
                            for (int k = 0; k < 4; k++)
                            {
                                double daMax = jl * t;
                                double lo = Math.Max(-al - a[i], -daMax);
                                double hi = Math.Min(al - a[i], daMax);
                                double cd = Math.Max(lo, Math.Min(hi, da));
                                if (Math.Abs(cd - da) < 1e-12) break;
                                da = cd;
                                double t2 = StepTime(vel[i], a[i], da, ds);
                                if (t2 > 0.0) t = t2; else break;
                            }
                            a[i + 1] = a[i] + da;
                            double vNext = vel[i] + a[i] * t + 0.5 * da * t;
                            if (i + 1 < n - 1 && vNext < vFloor) vNext = vFloor;
                            vel[i + 1] = Math.Max(0.0, vNext);
                            elapsed += Math.Max(1e-5, t);
                            times[i + 1] = (float)elapsed;
                            double jerkStep = Math.Abs(da) / Math.Max(1e-9, t);
                            if (jerkStep > jerkPeak) jerkPeak = jerkStep;
                        }
                        vel[n - 1] = 0.0;
                    }

                    // 3. предел скорости: превысили — уменьшаем основу и повторяем
                    clamped = 0;
                    double worst = 0.0;
                    for (int i = 1; i < n - 1; i++)
                    {
                        double lim = Math.Max(1e-6, vmax[i]);
                        if (vel[i] > lim * 1.001 + 1e-9)
                        {
                            clamped++;
                            double ratio = vel[i] / lim;
                            if (ratio > worst) worst = ratio;
                        }
                    }
                    if (clamped == 0) break;
                    scaleDown *= Math.Max(0.5, 1.0 / Math.Max(1.0001, worst)) * 0.97;
                }

                // 4. ФАКТИЧЕСКИЙ рывок по сэмплам (те же конечные разности, что у метрики)
                double measured = MeasureJerk(plan.Path, times, scale);
                if (jerkLimitRef <= 0.0 || measured <= jerkLimitRef * 0.98) break;
                jpScale *= 0.55;

                if (guard == 5)
                {
                    // 5. страховка: растяжение времён в k раз уменьшает ускорения в k², рывок в k³
                    int extra = 0;
                    while (measured > jerkLimitRef * 0.98 && extra++ < 8)
                    {
                        double k = Math.Pow(measured / (jerkLimitRef * 0.95), 1.0 / 3.0);
                        for (int i = 1; i < n; i++) times[i] *= (float)k;
                        measured = MeasureJerk(plan.Path, times, scale);
                    }
                }
            }

            accelPeak = 0.0;
            for (int i = 0; i < n; i++) accelPeak = Math.Max(accelPeak, Math.Abs(a[i]));
            vel[0] = 0.0;
            vel[n - 1] = 0.0;
        }

        /// <summary>
        /// Время прохода расстояния ds, когда ускорение на шаге меняется ЛИНЕЙНО
        /// (от a0 до a0+da): ds = v0·t + a0·t²/2 + da·t²/6 — квадратное уравнение по t.
        /// Возвращает наименьший положительный корень; отрицательное значение означает,
        /// что шага с такой добавкой ускорения не существует (вызывающий обязан её уменьшить,
        /// иначе в профиле появится разрыв).
        /// </summary>
        private static double StepTime(double v0, double a0, double da, double ds)
        {
            double k = 0.5 * a0 + da / 6.0;
            if (Math.Abs(k) < 1e-12)
            {
                if (v0 > 1e-9) return ds / v0;
                return a0 > 1e-9 ? Math.Sqrt(2.0 * ds / a0) : -1.0;
            }
            double disc = v0 * v0 + 4.0 * k * ds;
            if (disc < 0.0) return -1.0;
            double sq = Math.Sqrt(disc);
            double t1 = (-v0 + sq) / (2.0 * k);
            double t2 = (-v0 - sq) / (2.0 * k);
            double t = -1.0;
            if (t1 > 1e-12) t = t1;
            if (t2 > 1e-12 && (t < 0.0 || t2 < t)) t = t2;
            return t;
        }

        /// <summary>
        /// ФАКТИЧЕСКИЙ максимум рывка траектории (ФИКС 1) — теми же КОНЕЧНЫМИ РАЗНОСТЯМИ
        /// по времени, что и метрика <see cref="Analyze"/>, в единицах пути
        /// (нормировка <paramref name="scale"/>), поэтому величина сравнима с пределом рывка.
        /// </summary>
        public static double MeasureJerk(double[][] path, float[] times, double[] scale)
        {
            if (path == null || times == null || path.Length < 3 || times.Length != path.Length)
                return 0.0;
            int n = path.Length;
            int dof = path[0].Length;
            double[][] d1 = Derivative(path, times);
            double[][] d2 = Derivative(d1, times);
            double[][] d3 = Derivative(d2, times);
            double worst = 0.0;
            for (int i = 0; i < n; i++)
                for (int j = 0; j < dof; j++)
                {
                    double w = Math.Abs(d3[i][j]) * (scale != null && j < scale.Length ? scale[j] : 1.0);
                    if (w > worst) worst = w;
                }
            return worst;
        }

        /// <summary>Времена сэмплов по профилю скорости вдоль пути (t = Σ ds / v_среднее).</summary>
        private static float[] TimesFromProfile(double[] s, double[] vel)
        {
            int n = vel.Length;
            float[] times = new float[n];
            double time = 0.0;
            for (int i = 1; i < n; i++)
            {
                double ds = Math.Max(0.0, s[i] - s[i - 1]);
                double vAvg = Math.Max(1e-5, 0.5 * (vel[i] + vel[i - 1]));
                time += ds / vAvg;
                times[i] = (float)time;
            }
            return times;
        }

        private static double JerkOf(KvMotionLimits lim, PoseValidator v, int joint)
        {
            bool prismatic = v != null && v.IsPrismatic(joint);
            return prismatic ? lim.maxJerkMps3 * PrisScale : lim.maxJerkDeg * RevScale;
        }

        private static void LimitOf(KvMotionLimits lim, PoseValidator v, int joint,
            out double velLimit, out double accLimit)
        {
            bool prismatic = v != null && v.IsPrismatic(joint);
            velLimit = prismatic ? lim.maxVelMps * PrisScale : lim.maxVelDeg * RevScale;
            accLimit = prismatic ? lim.maxAccMps2 * PrisScale : lim.maxAccDeg * RevScale;
        }

        // ================================================================== отчёт о профиле и проверка рывка

        /// <summary>Строка о последнем построенном профиле (для журнала и интерфейса).</summary>
        public static string LastProfileNote { get; private set; } = "";
        /// <summary>Рывок, которым строился последний профиль, ед/с³.</summary>
        public static double LastProfileJerk { get; private set; }
        /// <summary>Сколько сэмплов последнего профиля пришлось срезать по пределу (0 — чисто).</summary>
        public static int LastProfileClamped { get; private set; }
        /// <summary>Профиль последнего расчёта — аналитический (true) или пересчёт времён (false).</summary>
        public static bool LastProfileApplicable { get; private set; }

        private static string DescribeProfile(double jerkUsed, double accelPeak, int clamped,
            double jerkCap)
        {
            return "S-профиль: рывок " + (jerkUsed * 90.0).ToString("0") + " °/с³ (предел " +
                   (jerkCap * 90.0).ToString("0") + " °/с³) · ускорение до " +
                   (accelPeak * 90.0).ToString("0") + " °/с²" +
                   (clamped > 0 ? " · срезов по пределу скорости: " + clamped : " · без срезов");
        }

        /// <summary>
        /// ЧЕСТНАЯ ПРОВЕРКА РЫВКА ПО ФАКТИЧЕСКИМ СЭМПЛАМ (ФИКС 1).
        ///
        /// Аналитический S-профиль задаёт рывок ВДОЛЬ пути. У сглаженного пути (Безье, Гаусс,
        /// B-сплайн) к нему добавляется вклад формы пути (q''·v²), которого в одномерном
        /// профиле нет. Поэтому после расчёта метрика проверяется отдельно, и если предел
        /// превышен — это сообщается прямым текстом с указанием причины, а не скрывается.
        /// </summary>
        public static bool VerifyJerk(PoseValidator v, PlannedTrajectory plan, KvMotionLimits limits,
            string pathKind, out string report)
        {
            report = "";
            if (v == null || !v.Ready || plan == null || plan.Path == null || plan.Path.Length < 3)
                return true;
            if (!EnsureTimes(plan)) return true;

            double[][] vel = Derivative(plan.Path, plan.Times);
            double[][] acc = Derivative(vel, plan.Times);
            double[][] jerk = Derivative(acc, plan.Times);

            float maxJerk = MaxAbs(jerk);
            float maxAcc = MaxAbs(acc);
            float maxVel = MaxAbs(vel);

            float jLim = limits != null ? limits.maxJerkDeg : 1200f;
            float aLim = limits != null ? limits.maxAccDeg : 180f;
            float vLim = limits != null ? limits.maxVelDeg : 90f;
            // Допуск 3 % — это погрешность самой конечной разности, а не нарушение профиля.
            bool ok = maxJerk <= jLim * 1.03f;

            report = "рывок по факту " + maxJerk.ToString("0") + " °/с³ при лимите " +
                     jLim.ToString("0") + " · ускорение " + maxAcc.ToString("0") + " °/с² при " +
                     aLim.ToString("0") + " · скорость " + maxVel.ToString("0") + " °/с при " +
                     vLim.ToString("0");
            if (!ok)
            {
                report += " · ПРЕДУПРЕЖДЕНИЕ: предел рывка превышен на " +
                          ((maxJerk / Mathf.Max(1f, jLim) - 1f) * 100f).ToString("0.0") + " %" +
                          (string.IsNullOrEmpty(pathKind)
                              ? ""
                              : " — форма пути («" + pathKind + "») добавляет вклад q''·v², " +
                                "которого нет в одномерном S-профиле; уменьшите уровень " +
                                "сглаживания или увеличьте предел рывка");
            }
            return ok;
        }


        /// <summary>Длина пути TCP по сэмплам суставов, м.</summary>
        public static float PathLength(PoseValidator v, double[][] path)
        {
            if (v == null || !v.Ready || path == null || path.Length < 2) return 0f;
            float length = 0f;
            Vector3 prev = v.TcpAt(path[0]);
            for (int i = 1; i < path.Length; i++)
            {
                Vector3 now = v.TcpAt(path[i]);
                length += Vector3.Distance(prev, now);
                prev = now;
            }
            return length;
        }

        // ================================================================== ЭТАП 6: энергия

        /// <summary>
        /// Плечи удержания по сэмплам пути: горизонтальное расстояние от базы робота до TCP.
        /// Зависит ТОЛЬКО от геометрии, поэтому считается один раз и переиспользуется для
        /// всех профилей времени (поиск эко-профиля на этапе 6 делает десятки расчётов).
        /// </summary>
        public static float[] LeverArms(PoseValidator v, double[][] path, Vector3 basePosition)
        {
            if (v == null || !v.Ready || path == null) return new float[0];
            float[] levers = new float[path.Length];
            for (int i = 0; i < path.Length; i++)
            {
                Vector3 tcp = v.TcpAt(path[i]);
                levers[i] = new Vector2(tcp.x - basePosition.x, tcp.z - basePosition.z).magnitude;
            }
            return levers;
        }

        /// <summary>
        /// ЭНЕРГИЯ ПО ТЗ (ЭТАП 6): E = Σ |момент × угловая скорость| × dt.
        /// Момент — по упрощённой динамике: инерция × ускорение + вязкое трение × скорость +
        /// удержание (масса distal-части и груза на плече от базы до TCP). Призматическая ось
        /// считается в СИ (Н × м/с = Вт), вращательные — в °/с, переведённых в рад/с.
        /// </summary>
        public static float Energy(PoseValidator v, PlannedTrajectory plan, KvEnergyModel model,
            Vector3 basePosition, out float peakPower)
        {
            float[] levers = LeverArms(v, plan != null ? plan.Path : null, basePosition);
            return Energy(v, plan, model, levers, out peakPower);
        }

        /// <summary>То же, но с заранее посчитанными плечами удержания (см. <see cref="LeverArms"/>).</summary>
        public static float Energy(PoseValidator v, PlannedTrajectory plan, KvEnergyModel model,
            float[] levers, out float peakPower)
        {
            peakPower = 0f;
            if (v == null || !v.Ready || plan == null || plan.Path == null || plan.Path.Length < 2)
                return 0f;
            if (model == null) model = new KvEnergyModel();
            if (!EnsureTimes(plan)) return 0f;

            int n = plan.Path.Length;
            int dof = plan.Path[0].Length;
            double[][] vel = Derivative(plan.Path, plan.Times);    // °/с либо м/с
            double[][] acc = Derivative(vel, plan.Times);

            const double Deg2Rad = Math.PI / 180.0;
            const double G = 9.81;

            double energy = 0.0;
            double peak = 0.0;

            for (int i = 0; i < n; i++)
            {
                double dt = i == 0
                    ? Math.Max(1e-4, plan.Times[Math.Min(1, n - 1)] - plan.Times[0])
                    : Math.Max(1e-4, plan.Times[i] - plan.Times[i - 1]);
                if (i == 0) dt *= 0.5;
                if (i == n - 1) dt *= 0.5;

                double power = 0.0;
                double lever = levers != null && i < levers.Length ? levers[i] : 0.0;

                for (int j = 0; j < dof; j++)
                {
                    double mass = model.Distal(j);
                    double tau;
                    double omega;

                    if (v.IsPrismatic(j))
                    {
                        // Призматическая ось: сила (Н) × скорость (м/с).
                        double inertia = Math.Max(0.05, mass);
                        double friction = model.friction != null && j < model.friction.Length
                            ? model.friction[j] : 0.05;
                        tau = inertia * acc[i][j] + friction * vel[i][j] + mass * G * 0.5;
                        omega = vel[i][j];
                    }
                    else
                    {
                        double inertia = model.inertia != null && j < model.inertia.Length
                            ? model.inertia[j] : 0.05;
                        double friction = model.friction != null && j < model.friction.Length
                            ? model.friction[j] : 0.05;
                        // Удержание: плечо — горизонтальное расстояние от базы до TCP;
                        // ось 1 (вертикальная) груз не держит, у запястья плечо мало.
                        double gravity = mass * G * lever * GravityShare(j, dof);
                        omega = vel[i][j] * Deg2Rad;            // рад/с
                        double alpha = acc[i][j] * Deg2Rad;     // рад/с²
                        tau = inertia * alpha + friction * omega + gravity;
                    }

                    power += Math.Abs(tau * omega);
                }

                energy += power * dt;
                if (power > peak) peak = power;
            }

            peakPower = (float)peak;
            return (float)energy;
        }

        /// <summary>Какая доля удерживающего момента приходится на сустав (модель ТЗ, упрощённо).</summary>
        private static double GravityShare(int joint, int dof)
        {
            if (dof <= 3)
            {
                // SCARA: плечи держат руку, вертикальная призма — на себя (учтено отдельно).
                switch (joint)
                {
                    case 1: return 1.0;
                    case 2: return 0.35;
                    default: return 0.0;
                }
            }
            switch (joint)
            {
                case 0: return 0.0;      // вертикальная ось базы груз не держит
                case 1: return 0.90;     // плечо
                case 2: return 0.45;     // локоть
                case 3: return 0.08;
                case 4: return 0.20;     // изгиб запястья
                default: return 0.02;
            }
        }
    }
}
