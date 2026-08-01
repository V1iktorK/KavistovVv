using UnityEngine;

public abstract class InputProvider : MonoBehaviour
{
    public abstract Vector3 GetPointerPosition();
    public abstract Vector3 GetPointerDirection();
    
    public abstract bool GetSelectDown();
    public virtual bool GetSelectHeld() => false;
    public abstract bool GetGrabDown();
    public virtual bool GetGrabHeld() => false;
    public abstract bool GetSwitchRobotDown();
    public abstract bool GetRecordDown();
    public abstract bool GetPlayDown();
    
    public abstract Vector2 GetMovement();
    public abstract Vector2 GetRotation();
    public abstract bool IsAvailable();
}