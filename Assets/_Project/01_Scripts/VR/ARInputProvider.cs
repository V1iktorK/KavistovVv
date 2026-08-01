using UnityEngine;

// Заглушка для AR-ввода. Если нужна реальная логика – допишите позже.
public class ARInputProvider : InputProvider
{
    public override Vector3 GetPointerPosition() => Camera.main?.transform.position ?? Vector3.zero;
    public override Vector3 GetPointerDirection() => Camera.main?.transform.forward ?? Vector3.forward;
    
    public override bool GetSelectDown() => Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began;
    public override bool GetSelectHeld() => Input.touchCount > 0;
    public override bool GetGrabDown() => false;
    public override bool GetGrabHeld() => false;
    public override bool GetSwitchRobotDown() => false;
    public override bool GetRecordDown() => false;
    public override bool GetPlayDown() => false;
    
    public override Vector2 GetMovement() => Vector2.zero;
    public override Vector2 GetRotation() => Vector2.zero;
    
    public override bool IsAvailable() => Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer;
    
    // Метод для PoseSelector
    public bool ARRaycastFromTouch(Vector2 touchPos, out Vector3 hitPoint)
    {
        // Заглушка – замените на реальную логику с ARRaycastManager
        hitPoint = Vector3.zero;
        return false;
    }
}