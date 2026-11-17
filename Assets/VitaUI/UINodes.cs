// File: Assets/VitaUI/UINodes.cs
using System.Collections.Generic;
using UnityEngine;

namespace VitaUI
{
    public static class UIAnchorMath
    {
        public static Vector2 Point(UIAnchor a)
        {
            switch (a)
            {
                case UIAnchor.TopLeft: return new Vector2(0, 0);
                case UIAnchor.TopCenter: return new Vector2(UI.GridW * 0.5f, 0);
                case UIAnchor.TopRight: return new Vector2(UI.GridW, 0);
                case UIAnchor.MiddleLeft: return new Vector2(0, UI.GridH * 0.5f);
                case UIAnchor.Center: return new Vector2(UI.GridW * 0.5f, UI.GridH * 0.5f);
                case UIAnchor.MiddleRight: return new Vector2(UI.GridW, UI.GridH * 0.5f);
                case UIAnchor.BottomLeft: return new Vector2(0, UI.GridH);
                case UIAnchor.BottomCenter: return new Vector2(UI.GridW * 0.5f, UI.GridH);
                default: return new Vector2(UI.GridW, UI.GridH);
            }
        }

        // Угол бокса = точка якоря; у правых/нижних якорей бокс растёт внутрь (отриц. ox/oy).
        public static Rect Place(UIAnchor a, float ox, float oy, Vector2 size)
        {
            Vector2 p = Point(a);
            float px = p.x + ox, py = p.y + oy;
            bool hR = a == UIAnchor.TopRight || a == UIAnchor.MiddleRight || a == UIAnchor.BottomRight;
            bool hC = a == UIAnchor.TopCenter || a == UIAnchor.Center || a == UIAnchor.BottomCenter;
            bool vB = a == UIAnchor.BottomLeft || a == UIAnchor.BottomCenter || a == UIAnchor.BottomRight;
            bool vC = a == UIAnchor.MiddleLeft || a == UIAnchor.Center || a == UIAnchor.MiddleRight;
            float x = hR ? px - size.x : hC ? px - size.x * 0.5f : px;
            float y = vB ? py - size.y : vC ? py - size.y * 0.5f : py;
            return new Rect(x, y, size.x, size.y);
        }
    }

    public abstract class UINode
    {
        public UIAnchor anchor; public float ox, oy;
        public Rect rect;
        public Vector2 size;
        public abstract void ComputeSize();
        public abstract void Arrange(Rect r);
        public abstract void Render(UIRenderer r, float panelAlpha);
        public virtual void CollectFocusables(List<UIWidget> outList) { }
    }

    public abstract class UIWidget : UINode
    {
        public bool focusable = true;
        public override void CollectFocusables(List<UIWidget> outList) { if (focusable) outList.Add(this); }
        public virtual void Activate() { }
        public virtual bool HandleAxis(int dir) { return false; }
    }

    public class UIBox : UINode
    {
        public bool vertical;
        public int spacing = -1;
        public Align align;
        public bool isWindow;
        public int padOverride = -1;
        public readonly List<UINode> children = new List<UINode>();

        public UIBox(bool vertical, int spacing, Align align)
        { this.vertical = vertical; this.spacing = spacing; this.align = align; }

        int Spacing { get { return spacing >= 0 ? spacing : UIStyle.Current.listSpacing; } }
        int Pad { get { return isWindow ? (padOverride >= 0 ? padOverride : UIStyle.Current.boxPadding) : 0; } }

        public UIBox AddVBox(int spacing = -1, Align align = Align.Center) { UIBox b = new UIBox(true, spacing, align); children.Add(b); return b; }
        public UIBox AddHBox(int spacing = -1, Align align = Align.Center) { UIBox b = new UIBox(false, spacing, align); children.Add(b); return b; }
        public UIBox AddUIBox(int padding = -1) { UIBox b = new UIBox(true, -1, Align.Center); b.isWindow = true; b.padOverride = padding; children.Add(b); return b; }

        public UIButton AddButton(string text, System.Action onClick, Vector2? size = null)
        { UIButton b = new UIButton(text, onClick, size); children.Add(b); return b; }
        public UIStaticText AddTitle(string text, int size = 48) { UIStaticText t = new UIStaticText(text, size, Align.Center); children.Add(t); return t; }
        public UIStaticText AddText(string text, int size = 24, Align align = Align.Center) { UIStaticText t = new UIStaticText(text, size, align); children.Add(t); return t; }
        public UIDynText AddDynText(float w, float h, int size = 24, Align align = Align.Center) { UIDynText t = new UIDynText(w, h, size, align); children.Add(t); return t; }
        public UISlider AddSlider(float trackW, float value, System.Action<float> onChange, string label = "Volume")
        { UISlider s = new UISlider(trackW, value, onChange, label); children.Add(s); return s; }

        public override void ComputeSize()
        {
            for (int i = 0; i < children.Count; i++) children[i].ComputeSize();
            int pad = Pad, sp = Spacing;
            float main = 0, cross = 0;
            for (int i = 0; i < children.Count; i++)
            {
                Vector2 cs = children[i].size;
                main += (vertical ? cs.y : cs.x) + (i > 0 ? sp : 0);
                float c = vertical ? cs.x : cs.y;
                if (c > cross) cross = c;
            }
            size = vertical ? new Vector2(cross + pad * 2, main + pad * 2)
                            : new Vector2(main + pad * 2, cross + pad * 2);
        }

