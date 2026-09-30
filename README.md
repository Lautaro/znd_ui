# Audio Tool UI Mock

A deliberately tiny Unity project that mocks the interface of an audio sound-authoring tool, made so UI designers can redesign it. It contains **only UI**: there is no audio engine, no audio processing and no real effects. Nothing here needs code knowledge to explore.

## How to open it

1. Open the project folder with **Unity 6000.3.10f1** (Unity Hub > Add > select this folder). Other Unity 6 versions should work too.
2. In the Project window, open `Assets/MockUI/Scenes/MockMain.unity`.
3. Press the **Play** button at the top of the Unity editor.

## What you can do in Play mode

- **Play** moves the yellow playhead across the waveform over 4 seconds. When it reaches the end it stops there; pressing Play again starts over.
- **Stop** halts the playhead and snaps it back to the start.
- **Loop** (checkbox): when ticked, the playhead wraps back to the start at the end and keeps going until Stop is pressed.
- The time readout shows elapsed / total seconds.
- **Effects Chain** panel: each row has an on/off checkbox and an amount slider with a value readout. They respond to clicks and dragging but are not connected to anything.
- **+ Add Effect (placeholder)** is a button with no action, marking where adding effects would live.

## Where things are (Hierarchy window)

- `Canvas > Background` - the page background.
- `Canvas > Title` - the sound name at the top.
- `Canvas > Sound Panel` - left panel. Contains `Waveform Area` (with `Waveform Bars`, 96 plain bar images, and the `Playhead`), the `Transport Bar` (Play, Stop, Loop, time readout) and a short info line.
- `Canvas > Effects Panel` - right panel with `Effects List` (four `Effect Row` entries) and the `Add Effect Button`.

Everything is standard Unity UI (uGUI): Images, Texts, Buttons, Toggles, Sliders and layout groups. You can select any element and change its colour, sprite, size, font or position in the Inspector; the Canvas scales to any screen size from a 1920x1080 reference.

## Tweakable settings

Select `Canvas > Sound Panel` and look at the **Playhead Animator** component in the Inspector:

- **Duration** - seconds for one pass across the waveform (default 4).
- **Loop** - start state of looping (the Loop checkbox also changes it at runtime).
- **Playhead** - which object moves. It must stay a child of `Waveform Area`; the waveform can be any size.
- **Time Label** - the text that shows the time.
- **Optional Audio** - leave empty. If you drop an AudioSource with a clip here, it plays when Play is pressed, purely for feel.

The Play and Stop buttons are wired to this component through their **On Click** lists in the Inspector, and the Loop checkbox through its **On Value Changed** list, so if you replace a button, re-point that list to the Playhead Animator's `Play` / `Stop` / `SetLoop`.
