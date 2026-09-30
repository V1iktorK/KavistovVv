using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using KazistovVvUI;

namespace KazistovVvFeatures
{
    // ============================================================================================
    //  РАЗДЕЛ «ГРАФИКА»: МОДЕЛЬ ДАННЫХ (ЭТАПЫ 1–2 ТЗ)
    //
    //  Здесь только ДАННЫЕ и их разбор — ничего не применяется и не сохраняется.
    //  Применение живёт в `KvGraphicsService`, показ — в `KvGraphicsUi`.
    //
    //  ПОЧЕМУ ТАК:
    //   * пресет — обычный класс с полями (а не ScriptableObject): его можно сериализовать
    //     в PlayerPrefs и прочитать из JSON без ассетов, .meta и импорта;
    //   * в JSON значения записаны ЧЕЛОВЕКО-ЧИТАЕМЫМИ строками («TAA», «1/2», «Baked+Realtime»),
    //     поэтому пресеты правятся без перекомпиляции и без знания кода;
    //   * разбор строк терпимый: принимаются английские и русские варианты, числа и мусор
    //     (мусор не ломает загрузку — берётся значение по умолчанию).
    // ============================================================================================

    /// <summary>Пресет качества. Значения фиксированы числами (стабильны в PlayerPrefs).</summary>
    public enum KvGraphicsPreset
    {
        Auto = 0,
        Low = 1,
        Medium = 2,
        High = 3,
        Ultra = 4,
        VRReady = 5,
        Custom = 6
    }

    /// <summary>Качество теней (сторона карты теней в пикселях: 512 / 1024 / 2048 / 4096).</summary>
    public enum KvShadowQuality { Low = 0, Medium = 1, High = 2, Ultra = 3 }

    /// <summary>Сглаживание. MSAA задаётся в HDRP-ассете, FXAA/SMAA/TAA — на камере.</summary>
    public enum KvAntiAliasing { Off = 0, FXAA = 1, SMAA = 2, TAA = 3, MSAA2x = 4, MSAA4x = 5, MSAA8x = 6 }

    /// <summary>
    /// ФИКС 1. РЕЖИМ ОТРИСОВКИ HDRP (`supportedLitShaderMode` в HDRP-ассете).
    /// MSAA в HDRP работает ТОЛЬКО при прямой отрисовке, поэтому без этой настройки выбор
    /// «MSAA 4x» молча превращался в TAA.
    ///
    /// ВАЖНО: значения СОВПАДАЮТ с `RenderPipelineSettings.SupportedLitShaderMode` из HDRP —
    /// это ФЛАГОВОЕ перечисление (`ForwardOnly = 1`, `DeferredOnly = 2`, `Both = 3`), а не
    /// 0/1/2. Именно на этом легко ошибиться: проверка «прямая отрисовка включена» — это
    /// побитовое И с `ForwardOnly`, а НЕ сравнение с нулём.
    /// </summary>
    public enum KvLitShaderMode
    {
        /// <summary>Только прямая отрисовка (HDRP: ForwardOnly = 1). MSAA работает.</summary>
        Forward = 1,
        /// <summary>Только отложенная (HDRP: DeferredOnly = 2). MSAA НЕ работает.</summary>
        Deferred = 2,
        /// <summary>Обе (HDRP: Both = 3). MSAA работает.</summary>
        Both = 3
    }

    /// <summary>Вертикальная синхронизация (как в QualitySettings.vSyncCount).</summary>
    public enum KvVSync { Off = 0, On = 1, Half = 2 }

    /// <summary>Отражения: только запечённые или запечённые + отражения в реальном времени.</summary>
    public enum KvReflectionMode { Baked = 0, BakedRealtime = 1 }

    /// <summary>Качество рельефа (уровень детализации Terrain).</summary>
    public enum KvTerrainQuality { Low = 0, Medium = 1, High = 2 }

    /// <summary>Предел кадровой частоты (Unlimited — без ограничения).</summary>
    public enum KvFrameCap { Fps30 = 0, Fps60 = 1, Fps72 = 2, Fps90 = 3, Fps120 = 4, Fps144 = 5, Unlimited = 6 }

    /// <summary>Класс железа, к которому отнесён компьютер (обоснование автоопределения).</summary>
    public enum KvHardwareClass { Unknown = 0, Low = 1, Medium = 2, High = 3, Ultra = 4, VRReady = 5 }

    // ============================================================================================ профиль

    /// <summary>
    /// НАБОР ПАРАМЕТРОВ ГРАФИКИ (один пресет или текущие настройки оператора).
    /// Поля — плоские и сериализуемые: класс целиком кладётся в PlayerPrefs как JSON
    /// (`JsonUtility`), поэтому индивидуальные правки режима «Свой» переживают перезапуск.
    /// </summary>
    [Serializable]
    public class KvGraphicsProfile
    {
        /// <summary>Ключ пресета («Low», «VRReady», «Custom»).</summary>
        public string id = "High";
        /// <summary>Подпись для интерфейса (переводится по ключу `graphics.preset.*`).</summary>
        public string title = "";

