using System;
using System.Collections.Generic;
using UnityEngine;
using KazistovVvUI;
using TrajectoryCore;

namespace KazistovVvFeatures
{
    /// <summary>
    /// ОБЩИЕ ОПЕРАЦИИ НАД ВАРИАНТАМИ ТРАЕКТОРИЙ (нужны этапам 4–6: сглаживание,
    /// время-оптимальная траектория, эко-профиль).
    ///
    /// Задача модуля — аккуратно подменить геометрию/время УЖЕ ПОСТРОЕННОГО варианта,
    /// не ломая ни «колбаску», ни летящие фантомы:
    ///   • путь копируется В ТЕ ЖЕ массивы (`double[][]`), если число сэмплов совпадает:
    ///     фантомы держат ссылку на эти массивы и сразу видят новую геометрию;
    ///   • «колбаска» (`TrajectoryTube`) перестраивается публичным `Build(...)`;
    ///   • метрики варианта (длина, время, зазор, запас) обновляются в самом кандидате.
    ///
    /// Ничего в ядре не меняется: используются только публичные поля и методы потока.
    /// </summary>
    public static class KvVariantKit
    {
        /// <summary>Позиция базы робота (для энергетической модели этапа 6).</summary>
        public static Vector3 RobotBase(TrajectoryFlowController flow)
        {
            if (flow != null && flow.Robot != null) return flow.Robot.transform.position;
            return Vector3.zero;
        }

        /// <summary>Индекс варианта, с которым работает оператор (выбранный, иначе лучший).</summary>
        public static int SelectedIndex(TrajectoryFlowController flow)
        {
            if (flow == null || flow.State == null) return -1;
            int selected = flow.State.selectedTrajectory;
            if (selected >= 0 && selected < flow.State.candidates.Count) return selected;
            return flow.State.candidates.Count > 0 ? 0 : -1;
        }

        /// <summary>Вариант по индексу (с проверкой диапазона).</summary>
        public static TrajectoryCandidate At(TrajectoryFlowController flow, int index)
        {
            if (flow == null || flow.State == null) return null;
            if (index < 0 || index >= flow.State.candidates.Count) return null;
            return flow.State.candidates[index];
        }

        /// <summary>Вариант, с которым работает оператор (или null, если траекторий нет).</summary>
        public static TrajectoryCandidate Selected(TrajectoryFlowController flow, out int index)
        {
            index = SelectedIndex(flow);
            return At(flow, index);
        }

        /// <summary>
        /// Подменить план варианта: геометрия копируется в существующие массивы (если
        /// число сэмплов совпадает — иначе ссылка заменяется), время берётся из нового
        /// плана, «колбаска» и метрики варианта обновляются.
        /// </summary>
        public static bool ApplyPlan(TrajectoryFlowController flow, TrajectoryCandidate candidate,
            PlannedTrajectory plan, bool rebuildTube = true)
        {
            if (candidate == null || plan == null || plan.Path == null || plan.Path.Length < 2)
                return false;

            if (candidate.plan != null)
            {
                double[][] oldPath = candidate.plan.Path;
                if (oldPath != null && oldPath.Length == plan.Path.Length &&
                    oldPath.Length > 0 && oldPath[0] != null &&
                    plan.Path[0] != null && oldPath[0].Length == plan.Path[0].Length)
                {
                    // Копируем ЗНАЧЕНИЯ: на эти же массивы ссылаются фантомы в полёте.
                    for (int i = 0; i < oldPath.Length; i++)
                        for (int j = 0; j < oldPath[i].Length; j++)
                            oldPath[i][j] = plan.Path[i][j];
                    plan.Path = oldPath;
                }
            }

            candidate.plan = plan;
            candidate.label = string.IsNullOrEmpty(plan.Label) ? candidate.label : plan.Label;
            candidate.timeS = (float)plan.Time;

            if (flow != null && flow.Validator != null && flow.Validator.Ready)
            {
                Vector3[] tube = KvTrajMath.TcpPolyline(flow.Validator, plan.Path);
                float length = 0f;
                for (int i = 1; i < tube.Length; i++) length += Vector3.Distance(tube[i - 1], tube[i]);
                candidate.tube = tube;
                candidate.lengthM = length;
                if (rebuildTube && candidate.view != null)
                {
                    try { candidate.view.Build(tube, candidate.color); }
                    catch (Exception e) { Debug.LogWarning("[Variants] колбаска не перестроена: " + e.Message); }
                }
            }
            return true;
        }

        /// <summary>Краткая подпись варианта для интерфейса: «№3 · название».</summary>
        public static string Label(TrajectoryCandidate candidate, int index)
        {
            if (candidate == null) return "№" + (index + 1);
            string label = string.IsNullOrEmpty(candidate.label) ? "" : " · " + candidate.label;
            return "№" + (index + 1) + label;
        }

        /// <summary>Сколько вариантов сейчас построено.</summary>
        public static int Count(TrajectoryFlowController flow)
        {
            return flow != null && flow.State != null ? flow.State.candidates.Count : 0;
        }

        /// <summary>Готова ли сцена к постобработке (есть варианты и валидатор).</summary>
        public static bool Ready(TrajectoryFlowController flow)
        {
            if (flow == null || flow.Validator == null || !flow.Validator.Ready) return false;
            if (flow.State == null || flow.State.candidates.Count == 0) return false;
            FlowState phase = flow.State.phase;
            return phase == FlowState.TrajectoriesShown || phase == FlowState.PhantomsMoving ||
                   phase == FlowState.RobotMoving;
        }

        /// <summary>Состояние сцены одной строкой (для вкладок верстака).</summary>
        public static string SceneStatus(TrajectoryFlowController flow)
        {
            if (flow == null) return KvLocExtra.T("smooth.noplan",
                "Нет траектории: выберите точку (Z + ЛКМ) и подтвердите вариант (X + ЛКМ)");
            if (flow.State == null || flow.State.candidates.Count == 0)
                return KvLocExtra.T("smooth.noplan",
                    "Нет траектории: выберите точку (Z + ЛКМ) и подтвердите вариант (X + ЛКМ)");
            return "";
        }
    }
}
