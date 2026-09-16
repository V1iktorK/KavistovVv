#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Автоматическое обновление окна Hierarchy (только редактор).
///
/// Штатного «таймера Hierarchy» в Unity нет, поэтому используется
/// EditorApplication.update + EditorApplication.RepaintHierarchyWindow().
/// Вызов строго лёгкий: один repaint раз в 2 секунды, без обхода сцены,
/// без FindObjects, без изменения выделения — редактор не тормозит.
///
/// Зачем: записи, которые уже уничтожены (остановка PlayMode, удаление объектов
/// из кода), иногда остаются нарисованными в окне Hierarchy до следующего
/// ручного клика. Периодический repaint убирает такие «фантомные» строки.
///
/// Остановка/запуск — пункт меню Tools/KazistovVv/Hierarchy (галочка сохраняется
/// в EditorPrefs «KazistovVv.HierarchyAutoRefresh»).
/// </summary>
[InitializeOnLoad]
public static class HierarchyAutoRefresh
{
    /// <summary>Интервал обновления, секунды (по ТЗ — 2 с).</summary>
    public const double IntervalSeconds = 2.0;

    private const string PrefKey = "KazistovVv.HierarchyAutoRefresh";
    private const string MenuPath = "Tools/KazistovVv/Hierarchy/Автообновление иерархии (2 с)";

    private static double nextRepaint;

    /// <summary>Включено ли автообновление (по умолчанию — да).</summary>
    public static bool Enabled
    {
        get { return EditorPrefs.GetBool(PrefKey, true); }
        set { EditorPrefs.SetBool(PrefKey, value); }
    }

    static HierarchyAutoRefresh()
    {
        // Подписка обязана быть в статическом конструкторе [InitializeOnLoad]:
        // при входе в PlayMode домен перезагружается и подписки теряются.
        EditorApplication.update += Tick;
        nextRepaint = EditorApplication.timeSinceStartup + IntervalSeconds;
    }

    private static void Tick()
    {
        if (!Enabled) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

        double now = EditorApplication.timeSinceStartup;
        if (now < nextRepaint) return;
        nextRepaint = now + IntervalSeconds;

        EditorApplication.RepaintHierarchyWindow();   // только перерисовка, ничего больше
    }

    [MenuItem(MenuPath, false, 40)]
    private static void Toggle()
    {
        Enabled = !Enabled;
        if (Enabled)
        {
            nextRepaint = EditorApplication.timeSinceStartup + IntervalSeconds;
            EditorApplication.RepaintHierarchyWindow();
        }
        Debug.Log("[HierarchyAutoRefresh] автообновление иерархии: " +
                  (Enabled ? "включено (каждые 2 с)" : "выключено"));
    }

    [MenuItem(MenuPath, true)]
    private static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, Enabled);
        return true;
    }
}
#endif
