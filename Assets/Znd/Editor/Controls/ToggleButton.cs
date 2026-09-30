using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Znd.Mock {

    /// <summary>
    /// A button that stays visibly pressed while on (class <c>znd-on</c>). An empty one (no label) shows a tick when on,
    /// so on and off never differ only by a tint; turn <see cref="markWhenOn"/> off when its face is an icon.
    /// <see cref="onColor"/> replaces the look's on state with a flat fill (Mute and Solo use it).
    /// </summary>
    public class ToggleButton : Button {

        bool _value, _mark;
        readonly Action<bool> _onChanged;
        Color? _onColor;

        public Color? onColor { get => _onColor; set { _onColor = value; ApplyOnColor(); } }

        public bool markWhenOn {
            get => _mark;
            set { _mark = value; EnableInClassList("znd-toggle--mark", value); if (!value && text == "✔") text = ""; this.value = _value; }
        }

        public bool value {
            get => _value;
            set {
                _value = value;
                EnableInClassList("znd-on", _value);
                if (_mark) text = _value ? "✔" : "";
                ApplyOnColor();
            }
        }

        public void SetValueWithoutNotify(bool v) => value = v;

        public ToggleButton(string label, string tooltip, bool value, Action<bool> onChanged) {
            this.tooltip = tooltip;
            text = label;
            _onChanged = onChanged;
            AddToClassList("znd-toggle");
            _mark = string.IsNullOrEmpty(label);
            if (_mark) AddToClassList("znd-toggle--mark");
            this.value = value;
            clicked += () => { this.value = !_value; _onChanged?.Invoke(_value); };
        }

        void ApplyOnColor() {
            if (!_onColor.HasValue) return;
            if (_value) {
                style.backgroundImage = StyleKeyword.None;
                style.backgroundColor = _onColor.Value;
                style.borderTopWidth = style.borderRightWidth = style.borderBottomWidth = style.borderLeftWidth = 0f;
            }
            else {
                style.backgroundImage = StyleKeyword.Null;
                style.backgroundColor = StyleKeyword.Null;
                style.borderTopWidth = style.borderRightWidth = style.borderBottomWidth = style.borderLeftWidth = StyleKeyword.Null;
            }
        }
    }
}
