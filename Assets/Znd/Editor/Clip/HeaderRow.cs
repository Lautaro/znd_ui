using UnityEngine;
using UnityEngine.UIElements;

namespace Znd.Mock {

    /// <summary>
    /// The top row of a sound's editor: Mute / Solo, Name, Volume and Pitch (each a fixed value or a range drawn per play),
    /// Chance, and the tags. Equal columns: Mute/Solo take 44 px, the rest share the width, each control 4 px narrower
    /// than its column.
    /// </summary>
    public class HeaderRow : VisualElement {

        readonly ClipData clip;
        readonly ToggleButton mute, solo;
        readonly TextField nameField;
        readonly RangeSlider volume, pitch;
        readonly TrackSlider chance;
        readonly Button tags;
        public System.Action onRenamed;

        static readonly Color MuteOn = new Color32(107, 50, 48, 255), SoloOn = new Color(.14f, .34f, .14f, 1f);

        public HeaderRow(ClipData clip) {
            this.clip = clip;
            AddToClassList("znd-headerrow");
            style.height = Ui.LineH; style.flexShrink = 0;
            mute = Ui.Toggle("M", "Mute / unmute", clip.mute, v => { clip.mute = v; if (v) { clip.solo = false; solo.value = false; } }, "FlatToggle", Corners.Left, -1f, -1f, MuteOn);
            solo = Ui.Toggle("S", "Solo", clip.solo, v => { clip.solo = v; if (v) { clip.mute = false; mute.value = false; } }, "FlatToggle", Corners.Right, -1f, -1f, SoloOn);
            nameField = new TextField { isDelayed = true, value = clip.name };
            nameField.AddToClassList("znd-namefield");
            nameField.RegisterValueChangedCallback(e => { clip.name = e.newValue; onRenamed?.Invoke(); });
            volume = new RangeSlider("Volume", clip.volLo, clip.volHi, 0f, 100f, "Volume: drag for one value; double-click to open it into a range drawn per play.",
                (a, b) => { clip.volLo = a; clip.volHi = b; MockPlayer.Edited(clip); });
            volume.AddToClassList("znd-slider-minmax");
            pitch = new RangeSlider("Pitch", clip.pitchLo, clip.pitchHi, 0f, 200f, "Pitch: drag for one value; double-click to open it into a range drawn per play.",
                (a, b) => { clip.pitchLo = a; clip.pitchHi = b; MockPlayer.Edited(clip); }, RangeSlider.LabelMode.LabelAndValues, bipolar: true);
            pitch.AddToClassList("znd-slider-minmaxpitch");
            chance = new TrackSlider("Chance", clip.chance, 0f, 100f, "Chance that a play actually sounds.", v => clip.chance = v, TrackSlider.LabelMode.LabelAndValue);
            chance.AddToClassList("znd-slider-chance");
            tags = new Button(ShowTags) { text = clip.tags, tooltip = "Tags: click to choose." };
            tags.AddToClassList("znd-tagsfield"); tags.AddToClassList("znd-text-tags");
            foreach (var e in new VisualElement[] { mute, solo, nameField, volume, pitch, chance, tags }) {
                e.style.position = Position.Absolute; e.style.top = 0; e.style.bottom = 0; e.style.height = StyleKeyword.Auto;
                Add(e);
            }
            RegisterCallback<GeometryChangedEvent>(_ => Arrange());
        }

        void ShowTags() {
            string[] all = { "UI", "Weapons", "Footsteps", "Ambience", "Voice" };
            var items = new (string, System.Action, bool, bool)[all.Length + 2];
            items[0] = ("-Untagged-", () => { clip.tags = "-Untagged-"; tags.text = clip.tags; }, clip.tags == "-Untagged-", true);
            items[1] = (null, null, false, true);
            for (int i = 0; i < all.Length; i++) {
                string t = all[i];
                bool on = clip.tags.Contains(t);
                items[i + 2] = (t, () => {
                    var list = new System.Collections.Generic.List<string>(clip.tags == "-Untagged-" ? new string[0] : clip.tags.Split(new[] { ", " }, System.StringSplitOptions.RemoveEmptyEntries));
                    if (list.Contains(t)) list.Remove(t); else list.Add(t);
                    clip.tags = list.Count == 0 ? "-Untagged-" : string.Join(", ", list);
                    tags.text = clip.tags;
                }, on, true);
            }
            Ui.Menu(items);
        }

        static void Place(VisualElement e, float x, float w) { e.style.left = x; e.style.width = Mathf.Max(0f, w); }

        void Arrange() {
            float width = resolvedStyle.width;
            if (float.IsNaN(width) || width <= 0f) return;
            const float ms = 44f, gap = 1f;
            float col = (width - ms) / 5f, w = col - 4f, half = (ms - gap) * 0.5f;
            Place(mute, 0f, half); Place(solo, half + gap, ms - half - gap);
            float x = ms;
            Place(nameField, x, w); x += col;
            Place(volume, x, w); x += col;
            Place(pitch, x, w); x += col;
            Place(chance, x, w); x += col;
            Place(tags, x, w);
        }
    }
}
