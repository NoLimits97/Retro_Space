// File: Assets/VitaUI/UIScreen.cs
using System.Collections.Generic;
using UnityEngine;

namespace VitaUI
{
    public abstract class UIScreen
    {
        public string name;
        public UIScreen parent;
        public UIPanel panel;

        bool _bound;
        bool _destroyed;

        protected UIScreen(string name, UIScreen parent = null)
        {
            this.name = name;
            this.parent = parent;
            this.panel = new UIPanel(this);
            UIRoot.Register(this);
        }

        // Только движок. Повторный OnBind на том же экземпляре запрещён.
        internal void DoBind()
        {
            if (_bound)
            {
                Debug.LogError("[VitaUI] OnBind() повторно на \"" + name + "\" — запрещено");
                return;
            }
            _bound = true;
            OnBind();
            panel.LayoutAll();
        }

        // Перед очисткой реестра / уничтожением. НЕ при Hide. _bound не сбрасывается.
        internal void DoUnbind()
        {
            if (!_bound || _destroyed) return;
            _destroyed = true;
            OnUnbind();
        }

        // Явное уничтожение экрана
        public void Destroy()
        {
            DoUnbind();
            UIRoot.RemoveStack(this);
            UIRoot.Unregister(this);
        }

        protected virtual void OnBind() { }
        protected virtual void OnShow() { }
        protected virtual void OnHide() { }
        protected virtual void OnTick() { }
        protected virtual void OnUnbind() { }

        // Дефолтный Back: parent → Hide(self)+Show(parent); иначе false → движок прячет сам.
        public virtual bool OnBack()
        {
            if (parent != null)
            {
                panel.Hide(0.2f);
                parent.panel.Show(0.2f);
                return true;
            }
            return false;
        }

        internal void RaiseShow() { OnShow(); }
        internal void RaiseHide() { OnHide(); }
        internal void RaiseTick() { OnTick(); }
    }

    public class UIPanel
    {
        public readonly UIScreen owner;
        public Color background = new Color(0, 0, 0, 0); // дим; a=0 — нет дима
        public bool visible { get; private set; }
        public float alpha { get; private set; }

        float target;
        float fadeDur = 0.2f;

        internal readonly List<UINode> topNodes = new List<UINode>();

        public UIPanel(UIScreen owner) { this.owner = owner; }

        // Семантика демо: панель остаётся в стеке до alpha=0. Обратный фейд без скачка.
        public void Show(float fade = 0.2f)
        {
            if (!visible)
            {
                visible = true;
                UIRoot.PushStack(owner);
                owner.RaiseShow();
            }
            target = 1f;
            fadeDur = Mathf.Max(0.01f, fade);
            UI.rebuilds++;
        }

        public void Hide(float fade = 0.2f)
        {
            target = 0f;
            fadeDur = Mathf.Max(0.01f, fade);
            UI.rebuilds++;
        }

        internal void UpdateFade(float unscaledDt)
        {
            if (!visible || alpha == target) return;
            alpha = Mathf.MoveTowards(alpha, target, unscaledDt / fadeDur);
            if (alpha <= 0f && target <= 0f)
            {
                visible = false;
                UIRoot.RemoveStack(owner);   // splice из стека только при alpha=0
                owner.RaiseHide();
            }
        }

        // ---- топ-узлы: сигнатура (anchor, ox, oy, ...) как в демо ----
        public UIBox AddVBox(UIAnchor anchor, float ox, float oy, int spacing = -1, Align align = Align.Center)
        { return AddBox(anchor, ox, oy, true, spacing, align); }

        public UIBox AddHBox(UIAnchor anchor, float ox, float oy, int spacing = -1, Align align = Align.Center)
        { return AddBox(anchor, ox, oy, false, spacing, align); }

        public UIBox AddUIBox(UIAnchor anchor, float ox, float oy, int padding = -1)
        {
            UIBox b = AddBox(anchor, ox, oy, true, -1, Align.Center);
            b.isWindow = true; b.padOverride = padding;
            return b;
        }

        public UIDynText AddDynText(UIAnchor anchor, float ox, float oy, float w, float h, int size = 24, Align align = Align.Center)
        {
            UIDynText t = new UIDynText(w, h, size, align);
            t.anchor = anchor; t.ox = ox; t.oy = oy;
            topNodes.Add(t); return t;
        }

        public UIStaticText AddText(UIAnchor anchor, float ox, float oy, string text, int size = 24)
        {
            UIStaticText t = new UIStaticText(text, size, Align.Center);
            t.anchor = anchor; t.ox = ox; t.oy = oy;
            topNodes.Add(t); return t;
        }

        UIBox AddBox(UIAnchor anchor, float ox, float oy, bool vertical, int spacing, Align align)
        {
            UIBox b = new UIBox(vertical, spacing, align);
            b.anchor = anchor; b.ox = ox; b.oy = oy;
            topNodes.Add(b); return b;
        }

        internal void LayoutAll()
        {
            for (int i = 0; i < topNodes.Count; i++) topNodes[i].ComputeSize();
            for (int i = 0; i < topNodes.Count; i++)
            {
                UINode n = topNodes[i];
                n.Arrange(UIAnchorMath.Place(n.anchor, n.ox, n.oy, n.size));
            }
            for (int i = 0; i < topNodes.Count; i++) UIGuards.Check(topNodes[i], owner.name);
            UI.rebuilds++;
        }

        internal void CollectFocusables(List<UIWidget> outList)
        {
            if (!visible || alpha <= 0.3f) return; // как demo topScreen(): ввод по панели с alpha>0.3
            for (int i = 0; i < topNodes.Count; i++) topNodes[i].CollectFocusables(outList);
        }

        internal void Render(UIRenderer r)
        {
            if (!visible || alpha <= 0f) return;
            if (background.a > 0f)
                r.DrawQuad(new Rect(0, 0, UI.GridW, UI.GridH), background, alpha);
            for (int i = 0; i < topNodes.Count; i++) topNodes[i].Render(r, alpha);
        }

    }

    // Канон: UITabs — НЕ экран, только вкладки верхнего уровня
    public class UITabs
    {
        public readonly List<UIScreen> list;
        public UIScreen current;

        public UITabs(params UIScreen[] list) { this.list = new List<UIScreen>(list); }

        public void SwitchTo(UIScreen s, float fade = 0.2f)
        {
            if (current == s || s == null) return;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != s && list[i].panel.visible) list[i].panel.Hide(fade);
            current = s;
            s.panel.Show(fade);
        }
    }
}
