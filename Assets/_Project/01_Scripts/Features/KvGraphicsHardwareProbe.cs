using System;
using System.Text;
using UnityEngine;
using UnityEngine.XR;
using KazistovVvUI;

namespace KazistovVvFeatures
{
    /// <summary>
    /// АВТООПРЕДЕЛЕНИЕ ЖЕЛЕЗА (ЭТАП 1 ТЗ).
    ///
    /// Всё берётся ШТАТНЫМИ средствами Unity (`SystemInfo`, `Screen`, `XRSettings`) —
    /// никаких внешних библиотек, реестра и WMI: в пакетном режиме (`-nographics`)
    /// и на разных платформах такие запросы либо недоступны, либо падают.
    ///
    /// Каждый запрос обёрнут в try/catch: если сведения недоступны (частая ситуация при
    /// пакетном прогоне), поле остаётся пустым, а классификация честно скажет, почему
    /// выбран именно этот пресет. Исключение из этого правила одно — уровень батареи:
    /// он и есть признак ноутбука, и его отсутствие трактуется как «настольный».
    ///
    /// Хеш конфигурации — MD5 от «видеокарта + видеопамять + процессор + ОЗУ + API».
    /// Он сохраняется в PlayerPrefs: если хеш изменился (сменили карту, добавили память,
    /// переустановили драйвер с другим API), автоопределение пересчитает пресет.
    /// </summary>
    public static class KvGraphicsHardwareProbe
    {
        /// <summary>Снять сведения о железе (безопасно вызывать в любом режиме).</summary>
        public static KvGraphicsHardware Probe()
        {
            KvGraphicsHardware hw = new KvGraphicsHardware();

            // --- видеокарта
            hw.gpu = SafeString(delegate { return SystemInfo.graphicsDeviceName; }, "");
            hw.vendor = SafeString(delegate { return SystemInfo.graphicsDeviceVendor; }, "");
            hw.api = SafeString(delegate { return SystemInfo.graphicsDeviceType.ToString(); }, "");
            hw.apiVersion = SafeString(delegate { return SystemInfo.graphicsDeviceVersion; }, "");
            hw.vramMb = SafeInt(delegate { return SystemInfo.graphicsMemorySize; }, 0);

            // --- память и процессор
            hw.ramMb = SafeInt(delegate { return SystemInfo.systemMemorySize; }, 0);
            hw.cpu = SafeString(delegate { return SystemInfo.processorType; }, "");
            hw.cpuCores = SafeInt(delegate { return SystemInfo.processorCount; }, 0);
            hw.cpuMhz = SafeInt(delegate { return SystemInfo.processorFrequency; }, 0);

            // --- поддержка функций
            hw.rayTracing = SafeBool(delegate { return SystemInfo.supportsRayTracing; });
            hw.computeShaders = SafeBool(delegate { return SystemInfo.supportsComputeShaders; });
            hw.asyncReadback = SafeBool(delegate { return SystemInfo.supportsAsyncGPUReadback; });
            hw.instancing = SafeBool(delegate { return SystemInfo.supportsInstancing; });

            // --- экран
            hw.screenWidth = SafeInt(delegate { return Screen.width; }, 0);
            hw.screenHeight = SafeInt(delegate { return Screen.height; }, 0);
            // Свойство Resolution.refreshRate устарело — частота берётся отношением
            // (RefreshRate), как требует Unity 6.
            hw.screenHz = SafeInt(delegate
            {
                RefreshRate ratio = Screen.currentResolution.refreshRateRatio;
                return Mathf.RoundToInt((float)ratio.value);
            }, 0);
            hw.dpi = SafeFloat(delegate { return Screen.dpi; }, 0f);
            if (hw.screenWidth <= 0 || hw.screenHeight <= 0)
            {
                hw.screenWidth = SafeInt(delegate { return Screen.currentResolution.width; }, hw.screenWidth);
                hw.screenHeight = SafeInt(delegate { return Screen.currentResolution.height; }, hw.screenHeight);
            }

            // --- батарея (признак ноутбука)
            hw.batteryLevel = SafeFloat(delegate { return SystemInfo.batteryLevel; }, -1f);
            hw.batteryPresent = hw.batteryLevel >= 0f;
            hw.laptop = hw.batteryPresent && hw.batteryLevel < 1.01f;

            // --- XR (VR-шлем)
            hw.xrPresent = SafeBool(delegate { return XRSettings.isDeviceActive; });
            hw.xrDevice = SafeString(delegate { return XRSettings.loadedDeviceName; }, "");
            hw.stereoMode = SafeString(delegate { return XRSettings.stereoRenderingMode.ToString(); }, "");
            if (!hw.xrPresent && !string.IsNullOrEmpty(hw.xrDevice) && hw.xrDevice != "None")
                hw.xrPresent = true;

            // --- встроенная ли графика
            hw.integrated = LooksIntegrated(hw.gpu, hw.vendor);
            if (hw.integrated) hw.rayTracing = false;          // трассировка на встроенной не считается
            if (!hw.computeShaders) hw.rayTracing = false;

            hw.hash = Hash(hw);
            hw.detected = true;
            return hw;
        }

