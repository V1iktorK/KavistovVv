using TrajectoryCore;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Инструменты сборки стендов (столы + роботы) в MainScene.
///
/// ВАЖНО: пункты меню Tools/KOMPAS отсюда УДАЛЕНЫ — столы и роботы теперь
/// стоят в MainScene как обычные объекты сцены (см. Assets/_Project/00_Scenes/
/// MainScene.unity) и не должны создаваться ни кнопкой, ни в рантайме.
/// Класс оставлен как утилита: методы можно вызвать из своего кода/консоли,
/// если потребуется пересобрать стенды вручную.
/// </summary>
public static class StandsMenu
{
    /// <summary>Пересобрать сцену: удалить старые столы/роботов и создать два стенда.</summary>
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

    /// <summary>Разместить два стенда, не удаляя существующие объекты.</summary>
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
