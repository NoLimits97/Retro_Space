using UnityEngine;

public class Path_Node : MonoBehaviour
{
    // 1. СПИСОК ВСЕХ ВОЗМОЖНЫХ ТИПОВ ТОЧЕК НА ДОРОЖКЕ
    public enum TriggerObjectType
    {
        None,                 // Обычная нода (космонавт просто идет мимо)
        NextRoomTransition,   // Переход в другую комнату в этой же сцене (смена камер)
        NextSceneTransition,  // Переход на другую сцену (уход в полет, в меню и т.д.)
        Gramophone            // Музыкальный автомат (заглушка)
    }

    [Header("Настройки уникальности (Для возврата из меню)")]
    [Tooltip("Укажите имя этой комнаты (например: Pilot_Seat). Если это обычный путь — оставьте пустым.")]
    public string uniqueNodeID = "";

    [Header("Настройки триггера")]
    [Tooltip("Поставьте галочку, если точка должна реагировать на кнопку взаимодействия 'E'")]
    public bool isTriggerNode = false;
    [Tooltip("Что именно находится в этой точке пути")]
    public TriggerObjectType triggerType = TriggerObjectType.None;

    [Tooltip("Что написать в подсказке интерфейса, например: 'сесть за штурвал', 'войти в отсек'")]
    public string interactionPrompt = "взаимодействовать";

    [Header("Настройки перехода комнат (Внутри сцены)")]
    [Tooltip("Точка на новой дорожке, куда телепортируется космонавт")]
    public Path_Node targetRoomNode;
    [Tooltip("Камера новой комнаты, которая включится при телепортации")]
    public Camera targetRoomCamera;

    [Header("Настройки перехода на другую сцену")]
    [Tooltip("Точное имя сцены из Build Settings, которую нужно загрузить (например: infinity или Hub_work)")]
    public string targetSceneName = "";

    [HideInInspector]
    public Path_Node nextNode; // Эту ссылку автоматически заполняет наш скрипт генерации редактора


    // 2. ГЛАВНЫЙ МЕТОД: Срабатывает, когда космонавт стоит на точке и нажимает Е
    public void Interact(contoller_2d player)
    {
        switch (triggerType)
        {
            case TriggerObjectType.NextRoomTransition:
                ExecuteRoomTransition(player);
                break;

            case TriggerObjectType.NextSceneTransition:
                ExecuteSceneTransition();
                break;

            case TriggerObjectType.Gramophone:
                ExecuteGramophoneInteraction(player);
                break;
        }
    }

    // ЛОГИКА А: Переход между комнатами (ТВОЙ РАБОЧИЙ МЕТОД БЕЗ ИЗМЕНЕНИЙ)
    private void ExecuteRoomTransition(contoller_2d player)
    {
        if (targetRoomNode == null) return;

        player.HidePrompt();

        // Переключаем камеру
        if (targetRoomCamera != null)
        {
            Camera currentCamera = Camera.main;
            if (currentCamera != null) currentCamera.gameObject.SetActive(false);
            targetRoomCamera.gameObject.SetActive(true);
        }

        // Переносим космонавта на рельсы нового пути
        Path_build newPath = targetRoomNode.transform.parent.GetComponent<Path_build>();
        player.pathScript = newPath;
        player.transform.position = targetRoomNode.transform.position;

        // Пересобираем рельсы движения
        player.InitializePath();

        player.ActivateTriggerImmunity(targetRoomNode);
    }
    
    // ЛОГИКА Б: Переход на другую цену (УПРОЩЕННЫЙ И УНИВЕРСАЛЬНЫЙ МЕТОД)
    private void ExecuteSceneTransition()
    {
        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogWarning("[Переход] На объекте " + gameObject.name + " не указано имя целевой сцены (targetSceneName)!");
            return;
        }

        // Передаем кастомное имя ноды в GameManager перед тем, как выгрузить сцену корабля
        if (!string.IsNullOrEmpty(uniqueNodeID))
        {
            GameManager.savedHubNodeID = uniqueNodeID;
        }
        else
        {
            GameManager.savedHubNodeID = gameObject.name;
        }

        Debug.Log("Сохранили ноду: " + GameManager.savedHubNodeID + ". Загружаем сцену: " + targetSceneName + "...");

        // Загружаем сцену, которую настроили в инспекторе
        UnityEngine.SceneManagement.SceneManager.LoadScene(targetSceneName);
    }


    // ЛОГИКА В: Музыкальный автомат (ЗАГЛУШКА)
    private void ExecuteGramophoneInteraction(contoller_2d player)
    {
        Debug.Log("[ЗАГЛУШКА]: Включаем граммофон на корабле...");
    }

    // Визуальное отображение линий и сфер в окне Scene
    void OnDrawGizmos()
    {
        Gizmos.color = isTriggerNode ? Color.yellow : Color.green;
        Gizmos.DrawSphere(transform.position, 0.15f);

        if (nextNode != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, nextNode.transform.position);
        }
    }
}
