using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows a slider's current value in a text label. Purely cosmetic, part of the UI mock:
/// the sliders in the effects panel are not connected to any audio processing.
/// </summary>
public class SliderValueLabel : MonoBehaviour
{
    public Slider slider;
    public Text label;
    [Tooltip("Number format, e.g. 0.00 or 0")]
    public string format = "0.00";

    void OnEnable()
    {
        if (slider != null) slider.onValueChanged.AddListener(Show);
        if (slider != null) Show(slider.value);
    }

    void OnDisable()
    {
        if (slider != null) slider.onValueChanged.RemoveListener(Show);
    }

    void Show(float v)
    {
        if (label != null) label.text = v.ToString(format);
    }
}
