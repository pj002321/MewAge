using Mewage.DB;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 챕터 상점 슬롯. 아이템 4개(T1~T4 / V1~V4) + 가격 + 잠금 상태를 표시합니다.
/// </summary>
public class ChapterShopSlot : MonoBehaviour
{
    [SerializeField] Image[]           itemIcons;    // 4개
    [SerializeField] TextMeshProUGUI[] amountTexts;  // 4개
    [SerializeField] TextMeshProUGUI   priceText;
    [SerializeField] Button            buyButton;
    [SerializeField] GameObject        lockedOverlay;

    public void Setup(DB_상점 row, bool locked, System.Action onBuy)
    {
        var cfg = RewardVisualConfig.Shared;
        var itemIds = new[] { row.f_T1, row.f_T2, row.f_T3, row.f_T4 };
        var amounts = new[] { row.f_V1, row.f_V2, row.f_V3, row.f_V4 };

        for (int i = 0; i < itemIcons.Length; i++)
        {
            bool valid = i < itemIds.Length && !string.IsNullOrEmpty(itemIds[i]);
            if (itemIcons[i])
            {
                itemIcons[i].gameObject.SetActive(valid);
                if (valid) { itemIcons[i].sprite = cfg?.GetIcon(itemIds[i]); itemIcons[i].SetNativeSize(); }
            }
            if (amountTexts[i])
            {
                amountTexts[i].gameObject.SetActive(valid);
                if (valid) amountTexts[i].text = amounts[i].ToString();
            }
        }

        if (priceText) priceText.text = row.f_C가격.ToString();

        if (lockedOverlay) lockedOverlay.SetActive(locked);
        if (buyButton)
        {
            buyButton.interactable = !locked;
            if (!locked) buyButton.onClick.AddListener(() => onBuy());
        }
    }
}
