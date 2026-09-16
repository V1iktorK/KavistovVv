#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Чистильщик «фантомных» записей в окне Hierarchy (только редактор, в рантайм не попадает).
///
/// Что такое «фантомная» запись:
///   * объект, которого нет ни в одной загруженной сцене (scene.IsValid() == false),
///     но который остался жив в редакторе — типичный след HideFlags.DontSave
///     (такой объект виден в Hierarchy, но не находится ни поиском по сцене,
///     ни Find/FindObjectsOfType — «висит» в дереве и не выбирается);
///   * служебный контейнер фантомов «Phantoms» / копии «Phantom_*», пережившие
///     остановку PlayMode (старый код ставил HideAndDontSave);
///   * копии роботов, созданные в PlayMode кнопкой «Добавить робота»
///     (ObjectSpawner.SpawnRobot → имя «&lt;robotName&gt;_HHmmss»).
///
/// Когда работает:
///   * при загрузке/перезагрузке домена (после компиляции скриптов) — один раз через delayCall;
///   * перед входом в PlayMode (ExitingEditMode) и сразу после остановки (EnteredEditMode) —
///     гарантия «в дереве только два робота»;
///   * вручную из меню Tools/KazistovVv/Hierarchy.
///
/// Объекты сцены (в том числе оба робота) не трогаются: удаляются только объекты
/// вне сцен, служебные контейнеры фантомов и — по отдельной команде с подтверждением —
/// копии роботов, созданные через UI.
/// </summary>
[InitializeOnLoad]
public static class HierarchyPhantomCleaner
{
    private const string LogTag = "[HierarchyGuard]";
    private const string MenuRoot = "Tools/KazistovVv/Hierarchy/";

    /// <summary>Имена служебных объектов фантомов (см. PhantomManager).</summary>
    private static readonly string[] PhantomNames = { "Phantoms", "Phantom_Robot", "Preview_Robot" };
    private static readonly string[] PhantomPrefixes = { "Phantom_", "GhostTrajectory_", "Траектория", "AimIndicator" };

