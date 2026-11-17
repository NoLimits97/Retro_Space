using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class contoller_2d : MonoBehaviour
{

    [Header("Ссылки на компоненты")]
    public Path_build pathScript;

    [Header("Настройки скорости")]
    public float moveSpeed = 5f;
    private Path_Node immuneNode = null; // Нода, на которой мы только что появились и которую нужно игнорировать
                                         // Защита от зажатого стика при старт
    [Header("Визуальное смещение")]
    public float yOffset = 1f; 

    private Path_Node currentNode;
    private Path_Node nextNode;

    public bool showInteractionPrompt = false;
    public Path_Node activeTriggerNode = null;

    void Start()
    {
        if (pathScript == null || pathScript.transform.childCount < 2) return;

        Transform targetStartNodeTransform = null;

        // Проверяем: зафиксировал ли GameManager имя уникальной ноды перед выходом?
        if (!string.IsNullOrEmpty(GameManager.savedHubNodeID))
        {
            // Перебираем все дочерние ноды на линии и ищем ту, у которой совпадает uniqueNodeID
            for (int i = 0; i < pathScript.transform.childCount; i++)
            {
                Path_Node nodeScript = pathScript.transform.GetChild(i).GetComponent<Path_Node>();
                if (nodeScript != null && nodeScript.uniqueNodeID == GameManager.savedHubNodeID)
                {
                    targetStartNodeTransform = nodeScript.transform;
                    break; // Нашли нужную ноду, останавливаем цикл
                }
            }

            // Очищаем память GameManager после успешного нахождения
            GameManager.savedHubNodeID = "";
        }

        // Если уникальная нода возврата найдена на сцене
        if (targetStartNodeTransform != null)
        {
            Path_Node savedNodeScript = targetStartNodeTransform.GetComponent<Path_Node>();
            currentNode = savedNodeScript;
            nextNode = currentNode.nextNode != null ? currentNode.nextNode : currentNode;

            Vector3 startPos = targetStartNodeTransform.position;
            startPos.y += yOffset;
            transform.position = startPos;
        }
        else
        {
            // Если пришли первый раз — включаем обычный поиск отрезка по центру экрана
            InitializePath();
        }
    }


    // Метод публичный, чтобы нода могла вызвать его после телепортации космонавта
    public void InitializePath()
    {
        if (pathScript == null || pathScript.transform.childCount < 2) return;

        float closestDist = Mathf.Infinity;
        int bestLeftNodeIndex = 0;

        for (int i = 0; i < pathScript.transform.childCount - 1; i++)
        {
            Transform nodeA = pathScript.transform.GetChild(i);
            Transform nodeB = pathScript.transform.GetChild(i + 1);

            Vector3 midPoint = (nodeA.position + nodeB.position) / 2f;
            float dist = Vector2.Distance(transform.position, midPoint);

            if (dist < closestDist)
            {
                closestDist = dist;
                bestLeftNodeIndex = i;
            }
        }

        currentNode = pathScript.transform.GetChild(bestLeftNodeIndex).GetComponent<Path_Node>();
        nextNode = currentNode.nextNode;

        if (currentNode != null && nextNode != null)
        {
            Vector3 startPos = transform.position;
            startPos.y = Mathf.Lerp(currentNode.transform.position.y, nextNode.transform.position.y, 0.5f) + yOffset;
            transform.position = startPos;
        }
    }

    void Update()
    {

        // Считываем текущее положение стика (замените "Horizontal" на имя вашей оси Vita, если меняли)
        float moveInput = Input.GetAxis("Horizon");

        // ОЖИДАНИЕ НАЖАТИЯ E
        if (showInteractionPrompt && activeTriggerNode != null)
        {
            if (Input.GetButton("ButtonA"))
            {
                Path_Node nodeToInteract = activeTriggerNode;
                HidePrompt();

                // Передаем управление самой ноде и отдаем ей ссылку на себя (this)
                nodeToInteract.Interact(this);
                return;
            }
        }

        if (currentNode == null || nextNode == null) return;

        //float moveInput = Input.GetAxis("Horizon");
        if (Mathf.Abs(moveInput) < 0.01f) return;

        Path_Node targetNode = (moveInput > 0) ? nextNode : currentNode;
        Vector3 targetWithOffset = new Vector3(targetNode.transform.position.x, targetNode.transform.position.y + yOffset, transform.position.z);

        transform.position = Vector3.MoveTowards(transform.position, targetWithOffset, moveSpeed * Mathf.Abs(moveInput) * Time.deltaTime);

        if (Vector2.Distance(transform.position, targetWithOffset) < 0.05f)
        {
            transform.position = targetWithOffset;

            // Если точка, на которую мы наступили — это та самая точка, где мы появились, игнорируем триггер
            if (targetNode == immuneNode)
            {
                // Ничего не делаем, плашка не загорится
            }
            else
            {
                // Если мы подошли к любой ДРУГОЙ точке, значит мы уже отошли от двери,
                // сбрасываем иммунитет и разрешаем триггерам работать как обычно
                immuneNode = null;
                CheckForTriggerNode(targetNode);
            }


            if (moveInput > 0)
            {
                if (nextNode.nextNode != null)
                {
                    currentNode = nextNode;
                    nextNode = nextNode.nextNode;
                }
            }
            else
            {
                int currentChildIndex = currentNode.transform.GetSiblingIndex();
                if (currentChildIndex > 0)
                {
                    Transform prevTransform = currentNode.transform.parent.GetChild(currentChildIndex - 1);
                    Path_Node prevNode = prevTransform.GetComponent<Path_Node>();
                    if (prevNode != null)
                    {
                        nextNode = currentNode;
                        currentNode = prevNode;
                    }
                }
            }
        }
    }

    void CheckForTriggerNode(Path_Node node)
    {
        // Теперь мы реагируем на ЛЮБОЙ тип триггера, кроме None
        if (node.isTriggerNode && node.triggerType != Path_Node.TriggerObjectType.None)
        {
            showInteractionPrompt = true;
            activeTriggerNode = node;
        }
        else
        {
            HidePrompt();
        }
    }

    void LateUpdate()
    {
        if (activeTriggerNode != null)
        {
            float distanceX = Mathf.Abs(transform.position.x - activeTriggerNode.transform.position.x);
            if (distanceX > 0.6f) HidePrompt();
        }
    }
    

    // Этот метод вызывается из Path_Node в момент телепортации
    public void ActivateTriggerImmunity(Path_Node spawnedNode)
    {
        immuneNode = spawnedNode;
    }

    // Делаем метод публичным, чтобы его мог вызвать скрипт ноды
    public void HidePrompt()
    {
        showInteractionPrompt = false;
        activeTriggerNode = null;
    }


}