        // --- качество изображения
        /// <summary>Масштаб отрисовки (0.5…1.5). 1.0 — «пиксель в пиксель».</summary>
        public float renderScale = 1f;
        /// <summary>Сторона карты теней, пикселей (512 / 1024 / 2048 / 4096).</summary>
        public int shadowResolution = 2048;
        /// <summary>Дистанция теней, метры.</summary>
        public float shadowDistance = 80f;
        /// <summary>Число каскадов теней (1…4).</summary>
        public int shadowCascades = 4;
        /// <summary>Качество теней (0…3) — для подписи и выбора разрешения.</summary>
        public int shadowQuality = 2;
        /// <summary>Затенение в экранном пространстве (SSAO).</summary>
        public bool ssao = true;
        /// <summary>Сила SSAO (0 — выключено, до 4).</summary>
        public float ssaoIntensity = 0.6f;
        /// <summary>Отражения в экранном пространстве (SSR).</summary>
        public bool ssr = true;
        /// <summary>Отражения: 0 — запечённые, 1 — запечённые + реального времени.</summary>
        public int reflectionMode = 1;
        /// <summary>Объёмный туман.</summary>
        public bool volumetricFog = true;
        /// <summary>Качество объёмного тумана (0 Low … 2 High) — разрешение и число слоёв.</summary>
        public int volumetricFogQuality = 1;
        /// <summary>Сглаживание (см. <see cref="KvAntiAliasing"/>).</summary>
        public int antiAliasing = 3;
        /// <summary>Свечение (Bloom).</summary>
        public bool bloom = true;
        /// <summary>Сила свечения.</summary>
        public float bloomIntensity = 0.25f;
        /// <summary>Размытие в движении.</summary>
        public bool motionBlur = false;
        /// <summary>Глубина резкости.</summary>
        public bool depthOfField = false;
        /// <summary>Трассировка лучей (только если железо поддерживает).</summary>
        public bool rayTracing = false;

        // --- текстуры и геометрия
        /// <summary>Предел мип-уровней: 0 — полные, 1 — 1/2, 2 — 1/4, 3 — 1/8.</summary>
        public int mipmapLimit = 0;
        /// <summary>Анизотропная фильтрация: 0 выкл, 1 вкл, 2 принудительно.</summary>
        public int anisotropic = 1;
        /// <summary>Смещение уровня детализации (0.3…2.0).</summary>
        public float lodBias = 1f;
        /// <summary>Качество декалей/деталей (0 Low … 3 Ultra).</summary>
        public int decalQuality = 2;
        /// <summary>Качество рельефа (0 Low … 2 High).</summary>
        public int terrainQuality = 1;

        // --- производительность
        /// <summary>Синхронизация кадров (см. <see cref="KvVSync"/>).</summary>
        public int vsync = 1;
        /// <summary>Предел кадровой частоты (см. <see cref="KvFrameCap"/>).</summary>
        public int frameCap = 1;
        /// <summary>Динамическое разрешение HDRP.</summary>
        public bool dynamicResolution = false;
        /// <summary>Адаптивное качество (снижение масштаба при просадке кадров).</summary>
        public bool adaptiveQuality = false;
        /// <summary>Целевая кадровая частота для адаптивного режима (30…144).</summary>
        public int targetFps = 60;

        /// <summary>Копия профиля (правки «Своего» режима идут по копии, пресет не портится).</summary>
        public KvGraphicsProfile Clone()
        {
            KvGraphicsProfile p = new KvGraphicsProfile();
            p.CopyFrom(this);
            return p;
        }

        /// <summary>Скопировать все поля из другого профиля.</summary>
        public void CopyFrom(KvGraphicsProfile other)
        {
            if (other == null) return;
            id = other.id;
            title = other.title;
            renderScale = other.renderScale;
            shadowResolution = other.shadowResolution;
            shadowDistance = other.shadowDistance;
            shadowCascades = other.shadowCascades;
            shadowQuality = other.shadowQuality;
            ssao = other.ssao;
            ssaoIntensity = other.ssaoIntensity;
            ssr = other.ssr;
            reflectionMode = other.reflectionMode;
            volumetricFog = other.volumetricFog;
            volumetricFogQuality = other.volumetricFogQuality;
            antiAliasing = other.antiAliasing;
            bloom = other.bloom;
            bloomIntensity = other.bloomIntensity;
            motionBlur = other.motionBlur;
            depthOfField = other.depthOfField;
            rayTracing = other.rayTracing;
            mipmapLimit = other.mipmapLimit;
            anisotropic = other.anisotropic;
            lodBias = other.lodBias;
            decalQuality = other.decalQuality;
            terrainQuality = other.terrainQuality;
            vsync = other.vsync;
            frameCap = other.frameCap;
            dynamicResolution = other.dynamicResolution;
            adaptiveQuality = other.adaptiveQuality;
            targetFps = other.targetFps;
        }

        /// <summary>Привести значения к допустимым (страховка от ручной правки JSON и старых файлов).</summary>
        public void Normalize(bool allowRayTracing)
        {
            renderScale = Mathf.Clamp(renderScale, 0.5f, 1.5f);
            if (shadowResolution <= 0) shadowResolution = KvGraphicsModel.ShadowResolutionOf(shadowQuality);
            shadowResolution = Mathf.Clamp(shadowResolution, 256, 4096);
            shadowDistance = Mathf.Clamp(shadowDistance, 5f, 300f);
            shadowCascades = Mathf.Clamp(shadowCascades, 1, 4);
            shadowQuality = Mathf.Clamp(shadowQuality, 0, 3);
            ssaoIntensity = Mathf.Clamp(ssaoIntensity, 0f, 4f);
            reflectionMode = Mathf.Clamp(reflectionMode, 0, 1);
            volumetricFogQuality = Mathf.Clamp(volumetricFogQuality, 0, 2);
            antiAliasing = Mathf.Clamp(antiAliasing, 0, 6);
            bloomIntensity = Mathf.Clamp(bloomIntensity, 0f, 1f);
            mipmapLimit = Mathf.Clamp(mipmapLimit, 0, 3);
            anisotropic = Mathf.Clamp(anisotropic, 0, 2);
            lodBias = Mathf.Clamp(lodBias, 0.3f, 2f);
            decalQuality = Mathf.Clamp(decalQuality, 0, 3);
            terrainQuality = Mathf.Clamp(terrainQuality, 0, 2);
            vsync = Mathf.Clamp(vsync, 0, 2);
            frameCap = Mathf.Clamp(frameCap, 0, 6);
            targetFps = Mathf.Clamp(targetFps, 30, 144);
            if (!allowRayTracing) rayTracing = false;
        }

