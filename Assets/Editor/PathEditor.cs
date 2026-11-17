using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(Path_build))]
public class PathEditor : Editor
{
    void OnSceneGUI()
    {
        Path_build pathScript = (Path_build)target;
        Event currentEvent = Event.current;

        // Поток пересчета связей (запускается при кликах или перемещении)
        bool needRestructure = false;

        // --- БЛОК 1: ИНТЕРАКТИВНЫЕ ТОЧКИ ДЛЯ НАСТРОЙКИ КООРДИНАТ ---
        for (int i = 0; i < pathScript.transform.childCount; i++)
        {
            Transform childTransform = pathScript.transform.GetChild(i);
            Path_Node nodeScript = childTransform.GetComponent<Path_Node>();

            if (nodeScript != null)
            {
                Handles.color = Color.green;

                EditorGUI.BeginChangeCheck();
                float size = HandleUtility.GetHandleSize(childTransform.position) * 0.15f;
                Vector3 newPosition = Handles.FreeMoveHandle(childTransform.position, Quaternion.identity, size, Vector3.zero, Handles.SphereHandleCap);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(childTransform, "Move Path Node");
                    childTransform.position = newPosition;
                    EditorUtility.SetDirty(nodeScript);
                    needRestructure = true; // Точку сдвинули — возможно, нарушился порядок слева-направо
                }
            }
        }

        // --- БЛОК 2: СОЗДАНИЕ НОВЫХ ТОЧЕК В ЛЮБОМ ПОРЯДКЕ (CTRL + ЛКМ) ---
        if (currentEvent.control && currentEvent.type == EventType.MouseDown && currentEvent.button == 0)
        {
            float enterDistance;
            Ray ray = HandleUtility.GUIPointToWorldRay(currentEvent.mousePosition);
            Plane plane = new Plane(pathScript.transform.forward, pathScript.transform.position);

            if (plane.Raycast(ray, out enterDistance))
            {
                Vector3 worldClickPos = ray.GetPoint(enterDistance);

                GameObject newNodeGO = new GameObject();
                Path_Node newNodeScript = newNodeGO.AddComponent<Path_Node>();

                Undo.RegisterCreatedObjectUndo(newNodeGO, "Create Path Node");
                newNodeGO.transform.SetParent(pathScript.transform);
                newNodeGO.transform.position = worldClickPos;

                currentEvent.Use();
                needRestructure = true; // Создали новую точку — нужно встроить её по координате X
            }
        }

        // --- БЛОК 3: УМНАЯ АВТОСОРТИРОВКА И СВЯЗЫВАНИЕ ЛИНИЙ (МАГИЯ РЕДАКТОРА) ---
        if (needRestructure || currentEvent.type == EventType.Layout)
        {
            // Собираем все рабочие ноды в один временный список
            List<Path_Node> allNodes = new List<Path_Node>();
            for (int i = 0; i < pathScript.transform.childCount; i++)
            {
                Path_Node node = pathScript.transform.GetChild(i).GetComponent<Path_Node>();
                if (node != null) allNodes.Add(node);
            }

            if (allNodes.Count > 0)
            {
                // Сортируем список строго по физическому положению на экране (по глобальной оси X)
                allNodes.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));

                // Наводим порядок в иерархии Unity, чтобы Node_0 была самой левой, а последняя — самой правой
                for (int i = 0; i < allNodes.Count; i++)
                {
                    if (allNodes[i].transform.GetSiblingIndex() != i)
                    {
                        allNodes[i].transform.SetSiblingIndex(i);
                    }
                    allNodes[i].gameObject.name = "Node_" + i;
                }

                // Переназначаем связи строго по цепочке слева-направо
                for (int i = 0; i < allNodes.Count; i++)
                {
                    Path_Node currentN = allNodes[i];
                    Path_Node targetNextN = (i < allNodes.Count - 1) ? allNodes[i + 1] : null;

                    if (currentN.nextNode != targetNextN)
                    {
                        Undo.RecordObject(currentN, "Auto Link Node");
                        currentN.nextNode = targetNextN;
                        EditorUtility.SetDirty(currentN);
                    }
                }
            }
        }

        // --- БЛОК 4: ВИЗУАЛИЗАЦИЯ ИДЕАЛЬНЫХ ЛИНИЙ В ОКНЕ СЦЕНЫ ---
        for (int i = 0; i < pathScript.transform.childCount - 1; i++)
        {
            Transform currentChild = pathScript.transform.GetChild(i);
            Transform nextChild = pathScript.transform.GetChild(i + 1);

            if (currentChild != null && nextChild != null)
            {
                Handles.color = Color.cyan;
                Handles.DrawLine(currentChild.position, nextChild.position);
            }
        }

        if (currentEvent.type == EventType.Layout)
        {
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        }
    }
}
