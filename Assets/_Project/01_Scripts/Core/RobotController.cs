// RobotController.cs — абстрактный базовый класс
using UnityEngine;

public class RobotController : MonoBehaviour // Убрали abstract
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
    protected bool hasTarget;

    protected virtual void Awake()
    {
        targetPosition = transform.position;
        targetRotation = transform.rotation;
    }
    
    // Заменили abstract на virtual и добавили пустые реализации
    public virtual void SetTarget(Vector3 position, Quaternion? rotation = null)
    {
        targetPosition = position;
        if (rotation.HasValue) targetRotation = rotation.Value;
        hasTarget = true;
    }

    public virtual void MoveToTarget(float deltaTime)
    {
        if (!hasTarget) return;
        float speed = maxSpeed * (SettingsData.Instance?.robotSpeed ?? 1f);
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * deltaTime);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, speed * 60f * deltaTime);
    }

    public virtual float[] GetJointAngles()
    {
        return new float[0]; // Возвращаем пустой массив по умолчанию
    }
    
    public virtual void SetActive(bool active)
    {
        isActive = active;
        foreach (var rend in GetComponentsInChildren<Renderer>(true))
        {
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