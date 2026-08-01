using UnityEngine;        

public class MRInputProvider : InputProvider
{
    [Header("MR / Passthrough")]
    public OVRPassthroughLayer passthroughLayer;
    
    [Header("Hand Tracking")]
    public OVRHand rightHand;
    public OVRSkeleton rightSkeleton;
    
    private Transform indexTip;
    private bool wasPinching = false;
    
    void Start()
    {
        // Включаем прозрачность (видим реальный мир)
        if (passthroughLayer != null)
        {
            passthroughLayer.enabled = true;
            passthroughLayer.textureOpacity = SettingsData.Instance?.passthroughOpacity ?? 0.8f;
        }
        
        // Находим кончик указательного пальца правой руки
        if (rightSkeleton != null)
        {
            foreach (var bone in rightSkeleton.Bones)
            {
                if (bone.Id == OVRSkeleton.BoneId.Hand_IndexTip)
                {
                    indexTip = bone.Transform;
                    break;
                }
            }
        }
    }
    
    public override Vector3 GetPointerPosition()
    {
        // Луч из кончика пальца, если рука видна
        if (indexTip != null && rightHand.IsTracked)
            return indexTip.position;
        
        // Fallback: луч из головы (head-gaze)
        return Camera.main.transform.position;
    }
    
    public override Vector3 GetPointerDirection()
    {
        if (indexTip != null && rightHand.IsTracked)
            return indexTip.forward;
        
        return Camera.main.transform.forward;
    }
    
    public override bool GetSelectDown()
    {
        // Pinch (щипок) — большой + указательный палец
        if (rightHand == null) return false;
        
        bool pinching = rightHand.GetFingerIsPinching(OVRHand.HandFinger.Index);
        bool result = pinching && !wasPinching;
        wasPinching = pinching;
        return result;
    }
    
    public override bool GetSelectHeld()
    {
        return rightHand != null && rightHand.GetFingerIsPinching(OVRHand.HandFinger.Index);
    }
    
    public override bool GetGrabDown()
    {
        // Grab — сжатие среднего пальца с большим (упрощённо)
        if (rightHand == null) return false;
        return rightHand.GetFingerIsPinching(OVRHand.HandFinger.Middle);
    }
    
    public override bool GetGrabHeld()
    {
        return rightHand != null && rightHand.GetFingerIsPinching(OVRHand.HandFinger.Middle);
    }
    
    public override bool GetSwitchRobotDown()
    {
        // A на правом контроллере (если контроллеры тоже используются)
        // Или голосовая команда — пока fallback на кнопку
        return OVRInput.GetDown(OVRInput.Button.One);
    }
    
    public override bool GetRecordDown() => OVRInput.GetDown(OVRInput.Button.Two);
    public override bool GetPlayDown() => OVRInput.GetDown(OVRInput.Button.Three);
    
    public override Vector2 GetMovement()
    {
        // Левый стик или слайд пальцами
        return OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);
    }
    
    public override Vector2 GetRotation()
    {
        return OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick);
    }
    
    public override bool IsAvailable()
    {
        // Проверяем, что это Quest с passthrough
        #if UNITY_ANDROID && !UNITY_EDITOR
            return OVRPlugin.productName.Contains("Quest");
        #else
            return true; // В редакторе доступно для теста
        #endif
    }
}
