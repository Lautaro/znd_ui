using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Znd.Mock {

    /// <summary>
    /// A min/max range inside one track. With both ends equal it looks and acts like a plain slider (a press sets both).
    /// Apart, it shows rest | fill | rest with an edge line at each end: press near an edge to drag it, inside the fill to
    /// pan both, on the empty rail to move the nearer edge. Double-click a single value to open it into a range; double-click
    /// a range to close it toward the clicked side. A bipolar range fills from its centre and has a centre marker.
    /// </summary>
    public class RangeSlider : VisualElement {

        public enum LabelMode { None, LabelOnly, ValuesOnly, LabelAndValues }

        readonly VisualElement _track, _restL, _fill, _restR, _edgeMin, _edgeMax, _center;
        readonly Label _label;
        readonly Action<float, float> _onChanged;
        readonly string _text, _fmt;
        readonly float _absMin, _absMax, _center01;
        readonly bool _bipolar;
        readonly LabelMode _mode;
        float _lo, _hi;
        enum Drag { None, Min, Max, Fill }
        Drag _drag;
        float _anchorLo, _anchorHi, _anchorX;
        const float EdgeGrab = 6f, OpenFraction = 0.1f;

        public float lo => _lo;
        public float hi => _hi;

        public RangeSlider(string text, float lo, float hi, float absMin, float absMax, string tooltip, Action<float, float> onChanged,
                           LabelMode mode = LabelMode.LabelAndValues, bool bipolar = false) {
            _text = text; _absMin = absMin; _absMax = absMax; _mode = mode; _bipolar = bipolar; _onChanged = onChanged;
            _fmt = TrackLabel.AutoFormat(absMin, absMax);
            _center01 = 0.5f;
            this.tooltip = tooltip;
            AddToClassList("znd-range");
            style.flexDirection = FlexDirection.Row;
            _track = new VisualElement();
            _track.AddToClassList("znd-range__track");
            _track.style.flexGrow = 1;
            Add(_track);
            VisualElement Part(string cls) {
                var e = new VisualElement { pickingMode = PickingMode.Ignore };
                e.AddToClassList(cls);
                e.style.position = Position.Absolute; e.style.top = 0; e.style.bottom = 0;
                _track.Add(e);
                return e;
            }
            _restL = Part("znd-slider__rest");
            _fill = Part("znd-slider__fill");
            _restR = Part("znd-slider__rest");
            _edgeMin = Part("znd-range__edge--min");
            _edgeMax = Part("znd-range__edge--max");
            _center = Part("znd-range__edge--center");
            _edgeMin.pickingMode = _edgeMax.pickingMode = PickingMode.Position;   // so the edges get :hover
            _label = TrackLabel.Create();
            _track.Add(_label);
            _track.RegisterCallback<PointerDownEvent>(OnDown);
            _track.RegisterCallback<PointerMoveEvent>(OnMove);
            _track.RegisterCallback<PointerUpEvent>(OnUp);
            _track.RegisterCallback<GeometryChangedEvent>(_ => Refresh());
            SetValuesWithoutNotify(lo, hi);
        }

        public void SetValuesWithoutNotify(float lo, float hi) {
            _lo = Mathf.Clamp(lo, _absMin, _absMax);
            _hi = Mathf.Clamp(hi, _lo, _absMax);
            Refresh();
        }

        string F(float v) => v.ToString(_fmt, CultureInfo.InvariantCulture);
        bool Single => F(_lo) == F(_hi);
        float T(float v) => _absMax > _absMin ? Mathf.InverseLerp(_absMin, _absMax, v) : 0f;

        static void Span(VisualElement e, float a, float b) {
            e.style.left = Length.Percent(a * 100f);
            e.style.width = Length.Percent(Mathf.Max(0f, b - a) * 100f);
            e.style.display = b - a > 0f ? DisplayStyle.Flex : DisplayStyle.None;
        }

        static void Edge(VisualElement e, float at, bool show) {
            e.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;
            e.style.left = Length.Percent(at * 100f);
            float w = e.resolvedStyle.width;
            e.style.marginLeft = float.IsNaN(w) ? 0f : -w * 0.5f;
        }

        void Refresh() {
            float a = T(_lo), b = T(_hi);
            if (Single) {
                float from = _bipolar ? Mathf.Min(_center01, a) : 0f, to = _bipolar ? Mathf.Max(_center01, a) : a;
                Span(_restL, 0f, from); Span(_fill, from, to); Span(_restR, to, 1f);
            }
            else { Span(_restL, 0f, a); Span(_fill, a, b); Span(_restR, b, 1f); }
            Edge(_edgeMin, a, !Single);
            Edge(_edgeMax, b, !Single);
            Edge(_center, _center01, _bipolar);
            string vals = Single ? F(_lo) : F(_lo) + "-" + F(_hi);
            string text = _mode == LabelMode.None ? "" : _mode == LabelMode.LabelOnly ? _text
                        : _mode == LabelMode.ValuesOnly ? vals : string.IsNullOrEmpty(_text) ? vals : _text + " " + vals;
            TrackLabel.Fit(_label, text, _mode == LabelMode.LabelAndValues ? vals : null, _track.resolvedStyle.width);
        }

        void Commit(float lo, float hi) {
            lo = Mathf.Clamp(lo, _absMin, _absMax); hi = Mathf.Clamp(hi, lo, _absMax);
            if (Mathf.Approximately(lo, _lo) && Mathf.Approximately(hi, _hi)) { Refresh(); return; }
            _lo = lo; _hi = hi; Refresh();
            _onChanged?.Invoke(_lo, _hi);
        }

        float Sample(float x) {
            float w = Mathf.Max(1f, _track.resolvedStyle.width);
            return Mathf.Round(Mathf.Lerp(_absMin, _absMax, Mathf.Clamp01(x / w)) * 100000f) / 100000f;
        }

        void OnDown(PointerDownEvent e) {
            if (e.button != 0) return;
            float w = Mathf.Max(1f, _track.resolvedStyle.width);
            float mx = e.localPosition.x, xMin = T(_lo) * w, xMax = T(_hi) * w;
            e.StopPropagation();
            if (e.clickCount == 2) {
                float range = _absMax - _absMin;
                if (_bipolar && Mathf.Abs(mx - _center01 * w) <= EdgeGrab) {
                    float cv = Mathf.Lerp(_absMin, _absMax, _center01);
                    if (Single && F(_lo) == F(cv)) Commit(cv - range * OpenFraction * 0.5f, cv + range * OpenFraction * 0.5f);
                    else Commit(cv, cv);
                    return;
                }
                if (Single) {
                    float half = range * OpenFraction * 0.5f;
                    float nMin = Mathf.Max(_absMin, _lo - half), nMax = Mathf.Min(_absMax, _lo + half);
                    if (nMin == _absMin) nMax = Mathf.Min(_absMax, _absMin + range * OpenFraction);
                    else if (nMax == _absMax) nMin = Mathf.Max(_absMin, _absMax - range * OpenFraction);
                    Commit(nMin, nMax);
                }
                else if (mx <= (xMin + xMax) * 0.5f) Commit(_lo, _lo);
                else Commit(_hi, _hi);
                return;
            }
            if (Single) { float v = Sample(mx); Commit(v, v); Begin(e, Drag.Fill, mx); return; }
            bool nearMin = Mathf.Abs(mx - xMin) <= EdgeGrab, nearMax = Mathf.Abs(mx - xMax) <= EdgeGrab;
            if (!nearMin && !nearMax && mx > xMin && mx < xMax) { Begin(e, Drag.Fill, mx); return; }
            bool grabMin = nearMin;
            if (nearMin && nearMax) grabMin = mx <= (xMin + xMax) * 0.5f;
            else if (!nearMin && !nearMax) grabMin = Mathf.Abs(mx - xMin) <= Mathf.Abs(mx - xMax);
            Begin(e, grabMin ? Drag.Min : Drag.Max, mx);
            DragEdge(mx);
        }

        void Begin(PointerDownEvent e, Drag d, float mx) {
            _drag = d; _anchorLo = _lo; _anchorHi = _hi; _anchorX = mx;
            _track.CapturePointer(e.pointerId);
            _edgeMin.EnableInClassList("znd-range__edge--active", d == Drag.Min);
            _edgeMax.EnableInClassList("znd-range__edge--active", d == Drag.Max);
        }

        void DragEdge(float mx) {
            float v = Sample(mx);
            if (_drag == Drag.Min) Commit(Mathf.Clamp(v, _absMin, _hi), _hi);
            else Commit(_lo, Mathf.Clamp(v, _lo, _absMax));
        }

        void OnMove(PointerMoveEvent e) {
            if (_drag == Drag.None || !_track.HasPointerCapture(e.pointerId)) return;
            float mx = e.localPosition.x;
            if (_drag == Drag.Fill) {
                float w = Mathf.Max(1f, _track.resolvedStyle.width);
                float d = (mx - _anchorX) / w * (_absMax - _absMin), span = _anchorHi - _anchorLo;
                float nMin = Mathf.Clamp(_anchorLo + d, _absMin, _absMax - span);
                Commit(nMin, nMin + span);
            }
            else DragEdge(mx);
        }

        void OnUp(PointerUpEvent e) {
            if (_drag == Drag.None) return;
            _drag = Drag.None;
            _edgeMin.RemoveFromClassList("znd-range__edge--active");
            _edgeMax.RemoveFromClassList("znd-range__edge--active");
            if (_track.HasPointerCapture(e.pointerId)) _track.ReleasePointer(e.pointerId);
        }
    }
}
