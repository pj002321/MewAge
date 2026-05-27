using UnityEngine;
using UnityEngine.UI;

public class VolumeSliderUI : MonoBehaviour
{
    public enum VolumeType { BGM, SFX }

    [SerializeField] private VolumeType volumeType;
    [SerializeField] private Slider     slider;

    void Start()
    {
        if (slider == null) return;

        float saved = volumeType == VolumeType.BGM
            ? (SoundManager.Shared?.BGMVolume ?? 1f)
            : (SoundManager.Shared?.SFXVolume ?? 1f);

        slider.SetValueWithoutNotify(saved);
        slider.onValueChanged.AddListener(OnValueChanged);
    }

    void OnValueChanged(float value)
    {
        if (volumeType == VolumeType.BGM)
            SoundManager.Shared?.SetBGMVolume(value);
        else
            SoundManager.Shared?.SetSFXVolume(value);
    }
}
