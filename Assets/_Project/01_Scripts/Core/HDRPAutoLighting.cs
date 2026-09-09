using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// Автоматически настраивает освещение HDRP:
/// добавляет аддитивный «студийный» световой сет (ключевой, заливающий
/// и контровой источники) над зоной роботов и слегка поднимает общую
/// яркость через глобальный Volume (Exposure / ColorAdjustments / AO / Bloom).
///
/// ВАЖНО:
///  - существующие направленные источники (солнце) НЕ трогаются;
///  - все цвета нейтральные (тёплый белый / белый), никакого фиолетового
///    тонирования; за фиолетовый оттенок отвечает только hueShift/colorFilter,
///    которые этот скрипт не изменяет.
/// </summary>
[ExecuteInEditMode]
public class HDRPAutoLighting : MonoBehaviour
{
    [Header("Key light (ключевой свет над зоной роботов)")]
    public bool enableKeyLight = true;
    [Tooltip("Дополняет существующее солнце, не заменяет его. Мягкое значение.")]
    public float mainLightIntensity = 20000f;  // люкс
    public Color keyLightColor = new Color(1f, 0.98f, 0.94f); // тёплый белый
    public float lightHeight = 4f;
    public float lightDistance = 4f;

    [Header("Fill light (заливающий свет)")]
    public float fillLightIntensity = 8000f;   // люкс
    public Color fillColor = new Color(0.96f, 0.98f, 1f); // холодно-нейтральный белый

    [Header("Rim light (контровой свет)")]
    public float rimLightIntensity = 6000f;    // люкс

    [Header("Зона освещения")]
    public Transform[] lightTargets;            // роботы/стол; если пусто — найдём роботов сами
    public Vector3 fallbackCenter = new Vector3(12f, 0f, -8f);

    [Header("Глобальная яркость (Volume)")]
    [Tooltip("Пост-экспозиция перед color grading, в EV. Ноль — чтобы GUI не выбеливался.")]
    public float ambientBoost = 0f;
    [Tooltip("Компенсация авто-экспозиции, в EV. Ноль — нейтральный кадр без белого клиппинга.")]
    public float cameraExposureCompensation = 0f;
    [Tooltip("Сила ambient occlusion (0 — полностью выключена, тёмные впадины уходят).")]
    public float ambientOcclusionStrength = 0.1f;
    public bool enableBloom = false;

    private const string KeyLightName = "Cline_KeyLight";
    private const string FillLightName = "Cline_FillLight";
    private const string RimLightName = "Cline_RimLight";

    void Start()
    {
        Apply();
    }

    /// <summary>Применяет студийный сет и настройки Volume. Идемпотентно.</summary>
    /// <remarks>
    /// Исправлен пересвет: направленные источники HDRP в люксах складываются,
    /// поэтому держим один аккуратный ключ + лёгкий fill (без «выжигающего» rim,
    /// и без +0.5 EV компенсации, которая выбеливала белую графику UI).
    /// Функция идемпотентна: повторный вызов не накапливает свет.
    /// </remarks>
    public void Apply()
    {
        Vector3 center = ResolveLightCenter();

        // Ключевой источник — единственный уверенный свет над роботом.
        Light sun = SunLight();
        float key = enableKeyLight ? SaturateLux(mainLightIntensity) : 0f;
        if (sun != null)
        {
            // Используем реальное «солнце» сцены; свой managed Key не создаём/отключаем,
            // иначе два огромных направленных сложатся и снова пересветят.
            sun.enabled = true;
            sun.color = keyLightColor;
            sun.intensity = key > 0f ? key : 0f;
            ManageKeyLight(false);
        }
        else if (enableKeyLight)
        {
            // Солнца нет — создаём мягкий ключ сами.
            SetupStudioLight(KeyLightName, keyLightColor, key,
                center + new Vector3(lightDistance, lightHeight, lightDistance * 0.5f), center);
            ManageKeyLight(true);
        }

        // Fill — небольшой, чтобы смягчить тени, не пережимать форму.
        SetupStudioLight(FillLightName, fillColor, SaturateFill(SaturateLux(fillLightIntensity)),
            center + new Vector3(-lightDistance, lightHeight * 0.8f, -lightDistance * 0.3f), center);

        // Rim-контур: лёгкая окантовка; большой контровой свет «выжигает» блики UI/модели.
        SetupStudioLight(RimLightName, Color.white, SaturateFill(SaturateLux(rimLightIntensity)),
            center + new Vector3(0f, lightHeight, -lightDistance * 1.2f), center);

        ConfigureGlobalVolume();
        Debug.Log($"[HDRPAutoLighting] Свет применён: sun={key:0} лк, fill={SaturateLux(fillLightIntensity):0} лк, rim={SaturateLux(rimLightIntensity):0} лк");
    }

    /// <summary>Находит существующий направленный источник (солнце) либо единственный активный directional.</summary>
    private Light SunLight()
    {
        // Приоритет — по имени (запечённый "Directional Light"), затем любой directional.
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
        {
            if (l.type == LightType.Directional && l.gameObject.name == "Directional Light")
                return l;
        }
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
        {
            if (l.type == LightType.Directional && !IsManaged(l))
                return l;
        }
        return null;
    }

    /// <summary>True, если источник создан самим этим скриптом (Key/Fill/Rim) — таких не используем как «солнце».</summary>
    private static bool IsManaged(Light l)
    {
        string n = l.gameObject.name;
        return n == KeyLightName || n == FillLightName || n == RimLightName;
    }

    /// <summary>Ограничивает интенсивность разумным для HDRP физический диапазоном (не даём 80k+ удивлять).</summary>
    private static float SaturateLux(float lux)
    {
        // Directional Lux в HDRP: солнце ~100k. Для ключа больше ~40k уже пережар.
        return Mathf.Clamp(lux, 0f, 40000f);
    }

