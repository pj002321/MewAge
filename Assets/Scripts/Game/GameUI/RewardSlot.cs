using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 보상 슬롯 프리팹에 붙이는 컴포넌트.
/// Inspector 연결: iconImg, amountText
/// </summary>
public class RewardSlot : MonoBehaviour
{
    [SerializeField] private Image           iconImg;
    [SerializeField] private TextMeshProUGUI amountText;

    public void Setup(Sprite icon, string amount)
    {
        if (iconImg)    iconImg.sprite = icon;
        if (amountText) amountText.text = amount;
    }
}