    static HierarchyPhantomCleaner()
    {
        // Автоматическая уборка — только в режиме редактирования: в PlayMode живут
        // рабочие фантомы потока, их трогать нельзя (для ручной уборки есть меню).
        EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.isPlaying) Purge(verbose: false);
        };
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        // Перед стартом Play: убрать остатки прошлых сессий — в PlayMode должны войти
        // ровно два робота сцены.
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            Purge(verbose: false);
        }
        // После остановки Play: play-объекты уничтожены, но «протёкшие» служебные
        // объекты (DontSave) остаются — убираем и сразу отчитываемся об инвентаре.
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            Purge(verbose: true);
            ReportRobotInventory();
        }
    }

    // ------------------------------------------------------------------ очистка

    /// <summary>Удалить «протёкшие» и служебные объекты фантомов. Возвращает число удалённых.</summary>
    public static int Purge(bool verbose)
    {
        var doomed = new List<GameObject>();
        var objects = Resources.FindObjectsOfTypeAll<GameObject>();

        foreach (GameObject go in objects)
        {
            if (go == null) continue;
            if (EditorUtility.IsPersistent(go)) continue;      // ассет (префаб/FBX) — не трогаем
            if (go.transform.parent != null) continue;         // рассматриваем только корни

            // 1. Сирота вне сцен с флагом DontSave — «фантом», который не находится поиском.
            bool orphan = !go.scene.IsValid();
            bool dontSave = (go.hideFlags & (HideFlags.DontSave | HideFlags.DontSaveInEditor |
                                             HideFlags.DontSaveInBuild)) != 0;
            if (orphan && dontSave && LooksLikePhantom(go))
            {
                doomed.Add(go);
                continue;
            }

            // 2. Служебный контейнер/копия, оставшиеся в сцене (HideInHierarchy + имя наше).
            bool hidden = (go.hideFlags & HideFlags.HideInHierarchy) != 0;
            if (hidden && IsPhantomName(go.name))
                doomed.Add(go);
        }

        int removed = 0;
        foreach (GameObject go in doomed)
        {
            if (go == null) continue;
            string name = go.name;
            if (verbose) Debug.Log(LogTag + " удалён «фантомный» объект вне сцены: " + name, go);
            Kill(go);
            removed++;
        }

        if (removed > 0)
        {
            EditorApplication.RepaintHierarchyWindow();
            Debug.Log(LogTag + " очищено «фантомных» объектов: " + removed);
        }
        else if (verbose)
        {
            Debug.Log(LogTag + " «фантомных» объектов не найдено — дерево чистое.");
        }
        return removed;
    }

    private static bool IsPhantomName(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        foreach (string n in PhantomNames)
            if (name == n) return true;
        foreach (string p in PhantomPrefixes)
            if (name.StartsWith(p)) return true;
        return false;
    }

    /// <summary>Похоже на наш служебный объект (по имени или по копии робота внутри).</summary>
    private static bool LooksLikePhantom(GameObject go)
    {
        if (IsPhantomName(go.name)) return true;
        // Копия модели робота (фантом = Instantiate робота): внутри есть RobotController.
        return go.GetComponentInChildren<RobotController>(true) != null;
    }

    /// <summary>Удаление: DestroyImmediate в редакторе, Destroy в PlayMode.</summary>
    private static void Kill(GameObject go)
    {
        if (go == null) return;
        if (Application.isPlaying) Object.Destroy(go);
        else Object.DestroyImmediate(go);
    }

    // ------------------------------------------------------------------ инвентарь роботов

    /// <summary>Отчёт: сколько роботов реально в открытой сцене (ожидается 2).</summary>
    public static void ReportRobotInventory()
    {
        var robots = new List<RobotController>();
        foreach (RobotController rc in Object.FindObjectsByType<RobotController>(
                     FindObjectsInactive.Include))
        {
            if (rc == null) continue;
            if ((rc.gameObject.hideFlags & HideFlags.HideInHierarchy) != 0) continue; // фантомы
            robots.Add(rc);
        }

        if (robots.Count == 2)
        {
            Debug.Log(LogTag + " роботов в сцене: 2 (" + Describe(robots) + ") — как и должно быть.");
            return;
        }

        var sb = new StringBuilder();
        sb.Append(LogTag).Append(" роботов в сцене: ").Append(robots.Count)
          .Append(" (ожидается 2): ").Append(Describe(robots));
        if (robots.Count > 2)
            sb.Append(". Лишние копии создаёт кнопка «Добавить робота» (ObjectSpawner.SpawnRobot); ")
              .Append("удалить их можно пунктом меню ").Append(MenuRoot).Append("«Удалить копии роботов (UI)».");
        Debug.LogWarning(sb.ToString());
    }

    private static string Describe(List<RobotController> robots)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < robots.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(robots[i].name).Append(" [").Append(robots[i].GetType().Name).Append(']');
        }
        return sb.ToString();
    }

    // ------------------------------------------------------------------ меню

    [MenuItem(MenuRoot + "Очистить фантомные записи", false, 10)]
    private static void MenuPurge()
    {
        int n = Purge(verbose: true);
        EditorApplication.RepaintHierarchyWindow();
        if (n > 0) EditorUtility.DisplayDialog("KazistovVv · Hierarchy",
            "Удалено «фантомных» объектов: " + n + ".\nДерево иерархии обновлено.", "OK");
    }

    [MenuItem(MenuRoot + "Проверить инвентарь роботов (ожидается 2)", false, 11)]
    private static void MenuInventory() => ReportRobotInventory();

    /// <summary>Диагностика: что вообще есть в редакторе вне загруженных сцен (ничего не удаляет).</summary>
    [MenuItem(MenuRoot + "Диагностика: объекты вне сцен", false, 12)]
    private static void MenuDumpOrphans()
    {
        int count = 0;
        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go == null || EditorUtility.IsPersistent(go) || go.scene.IsValid()) continue;
            count++;
            Debug.Log(LogTag + " вне сцены: «" + go.name + "» hideFlags=" + go.hideFlags +
                      (go.GetComponentInChildren<RobotController>(true) != null ? " (копия робота)" : ""), go);
        }
        Debug.Log(LogTag + " объектов вне сцен: " + count + " (детали — в списке выше).");
    }

    /// <summary>
    /// Копии роботов, созданные в PlayMode через UI («Добавить робота»):
    /// имя «&lt;robotName&gt;_HHmmss», есть RegisteredObject. Удаляются только по подтверждению.
    /// Роботы стендов («Робот_6ос_Стенд1», «SCARA_Стенд2») не затрагиваются.
    /// </summary>
    [MenuItem(MenuRoot + "Удалить копии роботов, созданные через UI", false, 13)]
    private static void MenuDeleteSpawnedCopies()
    {
        var copies = new List<GameObject>();
        foreach (RobotController rc in Object.FindObjectsByType<RobotController>(
                     FindObjectsInactive.Include))
        {
            if (rc == null || rc.transform.parent != null) continue;
            if (rc.name == "Робот_6ос_Стенд1" || rc.name == "SCARA_Стенд2") continue; // роботы сцены
            if (rc.GetComponent<KazistovVvUI.RegisteredObject>() == null) continue;       // копии из UI
            copies.Add(rc.gameObject);
        }

        if (copies.Count == 0)
        {
            EditorUtility.DisplayDialog("KazistovVv · Hierarchy",
                "Копий роботов, созданных через UI, не найдено.", "OK");
            return;
        }

        var sb = new StringBuilder("Будут удалены копии роботов:\n");
        foreach (GameObject go in copies) sb.Append("  • ").Append(go.name).Append('\n');
        if (!EditorUtility.DisplayDialog("KazistovVv · Hierarchy", sb.ToString(), "Удалить", "Отмена")) return;

        int removed = 0;
        foreach (GameObject go in copies)
        {
            if (go == null) continue;
            Debug.Log(LogTag + " удалена копия робота: " + go.name, go);
            Kill(go);
            removed++;
        }
        EditorApplication.RepaintHierarchyWindow();
        Debug.Log(LogTag + " удалено копий роботов: " + removed + " (роботы стендов не тронуты).");
    }
}
#endif
