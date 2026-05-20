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
            if (button.GetComponent<UIButtonSound>() == null)
                button.gameObject.AddComponent<UIButtonSound>();

            button.onClick.RemoveAllListeners();
            if (onClick != null) button.onClick.AddListener(() => onClick());
        }
    }
}
