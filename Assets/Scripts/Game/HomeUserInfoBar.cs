using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 홈 씬 상단 유저 정보 바.
/// Inspector에서 각 UI 요소를 연결하고 Refresh()로 갱신합니다.
/// 각 요소의 크기·위치는 Inspector의 RectTransform / LayoutElement에서 조절하세요.
/// </summary>
public class HomeUserInfoBar : MonoBehaviour
{
    [Header("프로필")]
    public Image            avatarImage;
    public TextMeshProUGUI  levelText;
    public TextMeshProUGUI  nameText;

    [Header("경험치")]
    public Slider           expSlider;
    public TextMeshProUGUI  expText;     // "120 / 200" 형식 (선택)

    [Header("재화 (0=골드 · 1=젬 · 2=스태미나)")]
    public Image[]          currencyIcons = new Image[3];
    public TextMeshProUGUI[] currencyTexts = new TextMeshProUGUI[3];

    void Start() => Refresh();

    /// <summary>표시 데이터를 갱신합니다. 실제 데이터 소스 연동 시 이 메서드를 수정하세요.</summary>
    public void Refresh()
    {
        if (levelText != null) levelText.text = "1";
        if (nameText  != null) nameText.text  = "용사";

        if (expSlider != null)
        {
            expSlider.minValue = 0f;
            expSlider.maxValue = 1f;
            expSlider.value    = 0f;
        }
        if (expText != null) expText.text = "0 / 100";

        for (int i = 0; i < currencyTexts.Length; i++)
            if (currencyTexts[i] != null) currencyTexts[i].text = "0";
    }
}