    /// <summary>Fill/Rim — акценты; не даём им подниматься до ключа (иначе клиппинг белого).</summary>
    private static float SaturateFill(float lux)
    {
        return Mathf.Clamp(lux, 0f, 9000f);
    }

    /// <summary>Центр зоны освещения: по заданным целям, иначе по роботам сцены, иначе fallback.</summary>
    private Vector3 ResolveLightCenter()
    {
        if (lightTargets != null && lightTargets.Length > 0)
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int i = 0; i < lightTargets.Length; i++)
            {
                if (lightTargets[i] != null)
                {
                    sum += lightTargets[i].position;
                    count++;
                }
            }
            if (count > 0)
            {
                return sum / count;
            }
        }

        RobotController[] robots = Object.FindObjectsByType<RobotController>(FindObjectsInactive.Include);
        if (robots.Length > 0)
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int i = 0; i < robots.Length; i++)
            {
                if (robots[i] != null)
                {
                    sum += robots[i].transform.position;
                    count++;
                }
            }
            return sum / count;
        }

        return fallbackCenter;
    }

    private void SetupStudioLight(string lightName, Color color, float intensityLux, Vector3 position, Vector3 lookAt)
    {
        GameObject go = GameObject.Find(lightName);
        if (go == null)
        {
            go = new GameObject(lightName);
            go.AddComponent<Light>();
            go.AddComponent<HDAdditionalLightData>();
        }

        Light light = go.GetComponent<Light>();
        if (light == null)
        {
            light = go.AddComponent<Light>();
        }

        light.type = LightType.Directional;
        light.color = color;
        light.lightUnit = LightUnit.Lux;
        light.intensity = intensityLux;

        go.transform.position = position;
        go.transform.LookAt(lookAt + Vector3.up * 1f);
    }

    /// <summary>Включает/выключает managed Key-источник, чтобы не дублировать солнце.</summary>
    private void ManageKeyLight(bool on)
    {
        GameObject go = GameObject.Find(KeyLightName);
        if (go != null && go.TryGetComponent<Light>(out Light l))
        {
            l.enabled = on;
        }
    }

    /// <summary>Настраивает глобальный Volume: экспозиция, пост-экспозиция, AO, Bloom.</summary>
    private void ConfigureGlobalVolume()
    {
        Volume volume = FindGlobalVolume();
        if (volume == null)
        {
            GameObject go = new GameObject("Cline_GlobalVolume", typeof(Volume));
            volume = go.GetComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
        }

        VolumeProfile profile = volume.profile;

        // Экспозиция: оставляем авто-режим, но добавляем компенсацию.
        if (!profile.TryGet<Exposure>(out Exposure exposure))
        {
            exposure = profile.Add<Exposure>(true);
        }
        exposure.mode.Override(ExposureMode.Automatic);
        exposure.compensation.Override(cameraExposureCompensation);

        // Пост-экспозиция перед color grading (нейтральная, без тонировки).
        if (!profile.TryGet<ColorAdjustments>(out ColorAdjustments colorAdjustments))
        {
            colorAdjustments = profile.Add<ColorAdjustments>(true);
        }
        colorAdjustments.postExposure.Override(ambientBoost);

        // Ambient Occlusion: почти выключаем, чтобы тёмные впадины не «съедали» модель.
        if (!profile.TryGet<ScreenSpaceAmbientOcclusion>(out ScreenSpaceAmbientOcclusion ao))
        {
            ao = profile.Add<ScreenSpaceAmbientOcclusion>(true);
        }
        ao.intensity.Override(ambientOcclusionStrength);

        // Bloom: по умолчанию выключен.
        if (!profile.TryGet<Bloom>(out Bloom bloom))
        {
            bloom = profile.Add<Bloom>(true);
        }
        if (enableBloom)
        {
            bloom.intensity.Override(0.15f);
            bloom.threshold.Override(0.9f);
        }
        else
        {
            bloom.intensity.Override(0f);
        }

        Debug.Log("[HDRPAutoLighting] Глобальный Volume настроен: compensation " +
                  cameraExposureCompensation + " EV, postExposure " + ambientBoost + " EV");
    }

    private Volume FindGlobalVolume()
    {
        Volume[] volumes = Object.FindObjectsByType<Volume>(FindObjectsInactive.Include);
        for (int i = 0; i < volumes.Length; i++)
        {
            if (volumes[i].isGlobal)
            {
                return volumes[i];
            }
        }
        return null;
    }

    #if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/Robots/Apply Studio Lighting (HDRP)")]
    static void MenuApplyStudioLighting()
    {
        // Ищем существующий компонент — не создаём дубликат.
        HDRPAutoLighting instance = Object.FindAnyObjectByType<HDRPAutoLighting>();
        if (instance == null)
        {
            GameObject go = new GameObject("HDRP Auto Lighting");
            instance = go.AddComponent<HDRPAutoLighting>();
        }
        instance.Apply();
        Debug.Log("[HDRPAutoLighting] Применено через меню Tools/Robots.");
    }

    [UnityEditor.MenuItem("Tools/Robots/Remove Studio Lighting (HDRP)")]
    static void MenuRemoveStudioLighting()
    {
        // Удаляем добавленные источники света, чтобы вернуть сцену к исходному состоянию.
        DestroyStudioLight(KeyLightName);
        DestroyStudioLight(FillLightName);
        DestroyStudioLight(RimLightName);
        Debug.Log("[HDRPAutoLighting] Студийный свет удалён.");
    }

    private static void DestroyStudioLight(string name)
    {
        GameObject go = GameObject.Find(name);
        if (go != null)
        {
            Object.DestroyImmediate(go);
        }
    }
    #endif
}
