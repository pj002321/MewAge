using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 업그레이드 패널의 카드 한 장 UI.
/// Prefab 구성: Image(무기아이콘) · Image(등급아이콘) · TMP_Text(이름) · TMP_Text(가격) · Button
/// </summary>
public class UpgradeCard : MonoBehaviour
{
    [SerializeField] private Image           weaponIcon;
    [SerializeField] private Image           gradeIcon;
    [SerializeField] private TextMeshProUGUI weaponNameText;
    [SerializeField] private TextMeshProUGUI priceText;
    [SerializeField] private Button          buyButton;

    private WeaponData data;

    public void Setup(WeaponData weaponData)
    {
        data = weaponData;
        gameObject.SetActive(true);

        if (weaponIcon    && weaponData.icon)        weaponIcon.sprite    = weaponData.icon;
        if (gradeIcon     && weaponData.gradeSprite) gradeIcon.sprite     = weaponData.gradeSprite;
        if (weaponNameText) weaponNameText.text = weaponData.weaponName;
        if (priceText)      priceText.text      = $"{weaponData.price} G";

        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(OnClickBuy);
    }

    public void SetEmpty() => gameObject.SetActive(false);

    private void OnClickBuy() => UpgradeManager.Shared?.TryBuy(data);
}
