using TrajectoryCore;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Меню редактора: разместить два стенда (столы + роботы) в текущей сцене.
///   Стенд 1: (0,0,-24) — 6-осевой;   Стенд 2: (0,0,-28) — SCARA.
/// Идемпотентно: повторный запуск ничего не дублирует.
/// </summary>
public static class StandsMenu
{
    [MenuItem("Tools/KOMPAS/Разместить 2 стенда (столы + роботы)")]
    public static void PlaceStands()
    {
        StandBuilder.EnsureStands(0.98f);
        if (!Application.isPlaying)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }
        Debug.Log("[Stands] Стенды размещены: (0,0,-24) 6-осевой, (0,0,-28) SCARA");
    }
}
