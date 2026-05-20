using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 골드상점·루비상점 공용 슬롯. 수량 텍스트 + 가격 텍스트 + 구매 버튼.
/// </summary>
public class CurrencyShopSlot : MonoBehaviour
{
    [SerializeField] Image           currencyIcon;
    [SerializeField] TextMeshProUGUI amountText;
    [SerializeField] TextMeshProUGUI priceText;
    [SerializeField] Button          buyButton;

    public void Setup(Sprite icon, int amount, int price, bool isCash, System.Action onBuy)
    {
        if (currencyIcon) { currencyIcon.sprite = icon; currencyIcon.SetNativeSize(); }
        if (amountText) amountText.text = amount.ToString();
        if (priceText)  priceText.text  = isCash ? $"₩ {price:#,0}" : price.ToString();
        buyButton?.onClick.AddListener(() => onBuy());
    }
}
