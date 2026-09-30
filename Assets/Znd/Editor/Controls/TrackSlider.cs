using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Znd.Mock {

    /// <summary>
    /// A slider whose track IS the control: a fill from the start to the value (<c>znd-slider__fill</c>), the rest of the
    /// track (<c>znd-slider__rest</c>), and its label centred across both (<c>znd-slider__label</c>). Press anywhere to set
    /// the value, drag to follow, double-click to reset to the default. Draws nothing itself: every pixel is USS.
    /// </summary>
    public class TrackSlider : VisualElement {

        public enum LabelMode { LabelOnly, ValueOnly, LabelAndValue, None }

        readonly VisualElement _fill, _rest;
        readonly Label _label;
        readonly Action<float> _onChanged;
        readonly float _min, _max;
        readonly float? _default;
        readonly LabelMode _mode;
        readonly Func<float, string> _format;
        float _value;
        string _text;
        bool _dragging;

        public float value { get => _value; set => SetValueWithoutNotify(value); }
        public string text { get => _text; set { _text = value; Refresh(); } }
        public float min => _min;
        public float max => _max;

        public TrackSlider(string text, float value, float min, float max, string tooltip, Action<float> onChanged,
                           LabelMode mode = LabelMode.LabelOnly, float? defaultValue = null, Func<float, string> format = null) {
            _text = text; _min = min; _max = max; _default = defaultValue; _mode = mode; _onChanged = onChanged;
            string fmt = TrackLabel.AutoFormat(min, max);
            _format = format ?? (v => v.ToString(fmt, CultureInfo.InvariantCulture));
            this.tooltip = tooltip;
            AddToClassList("znd-slider");
            style.flexDirection = FlexDirection.Row;
            _fill = new VisualElement { pickingMode = PickingMode.Ignore };
            _fill.AddToClassList("znd-slider__fill");
            _rest = new VisualElement { pickingMode = PickingMode.Ignore };
            _rest.AddToClassList("znd-slider__rest");
            _label = TrackLabel.Create();
            Add(_fill); Add(_rest); Add(_label);
            RegisterCallback<PointerDownEvent>(OnDown);
            RegisterCallback<PointerMoveEvent>(OnMove);
            RegisterCallback<PointerUpEvent>(OnUp);
            RegisterCallback<GeometryChangedEvent>(_ => Refresh());
            SetValueWithoutNotify(value);
        }

        public void SetValueWithoutNotify(float v) { _value = Mathf.Clamp(v, _min, _max); Refresh(); }

        /// <summary>0..1 position of the value along the track.</summary>
        public float Normalized => _max > _min ? Mathf.InverseLerp(_min, _max, _value) : 0f;

        void Refresh() {
            float t = Normalized;
            _fill.style.width = Length.Percent(t * 100f);
            _rest.style.width = Length.Percent((1f - t) * 100f);
            _fill.style.display = t > 0f ? DisplayStyle.Flex : DisplayStyle.None;
            _rest.style.display = t < 1f ? DisplayStyle.Flex : DisplayStyle.None;
            string v = _format(_value);
            string full = _mode == LabelMode.None ? "" : _mode == LabelMode.ValueOnly ? v
                        : _mode == LabelMode.LabelOnly ? _text
                        : string.IsNullOrEmpty(_text) ? v : _text + ": " + v;
            TrackLabel.Fit(_label, full, _mode == LabelMode.LabelAndValue ? v : null, resolvedStyle.width);
        }

        void SetFromPointer(float x) {
            float w = resolvedStyle.width;
            if (w <= 0f) return;
            float nv = Mathf.Round(Mathf.Lerp(_min, _max, Mathf.Clamp01(x / w)) * 100000f) / 100000f;
            if (Mathf.Approximately(nv, _value)) return;
            _value = nv; Refresh();
            _onChanged?.Invoke(_value);
        }

        void OnDown(PointerDownEvent e) {
            if (e.button != 0) return;
            if (e.clickCount == 2 && _default.HasValue) {
                _value = Mathf.Clamp(_default.Value, _min, _max); Refresh();
                _onChanged?.Invoke(_value);
                e.StopPropagation();
                return;
            }
            _dragging = true;
            this.CapturePointer(e.pointerId);
            SetFromPointer(e.localPosition.x);
            e.StopPropagation();
        }

        void OnMove(PointerMoveEvent e) { if (_dragging && this.HasPointerCapture(e.pointerId)) SetFromPointer(e.localPosition.x); }

        void OnUp(PointerUpEvent e) {
            if (!_dragging) return;
            _dragging = false;
            if (this.HasPointerCapture(e.pointerId)) this.ReleasePointer(e.pointerId);
        }
    }

    /// <summary>The centred label inside a slider track: shrinks a point at a time (down to 70 %) when it does not fit, then
    /// falls back to the value alone.</summary>
    static class TrackLabel {

        public static Label Create() {
            var l = new Label { pickingMode = PickingMode.Ignore };
            l.AddToClassList("znd-slider__label");
            l.style.position = Position.Absolute;
            l.style.left = 0; l.style.right = 0; l.style.top = 0; l.style.bottom = 0;
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            l.style.marginLeft = l.style.marginRight = l.style.marginTop = l.style.marginBottom = 0;
            l.style.paddingLeft = l.style.paddingRight = l.style.paddingTop = l.style.paddingBottom = 0;
            l.style.overflow = Overflow.Hidden;
            l.style.whiteSpace = WhiteSpace.Pre;
            return l;
        }

        public static void Fit(Label label, string text, string fallback, float width) {
            width -= 6f;
            label.style.fontSize = StyleKeyword.Null;
            label.text = text ?? "";
            if (string.IsNullOrEmpty(text) || float.IsNaN(width) || width <= 0f) return;
            float baseSize = label.resolvedStyle.fontSize > 0f ? label.resolvedStyle.fontSize : 12f;
            int size0 = Mathf.RoundToInt(baseSize);
            int floor = Mathf.Max(8, Mathf.FloorToInt(size0 * 0.7f));
            float WidthAt(string s, int size) =>
                label.MeasureTextSize(s, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x * size / baseSize;
            if (WidthAt(text, size0) <= width) return;
            bool Shrink(string s) {
                for (int size = size0; size >= floor; size--)
                    if (WidthAt(s, size) <= width) { label.text = s; label.style.fontSize = size; return true; }
                return false;
            }
            if (Shrink(text)) return;
            if (!string.IsNullOrEmpty(fallback) && fallback != text && Shrink(fallback)) return;
            label.text = string.IsNullOrEmpty(fallback) ? text : fallback;
            label.style.fontSize = floor;
        }

        public static string AutoFormat(float min, float max) {
            float range = Mathf.Abs(max - min);
            return range <= 1f ? "F2" : range <= 10f ? "F1" : "F0";
        }
    }
}
