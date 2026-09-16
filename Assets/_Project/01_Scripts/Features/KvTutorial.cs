using System;
using System.Collections.Generic;
using UnityEngine;
using KazistovVvUI;
using TrajectoryCore;

namespace KazistovVvFeatures
{
    /// <summary>
    /// ЭТАП 2 ТЗ: ТУТОРИАЛ / ONBOARDING.
    ///
    /// Четыре шага ровно по ТЗ:
    ///   1. как выбрать точку (красный лазер);
    ///   2. как подтвердить траекторию (зелёный лазер);
    ///   3. как двигать точку (режим перемещения точки);
    ///   4. как выбрать фантом (переключение варианта).
    ///
    /// Каждый шаг завершается САМ, когда оператор действительно выполнил действие —
    /// условие проверяется по фактическому состоянию State Machine (`FlowState`,
    /// `SelectionState`), а не по нажатию кнопки «Далее»: так обучение учит реальной
    /// работе. Кнопка «Далее» остаётся как ручной пропуск шага.
    ///
    /// ПОДСВЕТКА: нужная кнопка тулбара обводится пульсирующей рамкой
    /// (<see cref="KvHighlightFrame"/>), прямоугольник берётся у самой кнопки —
    /// интерфейс не перестраивается и не дублируется.
    ///
    /// ПРОГРЕСС сохраняется в PlayerPrefs (`KazistovVv.Tutorial.*`), поэтому обучение
    /// можно прервать в любой момент и продолжить позже (кнопка «Продолжить обучение»).
    /// Кнопка «Пропустить туториал» выключает подсказки, прогресс остаётся.
    ///
    /// Этот же модуль даёт экранную карточку для демонстрации (этап 3): `ShowCaption`.
    /// </summary>
    public class KvTutorial : MonoBehaviour
    {
        /// <summary>Ключ PlayerPrefs: номер текущего шага.</summary>
        public const string StepPrefsKey = "KazistovVv.Tutorial.Step";
        /// <summary>Ключ PlayerPrefs: обучение пройдено до конца.</summary>
        public const string DonePrefsKey = "KazistovVv.Tutorial.Done";
        /// <summary>Ключ PlayerPrefs: обучение было начато (для «продолжить»).</summary>
        public const string ActivePrefsKey = "KazistovVv.Tutorial.Active";

        public int sortingOrder = 210;

        public event Action<string> Message;

        /// <summary>Один шаг обучения.</summary>
        private class Step
        {
            public string TitleKey;
            public string TextKey;
            public string HighlightCommand;      // id команды тулбара ("" — не подсвечивать)
            public string Hotkey;                // «Z», «X», «Enter», «ЛКМ»
            public Func<bool> Done;              // условие автозавершения
        }

        private readonly List<Step> steps = new List<Step>();
        private Canvas canvas;
        private RectTransform canvasRect;
        private KvHintCard card;
        private KvHighlightFrame highlight;
        private TrajectoryFlowController flow;
        private int stepIndex;
        private bool active;
        private bool captionMode;
        private int phantomIndexAtStepStart = -1;

        public bool Active { get { return active; } }
        public bool CaptionVisible { get { return card != null && card.Visible; } }
        public int StepIndex { get { return stepIndex; } }
        public int StepCount { get { return steps.Count; } }
        public KvHintCard Card { get { return card; } }

        /// <summary>Обучение пройдено до конца (сохраняется между запусками).</summary>
        public static bool Completed { get { return PlayerPrefs.GetInt(DonePrefsKey, 0) != 0; } }
        /// <summary>Обучение было начато, но не закончено.</summary>
        public static bool StartedNotFinished
        {
            get { return PlayerPrefs.GetInt(ActivePrefsKey, 0) != 0 && !Completed; }
        }
        /// <summary>Сохранённый шаг (для «продолжить позже»).</summary>
        public static int SavedStep { get { return PlayerPrefs.GetInt(StepPrefsKey, 0); } }

        // ================================================================== сборка

        public void Build(TrajectoryFlowController controller)
        {
            flow = controller;
            BuildSteps();

            canvas = KvOverlayKit.CreateCanvas(transform, "KvTutorialCanvas", sortingOrder);
            canvasRect = (RectTransform)canvas.transform;
            card = KvHintCard.Create(canvasRect);
            highlight = KvHighlightFrame.Create(canvasRect);
            highlight.gameObject.SetActive(false);
        }

