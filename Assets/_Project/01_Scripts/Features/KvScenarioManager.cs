using System;
using System.Collections.Generic;
using UnityEngine;

namespace KazistovVvFeatures
{
    /// <summary>Один шаг сценария: что сделать и когда считать шаг завершённым.</summary>
    public class KvScenarioStep
    {
        public string Text = "";
        /// <summary>Выполнить шаг (может быть null — тогда шаг только «ждёт»).</summary>
        public Action Enter;
        /// <summary>Признак завершения (null — ждём только MinTime).</summary>
        public Func<bool> Done;
        /// <summary>Минимальное время шага, с.</summary>
        public float MinTime = 1.5f;
        /// <summary>Максимальное время шага, с (страховка от «зависания»).</summary>
        public float MaxTime = 60f;
    }

    /// <summary>Готовый сценарий: список шагов, выполняемых последовательно.</summary>
    public class KvScenario
    {
        public string Id = "";
        public string Title = "";
        public string Description = "";
        public readonly List<KvScenarioStep> Steps = new List<KvScenarioStep>();
    }

    /// <summary>
    /// МЕНЕДЖЕР СЦЕНАРИЕВ (ЭТАП 18 ТЗ).
    ///
    /// Список готовых сценариев («Показать workspace», «Продемонстрировать pick-and-place»,
    /// «Показать лимиты», «Демонстрация 8 траекторий»); каждый — набор шагов, выполняемых
    /// ПОСЛЕДОВАТЕЛЬНО. Кнопка «Запустить сценарий», пауза и отмена.
    ///
    /// Шаги оперируют только публичными возможностями проекта (визуализации, выбор точки,
    /// выбор траектории, внешнее движение через `PlayExternalPlan`), поэтому State Machine,
    /// лазеры, планировщик и IK не переписываются. Сценарии собирает хаб функций — там же,
    /// где живут сервисы, — а этот класс отвечает только за последовательное выполнение,
    /// паузу, отмену и журнал.
    /// </summary>
    public class KvScenarioManager
    {
        public event Action<string> Message;
        public event Action Changed;

        private readonly List<KvScenario> scenarios = new List<KvScenario>();
        private KvScenario running;
        private int stepIndex = -1;
        private float stepTime;
        private float totalTime;
        private bool paused;

        public IReadOnlyList<KvScenario> All { get { return scenarios; } }
        public bool Running { get { return running != null; } }
        public bool Paused { get { return paused; } }
        public string RunningTitle { get { return running != null ? running.Title : ""; } }
        public int StepIndex { get { return stepIndex; } }
        public int StepCount { get { return running != null ? running.Steps.Count : 0; } }
        public string CurrentStepText
        {
            get
            {
                if (running == null || stepIndex < 0 || stepIndex >= running.Steps.Count) return "";
                return running.Steps[stepIndex].Text;
            }
        }

        public void Add(KvScenario scenario)
        {
            if (scenario == null || string.IsNullOrEmpty(scenario.Id)) return;
            for (int i = 0; i < scenarios.Count; i++)
            {
                if (scenarios[i].Id == scenario.Id)
                {
                    scenarios[i] = scenario;
                    if (Changed != null) Changed();
                    return;
                }
            }
            scenarios.Add(scenario);
            if (Changed != null) Changed();
        }

        public KvScenario Find(string id)
        {
            for (int i = 0; i < scenarios.Count; i++)
                if (scenarios[i].Id == id) return scenarios[i];
            return null;
        }

        /// <summary>Запустить сценарий по id (или по индексу в списке).</summary>
        public bool Start(string id)
        {
            KvScenario scenario = Find(id);
            if (scenario == null)
            {
                Report("сценарий «" + id + "» не найден");
                return false;
            }
            return Start(scenario);
        }

        public bool Start(KvScenario scenario)
        {
            if (scenario == null || scenario.Steps.Count == 0) return false;
            if (running != null) Stop("перезапуск");

            running = scenario;
            stepIndex = -1;
            stepTime = 0f;
            totalTime = 0f;
            paused = false;
            Report("сценарий «" + scenario.Title + "» запущен · шагов " + scenario.Steps.Count);
            Advance();
            if (Changed != null) Changed();
            return true;
        }

        /// <summary>Пауза/продолжение сценария.</summary>
        public bool TogglePause()
        {
            if (running == null) return false;
            paused = !paused;
            Report("сценарий «" + running.Title + "» " + (paused ? "на паузе" : "продолжен"));
            if (Changed != null) Changed();
            return true;
        }

        public void SetPaused(bool value)
        {
            if (running == null) return;
            paused = value;
            if (Changed != null) Changed();
        }

        /// <summary>Отменить сценарий.</summary>
        public void Stop(string why = "отменено оператором")
        {
            if (running == null) return;
            string title = running.Title;
            running = null;
            stepIndex = -1;
            paused = false;
            Report("сценарий «" + title + "» " + why);
            if (Changed != null) Changed();
        }

        /// <summary>Кадровое ведение (вызывает хаб).</summary>
        public void Tick(float deltaTime)
        {
            if (running == null) return;

            if (stepIndex < 0 || stepIndex >= running.Steps.Count)
            {
                Finish();
                return;
            }
            if (paused) return;

            stepTime += deltaTime;
            totalTime += deltaTime;

            KvScenarioStep step = running.Steps[stepIndex];
            bool done = stepTime >= step.MinTime && (step.Done == null || Safe(step.Done));
            if (stepTime >= step.MaxTime)
            {
                Report("шаг «" + step.Text + "» не завершился за " +
                       step.MaxTime.ToString("0") + " с — перехожу к следующему");
                done = true;
            }

            if (done) Advance();
        }

        private void Advance()
        {
            if (running == null) return;
            stepIndex++;
            stepTime = 0f;
            if (stepIndex >= running.Steps.Count)
            {
                Finish();
                return;
            }

            KvScenarioStep step = running.Steps[stepIndex];
            Report("шаг " + (stepIndex + 1) + "/" + running.Steps.Count + ": " + step.Text);
            if (step.Enter != null)
            {
                try
                {
                    step.Enter();
                }
                catch (Exception e)
                {
                    Report("шаг «" + step.Text + "» упал: " + e.Message);
                }
            }
            if (Changed != null) Changed();
        }

        private void Finish()
        {
            string title = running != null ? running.Title : "";
            running = null;
            stepIndex = -1;
            Report("сценарий «" + title + "» завершён за " + totalTime.ToString("0.0") + " с");
            if (Changed != null) Changed();
        }

        private static bool Safe(Func<bool> predicate)
        {
            try
            {
                return predicate();
            }
            catch (Exception)
            {
                return true;    // ошибка в предикате не должна «вешать» сценарий
            }
        }

        private void Report(string text)
        {
            Debug.Log("[Scenario] " + text);
            if (Message != null) Message(text);
        }
    }
}
