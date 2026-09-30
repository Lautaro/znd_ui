using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Znd.Mock {

    /// <summary>The mock "pretend to be game code" values: while Drive is on, code-reachable modifiers follow these.</summary>
    public static class CodeTest {
        public class Row { public float value = 0.5f; public int pattern; public bool global; }   // pattern: 0 Hold, 1 Ramp, 2 Jitter
        public static bool Drive;
        public static int Instances = 3;
        public static readonly Dictionary<string, Row> Rows = new Dictionary<string, Row>();

        public static Row For(string id) { if (!Rows.TryGetValue(id, out var r)) Rows[id] = r = new Row(); return r; }

        public static float ValueFor(string id, float t, int index) {
            var r = For(id);
            float v = r.value;
            if (!r.global && r.pattern == 1) v += 0.3f * Mathf.Sin(t * Mathf.PI * 0.5f + index * 1.3f);
            else if (!r.global && r.pattern == 2) v += (MockSignal.Jitter(id + index, t, 0.3f) * 2f - 1f) * 0.3f;
            return Mathf.Clamp01(v);
        }
    }

    /// <summary>The snapshots strip: Capture, the glide time, then one chip per snapshot. Click a chip to "glide" to it (the
    /// chip fills over the glide time); right-click for Load / Capture again / Rename / Delete.</summary>
    public class SnapshotsRow : VisualElement {
        readonly ClipData clip;
        float glideMs = 800f;
        string gliding; double glideStart;
        readonly List<(string name, VisualElement chip, VisualElement fill)> chips = new List<(string, VisualElement, VisualElement)>();

        public SnapshotsRow(ClipData clip) {
            this.clip = clip;
            style.flexDirection = FlexDirection.Row; style.height = Ui.RowH; style.flexShrink = 0; style.marginBottom = 2f;
            Build();
            schedule.Execute(Tick).Every(33);
        }

        void Tick() {
            float p = gliding == null ? 0f : Mathf.Clamp01((float)(EditorApplication.timeSinceStartup - glideStart) * 1000f / Mathf.Max(1f, glideMs));
            foreach (var c in chips) {
                bool on = c.name == gliding;
                c.fill.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
                if (on) c.fill.style.width = Length.Percent(p * 100f);
                c.chip.EnableInClassList("znd-snapchip--target", on);
            }
        }

        void Build() {
            Clear(); chips.Clear();
            var title = Ui.Text("Snapshots", "Named sets of this sound's settings. Click one to glide there; right-click for more. Right-click this title for the whole list.", "znd-guilabel");
            title.style.width = 76f;
            title.RegisterCallback<PointerDownEvent>(e => {
                if (e.button != 1) return;
                Ui.Menu(new[] { "Default" }.Concat(clip.snapshots).Select(n => (n, (Action)(() => Glide(n)), false, true)).ToArray());
                e.StopPropagation();
            });
            Add(title);
            Add(Ui.Button("Capture", "Save the settings as they are now as a new snapshot.", "RichButton", () => { clip.snapshots.Add("Snapshot " + (clip.snapshots.Count + 1)); Build(); }, Corners.All, 64f, Ui.LineH));
            Add(Ui.Gap(6f));
            TrackSlider glide = null;
            glide = Ui.Slider("Glide " + glideMs.ToString("0") + " ms", glideMs, 0f, 5000f, "How long a glide takes when you click a snapshot here.",
                v => { glideMs = Mathf.Round(v / 10f) * 10f; glide.text = "Glide " + glideMs.ToString("0") + " ms"; }, TrackSlider.LabelMode.LabelOnly, 800f, "default", 130f, Ui.LineH);
            Add(glide);
            Add(Ui.Gap(8f));
            var strip = Ui.Row(-1f); strip.style.flexShrink = 1; strip.style.flexGrow = 1; strip.style.overflow = Overflow.Hidden;
            Add(strip);
            foreach (var n in new[] { "Default" }.Concat(clip.snapshots)) strip.Add(Chip(n));
        }

        VisualElement Chip(string name) {
            var chip = new VisualElement();
            chip.AddToClassList("znd-snapchip");
            var fill = new VisualElement { pickingMode = PickingMode.Ignore }; fill.AddToClassList("znd-snapchip__fill");
            var text = new Label(name) { pickingMode = PickingMode.Ignore }; text.AddToClassList("znd-snapchip__text");
            chip.Add(fill); chip.Add(text);
            chip.style.width = Mathf.Clamp(24f + name.Length * 6.5f, 56f, 140f); chip.style.height = Ui.LineH; chip.style.marginTop = 1f; chip.style.marginRight = 3f; chip.style.flexShrink = 0;
            chip.tooltip = name == "Default" ? "The settings as authored. Click to glide back to them." : "'" + name + "'. Click to glide to it; right-click for more.";
            chip.RegisterCallback<PointerDownEvent>(e => {
                if (e.button == 0) { Glide(name); e.StopPropagation(); }
                else if (e.button == 1) {
                    if (name == "Default") Ui.Menu(("Capture as new", () => { clip.snapshots.Add("Snapshot " + (clip.snapshots.Count + 1)); Build(); }, false, true));
                    else Ui.Menu(("Load into editor", () => { }, false, true), ("Capture again", () => { }, false, true),
                                 ("Rename…", () => NamePopup.Show(chip.worldBound, "Name", name, n => { clip.snapshots[clip.snapshots.IndexOf(name)] = n; Build(); }), false, true),
                                 ("Delete", () => { clip.snapshots.Remove(name); Build(); }, false, true));
                    e.StopPropagation();
                }
            });
            chips.Add((name, chip, fill));
            return chip;
        }

        void Glide(string name) { gliding = name; glideStart = EditorApplication.timeSinceStartup; }
    }

    /// <summary>"Code test": pretend to be the game and send values to the code-reachable modifiers while the sound plays.
    /// Hidden when no modifier has a code id.</summary>
    public class CodeTestPanel : VisualElement {
        readonly ClipData clip;
        string builtIds;

        public CodeTestPanel(ClipData clip) {
            this.clip = clip; style.flexShrink = 0;
            schedule.Execute(() => {
                var ids = string.Join("|", clip.modifiers.Where(m => m.HasCode).Select(m => m.codeId).Distinct());
                if (ids != builtIds) { builtIds = ids; Build(); }
            }).Every(100);
        }

        void Build() {
            Clear();
            var ids = clip.modifiers.Where(m => m.HasCode).Select(m => m.codeId).Distinct().ToList();
            style.display = ids.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            if (ids.Count == 0) return;
            var r = Ui.Row(); r.style.marginBottom = 1f;
            var title = Ui.Text("Code test", "Pretend to be the game: while Drive is on, playing copies of this sound follow the values below.", "znd-text-subheader", "znd-subheader");
            title.style.width = 128f;
            r.Add(title);
            ToggleButton drive = null;
            drive = Ui.Toggle("Drive", "On: plays follow the values below.", CodeTest.Drive, v => CodeTest.Drive = v, "RichToggle", Corners.All, 52f, Ui.LineH);
            r.Add(drive);
            r.Add(Ui.Gap(10f));
            var counts = new[] { 1, 3, 8 };
            var tg = new ToggleButton[3];
            for (int i = 0; i < 3; i++) {
                int n = counts[i], idx = i;
                tg[i] = Ui.Toggle("×" + n, "How many overlapping copies Play starts.", CodeTest.Instances == n, _ => { CodeTest.Instances = n; for (int k = 0; k < 3; k++) tg[k].value = k == idx; },
                                  "RichToggle", i == 0 ? Corners.Left : i == 2 ? Corners.Right : Corners.None, 30f, Ui.LineH);
                r.Add(tg[i]);
            }
            r.Add(Ui.Gap(4f));
            r.Add(Ui.Button("Play", "Start playing and turn Drive on.", "RichButton", () => { CodeTest.Drive = true; drive.value = true; MockPlayer.StartLoop(clip); }, Corners.All, 44f, Ui.LineH));
            r.Add(Ui.Gap(2f));
            r.Add(Ui.Button("Stop", "Stop the copies Play started.", "RichButton", MockPlayer.Stop, Corners.All, 44f, Ui.LineH));
            Add(r);
            foreach (var id in ids) Add(IdRow(id));
        }

        VisualElement IdRow(string id) {
            var row = CodeTest.For(id);
            var r = Ui.Row(); r.style.marginBottom = 1f;
            var label = Ui.Text("⚡ " + id, "What game code would send as '" + id + "'.", "znd-codemark");
            label.style.width = 128f; label.style.unityTextAlign = TextAnchor.MiddleLeft;
            r.Add(label);
            TrackSlider s = null;
            s = Ui.Slider("Value  " + row.value.ToString("0.00"), row.value, 0f, 1f, "The value sent (0..1).", v => { row.value = v; s.text = "Value  " + v.ToString("0.00"); },
                          TrackSlider.LabelMode.LabelOnly, 0.5f, "default", 200f, Ui.LineH);
            r.Add(s);
            r.Add(Ui.Gap(6f));
            string[] names = { "Hold", "Ramp", "Jitter" };
            var pt = new ToggleButton[3];
            for (int i = 0; i < 3; i++) {
                int idx = i;
                pt[i] = Ui.Toggle(names[i], names[i] + " pattern.", row.pattern == i, _ => { row.pattern = idx; for (int k = 0; k < 3; k++) pt[k].value = k == idx; },
                                  "RichToggle", i == 0 ? Corners.Left : i == 2 ? Corners.Right : Corners.None, 48f, Ui.LineH);
                pt[i].SetEnabled(!row.global);
                r.Add(pt[i]);
            }
            r.Add(Ui.Gap(6f));
            r.Add(Ui.Toggle("Project-wide", "Send one value for everything instead of per play.", row.global, v => { row.global = v; foreach (var p in pt) p.SetEnabled(!v); }, "RichToggle", Corners.All, 86f, Ui.LineH));
            return r;
        }
    }

    /// <summary>
    /// "Analyse": a view of what the chain is doing. Combined: a lane per modulated setting (where it sits across a play,
    /// with a moving now-line and dot), 72 frequency bars (green above the middle = louder, red below = quieter), frequency
    /// labels, a time line and a roster of the effects. Spectrum / Over time / Waveform: live pictures of the output.
    /// Every number and shape here is canned mock animation.
    /// </summary>
    public class AnalyserView : VisualElement {
        const float GraphH = 170f, LaneH = 30f, LaneLabelW = 150f;
        const int Bands = 72;
        static readonly float[] FreqTicks = { 20f, 50f, 100f, 200f, 500f, 1000f, 2000f, 5000f, 10000f, 20000f };
        static readonly Color Note = new Color(0.62f, 0.62f, 0.68f);

        readonly ClipData clip;
        readonly List<Action> perFrame = new List<Action>();
        bool open;
        int view;              // 0 Combined, 1 Spectrum, 2 Over time, 3 Waveform
        float gain = 1f;
        int hovered = -1;
        readonly Queue<float[]> history = new Queue<float[]>();
        string builtSig;

        public AnalyserView(ClipData clip) {
            this.clip = clip; style.flexShrink = 0;
            schedule.Execute(Tick).Every(33);
        }

        List<(string label, string key)> Lanes() {
            var list = new List<(string, string)>();
            foreach (var e in clip.effects) for (int k = 0; k < e.ps.Count; k++) if (clip.IsBound(e, k)) list.Add((e.name + " " + e.ps[k].name.ToLower(), e.name + k));
            foreach (var v in new[] { "Volume", "Pitch", "Speed", "Drive" }) if (clip.ModifiersOnOwn(v).Any()) list.Add(("Sound " + v.ToLower(), "own" + v));
            return list;
        }

        void Tick() {
            if (panel == null) return;
            string sig = open + "|" + view + "|" + string.Join(",", Lanes().Select(l => l.key)) + "|" + string.Join(",", clip.effects.Select(e => e.name + e.enabled));
            if (sig != builtSig) { builtSig = sig; Build(); }
            if (!open) return;
            foreach (var a in perFrame) a();
        }

        static Label Mini(string text, Color? c = null, TextAnchor a = TextAnchor.MiddleLeft) {
            var l = Ui.Text(text, "", "znd-mini");
            if (c.HasValue) l.style.color = c.Value;
            l.style.unityTextAlign = a;
            return l;
        }

        Label Status(Func<string> text) {
            var l = Mini("", Note);
            l.style.height = Ui.LineH; l.style.flexShrink = 0;
            perFrame.Add(() => l.text = text());
            return l;
        }

        void Build() {
            Clear(); perFrame.Clear();
            Add(Header());
            if (!open) return;
            Add(Status(() => view == 0
                ? (MockPlayer.Playing ? "Following the playing sound: the bars show the chain at this moment." : "Nothing is playing: the bars show the chain at its set values. Press Play to see it move.")
                : (MockPlayer.Playing ? "" : "")));
            var lanes = Lanes();
            for (int i = 0; i < lanes.Count; i++) Add(Lane(lanes[i].label, lanes[i].key));
            if (view == 0) {
                Add(Bars());
                Add(FreqLabels());
                Add(Status(() => MockPlayer.Playing ? "at " + MockPlayer.Elapsed.ToString("0.00") + " s of " + clip.PlaySeconds.ToString("0.00") + " s (following the play)" : "at 0.00 s (start of the play)"));
            }
            else {
                Add(LiveGraph());
                Add(Ui.Space(13f));
                Add(Status(() => MockPlayer.Playing ? "Reading the sound's output (mock)." : "Press play on this sound and it fills in."));
            }
            for (int i = 0; i < clip.effects.Count; i++) Add(RosterRow(clip.effects[i], i));
        }

        VisualElement Header() {
            var r = Ui.Row(-1f);
            r.Add(Ui.Toggle(open ? "Analyse ▾" : "Analyse ▸", "", open, v => { open = v; Tick(); }, "RichToggle", Corners.All, 90f, 30.5f));
            if (!open) { var l = Ui.Text("see what this chain is doing", "", "znd-text-default", "znd-subtle"); r.Add(l); return r; }
            r.Add(Ui.Gap(6f));
            string[] tabs = { "Combined", "Spectrum", "Over time", "Waveform" };
            var tg = new ToggleButton[4];
            for (int i = 0; i < 4; i++) {
                int idx = i;
                tg[i] = Ui.Toggle(tabs[i], "", view == i, _ => { if (view == idx) { tg[idx].value = true; return; } view = idx; Tick(); }, "RichToggle", Corners.All, 74f, 30.5f);
                r.Add(tg[i]);
            }
            r.Add(Ui.Flex());
            if (view == 0) {
                var note = Mini(Bands + " bands, 20 Hz–20 kHz", Note);
                note.style.width = 150f; note.style.height = Ui.LineH;
                note.tooltip = "The whole range of hearing in " + Bands + " bands. The bars move while the sound plays.";
                r.Add(note);
            }
            else {
                r.Add(Ui.Text("gain", "", "znd-text-default", "znd-subtle"));
                var s = new Slider(0.25f, 16f) { value = gain, showInputField = true };
                s.style.width = 90f; s.style.marginLeft = 0; s.style.marginRight = 3f;
                s.AddToClassList("znd-narrowslider");
                s.RegisterValueChangedCallback(e => gain = e.newValue);
                r.Add(s);
            }
            return r;
        }

        VisualElement Lane(string label, string key) {
            var row = Ui.Row(LaneH);
            var l = Mini(label); l.style.width = LaneLabelW;
            l.tooltip = label + " across one play (left = start, right = end). Faint level: where it was set; bright line: where it goes; the dot: now.";
            var canvas = new RectCanvas((g, fill) => {
                fill(g, new Color(0.10f, 0.10f, 0.12f));
                float set = 0.5f;
                float yOf(float p) => g.yMax - 1f - p * (g.height - 2f);
                float ya = yOf(set);
                fill(new Rect(g.x, ya, g.width, 1f), new Color(0.55f, 0.55f, 0.62f, 0.45f));
                int cols = Mathf.Max(1, (int)g.width);
                float prev = yOf(MockSignal.Wobble(key, 0f));
                for (int c = 0; c < cols; c++) {
                    float y = yOf(Mathf.Clamp01(MockSignal.Wobble(key, c / (float)cols * clip.PlaySeconds)));
                    fill(new Rect(g.x + c, Mathf.Min(y, ya), 1f, Mathf.Max(1f, Mathf.Abs(y - ya))), new Color(0.45f, 0.75f, 1f, 0.18f));
                    fill(new Rect(g.x + c, Mathf.Min(y, prev) - 0.5f, 1f, Mathf.Max(1.2f, Mathf.Abs(y - prev) + 1f)), new Color(0.55f, 0.82f, 1f, 0.95f));
                    prev = y;
                }
                float f = MockPlayer.Progress, xNow = g.x + f * g.width;
                if (MockPlayer.Playing) fill(new Rect(xNow - 0.5f, g.y, 1.5f, g.height), new Color(1f, 1f, 1f, 0.9f));
                fill(new Rect(xNow - 3f, yOf(Mathf.Clamp01(MockSignal.Wobble(key, f * clip.PlaySeconds))) - 3f, 6f, 6f), new Color(1f, 0.85f, 0.35f));
            });
            canvas.style.flexGrow = 1; canvas.style.marginTop = 1f; canvas.style.marginBottom = 1f;
            row.Add(l); row.Add(canvas);
            perFrame.Add(canvas.MarkDirtyRepaint);
            return row;
        }

        static float FreqX(float hz) => Mathf.InverseLerp(Mathf.Log(20f), Mathf.Log(20000f), Mathf.Log(hz));

        VisualElement Bars() {
            var holder = new VisualElement();
            holder.style.height = GraphH; holder.style.flexShrink = 0;
            var seenMin = new float[Bands]; var seenMax = new float[Bands];
            for (int b = 0; b < Bands; b++) { seenMin[b] = 99f; seenMax[b] = -99f; }
            const float range = 18f;
            var canvas = new RectCanvas((area, fill) => {
                fill(area, new Color(0.12f, 0.12f, 0.14f));
                float mid = area.y + area.height * 0.5f;
                foreach (var hz in FreqTicks) { float x = area.x + FreqX(hz) * area.width; if (x > area.x + 1f && x < area.xMax - 1f) fill(new Rect(x, area.y, 1f, area.height), new Color(1f, 1f, 1f, 0.05f)); }
                fill(new Rect(area.x, mid - 1f, area.width, 2f), new Color(0.45f, 0.45f, 0.5f));
                float slot = area.width / Bands, t = (float)EditorApplication.timeSinceStartup;
                for (int b = 0; b < Bands; b++) {
                    float x = area.x + b * slot, bw = Mathf.Max(1f, slot - 1f);
                    float db = MockSignal.BandDb(b, Bands, t, MockPlayer.Playing);
                    seenMin[b] = Mathf.Min(seenMin[b], db); seenMax[b] = Mathf.Max(seenMax[b], db);
                    if (b == hovered) fill(new Rect(x, area.y, bw, area.height), new Color(1f, 1f, 1f, 0.06f));
                    float top = mid - Mathf.Clamp(seenMax[b] / range, -1f, 1f) * area.height * 0.5f, bot = mid - Mathf.Clamp(seenMin[b] / range, -1f, 1f) * area.height * 0.5f;
                    fill(new Rect(x, top, bw, Mathf.Max(1f, bot - top)), new Color(0.3f, 0.45f, 0.6f, 0.35f));
                    float h = Mathf.Abs(Mathf.Clamp(db / range, -1f, 1f)) * area.height * 0.5f;
                    fill(db >= 0f ? new Rect(x, mid - h, bw, h) : new Rect(x, mid, bw, h), db >= 0f ? new Color(0.45f, 0.8f, 0.5f) : new Color(0.85f, 0.5f, 0.4f));
                }
            });
            canvas.style.position = Position.Absolute; canvas.style.left = 0; canvas.style.right = 0; canvas.style.top = 0; canvas.style.bottom = 0;
            holder.Add(canvas);
            Label Scale(string s, float top, bool fromBottom) {
                var l = Mini(s, new Color(0.7f, 0.7f, 0.76f), TextAnchor.UpperLeft);
                l.style.position = Position.Absolute; l.style.left = 3f; l.style.width = 70f; l.style.height = 14f;
                if (fromBottom) l.style.bottom = 1f; else l.style.top = top;
                holder.Add(l); return l;
            }
            Scale("+18 dB", 1f, false); Scale("0 dB", GraphH * 0.5f - 15f, false); Scale("−18 dB", 0f, true);
            var hover = Mini("", new Color(0.95f, 0.95f, 1f), TextAnchor.UpperRight);
            hover.style.position = Position.Absolute; hover.style.right = 4f; hover.style.top = 1f; hover.style.width = 356f; hover.style.height = 14f;
            holder.Add(hover);
            holder.tooltip = "What the chain does to each part of the frequency range right now, bass on the left, treble on the right. Middle line = unchanged; green above = louder, red below = quieter. The faint block behind a bar is every level it has reached. Point at a bar to read it.";
            holder.RegisterCallback<PointerMoveEvent>(e => hovered = Mathf.Clamp((int)(e.localPosition.x / Mathf.Max(1f, holder.layout.width) * Bands), 0, Bands - 1));
            holder.RegisterCallback<PointerLeaveEvent>(_ => hovered = -1);
            perFrame.Add(() => {
                if (hovered >= 0) {
                    float lo = 20f * Mathf.Pow(1000f, hovered / (float)Bands), hi = 20f * Mathf.Pow(1000f, (hovered + 1) / (float)Bands);
                    float db = MockSignal.BandDb(hovered, Bands, (float)EditorApplication.timeSinceStartup, MockPlayer.Playing);
                    hover.text = Hz(lo) + "–" + Hz(hi) + ":  " + (db >= 0f ? "+" : "") + db.ToString("0.0") + " dB now";
                }
                else hover.text = "";
                canvas.MarkDirtyRepaint();
            });
            return holder;
        }

        static string Hz(float f) => f >= 1000f ? (f / 1000f).ToString(f >= 10000f ? "0" : "0.#") + " kHz" : f.ToString("0") + " Hz";

        VisualElement FreqLabels() {
            var row = new VisualElement();
            row.style.height = 13f; row.style.flexShrink = 0;
            var labels = FreqTicks.Select(hz => { var l = Mini(Hz(hz).Replace(" Hz", "").Replace(" kHz", "k"), new Color(0.6f, 0.6f, 0.65f), TextAnchor.UpperCenter); l.style.position = Position.Absolute; l.style.top = 0; l.style.width = 40f; l.style.height = 13f; row.Add(l); return (l, FreqX(hz)); }).ToList();
            row.RegisterCallback<GeometryChangedEvent>(_ => {
                float w = row.layout.width;
                foreach (var (l, x01) in labels) l.style.left = Mathf.Clamp(x01 * w - 20f, 0f, Mathf.Max(0f, w - 40f));
            });
            return row;
        }

        VisualElement LiveGraph() {
            var holder = new VisualElement();
            holder.style.height = GraphH; holder.style.flexShrink = 0;
            var canvas = new RectCanvas((area, fill) => {
                fill(area, new Color(0.10f, 0.10f, 0.12f));
                float t = (float)EditorApplication.timeSinceStartup, mid = area.y + area.height * 0.5f;
                bool on = MockPlayer.Playing;
                if (view == 1) {
                    int n = 96; float slot = area.width / n;
                    for (int b = 0; b < n; b++) {
                        float lvl = on ? Mathf.Clamp01((MockSignal.BandDb(b * Bands / n, Bands, t, true) + 18f) / 36f * Mathf.Min(1f, gain * 0.6f)) : 0f;
                        fill(new Rect(area.x + b * slot, area.yMax - lvl * area.height, Mathf.Max(1f, slot - 1f), lvl * area.height), new Color(0.45f, 0.75f, 1f, 0.85f));
                    }
                }
                else if (view == 2) {
                    var cols = history.ToArray();
                    float cw = 3f;
                    for (int c = 0; c < cols.Length; c++) {
                        float x = area.xMax - (cols.Length - c) * cw;
                        if (x < area.x) continue;
                        for (int b = 0; b < cols[c].Length; b++) {
                            float hgt = area.height / cols[c].Length, v = cols[c][b];
                            fill(new Rect(x, area.yMax - (b + 1) * hgt, cw, hgt + 0.5f), new Color(v * 0.9f, v * 0.55f + 0.05f, 0.35f + v * 0.5f, 1f));
                        }
                    }
                }
                else {
                    fill(new Rect(area.x, mid, area.width, 1f), new Color(0.45f, 0.75f, 1f, 0.9f));
                    if (on) {
                        int n = (int)area.width; float prev = mid;
                        for (int c = 0; c < n; c++) {
                            float y = mid - (Mathf.Sin(c * 0.21f + t * 30f) * 0.5f + Mathf.Sin(c * 0.057f + t * 7f) * 0.3f) * area.height * 0.4f * Mathf.Min(1f, gain * 0.5f);
                            fill(new Rect(area.x + c, Mathf.Min(prev, y), 1f, Mathf.Max(1f, Mathf.Abs(y - prev))), new Color(0.45f, 0.75f, 1f, 0.9f));
                            prev = y;
                        }
                    }
                }
            });
            canvas.style.position = Position.Absolute; canvas.style.left = 0; canvas.style.right = 0; canvas.style.top = 0; canvas.style.bottom = 0;
            holder.Add(canvas);
            var state = Mini("", null, TextAnchor.UpperLeft); state.style.position = Position.Absolute; state.style.left = 6f; state.style.top = 4f; state.style.height = 16f;
            var peak = Mini("", null, TextAnchor.UpperRight); peak.style.position = Position.Absolute; peak.style.right = 6f; peak.style.top = 4f; peak.style.height = 16f;
            holder.Add(state); holder.Add(peak);
            perFrame.Add(() => {
                bool on = MockPlayer.Playing;
                if (view == 2) {
                    var col = new float[40];
                    for (int b = 0; b < 40; b++) col[b] = on ? Mathf.Clamp01((MockSignal.BandDb(b * Bands / 40, Bands, (float)EditorApplication.timeSinceStartup, true) + 18f) / 36f) : 0f;
                    history.Enqueue(col);
                    while (history.Count > 400) history.Dequeue();
                }
                state.text = on ? "playing" : "nothing playing";
                state.style.color = on ? new Color(0.55f, 0.85f, 0.6f) : Note;
                peak.style.display = view == 3 ? DisplayStyle.Flex : DisplayStyle.None;
                peak.text = "peak " + (on ? (0.6f + 0.3f * MockSignal.Wobble("peak", (float)EditorApplication.timeSinceStartup)).ToString("0.00") : "0.00");
                canvas.MarkDirtyRepaint();
            });
            return holder;
        }

        VisualElement RosterRow(Effect e, int i) {
            var row = new VisualElement(); row.style.height = Ui.LineH; row.style.flexShrink = 0;
            var kinds = new[] { ("exact", new Color(0.55f, 0.85f, 0.6f)), ("moves with its modifiers", new Color(0.6f, 0.75f, 0.95f)), ("depends on level", new Color(0.9f, 0.85f, 0.5f)), ("smeared in time", new Color(0.7f, 0.6f, 0.9f)) };
            var (word, col) = kinds[i % kinds.Length];
            var dot = new VisualElement(); dot.style.position = Position.Absolute; dot.style.left = 0; dot.style.width = 9f; dot.style.height = 9f; dot.style.top = (Ui.LineH - 9f) * 0.5f;
            dot.style.backgroundColor = e.enabled ? col : col * 0.45f;
            var text = Mini(e.name + (e.enabled ? "" : " (off)") + (view == 0 ? " — " + word : ""));
            text.style.position = Position.Absolute; text.style.left = 13f; text.style.right = 0; text.style.top = 0; text.style.bottom = 0;
            if (!e.enabled) text.style.color = new Color(0.5f, 0.5f, 0.5f);
            text.tooltip = "How closely this picture shows " + e.name + " (mock).";
            row.Add(dot); row.Add(text);
            return row;
        }
    }
}
