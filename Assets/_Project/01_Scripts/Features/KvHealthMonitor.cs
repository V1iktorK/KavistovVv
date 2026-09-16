using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using KazistovVvUI;
using TrajectoryCore;

namespace KazistovVvFeatures
{
    /// <summary>Одна точка истории телеметрии (для графиков).</summary>
    public struct KvHealthSample
    {
        public float t;             // время от начала мониторинга, с
        public float v;             // значение величины
    }

    /// <summary>
    /// ЭТАП 6 ТЗ — HEALTH MONITOR (мониторинг состояния суставов).
    ///
    /// ЧТО СЧИТАЕТСЯ (симуляция поверх РЕАЛЬНЫХ углов робота, кинематика не меняется):
    ///   * СКОРОСТЬ сустава — по фактическому изменению угла за кадр (у призмы SCARA — м/с);
    ///   * ТОК — «пропорционально нагрузке»: холостой ток + вклад скорости + вклад ускорения.
    ///     Нагрузка считается по моменту «на плече» (для несущих осей — по отклонению от
    ///     вертикали), поэтому ток растёт при движении и при удержании вытянутой руки;
    ///   * ТЕМПЕРАТУРА — тепловой баланс: нагрев ∝ току², охлаждение ∝ (T − T среды).
    ///     Отсюда ровно то поведение, что требует ТЗ: при движении температура РАСТЁТ,
    ///     в покое ПАДАЕТ к температуре среды;
    ///   * ИЗНОС — накопление нагрузки со временем: `wear += k·|ток|·dt`, 0…1 (1 — предел);
    ///   * ПРОГНОЗ РЕСУРСА — по текущей средней скорости износа: сколько часов до предела
    ///     (условного «критического износа»), а также до порога обслуживания.
    ///
    /// ГРАФИКИ — линейные, в реальном времени, каждый сустав своей линией своего цвета
    /// (палитра `KvJointGraph.JointColors`), отдельный график на каждую величину
    /// (температура / ток / скорость / износ). Текстурa перерисовывается не чаще
    /// `redrawInterval`, история — кольцевой буфер, в кадре аллокаций нет.
    ///
    /// ДАННЫЕ ПИШУТСЯ В ФАЙЛ (ТЗ: «Данные сохраняются в файл (для анализа)»):
    /// `%persistentDataPath%/KazistovVv/Logs/health_ГГГГММДД_HHMMSS.csv` — по строке на выборку
    /// (время + по каждому суставу температура, ток, скорость, износ), запись раз в `logInterval`.
    ///
    /// СУСТАВОВ: у робота 6, у SCARA 3 (J1, J2, Z) — число берётся из валидатора.
    /// </summary>
    public class KvHealthMonitor
    {
        public event Action<string> Message;

        // ------------------------------------------------------------------ параметры (публичные)
        [Header("Обновление")]
        public float sampleInterval = 0.05f;      // как часто считаем телеметрию, с
        public float redrawInterval = 0.25f;      // как часто перерисовываем графики, с
        public float logInterval = 1f;            // как часто пишем строку в файл, с
        public int historyLength = 300;           // сколько точек держим (кольцевой буфер)
        public float windowSeconds = 60f;         // окно графика по времени, с

        [Header("Тепловая модель")]
        public float ambientTemperature = 28f;    // температура среды, °C
        public float maxTemperature = 90f;        // предел, °C
        public float heatPerCurrent2 = 0.020f;    // нагрев ∝ I², °C/с на А²
        public float coolingPerDegree = 0.030f;   // охлаждение ∝ (T−Tсреды), 1/с

        [Header("Электрическая модель")]
        public float idleCurrent = 0.45f;         // холостой ток, А
        public float currentPerSpeed = 0.85f;     // А на единицу скорости (рад/с или м/с)
        public float currentPerAccel = 0.12f;     // А на единицу ускорения
        public float loadCurrent = 1.10f;         // добавка за удержание «вытянутой» руки, А

        [Header("Износ и ресурс")]
        public float wearPerAmpSecond = 1.1e-5f;  // износ за 1 А·с
        public float wearServiceLimit = 0.60f;    // порог «пора на обслуживание»
        public float wearCriticalLimit = 1.0f;    // «критический износ»
        public bool logEvents = true;

