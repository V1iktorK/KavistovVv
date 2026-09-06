using UnityEngine;

public class KeyboardController : MonoBehaviour
{
    public float moveSpeed = 2f;
    public float rotateSpeed = 90f;

    void Update()
    {
        // Получаем вектор движения (WASD или левый стик) из вашей системы ввода
        Vector2 moveInput = InputManager.Instance.Movement; // вместо GetMovement()
        
        // Поворот робота (влево/вправо)
        transform.Rotate(Vector3.up, moveInput.x * rotateSpeed * Time.deltaTime);
        
        // Движение вперед/назад
        transform.Translate(Vector3.forward * moveInput.y * moveSpeed * Time.deltaTime);

    }
}