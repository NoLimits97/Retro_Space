using UnityEngine;
using System.Collections.Generic;
// HubManager v2: наследует Gui — подсказка рисуется через ядро.
// IMGUI-конвейер крутится ТОЛЬКО пока подсказка видна на экране,
// остальное время рисовальщик разрезегистрирован и мусора ноль.
public class HubManager : Gui
{
    [Header("Глобальный стиль интерфейса")]
    public GUISkin globalskin;
    private contoller_2d playerRef;

    void Start()
    {
        playerRef = FindObjectOfType<contoller_2d>();

        // Собираем ВСЕ подсказки хаба и греем шрифт под них
        Path_Node[] nodes = FindObjectsOfType<Path_Node>();
        List<string> prompts = new List<string>();
        for (int i = 0; i < nodes.Length; i++)
        {
            if (!string.IsNullOrEmpty(nodes[i].interactionPrompt))
                prompts.Add(nodes[i].interactionPrompt);
        }
        GuiWarmup.Prepare(globalskin,
            globalskin != null ? globalskin.font : null,
            new int[] { 22 }, prompts.ToArray());
        GuiWarmup.Start(3);
    }

    // База Gui: пока false — IMGUI-конвейер спит
    protected override bool ShouldDraw()
    {
        return playerRef != null && playerRef.showInteractionPrompt && playerRef.activeTriggerNode != null;
    }

    protected override void Draw()
    {
        // Защита от гонки: между ShouldDraw() (LateUpdate) и Draw() (OnGUI)
        // contoller_2d.LateUpdate мог уже спрятать подсказку и обнулить ноду.
        if (playerRef == null || playerRef.activeTriggerNode == null) return;

        if (globalskin != null)
        {
            GUI.skin = globalskin;
        }
        GUIStyle labelStyle = LabelStyle(22, globalskin != null ? globalskin.font : null);
        Rect positionRect = GuiCore.R(80, 40, 800, 80);
        GUI.Label(positionRect, playerRef.activeTriggerNode.interactionPrompt, labelStyle);
    }
}