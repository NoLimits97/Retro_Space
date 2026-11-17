using UnityEngine;

public class Mover : MonoBehaviour
{
    [Header("Movement Settings")]
    // Скорость движения влево
    [SerializeField] float moveSpeed = 4f;

    // Граница выключения за экраном (теперь настраивается в инспекторе)
    [SerializeField] float destroyX = -12f;

    void Update()
    {
        // 1. Кешируем позицию на стек (один вызов к нативному transform вместо двух)
        Vector3 currentPosition = transform.position;

        // 2. Двигаем влево через обычную математику float (без вызова Vector3.left)
        currentPosition.x -= moveSpeed * Time.deltaTime;

        // 3. Проверяем границу экрана
        if (currentPosition.x < destroyX)
        {
            gameObject.SetActive(false);
            return; // Сразу выходим, чтобы не применять позицию к выключенному объекту
        }

        // 4. Записываем измененную позицию обратно в Unity
        transform.position = currentPosition;
    }
}
