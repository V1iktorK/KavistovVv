using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.ARFoundation;
using System.Collections.Generic;

public class SpatialAnchorManager : MonoBehaviour
{
    public static SpatialAnchorManager Instance;
    
    [Header("Placement")]
    public Transform robotRoot;         // Корневой объект робота
    public GameObject placementIndicator; // Круг/крестик на полу
    public ARRaycastManager raycastManager;
    public ARAnchorManager anchorManager;
    
    private bool isPlaced = false;
    private ARAnchor currentAnchor;
    
    void Awake() => Instance = this;
    
    void Update()
    {
        // Только для AR-режима
        if (InputManager.Instance?.ActiveProvider is not ARInputProvider) return;
        if (isPlaced) return;
        
        // Центр экрана телефона
        Vector2 screenCenter = new Vector2(Screen.width / 2, Screen.height / 2);
        List<ARRaycastHit> hits = new List<ARRaycastHit>();
        
        if (raycastManager != null && raycastManager.Raycast(screenCenter, hits, TrackableType.Planes))
        {
            // Показываем индикатор
            placementIndicator.SetActive(true);
            placementIndicator.transform.position = hits[0].pose.position;
            placementIndicator.transform.rotation = hits[0].pose.rotation;
            
            // По тапу ставим якорь
            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
            {
                PlaceRobot(hits[0].pose.position, hits[0].pose.rotation);
            }
        }
        else
        {
            placementIndicator.SetActive(false);
        }
    }
    
   public void PlaceRobot(Vector3 position, Quaternion rotation)
{
    // Создаём якорь в AR Foundation (если менеджер доступен)
    // if (anchorManager != null)
    // {
    //     // В версии 6.5.0 используется AddAnchor
    //     currentAnchor = anchorManager.AddAnchor(new Pose(position, rotation));
    // }
    
    robotRoot.position = position;
    robotRoot.rotation = rotation;
    isPlaced = true;
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
    // if (currentAnchor != null && anchorManager != null)
    // {
    //     anchorManager.RemoveAnchor(currentAnchor);
    //     currentAnchor = null;
    // }
    PlayerPrefs.DeleteKey("AnchorSaved");
}
    
    void Start()
    {
        // Восстанавливаем позицию, если есть
        if (PlayerPrefs.GetString("AnchorSaved", "") == "true")
        {
            Vector3 pos = new Vector3(
                PlayerPrefs.GetFloat("AnchorX"),
                PlayerPrefs.GetFloat("AnchorY"),
                PlayerPrefs.GetFloat("AnchorZ")
            );
            robotRoot.position = pos;
            isPlaced = true;
            placementIndicator.SetActive(false);
        }
    }
}
