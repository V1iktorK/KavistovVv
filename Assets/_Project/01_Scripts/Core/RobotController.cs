// RobotController.cs — абстрактный базовый класс
using UnityEngine;

public abstract class RobotController : MonoBehaviour
{
    [Header("Base")]
    public string robotName = "Robot";
    public Transform tcp;
    [Range(0.1f, 5f)] public float maxSpeed = 1f;
    [HideInInspector] public bool isActive = false;
    
    [Header("Industry 4.0 - Telemetry")]
    public bool telemetryEnabled = true;
    public float jointTemperature = 25f;
    public float operatingHours = 0f;
    
    protected Vector3 targetPosition;
    protected Quaternion targetRotation;
    
    public abstract void SetTarget(Vector3 position, Quaternion? rotation = null);
    public abstract void MoveToTarget(float deltaTime);
    public abstract float[] GetJointAngles();
    
    public virtual void SetActive(bool active)
    {
        isActive = active;
        foreach (var rend in GetComponentsInChildren<Renderer>(true))
        {
            // Сохраняем оригинальный цвет при первом вызове
            // Для MVP — просто включаем/выключаем Emission
            if (rend.material.HasProperty("_EmissionColor"))
            {
                rend.material.SetColor("_EmissionColor", active ? Color.green * 0.3f : Color.black);
                rend.material.EnableKeyword("_EMISSION");
            }
        }
    }
    
    public virtual void UpdateTelemetry(float deltaTime)
    {
        if (!telemetryEnabled) return;
        operatingHours += deltaTime / 3600f;
        float movement = Vector3.Distance(tcp != null ? tcp.position : transform.position, targetPosition);
        jointTemperature = Mathf.Lerp(jointTemperature, 25f + movement * 50f, deltaTime * 0.05f);
    }
}