        /// <summary>Отпечаток значений (для диагностики и сравнения с пресетом).</summary>
        public string Signature()
        {
            return "rs=" + renderScale.ToString("0.00", CultureInfo.InvariantCulture) +
                   ";sh=" + shadowResolution + ";sd=" + shadowDistance.ToString("0", CultureInfo.InvariantCulture) +
                   ";cc=" + shadowCascades + ";ao=" + (ssao ? 1 : 0) + ";ssr=" + (ssr ? 1 : 0) +
                   ";fog=" + (volumetricFog ? 1 : 0) + ";aa=" + antiAliasing + ";bl=" + (bloom ? 1 : 0) +
                   ";mb=" + (motionBlur ? 1 : 0) + ";dof=" + (depthOfField ? 1 : 0) +
                   ";rt=" + (rayTracing ? 1 : 0) + ";mip=" + mipmapLimit +
                   ";lod=" + lodBias.ToString("0.00", CultureInfo.InvariantCulture) +
                   ";vs=" + vsync + ";cap=" + frameCap;
        }
    }

    // ============================================================================================ железо

    /// <summary>
    /// СВЕДЕНИЯ О ЖЕЛЕЗЕ (этап 1 ТЗ). Заполняется один раз при старте и хранится строкой
    /// JSON в PlayerPrefs рядом с хешем — по нему видно, почему выбран такой пресет.
    /// </summary>
    [Serializable]
    public class KvGraphicsHardware
    {
        public string gpu = "";
        public string vendor = "";
        public string api = "";
        public string apiVersion = "";
        public int vramMb;
        public int ramMb;
        public string cpu = "";
        public int cpuCores;
        public int cpuMhz;
        public bool rayTracing;
        public bool computeShaders;
        public bool asyncReadback;
        public bool instancing;
        public int screenWidth;
        public int screenHeight;
        public int screenHz;
        public float dpi;
        public bool laptop;
        public bool batteryPresent;
        public float batteryLevel = -1f;
        public bool xrPresent;
        public string xrDevice = "";
        public string stereoMode = "";
        public bool integrated;
        public string hash = "";
        public bool detected;

        /// <summary>Короткая строка для интерфейса: «RTX 3070 · VRAM 8192 МБ · 32 ГБ ОЗУ».</summary>
        public string Short
        {
            get
            {
                return (string.IsNullOrEmpty(gpu) ? "GPU ?" : gpu) +
                       " · VRAM " + vramMb + " МБ" +
                       " · " + Mathf.RoundToInt(ramMb / 1024f) + " ГБ ОЗУ" +
                       (xrPresent ? " · XR: " + (string.IsNullOrEmpty(xrDevice) ? "да" : xrDevice) : "");
            }
        }

        /// <summary>Полная карточка железа (для отчёта и вкладки «Диагностика»).</summary>
        public string Details()
        {
            return "Видеокарта: " + gpu + " (" + vendor + ", " + api + " " + apiVersion + ")\n" +
                   "Видеопамять: " + vramMb + " МБ" + (integrated ? " · встроенная графика" : " · дискретная") + "\n" +
                   "Оперативная память: " + ramMb + " МБ\n" +
                   "Процессор: " + cpu + " · ядер " + cpuCores + " · " + cpuMhz + " МГц\n" +
                   "Поддержка: трассировка лучей " + Yes(rayTracing) + ", вычислительные шейдеры " + Yes(computeShaders) +
                   ", асинхронное чтение GPU " + Yes(asyncReadback) + ", инстансинг " + Yes(instancing) + "\n" +
                   "Экран: " + screenWidth + "×" + screenHeight + " @ " + screenHz + " Гц · DPI " +
                   dpi.ToString("0", CultureInfo.InvariantCulture) + "\n" +
                   "Устройство: " + (laptop ? "ноутбук" : "настольный") +
                   (batteryPresent ? " (батарея " + Mathf.RoundToInt(batteryLevel * 100f) + " %)" : " (батареи нет)") + "\n" +
                   "XR: " + (xrPresent ? ("подключён " + (string.IsNullOrEmpty(xrDevice) ? "шлем" : xrDevice) +
                       (string.IsNullOrEmpty(stereoMode) ? "" : ", стерео " + stereoMode)) : "не обнаружен") + "\n" +
                   "Хеш конфигурации: " + hash;
        }

        private static string Yes(bool value) { return value ? "да" : "нет"; }
    }

    // ============================================================================================ JSON

