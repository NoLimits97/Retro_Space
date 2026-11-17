// File: Assets/VitaUI/UIRenderer.cs
using System.Collections.Generic;
using UnityEngine;

namespace VitaUI
{
    // Dev-рендер слоя 2 (PC): dynamic font + динамический меш, ~2 draw call.
    // ВАЖНО: на Vita системных шрифтов нет — слой 3 заменяет на bitmap-атлас + батчер.
    // Вызывается из UIRoot.OnRenderObject внутри GL.LoadPixelMatrix.
    public class UIRenderer
    {
        static Font font;
        Material matQuads, matText;
        Mesh meshQuads, meshText;

        readonly List<Vector3> qv = new List<Vector3>();
        readonly List<Vector2> quv = new List<Vector2>();
        readonly List<Color> qc = new List<Color>();
        readonly List<int> qt = new List<int>();

        readonly List<Vector3> tv = new List<Vector3>();
        readonly List<Vector2> tuv = new List<Vector2>();
        readonly List<Color> tc = new List<Color>();
        readonly List<int> tt = new List<int>();

        public int triCount { get; private set; }

        float scale, offX, offY;

        public UIRenderer(Font assigned)
        {
            Shader sh = Shader.Find("UI/Default"); // Cull Off; для билда — в Always Included Shaders
            matQuads = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            matQuads.mainTexture = Texture2D.whiteTexture;
            matText = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            meshQuads = new Mesh(); meshQuads.MarkDynamic();
            meshText = new Mesh(); meshText.MarkDynamic();

            if (font == null)
            {
                font = assigned != null ? assigned : Font.CreateDynamicFontFromOSFont("Arial", 24);
                if (font == null)
                    Debug.LogWarning("[VitaUI] Динамический шрифт недоступен (Vita?) — нужен bitmap-атлас слоя 3");
            }
        }

        // Ретро-снап к целому: |raw-round|<0.05 → целое; на Vita — всегда.
        static float Snap(float v)
        {
            float r = Mathf.Round(v);
            return (UI.vita || Mathf.Abs(v - r) < 0.05f) ? r : v;
        }

        void ComputeTransform()
        {
            scale = Mathf.Min(Screen.width / UI.GridW, Screen.height / UI.GridH);
            offX = (Screen.width - UI.GridW * scale) * 0.5f;
            offY = (Screen.height - UI.GridH * scale) * 0.5f;
        }

        // Виртуальные координаты (y вниз) → экранные пиксели (y вверх, для GL).
        Vector3 ToScreen(float vx, float vy)
        {
            float x = Snap(offX + vx * scale);
            float y = Snap(Screen.height - (offY + vy * scale));
            return new Vector3(x, y, 0f);
        }

        public static float Measure(string text, int size)
        {
            if (string.IsNullOrEmpty(text) || font == null) return 0f;
            font.RequestCharactersInTexture(text, size);
            float w = 0;
            for (int i = 0; i < text.Length; i++)
            {
                CharacterInfo ci;
                if (font.GetCharacterInfo(text[i], out ci, size)) w += ci.advance;
            }
            return w;
        }

        public void BeginFrame()
        {
            ComputeTransform();
            qv.Clear(); quv.Clear(); qc.Clear(); qt.Clear();
            tv.Clear(); tuv.Clear(); tc.Clear(); tt.Clear();
            triCount = 0;
        }

        public void EndFrame()
        {
            if (qv.Count > 0)
            {
                meshQuads.Clear();
                meshQuads.SetVertices(qv);
                meshQuads.SetUVs(0, quv);
                meshQuads.SetColors(qc);
                meshQuads.SetTriangles(qt, 0);
                matQuads.SetPass(0);
                Graphics.DrawMeshNow(meshQuads, Matrix4x4.identity);
            }
            if (tv.Count > 0 && font != null)
            {
                matText.mainTexture = font.material.mainTexture; // атлас мог пересобраться
                meshText.Clear();
                meshText.SetVertices(tv);
                meshText.SetUVs(0, tuv);
                meshText.SetColors(tc);
                meshText.SetTriangles(tt, 0);
                matText.SetPass(0);
                Graphics.DrawMeshNow(meshText, Matrix4x4.identity);
            }
        }