        public override void Arrange(Rect r)
        {
            rect = r;
            int pad = Pad, sp = Spacing;
            float cursor = (vertical ? r.y : r.x) + pad;
            float crossStart = (vertical ? r.x : r.y) + pad;
            float crossSpan = (vertical ? r.width : r.height) - pad * 2;
            for (int i = 0; i < children.Count; i++)
            {
                UINode c = children[i];
                float cCross = vertical ? c.size.x : c.size.y;
                float off = align == Align.Center ? (crossSpan - cCross) * 0.5f
                          : align == Align.End ? crossSpan - cCross : 0f;
                Rect cr = vertical
                    ? new Rect(crossStart + off, cursor, c.size.x, c.size.y)
                    : new Rect(cursor, crossStart + off, c.size.x, c.size.y);
                c.Arrange(cr);
                cursor += (vertical ? c.size.y : c.size.x) + sp;
            }
        }

        public override void Render(UIRenderer r, float a)
        {
            if (isWindow)
            {
                r.DrawQuad(rect, UIStyle.Current.boxBg, a);
                r.DrawBorder(rect, UIStyle.Current.boxBorder, a);
            }
            for (int i = 0; i < children.Count; i++) children[i].Render(r, a);
        }

        public override void CollectFocusables(List<UIWidget> outList)
        { for (int i = 0; i < children.Count; i++) children[i].CollectFocusables(outList); }
    }

    public class UIButton : UIWidget
    {
        public string text; public System.Action onClick; public Vector2? sizeOverride;
        public UIButton(string text, System.Action onClick, Vector2? sizeOverride)
        { this.text = text; this.onClick = onClick; this.sizeOverride = sizeOverride; }
        public override void ComputeSize() { size = sizeOverride ?? UIStyle.Current.buttonSize; }
        public override void Arrange(Rect r) { rect = r; }
        public override void Render(UIRenderer r, float a)
        {
            r.DrawQuad(rect, this == UIRoot.focus ? new Color(1, 1, 1, 0.25f) : new Color(1, 1, 1, 0.08f), a);
            r.DrawBorder(rect, UIStyle.Current.boxBorder, a);
            r.DrawText(text, rect, UIStyle.Current.buttonFont, Align.Center, UIStyle.Current.textColor, a);
        }
        public override void Activate() { if (onClick != null) onClick(); else UI.Toast(text); }
    }

    public class UIStaticText : UIWidget
    {
        public string text; public int fontSize; public Align align;
        public UIStaticText(string text, int fontSize, Align align)
        { this.text = text; this.fontSize = fontSize; this.align = align; focusable = false; }
        public override void ComputeSize() { size = new Vector2(UIRenderer.Measure(text, fontSize), fontSize * 1.5f); }
        public override void Arrange(Rect r) { rect = r; }
        public override void Render(UIRenderer r, float a) { r.DrawText(text, rect, fontSize, align, UIStyle.Current.textColor, a); }
    }

    public class UIDynText : UIWidget
    {
        public int fontSize; public Align align;
        string text = "";
        public UIDynText(float w, float h, int fontSize, Align align)
        { size = new Vector2(w, h); this.fontSize = fontSize; this.align = align; focusable = false; }
        public void SetText(string s) { text = s; UI.rebuilds++; } // pooled — слой 3
        public override void ComputeSize() { }
        public override void Arrange(Rect r) { rect = r; }
        public override void Render(UIRenderer r, float a) { r.DrawText(text, rect, fontSize, align, UIStyle.Current.textColor, a, true); }
    }

    public class UISlider : UIWidget
    {
        public float trackW; public string label; public System.Action<float> onChange;
        float _value;
        public float value { get { return _value; } set { _value = Mathf.Clamp01(value); } }

        public UISlider(float trackW, float value, System.Action<float> onChange, string label)
        { this.trackW = trackW; this.onChange = onChange; this.label = label; _value = Mathf.Clamp01(value); }

        public override void ComputeSize()
        { size = new Vector2(UIRenderer.Measure(label, UIStyle.Current.buttonFont) + 12 + trackW, UIStyle.Current.sliderH); }
        public override void Arrange(Rect r) { rect = r; }
        public override void Render(UIRenderer r, float a)
        {
            float lw = UIRenderer.Measure(label, UIStyle.Current.buttonFont);
            r.DrawText(label, new Rect(rect.x, rect.y, lw, rect.height), UIStyle.Current.buttonFont, Align.Start, UIStyle.Current.textColor, a);
            float tx = rect.x + lw + 12;
            r.DrawQuad(new Rect(tx, rect.y + rect.height * 0.5f - 4, trackW, 8), new Color(1, 1, 1, 0.25f), a);
            float kw = 14;
            r.DrawQuad(new Rect(tx + (trackW - kw) * _value, rect.y + rect.height * 0.5f - 12, kw, 24),
                       this == UIRoot.focus ? Color.white : UIStyle.Current.boxBorder, a);
        }
        public override bool HandleAxis(int dir)
        {
            float nv = Mathf.Clamp01(_value + dir * 0.05f);
            if (Mathf.Approximately(nv, _value)) return false;
            value = nv;
            if (onChange != null) onChange(nv);
            return true;
        }
    }

    static class UIGuards
    {
        public static void Check(UINode n, string screen)
        {
            UIBox b = n as UIBox;
            if (b == null)
            {
                Rect r = n.rect;
                if (r.x < -0.5f || r.y < -0.5f || r.xMax > UI.GridW + 0.5f || r.yMax > UI.GridH + 0.5f)
                    Debug.LogWarning("[VitaUI GUARD] " + screen + ": " + n.GetType().Name + " out of bounds " + r);
                return;
            }
            for (int i = 0; i < b.children.Count; i++)
                for (int k = i + 1; k < b.children.Count; k++)
                    if (b.children[i].rect.Overlaps(b.children[k].rect))
                        Debug.LogWarning("[VitaUI GUARD] " + screen + ": overlap inside box");
            for (int i = 0; i < b.children.Count; i++) Check(b.children[i], screen);
        }
    }
}
