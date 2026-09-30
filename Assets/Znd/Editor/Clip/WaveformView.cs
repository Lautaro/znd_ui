using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Znd.Mock {

    /// <summary>
    /// The clip's waveform block: a toolbar (Trim / Clamp, the Volume, Pitch and Time curves each with an edit pencil and
    /// an eye, Keep length, the length) and the 150 px waveform with its trim dims and handles, the looper's crossmix
    /// spans, the curves drawn over it, their axes, the combined-result lines and the playhead.
    ///
    /// Interaction: drag a trim handle (right-drag moves both), mouse wheel zooms, a curve's pencil makes it the one that
    /// takes presses (only one at a time), its eye hides it. While playing, the playhead sweeps the trimmed range and
    /// the curves show dotted "what this play hears" lines. All mock.
    /// </summary>
    public class WaveformView : VisualElement {

        const float AreaH = 150f, HandleW = 3f, Slop = 3f;
        readonly ClipData clip;
        readonly ToggleButton trim, clamp, vol, volEdit, pitch, pitchEdit, keepLen, time, timeEdit, volEye, pitchEye, timeEye;
        readonly Label lengthLabel, pitchOld;
        readonly VisualElement area, bg, dimStart, dimEnd, handleStart, handleEnd, head, combined, pitchLine, timeLine;
        readonly Label pTop, pMid, pBot, tTop, tMid, tBot;
        readonly VisualElement[] xmix = new VisualElement[4];
        readonly Image wave;
        readonly CurveEditor volCurve, pitchCurve, timeCurve;
        bool volShown = true, pitchShown = true, timeShown = true;
        int selectedCurve = -1;       // 0 volume, 1 pitch, 2 time
        float viewStart, viewEnd;     // seconds shown (wheel zoom)
        int trimDrag;                 // 0 none, 1 start, 2 end, 3 both
        float trimAnchor, trimAnchorStart, trimAnchorEnd;

        static readonly Color Bg = new Color32(252, 192, 7, 255), Dim = new Color(0f, 0f, 0f, 0.5f);
        static readonly Color VolumeColor = new Color(0.1f, 0.7f, 0.1f), PitchColor = new Color(0.9f, 0.2f, 0.1f), TimeColor = new Color(0.30f, 0.85f, 1f);
        static readonly Color HeadColor = new Color(0.1f, 0.1f, 0.9f, 0.75f);

        public WaveformView(ClipData clip) {
            this.clip = clip;
            AddToClassList("znd-waveform");
            style.flexShrink = 0;
            viewStart = 0f; viewEnd = clip.length;

            // ── toolbar ──
            var bar = Ui.Row(Ui.LineH);
            trim = Ui.Toggle("Trim", "Trim the start and end off the source.", clip.trim, v => { clip.trim = v; Edited(); }, "RichToggle", Corners.Left, 60f, Ui.LineH);
            clamp = Ui.Toggle("Clamp", "Fit the curves to the trimmed part only.", clip.clamp, v => { clip.clamp = v; Edited(); }, "RichToggle", Corners.Right, 60f, Ui.LineH);
            vol = Ui.Toggle("Volume", "A volume curve over the play.", clip.volumeCurveOn, v => { clip.volumeCurveOn = v; Edited(); }, "RichToggle", Corners.Left, 75f, Ui.LineH);
            volEdit = Pencil(0); volEye = Eye(0, "volume");
            pitch = Ui.Toggle("Pitch", "A pitch curve over the play.", clip.pitchCurveOn, v => { clip.pitchCurveOn = v; Edited(); }, "RichToggle", Corners.Left, 65f, Ui.LineH);
            pitchEdit = Pencil(1); pitchEye = Eye(1, "pitch");
            pitchOld = Ui.Text("⚠", "Mock warning slot: shown when a curve needs attention.", "znd-warnmark");
            pitchOld.style.width = 16f; pitchOld.style.visibility = Visibility.Hidden;
            keepLen = Ui.Toggle("Keep length", "Keep the sound's length when the pitch curve changes its speed.", clip.keepLength, v => { clip.keepLength = v; Edited(); }, "RichToggle", Corners.All, 90f, Ui.LineH);
            time = Ui.Toggle("Time", "A speed curve over the play (faster or slower without changing pitch).", clip.timeCurveOn, v => { clip.timeCurveOn = v; Edited(); }, "RichToggle", Corners.Left, 60f, Ui.LineH);
            timeEdit = Pencil(2); timeEye = Eye(2, "time");
            lengthLabel = Ui.Text("", "Length of the part that plays.", "znd-mini");
            lengthLabel.style.width = 50f;
            foreach (var e in new VisualElement[] { trim, clamp, Ui.Gap(6f), vol, volEdit, Ui.Gap(1f), volEye, Ui.Gap(6f), pitch, pitchEdit, Ui.Gap(1f), pitchEye, pitchOld,
                                                    Ui.Gap(2f), keepLen, Ui.Gap(6f), time, timeEdit, Ui.Gap(1f), timeEye, Ui.Flex(), lengthLabel })
                bar.Add(e);
            Add(bar);
            Add(Ui.Space(5f));

            // ── the area ──
            var box = new VisualElement();
            box.AddToClassList("znd-waveform-box");
            box.style.height = AreaH; box.style.flexShrink = 0;
            Add(box);
            area = new VisualElement { tooltip = "Mouse wheel: zoom. Drag the white handles to trim (right-drag moves both)." };
            area.AddToClassList("znd-waveform-area");
            area.style.position = Position.Absolute; area.style.left = 4; area.style.right = 4; area.style.top = 4; area.style.bottom = 4;
            box.Add(area);
            bg = Ui.Abs(); area.Add(bg);
            wave = new Image { image = FakeWaveform.Texture, scaleMode = ScaleMode.StretchToFill, pickingMode = PickingMode.Ignore };
            wave.style.position = Position.Absolute; wave.style.top = 0; wave.style.bottom = 0;
            area.Add(wave);
            dimStart = Ui.Abs(); dimEnd = Ui.Abs(); area.Add(dimStart); area.Add(dimEnd);
            for (int i = 0; i < 4; i++) { xmix[i] = Ui.Abs(); xmix[i].AddToClassList("znd-xmix"); if (i >= 2) xmix[i].AddToClassList("znd-xmix--min"); area.Add(xmix[i]); }
            pitchLine = Ui.Abs(); area.Add(pitchLine);
            pTop = Axis("+12 st"); pMid = Axis("0 st"); pBot = Axis("−12 st");
            timeLine = Ui.Abs(); area.Add(timeLine);
            tTop = Axis("×4"); tMid = Axis("×1"); tBot = Axis("×¼");
            foreach (var l in new[] { tTop, tMid, tBot }) { l.style.unityTextAlign = TextAnchor.UpperRight; l.style.width = 26f; }
            combined = Ui.Abs();
            combined.generateVisualContent += PaintCombined;
            area.Add(combined);
            head = Playhead(); area.Add(head);
            volCurve = Overlay(clip.volumeCurve, VolumeColor);
            pitchCurve = Overlay(clip.pitchCurve, PitchColor);
            timeCurve = Overlay(clip.timeCurve, TimeColor);
            handleStart = Ui.Abs(); handleEnd = Ui.Abs();
            handleStart.AddToClassList("znd-trimhandle"); handleEnd.AddToClassList("znd-trimhandle");
            area.Add(handleStart); area.Add(handleEnd);

            area.RegisterCallback<WheelEvent>(OnWheel);
            area.RegisterCallback<PointerDownEvent>(OnDown, TrickleDown.TrickleDown);
            area.RegisterCallback<PointerMoveEvent>(OnMove);
            area.RegisterCallback<PointerUpEvent>(OnUp);
            area.RegisterCallback<GeometryChangedEvent>(_ => Refresh());
            schedule.Execute(Refresh).Every(33);
        }

        void Edited() { Refresh(); MockPlayer.Edited(clip); }

        Label Axis(string text) {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.AddToClassList("znd-lbl"); l.AddToClassList("znd-mini"); l.AddToClassList("znd-axislabel");
            l.style.position = Position.Absolute; l.style.width = 44f; l.style.height = 13f;
            area.Add(l);
            return l;
        }

        VisualElement Playhead() {
            var root = Ui.Abs();
            root.AddToClassList("znd-playhead");
            root.style.width = 0f; root.style.top = 0; root.style.bottom = 0;
            var line = Ui.Abs();
            line.style.left = -0.75f; line.style.width = 1.5f; line.style.top = 8f; line.style.bottom = 0; line.style.backgroundColor = HeadColor;
            var cap = new VisualElement { pickingMode = PickingMode.Ignore };
            cap.style.position = Position.Absolute; cap.style.left = -3f; cap.style.top = 0; cap.style.width = 6f; cap.style.height = 11f;
            cap.generateVisualContent += ctx => {
                var p = ctx.painter2D; p.fillColor = HeadColor;
                p.BeginPath(); p.MoveTo(new Vector2(0, 0)); p.LineTo(new Vector2(6, 0)); p.LineTo(new Vector2(6, 6)); p.LineTo(new Vector2(3, 11)); p.LineTo(new Vector2(0, 6)); p.ClosePath(); p.Fill();
            };
            root.Add(line); root.Add(cap);
            return root;
        }

        CurveEditor Overlay(Curve c, Color col) {
            var ce = new CurveEditor(c, col) { editable = false, pickingMode = PickingMode.Ignore };
            ce.style.position = Position.Absolute;
            ce.onChanged = () => MockPlayer.Edited(clip);
            ce.onPointContext = (p, world) => RandomPointPopup.Show(world, p, c, ce.Refresh);
            area.Add(ce);
            return ce;
        }

        ToggleButton Pencil(int which) {
            var t = Ui.Toggle("", "Edit this curve on the waveform (one curve at a time).", false, v => { selectedCurve = v ? which : -1; Refresh(); }, "RichToggle", Corners.Right, 25f, Ui.LineH);
            t.markWhenOn = false;
            var img = new Image { image = EditorGUIUtility.IconContent("d_editicon.sml").image, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            img.AddToClassList("znd-editicon");
            t.Add(img);
            return t;
        }

        ToggleButton Eye(int which, string name) => Ui.Eye(true,
            v => v ? "Shown: the " + name + " curve is drawn. Click to hide it (it still plays)." : "Hidden: the " + name + " curve is not drawn. Click to show it.",
            v => { if (which == 0) volShown = v; else if (which == 1) pitchShown = v; else timeShown = v; Refresh(); });

        // ─────────── mapping ───────────

        Rect AreaRect => new Rect(0f, 0f, area.contentRect.width, area.contentRect.height);
        float XOf(float seconds, Rect r) => r.x + (seconds - viewStart) / Mathf.Max(1e-4f, viewEnd - viewStart) * r.width;
        float SecondsAt(float x, Rect r) => viewStart + (x - r.x) / Mathf.Max(1f, r.width) * (viewEnd - viewStart);

        Rect Trimmed(Rect r) => clip.trim ? Rect.MinMaxRect(XOf(clip.trimStart, r), r.y, XOf(clip.trimEnd, r), r.yMax) : Rect.MinMaxRect(XOf(0f, r), r.y, XOf(clip.length, r), r.yMax);

        // ─────────── refresh ───────────

        public void Refresh() {
            if (panel == null) return;
            trim.SetValueWithoutNotify(clip.trim); clamp.SetValueWithoutNotify(clip.clamp);
            vol.SetValueWithoutNotify(clip.volumeCurveOn); pitch.SetValueWithoutNotify(clip.pitchCurveOn);
            keepLen.SetValueWithoutNotify(clip.keepLength); time.SetValueWithoutNotify(clip.timeCurveOn);
            volEdit.SetValueWithoutNotify(selectedCurve == 0); pitchEdit.SetValueWithoutNotify(selectedCurve == 1); timeEdit.SetValueWithoutNotify(selectedCurve == 2);
            volEye.style.visibility = clip.volumeCurveOn ? Visibility.Visible : Visibility.Hidden;
            pitchEye.style.visibility = clip.pitchCurveOn ? Visibility.Visible : Visibility.Hidden;
            timeEye.style.visibility = clip.timeCurveOn ? Visibility.Visible : Visibility.Hidden;
            lengthLabel.text = (clip.trim ? clip.trimEnd - clip.trimStart : clip.length).ToString("0.000") + "s";

            var r = AreaRect;
            if (r.width <= 1f || r.height <= 1f) return;
            Ui.PlaceRect(bg, r); bg.style.backgroundColor = Bg;
            float x0 = XOf(0f, r), x1 = XOf(clip.length, r);
            wave.style.left = x0; wave.style.width = x1 - x0;

            var trimmed = Trimmed(r);
            bool t = clip.trim;
            dimStart.style.display = dimEnd.style.display = handleStart.style.display = handleEnd.style.display = t ? DisplayStyle.Flex : DisplayStyle.None;
            if (t) {
                Ui.PlaceRect(dimStart, Rect.MinMaxRect(r.x, r.y, Mathf.Max(r.x, trimmed.x), r.yMax));
                Ui.PlaceRect(dimEnd, Rect.MinMaxRect(Mathf.Min(r.xMax, trimmed.xMax), r.y, r.xMax, r.yMax));
                dimStart.style.backgroundColor = dimEnd.style.backgroundColor = Dim;
                Ui.PlaceRect(handleStart, new Rect(trimmed.x - HandleW * 0.5f, r.y, HandleW, r.height));
                Ui.PlaceRect(handleEnd, new Rect(trimmed.xMax - HandleW * 0.5f, r.y, HandleW, r.height));
            }

            // Looper crossmix spans at both ends of the loop.
            bool loop = clip.looper;
            for (int i = 0; i < 4; i++) xmix[i].style.display = loop ? DisplayStyle.Flex : DisplayStyle.None;
            if (loop) {
                float from = t ? clip.trimStart : 0f, to = t ? clip.trimEnd : clip.length;
                Ui.PlaceRect(xmix[0], Rect.MinMaxRect(XOf(from, r), r.y, XOf(from + clip.crossHi, r), r.yMax));
                Ui.PlaceRect(xmix[1], Rect.MinMaxRect(XOf(to - clip.crossHi, r), r.y, XOf(to, r), r.yMax));
                Ui.PlaceRect(xmix[2], Rect.MinMaxRect(XOf(from, r), r.y, XOf(from + clip.crossLo, r), r.yMax));
                Ui.PlaceRect(xmix[3], Rect.MinMaxRect(XOf(to - clip.crossLo, r), r.y, XOf(to, r), r.yMax));
            }

            // Playhead: sweeps the trimmed range while the mock plays.
            head.style.display = MockPlayer.Playing ? DisplayStyle.Flex : DisplayStyle.None;
            if (MockPlayer.Playing) { head.style.left = trimmed.x + MockPlayer.Progress * trimmed.width; head.style.top = r.y; head.style.height = r.height; }

            var env = clip.clamp ? trimmed : Rect.MinMaxRect(x0, r.y, x1, r.yMax);
            PlaceAxes(env);
            UpdateCurve(volCurve, 0, clip.volumeCurveOn, volShown, env);
            UpdateCurve(pitchCurve, 1, clip.pitchCurveOn, pitchShown, env);
            UpdateCurve(timeCurve, 2, clip.timeCurveOn, timeShown, env);
            Ui.PlaceRect(combined, r);
            combined.MarkDirtyRepaint();
            handleStart.BringToFront(); handleEnd.BringToFront();
        }

        void PlaceAxes(Rect env) {
            bool p = clip.pitchCurveOn && pitchShown, tm = clip.timeCurveOn && timeShown;
            foreach (var e in new VisualElement[] { pTop, pMid, pBot, pitchLine }) e.style.display = p ? DisplayStyle.Flex : DisplayStyle.None;
            foreach (var e in new VisualElement[] { tTop, tMid, tBot, timeLine }) e.style.display = tm ? DisplayStyle.Flex : DisplayStyle.None;
            float my = Mathf.Round(env.y + env.height * 0.5f);
            if (p) {
                foreach (var l in new[] { pTop, pMid, pBot }) { l.style.left = env.x + 3f; l.style.color = new Color(PitchColor.r, PitchColor.g, PitchColor.b, 0.9f); }
                pTop.style.top = env.y + 1f; pMid.style.top = my - 14f; pBot.style.top = env.yMax - 14f;
                Ui.PlaceRect(pitchLine, new Rect(env.x, my, env.width, 1f));
                pitchLine.style.backgroundColor = new Color(PitchColor.r, PitchColor.g, PitchColor.b, 0.35f);
            }
            if (tm) {
                foreach (var l in new[] { tTop, tMid, tBot }) { l.style.left = env.xMax - 29f; l.style.color = TimeColor; }
                tTop.style.top = env.y + 1f; tMid.style.top = my - 14f; tBot.style.top = env.yMax - 14f;
                Ui.PlaceRect(timeLine, new Rect(env.x, my, env.width, 1f));
                timeLine.style.backgroundColor = new Color(TimeColor.r, TimeColor.g, TimeColor.b, 0.3f);
            }
        }

        void UpdateCurve(CurveEditor ce, int which, bool on, bool shown, Rect env) {
            bool selected = selectedCurve == which;
            bool draw = on && (shown || selected);
            ce.style.display = draw ? DisplayStyle.Flex : DisplayStyle.None;
            if (!draw) return;
            Ui.PlaceRect(ce, env);
            ce.editable = selected;
            ce.pickingMode = selected ? PickingMode.Position : PickingMode.Ignore;
            // What the current play "hears", dotted: the curve nudged by a canned per-play wobble.
            if (MockPlayer.Playing && shown) {
                float seed = MockSignal.Jitter("play" + which, (float)EditorApplication.timeSinceStartup, 4f) - 0.5f;
                var c = ce.curve;
                ce.liveCurves = new List<System.Func<float, float>> { x => c.Evaluate(x) + seed * 0.12f * Mathf.Sin(x * 6.283f) };
            }
            else ce.liveCurves = null;
            ce.Refresh();
        }

        /// <summary>The combined result of everything moving a curve's value (lighter and thinner than the curve itself).</summary>
        void PaintCombined(MeshGenerationContext ctx) {
            var r = AreaRect;
            var env = clip.clamp ? Trimmed(r) : Rect.MinMaxRect(XOf(0f, r), r.y, XOf(clip.length, r), r.yMax);
            var p = ctx.painter2D;
            p.lineWidth = 1.25f;
            void Line(Curve c, Color col, System.Func<float, float, float> f) {
                p.strokeColor = new Color(col.r, col.g, col.b, 0.55f);
                p.BeginPath();
                int n = Mathf.Max(2, (int)(env.width / 3f));
                for (int i = 0; i <= n; i++) {
                    float x = i / (float)n, y = Mathf.Clamp01(f(x, c.Evaluate(x)));
                    var pt = new Vector2(env.x + x * env.width, env.y + (1f - y) * env.height);
                    if (i == 0) p.MoveTo(pt); else p.LineTo(pt);
                }
                p.Stroke();
            }
            if (clip.volumeCurveOn && volShown) Line(clip.volumeCurve, VolumeColor, (x, v) => v * (0.75f + 0.25f * Mathf.Sin(x * 3.1f)));
            if (clip.pitchCurveOn && pitchShown) {
                var step = clip.modifiers.Find(m => m.type == ModifierType.Step && m.enabled);
                if (step != null) Line(clip.pitchCurve, PitchColor, (x, v) => v + step.steps[Mathf.Min(step.steps.Length - 1, (int)(x * step.steps.Length))] * 0.08f);
            }
        }

        // ─────────── input ───────────

        void OnWheel(WheelEvent e) {
            var r = AreaRect;
            float at = SecondsAt(e.localMousePosition.x, r);
            float span = viewEnd - viewStart, ns = Mathf.Clamp(span * (e.delta.y > 0f ? 1.15f : 1f / 1.15f), 0.1f, clip.length);
            float k = (at - viewStart) / span;
            viewStart = Mathf.Clamp(at - ns * k, 0f, clip.length - ns); viewEnd = viewStart + ns;
            Refresh();
            e.StopPropagation();
        }

        void OnDown(PointerDownEvent e) {
            if (!clip.trim) return;
            var r = AreaRect; var tr = Trimmed(r);
            float x = e.localPosition.x;
            bool nearStart = Mathf.Abs(x - tr.x) <= HandleW * 0.5f + Slop, nearEnd = Mathf.Abs(x - tr.xMax) <= HandleW * 0.5f + Slop;
            if (!nearStart && !nearEnd) return;
            trimDrag = e.button == 1 ? 3 : nearEnd && (!nearStart || x > (tr.x + tr.xMax) * 0.5f) ? 2 : 1;
            trimAnchor = SecondsAt(x, r); trimAnchorStart = clip.trimStart; trimAnchorEnd = clip.trimEnd;
            area.CapturePointer(e.pointerId);
            e.StopImmediatePropagation();
        }

        void OnMove(PointerMoveEvent e) {
            if (trimDrag == 0 || !area.HasPointerCapture(e.pointerId)) return;
            float s = Mathf.Clamp(SecondsAt(e.localPosition.x, AreaRect), 0f, clip.length);
            if (trimDrag == 1) clip.trimStart = Mathf.Min(s, clip.trimEnd - 0.02f);
            else if (trimDrag == 2) clip.trimEnd = Mathf.Max(s, clip.trimStart + 0.02f);
            else {
                float d = s - trimAnchor, len = trimAnchorEnd - trimAnchorStart;
                clip.trimStart = Mathf.Clamp(trimAnchorStart + d, 0f, clip.length - len); clip.trimEnd = clip.trimStart + len;
            }
            Refresh();
        }

        void OnUp(PointerUpEvent e) {
            if (trimDrag == 0) return;
            trimDrag = 0;
            if (area.HasPointerCapture(e.pointerId)) area.ReleasePointer(e.pointerId);
            MockPlayer.Edited(clip);
        }
    }
}
