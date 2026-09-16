using System.Collections.Generic;
using TrajectoryCore;
using UnityEngine;
using UnityEngine.UI;
using KazistovVvUI;

/// <summary>
/// ПАНЕЛЬ МЕТРИК ТРАЕКТОРИЙ (ТЗ сессии 14.09.2026, ЗАДАЧА 5) — временное решение до
/// полноценного UI: простая, но читаемая панель, по которой оператор ОСОЗНАННО выбирает
/// из 8 вариантов.
///
/// Для каждой сгенерированной траектории показывается строка ЕЁ цветом (тем же, что и
/// «колбаска» в сцене) и метрики:
///   * ДЛИНА пути TCP (юниты),
///   * ВРЕМЯ выполнения при ТЕКУЩЕЙ скорости робота (обновляется на лету),
///   * КРИВИЗНА: суммарная (сколько всего путь петляет) и максимальная (самый резкий перелом),
///   * минимальный ЗАЗОР до препятствий (мм) — из проверок коллизий планировщика,
///   * минимальный ЗАПАС до лимитов суставов по всему пути (град).
///
/// Дополнительно:
///   * сортировка строк по выбранному критерию (публичное поле `sortBy` / методы
///     `SetSort`/`CycleSort` — БИНДЫ НЕ ИСПОЛЬЗУЮТСЯ);
///   * ЛУЧШЕЕ значение по каждому критерию подсвечивается прямо в строке;
///   * строка ВЫБРАННОЙ траектории (зелёный лазер + ЛКМ) подсвечивается фоном;
///   * панель обновляется при генерации новых траекторий и при смене выбора;
///   * включается/выключается публичным флагом `showPanel` (или SetVisible(bool)).
///
/// Панель стоит СПРАВА (ниже верхней панели KazistovVv) и не перекрывает рабочую зону робота.
/// Все объекты — служебные: HideInHierarchy, raycastTarget = false (клики и колесо мыши
/// панель не перехватывает), коллайдеров нет, в CollisionWorld не попадают.
///
/// Компонент создаётся потоком этапов (`TrajectoryFlowController.Awake`) — сцена не меняется.
/// </summary>
public class TrajectoryMetricsPanel : MonoBehaviour
{
    /// <summary>По какому критерию сортируются строки панели.</summary>
    public enum MetricSort
    {
        Score,        // оценка планировщика (время+длина+кривизна+зазор+запас+поза)
        Length,       // длина пути TCP
        Time,         // время выполнения
        Curvature,    // суммарная кривизна
        Clearance,    // минимальный зазор до препятствий
        LimitMargin   // минимальный запас до лимитов суставов
    }

    [Header("Панель")]
    [Tooltip("Показывать панель метрик. Публичный флаг: правится в инспекторе в PlayMode " +
             "или методом SetVisible(bool). Биндов нет.")]
    public bool showPanel = true;
    [Tooltip("Критерий сортировки строк (публичное поле — можно менять в инспекторе в PlayMode).")]
    public MetricSort sortBy = MetricSort.Score;
    [Tooltip("Показывать только первые N строк (0 — все). 8 траекторий читаются целиком.")]
    public int maxRows = 0;

    [Header("Геометрия (screen space)")]
    public float panelWidth = 700f;
    public float panelRightOffset = 20f;
    public float panelTopOffset = 178f;
    public int sortingOrder = 50;           // ниже панели лимитов (55) и HUD перемещения точки (60)
    public float rowHeight = 19f;
    public int fontSize = 12;

    [Header("Живые значения")]
    [Tooltip("Скорость РОБОТА, юнитов/с — по ней показывается время выполнения (синхронизирует поток).")]
    public float robotSpeed = 1f / 15f;
    [Tooltip("Скорость ФАНТОМОВ, юнитов/с (= robotSpeed × множитель) — справочно в заголовке.")]
    public float phantomSpeed = 0.2f;
    [Tooltip("Как часто (с) пересчитывать тексты (время зависит от текущей скорости робота).")]
    public float refreshInterval = 0.5f;

