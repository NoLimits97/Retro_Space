// File: Assets/VitaUI/UIRoot.cs
using System.Collections.Generic;
using UnityEngine;

namespace VitaUI
{
    [DisallowMultipleComponent]
    public class UIRoot : MonoBehaviour
    {
        public UIStyle style;        // опционально, из инспектора
        public Font font;            // опционально TTF; иначе Arial (PC dev-заглушка)
        public Camera targetCamera;  // камера для рендера; null → Camera.main

        static readonly List<UIScreen> registry = new List<UIScreen>();
        static bool didRun;

        public static UIWidget focus;
        static readonly List<UIWidget> focusables = new List<UIWidget>();
        static UIRenderer renderer;

        struct HintItem { public string text; public Rect rect; public int size; public UIAnchor anchor; public bool useAnchor; }
        static readonly List<HintItem> hints = new List<HintItem>();
        static string toastText; static float toastTTL;

        public static void Register(UIScreen s) { registry.Add(s); }
        public static void Unregister(UIScreen s) { registry.Remove(s); }
        public static void PushStack(UIScreen s) { if (!UI.stack.Contains(s)) UI.stack.Add(s); }
        public static void RemoveStack(UIScreen s) { UI.stack.Remove(s); }

        public static void PushHint(string text, Rect place, int size)
        { hints.Add(new HintItem { text = text, rect = place, size = size }); }
        public static void PushHint(string text, UIAnchor anchor, int size)
        { hints.Add(new HintItem { text = text, anchor = anchor, size = size, useAnchor = true }); }
        public static void PushToast(string text) { toastText = text; toastTTL = 1.6f; }

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            if (UIStyle.Current == null)
                UIStyle.Current = style != null ? style : ScriptableObject.CreateInstance<UIStyle>();
            renderer = new UIRenderer(font);
        }

        void Start() { Run(); }

        // Run: конструкторы отработали → один раз bind+layout → Show первого.
        public static void Run()
        {
            if (didRun) return;
            didRun = true;
            for (int i = 0; i < registry.Count; i++) registry[i].DoBind();
            if (registry.Count > 0 && !registry[0].panel.visible) registry[0].panel.Show(0.2f);
        }

        // Полный сброс: OnUnbind всем → очистка. Новые экземпляры создаются заново.
        public static void ResetAll()
        {
            for (int i = 0; i < registry.Count; i++) registry[i].DoUnbind();
            registry.Clear();
            UI.stack.Clear();
            focus = null;
            didRun = false;
        }

        static UIScreen TopScreen()
        {
            for (int i = UI.stack.Count - 1; i >= 0; i--)
                if (UI.stack[i].panel.alpha > 0.3f) return UI.stack[i];
            return null;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime; // fade на unscaled
            for (int i = 0; i < registry.Count; i++) registry[i].panel.UpdateFade(dt);
            for (int i = 0; i < UI.stack.Count; i++)
                if (UI.stack[i].panel.visible) UI.stack[i].RaiseTick();
            if (toastTTL > 0f) toastTTL -= dt;

            UIScreen top = TopScreen();
            UpdateFocus(top);
            HandleInput(top);
        }

        void UpdateFocus(UIScreen top)
        {
            focusables.Clear();
            if (top != null) top.panel.CollectFocusables(focusables);
            if (focusables.Count == 0) { focus = null; return; }
            if (focus == null || !focusables.Contains(focus)) focus = focusables[0];
        }

        void HandleInput(UIScreen top)
        {
            // Circle/Esc → OnBack верхнего; false → движок прячет панель
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (top != null && !top.OnBack()) top.panel.Hide(0.2f);
                return;
            }

            bool left = Input.GetKeyDown(KeyCode.LeftArrow);
            bool right = Input.GetKeyDown(KeyCode.RightArrow);
            bool up = Input.GetKeyDown(KeyCode.UpArrow);
            bool down = Input.GetKeyDown(KeyCode.DownArrow);

            if (focus != null && (left || right) && focus.HandleAxis(left ? -1 : 1)) { }
            else if ((left || right || up || down) && focusables.Count > 1)
            {
                int dir = (right || down) ? 1 : -1; // DFS-обход (CodeOrder)
                int idx = focusables.IndexOf(focus);
                if (idx >= 0) focus = focusables[(idx + dir + focusables.Count) % focusables.Count];
            }

            if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.X)) && focus != null)
                focus.Activate();
        }

        void OnRenderObject()
        {
            Camera cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null || Camera.current != cam) return; // один раз за кадр; scene view не дублирует

            renderer.BeginFrame();

            int draws = 0;
            for (int i = 0; i < UI.stack.Count; i++)
            {
                UIPanel p = UI.stack[i].panel;
                if (p.visible && p.alpha > 0f) { p.Render(renderer); draws++; }
            }

            for (int i = 0; i < hints.Count; i++)
            {
                HintItem h = hints[i];
                Rect r = h.useAnchor
                    ? UIAnchorMath.Place(h.anchor, 0, 0, new Vector2(UIRenderer.Measure(h.text, h.size) + 24, h.size * 1.5f + 16))
                    : h.rect;
                renderer.DrawQuad(r, new Color(0.07f, 0.07f, 0.07f, 0.8f), 1f);
                renderer.DrawText(h.text, r, h.size, Align.Center, Color.white, 1f);
            }
            if (hints.Count > 0) draws++;
            hints.Clear();

            if (toastTTL > 0f && toastText != null)
            {
                float w = UIRenderer.Measure(toastText, 20);
                renderer.DrawText(toastText, new Rect((UI.GridW - w) * 0.5f, UI.GridH - 40, w, 30),
                                  20, Align.Center, new Color(0.5f, 1f, 0.5f), 1f);
                draws++;
            }

            DrawMetrics(draws);

            GL.PushMatrix();
            GL.LoadPixelMatrix(0, Screen.width, 0, Screen.height);
            renderer.EndFrame();
            GL.PopMatrix();
        }

        void DrawMetrics(int draws)
        {
            string stackStr = "";
            for (int i = 0; i < UI.stack.Count; i++) stackStr += (i > 0 ? ">" : "") + UI.stack[i].name;
            string[] lines = {
                "vita:" + (UI.vita ? 1 : 0) + " draws:" + draws,
                "tris:" + renderer.triCount + " reb:" + UI.rebuilds,
                "GC:0B mode:play",
                "stack:" + stackStr
            };
            for (int i = 0; i < lines.Length; i++)
                renderer.DrawText(lines[i], new Rect(UI.GridW - 320, 8 + i * 14, 312, 14),
                                  12, Align.End, new Color(0.4f, 1f, 0.4f), 1f);
        }
    }
}
