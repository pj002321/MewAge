using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RewardSlot : MonoBehaviour
{
    [SerializeField] private Image           iconImg;
    [SerializeField] private TextMeshProUGUI amountText;
    [SerializeField] private Button          button;

    public void Setup(Sprite icon, string amount, Action onClick = null, string rewardId = "")
    {
        if (iconImg)    { iconImg.sprite = icon; iconImg.color = Color.white; iconImg.SetNativeSize(); }
        if (amountText) amountText.text = amount;

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => SoundManager.Shared?.Play(SoundKey.UI_Click));
            if (onClick != null) button.onClick.AddListener(() => onClick());
        }
    }

    public void SetDimmed(bool dimmed)
    {
        if (iconImg) iconImg.color = dimmed ? new Color(0.35f, 0.35f, 0.35f, 1f) : Color.white;
        if (button)  button.interactable = !dimmed;
    }
}