    private static readonly Color ColorBest = new Color(1f, 0.95f, 0.45f);      // лучшее значение
    private static readonly Color ColorDim = new Color(0.68f, 0.70f, 0.74f);    // обычное значение
    private static readonly Color ColorBad = new Color(1f, 0.35f, 0.35f);       // ниже порога
    private static readonly Color RowSelected = new Color(0.16f, 0.40f, 0.24f, 0.85f);

    private class Row
    {
        public GameObject root;
        public Image background;
        public Image swatch;
        public Text text;
    }

    private readonly List<Row> rows = new List<Row>();
    private readonly List<TrajectoryCandidate> items = new List<TrajectoryCandidate>();

    private GameObject canvasRoot;
    private RectTransform body;
    private Text title;
    private Text sortLine;
    private bool built;
    private int selected = -1;
    private float timer;

    /// <summary>Панель показывается (публичный флаг).</summary>
    public bool Visible => showPanel;
    /// <summary>Сколько строк сейчас в панели (диагностика/проверки).</summary>
    public int RowCount => items.Count;
    /// <summary>Текст строки i (диагностика/проверки).</summary>
    public string RowText(int i) => i >= 0 && i < rows.Count && rows[i].text != null ? rows[i].text.text : "";
    /// <summary>Цвет строки i — тот же, что у «колбаски» траектории (диагностика/проверки).</summary>
    public Color RowColor(int i)
        => i >= 0 && i < rows.Count && rows[i].swatch != null ? rows[i].swatch.color : Color.white;
    /// <summary>Индекс выбранной траектории (строка подсвечена) — для проверок.</summary>
    public int SelectedDisplayIndex { get; private set; } = -1;
    /// <summary>HEX цвета «лучшее значение» (подсветка в строке) — для диагностики/проверок.</summary>
    public static string BestValueColorHex => ColorUtility.ToHtmlStringRGB(ColorBest);
    /// <summary>HEX цвета обычного значения — для диагностики/проверок.</summary>
    public static string PlainValueColorHex => ColorUtility.ToHtmlStringRGB(ColorDim);
    /// <summary>Фон строки i (подсвечен у выбранной траектории) — для проверок.</summary>
    public Color RowBackground(int i)
        => i >= 0 && i < rows.Count && rows[i].background != null ? rows[i].background.color : Color.clear;
    /// <summary>Текущий критерий сортировки (для проверок).</summary>
    public MetricSort SortCriterion => sortBy;

    // ------------------------------------------------------------------ публичное управление

    /// <summary>Включить/выключить панель (публичный метод — бинды не используются).</summary>
    public void SetVisible(bool on)
    {
        showPanel = on;
        ApplyVisibility();
    }

    /// <summary>Переключить панель.</summary>
    public void ToggleVisible() { SetVisible(!showPanel); }

    /// <summary>Задать критерий сортировки строк.</summary>
    public void SetSort(MetricSort sort)
    {
        sortBy = sort;
        RebuildRows();
    }

    /// <summary>Следующий критерий сортировки (для UI-кнопки или внешнего вызова).</summary>
    public void CycleSort()
    {
        int n = System.Enum.GetValues(typeof(MetricSort)).Length;
        sortBy = (MetricSort)(((int)sortBy + 1) % n);
        RebuildRows();
    }

    /// <summary>
    /// Показать метрики набора траекторий (вызывается при генерации вариантов) и отметить
    /// выбранную (индекс в списке `SelectionState.candidates`; -1 — не выбрана).
    /// </summary>
    public void SetTrajectories(List<TrajectoryCandidate> candidates, int selectedIndex)
    {
        items.Clear();
        if (candidates != null)
            foreach (TrajectoryCandidate c in candidates)
                if (c != null) items.Add(c);

        selected = selectedIndex;
        RebuildRows();
    }

    /// <summary>Отметить выбранную траекторию (шаг 3/4 ТЗ) — подсветка строки.</summary>
    public void SetSelected(int selectedIndex)
    {
        selected = selectedIndex;
        RebuildRows();
    }

