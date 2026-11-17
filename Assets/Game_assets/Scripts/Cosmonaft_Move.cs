using UnityEngine;

public class Cosmonaft_Move : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeedY = 3f;
    public float moveSpeedX = 4f;

    [Header("Ссылка на инструмент пути")]
    public PerspectivePath movementPath;

    private SpriteRenderer spriteRenderer;

    private float currentProgress = 0f;
    private float sideOffset = 0f;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (movementPath == null)
        {
            movementPath = GameObject.FindObjectOfType<PerspectivePath>();
        }
    }

    void Update()
    {
        if (movementPath == null || movementPath.points.Count < 2) return;

        // 1. Движение ВГЛУБЬ
        float inputY = Input.GetAxis("Vertical");
        if (Mathf.Abs(inputY) > 0.1f)
        {
            currentProgress += inputY * moveSpeedY * Time.deltaTime;
            currentProgress = Mathf.Clamp(currentProgress, 0f, movementPath.points.Count - 1);
        }

        // Получаем асимметричные данные пути
        Vector3 centerPosition;
        float targetScale;
        float leftLimit;
        float rightLimit;
        movementPath.GetPathData(currentProgress, out centerPosition, out targetScale, out leftLimit, out rightLimit);

        // 2. Движение ВБОК
        float inputX = Input.GetAxis("Horizontal");
        if (Mathf.Abs(inputX) > 0.1f)
        {
            sideOffset += inputX * moveSpeedX * Time.deltaTime;

            if (inputX > 0.1f) spriteRenderer.flipX = false;
            else if (inputX < -0.1f) spriteRenderer.flipX = true;
        }

        // Зажимаем смещение персонажа: левый лимит уходит в минус, правый в плюс
        sideOffset = Mathf.Clamp(sideOffset, -leftLimit, rightLimit);

        // Итоговая позиция
        Vector3 finalPosition = centerPosition + Vector3.right * sideOffset;
        transform.position = finalPosition;

        transform.localScale = new Vector3(targetScale, targetScale, 1f);
    }
}
