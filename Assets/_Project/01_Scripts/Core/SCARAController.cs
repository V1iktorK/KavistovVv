using UnityEngine;

public class SCARAController : RobotController
{
    [Header("SCARA Joints")]
    public Transform joint1;
    public Transform joint2;
    public Transform joint3; // Prismatic Z
    public Transform joint4; // Tool rotation
    
    [Header("Dimensions")]
    public float L1 = 0.4f;
    public float L2 = 0.3f;
    public float ZBase = 0.5f;
    public float ZMin = 0f;
    public float ZMax = 0.3f;
    
    void Update()
    {
        MoveToTarget(Time.deltaTime);
        UpdateTelemetry(Time.deltaTime);
    }
    
    public override void SetTarget(Vector3 position, Quaternion? rotation = null)
    {
        targetPosition = position;
        targetRotation = rotation ?? Quaternion.identity;
    }
    
    public override void MoveToTarget(float deltaTime)
    {
        float x = targetPosition.x - transform.position.x;
        float y = targetPosition.z - transform.position.z;
        float z = targetPosition.y - transform.position.y;
        
        float r = Mathf.Sqrt(x * x + y * y);
        float cosTheta2 = Mathf.Clamp((r * r - L1 * L1 - L2 * L2) / (2 * L1 * L2), -1f, 1f);
        float theta2 = Mathf.Acos(cosTheta2);
        float theta1 = Mathf.Atan2(y, x) - Mathf.Atan2(L2 * Mathf.Sin(theta2), L1 + L2 * Mathf.Cos(theta2));
        float d3 = Mathf.Clamp(z - ZBase, ZMin, ZMax);
        float theta4 = targetRotation.eulerAngles.y - (theta1 + theta2) * Mathf.Rad2Deg;
        
        float speed = maxSpeed * (SettingsData.Instance?.robotSpeed ?? 1f) * deltaTime * 5f;
        
        if (joint1 != null)
            joint1.localRotation = Quaternion.Slerp(joint1.localRotation, Quaternion.Euler(0, theta1 * Mathf.Rad2Deg, 0), speed);
        if (joint2 != null)
            joint2.localRotation = Quaternion.Slerp(joint2.localRotation, Quaternion.Euler(0, theta2 * Mathf.Rad2Deg, 0), speed);
        if (joint3 != null)
            joint3.localPosition = Vector3.Lerp(joint3.localPosition, new Vector3(0, d3, 0), speed);
        if (joint4 != null)
            joint4.localRotation = Quaternion.Slerp(joint4.localRotation, Quaternion.Euler(0, theta4, 0), speed);
    }
    
    public override float[] GetJointAngles()
    {
        return new float[]
        {
            joint1 ? joint1.localEulerAngles.y : 0f,
            joint2 ? joint2.localEulerAngles.y : 0f,
            joint3 ? joint3.localPosition.y : 0f,
            joint4 ? joint4.localEulerAngles.y : 0f
        };
    }
}