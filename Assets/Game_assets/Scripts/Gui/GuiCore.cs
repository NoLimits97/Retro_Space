using UnityEngine;
using System.Collections.Generic;

// ============================================================================
// GuiCore — единственный носитель OnGUI в проекте + раскладка под разрешения.
//
// На сцене НЕ лежит: создаёт себя сам, когда зарегистрировался первый
// наследник Gui, и отключается, когда рисовать нечему. Нет активных
// рисователей — IMGUI-конвейер не крутится и не сыпет ~0.8 КБ/кадр мусора.
//
// Раскладка БЕЗ GUI.matrix: uniform-скейл + letterbox-центрирование в
// виртуальную сетку 960x544, ретро-снап к целому, крисп-шрифты через fontSize.
// Координаты в наследниках — только R()/RR(), размеры шрифтов — FS()/LabelStyle().
// ============================================================================
public class GuiCore : MonoBehaviour
{
    // ----------------------------- LAYOUT -----------------------------
    public const float VW = 960f, VH = 544f;  // виртуальная сетка (родная Vita)
    public static float S = 1f;              // uniform-скейл
    public static float OX = 0f, OY = 0f;     // letterbox-центрирование
    private static int lastW = -1, lastH = -1;

    // Пересчёт только при смене разрешения; вызывается раз в кадр из OnGUI
    private static void UpdateLayout()
    {
        int w = Screen.width, h = Screen.height;
        if (w == lastW && h == lastH) return;
        lastW = w; lastH = h;

        float raw = Mathf.Min(w / VW, h / VH);
        float snapped = Mathf.Round(raw);
        // Ретро-снап: если близко к целому — берём целое (пиксель-в-пиксель)
        S = (Mathf.Abs(raw - snapped) < 0.05f) ? snapped : raw;

        OX = (w - VW * S) * 0.5f;
        OY = (h - VH * S) * 0.5f;

        Gui.OnResolutionChanged();   // сброс кэшей стилей под новый размер
    }

    // Rect из виртуальной сетки -> реальные пиксели (центрировано, аспект не ломается)
    public static Rect R(float x, float y, float w, float h)
    {
        return new Rect(OX + x * S, OY + y * S, w * S, h * S);
    }

    // ПРАВЫЙ ЯКОРЬ: правая кромка элемента прижата к реальному правому краю
    // экрана (дебаг-монитор на широких мониторах). x,y,w,h — в единицах сетки.
    // На Vita (S=1, OX=0) совпадает с R().
    public static Rect RR(float x, float y, float w, float h)
    {
        return new Rect(Screen.width - (VW - x) * S, OY + y * S, w * S, h * S);
    }

    // Крисп-размер шрифта: шрифт перерастрируется нативно, а не тянется матрицей
    public static int FS(int baseSize)
    {
        return Mathf.Max(8, Mathf.RoundToInt(baseSize * S));
    }

    // ----------------------------- CORE -----------------------------
    private static GuiCore inst;
    private static readonly List<System.Action> drawers = new List<System.Action>();

    public static void Register(System.Action draw)
    {
        if (inst == null)
        {
            GameObject go = new GameObject("GuiCore");
            DontDestroyOnLoad(go);
            inst = go.AddComponent<GuiCore>();
        }
        if (!drawers.Contains(draw)) drawers.Add(draw);
        inst.enabled = true;
    }

    public static void Unregister(System.Action draw)
    {
        drawers.Remove(draw);
        if (inst != null) inst.enabled = drawers.Count > 0;
    }

    void OnGUI()
    {
        UpdateLayout();
        for (int i = 0; i < drawers.Count; i++)
        {
            try { drawers[i](); }
            catch (System.Exception e)
            {
                Debug.LogError("[GuiCore] drawer EXC: " + e.Message);
            }
        }
    }
}