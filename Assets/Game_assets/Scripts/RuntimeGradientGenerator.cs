using UnityEngine;

public class RuntimeGradientGenerator : MonoBehaviour
{
    void Awake()
    {
        // Создаем квадратную текстуру 256x256
        Texture2D texture = new Texture2D(256, 256, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        // Заполняем пиксели по сетке
        for (int x = 0; x < 256; x++)
        {
            // Градиент от центра к краям по горизонтали (X)
            float normX = (float)x / 255f;
            float fadeX = 1f - Mathf.Abs(normX - 0.5f) * 2f;
            fadeX = Mathf.SmoothStep(0f, 1f, fadeX); // Смягчаем боковые края

            for (int y = 0; y < 256; y++)
            {
                // Градиент сверху вниз по вертикали (Y)
                float fadeY = (float)y / 255f;

                // Перемножаем обе прозрачности
                float finalAlpha = fadeX * fadeY;

                texture.SetPixel(x, y, new Color(1f, 1f, 1f, finalAlpha));
            }
        }
        texture.Apply();

        // Создаем полноценный 2D-Спрайт из квадратной текстуры
        Sprite gradientSprite = Sprite.Create(texture, new Rect(0, 0, 256, 256), new Vector2(0.5f, 0.5f));

        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = gradientSprite;
        }
    }
}