        private void BuildSteps()
        {
            steps.Clear();

            steps.Add(new Step
            {
                TitleKey = "tut.step1.title",
                TextKey = "tut.step1.text",
                HighlightCommand = "point.select",
                Hotkey = "Z + ЛКМ",
                Done = delegate
                {
                    return flow != null && flow.State != null && flow.State.hasPoint;
                }
            });

            steps.Add(new Step
            {
                TitleKey = "tut.step2.title",
                TextKey = "tut.step2.text",
                HighlightCommand = "path.select",
                Hotkey = "X + ЛКМ",
                Done = delegate
                {
                    if (flow == null || flow.State == null) return false;
                    FlowState phase = flow.State.phase;
                    return flow.State.selectedTrajectory >= 0 &&
                           (phase == FlowState.PhantomsMoving || phase == FlowState.RobotMoving);
                }
            });

            steps.Add(new Step
            {
                TitleKey = "tut.step3.title",
                TextKey = "tut.step3.text",
                HighlightCommand = "",
                Hotkey = "Enter · QWEASD · Esc",
                Done = delegate
                {
                    if (flow == null || flow.State == null) return false;
                    // Шаг засчитан, если оператор вошёл в режим перемещения точки
                    // и вышел из него (подтвердил или отменил) — то есть реально попробовал.
                    if (flow.State.phase == FlowState.PointMoveMode)
                    {
                        enteredMoveMode = true;
                        return false;
                    }
                    return enteredMoveMode;
                }
            });

            steps.Add(new Step
            {
                TitleKey = "tut.step4.title",
                TextKey = "tut.step4.text",
                HighlightCommand = "path.select",
                Hotkey = "X + ЛКМ",
                Done = delegate
                {
                    if (flow == null || flow.State == null) return false;
                    if (flow.State.phase != FlowState.PhantomsMoving) return false;
                    if (phantomIndexAtStepStart < 0) return false;
                    return flow.State.selectedTrajectory != phantomIndexAtStepStart;
                }
            });
        }

        private bool enteredMoveMode;

        // ================================================================== управление обучением

        /// <summary>Начать (или продолжить) обучение.</summary>
        public void StartTutorial(bool resume)
        {
            if (card == null) return;
            captionMode = false;
            active = true;
            enteredMoveMode = false;
            phantomIndexAtStepStart = -1;

            stepIndex = resume ? Mathf.Clamp(SavedStep, 0, Mathf.Max(0, steps.Count - 1)) : 0;
            if (!resume)
            {
                PlayerPrefs.SetInt(DonePrefsKey, 0);
                PlayerPrefs.SetInt(StepPrefsKey, 0);
            }
            PlayerPrefs.SetInt(ActivePrefsKey, 1);
            PlayerPrefs.Save();

            ShowCurrentStep();
            Report("туториал: шаг " + (stepIndex + 1) + " из " + steps.Count +
                   (resume ? " (продолжение)" : ""));
        }

        /// <summary>Тумблер по клавише F3: включить/выключить обучение.</summary>
        public void Toggle()
        {
            if (active) Skip("кнопка интерфейса");
            else if (StartedNotFinished) StartTutorial(true);
            else StartTutorial(false);
        }

        /// <summary>«Пропустить туториал»: подсказки выключаются, прогресс сохраняется.</summary>
        public void Skip(string why)
        {
            active = false;
            captionMode = false;
            if (card != null) card.Hide();
            if (highlight != null) highlight.SetTarget(null);
            PlayerPrefs.SetInt(StepPrefsKey, stepIndex);
            PlayerPrefs.Save();
            Report("туториал прерван (" + why + ") · прогресс сохранён, шаг " + (stepIndex + 1) +
                   " — «Продолжить обучение»");
        }

        /// <summary>Начать обучение заново (кнопка после завершения).</summary>
        public void RestartTutorial()
        {
            PlayerPrefs.SetInt(DonePrefsKey, 0);
            PlayerPrefs.SetInt(StepPrefsKey, 0);
            PlayerPrefs.Save();
            StartTutorial(false);
        }

        private void Complete()
        {
            active = false;
            PlayerPrefs.SetInt(DonePrefsKey, 1);
            PlayerPrefs.SetInt(StepPrefsKey, steps.Count);
            PlayerPrefs.Save();

            if (card != null)
            {
                card.SetTitle(KvLocExtra.T("tut.title", "Обучение"));
                card.SetBody(KvLocExtra.T("tut.done", "Обучение пройдено — все шаги выполнены"));
                card.SetStep(steps.Count - 1, steps.Count);
                card.SetFooter(KvLocExtra.T("tut.progress",
                    "Прогресс сохраняется автоматически — обучение можно продолжить позже"));
                card.SetButtons(
                    new[] { KvLocExtra.T("tut.restart", "Начать заново"),
                            KvLocExtra.T("common.close", "Закрыть") },
                    new Action[] { RestartTutorial, delegate { if (card != null) card.Hide(); } });
                card.Show();
            }
            if (highlight != null) highlight.SetTarget(null);
            Report("туториал пройден полностью");
        }