        // ------------------------------------------------------------------ состояние
        private TrajectoryFlowController flow;
        private PoseValidator v;
        private int dof;
        private RobotController boundRobot;
        private bool bound;

        private double[] prevQ;
        private float[] prevSpeed;
        private float[] temperature;
        private float[] current;
        private float[] speed;
        private float[] accel;
        private float[] wear;

        private readonly List<KvHealthSample>[] historyTemp = { new List<KvHealthSample>(), new List<KvHealthSample>(), new List<KvHealthSample>(), new List<KvHealthSample>(), new List<KvHealthSample>(), new List<KvHealthSample>() };
        private readonly List<KvHealthSample>[] historyAmp = { new List<KvHealthSample>(), new List<KvHealthSample>(), new List<KvHealthSample>(), new List<KvHealthSample>(), new List<KvHealthSample>(), new List<KvHealthSample>() };
        private readonly List<KvHealthSample>[] historySpd = { new List<KvHealthSample>(), new List<KvHealthSample>(), new List<KvHealthSample>(), new List<KvHealthSample>(), new List<KvHealthSample>(), new List<KvHealthSample>() };
        private readonly List<KvHealthSample>[] historyWear = { new List<KvHealthSample>(), new List<KvHealthSample>(), new List<KvHealthSample>(), new List<KvHealthSample>(), new List<KvHealthSample>(), new List<KvHealthSample>() };

        private float timer, clock, logTimer, elapsed;
        private string logPath = "";
        private int logRows;

        public bool Enabled { get; private set; }
        public int Dof { get { return dof; } }
        public string RobotName { get; private set; }

        public float[] Temperature { get { return temperature; } }
        public float[] Current { get { return current; } }
        public float[] Speed { get { return speed; } }
        public float[] Wear { get { return wear; } }
        public float Elapsed { get { return elapsed; } }
        public string LogPath { get { return logPath; } }
        public int LogRows { get { return logRows; } }
        public List<KvHealthSample>[] Histories(int metric)
        {
            switch (metric)
            {
                case 0: return historyTemp;
                case 1: return historyAmp;
                case 2: return historySpd;
                default: return historyWear;
            }
        }

        public KvHealthMonitor()
        {
            RobotName = "";
        }

        public void Bind(TrajectoryFlowController controller)
        {
            flow = controller;
            if (flow != null && flow.Validator != null && flow.Validator.Ready) Rebind(flow.Robot);
        }

        /// <summary>Сменить робота: пересоздать массивы под его число осей (их 6 или 3).</summary>
        public void Rebind(RobotController robot)
        {
            if (robot == null || flow == null || flow.Validator == null || !flow.Validator.Ready) return;
            if (boundRobot == robot) return;
            boundRobot = robot;

            v = flow.Validator;
            dof = Mathf.Clamp(v.Dof, 1, 6);
            RobotName = robot.robotName;

            prevQ = v.CopyCurrent();
            prevSpeed = new float[dof];
            temperature = new float[dof];
            current = new float[dof];
            speed = new float[dof];
            accel = new float[dof];
            wear = new float[dof];

            for (int i = 0; i < dof; i++) temperature[i] = ambientTemperature;
            for (int i = 0; i < historyTemp.Length; i++)
            {
                historyTemp[i].Clear(); historyAmp[i].Clear();
                historySpd[i].Clear(); historyWear[i].Clear();
            }
            clock = 0f;
            elapsed = 0f;
            bound = true;

            OpenLog();
            Debug.Log("[Health] мониторинг привязан к роботу «" + RobotName + "» · суставов " + dof +
                      " · журнал: " + logPath);
        }

        // ================================================================== журнал в файл

        private void OpenLog()
        {
            try
            {
                string dir = FeatureStorage.LogsDir;
                logPath = Path.Combine(dir, "health_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv");
                var sb = new StringBuilder();
                sb.Append("time_s");
                for (int i = 0; i < dof; i++) sb.Append(",T").Append(i + 1);
                for (int i = 0; i < dof; i++) sb.Append(",I").Append(i + 1);
                for (int i = 0; i < dof; i++) sb.Append(",V").Append(i + 1);
                for (int i = 0; i < dof; i++) sb.Append(",W").Append(i + 1);
                File.WriteAllText(logPath, sb.ToString() + Environment.NewLine);
                logRows = 0;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Health] файл журнала не создан: " + e.Message);
                logPath = "";
            }
        }