    /// <summary>Строка JSON-файла пресетов (`StreamingAssets/kazistovvv_graphics.json`).</summary>
    /// <remarks>
    /// ЧИСЛОВЫЕ ПОЛЯ ПО УМОЛЧАНИЮ — НОЛЬ, И ЭТО ВАЖНО. Ноль означает «в файле не задано»,
    /// и тогда берётся значение из встроенной таблицы (этап 2). Если бы здесь стояли
    /// «правдоподобные» значения (1,0 / 80 / 4), отсутствующее поле молча подменяло бы
    /// пресет чужими числами — именно эту ошибку поймал тест
    /// `KvGraphicsTests.FromDto_EmptyDto_LeavesBuiltinIntact`.
    /// </remarks>
    [Serializable]
    public class KvGraphicsPresetDto
    {
        public string id = "";
        public string title = "";
        public float renderScale = 0f;         // 0 — не задано
        public string shadows = "";            // «Low» / «512» — качество и разрешение карты теней
        public float shadowDistance = 0f;      // 0 — не задано
        public int shadowCascades = 0;         // 0 — не задано
        public string ssao = "";               // «off» / «low» / «medium» / «high»
        public string ssr = "";                // «off» / «on»
        public string reflectionProbes = "";   // «Baked» / «Baked+Realtime»
        public string volumetricFog = "";      // «off» / «low» / «medium» / «high»
        public string antiAliasing = "";       // «Off» / «FXAA» / «SMAA» / «TAA» / «MSAA 4x»
        public string bloom = "";              // «off» / «on»
        public string motionBlur = "";         // «off» / «on»
        public string depthOfField = "";       // «off» / «on»
        public string rayTracing = "";         // «off» / «on» / «auto»
        public string textures = "";           // «Full» / «1/2» / «1/4» / «1/8»
        public float lodBias = 0f;             // 0 — не задано
        public string anisotropic = "";        // «off» / «on» / «force»
        public string decals = "";             // «Low» … «Ultra»
        public string terrain = "";            // «Low» / «Medium» / «High»
        public string vsync = "";              // «Off» / «On» / «Every Second V-Blank»
        public string frameCap = "";           // «30» … «144» / «Unlimited»
        public string dynamicResolution = "";  // «off» / «on»
        public string adaptiveQuality = "";    // «off» / «on»
        public int targetFps = 0;              // 0 — не задано
    }

    /// <summary>Корень файла пресетов.</summary>
    [Serializable]
    public class KvGraphicsFile
    {
        public int version = 1;
        public string note = "";
        /// <summary>Применять ли настройки самого HDRP-ассета (можно выключить для осторожного режима).</summary>
        public bool applyAssetSettings = true;
        public KvGraphicsPresetDto[] presets = new KvGraphicsPresetDto[0];
    }

    // ============================================================================================ модель

    /// <summary>
    /// СТАТИЧЕСКИЕ ЗНАНИЯ О ГРАФИКЕ: встроенная таблица пресетов ТЗ, преобразование строк
    /// в значения, подписи для интерфейса. Ничего не применяет — только считает.
    /// </summary>
    public static class KvGraphicsModel
    {
        /// <summary>Порядок пресетов на экране (сегментированный переключатель).</summary>
        public static readonly KvGraphicsPreset[] Order =
        {
            KvGraphicsPreset.Auto, KvGraphicsPreset.Low, KvGraphicsPreset.Medium,
            KvGraphicsPreset.High, KvGraphicsPreset.Ultra, KvGraphicsPreset.VRReady,
            KvGraphicsPreset.Custom
        };

        /// <summary>Разрешение карты теней по качеству.</summary>
        public static int ShadowResolutionOf(int quality)
        {
            switch (Mathf.Clamp(quality, 0, 3))
            {
                case 0: return 512;
                case 1: return 1024;
                case 2: return 2048;
                default: return 4096;
            }
        }

        /// <summary>Качество по разрешению карты теней.</summary>
        public static int ShadowQualityOf(int resolution)
        {
            if (resolution <= 512) return 0;
            if (resolution <= 1024) return 1;
            if (resolution <= 2048) return 2;
            return 3;
        }

        /// <summary>Дистанция прорисовки декалей по качеству.</summary>
        public static int DecalDistanceOf(int quality)
        {
            switch (Mathf.Clamp(quality, 0, 3))
            {
                case 0: return 10;
                case 1: return 25;
                case 2: return 50;
                default: return 100;
            }
        }

        /// <summary>Кадровый предел в кадрах в секунду (−1 — без предела).</summary>
        public static int FrameCapValue(int cap)
        {
            switch (Mathf.Clamp(cap, 0, 6))
            {
                case 0: return 30;
                case 1: return 60;
                case 2: return 72;
                case 3: return 90;
                case 4: return 120;
                case 5: return 144;
                default: return -1;
            }
        }

        /// <summary>Индекс кадрового предела по числу кадров.</summary>
        public static int FrameCapIndex(int fps)
        {
            switch (fps)
            {
                case 30: return 0;
                case 60: return 1;
                case 72: return 2;
                case 90: return 3;
                case 120: return 4;
                case 144: return 5;
                default: return 6;
            }
        }

        /// <summary>Масштаб текстур (множитель мип-уровня) по индексу 0…3.</summary>
        public static string TextureLabel(int mipmapLimit)
        {
            switch (Mathf.Clamp(mipmapLimit, 0, 3))
            {
                case 1: return "1/2";
                case 2: return "1/4";
                case 3: return "1/8";
                default: return "Full";
            }
        }

        /// <summary>Ключ пресета (латиницей) по enum — им подписан JSON и PlayerPrefs.</summary>
        public static string KeyOf(KvGraphicsPreset preset)
        {
            switch (preset)
            {
                case KvGraphicsPreset.Low: return "Low";
                case KvGraphicsPreset.Medium: return "Medium";
                case KvGraphicsPreset.High: return "High";
                case KvGraphicsPreset.Ultra: return "Ultra";
                case KvGraphicsPreset.VRReady: return "VRReady";
                case KvGraphicsPreset.Custom: return "Custom";
                default: return "Auto";
            }
        }

