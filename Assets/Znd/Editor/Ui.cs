using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Znd.Mock {

    /// <summary>Which corners of a control are rounded. None keeps the look's own corners.</summary>
    public enum Corners { None, All, Left, Right, Top, Bottom, Square }

    /// <summary>
    /// Small factory for the Znd controls. Every control is plain UI Toolkit; its look comes entirely from the two style
    /// sheets (Skin/ZndSkin.uss for the looks, ZndLayout.uss for placement), so a reskin never needs to touch C#.
    /// </summary>
    public static class Ui {

        public const float RowH = 20f;
        public const float LineH = 18f;

        const string SkinPath = "Assets/Znd/Editor/Skin/ZndSkin.uss";
        const string LayoutPath = "Assets/Znd/Editor/ZndLayout.uss";

        /// <summary>Makes an element a Znd root: both style sheets, and the class every rule hangs off.</summary>
        public static void Attach(VisualElement root) {
            foreach (var path in new[] { SkinPath, LayoutPath }) {
                var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                if (sheet != null && !root.styleSheets.Contains(sheet)) root.styleSheets.Add(sheet);
            }
            root.AddToClassList("znd-root");
        }

        static readonly string[] CornerClasses = {
            "", "znd-corners--all", "znd-corners--left", "znd-corners--right", "znd-corners--top", "znd-corners--bottom", "znd-corners--square"
        };

        public static T WithCorners<T>(this T e, Corners c) where T : VisualElement {
            for (int i = 1; i < CornerClasses.Length; i++) e.RemoveFromClassList(CornerClasses[i]);
            if (c != Corners.None) e.AddToClassList(CornerClasses[(int)c]);
            return e;
        }

        public static T Size<T>(this T e, float w, float h) where T : VisualElement {
            if (w > 0f) e.style.width = w;
            if (h > 0f) e.style.height = h;
            e.style.flexShrink = 0; e.style.flexGrow = 0;
            return e;
        }

        /// <summary>A button in one of the skin's looks ("RichButton", "Default", "Flat", "BigButton", ...).</summary>
        public static Button Button(string label, string tooltip, string look, Action onClick, Corners corners = Corners.None, float w = -1f, float h = RowH) {
            var b = new Button(onClick) { text = label, tooltip = tooltip };
            b.AddToClassList("znd-" + look);
            return b.WithCorners(corners).Size(w, h);
        }

        /// <summary>A button that stays pressed while on. <paramref name="onColor"/> replaces the look's on state with a flat fill.</summary>
        public static ToggleButton Toggle(string label, string tooltip, bool value, Action<bool> onChanged, string look = "RichToggle",
                                          Corners corners = Corners.None, float w = -1f, float h = RowH, Color? onColor = null) {
            var t = new ToggleButton(label, tooltip, value, onChanged) { onColor = onColor };
            t.AddToClassList("znd-" + look);
            return t.WithCorners(corners).Size(w, h);
        }

        /// <summary>The eye toggle: open while the thing is shown, closed while hidden.</summary>
        public static ToggleButton Eye(bool visible, Func<bool, string> tooltip, Action<bool> onChanged, float w = 22f, float h = LineH) {
            ToggleButton t = null;
            var icon = new Image { scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            icon.AddToClassList("znd-eyeicon");
            void Sync(bool v) {
                icon.image = EditorGUIUtility.IconContent(v ? "d_scenevis_visible_hover" : "d_scenevis_hidden_hover").image;
                t.tooltip = tooltip(v);
            }
            t = Toggle("", tooltip(visible), visible, v => { Sync(v); onChanged?.Invoke(v); }, "RichToggle", Corners.All, w, h);
            t.markWhenOn = false;
            t.Add(icon);
            Sync(visible);
            return t;
        }

        /// <summary>A slider with its name and value drawn inside its own track.</summary>
        public static TrackSlider Slider(string text, float value, float min, float max, string tooltip, Action<float> onChanged,
                                         TrackSlider.LabelMode mode = TrackSlider.LabelMode.LabelOnly, float? defaultValue = null,
                                         string look = "default", float w = 150f, float h = LineH, Func<float, string> format = null) {
            var s = new TrackSlider(text, value, min, max, tooltip, onChanged, mode, defaultValue, format);
            s.AddToClassList("znd-slider-" + look);
            return s.Size(w, h);
        }

        /// <summary>A two-handled range in one track: handles together is one fixed value, apart is a range.</summary>
        public static RangeSlider Range(string text, float lo, float hi, float absMin, float absMax, string tooltip, Action<float, float> onChanged,
                                        string look = "default", RangeSlider.LabelMode mode = RangeSlider.LabelMode.LabelAndValues,
                                        bool bipolar = false, float w = 150f, float h = LineH) {
            var r = new RangeSlider(text, lo, hi, absMin, absMax, tooltip, onChanged, mode, bipolar);
            r.AddToClassList("znd-slider-" + look);
            return r.Size(w, h);
        }

        // ── layout helpers ──

        public static VisualElement Row(float h = RowH) {
            var r = new VisualElement();
            r.style.flexDirection = FlexDirection.Row; r.style.flexShrink = 0;
            if (h > 0f) r.style.height = h;
            return r;
        }

        /// <summary>A row whose children are placed at fixed offsets (absolute), like the chain rows.</summary>
        public static VisualElement PlacedRow(float h = RowH) {
            var r = new VisualElement();
            r.AddToClassList("znd-placedrow");
            r.style.height = h; r.style.flexShrink = 0;
            return r;
        }

        public static VisualElement Gap(float w) { var e = new VisualElement(); e.style.width = w; e.style.flexShrink = 0; return e; }
        public static VisualElement Space(float h) { var e = new VisualElement(); e.style.height = h; e.style.flexShrink = 0; return e; }
        public static VisualElement Flex() { var e = new VisualElement(); e.style.flexGrow = 1; return e; }

        public static Label Text(string text, string tooltip, params string[] classes) {
            var l = new Label(text) { tooltip = tooltip };
            l.AddToClassList("znd-lbl");
            foreach (var c in classes) l.AddToClassList(c);
            return l;
        }

        public static T Place<T>(T e, float x, float y, float w, float h) where T : VisualElement {
            e.style.position = Position.Absolute;
            e.style.left = x; e.style.top = y;
            if (w >= 0f) e.style.width = w;
            if (h >= 0f) e.style.height = h;
            return e;
        }

        public static T PlaceRight<T>(T e, float right, float y, float w, float h) where T : VisualElement {
            e.style.position = Position.Absolute;
            e.style.right = right; e.style.top = y; e.style.width = w; e.style.height = h;
            return e;
        }

        public static VisualElement Fill(Color c) {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.style.position = Position.Absolute;
            e.style.left = 0; e.style.right = 0; e.style.top = 0; e.style.bottom = 0;
            e.style.backgroundColor = c;
            return e;
        }

        public static VisualElement Abs() {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.style.position = Position.Absolute;
            return e;
        }

        public static void PlaceRect(VisualElement e, Rect r) {
            e.style.left = r.x; e.style.top = r.y; e.style.width = Mathf.Max(0f, r.width); e.style.height = Mathf.Max(0f, r.height);
        }

        /// <summary>Opens an ordinary editor context menu. Items are (label, action, checked, enabled); "/" makes a submenu, null label a separator.</summary>
        public static void Menu(params (string label, Action action, bool on, bool enabled)[] items) {
            var menu = new GenericMenu();
            foreach (var it in items) {
                if (it.label == null) { menu.AddSeparator(""); continue; }
                var c = new GUIContent(it.label);
                if (!it.enabled || it.action == null) menu.AddDisabledItem(c, it.on);
                else { var a = it.action; menu.AddItem(c, it.on, () => a()); }
            }
            menu.ShowAsContext();
        }
    }
}
