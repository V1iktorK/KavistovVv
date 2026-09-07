using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// Автоматически исправляет освещение HDRP при запуске сцены.
/// Использует только актуальный HDRP API (2023.3+).
/// </summary>
public class HDRPAutoLighting : MonoBehaviour
{
    [Header("Light Settings")]
    [Tooltip("Intensity of main directional light in Lux")]
    public float mainLightIntensity = 80000f;
    
    [Tooltip("Fill light intensity in Lux (0 = disabled)")]
    public float fillLightIntensity = 20000f;
    
    [Tooltip("Ambient brightness boost")]
    public float ambientBoost = 0.5f;
    
    [Header("Camera")]
    [Tooltip("Extra exposure compensation for camera (+EV)")]
    public float cameraExposureCompensation = 1.0f;

    void Start()
    {
        FixDirectionalLight();
        if (fillLightIntensity > 0f)
        {
            CreateFillLight();
        }
        FixAmbientLighting();
        FixCameraExposure();
        
        Debug.Log("[HDRPAutoLighting] Lighting fixed. Main: " + mainLightIntensity + " Lux, Fill: " + fillLightIntensity + " Lux");
    }

    void FixDirectionalLight()
    {
        // Находим Directional Light в сцене
        Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (var light in lights)
        {
            if (light.type != LightType.Directional) continue;
            
            // Настройка через Light компонент (актуальный API)
            light.color = new Color(1f, 0.95f, 0.88f);
            light.intensity = mainLightIntensity;
            light.lightUnit = LightUnit.Lux;
            
            // Настройка теней
            light.shadows = LightShadows.SuperHard;
            light.shadowDimmer = 1f;
            light.shadowCastMode = UnityEngine.Rendering.LightShadowCastMode.Oneside;
            
            // Bounce light для отражений
            light.bounceIntensity = 1.5f;
            
            // Увеличиваем радиус для volumetric effects
            light.volumetricDimmer = 2.0f;
            
            Debug.Log($"[HDRPAutoLighting] Fixed Directional Light: {light.gameObject.name} -> {mainLightIntensity} Lux");
            return;
        }
        
        // Если Directional Light не найден — создаём
        CreateMainDirectionalLight();
    }

    void CreateMainDirectionalLight()
    {
        GameObject go = new GameObject("MainDirectionalLight");
        Light light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.95f, 0.88f);
        light.intensity = mainLightIntensity;
        light.lightUnit = LightUnit.Lux;
        light.shadows = LightShadows.SuperHard;
        light.shadowDimmer = 1f;
        light.shadowCastMode = UnityEngine.Rendering.LightShadowCastMode.Oneside;
        light.bounceIntensity = 1.5f;
        light.volumetricDimmer = 2.0f;
        
        go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        Debug.Log("[HDRPAutoLighting] Created new Directional Light");
    }

    void CreateFillLight()
    {
        GameObject go = GameObject.Find("FillLight");
        if (go == null)
        {
            go = new GameObject("FillLight");
        }
        
        Light light = go.GetComponent<Light>() ?? go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(0.65f, 0.75f, 1f); // Холодный голубоватый
        light.intensity = fillLightIntensity;
        light.lightUnit = LightUnit.Lux;
        light.shadows = LightShadows.None;
        light.bounceIntensity = 0.8f;
        
        go.transform.rotation = Quaternion.Euler(-15f, 160f, 0f);
        Debug.Log($"[HDRPAutoLighting] Created Fill Light: {fillLightIntensity} Lux");
    }

    void FixAmbientLighting()
    {
        // Находим Volume в сцене
        Volume[] volumes = Object.FindObjectsByType<Volume>(FindObjectsSortMode.None);
        Volume globalVolume = null;
        
        foreach (var vol in volumes)
        {
            if (vol.isGlobal)
            {
                globalVolume = vol;
                break;
            }
        }
        
        if (globalVolume == null)
        {
            globalVolume = new GameObject("GlobalAmbientVolume", typeof(Volume)).GetComponent<Volume>();
            globalVolume.isGlobal = true;
            globalVolume.priority = 0;
        }
        
        var profile = globalVolume.profile;
        
        // Уменьшаем AO чтобы тени не были чёрными
        var ambientOcclusion = profile.Get<AmbientOcclusion>();
        if (ambientOcclusion != null)
        {
            ambientOcclusion.intensity.Override(0f);
        }
        
        // Color Adjustments — повышаем яркость
        var colorAdjustments = profile.Get<ColorAdjustments>();
        if (colorAdjustments == null)
        {
            colorAdjustments = ScriptableObject.CreateInstance<ColorAdjustments>();
            profile.Add(colorAdjustments);
        }
        colorAdjustments.postExposure.Override(ambientBoost);
        
        // Bloom — немного для мягкости
        var bloom = profile.Get<Bloom>();
        if (bloom == null)
        {
            bloom = ScriptableObject.CreateInstance<Bloom>();
            profile.Add(bloom);
        }
        bloom.intensity.Override(0.15f);
        bloom.threshold.Override(0.9f);
        
        Debug.Log("[HDRPAutoLighting] Ambient lighting fixed");
    }

    void FixCameraExposure()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null) return;
        
        // Настройка exposure через HDAdditionalLightData
        var hdLightData = mainCamera.GetComponent<HDAdditionalLightData>();
        if (hdLightData == null)
        {
            hdLightData = mainCamera.gameObject.AddComponent<HDAdditionalLightData>();
        }
        
        hdLightData.exposureCompensation = cameraExposureCompensation;
        
        Debug.Log($"[HDRPAutoLighting] Camera exposure +{cameraExposureCompensation} EV");
    }
}
