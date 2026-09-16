using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KazistovVvUI;

namespace KazistovVvUI.EditorTools
{
    /// <summary>Меню для быстрого создания KazistovVv-интерфейса в сцене.</summary>
    public static class KazistovVvMenu
    {
        [MenuItem("Tools/KazistovVv UI/Создать в сцене")]
        public static void CreateInScene()
        {
            // Если уже есть — подсвечиваем.
            KazistovVvUIManager existing = Object.FindAnyObjectByType<KazistovVvUIManager>();
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                Debug.Log("[KazistovVvUI] Менеджер уже есть в сцене: " + existing.name +
                          " (в Play-режиме он пересоздаёт интерфейс автоматически).");
                return;
            }

            GameObject go = new GameObject("KazistovVv_UI");
            go.AddComponent<KazistovVvUIManager>();
            Selection.activeGameObject = go;

            // MarkSceneDirty нельзя вызывать в Play-режиме.
            if (!EditorApplication.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(go.scene);
                Debug.Log("[KazistovVvUI] KazistovVv_UI создан. Нажмите Play — интерфейс появится перед камерой.");
            }
            else
            {
                Debug.Log("[KazistovVvUI] KazistovVv_UI создан прямо в Play-режиме. " +
                          "Чтобы сохранить его в сцене — создайте ещё раз вне Play (объект пересоздастся).");
            }
        }

        [MenuItem("Tools/KazistovVv UI/Удалить из сцены")]
        public static void RemoveFromScene()
        {
            KazistovVvUIManager mgr = Object.FindAnyObjectByType<KazistovVvUIManager>();
            if (mgr == null)
            {
                Debug.Log("[KazistovVvUI] Менеджера в сцене нет.");
                return;
            }
            GameObject go = mgr.gameObject;
            Object.DestroyImmediate(go);
            if (!EditorApplication.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }
            Debug.Log("[KazistovVvUI] KazistovVv_UI удалён.");
        }

        [MenuItem("Tools/KazistovVv UI/Пересобрать дерево")]
        public static void RebuildTree()
        {
            KazistovVvUIManager mgr = Object.FindAnyObjectByType<KazistovVvUIManager>();
            if (mgr != null) mgr.Refresh();
        }
    }
}
