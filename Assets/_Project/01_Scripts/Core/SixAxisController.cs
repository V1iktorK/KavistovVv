using UnityEngine;

public class SixAxisController : RobotController
{
    [Header("6-Axis IK")]
    public InverseKinematics ik;
    public Transform[] jointTransforms;
    
    void Update()
    {
        MoveToTarget(Time.deltaTime);
        UpdateTelemetry(Time.deltaTime);
    }
    
    public override void SetTarget(Vector3 position, Quaternion? rotation = null)
    {
        targetPosition = position;
        targetRotation = rotation ?? Quaternion.identity;
        if (ik != null && ik.target != null)
        {
            ik.target.position = targetPosition;
            ik.target.rotation = targetRotation;
        }
    }
    
    public override void MoveToTarget(float deltaTime)
    {
        // IK работает в своём компоненте, здесь можно добавить плавность
    }
    
    public override float[] GetJointAngles()
    {
        if (jointTransforms == null) return new float[0];
        float[] angles = new float[jointTransforms.Length];
        for (int i = 0; i < jointTransforms.Length; i++)
            angles[i] = jointTransforms[i].localEulerAngles.y;
        return angles;
    }
}
