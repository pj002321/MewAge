using System.Collections.Generic;
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
    [SerializeField] private Image           starImage;
    [SerializeField] private Button          buyButton;
    [SerializeField] private Button          adButton;
    [SerializeField] private Button          infoButton;
    [SerializeField] private List<GameObject> starObjects; // 0=1성, 1=2성, 2=3성, 3=4성

    private WeaponData data;

    public void Setup(WeaponData weaponData)
    {
        data = weaponData;
        gameObject.SetActive(true);

        if (cardFrame)
        {
            cardFrame.sprite = Resources.Load<Sprite>("Weapons/grade/" + weaponData.grade.ToString().ToLower());
            cardFrame.color  = Color.white;
        }
        if (weaponIcon)
        {
            weaponIcon.sprite  = weaponData.icon;
            weaponIcon.enabled = weaponData.icon != null;
            weaponIcon.color   = Color.white;
        }
        if (weaponNameText) weaponNameText.text = weaponData.weaponName;
        if (priceText)      priceText.text      = $"{weaponData.price}";
        SetStarObject(starObjects, weaponData.star);

        buyButton.gameObject.SetActive(true);
        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(OnClickBuy);

        adButton?.gameObject.SetActive(false);

        infoButton?.onClick.RemoveAllListeners();
        infoButton?.onClick.AddListener(OnClickInfo);
    }

    public void SetupAdCard(WeaponData weaponData, System.Action onAdWatch)
    {
        Setup(weaponData);
        buyButton.gameObject.SetActive(false);
        if (priceText) priceText.text = "";
        adButton?.gameObject.SetActive(true);
        adButton?.onClick.RemoveAllListeners();
        adButton?.onClick.AddListener(() => onAdWatch?.Invoke());
    }

    public void SetEmpty() => gameObject.SetActive(false);

    private void OnClickBuy()  => UpgradeManager.Shared?.TryBuy(data);
    private void OnClickInfo() => WeaponInfoPanel.Shared?.Show(data,
        onBuy: () => UpgradeManager.Shared?.TryBuy(data));

    private static void SetStarObject(List<GameObject> objs, int star)
    {
        if (objs == null) return;
        for (int i = 0; i < objs.Count; i++)
            if (objs[i] != null) objs[i].SetActive(i == star - 1 && star > 0);
    }
}
