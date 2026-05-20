using System;
using System.Globalization;
using TMPro;
using UnityEngine;

/// <summary>
/// 자동사냥 오프라인 보상 팝업.
/// 마지막 수령 시각부터 경과한 시간만큼 골드·조각을 지급합니다.
///
/// Inspector 연결:
///   slotPrefab      — RewardSlot 프리팹
///   elapsedTimeText — "3시간 24분 동안 사냥했습니다" (선택)
/// </summary>
public class PopUpAutoHunt : PopUpRewardBase
{
    [SerializeField] private RewardSlot       slotPrefab;
    [SerializeField] private TextMeshProUGUI  elapsedTimeText;

    private const string K_LastTime   = "AutoHunt_LastTime";
    private const float  GoldPerHour  = 1000f;  // 시간당 골드
    private const float  FragPerHour  = 0.5f;   // 시간당 장비 조각 (소수 누적)
    private const float  MaxHours     = 8f;      // 최대 누적 시간

    private RewardItem[] _pendingRewards;

    protected override void Awake()
    {
        base.Awake();
    }

    public new void Show()
    {
        _pendingRewards = CalculateRewards();
        RefreshUI();
        base.Show();
        StartCoroutine(TickTimer());
    }

    private System.Collections.IEnumerator TickTimer()
    {
        while (gameObject.activeInHierarchy)
        {
            if (elapsedTimeText != null)
            {
                float h       = Mathf.Clamp(GetElapsedHours(), 0f, MaxHours);
                int   hours   = (int)h;
                int   minutes = (int)((h - hours) * 60f);
                elapsedTimeText.text = $"{hours:D2}:{minutes:D2} 째 탐색 중 . . .";
            }
            yield return new WaitForSecondsRealtime(1f);
        }
    }

    private RewardItem[] CalculateRewards()
    {
        float hours = Mathf.Clamp(GetElapsedHours(), 0f, MaxHours);

        int gold = Mathf.RoundToInt(GoldPerHour * hours);
        int frag = Mathf.FloorToInt(FragPerHour * hours);

        // 경과 시간이 없으면 임시 더미 보상
        if (gold == 0 && frag == 0)
        {
            return new[]
            {
                new RewardItem("COIN",       500),
                new RewardItem("MAT_HERO",     1),
            };
        }

        var list = new System.Collections.Generic.List<RewardItem>();
        if (gold > 0) list.Add(new RewardItem("COIN",     gold));
        if (frag > 0) list.Add(new RewardItem("MAT_HERO", frag));
        return list.ToArray();
    }

    private void RefreshUI()
    {
        ClearRewardContents();
        SpawnSlots(_pendingRewards, slotPrefab);

    }

    protected override void OnClaim()
    {
        if (_pendingRewards != null)
            RewardManager.Shared?.GiveRewards(_pendingRewards);

        SaveNow();
        ClearRewardContents();
        _pendingRewards = null;
    }

    // ── 시간 계산 ────────────────────────────────────────────────────────

    private static float GetElapsedHours()
    {
        string saved = PlayerPrefs.GetString(K_LastTime, "");
        if (string.IsNullOrEmpty(saved)) return 0f;
        if (!DateTime.TryParse(saved, null, DateTimeStyles.RoundtripKind, out var last)) return 0f;
        return (float)(DateTime.UtcNow - last).TotalHours;
    }

    private static void SaveNow()
    {
        PlayerPrefs.SetString(K_LastTime, DateTime.UtcNow.ToString("O"));
        PlayerPrefs.Save();
    }
}
