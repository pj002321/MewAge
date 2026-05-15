using UnityEngine;
using UnityEngine.UI;

public class PopUpBreakthroughReward : MonoBehaviour
{
    [SerializeField] RectTransform rewardContent;  // 보상 아이템 목록의 부모
    [SerializeField] Button        claimButton;

    ScalablePopup        _popup;
    RewardCollectEffect  _effect;

    ScalablePopup Popup => _popup != null ? _popup : (_popup = GetComponent<ScalablePopup>());

    void Awake()
    {
        _effect = GetComponent<RewardCollectEffect>() ?? gameObject.AddComponent<RewardCollectEffect>();
        claimButton?.onClick.AddListener(OnClaim);
    }

    public void Show() => Popup.Show();
    public void Hide() => Popup.Hide();

    void OnClaim()
    {
        if (claimButton != null) claimButton.interactable = false;
        _effect.Play(rewardContent, GetComponent<RectTransform>());
        // TODO: 실제 보상 지급
    }
}