        /// <summary>
        /// КЛАССИФИКАЦИЯ (этап 1 ТЗ, уточнена по факту доступных метрик).
        /// Возвращает класс железа и КРАТКОЕ ОБОСНОВАНИЕ — оно показывается оператору
        /// («VRAM 4 ГБ → высокое») и попадает в журнал.
        /// </summary>
        public static KvHardwareClass Classify(KvGraphicsHardware hw, out string reason)
        {
            reason = "";
            if (hw == null || !hw.detected)
            {
                reason = KvLocExtra4.T("graphics.detect.nodata", "сведения о железе недоступны");
                return KvHardwareClass.Unknown;
            }

            int vramGb = Mathf.Max(0, Mathf.RoundToInt(hw.vramMb / 1024f));
            int ramGb = Mathf.Max(0, Mathf.RoundToInt(hw.ramMb / 1024f));

            // 1. VR-шлем — приоритет над всеми классами (ТЗ: «VR-Ready приоритет над Ultra»).
            if (hw.xrPresent)
            {
                reason = KvLocExtra4.F("graphics.reason.xr",
                    "обнаружен VR-шлем {0} — пресет VR-Ready", 
                    string.IsNullOrEmpty(hw.xrDevice) || hw.xrDevice == "None" ? "OpenXR" : hw.xrDevice);
                return KvHardwareClass.VRReady;
            }

            // 2. Без вычислительных шейдеров HDRP работает плохо — сразу низкий.
            if (!hw.computeShaders)
            {
                reason = KvLocExtra4.T("graphics.reason.nocompute",
                    "нет поддержки вычислительных шейдеров — выбран низкий пресет");
                return KvHardwareClass.Low;
            }

            // 3. Встроенная графика — низкий пресет (ТЗ).
            if (hw.integrated)
            {
                reason = KvLocExtra4.F("graphics.reason.integrated",
                    "встроенная графика {0} — выбран низкий пресет", hw.gpu);
                return KvHardwareClass.Low;
            }

            KvHardwareClass cls;
            if (hw.vramMb >= 8192)
            {
                cls = KvHardwareClass.Ultra;
                reason = hw.rayTracing
                    ? KvLocExtra4.F("graphics.reason.ultra.rt",
                        "VRAM {0} ГБ и поддержка трассировки лучей — пресет «Ультра»", vramGb)
                    : KvLocExtra4.F("graphics.reason.ultra",
                        "VRAM {0} ГБ — пресет «Ультра»", vramGb);
            }
            else if (hw.vramMb >= 4096)
            {
                cls = KvHardwareClass.High;
                reason = KvLocExtra4.F("graphics.reason.high",
                    "VRAM {0} ГБ, дискретная видеокарта среднего уровня — пресет «Высокое»", vramGb);
            }
            else if (hw.vramMb >= 2048)
            {
                cls = KvHardwareClass.Medium;
                reason = KvLocExtra4.F("graphics.reason.medium",
                    "VRAM {0} ГБ, видеокарта начального уровня — пресет «Среднее»", vramGb);
            }
            else
            {
                cls = KvHardwareClass.Low;
                reason = KvLocExtra4.F("graphics.reason.vram.low",
                    "VRAM {0} ГБ (меньше 2 ГБ) — выбран низкий пресет", vramGb);
            }

            // 4. Ограничения по памяти и процессору: понижают класс, но не повышают.
            if (hw.ramMb > 0 && hw.ramMb < 4096 && cls > KvHardwareClass.Low)
            {
                cls = KvHardwareClass.Low;
                reason += " · " + KvLocExtra4.F("graphics.reason.ram.low",
                    "оперативной памяти {0} ГБ (меньше 4) — пресет понижен до низкого", ramGb);
            }
            else if (hw.ramMb > 0 && hw.ramMb < 8192 && cls > KvHardwareClass.Medium)
            {
                cls = KvHardwareClass.Medium;
                reason += " · " + KvLocExtra4.F("graphics.reason.ram.mid",
                    "оперативной памяти {0} ГБ (меньше 8) — пресет понижен до среднего", ramGb);
            }

            if (hw.cpuCores > 0 && hw.cpuCores <= 2 && cls > KvHardwareClass.Medium)
            {
                cls = KvHardwareClass.Medium;
                reason += " · " + KvLocExtra4.T("graphics.reason.cpu",
                    "два ядра процессора — пресет не выше среднего");
            }

            return cls;
        }

        /// <summary>Пресет, соответствующий классу железа (VR-Ready выводится как есть).</summary>
        public static KvGraphicsPreset PresetOf(KvHardwareClass cls)
        {
            switch (cls)
            {
                case KvHardwareClass.Low: return KvGraphicsPreset.Low;
                case KvHardwareClass.Medium: return KvGraphicsPreset.Medium;
                case KvHardwareClass.High: return KvGraphicsPreset.High;
                case KvHardwareClass.Ultra: return KvGraphicsPreset.Ultra;
                case KvHardwareClass.VRReady: return KvGraphicsPreset.VRReady;
                default: return KvGraphicsPreset.Medium;
            }
        }

