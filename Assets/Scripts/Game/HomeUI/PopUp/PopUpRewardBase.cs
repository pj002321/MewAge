using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 수령 버튼 + RewardCollectEffect를 가진 보상 팝업의 공통 베이스.
/// 연출 재생 중에는 Hide()를 차단합니다.
/// 보상 지급 로직은 OnClaim()을 오버라이드하세요.
/// </summary>
public abstract class PopUpRewardBase : MonoBehaviour
{
    [SerializeField] protected RectTransform rewardContent;
    [SerializeField] protected Button        claimButton;

    ScalablePopup       _popup;
    RewardCollectEffect _effect;

    protected ScalablePopup      Popup  => _popup  != null ? _popup  : (_popup  = GetComponent<ScalablePopup>());
    protected RewardCollectEffect Effect => _effect != null ? _effect : (_effect = GetComponent<RewardCollectEffect>()
                                                                                ?? gameObject.AddComponent<RewardCollectEffect>());

    protected virtual void Awake()
    {
        _ = Effect; // 캐시 초기화
        claimButton?.onClick.AddListener(OnClaimInternal);
    }

    public void Show() => Popup.Show();

    public void Hide()
    {
        if (Effect.IsPlaying) return;
        Popup.Hide();
    }

    void OnClaimInternal()
    {
        if (claimButton != null) claimButton.interactable = false;
        Effect.Play(rewardContent, GetComponent<RectTransform>());
        OnClaim();
    }

    /// <summary>실제 보상 지급 로직을 여기에 구현하세요.</summary>
    protected virtual void OnClaim() { }
}
