using UnityEngine;

/// <summary>
/// Управляет якорем размещения робота: сохраняет и восстанавливает
/// и позицию, и поворот (PlayerPrefs), чтобы после перезапуска
/// виртуальный робот совпадал с реальным.
/// </summary>
public class SpatialAnchorManager : MonoBehaviour
{
    public static SpatialAnchorManager Instance;

    [Header("Placement")]
    public Transform robotRoot;
    public GameObject placementIndicator;

    private const string KeyAnchorSaved = "AnchorSaved";
    private const string KeyAnchorX = "AnchorX";
    private const string KeyAnchorY = "AnchorY";
    private const string KeyAnchorZ = "AnchorZ";
    private const string KeyAnchorQx = "AnchorQx";
    private const string KeyAnchorQy = "AnchorQy";
    private const string KeyAnchorQz = "AnchorQz";
    private const string KeyAnchorQw = "AnchorQw";

    void Awake() => Instance = this;

    public void PlaceRobot(Vector3 position, Quaternion rotation)
    {
        if (robotRoot != null)
        {
            robotRoot.position = position;
            robotRoot.rotation = rotation;
        }

        if (placementIndicator != null)
        {
            placementIndicator.SetActive(false);
        }

        PlayerPrefs.SetInt(KeyAnchorSaved, 1);
        PlayerPrefs.SetFloat(KeyAnchorX, position.x);
        PlayerPrefs.SetFloat(KeyAnchorY, position.y);
        PlayerPrefs.SetFloat(KeyAnchorZ, position.z);
        PlayerPrefs.SetFloat(KeyAnchorQx, rotation.x);
        PlayerPrefs.SetFloat(KeyAnchorQy, rotation.y);
        PlayerPrefs.SetFloat(KeyAnchorQz, rotation.z);
        PlayerPrefs.SetFloat(KeyAnchorQw, rotation.w);
        PlayerPrefs.Save();

        Debug.Log("[SpatialAnchor] Робот размещён: " + position + ", " + rotation.eulerAngles);
    }

    public void Recalibrate()
    {
        PlayerPrefs.DeleteKey(KeyAnchorSaved);
        PlayerPrefs.Save();

        if (placementIndicator != null)
        {
            placementIndicator.SetActive(true);
        }
    }

    void Start()
    {
        RestoreSavedAnchor();
    }

    /// <summary>Восстанавливает сохранённый якорь (позиция + поворот).</summary>
    private void RestoreSavedAnchor()
    {
        if (robotRoot == null || PlayerPrefs.GetInt(KeyAnchorSaved, 0) != 1)
        {
            return;
        }

        Vector3 position = new Vector3(
            PlayerPrefs.GetFloat(KeyAnchorX),
            PlayerPrefs.GetFloat(KeyAnchorY),
            PlayerPrefs.GetFloat(KeyAnchorZ));

        Quaternion rotation = new Quaternion(
            PlayerPrefs.GetFloat(KeyAnchorQx),
            PlayerPrefs.GetFloat(KeyAnchorQy),
            PlayerPrefs.GetFloat(KeyAnchorQz),
            PlayerPrefs.GetFloat(KeyAnchorQw));

        robotRoot.position = position;
        robotRoot.rotation = rotation;

        if (placementIndicator != null)
        {
            placementIndicator.SetActive(false);
        }
    }
}