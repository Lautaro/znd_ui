using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Znd.Mock {

    /// <summary>
    /// Draws and edits a <see cref="Curve"/>:
    /// - press a point to drag it (and select it); double-click a point to delete it;
    /// - press on the line to add a point there and drag it; double-click empty space to add one;
    /// - Shift + drag on the line moves that whole segment; Shift + right-drag bends it;
    /// - drag on empty space to box-select several points, then drag to move them together; Delete removes the selection;
    /// - right-click a point for its settings (<see cref="onPointContext"/>).
    /// While the sound plays, <see cref="liveCurves"/> are drawn dotted over it: what each play is "hearing".
    /// </summary>
    public class CurveEditor : VisualElement {

        public Curve curve;
        public Color color;
        public float thickness = 1.5f;
        public float handleRadius = 4f;
        public bool editable = true, showHandles = true;
        public Action onChanged;
        public Action<CurvePoint, Vector2> onPointContext;
        public List<Func<float, float>> liveCurves;

        static readonly Color SelectedLine = new Color(0.1f, 0.7f, 0.9f), SelectedHandle = new Color(0.1f, 0.75f, 0.85f);

        CurvePoint dragged; int draggedSeg = -1, bentSeg = -1;
        bool boxing, pressed, multi, hovering, shift;
        Vector2 boxStart, pointer = new Vector2(-1, -1);
        readonly List<CurvePoint> selected = new List<CurvePoint>();

        public CurveEditor(Curve curve, Color color) {
            this.curve = curve; this.color = color;
            focusable = true;
            AddToClassList("znd-curve");
            generateVisualContent += Paint;
            RegisterCallback<PointerDownEvent>(OnDown);
            RegisterCallback<PointerMoveEvent>(OnMove);
            RegisterCallback<PointerUpEvent>(OnUp);
            RegisterCallback<PointerEnterEvent>(_ => hovering = true);
            RegisterCallback<PointerLeaveEvent>(_ => { hovering = false; MarkDirtyRepaint(); });
            RegisterCallback<KeyDownEvent>(OnKey);
        }

        Vector2 Size => contentRect.size;
        float XR => curve.xMax - curve.xMin;
        float YR => curve.yMax - curve.yMin;
        Vector2 ToLocal(float t, float v) => new Vector2((t - curve.xMin) / XR * Size.x, Size.y - (v - curve.yMin) / YR * Size.y);
        Vector2 ToLocal(CurvePoint p) => ToLocal(p.time, p.value);
        float TimeAt(float x) => curve.xMin + XR * x / Mathf.Max(1f, Size.x);
        float ValueAt(float y) => curve.yMin + YR * (1f - y / Mathf.Max(1f, Size.y));
        float LineY(float x) => ToLocal(TimeAt(x), curve.Evaluate(TimeAt(x))).y;
        bool Inside(Vector2 l) => l.x >= 0f && l.y >= 0f && l.x <= Size.x && l.y <= Size.y;
        bool OnLine(Vector2 l) => Inside(l) && Mathf.Abs(l.y - LineY(l.x)) <= 4f;

        CurvePoint PointAt(Vector2 l) {
            foreach (var p in curve.points) if ((ToLocal(p) - l).sqrMagnitude <= handleRadius * handleRadius * 2.2f) return p;
            return null;
        }

        void Changed() { onChanged?.Invoke(); MarkDirtyRepaint(); }
        public void Refresh() => MarkDirtyRepaint();

        void OnDown(PointerDownEvent e) {
            if (curve == null || !editable) return;
            var l = (Vector2)e.localPosition;
            shift = e.shiftKey;
            Focus();
            var hit = PointAt(l);
            if (hit != null && e.button == 1 && !e.shiftKey) { onPointContext?.Invoke(hit, this.LocalToWorld(ToLocal(hit))); e.StopPropagation(); return; }
            if (hit != null && e.button == 0) {
                if (e.clickCount == 2) { selected.Clear(); curve.Remove(hit); Changed(); }
                else { dragged = hit; if (!selected.Contains(hit)) { selected.Clear(); selected.Add(hit); } Capture(e); }
                e.StopPropagation();
                return;
            }
            if (OnLine(l)) {
                float t = TimeAt(l.x);
                if (e.shiftKey) {
                    int i = curve.SegmentAt(t);
                    if (i > 0) { if (e.button == 0) draggedSeg = i; else bentSeg = i; selected.Clear(); Capture(e); }
                }
                else if (e.button == 0) {
                    dragged = curve.Add(t, curve.Evaluate(t));
                    selected.Clear(); selected.Add(dragged);
                    Changed(); Capture(e);
                }
                e.StopPropagation();
                return;
            }
            if (e.button == 0 && Inside(l)) {
                if (e.clickCount == 2) { curve.Add(TimeAt(l.x), ValueAt(l.y)); Changed(); }
                else if (selected.Count > 1) { pressed = true; multi = false; boxStart = l; Capture(e); }
                else { boxing = true; boxStart = l; selected.Clear(); Capture(e); }
                e.StopPropagation();
            }
        }

        void Capture(PointerDownEvent e) { pressed = true; this.CapturePointer(e.pointerId); MarkDirtyRepaint(); }

        void OnMove(PointerMoveEvent e) {
            if (curve == null) return;
            var l = (Vector2)e.localPosition;
            pointer = l; shift = e.shiftKey; hovering = true;
            var d = (Vector2)e.deltaPosition;
            if (pressed && this.HasPointerCapture(e.pointerId)) {
                if (boxing) Box(l);
                else if (draggedSeg > 0) MovePoints(new[] { curve.points[draggedSeg - 1], curve.points[draggedSeg] }, d);
                else if (bentSeg > 0) {
                    var p = curve.points[bentSeg];
                    float dir = p.value < curve.points[bentSeg - 1].value ? -1f : 1f;
                    p.bend = Mathf.Clamp(p.bend * Mathf.Exp(dir * d.y / Mathf.Max(1f, Size.y) * 4f), 0.1f, 10f);
                    Changed();
                }
                else if (selected.Count > 1 && (dragged != null || d != Vector2.zero)) { multi = true; MovePoints(selected.ToArray(), d); }
                else if (dragged != null) { DragTo(dragged, l); Changed(); }
            }
            MarkDirtyRepaint();
        }

        void DragTo(CurvePoint p, Vector2 l) {
            int i = curve.points.IndexOf(p);
            float t = TimeAt(l.x), v = Mathf.Clamp(ValueAt(l.y), curve.yMin, curve.yMax);
            if (i == 0) t = curve.xMin;
            else if (i == curve.Count - 1) t = curve.xMax;
            else t = Mathf.Clamp(t, curve.points[i - 1].time, curve.points[i + 1].time);
            p.time = t; p.value = v;
        }

        void MovePoints(CurvePoint[] pts, Vector2 d) {
            float dt = d.x / Mathf.Max(1f, Size.x) * XR, dv = -d.y / Mathf.Max(1f, Size.y) * YR;
            foreach (var p in pts) {
                int i = curve.points.IndexOf(p);
                if (i < 0) continue;
                p.value = Mathf.Clamp(p.value + dv, curve.yMin, curve.yMax);
                if (i > 0 && i < curve.Count - 1) p.time = Mathf.Clamp(p.time + dt, curve.points[i - 1].time, curve.points[i + 1].time);
            }
            Changed();
        }

        void Box(Vector2 l) {
            var r = Rect.MinMaxRect(Mathf.Min(boxStart.x, l.x), Mathf.Min(boxStart.y, l.y), Mathf.Max(boxStart.x, l.x), Mathf.Max(boxStart.y, l.y));
            selected.Clear();
            foreach (var p in curve.points) if (r.Contains(ToLocal(p))) selected.Add(p);
        }

        void OnUp(PointerUpEvent e) {
            if (pressed && selected.Count > 1 && !multi && dragged == null && !boxing) selected.Clear();
            pressed = boxing = multi = false;
            dragged = null; draggedSeg = bentSeg = -1;
            if (this.HasPointerCapture(e.pointerId)) this.ReleasePointer(e.pointerId);
            MarkDirtyRepaint();
        }

        void OnKey(KeyDownEvent e) {
            if (e.keyCode != KeyCode.Delete || selected.Count == 0) return;
            foreach (var p in selected) curve.Remove(p);
            selected.Clear();
            Changed();
            e.StopPropagation();
        }

        // ─────────── drawing ───────────

        void Paint(MeshGenerationContext ctx) {
            if (curve == null || Size.x <= 1f || Size.y <= 1f) return;
            var p2 = ctx.painter2D;
            p2.lineJoin = LineJoin.Round; p2.lineCap = LineCap.Round;
            Stroke(p2, curve.xMin, curve.xMax, color, thickness);

            int seg = draggedSeg > 0 ? draggedSeg : bentSeg > 0 ? bentSeg : -1;
            if (seg < 0 && hovering && shift && OnLine(pointer)) seg = curve.SegmentAt(TimeAt(pointer.x));
            if (seg > 0) Stroke(p2, curve.points[seg - 1].time, curve.points[seg].time, SelectedLine, 1.5f);

            if (liveCurves != null)
                foreach (var f in liveCurves) {
                    if (f == null) continue;
                    int n = Mathf.Max(2, (int)(Size.x / 3f));
                    var pts = new Vector2[n + 1];
                    for (int i = 0; i <= n; i++) { float t = curve.xMin + XR * i / n; pts[i] = ToLocal(t, Mathf.Clamp(f(t), curve.yMin, curve.yMax)); }
                    Dotted(p2, pts, Color.Lerp(color, Color.white, 0.45f), 1.2f);
                }

            foreach (var p in curve.points)
                if (p.randomX > 0f || p.randomY > 0f)
                    Ellipse(p2, ToLocal(p), Mathf.Max(1f, p.randomX / XR * Size.x), Mathf.Max(1f, p.randomY / YR * Size.y),
                            new Color(color.r, color.g, color.b, 0.14f), new Color(color.r, color.g, color.b, 0.75f));

            if (showHandles)
                foreach (var p in curve.points) {
                    var c = ToLocal(p);
                    bool hi = editable && (p == dragged || (hovering && (c - pointer).sqrMagnitude <= handleRadius * handleRadius * 2.2f));
                    var col = color;
                    if (hi) { Color.RGBToHSV(col, out float h, out float s, out float v); col = Color.HSVToRGB(h, s * 0.8f, Mathf.Min(1f, v * 1.5f)); }
                    Disc(p2, c, handleRadius, col);
                }
            foreach (var p in selected) Disc(p2, ToLocal(p), handleRadius, SelectedHandle);

            if (editable && hovering && !pressed && !shift && PointAt(pointer) == null && OnLine(pointer))
                Disc(p2, new Vector2(pointer.x, LineY(pointer.x)), handleRadius, new Color(1f, 1f, 1f, 0.5f));

            if (boxing) {
                var r = Rect.MinMaxRect(Mathf.Min(boxStart.x, pointer.x), Mathf.Min(boxStart.y, pointer.y), Mathf.Max(boxStart.x, pointer.x), Mathf.Max(boxStart.y, pointer.y));
                p2.fillColor = new Color(1f, 1f, 1f, 0.1f); p2.strokeColor = Color.white; p2.lineWidth = 1f;
                p2.BeginPath(); p2.MoveTo(r.min); p2.LineTo(new Vector2(r.xMax, r.yMin)); p2.LineTo(r.max); p2.LineTo(new Vector2(r.xMin, r.yMax)); p2.ClosePath();
                p2.Fill(); p2.Stroke();
            }
        }

        void Stroke(Painter2D p2, float from, float to, Color c, float w) {
            int n = Mathf.Max(2, (int)((to - from) / XR * Size.x / 3f));
            p2.strokeColor = c; p2.lineWidth = w;
            p2.BeginPath();
            for (int i = 0; i <= n; i++) {
                float t = Mathf.Lerp(from, to, i / (float)n);
                var pt = ToLocal(t, curve.Evaluate(t));
                if (i == 0) p2.MoveTo(pt); else p2.LineTo(pt);
            }
            p2.Stroke();
        }

        public static void Disc(Painter2D p2, Vector2 c, float r, Color col) {
            p2.fillColor = col;
            p2.BeginPath(); p2.Arc(c, r, 0f, 360f); p2.ClosePath(); p2.Fill();
        }

        public static void Dotted(Painter2D p2, Vector2[] pts, Color c, float w) {
            p2.strokeColor = c; p2.lineWidth = w;
            const float dash = 3f, gap = 3f;
            float carry = 0f; bool on = true;
            p2.BeginPath();
            for (int i = 1; i < pts.Length; i++) {
                var a = pts[i - 1]; var b = pts[i];
                float len = Vector2.Distance(a, b), pos = 0f;
                while (pos < len) {
                    float step = Mathf.Min((on ? dash : gap) - carry, len - pos);
                    var s = Vector2.Lerp(a, b, pos / len); var e = Vector2.Lerp(a, b, (pos + step) / len);
                    if (on) { p2.MoveTo(s); p2.LineTo(e); }
                    pos += step; carry += step;
                    if (carry >= (on ? dash : gap) - 1e-4f) { carry = 0f; on = !on; }
                }
            }
            p2.Stroke();
        }

        public static void Ellipse(Painter2D p2, Vector2 c, float rx, float ry, Color fill, Color line) {
            p2.fillColor = fill; p2.strokeColor = line; p2.lineWidth = 1f;
            p2.BeginPath();
            for (int i = 0; i <= 32; i++) {
                float a = i / 32f * Mathf.PI * 2f;
                var pt = c + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
                if (i == 0) p2.MoveTo(pt); else p2.LineTo(pt);
            }
            p2.ClosePath(); p2.Fill(); p2.Stroke();
        }
    }
}
