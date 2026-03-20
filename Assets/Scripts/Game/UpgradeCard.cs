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
    [SerializeField] private TextMeshProUGUI starText;
    [SerializeField] private Button          buyButton;
    [SerializeField] private Button          infoButton;

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
            weaponIcon.color   = Color.white;
        }
        if (weaponNameText) weaponNameText.text = weaponData.weaponName;
        if (priceText)      priceText.text      = $"{weaponData.price}";
        if (starText)       starText.text       = new string('★', weaponData.star);

        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(OnClickBuy);

        infoButton?.onClick.RemoveAllListeners();
        infoButton?.onClick.AddListener(OnClickInfo);
    }

    public void SetEmpty() => gameObject.SetActive(false);

    private void OnClickBuy()  => UpgradeManager.Shared?.TryBuy(data);
    private void OnClickInfo() => WeaponInfoPanel.Shared?.Show(data);
}
