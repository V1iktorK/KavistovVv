using UnityEngine;
using UnityEngine.Rendering;

namespace KazistovVvFeatures
{
    /// <summary>
    /// ПРОВЕРКА НАЛИЧИЯ ГРАФИКИ (для сервисов этапов 13–36).
    ///
    /// Зачем это нужно. Все новые сервисы разделены на ЛОГИКУ и ПОКАЗ: расчёт моментов,
    /// времени достижимости, распознавание жестов, макросы, дерево поведения, имитация отказов
    /// и проверка перед пуском работают без графики, а вот стрелки, меши, окна «картинки в
    /// картинке» и трёхмерные маркеры — нет. В режиме без видеокарты (например, пакетный запуск
    /// `-nographics`, используемый для автоматических прогонов) попытка отрисовать материал HDRP
    /// приводит к ошибке создания буферов рендера, после чего движение кадров прекращается —
    /// то есть «пропадает» не только картинка, но и вся дальнейшая логика.
    ///
    /// Поэтому сервисы спрашивают <see cref="Available"/> и, если графики нет, честно сообщают
    /// об этом в журнал и НЕ создают трёхмерные объекты, продолжая считать всё остальное.
    /// В обычном редакторе с видеокартой значение всегда true — поведение не меняется.
    /// </summary>
    public static class KvGraphics
    {
        private static int probed = -1;

        /// <summary>Есть ли рабочая графика (не «Null Device» и не нулевой уровень шейдеров).</summary>
        public static bool Available
        {
            get
            {
                if (probed < 0) Probe();
                return probed == 1;
            }
        }

        /// <summary>Короткое объяснение для журнала и интерфейса.</summary>
        public static string Reason { get; private set; }

        private static void Probe()
        {
            GraphicsDeviceType device = GraphicsDeviceType.Null;
            int shaderLevel = 0;
            try
            {
                device = SystemInfo.graphicsDeviceType;
                shaderLevel = SystemInfo.graphicsShaderLevel;
            }
            catch (System.Exception)
            {
                // В некоторых пакетных режимах запрос к системе графики недоступен — считаем,
                // что графики нет: логика продолжит работать, показ будет выключен.
                probed = 0;
                Reason = "сведения о графическом устройстве недоступны";
                return;
            }

            bool ok = device != GraphicsDeviceType.Null && shaderLevel > 0;
            probed = ok ? 1 : 0;
            Reason = ok
                ? "графика: " + device + " (уровень шейдеров " + shaderLevel + ")"
                : "графика недоступна: устройство " + device + ", уровень шейдеров " + shaderLevel +
                  " — расчёты работают, показ выключен";
            if (!ok) Debug.Log("[Graphics] " + Reason);
        }

        /// <summary>Сбросить кэш проверки (нужно, если устройство сменилось — например при старте XR).</summary>
        public static void Reset()
        {
            probed = -1;
            Reason = "";
        }
    }
}
