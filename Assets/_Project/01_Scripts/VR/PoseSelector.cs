using UnityEngine;

public class PoseSelector : MonoBehaviour
{
    [Header("Visuals")]
    public LineRenderer laserPointer;
    public TargetMarker targetMarker;
    public LayerMask selectableLayers;
    public float maxDistance = 15f;
    
    void Update()
    {
        if (InputManager.Instance == null || InputManager.Instance.ActiveProvider == null) return;
        
        var input = InputManager.Instance;
        
        if (input.ActiveProvider is ARInputProvider arInput)
        {
            HandleARInput(arInput);
            return;
        }
        
        HandleStandardInput(input);
    }
    
    void HandleStandardInput(InputManager input)
    {
        Vector3 origin = input.PointerPosition;
        Vector3 dir = input.PointerDirection;
        
        if (laserPointer != null)
        {
            laserPointer.SetPosition(0, origin);
        }
        
        if (Physics.Raycast(origin, dir, out RaycastHit hit, maxDistance, selectableLayers))
        {
            if (laserPointer != null) laserPointer.SetPosition(1, hit.point);
            if (targetMarker != null)
            {
                targetMarker.transform.position = hit.point;
                targetMarker.gameObject.SetActive(true);
            }
            
            if (input.SelectDown)
                RobotSelector.Instance?.GetActiveRobot()?.SetTarget(hit.point);
        }
        else
        {
            if (laserPointer != null) laserPointer.SetPosition(1, origin + dir * maxDistance);
            if (targetMarker != null) targetMarker.gameObject.SetActive(false);
        }
    }
    
    void HandleARInput(ARInputProvider arInput)
    {
        if (Input.touchCount == 0) 
        {
            if (targetMarker != null) targetMarker.gameObject.SetActive(false);
            return;
        }
        
        Vector2 touchPos = Input.GetTouch(0).position;
        
        if (arInput.ARRaycastFromTouch(touchPos, out Vector3 hitPoint))
        {
            if (laserPointer != null)
            {
                laserPointer.SetPosition(0, arInput.GetPointerPosition());
                laserPointer.SetPosition(1, hitPoint);
            }
            if (targetMarker != null)
            {
                targetMarker.transform.position = hitPoint;
                targetMarker.gameObject.SetActive(true);
            }
            if (InputManager.Instance.SelectDown)
                RobotSelector.Instance?.GetActiveRobot()?.SetTarget(hitPoint);
        }
        else
        {
            if (targetMarker != null) targetMarker.gameObject.SetActive(false);
        }
    }
}
