using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 업그레이드 패널의 카드 한 장 UI.
/// 등급은 icon이 아닌 cardFrame 색상으로 구분합니다.
/// </summary>
public class UpgradeCard : MonoBehaviour
{
    [SerializeField] private Image           cardFrame;      // 등급 색상 적용 (배경/테두리)
    [SerializeField] private Image           weaponIcon;
    [SerializeField] private TextMeshProUGUI weaponNameText;
    [SerializeField] private TextMeshProUGUI priceText;
    [SerializeField] private Button          buyButton;

    private WeaponData data;

    public void Setup(WeaponData weaponData)
    {
        data = weaponData;
        gameObject.SetActive(true);

        if (cardFrame)      cardFrame.color     = WeaponGradeColors.Get(weaponData.grade);
        if (weaponIcon)
        {
            weaponIcon.sprite  = weaponData.icon;
            weaponIcon.enabled = weaponData.icon != null;
            weaponIcon.color   = Color.white; // 이전 카드에서 색상 변경됐을 경우 초기화
        }
        if (weaponNameText) weaponNameText.text = weaponData.weaponName;
        if (priceText)      priceText.text      = $"{weaponData.price} G";

        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(OnClickBuy);
    }

    public void SetEmpty() => gameObject.SetActive(false);

    private void OnClickBuy() => UpgradeManager.Shared?.TryBuy(data);
}
