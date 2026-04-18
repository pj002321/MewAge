using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스테이지 선택 캐러셀 카드 1장.
/// StageSelectPanel이 런타임에 생성하고 Bind()로 데이터를 채웁니다.
/// </summary>
public class StageCardView : MonoBehaviour
{
    public Image           iconImage;
    public Image           overlayImage; // 잠금 오버레이
    public RawImage        playerPortrait; // 플레이어 랜딩 애니메이션
    public GameObject      symbolObjects;
    public TextMeshProUGUI stageNameText;
    public TextMeshProUGUI bestWaveText;


    public void Bind(StageSelectPanel.StageEntry entry)
    {
        if (iconImage != null)
        {
            if (entry.icon != null)
            {
                iconImage.sprite = entry.icon;
                iconImage.color  = Color.white;
            }
            // entry.icon이 null이면 프리팹에 직접 할당된 스프라이트를 그대로 사용
            iconImage.enabled = iconImage.sprite != null;
        }
        if (overlayImage != null)
        {
            overlayImage.sprite = iconImage.sprite;
            overlayImage.gameObject.SetActive(entry.isLocked);
        }
        if (stageNameText != null) stageNameText.text = entry.stageName;
        if (bestWaveText  != null)
            bestWaveText.text = entry.bestWave > 0 ? $"최고 웨이브: {entry.bestWave}" : "최고 웨이브: -";
    }

    // 중앙 카드: 이름·웨이브 표시 / 비중앙 카드: 아이콘만 표시
    public void SetCenterCard(bool isCenter)
    {
        if (stageNameText  != null) stageNameText.gameObject.SetActive(isCenter);
        if (bestWaveText   != null) bestWaveText.gameObject.SetActive(isCenter);
        if (symbolObjects  != null) symbolObjects.SetActive(isCenter);
        if (playerPortrait != null) playerPortrait.gameObject.SetActive(isCenter);
    }

    // 플레이어 랜딩 효과 (UpgradePanel처럼)
    public void PlayLandingEffect()
    {
        if (playerPortrait.GetComponent<Animator>()!= null)
            playerPortrait.GetComponent<Animator>().Play("enter_portrait");
    }
}
