using UnityEngine;
using System.Collections.Generic;

public class PerspectivePath : MonoBehaviour
{
    [System.Serializable]
    public struct PathPoint
    {
        public Vector3 position;       // Центральная позиция точки
        public float characterScale;   // Масштаб персонажа в этой точке
    }

    public List<PathPoint> points = new List<PathPoint>();
    public Color pathColor = Color.green;

    [Header("Асимметричная ширина")]
    [Tooltip("На сколько метров персонаж может уйти ВЛЕВО от центра (0 - начало пути, 1 - конец)")]
    public AnimationCurve leftWidthCurve = AnimationCurve.Linear(0f, 2f, 1f, 0.5f);

    [Tooltip("На сколько метров персонаж может уйти ВПРАВО от центра (0 - начало пути, 1 - конец)")]
    public AnimationCurve rightWidthCurve = AnimationCurve.Linear(0f, 2f, 1f, 0.5f);

    private void OnDrawGizmos()
    {
        if (points == null || points.Count < 2) return;

        Gizmos.color = pathColor;
        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector3 p1 = transform.TransformPoint(points[i].position);
            Vector3 p2 = transform.TransformPoint(points[i + 1].position);
            Gizmos.DrawLine(p1, p2);
        }
    }

    // Получение всех данных в текущей точке прогресса
    public void GetPathData(float progress, out Vector3 centerPos, out float scale, out float leftWidth, out float rightWidth)
    {
        centerPos = Vector3.zero;
        scale = 1f;
        leftWidth = 1f;
        rightWidth = 1f;

        if (points == null || points.Count == 0) return;

        int segmentIndex = Mathf.FloorToInt(progress);
        float segmentPercent = progress - segmentIndex;

        if (segmentIndex >= points.Count - 1)
        {
            segmentIndex = points.Count - 2;
            segmentPercent = 1f;
        }

        // Позиция центра
        Vector3 pA = transform.TransformPoint(points[segmentIndex].position);
        Vector3 pB = transform.TransformPoint(points[segmentIndex + 1].position);
        centerPos = Vector3.Lerp(pA, pB, segmentPercent);

        // Масштаб персонажа
        float scaleA = points[segmentIndex].characterScale;
        float scaleB = points[segmentIndex + 1].characterScale;
        scale = Mathf.Lerp(scaleA, scaleB, segmentPercent);

        // Считываем независимую ширину сторон из соответствующих кривых
        float totalPercent = progress / (points.Count - 1);
        leftWidth = leftWidthCurve.Evaluate(totalPercent);
        rightWidth = rightWidthCurve.Evaluate(totalPercent);
    }
}
