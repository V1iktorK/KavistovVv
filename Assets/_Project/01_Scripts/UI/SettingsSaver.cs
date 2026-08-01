using UnityEngine;
using System.IO;

public static class SettingsSaver
{
    private static string FilePath => Path.Combine(Application.persistentDataPath, "settings_vr_robot.json");
    
    public static void Save(SettingsData data)
    {
        if (data == null) return;
        File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
        Debug.Log($"[SettingsSaver] Сохранено: {FilePath}");
    }
    
    public static void Load(SettingsData data)
    {
        if (!File.Exists(FilePath)) return;
        JsonUtility.FromJsonOverwrite(File.ReadAllText(FilePath), data);
        Debug.Log("[SettingsSaver] Загружено");
    }
    
    public static void Delete()
    {
        if (File.Exists(FilePath)) File.Delete(FilePath);
    }
}
