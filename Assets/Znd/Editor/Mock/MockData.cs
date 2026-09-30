using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Znd.Mock {

    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────
    // The mock data the Znd windows edit. Everything here is hardcoded placeholder content that exists only so the UI has
    // something realistic to show and change. Nothing reads it to make sound; nothing is saved.
    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    public enum ParamKind { Slider, LogSlider, Integer, Toggle, Choice, Icons, Range }
    public enum IconShape { Sine, Triangle, Saw, Square, Random }

    /// <summary>One setting of an effect or a modifier.</summary>
    public class Param {
        public string name, unit = "", tip = "";
        public ParamKind kind = ParamKind.Slider;
        public float value, value2, min, max = 1f, def;
        public string[] options;
        public IconShape[] icons;
        public string warning;               // shown as a ⚠ after the control

        public Param Clone() => (Param)MemberwiseClone();

        public float Normalized(float v) {
            if (kind == ParamKind.LogSlider) return Mathf.InverseLerp(Mathf.Log(min), Mathf.Log(max), Mathf.Log(Mathf.Max(v, 1e-4f)));
            return Mathf.InverseLerp(min, max, v);
        }

        public float FromNormalized(float t) {
            if (kind == ParamKind.LogSlider) return Mathf.Exp(Mathf.Lerp(Mathf.Log(min), Mathf.Log(max), t));
            return Mathf.Lerp(min, max, t);
        }

        public string Format(float v) {
            if (kind == ParamKind.Choice || kind == ParamKind.Icons) {
                int n = options?.Length ?? icons?.Length ?? 1;
                int i = Mathf.Clamp(Mathf.RoundToInt(v), 0, n - 1);
                return options != null ? options[i] : icons[i].ToString();
            }
            if (kind == ParamKind.Toggle) return v >= 0.5f ? "on" : "off";
            if (kind == ParamKind.Integer) return Mathf.RoundToInt(v).ToString();
            if (unit == "dB") return v.ToString("+0.0;-0.0;0.0") + " dB";
            string s = Mathf.Abs(v) >= 100f ? v.ToString("0") : Mathf.Abs(v) >= 10f ? v.ToString("0.0") : v.ToString("0.00");
            return string.IsNullOrEmpty(unit) ? s : s + " " + unit;
        }
    }

    public class Effect {
        public string name, summary;
        public bool enabled = true;
        public List<Param> ps = new List<Param>();
    }

    public enum ModifierType { Envelope, LFO, Random, Step }

    /// <summary>What a modifier drives: an effect's setting, or one of the sound's own values.</summary>
    public class Binding {
        public Effect effect;            // null: the sound's own value
        public int param;                // index in effect.ps
        public string ownValue;          // "Volume", "Pitch", "Speed", "Drive" when effect is null
        public int combine = 1;          // 0 Shift, 1 Set, 2 Scale, 3 Ratio
        public float depth = 1f;
        public bool Targets(Effect e, int k) => effect == e && (e == null ? false : param == k);
        public bool TargetsOwn(string v) => effect == null && ownValue == v;
    }

    public class Modifier {
        public ModifierType type;
        public string name;
        public bool enabled = true, folded, visible = true;
        public string codeId = "";       // non-empty: game code can reach it (the amber chip)
        public int codeMode;             // 0 Scale, 1 Set
        public float codeRest = -1f;     // below 0: as authored
        public float codeFollowMs = 30f;
        public List<Param> ps = new List<Param>();
        public Curve curve;
        public float[] steps;
        public readonly List<Binding> bindings = new List<Binding>();
        public bool HasCode => !string.IsNullOrEmpty(codeId);
        public string TypeName => type == ModifierType.LFO ? "LFO" : type.ToString();
    }

    public class ClipData {
        // header
        public string name = "Mock Clip 01", tags = "-Untagged-", sourceName = "mock_source_01";
        public bool mute, solo;
        public float volLo = 100f, volHi = 100f, pitchLo = 100f, pitchHi = 100f, chance = 100f;
        // waveform
        public float length = 2.4f;
        public bool trim = true, clamp, keepLength;
        public float trimStart = 0.12f, trimEnd = 2.1f;
        public bool volumeCurveOn = true, pitchCurveOn = true, timeCurveOn;
        public Curve volumeCurve, pitchCurve, timeCurve;
        // looper
        public bool looper;
        public float crossLo = 0.10f, crossHi = 0.25f;
        // speed
        public bool liveSpeed;
        public float speed = 1f, windowMs = 30f;
        public bool keepHits = true;
        public int algorithm;            // 0 Clean, 1 Grainy
        // chain
        public string presetName;        // null: local chain
        public bool canReconnect = true;
        public readonly List<Effect> effects = new List<Effect>();
        public readonly List<Modifier> modifiers = new List<Modifier>();
        public readonly List<string> snapshots = new List<string> { "Calm", "Angry" };

        public bool StretcherRuns => liveSpeed || timeCurveOn || keepLength;

        /// <summary>How long one mock play lasts, in seconds (only drives the playhead animation).</summary>
        public float PlaySeconds {
            get {
                float s = trim ? Mathf.Max(0.05f, trimEnd - trimStart) : length;
                if (liveSpeed) s /= Mathf.Max(0.1f, speed);
                return s;
            }
        }

        public IEnumerable<Binding> BindingsOn(Effect e, int k) => modifiers.SelectMany(m => m.bindings.Where(b => b.Targets(e, k)));
        public bool IsBound(Effect e, int k) => BindingsOn(e, k).Any();
        public IEnumerable<Modifier> ModifiersOn(Effect e, int k) => modifiers.Where(m => m.bindings.Any(b => b.Targets(e, k)));
        public IEnumerable<Modifier> ModifiersOnOwn(string v) => modifiers.Where(m => m.bindings.Any(b => b.TargetsOwn(v)));

        public string TargetLabel(Binding b) {
            if (b.effect == null) return "Sound " + b.ownValue.ToLower();
            int i = effects.IndexOf(b.effect);
            return b.effect.name + " " + (i + 1) + " " + b.effect.ps[b.param].name;
        }
    }

    /// <summary>The hardcoded example content, and the menus of things that can be added.</summary>
    public static class MockLibrary {

        public static readonly string[] EffectTypes = { "Gain", "Low pass", "High pass", "EQ", "Distortion", "Delay", "Reverb", "Chorus", "Compressor", "Bit crush" };
        public static readonly string[] Presets = { "Punchy hit", "Radio voice", "Far away", "Underwater" };

        static ClipData s_current;
        public static ClipData Current => s_current ??= CreateExample();
        public static void Reset() => s_current = CreateExample();

        static Param Slider(string name, float v, float min, float max, string unit = "", string tip = "") =>
            new Param { name = name, value = v, def = v, min = min, max = max, unit = unit, tip = tip };
        static Param Log(string name, float v, float min, float max, string unit) =>
            new Param { name = name, kind = ParamKind.LogSlider, value = v, def = v, min = min, max = max, unit = unit };
        static Param Int(string name, float v, float min, float max) =>
            new Param { name = name, kind = ParamKind.Integer, value = v, def = v, min = min, max = max };
        static Param Toggle(string name, bool on) => new Param { name = name, kind = ParamKind.Toggle, value = on ? 1 : 0, max = 1 };
        static Param Choice(string name, int v, params string[] options) =>
            new Param { name = name, kind = ParamKind.Choice, value = v, max = options.Length - 1, options = options };
        static Param Icons(string name, int v, params IconShape[] icons) =>
            new Param { name = name, kind = ParamKind.Icons, value = v, max = icons.Length - 1, icons = icons };

        public static Effect MakeEffect(string type) {
            var e = new Effect { name = type, summary = "Placeholder " + type.ToLower() + " effect (mock)." };
            switch (type) {
                case "Gain": e.ps.Add(Slider("Gain", 1f, 0f, 4f, "x")); break;
                case "Low pass": e.ps.Add(Log("Cutoff", 20000f, 20f, 20000f, "Hz")); e.ps.Add(Slider("Resonance", 0.71f, 0.1f, 10f, "Q")); break;
                case "High pass": e.ps.Add(Log("Cutoff", 80f, 20f, 20000f, "Hz")); e.ps.Add(Slider("Resonance", 0.71f, 0.1f, 10f, "Q")); break;
                case "EQ":
                    foreach (var (n, f) in new[] { ("Sub", "60"), ("Low", "150"), ("Low-mid", "400"), ("Mid", "1k"), ("High-mid", "2.5k"), ("High", "6k"), ("Air", "12k") })
                        e.ps.Add(Slider(n + " " + f, 0f, -18f, 18f, "dB"));
                    e.ps.Add(Log("Low cut", 10f, 10f, 1000f, "Hz"));
                    e.ps.Add(Log("High cut", 22000f, 1000f, 22000f, "Hz"));
                    break;
                case "Distortion": e.ps.Add(Slider("Drive", 10f, 1f, 50f)); e.ps.Add(Slider("Tone", 0.5f, 0f, 1f)); e.ps.Add(Slider("Mix", 1f, 0f, 1f)); break;
                case "Delay":
                    e.ps.Add(Slider("Time", 250f, 1f, 1000f, "ms")); e.ps.Add(Slider("Feedback", 0.4f, 0f, 0.95f));
                    e.ps.Add(Slider("Mix", 0.3f, 0f, 1f)); e.ps.Add(Slider("Max time", 500f, 10f, 2000f, "ms")); e.ps.Add(Toggle("Ping-pong", false));
                    break;
                case "Reverb": e.ps.Add(Slider("Size", 0.6f, 0f, 1f)); e.ps.Add(Slider("Damping", 0.4f, 0f, 1f)); e.ps.Add(Slider("Mix", 0.25f, 0f, 1f)); break;
                case "Chorus": e.ps.Add(Slider("Rate", 0.8f, 0.05f, 5f, "Hz")); e.ps.Add(Slider("Depth", 0.3f, 0f, 1f)); e.ps.Add(Slider("Mix", 0.5f, 0f, 1f)); break;
                case "Compressor": e.ps.Add(Slider("Threshold", -12f, -60f, 0f, "dB")); e.ps.Add(Slider("Ratio", 4f, 1f, 20f)); e.ps.Add(Choice("Knee", 0, "Hard", "Soft")); break;
                case "Bit crush": e.ps.Add(Int("Bits", 8f, 1f, 16f)); e.ps.Add(Slider("Downsample", 1f, 1f, 32f, "x")); e.ps.Add(Slider("Mix", 1f, 0f, 1f)); break;
            }
            return e;
        }

        public static Modifier MakeModifier(ModifierType type) {
            var m = new Modifier { type = type, name = type == ModifierType.LFO ? "LFO" : type.ToString() };
            switch (type) {
                case ModifierType.Envelope:
                    m.ps.Add(Slider("Extra time", 0f, 0f, 2f, "s"));
                    m.ps.Add(Choice("Measure", 0, "Waveform", "Play time"));
                    m.curve = new Curve(0f, 1f, 0f, 1f, new Vector2(0f, 0.8f), new Vector2(0.85f, 0.82f), new Vector2(1f, 0.3f));
                    break;
                case ModifierType.LFO:
                    m.ps.Add(Slider("Amount", 1f, 0f, 1f));
                    m.ps.Add(Slider("Rate", 1f, 0.05f, 20f, "Hz"));
                    m.ps.Add(Icons("Shape", 0, IconShape.Sine, IconShape.Triangle, IconShape.Saw, IconShape.Square));
                    m.ps.Add(Choice("Run", 1, "Always", "Per play"));
                    m.ps.Add(Icons("Mode", 0, IconShape.Sine, IconShape.Random));
                    m.ps.Add(Slider("Offset", 0f, -1f, 1f));
                    m.curve = new Curve(0f, 1f, 0f, 1f, new Vector2(0f, 1f), new Vector2(1f, 1f));
                    break;
                case ModifierType.Random:
                    m.ps.Add(new Param { name = "Min", kind = ParamKind.Range, value = -0.3f, value2 = 0.3f, min = -1f, max = 1f });
                    m.ps.Add(Slider("Bias", 1f, 0f, 2f));
                    break;
                case ModifierType.Step:
                    m.ps.Add(Choice("Timing", 0, "Per play", "Per interval"));
                    m.ps.Add(Choice("Order", 0, "Sequential", "Round robin"));
                    m.ps.Add(Toggle("Start random", false));
                    m.ps.Add(Toggle("Retrigger", false));
                    m.steps = new[] { 0.6f, -0.4f, 0.2f, -0.8f };
                    break;
            }
            return m;
        }

        /// <summary>The example sound: one plausible chain that between its rows shows every kind of control the tool has.</summary>
        static ClipData CreateExample() {
            var c = new ClipData();
            c.volumeCurve = new Curve(0f, 1f, 0f, 1f, new Vector2(0f, 0.55f), new Vector2(0.12f, 0.9f), new Vector2(0.7f, 0.75f), new Vector2(1f, 0.15f));
            c.pitchCurve = new Curve(0f, 1f, 0f, 1f, new Vector2(0f, 0.5f), new Vector2(0.45f, 0.62f), new Vector2(1f, 0.4f));
            c.pitchCurve.points[1].randomX = 0.05f; c.pitchCurve.points[1].randomY = 0.12f;
            c.timeCurve = new Curve(0f, 1f, 0f, 1f, new Vector2(0f, 0.5f), new Vector2(0.4f, 0.35f), new Vector2(1f, 0.7f));

            var gain = MakeEffect("Gain");
            var lowpass = MakeEffect("Low pass"); lowpass.ps[0].value = 6200f;
            var eq = MakeEffect("EQ"); eq.ps[1].value = 3.5f; eq.ps[3].value = -2f; eq.ps[5].value = 1.5f;
            var delay = MakeEffect("Delay"); delay.enabled = false;
            var crush = MakeEffect("Bit crush"); crush.ps[1].value = 4f; crush.ps[1].warning = "Downsample above 16 x can alias on short sounds";
            c.effects.AddRange(new[] { gain, lowpass, eq, delay, crush });

            var env = MakeModifier(ModifierType.Envelope); env.name = "Volume";
            env.bindings.Add(new Binding { effect = gain, param = 0, combine = 1, depth = 1f });
            env.bindings.Add(new Binding { ownValue = "Volume", combine = 2, depth = 0.8f });

            var lfo = MakeModifier(ModifierType.LFO); lfo.name = "Wobble"; lfo.codeId = "wobble";
            lfo.ps[1].value = 0.4f; lfo.ps[1].warning = "Slow for this sound: less than one cycle per play";
            lfo.curve = new Curve(0f, 1f, 0f, 1f, new Vector2(0f, 0.2f), new Vector2(0.3f, 1f), new Vector2(1f, 1f));
            lfo.bindings.Add(new Binding { effect = lowpass, param = 0, combine = 0, depth = 0.6f });

            var rnd = MakeModifier(ModifierType.Random); rnd.name = "Random";
            rnd.bindings.Add(new Binding { effect = eq, param = 3, combine = 0, depth = 1f });

            var step = MakeModifier(ModifierType.Step); step.name = "Step";
            step.bindings.Add(new Binding { ownValue = "Pitch", combine = 0, depth = 0.25f });

            c.modifiers.AddRange(new[] { env, lfo, rnd, step });
            return c;
        }
    }
}
