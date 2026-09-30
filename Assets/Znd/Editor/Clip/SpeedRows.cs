using UnityEngine;
using UnityEngine.UIElements;

namespace Znd.Mock {

    /// <summary>The Looper row: the Looper toggle and, while on, the crossmix length as a two-handled range.</summary>
    public class LooperRow : VisualElement {
        readonly ClipData clip;

        public LooperRow(ClipData clip) { this.clip = clip; style.flexShrink = 0; Build(); }

        void Build() {
            Clear();
            var r = Ui.Row();
            r.Add(Ui.Toggle("Looper", clip.looper ? "Stop looping: play once and end." : "Loop the trimmed part until stopped.", clip.looper,
                v => { clip.looper = v; Build(); MockPlayer.Edited(clip); }, "RichToggle", Corners.All, 64f, Ui.RowH));
            if (clip.looper) {
                r.Add(Ui.Gap(8f));
                float limit = Mathf.Max(0.05f, (clip.trim ? clip.trimEnd - clip.trimStart : clip.length) * 0.5f);
                r.Add(Ui.Range("Crossmix s", clip.crossLo, clip.crossHi, 0f, limit,
                    "How long the old pass fades out while the new one fades in. Handles together: one length. Apart: each loop picks its own.",
                    (a, b) => { clip.crossLo = a; clip.crossHi = b; }, "default", RangeSlider.LabelMode.LabelAndValues, false, 240f, Ui.LineH));
            }
            r.Add(Ui.Flex());
            Add(r);
        }
    }

    /// <summary>
    /// The speed row: Live speed (with its Speed slider) and, whenever the stretcher would run, its window, Keep hits and
    /// the two stretch styles; at the right, how long one play lasts.
    /// </summary>
    public class SpeedRow : VisualElement {
        readonly ClipData clip;
        Label length;
        bool builtRuns;

        public SpeedRow(ClipData clip) {
            this.clip = clip; style.flexShrink = 0; Build();
            schedule.Execute(() => {
                if (clip.StretcherRuns != builtRuns) Build();
                if (length != null) length.text = clip.looper ? "Loops" : "Plays " + clip.PlaySeconds.ToString("0.00") + " s";
            }).Every(250);
        }

        void Build() {
            Clear();
            builtRuns = clip.StretcherRuns;
            var r = Ui.Row();
            r.Add(Ui.Toggle("Live speed", "Give this sound its own speed, changeable while it plays, without changing its pitch.", clip.liveSpeed,
                v => { clip.liveSpeed = v; Build(); MockPlayer.Edited(clip); }, "RichToggle", Corners.All, 84f, Ui.RowH));
            if (clip.liveSpeed) {
                r.Add(Ui.Gap(6f));
                float lmin = Mathf.Log(0.25f), lmax = Mathf.Log(4f);
                TrackSlider s = null;
                s = Ui.Slider("Speed ×" + clip.speed.ToString("0.00"), Mathf.InverseLerp(lmin, lmax, Mathf.Log(clip.speed)), 0f, 1f,
                    "How fast the sound moves through its source. Double-click resets to 1.",
                    t => { clip.speed = Mathf.Exp(Mathf.Lerp(lmin, lmax, t)); s.text = "Speed ×" + clip.speed.ToString("0.00"); },
                    TrackSlider.LabelMode.LabelOnly, Mathf.InverseLerp(lmin, lmax, 0f), "default", 150f, Ui.LineH);
                r.Add(s);
            }
            if (builtRuns) {
                r.Add(Ui.Gap(6f));
                TrackSlider ws = null;
                ws = Ui.Slider("Window " + clip.windowMs.ToString("0") + " ms", clip.windowMs, 10f, 100f, "Length of the pieces the sound is cut into to stretch it.",
                    w => { clip.windowMs = Mathf.Round(w); ws.text = "Window " + clip.windowMs.ToString("0") + " ms"; },
                    TrackSlider.LabelMode.LabelOnly, 30f, "default", 120f, Ui.LineH);
                r.Add(ws);
                r.Add(Ui.Gap(6f));
                r.Add(Ui.Toggle("Keep hits", "Keep attacks sharp: play each hit once at normal speed.", clip.keepHits, v => clip.keepHits = v, "RichToggle", Corners.All, 72f, Ui.RowH));
                r.Add(Ui.Gap(6f));
                ToggleButton a = null, b = null;
                a = Ui.Toggle("Clean", "Stretch style: clean.", clip.algorithm == 0, _ => { clip.algorithm = 0; a.value = true; b.value = false; }, "RichToggle", Corners.Left, 60f, Ui.RowH);
                b = Ui.Toggle("Grainy", "Stretch style: grainy.", clip.algorithm == 1, _ => { clip.algorithm = 1; b.value = true; a.value = false; }, "RichToggle", Corners.Right, 70f, Ui.RowH);
                r.Add(a); r.Add(b);
            }
            r.Add(Ui.Flex());
            length = Ui.Text("", "How long one play lasts.", "znd-minilabel");
            length.style.width = 110f; length.style.unityTextAlign = TextAnchor.MiddleRight;
            length.text = clip.looper ? "Loops" : "Plays " + clip.PlaySeconds.ToString("0.00") + " s";
            r.Add(length);
            Add(r);
        }
    }
}
