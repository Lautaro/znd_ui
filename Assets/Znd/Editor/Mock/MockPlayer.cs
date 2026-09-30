using System;
using UnityEditor;
using UnityEngine;

namespace Znd.Mock {

    /// <summary>
    /// The fake transport. "Playing" is only a clock: it tells the UI how far through a play we are, so the playheads,
    /// the live overlays, the analyser and the dotted live curves can animate. No audio is produced at all.
    ///
    /// Behaviour (to design against):
    /// - Play runs one pass of <see cref="ClipData.PlaySeconds"/> and stops at the end;
    /// - a Looper clip, or the audition card's Loop, restarts the pass until Stop;
    /// - Burst plays <see cref="burstCount"/> passes with <see cref="burstGap"/> seconds between them;
    /// - Stop ends everything at once.
    /// </summary>
    [InitializeOnLoad]
    public static class MockPlayer {

        public static bool Playing { get; private set; }
        public static bool LoopRunning { get; private set; }
        public static bool BurstRunning => burstLeft > 0;
        public static int BurstDone => burstCount - burstLeft;
        public static bool playOnChange;
        public static int burstCount = 4;
        public static float burstGap = 0.5f;
        public static int gapMode;   // 0 Steady, 1 From start, 2 From end

        /// <summary>Raised whenever playing starts or stops (for buttons whose text follows it).</summary>
        public static event Action Changed;

        static double startedAt, waitUntil = -1;
        static int burstLeft;
        static ClipData clip;

        static double Now => EditorApplication.timeSinceStartup;

        /// <summary>Seconds into the current pass.</summary>
        public static float Elapsed => Playing ? (float)(Now - startedAt) : 0f;

        /// <summary>0..1 through the current pass (0 when stopped).</summary>
        public static float Progress => Playing && clip != null ? Mathf.Clamp01(Elapsed / clip.PlaySeconds) : 0f;

        static MockPlayer() { EditorApplication.update += Tick; }

        public static void Play(ClipData c) {
            clip = c;
            startedAt = Now; Playing = true; waitUntil = -1;
            Changed?.Invoke();
        }

        public static void StartLoop(ClipData c) { burstLeft = 0; LoopRunning = true; Play(c); }
        public static void StartBurst(ClipData c) { LoopRunning = false; burstLeft = burstCount; Play(c); }

        public static void Stop() {
            Playing = false; LoopRunning = false; burstLeft = 0; waitUntil = -1;
            Changed?.Invoke();
        }

        /// <summary>"Play on change": every finished edit restarts a play (the UI calls this after an edit).</summary>
        public static void Edited(ClipData c) { if (playOnChange && !LoopRunning && !BurstRunning) Play(c); }

        static void Tick() {
            if (waitUntil > 0 && Now >= waitUntil) { waitUntil = -1; Play(clip); return; }
            if (!Playing || clip == null) return;
            if (Elapsed < clip.PlaySeconds) return;
            if (clip.looper || LoopRunning) { startedAt = Now; return; }
            if (burstLeft > 1) {
                burstLeft--;
                Playing = false;
                waitUntil = Now + burstGap;
                Changed?.Invoke();
                return;
            }
            burstLeft = 0;
            Playing = false;
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Canned movement for everything that animates while "playing". Deterministic wobbles from sines and a cheap hash;
    /// purely decorative, chosen only to look alive.
    /// </summary>
    public static class MockSignal {

        static float Hash(int n) {
            unchecked {
                uint h = (uint)n * 2654435761u;
                h ^= h >> 15; h *= 0x2c1b3c6du; h ^= h >> 12;
                return (h & 0xFFFF) / 65535f;
            }
        }

        static int Seed(string key) { unchecked { int s = 17; foreach (char ch in key ?? "") s = s * 31 + ch; return s; } }

        /// <summary>A smooth 0..1 wobble for <paramref name="key"/> at time <paramref name="t"/>.</summary>
        public static float Wobble(string key, float t) {
            int s = Seed(key);
            float f1 = 0.6f + Hash(s) * 1.4f, f2 = 1.7f + Hash(s + 1) * 2.3f, ph = Hash(s + 2) * 6.283f;
            return 0.5f + 0.32f * Mathf.Sin(t * f1 * 6.283f + ph) + 0.12f * Mathf.Sin(t * f2 * 6.283f + ph * 1.7f);
        }

        /// <summary>A jumpy 0..1 value that changes every <paramref name="every"/> seconds.</summary>
        public static float Jitter(string key, float t, float every = 0.3f) => Hash(Seed(key) + Mathf.FloorToInt(t / every) * 7919);

        /// <summary>One fake "spectrum" bar in dB for band <paramref name="b"/> of <paramref name="n"/>: a gentle tilt, a few bumps, some motion.</summary>
        public static float BandDb(int b, int n, float t, bool moving) {
            float x = b / (float)(n - 1);
            float shape = 7f - 6f * x * x + 3f * Mathf.Sin(x * 9f) - (x > 0.85f ? (x - 0.85f) * 60f : 0f);
            float motion = moving ? 4f * Mathf.Sin(t * 3.1f + x * 11f) + 2f * (Hash(b + Mathf.FloorToInt(t * 12f) * 131) - 0.5f) : 0f;
            return Mathf.Clamp(shape + motion, -18f, 18f);
        }
    }
}
