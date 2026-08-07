using UnityEngine;

public class SpatialAnchorManager : MonoBehaviour
{
    public static SpatialAnchorManager Instance;
    
    [Header("Placement")]
    public Transform robotRoot;
    public GameObject placementIndicator;
    
    private bool isPlaced = false;
    
    void Awake() => Instance = this;
    
    public void PlaceRobot(Vector3 position, Quaternion rotation)
    {
        robotRoot.position = position;
        robotRoot.rotation = rotation;
        isPlaced = true;
        
        if (placementIndicator != null)
            placementIndicator.SetActive(false);
        
        PlayerPrefs.SetString("AnchorSaved", "true");
        PlayerPrefs.SetFloat("AnchorX", position.x);
        PlayerPrefs.SetFloat("AnchorY", position.y);
        PlayerPrefs.SetFloat("AnchorZ", position.z);
        PlayerPrefs.Save();
        
        Debug.Log($"[SpatialAnchor] Робот размещён: {position}");
    }

    public void Recalibrate()
    {
        isPlaced = false;
        PlayerPrefs.DeleteKey("AnchorSaved");
        if (placementIndicator != null)
            placementIndicator.SetActive(true);
    }
    
    void Start()
    {
        if (PlayerPrefs.GetString("AnchorSaved", "") == "true")
        {
            Vector3 pos = new Vector3(
                PlayerPrefs.GetFloat("AnchorX"),
                PlayerPrefs.GetFloat("AnchorY"),
                PlayerPrefs.GetFloat("AnchorZ")
            );
            robotRoot.position = pos;
            isPlaced = true;
            if (placementIndicator != null)
                placementIndicator.SetActive(false);
        }
    }
}