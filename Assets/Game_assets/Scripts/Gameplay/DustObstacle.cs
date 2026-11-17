using System.Collections;
using UnityEngine;

public class DustObstacle : MonoBehaviour
{
    [Header("Dust Mechanics")]
    [SerializeField] private float slowAmount = 0.5f; // На сколько процентов замедлить (0.5 = на 50%)

    // Пока игрок НАХОДИТСЯ внутри зоны пыли
    private void OnTriggerStay2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                player.SetSlow(slowAmount); // Передаем замедление
            }
        }
    }

    // Как только игрок ВЫЛЕТАЕТ из зоны пыли
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                player.ResetSlow(); // Убираем замедление
            }
        }
    }
}
