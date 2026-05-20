using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 홈 씬 상단 유저 정보 바.
/// RewardManager.OnChanged 이벤트에 구독해 재화를 자동 갱신합니다.
/// currencyTexts[0]=골드, [1]=루비, [2]=스태미나
/// </summary>
public class HomeUserInfoBar : MonoBehaviour
{
    [Header("UI")]
    public Button avatarProfileButton;
    public Button lubyChargeButton;
    public Button goldChargeButton;
    public PopUpUserProfile popup_userProfile;

    [Header("상점 연동")]
    public HomeUIManager  homeUIManager;
    public int            shopTabIndex = 0;
    public HomeShopPanel  shopPanel;

    [Header("프로필")]
    public Image            avatarImage;
    public TextMeshProUGUI  levelText;
    public TextMeshProUGUI  nameText;

    [Header("경험치")]
    public Slider           expSlider;
    public TextMeshProUGUI  expText;

    [Header("재화 (0=골드 · 1=루비 · 2=스태미나)")]
    public Image[]           currencyIcons = new Image[3];
    public TextMeshProUGUI[] currencyTexts = new TextMeshProUGUI[3];

    [Header("플라이 이펙트")]
    public CurrencyFlyEffect flyEffect;
    public RectTransform     expIconRT;  // 경험치 목표 지점

    private void Awake()
    {
        avatarProfileButton?.onClick.AddListener(() => popup_userProfile?.Show());
        lubyChargeButton?.onClick.AddListener(() => GoToShop(gem: true));
        goldChargeButton?.onClick.AddListener(() => GoToShop(gem: false));
    }

    private void OnEnable()
    {
        RewardManager.OnChanged        += Refresh;
        RewardManager.OnCurrencyFlying += OnCurrencyFlying;
        Refresh();
    }

    private void OnDisable()
    {
        RewardManager.OnChanged        -= Refresh;
        RewardManager.OnCurrencyFlying -= OnCurrencyFlying;
    }

    private void OnCurrencyFlying(RewardType type, int amount, Vector2 screenFrom)
    {
        if (flyEffect == null) return;

        // screenFrom이 지정 안 됐으면 화면 하단 중앙 기본값
        if (screenFrom == default)
            screenFrom = new Vector2(Screen.width * 0.5f, Screen.height * 0.3f);

        RectTransform target = null;
        Sprite        icon   = null;

        switch (type)
        {
            case RewardType.Gold:
                target = currencyIcons.Length > 0 && currencyIcons[0] != null
                    ? currencyIcons[0].rectTransform : null;
                icon = currencyIcons.Length > 0 && currencyIcons[0] != null
                    ? currencyIcons[0].sprite : null;
                break;
            case RewardType.Ruby:
                target = currencyIcons.Length > 1 && currencyIcons[1] != null
                    ? currencyIcons[1].rectTransform : null;
                icon = currencyIcons.Length > 1 && currencyIcons[1] != null
                    ? currencyIcons[1].sprite : null;
                break;
            case RewardType.Exp:
                target = expIconRT != null ? expIconRT
                    : expSlider != null ? expSlider.GetComponent<RectTransform>() : null;
                icon = currencyIcons.Length > 0 && currencyIcons[0] != null
                    ? currencyIcons[0].sprite : null; // 임시 — 경험치 아이콘 있으면 교체
                break;
        }

        if (target == null || icon == null) return;

        int count = amount >= 1000 ? 8 : amount >= 100 ? 6 : 4;
        flyEffect.Play(icon, screenFrom, target, count);
    }

    public void Refresh()
    {
        var rm = RewardManager.Shared;

        // 레벨
        if (levelText != null) levelText.text = rm != null ? rm.Level.ToString() : "1";
        if (nameText  != null) nameText.text  = "용사";

        // 경험치 슬라이더
        if (expSlider != null)
        {
            int needed = rm != null ? rm.ExpToNext(rm.Level) : 100;
            int cur    = rm?.Exp ?? 0;
            expSlider.minValue = 0f;
            expSlider.maxValue = needed;
            expSlider.value    = cur;
            if (expText != null) expText.text = $"{cur} / {needed}";
        }

        // 골드
        if (currencyTexts.Length > 0 && currencyTexts[0] != null)
            currencyTexts[0].text = rm != null ? FormatCurrency(rm.Gold) : "0";

        // 루비
        if (currencyTexts.Length > 1 && currencyTexts[1] != null)
            currencyTexts[1].text = rm != null ? rm.Ruby.ToString() : "0";

        // 스태미나 (RewardManager 미지원 — 추후 연동)
        if (currencyTexts.Length > 2 && currencyTexts[2] != null)
            currencyTexts[2].text = "0";
    }

    private static string FormatCurrency(int amount)
    {
        if (amount >= 1_000_000) return (amount / 1_000_000f).ToString("0.#") + "M";
        if (amount >= 1_000)     return (amount / 1_000f).ToString("0.#") + "K";
        return amount.ToString();
    }

    private void GoToShop(bool gem)
    {
        homeUIManager?.ShowTab(shopTabIndex);
        if (gem) shopPanel?.ScrollToGem();
        else      shopPanel?.ScrollToGold();
    }
}
