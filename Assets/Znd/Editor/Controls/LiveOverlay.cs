using UnityEngine;
using UnityEngine.UIElements;

namespace Znd.Mock {

    /// <summary>
    /// Laid over a slider track to show where a value IS while the sound plays, as opposed to where it was SET:
    /// a thin dark-and-bright marker at the set value, a band from the marker to the live value (blue above / dark below
    /// when a modifier moves it, amber when game code drives it), a tick where a code-driven value is heading, and a brief
    /// amber pulse around the track when a new value arrives. Never takes a click; every colour is a USS class
    /// (<c>.znd-live__*</c>).
    /// </summary>
    public class LiveOverlay : VisualElement {

        public const float PulseSeconds = 0.5f;
        readonly VisualElement _band, _markDark, _markBright, _tick, _pulse;
        float _set = -1f, _live = -1f, _target = -1f, _pulseAt = -100f;
        bool _driven;

        public LiveOverlay() {
            pickingMode = PickingMode.Ignore;
            AddToClassList("znd-live");
            style.position = Position.Absolute;
            style.left = 1f; style.right = 1f; style.top = 1f; style.bottom = 1f;
            _band = Part("znd-live__band");
            _markDark = Part("znd-live__mark-dark");
            _markBright = Part("znd-live__mark-bright");
            _tick = Part("znd-live__tick");
            _pulse = Part("znd-live__pulse");
            _pulse.style.left = 0; _pulse.style.right = 0;
            RegisterCallback<GeometryChangedEvent>(_ => Relayout());
            schedule.Execute(Relayout).Every(33);
        }

        VisualElement Part(string cls) {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.AddToClassList(cls);
            e.style.position = Position.Absolute; e.style.top = 0; e.style.bottom = 0;
            e.style.display = DisplayStyle.None;
            Add(e);
            return e;
        }

        public void SetSet(float t) => _set = t;
        public void SetLive(float t, bool driven) { _live = t; _driven = driven; }
        public void ClearLive() { _live = -1f; _target = -1f; }
        public void SetTarget(float t) => _target = t;
        public void Pulse() => _pulseAt = (float)UnityEditor.EditorApplication.timeSinceStartup;

        void Relayout() {
            float w = resolvedStyle.width;
            if (float.IsNaN(w) || w <= 0f) return;
            if (_set >= 0f) {
                float xm = Mathf.Clamp(w * _set, 1f, w - 1f);
                _markDark.style.display = DisplayStyle.Flex; _markDark.style.left = xm - 1f; _markDark.style.width = 1f;
                _markBright.style.display = DisplayStyle.Flex; _markBright.style.left = xm; _markBright.style.width = 1f;
            }
            else _markDark.style.display = _markBright.style.display = DisplayStyle.None;

            float a = _set >= 0f ? _set : _live;
            if (_live >= 0f && Mathf.Abs(_live - a) >= 0.001f) {
                float xa = w * a, xl = w * _live;
                _band.style.display = DisplayStyle.Flex;
                _band.style.left = Mathf.Min(xa, xl); _band.style.width = Mathf.Max(1f, Mathf.Abs(xl - xa));
                _band.EnableInClassList("znd-live__band--driven", _driven);
                _band.EnableInClassList("znd-live__band--up", !_driven && _live > a);
                _band.EnableInClassList("znd-live__band--down", !_driven && _live <= a);
            }
            else _band.style.display = DisplayStyle.None;

            if (_target >= 0f && _live >= 0f && Mathf.Abs(_target - _live) >= 0.002f) {
                _tick.style.display = DisplayStyle.Flex;
                _tick.style.left = Mathf.Clamp(w * _target, 1f, w - 2f) - 1f; _tick.style.width = 2f;
            }
            else _tick.style.display = DisplayStyle.None;

            float age = (float)UnityEditor.EditorApplication.timeSinceStartup - _pulseAt;
            if (age >= 0f && age < PulseSeconds) { _pulse.style.display = DisplayStyle.Flex; _pulse.style.opacity = 1f - age / PulseSeconds; }
            else _pulse.style.display = DisplayStyle.None;
        }
    }
}