        private void ShowCurrentStep()
        {
            if (card == null || steps.Count == 0) return;
            stepIndex = Mathf.Clamp(stepIndex, 0, steps.Count - 1);
            Step step = steps[stepIndex];

            card.SetTitle(KvLocExtra.T(step.TitleKey, step.TitleKey));
            string body = KvLocExtra.T(step.TextKey, "");
            if (!string.IsNullOrEmpty(step.Hotkey)) body += "\n\n" + step.Hotkey;
            card.SetBody(body);
            card.SetStep(stepIndex, steps.Count);
            card.SetFooter(KvLocExtra.T("tut.waiting",
                "Выполните действие в сцене — шаг завершится сам") + "   ·   " +
                KvLocExtra.T("tut.progress",
                    "Прогресс сохраняется автоматически — обучение можно продолжить позже"));

            bool last = stepIndex >= steps.Count - 1;
            card.SetButtons(
                new[]
                {
                    KvLocExtra.T("tut.skip", "Пропустить туториал"),
                    KvLocExtra.T("tut.manual", "Отметить выполненным"),
                    last ? KvLocExtra.T("common.close", "Закрыть") : KvLocExtra.T("tut.next", "Далее")
                },
                new Action[]
                {
                    delegate { Skip("кнопка «Пропустить туториал»"); },
                    delegate { MarkStepDone(); },
                    delegate { if (last) Skip("кнопка «Закрыть»"); else MarkStepDone(); }
                });

            card.Show();
            AimHighlight(step);
            PlayerPrefs.SetInt(StepPrefsKey, stepIndex);
            PlayerPrefs.Save();
        }

        private void AimHighlight(Step step)
        {
            if (highlight == null) return;
            RectTransform target = null;

            KazistovVvUIManager ui = KazistovVvUIManager.Instance;
            if (ui != null && !string.IsNullOrEmpty(step.HighlightCommand))
                target = ui.CommandButtonRect(step.HighlightCommand);

            highlight.SetTarget(target);
            if (target == null)
                Report("подсказка шага " + (stepIndex + 1) + ": кнопка тулбара скрыта — " +
                       "ориентируйтесь на текст подсказки");
        }

        /// <summary>
        /// Отметить текущий шаг выполненным (кнопка карточки «Отметить выполненным»,
        /// а также внешние вызовы — например диагностический прогон).
        /// </summary>
        public void MarkStepDone()
        {
            Advance(true);
        }

        private void Advance(bool manual)
        {
            if (!active) return;

            if (stepIndex >= steps.Count - 1)
            {
                Complete();
                return;
            }
            stepIndex++;
            if (steps[stepIndex].TitleKey == "tut.step4.title")
                phantomIndexAtStepStart = flow != null && flow.State != null
                    ? flow.State.selectedTrajectory : -1;
            ShowCurrentStep();
            Report("туториал: шаг " + (stepIndex + 1) + " из " + steps.Count +
                   (manual ? " (отмечен вручную)" : " (выполнен)"));
        }

        /// <summary>Кадровое обслуживание: проверка условия шага.</summary>
        public void Tick(float deltaTime)
        {
            if (!active || captionMode || card == null) return;
            if (flow == null)
            {
                KazistovVvUIManager ui = KazistovVvUIManager.Instance;
                if (ui != null) flow = ui.Flow;
                if (flow == null) return;
            }

            // Язык могли переключить на ходу — подписи шага обновляются.
            if (KvLoc.CurrentCode != lastLanguage)
            {
                lastLanguage = KvLoc.CurrentCode;
                ShowCurrentStep();
            }

            if (steps.Count == 0) return;
            Step step = steps[stepIndex];
            if (step.Done == null) return;
            bool done;
            try { done = step.Done(); }
            catch (Exception) { done = false; }
            if (done) Advance(false);
        }

        private string lastLanguage = "";

        // ================================================================== карточка для демонстрации (этап 3)

        /// <summary>Показать карточку с произвольным текстом (режим демонстрации).</summary>
        public void ShowCaption(string title, string body, string footer, string[] buttons,
            Action[] actions, int index = -1, int total = 0)
        {
            if (card == null) return;
            captionMode = true;
            if (highlight != null) highlight.SetTarget(null);
            card.SetTitle(title);
            card.SetBody(body);
            card.SetFooter(footer);
            if (total > 0) card.SetStep(index, total);
            card.SetButtons(buttons, actions);
            card.Show();
        }

        /// <summary>Скрыть карточку (демонстрация завершена, обучение выключено).</summary>
        public void HideCaption()
        {
            captionMode = false;
            if (card != null) card.Hide();
            if (highlight != null) highlight.SetTarget(null);
        }

        /// <summary>Просто скрыть карточку, не меняя режим (используется при выходе из демо).</summary>
        public void HideCardOnly()
        {
            if (card != null) card.Hide();
        }

        private void Report(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            Debug.Log("[Tutorial] " + text);
            if (Message != null) Message(text);
        }
    }
}
