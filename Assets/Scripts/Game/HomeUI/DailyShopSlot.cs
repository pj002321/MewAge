using Mewage.DB;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 일일 상점 슬롯.
/// Inspector 연결: itemIcon, itemNameText, amountText, currencyIcon, priceText, limitText, buyButton
/// </summary>
public class DailyShopSlot : MonoBehaviour
{
    [SerializeField] Image           itemIcon;
    [SerializeField] TextMeshProUGUI itemNameText;
    [SerializeField] TextMeshProUGUI amountText;
    [SerializeField] Image           currencyIcon;
    [SerializeField] TextMeshProUGUI priceText;
    [SerializeField] TextMeshProUGUI limitText;
    [SerializeField] Button          buyButton;

    private string _prefKey;
    private int    _limit;

    public void Setup(DB_상점 row, string prefKey, System.Action<DB_상점, DailyShopSlot> onBuy)
    {
        _prefKey = prefKey;
        _limit   = row.f_한도;

        var cfg = RewardVisualConfig.Shared;

        // 아이템 아이콘
        if (itemIcon)
        {
            itemIcon.sprite = cfg?.GetIcon(row.f_D보상);
            itemIcon.SetNativeSize();
        }

        // 아이템 이름 — 보상ID 행의 f_보상이름 참조
        if (itemNameText)
        {
            string name = DB_상점.FindEntity(e => e.f_보상ID == row.f_D보상)?.f_보상이름 ?? row.f_D보상;
            itemNameText.text = name;
        }

        if (amountText) amountText.text = "×1";

        // 결제 재화
        bool useGold = row.f_D골드 > 0;
        int  price   = useGold ? row.f_D골드 : row.f_D루비;

        if (currencyIcon) currencyIcon.sprite = cfg?.GetIcon(useGold ? RewardType.Gold : RewardType.Ruby);
        if (priceText)    priceText.text      = price.ToString();

        // 버튼
        if (buyButton)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(() => onBuy(row, this));
        }

        RefreshLimit();
    }

    public void RefreshLimit()
    {
        int bought = PlayerPrefs.GetInt(_prefKey, 0);
        int remain = _limit > 0 ? _limit - bought : -1;

        if (limitText)
            limitText.text = remain < 0 ? "" : remain == 0 ? "품절" : $"{remain}회 구매 가능";

        bool soldOut = _limit > 0 && bought >= _limit;
        if (buyButton)  buyButton.interactable  = !soldOut;
        if (itemIcon)   itemIcon.color = soldOut ? new Color(0.35f, 0.35f, 0.35f, 1f) : Color.white;
    }
}
