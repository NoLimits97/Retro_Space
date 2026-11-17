// File: Assets/VitaUI/UI.cs
using System.Collections.Generic;
using UnityEngine;

namespace VitaUI
{
    // Канон: Center = центральная зона (как в спеке и демо)
    public enum UIAnchor
    {
        TopLeft, TopCenter, TopRight,
        MiddleLeft, Center, MiddleRight,
        BottomLeft, BottomCenter, BottomRight
    }

    public enum Align { Start, Center, End }

    public static class UI
    {
#if VITA_UI
        public static bool vita = true;
#else
        public static bool vita = false;
#endif
        public const float GridW = 960f;
        public const float GridH = 544f;

        public static readonly List<UIScreen> stack = new List<UIScreen>();
        public static int rebuilds; // счётчик reb для оверлея

        public static void GoSub(UIScreen parent, UIScreen child)
        {
            if (parent != null) parent.panel.Hide(0.2f);
            if (child != null) child.panel.Show(0.2f);
        }

        // immediate: вызвал в кадре = есть
        public static void Hint(string text, Rect place, int size) { UIRoot.PushHint(text, place, size); }
        public static void Hint(string text, UIAnchor anchor, int size) { UIRoot.PushHint(text, anchor, size); }
        public static void Toast(string text) { UIRoot.PushToast(text); }
    }

    [CreateAssetMenu(menuName = "VitaUI/UIStyle")]
    public class UIStyle : ScriptableObject
    {
        public Vector2 buttonSize = new Vector2(100, 60);
        public int buttonFont = 24;
        public int titleFont = 48;
        public int textFont = 24;
        public int listSpacing = 10;
        public int boxPadding = 24;
        public int sliderH = 40;
        public Color textColor = Color.white;
        public Color boxBg = new Color(0.08f, 0.09f, 0.1f, 0.92f);
        public Color boxBorder = new Color(0.6f, 0.6f, 0.6f, 1f);

        public static UIStyle Current { get; set; }
    }
}
