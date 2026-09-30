using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Znd.Mock {

    /// <summary>
    /// A card laid over the window next to an anchor (above it when there is room). A click outside it or Esc closes it.
    /// Styled by <c>.znd-popover</c>.
    /// </summary>
    public class Popover : VisualElement {

        readonly VisualElement host;
        EventCallback<PointerDownEvent> outside;
        public Action onClosed;

        public static Popover Show(VisualElement anchor, Action<VisualElement> build) {
            var host = anchor.panel?.visualTree;
            if (host == null) return null;
            // Stack it on the window's own root so it takes the Znd style sheets.
            var root = anchor;
            while (root.parent != null && !root.ClassListContains("znd-root")) root = root.parent;
            var p = new Popover(root);
            build(p);
            p.Open(anchor);
            return p;
        }

        Popover(VisualElement host) {
            this.host = host;
            AddToClassList("znd-popover");
            style.position = Position.Absolute;
            focusable = true;
            RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Escape) Close(); });
        }

        void Open(VisualElement anchor) {
            host.Add(this);
            style.visibility = Visibility.Hidden;
            schedule.Execute(() => {
                var a = host.WorldToLocal(anchor.worldBound);
                float w = resolvedStyle.width, h = resolvedStyle.height;
                float x = Mathf.Clamp(a.xMax - w, 4f, Mathf.Max(4f, host.layout.width - w - 4f));
                float y = a.y - h - 4f >= 0f ? a.y - h - 4f : a.yMax + 4f;
                style.left = x; style.top = y;
                style.visibility = Visibility.Visible;
                Focus();
            });
            outside = e => { if (!worldBound.Contains(e.position)) Close(); };
            host.RegisterCallback(outside, TrickleDown.TrickleDown);
        }

        public void Close() {
            if (parent == null) return;
            host.UnregisterCallback(outside, TrickleDown.TrickleDown);
            RemoveFromHierarchy();
            onClosed?.Invoke();
        }
    }

    /// <summary>Base for the small popup windows: attaches the Znd look and builds UI Toolkit content.</summary>
    public abstract class ZndPopup : PopupWindowContent {
        protected abstract Vector2 Size { get; }
        protected abstract void Build(VisualElement root);
        public override Vector2 GetWindowSize() => Size;
        public override void OnGUI(Rect rect) { }
        public override void OnOpen() {
            var root = editorWindow.rootVisualElement;
            Ui.Attach(root);
            root.AddToClassList("znd-popupwindow");
            Rebuild();
        }
        protected void Rebuild() { var root = editorWindow.rootVisualElement; root.Clear(); Build(root); }
    }

    /// <summary>A modifier's code-access settings, opened from its ⚡ chip: the id game code uses, Scale or Set, where it
    /// rests until code sends a value, and how quickly it follows.</summary>
    public class CodeHookPopup : ZndPopup {
        readonly Modifier mod; readonly Action changed;
        CodeHookPopup(Modifier mod, Action changed) { this.mod = mod; this.changed = changed; }
        public static void Show(Rect anchor, Modifier mod, Action changed) => UnityEditor.PopupWindow.Show(anchor, new CodeHookPopup(mod, changed));
        protected override Vector2 Size => new Vector2(6f + 32f + 130f + 14f + 6f + 48f + 40f + 6f + 84f + 4f + 110f + 6f + 130f + 6f, 32f);

        protected override void Build(VisualElement root) {
            var r = Ui.Row();
            var id = Ui.Text("⚡ Id", "The name game code would use to reach this modifier. Empty: code cannot reach it.", "znd-codemark");
            id.style.width = 32f; id.style.unityTextAlign = TextAnchor.MiddleLeft;
            r.Add(id);
            var field = new TextField { value = mod.codeId, tooltip = id.tooltip };
            field.AddToClassList("znd-namefield");
            field.Size(130f, Ui.LineH);
            field.RegisterValueChangedCallback(e => { mod.codeId = e.newValue.Trim(); changed?.Invoke(); });
            r.Add(field);
            var warn = Ui.Text("⚠", "Another modifier already uses this id (mock warning).", "znd-warnmark");
            warn.style.width = 14f; warn.style.visibility = Visibility.Hidden;
            r.Add(warn);
            r.Add(Ui.Gap(6f));
            ToggleButton scale = null, set = null;
            scale = Ui.Toggle("Scale", "The value scales how strongly this modifier acts.", mod.codeMode == 0,
                _ => { mod.codeMode = 0; scale.value = true; set.value = false; changed?.Invoke(); }, "RichToggle", Corners.Left, 48f, Ui.LineH);
            set = Ui.Toggle("Set", "The value is how strongly this modifier acts.", mod.codeMode == 1,
                _ => { mod.codeMode = 1; set.value = true; scale.value = false; changed?.Invoke(); }, "RichToggle", Corners.Right, 40f, Ui.LineH);
            r.Add(scale); r.Add(set);
            r.Add(Ui.Gap(6f));
            bool authored = mod.codeRest < 0f;
            r.Add(Ui.Toggle("As authored", "Rest as tuned until code sends a value. Off: choose the resting value beside it.", authored,
                v => { mod.codeRest = v ? -1f : 1f; changed?.Invoke(); Rebuild(); }, "RichToggle", Corners.All, 84f, Ui.LineH));
            r.Add(Ui.Gap(4f));
            var rest = Ui.Slider("Rest", authored ? 1f : mod.codeRest, 0f, 1f, "Where the value rests before code sends anything.",
                v => { mod.codeRest = v; changed?.Invoke(); }, TrackSlider.LabelMode.LabelAndValue, 1f, "default", 110f, Ui.LineH);
            rest.style.visibility = authored ? Visibility.Hidden : Visibility.Visible;
            r.Add(rest);
            r.Add(Ui.Gap(6f));
            TrackSlider follow = null;
            follow = Ui.Slider("Follow " + mod.codeFollowMs.ToString("0") + " ms", mod.codeFollowMs, 0f, 500f, "How long a new value takes to be reached.",
                v => { mod.codeFollowMs = Mathf.Round(v); follow.text = "Follow " + mod.codeFollowMs.ToString("0") + " ms"; changed?.Invoke(); },
                TrackSlider.LabelMode.LabelOnly, 30f, "default", 130f, Ui.LineH);
            r.Add(follow);
            root.Add(r);
        }
    }

    /// <summary>A curve point's random settings, opened by right-clicking the point.</summary>
    public class RandomPointPopup : ZndPopup {
        readonly CurvePoint point; readonly Curve curve; readonly Action changed;
        RandomPointPopup(CurvePoint p, Curve c, Action changed) { point = p; curve = c; this.changed = changed; }
        public static void Show(Vector2 world, CurvePoint p, Curve c, Action changed) =>
            UnityEditor.PopupWindow.Show(new Rect(world.x - 4f, world.y + 6f, 8f, 8f), new RandomPointPopup(p, c, changed));
        protected override Vector2 Size => new Vector2(556f, 32f);

        protected override void Build(VisualElement root) {
            var r = Ui.Row();
            bool on = point.randomX > 0f || point.randomY > 0f;
            float xr = curve.xMax - curve.xMin, yr = curve.yMax - curve.yMin;
            r.Add(Ui.Toggle("Random", on ? "Make this an ordinary point again." : "Let every play move this point inside an ellipse around it.", on, v => {
                if (v) { point.randomX = 0.05f * xr; point.randomY = 0.1f * yr; } else { point.randomX = point.randomY = 0f; }
                changed?.Invoke(); Rebuild();
            }, "RichToggle", Corners.All, 72f, Ui.RowH));
            if (on) {
                r.Add(Ui.Gap(8f));
                TrackSlider sx = null, sy = null;
                sx = Ui.Slider("Across ±" + (point.randomX / xr * 100f).ToString("0") + " %", point.randomX / xr, 0f, 0.5f, "How far a play may move it along the curve.",
                    v => { point.randomX = v * xr; sx.text = "Across ±" + (v * 100f).ToString("0") + " %"; changed?.Invoke(); }, TrackSlider.LabelMode.LabelOnly, 0.05f, "default", 150f, Ui.LineH);
                r.Add(sx); r.Add(Ui.Gap(6f));
                sy = Ui.Slider("Height ±" + (point.randomY / yr * 100f).ToString("0") + " %", point.randomY / yr, 0f, 0.5f, "How far a play may move it up or down.",
                    v => { point.randomY = v * yr; sy.text = "Height ±" + (v * 100f).ToString("0") + " %"; changed?.Invoke(); }, TrackSlider.LabelMode.LabelOnly, 0.1f, "default", 150f, Ui.LineH);
                r.Add(sy); r.Add(Ui.Gap(6f));
                r.Add(Ui.Slider("Bias", point.bias, 0f, 1f, "Where in the ellipse it tends to land.", v => { point.bias = v; changed?.Invoke(); },
                    TrackSlider.LabelMode.LabelAndValue, 0.5f, "default", 140f, Ui.LineH));
            }
            root.Add(r);
        }
    }

    /// <summary>A one-field name popup (rename a snapshot, a preset). Applies on Enter or on clicking away; Esc cancels.</summary>
    public class NamePopup : ZndPopup {
        readonly string label, value; readonly Action<string> apply;
        TextField field; bool cancelled;
        NamePopup(string label, string value, Action<string> apply) { this.label = label; this.value = value; this.apply = apply; }
        public static void Show(Rect anchor, string label, string value, Action<string> apply) => UnityEditor.PopupWindow.Show(anchor, new NamePopup(label, value, apply));
        protected override Vector2 Size => new Vector2(256f, 32f);

        protected override void Build(VisualElement root) {
            var r = Ui.Row();
            var l = Ui.Text(label, "", "znd-guilabel"); l.style.width = 44f;
            field = new TextField { value = value }; field.AddToClassList("znd-namefield"); field.Size(180f, Ui.LineH);
            field.RegisterCallback<KeyDownEvent>(e => {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) editorWindow.Close();
                else if (e.keyCode == KeyCode.Escape) { cancelled = true; editorWindow.Close(); }
            });
            r.Add(l); r.Add(field);
            root.Add(r);
            field.schedule.Execute(() => { field.Focus(); field.SelectAll(); });
        }

        public override void OnClose() { if (!cancelled && field != null && field.value != value && !string.IsNullOrWhiteSpace(field.value)) apply?.Invoke(field.value.Trim()); }
    }

    /// <summary>The chain presets: use one, audition it, rename, duplicate or delete (all mock).</summary>
    public class LibraryPopup : ZndPopup {
        readonly ClipData clip; readonly Action changed;
        LibraryPopup(ClipData clip, Action changed) { this.clip = clip; this.changed = changed; }
        public static void Show(Rect anchor, ClipData clip, Action changed) => UnityEditor.PopupWindow.Show(anchor, new LibraryPopup(clip, changed));
        protected override Vector2 Size => new Vector2(360f, 8f + (MockLibrary.Presets.Length + 1) * 22f);

        protected override void Build(VisualElement root) {
            var title = Ui.Text("Chain presets", "", "znd-subheader");
            title.style.height = Ui.RowH;
            root.Add(title);
            foreach (var name in MockLibrary.Presets) {
                var r = Ui.Row(); r.style.marginBottom = 2f;
                var l = Ui.Text(name + (clip.presetName == name ? "  (in use)" : ""), "", "znd-guilabel"); l.style.flexGrow = 1;
                r.Add(l);
                string n = name;
                r.Add(Ui.Button("Use", "Link this sound to the preset.", "RichButton", () => { clip.presetName = n; changed?.Invoke(); editorWindow.Close(); }, Corners.Left, 44f, Ui.LineH));
                r.Add(Ui.Button("▶", "Audition the preset on this sound.", "RichButton", () => MockPlayer.Play(clip), Corners.None, 26f, Ui.LineH));
                r.Add(Ui.Button("⋯", "Rename, duplicate, delete.", "RichButton", () => Ui.Menu(("Rename…", () => { }, false, true), ("Duplicate", () => { }, false, true), (null, null, false, true), ("Delete", () => { }, false, true)), Corners.Right, 26f, Ui.LineH));
                root.Add(r);
            }
        }
    }
}
