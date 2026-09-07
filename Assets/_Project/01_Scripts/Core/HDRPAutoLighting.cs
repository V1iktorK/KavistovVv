using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// Автоматически исправляет освещение HDRP при запуске сцены.
/// </summary>
public class HDRPAutoLighting : MonoBehaviour
{
    [Header("Light Settings")]
    public float mainLightIntensity = 80000f;
    public float fillLightIntensity = 20000f;
    public float ambientBoost = 0.5f;
    public float cameraExposureCompensation = 1.0f;

    void Start()
    {
        FixDirectionalLight();
        if (fillLightIntensity > 0f)
            CreateFillLight();
        FixAmbientLighting();
        FixCameraExposure();
        Debug.Log("[HDRPAutoLighting] Lighting fixed. Main: " + mainLightIntensity + " Lux, Fill: " + fillLightIntensity + " Lux");
    }

    void FixDirectionalLight()
    {
        Light[] lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Ignore, FindObjectsSortMode.None);
        foreach (Light light in lights)
        {
            if (light.type != LightType.Directional) continue;

            light.color = new Color(1f, 0.95f, 0.88f);
            light.intensity = mainLightIntensity;
            light.lightUnit = LightUnit.Lux;
            light.shadows = LightShadows.Soft;
            light.bounceIntensity = 1.5f;

            HDAdditionalLightData hdLight = light.GetComponent<HDAdditionalLightData>();
            if (hdLight != null)
            {
                hdLight.volumetricDimmer = 2.0f;
            }

            Debug.Log("[HDRPAutoLighting] Fixed: " + light.gameObject.name);
            return;
        }
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
        light.shadows = LightShadows.Soft;
        light.bounceIntensity = 1.5f;

        go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        Debug.Log("[HDRPAutoLighting] Created Directional Light");
    }

    void CreateFillLight()
    {
        GameObject go = GameObject.Find("FillLight");
        if (go == null)
            go = new GameObject("FillLight");

        Light light = go.GetComponent<Light>();
        if (light == null)
            light = go.AddComponent<Light>();

        light.type = LightType.Directional;
        light.color = new Color(0.65f, 0.75f, 1f);
        light.intensity = fillLightIntensity;
        light.lightUnit = LightUnit.Lux;
        light.shadows = LightShadows.None;
        light.bounceIntensity = 0.8f;

        go.transform.rotation = Quaternion.Euler(-15f, 160f, 0f);
        Debug.Log("[HDRPAutoLighting] Created Fill Light");
    }

    void FixAmbientLighting()
    {
        Volume[] volumes = Object.FindObjectsByType<Volume>(FindObjectsInactive.Ignore, FindObjectsSortMode.None);
        Volume globalVolume = null;

        foreach (Volume vol in volumes)
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

        VolumeProfile profile = globalVolume.profile;

        // Ambient Occlusion
        bool foundAO = profile.TryGet<ScreenSpaceAmbientOcclusion>(out ScreenSpaceAmbientOcclusion ao);
        if (foundAO)
            ao.intensity.Override(0f);

        // Color Adjustments — повышаем яркость
        bool foundColor = profile.TryGet<ColorAdjustments>(out ColorAdjustments colorAdj);
        if (foundColor)
            colorAdj.postExposure.Override(ambientBoost);

        // Bloom — немного для мягкости
        bool foundBloom = profile.TryGet<Bloom>(out Bloom bloom);
        if (foundBloom)
        {
            bloom.intensity.Override(0.15f);
            bloom.threshold.Override(0.9f);
        }

        Debug.Log("[HDRPAutoLighting] Ambient lighting fixed");
    }

    void FixCameraExposure()
    {
        // Настройка exposure через Exposure volume component
        Volume[] volumes = Object.FindObjectsByType<Volume>(FindObjectsInactive.Ignore, FindObjectsSortMode.None);
        Volume globalVolume = null;

        foreach (Volume vol in volumes)
        {
            if (vol.isGlobal)
            {
                globalVolume = vol;
                break;
            }
        }

        if (globalVolume == null)
        {
            globalVolume = new GameObject("ExposureVolume", typeof(Volume)).GetComponent<Volume>();
            globalVolume.isGlobal = true;
            globalVolume.priority = 1;
        }

        VolumeProfile profile = globalVolume.profile;
        bool foundExposure = profile.TryGet<Exposure>(out Exposure exposure);
        if (foundExposure)
        {
            exposure.compensation.Override(cameraExposureCompensation);
        }

        Debug.Log("[HDRPAutoLighting] Camera exposure: " + cameraExposureCompensation);
    }
}