    /// <summary>Убрать все строки (сброс, Esc, движение робота).</summary>
    public void Clear()
    {
        items.Clear();
        selected = -1;
        RebuildRows();
    }

    // ------------------------------------------------------------------ жизненный цикл

    private void Awake()
    {
        Build();
        RebuildRows();
    }

    private void OnDestroy()
    {
        if (canvasRoot != null) Destroy(canvasRoot);
    }

    /// <summary>Живое обновление: время выполнения зависит от текущей скорости робота.</summary>
    public void Tick()
    {
        if (!showPanel || items.Count == 0) return;
        timer -= Time.unscaledDeltaTime;
        if (timer > 0f) return;
        timer = Mathf.Max(0.1f, refreshInterval);
        RefreshTexts();
    }

    // ------------------------------------------------------------------ сборка

    private void Build()
    {
        if (built) return;
        built = true;

        canvasRoot = new GameObject("TrajectoryMetricsPanel", typeof(Canvas), typeof(CanvasScaler));
        canvasRoot.transform.SetParent(transform, false);
        canvasRoot.hideFlags = HideFlags.HideInHierarchy;
        Canvas canvas = canvasRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        CanvasScaler scaler = canvasRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        Image panel = KvTheme.CreatePanel((RectTransform)canvasRoot.transform, "Panel",
            new Color(0.10f, 0.11f, 0.13f, 0.84f));
        panel.raycastTarget = false;             // панель — индикация, клики/колесо не перехватывает
        body = panel.rectTransform;
        body.anchorMin = new Vector2(1f, 1f);
        body.anchorMax = new Vector2(1f, 1f);
        body.pivot = new Vector2(1f, 1f);
        body.anchoredPosition = new Vector2(-panelRightOffset, -panelTopOffset);
        body.sizeDelta = new Vector2(panelWidth, 64f);

        title = KvTheme.CreateText(body, "Title", "МЕТРИКИ ТРАЕКТОРИЙ", 14,
            TextAnchor.UpperLeft, KvTheme.TextMain);
        RectTransform trt = title.rectTransform;
        trt.anchorMin = new Vector2(0f, 1f);
        trt.anchorMax = new Vector2(1f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.sizeDelta = new Vector2(-24f, 20f);
        trt.anchoredPosition = new Vector2(-4f, -6f);

        sortLine = KvTheme.CreateText(body, "Sort", "", 11, TextAnchor.UpperLeft, KvTheme.TextDim);
        RectTransform srt = sortLine.rectTransform;
        srt.anchorMin = new Vector2(0f, 1f);
        srt.anchorMax = new Vector2(1f, 1f);
        srt.pivot = new Vector2(0.5f, 1f);
        srt.sizeDelta = new Vector2(-24f, 16f);
        srt.anchoredPosition = new Vector2(-4f, -26f);

        canvasRoot.SetActive(showPanel);
    }

    /// <summary>Строки панели: по одной на траекторию (цвет = цвет «колбаски» в сцене).</summary>
    private void EnsureRows(int count)
    {
        if (body == null) return;
        while (rows.Count < count)
        {
            int i = rows.Count;
            var row = new Row();

            var go = new GameObject("Row" + i, typeof(Image));
            go.transform.SetParent(body, false);
            go.hideFlags = HideFlags.HideInHierarchy;
            row.root = go;
            row.background = go.GetComponent<Image>();
            row.background.raycastTarget = false;
            row.background.color = new Color(0f, 0f, 0f, 0f);
            RectTransform rrt = row.background.rectTransform;
            rrt.anchorMin = new Vector2(0f, 1f);
            rrt.anchorMax = new Vector2(1f, 1f);
            rrt.pivot = new Vector2(0.5f, 1f);
            rrt.sizeDelta = new Vector2(-12f, rowHeight);
            rrt.anchoredPosition = new Vector2(-4f, -46f - i * rowHeight);

            row.swatch = KvTheme.CreatePanel(rrt, "Swatch", Color.white);
            row.swatch.raycastTarget = false;
            RectTransform srt = row.swatch.rectTransform;
            srt.anchorMin = new Vector2(0f, 0.5f);
            srt.anchorMax = new Vector2(0f, 0.5f);
            srt.pivot = new Vector2(0f, 0.5f);
            srt.sizeDelta = new Vector2(9f, 13f);
            srt.anchoredPosition = new Vector2(2f, 0f);

            row.text = KvTheme.CreateText(rrt, "Text", "", fontSize, TextAnchor.MiddleLeft, ColorDim);
            row.text.supportRichText = true;
            RectTransform trt = row.text.rectTransform;
            trt.anchorMin = new Vector2(0f, 0f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.offsetMin = new Vector2(16f, 0f);
            trt.offsetMax = new Vector2(-2f, 0f);

            rows.Add(row);
        }

        // Лишние строки (траекторий стало меньше) — выключаем, а не удаляем: пул.
        for (int i = 0; i < rows.Count; i++)
            if (rows[i].root != null) rows[i].root.SetActive(i < count);

        body.sizeDelta = new Vector2(panelWidth, 50f + count * rowHeight);
    }

    // ------------------------------------------------------------------ содержимое

    /// <summary>Порядок строк: по выбранному критерию (лучшее — сверху).</summary>
    private List<TrajectoryCandidate> Sorted()
    {
        var list = new List<TrajectoryCandidate>(items);
        if (list.Count < 2) return list;

        float TimeOf(TrajectoryCandidate c) => SpeedOf() > 0.0001f ? c.lengthM / SpeedOf() : c.timeS;
        switch (sortBy)
        {
            case MetricSort.Length:
                list.Sort((a, b) => MetricLength(a).CompareTo(MetricLength(b)));
                break;
            case MetricSort.Time:
                list.Sort((a, b) => TimeOf(a).CompareTo(TimeOf(b)));
                break;
            case MetricSort.Curvature:
                list.Sort((a, b) => CurvatureTotalOf(a).CompareTo(CurvatureTotalOf(b)));
                break;
            case MetricSort.Clearance:
                list.Sort((a, b) => MetricClearance(b).CompareTo(MetricClearance(a)));   // больше — лучше
                break;
            case MetricSort.LimitMargin:
                list.Sort((a, b) => MetricMargin(b).CompareTo(MetricMargin(a)));         // больше — лучше
                break;
            default:
                list.Sort((a, b) => a.score.CompareTo(b.score));
                break;
        }
        return list;
    }

    private float SpeedOf() => Mathf.Max(0.0001f, robotSpeed);
    private static float MetricLength(TrajectoryCandidate c) => c.lengthM;
    private static float MetricClearance(TrajectoryCandidate c) => c.minClearance;
    private static float MetricMargin(TrajectoryCandidate c) => c.limitMarginDeg;
    private static double CurvatureTotalOf(TrajectoryCandidate c)
        => c.plan != null ? c.plan.CurvatureTotal : 0.0;
    private static double CurvatureMaxOf(TrajectoryCandidate c)
        => c.plan != null ? c.plan.CurvatureMax : 0.0;
    private static double CurvatureAvgOf(TrajectoryCandidate c)
        => c.plan != null ? c.plan.Curvature : 0.0;

    private void RebuildRows()
    {
        Build();
        List<TrajectoryCandidate> list = Sorted();
        int count = maxRows > 0 ? Mathf.Min(maxRows, list.Count) : list.Count;
        EnsureRows(count);

        SelectedDisplayIndex = -1;
        for (int i = 0; i < count; i++)
        {
            TrajectoryCandidate c = list[i];
            if (rows[i].swatch != null) rows[i].swatch.color = c.color;
            bool isSelected = items.IndexOf(c) == selected;
            if (isSelected) SelectedDisplayIndex = i;
            if (rows[i].background != null)
                rows[i].background.color = isSelected ? RowSelected : new Color(0f, 0f, 0f, 0f);
        }

        RefreshTexts();
        if (sortLine != null)
        {
            string[] names = { "оценка планировщика", "длина пути", "время выполнения",
                               "суммарная кривизна", "минимальный зазор", "запас до лимитов" };
            sortLine.text = "сортировка: " + names[(int)sortBy] +
                            " · лучшее значение в столбце подсвечено · " +
                            "скорость робота " + SpeedOf().ToString("0.0000") + " ю/с, фантомы " +
                            phantomSpeed.ToString("0.000") + " ю/с";
        }
        if (title != null)
            title.text = "МЕТРИКИ ТРАЕКТОРИЙ · " + count + " из " + MaxTrajectories;
        ApplyVisibility();
    }

    private const int MaxTrajectories = 8;

    /// <summary>Тексты строк: значения + подсветка лучшего по каждому критерию.</summary>
    private void RefreshTexts()
    {
        if (items.Count == 0) return;
        List<TrajectoryCandidate> list = Sorted();
        int count = maxRows > 0 ? Mathf.Min(maxRows, list.Count) : Mathf.Min(list.Count, rows.Count);

        // Лучшие значения по каждому критерию — по ВСЕМУ набору (не только по видимым строкам).
        float bestLen = float.MaxValue, bestTime = float.MaxValue, bestClear = float.MinValue, bestMargin = float.MinValue;
        double bestCurv = double.MaxValue;
        foreach (TrajectoryCandidate c in list)
        {
            bestLen = Mathf.Min(bestLen, c.lengthM);
            bestTime = Mathf.Min(bestTime, c.lengthM / SpeedOf());
            bestCurv = System.Math.Min(bestCurv, CurvatureTotalOf(c));
            bestClear = Mathf.Max(bestClear, c.minClearance);
            bestMargin = Mathf.Max(bestMargin, c.limitMarginDeg);
        }

        for (int i = 0; i < count && i < rows.Count; i++)
        {
            TrajectoryCandidate c = list[i];
            float len = c.lengthM;
            float time = SpeedOf() > 0.0001f ? len / SpeedOf() : c.timeS;
            double curvTotal = CurvatureTotalOf(c);
            double curvMax = CurvatureMaxOf(c);
            double curvAvg = CurvatureAvgOf(c);
            float clearMm = c.minClearance * 1000f;
            float margin = c.limitMarginDeg;

            var sb = new System.Text.StringBuilder(160);
            sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(c.color)).Append(">")
              .Append(items.IndexOf(c) + 1 < 10 ? "0" : "").Append(items.IndexOf(c) + 1).Append("</color>  ");
            sb.Append("длина ").Append(Val(len, "F2", Mathf.Abs(len - bestLen) < 1e-4f)).Append(" ю · ");
            sb.Append("время ").Append(Val(time, "F1", Mathf.Abs(time - bestTime) < 1e-3f)).Append(" с · ");
            sb.Append("кривизна ").Append(Val((float)curvTotal, "F0", curvTotal <= bestCurv + 1e-6)).Append("° (сред ")
              .Append(curvAvg.ToString("F1")).Append("°, макс ").Append(curvMax.ToString("F0")).Append("°) · ");
            sb.Append("зазор ").Append(Val(clearMm, "F0", clearMm >= bestClear * 1000f - 1e-3f)).Append(" мм · ");
            sb.Append("запас лимитов ").Append(Val(margin, "F1", margin >= bestMargin - 1e-3f)).Append("°");

            if (rows[i].text != null) rows[i].text.text = sb.ToString();
        }
    }

    /// <summary>Значение: лучшее — ярким цветом, остальные — приглушённым.</summary>
    private static string Val(float value, string format, bool best)
        => "<color=#" + ColorUtility.ToHtmlStringRGB(best ? ColorBest : ColorDim) + ">" +
           value.ToString(format) + "</color>";

    private void ApplyVisibility()
    {
        if (canvasRoot != null) canvasRoot.SetActive(showPanel);
    }
}
