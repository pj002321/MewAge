using TMPro;
using UnityEngine;

/// <summary>
/// TextMeshProUGUI에 붙이면 UIDataBus.OnCurrencyChanged 구독으로 자동 갱신됩니다.
/// Gold, Ruby 전역 재화 전용. EquipMat처럼 컨텍스트에 따라 달라지는 수치는 각 UI가 직접 처리합니다.
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public class CurrencyText : MonoBehaviour
{
    public enum Source { Gold, Ruby }
    public Source source = Source.Gold;

    TextMeshProUGUI _text;

    void Awake() => _text = GetComponent<TextMeshProUGUI>();

    void OnEnable()
    {
        UIDataBus.OnCurrencyChanged += Refresh;
        Refresh();
    }

    void OnDisable() => UIDataBus.OnCurrencyChanged -= Refresh;

    public void Refresh()
    {
        if (_text == null) return;
        var rm = RewardManager.Shared;
        _text.text = source == Source.Gold
            ? FormatNumber(rm?.Gold ?? 0)
            : (rm?.Ruby ?? 0).ToString();
    }

    static string FormatNumber(int v)
    {
        if (v >= 1_000_000) return (v / 1_000_000f).ToString("0.#") + "M";
        if (v >= 1_000)     return (v / 1_000f).ToString("0.#") + "K";
        return v.ToString();
    }
}
