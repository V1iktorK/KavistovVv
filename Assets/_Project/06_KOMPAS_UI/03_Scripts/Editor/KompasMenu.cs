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
                Debug.Log("[KompasUI] Менеджер уже есть в сцене: " + existing.name +
                          " (в Play-режиме он пересоздаёт интерфейс автоматически).");
                return;
            }

            GameObject go = new GameObject("KOMPAS_UI");
            go.AddComponent<KompasUIManager>();
            Selection.activeGameObject = go;

            // MarkSceneDirty нельзя вызывать в Play-режиме.
            if (!EditorApplication.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(go.scene);
                Debug.Log("[KompasUI] KOMPAS_UI создан. Нажмите Play — интерфейс появится перед камерой.");
            }
            else
            {
                Debug.Log("[KompasUI] KOMPAS_UI создан прямо в Play-режиме. " +
                          "Чтобы сохранить его в сцене — создайте ещё раз вне Play (объект пересоздастся).");
            }
        }

        [MenuItem("Tools/KOMPAS UI/Удалить из сцены")]
        public static void RemoveFromScene()
        {
            KompasUIManager mgr = Object.FindAnyObjectByType<KompasUIManager>();
            if (mgr == null)
            {
                Debug.Log("[KompasUI] Менеджера в сцене нет.");
                return;
            }
            GameObject go = mgr.gameObject;
            Object.DestroyImmediate(go);
            if (!EditorApplication.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }
            Debug.Log("[KompasUI] KOMPAS_UI удалён.");
        }

        [MenuItem("Tools/KOMPAS UI/Пересобрать дерево")]
        public static void RebuildTree()
        {
            KompasUIManager mgr = Object.FindAnyObjectByType<KompasUIManager>();
            if (mgr != null) mgr.Refresh();
        }
    }
}
