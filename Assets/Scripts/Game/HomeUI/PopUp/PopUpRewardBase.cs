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
        _ = Effect;
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

    // ── 공통 유틸 ───────────────────────────────────────────────────────

    /// <summary>rewardContent 하위 슬롯을 모두 제거합니다.</summary>
    protected void ClearRewardContents()
    {
        if (rewardContent == null) return;
        foreach (Transform child in rewardContent)
            Destroy(child.gameObject);
    }

    /// <summary>PlayerPrefs에 수령 여부를 기록합니다.</summary>
    protected static void MarkClaimed(string key) =>
        PlayerPrefs.SetInt(key, 1);

    /// <summary>이미 수령했는지 확인합니다.</summary>
    protected static bool IsClaimed(string key) =>
        PlayerPrefs.GetInt(key, 0) == 1;

    private static string RewardTypeToId(RewardType type) => RewardVisualConfig.TypeToId(type);

    /// <summary>RewardSlot 프리팹으로 보상 슬롯을 생성합니다.</summary>
    protected void SpawnSlots(RewardItem[] items, RewardSlot slotPrefab)
    {
        if (slotPrefab == null || rewardContent == null) return;
        var cfg = RewardVisualConfig.Shared;
        foreach (var item in items)
        {
            var id   = !string.IsNullOrEmpty(item.rewardId) ? item.rewardId : RewardTypeToId(item.type);
            var icon = cfg?.GetIcon(id);
            Instantiate(slotPrefab, rewardContent).Setup(icon, item.amount.ToString(), null, id);
        }
    }
}
