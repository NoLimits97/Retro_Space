using UnityEngine;

public class ShaderTimeUpdater : MonoBehaviour
{
    private Material mat;
    private int pulseSpeedID;
    private int pulseAmountID;
    private float customTime;

    [SerializeField] private float maxBrightness = 0.6f; // Яркость луча (от 0.0 до 1.0)


    void Start()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            // Берем материал. sharedMaterial использовать нельзя, 
            // так как нам нужна анимация только этого конкретного луча
            mat = sr.material;
        }

        // Кэшируем ID свойств шейдера, чтобы Vita не тратила время на поиск по строкам
        pulseSpeedID = Shader.PropertyToID("_PulseSpeed");
        pulseAmountID = Shader.PropertyToID("_PulseAmount");
    }

    void Update()
    {
        if (mat == null) return;

        // Считываем настройки прямо из инспектора вашего материала
        float speed = mat.GetFloat(pulseSpeedID);
        float amount = mat.GetFloat(pulseAmountID);

        if (speed <= 0 || amount <= 0) return;

        // Считаем собственное время
        customTime += Time.deltaTime * speed;

        // Считаем синусоиду мерцания
        float pulse = Mathf.Sin(customTime);
        float wave = (pulse + 1.0f) * 0.5f * amount;

        // Напрямую меняем прозрачность цвета в материале
        // Это гарантирует работу анимации даже если системный _Time завис
        Color c = mat.color;
        // Мы можем управлять итоговой яркостью через Tint Color
        mat.SetColor("_Color", new Color(c.r, c.g, c.b, maxBrightness - wave * maxBrightness));

    }
}
