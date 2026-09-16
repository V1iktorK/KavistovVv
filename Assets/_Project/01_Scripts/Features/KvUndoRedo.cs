using System;
using System.Collections.Generic;
using UnityEngine;

namespace KazistovVvFeatures
{
    /// <summary>Одно отменяемое действие (ЭТАП 15 ТЗ).</summary>
    public interface IKvUndoAction
    {
        /// <summary>Подпись для журнала и подсказки кнопки.</summary>
        string Title { get; }
        /// <summary>Отменить действие.</summary>
        void Undo();
        /// <summary>Повторить действие после отмены.</summary>
        void Redo();
    }

    /// <summary>Готовое действие из двух делегатов (для мелких операций).</summary>
    public class KvDelegateAction : IKvUndoAction
    {
        public string title = "действие";
        public Action undo;
        public Action redo;

        public string Title { get { return title; } }
        public void Undo() { if (undo != null) undo(); }
        public void Redo() { if (redo != null) redo(); }
    }

    /// <summary>
    /// UNDO / REDO (ЭТАП 15 ТЗ).
    ///
    /// Стек отмен глубиной 20 (по ТЗ 10–20): смена точки, выбор траектории, запуск движения,
    /// а также операции новых функций (зоны, позы, записи). Отмена/повтор НЕ переписывают
    /// State Machine: каждая операция выполняется теми же публичными входами, что и действие
    /// оператора (`LockPointFromUi`, `SelectCandidateByIndex`, `StopExternalMotion` и т.п.).
    ///
    /// Горячие клавиши — Ctrl+Z / Ctrl+Y: проверено поиском по проекту, эти сочетания
    /// нигде не заняты (существующие бинды не тронуты).
    /// </summary>
    public class KvUndoStack
    {
        /// <summary>Глубина истории (ТЗ: 10–20 шагов).</summary>
        public int depth = 20;

        public event Action Changed;
        public event Action<string> Performed;   // «Отменено: …» / «Повторено: …»

        private readonly List<IKvUndoAction> undo = new List<IKvUndoAction>();
        private readonly List<IKvUndoAction> redo = new List<IKvUndoAction>();
        private bool applying;

        public int UndoCount { get { return undo.Count; } }
        public int RedoCount { get { return redo.Count; } }
        public bool CanUndo { get { return undo.Count > 0; } }
        public bool CanRedo { get { return redo.Count > 0; } }

        /// <summary>Заголовок следующей отмены/повтора (для подсказок кнопок).</summary>
        public string NextUndoTitle { get { return CanUndo ? undo[undo.Count - 1].Title : ""; } }
        public string NextRedoTitle { get { return CanRedo ? redo[redo.Count - 1].Title : ""; } }

        /// <summary>Записать выполненное действие в историю.</summary>
        public void Record(IKvUndoAction action)
        {
            if (action == null || applying) return;
            undo.Add(action);
            while (undo.Count > Mathf.Max(2, depth)) undo.RemoveAt(0);
            redo.Clear();
            if (Changed != null) Changed();
        }

        /// <summary>Записать действие делегатами.</summary>
        public void Record(string title, Action undoAction, Action redoAction)
        {
            Record(new KvDelegateAction { title = title, undo = undoAction, redo = redoAction });
        }

        public bool Undo()
        {
            if (!CanUndo) return false;
            IKvUndoAction action = undo[undo.Count - 1];
            undo.RemoveAt(undo.Count - 1);
            applying = true;
            try
            {
                action.Undo();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Undo] «" + action.Title + "»: " + e.Message);
            }
            finally
            {
                applying = false;
            }
            redo.Add(action);
            Debug.Log("[Undo] отменено: " + action.Title);
            if (Performed != null) Performed("Отменено: " + action.Title);
            if (Changed != null) Changed();
            return true;
        }

        public bool Redo()
        {
            if (!CanRedo) return false;
            IKvUndoAction action = redo[redo.Count - 1];
            redo.RemoveAt(redo.Count - 1);
            applying = true;
            try
            {
                action.Redo();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Undo] повтор «" + action.Title + "»: " + e.Message);
            }
            finally
            {
                applying = false;
            }
            undo.Add(action);
            Debug.Log("[Undo] повторено: " + action.Title);
            if (Performed != null) Performed("Повторено: " + action.Title);
            if (Changed != null) Changed();
            return true;
        }

        /// <summary>Идёт применение отмены/повтора (чтобы наблюдатель не записал это как новое действие).</summary>
        public bool Applying { get { return applying; } }

        public void Clear()
        {
            undo.Clear();
            redo.Clear();
            if (Changed != null) Changed();
        }

        /// <summary>Список последних действий (для панели журнала/диагностики).</summary>
        public List<string> History(int max = 10)
        {
            List<string> result = new List<string>();
            int from = Mathf.Max(0, undo.Count - max);
            for (int i = undo.Count - 1; i >= from; i--)
                result.Add((i == undo.Count - 1 ? "→ " : "  ") + undo[i].Title);
            return result;
        }
    }
}
