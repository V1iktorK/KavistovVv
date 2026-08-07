using UnityEngine;
// CollisionGuard.cs — простейшая проверка перед движением
public class CollisionGuard : MonoBehaviour
{
    public RobotController robot;
    public LayerMask obstacleLayers;    // Стол, стены, другие роботы
    
        public bool CanMoveTo(Vector3 targetPos)
    {
        if (robot == null || robot.tcp == null) return true; // нет данных — пропускаем
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
