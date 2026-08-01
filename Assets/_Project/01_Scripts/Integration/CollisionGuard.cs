using UnityEngine;
// CollisionGuard.cs — простейшая проверка перед движением
public class CollisionGuard : MonoBehaviour
{
    public RobotController robot;
    public LayerMask obstacleLayers;    // Стол, стены, другие роботы
    
    public bool CanMoveTo(Vector3 targetPos)
    {
        // Проверяем, нет ли препятствия на пути
        Vector3 direction = targetPos - robot.tcp.position;
        float distance = direction.magnitude;
        
        if (Physics.Raycast(robot.tcp.position, direction.normalized, distance, obstacleLayers))
        {
            Debug.LogWarning("⚠ Обнаружено препятствие! Движение отменено.");
            return false;
        }
        return true;
    }
}
