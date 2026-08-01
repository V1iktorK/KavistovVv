using UnityEngine;
using System.Collections.Generic;
using System.IO;

[System.Serializable]
public class TrajectoryPoint
{
    public float time;
    public Vector3 position;
    public Quaternion rotation;
    public float[] jointAngles;
}

public class TrajectoryRecorder : MonoBehaviour
{
    public RobotController robot;
    private List<TrajectoryPoint> trajectory = new List<TrajectoryPoint>();
    private bool isRecording = false;
    private float startTime;
    
    void Update()
    {
        if (InputManager.Instance == null) return;
        
        if (InputManager.Instance.GrabDown)
        {
            if (!isRecording) StartRecording();
            else StopRecording();
        }
        
        if (isRecording && robot != null)
        {
            trajectory.Add(new TrajectoryPoint
            {
                time = Time.time - startTime,
                position = robot.tcp.position,
                rotation = robot.tcp.rotation,
                jointAngles = robot.GetJointAngles()
            });
        }
    }
    
    public void StartRecording()
    {
        trajectory.Clear();
        isRecording = true;
        startTime = Time.time;
        Debug.Log("[TrajectoryRecorder] ▶ Запись начата");
    }
    
    public void StopRecording()
    {
        isRecording = false;
        Debug.Log($"[TrajectoryRecorder] ⏹ Запись окончена. Точек: {trajectory.Count}");
        
        if (SettingsData.Instance != null && SettingsData.Instance.autoSaveTrajectory)
            SaveToFile();
    }
    
    public List<TrajectoryPoint> GetTrajectory() => trajectory;
    
    void SaveToFile()
    {
        string path = Path.Combine(
            string.IsNullOrEmpty(SettingsData.Instance.trajectorySavePath) ? Application.persistentDataPath : SettingsData.Instance.trajectorySavePath,
            $"trajectory_{System.DateTime.Now:yyyyMMdd_HHmmss}.json"
        );
        string json = JsonUtility.ToJson(new TrajectoryDataWrapper { points = trajectory.ToArray() }, true);
        File.WriteAllText(path, json);
        Debug.Log($"[TrajectoryRecorder] Сохранено: {path}");
    }
}

[System.Serializable]
public class TrajectoryDataWrapper
{
    public TrajectoryPoint[] points;
}
