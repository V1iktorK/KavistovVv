using UnityEngine;

public class CalibrationTool : MonoBehaviour
{
    [Header("Calibration")]
    public Transform virtualRobotBase;
    public GameObject calibrationPointPrefab; // Красная сфера
    public GameObject calibrationUI;
    
    private Vector3[] realPoints = new Vector3[3];
    private Vector3[] modelPoints = new Vector3[3];
    private int currentPoint = 0;
    private bool isCalibrating = false;
    
    public void StartCalibration()
    {
        isCalibrating = true;
        currentPoint = 0;
        calibrationUI.SetActive(true);
        Debug.Log("[Calibration] Наведите луч на 3 точки реального робота и нажмите Select");
    }
    
    void Update()
    {
        if (!isCalibrating) return;
        
        var input = InputManager.Instance;
        if (input == null) return;
        
        // Визуализация луча
        Debug.DrawRay(input.PointerPosition, input.PointerDirection * 3f, Color.yellow);
        
        if (input.SelectDown)
        {
            Ray ray = new Ray(input.PointerPosition, input.PointerDirection);
            if (Physics.Raycast(ray, out RaycastHit hit, 5f))
            {
                SetPoint(hit.point);
            }
        }
    }
    
    void SetPoint(Vector3 realWorldPos)
    {
        if (currentPoint >= 3) return;
        
        realPoints[currentPoint] = realWorldPos;
        
        // Соответствующая точка на модели (заранее заданные)
        modelPoints[currentPoint] = GetModelCalibrationPoint(currentPoint);
        
        // Визуальный маркер
        Instantiate(calibrationPointPrefab, realWorldPos, Quaternion.identity);
        
        currentPoint++;
        Debug.Log($"[Calibration] Точка {currentPoint}/3");
        
        if (currentPoint >= 3)
        {
            ApplyCalibration();
        }
    }
    
    Vector3 GetModelCalibrationPoint(int index)
    {
        // Три ключевые точки на модели робота
        switch(index)
        {
            case 0: return Vector3.zero;                    // Основание
            case 1: return new Vector3(0, 0.4f, 0);         // Сустав 2
            case 2: return new Vector3(0.3f, 0.8f, 0);      // Фланец (пример)
            default: return Vector3.zero;
        }
    }
    
    void ApplyCalibration()
    {
        // Вычисляем смещение: позиция + поворот
        Vector3 realDir = (realPoints[1] - realPoints[0]).normalized;
        Vector3 modelDir = (modelPoints[1] - modelPoints[0]).normalized;
        
        Quaternion rotOffset = Quaternion.FromToRotation(modelDir, realDir);
        Vector3 posOffset = realPoints[0] - (rotOffset * modelPoints[0]);
        
        virtualRobotBase.rotation = rotOffset * virtualRobotBase.rotation;
        virtualRobotBase.position = posOffset;
        
        // Сохраняем
        PlayerPrefs.SetFloat("CalX", posOffset.x);
        PlayerPrefs.SetFloat("CalY", posOffset.y);
        PlayerPrefs.SetFloat("CalZ", posOffset.z);
        PlayerPrefs.SetFloat("CalQx", rotOffset.x);
        PlayerPrefs.SetFloat("CalQy", rotOffset.y);
        PlayerPrefs.SetFloat("CalQz", rotOffset.z);
        PlayerPrefs.SetFloat("CalQw", rotOffset.w);
        PlayerPrefs.Save();
        
        isCalibrating = false;
        calibrationUI.SetActive(false);
        Debug.Log("[Calibration] Готово! Виртуальный робот совмещён с реальным.");
    }
}
