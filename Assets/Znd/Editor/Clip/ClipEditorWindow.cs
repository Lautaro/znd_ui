using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Znd.Mock {

    /// <summary>
    /// The Clip editor: one sound's editor window. Top to bottom: the header row (Mute/Solo, name, Volume, Pitch, Chance,
    /// tags), then a framed box holding the Source, the waveform block, the action row (Render / Remove / Convert, Play),
    /// the Looper and speed rows, and the effect chain with its modifiers, code test and analyser.
    ///
    /// UI mock only. Play animates a playhead and everything that moves while a sound plays; nothing makes sound.
    /// Right-click Play for the audition card (Play on change, Burst, Loop).
    /// </summary>
    public class ClipEditorWindow : EditorWindow {

        const float Row = 10f;
        ClipData clip;
        Button playButton;

        [MenuItem("Window/Znd/Clip Editor")]
        public static ClipEditorWindow Open() {
            var w = GetWindow<ClipEditorWindow>();
            w.minSize = new Vector2(480f, 400f);
            w.Show();
            return w;
        }

        [MenuItem("Window/Znd/Reset Mock Data")]
        static void ResetData() {
            MockLibrary.Reset();
            MockPlayer.Stop();
            foreach (var w in Resources.FindObjectsOfTypeAll<ClipEditorWindow>()) w.Build();
        }

        void OnEnable() { MockPlayer.Changed += SyncPlay; Build(); }
        void OnDisable() { MockPlayer.Changed -= SyncPlay; }

        void Build() {
            clip = MockLibrary.Current;
            titleContent = new GUIContent("Clip: " + clip.name);
            var root = rootVisualElement;
            root.Clear();
            Ui.Attach(root);
            root.AddToClassList("znd-clipwindow");

            root.Add(Ui.Space(Row));
            var header = new HeaderRow(clip) { onRenamed = () => titleContent = new GUIContent("Clip: " + clip.name) };
            root.Add(header);
            root.Add(Ui.Space(Row));

            var box = new VisualElement();
            box.AddToClassList("znd-box-default");
            box.style.flexGrow = 1;
            root.Add(box);
            box.Add(Ui.Space(Row));
            box.Add(SourceRow());

            var scroll = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            scroll.style.flexGrow = 1;
            box.Add(scroll);
            scroll.Add(Ui.Space(Row));
            scroll.Add(new WaveformView(clip));
            scroll.Add(Ui.Space(Row));
            scroll.Add(ActionRow());
            scroll.Add(Ui.Space(Row * 3f));
            scroll.Add(new LooperRow(clip));
            scroll.Add(Ui.Space(4f));
            scroll.Add(new SpeedRow(clip));
            scroll.Add(Ui.Space(Row));
            scroll.Add(new ChainView(clip));
            scroll.Add(Ui.Space(Row * 2f));
        }

        /// <summary>"Source:" and the source audio's name, looking like an object field (the target circle picks another).</summary>
        VisualElement SourceRow() {
            var r = Ui.Row(Ui.LineH);
            r.AddToClassList("znd-sourcerow");
            var label = Ui.Text("Source:", "The audio this clip plays (mock).", "znd-sourcelabel");
            r.Add(label);
            var field = new VisualElement();
            field.AddToClassList("znd-sourcefield");
            var icon = new Image { image = EditorGUIUtility.IconContent("AudioClip Icon").image, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            icon.AddToClassList("znd-sourcefield__icon");
            var name = new Label(clip.sourceName) { pickingMode = PickingMode.Ignore };
            name.AddToClassList("znd-sourcefield__name");
            var pick = new Button(() => Ui.Menu(("mock_source_01", () => { clip.sourceName = "mock_source_01"; name.text = clip.sourceName; }, clip.sourceName == "mock_source_01", true),
                                                ("mock_source_02", () => { clip.sourceName = "mock_source_02"; name.text = clip.sourceName; }, clip.sourceName == "mock_source_02", true),
                                                ("mock_source_03", () => { clip.sourceName = "mock_source_03"; name.text = clip.sourceName; }, clip.sourceName == "mock_source_03", true))) { tooltip = "Pick another source (mock list)." };
            pick.AddToClassList("znd-sourcefield__pick");
            var target = new Image { image = EditorGUIUtility.IconContent("d_pick").image, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            pick.Add(target);
            field.Add(icon); field.Add(name); field.Add(pick);
            r.Add(field);
            return r;
        }

        VisualElement ActionRow() {
            const float h = 20f;
            var r = Ui.Row(h);
            Button render = null;
            render = Ui.Button("Render", "Bounce this sound to a file (mock: nothing is written).", "RichButton", () => {
                render.text = "Rendering…";
                render.schedule.Execute(() => render.text = "Render").StartingIn(900);
            }, Corners.All, 60f, h);
            r.Add(render);
            r.Add(Ui.Gap(4f));
            r.Add(Ui.Button("Remove", "Remove this sound from the project (mock).", "RichButton",
                () => EditorUtility.DisplayDialog("Remove " + clip.name + "?", "This is a UI mock: nothing is removed.", "OK"), Corners.All, 70f, h));
            r.Add(Ui.Gap(4f));
            r.Add(Ui.Button("Convert to Seq", "Turn this sound into a sequence containing it (mock).", "RichButton",
                () => EditorUtility.DisplayDialog("Convert to sequence", "This is a UI mock: nothing is converted.", "OK"), Corners.All, 100f, h));
            r.Add(Ui.Flex());
            r.Add(Ui.Gap(4f));
            r.Add(Ui.Button("Stress test", "A developer check button (mock: does nothing).", "RichButton", () => { }, Corners.All, 72f, h));
            r.Add(Ui.Gap(8f));
            playButton = Ui.Button("Play", "", "RichButton", () => { if (MockPlayer.Playing || MockPlayer.LoopRunning || MockPlayer.BurstRunning) MockPlayer.Stop(); else MockPlayer.Play(clip); }, Corners.All, 60f, h);
            playButton.RegisterCallback<PointerDownEvent>(e => { if (e.button != 1) return; e.StopPropagation(); AuditionCard(); }, TrickleDown.TrickleDown);
            r.Add(playButton);
            SyncPlay();
            return r;
        }

        void SyncPlay() {
            if (playButton == null) return;
            bool live = MockPlayer.Playing || MockPlayer.LoopRunning || MockPlayer.BurstRunning;
            playButton.text = (live ? "Stop" : "Play") + (MockPlayer.playOnChange ? " •" : "");
            playButton.tooltip = (live ? "Stop everything this window is playing." : "Play this sound (mock: animation only).")
                               + (MockPlayer.playOnChange ? "\n\n• Play on change is on." : "") + "\n\nRight-click: Play on change, Burst, Loop.";
        }

        /// <summary>The audition card, above Play: Play on change, Burst, Loop; then Plays, Gap and how the gap is counted.</summary>
        void AuditionCard() {
            Popover.Show(playButton, card => {
                var r1 = Ui.Row();
                ToggleButton burst = null, loop = null;
                r1.Add(Ui.Toggle("Play on change", "Play again every time you change something.", MockPlayer.playOnChange, v => { MockPlayer.playOnChange = v; SyncPlay(); }, "RichToggle", Corners.All, 110f, Ui.RowH));
                r1.Add(Ui.Gap(8f));
                burst = Ui.Toggle("Burst", "Play the sound several times with a gap.", MockPlayer.BurstRunning, v => { if (v) MockPlayer.StartBurst(clip); else MockPlayer.Stop(); }, "RichToggle", Corners.Left, 84f, Ui.RowH);
                loop = Ui.Toggle("Loop", "Play over and over until pressed again.", MockPlayer.LoopRunning, v => { if (v) MockPlayer.StartLoop(clip); else MockPlayer.Stop(); }, "RichToggle", Corners.Right, 56f, Ui.RowH);
                r1.Add(burst); r1.Add(loop);
                card.Add(r1);
                card.Add(Ui.Space(4f));
                var r2 = Ui.Row();
                r2.Add(Ui.Slider("Plays", MockPlayer.burstCount, 1f, 16f, "How many times Burst plays.", v => MockPlayer.burstCount = Mathf.RoundToInt(v), TrackSlider.LabelMode.LabelAndValue, 4f, "default", 96f, Ui.LineH, v => Mathf.RoundToInt(v).ToString()));
                r2.Add(Ui.Gap(6f));
                r2.Add(Ui.Slider("Gap", MockPlayer.burstGap, 0f, 2f, "Time between plays.", v => MockPlayer.burstGap = v, TrackSlider.LabelMode.LabelAndValue, 0.5f, "default", 120f, Ui.LineH, v => v.ToString("0.00") + " s"));
                r2.Add(Ui.Gap(6f));
                string[] modes = { "Steady", "From start", "From end" };
                var seg = new ToggleButton[3];
                for (int i = 0; i < 3; i++) {
                    int idx = i;
                    seg[i] = Ui.Toggle(modes[i], "How the gap between plays is counted.", MockPlayer.gapMode == i, _ => { MockPlayer.gapMode = idx; for (int k = 0; k < 3; k++) seg[k].value = k == idx; },
                                       "RichToggle", i == 0 ? Corners.Left : i == 2 ? Corners.Right : Corners.None, i == 0 ? 60f : 76f, Ui.RowH);
                    r2.Add(seg[i]);
                }
                card.Add(r2);
                card.schedule.Execute(() => {
                    burst.value = MockPlayer.BurstRunning; loop.value = MockPlayer.LoopRunning;
                    burst.text = MockPlayer.BurstRunning ? "Burst " + MockPlayer.BurstDone + "/" + MockPlayer.burstCount : "Burst";
                }).Every(100);
            });
        }
    }
}
