using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Znd.Mock {

    /// <summary>The amber lightning bolt that marks "game code can move this". Painted so it stays crisp in a narrow slot.</summary>
    public class Bolt : VisualElement {
        public static readonly Color Amber = new Color(1f, 150f / 255f, 40f / 255f);

        public Bolt() { AddToClassList("znd-bolt"); generateVisualContent += Paint; }

        void Paint(MeshGenerationContext ctx) {
            var r = contentRect;
            float s = Mathf.Min(r.width, r.height) * 0.72f;
            if (s <= 1f) return;
            float cx = r.x + r.width * 0.5f, cy = r.y + r.height * 0.5f;
            Vector2 P(float x, float y) => new Vector2(cx + (x - 0.5f) * s * 0.62f, cy + (y - 0.5f) * s);
            var p = ctx.painter2D;
            p.fillColor = Amber;
            p.BeginPath();
            p.MoveTo(P(0.72f, 0f)); p.LineTo(P(0.18f, 0.56f)); p.LineTo(P(0.50f, 0.56f));
            p.LineTo(P(0.30f, 1f)); p.LineTo(P(0.84f, 0.42f)); p.LineTo(P(0.52f, 0.42f));
            p.ClosePath(); p.Fill();
        }
    }

    /// <summary>A small waveform-shape icon (sine, triangle, saw, square, random), painted in the element's text colour.</summary>
    public class WaveIcon : VisualElement {
        readonly IconShape shape;

        public WaveIcon(IconShape shape) {
            this.shape = shape;
            pickingMode = PickingMode.Ignore;
            AddToClassList("znd-waveicon");
            generateVisualContent += Paint;
        }

        void Paint(MeshGenerationContext ctx) {
            var r = contentRect;
            if (r.width < 2f) return;
            var p = ctx.painter2D;
            p.strokeColor = resolvedStyle.color; p.lineWidth = 1.3f; p.lineJoin = LineJoin.Round;
            p.BeginPath();
            int n = 24;
            for (int i = 0; i <= n; i++) {
                float t = i / (float)n, y;
                switch (shape) {
                    case IconShape.Triangle: y = 1f - Mathf.Abs(((t * 2f) % 2f) - 1f) * 2f; break;
                    case IconShape.Saw: y = ((t * 1.5f) % 1f) * 2f - 1f; break;
                    case IconShape.Square: y = Mathf.Sin(t * Mathf.PI * 2f) >= 0f ? 1f : -1f; break;
                    case IconShape.Random: y = Mathf.Sin(t * 19f) * 0.6f + Mathf.Sin(t * 41f) * 0.4f; break;
                    default: y = Mathf.Sin(t * Mathf.PI * 2f); break;
                }
                var pt = new Vector2(r.x + t * r.width, r.y + (0.5f - y * 0.45f) * r.height);
                if (i == 0) p.MoveTo(pt); else p.LineTo(pt);
            }
            p.Stroke();
        }
    }

    /// <summary>A canvas painted as filled rectangles by a callback (the analyser's lanes and bars).</summary>
    public class RectCanvas : VisualElement {
        readonly Action<Rect, Action<Rect, Color>> paint;

        public RectCanvas(Action<Rect, Action<Rect, Color>> paint) {
            this.paint = paint;
            generateVisualContent += ctx => {
                var area = contentRect;
                if (area.width <= 0f || area.height <= 0f) return;
                var p = ctx.painter2D;
                paint(area, (rect, c) => {
                    if (rect.width <= 0f || rect.height <= 0f) return;
                    p.fillColor = c;
                    p.BeginPath();
                    p.MoveTo(rect.min); p.LineTo(new Vector2(rect.xMax, rect.yMin)); p.LineTo(rect.max); p.LineTo(new Vector2(rect.xMin, rect.yMax));
                    p.ClosePath(); p.Fill();
                });
            };
        }
    }

    /// <summary>The fake waveform picture: black bursts on transparent, made once from a fixed pattern (no audio involved).</summary>
    public static class FakeWaveform {
        static Texture2D s_tex;

        public static Texture2D Texture {
            get {
                if (s_tex != null) return s_tex;
                const int W = 1024, H = 128;
                s_tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
                var px = new Color32[W * H];
                float[] bursts = { 0.05f, 0.30f, 0.47f, 0.66f, 0.84f };
                float[] widths = { 0.16f, 0.12f, 0.13f, 0.12f, 0.1f };
                uint h = 12345;
                for (int x = 0; x < W; x++) {
                    float t = x / (float)W, env = 0.02f;
                    for (int b = 0; b < bursts.Length; b++) {
                        float u = (t - bursts[b]) / widths[b];
                        if (u >= 0f && u <= 1f) env = Mathf.Max(env, Mathf.Pow(Mathf.Sin(u * Mathf.PI), 0.6f) * (0.55f + 0.4f * Mathf.Sin(u * 23f + b)));
                    }
                    h ^= h << 13; h ^= h >> 17; h ^= h << 5;
                    float noise = 0.35f + 0.65f * ((h & 0xFFFF) / 65535f);
                    if ((h & 0x3F) == 0) noise = 1.4f;   // occasional spikes
                    float amp = Mathf.Clamp01(env * noise) * 0.48f;
                    int y0 = Mathf.RoundToInt((0.5f - amp) * H), y1 = Mathf.RoundToInt((0.5f + amp) * H);
                    for (int y = 0; y < H; y++) px[y * W + x] = (y >= y0 && y <= y1) || y == H / 2 ? new Color32(0, 0, 0, 255) : new Color32(0, 0, 0, 0);
                }
                s_tex.SetPixels32(px);
                s_tex.Apply();
                return s_tex;
            }
        }
    }
}
