using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KompasUI;

namespace KompasUI.EditorTools
{
    /// <summary>Меню для быстрого создания KOMPAS-интерфейса в сцене.</summary>
    public static class KompasMenu
    {
        [MenuItem("Tools/KOMPAS UI/Создать в сцене")]
        public static void CreateInScene()
        {
            // Если уже есть — подсвечиваем.
            KompasUIManager existing = Object.FindAnyObjectByType<KompasUIManager>();
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                Debug.Log("[KompasUI] Менеджер уже есть в сцене: " + existing.name);
                return;
            }

            GameObject go = new GameObject("KOMPAS_UI");
            go.AddComponent<KompasUIManager>();
            Selection.activeGameObject = go;
            Debug.Log("[KompasUI] KOMPAS_UI создан. Нажмите Play — интерфейс появится перед камерой.");
            EditorSceneManager.MarkSceneDirty(go.scene);
        }

        [MenuItem("Tools/KOMPAS UI/Пересобрать дерево")]
        public static void RebuildTree()
        {
            KompasUIManager mgr = Object.FindAnyObjectByType<KompasUIManager>();
            if (mgr != null) mgr.Refresh();
        }
    }
}
