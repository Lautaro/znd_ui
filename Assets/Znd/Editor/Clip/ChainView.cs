using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Znd.Mock {

    /// <summary>
    /// The effect chain and its modifiers, laid out wide rather than tall: every setting is a self-labelled control of a
    /// known width, and settings flow left to right, starting a new row only when the next one does not fit. An effect
    /// whose settings all fit shows them on its own row; otherwise it shows a summary that expands into wrapped rows.
    ///
    /// Rows are rebuilt only when the structure changes (effects, modifiers, bindings, what is folded or expanded, where
    /// rows wrap); a value change updates controls in place so a drag never loses its slider. All data is mock.
    /// </summary>
    public class ChainView : VisualElement {

        // Row geometry (px)
        const float RowH = 20f, GripW = 14f, OnW = 30f, RemoveW = 20f, LabelW = 88f, NameW = 84f, Gap = 6f;
        const float SliderW = 158f, TagW = 14f, WarnW = 16f, IconCell = 30f, ChipW = 150f, EyeW = 22f, OwnW = 150f;
        const float StepBarMaxW = 44f, StepBandH = 64f, CurveH = 56f;
        static float HeaderLead => GripW + 2f + OnW + 6f + NameW + Gap;
        static readonly string[] CombineLabels = { "Shift", "Set", "Scale", "Ratio" };
        static readonly string[] CombineTips = {
            "Shift: adds the modifier's movement to the set value.",
            "Set: the modifier's value replaces the set value.",
            "Scale: multiplies the set value.",
            "Ratio: moves the value by ratios (for pitch, speed, frequency)."
        };

        readonly ClipData clip;
        readonly SnapshotsRow snapshots;
        readonly CodeTestPanel codeTest;
        readonly AnalyserView analyser;
        readonly List<Action> perTick = new List<Action>();
        int expanded = -1;
        string builtSig;

        // drag-reorder of effects
        readonly List<VisualElement> effectRows = new List<VisualElement>();
        VisualElement effectsBox, dropLine;
        int dragFrom = -1, dragTo = -1;

        public ChainView(ClipData clip) {
            this.clip = clip;
            AddToClassList("znd-chain");
            style.flexShrink = 0;
            snapshots = new SnapshotsRow(clip);
            codeTest = new CodeTestPanel(clip);
            analyser = new AnalyserView(clip);
            RegisterCallback<GeometryChangedEvent>(_ => Tick());
            schedule.Execute(Tick).Every(33);
        }

        float Width => float.IsNaN(resolvedStyle.width) ? 0f : resolvedStyle.width;
        static float Avail(float w) => Mathf.Max(200f, w);

        public void Rebuild() { builtSig = null; Tick(); }

        void Structural() { Rebuild(); MockPlayer.Edited(clip); }

        void Tick() {
            if (panel == null) return;
            float w = Width;
            if (w <= 0f) return;
            string sig = Signature(w);
            if (sig != builtSig) { builtSig = sig; Build(w); }
            foreach (var a in perTick) a();
        }

        // ─────────── sizes ───────────

        static float TextW(string s) => (s?.Length ?? 0) * 6.8f;

        float UnitWidth(Param p, bool bound) {
            float w;
            switch (p.kind) {
                case ParamKind.Icons: w = p.icons.Length * IconCell; break;
                case ParamKind.Range: w = SliderW + 60f; break;
                case ParamKind.Choice: w = p.options.Sum(o => Mathf.Max(38f, TextW(o) + 14f)); break;
                case ParamKind.Toggle: w = Mathf.Max(64f, TextW(p.name) + 20f); break;
                default: w = SliderW; break;
            }
            if (bound) w += TagW;
            if (p.warning != null) w += WarnW;
            return w;
        }

        List<float> EffectWidths(Effect e) => e.ps.Select((p, k) => UnitWidth(p, clip.IsBound(e, k))).ToList();
        List<float> ModifierWidths(Modifier m) => m.ps.Select(p => UnitWidth(p, false)).ToList();
        static float OneRow(List<float> ws) => ws.Sum() + Gap * Mathf.Max(0, ws.Count - 1);
        bool Inline(Effect e, float w) => OneRow(EffectWidths(e)) <= Avail(w) - HeaderLead - RemoveW - Gap;

        static int CountRows(float avail, List<float> ws) {
            int rows = 0; float used = 0f;
            foreach (var u in ws) {
                float need = rows > 0 && used > 0f ? Gap + u : u;
                if (rows == 0 || used + need > avail + 0.01f) { rows++; used = u; } else used += need;
            }
            return rows;
        }

        string Signature(float w) {
            var sb = new StringBuilder();
            sb.Append(clip.presetName).Append('|').Append(expanded).Append('|').Append(clip.snapshots.Count).Append('|');
            for (int i = 0; i < clip.effects.Count; i++) {
                var e = clip.effects[i];
                sb.Append(e.name).Append(e.enabled ? '+' : '-');
                for (int k = 0; k < e.ps.Count; k++) sb.Append(clip.IsBound(e, k) ? 'b' : '.');
                bool inl = Inline(e, w);
                sb.Append(inl ? 'I' : 'E');
                if (!inl && expanded == i) sb.Append(CountRows(Avail(w) - (GripW + 6f), EffectWidths(e)));
                sb.Append(';');
            }
            foreach (var m in clip.modifiers) {
                sb.Append((int)m.type).Append(m.enabled ? '+' : '-').Append(m.folded ? 'F' : 'U').Append(m.HasCode ? 'Z' : 'z').Append(m.bindings.Count);
                foreach (var b in m.bindings) sb.Append(clip.effects.IndexOf(b.effect)).Append(b.param).Append(b.ownValue).Append(b.combine);
                if (!m.folded) sb.Append('r').Append(CountRows(Avail(w) - (GripW + 6f), ModifierWidths(m)));
                if (m.steps != null) sb.Append('s').Append(m.steps.Length);
                sb.Append(';');
            }
            return sb.ToString();
        }

        // ─────────── build ───────────

        void Build(float w) {
            Clear();
            perTick.Clear(); effectRows.Clear();
            Add(LibraryBar());
            Add(snapshots);
            Add(ErrorRow());
            Add(Effects(w));
            Add(AddEffectRow());
            Add(Ui.Space(5f));
            Add(ModifiersHeader());
            Add(OwnValuesRow());
            foreach (var m in clip.modifiers) {
                Add(ModifierRow(m));
                if (!m.folded) { ModifierBody(m, w); Bindings(m); }
            }
            Add(Ui.Space(5f));
            Add(codeTest);
            Add(analyser);
        }

        // ─────────── library bar / error row ───────────

        VisualElement LibraryBar() {
            var r = Ui.Row();
            var title = Ui.Text(clip.presetName != null ? clip.presetName + "  (3 users)" : "Local chain",
                clip.presetName != null ? "This sound follows a library preset: editing it changes every sound using it." : "This sound's own chain.",
                "znd-text-subheader", "znd-subheader");
            title.style.width = 220f;
            r.Add(title);
            r.Add(Ui.Flex());
            Button lib = null, save = null;
            lib = Ui.Button("Library…", "Browse the chain presets.", "RichButton", () => LibraryPopup.Show(lib.worldBound, clip, Structural), Corners.Left, 70f, RowH);
            save = Ui.Button("Save as…", "Save this chain as a new preset and link this sound to it.", "RichButton",
                () => NamePopup.Show(save.worldBound, "Name", clip.name + " chain", n => { clip.presetName = n; Structural(); }), Corners.None, 70f, RowH);
            r.Add(lib); r.Add(save);
            if (clip.presetName != null)
                r.Add(Ui.Button("Detach", "Keep a private copy and stop following the preset.", "RichButton", () => { clip.presetName = null; clip.canReconnect = true; Structural(); }, Corners.Right, 62f, RowH));
            else {
                var b = Ui.Button("Reconnect", "Follow the preset this chain was detached from again.", "RichButton", () => { clip.presetName = MockLibrary.Presets[0]; Structural(); }, Corners.Right, 78f, RowH);
                b.SetEnabled(clip.canReconnect);
                r.Add(b);
            }
            return r;
        }

        VisualElement ErrorRow() {
            var r = Ui.PlacedRow(Ui.LineH);
            var bar = Ui.Place(new VisualElement(), 0f, 0f, 3f, -1f);
            bar.AddToClassList("znd-errorbar"); bar.style.bottom = 0;
            var msg = Ui.Place(Ui.Text("", "", "znd-mini", "znd-warntext"), 7f, 0f, -1f, -1f);
            msg.style.right = 0; msg.style.bottom = 0;
            r.Add(bar); r.Add(msg);
            perTick.Add(() => {
                var d = clip.effects.FirstOrDefault(e => e.name == "Delay" && e.enabled);
                string err = d != null && d.ps[0].value > d.ps[3].value ? "Delay: Time is longer than Max time, so it is held at " + d.ps[3].Format(d.ps[3].value) + " (mock warning)." : null;
                bar.style.display = msg.style.display = err != null ? DisplayStyle.Flex : DisplayStyle.None;
                msg.text = err != null ? "⚠ " + err : "";
            });
            return r;
        }

        // ─────────── effects ───────────

        VisualElement Effects(float w) {
            effectsBox = new VisualElement();
            effectsBox.style.flexShrink = 0;
            if (clip.effects.Count == 0) { var none = Ui.Text("No effects.", "", "znd-subtle"); none.style.height = Ui.LineH; effectsBox.Add(none); }
            for (int i = 0; i < clip.effects.Count; i++) {
                int ei = i;
                var e = clip.effects[i];
                bool inline = Inline(e, w), open = expanded == i;
                var row = Ui.PlacedRow();
                effectRows.Add(row);
                effectsBox.Add(row);
                if (open && !inline) row.Add(Ui.Fill(new Color(1f, 1f, 1f, 0.04f)));

                var grip = Ui.Place(Ui.Text("≡", "Drag to reorder. Signal flows top to bottom.", "znd-greymini"), 0f, 0f, GripW, RowH);
                grip.AddToClassList("znd-grip");
                grip.RegisterCallback<PointerDownEvent>(ev => { if (ev.button != 0) return; dragFrom = dragTo = ei; grip.CapturePointer(ev.pointerId); ShowDrop(); ev.StopPropagation(); });
                grip.RegisterCallback<PointerMoveEvent>(ev => { if (dragFrom < 0 || !grip.HasPointerCapture(ev.pointerId)) return; dragTo = DropAt(effectsBox.WorldToLocal(ev.position).y); ShowDrop(); });
                grip.RegisterCallback<PointerUpEvent>(ev => { if (dragFrom < 0) return; if (grip.HasPointerCapture(ev.pointerId)) grip.ReleasePointer(ev.pointerId); FinishDrag(); });
                row.Add(grip);

                row.Add(Ui.Place(Ui.Toggle("On", e.enabled ? "Bypass this effect." : "Enable this effect.", e.enabled, v => { e.enabled = v; Structural(); }, "RichToggle", Corners.None, OnW, RowH - 2f),
                                 GripW + 2f, 1f, OnW, RowH - 2f));
                float nameX = GripW + 2f + OnW + 6f;
                var name = Ui.Place(Ui.Text(e.name, e.summary, "znd-bold"), nameX, 0f, NameW, RowH);
                row.Add(name);
                if (inline) {
                    float x = nameX + NameW + Gap;
                    for (int k = 0; k < e.ps.Count; k++) {
                        float uw = UnitWidth(e.ps[k], clip.IsBound(e, k));
                        row.Add(EffectUnit(e, k, x, uw));
                        x += uw + Gap;
                    }
                }
                else {
                    var summary = Ui.Place(Ui.Text("", open ? "Click to fold the settings away." : "Click to show all " + e.ps.Count + " settings. They do not fit on this row at this width.", "znd-mini", "znd-summary"),
                                           nameX + NameW, 0f, -1f, RowH);
                    summary.style.right = RemoveW + 4f;
                    perTick.Add(() => summary.text = (open ? "▾ " : "▸ ") + Summary(e));
                    EventCallback<PointerDownEvent> toggle = ev => { if (ev.button != 0) return; expanded = open ? -1 : ei; ev.StopPropagation(); Rebuild(); };
                    summary.RegisterCallback(toggle); name.RegisterCallback(toggle);
                    row.Add(summary);
                }
                row.Add(Ui.PlaceRight(Ui.Button("×", "Remove this effect.", "RichButton", () => {
                    foreach (var m in clip.modifiers) m.bindings.RemoveAll(b => b.effect == e);
                    clip.effects.Remove(e); if (expanded >= clip.effects.Count) expanded = -1; Structural();
                }, Corners.All, RemoveW, RowH - 2f), 0f, 1f, RemoveW, RowH - 2f));
                if (!inline && open) Wrap(e.ps.Select((p, k) => (UnitWidth(p, clip.IsBound(e, k)), (Func<float, float, float, VisualElement>)((x, y, uw) => EffectUnit(e, k, x, uw)))).ToList(), w, effectsBox);
            }
            dropLine = new VisualElement { pickingMode = PickingMode.Ignore };
            dropLine.AddToClassList("znd-dropline");
            dropLine.style.position = Position.Absolute; dropLine.style.left = 0; dropLine.style.right = 0; dropLine.style.height = 2f;
            dropLine.style.display = DisplayStyle.None;
            effectsBox.Add(dropLine);
            return effectsBox;
        }

        string Summary(Effect e) {
            var parts = new List<string>();
            for (int k = 0; k < e.ps.Count && parts.Count < 3; k++) {
                var p = e.ps[k];
                if (p.kind == ParamKind.Toggle && p.value < 0.5f) continue;
                parts.Add(p.name + " " + p.Format(p.value) + (clip.IsBound(e, k) ? "~" : ""));
            }
            return string.Join("  ", parts);
        }

        /// <summary>Settings flow left to right from the indent; a new row when the next does not fit.</summary>
        void Wrap(List<(float w, Func<float, float, float, VisualElement> make)> units, float width, VisualElement into) {
            float indent = GripW + 6f, avail = Mathf.Max(1f, Avail(width) - indent);
            VisualElement row = null; float used = 0f;
            foreach (var (uw, make) in units) {
                float need = row != null && used > 0f ? Gap + uw : uw;
                if (row == null || used + need > avail + 0.01f) { row = Ui.PlacedRow(); into.Add(row); used = 0f; need = uw; }
                row.Add(make(indent + used + (need - uw), 1f, uw));
                used += need;
            }
        }

        int DropAt(float y) {
            for (int i = 0; i < effectRows.Count; i++) { var l = effectRows[i].layout; if (y < l.y + l.height * 0.5f) return i; }
            return effectRows.Count;
        }

        void ShowDrop() {
            if (dropLine == null || effectRows.Count == 0 || dragTo < 0) return;
            float y = dragTo < effectRows.Count ? effectRows[dragTo].layout.y : effectRows[effectRows.Count - 1].layout.yMax;
            dropLine.style.top = y - 1f; dropLine.style.display = DisplayStyle.Flex; dropLine.BringToFront();
            for (int i = 0; i < effectRows.Count; i++) effectRows[i].EnableInClassList("znd-dragging", i == dragFrom);
        }

        void FinishDrag() {
            int from = dragFrom, to = dragTo;
            dragFrom = dragTo = -1;
            if (dropLine != null) dropLine.style.display = DisplayStyle.None;
            foreach (var r in effectRows) r.RemoveFromClassList("znd-dragging");
            if (from < 0 || to < 0) return;
            if (to > from) to--;
            if (to == from || to >= clip.effects.Count) return;
            var e = clip.effects[from];
            clip.effects.RemoveAt(from); clip.effects.Insert(to, e);
            expanded = -1;
            Structural();
        }

        VisualElement AddEffectRow() {
            var r = Ui.Row();
            r.Add(Ui.Button("Add effect…", "Append an effect to the end of the chain.", "RichButton", () =>
                Ui.Menu(MockLibrary.EffectTypes.Select(t => (t, (Action)(() => { clip.effects.Add(MockLibrary.MakeEffect(t)); expanded = clip.effects.Count - 1; Structural(); }), false, true)).ToArray()),
                Corners.All, 90f, RowH));
            r.Add(Ui.Flex());
            var tail = Ui.Text("", "How long the chain keeps ringing after the source stops.", "znd-mini");
            tail.style.width = 110f; tail.style.height = RowH;
            perTick.Add(() => {
                bool rings = clip.effects.Any(e => e.enabled && (e.name == "Delay" || e.name == "Reverb"));
                tail.text = rings ? "tail 0.50 s" : "";
            });
            r.Add(tail);
            return r;
        }

        // ─────────── one setting ───────────

        VisualElement EffectUnit(Effect e, int k, float x, float w) {
            var p = e.ps[k];
            var mods = clip.ModifiersOn(e, k).ToList();
            return Unit(p, x, 1f, w, mods, () => Rebuild(), ev => ParamMenu(e, k, null));
        }

        /// <summary>One setting as its own labelled control at (x, y): a slider with name and value in its track, a toggle whose
        /// face is its name, a choice strip, an icon strip or a range; then "~" (or the amber bolt) when something drives it,
        /// and ⚠ for a warning. Right-click opens its modulation menu.</summary>
        VisualElement Unit(Param p, float x, float y, float w, List<Modifier> mods, Action structural, Action<PointerDownEvent> menu) {
            float h = RowH - 2f;
            bool bound = mods != null && mods.Count > 0;
            bool code = bound && mods.Any(m => m.HasCode);
            var box = Ui.Place(new VisualElement(), x, y, w, h);
            box.AddToClassList("znd-unit");
            float cw = w - (bound ? TagW : 0f) - (p.warning != null ? WarnW : 0f);
            string tip = string.IsNullOrEmpty(p.tip) ? p.name + " (mock setting). Right-click to modulate it." : p.tip;

            switch (p.kind) {
                case ParamKind.Range:
                    box.Add(Ui.Place(Ui.Range("Min–Max", p.value, p.value2, p.min, p.max, "The range a value is drawn from, once per play.",
                        (a, b) => { p.value = a; p.value2 = b; }, "default", RangeSlider.LabelMode.LabelAndValues, false, cw, h), 0f, 0f, cw, h));
                    break;
                case ParamKind.Choice:
                case ParamKind.Icons: {
                    int n = p.kind == ParamKind.Icons ? p.icons.Length : p.options.Length;
                    var ow = new float[n]; float total = 0f;
                    for (int o = 0; o < n; o++) { ow[o] = p.kind == ParamKind.Icons ? IconCell : Mathf.Max(38f, TextW(p.options[o]) + 14f); total += ow[o]; }
                    float scale = cw / Mathf.Max(1f, total), ox = 0f;
                    var ts = new ToggleButton[n];
                    for (int o = 0; o < n; o++) {
                        int oi = o;
                        var corner = n == 1 ? Corners.All : o == 0 ? Corners.Left : o == n - 1 ? Corners.Right : Corners.None;
                        ts[o] = Ui.Toggle(p.kind == ParamKind.Icons ? "" : p.options[o], p.name + ": " + (p.kind == ParamKind.Icons ? p.icons[o].ToString() : p.options[o]),
                            Mathf.RoundToInt(p.value) == o, _ => { p.value = oi; for (int q = 0; q < n; q++) ts[q].value = q == oi; MockPlayer.Edited(clip); }, "RichToggle", corner, ow[o] * scale, h);
                        if (p.kind == ParamKind.Icons) { ts[o].markWhenOn = false; ts[o].Add(new WaveIcon(p.icons[o])); }
                        box.Add(Ui.Place(ts[o], ox, 0f, ow[o] * scale, h));
                        ox += ow[o] * scale;
                    }
                    break;
                }
                case ParamKind.Toggle:
                    box.Add(Ui.Place(Ui.Toggle(p.name, tip, p.value >= 0.5f, v => { p.value = v ? 1f : 0f; MockPlayer.Edited(clip); }, "RichToggle", Corners.None, cw, h), 0f, 0f, cw, h));
                    break;
                default: {
                    string Label(float v) => p.name + "  " + p.Format(v);
                    TrackSlider s = null;
                    bool log = p.kind == ParamKind.LogSlider;
                    s = Ui.Slider(Label(p.value), log ? p.Normalized(p.value) : p.value, log ? 0f : p.min, log ? 1f : p.max, tip, v => {
                        p.value = log ? p.FromNormalized(v) : p.kind == ParamKind.Integer ? Mathf.Round(v) : v;
                        s.text = Label(p.value);
                    }, TrackSlider.LabelMode.LabelOnly, log ? p.Normalized(p.def) : p.def, "default", cw, h);
                    box.Add(Ui.Place(s, 0f, 0f, cw, h));
                    if (bound) Live(s, p, mods, code, () => s.text = Label(p.value));
                    break;
                }
            }
            float right = cw;
            if (bound) {
                string by = string.Join(", ", mods.Select(m => m.name));
                VisualElement tag = code
                    ? new Bolt { tooltip = "Game code can move this, through '" + mods.First(m => m.HasCode).codeId + "' (modulated by " + by + "). While playing, amber shows where code has it." }
                    : Ui.Text("~", "Modulated by " + by + ". The thin line marks the set value; while playing, the band shows where it is.", "znd-greymini");
                box.Add(Ui.Place(tag, right, -y, TagW, RowH));
                right += TagW;
            }
            if (p.warning != null) box.Add(Ui.Place(Ui.Text("⚠", p.warning, "znd-warnmark"), right, -y, WarnW, RowH));
            box.RegisterCallback<PointerDownEvent>(ev => { if (ev.button != 1) return; menu(ev); ev.StopPropagation(); }, TrickleDown.TrickleDown);
            return box;
        }

        /// <summary>A modulated slider's live band: where the value is while "playing" (canned wobble), amber when code drives it.</summary>
        void Live(TrackSlider s, Param p, List<Modifier> mods, bool code, Action resetText) {
            var overlay = new LiveOverlay();
            s.Add(overlay);
            float lastJitter = -1f;
            string key = p.name + mods[0].name;
            perTick.Add(() => {
                float set = s.Normalized;
                overlay.SetSet(set);
                if (!MockPlayer.Playing) { overlay.ClearLive(); resetText(); return; }
                float t = (float)EditorApplication.timeSinceStartup;
                bool driven = code && CodeTest.Drive;
                float live = driven ? CodeTest.ValueFor(mods.First(m => m.HasCode).codeId, t, 0)
                                    : Mathf.Clamp01(set + (MockSignal.Wobble(key, t) - 0.5f) * 0.8f * mods[0].bindings.Max(b => b.depth));
                overlay.SetLive(live, driven);
                if (driven) {
                    overlay.SetTarget(Mathf.Clamp01(live + 0.05f));
                    float j = MockSignal.Jitter(key, t, 0.3f);
                    if (j != lastJitter) { if (lastJitter >= 0f) overlay.Pulse(); lastJitter = j; }
                    s.text = p.name + "  " + p.Format(p.value) + "  ⚡" + p.Format(p.FromNormalized(live));
                }
                else resetText();
            });
        }

        void ParamMenu(Effect e, int k, string own) {
            var items = new List<(string, Action, bool, bool)>();
            foreach (var m in clip.modifiers) {
                var mm = m;
                bool already = own != null ? m.bindings.Any(b => b.TargetsOwn(own)) : m.bindings.Any(b => b.Targets(e, k));
                items.Add(("Modulate with/" + m.name + " (" + m.TypeName + ")", () => { mm.bindings.Add(NewBinding(e, k, own)); Structural(); }, already, !already));
            }
            foreach (ModifierType t in Enum.GetValues(typeof(ModifierType))) {
                var tt = t;
                items.Add(("Modulate with/New " + (t == ModifierType.LFO ? "LFO" : t.ToString().ToLower()), () => {
                    var m = MockLibrary.MakeModifier(tt); m.bindings.Add(NewBinding(e, k, own)); clip.modifiers.Add(m); Structural();
                }, false, true));
            }
            foreach (var m in own != null ? clip.ModifiersOnOwn(own) : clip.ModifiersOn(e, k)) {
                var mm = m;
                items.Add(("Remove modulation: " + m.name, () => { mm.bindings.RemoveAll(b => own != null ? b.TargetsOwn(own) : b.Targets(e, k)); Structural(); }, false, true));
            }
            Ui.Menu(items.ToArray());
        }

        static Binding NewBinding(Effect e, int k, string own) => own != null ? new Binding { ownValue = own, combine = 0, depth = 0.5f } : new Binding { effect = e, param = k, combine = 0, depth = 0.5f };

        // ─────────── modifiers ───────────

        VisualElement ModifiersHeader() {
            var r = Ui.Row();
            var l = Ui.Text("Modifiers", "Things that move settings over time: envelopes, LFOs, a random value per play, or a step list. Bind one from a setting's right-click menu.",
                            "znd-text-subheader", "znd-subheader");
            l.style.width = 80f;
            r.Add(l);
            r.Add(Ui.Flex());
            r.Add(Ui.Button("Add modifier…", "Add a modifier; bind it from a setting's right-click menu.", "RichButton", () =>
                Ui.Menu(Enum.GetValues(typeof(ModifierType)).Cast<ModifierType>().Select(t => (t == ModifierType.LFO ? "LFO" : t.ToString(),
                    (Action)(() => { clip.modifiers.Add(MockLibrary.MakeModifier(t)); Structural(); }), false, true)).ToArray()),
                Corners.All, 100f, RowH));
            return r;
        }

        VisualElement OwnValuesRow() {
            var r = Ui.Row();
            r.style.marginBottom = 2f;
            var title = Ui.Text("Sound", "The sound's own values (not its effects). Right-click one to make a modifier move it.", "znd-guilabel");
            title.style.width = GripW + 6f + 44f; title.style.paddingLeft = GripW + 6f;
            r.Add(title);
            foreach (var v in new[] { "Volume", "Pitch", "Speed", "Drive" }) { r.Add(OwnValue(v)); r.Add(Ui.Gap(6f)); }
            return r;
        }

        VisualElement OwnValue(string name) {
            var mods = clip.ModifiersOnOwn(name).ToList();
            bool bound = mods.Count > 0, code = mods.Any(m => m.HasCode);
            var box = new VisualElement();
            box.AddToClassList("znd-ownvalue");
            box.EnableInClassList("znd-ownvalue--bound", bound);
            box.EnableInClassList("znd-ownvalue--code", code);
            box.style.width = OwnW; box.style.height = RowH - 2f; box.style.marginTop = 1f; box.style.flexShrink = 0;
            var overlay = new LiveOverlay();
            box.Add(overlay);
            var text = new Label { pickingMode = PickingMode.Ignore };
            text.AddToClassList("znd-ownvalue__text");
            text.style.position = Position.Absolute; text.style.left = 0; text.style.right = 0; text.style.top = 0; text.style.bottom = 0;
            box.Add(text);
            string mark = !bound ? "" : (code ? "  ⚡ " : "  ~ ") + string.Join(", ", mods.Select(m => m.name));
            text.text = name + mark;
            box.tooltip = name + (bound ? ": moved by " + string.Join(", ", mods.Select(m => m.name)) + "." : ": nothing moves it.") + " Right-click to change what moves it.";
            box.RegisterCallback<PointerDownEvent>(e => { if (e.button != 1) return; ParamMenu(null, -1, name); e.StopPropagation(); });
            perTick.Add(() => {
                overlay.SetSet(0.5f);
                if (!bound || !MockPlayer.Playing) { overlay.ClearLive(); text.text = name + mark; return; }
                float live = MockSignal.Wobble("own" + name, (float)EditorApplication.timeSinceStartup);
                overlay.SetLive(live, code && CodeTest.Drive);
                text.text = name + " " + (name == "Pitch" ? ((live - 0.5f) * 24f).ToString("+0.0;-0.0") + " st" : "×" + (live * 2f).ToString("0.00")) + (code ? "  ⚡" : "  ~");
            });
            return box;
        }

        VisualElement ModifierRow(Modifier m) {
            var r = Ui.PlacedRow();
            r.AddToClassList("znd-modrow");
            r.Add(Ui.Fill(new Color(1f, 1f, 1f, 0.04f)));
            var fold = Ui.Place(Ui.Text(m.folded ? "▸" : "▾", m.folded ? "Expand" : "Collapse", "znd-greymini"), 0f, 0f, GripW, RowH);
            fold.RegisterCallback<PointerDownEvent>(e => { if (e.button != 0) return; m.folded = !m.folded; e.StopPropagation(); Rebuild(); });
            r.Add(fold);
            r.Add(Ui.Place(Ui.Toggle("On", m.enabled ? "Disable: its bindings stop applying." : "Enable this modifier.", m.enabled, v => { m.enabled = v; MockPlayer.Edited(clip); }, "RichToggle", Corners.None, OnW, RowH - 2f),
                           GripW + 2f, 1f, OnW, RowH - 2f));
            float typeX = GripW + 2f + OnW + 6f;
            r.Add(Ui.Place(Ui.Text(m.TypeName, "A " + m.TypeName + " modifier (mock).", "znd-bold"), typeX, 0f, 62f, RowH));
            var nameField = new TextField { value = m.name, tooltip = "The name shown on the settings this modifier drives." };
            nameField.AddToClassList("znd-namefield");
            nameField.RegisterValueChangedCallback(e => { m.name = e.newValue; });
            nameField.RegisterCallback<FocusOutEvent>(_ => Rebuild());
            r.Add(Ui.Place(nameField, typeX + 64f, 1f, 120f, RowH - 2f));
            float chipX = typeX + 64f + 120f + 6f;
            r.Add(Ui.Place(CodeChip(m), chipX, 1f, ChipW, RowH - 2f));
            var targets = Ui.Place(Ui.Text(m.bindings.Count == 0 ? "not bound — right-click a setting to bind it" : "→ " + string.Join(", ", m.bindings.Select(clip.TargetLabel)), "The settings this modifier drives.", "znd-mini"),
                                   chipX + ChipW + 6f, 0f, -1f, RowH);
            targets.style.right = RemoveW + 10f + EyeW + 4f;
            r.Add(targets);
            r.Add(Ui.PlaceRight(Ui.Eye(m.visible, v => v ? "Shown in the waveform's pictures. Click to leave it out (it keeps playing)." : "Hidden from the waveform's pictures. Click to show it.",
                v => m.visible = v, EyeW, RowH - 2f), RemoveW + 6f, 1f, EyeW, RowH - 2f));
            r.Add(Ui.PlaceRight(Ui.Button("×", "Remove this modifier and its bindings.", "RichButton", () => { clip.modifiers.Remove(m); Structural(); }, Corners.All, RemoveW, RowH - 2f),
                                0f, 1f, RemoveW, RowH - 2f));
            return r;
        }

        /// <summary>The chip that says whether game code can reach a modifier: grey "⚡" when not, amber "⚡ id" when it can.
        /// While playing, its fill shows the value code has it at, and it flashes when a new value arrives. Click to edit.</summary>
        VisualElement CodeChip(Modifier m) {
            var chip = new VisualElement();
            chip.AddToClassList("znd-codechip");
            var fill = new VisualElement { pickingMode = PickingMode.Ignore }; fill.AddToClassList("znd-codechip__fill");
            var text = new Label { pickingMode = PickingMode.Ignore }; text.AddToClassList("znd-codechip__text");
            text.style.position = Position.Absolute; text.style.left = 0; text.style.right = 0; text.style.top = 0; text.style.bottom = 0;
            chip.Add(fill); chip.Add(text);
            chip.RegisterCallback<PointerDownEvent>(e => {
                if (e.button != 0 && e.button != 1) return;
                CodeHookPopup.Show(chip.worldBound, m, Rebuild);
                e.StopPropagation();
            });
            float lastJ = -1f, pulseUntil = 0f;
            perTick.Add(() => {
                chip.EnableInClassList("znd-codechip--on", m.HasCode);
                chip.tooltip = m.HasCode ? "Game code can reach this modifier as '" + m.codeId + "'. Click to change." : "Game code cannot reach this modifier. Click to give it an id.";
                string label = m.HasCode ? "⚡ " + m.codeId : "⚡";
                float now = (float)EditorApplication.timeSinceStartup;
                if (!m.HasCode || !MockPlayer.Playing) { text.text = label; fill.style.display = DisplayStyle.None; lastJ = -1f; }
                else {
                    bool driven = CodeTest.Drive;
                    float v = driven ? CodeTest.ValueFor(m.codeId, now, 0) : (m.codeRest < 0f ? 1f : m.codeRest);
                    fill.style.display = DisplayStyle.Flex;
                    float w = chip.resolvedStyle.width; if (float.IsNaN(w)) w = ChipW;
                    fill.style.width = w * Mathf.Clamp01(v);
                    text.text = label + " " + (v >= 0.995f ? "1.00" : Mathf.Clamp01(v).ToString(".00")) + " · " + (driven ? "play" : "rest");
                    float j = driven ? MockSignal.Jitter(m.codeId, now, 0.3f) : 0f;
                    if (driven && j != lastJ) { if (lastJ >= 0f) pulseUntil = now + LiveOverlay.PulseSeconds; lastJ = j; }
                }
                chip.EnableInClassList("znd-codechip--pulse", now < pulseUntil);
            });
            return chip;
        }

        void ModifierBody(Modifier m, float w) {
            Wrap(m.ps.Select(p => (UnitWidth(p, false), (Func<float, float, float, VisualElement>)((x, y, uw) =>
                Unit(p, x, y, uw, null, Rebuild, ev => Ui.Menu(("Not modulatable", null, false, false)))))).ToList(), w, this);
            if (m.type == ModifierType.Envelope || m.type == ModifierType.LFO) Add(CurveGround(m));
            if (m.type == ModifierType.Step) Add(StepsRow(m));
        }

        /// <summary>A modifier's curve on its dark ground: quarter lines, caption, end labels, the curve (editable), and while
        /// playing an upright playhead with a dot on the curve and a dotted "what this play hears" line.</summary>
        VisualElement CurveGround(Modifier m) {
            bool lfo = m.type == ModifierType.LFO;
            var holder = new VisualElement();
            holder.style.height = CurveH; holder.style.flexShrink = 0;
            var ground = new VisualElement();
            ground.AddToClassList("znd-curveground");
            ground.style.position = Position.Absolute; ground.style.left = GripW + 6f; ground.style.right = 0; ground.style.top = 0; ground.style.bottom = 0;
            ground.tooltip = lfo ? "How strongly the LFO applies across the play. Drag points; double-click to add one."
                                 : "The value this envelope gives across the play. Drag points; double-click to add one.";
            holder.Add(ground);
            for (int q = 1; q < 4; q++) {
                var hl = Ui.Abs(); hl.AddToClassList("znd-curveground__grid"); hl.style.left = 0; hl.style.right = 0; hl.style.top = Length.Percent(q * 25f); hl.style.height = 1f;
                var vl = Ui.Abs(); vl.AddToClassList("znd-curveground__grid"); vl.style.top = 0; vl.style.bottom = 0; vl.style.left = Length.Percent(q * 25f); vl.style.width = 1f;
                ground.Add(hl); ground.Add(vl);
            }
            if (lfo) {
                // Faintly, behind the curve: what the LFO puts out across the play (a canned wave under the strength curve).
                var output = Ui.Abs(); output.style.left = 0; output.style.right = 0; output.style.top = 0; output.style.bottom = 0;
                output.generateVisualContent += ctx => {
                    var rect = output.contentRect;
                    var p = ctx.painter2D; p.fillColor = new Color(0.6f, 0.75f, 1f, 0.16f);
                    float rate = m.ps[1].value * 3f;
                    p.BeginPath();
                    for (int c = 0; c < (int)rect.width; c += 1) {
                        float x = c / Mathf.Max(1f, rect.width), hgt = Mathf.Abs(Mathf.Sin(x * rate * Mathf.PI * 2f)) * Mathf.Clamp01(m.curve.Evaluate(x)) * rect.height;
                        if (hgt <= 0f) continue;
                        p.MoveTo(new Vector2(c, rect.height - hgt)); p.LineTo(new Vector2(c + 1, rect.height - hgt)); p.LineTo(new Vector2(c + 1, rect.height)); p.LineTo(new Vector2(c, rect.height)); p.ClosePath();
                    }
                    p.Fill();
                };
                ground.Add(output);
                perTick.Add(output.MarkDirtyRepaint);
            }
            var baseLine = Ui.Abs(); baseLine.AddToClassList("znd-curveground__base"); baseLine.style.left = 0; baseLine.style.right = 0; baseLine.style.bottom = 0; baseLine.style.height = 1f;
            ground.Add(baseLine);
            AxisLabels(m, lfo, out string top, out string bottom);
            var cap = Ui.Place(Ui.Text(lfo ? "strength over the play" : "shape over the play", "", "znd-curvecap"), 9f, 0f, 200f, 13f);
            var topL = Ui.Text(top, "", "znd-curvelabel", "znd-right"); topL.style.position = Position.Absolute; topL.style.right = 2f; topL.style.top = 0; topL.style.width = 60f; topL.style.height = 12f;
            var botL = Ui.Text(bottom, "", "znd-curvelabel"); botL.style.position = Position.Absolute; botL.style.left = 9f; botL.style.bottom = 1f; botL.style.width = 60f; botL.style.height = 12f;
            var secs = Ui.Text(clip.PlaySeconds.ToString("0.00") + " s", "", "znd-curvelabel", "znd-right"); secs.style.position = Position.Absolute; secs.style.right = 2f; secs.style.bottom = 1f; secs.style.width = 60f; secs.style.height = 12f;
            perTick.Add(() => secs.text = clip.PlaySeconds.ToString("0.00") + " s");
            ground.Add(cap); ground.Add(topL); ground.Add(botL); ground.Add(secs);

            var curve = new CurveEditor(m.curve, lfo ? new Color(0.9f, 0.2f, 0.1f) : new Color(0.1f, 0.7f, 0.1f));
            curve.style.position = Position.Absolute; curve.style.left = 0; curve.style.right = 0; curve.style.top = 0; curve.style.bottom = 0;
            curve.tooltip = ground.tooltip;
            curve.onChanged = () => MockPlayer.Edited(clip);
            curve.onPointContext = (pt, world) => RandomPointPopup.Show(world, pt, m.curve, curve.Refresh);
            ground.Add(curve);

            var head = Ui.Abs(); head.AddToClassList("znd-curvehead"); head.style.top = 0; head.style.bottom = 0; head.style.width = 1.5f;
            var dot = Ui.Abs(); dot.AddToClassList("znd-curvedot"); dot.style.width = 6f; dot.style.height = 6f;
            ground.Add(head); ground.Add(dot);
            bool wasPlaying = false;
            perTick.Add(() => {
                bool on = MockPlayer.Playing;
                head.style.display = dot.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
                if (on) {
                    var rc = ground.contentRect;
                    float f = MockPlayer.Progress, x = f * rc.width;
                    head.style.left = x - 0.5f;
                    dot.style.left = x - 3f; dot.style.top = rc.height - Mathf.Clamp01(m.curve.Evaluate(f)) * rc.height - 3f;
                    if (!lfo) {
                        float seed = MockSignal.Jitter("env" + m.name, (float)EditorApplication.timeSinceStartup, 3f) - 0.5f;
                        var c = m.curve;
                        curve.liveCurves = new List<Func<float, float>> { t => c.Evaluate(t) + seed * 0.1f };
                    }
                    curve.Refresh();
                }
                else if (wasPlaying) { curve.liveCurves = null; curve.Refresh(); }
                wasPlaying = on;
            });
            return holder;
        }

        void AxisLabels(Modifier m, bool lfo, out string top, out string bottom) {
            if (lfo) { top = "full"; bottom = "none"; return; }
            var b = m.bindings.FirstOrDefault();
            if (b == null) { top = "top"; bottom = "bottom"; return; }
            if (b.effect == null) {
                if (b.ownValue == "Pitch") { top = "+12 st"; bottom = "−12 st"; return; }
                top = "×2"; bottom = "×0"; return;
            }
            var p = b.effect.ps[b.param];
            top = p.Format(p.max); bottom = p.Format(p.min);
        }

        /// <summary>A step list: one bar per step, dragged up and down, with + and − to add or remove steps. While playing,
        /// the step being "played" is lit.</summary>
        VisualElement StepsRow(Modifier m) {
            int n = m.steps.Length;
            var r = Ui.PlacedRow(StepBandH);
            float w = Width - (GripW + 6f), buttons = 22f * 2f + Gap;
            float barW = Mathf.Min(StepBarMaxW, (w - buttons) / n);
            var bars = new StepBars(m.steps, -1f, 1f, 0f, v => { m.steps = v; }, i => i < m.steps.Length ? "Step " + (i + 1) + ": " + m.steps[i].ToString("+0.00;-0.00;0.00") : null);
            bars.AddToClassList("znd-slider-default");
            bars.tooltip = "Steps: drag a bar up or down, or sweep across several; double-click a bar to put it back on the line.";
            r.Add(Ui.Place(bars, GripW + 6f, 2f, barW * n, StepBandH - 4f));
            perTick.Add(() => bars.highlight = MockPlayer.Playing ? Mathf.Min(n - 1, (int)(MockPlayer.Progress * n)) : -1);
            float bx = GripW + 6f + barW * n + Gap, by = 2f + (StepBandH - 4f - (RowH - 2f)) * 0.5f;
            var add = Ui.Button("+", "Add a step at the end.", "RichButton", () => { m.steps = m.steps.Concat(new[] { m.steps[m.steps.Length - 1] }).ToArray(); Structural(); }, Corners.Left, 22f, RowH - 2f);
            add.SetEnabled(n < 24);
            var rem = Ui.Button("−", "Remove the last step.", "RichButton", () => { m.steps = m.steps.Take(m.steps.Length - 1).ToArray(); Structural(); }, Corners.Right, 22f, RowH - 2f);
            rem.SetEnabled(n > 1);
            r.Add(Ui.Place(add, bx, by, 22f, RowH - 2f));
            r.Add(Ui.Place(rem, bx + 22f, by, 22f, RowH - 2f));
            return r;
        }

        void Bindings(Modifier m) {
            foreach (var b in m.bindings) {
                var bb = b;
                var r = Ui.PlacedRow();
                float lx = GripW + 6f;
                r.Add(Ui.Place(Ui.Text("→ " + clip.TargetLabel(b), "The setting this binding drives.", "znd-mini"), lx, 0f, LabelW + 60f, RowH));
                float x = lx + LabelW + 60f + 4f;
                bool ratioOk = b.effect == null ? b.ownValue == "Pitch" || b.ownValue == "Speed" : b.effect.ps[b.param].kind == ParamKind.LogSlider;
                bool scaleOk = b.effect == null || b.effect.ps[b.param].min >= 0f;
                var ops = new ToggleButton[4];
                for (int o = 0; o < 4; o++) {
                    int oi = o;
                    var corner = o == 0 ? Corners.Left : o == 3 ? Corners.Right : Corners.None;
                    bool offered = o == 2 ? scaleOk : o == 3 ? ratioOk : true;
                    ops[o] = Ui.Toggle(CombineLabels[o], offered ? CombineTips[o] : CombineLabels[o] + " does nothing on this setting.", b.combine == o,
                        _ => { bb.combine = oi; for (int q = 0; q < 4; q++) ops[q].value = q == oi; MockPlayer.Edited(clip); }, "RichToggle", corner, 42f, RowH - 2f);
                    ops[o].SetEnabled(offered);
                    r.Add(Ui.Place(ops[o], x + o * 42f, 1f, 42f, RowH - 2f));
                }
                r.Add(Ui.Place(Ui.Slider("Depth", b.depth, 0f, 1f, "How far the modifier moves the setting.", v => bb.depth = v, TrackSlider.LabelMode.LabelAndValue, 0.25f, "default", 120f, RowH - 2f),
                               x + 176f, 1f, 120f, RowH - 2f));
                r.Add(Ui.Place(Ui.Button("×", "Remove this binding.", "RichButton", () => { m.bindings.Remove(bb); Structural(); }, Corners.All, RemoveW, RowH - 2f),
                               x + 176f + 124f, 1f, RemoveW, RowH - 2f));
                Add(r);
            }
        }
    }
}
