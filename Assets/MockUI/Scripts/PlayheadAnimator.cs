using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mock transport for the UI mock. There is NO audio engine here: pressing Play only
/// slides the playhead marker across the waveform area over <see cref="duration"/> seconds.
///
/// Behaviour:
///  - Play: starts moving the playhead from where it is (or from the start if it already
///    reached the end). Pressing Play while already playing does nothing.
///  - Stop: halts the movement and snaps the playhead back to the start.
///  - Reaching the end: if Loop is off, the playhead stops at the end. If Loop is on,
///    it wraps back to the start and keeps going until Stop is pressed.
///
/// The playhead must be a child of the waveform area ("track"). Its horizontal position is
/// driven through its anchors (0 = left edge, 1 = right edge), so it stays correct at any
/// screen size and whatever size the designer gives the waveform area.
/// </summary>
public class PlayheadAnimator : MonoBehaviour
{
    [Tooltip("The vertical marker that moves across the waveform. Must be a child of the waveform area.")]
    public RectTransform playhead;

    [Tooltip("Optional text that shows 'elapsed / total' time.")]
    public Text timeLabel;

    [Tooltip("How many seconds one pass of the playhead across the waveform takes.")]
    [Min(0.1f)] public float duration = 4f;

    [Tooltip("When on, the playhead wraps back to the start at the end instead of stopping.")]
    public bool loop;

    [Tooltip("Optional. If set, this AudioSource's clip is played when Play is pressed. Not required.")]
    public AudioSource optionalAudio;

    float _time;
    bool _playing;

    public bool IsPlaying => _playing;

    /// <summary>0..1 position of the playhead across the waveform.</summary>
    public float Normalized => Mathf.Clamp01(_time / duration);

    void Start() => Apply();

    public void Play()
    {
        if (_playing) return;
        if (_time >= duration) _time = 0f;
        _playing = true;
        if (optionalAudio != null && optionalAudio.clip != null) optionalAudio.Play();
    }

    public void Stop()
    {
        _playing = false;
        _time = 0f;
        if (optionalAudio != null) optionalAudio.Stop();
        Apply();
    }

    public void SetLoop(bool on) => loop = on;

    void Update()
    {
        if (!_playing) return;
        _time += Time.deltaTime;
        if (_time >= duration)
        {
            if (loop) _time %= duration;
            else { _time = duration; _playing = false; }
        }
        Apply();
    }

    void Apply()
    {
        if (playhead != null)
        {
            float x = Normalized;
            playhead.anchorMin = new Vector2(x, 0f);
            playhead.anchorMax = new Vector2(x, 1f);
            playhead.anchoredPosition = new Vector2(0f, playhead.anchoredPosition.y);
        }
        if (timeLabel != null)
            timeLabel.text = $"{Mathf.Min(_time, duration):0.00}s / {duration:0.00}s";
    }
}