        /// <summary>Пресет по ключу (терпимо к «VR-Ready», «vr_ready», «Авто»…).</summary>
        public static KvGraphicsPreset PresetOf(string key)
        {
            if (string.IsNullOrEmpty(key)) return KvGraphicsPreset.Auto;
            string s = key.Trim().ToLowerInvariant().Replace("-", "").Replace("_", "").Replace(" ", "");
            switch (s)
            {
                case "low": case "низкое": case "низкий": return KvGraphicsPreset.Low;
                case "medium": case "среднее": case "средний": return KvGraphicsPreset.Medium;
                case "high": case "высокое": case "высокий": return KvGraphicsPreset.High;
                case "ultra": case "ультра": return KvGraphicsPreset.Ultra;
                case "vrready": case "vr": case "вrготово": return KvGraphicsPreset.VRReady;
                case "custom": case "свой": case "своё": case "свое": return KvGraphicsPreset.Custom;
                default: return KvGraphicsPreset.Auto;
            }
        }

        /// <summary>Переводимая подпись пресета.</summary>
        public static string Label(KvGraphicsPreset preset)
        {
            switch (preset)
            {
                case KvGraphicsPreset.Low: return KvLocExtra4.T("graphics.preset.low", "Низкое");
                case KvGraphicsPreset.Medium: return KvLocExtra4.T("graphics.preset.medium", "Среднее");
                case KvGraphicsPreset.High: return KvLocExtra4.T("graphics.preset.high", "Высокое");
                case KvGraphicsPreset.Ultra: return KvLocExtra4.T("graphics.preset.ultra", "Ультра");
                case KvGraphicsPreset.VRReady: return KvLocExtra4.T("graphics.preset.vrready", "VR-Ready");
                case KvGraphicsPreset.Custom: return KvLocExtra4.T("graphics.preset.custom", "Свой");
                default: return KvLocExtra4.T("graphics.preset.auto", "Авто");
            }
        }

        /// <summary>Переводимая подпись качества теней.</summary>
        public static string ShadowLabel(int quality)
        {
            switch (Mathf.Clamp(quality, 0, 3))
            {
                case 0: return KvLocExtra4.T("graphics.shadow.low", "Низкое");
                case 1: return KvLocExtra4.T("graphics.shadow.medium", "Среднее");
                case 2: return KvLocExtra4.T("graphics.shadow.high", "Высокое");
                default: return KvLocExtra4.T("graphics.shadow.ultra", "Ультра");
            }
        }

        /// <summary>Переводимая подпись сглаживания.</summary>
        public static string AntiAliasingLabel(int aa)
        {
            switch (Mathf.Clamp(aa, 0, 6))
            {
                case 1: return "FXAA";
                case 2: return "SMAA";
                case 3: return "TAA";
                case 4: return "MSAA 2x";
                case 5: return "MSAA 4x";
                case 6: return "MSAA 8x";
                default: return KvLocExtra4.T("graphics.aa.off", "Выкл");
            }
        }

        /// <summary>
        /// ФИКС 1. Порядок режимов отрисовки для интерфейса (значения перечисления — флаговые,
        /// поэтому индекс переключателя и значение НЕ совпадают: см. LitShaderModeAt/Index).
        /// </summary>
        public static readonly KvLitShaderMode[] LitShaderModeOrder =
        {
            KvLitShaderMode.Deferred, KvLitShaderMode.Forward, KvLitShaderMode.Both
        };

        /// <summary>Режим по индексу переключателя (0 — отложенная, 1 — прямая, 2 — обе).</summary>
        public static KvLitShaderMode LitShaderModeAt(int index)
        {
            return LitShaderModeOrder[Mathf.Clamp(index, 0, LitShaderModeOrder.Length - 1)];
        }

        /// <summary>Индекс режима в переключателе (0 — отложенная, 1 — прямая, 2 — обе).</summary>
        public static int LitShaderModeIndex(KvLitShaderMode mode)
        {
            for (int i = 0; i < LitShaderModeOrder.Length; i++)
                if (LitShaderModeOrder[i] == mode) return i;
            return 0;
        }

        /// <summary>
        /// ФИКС 1. Переводимая подпись режима отрисовки HDRP.
        /// </summary>
        public static string LitShaderModeLabel(int mode)
        {
            switch (mode)
            {
                case (int)KvLitShaderMode.Forward:
                    return KvLocExtra4.T("graphics.raster.forward", "Прямая (Forward)");
                case (int)KvLitShaderMode.Both:
                    return KvLocExtra4.T("graphics.raster.both", "Обе (Forward + Deferred)");
                default:
                    return KvLocExtra4.T("graphics.raster.deferred", "Отложенная (Deferred)");
            }
        }

        /// <summary>Переводимая подпись синхронизации кадров.</summary>
        public static string VSyncLabel(int vsync)
        {
            switch (Mathf.Clamp(vsync, 0, 2))
            {
                case 1: return KvLocExtra4.T("graphics.vsync.on", "Включена");
                case 2: return KvLocExtra4.T("graphics.vsync.half", "Каждый второй кадр");
                default: return KvLocExtra4.T("graphics.vsync.off", "Выключена");
            }
        }

        /// <summary>Подпись кадрового предела.</summary>
        public static string FrameCapLabel(int cap)
        {
            int value = FrameCapValue(cap);
            return value < 0 ? KvLocExtra4.T("graphics.fps.unlimited", "Без предела") : value.ToString();
        }

        /// <summary>Подпись качества (общая: тени, декали, туман, рельеф).</summary>
        public static string QualityLabel(int quality)
        {
            switch (Mathf.Clamp(quality, 0, 3))
            {
                case 0: return KvLocExtra4.T("graphics.q.low", "Низкое");
                case 1: return KvLocExtra4.T("graphics.q.medium", "Среднее");
                case 2: return KvLocExtra4.T("graphics.q.high", "Высокое");
                default: return KvLocExtra4.T("graphics.q.ultra", "Ультра");
            }
        }