        public void DrawQuad(Rect r, Color c, float alpha)
        {
            c.a *= alpha;
            int i0 = qv.Count;
            qv.Add(ToScreen(r.x, r.y));
            qv.Add(ToScreen(r.x, r.yMax));
            qv.Add(ToScreen(r.xMax, r.yMax));
            qv.Add(ToScreen(r.xMax, r.y));
            for (int i = 0; i < 4; i++) { qc.Add(c); quv.Add(Vector2.zero); }
            qt.Add(i0); qt.Add(i0 + 1); qt.Add(i0 + 2);
            qt.Add(i0); qt.Add(i0 + 2); qt.Add(i0 + 3);
            triCount += 2;
        }

        public void DrawBorder(Rect r, Color c, float alpha)
        {
            float t = 2f;
            DrawQuad(new Rect(r.x, r.y, r.width, t), c, alpha);
            DrawQuad(new Rect(r.x, r.yMax - t, r.width, t), c, alpha);
            DrawQuad(new Rect(r.x, r.y, t, r.height), c, alpha);
            DrawQuad(new Rect(r.xMax - t, r.y, t, r.height), c, alpha);
        }

        public void DrawText(string text, Rect r, int size, Align align, Color c, float alpha, bool clip = false)
        {
            if (string.IsNullOrEmpty(text) || font == null) return;
            c.a *= alpha;
            font.RequestCharactersInTexture(text, size);
            float w = Measure(text, size);
            float x = align == Align.Center ? r.x + (r.width - w) * 0.5f
                    : align == Align.End ? r.xMax - w : r.x;
            float baseline = r.y + (r.height - size) * 0.5f + size;
            EmitGlyphs(text, x, baseline, size, c, clip ? (Rect?)r : null);
        }

        void EmitGlyphs(string text, float penX, float baseline, int size, Color c, Rect? clipRect)
        {
            float x = penX;
            for (int i = 0; i < text.Length; i++)
            {
                CharacterInfo ci;
                if (!font.GetCharacterInfo(text[i], out ci, size)) continue;
                float gx = x + ci.minX;
                float gy = baseline - ci.maxY;
                float gw = ci.glyphWidth;
                float gh = ci.glyphHeight;
                if (gw > 0 && gh > 0)
                {
                    // Грубый клип: отбрасываем только целиком вышедшие глифы.
                    // Точный клип частично видимых глифов — scissors в слое 3.
                    bool inside = true;
                    if (clipRect.HasValue)
                    {
                        Rect cr = clipRect.Value;
                        if (gx + gw < cr.x || gx > cr.xMax || gy + gh < cr.y || gy > cr.yMax)
                            inside = false;
                    }
                    if (inside)
                    {
                        Vector2 uvTL = ci.uvTopLeft;
                        Vector2 uvBL = ci.uvBottomLeft;
                        Vector2 uvBR = ci.uvBottomRight;
                        Vector2 uvTR = ci.uvTopRight;
                        if (ci.flipped) // на части платформ UV глифов перевёрнуты
                        {
                            Vector2 t1 = uvTL; uvTL = uvBL; uvBL = t1;
                            Vector2 t2 = uvTR; uvTR = uvBR; uvBR = t2;
                        }
                        int i0 = tv.Count;
                        tv.Add(ToScreen(gx, gy)); tuv.Add(uvTL);
                        tv.Add(ToScreen(gx, gy + gh)); tuv.Add(uvBL);
                        tv.Add(ToScreen(gx + gw, gy + gh)); tuv.Add(uvBR);
                        tv.Add(ToScreen(gx + gw, gy)); tuv.Add(uvTR);
                        for (int k = 0; k < 4; k++) tc.Add(c);
                        tt.Add(i0); tt.Add(i0 + 1); tt.Add(i0 + 2);
                        tt.Add(i0); tt.Add(i0 + 2); tt.Add(i0 + 3);
                        triCount += 2;
                    }
                }
                x += ci.advance;
            }
        }
    }
}
