using UnityEngine;

public class VRHandTracker : MonoBehaviour
{
    [Header("Visualization")]
    public LineRenderer laserLine;
    public Transform handProxy;
    
    void Update()
    {
        if (InputManager.Instance == null || InputManager.Instance.ActiveProvider == null) return;
        
        var input = InputManager.Instance.ActiveProvider;
        
        if (handProxy != null)
        {
            handProxy.position = input.GetPointerPosition();
            handProxy.rotation = Quaternion.LookRotation(input.GetPointerDirection());
        }
        
        if (laserLine != null)
        {
            laserLine.SetPosition(0, input.GetPointerPosition());
            laserLine.SetPosition(1, input.GetPointerPosition() + input.GetPointerDirection() * 2f);
        }
    }
}