        private void WriteRow()
        {
            if (string.IsNullOrEmpty(logPath)) return;
            var sb = new StringBuilder();
            sb.Append(elapsed.ToString("0.00", CultureInfo.InvariantCulture));
            for (int i = 0; i < dof; i++) sb.Append(',').Append(temperature[i].ToString("0.00", CultureInfo.InvariantCulture));
            for (int i = 0; i < dof; i++) sb.Append(',').Append(current[i].ToString("0.000", CultureInfo.InvariantCulture));
            for (int i = 0; i < dof; i++) sb.Append(',').Append(speed[i].ToString("0.000", CultureInfo.InvariantCulture));
            for (int i = 0; i < dof; i++) sb.Append(',').Append(wear[i].ToString("0.000000", CultureInfo.InvariantCulture));
            try
            {
                FeatureStorage.AppendLine(logPath, sb.ToString());
                logRows++;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Health] строка не записана: " + e.Message);
            }
        }

        // ================================================================== симуляция

        public void SetEnabled(bool value)
        {
            Enabled = value;
            Say(KvLoc.T("health.title", "Мониторинг состояния") + ": " +
                (value ? KvLoc.T("common.on", "включено") : KvLoc.T("common.off", "выключено")));
            if (value) timer = 0f;
        }

        public bool Toggle()
        {
            SetEnabled(!Enabled);
            return Enabled;
        }

        /// <summary>Кадровое обслуживание. Считается всегда, когда включено (и во время движения).</summary>
        public void Tick(float dt)
        {
            if (flow == null) return;
            if (flow.Robot != null && flow.Robot != boundRobot) Rebind(flow.Robot);
            if (!bound) return;

            timer -= dt;
            if (timer <= 0f)
            {
                timer = Mathf.Max(0.01f, sampleInterval);
                Sample(Mathf.Max(0.01f, sampleInterval));
            }

            if (!Enabled) return;

            logTimer -= dt;
            if (logTimer <= 0f && logPath.Length > 0)
            {
                logTimer = Mathf.Max(0.1f, logInterval);
                WriteRow();
            }
        }

        /// <summary>Один шаг телеметрии: скорость → ток → температура → износ → история.</summary>
        public void Sample(float dt)
        {
            if (!bound || v == null) return;
            elapsed += dt;
            clock += dt;

            double[] q = new double[dof];
            v.CopyCurrentInto(q);

            float[] ratios = new float[dof];
            for (int i = 0; i < dof; i++)
            {
                double dq = q[i] - prevQ[i];
                if (!v.IsPrismatic(i)) dq = Mathf.Deg2Rad * Mathf.DeltaAngle(0f, (float)(dq));
                float s = (float)(dq / dt);
                speed[i] = s;
                accel[i] = (s - prevSpeed[i]) / dt;
                prevSpeed[i] = s;
                prevQ[i] = q[i];

                // «Нагрузка на плече»: насколько сустав держит руку против гравитации.
                // Для шарнирных осей — по отклонению угла, для призмы — по вылету.
                ratios[i] = Load(i, q);
            }

            for (int i = 0; i < dof; i++)
            {
                float i0 = idleCurrent + loadCurrent * ratios[i];
                current[i] = Mathf.Max(0.05f,
                    i0 + currentPerSpeed * Mathf.Abs(speed[i]) + currentPerAccel * Mathf.Abs(accel[i]));

                // Тепловой баланс: нагрев ∝ I², охлаждение ∝ (T − Tсреды).
                float heat = heatPerCurrent2 * current[i] * current[i];
                float cool = coolingPerDegree * (temperature[i] - ambientTemperature);
                temperature[i] = Mathf.Clamp(temperature[i] + (heat - cool) * dt,
                    ambientTemperature - 5f, maxTemperature + 25f);

                wear[i] = Mathf.Clamp01(wear[i] + wearPerAmpSecond * current[i] * dt);
            }

            PushHistory();
        }

        /// <summary>Оценка статической нагрузки сустава (0…1) — «чем держит, тем тяжелее».</summary>
        private float Load(int index, double[] q)
        {
            if (v == null) return 0f;
            if (v.IsPrismatic(index)) return 0.55f;               // призма всегда держит вес руки
            float a = Mathf.Abs(Mathf.DeltaAngle(0f, (float)q[index])) / 180f;
            // Ближе к «горизонтальному» положению (90°) — момент максимален.
            return Mathf.Clamp01(1f - Mathf.Abs(a * 2f - 1f));
        }

        private void PushHistory()
        {
            for (int i = 0; i < dof && i < historyTemp.Length; i++)
            {
                Push(historyTemp[i], temperature[i]);
                Push(historyAmp[i], current[i]);
                Push(historySpd[i], speed[i]);
                Push(historyWear[i], wear[i]);
            }
            for (int i = dof; i < historyTemp.Length; i++)
            {
                historyTemp[i].Clear(); historyAmp[i].Clear();
                historySpd[i].Clear(); historyWear[i].Clear();
            }
        }

        private void Push(List<KvHealthSample> target, float value)
        {
            var sample = new KvHealthSample();
            sample.t = clock;
            sample.v = value;
            target.Add(sample);
            while (target.Count > Mathf.Max(16, historyLength)) target.RemoveAt(0);
        }

        // ================================================================== сводка

        /// <summary>Сустав с наибольшей температурой и его значение.</summary>
        public int HottestJoint(out float value)
        {
            value = 0f;
            int index = -1;
            if (temperature == null) return -1;
            for (int i = 0; i < temperature.Length; i++)
            {
                if (index < 0 || temperature[i] > value) { value = temperature[i]; index = i; }
            }
            return index;
        }

        /// <summary>Прогноз ресурса: часы до порога обслуживания и до критического износа.</summary>
        public float ForecastHours(bool critical, out int worstJoint)
        {
            worstJoint = -1;
            if (wear == null || elapsed < 1f) return -1f;

            float worst = 0f;
            for (int i = 0; i < wear.Length; i++)
            {
                if (worstJoint < 0 || wear[i] > worst) { worst = wear[i]; worstJoint = i; }
            }
            if (worstJoint < 0) return -1f;

            float limit = critical ? wearCriticalLimit : wearServiceLimit;
            float rate = worst / Mathf.Max(1f, elapsed);              // износ в секунду
            if (rate <= 1e-9f) return -1f;                            // износа нет — ресурс бесконечен
            float remaining = Mathf.Max(0f, limit - worst) / rate;    // секунды
            return remaining / 3600f;
        }

        /// <summary>Строка состояния (статус-бар, панель, отчёт).</summary>
        public string Status
        {
            get
            {
                if (!bound) return KvLoc.T("health.title", "Мониторинг состояния") + ": —";
                float hottest;
                int hot = HottestJoint(out hottest);
                int wj;
                float hours = ForecastHours(false, out wj);
                string forecast = hours < 0f
                    ? KvLoc.T("health.forecast", "Прогноз ресурса") + ": —"
                    : KvLoc.T("health.forecast", "Прогноз ресурса") + ": " + hours.ToString("0.0") + " ч";
                return KvLoc.T("health.title", "Мониторинг состояния") + ": " +
                       (hot >= 0 ? "T" + (hot + 1) + " " + hottest.ToString("0.0") + "°C" : "—") +
                       " · " + forecast + " · строк в файле: " + logRows;
            }
        }

        public void Dispose()
        {
            if (logPath.Length > 0) Say("журнал состояния закрыт: " + logPath + " · строк " + logRows);
            bound = false;
        }

        private void Say(string message)
        {
            if (logEvents) Debug.Log("[Health] " + message);
            if (Message != null) Message(message);
        }
    }

    /// <summary>
    /// Панель мониторинга: четыре графика в реальном времени (температура, ток, скорость,
    /// износ) + строка прогноза ресурса. Собственный Canvas (как у окна функций), скрыт
    /// из иерархии, кликов не перехватывает — обычный интерфейс не меняется.
    /// </summary>
    public class KvHealthPanel : MonoBehaviour
    {
        public int sortingOrder = 44;
        public float panelWidth = 470f;
        public float panelHeight = 420f;
        public int textureWidth = 400;
        public int textureHeight = 74;
        public float refreshInterval = 0.25f;

        private KvHealthMonitor monitor;
        private Canvas canvas;
        private RectTransform panel;
        private Text titleText, forecastText, legendText;
        private readonly RawImage[] plots = new RawImage[4];
        private readonly Texture2D[] textures = new Texture2D[4];
        private readonly Text[] captions = new Text[4];
        private float timer;
        private bool visible;

        public bool Visible { get { return visible; } }

        private static readonly string[] MetricKeys =
        {
            "health.temp", "health.current", "health.speed", "health.wear"
        };
        private static readonly Color[] MetricColors =
        {
            new Color(1f, 0.45f, 0.25f), new Color(1f, 0.85f, 0.25f),
            new Color(0.35f, 0.85f, 1f), new Color(0.85f, 0.45f, 1f)
        };

        public void Build(KvHealthMonitor source)
        {
            monitor = source;
            if (panel != null) return;

            GameObject canvasGo = new GameObject("KvHealthCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            canvasGo.hideFlags = HideFlags.HideInHierarchy;
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform canvasRect = (RectTransform)canvasGo.transform;

            GameObject panelGo = new GameObject("KvHealthPanel", typeof(Image));
            panelGo.transform.SetParent(canvasRect, false);
            panel = (RectTransform)panelGo.transform;
            panel.anchorMin = new Vector2(1f, 0f);
            panel.anchorMax = new Vector2(1f, 0f);
            panel.pivot = new Vector2(1f, 0f);
            panel.sizeDelta = new Vector2(panelWidth, panelHeight);
            panel.anchoredPosition = new Vector2(-12f, 34f);
            Image bg = panelGo.GetComponent<Image>();
            bg.sprite = KvTheme.WhiteSprite;
            bg.color = KvTheme.PanelBg;
            bg.raycastTarget = false;

            titleText = KvTheme.CreateText(panel, "Title",
                KvLoc.T("health.title", "Мониторинг состояния").ToUpperInvariant(),
                KvTheme.FontSizeSmall + 1, TextAnchor.UpperLeft, KvTheme.TextMain);
            titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            titleText.rectTransform.pivot = new Vector2(0.5f, 1f);
            titleText.rectTransform.sizeDelta = new Vector2(-16f, 18f);
            titleText.rectTransform.anchoredPosition = new Vector2(0f, -4f);

            float y = 26f;
            for (int m = 0; m < 4; m++)
            {
                Text cap = KvTheme.CreateText(panel, "Cap" + m,
                    KvLoc.T(MetricKeys[m], MetricKeys[m]).ToUpperInvariant(),
                    KvTheme.FontSizeSmall - 1, TextAnchor.UpperLeft, MetricColors[m]);
                cap.rectTransform.anchorMin = new Vector2(0f, 1f);
                cap.rectTransform.anchorMax = new Vector2(1f, 1f);
                cap.rectTransform.pivot = new Vector2(0.5f, 1f);
                cap.rectTransform.sizeDelta = new Vector2(-16f, 14f);
                cap.rectTransform.anchoredPosition = new Vector2(0f, -y);
                captions[m] = cap;
                y += 15f;

                GameObject plotGo = new GameObject("Plot" + m, typeof(RawImage));
                plotGo.transform.SetParent(panel, false);
                RawImage img = plotGo.GetComponent<RawImage>();
                img.raycastTarget = false;
                RectTransform pr = img.rectTransform;
                pr.anchorMin = new Vector2(0f, 1f);
                pr.anchorMax = new Vector2(1f, 1f);
                pr.pivot = new Vector2(0.5f, 1f);
                pr.sizeDelta = new Vector2(-16f, textureHeight);
                pr.anchoredPosition = new Vector2(0f, -y);
                plots[m] = img;

                textures[m] = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
                textures[m].wrapMode = TextureWrapMode.Clamp;
                textures[m].filterMode = FilterMode.Point;
                textures[m].hideFlags = HideFlags.HideAndDontSave;
                img.texture = textures[m];
                Fill(textures[m], new Color(0.08f, 0.09f, 0.11f, 1f));
                textures[m].Apply(false);
                y += textureHeight + 6f;
            }

            legendText = KvTheme.CreateText(panel, "Legend", "", KvTheme.FontSizeSmall - 1,
                TextAnchor.UpperLeft, KvTheme.TextDim);
            legendText.horizontalOverflow = HorizontalWrapMode.Wrap;
            legendText.rectTransform.anchorMin = new Vector2(0f, 1f);
            legendText.rectTransform.anchorMax = new Vector2(1f, 1f);
            legendText.rectTransform.pivot = new Vector2(0.5f, 1f);
            legendText.rectTransform.sizeDelta = new Vector2(-16f, 30f);
            legendText.rectTransform.anchoredPosition = new Vector2(0f, -y);

            forecastText = KvTheme.CreateText(panel, "Forecast", "",
                KvTheme.FontSizeSmall, TextAnchor.UpperLeft, KvTheme.TextMain);
            forecastText.rectTransform.anchorMin = new Vector2(0f, 0f);
            forecastText.rectTransform.anchorMax = new Vector2(1f, 0f);
            forecastText.rectTransform.pivot = new Vector2(0.5f, 0f);
            forecastText.rectTransform.sizeDelta = new Vector2(-16f, 34f);
            forecastText.rectTransform.anchoredPosition = new Vector2(0f, 6f);

            panelGo.SetActive(false);
        }

        public void SetVisible(bool value)
        {
            visible = value;
            if (panel != null) panel.gameObject.SetActive(value);
            if (value) { timer = 0f; Redraw(); }
            Debug.Log("[Health] панель мониторинга: " + (value ? "показана" : "скрыта"));
        }

        public bool Toggle()
        {
            SetVisible(!visible);
            return visible;
        }

        /// <summary>Обновить графики (не чаще refreshInterval).</summary>
        public void Refresh()
        {
            if (!visible || monitor == null) return;
            timer -= Time.unscaledDeltaTime;
            if (timer > 0f) return;
            timer = Mathf.Max(0.05f, refreshInterval);
            Redraw();
        }

        public void Redraw()
        {
            if (!visible || monitor == null || plots[0] == null) return;

            if (titleText != null)
                titleText.text = KvLoc.T("health.title", "Мониторинг состояния").ToUpperInvariant() +
                                 " · " + monitor.RobotName + " · " + monitor.Dof + " × " +
                                 KvLoc.T("prop.section.joints", "Суставы").ToLowerInvariant() +
                                 " · " + monitor.Elapsed.ToString("0") + " с";

            for (int m = 0; m < 4; m++) DrawMetric(m);

            if (legendText != null)
            {
                var sb = new StringBuilder();
                for (int i = 0; i < monitor.Dof; i++)
                {
                    if (i > 0) sb.Append("   ");
                    sb.Append('J').Append(i + 1);
                }
                sb.Append("   ·   ").Append(KvLoc.T("health.temp", "Температура")).Append(": ");
                for (int i = 0; i < monitor.Dof; i++)
                    sb.Append(i > 0 ? " / " : "").Append(monitor.Temperature[i].ToString("0.0"));
                sb.Append(" °C");
                legendText.text = sb.ToString();
            }

            if (forecastText != null)
            {
                int hot; float hottest = 0f;
                hot = monitor.HottestJoint(out hottest);
                int wj; float service = monitor.ForecastHours(false, out wj);
                int wc; float critical = monitor.ForecastHours(true, out wc);

                string text = "";
                if (hot >= 0)
                    text += "T" + (hot + 1) + ": " + hottest.ToString("0.0") + " °C\n";
                text += KvLoc.T("health.forecast", "Прогноз ресурса") + ": ";
                text += service < 0f ? "—" : service.ToString("0.0") + " ч";
                text += critical < 0f ? "" : "   ·   " + critical.ToString("0.0") + " ч";
                if (wj >= 0) text += "   (J" + (wj + 1) + " · износ " +
                                     (monitor.Wear[wj] * 100f).ToString("0.0") + " %)";
                forecastText.text = text;
            }
        }

        private void DrawMetric(int metric)
        {
            Texture2D tex = textures[metric];
            if (tex == null) return;

            Fill(tex, new Color(0.08f, 0.09f, 0.11f, 1f));
            Grid(tex);

            List<KvHealthSample>[] history = monitor.Histories(metric);
            float window = Mathf.Max(1f, monitor.windowSeconds);

            // Общий масштаб по всем суставам — линии сравнимы между собой.
            float min = float.MaxValue, max = float.MinValue;
            for (int j = 0; j < monitor.Dof && j < history.Length; j++)
            {
                List<KvHealthSample> list = j < history.Length ? history[j] : null;
                if (list == null) continue;
                for (int i = 0; i < list.Count; i++)
                {
                    float val = list[i].v;
                    if (val < min) min = val;
                    if (val > max) max = val;
                }
            }
            if (min > max) { min = 0f; max = 1f; }
            if (max - min < 1e-4f) { max = min + 1f; }
            else max += (max - min) * 0.08f;

            for (int j = 0; j < monitor.Dof && j < history.Length; j++)
            {
                List<KvHealthSample> list = j < history.Length ? history[j] : null;
                if (list == null || list.Count < 2) continue;

                float tEnd = list[list.Count - 1].t;
                float tStart = tEnd - window;
                Color color = KvJointGraph.JointColors[j % KvJointGraph.JointColors.Length];

                float prevX = 0f, prevY = 0f;
                bool hasPrev = false;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].t < tStart) continue;
                    float x = Normalize(list[i].t, tStart, tEnd) * (textureWidth - 1);
                    float y = Normalize(list[i].v, min, max) * (textureHeight - 1);
                    if (hasPrev) Line(tex, (int)prevX, (int)prevY, (int)x, (int)y, color);
                    prevX = x; prevY = y; hasPrev = true;
                }
            }
            tex.Apply(false);
        }

        private static float Normalize(float value, float from, float to)
        {
            if (to - from < 1e-5f) return 0f;
            return Mathf.Clamp01((value - from) / (to - from));
        }

        /// <summary>Буфер пикселей переиспользуется: в кадре нет аллокаций под графики.</summary>
        private Color32[] pixelBuffer;

        private void Fill(Texture2D tex, Color color)
        {
            int need = tex.width * tex.height;
            if (pixelBuffer == null || pixelBuffer.Length < need) pixelBuffer = new Color32[need];
            Color32 c = color;
            for (int i = 0; i < need; i++) pixelBuffer[i] = c;
            tex.SetPixels32(pixelBuffer);
        }

        private static void Grid(Texture2D tex)
        {
            Color32 line = new Color(1f, 1f, 1f, 0.10f);
            for (int i = 1; i < 4; i++)
            {
                int x = tex.width * i / 4;
                for (int y = 0; y < tex.height; y++) tex.SetPixel(x, y, line);
            }
            for (int i = 1; i < 3; i++)
            {
                int y = tex.height * i / 3;
                for (int x = 0; x < tex.width; x++) tex.SetPixel(x, y, line);
            }
        }

        private static void Line(Texture2D tex, int x0, int y0, int x1, int y1, Color color)
        {
            int dx = Mathf.Abs(x1 - x0), dy = Mathf.Abs(y1 - y0);
            int steps = Mathf.Max(dx, dy);
            if (steps <= 0)
            {
                if (x0 >= 0 && x0 < tex.width && y0 >= 0 && y0 < tex.height) tex.SetPixel(x0, y0, color);
                return;
            }
            for (int i = 0; i <= steps; i++)
            {
                int x = x0 + (x1 - x0) * i / steps;
                int y = y0 + (y1 - y0) * i / steps;
                if (x < 0 || x >= tex.width || y < 0 || y >= tex.height) continue;
                tex.SetPixel(x, y, color);
                if (x + 1 < tex.width) tex.SetPixel(x + 1, y, color);   // линия толщиной 2 px
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < textures.Length; i++)
                if (textures[i] != null) Destroy(textures[i]);
            if (canvas != null) Destroy(canvas.gameObject);
        }
    }
}
