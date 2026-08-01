using UnityEngine;

public class RobotSelector : MonoBehaviour
{
    public static RobotSelector Instance;
    public RobotController[] robots;
    private int activeIndex = 0;
    
    void Awake() => Instance = this;
    
    void Start()
    {
        for (int i = 0; i < robots.Length; i++)
            robots[i].SetActive(i == 0);
    }
    
    void Update()
    {
        if (InputManager.Instance != null && InputManager.Instance.SwitchRobotDown)
        {
            robots[activeIndex].SetActive(false);
            activeIndex = (activeIndex + 1) % robots.Length;
            robots[activeIndex].SetActive(true);
            Debug.Log($"[RobotSelector] Активен: {robots[activeIndex].robotName}");
        }
    }
    
    public RobotController GetActiveRobot() => robots[activeIndex];
}