        // ------------------------------------------------------------------ встроенная таблица ТЗ

        /// <summary>
        /// ВСТРОЕННЫЕ ПРЕСЕТЫ (таблица ТЗ, этап 2). Используются, если файл
        /// `kazistovvv_graphics.json` отсутствует или испорчен: сервис обязан работать
        /// и без него, а внешний файл лишь уточняет значения.
        /// </summary>
        public static KvGraphicsProfile Builtin(KvGraphicsPreset preset)
        {
            KvGraphicsProfile p = new KvGraphicsProfile();
            p.id = KeyOf(preset);
            switch (preset)
            {
                case KvGraphicsPreset.Low:
                    p.renderScale = 0.7f; p.shadowQuality = 0; p.shadowResolution = 512;
                    p.shadowDistance = 30f; p.shadowCascades = 1;
                    p.ssao = false; p.ssaoIntensity = 0f; p.ssr = false; p.reflectionMode = 0;
                    p.volumetricFog = false; p.volumetricFogQuality = 0;
                    p.antiAliasing = (int)KvAntiAliasing.FXAA;
                    p.bloom = false; p.bloomIntensity = 0f; p.motionBlur = false; p.depthOfField = false;
                    p.rayTracing = false;
                    p.mipmapLimit = 2; p.anisotropic = 0; p.lodBias = 0.5f;
                    p.decalQuality = 0; p.terrainQuality = 0;
                    p.vsync = (int)KvVSync.On; p.frameCap = (int)KvFrameCap.Fps60;
                    p.dynamicResolution = false; p.adaptiveQuality = false; p.targetFps = 60;
                    break;
                case KvGraphicsPreset.Medium:
                    p.renderScale = 0.85f; p.shadowQuality = 1; p.shadowResolution = 1024;
                    p.shadowDistance = 50f; p.shadowCascades = 2;
                    p.ssao = true; p.ssaoIntensity = 0.4f; p.ssr = false; p.reflectionMode = 0;
                    p.volumetricFog = true; p.volumetricFogQuality = 0;
                    p.antiAliasing = (int)KvAntiAliasing.FXAA;
                    p.bloom = true; p.bloomIntensity = 0.2f; p.motionBlur = false; p.depthOfField = false;
                    p.rayTracing = false;
                    p.mipmapLimit = 1; p.anisotropic = 1; p.lodBias = 0.7f;
                    p.decalQuality = 1; p.terrainQuality = 1;
                    p.vsync = (int)KvVSync.On; p.frameCap = (int)KvFrameCap.Fps60;
                    p.dynamicResolution = false; p.adaptiveQuality = false; p.targetFps = 60;
                    break;
                case KvGraphicsPreset.Ultra:
                    p.renderScale = 1f; p.shadowQuality = 3; p.shadowResolution = 4096;
                    p.shadowDistance = 120f; p.shadowCascades = 4;
                    p.ssao = true; p.ssaoIntensity = 0.8f; p.ssr = true; p.reflectionMode = 1;
                    p.volumetricFog = true; p.volumetricFogQuality = 2;
                    p.antiAliasing = (int)KvAntiAliasing.TAA;
                    p.bloom = true; p.bloomIntensity = 0.3f; p.motionBlur = true; p.depthOfField = true;
                    p.rayTracing = true;
                    p.mipmapLimit = 0; p.anisotropic = 2; p.lodBias = 1.2f;
                    p.decalQuality = 3; p.terrainQuality = 2;
                    p.vsync = (int)KvVSync.Off; p.frameCap = (int)KvFrameCap.Unlimited;
                    p.dynamicResolution = false; p.adaptiveQuality = false; p.targetFps = 144;
                    break;
                case KvGraphicsPreset.VRReady:
                    p.renderScale = 0.9f; p.shadowQuality = 1; p.shadowResolution = 1024;
                    p.shadowDistance = 40f; p.shadowCascades = 2;
                    p.ssao = true; p.ssaoIntensity = 0.5f; p.ssr = false; p.reflectionMode = 0;
                    p.volumetricFog = false; p.volumetricFogQuality = 0;
                    p.antiAliasing = (int)KvAntiAliasing.TAA;
                    p.bloom = true; p.bloomIntensity = 0.2f; p.motionBlur = false; p.depthOfField = false;
                    p.rayTracing = false;
                    p.mipmapLimit = 1; p.anisotropic = 1; p.lodBias = 0.7f;
                    p.decalQuality = 1; p.terrainQuality = 0;
                    p.vsync = (int)KvVSync.On; p.frameCap = (int)KvFrameCap.Fps90;
                    p.dynamicResolution = true; p.adaptiveQuality = true; p.targetFps = 90;
                    break;
                default: // High — рабочий пресет проекта
                    p.renderScale = 1f; p.shadowQuality = 2; p.shadowResolution = 2048;
                    p.shadowDistance = 80f; p.shadowCascades = 4;
                    p.ssao = true; p.ssaoIntensity = 0.6f; p.ssr = true; p.reflectionMode = 1;
                    p.volumetricFog = true; p.volumetricFogQuality = 1;
                    p.antiAliasing = (int)KvAntiAliasing.TAA;
                    p.bloom = true; p.bloomIntensity = 0.25f; p.motionBlur = true; p.depthOfField = true;
                    p.rayTracing = false;
                    p.mipmapLimit = 0; p.anisotropic = 1; p.lodBias = 1f;
                    p.decalQuality = 2; p.terrainQuality = 2;
                    p.vsync = (int)KvVSync.On; p.frameCap = (int)KvFrameCap.Fps120;
                    p.dynamicResolution = false; p.adaptiveQuality = false; p.targetFps = 60;
                    break;
            }
            p.title = Label(preset);
            return p;
        }

