using UnityEngine;
using UnityEngine.UI;

public class SettingButtonUI : MonoBehaviour
{
    public enum SettingType { BGM, SFX, Alarm, Vibrate }

    [Header("설정 타입")]
    public SettingType settingType;

    [Header("버튼 — ON 상태일 때 표시")]
    public Button onButton;

    [Header("버튼 — OFF 상태일 때 표시")]
    public Button offButton;

    static string PrefKey(SettingType t) => $"Setting_{t}";

    bool IsOn => PlayerPrefs.GetInt(PrefKey(settingType), 1) == 1;

    void Start()
    {
        onButton?.onClick.AddListener(() => SetState(false));
        offButton?.onClick.AddListener(() => SetState(true));
        Refresh(IsOn);
    }

    void SetState(bool on)
    {
        PlayerPrefs.SetInt(PrefKey(settingType), on ? 1 : 0);
        PlayerPrefs.Save();
        Apply(on);
        Refresh(on);
    }

    void Refresh(bool on)
    {
        if (onButton  != null) onButton.gameObject.SetActive(on);
        if (offButton != null) offButton.gameObject.SetActive(!on);
    }

    void Apply(bool on)
    {
        switch (settingType)
        {
            case SettingType.BGM:
                SoundManager.Shared?.SetBGMEnabled(on);
                break;
            case SettingType.SFX:
                SoundManager.Shared?.SetSFXEnabled(on);
                break;
            case SettingType.Alarm:
                // TODO: 알람 설정
                break;
            case SettingType.Vibrate:
                // TODO: 진동 설정
                break;
        }
    }
}
