using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 업그레이드 패널의 카드 한 장 UI.
/// 등급은 cardFrame 스프라이트(9-slice)로 구분합니다.
/// </summary>
public class UpgradeCard : MonoBehaviour
{
    [SerializeField] private Image            frameImg;
    [SerializeField] private Image            weaponIcon;
    [SerializeField] private Image            weaponTypeImg;
    [SerializeField] private TextMeshProUGUI  weaponNameText;
    [SerializeField] private TextMeshProUGUI  priceText;
    [SerializeField] private Button           buyButton;
    [SerializeField] private Button           adButton;
    [SerializeField] private Button           infoButton;
    [SerializeField] private List<GameObject> starObjects; // 0=1성, 1=2성, 2=3성, 3=4성
    [SerializeField] private Image soldOverlayImg;
    private WeaponData data;
    private int        cardIndex;
    public WeaponData Data => data;

    private void Awake()
    {
        if (soldOverlayImg != null)
        {
            soldOverlayImg.gameObject.SetActive(false);
            soldOverlayImg.raycastTarget = false; // 숨긴 상태에선 입력 통과
        }
    }

    public void Setup(WeaponData weaponData, int index, bool isSold = false)
    {
        data = weaponData;
        cardIndex = index;
        gameObject.SetActive(true);

        if (frameImg)
        {
            var gradeSprite = WeaponVisualConfig.Shared?.GetGradeSprite(weaponData.grade);
            if (gradeSprite != null)
            {
                frameImg.sprite = gradeSprite;
                frameImg.type   = Image.Type.Simple;
                frameImg.color  = Color.white;
            }
        }

        if (weaponIcon)
        {
            weaponIcon.sprite  = weaponData.icon;
            weaponIcon.enabled = weaponData.icon != null;
            weaponIcon.color   = Color.white;
        }
        ApplyWeaponOverlays(weaponData);
        if (weaponNameText) weaponNameText.text = weaponData.weaponName;
        if (priceText)      priceText.text      = $"{weaponData.price}";
        SetStarObject(starObjects, weaponData.star);

        // 판매 여부에 따른 설정
        if (isSold)
        {
            SoldOut(false); // 이미 판매된 상태면 흔들림 없이 오버레이만 활성화
        }
        else
        {
            if (soldOverlayImg != null)
            {
                soldOverlayImg.gameObject.SetActive(false);
                soldOverlayImg.raycastTarget = false;
            }
            buyButton.gameObject.SetActive(true);
            buyButton.interactable = true;
            if (adButton) adButton.interactable = true;
            if (infoButton != null)
            {
                infoButton.onClick.RemoveAllListeners();
                infoButton.onClick.AddListener(OnClickInfo);
            }
        }

        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(OnClickBuy);

        // 기본적으로 광고 버튼은 끔 (SetupAdCard에서 필요 시 켬)
        adButton?.gameObject.SetActive(false);
    }

    public void SetupAdCard(WeaponData weaponData, int index, System.Action onAdWatch, bool isSold = false)
    {
        Setup(weaponData, index, isSold);
        buyButton.gameObject.SetActive(false);
        if (priceText) priceText.text = "";
        
        if (adButton)
        {
            adButton.gameObject.SetActive(!isSold);
            adButton.interactable = !isSold; // 판매 상태에 따라 클릭 가능 여부 설정
            adButton.onClick.RemoveAllListeners();
            adButton.onClick.AddListener(() => { onAdWatch?.Invoke(); });
        }
    }

    public void SetEmpty() => gameObject.SetActive(false);

    public void SoldOut(bool shake = true)
    {
        if (soldOverlayImg != null)
        {
            soldOverlayImg.gameObject.SetActive(true);
            soldOverlayImg.raycastTarget = true; // 오버레이가 하위 버튼 클릭을 차단
            if (shake && gameObject.activeInHierarchy)
            {
                StopAllCoroutines();
                StartCoroutine(ShakeRoutine());
            }
        }
        buyButton.interactable = false;
        if (adButton) adButton.interactable = false;

        // 판매 후 info 버튼은 구매 콜백 없이 정보만 표시
        if (infoButton != null)
        {
            infoButton.onClick.RemoveAllListeners();
            infoButton.onClick.AddListener(OnClickInfoOnly);
        }
    }

    private void OnClickBuy()      => UpgradeManager.Shared?.TryBuy(data, cardIndex);
    private void OnClickInfo()     => WeaponInfoPanel.Shared?.Show(data,
        onBuy: () => UpgradeManager.Shared?.TryBuy(data, cardIndex));
    private void OnClickInfoOnly() => WeaponInfoPanel.Shared?.Show(data);

    private static void SetStarObject(List<GameObject> objs, int star)
    {
        if (objs == null) return;
        for (int i = 0; i < objs.Count; i++)
            if (objs[i] != null) objs[i].SetActive(i == star - 1 && star > 0);
    }

    private void ApplyWeaponOverlays(WeaponData data)
    {
        var cfg = WeaponVisualConfig.Shared;

        if (weaponTypeImg)
        {
            var sprite = cfg?.GetTypeSprite(data.weaponType);
            weaponTypeImg.sprite  = sprite;
            weaponTypeImg.enabled = sprite != null;
            if (sprite != null) weaponTypeImg.color = WeaponGradeColors.Get(data.grade);
        }
    }

    private IEnumerator ShakeRoutine()
    {
        if (soldOverlayImg == null) yield break;

        var rt = soldOverlayImg.rectTransform;
        var originalPos = rt.anchoredPosition;
        float duration = 0.4f;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float strength = (1f - (elapsed / duration)) * 8f; // 시간이 지날수록 약해지는 떨림
            float shakeX = Random.Range(-1f, 1f) * strength;
            float shakeY = Random.Range(-1f, 1f) * strength;
            rt.anchoredPosition = originalPos + new Vector2(shakeX, shakeY);
            yield return null;
        }
        rt.anchoredPosition = originalPos;
    }
}