        /// <summary>Все встроенные пресеты (Low…VRReady).</summary>
        public static List<KvGraphicsProfile> BuiltinAll()
        {
            List<KvGraphicsProfile> list = new List<KvGraphicsProfile>();
            list.Add(Builtin(KvGraphicsPreset.Low));
            list.Add(Builtin(KvGraphicsPreset.Medium));
            list.Add(Builtin(KvGraphicsPreset.High));
            list.Add(Builtin(KvGraphicsPreset.Ultra));
            list.Add(Builtin(KvGraphicsPreset.VRReady));
            return list;
        }

        // ------------------------------------------------------------------ разбор строк JSON

        /// <summary>Пресет из строки JSON-файла (все поля — терпимо, мусор игнорируется).</summary>
        public static KvGraphicsProfile FromDto(KvGraphicsPresetDto dto, KvGraphicsProfile fallback)
        {
            KvGraphicsProfile p = fallback != null ? fallback.Clone() : new KvGraphicsProfile();
            if (dto == null) return p;
            if (!string.IsNullOrEmpty(dto.id)) p.id = dto.id.Trim();
            if (!string.IsNullOrEmpty(dto.title)) p.title = dto.title;

            if (dto.renderScale > 0f) p.renderScale = dto.renderScale;
            if (dto.shadowDistance > 0f) p.shadowDistance = dto.shadowDistance;
            if (dto.shadowCascades > 0) p.shadowCascades = dto.shadowCascades;
            if (dto.targetFps > 0) p.targetFps = dto.targetFps;
            if (!Mathf.Approximately(dto.lodBias, 0f)) p.lodBias = dto.lodBias;

            int shadow;
            if (TryShadow(dto.shadows, out shadow))
            {
                p.shadowQuality = ShadowQualityOf(shadow);
                p.shadowResolution = shadow;
            }

            float level;
            if (TryLevel(dto.ssao, out level))
            {
                p.ssao = level > 0f;
                p.ssaoIntensity = level > 0f ? Mathf.Clamp(level, 0.1f, 1f) * 1.2f : 0f;
            }
            if (TryLevel(dto.volumetricFog, out level))
            {
                p.volumetricFog = level > 0f;
                p.volumetricFogQuality = level <= 0.34f ? 0 : (level <= 0.67f ? 1 : 2);
            }

            bool flag;
            if (TryFlag(dto.ssr, out flag)) p.ssr = flag;
            if (TryFlag(dto.bloom, out flag)) p.bloom = flag;
            if (TryFlag(dto.motionBlur, out flag)) p.motionBlur = flag;
            if (TryFlag(dto.depthOfField, out flag)) p.depthOfField = flag;
            if (TryFlag(dto.rayTracing, out flag)) p.rayTracing = flag;
            if (TryFlag(dto.dynamicResolution, out flag)) p.dynamicResolution = flag;
            if (TryFlag(dto.adaptiveQuality, out flag)) p.adaptiveQuality = flag;

            if (!string.IsNullOrEmpty(dto.reflectionProbes))
            {
                string s = dto.reflectionProbes.ToLowerInvariant();
                p.reflectionMode = (s.Contains("realtime") || s.Contains("реальн") || s.Contains("+")) ? 1 : 0;
            }
            if (!string.IsNullOrEmpty(dto.antiAliasing))
                p.antiAliasing = (int)ParseAntiAliasing(dto.antiAliasing, (KvAntiAliasing)p.antiAliasing);
            if (!string.IsNullOrEmpty(dto.textures)) p.mipmapLimit = ParseMipmap(dto.textures, p.mipmapLimit);
            if (!string.IsNullOrEmpty(dto.anisotropic)) p.anisotropic = ParseAnisotropic(dto.anisotropic, p.anisotropic);
            if (!string.IsNullOrEmpty(dto.decals)) p.decalQuality = ParseQuality(dto.decals, p.decalQuality);
            if (!string.IsNullOrEmpty(dto.terrain)) p.terrainQuality = ParseQuality(dto.terrain, p.terrainQuality);
            if (!string.IsNullOrEmpty(dto.vsync)) p.vsync = ParseVSync(dto.vsync, p.vsync);
            if (!string.IsNullOrEmpty(dto.frameCap))
            {
                int fps = ParseInt(dto.frameCap, FrameCapValue(p.frameCap));
                p.frameCap = FrameCapIndex(fps);
            }
            return p;
        }

        /// <summary>Сглаживание по строке («TAA», «MSAA 4x», «выкл»).</summary>
        public static KvAntiAliasing ParseAntiAliasing(string text, KvAntiAliasing fallback)
        {
            if (string.IsNullOrEmpty(text)) return fallback;
            string s = text.Trim().ToLowerInvariant();
            if (s.Contains("msaa") || s.Contains("мсаа"))
            {
                int x = ParseInt(s, 0);
                if (x >= 8) return KvAntiAliasing.MSAA8x;
                if (x >= 4) return KvAntiAliasing.MSAA4x;
                if (x >= 2) return KvAntiAliasing.MSAA2x;
                return KvAntiAliasing.MSAA4x;
            }
            if (s.Contains("smaa")) return KvAntiAliasing.SMAA;
            if (s.Contains("taa") || s.Contains("тсаа")) return KvAntiAliasing.TAA;
            if (s.Contains("fxaa") || s.Contains("фхаа")) return KvAntiAliasing.FXAA;
            if (s.Contains("off") || s.Contains("none") || s.Contains("выкл") || s.Contains("нет"))
                return KvAntiAliasing.Off;
            return fallback;
        }

