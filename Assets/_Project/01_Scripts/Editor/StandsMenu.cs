using TrajectoryCore;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Меню редактора: пересоздать сцену стендов прямо в MainScene
/// (удалить старые столы/роботов, создать два стола и двух роботов).
/// </summary>
public static class StandsMenu
{
    [MenuItem("Tools/KOMPAS/Пересоздать сцену: 2 стола + роботы")]
    public static void RebuildScene()
    {
        StandBuilder.RebuildStandaloneScene(0.98f);
        if (!Application.isPlaying)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }
        Debug.Log("[Stands] Сцена пересобрана: (0,0,-24) — 6-осевой, (0,0,-28) — SCARA");
    }

    [MenuItem("Tools/KOMPAS/Разместить 2 стенда (без удаления старого)")]
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
