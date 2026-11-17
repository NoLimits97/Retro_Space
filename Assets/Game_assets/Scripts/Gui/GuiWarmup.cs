using UnityEngine;
using System.Collections.Generic;

// GuiWarmup — скрытая инициализация IMGUI-конвейера в загрузочной задержке.
// Несколько кадров рисует «невидимый» комплект (прозрачно + за экраном):
// бокс скина + лейблы нужных размеров/текстов. Атлас шрифта, текстуры скина
// и IMGUI-буферы уезжают в VRAM заранее — первый реальный показ чистый.
public static class GuiWarmup
{
    private static GUISkin skin;
    private static Font font;
    private static readonly List<int> sizes = new List<int>();
    private static readonly List<string> texts = new List<string>();
    private static int framesLeft = 0;
    private static readonly System.Action draw = Draw;

    public static void Prepare(GUISkin s, Font f, int[] fontSizes, string[] samples)
    {
        skin = s;
        font = f;
        sizes.Clear();
        if (fontSizes != null) sizes.AddRange(fontSizes);
        texts.Clear();
        if (samples != null) texts.AddRange(samples);
    }

    // Вызывать на старте сцены. frames — ручка «длина прогрева» (2..5)
    public static void Start(int frames)
    {
        if (framesLeft > 0) return;
        framesLeft = frames;
        GuiCore.Register(draw);
    }

    private static void Draw()
    {
        if (framesLeft <= 0) return;
        framesLeft--;

        if (skin != null) GUI.skin = skin;
        GUI.color = new Color(1f, 1f, 1f, 0f);      // прозрачно...
        float y = -2000f;                            // ...и за экраном
        GUI.Box(new Rect(-2000f, y, 480f, 544f), "warmup");
        y -= 600f;

        for (int i = 0; i < texts.Count; i++)
        {
            for (int k = 0; k < sizes.Count; k++)
            {
                GUIStyle st = new GUIStyle(GUI.skin.label);
                if (font != null) st.font = font;
                st.fontSize = sizes[k];
                st.wordWrap = true;
                GUI.Label(new Rect(-2000f, y, 900f, 80f), texts[i], st);
                y -= 100f;
            }
        }

        GUI.color = Color.white;
        if (framesLeft == 0) GuiCore.Unregister(draw);
    }
}