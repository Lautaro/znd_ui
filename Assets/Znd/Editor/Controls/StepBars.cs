using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Znd.Mock {

    /// <summary>
    /// A row of vertical bars, one per value, each set by dragging its height. Each bar is a groove (the slider's
    /// <c>znd-slider__rest</c>) with a fill (<c>znd-slider__fill</c>) from the baseline to its value, so it shares the
    /// slider look. Sweep across bars to paint them; double-click puts a bar back on the baseline. <see cref="highlight"/>
    /// marks the bar currently playing.
    /// </summary>
    public class StepBars : VisualElement {

        const float Gap = 2f;
        float[] _values;
        readonly float _min, _max, _baseline;
        readonly Action<float[]> _onChanged;
        readonly Func<int, string> _tooltipFor;
        VisualElement[] _bars, _fills, _hovers;
        readonly VisualElement _baseLine;
        int _last = -1, _hovered = -1, _highlight = -1;
        float _lastValue;
        bool _dragging;

        public int highlight { get => _highlight; set { if (value != _highlight) { _highlight = value; Relayout(); } } }

        public StepBars(float[] values, float min, float max, float baseline, Action<float[]> onChanged, Func<int, string> tooltipFor = null) {
            _min = min; _max = max; _baseline = baseline; _onChanged = onChanged; _tooltipFor = tooltipFor;
            AddToClassList("znd-stepbars");
            _baseLine = new VisualElement { pickingMode = PickingMode.Ignore };
            _baseLine.AddToClassList("znd-stepbars__baseline");
            _baseLine.style.position = Position.Absolute; _baseLine.style.left = 0; _baseLine.style.right = 0; _baseLine.style.height = 1f;
            SetValues(values);
            RegisterCallback<PointerDownEvent>(OnDown);
            RegisterCallback<PointerMoveEvent>(OnMove);
            RegisterCallback<PointerUpEvent>(OnUp);
            RegisterCallback<PointerLeaveEvent>(_ => { _hovered = -1; Relayout(); });
            RegisterCallback<GeometryChangedEvent>(_ => Relayout());
        }

        public void SetValues(float[] values) {
            int n = values?.Length ?? 0;
            bool rebuild = _values == null || _values.Length != n;
            _values = values != null ? (float[])values.Clone() : new float[0];
            if (rebuild) {
                Clear();
                _bars = new VisualElement[n]; _fills = new VisualElement[n]; _hovers = new VisualElement[n];
                for (int i = 0; i < n; i++) {
                    var bar = new VisualElement { pickingMode = PickingMode.Ignore };
                    bar.AddToClassList("znd-slider__rest"); bar.AddToClassList("znd-stepbars__bar");
                    bar.style.position = Position.Absolute; bar.style.top = 0; bar.style.bottom = 0;
                    var fill = new VisualElement { pickingMode = PickingMode.Ignore };
                    fill.AddToClassList("znd-slider__fill");
                    fill.style.position = Position.Absolute;
                    var hover = new VisualElement { pickingMode = PickingMode.Ignore };
                    hover.AddToClassList("znd-stepbars__hover");
                    hover.style.position = Position.Absolute; hover.style.top = 0; hover.style.bottom = 0;
                    Add(bar); Add(fill); Add(hover);
                    _bars[i] = bar; _fills[i] = fill; _hovers[i] = hover;
                }
                Add(_baseLine);
            }
            Relayout();
        }

        float Slot => _values.Length > 0 ? layout.width / _values.Length : 0f;
        int IndexAt(float x) => Mathf.Clamp((int)(x / Mathf.Max(1e-3f, Slot)), 0, _values.Length - 1);
        float ValueAt(float y) => Mathf.Clamp(Mathf.Lerp(_max, _min, Mathf.InverseLerp(0f, layout.height, y)), _min, _max);
        float YOf(float v) => Mathf.Lerp(layout.height, 0f, Mathf.InverseLerp(_min, _max, v));

        void Relayout() {
            float w = layout.width;
            if (float.IsNaN(w) || w <= 0f || _bars == null) return;
            float slot = Slot, yBase = YOf(Mathf.Clamp(_baseline, _min, _max));
            for (int i = 0; i < _values.Length; i++) {
                float x = i * slot + Gap * 0.5f, bw = Mathf.Max(1f, slot - Gap);
                _bars[i].style.left = x; _bars[i].style.width = bw;
                float yv = YOf(Mathf.Clamp(_values[i], _min, _max));
                var f = _fills[i];
                f.style.left = x; f.style.width = bw;
                f.style.top = Mathf.Min(yv, yBase); f.style.height = Mathf.Max(1f, Mathf.Abs(yBase - yv));
                _hovers[i].style.left = x; _hovers[i].style.width = bw;
                bool lit = i == _hovered || (_dragging && i == _last);
                _hovers[i].style.display = lit || i == _highlight ? DisplayStyle.Flex : DisplayStyle.None;
                _hovers[i].EnableInClassList("znd-stepbars__hover--playing", i == _highlight);
            }
            _baseLine.style.display = _baseline > _min && _baseline < _max ? DisplayStyle.Flex : DisplayStyle.None;
            _baseLine.style.top = yBase - 0.5f;
        }

        void Commit(float[] copy) {
            _values = copy;
            Relayout();
            _onChanged?.Invoke((float[])copy.Clone());
        }

        void OnDown(PointerDownEvent e) {
            if (e.button != 0 || _values.Length == 0) return;
            var copy = (float[])_values.Clone();
            int i = IndexAt(e.localPosition.x);
            if (e.clickCount == 2) copy[i] = Mathf.Clamp(_baseline, _min, _max);
            else {
                _dragging = true; _last = i; _lastValue = ValueAt(e.localPosition.y);
                copy[i] = _lastValue;
                this.CapturePointer(e.pointerId);
            }
            Commit(copy);
            e.StopPropagation();
        }

        void OnMove(PointerMoveEvent e) {
            if (_values.Length == 0) return;
            int i = IndexAt(e.localPosition.x);
            if (!_dragging || !this.HasPointerCapture(e.pointerId)) {
                if (i != _hovered) { _hovered = i; tooltip = _tooltipFor?.Invoke(i) ?? tooltip; Relayout(); }
                return;
            }
            float v = ValueAt(e.localPosition.y);
            var copy = (float[])_values.Clone();
            int from = _last < 0 ? i : _last, step = i >= from ? 1 : -1;
            for (int k = from; k != i + step; k += step) {
                float t = i == from ? 1f : (float)(k - from) / (i - from);
                copy[k] = Mathf.Lerp(_lastValue, v, t);
            }
            _last = i; _lastValue = v;
            Commit(copy);
        }

        void OnUp(PointerUpEvent e) {
            if (!_dragging) return;
            _dragging = false; _last = -1;
            if (this.HasPointerCapture(e.pointerId)) this.ReleasePointer(e.pointerId);
            Relayout();
        }
    }
}
