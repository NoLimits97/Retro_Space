using UnityEngine;
using System.Collections.Generic;

// ============================================================================
// Gui — базовый класс IMGUI-отрисовки.
// Наследник реализует только ShouldDraw() («рисуем ли сейчас») и Draw() («что»).
// Координаты в Draw() — через GuiCore.R()/RR() (виртуальная сетка 960x544),
// шрифты — через GuiCore.FS()/LabelStyle() (крисп на любом разрешении).
//
// Пока ShouldDraw() == false — наследник разрезегистрирован: ядро выключено,
// IMGUI-конвейер не крутится, мусора ноль. Ядро на сцене не лежит — наследники
// создают его сами через Register.
//
// ВАЖНО: переопределяешь LateUpdate/OnDestroy в наследнике — вызывай base!
// ============================================================================
public abstract class Gui : MonoBehaviour
{
    private bool registered;
    private System.Action cachedDraw;   // делегат создаётся один раз, не каждый кадр

    protected abstract bool ShouldDraw();
    protected abstract void Draw();

    protected virtual void LateUpdate()
    {
        if (cachedDraw == null) cachedDraw = DoDraw;
        bool need = ShouldDraw();
        if (need && !registered) { GuiCore.Register(cachedDraw); registered = true; }
        else if (!need && registered) { GuiCore.Unregister(cachedDraw); registered = false; }
    }

    private void DoDraw() { Draw(); }

    protected virtual void OnDestroy()
    {
        if (registered) GuiCore.Unregister(cachedDraw);
    }

    // ---------------- кэш крисп-стилей ----------------
    private static readonly Dictionary<int, GUIStyle> styleCache = new Dictionary<int, GUIStyle>();

    // Лейбл-стиль с крисп-размером; кэшируется по размеру, ноль аллокаций в кадре.
    // Шрифт передавай из скина: LabelStyle(24, globalSkin.font)
    protected static GUIStyle LabelStyle(int baseSize, Font font = null)
    {
        int size = GuiCore.FS(baseSize);
        GUIStyle st;
        if (!styleCache.TryGetValue(size, out st) || st == null)
        {
            st = new GUIStyle(GUI.skin.label);
            if (font != null) st.font = font;
            st.fontSize = size;
            st.alignment = TextAnchor.MiddleCenter;
            st.wordWrap = true;
            styleCache[size] = st;
        }
        return st;
    }

    // Вызывается ядром при смене разрешения — кэш пересоздастся под новый размер
    public static void OnResolutionChanged()
    {
        styleCache.Clear();
    }
}