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
    public TextMeshProUGUI stageNameText;
    public TextMeshProUGUI bestWaveText;

    public void Bind(StageSelectPanel.StageEntry entry)
    {
        if (iconImage != null)
        {
            iconImage.enabled = entry.icon != null;
            if (entry.icon != null)
            {
                iconImage.sprite = entry.icon;
                iconImage.color  = Color.white;
            }
        }
        if (overlayImage != null)
        {
            overlayImage.gameObject.SetActive(entry.isLocked);
        }
        if (stageNameText != null) stageNameText.text = entry.stageName;
        if (bestWaveText  != null)
            bestWaveText.text = entry.bestWave > 0 ? $"최고 웨이브: {entry.bestWave}" : "최고 웨이브: -";
    }
}
