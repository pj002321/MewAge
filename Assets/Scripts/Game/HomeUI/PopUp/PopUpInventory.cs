using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보관함 팝업. 수령된 비-재화 아이템(열쇠·재료·조각 등)을 표시합니다.
/// 아이템은 Dispatch() 시점에 이미 지급 완료 — 여기서는 표시만 합니다.
/// Inspector 연결: slotContainer, slotPrefab
/// </summary>
public class PopUpInventory : MonoBehaviourSingleton<PopUpInventory>
{
    protected override bool DontDestroy => true;

    [SerializeField] private Transform  slotContainer;
    [SerializeField] private RewardSlot slotPrefab;

    private ScalablePopup _popup;
    private ScalablePopup Popup => _popup != null ? _popup : (_popup = GetComponent<ScalablePopup>());

    private readonly List<(RewardItem item, Sprite icon)> _received = new();

    // ── 공개 API ──────────────────────────────────────────────────────────

    public void Show()
    {
        RefreshSlots();
        Popup.Show();
    }

    public void Hide() => Popup.Hide();

    /// <summary>수령된 아이템을 목록에 추가. 같은 rewardId면 수량 합산.</summary>
    public void AddReward(RewardItem item, Sprite icon = null)
    {
        for (int i = 0; i < _received.Count; i++)
        {
            if (!string.IsNullOrEmpty(item.rewardId) && _received[i].item.rewardId == item.rewardId)
            {
                var old = _received[i];
                _received[i] = (new RewardItem(item.rewardId, old.item.amount + item.amount), old.icon != null ? old.icon : icon);
                return;
            }
        }
        _received.Add((item, icon));
    }

    public int ReceivedCount => _received.Count;

    // ── 내부 ──────────────────────────────────────────────────────────────

    private void RefreshSlots()
    {
        if (slotContainer == null || slotPrefab == null) return;

        foreach (Transform child in slotContainer)
            Destroy(child.gameObject);

        foreach (var (item, icon) in _received)
        {
            var resolvedIcon = icon ?? RewardVisualConfig.Shared?.GetIcon(item.rewardId);
            Instantiate(slotPrefab, slotContainer)
                .Setup(resolvedIcon, $"×{item.amount}", null, item.rewardId);
        }
    }
}
