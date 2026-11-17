using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{

    [Header("Настройки баланса (Видны в Инспекторе)")]
    [SerializeField] int startHealth = 5;      // Сколько жизней давать при старте
    [SerializeField] float max_speed = 5f;     // Предельная скорость
    [SerializeField] float acceleration = 50f; // Сила разгона
    [SerializeField] float friction = 10f;     // Плавность торможения

    [SerializeField] float slowTimer = 0f;
    [SerializeField] bool IsSlowed
    {
        get { return slowTimer > 0f; }
    }

    [SerializeField] float currentSlowMultiplier = 1f;




    int currentHealth;
    float velocityX = 0f;
    float velocityY = 0f;
    float gameTime = 0f;

    public float time = 0;

    [Header("Границы экрана")]
    [SerializeField] float xMin = -8.5f;
    [SerializeField] float xMax = 8.5f;
    [SerializeField] float yMin = -4.5f;
    [SerializeField] float yMax = 4.5f;

    [Header("Настройки неуязвимости")]
    [SerializeField] float invulnerabilityDuration = 1.5f; // Сколько секунд длится защита
    [SerializeField] float blinkInterval = 0.15f;          // Как часто мигает спрайт
    [SerializeField] SpriteRenderer spriteRenderer;         // Ссылка на картинку ракеты

    bool isInvulnerable = false; // Флаг: неуязвим ли корабль прямо сейчас

    [Header("Dash Settings")]
    public float dashSpeed = 20f;       // Скорость во время рывка
    public float dashDuration = 0.2f;    // Сколько секунд длится рывок
    public float dashCooldown = 2f;     // Перезарядка (КД) рывка в секундах

    private float dashTimer = 0f;       // Таймер длительности текущего рывка
    private float cooldownTimer = 0f;   // Таймер перезарядки
    private Vector3 dashDirection;      // Направление, куда летит игрок
    private bool isDashing = false;     // Флаг: находится ли игрок в рывке прямо сейчас


    void Start()
    {
        ResetPlayerState();
        currentHealth = startHealth;

        // Автоматически находим SpriteRenderer на объекте ракеты
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }
    }

    // НОВЫЙ МЕТОД: Полный сброс состояния ракеты для бесшовного перезапуска
    public void ResetPlayerState()
    {
        currentHealth = startHealth;

        // Обнуляем скорости, чтобы ракета не улетала по инерции из прошлого раунда
        velocityX = 0f;
        velocityY = 0f;

        isInvulnerable = false;

        // Возвращаем видимость спрайту на случай, если игра завершилась в момент мигания
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
        }
    }

    void Update()
    {
        // Уменьшаем таймер перезарядки рывка каждую секунду
        if (cooldownTimer > 0)
        {
            cooldownTimer -= Time.deltaTime;
        }

        // --- ЛОГИКА РЫВКА ---
        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0)
            {
                isDashing = false; // Рывок закончился, возвращаем управление
                velocityX = 0f;    // Обнуляем скорость, чтобы не было заноса
                velocityY = 0f;
            }
            else
            {
                // Двигаем ракету в направлении рывка (рывок НЕ замедляется пылью)
                transform.position += dashDirection * dashSpeed * Time.deltaTime;

                // ОГРАНИЧЕНИЕ ГРАНИЦ ДЛЯ РЫВКА (чтобы не вылететь за экран)
                float dashClampedX = Mathf.Clamp(transform.position.x, xMin, xMax);
                float dashClampedY = Mathf.Clamp(transform.position.y, yMin, yMax);
                transform.position = new Vector3(dashClampedX, dashClampedY, transform.position.z);

                return; // Пропускаем обычное движение в этом кадре
            }
        }

        // --- 1. СБОР ВВОДА ---
        float stickX = Input.GetAxisRaw("Horizon");
        float stickY = Input.GetAxisRaw("Vert");
        bool right_bumper = Input.GetButtonDown("ButtonA"); // Срабатывает один раз при нажатии

        float dpadX = Input.GetAxisRaw("DpadX");
        float dpadY = Input.GetAxisRaw("DpadY");

        float inputX = Mathf.Clamp(stickX + dpadX, -1f, 1f);
        float inputY = Mathf.Clamp(stickY + dpadY, -1f, 1f);

        // --- АКТИВАЦИЯ РЫВКА ---
        if (right_bumper && cooldownTimer <= 0 && (Mathf.Abs(inputX) > 0.15f || Mathf.Abs(inputY) > 0.15f))
        {
            isDashing = true;
            dashTimer = dashDuration;
            cooldownTimer = dashCooldown;

            dashDirection = new Vector3(inputX, inputY, 0f).normalized;

            return; // Сразу выходим из кадра
        }

        // --- 2. РАЗГОН И ТРЕНИЕ ПО ОСИ X ---
        if (Mathf.Abs(inputX) > 0.15f)
        {
            velocityX += inputX * acceleration * Time.deltaTime;
        }
        else
        {
            if (velocityX > 0)
            {
                velocityX -= friction * Time.deltaTime;
                if (velocityX < 0) velocityX = 0;
            }
            else if (velocityX < 0)
            {
                velocityX += friction * Time.deltaTime;
                if (velocityX > 0) velocityX = 0;
            }
        }

        // --- 3. РАЗГОН И ТРЕНИЕ ПО ОСИ Y ---
        if (Mathf.Abs(inputY) > 0.15f)
        {
            velocityY += inputY * acceleration * Time.deltaTime;
        }
        else
        {
            if (velocityY > 0)
            {
                velocityY -= friction * Time.deltaTime;
                if (velocityY < 0) velocityY = 0;
            }
            else if (velocityY < 0)
            {
                velocityY += friction * Time.deltaTime;
                if (velocityY > 0) velocityY = 0;
            }
        }

        // --- 4. ДИНАМИЧЕСКОЕ ОГРАНИЧЕНИЕ МАКСИМАЛЬНОЙ СКОРОСТИ ---
        // Вычисляем текущий предел скорости с учетом дебаффа (например: 5f * 0.5f = 2.5f)
        float currentMaxSpeed = max_speed * currentSlowMultiplier;

        if (velocityX > currentMaxSpeed) velocityX = currentMaxSpeed;
        if (velocityX < -currentMaxSpeed) velocityX = -currentMaxSpeed;

        if (velocityY > currentMaxSpeed) velocityY = currentMaxSpeed;
        if (velocityY < -currentMaxSpeed) velocityY = -currentMaxSpeed;

        // --- 5. ОБЫЧНОЕ ПЕРЕМЕЩЕНИЕ РАКЕТЫ ---
        transform.position += new Vector3(velocityX, velocityY, 0f) * Time.deltaTime;

        // --- 6. ОГРАНИЧЕНИЕ ГРАНИЦ ЭКРАНА ---
        float clampedX = Mathf.Clamp(transform.position.x, xMin, xMax);
        float clampedY = Mathf.Clamp(transform.position.y, yMin, yMax);
        transform.position = new Vector3(clampedX, clampedY, transform.position.z);
    }



    public void TakeDamage(int damage)
    {
        currentHealth -= damage;

        Debug.Log("Бабах! Осталось жизней: " + currentHealth);

        if (currentHealth <= 0)
        {
            Debug.Log("Корабль уничтожен! Game Over.");

            // Ищем GameManager на сцене и вызываем возврат в меню
            GameManager manager = FindObjectOfType<GameManager>();
            if (manager != null)
            {
                manager.GameOver();
            }
            else
            {
                // Запасной вариант, если менеджер не найден
                gameObject.SetActive(false);
            }
        }
        else
        {
            StartCoroutine(InvulnerabilityRoutine());
        }
    }



    // Корутина для реализации мигания и таймера неуязвимости
    private System.Collections.IEnumerator InvulnerabilityRoutine()
    {
        isInvulnerable = true;
        float elapsed = 0f;

        // Мигаем спрайтом, пока идет время неуязвимости
        while (elapsed < invulnerabilityDuration)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = !spriteRenderer.enabled; // Инвертируем видимость спрайта
            }

            yield return new WaitForSeconds(blinkInterval);
            elapsed += blinkInterval;
        }

        // Гарантируем, что после завершения корутины спрайт останется видимым
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
        }

        isInvulnerable = false;
    }

    // Автоматическая обработка столкновений через триггер Unity
    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Проверяем тег метеорита и состояние уязвимости
        if (collision.CompareTag("Meteor") && !isInvulnerable)
        {
            TakeDamage(1);

            // Уничтожаем метеорит при таране, чтобы он не наносил урон повторно
            collision.gameObject.SetActive(false);
        }
    }

    public void ApplySlow(float amount, float duration)
    {
        currentSlowMultiplier = 1f - amount;
        slowTimer = duration;
    }

    // Метод включает замедление
    public void SetSlow(float amount)
    {
        currentSlowMultiplier = 1f - amount;
    }

    // Метод мгновенно возвращает скорость в норму
    public void ResetSlow()
    {
        currentSlowMultiplier = 1f;
    }

}