        /// <summary>Синхронизация кадров по строке.</summary>
        public static int ParseVSync(string text, int fallback)
        {
            if (string.IsNullOrEmpty(text)) return fallback;
            string s = text.Trim().ToLowerInvariant();
            if (s.Contains("every second") || s.Contains("half") || s.Contains("второй")) return 2;
            if (s.Contains("off") || s.Contains("выкл") || s.Contains("нет") || s == "0") return 0;
            if (s.Contains("on") || s.Contains("вкл") || s.Contains("да") || s == "1") return 1;
            return fallback;
        }

        /// <summary>Предел текстур по строке: «Full» → 0, «1/2» → 1, «1/4» → 2, «1/8» → 3.</summary>
        public static int ParseMipmap(string text, int fallback)
        {
            if (string.IsNullOrEmpty(text)) return fallback;
            string s = text.Trim().ToLowerInvariant();
            if (s.Contains("full") || s.Contains("полн")) return 0;
            if (s.Contains("1/8") || s.Contains("⅛")) return 3;
            if (s.Contains("1/4") || s.Contains("¼")) return 2;
            if (s.Contains("1/2") || s.Contains("½") || s.Contains("half")) return 1;
            int n = ParseInt(s, -1);
            if (n >= 0) return Mathf.Clamp(n, 0, 3);
            return fallback;
        }

        /// <summary>Анизотропная фильтрация по строке.</summary>
        public static int ParseAnisotropic(string text, int fallback)
        {
            if (string.IsNullOrEmpty(text)) return fallback;
            string s = text.Trim().ToLowerInvariant();
            if (s.Contains("force") || s.Contains("принуд")) return 2;
            if (s.Contains("off") || s.Contains("выкл") || s.Contains("нет") || s == "0") return 0;
            if (s.Contains("on") || s.Contains("вкл") || s.Contains("да") || s == "1") return 1;
            return fallback;
        }

        /// <summary>Качество по названию («Low»/«Низкое»/«0»…).</summary>
        public static int ParseQuality(string text, int fallback)
        {
            if (string.IsNullOrEmpty(text)) return fallback;
            string s = text.Trim().ToLowerInvariant();
            if (s.Contains("ultra") || s.Contains("ультра") || s.Contains("макс")) return 3;
            if (s.Contains("high") || s.Contains("высок")) return 2;
            if (s.Contains("medium") || s.Contains("средн")) return 1;
            if (s.Contains("low") || s.Contains("низк")) return 0;
            int n = ParseInt(s, -1);
            return n >= 0 ? Mathf.Clamp(n, 0, 3) : fallback;
        }

        /// <summary>Качество теней: название или число пикселей.</summary>
        public static bool TryShadow(string text, out int resolution)
        {
            resolution = 0;
            if (string.IsNullOrEmpty(text)) return false;
            string s = text.Trim().ToLowerInvariant();
            int n = ParseInt(s, -1);
            if (n > 0) { resolution = Mathf.Clamp(n, 256, 4096); return true; }
            if (s.Contains("ultra") || s.Contains("ультра")) { resolution = 4096; return true; }
            if (s.Contains("high") || s.Contains("высок")) { resolution = 2048; return true; }
            if (s.Contains("medium") || s.Contains("средн")) { resolution = 1024; return true; }
            if (s.Contains("low") || s.Contains("низк")) { resolution = 512; return true; }
            return false;
        }

        /// <summary>Уровень 0…1 из строки («off», «low», «medium», «high»).</summary>
        public static bool TryLevel(string text, out float level)
        {
            level = 0f;
            if (string.IsNullOrEmpty(text)) return false;
            string s = text.Trim().ToLowerInvariant();
            if (s.Contains("off") || s.Contains("выкл") || s.Contains("нет") || s == "0") { level = 0f; return true; }
            if (s.Contains("ultra") || s.Contains("ультра")) { level = 1f; return true; }
            if (s.Contains("high") || s.Contains("высок")) { level = 0.85f; return true; }
            if (s.Contains("medium") || s.Contains("средн")) { level = 0.55f; return true; }
            if (s.Contains("low") || s.Contains("низк")) { level = 0.3f; return true; }
            float f;
            if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out f))
            {
                level = Mathf.Clamp01(f);
                return true;
            }
            return false;
        }

        /// <summary>Логическое значение из строки («on/off», «да/нет», «auto»).</summary>
        public static bool TryFlag(string text, out bool value)
        {
            value = false;
            if (string.IsNullOrEmpty(text)) return false;
            string s = text.Trim().ToLowerInvariant();
            if (s.Contains("auto") || s.Contains("авто")) { value = true; return true; }
            if (s.Contains("off") || s.Contains("none") || s.Contains("выкл") || s.Contains("нет") || s == "0")
            {
                value = false;
                return true;
            }
            if (s.Contains("on") || s.Contains("вкл") || s.Contains("да") || s == "1")
            {
                value = true;
                return true;
            }
            return false;
        }

        /// <summary>Целое число из строки (первое встреченное).</summary>
        public static int ParseInt(string text, int fallback)
        {
            if (string.IsNullOrEmpty(text)) return fallback;
            string digits = "";
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c >= '0' && c <= '9') digits += c;
                else if (digits.Length > 0) break;
            }
            if (digits.Length == 0) return fallback;
            int value;
            return int.TryParse(digits, out value) ? value : fallback;
        }
    }
}
