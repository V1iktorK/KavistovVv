using UnityEngine;

// InverseKinematics.cs — упрощённый CCD для 6-осевого
public class InverseKinematics : MonoBehaviour
{
    public Transform[] joints;      // 6 суставов от основания к фланцу
    public Transform target;        // Куда должна прийти TCP (Tool Center Point)
    public float threshold = 0.01f;
    public int maxIterations = 10;
    
    void Update()
    {
        // CCD: идём от последнего сустава к первому
        for (int iter = 0; iter < maxIterations; iter++)
        {
            for (int i = joints.Length - 1; i >= 0; i--)
            {
                Vector3 toTarget = target.position - joints[i].position;
                Vector3 toEnd = joints[joints.Length - 1].position - joints[i].position;
                
                float angle = Vector3.SignedAngle(toEnd, toTarget, joints[i].up);
                joints[i].Rotate(joints[i].up, angle, Space.World);
            }
            
            if (Vector3.Distance(joints[joints.Length - 1].position, target.position) < threshold)
                break;
        }
    }
}