using UnityEngine;

public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance;

    private void Awake()
    {
        Instance = this;
    }

    // Убрали IEnumerator и корутины, теперь переход происходит мгновенно в один кадр
    public void SwitchRoom(Transform player, Vector3 newPosition, Camera targetCamera, System.Action onComplete)
    {
        // 1. Телепортируем космонавта
        player.position = newPosition;

        // 2. Логика переключения камер
        if (targetCamera != null)
        {
            // Находим текущую активную камеру на сцене и выключаем её
            Camera currentActiveCamera = Camera.main;
            if (currentActiveCamera != null && currentActiveCamera != targetCamera)
            {
                currentActiveCamera.gameObject.SetActive(false);
            }

            // Включаем новую камеру целевой комнаты
            targetCamera.gameObject.SetActive(true);
        }

        // 3. Выполняем перенастройку путей у космонавта
        if (onComplete != null)
        {
            onComplete.Invoke();
        }
    }
}
