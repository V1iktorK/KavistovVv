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
        // Визуальное выделение: меняем цвет материала
        var renderer = GetComponentInChildren<Renderer>();
        if (renderer != null)
            renderer.material.color = active ? Color.green : Color.white;
    }
    
    public virtual void UpdateTelemetry(float deltaTime)
    {
        if (!telemetryEnabled) return;
        operatingHours += deltaTime / 3600f;
        float movement = Vector3.Distance(tcp != null ? tcp.position : transform.position, targetPosition);
        jointTemperature = Mathf.Lerp(jointTemperature, 25f + movement * 50f, deltaTime * 0.05f);
    }
}
