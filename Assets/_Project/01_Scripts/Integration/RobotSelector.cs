using UnityEngine;

public class RobotSelector : MonoBehaviour
{
    public static RobotSelector Instance;
    public RobotController[] robots;
    private int activeIndex = 0;

    void Awake() => Instance = this;

    void Start()
    {
        if (robots == null || robots.Length == 0)
        {
            return;
        }

        for (int i = 0; i < robots.Length; i++)
        {
            if (robots[i] != null)
            {
                robots[i].SetActive(i == 0);
            }
        }
    }

    void Update()
    {
        if (robots == null || robots.Length == 0 || InputManager.Instance == null)
        {
            return;
        }

        if (InputManager.Instance.SwitchRobotDown)
        {
            if (robots[activeIndex] != null)
            {
                robots[activeIndex].SetActive(false);
            }

            int attempts = 0;
            do
            {
                activeIndex = (activeIndex + 1) % robots.Length;
                attempts++;
            }
            while (robots[activeIndex] == null && attempts < robots.Length);

            if (robots[activeIndex] != null)
            {
                robots[activeIndex].SetActive(true);
                Debug.Log("[RobotSelector] Активен: " + robots[activeIndex].robotName);
            }
        }
    }

    public RobotController GetActiveRobot()
    {
        if (robots == null || robots.Length == 0)
        {
            return null;
        }

        RobotController robot = robots[activeIndex];
        return robot != null ? robot : null;
    }
}
