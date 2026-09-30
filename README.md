# Znd UI Mock

A UI-only copy of the **Znd** sound tool's editor windows, made so the interface can be redesigned. It looks and behaves like the real tool: the same layout, the same controls, and the same interactions (dragging, expanding, right-click menus, curve editing and playback animation). There is **no audio engine behind it**. Every value is hardcoded mock data held in memory, and "Play" only animates the screen. Nothing makes sound and nothing is saved.

## Open it

1. Open this folder with **Unity 6000.3.10f1** (Unity Hub > Add > this folder). Any Unity 6 should work.
2. In Unity's menu bar choose **Window > Znd > Clip Editor**.
3. Interact directly in that window. There is no need to press Unity's own Play button: the tool is an editor window, exactly like the real one.

**Window > Znd > Reset Mock Data** puts the example back the way it started.

## Where the look lives (what to edit)

Everything visual is in two style sheets (USS, Unity's CSS). The C# only builds the structure.

- `Assets/Znd/Editor/Skin/ZndSkin.uss` holds the named looks. It has buttons and toggles (`.znd-RichButton`, `.znd-RichToggle`, `.znd-Default`, `.znd-Flat`, `.znd-BigButton`, `.znd-BigButtonFlat`, `.znd-FlatToggle`, `.znd-MainTab`), each with `:hover`, `:active`, and `.znd-on` for a latched toggle. It also has sliders (`.znd-slider-default`, `-smallslider`, `-bigslider`, `-minmax`, `-minmaxpitch`, `-chance`, each styling `.znd-slider__fill`, `.znd-slider__rest`, `.znd-slider__label` and the range edges `.znd-range__edge--min/--max/--center`), plus text styles and the framed boxes (`.znd-box-default` etc.). The small PNGs next to it are the images those looks use.
- `Assets/Znd/Editor/ZndLayout.uss` holds everything else: labels, the Source field, the waveform box and trim handles, chain rows, the amber "driven by code" chip (`.znd-codechip`), the sound's own-value chips (`.znd-ownvalue`), snapshot chips, curve grounds, step bars, the live-value overlay on moving sliders (`.znd-live__*`), and the popover card.
- Corner shapes are classes too: `.znd-corners--all/left/right/top/bottom/square`. Joined strips of buttons use left / none / right so they read as one piece.
- A few drawn things take their colours from C#, because they are painted rather than styled: the waveform picture, the curve lines, the playhead, and the analyser's bars and lanes. They are listed at the top of `Clip/WaveformView.cs` and in `Clip/ChainExtras.cs`.

## What is on the Clip Editor screen, top to bottom

- **Header row.** M / S (mute, solo), the name, **Volume** and **Pitch** (drag for one value; double-click to open it into a min/max range, then drag either handle or the middle), **Chance**, and the tags (click for a menu).
- **Source.** A field showing the source audio. The circle opens a mock picker.
- **Waveform toolbar.**
  - Trim | Clamp.
  - The Volume, Pitch and Time curves. Each has a pencil that makes it the one curve you edit on the waveform, and an eye that shows or hides it.
  - Keep length, and the length readout.
- **Waveform.**
  - Amber picture with darkened trimmed ends. Drag the white handles to trim; right-drag one to move both. The mouse wheel zooms.
  - With a curve's pencil on, drag its points, click the line to add a point, double-click a point to delete it, Shift-drag a segment, Shift + right-drag to bend it, and drag in empty space to box-select. Right-click a point for its "random point" settings (drawn as an ellipse).
  - Faint lines show the combined result. Pitch and time curves get axis labels.
- **Action row.** Render, Remove, Convert to Seq, a developer button, and **Play**. Right-click Play for the audition card: Play on change, Burst, Loop, Plays, Gap, and how the gap is counted.
- **Looper row.** When on, a crossmix range appears (and blue spans show on the waveform).
- **Speed row.** Live speed and its Speed slider; while stretching applies, Window, Keep hits and Clean | Grainy. At the right, how long one play lasts.
- **Chain header.** "Local chain" with Library… / Save as… / Reconnect (or Detach), and the Snapshots strip (Capture, Glide, chips: click to glide, right-click for more).
- **Effect rows.**
  - Each row has a grip (drag to reorder), On, the name, then its settings. If they fit on one row they are shown inline; otherwise a summary is shown, and clicking it expands wrapped rows (the EQ shows this).
  - Every setting is right-clickable ("Modulate with / New … / Remove modulation").
  - `~` marks a setting a modifier moves; the amber bolt marks one game code can move; ⚠ is a warning.
- **Modifiers.**
  - The sound's own values (Volume, Pitch, Speed, Drive) as chips.
  - Then one card per modifier: fold arrow, On, type, editable name, the **code chip** (grey when game code cannot reach it, amber with an id when it can; click for its settings popup), what it drives, an eye, and ×.
  - A card holds its settings. The example chain includes a slider, a min–max range, a text choice strip, a strip of wave icons, and toggles.
  - Envelopes and LFOs have a curve on a dark ground; Step has draggable bars with + / −.
  - Each binding row has Shift / Set / Scale / Ratio and Depth.
- **Code test.** Pretend to be game code: Drive, ×1/×3/×8, Play/Stop, and per id a value with Hold / Ramp / Jitter and Project-wide. With Drive on, the code-driven sliders and chips turn amber and flash as "new values arrive".
- **Analyse.**
  - Combined view: a lane per modulated setting, 72 frequency bars (green = louder, red = quieter), frequency labels, a time line, and a roster of the effects.
  - Spectrum / Over time / Waveform are live pictures, and a gain box.

## What Play animates (all fake)

While "playing", the following move:

- the playhead sweeps the trimmed waveform, and dotted "what this play hears" lines follow the curves;
- the Play button reads Stop;
- modulated sliders show a moving live band, and the own-value chips show live values;
- curve grounds get a playhead and a dot, and the current step lights up;
- code chips fill and flash (with Code test Drive on);
- the analyser's lanes, bars and live views move.

A pass lasts as long as the trimmed part of the example (about 2 s). A Looper, Loop or Burst repeats. Everything is driven by timers and canned wobbles.

## Scope of this mock

The example chain has five effects (Gain, Low pass, EQ, Delay, Bit crush) and four modifiers (Envelope, LFO, Random, Step). Between them they use every kind of control the real tool has. The real tool has more effect types, but they are built from the same controls, so this set covers every pattern. "Add effect…" and "Add modifier…" add more placeholder rows.

## Files

- `Assets/Znd/Editor/Clip/`: the Clip Editor window and its sections (header, waveform, speed rows, chain, analyser, code test, snapshots).
- `Assets/Znd/Editor/Controls/`: the reusable controls (toggle button, in-track slider, range, step bars, live overlay, curve editor, painted icons).
- `Assets/Znd/Editor/Mock/`: the hardcoded example data and the fake player.
- `Assets/Znd/Editor/Popups.cs`: the popover card and the small popup windows.