        /// <summary>Класс по пресету (обратное преобразование — для подписи «обоснование»).</summary>
        public static string ClassLabel(KvHardwareClass cls)
        {
            switch (cls)
            {
                case KvHardwareClass.Low: return KvGraphicsModel.Label(KvGraphicsPreset.Low);
                case KvHardwareClass.Medium: return KvGraphicsModel.Label(KvGraphicsPreset.Medium);
                case KvHardwareClass.High: return KvGraphicsModel.Label(KvGraphicsPreset.High);
                case KvHardwareClass.Ultra: return KvGraphicsModel.Label(KvGraphicsPreset.Ultra);
                case KvHardwareClass.VRReady: return KvGraphicsModel.Label(KvGraphicsPreset.VRReady);
                default: return KvLocExtra4.T("graphics.class.unknown", "не определён");
            }
        }

        // ------------------------------------------------------------------ признаки

        /// <summary>
        /// Встроенная ли это графика. Проверка по имени/вендору — единственный способ,
        /// доступный без внешних библиотек. Дискретные исключения (Intel Arc, Apple M
        /// в настольном исполнении, NVIDIA/AMD) обрабатываются явно.
        /// </summary>
        public static bool LooksIntegrated(string gpuName, string vendor)
        {
            string s = ((gpuName ?? "") + " " + (vendor ?? "")).ToLowerInvariant();
            if (s.Length == 0) return false;

            if (s.Contains("nvidia") || s.Contains("geforce") || s.Contains("quadro") ||
                s.Contains("rtx") || s.Contains("gtx")) return false;
            if (s.Contains("arc") || s.Contains("a770") || s.Contains("a750") || s.Contains("b580")) return false;
            if (s.Contains("radeon rx") || s.Contains("radeon pro") || s.Contains("vega 56") ||
                s.Contains("vega 64") || s.Contains("vii")) return false;

            if (s.Contains("intel")) return true;                       // UHD / Iris / HD Graphics
            if (s.Contains("radeon graphics") || s.Contains("radeon vega") ||
                s.Contains("vega 8") || s.Contains("vega 11") || s.Contains("apu")) return true;
            if (s.Contains("apple m")) return true;                     // единая память
            if (s.Contains("microsoft basic") || s.Contains("llvmpipe") ||
                s.Contains("swiftshader") || s.Contains("software")) return true;
            return false;
        }

        /// <summary>Поддерживает ли железо трассировку лучей (для серого пункта в интерфейсе).</summary>
        public static bool SupportsRayTracing()
        {
            bool rt = SafeBool(delegate { return SystemInfo.supportsRayTracing; });
            if (!rt) return false;
            return !LooksIntegrated(SafeString(delegate { return SystemInfo.graphicsDeviceName; }, ""),
                SafeString(delegate { return SystemInfo.graphicsDeviceVendor; }, ""));
        }

        // ------------------------------------------------------------------ хеш

        /// <summary>Хеш конфигурации: MD5(видеокарта + VRAM + процессор + ОЗУ + API).</summary>
        public static string Hash(KvGraphicsHardware hw)
        {
            if (hw == null) return "";
            string source = (hw.gpu ?? "") + "|" + hw.vramMb + "|" + (hw.cpu ?? "") + "|" +
                            hw.ramMb + "|" + (hw.api ?? "");
            return Md5(source);
        }

        /// <summary>MD5 строки в hex. Если криптография недоступна — устойчивый FNV-1a.</summary>
        public static string Md5(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            try
            {
                using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
                {
                    byte[] bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(text));
                    StringBuilder sb = new StringBuilder(bytes.Length * 2);
                    for (int i = 0; i < bytes.Length; i++) sb.Append(bytes[i].ToString("x2"));
                    return sb.ToString();
                }
            }
            catch (Exception)
            {
                return Fnv(text);
            }
        }

        /// <summary>Резервный хеш (FNV-1a 64) — на случай недоступности MD5 на платформе.</summary>
        private static string Fnv(string text)
        {
            unchecked
            {
                ulong hash = 14695981039346656037UL;
                for (int i = 0; i < text.Length; i++)
                {
                    hash ^= text[i];
                    hash *= 1099511628211UL;
                }
                return hash.ToString("x16");
            }
        }

        // ------------------------------------------------------------------ безопасные запросы

        private static int SafeInt(Func<int> get, int fallback)
        {
            try { return get(); }
            catch (Exception) { return fallback; }
        }

        private static float SafeFloat(Func<float> get, float fallback)
        {
            try { return get(); }
            catch (Exception) { return fallback; }
        }

        private static bool SafeBool(Func<bool> get)
        {
            try { return get(); }
            catch (Exception) { return false; }
        }

        private static string SafeString(Func<string> get, string fallback)
        {
            try
            {
                string s = get();
                return string.IsNullOrEmpty(s) ? fallback : s;
            }
            catch (Exception) { return fallback; }
        }
    }
}
