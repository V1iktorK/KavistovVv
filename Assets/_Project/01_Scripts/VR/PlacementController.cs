using UnityEngine;

public class PlacementController : MonoBehaviour
{
    [Header("References")]
    public GameObject indicator;            
    public SpatialAnchorManager anchorManager;
    public LayerMask floorLayer;            

    private RaycastHit hit;
    private bool isPointingAtFloor = false;

    void Update()
    {
        // Берем позицию и направление из активного InputProvider
        Vector3 pointerPos = InputManager.Instance.PointerPosition;
        Vector3 pointerDir = InputManager.Instance.PointerDirection;

        // Пускаем луч
        if (Physics.Raycast(pointerPos, pointerDir, out hit, 10f, floorLayer))
        {
            isPointingAtFloor = true;
            indicator.SetActive(true);
            indicator.transform.position = hit.point;
            indicator.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
        }
        else
        {
            isPointingAtFloor = false;
            indicator.SetActive(false);
        }

        // Проверяем нажатие Select (ЛКМ в Keyboard/Mouse, Курок в VR/Геймпад)
        if (isPointingAtFloor && InputManager.Instance.SelectDown)
        {
            PlaceRobot();
        }
    }

    private void PlaceRobot()
    {
        if (anchorManager != null)
        {
            anchorManager.PlaceRobot(indicator.transform.position, indicator.transform.rotation);
            indicator.SetActive(false);
        }
    }
}