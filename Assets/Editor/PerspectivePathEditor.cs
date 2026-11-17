using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(PerspectivePath))]
public class PerspectivePathEditor : Editor
{
    private PerspectivePath pathScript;

    private void OnEnable()
    {
        pathScript = (PerspectivePath)target;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(10);
        GUI.backgroundColor = Color.cyan;

        if (GUILayout.Button("Добавить точку в конец пути", GUILayout.Height(30)))
        {
            Undo.RecordObject(pathScript, "Add Path Point");

            PerspectivePath.PathPoint newPoint = new PerspectivePath.PathPoint();
            if (pathScript.points.Count > 0)
            {
                newPoint.position = pathScript.points[pathScript.points.Count - 1].position + new Vector3(0f, 0.5f, 0);
                newPoint.characterScale = pathScript.points[pathScript.points.Count - 1].characterScale;
            }
            else
            {
                newPoint.position = Vector3.zero;
                newPoint.characterScale = 1f;
            }

            pathScript.points.Add(newPoint);
            EditorUtility.SetDirty(pathScript);
        }

        if (pathScript.points.Count > 0 && GUILayout.Button("Удалить последнюю точку", GUILayout.Height(25)))
        {
            Undo.RecordObject(pathScript, "Remove Path Point");
            pathScript.points.RemoveAt(pathScript.points.Count - 1);
            EditorUtility.SetDirty(pathScript);
        }
    }

    private void OnSceneGUI()
    {
        if (pathScript.points == null || pathScript.points.Count == 0) return;

        Transform pathTransform = pathScript.transform;

        // Рисуем границы дорожки на основе кривой прямо в окне Scene для предпросмотра
        int итерацийОтрезков = 20;
        Vector3 previousLeft = Vector3.zero;
        Vector3 previousRight = Vector3.zero;

        for (int i = 0; i <= итерацийОтрезков; i++)
        {
            float t = (float)i / итерацийОтрезков;
            float progress = t * (pathScript.points.Count - 1);

            Vector3 center;
            float scale;
            float leftW;   // Создаем переменную для левой стороны
            float rightW;  // Создаем переменную для правой стороны

            // Передаем обе переменные в обновленный метод
            pathScript.GetPathData(progress, out center, out scale, out leftW, out rightW);

            // Считаем позиции краев дороги с учетом левой и правой ширины
            Vector3 leftPos = center - Vector3.right * leftW;
            Vector3 rightPos = center + Vector3.right * rightW;
    

            Handles.color = new Color(0f, 0.5f, 1f, 0.4f); // Полупрозрачный синий цвет краев дороги
            Handles.DrawLine(leftPos, rightPos);

            if (i > 0)
            {
                Handles.color = Color.blue;
                Handles.DrawLine(previousLeft, leftPos);
                Handles.DrawLine(previousRight, rightPos);
            }

            previousLeft = leftPos;
            previousRight = rightPos;
        }

        // Отрисовка стрелок перемещения для центральных точек
        for (int i = 0; i < pathScript.points.Count; i++)
        {
            PerspectivePath.PathPoint point = pathScript.points[i];
            Vector3 worldPos = pathTransform.TransformPoint(point.position);

            GUIStyle labelStyle = new GUIStyle();
            labelStyle.normal.textColor = Color.yellow;
            labelStyle.fontSize = 11;

            Handles.Label(worldPos + new Vector3(0, 0.1f, 0), string.Format("Точка {0} (Scale: {1:F2})", i, point.characterScale), labelStyle);

            EditorGUI.BeginChangeCheck();
            Vector3 newWorldPos = Handles.PositionHandle(worldPos, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(pathScript, "Move Path Point");
                point.position = pathTransform.InverseTransformPoint(newWorldPos);
                pathScript.points[i] = point;
                EditorUtility.SetDirty(pathScript);
            }
        }
    }
}
